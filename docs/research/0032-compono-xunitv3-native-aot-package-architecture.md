# [RESEARCH-0032] Compono.XunitV3 Native AOT: Package & Generator Architecture Research

**Status:** Research complete. Recommends admission and a specific architecture (see §11-14). An ADR
([ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md), `Proposed`) and a phased
implementation plan ([PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md),
`Not Started`) accompany this document.

**Governing process:** [`docs/architecture/capability-admission.md`](../architecture/capability-admission.md).

**Relationship to RESEARCH-0031:** [RESEARCH-0031](0031-native-aot-framework-native-testing-admission-research.md)
is frozen and not modified by this document. This is a new, focused follow-on, scoped specifically to
the question RESEARCH-0031 §5/§12/§18 flagged as open: *can `Compono.XunitV3` support xUnit v3 Native
AOT, and if so, how.* RESEARCH-0031's corrected empirical findings are treated as established evidence
here, not re-derived: plain xUnit v3 AOT works via `xunit.v3.aot.mtp-v2`; `Compono.XunitV3`'s shipped
package depends on the reflection-mode `xunit.v3.extensibility.core`, causing `CS0433` against the
`.aot` family; this is a Compono-side compatibility gap, not an upstream xUnit bug; xUnit's AOT
extensibility samples use companion source generators emitting
`RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)` calls, which `ComposeAttribute` and
`Compono.Generators` don't participate in today.

**Trigger:** the product owner, after accepting RESEARCH-0031's corrected xUnit finding, requested this
deeper, focused pass specifically to answer whether `Compono.XunitV3` should (and can) support xUnit
v3 Native AOT, and what the smallest correct architecture is.

---

## 1. Why does the current package fail?

Two independent, compounding reasons, both confirmed directly this session (not inferred):

1. **Compile-time assembly-identity conflict.** `Compono.XunitV3`'s shipped `.nuspec` declares a
   dependency on `xunit.v3.extensibility.core` (the reflection-mode package) for every target
   framework. `Compono.XunitV3.ComposeAttribute : DataAttribute` is therefore permanently compiled
   against that specific `DataAttribute` type. xUnit's real AOT package family
   (`xunit.v3.aot.mtp-v2` → `xunit.v3.core.aot`/`xunit.v3.extensibility.core.aot`) ships a
   **different, non-interchangeable** `DataAttribute`/`TheoryAttribute` type in a differently-named
   assembly. Referencing both `Compono.XunitV3` and `xunit.v3.aot.mtp-v2` in one project restores both
   assemblies side by side and fails to compile: `error CS0433: The type 'TheoryAttribute' exists in
   both 'xunit.v3.core.aot, ...' and 'xunit.v3.core, ...'`. This is not a version-range or
   floating-dependency problem fixable by widening a version constraint — the two assemblies are
   **different physical types with the same name**, by xUnit's own design (see §2).
2. **No participation in xUnit's AOT theory-data extensibility contract**, even hypothetically —
   `Compono.Generators` emits nothing calling `Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory`,
   the API xUnit's own AOT extensibility samples (`AotCsvDataSource`, `AotRetryFact`,
   `AotTraitExtensibility` — all three, independently, same shape) use as the **only** supported way to
   supply theory data under AOT. `ComposeAttribute`'s current `GetData` override (~300 lines of runtime
   composition logic — inline-value validation, `[Shared]` binding, seed policy) is exactly the kind of
   runtime logic the AOT model doesn't invoke at all (see §2/§5).

## 2. What does xUnit's AOT extensibility model actually require?

Traced directly from xUnit's own source (`xunit/xunit` on GitHub) and its official
`xunit/samples.xunit` reference implementations — not assumed from prose:

- **`RegisteredEngineConfig` is not one shared type — it's two independent types with the same name,
  in different namespaces' physical assemblies, deliberately kept API-compatible where the two modes'
  concerns overlap** (`xunit.v3.core.aot`'s `RegisteredEngineConfig_aot.cs` carries the comment "The
  signatures of these APIs are fixed between reflection and AOT" for the *shared* subset — orderers,
  assembly-level config, etc.). **But `RegisterTheoryDataRowFactory`/`GetTheoryDataRowFactories` exist
  only in the `.aot` variant** — confirmed by reading both files in full. The reflection-mode
  `RegisteredEngineConfig` (`xunit.v3.core`) has no theory-data-row-factory concept at all, because
  reflection mode gets theory data by calling `DataAttribute.GetData` directly at runtime — it doesn't
  need a compile-time registry. **This means generated code calling
  `RegisteredEngineConfig.RegisterTheoryDataRowFactory` cannot compile against the reflection-mode
  package at all** — a second, independent reason (beyond the `TheoryAttribute` conflict) that the two
  modes cannot share one compiled artifact.
- **xUnit ships a reusable generator base class for exactly this purpose**:
  `Xunit.Generators.DataAttributeGenerator` (in `xunit.v3.generatorutility`, a build-time-only,
  `netstandard2.0` analyzer-authoring package). A custom data attribute's own generator derives from
  it, supplies the attribute's fully-qualified metadata name to the base constructor, and overrides one
  method: `ProcessAttribute(SemanticModel, testClass, testMethod, AttributeData, TResult result, ...)`,
  which appends a **factory lambda source string** (arbitrary C#, emitted as text) to
  `result.Factories`. The base class's `CreateSource` override does the actual emission:
  ```csharp
  global::Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(
      TestClassTypeName, "MethodName", disableDiscoveryEnumeration,
      <factory lambda text>
  );
  ```
  wrapped in a generated module-initializer-style "init attribute" — the same registration shape
  Compono's own `PlanCache<T>`/`RowInvokerRegistry` module initializers already use (ADR-0041). The
  factory lambda itself can contain **arbitrary code**, including calls into any other package's public
  API — xUnit's own `AotCsvDataSource` sample's factory calls into a third-party CSV-parsing library
  (`Sep`) this way.
- **A custom `DataAttribute` subclass under AOT is typically a near-empty marker** — xUnit's own
  `CsvDataAttribute` sample has **no `GetData` override at all**. The attribute instance is discovered
  by the generator at compile time (via its Roslyn `AttributeData`), and the registered factory —
  looked up from `RegisteredEngineConfig` by test-class/method name — is what actually supplies rows at
  discovery/execution time. `DataAttribute.GetData`'s runtime override is simply never called in this
  path.

## 3. Can one package support both reflection and AOT modes?

**No — ruled out by two independent, hard technical constraints, not a matter of preference or
cleverness:**

1. `ComposeAttribute : DataAttribute` is a single compiled type, permanently bound at Compono's own
   build time to one specific `DataAttribute` assembly. There is no NuGet or MSBuild mechanism that
   lets a *library's* compiled output retroactively rebind to a different assembly identity based on a
   downstream consumer's later choice (which xUnit package family they reference, or whether they later
   `-p:PublishAot=true`) — that choice happens in the *consumer's* project file, at a point in time
   after `Compono.XunitV3.dll` has already been built and packed.
2. Even granting a hypothetical way around (1), the generated registration code
   (`RegisteredEngineConfig.RegisterTheoryDataRowFactory`) **would not compile** against the
   reflection-mode assembly at all — the method doesn't exist there (§2). There is no single piece of
   source text that compiles unconditionally against both package families for this API.

NuGet's per-TargetFramework dependency-group mechanism (which the product owner correctly anticipated
being skeptical of) doesn't help either: `xunit.v3.mtp-v2` and `xunit.v3.aot.mtp-v2` target the
**same** real TFMs (net9.0+) — there is no TFM axis to key a conditional dependency group on. The
reflection/AOT choice is made by which package the *consumer* references, not by TFM, RID, or any other
axis NuGet dependency groups can select on. **Option A (single package, both modes) is not the smaller
or riskier option among viable choices — it is not achievable at all through supported NuGet/MSBuild
mechanisms**, confirming (with hard evidence, not just caution) the product owner's explicit skepticism
about "conditional-dependency tricks, hidden MSBuild magic, or restore-order-dependent behavior."

## 4. Smallest justified package boundary

A second, genuinely separate compiled artifact is required for the attribute type itself (§3). Given
that constraint, the real design question is scope: what else needs to move with it, and what can stay
shared.

**What must live in the new package:** a `Compono.XunitV3`-namespaced `ComposeAttribute` (and its
`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` siblings, deferred per §17) compiled against
`xunit.v3.extensibility.core.aot`. Per §2, this is close to a marker attribute — no `GetData` override
needed, since xUnit's AOT pipeline never calls it.

**What does not need to move, and should not:** core `Compono` (Composer, CompositionRow,
CompositionContext, `RowInvokerRegistry`, `PlanCache<T>`) is entirely framework-agnostic already and
has zero xUnit dependency of any kind — confirmed by this session's own proof spike (§9), which
referenced only the core `Compono` package (not even `Compono.XunitV3`) and worked correctly under real
Native AOT with zero changes. The existing, unmodified `Compono.XunitV3` package also does not need to
change at all (§10) — it keeps serving reflection-mode consumers exactly as today.

**Naming (product-owner decision, this session): `Compono.XunitV3.Aot`.** Mirrors xUnit's own
`xunit.v3` → `xunit.v3.aot` convention exactly, which is the most discoverable, predictable name for a
`Compono.XunitV3` user who already knows to reach for xUnit's own `.aot` packages.

## 5. Where does generator responsibility live?

**In `Compono.Generators` itself — no new generator/analyzer project, and no new package reference
from `Compono.Generators` to any xUnit assembly.** Two findings drive this:

- `Compono.Generators` **already** hardcodes each integration package's `[Compose]`-family attribute
  metadata names as string constants (`ComposeMethodDiscovery.AttributeMetadataName = "Compono.XunitV3.ComposeAttribute"`,
  and five more for `Compono.TUnit`/`Compono.MSTest`/`Compono.NUnit`) — confirmed directly,
  `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs:33-117`. This coupling is by **string
  metadata name only** (Roslyn's `ForAttributeWithMetadataName`), never by an actual assembly reference
  to `Compono.XunitV3.dll`/xUnit's own assemblies. Adding a seventh constant
  (`"Compono.XunitV3.Aot.ComposeAttribute"`) is the same, already-established, already-precedented
  pattern — not a new category of coupling, and not the kind of "core knows about an integration
  package" coupling `references/design-decisions.md` rule 3 forbids (`Compono.Generators` is not core
  `Compono`, and even if it were, this is string-based attribute-name matching, not an assembly
  reference).
- **Generated source text can call any well-known API by fully-qualified name without the generator
  itself ever referencing that API's defining assembly** — this is exactly how `Compono.Generators`
  already emits `RowInvokerRegistry.Register(...)`/`PlanCache<T>` calls today: it builds C# source as
  text, and the *consumer's own compilation* (which does reference the relevant assemblies) is what
  actually resolves and compiles that text. Emitting
  `global::Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)` as generated text requires
  zero new package references from `Compono.Generators`, confirmed by this session's own proof spike
  (§9), which hand-wrote exactly this shape of code with no dependency on `xunit.v3.generatorutility` —
  deriving from xUnit's `DataAttributeGenerator` base class is **not required**; it's a convenience for
  simple cases (handles the Roslyn attribute-discovery plumbing), but `Compono.Generators` already has
  its own, more capable discovery pipeline (`ComposeMethodDiscovery`/`ComposedTypeAnalyzer`) that
  already walks every `[Compose]`-attributed method's full parameter list, including types requiring
  generated `PlanCache<T>` plans — richer analysis than the sample generator's `ProcessAttribute` does
  for a single CSV column mapping.
- **Per-compilation mode detection is a legitimate, precedented Roslyn technique, not an MSBuild
  trick.** The generator must not emit the `RegisteredEngineConfig.RegisterTheoryDataRowFactory` call
  when compiling a reflection-mode consumer (it wouldn't compile there — §2). The correct check is
  `compilation.GetTypeByMetadataName("Xunit.v3.RegisteredEngineConfig")` combined with a member-existence
  check for `RegisterTheoryDataRowFactory` (or, more simply, checking which `ComposeAttribute` metadata
  name matched — `Compono.XunitV3.ComposeAttribute` vs. `Compono.XunitV3.Aot.ComposeAttribute` are
  already distinct attribute types once the new package exists, so the *existing*
  attribute-metadata-name-based dispatch already solves this for free, with no separate mode-detection
  logic needed at all). This is an ordinary semantic-model query against the compilation the generator
  is already running inside — deterministic, cacheable, incremental-generator-safe, and unrelated to
  MSBuild property evaluation, restore order, or any of the fragile patterns the product owner was
  right to rule out.

**Generated-code emission shape needed, concretely** (proven working end to end, §9): per
`[Compose]`-attributed method in a project referencing `Compono.XunitV3.Aot`, emit one module
initializer calling `RegisteredEngineConfig.RegisterTheoryDataRowFactory(typeName, methodName, false,
factory)`, where `factory` is a lambda that builds a `CompositionRow` (via `Composer.Create()`/
`CreateRow`) and, for each parameter, calls `row.Resolve<T>(descriptor)` with **T known at compile
time from the method symbol's own parameter types** — exactly the same information
`ComposeMethodDiscovery` already extracts for `PlanCache<T>`/`RowInvokerRegistry` emission today. This
generated path doesn't even need `RowInvokerRegistry`'s `Type`-keyed runtime lookup — the generator
already knows every `T` statically, so it can emit direct `row.Resolve<Widget>(descriptor)` calls,
which is *more* AOT-idiomatic than the existing reflection-mode dispatch (matching `PlanCache<T>`'s own
stated philosophy: a closed generic call site, not a runtime `Type`-keyed lookup).

## 6. Can `[Theory]`+`[Compose]` stay unchanged?

**Yes — proven, not assumed, with one precise qualification.** Existing `[Theory]` + `[Compose]` test
syntax remains unchanged — a test author still writes
```csharp
[Theory]
[Compose]
public void My_test(Widget widget, string leaf) { ... }
```
but a consumer opts into the AOT integration by changing package/reference imports to
`Compono.XunitV3.Aot` (alongside `xunit.v3.aot.mtp-v2` instead of `xunit.v3.mtp-v2`) — exactly the same
kind of upfront choice xUnit itself already requires independent of Compono. This is a package and
namespace change, not a no-op: `ComposeAttribute`'s fully-qualified type name differs
(`Compono.XunitV3.Aot.ComposeAttribute` vs. `Compono.XunitV3.ComposeAttribute`), so the `using` directive
changes too, even though the attribute usage at the test method — `[Compose]`, unqualified — reads
identically in source. Only the attribute-body/method-signature syntax is unaffected; the reference and
`using` are not.

## 7. Dependency/assembly-identity implications

- `Compono.XunitV3.Aot` depends on `Compono` (unchanged, core) and `xunit.v3.extensibility.core.aot`
  (not `xunit.v3.extensibility.core`) — mirroring `Compono.XunitV3`'s existing dependency shape exactly,
  just against the parallel assembly family.
- `Compono.XunitV3.Aot` must **not** be referenced alongside `Compono.XunitV3` in the same project —
  both declare a type named `Compono.XunitV3.ComposeAttribute` in principle, though placing the new
  package's type under a distinct namespace (`Compono.XunitV3.Aot`, matching the package name) avoids
  even a *namespace*-level collision, not just an assembly-identity one; this is a design decision for
  the ADR, not settled by evidence alone (see ADR §Decision Outcome).
- No change to `Compono`'s or `Compono.Generators`' own dependency graphs — `Compono.Generators`
  remains reference-free with respect to any test framework, exactly as today (§5).

## 8. Supportable versions/TFMs

| | Requirement | Evidence |
|---|---|---|
| xUnit v3 | **4.0.0+** (`xunit.v3.aot.mtp-v2`) — the version that introduced Native AOT support at all; no lower floor is possible | RESEARCH-0031 §3, confirmed again this session |
| .NET | **net9.0+** (xUnit's own stated Native AOT floor, due to `[OverloadResolutionPriority]` usage) | xUnit's official Native AOT doc, confirmed this session and RESEARCH-0031 |
| Compono | Whatever `Compono`'s current floor already is — no new core requirement; `Composer`/`CompositionRow` needed no changes to work under this proof (§9) | This session's proof spike |
| `Compono.XunitV3` (existing, unchanged) | No change to its current supported xUnit 3.x/version floor | N/A — untouched |

`Compono.XunitV3.Aot` would need `net9.0` as its floor (it cannot go lower than xUnit's own AOT floor),
which is narrower than `Compono.XunitV3`'s existing multi-TFM sweep
(`net8.0;net9.0;net10.0;net11.0`) — an accepted, expected narrowing for an AOT-specific package,
consistent with `Compono.TUnit`'s/`Compono.MSTest`'s own TFM floors being set by their own frameworks'
requirements rather than Compono's general floor.

## 9. Real end-to-end proof (this session's spike)

A throwaway spike (scratchpad only, discarded, no `src/` changes) built directly from xUnit's own
official `AotCsvDataSource` sample shape:

- Hand-written thin `ComposeAttribute : DataAttribute` (no `GetData` override — matching
  `CsvDataAttribute`'s shape exactly).
- A hand-written `[ModuleInitializer]`-decorated registration method, standing in for what
  `Compono.Generators` would emit, calling
  `RegisteredEngineConfig.RegisterTheoryDataRowFactory("global::AotXunitProof.RealTests",
  "Widget_is_composed_and_test_passes", false, factory)`.
- `factory` is a real closure calling only packaged, **unmodified** core `Compono` public API:
  `Composer.Create()`, `composer.CreateRow(typeof(RealTests))`, `row.Resolve<Widget>(descriptor)`,
  `row.Resolve<string>(descriptor)` — both `T`s known at compile time in the hand-written stand-in,
  exactly as a real generator's emission would have them known from the method symbol.
- A real `[Theory][Compose] public void Widget_is_composed_and_test_passes(Widget widget, string leaf)`
  test method, restored with **zero** reflection-mode xUnit assemblies present (only `Compono` +
  `xunit.v3.aot.mtp-v2`).

Result: compiled without `CS0433` or any other error; `dotnet publish -c Release -f net10.0 -r
osx-arm64 --self-contained true -p:PublishAot=true` produced **zero warnings** (not even the upstream
`xunit.v3.*` IL3000/IL2104/IL3053 warnings the plain-xUnit baseline in RESEARCH-0031 showed — this
project's smaller reference surface avoided even those); the published native binary, run directly,
self-hosted, discovered the test via the registered factory, executed real Compono composition inside
the native process, and passed:
```
xUnit.net v3 In-Process Runner v4.0.0+8bf043c053 [native/osx-arm64] (.NET 10.0.12)
  Discovering: AotXunitProof
  Discovered:  AotXunitProof
  Starting:    AotXunitProof
  Finished:    AotXunitProof (...)
=== TEST EXECUTION SUMMARY ===
   AotXunitProof  Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.001s
```
Exit code 0. (An earlier iteration of this same spike, before correcting a misuse of
`CompositionRow.Resolve<T>()`'s parameterless overload — which is reserved for a different calling
context per its own contract — surfaced a real, correctly-thrown `InvalidOperationException` from core
`Compono` itself, confirming the whole pipeline, including error propagation, was genuinely exercised
end to end, not silently no-op.)

**This is decisive evidence the proposed architecture works**, not a hopeful design argument. It
does not yet cover inline values, `[Shared]`, or profile variants (§17), and it hand-wrote what a real
generator would emit rather than actually extending `Compono.Generators` (disallowed this phase, per
the research/design boundary).

## 10. Effect on existing xUnit 3.x consumers

**None.** `Compono.XunitV3` is not modified by this proposal in any way — same package ID, same
dependency on `xunit.v3.extensibility.core`, same behavior, same version. A consumer who never installs
`Compono.XunitV3.Aot` sees zero change of any kind. (Product-owner decision, this session: purely
additive, no breaking change, no major version bump.)

## 11. Is any public API change actually required?

**No new public API on existing packages.** `Compono` core is unmodified (§9 proves this — the proof
spike used only its already-shipped public surface). `Compono.XunitV3` is unmodified (§10). The only
new public surface is an entirely new package's necessarily-public `ComposeAttribute` type (and its
generic siblings, deferred to a later phase per §17) — which is not "new API added to existing public
surface" in the sense the admission process's "public API discipline" step is meant to gate; it's the
minimum public surface a new, opt-in package needs to be usable at all, textually identical in shape to
`Compono.XunitV3`'s own existing, already-`Accepted` public attribute. `Compono.Generators`' new
emission logic is entirely internal/generated implementation detail, never itself public API.

## 12. Gate A verdict

Clears, on stronger grounds than most of this repo's own past candidates:

1. **Compono-specific value** — real. Makes an existing, established, heavily-dogfooded integration
   package (`Compono.XunitV3`) usable at all in a mode xUnit itself now officially supports — closing a
   real compatibility gap, not adding convenience around an already-easy call.
2. **Native ecosystem fit** — exact. The proposed package mirrors xUnit's own real, documented AOT
   extensibility mechanism (`DataAttributeGenerator`-shaped emission, `RegisteredEngineConfig`
   registration) precisely, not a Compono-invented shape bolted on.
3. **Meaningful abstraction** — clears easily. Without this, a consumer wanting `[Compose]` under xUnit
   AOT would need to hand-write the entire generator-registration mechanism themselves, per test
   project, from scratch (proven non-trivial by this session's own multi-hour investigation) — exactly
   the kind of ceremony a Compono package exists to collapse into a coherent, discoverable concept.
4. **Architectural fit** — clean. Built entirely on xUnit's own public, documented extension point
   (`RegisteredEngineConfig`/`DataAttributeGenerator`-shaped source generation); no core change
   invented ad hoc (§9's proof spike required zero core `Compono` changes).
5. **Package-boundary justification** — satisfied by hard technical necessity, not preference (§3/§4):
   the dependency (`xunit.v3.extensibility.core.aot`) cannot live in the existing package at all, and is
   substantial enough (a real, working generator-emission mechanism) to justify its own artifact.

## 13. Gate B verdict

Clears. Per the product owner's own framing (explicitly rejecting the narrow "has anyone asked for
this by name" standard): `Compono.XunitV3` is Compono's oldest, most established, most heavily
dogfooded integration package; Compono has explicit Native AOT/source-generation goals stated as a
core design principle; xUnit itself now officially, prominently supports Native AOT (4.0.0, a major
release); the current package cannot even coexist with that now-official mode at all (a real, present
compatibility gap, not a hypothetical future one); and this exact investigation was requested twice,
with escalating specificity, by the product owner — itself the same kind of explicit, direct
product-owner evidence that already cleared Gate B for `Compono.TUnit`, `Compono.NUnit`, and
Compono-owned test doubles with no dogfooding history at all.

MSTest and TUnit can already satisfy the broader "real AOT tests instead of console validators" outcome
on their own (RESEARCH-0031) — but that doesn't diminish the case here: a consumer already invested in
`Compono.XunitV3` (this repo's own most-used integration) shouldn't need to migrate to a different test
framework just to gain Native AOT compatibility their existing framework itself now officially offers.

## 14. Recommendation: admit, and the smallest viable architecture

**Admit.** Ship `Compono.XunitV3.Aot` as a new, separate package — versioned in lockstep with the rest
of the Compono package family, per [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)'s
existing lockstep policy; this research does not propose, and finds no evidence justifying, an
independent version lifecycle for it:

- A `Compono.XunitV3.Aot.ComposeAttribute` (namespace matching the package: `Compono.XunitV3.Aot`, not
  reusing `Compono.XunitV3`'s own namespace — avoids any possibility of a using-directive collision for
  a consumer who, for whatever reason, has both packages in a shared solution though never the same
  project) — thin marker, no `GetData` override, mirroring `CsvDataAttribute`'s shape.
- `Compono.Generators` extended with one new attribute-metadata-name constant
  (`"Compono.XunitV3.Aot.ComposeAttribute"`, following the exact existing pattern for the other four
  integration packages) and new emission logic producing the `RegisteredEngineConfig.RegisterTheoryDataRowFactory`
  module-initializer shape proven in §9 — no new package reference, no new project.
- `Compono.XunitV3` and core `Compono` are untouched.
- **Phase 1 scope (product-owner decision, this session): plain `[Compose]` only** — no inline values,
  no `[Shared]`, no `[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` yet. These are real,
  additional generator work (reproducing `BindingPlan`'s inline-value validation and `[Shared]`
  ordering at compile time, and `ConfigProfileBinder`'s profile-construction logic as generated code
  instead of `ConstructorInfo.Invoke`) — technically plausible by the same pattern, but unproven by this
  session's spike and appropriately deferred to a later phase rather than gating the first ship.

## 15. Required permanent CI proof, if admitted

A new AOT smoke leg exercising the **real** generator-emitted registration path end to end — not the
existing direct-dispatch `*.AotSmokeTest` pattern (which, per ADR-0041 Amendment 7, deliberately never
exercises a real test framework's own discovery/execution engine). Concretely: a real `[Theory]`+
`Compono.XunitV3.Aot`'s `[Compose]` test project, `PublishAot=true`, native binary executed directly,
asserting the process exits 0 and the expected test-summary output appears — mirroring this session's
proof spike, but through the real (once built) `Compono.Generators` emission rather than a hand-written
stand-in. One `linux-x64` leg is sufficient, consistent with ADR-0041 Amendment 7's existing nine legs
(no evidence of platform-specific behavior anywhere in this investigation).

## 16. Documentation/skill changes that would follow (not made now)

- A new Package Guide (`docs/packages/compono-xunitv3-aot.md`) following the existing per-package doc
  convention.
- `docs/packages/compono-xunitv3.md` gains a short cross-reference noting the AOT-specific sibling
  package and when to reach for it instead.
- The `compono` agent skill's per-package reference-file set gains a new entry (its detection table
  already routes by referenced package — this is the same, already-established pattern the skill uses
  for every other integration package).
- `evals.json`/baseline comparison would need updating once the skill changes, per the repo's existing
  skill-maintenance process — not evaluated in detail here, deferred to implementation time.
- RESEARCH-0031's own Cookbook/documentation implications (its §18) can now be updated to point at a
  real, working xUnit v3 AOT path instead of leaving it as an open product-owner question — but
  RESEARCH-0031 itself stays frozen per this session's explicit instruction; that update belongs to
  whichever PR actually ships `Compono.XunitV3.Aot`.

## 17. Remaining tradeoffs/risks

- **Inline values, `[Shared]`, and profile variants are unproven** (§14) — real, nontrivial generator
  work remains before `Compono.XunitV3.Aot` reaches parity with `Compono.XunitV3`'s full surface. The
  architecture pattern (compile-time-known `T`, generated factory closures) generalizes in principle
  (the same technique already handles arbitrarily complex object graphs for `PlanCache<T>`), but this
  is a design/implementation risk for the plan's later phases, not a re-opened architecture question.
- **Extension packages reachable through `[Compose]`** (`Compono.NSubstitute`, `Compono.Bogus`,
  `Compono.TestDoubles`, etc.) were not individually re-verified under this new AOT dispatch path this
  session — but since the generated factory calls the same core `Compono.CompositionRow.Resolve<T>()`
  surface every other integration already uses, and core `Compono` needed zero changes for this proof
  (§9), there's no structural reason any of them would behave differently. Confirming this for real is
  implementation-time verification, not a design blocker.
- **`Compono.Generators`' new mode-detection logic is a small, real addition** — while low-risk (§5),
  it's still new incremental-generator surface that needs its own caching/determinism verification once
  built (standard practice for any `Compono.Generators` change, not a new category of risk this
  proposal introduces).
- **`xunit.v3.generatorutility`/`DataAttributeGenerator` was deliberately not adopted** — `Compono.Generators`
  reimplements the same emission shape directly rather than taking a new build-time dependency, since
  its own existing discovery pipeline is already more capable. This is a considered tradeoff (control
  and consistency with Compono's existing generator architecture vs. reusing xUnit's own scaffolding)
  worth recording, not an oversight.
- **A future xUnit major version could change or remove `RegisteredEngineConfig`'s current shape** —
  the same maintenance exposure every framework-integration package already carries (`Compono.MSTest`'s
  own 3.x/4.x binary-incompatibility discovery, ADR-0057 Amendment 1, is a real precedent for this
  class of risk); not a reason to avoid building it now.

## Provenance

- [RESEARCH-0031](0031-native-aot-framework-native-testing-admission-research.md) — frozen, established
  evidence this document builds on without re-deriving.
- [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md) — `RowInvokerRegistry`/`PlanCache<T>`'s
  existing generated-module-initializer pattern, the direct precedent this proposal's generator
  emission mirrors.
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs` — the existing, already-precedented
  attribute-metadata-name-based discovery pattern this proposal extends with a seventh constant.
- xUnit source, read directly this session: `src/xunit.v3.generatorutility/TheoryData/DataAttributeGenerator.cs`,
  `src/xunit.v3.core.aot/Configuration/RegisteredEngineConfig_aot.cs`,
  `src/xunit.v3.core/Configuration/RegisteredEngineConfig_reflection.cs` (all `github.com/xunit/xunit`).
- `github.com/xunit/samples.xunit`, `v3/AotCsvDataSource` — the official reference implementation this
  proposal's proof spike was modeled on directly.
- [ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md) (`Proposed`) and
  [PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md) (`Not Started`) —
  the durable decision record and execution tracker this research feeds.
