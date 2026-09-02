# [PLAN-0057] `Compono.MSTest` Package Design — Implementation Plan

**Status:** Done

**Implements:** [ADR-0057](../adr/0057-compono-mstest-package-design.md)
including Amendment 1 (`MSTest.TestFramework` minimum raised from `3.0.0`
to `4.0.0` — the two lines ship under different, binary-incompatible
assembly identities, found during this plan's own implementation)
(`Compono.MSTest` package: `ITestDataSource`-implementing `[Compose]`
attribute family, `MSTest.TestFramework`-only dependency at a `4.0.0`
floor, `CompositionRow`/`RowInvokerRegistry` reused unchanged, the
`BindingPlan`/`RowInvokers` pattern adapted package-locally rather than
extracted, `GetDisplayName`-based seed reporting, the documented
discovery/execution repeat-composition contract, no `TestContext`
auto-injection, no disposal ownership)

**Note:** ADR-0057 is `Accepted` as of this plan's creation (2026-09-02) —
implementation may begin. Amendment 1 (2026-09-02) raised the
`MSTest.TestFramework` floor from `3.0.0` to `4.0.0` after implementation
evidence showed the `3.x`/`4.x` lines are binary-incompatible; see this
plan's own Notes for the implementation-time finding that triggered it.

## Goal

```csharp
[TestClass]
public class OrderServiceTests
{
    [TestMethod]
    [Compose<NSubstituteTestProfile>]
    public void Saves_order(
        [Shared] IOrderRepository repository,
        CreateOrderHandler handler,
        PlaceOrder command)
    {
        handler.Handle(command);

        repository.Received(1).Save(Arg.Any<Order>());
    }
}
```

runs end-to-end under a real MSTest test host — the same scenario
`Compono.XunitV3.SampleTests.NSubstituteTests.Saves_order`/
`Compono.TUnit.SampleTests.NSubstituteTests.Saves_order` already prove,
reproduced a third time under `[TestMethod]` + `[Compose]`, with no
`[DataTestMethod]`, `repository` shared across `handler`'s own composed
constructor parameter via `[Shared]`, the row's seed visible in
`GetDisplayName`'s output under both MTP and the classic VSTest adapter,
and a generated-code-reachability proof that a type reached only through a
`Compono.MSTest`-attributed method's parameter still gets a generated
composition plan. Done means: `Compono.MSTest.nupkg` builds and packs,
every ADR-0057 behavioral contract (§1-§16) has a passing test or a
recorded real-run verification proving it, `Compono.MSTest` is reflection-
free/AOT-clean on the same terms as `Compono.XunitV3`/`Compono.TUnit`,
every documentation/skill/eval surface ADR-0057 §16 names is updated and
consistent with the shipped API, and a dedicated external MSTest
**packaged-consumer validation fixture** has been validated via
`scripts/dogfood-validate.sh` against freshly packed local packages — see
task group 15's own terminology note: this is real, pre-1.0 external
consumption validation, not product dogfooding (no real
LayeredCraft/ncipollina MSTest consumer exists to dogfood against yet).

## Scope

Per ADR-0057's Decision Outcome — carried forward exactly, not reopened
unless implementation exposes a genuine contradiction:

**In scope**: a new `Compono.MSTest` package (`ComposeAttribute`/
`ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>`/
`SharedAttribute`, §6's frozen public shape), its package-local
`BindingPlan`/`ParameterBindingPlan`/`PositionalArgumentBinder`/
`ConfigProfileBinder`/`RowInvokers` binding implementation (adapted from
`Compono.XunitV3`'s, dispatching through the existing, unchanged
`RowInvokerRegistry`/`CompositionRow`), the three-metadata-name
`Compono.Generators` discovery extension (§5 "Generator discovery"), MTP
and classic-VSTest-adapter support, `GetDisplayName`-based seed reporting,
the documented discovery/execution repeat-composition contract (§9),
`[DataRow]`/`[DynamicData]` independent-row coexistence (§10), the full
documentation/skill/eval/dogfooding completion-gate work ADR-0057 §16
requires as part of this feature's definition of done, and all repository
build/CI/packaging integration a new package needs.

**Explicitly deferred / non-goals** (per ADR-0057's own Deferred Decisions
section — not this plan's job to solve): per-parameter mixing of
`[DataRow]`/`[DynamicData]` with `[Compose]`; class/assembly-level
`[Compose<TProfile>]`; automatic `TestContext`/framework-value injection;
a supported deferred/lazy `ITestDataSource` evaluation mechanism; async
composition; extracting a shared `BindingPlan`/`RowInvokers` base across
`Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest` (revisit only if a
fourth framework, e.g. NUnit, is researched and shows the pattern actually
generalizes — not a task in this plan); any `Compono.MSTest`-owned
disposal mechanism; raising the `4.0.0` floor further without a concrete
missing-capability finding (the floor was already raised once, from
`3.0.0`, by ADR-0057 Amendment 1's binary-incompatibility finding — see
this plan's Notes).

**One PR for everything inside this repository.** Task groups 1-14 below —
the `Compono.MSTest` runtime package, its `Compono.Generators` discovery
extension, the full test suite, Native AOT/MTP/VSTest validation, and the
complete documentation/skill/eval synchronization — are one cohesive
feature with one public API boundary and one definition of done; they ship
as **one PR**, not split into phase-per-PR slices the way
[PLAN-0040](0040-compono-tunit-package-design.md) was (that plan predates
this repo's current "don't manufacture PR boundaries out of a single
package feature" guidance — see ADR-0057's own request framing). Task
group 15 (the external MSTest packaged-consumer validation fixture) is, by
its nature, a
separate repository's own history and cannot land in this repo's PR at
all — it follows [PLAN-0055](0055-compono-logging-testing-support-package-impl-plan.md)
task 18's precedent: this plan's `Status: Done` still requires it to be
substantially complete, but its own commits live outside this repo.

## Tasks

### 1. Package/project creation

- [x] `src/Compono.MSTest/Compono.MSTest.csproj` — `net8.0;net9.0;net10.0;net11.0`
      (ADR-0038's TFM window, matching every other integration package).
      `ProjectReference` to `..\Compono\Compono.csproj`
      (`PrivateAssets="none"`, per the real `Compono.XunitV3`/`Compono.TUnit`
      packaging lesson PLAN-0004 Phase 3/PLAN-0040 Phase 0 both record — do
      not repeat that mistake) and `PackageReference` to
      `MSTest.TestFramework` **only** — no `MSTest`, no `MSTest.TestAdapter`,
      no `Microsoft.NET.Test.Sdk` (ADR-0057 §7, floor `4.0.0` per
      Amendment 1). Same
      `PinProjectReferenceVersionsExact` MSBuild target every other
      integration project's `.csproj` carries. `InternalsVisibleTo` for
      `Compono.MSTest.Tests`.
- [x] `Directory.Packages.props`: add `MSTest.TestFramework`'s
      `PackageVersion` entry pinned at (or compatible with) the `4.0.0`
      floor ADR-0057 §7 (Amendment 1) accepts, and `Compono.MSTest`'s own entry
      (`Version="1.0.0"`, matching every other integration package's
      existing pattern — needed the moment any in-repo consumer project
      restores a real `PackageReference` to it, not just for the packed
      artifact itself).
- [x] `test/Compono.MSTest.Tests/Compono.MSTest.Tests.csproj` — this
      project executes as a real MSTest test run, so it needs the full
      test-execution chain `src/Compono.MSTest` deliberately doesn't carry:
      `PackageReference` to `Compono.MSTest` (`ProjectReference` in-repo)
      plus `MSTest` (the umbrella package) and `Microsoft.NET.Test.Sdk`.
      Confirm the exact required properties (MTP-runner opt-in/out,
      `<UseVSTest>`) against a real MSTest project template during
      implementation rather than guessing them here — both runner paths
      need to be exercisable from this project or a sibling (task group 8).
      Add `Directory.Packages.props` entries for `MSTest`/
      `Microsoft.NET.Test.Sdk` alongside `MSTest.TestFramework`. Add a
      `Compono.MSTest.Tests`-name exclusion to `test/Directory.Build.props`'s
      two `IsTestProject`-scoped `ItemGroup`s (the shared xUnit-v3-runner
      packages/global `using Xunit;` set that every other test project gets
      by default don't belong in an MSTest-run project — mirrors the
      `Compono.TUnit.Tests` exclusion PLAN-0040 Phase 0 added).
- [x] `Compono.slnx`: add both new projects.

### 2. Public API surface (ADR-0057 §6, frozen — no deviation without stopping to report)

- [x] `ComposeAttribute : Attribute, ITestDataSource` — `public
      ComposeAttribute(params object?[] inlineValues)`; `public int Seed
      { get; set; }` (non-negative-only contract, same as
      `Compono.XunitV3`/`Compono.TUnit`); `IEnumerable<object?[]>
      GetData(MethodInfo methodInfo)`; `string?
      GetDisplayName(MethodInfo methodInfo, object?[]? data)`.
      `[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]`.
      Does **not** derive from any MSTest attribute base type (§3).
- [x] `ComposeAttribute<TProfile> : ComposeAttribute where TProfile :
      ICompositionProfile, new()` — inline-value constructor pass-through,
      `sealed`. Confirm the inline-value constructor ships on the *base*
      type in this same task group, not deferred to a later one — moving it
      after a parameterless-only base ships would be a binary-compatibility
      break for anything already compiled against it (the exact mistake
      PLAN-0040's sixth Codex review round caught and fixed for
      `Compono.TUnit` — do not repeat it here).
- [x] `ComposeAttribute<TProfile, TConfig> : ComposeAttribute where
      TProfile : ICompositionProfile` (no `new()` constraint) — `public
      ComposeAttribute(params object?[] configArguments) : base()` (zero
      inline values passed to the base; `configArguments` stored in its own
      field, bound to `TConfig`'s single public constructor via a
      package-local `ConfigProfileBinder`, then `TProfile` constructed from
      that `TConfig` instance and applied via
      `CompositionBuilder.AddProfile`). Every test-method parameter is
      composed in full under this form — `configArguments` never bind to
      test-method parameters. Negative-seed validation runs before any
      config/profile binding is attempted (matching `Compono.XunitV3`'s
      `ApplyProfile` ordering, so a bad `TConfig`/`TProfile` shape combined
      with a negative configured seed reports the documented negative-seed
      diagnostic, not a binder failure with a bogus seed embedded).
- [x] `SharedAttribute` — `[AttributeUsage(AttributeTargets.Parameter,
      AllowMultiple = false)]`, package-local marker mirroring
      `Compono.XunitV3.SharedAttribute`'s shape and duplicate-shared-type
      validation.
- [x] Stacked Compose-family attribute validation: reject a method carrying
      more than one of `[Compose]`/`[Compose<TProfile>]`/
      `[Compose<TProfile, TConfig>]` — `AllowMultiple = false` is
      per-exact-type only, so nothing else stops two *different*
      Compose-family types stacking on one method. `MethodInfo` is directly
      available here (`ITestDataSource.GetData(MethodInfo methodInfo)` hands
      it over directly, unlike `Compono.TUnit`'s `DataGeneratorMetadata`
      indirection) — no reflection workaround needed to find it, unlike
      PLAN-0040 Phase 1's `ResolveMethodInfo` fallback.
- [x] `test/Compono.MSTest.Tests`: API-surface/approval test locking the
      exact four-type public shape (`ComposeAttribute`, `` ComposeAttribute`1``,
      `` ComposeAttribute`2``, `SharedAttribute`), matching
      `Compono.XunitV3.Tests`'/`Compono.TUnit.Tests`' existing pattern.

### 3. Binding implementation (`src/Compono.MSTest/Binding/*`)

- [x] `BindingPlan.cs`/`ParameterBindingPlan.cs`/`PositionalArgumentBinder.cs`
      — a package-local port of `Compono.XunitV3`'s own (ADR-0057's
      binding-logic decision: adapted, not shared core), operating on
      `MethodInfo`/`ParameterInfo` — the *same* input shape
      `Compono.XunitV3` already uses, so this is a closer, more direct port
      than `Compono.TUnit`'s `DataGeneratorMetadata`-based version was.
      Covers: parameter discovery via `MethodInfo.GetParameters()`,
      `[Shared]` detection, nullability inference, inline-value positional
      binding/precedence, generic-method rejection, `ref`/`out`/`in`/`params`
      rejection, `ref struct`/pointer-typed by-value-parameter rejection
      (the same dispatch-eligibility guard ADR-0041/PLAN-0041 added to
      `Compono.XunitV3`/`Compono.TUnit`'s own `BindingPlan` — carry it here
      from the start, don't rediscover it), duplicate-`[Shared]`-type
      rejection, more-than-one-Compose-family-attribute rejection (task
      group 2's last item).
- [x] `RowInvokers.cs` — built against core `Compono`'s existing
      `RowInvokerRegistry.TryGet` from its first commit (per
      [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)). **No
      throwaway `MakeGenericMethod`/`Delegate.CreateDelegate`-based version
      ships first** — `RowInvokerRegistry` already exists and is stable on
      `main`, so unlike PLAN-0040 (which had to wait on PLAN-0041 to merge
      first), this package can be built against it directly from the start.
- [x] `ConfigProfileBinder.cs` — package-local port of
      `Compono.XunitV3`'s (constructor-shape lookup for `TConfig`/`TProfile`
      via reflection, bounded to once per attribute instance by the same
      `Lazy<Composer>`-backed caching pattern, never on the repeated
      per-`GetData`-call path). Unsupported constructor shapes are a
      deterministic `CompositionException`, not a compile error, matching
      `Compono.XunitV3`'s documented behavior exactly.
- [x] `ComposeAttribute.GetData(MethodInfo)`: one `CompositionRow` per
      invocation (`composer.CreateRow(...)`), binds every parameter via
      `row.Resolve<T>()`/`row.ResolveShared<T>()`/`row.ShareExplicit<T>()`
      through `RowInvokers`, returns one `object?[]` row wrapped in a
      single-element `IEnumerable<object?[]>` (`ITestDataSource.GetData`'s
      contract — confirm during implementation whether MSTest expects
      exactly one row per `[Compose]` invocation or tolerates/requires a
      different cardinality; ADR-0057's target experience assumes one row
      per method). **No graph state shared across separate `GetData`
      calls** — no static/module-level row cache of any kind (ADR-0057 §9's
      contract depends on this).
- [x] Every `Resolve`/`ResolveShared`/`ShareExplicit` call wrapped to catch
      `CompositionException` and rethrow via
      `CompositionException.WithSeedInMessage(exception, row.Seed)` — the
      same unconditional, pasteable-seed guarantee `Compono.XunitV3`/
      `Compono.TUnit` already make (ADR-0057 §15).
- [x] `test/Compono.MSTest.Tests`: binding-plan unit coverage mirroring
      `Compono.XunitV3.Tests`' own — parameter resolution, inline-value
      precedence, `[Shared]`/duplicate-`[Shared]`-type validation,
      nullability, signature-validation errors (generic method, `ref`/
      `out`/`in`/`params`, `ref struct`/pointer by-value), stacked-attribute
      rejection, `CompositionException` seed enrichment. Hand-built
      `MethodInfo`/`ParameterInfo` fixtures are directly usable here (unlike
      `Compono.TUnit.Tests`, which needed `MethodMetadataFactory` — MSTest's
      `GetData(MethodInfo)` takes exactly what ordinary reflection already
      produces).

### 4. Generator discovery (`Compono.Generators`)

- [x] `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
      `src/Compono.Generators/ComponoIncrementalGenerator.cs`: three new
      metadata-name constants (`Compono.MSTest.ComposeAttribute`/`` `1``/
      `` `2``) and three new `SyntaxValueProvider
      .ForAttributeWithMetadataName` registrations, feeding the existing,
      already attribute-family-agnostic `ComposeMethodDiscovery
      .TransformMethod` — no fork or reimplementation of that method
      (ADR-0057 §5 "Generator discovery").
- [x] `test/Compono.Generators.Tests`: a snapshot test proving a concrete
      parameter type reachable *only* through a `Compono.MSTest`-attributed
      method's own parameter (no other discovery path in the same
      compilation) receives a generated composition plan and a
      `RowInvokerRegistry` registration — mirroring the equivalent
      `Compono.XunitV3`/`Compono.TUnit` regression coverage exactly.
- [x] A minimal local-feed packaged-consumer proof of this specific change,
      in this task group, not deferred — a `dotnet pack` → local-feed →
      real restore consumer (mirroring PLAN-0004 Phase 3/PLAN-0040 Phase
      0's precedent) using the unqualified `[Compose]` attribute against a
      type with no other discovery path, proving the packed
      `Compono.MSTest`/`Compono` dependency chain actually generates a plan
      for it — a snapshot test alone doesn't prove the real NuGet
      dependency chain works. Task group 9's packaged-consumer sample
      project can absorb this once it exists, but this task group needs its
      own minimal proof if sequencing puts generator work first.

### 5. Display name and seed diagnostics (ADR-0057 §15)

- [x] `GetDisplayName(MethodInfo methodInfo, object?[]? data)` returns
      `"{methodName} (Compono, seed: {seed})"` — the row's actual seed, not
      a placeholder. No `TestContext.Properties`/`TestProperty` usage for
      the primary seed-reporting path.
- [x] `test/Compono.MSTest.Tests`: unit coverage that `GetDisplayName`'s
      output contains the exact seed a given `[Compose(Seed = N)]`/
      generated-seed row used.
- [x] Real-run verification (task group 8) that the display name is
      actually visible in `dotnet test`/Test Explorer output under both MTP
      and the classic VSTest adapter — a design-time claim confirmed
      against a real runner, not assumed from the API contract alone.

### 6. `[DataRow]`/`[DynamicData]` coexistence (ADR-0057 §10)

- [x] `test/Compono.MSTest.Tests` or the sample project (task group 9):
      a method carrying both `[DataRow(...)]` and `[Compose]` produces two
      independent test cases, each supplying a complete row for every
      parameter — documents/proves the boundary rather than building any
      merging machinery. No new execution-layer code needed; this is a
      structural property of MSTest's own `ITestDataSource` model.

### 7. Discovery/execution repeat-composition behavior (ADR-0057 §9)

- [x] **Do not build any workaround** — no cross-process caching, no
      global/static row cache, no value serialization between discovery and
      execution, no custom `TestMethodAttribute`. Tests verify Compono's own
      semantics only: each `GetData` call gets a fresh, independent
      `CompositionRow`; `[Shared]`/`Share<T>()` stay correct *within* one
      call; two independently-composed rows for the same seed are logically
      equivalent in value even though they're distinct instances; an
      observable side effect in a `Register<T>()` factory or
      `ICompositionValueProvider` genuinely repeats if `GetData` is called
      more than once.
- [x] A real-run check (not a permanently-asserted unit test, since this is
      an MSTest-runner-workflow property, not something `Compono.MSTest`'s
      own code controls): confirm, once, the RESEARCH-0017 §20/§20a
      evidence still holds against whatever current MSTest/MTP/VSTest
      versions this plan actually builds against — single `dotnet test`
      under MTP: one `GetData` call; single `dotnet test` under classic
      VSTest: one call; separate `dotnet test --list-tests` (discovery) +
      `dotnet test` (execution) under classic VSTest: two calls. Record the
      actual observed result in this plan's Notes, matching PLAN-0040's own
      "prove once via a real run, then keep only a passing regression"
      pattern for its own runner-lifecycle finding.
- [x] Package README/skill content (task groups 11-12) states this contract
      as `Compono.MSTest`'s own documented behavior, not just this ADR's.

### 8. MTP/VSTest and version-compatibility validation

- [x] Confirm `Compono.MSTest` works correctly under **both** MTP (the
      `dotnet new mstest` default) and the classic VSTest adapter
      (`<UseVSTest>true</UseVSTest>`) — real runs, not assumed from
      `ITestDataSource` being runner-agnostic on paper. `Compono.MSTest`
      itself introduces no runner-selection logic or MTP-/VSTest-specific
      API of any kind; runner choice stays entirely the consumer project's
      configuration.
- [x] Validate the accepted `MSTest.TestFramework` `4.0.0` floor (ADR-0057
      §7 Amendment 1) actually restores and runs correctly for
      `test/Compono.MSTest.Tests` (or a dedicated compatibility-matrix
      sibling project/CI leg — `implement.md`'s call which), against both
      MTP and the classic VSTest adapter. **Do not raise the floor further**
      merely because a later `4.x` API would be convenient — if
      implementation genuinely cannot satisfy the ADR within the `4.0.0`
      capability set, stop and report the exact missing API/behavior before
      touching the floor again, the same discipline that produced
      Amendment 1 itself.
- [x] Validate current `MSTest.TestFramework` `4.x` (the latest patch as of
      implementation time, e.g. `4.3.3`) against both MTP and the classic
      VSTest adapter — both ends of the `4.x` line must pass the same
      behavioral suite. `MSTest.TestFramework` `3.x` is **not** validated or
      supported (ADR-0057 §7 Amendment 1) — do not add a `3.x` compatibility
      leg.
- [x] The full compatibility matrix this task group validates:
      `MSTest.TestFramework` `4.0.0` × MTP, `4.0.0` × classic VSTest,
      current `4.x` × MTP, current `4.x` × classic VSTest. If the floor and
      "current" happen to be the same version at implementation time, note
      that explicitly rather than silently running a redundant fifth/sixth
      leg.
- [x] Record the actual compatibility matrix exercised (MSTest version ×
      MTP/VSTest) in this plan's Notes once run for real.

### 9. Packaged-consumer sample project (`test/Compono.MSTest.SampleTests`)

- [x] A real packaged-consumer project (mirroring PLAN-0004 Phase 3/
      PLAN-0040 Phase 0/2's precedent exactly) exercising the *complete*
      attribute family (`[Compose]`/`[Compose<TProfile>]`/
      `[Compose<TProfile, TConfig>]`/`[Shared]`) through the actual packaged
      `Compono.MSTest` → `Compono` dependency chain, not
      `Compono.MSTest.Tests`' own `ProjectReference`-based calls — a
      `ProjectReference` doesn't propagate `Compono.Generators` as an
      analyzer the way a packed nupkg's `analyzers/dotnet/cs` delivery does
      (the same constraint `testing.md` documents and PLAN-0040 Phase 0 hit
      directly), so any test needing a real generated plan for a genuinely
      composed custom type belongs here, not in `Compono.MSTest.Tests`.
- [x] Includes the `NSubstituteTests.Saves_order`-shaped scenario from this
      plan's Goal section, run for real (needs `Compono.NSubstitute` as an
      additional project dependency, matching the xUnit v3/TUnit sample
      projects).
- [x] Includes `ConfigProfileTests`-shaped coverage for
      `[Compose<TProfile, TConfig>]`, mirroring
      `Compono.XunitV3.SampleTests`/`Compono.TUnit.SampleTests`.

### 10. Native AOT / reflection-free validation

- [x] Confirm `Compono.MSTest`'s own code contains no `MakeGenericType`,
      `Activator.CreateInstance` (beyond `ConfigProfileBinder`'s existing,
      already-accepted bounded reflection pattern — see below),
      `MakeGenericMethod`, or reflection-based fallback construction
      outside the `MethodInfo`/`ParameterInfo` metadata access `ADR-0057
      §14 explicitly accepts as framework-required, not a new reflection
      category.
- [x] `ConfigProfileBinder`'s `TConfig`/`TProfile` construction needs the
      same AOT gate ADR-0041 Amendment 1 already required for
      `Compono.TUnit`'s equivalent binder — verify for real (don't assume
      `ConstructorInfo.Invoke` on an already-known, non-generic `Type` is
      automatically trim-safe); add
      `[DynamicallyAccessedMembers(...PublicConstructors)]` annotations if
      the real publish-and-run proof surfaces a trim/AOT failure, matching
      the fix PLAN-0040 Phase 1's real implementation already had to apply.
- [x] A dedicated `test/Compono.MSTest.AotSmokeTest` (or equivalent)
      project, `dotnet publish -c Release -p:PublishAot=true` + run,
      exercising the real, packaged `Compono.MSTest.ComposeAttribute
      .GetData` path directly (not a hand-rolled stand-in for it) —
      composing both a custom type and a provider-resolved leaf type,
      through both the no-profile and `[Compose<TProfile, TConfig>]` forms.
      `-p:TrimmerSingleWarn=false` pass: zero warnings attributable to
      `Compono.MSTest`'s own shipped code.
- [x] Source-level guard (a simple text/syntax scan over
      `src/Compono.MSTest/**/*.cs`, mirroring `Compono.Logging`'s own
      guard) that fails the build if `MakeGenericType`,
      `Delegate.CreateDelegate`, `DynamicMethod`, or
      `System.Linq.Expressions` ever appears — so a future change can't
      silently reintroduce a reflection-dispatch path this design rejected.

### 11. Build/CI infrastructure wiring

Creating the projects alone leaves them outside every place this repo's
build/release/validation pipeline enumerates packages by name:

- [x] `Compono.slnx` — the two core project entries added (task group 1).
      The sample/AOT-smoke projects (`Compono.MSTest.SampleTests`,
      `Compono.MSTest.AotSmokeTest`) are deliberately **not** added, matching
      `Compono.TUnit.SampleTests`/`Compono.TUnit.AotSmokeTest`'s own existing
      precedent — confirmed by `Compono.slnx`'s own comment block: these are
      manual, one-shot/local-feed-driven proofs run outside `dotnet build
      Compono.slnx`, not part of the ordinary solution-wide build.
- [x] `.github/workflows/docs.yml` — `src/Compono.MSTest/**` added to both
      `paths:` trigger lists, `Compono.MSTest` added to the API-reference
      build loop.
- [x] `.github/workflows/package-validation.yaml` — `Compono.MSTest` added
      to its `for pkg in ...` loop and explicit `pack_one`/path lists.
- [x] `.github/scripts/inspect-packed-nupkgs.sh` — `Compono.MSTest` added,
      with its own expected-dependency-set `case` branch (`Compono` +
      `MSTest.TestFramework`, no embedded `Compono.Generators.dll`).
- [x] `.github/scripts/generate-api-reference.sh` — `Compono.MSTest` added
      to `integration_pkgs`.
- [x] Confirm, directly (real local pack + restore, not assumed), that a
      consumer referencing only `Compono.MSTest` (pulling `Compono` in
      transitively) receives `Compono.Generators.dll`'s execution with zero
      extra steps.

### 12. Documentation (ADR-0057 §16 — completion-gate work, not cleanup)

- [x] `docs/packages/compono-mstest.md` (new) — following
      `docs/packages/compono-tunit.md`'s shape: `[TestMethod]` + `[Compose]`
      is the intended syntax (`[DataTestMethod]` unnecessary and actively
      discouraged, `MSTEST0044`); the full attribute family with worked
      examples; `[Shared]`/`Share<T>()`; the discovery/execution
      repeat-composition contract (§9), stated as this package's own
      documented behavior; `[DataRow]`/`[DynamicData]` independent-row
      boundary (§10); synchronous-only composition; non-ownership/no
      disposal (§12); `TestContext` remains MSTest-owned, no auto-injection
      (§13); MTP and VSTest both supported, MTP modern/preferred but not
      required (§8); `MSTest.TestFramework` `4.0.0` floor and current `4.x`
      compatibility (§7, Amendment 1) — state plainly that `MSTest` `3.x` is
      not supported and a `3.x` consumer must upgrade to `4.x`, not just
      omit `3.x` silently; `GetDisplayName`-based seed reporting (§15).
- [x] `docs/packages/index.md` — add `Compono.MSTest`'s row.
- [x] `README.md` and `docs/index.md` — add `Compono.MSTest`'s row to both
      front-door package tables (confirmed by direct read, not assumed —
      `docs/index.md` was already stale relative to `README.md`, missing
      `Compono.Http`/`Compono.Logging` too; added all three rather than
      leaving an inconsistent table next to the new one).
- [x] `docs/mvp.md` — **deviation, recorded here rather than silently
      skipped**: confirmed by direct read that `docs/mvp.md` is a closed
      historical record of the original MVP milestone (Compono/XunitV3/
      NSubstitute/Bogus only) — none of `Compono.TUnit`/`TestDoubles`/
      `DependencyInjection`/`Http`/`Logging` were ever added there either.
      `docs/roadmap/future-packages.md` is the doc actually live-tracking
      "scoped pre-1.0 package" status (its own "graduated from this page's
      roadmap" pattern for `Compono.TUnit`/`TestDoubles`/`Http`) — updated
      that instead: `Compono.MSTest` removed from "Admitted candidates",
      its own graduation paragraph added matching the others' shape, and
      the "No committed sequence" section's stale two-candidate reference
      corrected to the one remaining (`Compono.NUnit`).
- [x] `docs/architecture.md`/`docs/public-api.md` — wherever these
      enumerate supported test frameworks or package guides, add MSTest;
      verify by direct read whether either currently states an
      exhaustive/closed framework list that would become stale by omission
      (`docs/public-api.md`'s "Package Guides" bullet is a known such spot,
      per its current `Compono.XunitV3`, `Compono.TUnit`, ... enumeration).
- [x] `docs/concepts/shared-values.md`/`docs/getting-started/installation.md`
      or equivalent how-to pages — extend to name `Compono.MSTest`
      alongside the existing framework packages, following PLAN-0040
      Phase 0's own precedent for exactly this kind of stale-doc sweep.
- [x] A short "migrating an MSTest `[DynamicData]`-based test to
      `[Compose]`" migration note — folded into
      `docs/packages/compono-mstest.md`'s own "Migrating from
      `[DynamicData]`" section rather than a separate top-level file
      (`docs/migrating-from-autofixture.md` is a framework-agnostic,
      cross-cutting migration; this one is MSTest-specific and belongs with
      the package guide it's about) — explicitly acknowledges no real
      internal MSTest consumer existed to validate it against until task
      group 15's external packaged-consumer validation fixture.
- [x] Public-API/reference regeneration (`docs/reference/api`) — per
      ADR-0032, regenerate `docs/reference/api/Compono.MSTest/` as part of
      this PR.

### 13. Skill/reference synchronization

- [x] `skills/compono/SKILL.md` — add `Compono.MSTest` to the
      package-enumeration sentence; add a `.csproj`-detection row to the
      Detection table (`<PackageReference Include="Compono.MSTest"` →
      `[TestMethod]` + `[Compose]` available, load `references/mstest.md`);
      add a `references/mstest.md` row to the references-index table;
      remove `Compono.MSTest` from any "don't invent an unshipped package"
      guardrail's named-absent list, if one exists.
- [x] `skills/compono/references/mstest.md` (new) — matching
      `xunit-v3.md`/`tunit.md`'s depth: `[TestMethod]` + `[Compose]` is the
      intended syntax; `[DataTestMethod]` unnecessary; `[DataRow]`/
      `[DynamicData]` do not merge with `[Compose]`; composition is
      synchronous; Compono does not own/dispose composed values;
      `TestContext` remains MSTest-owned; MTP and VSTest both supported,
      MTP modern/preferred but not required; `GetData`/registration
      factories may run more than once across MSTest discovery/execution
      sessions and consumers must not rely on exactly-once invocation;
      deterministic seeds make repeated rows logically reproducible, not
      the same object graph.

### 14. Eval coverage

- [x] `skills/compono-evals/evals.json` — a `Compono.MSTest`-specific
      discriminating eval (mirroring RESEARCH-0014's `Share<T>()` eval
      pattern and PLAN-0040's own routing-eval precedent) distinguishing at
      least one genuine MSTest-specific trap: unnecessary `[DataTestMethod]`
      usage, assuming `[DataRow]` partially merges with `[Compose]`,
      assuming MTP is required, assuming `GetData`/factories run exactly
      once, or trying to auto-compose `TestContext`. Keep it focused — one
      or two scenarios proving the skill/reference material actually
      teaches framework-specific behavior, not a broad testing project.
- [x] Run the existing before/after benchmark harness for the new eval(s)
      against the updated skill, recording the result per
      `skills/compono-evals`' established convention —
      `skills/compono-evals/benchmarks/2026-09-02/README.md` (9/9 with
      skill, 3/9 without; graded by this implementing session directly,
      same methodology limitation the 2026-08-28 benchmark already
      records, not a new one).

### 15. Dedicated external MSTest packaged-consumer validation fixture (separate repo — see Scope's PR-sequencing note; not true product dogfooding — see the fixture's own terminology note below)

ADR-0057 §16 requires this as a real consumer-validation target, not an
internal-unit-test substitute — no existing LayeredCraft/ncipollina MSTest
consumer exists today (checked including branches, per RESEARCH-0017 §17).

**Terminology note (2026-09-02 clarification):** this fixture is **external
packaged-consumer validation**, not true product dogfooding. Dogfooding
implies validating against a real, pre-existing application that actually
depends on the package for its own purposes — no such MSTest consumer
exists in any LayeredCraft/ncipollina repository, and this task does not
manufacture one by migrating a real application to MSTest solely to create
that appearance. What this task group does provide, and is real: a
dedicated fixture that lives outside `Compono.MSTest`'s own implementation,
consumes freshly packed local NuGet packages (never `ProjectReference`),
is validated through `scripts/dogfood-validate.sh`, exercises realistic
`Compono.MSTest` usage, and validates MTP/VSTest where practical — genuine
pre-1.0 external-consumption validation. The distinction matters for the
final report: **external packaged-consumer validation completed before
1.0** is what this task group delivers; **true dogfooding is unavailable**
because no real MSTest consumer currently exists. If one appears in the
future, that can provide genuine dogfooding evidence then — this task
group's own fixture is not a substitute for that, and should not be
described as if it were.

- [x] Create a small, dedicated, purpose-built MSTest consumer repository
      (its own `git init`, own `Directory.Packages.props` —
      `scripts/dogfood-validate.sh` requires a real git repo at
      `--consumer-repo` with its own `Directory.Packages.props`, per its
      existing `--consumer-repo`/`--consumer-solution` contract; confirm
      during implementation whether it needs any additive extension beyond
      pointing `--consumer-repo`/`DOGFOOD_CONSUMER_REPO` at the new fixture
      and `--packages`/`DOGFOOD_PACKAGES` at `"Compono Compono.MSTest
      Compono.NSubstitute Compono.Logging"` — the script's own
      `--packages` generalization (PLAN-0051 task 11) already supports an
      arbitrary package set, so no script change may be needed; only add
      one if a real gap surfaces). A single, purpose-built consumer
      project, not a synthetic one-file example.
- [x] Exercise at minimum: ordinary composition; profiles; `[Shared]`;
      `Share<T>()`; `Register<T>()`; constructor selection;
      `Compono.TestDoubles` integration; `Compono.Logging` integration
      where appropriate; deterministic seed reproduction; diagnostics; the
      `4.0.0` version floor; current MSTest `4.x`; MTP execution; VSTest
      execution where practical.
- [x] Validate via `scripts/dogfood-validate.sh` against freshly packed
      local packages, never `ProjectReference`s or stale package artifacts
      — the same discipline every other package's dogfood validation
      follows.
- [x] Record the fixture's repo location/link and the validation result in
      this plan's Notes once run for real.

## Critical Files

- `src/Compono.MSTest/Compono.MSTest.csproj` — new
- `src/Compono.MSTest/ComposeAttribute.cs`,
  `ComposeAttribute{TProfile}.cs`, `ComposeAttribute{TProfile,TConfig}.cs`,
  `SharedAttribute.cs` — new
- `src/Compono.MSTest/Binding/BindingPlan.cs`, `ParameterBindingPlan.cs`,
  `PositionalArgumentBinder.cs`, `ConfigProfileBinder.cs`, `RowInvokers.cs`
  — new. `RowInvokers.cs` built against core `Compono`'s existing,
  unchanged `RowInvokerRegistry` from its first commit; the rest is a
  package-local port of `Compono.XunitV3`'s equivalent files (ADR-0057's
  binding-logic decision).
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `src/Compono.Generators/ComponoIncrementalGenerator.cs` — modified
  (three new metadata-name constants/registrations for `Compono.MSTest`'s
  attribute family)
- `test/Compono.MSTest.Tests/*` — new
- `test/Compono.MSTest.SampleTests/*` — new (packaged-consumer proof)
- `test/Compono.MSTest.AotSmokeTest/*` — new
- `test/Compono.Generators.Tests/*` — modified (new snapshot test for
  `Compono.MSTest`-only-reachable discovery)
- `Compono.slnx` — modified
- `test/Directory.Build.props` — modified (`Compono.MSTest.Tests`-name
  exclusion from the xUnit-v3-specific `ItemGroup`s)
- `Directory.Packages.props` — modified (`MSTest.TestFramework`/`MSTest`/
  `Microsoft.NET.Test.Sdk`/`Compono.MSTest` `PackageVersion` entries)
- `.github/workflows/docs.yml`, `.github/workflows/package-validation.yaml`,
  `.github/scripts/inspect-packed-nupkgs.sh`,
  `.github/scripts/generate-api-reference.sh` — modified
- `docs/packages/compono-mstest.md` — new
- `docs/packages/index.md`, `docs/mvp.md`, `docs/architecture.md`,
  `docs/public-api.md`, `docs/concepts/shared-values.md`,
  `docs/getting-started/installation.md`, `README.md`, `docs/index.md`,
  `docs/migrating-from-autofixture.md`-equivalent migration note — updated
- `docs/reference/api/Compono.MSTest/` — new (regenerated)
- `skills/compono/SKILL.md`, `skills/compono/references/mstest.md`,
  `skills/compono-evals/evals.json` — new/updated
- A new external MSTest dogfood fixture repository — outside this repo,
  see task group 15

## Test Plan

Per `testing.md`'s established pattern: unit coverage for the binding plan
and `ConfigProfileBinder`/seed logic in isolation
(`test/Compono.MSTest.Tests`), generator-discovery snapshot coverage
(`test/Compono.Generators.Tests`), a packaged-consumer end-to-end suite
proving the full attribute family through the real `Compono.MSTest` →
`Compono` dependency chain (`test/Compono.MSTest.SampleTests`), an
API-surface/approval test locking the public shape, Native AOT
publish-and-run proof through the real `GetData` path, and — the items
ADR-0057 explicitly needs empirical confirmation for, not just design-time
reasoning — a real MTP-vs-VSTest single-run/discovery-then-execution
repeat-invocation check (recorded once as a finding, not a permanently
asserted test, matching PLAN-0040's own precedent for an equivalent
runner-lifecycle property) and a real display-name/seed-visibility check
under both runners. Reuses behavioral expectations from
`Compono.XunitV3.Tests`/`Compono.TUnit.Tests` wherever ADR-0057 states the
semantics are intentionally identical, rather than duplicating coverage
mechanically. Every task group above carries its own test/verification
item — tests land with the behavior they cover, not batched into a later
catch-all task group.

## Notes

**MSTest.TestFramework `3.x`/`4.x` binary-incompatibility finding
(2026-09-02) — ADR-0057 Amendment 1.** While implementing task group 8's
compatibility-matrix validation, building `src/Compono.MSTest` against the
originally-accepted `3.0.0` floor and running `test/Compono.MSTest.Tests`
(which resolves `MSTest.TestFramework` to `4.3.3` transitively via the
`MSTest` meta-package) failed with
`FileNotFoundException: Microsoft.VisualStudio.TestPlatform.TestFramework,
Version=14.0.0.0`. Root-caused by direct `.nupkg` inspection: every `3.x`
release (including the latest, `3.11.1`) compiles its framework types into
`Microsoft.VisualStudio.TestPlatform.TestFramework.dll`; every `4.x`
release (`4.0.0`-`4.3.3`) compiles the same types into a renamed
`MSTest.TestFramework.dll` — two different assembly identities, no
type-forwarder/facade bridge in either package. A `Compono.MSTest.dll`
compiled against `3.x` cannot implement the `ITestDataSource` a `4.x` test
host looks for, and vice versa — this is a hard binary break, not a
version-skew warning. Reported to the user rather than silently building
around it; the user accepted raising the floor (ADR-0057 Amendment 1:
`MSTest.TestFramework` `4.0.0` minimum, `3.x` explicitly unsupported —
deliberate product decision for a new pre-1.0 package, not a claim that
`3.x` has no users) rather than shipping dual binaries/conditional package
assets to preserve `3.x`. `Directory.Packages.props`,
`src/Compono.MSTest/Compono.MSTest.csproj`, and this plan's own task
groups 1/8/9/12/15 were updated to `4.0.0` throughout. Re-verified after
the floor change: `src/Compono.MSTest` now resolves `MSTest.TestFramework`
to `4.0.0` (its `PackageVersion` floor) with zero override needed, and the
full `test/Compono.MSTest.Tests` suite (29/29) passes cleanly against it.

**Task groups 1-5 implementation (2026-09-02)**: package/project creation,
the full frozen public API (`ComposeAttribute`/`ComposeAttribute<TProfile>`/
`ComposeAttribute<TProfile, TConfig>`/`SharedAttribute`), the package-local
`Binding/` port (`BindingPlan`, `ParameterBindingPlan`,
`PositionalArgumentBinder`, `ConfigProfileBinder`, `RowInvokers` — built
against the existing `RowInvokerRegistry` from the first commit, no
throwaway reflection dispatch), the `Compono.Generators` discovery
extension (three new metadata-name registrations, folded into the existing
`ComposeMethodDiscovery`/`ComponoIncrementalGenerator` pipeline
unforked), and `GetDisplayName`-based seed reporting are implemented and
tested. Real findings from this pass:
- `ITestDataSource.GetData`/`GetDisplayName` are called separately by
  MSTest with no shared context object threaded between them — unlike
  `Compono.XunitV3`'s `TheoryDataRow.Traits`, there is no built-in carrier
  for a row's seed. Resolved with a `ConditionalWeakTable<object?[], object>`
  keyed by the exact row-array instance `GetData` returned, read back in
  `GetDisplayName` — an implementation-time decision RESEARCH-0017 §11/§15
  explicitly left open, not specified by the ADR.
  `GetDisplayName_ReportsDifferentSeeds_ForTwoIndependentCalls` proves this
  works correctly across two independent `GetData` calls with different
  generated seeds, matching §9's "no state shared across calls" contract.
- Full `test/Compono.MSTest.Tests` suite: 29 tests, covering binding-plan
  signature validation (generic method, `ref`/`out`/`in`/`params`, `ref
  struct`/pointer, duplicate `[Shared]`, stacked Compose-family attributes),
  inline-value binding/precedence, `[Shared]` sharing within one row,
  negative-seed rejection (both the base form and the pre-config-binding
  check on `ComposeAttribute<TProfile, TConfig>`), `ComposeAttribute<TProfile>`/
  `ComposeAttribute<TProfile, TConfig>` profile application,
  `ConfigProfileBinder` failure diagnostics, seed reporting, and the exact
  four-type public-API-surface lock. All passing.
- Generator-discovery snapshot test
  (`MSTestComposeAttributedMethodParameter_GeneratesCompositionPlan`)
  proves a type reachable only through a `Compono.MSTest`-attributed
  method's parameter gets both a generated `ICompositionPlan<T>` and a
  `RowInvokerRegistry` registration — the exact gap ADR-0057 §5 requires
  closing. Full `test/Compono.Generators.Tests` suite (299/299) confirmed
  unaffected — this is additive, not a change to existing discovery
  behavior.
- A quick real-run smoke check (not yet the full task-8 matrix): the same
  29-test suite passes identically under `dotnet test -f net10.0` (MTP,
  the project's default) and `dotnet test -f net10.0 -p:UseVSTest=true`.
  The `-p:UseVSTest=true` run did not visibly switch off MTP in the
  console output (`EnableMSTestRunner`/`UseMicrosoftTestingPlatformRunner`
  may be taking precedence) — task group 8 needs to confirm the VSTest
  adapter path is genuinely exercised, not just that the flag was accepted,
  before this counts as real VSTest-adapter proof.
- Solution-wide `dotnet build Compono.slnx`: 0 errors, only pre-existing
  warnings in unrelated projects (`Compono.DependencyInjection.Tests`
  xUnit analyzer warnings, present before this work).

**Task groups 6–8 implementation (2026-09-02)**: real-runner verification,
not unit-level simulation. Added `test/Compono.MSTest.Tests/RealRunnerRowIdentityTests.cs`
(genuine `[TestMethod]` + `[Compose]`/`[Shared]`/inline-value tests, unlike
every other test file in this project, which calls `GetData`/`GetDisplayName`
directly without going through MSTest's own pipeline) and
`DataRowCoexistenceTests.cs` (`[DataRow]` + `[Compose]` stacked on one
method). Two new test-only instrumentation hooks on `ComposeAttribute`
(`SeedByRowHitCount`/`MissCount`, `GetDataCallCount`, `internal`, gated by
existing `InternalsVisibleTo`) — no new public surface, no product-code
behavior change.

- **Row-array-identity assumption (the seed/display-name bridge's own
  dependency) — confirmed under both runners.** `./Compono.MSTest.Tests
  --list-tests` (MTP discovery) and `dotnet vstest Compono.MSTest.Tests.dll
  -lt`/`/Tests:...` (classic VSTest, confirmed via its own distinct
  `VSTest version 18.10.0` banner — not just a flag that was silently
  ignored, the actual concern flagged in the prior round) both show the
  real, correct per-row seed in `GetDisplayName`'s output, e.g.
  `ComposesTwoStrings_RealRun (Compono, seed: 1913922119)`, a different
  seed on every independent run. `SeedByRowMissCount` stayed `0` across
  every scenario tried (ordinary MTP execution, ordinary VSTest execution,
  MTP discovery, VSTest discovery) — the `ConditionalWeakTable`-keyed
  bridge's identity assumption holds. **Real, non-obvious finding along
  the way**: neither runner calls `GetDisplayName` during ordinary
  *execution* (`dotnet test`/`dotnet vstest`) — only during
  *discovery*/listing. `RealRunnerRowIdentityTests`' own regression
  assertion was corrected to reflect this (asserts `MissCount == 0`
  unconditionally; does not assert `HitCount > 0`, since that's
  execution-vs-discovery-mode-dependent, not a real invariant).
- **Discovery/execution repeat-composition contract (ADR-0057 §9) —
  reproduced with current versions, PID-tagged, mirroring RESEARCH-0017
  §20/§20a's own methodology.** Via a temporary `COMPONO_MSTEST_GETDATA_LOG`
  env-var-gated logging hook in `GetData` (removed after capturing evidence
  — not shipped):
  - Single combined process (`dotnet test`/direct MTP exe invocation, one
    process doing both discovery and execution): **exactly one** `GetData`
    call per method. Confirmed under MTP.
  - Single combined process, classic VSTest (`dotnet vstest`, one process):
    **exactly one** `GetData` call per method.
  - Separate discovery process (`--list-tests`/`-lt`) followed by a
    separate execution process: **exactly two** `GetData` calls per method
    (one per process, confirmed via distinct OS PIDs in the log) — under
    **both** MTP and classic VSTest, not only VSTest as RESEARCH-0017's
    original framing might suggest; the doubling is a property of
    "discovery and execution are separate process invocations," not
    specific to the VSTest adapter. This is consistent with, and a
    same-version reproduction of, RESEARCH-0017's own findings — no
    contradiction found, no workaround built (no cross-process cache, no
    static row cache, no custom `TestMethodAttribute`), matching this task
    group's own constraint.
- **`[DataRow]`/`[Compose]` coexistence — confirmed independent-row, not
  merged**, via `DataRowCoexistenceTests.DataRowAndComposeProduceIndependentRows`:
  a method carrying both `[DataRow(42, "from-datarow")]` and `[Compose]`
  produces exactly two test cases under a real run — one with `[DataRow]`'s
  exact literal values, one with independently `[Compose]`-composed values
  — never a merged/partial row. Passing under both MTP and classic VSTest.
- **Version-compatibility matrix — all four legs confirmed, floor and
  current genuinely distinct package versions** (`MSTest.TestFramework`
  `4.0.0.0` vs `4.3.3.0` — confirmed different `AssemblyVersion`s by direct
  inspection, unlike the `3.x`→`4.x` boundary's *assembly-name* break):
  `4.0.0` × MTP, `4.0.0` × classic VSTest (34/34 passing, `Compono.MSTest`
  compiled against its own `4.0.0` floor, consumed by a test project
  transitively resolving `MSTest.TestFramework` to `4.3.3` via the `MSTest`
  4.3.3 meta-package — i.e. the realistic "package built once, consumed
  against a newer same-major version" shape); `4.3.3` × MTP, `4.3.3` ×
  classic VSTest (34/34 passing, `Compono.MSTest` itself temporarily
  rebuilt against `4.3.3` via `VersionOverride`, reverted immediately after
  — the shipped package still targets the `4.0.0` floor). Within the `4.x`
  line, floor and current are ABI-compatible (.NET's ordinary same-name
  assembly version tolerance) — this is exactly the property the `3.x`→`4.x`
  boundary lacked.

**Task groups 4 (remaining item), 9-15 implementation (2026-09-02)**:

- **Task group 9 — packaged-consumer sample project.** New
  `test/Compono.MSTest.SampleTests` (mirrors `Compono.TUnit.SampleTests`'
  own `pack-to-local-feed.sh`/isolated-`RestorePackagesPath` shape exactly):
  `CompositionTests.cs`, `SharedTests.cs`, `ConfigProfileTests.cs`,
  `NSubstituteTests.cs` — the complete attribute family
  (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/
  `[Shared]`) through the real packaged `Compono.MSTest` → `Compono` →
  `Compono.Generators` dependency chain, never a `ProjectReference`. 5/5
  passing under both `dotnet test` (MTP, all 4 TFMs in Release) and
  `dotnet vstest` (classic adapter, confirmed via its own distinct
  "VSTest version 18.10.0" banner). This also closed task group 4's own
  outstanding "minimal local-feed packaged-consumer proof" item — it now
  has the fuller proof rather than a separate minimal one.
- **Task group 10 — Native AOT.** New `test/Compono.MSTest.AotSmokeTest`
  (mirrors `Compono.TUnit.AotSmokeTest`'s `pack-compono.sh`/local-feed
  shape), driving the real, packaged `ComposeAttribute.GetData(MethodInfo)`
  directly (simpler than `Compono.TUnit`'s own harness — MSTest hands a
  plain `MethodInfo`, no hand-built `DataGeneratorMetadata` needed).
  **Real trim gap found and fixed**, identical in shape to `Compono.TUnit`'s
  own ADR-0041 Amendment 1 finding: `ConfigProfileBinder`'s
  `ConstructorInfo.Invoke`-based `TConfig`/`TProfile` construction failed
  at runtime under `dotnet publish -p:PublishAot=true` with `'ProfileConfig'
  must have exactly one public constructor ... but has 0` — the trimmer
  strips a closed generic argument's public constructors by default.
  Fixed with `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
  annotations on `ConfigProfileBinder`'s `Type`-typed parameters and on
  `ComposeAttribute<TProfile, TConfig>`'s own `TProfile`/`TConfig` type
  parameters. Re-verified clean: both `[Compose]` and
  `[Compose<TProfile, TConfig>]` pass under a real `dotnet publish -c
  Release -f net10.0 -p:PublishAot=true -r osx-arm64 --self-contained
  true` + run, and a `-p:TrimmerSingleWarn=false` pass shows **zero**
  warnings attributable to `Compono.MSTest`'s own code. A source-level
  guard (`test/Compono.MSTest.Tests/ReflectionSourceGuardTests.cs`) scans
  `src/Compono.MSTest/**/*.cs` for `MakeGenericType`/`MakeGenericMethod`/
  `Activator.CreateInstance`/`DynamicMethod`/`Delegate.CreateDelegate`/
  `System.Linq.Expressions` — none found (confirmed by direct grep before
  writing the test).
- **Task group 11 — CI/packaging wiring.** `docs.yml`,
  `package-validation.yaml`, `inspect-packed-nupkgs.sh`,
  `generate-api-reference.sh` all updated and **exercised for real**, not
  just edited: a fresh `dotnet pack` of all nine publishable packages run
  through `inspect-packed-nupkgs.sh` passed every assertion for
  `Compono.MSTest` (exact file listing — confirming **no** embedded
  `Compono.Generators.dll`, correct `<title>`, exact-pin `Compono`
  dependency, `MSTest.TestFramework` `[4.0.0, 5.0.0)` range); a real
  `dotnet build -p:WarningsAsErrors=CS1591` pass on `Compono.MSTest.csproj`
  was clean (every public member documented); `generate-api-reference.sh`
  was actually run, producing `docs/reference/api/Compono.MSTest/` with
  git-diff-confirmed zero drift anywhere else. `Compono.MSTest.SampleTests`/
  `Compono.MSTest.AotSmokeTest` deliberately **not** added to `Compono.slnx`,
  matching `Compono.TUnit.SampleTests`/`Compono.TUnit.AotSmokeTest`'s own
  established, documented convention (manual/local-feed-driven proofs, not
  part of `dotnet build Compono.slnx`).
- **Task groups 12-14 — documentation/skill/eval.** New
  `docs/packages/compono-mstest.md` (full package guide, including the
  `[DynamicData]`-migration section task group 12 required).
  `docs/roadmap/future-packages.md` — real deviation from the plan's
  original task wording, recorded rather than silently substituted:
  `docs/mvp.md` turned out to be a closed historical record of the
  original MVP milestone only (confirmed by direct read — none of
  `Compono.TUnit`/`TestDoubles`/`DependencyInjection`/`Http`/`Logging` were
  ever added there either); `docs/roadmap/future-packages.md` is the doc
  that actually live-tracks "scoped pre-1.0 package" status via its own
  established "graduated from this page's roadmap" pattern, so
  `Compono.MSTest` was graduated there instead, matching precedent exactly.
  `README.md`/`docs/index.md`/`docs/packages/index.md`/`docs/public-api.md`/
  `docs/concepts/shared-values.md`/`docs/getting-started/installation.md`
  all updated (found `docs/index.md` already stale relative to `README.md`,
  missing `Compono.Http`/`Compono.Logging` too — fixed alongside the new
  `Compono.MSTest` row rather than leaving an inconsistent table next to
  it). `skills/compono/SKILL.md` (package enumeration, Detection table,
  named-absent guardrail list, references index) and new
  `skills/compono/references/mstest.md` (matching `tunit.md`'s depth).
  Two new evals (ids 35-36 in `skills/compono-evals/evals.json`) — a
  routing scenario and a three-part behavioral-correctness scenario
  (`[DataTestMethod]` unnecessary, `[DataRow]`/`[Compose]` never merge, no
  exactly-once `GetData` guarantee). Benchmark recorded in
  `skills/compono-evals/benchmarks/2026-09-02/README.md` (9/9 with skill,
  3/9 estimated without — graded by this implementing session directly,
  same methodology limitation the 2026-08-28 benchmark already records).
- **Task group 15 — external MSTest packaged-consumer validation
  fixture** (terminology per the 2026-09-02 mid-turn clarification: this is
  **not** true product dogfooding — no real LayeredCraft/ncipollina MSTest
  consumer exists, and none was manufactured to create that appearance).
  Created a small, purpose-built fixture (`OrderProcessing.Tests`, its own
  `git init`/`Directory.Packages.props`/`.gitignore`) at a location
  external to this repository, exercising ordinary composition,
  `[Compose<TProfile>]` with `UseNSubstitute()` + `[Shared]`, `Register<T>()`,
  `Share<T>()` graph-wide sharing, constructor selection (a private
  constructor correctly excluded as a candidate), `Compono.TestDoubles`
  integration (`UseGeneratedTestDoubles()` + `Verify()`), `Compono.Logging`
  integration (`UseLogging()` + `GetLastCapturedEntry()`), deterministic
  seed reproduction, and a negative-seed `CompositionException` diagnostic.
  Validated via `scripts/dogfood-validate.sh` — **no script changes were
  needed**, confirming the plan's own prediction that the script's existing
  `--packages` generalization (PLAN-0051 task 11) would be sufficient.

  **Reproducible fixture spec (recorded in full here, not by reference to
  the ephemeral local checkout, per the 2026-09-02 adversarial-review
  finding that a `<fixture>` placeholder isn't durable evidence)**:

  - **Layout**: a standalone git repository (`git init`, no relation to
    this repo's history), root files `OrderProcessing.slnx` (one project,
    `tests/OrderProcessing.Tests/OrderProcessing.Tests.csproj`),
    `Directory.Packages.props`, `.gitignore` (`bin/`, `obj/`). Test project
    files: `Domain.cs` (`Customer`, `Order`, `IOrderRepository`,
    `OrderService`, `INotifier`, `OrderNotifier`, `ShippingAddress` — the
    last with one public + one private constructor), `CompositionTests.cs`,
    `TestDoublesIntegrationTests.cs`, `LoggingIntegrationTests.cs`.
  - **`Directory.Packages.props`** (`ManagePackageVersionsCentrally=true`):
    placeholder `PackageVersion` entries for `Compono`/`Compono.MSTest`/
    `Compono.NSubstitute`/`Compono.TestDoubles`/`Compono.Logging` at
    `0.0.0` each (the version `dogfood-validate.sh` overwrites via its own
    generated temp copy — the placeholder value itself is never restored
    against), plus `MSTest` `4.3.3` and `Microsoft.Extensions.Logging.Abstractions`
    `10.0.10` pinned directly (not swapped by the script).
  - **Test project csproj**: `TargetFramework net10.0`,
    `EnableMSTestRunner`/`TestingPlatformDotnetTestSupport`/
    `UseMicrosoftTestingPlatformRunner` all `true`,
    `ComponoGeneratedTestDoubles=true`. **`PackageReference` only — zero
    `ProjectReference` to any `src/Compono*` project anywhere in the
    fixture, confirmed by direct read of the one csproj file** (`MSTest`,
    `Compono.MSTest`, `Compono.NSubstitute`, `Compono.TestDoubles`,
    `Compono.Logging`).
  - **Exact command run**: `bash scripts/dogfood-validate.sh
    --consumer-repo <path-to-fixture> --consumer-solution
    <path-to-fixture>/OrderProcessing.slnx --packages "Compono
    Compono.MSTest Compono.NSubstitute Compono.TestDoubles Compono.Logging"
    --feed-dir <throwaway-feed-dir>` (from this repo's root).
  - **Package versions actually validated**: all five requested packages
    packed and resolved at the identical freshly-built local version
    `99.0.0-local.20260902110005-89031-26434` (confirmed by
    `dogfood-validate.sh`'s own built-in anti-stale-cache assertion,
    which greps every restored `project.assets.json` and fails the run if
    any requested package resolves to anything else); `MSTest`/
    `MSTest.TestFramework`/`MSTest.TestAdapter`/`MSTest.Analyzers` at
    `4.3.3` (the pinned, non-swapped entry).
  - **Result**: `dogfood-validate.sh: PASS - consumer test suite succeeded
    against local Compono 99.0.0-local.20260902110005-89031-26434` — 9/9
    tests, exit code 0. Fixture repo's git working tree confirmed
    byte-identical before/after (the script's own safety-net
    status/diff comparison, both empty).
  - **Runner paths exercised**: the script's own `dotnet test` invocation
    (MTP) — 9/9; separately, `dotnet vstest
    <built-consumer-dll>` (classic VSTest adapter, confirmed via its own
    distinct "VSTest version 18.10.0" banner, not inferred from a flag) —
    9/9 again, same assembly.
  - **Cleanup**: the fixture and its throwaway local NuGet feed directory
    were deleted after validation — this is a disposable, reproducible-
    from-spec proof artifact, not a retained system, per explicit
    direction not to preserve a fake long-lived external repo merely to
    keep a URL. Anyone needing to re-verify this can reconstruct the exact
    fixture from the spec above and rerun the exact command.

  Two real, non-Compono-defect issues found and fixed while building the
  fixture itself, both instructive: (1) an XML comment containing `--`
  broke MSBuild's parser entirely (`Directory.Packages.props` silently
  failed to import, surfacing as a confusing downstream `NU1015`/`NU1101`
  rather than the real `MSB4024` XML error — worth remembering for any
  future hand-authored `.props`/`.targets` file); (2) a deliberately
  ambiguous two-public-constructor type correctly triggered `CMP0001`
  ("has 2 accessible constructors and no way to disambiguate them") rather
  than Compono silently picking one — this is Compono's real, intentional
  behavior, not a bug; the fixture's own `Customer` type was simplified to
  one constructor, and `ShippingAddress` (one public, one private
  constructor) was added to exercise real, unambiguous constructor
  selection instead.

**Adversarial review response (2026-09-02)**: an independent review
("Pi") of the above found one HIGH and three MEDIUM issues. All four
addressed for real, not just noted:

- **HIGH — the `4.0.0`/`4.x` compatibility matrix's "`4.0.0`" legs hadn't
  actually run against 4.0.0.** Confirmed correct: the original two
  "`4.0.0`" legs consumed `Compono.MSTest` (compiled against its own
  `4.0.0` floor) from a test project that transitively resolved
  `MSTest.TestFramework` to `4.3.3` via the `MSTest` `4.3.3` meta-package —
  real evidence for "floor-compiled binary runs under current," not for
  "the floor version itself executes." Fixed by re-running with the whole
  `MSTest` family genuinely pinned to `4.0.0`: temporarily added
  `VersionOverride="4.0.0"` to the `MSTest` `PackageReference` in both
  `test/Compono.MSTest.Tests.csproj` and
  `test/Compono.MSTest.SampleTests.csproj`, restored, and **confirmed by
  direct `project.assets.json` inspection** that `MSTest`/
  `MSTest.TestFramework`/`MSTest.TestAdapter`/`MSTest.Analyzers` all
  resolved to exactly `4.0.0` (not just the requested version — the
  actual resolved one). Ran all four real legs:
  - `Compono.MSTest.Tests`, `MSTest` family `4.0.0`, MTP
    (`dotnet test -f net10.0`): 35/35 passing.
  - `Compono.MSTest.Tests`, `MSTest` family `4.0.0`, classic VSTest
    (`dotnet vstest <dll>`): 35/35 passing.
  - `Compono.MSTest.SampleTests` (the real packaged-consumer project, not
    just the unit suite), `MSTest` family `4.0.0`, MTP: 5/5 passing.
  - `Compono.MSTest.SampleTests`, `MSTest` family `4.0.0`, classic VSTest:
    5/5 passing.

  Current `4.x` (`4.3.3`) × MTP/VSTest was already proven for real earlier
  in this Notes section (both the unit suite and the sample project). The
  `VersionOverride`s were reverted immediately after capturing this
  evidence — the shipped csproj files still target the plain `4.0.0`
  floor via the central `[4.0.0, 5.0.0)` range, confirmed by re-restoring
  and re-running (35/35) after the revert. No dependency conflict was
  hit — a genuine `4.0.0` leg was achievable and is now proven, not just
  claimed.
- **MEDIUM — `GetDisplayName`/`dotnet test` wording overclaimed.**
  Confirmed real by the implementation evidence already in this Notes
  section (the `RealRunnerRowIdentityTests` hit/miss-counter finding).
  Per explicit direction, treated as an ADR-level correction, not just a
  doc edit: added **ADR-0057 Amendment 2** (`GetDisplayName` is a
  discovery/listing-time surface — `--list-tests`, `dotnet vstest -lt`,
  Test Explorer's tree population — under both MTP and classic VSTest, and
  is **not** called during an ordinary `dotnet test`/`dotnet vstest`
  execution run under either runner; a composition failure's seed is
  independently carried by `CompositionException.WithSeedInMessage`
  regardless of `GetDisplayName`). `docs/packages/compono-mstest.md`'s
  "Seed and display name" section and
  `skills/compono/references/mstest.md`'s equivalent bullet were both
  rewritten to state the corrected, verified behavior directly, distinguishing
  discovery/listing display names from execution-time
  `CompositionException` diagnostics. The `ConditionalWeakTable`
  implementation itself was **not** changed — per explicit direction, this
  finding is about when MSTest calls the hook, not a defect in the bridge.
- **MEDIUM — `[DynamicData]` coexistence was claimed but never actually
  exercised.** Confirmed: only `[DataRow]` had a real runner-level test.
  Fixed by extending `DataRowCoexistenceTests.cs` with a genuine
  `[DynamicData(nameof(GetDynamicRows))]` + `[Compose]` stacked scenario
  (`DynamicDataAndComposeProduceIndependentRows`), proving the identical
  independent-complete-row behavior for `[DynamicData]` specifically, not
  inferred from the `[DataRow]` case. No merging machinery was added.
  37/37 passing (up from 35) under both MTP and classic VSTest.
- **MEDIUM — external validation evidence wasn't durable.** Addressed
  directly above (this Notes entry now records the full fixture spec,
  exact command, exact resolved versions, and exact result — reproducible
  without the now-deleted fixture existing), per explicit direction not
  to fabricate a permanent fake external repo.
- **The `Compono.XunitV3.Binding.ConfigProfileBinder` Native AOT finding**
  (identified by the review as a likely pre-existing latent gap) is
  recorded as **ADR-0041 Amendment 4** — found incidentally while fixing
  `Compono.MSTest`'s own identical gap, explicitly **not** fixed as part
  of ADR-0057/PLAN-0057, and explicitly **not** applied to any
  `Compono.XunitV3` source file. Tracked as a separate future follow-up
  against ADR-0041, per direction.
- **Not touched, per explicit direction — no new contradictory evidence
  surfaced**: the `ConditionalWeakTable` design, the `Compono.MSTest`
  `DynamicallyAccessedMembers` fix, and the public API surface all stand
  as already accepted.

**Final validation gate, re-run after the adversarial-review fixes
(2026-09-02)**: `dotnet build Compono.slnx` — 0 errors, 0 new warnings.
`dotnet test Compono.slnx -f net10.0` — whole-repo run, **964/964
passing**, zero regressions in any existing package (`Compono.Tests` 291,
`Compono.Generators.Tests` 299, `Compono.XunitV3.Tests` 76,
`Compono.TUnit.Tests` 52, `Compono.NSubstitute.Tests` 23,
`Compono.Bogus.Tests` 63, `Compono.TestDoubles.Tests` 6,
`Compono.DependencyInjection.Tests` 17, `Compono.Http.Tests` 29,
`Compono.Logging.Tests` 59, samples 12, plus `Compono.MSTest.Tests`' own
**37** — up from 35, the two new `[DynamicData]` rows). Packaging
(`inspect-packed-nupkgs.sh`), CS1591 doc-completeness, API reference
regeneration, Native AOT (both attribute forms, zero
`Compono.MSTest`-attributable trim warnings), MTP + classic VSTest (unit
suite, sample project, and the external validation fixture, all three,
now including a genuine `MSTest` `4.0.0`-family leg for both the unit
suite and the sample project, not only current `4.3.3`), the full
`4.0.0`/current-`4.x` × MTP/VSTest compatibility matrix (actual resolved
package versions confirmed by direct `project.assets.json` inspection,
not requested/configured versions), and `[DataRow]`/`[DynamicData]`
coexistence (both, not just `[DataRow]`) were each verified for real, not
assumed — see the task-group entries and the adversarial-review-response
entry above for each one's own command/output. Working tree: every change
is an intentional part of this feature (`git status` reviewed, all
`VersionOverride` experiments reverted and re-verified passing at the
shipped `4.0.0` floor); the external validation fixture and its throwaway
local NuGet feed directories lived entirely outside this repository and
were deleted after validation — this Notes section's own fixture-spec
entry is the durable, reproducible record of what was validated.

**PLAN-0057 is now legitimately `Done`** — all 15 task groups' checkboxes
are checked, and the four Pi-identified findings (one HIGH, three MEDIUM)
are resolved with real, re-verified evidence rather than narrowed claims.
Not committed, pushed, or opened as a PR, per this session's explicit
instructions throughout. `Status` below reflects the work done;
the user should review before this plan is considered fully closed out in
the sense of a merged PR.
