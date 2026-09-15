# RESEARCH-0035: Generator-Wide Failure Isolation for Test-Double Generation (Issue #143)

## 1. Issue and context

[Issue #143](https://github.com/LayeredCraft/compono/issues/143): "One test
double's failed generation discards all unrelated generated output for the
compilation." Discovered as a side effect of RESEARCH-0034 (issue #142):
before #142's fix, a large interface's Scriban render crashed with an
unhandled `ScriptRuntimeException`, and Roslyn reported a single
generator-wide `CS8785` while **every** generated output for that
compilation - including healthy, unrelated `Composer.Create<T>()` plans and
other test doubles that had nothing wrong with them - silently disappeared.

#142 removed one concrete trigger (the two inherited Scriban limits). #143
is the broader defect: nothing about the pipeline's structure changed, so
any *other* future exception during test-double emission (a real
generator bug, an unanticipated shape, a future template change) will
reproduce the exact same cascade. This is not about Scriban limits, AWS
SDK interfaces, or large interfaces at all - it's about what happens when
one interface's generation throws, full stop.

## 2. Minimal, Scriban-independent reproduction (empirical)

Built a Compono-independent probe (`IIncrementalGenerator` implementations
registered directly against a `CSharpCompilation`, using
`Microsoft.CodeAnalysis.CSharp.Testing`-free raw driver APIs - no Scriban,
no Compono discovery/emitter code at all) to isolate **Roslyn's own**
exception-handling semantics from anything Compono-specific. Temporary
file `test/Compono.Generators.Tests/ZZZGeneratorIsolationExperiment.cs`,
removed after the investigation (confirmed via `git status --short`,
clean).

### Scenario A - one `RegisterSourceOutput`, two values, one throws

```csharp
var values = context.CompilationProvider.SelectMany(static (_, _) => new[] { 1, 2 });
context.RegisterSourceOutput(values, (spc, value) =>
{
    if (value == 1) throw new InvalidOperationException("Simulated per-item failure for value 1");
    spc.AddSource($"Value{value}.g.cs", $"public class Generated{value} {{}}");
});
```

Result: `DIAGS=[CS8785] TREES=0`. Value 2's `AddSource` never survives -
**even though its own callback invocation completed successfully and threw
nothing.** This is Compono's discovered-test-doubles registration's exact
shape today (`ComponoIncrementalGenerator.cs:442`, one callback invocation
per `DiscoveredTestDoubleInfo`).

### Scenario C - two separate `RegisterSourceOutput` registrations on the same generator, only one throws

```csharp
context.RegisterSourceOutput(failingValues, (_, _) => throw new InvalidOperationException(...));
context.RegisterSourceOutput(healthyValues, (spc, value) => spc.AddSource(...));
```

Result: `DIAGS=[CS8785] TREES=0`. The **completely unrelated, separate**
registration's healthy output is *also* discarded. This means the blast
radius is not "the registration that threw" - it's **the whole generator
instance's entire run**. `ComponoIncrementalGenerator` has eight
`RegisterSourceOutput` calls (collections, test doubles, composition
plans, row-invoker types, logging activation/status, AOT row
registrations). An unhandled exception in *any one* of them today would
discard output from *all* of them, not just test doubles - this is wider
than issue #143's literal title suggests, and worth flagging explicitly
(see §12/§16).

### Scenario D - the callback catches its own per-item exception, reports a diagnostic, and returns without `AddSource`

```csharp
context.RegisterSourceOutput(values, (spc, value) =>
{
    try
    {
        if (value == 1) throw new InvalidOperationException("Simulated per-item failure for value 1");
        spc.AddSource($"Value{value}.g.cs", $"public class Generated{value} {{}}");
    }
    catch (Exception ex)
    {
        spc.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
            "PROBE001", "Simulated failure", $"Failed: {ex.Message}", "Probe", DiagnosticSeverity.Error, true),
            Location.None));
    }
});
```

Result: `DIAGS=[error PROBE001: Failed: Simulated per-item failure for value 1] TREES=1`,
`TREE_NAMES=[.../Value2.g.cs]`. No `CS8785`. The healthy sibling's source
survives, and the failing item gets a real, attributable Error diagnostic
instead of a generic Roslyn failure. **This is the entire fix**: nothing
Roslyn-side needs to change - the callback must never let an exception
escape it.

Scenario B (multiple logical test doubles emitted from *one* callback
invocation) doesn't apply to Compono's actual shape - `discoveredTestDoubles`
is an `IncrementalValuesProvider<DiscoveredTestDoubleInfo>`, and Roslyn
already invokes the `RegisterSourceOutput` callback once per value (this is
exactly Scenario D's shape), not once for a batch. No separate experiment
needed; the pipeline trace in §3 confirms this directly from source.

## 3. Generator pipeline trace (`ComponoIncrementalGenerator.cs`)

- `discoveredTestDoubles`: an `IncrementalValuesProvider<DiscoveredTestDoubleInfo>`
  built by merging every discovery path's `TestDoubles` array, deduping by
  `InterfaceFullyQualifiedName` (lines 385-440). **Each interface is
  already its own independent pipeline value** by the time it reaches
  emission - this is the natural per-interface isolation boundary; no
  restructuring is needed to get one, it already exists.
- `context.RegisterSourceOutput(discoveredTestDoubles, static (productionContext, testDouble) => {...})`
  (line 442): invoked once per `DiscoveredTestDoubleInfo`. Reports
  `testDouble.Diagnostics` (analysis-time failures - already correctly
  isolated, since a failing interface's `Diagnostics.Count > 0` short-circuits
  before any emission call, line 447-448), then `testDouble.InfoDiagnostics`
  (non-blocking per-overload notes), then calls
  `TestDoubleEmitter.Generate(productionContext, testDouble)` (line 456) -
  **unguarded**. This is where an unanticipated exception (a real bug in
  `TestDoubleEmitter.Generate` or in `TemplateHelper.Render`, called from
  inside it) currently escapes.
- `TestDoubleEmitter.Generate` (`Emitters/TestDoubleEmitter.cs:13-235`):
  builds an anonymous projection model from `testDouble.Members`, calls
  `TemplateHelper.Render("TestDouble.scriban", model)` (line 232), then
  `context.AddSource(...)` (line 234).
- `TemplateHelper.Render` (`Emitters/TemplateHelper.cs:48-67`): loads (and
  caches) the parsed `Template` via `LoadTemplate`, builds a
  `Scriban.TemplateContext`, calls `template.Render(context)`.
  `LoadTemplate` (lines 69-92) can itself throw `InvalidOperationException`
  if the embedded resource is missing or fails to parse - this is the one
  place in the whole call chain that is **not** interface-specific: the
  same cached `Template` (or the same failure re-attempted, since a failed
  `GetOrAdd` doesn't cache an exception) is used for every interface. See
  §6 for why this still doesn't need special-casing.
- `AddSource` itself: only reached after `Render` returns successfully; not
  a realistic failure point on its own (a duplicate-hint-name crash there
  would be a pre-existing, unrelated dedup bug - out of scope here, and
  `InterfaceFullyQualifiedName`-based dedup at line 407 already prevents
  it for legitimate discovery).

Every other `RegisterSourceOutput` registration in the file
(`discoveredCollections`, `discoveredTypes`, `rowInvokerTypes`,
`loggingRuntimeSymbolsStatus`, `discoveredLoggingCategories`,
`aotComposeMethodsAll`) follows the identical shape: report diagnostics,
early-return if any are blocking, call one `*Emitter.Generate` method,
unguarded. None of them currently isolate an unexpected emission-time
exception either - see §12.

## 4. Roslyn isolation experiments - summary

| Scenario | Setup | Result |
|---|---|---|
| A | One registration, values `[1,2]`, value 1 throws unhandled | `CS8785`, 0 trees (value 2 lost) |
| C | Two registrations, one throws unhandled, other is unrelated/healthy | `CS8785`, 0 trees (healthy registration's output *also* lost) |
| D | One registration, values `[1,2]`, value 1's callback catches its own exception + reports a diagnostic + skips `AddSource` | No `CS8785`; value 2's tree present; diagnostic present |

Conclusion: **Roslyn provides no built-in per-value isolation** for an
exception escaping a `RegisterSourceOutput` callback - it treats any such
exception as fatal to the entire generator's run, not just the failing
value or the failing registration. The only isolation mechanism available
is to never let an exception escape the callback in the first place -
which Compono's own existing diagnostic-based failure paths (CMP0001,
CMP0010, ADR-0043's own `Diagnostics.Count > 0` early-return) already do
correctly for every *anticipated* failure. The gap is specifically
**unanticipated** exceptions during emission.

## 5. Recommended failure boundary

Wrap the emission call (and nothing upstream of it) per interface, inside
the existing `discoveredTestDoubles` `RegisterSourceOutput` callback:

```csharp
context.RegisterSourceOutput(discoveredTestDoubles, static (productionContext, testDouble) =>
{
    foreach (var diagnostic in testDouble.Diagnostics)
        diagnostic.Report(productionContext);

    if (testDouble.Diagnostics.Count > 0)
        return;

    foreach (var infoDiagnostic in testDouble.InfoDiagnostics)
        infoDiagnostic.Report(productionContext);

    try
    {
        TestDoubleEmitter.Generate(productionContext, testDouble);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        productionContext.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.TestDoubleGenerationFailed,
            Location.None,
            testDouble.InterfaceFullyQualifiedName,
            ex.GetType().Name,
            ex.Message));
    }
});
```

This is deliberately **not** "wrap `Emit(interface)` in try/catch" applied
blindly - it's the narrowest point that is already, structurally, one
interface's entire remaining emission work (model projection, template
render, `AddSource`) with nothing else sharing the frame. Scenario D
proves this boundary works: catching here, reporting a diagnostic, and
not calling `AddSource` for the failed interface is sufficient for Roslyn
to keep every other interface's `AddSource` output intact - **no pipeline
restructuring, no new incremental value shape, no change to how
`discoveredTestDoubles` is built** is needed. The per-interface
independence this isolation boundary relies on already exists (§3); the
only change is not letting an exception cross it.

`when (ex is not OperationCanceledException)`: cancellation must still
propagate normally so the IDE/build's own cancellation semantics aren't
broken by treating a cancelled build as "this interface failed to
generate."

### Is there a meaningful distinction between failure classes?

Investigated per the research brief's explicit ask. Three candidate
classes:

1. **Expected/per-interface generation failures** - already fully handled
   today via `testDouble.Diagnostics` at analysis time (unsupported
   shapes, accessibility, naming collisions, ...). Not this issue's
   concern; already correct.
2. **Unexpected but interface-local internal failures** - a bug in
   `TestDoubleEmitter.Generate`'s C# model-building code, or a Scriban
   render failure specific to this interface's member shapes (the #142
   history). Interface-local in effect: only this interface's emission
   throws; every other interface's `TestDoubleEmitter.Generate` call runs
   on entirely independent local state (a fresh anonymous model, a fresh
   `Scriban.TemplateContext`) and has no way to be corrupted by another
   interface's failure.
3. **Generator-global failures** - the one candidate found is
   `TemplateHelper.LoadTemplate`'s embedded-resource-missing/parse-failure
   path. This is not interface-specific: the same template resource
   backs every interface's render. But empirically this does **not**
   need separate handling: if the embedded template is genuinely
   corrupted, *every* interface's `TestDoubleEmitter.Generate` call
   fails, and the recommended per-item `try/catch` correctly reports the
   same `CMP0050` diagnostic for every single one of them - which is the
   truthful outcome ("this interface's double could not be generated"),
   just reported N times instead of once. That's a worse message than a
   single "the generator's own template asset is corrupted" diagnostic
   would be, but it is not *incorrect*, it does not hide a real failure,
   and it doesn't require a separate distinction to implement. A cleaner
   single-diagnostic experience for this specific case is a plausible
   future refinement (e.g. checking `LoadTemplate`'s result once,
   compilation-wide, via a dedicated pipeline value) but isn't required to
   close #143's actual defect and would add pipeline shape this research
   wasn't asked to justify.

No reliable, general way was found to distinguish "class 2" from "class
3" *at the catch site* - by the time an exception reaches this boundary,
both look identical (an `Exception` thrown from inside
`TestDoubleEmitter.Generate`). The distinction that matters in practice
is: does the same failure recur for every interface (symptom of a
generator-global cause) or only one (symptom of an interface-local
cause)? That's an observable pattern across the diagnostic's occurrences
in a real build, not something the catch site itself can determine, and
per the brief's instruction not to invent a distinction that can't be
made reliably, none is proposed here. Catching `Exception` broadly (minus
`OperationCanceledException`) at this one boundary is recommended as-is.

## 6. Diagnostic contract (proposed, not implemented)

- **ID**: `CMP0050` (next free ID in `DiagnosticDescriptors.cs`; existing
  file runs `CMP0001`-`CMP0049` with no gaps, per `grep` inventory below).
- **Category**: `Compono.TestDoubles` (matches every other ADR-0043
  generated-test-double diagnostic - `CMP0020`-`CMP0032`, `CMP0035`-`CMP0039`
  are all this category; `Compono.Usage` is used for constructor-selection/
  profile diagnostics, a different concern).
- **Severity**: `Error`, `isEnabledByDefault: true` - matches the brief's
  stated intent and the existing precedent (`CMP0033`/`CMP0034` are
  `Compono.Usage` **Error** diagnostics for a similarly "this specific
  request cannot be honored" failure).
- **Title**: "Test double generation failed unexpectedly."
- **Message format** (three args: interface name, exception type name,
  exception message):
  `"'{0}' could not have a test double generated for it due to an unexpected internal error ({1}: {2}). This interface's Configure()/Verify() surface will not be available; other generated output in this compilation is unaffected."`
- **Location**: `Location.None`. `DiscoveredTestDoubleInfo` does not
  currently carry the original request-site `Location` through to
  emission time (only `testDouble.Diagnostics`/`InfoDiagnostics` - each
  its own `DiagnosticInfo` with its own `Location`, captured at *analysis*
  time - do). Piping an original call-site `Location` through to survive
  all the way to `TestDoubleEmitter.Generate` for this one, hopefully-rare
  path is exactly the kind of pipeline-shape change §5 avoids, and
  `Location.None` already has direct precedent in this exact file for a
  compilation-wide, non-single-site failure (`ConflictingTestDoubleMetadata`
  at line 435, `ConflictingCollectionMetadata` at line 362 - both pass
  `null` for `Location`).
- **Exception information surfaced**: exception **type name** and
  **message only** - never the stack trace. A stack trace as a normal
  compiler diagnostic is implementation noise a consumer can't act on and
  may occasionally leak internal file-path/line information from the
  generator's own build; type name + message is enough to distinguish "a
  null reference in some model-building step" from "an out-of-memory
  condition" without being unreadable in a build log. If deeper triage is
  ever needed, that's a `docs/research/` investigation using the harness
  in this repo, not something a consumer's own build output needs to
  carry.

## 7. Failed-interface output: no source (Option A)

Confirmed rather than assumed: emitting **no** source for the failed
interface, plus the dedicated Error diagnostic, is correct - not just the
stated preference. Reasoning, validated against actual behavior:

- Scenario D already proves Roslyn keeps every *other* interface's
  `AddSource` output when this interface's callback reports a diagnostic
  and returns without calling `AddSource` - there's no technical reason to
  emit anything for the failed interface at all.
- A placeholder/fallback double would have to either (a) implement the
  interface with `default`/`throw`-everywhere bodies, silently changing
  behavior for a consumer's existing tests in a way an Error-severity
  diagnostic already tells them not to rely on, or (b) not implement the
  interface, which doesn't compile and would itself need its own separate
  diagnostic to explain - strictly worse than reporting one honest
  diagnostic and stopping.
- Compono's own precedent throughout this file (`testDouble.Diagnostics.Count > 0`
  ⇒ `return` with **zero** source for that interface, e.g. an
  inaccessible-interface or unsupported-member-shape rejection) already
  treats "diagnosed failure ⇒ no source for that leaf" as the established
  pattern for every *other* kind of test-double failure. Treating an
  unexpected-exception failure any differently would be a new, unjustified
  special case.
- Since `Diagnostics.Count > 0` for a *different* reason already means "no
  generated double, caller falls back to whatever the runtime-provider
  path does for an ungenerated interface" - this failure mode fits that
  same, already-understood consumer-facing contract exactly.

## 8. Regression-test strategy (recommended, not implemented)

The natural per-interface failure trigger search (§9 in the original
brief) turned up nothing reliable inside `TestDoubleAnalyzer`/
`TestDoubleEmitter`'s current logic that fails *only* at emission time for
a legally-analyzed interface without deliberately engineering a shape to
exploit it - and deliberately engineering one would make the regression
test coupled to whatever narrow internal bug it exploits, the exact
coupling problem the brief explicitly warns against (mirrors why #142's
own regression test stayed behavior-level rather than asserting internal
Scriban counters).

Recommended approach: a **generator-driver-level integration test against
a small, purpose-built test seam**, not a naturally-occurring failing
interface shape:

- Add one narrow, internal-only test hook - e.g. an `internal static`
  settable delegate on `TestDoubleEmitter` (something like
  `internal static Action<string>? SimulateFailureFor`, called at the very
  top of `Generate` with `testDouble.InterfaceFullyQualifiedName`, a no-op
  by default) that only `Compono.Generators.Tests` (already
  `InternalsVisibleTo`) can set. This is the smallest possible seam: zero
  production behavior change when unset (the default, always true for a
  real consumer build), one method call, no new production dependency, no
  test-only production API surface exposed publicly.
- A generator-driver test (same shape as `GeneratorTestHelpers.GenerateFromSource`)
  requests two interfaces in the same compilation - one healthy, one whose
  name is configured to trip the hook - and asserts, directly on
  `GeneratorDriverRunResult`:
  1. `result.Diagnostics` contains `CMP0050` for the failing interface's
     name, and does **not** contain `CS8785`.
  2. `result.GeneratedTrees` contains a generated file for the healthy
     interface (proving sibling survival) and **no** generated
     `.TestDouble.g.cs` file for the failing interface (proving no
     partial/fallback source, §7).
  3. The healthy interface's generated source still compiles and the
     resulting assembly can actually be exercised via
     `CompileAndExecute` (proving "usable to the extent possible" per
     the brief's item 5) - a real end-to-end double, not just a
     diagnostics-only check.
  4. Run twice (or re-run the driver against an equivalent/incrementally-
     updated compilation) to confirm determinism - same diagnostic, same
     tree set both times.
  5. A control test with the hook unset (or never triggered) proves
     ordinary, all-healthy generation is byte-for-byte unchanged from
     today - the isolation change must be behavior-neutral for the
     success path.
- Reset the hook to its no-op default in the test's own cleanup/dispose
  (or scope it per-test via a distinct interface name, avoiding any
  cross-test static-state leakage) - this needs the same care
  `GeneratorTestHelpers`' existing static state (none currently) would
  need if it had any.

This is smaller and more honest than either alternative: a "naturally
unsupported-but-late-failing shape" doesn't currently exist to exploit
(and inventing one by strange-casing an existing analyzer gap risks the
analyzer being fixed later and silently deleting this regression test's
coverage), and a pure emitter-unit-test (calling `TestDoubleEmitter.Generate`
directly with a hand-built `DiscoveredTestDoubleInfo` designed to throw)
wouldn't exercise the actual `RegisterSourceOutput`/Roslyn boundary this
issue is about at all - it would only prove `TestDoubleEmitter.Generate`
throws, which was never in question.

## 9. Incremental-generator implications

- **Caching/equality**: no change. `DiscoveredTestDoubleInfo`'s record
  equality, the `discoveredTestDoubles` provider's shape, and every
  `WithTrackingName` stage are untouched - the fix is entirely inside the
  `RegisterSourceOutput` callback body, which incremental caching treats
  as opaque per-value work already.
- **Deterministic output**: unaffected. The failing interface deterministically
  produces the same diagnostic and no source on every run with the same
  input (proven directly by the Scenario D probe, and by recommended
  regression-test item 4 above).
- **Hint names**: unaffected - a failed interface simply never reaches
  `context.AddSource`, so no hint name is claimed for it at all (no
  collision risk with a later successful run for the same interface,
  since dedup already guarantees at most one `DiscoveredTestDoubleInfo`
  per `InterfaceFullyQualifiedName`).
- **Ordering**: unaffected - `RegisterSourceOutput` callback invocation
  order for `discoveredTestDoubles`' values is unchanged; only the
  presence/absence of one value's diagnostic and source changes based on
  whether its own callback invocation throws.
- **Duplicate `AddSource` behavior**: unaffected/not implicated - this fix
  never adds a new `AddSource` call for the failed path (§7); it only
  guards the existing one.
- **Diagnostic ordering**: `CMP0050` reports through the same
  `SourceProductionContext.ReportDiagnostic` every other diagnostic in
  this file already uses - no new ordering concern beyond what already
  exists for `testDouble.Diagnostics`/`InfoDiagnostics`.
- **Memory/allocation**: negligible - one additional `try/catch` frame per
  interface, on a path that already builds a full anonymous model and
  renders a template; the `try` block itself has zero cost in the
  non-throwing (overwhelmingly common) case.
- **Parallel/concurrent execution**: `RegisterSourceOutput` callbacks are
  not guaranteed thread-safe by Roslyn's own contract in general, but
  Compono's callback here has always been `static` and operates only on
  its own `testDouble` parameter and the passed-in `productionContext` -
  wrapping part of that same self-contained work in a `try/catch`
  introduces no new shared mutable state and changes nothing about the
  existing concurrency posture.

## 10. Public API / package implications

Confirmed, not assumed: this fix is entirely internal to
`Compono.Generators`.

- No runtime (`Compono`, `Compono.TestDoubles`, or any integration
  package) changes - the runtime-provider fallback path a
  `Diagnostics.Count > 0` interface already defers to is unchanged and
  already handles "this interface has no generated double."
- No public API changes - `CMP0050` is a new diagnostic ID, not a new
  type/method/attribute; diagnostic IDs are not part of Compono's public
  API surface contract the way types/methods are (consistent with how
  `CMP0001`-`CMP0049` were each added without an API-surface discussion).
- No consumer configuration - the diagnostic fires unconditionally on the
  (expected to be rare) failure path; no opt-in/opt-out needed or
  proposed, matching every other `isEnabledByDefault: true` diagnostic in
  this file.
- No analyzer-config/MSBuild changes - unlike ADR-0043's
  `ComponoGeneratedTestDoubles` flag or ADR-0055's `ComponoGeneratedLogging`
  flag, this fix applies unconditionally whenever test-double generation
  is already enabled; it doesn't need its own opt-in.
- No package dependency changes - no new dependency is needed; the fix
  uses only `System.Exception`/`Microsoft.CodeAnalysis` APIs already in
  use throughout this project.

## 11. ADR classification recommendation

**Recommend a new, light-dive ADR** (not a bug-fix-only Plan, and not an
amendment to an existing ADR).

- Not amendment-eligible: neither `docs/adr/0005-generator-implementation-conventions.md`
  nor `docs/adr/0043-compono-generated-test-doubles.md` (nor any other ADR
  found via search) establishes any policy about generator-wide exception
  handling, failure isolation, or a diagnostic contract for an
  unanticipated internal failure - there's nothing existing to correct or
  extend, which is what an amendment is for.
- Not "no ADR needed": per `references/design-decisions.md`'s rule 1,
  "every design decision gets an ADR", and this genuinely is one - it's
  simultaneously (a) a new public compiler-diagnostic contract (`CMP0050`,
  its ID/message/severity are effectively part of what a consumer's build
  output can show them) and (b) a **generator-wide policy decision** about
  what happens when *any* per-item emission callback in
  `ComponoIncrementalGenerator` throws - a decision that, per Scenario C
  (§2), has a blast radius wider than test doubles alone. That policy
  question ("should every `RegisterSourceOutput` registration in this
  generator get the same per-item isolation treatment, or only the
  test-double one issue #143 names?") is exactly the kind of "genuine fork
  between two plausible shapes" `AGENTS.md`'s "When uncertain" section
  says to talk through with the user rather than silently deciding in
  code.
- **Light dive, not deep**: the problem is fully diagnosed and the shape
  of the solution is already clear and empirically validated (§5, Scenario
  D) - this ADR mostly needs to record the decision and its evidence, not
  explore open design space. Most sections should compress the way
  `references/design-decisions.md`'s "Light" dive guidance describes.

## 12. Recommended implementation direction (summary)

1. New ADR (light dive) recording: the generator-wide isolation policy
   decision (§2 Scenario C's finding - scoped to test doubles only per
   issue #143, or extended to every `RegisterSourceOutput` registration in
   `ComponoIncrementalGenerator` - **this specific scope choice needs your
   decision before a Plan is written**, see §16), the `CMP0050` diagnostic
   contract (§6), and the "no source for a failed interface" decision
   (§7, formalizing what's already this file's established convention).
2. A Plan implementing that ADR:
   - `DiagnosticDescriptors.CMP0050` (`Compono.TestDoubles` category,
     `Error`, per §6).
   - The `try/catch` boundary in `ComponoIncrementalGenerator.cs`'s
     `discoveredTestDoubles` `RegisterSourceOutput` callback (§5) - and,
     depending on the ADR's scope decision, the same treatment applied to
     `discoveredCollections`/`discoveredTypes`/`rowInvokerTypes`/
     `discoveredLoggingCategories`/`aotComposeMethodsAll`'s own emission
     calls.
   - The `TestDoubleEmitter.SimulateFailureFor` (or equivalently minimal)
     test-only seam and the generator-driver regression test (§8).
   - `docs/architecture.md` (or wherever generator diagnostics are
     currently indexed, if anywhere) updated in the same PR per
     `AGENTS.md`'s "Update the relevant docs in the same PR" rule.

## 13. Smallest likely implementation scope/files

- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` - one new
  descriptor (`TestDoubleGenerationFailed`, `CMP0050`).
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` - one
  `try/catch` added around the `discoveredTestDoubles` callback's
  `TestDoubleEmitter.Generate` call (plus, pending the scope decision in
  §16, up to five more equivalent wraps elsewhere in the same file).
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` - the minimal
  test-only failure-injection seam, if the Plan adopts §8's recommended
  regression-test strategy.
- `test/Compono.Generators.Tests/` - one new test file for the
  generator-driver-level regression test.
- A new `docs/adr/00NN-...md` and a new `docs/plans/00NN-...md`.

No changes anywhere else - no runtime package, no `TestDoubleAnalyzer.cs`
change (analysis-time diagnosed failures are already correct), no
template change.

## 14. Exact research files changed/created

- Created: `docs/research/0035-testdoubles-generator-failure-isolation-issue-143-investigation.md`
  (this file).
- No other tracked files changed.

## 15. Temporary artifacts and removal confirmation

- Created and removed: `test/Compono.Generators.Tests/ZZZGeneratorIsolationExperiment.cs`
  (the Scenario A/C/D probe generators and tests, §2). Removed via `rm`;
  confirmed via `git status --short` returning empty before writing this
  document.
- No git worktrees created for this investigation (unlike RESEARCH-0034 -
  this investigation needed no separate checkout, since it never touched
  Compono's own generator/discovery/emitter code, only a standalone probe
  inside the existing test project).

## 16. Findings that could change #143's intended behavior

1. **Blast radius is wider than "test doubles"** (§2 Scenario C, §11):
   issue #143's title and body both frame this as a test-double-specific
   defect, but the underlying Roslyn behavior means *any* unhandled
   exception from *any* of `ComponoIncrementalGenerator`'s eight
   `RegisterSourceOutput` registrations today discards **all** of them,
   not just test doubles. A fix scoped literally to `discoveredTestDoubles`
   closes the exact reproduction in #142/#143's history, but leaves five
   other emission call sites (`CollectionPlanEmitter`,
   `CompositionPlanEmitter`, `RowInvokerRegistrationEmitter`,
   `LoggingActivationEmitter`, `AotTheoryDataRowRegistrationEmitter`) with
   the identical unguarded-exception hazard, discoverable by the identical
   Scenario C reproduction substituting any of them. This needs an
   explicit decision: scope this work to test doubles only (matching
   #143's literal title, deferring the other five registrations as their
   own future issue/plan, mirroring how #142 and #143 were themselves
   split apart), or address all six registrations under one broadened
   ADR/Plan now that the pattern and fix are both already fully
   understood and identical for each. Both are legitimate; this research
   deliberately doesn't pick one, per the brief's own scope-discipline
   instructions and the "talk it through" guidance in `AGENTS.md`.
2. Everything else investigated (diagnostic contract, failure boundary,
   regression-test strategy, incremental/API implications) confirmed the
   assumptions already stated in #143 and in your research brief - no
   other finding contradicts them.
