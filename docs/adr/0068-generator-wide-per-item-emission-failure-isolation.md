# [ADR-0068] Generator-Wide Per-Item Emission Failure Isolation

**Status:** Accepted

**Date:** 2026-09-15

**Decision Makers:** solo (product owner), assisted by Claude Sonnet 5

## Context

Issue #143: a test double's failed generation was observed to discard all
unrelated generated output for the compilation. RESEARCH-0034 (issue
#142) first surfaced this as a side effect of a Scriban render crash;
RESEARCH-0035 investigated it directly and established, empirically
(Compono-independent `IIncrementalGenerator` probes against a real
`CSharpGeneratorDriver`, no Scriban/AWS/large-interface involvement), that
the defect is **not** specific to test-double generation:

> Any unhandled exception escaping any `RegisterSourceOutput` callback
> invalidates the entire generator run (`CS8785`) and discards every
> `AddSource` output that generator instance produced in that run -
> including output from other, completely unrelated
> `RegisterSourceOutput` registrations on the same generator (RESEARCH-0035
> §2, Scenario C).

Roslyn provides no built-in per-value or per-registration isolation for
this. The only isolation mechanism available is: never let an exception
escape a `RegisterSourceOutput` callback in the first place. RESEARCH-0035
Scenario D proved this directly - catching the exception, reporting a
diagnostic, and returning without calling `AddSource` for the failed item
is sufficient for every other item (in the same registration, and in
other registrations) to keep its generated output intact, with no
pipeline restructuring.

`ComponoIncrementalGenerator.cs` has six independent-item emission call
sites today (`discoveredCollections` → `CollectionPlanEmitter`,
`discoveredTestDoubles` → `TestDoubleEmitter`, `discoveredTypes` →
`CompositionPlanEmitter`, `rowInvokerTypes` → `RowInvokerRegistrationEmitter`,
`discoveredLoggingCategories` → `LoggingActivationEmitter`,
`aotComposeMethodsAll` → `AotTheoryDataRowRegistrationEmitter`), plus one
non-emitting registration (`loggingRuntimeSymbolsStatus`, a
compilation-wide status diagnostic with no per-item artifact). Nothing in
the existing architecture (ADR-0005's generator conventions, ADR-0043's
test-double design) establishes a policy for what happens when an
emission callback throws unexpectedly - every existing diagnostic in
`DiagnosticDescriptors.cs` covers an *anticipated* failure found during
analysis, not an unexpected exception during emission.

This ADR decides the generator-wide policy (not just a test-double-specific
one, per product-owner direction) and its diagnostic contract. Full
empirical evidence lives in RESEARCH-0035; this ADR does not repeat it.

## Decision Drivers

- Blast radius found in RESEARCH-0035 §2 Scenario C is generator-wide, not
  registration-specific - a policy scoped to test doubles alone would
  leave five structurally identical hazards unaddressed.
- No consumer-visible regression: the overall compilation must still fail
  precisely (Error diagnostic) for a genuinely broken requested artifact;
  this is isolation of *unrelated* output, not an attempt to make a broken
  artifact appear to succeed.
- Diagnostic surface should stay small - one new descriptor is preferable
  to six, provided the message stays useful per call site.
- No mutable static test-only state may ship inside `Compono.Generators`
  (a Roslyn generator's execution/concurrency model must stay free of
  test-only global hooks).
- Minimal pipeline disruption - RESEARCH-0035 already confirmed no
  incremental-caching/equality/ordering impact from wrapping the existing
  per-item callback bodies; this ADR should not introduce a reason to
  revisit that.

## Considered Options

1. **Scope isolation to `discoveredTestDoubles` only** (the literal
   #143 reproduction), leaving the other five registrations unguarded.
2. **Generator-wide isolation**: apply the same catch/diagnose/skip-source
   policy to every `RegisterSourceOutput` registration that has a natural
   independent per-item boundary.
3. Diagnostic model, independently of (1)/(2):
   - **3A.** One generic diagnostic (`CMP0050`), parameterized by artifact
     kind + item identity + exception info.
   - **3B.** A separate diagnostic per domain (test doubles, composition
     plans, collections, logging, row-invoker/AOT).
   - **3C.** A shared descriptor with call-site-supplied context (a
     middle ground between 3A and 3B).
4. Implementation mechanism: explicit `try/catch` duplicated at each of
   the six call sites, vs. one small shared internal helper.
5. Regression-test architecture: a mutable static failure-injection hook
   on a real emitter, vs. a design with no shipped test-only state.

## Decision Outcome

Chosen: **Option 2** (generator-wide policy) + **Option 3A/3C hybrid**
(one shared descriptor, `CMP0050`, parameterized per call site - see
"Diagnostic model" below) + **a small shared helper** (Option 4) + **no
static failure-injection hook** (Option 5's "no shipped test-only state"
alternative - see "Regression-test architecture").

### The policy

> Any `RegisterSourceOutput` registration whose values represent
> independently-requested, independently-emittable artifacts must not let
> an exception from that artifact's own emission work escape the
> callback. The callback catches the exception, reports `CMP0050`
> attributing it to that item, and emits no source for that item.
> Everything else in the same compilation - other items in the same
> registration, and every other registration - continues unaffected.
> `OperationCanceledException` is never caught by this policy; it always
> propagates.

This is deliberately narrower than "wrap everything in try/catch." It
applies **only** where three conditions all hold (verified per
registration below, not assumed):

1. The registration's `IncrementalValuesProvider` already represents one
   value per independently-requested artifact (a natural per-item
   boundary Compono's discovery/dedup logic already produces - not
   something this ADR needs to construct).
2. The wrapped call is that item's entire remaining emission work, ending
   in at most one `AddSource` call for that item, so catching cannot
   leave a partially-added / corrupted source file behind.
3. Skipping source for a failed item is already this codebase's
   established behavior for every other kind of diagnosed failure at this
   same call site (each registration already does `if (diagnostics.Count > 0) return;`
   immediately before the emitter call, with zero source for that item -
   this policy adds exactly one more reason that early-return-with-no-source
   path can be reached, it does not invent a new consumer-facing
   contract).

### Registrations covered

Verified directly against `src/Compono.Generators/ComponoIncrementalGenerator.cs`
and each emitter's `Generate` method (each confirmed to call `AddSource`
exactly once, at the end, after building its complete source string - so
condition 2 above holds for all six with no exceptions):

| Registration | Item | Emitter | `AddSource` calls | Covered |
|---|---|---|---|---|
| `discoveredCollections` | `DiscoveredCollectionInfo` (one closed collection type) | `CollectionPlanEmitter` | 1, at end | Yes |
| `discoveredTestDoubles` | `DiscoveredTestDoubleInfo` (one interface leaf) | `TestDoubleEmitter` | 1, at end | Yes |
| `discoveredTypes` | `DiscoveredTypeInfo` (one composed type) | `CompositionPlanEmitter` | 1, at end | Yes |
| `rowInvokerTypes` | `RowInvokerTypeInfo` (one row-invoker-eligible type) | `RowInvokerRegistrationEmitter` | 1, at end | Yes |
| `discoveredLoggingCategories` (`.Combine(loggingRuntimeSymbolsStatus)`) | `DiscoveredLoggingCategoryInfo` (one `ILogger<T>` category) | `LoggingActivationEmitter` | 1, at end | Yes |
| `aotComposeMethodsAll` | `AotComposeMethodInfo` (one attributed method) | `AotTheoryDataRowRegistrationEmitter` | 1, at end | Yes |

### Registration excluded, and why

`loggingRuntimeSymbolsStatus`'s own `RegisterSourceOutput` (line 489-493)
is **not** covered - and not because it fails one of the three
conditions, but because it doesn't emit anything to isolate. Its callback
only calls `productionContext.ReportDiagnostic` for a single
compilation-wide status value (`LoggingRuntimeSymbolsStatus`); there is no
`AddSource` call, no per-item artifact, and no independent sibling that
could be lost if this callback threw. `Diagnostic.Create(...)` and
`ReportDiagnostic(...)` are not realistic throw sites here (no user input,
no I/O, no template rendering) - wrapping it would add a `try/catch` with
no attributable failure mode to catch. If this callback's body ever grows
real per-item emission work, it should be reassessed against the same
three conditions at that time, not preemptively wrapped now.

No registration needed to be excluded on isolation-safety grounds (i.e.
no case was found where catching would hide corruption of already-added
source or violate a generator invariant) - the "exactly one `AddSource`,
at the end" shape is uniform across all six.

### Exception boundary

`catch (Exception ex) when (ex is not OperationCanceledException)`, at
the emission-call boundary, per RESEARCH-0035 §5/§6's leading design - no
change found necessary for any of the six registrations. No taxonomy of
"local" vs. "global" exception causes is introduced; RESEARCH-0035 §6
established this can't be done reliably at the catch site, and per
product-owner direction this ADR does not attempt it. If a
generator-global asset is genuinely broken (RESEARCH-0035's `TemplateHelper.LoadTemplate`
example), every independent item that reaches that broken asset
correctly reports the same `CMP0050` - repetitive but truthful, and not a
reason to restructure the pipeline to deduplicate a rare diagnostic.

### Diagnostic model

**One shared descriptor, parameterized per call site** (Option 3C - the
smallest contract that stays domain-legible):

- **ID:** `CMP0050` (next free ID; `CMP0001`-`CMP0049` have no gaps).
- **Name:** `GeneratedSourceEmissionFailed`.
- **Category:** `Compono.Generators` (new category - none of the existing
  three fit: `Compono.Usage` is for a consumer-caused, actionable
  request-shape problem; `Compono.TestDoubles`/`Compono.Logging` are
  domain-specific and this diagnostic is deliberately cross-domain and
  about the generator's own internal robustness, not a consumer usage
  error or one domain's concern).
- **Severity:** `Error`, `isEnabledByDefault: true`.
- **Title:** "Generated source emission failed unexpectedly"
- **Message format** (four args: artifact kind, item identity, exception
  type name, exception message):
  `"Compono could not emit generated {0} for '{1}' due to an unexpected internal error ({2}: {3}). Generated output for this item is unavailable."`
  (Deliberately does not claim other output is unaffected: the isolation
  policy protects unrelated output from *this* exception, but multiple
  artifacts can independently fail, and a generator-global underlying
  problem may cause every emission to report `CMP0050` - the message
  should not imply a guarantee broader than what this one diagnostic
  actually establishes.)
  - `{0}` (artifact kind) is a short, call-site-supplied noun phrase -
    `"test double"`, `"composition plan"`, `"collection plan"`,
    `"row-invoker registration"`, `"logging activation"`, `"AOT
    theory-data-row registration"` - so the message reads naturally
    without needing six descriptors.
  - `{1}` (item identity) is the same identity string each registration
    already uses for its own diagnostics (e.g.
    `testDouble.InterfaceFullyQualifiedName`, `type.FullyQualifiedName`,
    `collection.FullyQualifiedCollectionTypeName`) - no new identity
    concept is introduced.
- **Location:** `Location.None` at every call site, uniformly. None of
  the six `Discovered*Info`/`*TypeInfo` records carry an original
  request-site `Location` outside their own `Diagnostics`/`InfoDiagnostics`
  collections (which are populated only for *analysis-time* failures,
  captured with their own `Location` at the point of analysis - a
  different, already-solved concern). Piping a `Location` through to
  survive to this rare, post-analysis emission-time path would require a
  pipeline-shape change to at least one model per registration, for a
  benefit RESEARCH-0035 didn't find evidence to justify; `Location.None`
  already has direct precedent in this same file
  (`ConflictingTestDoubleMetadata`, `ConflictingCollectionMetadata`).
- **Exception information surfaced:** exception type name + message only,
  never a stack trace - matches RESEARCH-0035 §6 and the product-owner
  instruction; the item identity plus the message body already state
  which artifact failed without needing implementation-noise detail.

**Option 3B (per-domain diagnostics) rejected**: would duplicate the
identical policy and message shape six times for what is, underneath,
the exact same internal failure mechanism (an exception escaping an
emission call) - the product-owner instruction to avoid diagnostics whose
"only purpose is architectural symmetry" applies in reverse here too:
six near-identical descriptors differing only in a hardcoded noun would
be symmetry for its own sake, not a real messaging improvement over one
descriptor with an artifact-kind argument.

### Failed-item source behavior

No source for the failed item (RESEARCH-0035 §7's Option A), for all six
registrations uniformly - never a placeholder/fallback. This is not a new
consumer-facing contract; it's the same "diagnosed failure ⇒ zero source
for that item, unaffected items continue" shape every one of these six
registrations already implements for its own analysis-time diagnosed
failures (`if (diagnostics.Count > 0) return;` immediately before each
emitter call). Overall compilation still fails, because `CMP0050` is
`Error` severity - the improvement is precision and blast radius, not
leniency.

### Cancellation behavior

`OperationCanceledException` is explicitly excluded from the catch at
every one of the six sites (`when (ex is not OperationCanceledException)`)
so IDE/build cancellation semantics are never mistaken for, or reported
as, an artifact-generation failure.

### Shared implementation mechanism

A **small shared internal helper**, not six duplicated `try/catch` blocks
and not a heavier abstraction:

```csharp
internal static class EmissionIsolation
{
    public static void TryEmit(SourceProductionContext context, string artifactKind, string itemIdentity, Action emit)
    {
        try
        {
            emit();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedSourceEmissionFailed,
                Location.None,
                artifactKind, itemIdentity, ex.GetType().Name, ex.Message));
        }
    }
}
```

Each of the six call sites becomes one line, e.g.:

```csharp
EmissionIsolation.TryEmit(productionContext, "test double", testDouble.InterfaceFullyQualifiedName,
    () => TestDoubleEmitter.Generate(productionContext, testDouble));
```

This was chosen over explicit per-callback `try/catch` because six
independent copies of identical catch/diagnose logic is exactly the kind
of duplication a two-line, explicit, single-purpose helper legitimately
earns its keep removing - it does not hide which artifact failed (the
caller passes both the kind and the identity explicitly, printed straight
into the message), does not change `SourceProductionContext` usage at any
call site (still passed straight through), and does not introduce any
interface, generic type parameter, or DI-style indirection - it is one
concrete method taking a `SourceProductionContext`, two strings, and an
`Action`. This is also, incidentally, the natural seam for testing
described next - a happy side effect, not the reason it was chosen.

No existing generator utility already owns this responsibility (confirmed
by inspection of `Emitters/`, `Diagnostics/`, and `WellKnownTypes/` -
nothing there currently touches exception handling at all).

### Regression-test architecture

No mutable static failure-injection hook ships in `Compono.Generators`.
Two focused layers replace the previously-proposed
`TestDoubleEmitter.SimulateFailureFor` static hook:

1. **Unit tests directly against `EmissionIsolation.TryEmit`** (no Roslyn
   driver, no generator involved) - `Compono.Generators.Tests` already
   has `InternalsVisibleTo` access. Exercise the helper as a plain method
   with local lambdas as its `emit` argument:
   - a delegate that throws `InvalidOperationException` → the shared
     `CMP0050` descriptor is reported with the expected `artifactKind`/
     `itemIdentity`/exception-type/message arguments, and no exception
     escapes `TryEmit`.
   - a delegate that throws `OperationCanceledException` → it propagates
     out of `TryEmit` uncaught, and no diagnostic is reported.
   - a delegate that succeeds → no diagnostic is reported, and the
     delegate is confirmed to have run (e.g. via a captured local flag).

   This proves properties 1-3 and 8 (no mutable global state at all -
   the "failure" is a local lambda owned by each test, not shared state)
   with zero Roslyn machinery.

2. **A generator-driver integration test using a test-project-local probe
   generator** - not a real Compono emitter, never shipped - that wires
   the real, production `EmissionIsolation.TryEmit` into two independent
   incremental values through an actual `IIncrementalGenerator`/
   `CSharpGeneratorDriver` run (the same technique RESEARCH-0035 §2 used
   to establish Roslyn's own semantics, but this time calling Compono's
   *real* shared helper instead of a hand-rolled `try/catch`):

   ```csharp
   internal sealed class EmissionIsolationProbeGenerator : IIncrementalGenerator
   {
       public void Initialize(IncrementalGeneratorInitializationContext context)
       {
           var values = context.CompilationProvider.SelectMany(static (_, _) => new[] { "failing", "healthy" });

           context.RegisterSourceOutput(values, (spc, value) =>
               EmissionIsolation.TryEmit(spc, "probe artifact", value, () =>
               {
                   if (value == "failing")
                       throw new InvalidOperationException("probe failure");

                   spc.AddSource($"{value}.g.cs", $"public class Generated_{value} {{}}");
               }));
       }
   }
   ```

   Asserted directly on `GeneratorDriverRunResult`: `CMP0050` present for
   `"failing"`, no `CS8785`; `GeneratedTrees` contains the `"healthy"`
   item's source and nothing for `"failing"`; re-running the driver
   produces the identical result (determinism). This proves properties
   4-6 using the real production helper and real Roslyn semantics,
   without depending on any historical Scriban/AWS/large-interface
   trigger and without touching any real Compono emitter.

3. **Property 7 (normal successful emission is unchanged)** needs no new
   test: it's exactly what the existing `Compono.Generators.Tests` suite
   already proves end-to-end for every real emitter (composition plans,
   collections, test doubles, logging, row invokers, AOT rows) - since
   `EmissionIsolation.TryEmit`'s non-throwing branch is a direct,
   transparent call to `emit()`, wrapping each real call site in it must
   leave every currently-passing test's observed behavior identical. The
   Plan's Test Plan should call this out explicitly as an acceptance
   check (full suite green, no snapshot changes) rather than writing a
   redundant new test for it.

This was chosen over the rejected static-hook design because it needs no
shipped mutable state at all - the "failure" in every test is a
locally-scoped `Action`/lambda, either passed directly to the unit-tested
helper or captured inside one small, test-project-only probe generator
class that is never part of the shipped `Compono.Generators` assembly.
It also directly satisfies the product-owner's split-responsibility
guidance: pure unit tests own the helper's own contract (throw/cancel/
succeed), and one generator-driver test owns proving Roslyn's own
sibling-survival behavior actually holds when the real helper is wired
into a real `RegisterSourceOutput` callback - neither layer has to prove
both, so neither needs an uglier design than its own property requires.

### Positive Consequences

- Closes issue #143's actual reported defect and its five
  previously-unguarded structural twins in the same generator, under one
  coherent, evidence-based policy rather than a narrower patch that would
  leave known-identical hazards behind.
- One diagnostic ID (`CMP0050`) for consumers to recognize/search for,
  instead of six near-identical ones.
- No production static test-only state introduced.
- Zero incremental-caching/ordering/hint-name impact (RESEARCH-0035 §9,
  unaffected by this ADR's broadened scope - the helper wraps existing
  per-item callback bodies exactly as RESEARCH-0035 already validated for
  the test-double case).
- No public API, runtime package, or consumer-configuration change.

### Negative Consequences

- A genuinely generator-global failure (e.g. a corrupted embedded
  template resource) will surface as the same `CMP0050` repeated once per
  affected item, rather than one summary diagnostic - accepted per
  RESEARCH-0035 §6 and product-owner direction as truthful, not
  incorrect, and not worth new pipeline complexity to deduplicate for
  what should be a very rare condition.
- `Location.None` means a consumer sees this diagnostic without a
  squiggle at a specific source line - mitigated by the message
  including the failing item's own identity string (interface/type/
  category/method fully-qualified name), which is what a consumer
  actually needs to find the request site themselves.
- A new diagnostic category (`Compono.Generators`) is introduced rather
  than reusing an existing one - accepted because none of the three
  existing categories accurately describe "the generator's own internal
  robustness," and mislabeling this as `Compono.Usage` would incorrectly
  suggest the consumer did something wrong.

## Pros and Cons of the Options

### Option 1 - test-double-only scope

- Good, because it's the smallest possible change and matches issue
  #143's literal title.
- Bad, because RESEARCH-0035 §2 Scenario C already proved the identical
  hazard exists at five other call sites with the literal same shape and
  the literal same fix - leaving them unguarded is knowingly shipping a
  known defect class rather than fixing it once it's understood.

### Option 2 - generator-wide scope (chosen)

- Good, because it closes every known instance of the same structural
  defect in one coherent policy, verified per-registration rather than
  applied blindly.
- Good, because the three-condition verification (§ "Registrations
  covered") means nothing was wrapped without confirming it was safe to.
- Bad, because it touches more of `ComponoIncrementalGenerator.cs` than
  the minimal #143 reproduction alone would require - mitigated by the
  shared helper keeping each site to one line.

### Option 3A - one fully generic diagnostic (no per-call-site context)

- Good, because it's the absolute smallest diagnostic surface.
- Bad, because a message with no artifact-kind/identity context ("source
  emission failed") would force a consumer to guess what actually broke -
  rejected in favor of 3C, which keeps one descriptor but restores that
  context via arguments.

### Option 3B - per-domain diagnostics

- Good, because each message could be hand-tuned per domain.
- Bad, because it duplicates the identical underlying policy six times
  for what is the same failure mechanism throughout - the "architectural
  symmetry" the product-owner instruction explicitly warned against.

### Option 3C - shared descriptor, call-site-supplied context (chosen)

- Good, because one descriptor stays consumer-searchable while the
  artifact-kind/identity arguments keep every message specific and
  actionable.
- Bad, because the message's `{0}` noun phrase is a plain string, not a
  strongly-typed enum - accepted as proportionate for six known, fixed
  call sites that will not grow without a code change to add the
  argument anyway.

### Static failure-injection hook (rejected)

- Good, because it would have been the simplest thing to wire up for a
  test.
- Bad, because it ships mutable global state inside a Roslyn generator
  assembly, an execution/concurrency-sensitive component where global
  state is specifically the kind of hazard this repo's own conventions
  avoid elsewhere - rejected outright per product-owner direction, and
  superseded by the two-layer helper-unit-test + probe-generator design
  above, which needs no such thing.

## Links

- [Issue #143](https://github.com/LayeredCraft/compono/issues/143)
- [RESEARCH-0035](../research/0035-testdoubles-generator-failure-isolation-issue-143-investigation.md) - full empirical evidence (Roslyn Scenario A/C/D experiments, pipeline trace, per-registration analysis)
- [ADR-0005](0005-generator-implementation-conventions.md) - generator implementation conventions (no existing exception-isolation policy found there; this ADR is new, not an amendment)
- [ADR-0043](0043-compono-generated-test-doubles.md) - generated test doubles (the domain that first exposed this defect, per RESEARCH-0034/0035)

## Non-Goals

- Does not revisit issue #142 or its Scriban `LoopLimit`/`LimitToString`
  values (`20_000` / `20_000_000`, unchanged).
- Does not address the repeated-generated-comment finding from
  RESEARCH-0034 §17.
- Does not investigate or adopt multi-file generated output.
- Does not restructure the incremental pipeline - every registration's
  existing shape, dedup logic, and `WithTrackingName` stages are
  unchanged.
- Does not change any public runtime API, add consumer configuration, or
  change any package dependency.
- Does not implement anything - this ADR records the decision only; a
  Plan follows once this ADR is approved.
