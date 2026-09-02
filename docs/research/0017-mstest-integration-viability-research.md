# [RESEARCH-0017] MSTest Integration Viability for Compono

**Status:** Research complete, Outcome A accepted in principle by the
requester. No ADR yet — scoped to research only, per the request that
produced it.

**Revision note (targeted follow-up, two evidence gaps closed):** (1) §5's
original MTP-only spike left the classic VSTest-adapter discovery/execution
behavior unverified; a second spike (§20) now closes that gap directly, and
the "registration factories are already expected to be side-effect-free"
claim used to soften the double-evaluation risk turned out **not** to be an
existing documented Compono contract — corrected below (§5/§19), with the
one real, narrower documented contract that does exist
(`ICompositionValueProvider`'s "safe to invoke repeatedly, including
concurrently" remark) cited in its place. (2) §16's original "MSTest 4.x
only" version recommendation is corrected to a evidence-driven minimum of
**MSTest 3.0.0** — a real capability matrix (new, below) shows every API
`Compono.MSTest` actually needs has been stable since `MSTest.TestFramework`
v1.2.1, so 4.x was not, in fact, required by the design; §14's dependency
choice is now stated as one concrete recommendation
(`MSTest.TestFramework`, confirmed via binary inspection to be the package
containing `ITestDataSource`/`TestMethodAttribute`, not `MSTest.TestAdapter`
or the `MSTest` umbrella package) rather than left as an "either/or."
**Neither correction changes Outcome A** — see the restated §22.

**Framing (explicitly different from RESEARCH-0016):** the working bias for
this research, set by the requester, is that Compono should add
`Compono.MSTest` before 1.0 unless MSTest's extension model makes a
high-quality integration genuinely infeasible or forces a violation of
Compono's architecture. The burden of proof is on **not** shipping it, not
on justifying shipping it. This document is written from that posture: it
looks for a real blocker, not for a reason the package might not be worth
building.

**Inputs carried forward, re-verified against MSTest specifically, not
re-litigated:**
- RESEARCH-0016 §8: MSTest's parameter-data mechanism is synchronous.
  **Re-verified below (§8) — still true**, and the specific reason is now
  MSTest's own, not inherited generically: `ITestDataSource.GetData`
  returns `IEnumerable<object?[]>`, not `Task<...>`/`ValueTask<...>`.
- RESEARCH-0015: Compono 1.0 is non-owning; composed values are never
  disposed by Compono. **Re-verified below (§12) — nothing about MSTest's
  lifecycle changes this.**

## 1. Desired consumer experience

The target syntax, closest to what `Compono.XunitV3`/`Compono.TUnit`
already offer and idiomatic for MSTest (confirmed in §4 that
`[DataTestMethod]` is being removed, so `[TestMethod]` is correct, not a
compromise):

```csharp
[TestClass]
public class OrderServiceTests
{
    [TestMethod]
    [Compose]
    public void Creates_service(
        SomeService sut,
        [Shared] SomeDependency dependency)
    {
        ...
    }
}
```

with the same generic-profile family as the other two packages
(`[Compose<TProfile>]`, `[Compose<TProfile, TConfig>]`), `[Shared]`,
`CompositionBuilder.Share<T>()` (core, framework-independent — nothing
`Compono.MSTest`-specific is needed for it to work, confirmed in §5), exact
registrations, constructor selection, and every integration package
(`Compono.TestDoubles`, `Compono.NSubstitute`, `Compono.Bogus`,
`Compono.Logging`, `Compono.DependencyInjection`, `Compono.Http`) working
unmodified, because none of them know or care which test-framework
attribute called `CompositionRow`/`Composer.CreateRow` (verified: none of
`src/Compono.TestDoubles`, `src/Compono.NSubstitute`, `src/Compono.Bogus`,
`src/Compono.Logging`, `src/Compono.DependencyInjection`, `src/Compono.Http`
reference `Compono.XunitV3`/`Compono.TUnit` — they register providers and
generator hooks against core `Compono` only).

This is idiomatic MSTest (`[TestMethod]` + a custom `ITestDataSource`
attribute, MSTest's own documented extension mechanism, §2) wearing
idiomatic Compono (`[Compose]`/`[Shared]`), not xUnit syntax forced onto a
different framework.

## 2. Current MSTest extension points (verified against current docs/source, not memory)

Verified against Microsoft's current MSTest documentation
(`learn.microsoft.com/.../unit-testing-mstest-writing-tests-data-driven`,
dated 2026-06-16, updated 2026-07-08) and `microsoft/testfx-docs`' RFC-005
(custom-data-source extensibility), not recalled from prior xUnit-v2-era or
older MSTest familiarity, matching the same discipline ADR-0022 applied to
xUnit v3.

- **Current stable version: MSTest 4.2.3** (verified directly — `dotnet new
  mstest` today generates a `.csproj` referencing `PackageReference
  Include="MSTest" Version="4.2.3"`). MSTest 4.4 is in preview as of
  August 2026.
- **`[TestMethod]` vs `[DataTestMethod]`:** `DataTestMethodAttribute`
  "provides no additional value over `TestMethodAttribute` and will be
  removed in a future version" — confirmed via `microsoft/testfx` issue
  #4166 ("[Breaking] Remove `[DataTestMethod]`") and analyzer
  **MSTEST0044** ("Prefer TestMethod over DataTestMethod"), which actively
  flags `[DataTestMethod]` usage today with an automatic code fixer to
  `[TestMethod]`. `[TestMethod]` alone is therefore not just sufficient but
  the actively-recommended current shape — see §4.
- **`ITestDataSource`** is the documented, current, first-party
  extensibility interface for a fully custom data-source attribute: two
  methods, `GetData(MethodInfo methodInfo) : IEnumerable<object?[]>` and
  `GetDisplayName(MethodInfo methodInfo, object?[]? data) : string?`.
  Confirmed as still current (not a legacy compatibility shim) — it's the
  mechanism the docs' own "Custom display names"/"Real-world example"
  sections build directly, dated with the rest of the 2026 documentation
  pass.
- **`DataRowAttribute`/`DynamicDataAttribute`** are the built-in
  higher-level attributes, both implemented in terms of `ITestDataSource`
  (`DynamicDataAttribute` implements `ITestDataSource`,
  `ITestDataSourceIgnoreCapability`, and
  `ITestDataSourceUnfoldingCapability`) — not a separate mechanism
  `Compono.MSTest` would need to reimplement or hook differently.
- **`TestDataRow<T>`** (MSTest 3.8+) is a richer per-row metadata wrapper
  (display name, test categories, ignore message) usable as a
  `DynamicData` return element. Not required for `Compono.MSTest`'s core
  scenario (§11 covers display names via `ITestDataSource.GetDisplayName`
  directly, which is simpler and already sufficient), but worth knowing as
  the modern idiom other MSTest code in a mixed codebase will already use.
- **`ITestDataSourceUnfoldingCapability`/`TestDataSourceUnfoldingStrategy`**
  (`Auto`/`Unfold`/`Fold`) control whether each row becomes an
  independently-runnable Test Explorer/TRX entry (`Unfold`, the default via
  `Auto`) or all rows collapse into one node (`Fold`). Directly relevant to
  §5/§9 — see below.
- **No separate "newer extensibility model" superseding `ITestDataSource`
  was found.** It remains the current, documented, actively-used
  mechanism; the 2026 docs pass adds `TestDataRow<T>` and the unfolding
  strategy as refinements on top of it, not a replacement.
- **AOT/source generation:** `MSTest.SourceGeneration` (added via the
  `MSTestSourceGenMode` property) discovers tests at compile time instead
  of runtime reflection; starting MSTest 4.3.2, `MSTestSourceGenMode`
  defaults to `ReflectionFree` for trimmed/Native AOT projects. This is
  MSTest's own test-discovery generator (finding `[TestMethod]`s), separate
  from and unrelated to `Compono.Generators`' composition-plan generation —
  the two operate on different concerns and don't conflict (§7).

## 3. Best `[Compose]`-style integration seam

**Option A (`ITestDataSource`-based attribute) is the correct and only
seam actually needed.** Verified directly via spike (§20):

- It receives the real `MethodInfo` for the decorated test method — the
  same input `Compono.XunitV3`'s `ComposeAttribute.GetData(MethodInfo, ...)`
  already receives, so `BindingPlan.Build(MethodInfo)` (parameter
  reflection, signature validation, `[Shared]` detection,
  `RowInvokers.Build` per parameter) is **directly reusable, unchanged** —
  see §6.
- It participates correctly in discovery (confirmed by spike: `dotnet test
  --list-tests` enumerated both spike test methods with `[Compose]`-style
  custom-display-name rows, no `[DataTestMethod]` needed).
- It can produce one row (`yield return new object?[] { ... }` once) — the
  same one-row-per-`GetData`-call shape `Compono.XunitV3`'s attribute
  already implements, just with a different collection interface
  (`IEnumerable<object?[]>` instead of
  `ValueTask<IReadOnlyCollection<ITheoryDataRow>>`).
- It controls display names via `GetDisplayName(MethodInfo, object?[]?)`,
  confirmed working in the spike (`PlainTestMethodWithCustomDataSource
  (Compono: composed-value-1)` appeared exactly as authored).
- It preserves per-row composition identity: nothing about `ITestDataSource`
  forces sharing state across rows or across `GetData` calls — a fresh
  `Composer.CreateRow(...)` per call (matching `Compono.XunitV3`'s own
  per-`GetData`-call `composer.CreateRow(testMethod.DeclaringType!)`) keeps
  `[Shared]`/`Share<T>()` scoped correctly to one row.
- It can surface composition failures as an ordinary thrown
  `CompositionException` from `GetData` — MSTest doesn't require a special
  result-wrapping shape here (confirmed by the docs' own `ITestDataSource`
  examples, which are ordinary iterator methods that could throw).

**Option B (custom `TestMethodAttribute`) is unnecessary and should be
rejected**, exactly per the request's own skepticism: nothing found in §2's
audit requires replacing MSTest's own test-execution mechanism. Every
Compono semantic the target experience (§1) needs — composed parameters,
`[Shared]`, `Share<T>()`, profile selection, failure diagnostics, display
names — is fully reachable through `ITestDataSource` alone. A custom
`TestMethodAttribute` would be strictly more invasive for zero additional
capability; not recommended.

**Option C (a newer extensibility model):** none found beyond
`ITestDataSource`/`TestDataRow<T>`/the unfolding-strategy capability
interfaces, all covered above.

## 4. `[TestMethod]` vs `[DataTestMethod]` — resolved

**`[TestMethod]` alone is correct, not merely sufficient.** Confirmed three
ways: (a) current MSTest docs use `[TestMethod]` + custom `ITestDataSource`
attributes throughout, never `[DataTestMethod]`; (b) analyzer MSTEST0044
actively recommends migrating away from `[DataTestMethod]`; (c) spike (§20)
compiled and ran a `[TestMethod]`-only method with a custom
`ITestDataSource` attribute with zero issues. Requiring both
`[DataTestMethod]` and `[Compose]` would be needless, actively
discouraged-by-the-framework ceremony. `ComposeAttribute` does not need to
derive from any MSTest attribute type (`ITestDataSource` is an interface
implemented on a plain `Attribute` subclass, matching every example in the
current docs) — no surprising inheritance tricks needed or beneficial.

## 5. CompositionRow lifecycle mapping

**This is the section closest to the request's flagged "main feasibility
gate," and it resolved cleanly, but with one real nuance MSTest introduces
that xUnit v3/TUnit do not.**

- `Composer.CreateRow(declaringType)` would be called once per `GetData`
  invocation, exactly mirroring `Compono.XunitV3.ComposeAttribute.GetData`
  (`ComposeAttribute.cs:152`). All parameters for that test method bind
  from that one `CompositionRow` within that one `GetData` call — proven
  architecturally sound already by ADR-0021/ADR-0022, nothing MSTest-specific
  changes this.
- **MSTest's documented behavior (confirmed via current Microsoft Learn
  docs, "Discovery and execution phases" section): `GetData` is
  evaluated once during *discovery* (before `AssemblyInitialize`/
  `ClassInitialize` run) to enumerate test cases, and — per the docs —
  "MSTest evaluates the data source again" during *execution*.** This is
  the opposite of `Compono.XunitV3`'s `SupportsDiscoveryEnumeration() =>
  false` design (ADR-0022's explicit choice to defer all composition to
  execution, specifically to avoid a double-composition/discovery-vs-execution-drift
  problem) and `Compono.TUnit`'s equally deferred `GenerateDataSources`
  factory (`yield return () => ComposeRow(...)`, invoked only when TUnit
  actually calls the returned `Func`). **`ITestDataSource` offers no
  discovery-deferral switch — `GetData` itself *is* the discovery-time
  enumeration call.**
- **Spike-verified for both runner modes now (§20, follow-up closes the
  original gap) — the finding is more precise than either the docs or the
  first spike pass suggested:**
  - **Modern MTP runner** (default MSTest 4.2.3 template): `dotnet test
    --list-tests` then a separate `dotnet test` showed **no additional
    `GetData` calls** beyond the discovery pass — confirmed again in this
    follow-up, unchanged from the original finding.
  - **Classic VSTest adapter** (`<UseVSTest>true</UseVSTest>`,
    `MSTest.TestAdapter` 4.2.3, `Microsoft.NET.Test.Sdk` 18.9.0 — see §20 for
    exact commands): **a single `dotnet test` invocation** (discovery and
    execution happening inside one testhost process, one `dotnet test`
    command with no prior `--list-tests`) also invoked `GetData` **exactly
    once per method** — matching the MTP runner, not the docs' "evaluates
    the data source again" framing. **But** running `dotnet test
    --list-tests` (a real discovery pass) and *then*, as a **separate later
    command**, running `dotnet test` (a real execution pass) invoked
    `GetData` **twice per method — once per process, two separate OS
    processes** (verified via distinct `Environment.ProcessId` values
    logged to a file that survives across process boundaries). This is the
    scenario the docs' framing actually describes and the one a real IDE
    workflow reproduces: Visual Studio's Test Explorer performs a discovery
    pass once (to populate the tree) and a *separate*, later execution pass
    when the user runs a test — these are genuinely two different
    invocations of the test host, not two evaluations inside one run.
  - **Corrected characterization:** double-evaluation is real, but it's a
    property of *how many times the whole discovery-then-execution pipeline
    is separately invoked*, not of MSTest secretly re-running `GetData`
    within a single logical test run. A CI pipeline that runs `dotnet test`
    once (the common case, either runner mode) gets exactly one `GetData`
    call per method. A Visual Studio Test Explorer session — discover once,
    run later, possibly repeatedly — can produce it more than once across
    that session's lifetime, specifically under the classic VSTest adapter.
- **The "registration factories are already expected to be side-effect-free"
  claim (previous pass of this section) is corrected — that framing
  overstated an existing Compono contract that does not exist.** Checked
  directly (search of `docs/public-api.md`, `docs/architecture.md`, every
  ADR under `docs/adr/` for "side-effect"/"deterministic"/"idempotent" near
  "Register"/"provider", and the XML doc comments on
  `CompositionBuilder.Register<T>(Func<ICompositionContext,T>)`/
  `Register<T>(Func<T>)` in `src/Compono/CompositionBuilder.cs:60-92`):
  **`Register<T>()`'s own public documentation states no purity,
  determinism, or repeat-invocation-safety contract at all** — its XML doc
  comment describes only what stage it participates in and the
  duplicate-registration conflict rule, nothing about side effects.
  **What *does* exist, and is the correct citation, is narrower and on a
  different type:** `ICompositionValueProvider`'s own `<remarks>`
  (`src/Compono/ICompositionValueProvider.cs:12-16`) states "An
  implementation must be safe to invoke repeatedly, including concurrently,
  once constructed" — a real, documented public contract, but for
  *providers*, not `Register<T>()` factories, and it promises **safety**
  (won't crash, corrupt state, or behave incorrectly under repeat/concurrent
  invocation), not **purity** (no observable side effects at all) — a
  provider satisfying this contract could still, say, increment a counter
  on every call and remain compliant. Separately, ADR-0019's own
  Consequences section (`docs/adr/0019-registrations-and-service-provider-injection.md:339-345`)
  records an **internal design assumption**, not a stated public promise:
  reproducible `Ordinal`/fork-key sequences across two independent
  `Create<T>()` calls with the same seed "assumes factories/rules are
  side-effect-free with respect to how many times they call
  `Resolve<T>()`" — a narrow claim about nested-`Resolve<T>()` call-count
  stability for the engine's own seed-derivation machinery, not a general
  "no observable side effects" promise a consumer's `Register<T>()` factory
  is bound by today.
- **Practical consequence for `Compono.MSTest`, stated honestly:** a
  consumer's `Register<T>()` factory or `ICompositionValueProvider` **may**
  be invoked more than once for what the consumer perceives as one eventual
  test case, specifically under the classic VSTest adapter across a
  discover-then-execute session. Compono does not today forbid, and has
  never asked consumers to avoid, an observable side effect in a
  registration factory (the user's own examples — a counter, a captured
  test harness, a prepared resource — are all currently legitimate,
  undocumented-as-unsafe uses). **This means MSTest-runner-driven repeat
  invocation is a genuinely new consequence for those consumers, not
  something an existing Compono contract already covers or excuses.**
  `[Shared]`/`Share<T>()` remain internally correct in each independently-
  created `CompositionRow` per `GetData` call (each call gets its own fresh
  row/graph, per §5's first bullet — sharing is never split across calls),
  so this is not a `[Shared]`-correctness problem; deterministic seeding
  (ADR-0009/ADR-0012) means two independently-composed rows for the same
  test case are logically equivalent (same seed → same generated values)
  even though they're different object instances — so the *values* a
  consumer sees are consistent across repeat evaluations, but any
  *observable side effect* the factory performs (I/O, a counter, external
  state mutation) genuinely happens more than once. No Compono contract
  promises otherwise. `Compono.MSTest`'s own package documentation must
  therefore state this plainly as new, package-specific guidance: *under
  some MSTest runner/IDE workflows, composition — including any
  registration factory or provider it invokes — may run more than once for
  what appears to be one test case; do not rely on a registration factory
  or provider running exactly once, and avoid observable side effects in
  one if this matters for your scenario.* This is stricter guidance than
  `Compono.XunitV3`/`Compono.TUnit` need to state (both structurally
  guarantee exactly one real composition per row) — a genuine,
  MSTest-specific cost, and, per §5's disposal note below, a discovery-time-
  created disposable value that's discarded without disposal is simply a
  restatement of RESEARCH-0015's already-accepted non-ownership stance, not
  a new problem this research needs to solve.
- **Deferring composition to execution time only — investigated, no
  supported mechanism found.** `ITestDataSourceUnfoldingCapability`/
  `TestDataSourceUnfoldingStrategy` (§2) control whether rows *display* as
  one collapsed node or several — they do not defer *when* `GetData` itself
  runs; `GetData` still must return real, already-materialized
  `object?[]` rows synchronously, at whatever point MSTest calls it. No
  supported MSTest mechanism was found for "declare a row now, materialize
  its values later at execution time" — achieving that would require
  abandoning `ITestDataSource` for the already-rejected Option B (a custom
  `TestMethodAttribute`/execution-layer override, §3) or serializing
  composed state across the discovery/execution process boundary (not
  reliably possible in general — composed values aren't required to be
  serializable). Per the request's own stated preference, documenting the
  limitation (previous bullet) is the correct answer, not building around
  it.
- **Exception-during-discovery behavior (confirmed from docs):** if
  `GetData` throws during the discovery pass, MSTest falls back to a
  single "folded" test node rather than failing cleanly per-row; the real
  exception then surfaces normally when execution's own pass re-evaluates
  `GetData`. So a genuine composition failure (bad registration, missing
  provider) still surfaces as a real, informative test failure at
  execution time — just with a less granular Test Explorer node during
  discovery. Not a correctness problem; a display/UX nuance worth
  documenting.
- **Side-effecting registrations "running more than once" — is this
  unsafe?** Not for anything Compono's pipeline itself does (composition
  is in-memory/CPU-bound, ADR-0010) — the risk is entirely in
  consumer-authored registration factories/providers that do I/O or mutate
  external state, which is already discouraged guidance for every existing
  integration, just newly load-bearing here.

## 6. Generator/binding architecture — highly reusable, confirmed by reading actual source

**`Compono.MSTest` can be a genuinely thin package, reusing nearly all
existing binding machinery unchanged**, verified by reading
`src/Compono.XunitV3/Binding/*.cs` directly (not inferred):

- **`RowInvokerRegistry`** (`src/Compono/RowInvokerRegistry.cs`) is core,
  framework-agnostic, `Type`-keyed, populated by generator-emitted module
  initializers — zero xUnit/TUnit awareness. `Compono.MSTest` reuses it
  exactly as-is; no new registry needed.
- **`CompositionRow`** (core, ADR-0021) is exactly the graph-lifetime
  abstraction `Compono.MSTest` needs — reused unchanged.
- **`BindingPlan`/`ParameterBindingPlan`/signature validation**
  (`src/Compono.XunitV3/Binding/BindingPlan.cs`) operates entirely on
  `System.Reflection.MethodInfo`/`ParameterInfo` — framework-agnostic
  inputs. This file's logic (parameter reflection, `[Shared]` detection,
  nullability inference, generic-method/ref-parameter rejection,
  duplicate-`[Shared]`-type rejection) is **directly portable to
  `Compono.MSTest`** — likely copied with the same "adapted, not
  byte-for-byte ported" posture `Compono.TUnit` already used relative to
  `Compono.XunitV3` (confirmed: `Compono.TUnit`'s own
  `ComposeAttribute.cs` doc comment says exactly this — "adapted from
  `Compono.XunitV3.ComposeAttribute`, not a byte-for-byte port"). Whether
  this becomes a genuinely shared internal library or stays duplicated
  per-package (as `[Shared]`'s own `SharedAttribute` already is,
  independently declared in both `Compono.XunitV3` and `Compono.TUnit`
  "per ADR-0040's own binding-logic decision," per RESEARCH-0014) is an
  implementation-plan question, not a viability question — both are proven
  workable precedents already in this repo.
- **`RowInvokers.Build`** (`src/Compono.XunitV3/Binding/RowInvokers.cs`) is
  an ordinary `Type`-keyed dictionary lookup against `RowInvokerRegistry`,
  zero reflection, zero xUnit-specific logic — copy or share verbatim.
- **What's genuinely framework-specific and must be newly written:** the
  `ComposeAttribute` class itself (implementing `ITestDataSource` instead
  of `DataAttribute`/`UntypedDataSourceGeneratorAttribute`), its
  `GetData`/`GetDisplayName` method bodies (MSTest's own shape:
  `IEnumerable<object?[]>` rather than `ValueTask<IReadOnlyCollection<ITheoryDataRow>>`
  or TUnit's `IEnumerable<Func<object?[]?>>`), and the seed-reporting
  mechanism (§11 — MSTest has no direct `Traits`-equivalent on
  `ITestDataSource` itself; `GetDisplayName`'s returned string is the most
  natural place for it, or a `TestProperty`/`TestContext.Properties` write
  if execution-time access is available — an implementation-plan detail,
  not a viability blocker).
- **No second composition engine would be created.** The architecture
  matches the request's own target shape exactly: MSTest extension point
  (`ITestDataSource`) → existing binding machinery (`BindingPlan`,
  `RowInvokers`, `CompositionRow`) → generated invocation (`RowInvokerRegistry`
  dispatch, already emitted by `Compono.Generators` today with no
  MSTest-specific generator work needed — the generator emits registrations
  keyed by `Type`, not by which integration package will consume them).

## 7. Reflection and Native AOT

- **MSTest itself hands `Compono.MSTest` a `MethodInfo`** (`ITestDataSource.GetData(MethodInfo)`)
  — this is framework-required metadata access, identical in kind to what
  `Compono.XunitV3` already receives and already treats as AOT-safe input
  (ADR-0022: "no additional reflection cost `Compono.Xunit` introduces
  beyond what xUnit itself already performs"). Reading `MethodInfo.GetParameters()`,
  attribute presence (`[Shared]`), and nullability metadata off it is
  exactly what `BindingPlan.Build` already does for xUnit v3/TUnit — not a
  new category of reflection.
- **No `MakeGenericType`/`Activator.CreateInstance`/dynamic generic
  instantiation would be needed.** `RowInvokerRegistry.TryGet` is a plain
  `Type`-keyed dictionary lookup (confirmed, `src/Compono/RowInvokerRegistry.cs`'s
  own doc comment: "this registry never does that \[`MakeGenericMethod`\] -
  every `Resolve<T>()`/... call a registered entry actually makes is
  written directly, with a compile-time-known `T`, in generator-emitted
  source"). `Compono.MSTest` would reuse this unchanged — the same
  reflection-free dispatch guarantee `Compono.XunitV3`/`Compono.TUnit`
  already rely on, not a fallback to a reflection-based composition
  engine.
- **MSTest's own AOT posture is compatible, not a constraint on Compono:**
  `MSTest.SourceGeneration` (compile-time test discovery, `MSTestSourceGenMode`
  defaulting to `ReflectionFree` for trimmed/AOT projects starting MSTest
  4.3.2) is MSTest discovering *its own* `[TestMethod]`s at compile time —
  an orthogonal concern to how `Compono.MSTest` composes parameters once a
  test is already found. No conflict identified between the two
  generators; each operates on a different part of the pipeline
  (MSTest.SourceGeneration: which methods are tests; Compono.Generators:
  how a given type gets constructed).
- **Conclusion: `Compono.MSTest` can honestly preserve Compono's existing
  reflection-free/source-generated posture**, on the same terms
  `Compono.XunitV3`/`Compono.TUnit` already do — framework-required
  metadata access via `MethodInfo`, never a fallback composition engine.

## 8. Async limitation — re-verified against MSTest specifically

RESEARCH-0016's general finding (async parameter-data sources are not
universally available) holds for MSTest, confirmed at the API-signature
level, not just by prior-research inheritance: `ITestDataSource.GetData`
returns `IEnumerable<object?[]>` — a plain synchronous enumerable, no
`Task`/`ValueTask` anywhere in the signature (confirmed directly from the
current Microsoft Learn interface table and the spike's own compiled
implementation). This is a **harder** constraint than xUnit v3's
`ValueTask`-returning `GetData` (RESEARCH-0016 §8 found xUnit v3's own
`GetData` *is already* `ValueTask`-returning, just synchronously-completed
today) — MSTest's extension point has no async door to leave open at all,
today.

Per RESEARCH-0016's already-settled principle: this is not, by itself, a
reason to reject `Compono.MSTest`. `[Compose]`-supplied MSTest parameters
would be synchronously composed, full stop; async setup belongs in MSTest's
own lifecycle (`[ClassInitialize]`/`[AssemblyInitialize]`/`TestContext`),
with the already-initialized resource registered into Compono
synchronously — the same boundary already recommended for xUnit
v3/TUnit/DI/`WebApplicationFactory` scenarios.

## 9. `DataRow`/`DynamicData` coexistence — spike-verified, not assumed

**Verified directly (§20): a `[DataRow(1)]` and a custom `[ComposeSpike]`
`ITestDataSource` attribute on the same method do *not* merge into one
row.** `dotnet test --list-tests` on a spike method
`MixedDataRowAndCustomSource(object)` decorated with both produced **two
independent test cases**:

```
MixedDataRowAndCustomSource (1)
MixedDataRowAndCustomSource (Compono: composed-value-2)
```

— i.e. MSTest runs the test once per data-source attribute, each attribute
supplying a *complete* row for every parameter, not a partial row Compono
could fill the gaps of. This settles §9's central question directly: **a
mixed "some parameters from `[DataRow]`, the rest from `[Compose]`" row is
not supported by MSTest's data-source model**, because `[DataRow]`'s
values and `[Compose]`'s composed values arrive as two competing complete
rows, not one merged row — the same structural limit exists for any two
`ITestDataSource`-family attributes on one method, not something specific
to `[DataRow]`.

**Classification (per the request's §9 framing):**
- **Cleanly supported for 1.0:** `[Compose]` as the sole data source on a
  method (§1's target shape) — this is the dominant, intended usage and
  works cleanly.
- **Fundamentally awkward/unsupported due to MSTest's model:** genuine
  per-parameter mixing of `[DataRow]`-supplied and `[Compose]`-composed
  values on one row. `Compono.XunitV3`/`Compono.TUnit` don't attempt this
  either (inline values are supplied through `[Compose(...)]`'s own
  constructor, not a separate `[InlineData]`/`[Arguments]`-style
  attribute) — so this isn't a regression relative to the existing
  packages' own scope, it's the same boundary, arrived at for a
  structurally different reason (MSTest's independent-row model vs.
  xUnit/TUnit's single-attribute-owns-the-row model).
- **Should deliberately not be supported:** attempting to make `[Compose]`
  "fill in" values `[DataRow]` didn't supply would require abandoning
  `ITestDataSource` for the rejected Option B (a custom
  `TestMethodAttribute`/execution-layer override) per §3 — not worth the
  architectural cost for a scenario `Compono.XunitV3`/`Compono.TUnit`
  don't support either.

This is the request's own §22 "Outcome B" trigger criterion
("mixed DataRow + composed parameters cannot be supported cleanly") — but
per §9's classification above, this is a *pre-existing* scope boundary the
other two packages share, not a new, `Compono.MSTest`-specific compromise.
See §22's outcome reasoning.

## 10. Multiple Compose/configuration attributes

- **One `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile,TConfig>]`
  attribute owns the entire row**, mirroring `Compono.XunitV3`/`Compono.TUnit`
  exactly — `AttributeUsage(AttributeTargets.Method, AllowMultiple = false)`
  on each, plus an explicit "more than one Compose-family attribute" check
  in `BindingPlan.ValidateSignature` (confirmed present in
  `src/Compono.XunitV3/Binding/BindingPlan.cs:101-104`, reusable logic for
  the MSTest port).
- **Profiles are specified via the generic attribute type argument**
  (`[Compose<TProfile>]`), not attribute stacking or ordering — this
  sidesteps MSTest attribute-discovery-order questions entirely, matching
  the request's own preference ("favor deterministic configuration
  independent of reflection-return ordering"). Class/assembly-level
  profile inheritance is not a feature of the existing two packages either
  (method-level only, per ADR-0022's Decision Outcome, "confirmed with the
  user"), so there's no new precedent to establish or break here — same
  scope as today.
- **Attribute ordering reliability:** not needed at all given the
  above — `Compono.MSTest` never needs to read "which attribute came
  first" because there is exactly one Compose-family attribute per method
  by construction (enforced), and profile selection is a type parameter,
  not a second attribute.

## 11. Display names and diagnostics

- **`ITestDataSource.GetDisplayName(MethodInfo, object?[]?)` is the exact
  hook needed**, spike-confirmed working
  (`PlainTestMethodWithCustomDataSource (Compono: composed-value-1)`
  appeared in `dotnet test --list-tests` output exactly as implemented).
  `Compono.MSTest` can produce a stable, non-huge-object-dump name (e.g.
  `"{methodName} (Compono)"` or `"{methodName} (Compono, seed: {seed})"`)
  the same way `Compono.XunitV3`'s trait-based seed reporting
  (`SeedTraitName`) already communicates a reproducible seed, just via a
  different MSTest-native surface (display name and/or
  `TestContext.Properties`/`TestProperty` if execution-time write access
  proves available — an implementation-plan detail).
- **Composition-failure surfacing:** an exception thrown from `GetData`
  during *execution* surfaces as a normal MSTest test failure with
  `Compono`'s own `CompositionException.Message` intact (ordinary .NET
  exception propagation — MSTest doesn't re-wrap `ITestDataSource`
  exceptions into an opaque generic message, per the docs' own framing of
  discovery-time exceptions "falling back to folded" rather than being
  swallowed). An exception thrown during *discovery* causes the
  Test-Explorer-visible "folding" behavior from §5 rather than an
  immediately-visible per-row error — a real but survivable diagnostics
  degradation (the *execution*-time re-throw still carries the full
  `CompositionException` message), not silent failure.

## 12. Test lifecycle and disposal boundary — RESEARCH-0015 re-confirmed for MSTest

No new ownership questions. `Compono.MSTest` must not, and by design would
not, dispose composed argument objects — matching `Compono.XunitV3`'s own
explicit stance (`ComposeAttribute.cs:130-142`'s remarks: `disposalTracker`
"deliberately never used to register a composed value," for exactly the
provenance-ambiguity reason RESEARCH-0015 documents at length). MSTest
exposes its own post-test lifecycle (`[TestCleanup]`,
`IDisposable`/`IAsyncDisposable` on the test class itself) — an available
future seam for a consumer's *own* disposal, same shape RESEARCH-0015 §6
already identified generically (a framework could, in principle, wire a
future opt-in `CompositionRow.DisposeAsync()` into its own lifecycle hook)
— but nothing here requires or should trigger designing that now. No
framework-owned resource (`TestContext`, any MSTest-internal type) would
ever be owned or disposed by `Compono.MSTest`'s row/graph — consistent with
RESEARCH-0015's Model A stance and RESEARCH-0016 §9's identical
"don't accidentally own a framework lifecycle" guidance for
DI/`WebApplicationFactory`.

## 13. `TestContext`/framework-owned values

**Recommendation: do not auto-inject `TestContext` (or any MSTest
framework value) as a composed parameter.** Verified this isn't even a gap
worth filling: MSTest already provides `TestContext` idiomatically via
constructor injection (MSTest 3.6+) or the classic `public TestContext
TestContext { get; set; }` property — a consumer who needs it already has
a clean, framework-native way to get it, with ownership unambiguously
MSTest's. `TestContext.CancellationToken` (superseding
`TestContext.CancellationTokenSource.Token`, flagged by analyzer
MSTEST0054) is reached the same way. Auto-composing either through
`[Compose]` would duplicate an existing, better-owned mechanism and blur
the "who owns this" line RESEARCH-0015/§12 above are careful to keep
clear — conservative, no-new-surface answer, matching the request's own
"be conservative" instruction.

## 14. Package shape and dependencies

- **Package name: `Compono.MSTest`**, matching the `Compono.XunitV3`/
  `Compono.TUnit` naming convention.
- **Dependency: `MSTest.TestFramework` — resolved to one concrete
  recommendation, not left as an either/or (follow-up correction).**
  Verified directly, not assumed: (a) `ITestDataSource`/`TestMethodAttribute`
  are compiled into `MSTest.TestFramework.dll`, confirmed by binary
  inspection of the installed NuGet package
  (`~/.nuget/packages/mstest.testframework/4.2.3/lib/net9.0/MSTest.TestFramework.dll`
  contains both type names); (b) a real spike project's
  `obj/project.assets.json` showed the umbrella `MSTest` package (what
  `dotnet new mstest` references by default) resolves to **four separate**
  NuGet packages transitively — `MSTest`, `MSTest.TestFramework`,
  `MSTest.TestAdapter`, and `MSTest.Analyzers` — confirming the umbrella
  package would pull `MSTest.TestAdapter` (a consumer test-project/runner
  concern) into every `Compono.MSTest` consumer's dependency graph
  unnecessarily, exactly the outcome the request said to avoid. **Concrete
  recommendation: `Compono.MSTest` should depend on `MSTest.TestFramework`
  only**, not `MSTest`, not `MSTest.TestAdapter` — mirroring how
  `Compono.XunitV3` depends on `xunit.v3.extensibility.core`/similar
  compile-time-only surface rather than the runner package.
- **Minimum supported MSTest version: revised to MSTest 3.0.0 (follow-up
  correction — the original "4.x only" recommendation was not, in fact,
  driven by any actual capability requirement).** See the capability
  matrix and reasoning in the restated §16 below.
- **Analyzer/source-generator interaction:** `MSTest.SourceGeneration`
  (§7) is opt-in, orthogonal, and consumer-configured — `Compono.MSTest`
  doesn't need to reference or coordinate with it; `Compono.Generators`
  already sees everything it needs (the composed types' own constructors)
  once `Compono.MSTest` is referenced, same as today for
  `Compono.XunitV3`/`Compono.TUnit`.
- Keep the dependency footprint to exactly `MSTest.TestFramework`(or `MSTest`)
  plus core `Compono` — no additional extension package identified as
  necessary.

## 15. Public API sketch (illustrative only, not an ADR-level design)

Mirroring `Compono.XunitV3`'s shape where semantics match, diverging only
where MSTest's model requires it:

```csharp
namespace Compono.MSTest;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : Attribute, ITestDataSource
{
    public ComposeAttribute(params object?[] inlineValues);
    public int Seed { get; set; }

    public IEnumerable<object?[]> GetData(MethodInfo methodInfo);
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data);
}

public class ComposeAttribute<TProfile> : ComposeAttribute
    where TProfile : ICompositionProfile, new();

public class ComposeAttribute<TProfile, TConfig> : ComposeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SharedAttribute : Attribute;
```

No public types beyond this are anticipated solely for generated code —
`RowInvokerRegistry`/`CompositionRow` are already public core types
`Compono.MSTest`'s internal `BindingPlan` (internal, like
`Compono.XunitV3`'s own) would consume, not new surface. Consistent with
`Compono.XunitV3`/`Compono.TUnit` where semantics match (attribute family
shape, `[Shared]`, `Seed`); the only structural divergence is
`ITestDataSource`'s synchronous `IEnumerable<object?[]>` return instead of
`ValueTask<IReadOnlyCollection<ITheoryDataRow>>`/`IEnumerable<Func<object?[]?>>`
— forced by MSTest's own extension-point signature, not a design choice.

## 16. Versioning and compatibility — revised (follow-up correction)

**The original "MSTest 4.x only" recommendation is corrected: it was not
actually driven by a capability requirement, and the evidence instead
supports MSTest 3.0.0 as the minimum.** Built directly from the current
official `testfx` changelog (`github.com/microsoft/testfx/blob/main/docs/Changelog.md`,
fetched and read directly, not recalled) and current Microsoft Learn API
reference pages' own "Applies to" version lists — not assumed:

| Capability | First available in | Does `Compono.MSTest`'s design (§15) actually need it? |
| --- | --- | --- |
| `TestMethodAttribute` | MSTest.TestFramework v1.x (predates the versioned API-reference history entirely) | **Yes — required, and satisfied since v1.** |
| `ITestDataSource` interface, `GetData(MethodInfo)`, `GetDisplayName(MethodInfo, object?[])` | **v1.2.1** — confirmed via the interface's own Microsoft Learn "Applies to" list, which enumerates every version from 1.2.1 through the current 4.3.0 with an *unchanged* two-member shape (no signature divergence found across that entire range) | **Yes — the core mechanism (§3). Required, and satisfied since v1.2.1.** |
| `DataRowAttribute`/`DynamicDataAttribute` (both `ITestDataSource`-derived) | Predates the versioned reference history, same vintage as `ITestDataSource` itself | Not consumed by `Compono.MSTest`'s own code — relevant only for §9's coexistence finding, which needs them to exist at all, not any particular version. |
| `TestContext` constructor injection | **v3.6.0** (2024-09-11, `testfx` changelog: "Feature: add support for injecting TestContext in ctor") | **No — §13 explicitly recommends against auto-injecting `TestContext`.** Not a `Compono.MSTest` dependency. |
| `TestDataRow<T>` | **v3.8.0** (2025-02-12, changelog: "Add TestDataRow class") | **No — §2/§15 already found `Compono.MSTest`'s own `GetDisplayName` is simpler and sufficient**; `TestDataRow<T>` is relevant only as "modern idiom other code in a mixed codebase will already use," not a package dependency. |
| `ITestDataSourceUnfoldingCapability`/`TestDataSourceUnfoldingStrategy` | **v3.7.3** (2025-01-27, per the enum's own Microsoft Learn "Applies to" list — the earliest version listed) | **No — §2 already found this "not required for `Compono.MSTest`'s core scenario."** |
| `[DataTestMethod]`→`[TestMethod]` analyzer/codefix (MSTEST0044-equivalent) | **v3.10.0** (2025-07-29, changelog: "Implement analyzer/codefix to move from DataTestMethodAttribute to TestMethodAttribute") | **No — this is guidance for *consumers* of `[DataTestMethod]`, not something `Compono.MSTest`'s own code depends on.** `[TestMethod]`-only has always compiled and run correctly (verified in the original §20 spike) — the analyzer is a later nudge toward already-correct behavior, not a prerequisite for it. |
| `MSTestSourceGenMode=ReflectionFree` default for AOT/trimmed projects | **v4.3.2** (2026-07-13, per §7's original finding) | **No — this is MSTest's own test-discovery AOT posture (§7), orthogonal to `Compono.MSTest`'s composition work, and a consumer's opt-in choice regardless of version.** |
| Breaking change to `ITestDataSource` itself between 3.x and 4.x | **None found** — the interface's "Applies to" list shows continuous, unchanged membership from v3.0.4 through v4.3.0 | N/A — confirms no forced-4.x floor from the interface's own evolution. |

**Conclusion: every capability `Compono.MSTest`'s actual design (§3/§13/§15)
needs — `TestMethodAttribute` and `ITestDataSource`'s two members — has
been stable since MSTest.TestFramework v1.2.1.** Nothing in the proposed
design touches `TestContext` constructor injection, `TestDataRow<T>`, the
unfolding-capability interfaces, or the `[DataTestMethod]` analyzer.
Recommending MSTest 1.x/2.x as the floor would still be irresponsible
despite this — those are genuinely legacy, pre-.NET-Core-consolidation
MSTest generations with no realistic current adoption signal to justify
carrying them, exactly the ancient-compatibility-for-its-own-sake the
request warns against.

**Revised recommendation: MSTest 3.0.0 (2022-12-06) as the minimum
supported version — not 4.x.** Reasoning, applying the request's own
"costs nothing, so support it" test directly: (a) 3.0.0 is the first
"modern" MSTest major — SDK-style, `.NET 6+`-aligned, the natural line
enterprise consumers who haven't yet moved to the newest major would
realistically still be on; (b) supporting it costs `Compono.MSTest`
literally nothing, because none of the newer-than-3.0 capabilities in the
table above are used by the design; (c) real, if narrowing, adoption
signal that 3.x is *recently* still-relevant, not ancient: the changelog
shows `MSTest.TestFramework` 3.11.1 shipped 2025-11-11 — the **same day**
as 4.0.2 — meaning the maintainers kept the two lines in parallel for over
a year after 4.0.0's 2025-10-07 release, not an immediate hard cutover.
**Caveat, stated honestly:** the changelog shows no 3.x patch release after
3.11.1 (2025-11-11) as of this research (current date 2026-08-28) — roughly
nine months with 4.x receiving multiple further releases (up to 4.3.3,
2026-07-28) and 3.x receiving none — so the 3.x line reads as de facto
frozen/unmaintained today, not actively patched. This is a real
consideration for a future ADR (a 3.0.0 floor means no upstream security
fixes land for the floor version itself), but it doesn't change the
capability analysis above: nothing about `Compono.MSTest`'s own design
requires 4.x, and the "no evidence an older major would materially
increase adoption" conclusion from the original pass is now **reversed**
by concrete data (3.x's parallel-maintenance period and its own recent
activity), not merely asserted differently. **The eventual ADR should
decide explicitly between "3.0.0, accepting the frozen-3.x caveat" and
"a narrower 3.6.0+/3.10.0+ floor for defensive reasons unrelated to
capability" — this research supplies the evidence for that decision but
does not resolve the maintenance-currency tradeoff itself**, since that is
a judgment call (adoption reach vs. supporting an unmaintained major), not
a capability question this research can settle unilaterally.

## 17. Dogfooding target

**No existing MSTest usage found in any repository available to this
research.** Checked, not merely assumed: `grep -rl` for
`Microsoft.VisualStudio.TestTools.UnitTesting`/`MSTest.TestFramework`/an
`MSTest` `PackageReference` across every repo under
`/Users/ncipollina/source/repos/layered-craft/` and
`/Users/ncipollina/source/repos/ncipollina/` (default checkouts) returned
nothing; also checked every local/remote branch name across those same
repos for anything mentioning `mstest` (applying the exact lesson
RESEARCH-0015 learned the hard way about `dynamodb-distributed-lock`'s
migration branch not being the default checkout) — none found.

Per the request's own instruction, **this is explicitly not evidence
against shipping `Compono.MSTest`** — ecosystem adoption, not existing
internal dogfooding, is this package's stated motivation (§18). A future
implementation plan should identify a small, dedicated MSTest dogfood
fixture (a purpose-built minimal consumer, not a synthetic single-file
example) rather than relying on migrating an existing repo, since none
exists to migrate.

## 18. Adoption significance

- **Raw NuGet download volume:** xUnit leads (~268M downloads, ~70K/day)
  and NUnit follows (~216M downloads, ~50K/day) ahead of MSTest by this
  specific metric, per current comparative sources.
- **Strategic/ecosystem significance independent of raw download rank:**
  MSTest is Microsoft's own first-party framework, ships with Visual
  Studio, and is tightly integrated into Azure DevOps test reporting —
  real enterprise-adoption signal that download-count alone understates
  (an enterprise consumer often pulls MSTest transitively via VS/ADO
  tooling defaults rather than an explicit, counted opt-in choice). MSTest
  v3/v4 has closed most historical feature gaps with xUnit/NUnit
  (`[TestMethod]`-only simplification, `TestDataRow<T>`, source-generated
  AOT discovery, full `Microsoft.Testing.Platform` support alongside every
  other major .NET framework per the "Microsoft.Testing.Platform: Now
  Supported by All Major .NET Test Frameworks" .NET blog post found in
  this research).
- **Conclusion: the user's stated expectation is confirmed, not
  overturned.** Lack of MSTest support is a real, defensible adoption
  barrier for a general-purpose .NET test-composition library — enterprise
  and VS/ADO-centric teams are a meaningful population `Compono` would
  otherwise categorically exclude, independent of exactly where MSTest
  ranks by download count.

## 19. Feasibility blockers — none found

Checked every example blocker the request names explicitly:

- MSTest **can** provide method parameters from a custom extension point
  compatible with Compono (§3, spike-confirmed).
- Composition executing during discovery does **not** make ordinary
  registrations/providers *unsafe* in the sense of producing incorrect
  values or corrupting state — `[Shared]`/`Share<T>()` stay correct within
  each independently-created row, and deterministic seeding means repeated
  composition is logically equivalent, not divergent (§5, follow-up
  correction). It **does** mean, honestly stated (§5, correcting the
  original pass's overstated purity claim), that a registration factory's
  own *observable side effects* (if it has any — Compono has no existing
  contract forbidding them) may run more than once under some runner/IDE
  workflows (specifically: classic VSTest adapter, discovery and execution
  as separately-invoked processes — not every `dotnet test` invocation,
  §5/§20). This is a documentable, new, MSTest-specific behavioral
  contract for `Compono.MSTest`'s own docs to state — not a correctness
  violation, and not something an existing Compono-wide guarantee already
  covers.
- Native AOT/source-generation goals do **not** need to be abandoned (§7)
  — `RowInvokerRegistry`'s existing reflection-free dispatch design
  carries over unchanged; only framework-required `MethodInfo` metadata
  access is introduced, the same category `Compono.XunitV3` already relies
  on.
- Correct `[Shared]`/row semantics **can** be maintained (§5/§6) — one
  `CompositionRow` per `GetData` call, same as the other two packages.
- The integration does **not** require deep dependence on unstable/internal
  MSTest APIs — `ITestDataSource`, `MethodInfo`, `TestDataRow<T>`, and the
  unfolding-strategy capability interfaces are all public, documented,
  stable surface.
- The resulting user experience (`[TestMethod]` + `[Compose]`) is **not**
  unlike normal MSTest — it's the framework's own currently-recommended
  data-driven-test shape, plus one attribute.

**Only a real, if narrow, cost was found:** the discovery/execution
double-evaluation risk under some runner modes (§5), and the inability to
mix `[DataRow]`-supplied and `[Compose]`-composed parameters on one row
(§9). Neither rises to the request's own bar for a blocker ("MSTest cannot
provide parameters... in a way compatible with Compono," "composition
would have to execute during discovery in a way that makes ordinary
registrations/providers unsafe," etc.) — both are documentable,
survivable limitations, not architectural violations.

## 20. Spike performed and exact results

**A real, throwaway MSTest 4.2.3 console test project was built** (outside
the repo, in the scratch directory, fully deleted afterward — `git status`
in the `compono` repo tree confirmed clean aside from this research
document, and the spike never touched anything inside the repo tree to
begin with). Contents: one custom `ComposeSpikeAttribute : Attribute,
ITestDataSource` with a static call-counter and a file-backed log (to
survive across process boundaries between `dotnet test --list-tests` and
`dotnet test`), applied to two `[TestMethod]`-only test methods (no
`[DataTestMethod]` anywhere) — one plain, one combined with a real
`[DataRow(1)]`.

**Exact observed results:**
- `dotnet build`: 0 warnings, 0 errors — `[TestMethod]` + custom
  `ITestDataSource` attribute compiles cleanly with no additional
  attribute required.
- `dotnet test --list-tests` (discovery only): enumerated
  `PlainTestMethodWithCustomDataSource (Compono: composed-value-1)`,
  `MixedDataRowAndCustomSource (1)`, and `MixedDataRowAndCustomSource
  (Compono: composed-value-2)` — three independent test cases from two
  `[TestMethod]`s, confirming (a) custom display names work exactly as
  authored, (b) `[DataRow]` and a custom `ITestDataSource` attribute on
  one method produce two separate rows, not a merged one (§9), and (c)
  `GetData` ran during discovery alone (log showed exactly 2 calls, one
  per method, before any test executed).
- `dotnet test` (full discovery + execution, same process, default MSTest
  4.2.3 template which uses `Microsoft.Testing.Platform`): 3 passed, 0
  failed; the file-backed log showed **no additional `GetData` calls**
  beyond the 2 already recorded during the preceding discovery-only run —
  i.e., under this specific default runner mode, discovery-time
  enumeration was reused for execution rather than re-evaluated.

### 20a. Follow-up VSTest-adapter spike (closes the gap the original pass left open)

**A second real, throwaway MSTest 4.2.3 project was built** (same scratch
directory, same cleanup discipline — fully deleted afterward, confirmed via
`git status`) to close the classic-VSTest-adapter question the first spike
explicitly left unverified. **Exact configuration:** `dotnet new mstest`
scaffold, `.csproj` edited to reference `MSTest.TestFramework` 4.2.3,
`MSTest.TestAdapter` 4.2.3, and `Microsoft.NET.Test.Sdk` 18.9.0 explicitly
(added because a bare `<UseVSTest>true</UseVSTest>` alone produced no test
output at all until `Microsoft.NET.Test.Sdk` was present — the classic
VSTest pipeline requires it, a real, if minor, additional finding), with
`<UseVSTest>true</UseVSTest>` in the `.csproj` (confirmed as the current,
documented mechanism via a live Microsoft Learn search, not assumed) to
force the legacy adapter instead of the MTP default. `dotnet` SDK
`11.0.100-preview.7.26381.103`. Three `[TestMethod]`s: the same
plain-composed and `[DataRow]`-mixed cases as the original spike, plus a
new `ThrowingComposeSpikeAttribute.GetData` that unconditionally throws, to
observe discovery-time exception behavior under VSTest specifically. The
custom attribute logs each `GetData` call, including `Environment.ProcessId`,
to a file in the OS temp directory (survives across separate process
invocations, unlike an in-memory counter).

**Exact observed results:**
- `dotnet build`: 0 warnings, 0 errors — confirms build output labeled
  "VSTest target(s)" (vs. the MTP template's different build target name),
  independent confirmation the classic adapter path was actually exercised,
  not silently still running MTP.
- `dotnet test --list-tests` (a real, separate discovery-only invocation,
  process A): enumerated the same five test cases the plain compile
  produced (including `ThrowingDuringDiscovery` — notably present, not
  suppressed, but with **no** composed display name, i.e. the "folded"
  single node the docs describe). Log showed exactly 2 `GetData` calls (one
  per working method) tagged with process A's PID.
- `dotnet test` run **as a separate, later command** (process B, a genuine
  second invocation, not a continuation of the discovery command): produced
  a normal MSTest failure report —
  `Failed ThrowingDuringDiscovery` / `Error Message: SPIKE: simulated
  composition failure during discovery` / a full stack trace naming
  `vstest_spike.ThrowingComposeSpikeAttribute.GetData` by exact type and
  line number, through MSTest's own
  `TestMethodRunner.TryExecuteFoldedDataDrivenTestsAsync` — **the original
  exception's message and type survive intact through MSTest's own
  wrapping**, confirming §11's diagnostics-quality finding for VSTest too,
  not just MTP. 4 passed, 1 failed (the deliberately-throwing case). The
  log showed **2 additional `GetData` calls, tagged with process B's PID**
  (a genuinely different OS process from process A) — **4 total calls
  across the two-command session for 2 real methods: a true double
  evaluation**, the first hard confirmation of the docs' "evaluates the
  data source again" framing found in this research.
- **Control run — a single, fresh `dotnet test` invocation with no prior
  `--list-tests` command** (one command, one process, both discovery and
  execution happening internally within that one invocation): produced the
  same failure report, and the log showed only **2 `GetData` calls total**
  (one per working method), both tagged with the *same* PID. **This
  isolates the actual cause precisely: it is not "VSTest always evaluates
  `GetData` twice per run" — it is "each separate `dotnet test`/Test-Explorer
  invocation performs its own discovery pass," and classic VSTest, unlike
  MTP, does not cache/reuse a prior separate invocation's discovery
  results.** A CI pipeline running `dotnet test` once, under either runner
  mode, gets exactly one `GetData` call per method. A Visual Studio Test
  Explorer session (discover once when the tree populates, execute
  separately and possibly repeatedly afterward) is the realistic scenario
  that reproduces the double-evaluation the docs describe, and it is
  specific to the classic VSTest adapter's lack of cross-invocation
  discovery-result reuse.
- **Spike fully removed** — `rm -rf` on the entire throwaway project
  directory; confirmed via `git status --short` in the `compono` repo
  showing only the three research documents (`0015`, `0016`, and this
  modified `0017`) as changed/untracked, nothing from either spike.

## 21. Documentation/skill implications (not applied — description only)

If Outcome A/B (§22) is accepted, the following would need updating,
matching RESEARCH-0015/0016's own "definition of done, not cleanup"
framing:

- **`docs/mvp.md`/roadmap** — add `Compono.MSTest` as a scoped package,
  same tier as `Compono.XunitV3`/`Compono.TUnit`.
- **Root `README.md`/package matrix** — add `Compono.MSTest` to whatever
  table already lists the framework-integration packages.
- **`docs/architecture.md`/`docs/public-api.md`** — wherever these
  currently enumerate supported test frameworks, add MSTest; check
  specifically whether either doc's current wording (e.g. "xUnit v3 and
  TUnit are supported") would become factually stale by omission once
  `Compono.MSTest` ships — this research did not find such wording stated
  as an exhaustive/closed list during its reading, but a future
  implementation pass should re-check at the point of writing, not assume.
- **`skills/compono/SKILL.md`** — add MSTest alongside the existing
  `xunit-v3.md`/`tunit.md` reference-loading pattern (a new
  `mstest.md` reference file, loaded only when `Compono.MSTest` is
  referenced/requested, matching the skill's existing scoping rule for the
  other framework references).
- **A new `docs/adr/00NN-compono-mstest-package-design.md`-equivalent
  ADR** (not written by this research) — would need to record the actual
  binding algorithm, seed-reporting mechanism, and the discovery/execution
  double-evaluation guidance from §5 as explicit, permanent package
  documentation (its own README), not just this research document. **Made
  more concrete by the follow-up (§5/§20a):** the package's own docs must
  state plainly, as part of `Compono.MSTest`'s documented behavioral
  contract (not buried in an ADR only), that under some MSTest workflows
  (specifically: the classic VSTest adapter, discovery and execution
  invoked as separate commands/sessions — the realistic Visual Studio Test
  Explorer pattern) composition — including any registration factory or
  provider it invokes — may run more than once for what appears to be one
  test case, and that Compono has no existing contract making an
  observable side effect in a registration factory safe to repeat; a
  consumer relying on exactly-once invocation needs to know this before
  adopting the package, not discover it later.
- **Examples/evals** — a `Compono.MSTest`-specific eval (mirroring
  RESEARCH-0014's "5/5 with-skill vs 2/5 without-skill" discriminating
  eval for `Share<T>()`) would be a reasonable completion bar for the
  eventual implementation plan.
- **Migration guidance** — a short "migrating an MSTest `[DynamicData]`-based
  test to `[Compose]`" note would help, mirroring
  `docs/migrating-from-autofixture.md`'s existing role for AutoFixture
  migrations, though no real MSTest consumer exists yet to validate it
  against (§17).

No existing documentation was found to be factually wrong about current
MSTest support during this research (there is no MSTest documentation yet
to be wrong) — no out-of-band doc corrections were needed or made.

## 22. Recommendation: **Outcome A — Add `Compono.MSTest` before 1.0**

**Reconfirmed after the follow-up (§5/§16/§20a): neither correction changes
this outcome.** The double-`GetData`-evaluation risk turned out to be more
precisely characterized (a real, confirmed, VSTest-specific effect of
separately-invoked discovery-then-execution sessions — not a per-run
guarantee-breaking property) and its previous justification (an assumed
Compono purity contract) turned out not to exist — but the corrected,
honest characterization is still a documentable limitation, not a
correctness violation or an architectural blocker, exactly as before. The
version-floor correction (MSTest 3.0.0, not 4.x) *expands* the package's
useful reach at zero design cost; it doesn't narrow feasibility in any way.
Both corrections make this research more trustworthy going into the ADR,
not less favorable to shipping.

A clean, maintainable integration is feasible, reusing nearly all of
Compono's existing binding architecture (`CompositionRow`,
`RowInvokerRegistry`, the `BindingPlan`/`RowInvokers` pattern) with only
framework-specific glue genuinely new. No blocker was found that meets the
request's own bar (§19). The two real limitations found — possible
double-`GetData`-evaluation depending on runner mode (§5) and no
`[DataRow]`+`[Compose]` per-parameter mixing (§9) — are documentable,
survivable, and, in the `[DataRow]`/`[Compose]` case, not even a
regression relative to `Compono.XunitV3`/`Compono.TUnit`'s own existing
scope (neither of those packages mixes inline framework-native data
attributes with composed parameters either, by a different but equally
firm design choice).

**This is not Outcome B.** The request's own Outcome-B trigger (mixed
`DataRow`+composed parameters can't be supported cleanly) is real (§9),
but per §9's own classification, that gap doesn't narrow `Compono.MSTest`'s
*core* value — the primary, intended `[Compose]`-as-sole-data-source
scenario (§1) is fully, cleanly supported, matching what
`Compono.XunitV3`/`Compono.TUnit` already ship as their own primary
scenario. Calling this "Outcome B, deliberately narrower scope" would
overstate the gap relative to the precedent the other two packages already
set for the exact same boundary — this is Outcome A with one documented,
inherent-to-MSTest's-model limitation, not a scope reduction relative to
Compono's own existing product shape.

**Preferred extension point:** `ITestDataSource`-implementing attribute
(§3, Option A) — not a custom `TestMethodAttribute`.

**Expected user-facing syntax:** `[TestMethod] [Compose]` (§1/§4) — no
`[DataTestMethod]` needed or wanted.

**Key limitations to document, not solve now:** composition (including any
registration factory/provider side effects) may run more than once for one
eventual test case under classic-VSTest-adapter discover-then-execute
sessions (real Visual Studio Test Explorer usage), spike-confirmed, not
excused by any existing Compono purity contract (§5/§20a); no per-parameter
mixing with `[DataRow]`/`[DynamicData]` (§9); synchronous-only composition
(§8, inherited from RESEARCH-0016, now MSTest-signature-confirmed).

**Likely package/API shape:** `Compono.MSTest`, referencing
`MSTest.TestFramework` only (confirmed via binary inspection — not
`MSTest.TestAdapter`, not the `MSTest` umbrella package, §14), API surface
per §15, binding logic adapted from (not a byte-for-byte port of)
`Compono.XunitV3`'s `BindingPlan`/`RowInvokers`/`ComposeAttribute` pattern,
targeting **MSTest 3.0.0** as the minimum supported version — revised from
4.x, evidence-driven via the capability matrix in §16, with the eventual
ADR still needing to weigh 3.x's current lack of active patch releases
against the adoption-reach benefit of a lower floor (§16's stated open
tradeoff).

**Dogfooding strategy:** no existing internal MSTest consumer exists (§17)
— build a small, dedicated dogfood fixture as part of the implementation
plan rather than deferring on the absence of one, since absence of a
dogfood repo is explicitly not evidence against shipping this package.

**What should feed the eventual ADR:** this research's §3 (extension-point
choice and why Option B was rejected), §5 (the corrected discovery/execution
evaluation-count finding and its exact documentation requirement), §6
(exactly which existing types are reused vs. newly written), §9 (the
`[DataRow]` mixing boundary and why it mirrors existing package scope
rather than narrowing it), §14 (the resolved `MSTest.TestFramework`-only
dependency), §16 (the MSTest 3.0.0 capability-driven floor), and §15's
illustrative API sketch as a starting point, not a final shape.

**One decision this research intentionally leaves open for the ADR, not
resolved here:** whether to accept MSTest 3.0.0 as the actual floor despite
its currently-unmaintained status (no patch since 2025-11-11, §16), or
choose a narrower, more defensively-current floor (e.g. 3.6.0+, aligned
with `TestContext` ctor-injection-era MSTest even though `Compono.MSTest`
doesn't use that feature, purely as a "recent enough to still receive
fixes" signal) — this is a maintenance-currency-vs-adoption-reach tradeoff,
not a capability question, and the research's job was to supply the
capability evidence (done), not make that judgment call.

## Evidence index

- `src/Compono/RowInvokerRegistry.cs` — core, framework-agnostic dispatch
  registry; read directly for §6/§7.
- `src/Compono.XunitV3/ComposeAttribute.cs`,
  `src/Compono.XunitV3/Binding/BindingPlan.cs`,
  `src/Compono.XunitV3/Binding/RowInvokers.cs` — read directly for §1/§3/§5/§6/§10.
- `src/Compono.TUnit/ComposeAttribute.cs` — read directly for §1/§6 (the
  "adapted, not byte-for-byte ported" precedent).
- [ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md),
  [ADR-0022](../adr/0022-compono-xunit-package-design.md) — read directly
  for §1/§3/§5/§6/§12.
- [RESEARCH-0015](0015-disposal-ownership-research.md),
  [RESEARCH-0016](0016-async-composition-viability-research.md) — carried
  forward per §8/§12, re-verified against MSTest specifically rather than
  restated.
- [Data-driven testing in MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests-data-driven)
  (Microsoft Learn, dated 2026-06-16, updated 2026-07-08) — fetched
  directly; primary evidence for §2/§3/§4/§5/§9/§11.
- [RFC-005: Framework Extensibility - Custom DataSource](https://github.com/microsoft/testfx-docs/blob/main/RFCs/005-Framework-Extensibility-Custom-DataSource.md)
  (`microsoft/testfx-docs`) — fetched directly; primary evidence for §2/§3.
- `microsoft/testfx` issue #4166 ("[Breaking] Remove `[DataTestMethod]`"),
  analyzer MSTEST0044 — primary evidence for §4.
- MSTest AOT/source-generation search findings (MSTest.SourceGeneration,
  `MSTestSourceGenMode`, Microsoft.Testing.Platform adoption) — primary
  evidence for §7/§18.
- MSTest `TestContext`/`CancellationToken` search findings (constructor
  injection since MSTest 3.6, analyzer MSTEST0054) — primary evidence for
  §13.
- A throwaway MSTest 4.2.3 spike project (built, run, deleted; never
  inside the repo tree) — primary evidence for §3/§4/§5/§9/§20.
- **(Follow-up)** A second throwaway MSTest 4.2.3 spike project, classic
  VSTest adapter (`<UseVSTest>true</UseVSTest>`, `MSTest.TestAdapter`
  4.2.3, `Microsoft.NET.Test.Sdk` 18.9.0; built, run, deleted; never inside
  the repo tree) — primary evidence for §5/§20a.
- **(Follow-up)** `src/Compono/CompositionBuilder.cs:60-92` (`Register<T>`
  XML docs, no purity contract found), `src/Compono/ICompositionValueProvider.cs:12-16`
  (the real "safe to invoke repeatedly, including concurrently" provider
  contract), `docs/adr/0019-registrations-and-service-provider-injection.md:339-345`
  (the narrower internal call-count-determinism assumption) — primary
  evidence for the §5/§19 purity-contract correction.
- **(Follow-up)** `github.com/microsoft/testfx/blob/main/docs/Changelog.md`
  (fetched directly), and the Microsoft Learn API reference "Applies to"
  version lists for `ITestDataSource` and `TestDataSourceUnfoldingStrategy`
  — primary evidence for §16's capability matrix.
- **(Follow-up)** Binary inspection of
  `~/.nuget/packages/mstest.testframework/4.2.3/lib/net9.0/MSTest.TestFramework.dll`
  (confirms `ITestDataSource`/`TestMethodAttribute` are compiled into
  `MSTest.TestFramework.dll`) and a spike project's
  `obj/project.assets.json` (confirms the `MSTest` umbrella package
  transitively resolves to four separate packages including
  `MSTest.TestAdapter`) — primary evidence for §14's dependency resolution.
- NuGet download/adoption comparative search findings — primary evidence
  for §18.
- Repo-wide search across `/Users/ncipollina/source/repos/layered-craft/`
  and `/Users/ncipollina/source/repos/ncipollina/` (default checkouts and
  all local/remote branch names) — primary evidence for §17 ("no MSTest
  usage found" is a checked, not assumed, finding).

## Links

- Feeds a future `Compono.MSTest` package-design ADR if Outcome A is
  accepted, per §21/§22's "what should feed the ADR" list — no ADR
  drafted by this research.
- Carries forward RESEARCH-0015 (§12) and RESEARCH-0016 (§8) without
  reopening either.
- Should inform a later NUnit-integration-viability research pass (if one
  is commissioned): §6's "highly reusable binding architecture" finding
  and §9's "independent-row, not merged-row" data-source model are both
  plausibly NUnit-relevant questions worth re-verifying independently
  rather than assuming NUnit's `ITestCaseSource`/`ValueSourceAttribute`
  model behaves identically to MSTest's `ITestDataSource` — a distinct
  interface with its own possibly-different discovery/execution contract.
