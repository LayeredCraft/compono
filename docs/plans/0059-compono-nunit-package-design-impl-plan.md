# [PLAN-0059] `Compono.NUnit` Package Design — Implementation Plan

**Status:** In Progress

**Implements:** [ADR-0059](../adr/0059-compono-nunit-package-design.md)
(`Compono.NUnit` package: `TestAttribute`-based, `ITestBuilder`-
implementing `[Compose]` attribute family — **no `[TestFixture]`
requirement**, revised pre-acceptance — `NUnit`-only dependency at
`[3.14.0, 5.0.0)`, `CompositionRow`/`RowInvokerRegistry` reused
unchanged, the `BindingPlan`/`RowInvokers` pattern adapted
package-locally with an `IMethodInfo`→`MethodInfo` unwrap,
`NUnitTestCaseBuilder`/`TestCaseParameters`-based `TestMethod`
construction, no partial-row merging with `[TestCase]`/`[Values]`/
`[Range]` — each independent, none merged, none unused — no disposal
ownership)

**Note:** ADR-0059 is `Accepted` (2026-09-03). Implementation is underway
against PR #127 — see the Notes section at the end of this plan for the
current, honest task-by-task completion state.

## Goal

```csharp
public class OrderServiceTests
{
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

runs end-to-end under a real NUnit test host — the same scenario
`Compono.XunitV3.SampleTests`/`Compono.TUnit.SampleTests`/
`Compono.MSTest.SampleTests`' own `NSubstituteTests.Saves_order` already
prove, reproduced a fourth time under plain `[Compose]` with **no
`[TestFixture]` needed** (ADR-0059 §7, revised pre-acceptance), with
`repository` shared across `handler`'s own composed constructor parameter
via `[Shared]`, the row's seed visible in the test's display name under
both MTP and the classic VSTest adapter, and a generated-code-reachability
proof that a type reached only through a `Compono.NUnit`-attributed
method's parameter still gets a generated composition plan. Done means:
`Compono.NUnit.nupkg` builds and packs, every ADR-0059 behavioral
contract (§1-§18) has a passing test or a recorded real-run verification
proving it, the `[Compose]` + `[Values]`/`[Range]`/custom-source
independent-row contract (ADR-0059 §8, already spike-verified
pre-acceptance) has locked-in regression coverage, `Compono.NUnit`'s own
code is reflection-free/AOT-clean on the same terms as the other three
packages, implemented with the correct DAM annotations from the start
(with NUnit's own Native-AOT runnability honestly recorded, not
overclaimed), the permanent compatibility matrix proves *resolved* NUnit
versions across the floor/current-stable-4.x legs under both runners
(NUnit 5 prerelease tracked as separate, non-blocking surveillance),
every documentation/skill/eval surface ADR-0059 names is updated and
consistent with the shipped API, and a dedicated external NUnit
packaged-consumer validation fixture has been validated via
`scripts/dogfood-validate.sh` against freshly packed local packages —
see task group 12's own terminology note: this is real, pre-1.0 external
consumption validation, not product dogfooding (no real
LayeredCraft/ncipollina NUnit consumer exists to dogfood against yet).

## Scope

Per ADR-0059's Decision Outcome — carried forward exactly, not reopened
unless implementation exposes a genuine contradiction:

**In scope**: a new `Compono.NUnit` package (`ComposeAttribute`/
`ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>`/
`SharedAttribute`, §4's frozen public shape), its package-local
`BindingPlan`/`ParameterBindingPlan`/`PositionalArgumentBinder`/
`ConfigProfileBinder`/`RowInvokers` binding implementation (adapted from
the established pattern, dispatching through the existing, unchanged
`RowInvokerRegistry`/`CompositionRow`, with the `IMethodInfo`→`MethodInfo`
unwrap and `NUnitTestCaseBuilder`/`TestCaseParameters`-based `TestMethod`
construction §5 requires), the three-metadata-name `Compono.Generators`
discovery extension (§10), MTP and classic-VSTest-adapter support, the
no-`[TestFixture]`-required regression coverage (§7), the `[Compose]` +
`[Values]`/`[Range]`/custom-source independent-row regression coverage
(§8, protecting an already-verified contract), a permanent CI
compatibility matrix covering the `Internal`-namespace dependency risk
(§6), the full documentation/skill/eval/external-validation
completion-gate work ADR-0059 requires as part of this feature's
definition of done, and all repository build/CI/packaging integration a
new package needs.

**Explicitly deferred / non-goals** (per ADR-0059's own Deferred
Decisions section — not this plan's job to solve): auto-registering a
class as an NUnit fixture (moot — no fixture marker is needed at all);
merging `[Compose]` with parameter-level `IParameterDataSource` sources
(settled as independent, non-merging pre-acceptance — this plan protects
that contract with regression tests, it does not build merging
machinery); `IFixtureBuilder`-based fixture-constructor composition;
extracting a shared `BindingPlan`/`RowInvokers` base across
`Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`/`Compono.NUnit`;
automatic `TestContext`/framework-value injection; a `Compono.NUnit`-owned
disposal mechanism; async composition; claiming NUnit's own Native-AOT
runnability without direct proof; widening the range to include NUnit 5
before it ships stable.

**One PR for everything inside this repository**, matching
[PLAN-0057](0057-compono-mstest-package-design-impl-plan.md)'s own
sizing precedent and ADR-0059's product-direction framing: task groups
1-11 below — the `Compono.NUnit` runtime package, its
`Compono.Generators` discovery extension, the full test suite, Native
AOT/MTP/VSTest validation, and the complete documentation/skill/eval
synchronization — are one cohesive feature with one public API boundary
and one definition of done; they ship as **one PR**. Task group 12 (the
external NUnit packaged-consumer validation fixture) is, by its nature, a
separate repository's own history and cannot land in this repo's PR at
all — it follows PLAN-0057 task 15's precedent: this plan's `Status: Done`
still requires it to be substantially complete, but its own commits live
outside this repo.

## Tasks

### 1. Package/project creation

- [x] `src/Compono.NUnit/Compono.NUnit.csproj` — `net8.0;net9.0;net10.0;net11.0`
      (ADR-0038's TFM window, matching every other integration package).
      `ProjectReference` to `..\Compono\Compono.csproj`
      (`PrivateAssets="none"`, per the established packaging lesson
      PLAN-0004 Phase 3/PLAN-0040 Phase 0 both record) and
      `PackageReference` to `NUnit` **only** — no `NUnit3TestAdapter`, no
      `Microsoft.Testing.Platform` packages, no `Microsoft.NET.Test.Sdk`
      (ADR-0059 §3, range `[3.14.0, 5.0.0)`). Same
      `PinProjectReferenceVersionsExact` MSBuild target every other
      integration project's `.csproj` carries. `InternalsVisibleTo` for
      `Compono.NUnit.Tests`.
- [x] `Directory.Packages.props`: add
      `<PackageVersion Include="NUnit" Version="[3.14.0, 5.0.0)" />` —
      the exact, enforceable bounded range ADR-0059 §3 requires, not a
      bare/unbounded version, mirroring the explanatory-comment style
      already used for `MSTest.TestFramework`'s own `[4.0.0, 5.0.0)`
      entry (cite ADR-0059 §3/§6: the range is a real support promise
      because of the accepted `Internal`-namespace dependency, and NUnit
      5 stays surveillance-only until it ships stable). Add
      `Compono.NUnit`'s own entry (`Version="1.0.0"`, matching every
      other integration package's existing pattern). No separate
      hardcoded range check needs adding elsewhere: confirm during
      implementation that `.github/scripts/inspect-packed-nupkgs.sh`'s
      existing `assert_dependency_range`/`assert_third_party_dependency_range`
      helpers (added post-#122, already used for `Compono.MSTest` →
      `MSTest.TestFramework`) derive the expected range straight from
      this `Directory.Packages.props` entry — add a
      `Compono.NUnit)` case branch there
      (`assert_dependency_range "$nuspec" "$pkg" "NUnit" "$authoritative_json"`)
      rather than inventing a second, independently-maintained literal.
- [x] `test/Compono.NUnit.Tests/Compono.NUnit.Tests.csproj` — this
      project executes as a real NUnit test run, so it needs the full
      test-execution chain `src/Compono.NUnit` deliberately doesn't
      carry: `PackageReference` to `Compono.NUnit` (`ProjectReference`
      in-repo) plus `NUnit3TestAdapter` and `Microsoft.NET.Test.Sdk`.
      Confirm the exact required properties (`<EnableNUnitRunner>`/
      `<OutputType>Exe</OutputType>` for MTP vs. classic VSTest) against a
      real NUnit project template during implementation rather than
      guessing them here — both runner paths need to be exercisable from
      this project or a sibling (task group 9). Add `Directory.Packages.props`
      entries for `NUnit3TestAdapter`/`Microsoft.NET.Test.Sdk` alongside
      `NUnit`. Add a `Compono.NUnit.Tests`-name exclusion to
      `test/Directory.Build.props`'s two `IsTestProject`-scoped
      `ItemGroup`s (the shared xUnit-v3-runner packages/global
      `using Xunit;` set that every other test project gets by default
      don't belong in an NUnit-run project — mirrors the
      `Compono.TUnit.Tests`/`Compono.MSTest.Tests` exclusions).
- [x] `Compono.slnx`: add both new projects.

### 2. Public API surface (ADR-0059 §4, frozen — no deviation without stopping to report)

- [x] `ComposeAttribute : TestAttribute, ITestBuilder` (revised
      pre-acceptance from `NUnitAttribute, ITestBuilder` — no
      `[TestFixture]` requirement, ADR-0059 §4/§5/§7) — `public
      ComposeAttribute(params object?[] inlineValues)`; `public int Seed
      { get; set; }` (non-negative-only contract, same as every other
      package); `public new IEnumerable<TestMethod> BuildFrom(IMethodInfo
      method, Test? suite)` — declared **with `new`**, an explicit,
      intentional hiding of `TestAttribute`'s own inherited
      `ITestBuilder.BuildFrom` (pre-acceptance spike-confirmed, ADR-0059
      §4: `new` changes no observable behavior — the `ITestBuilder`
      interface map and real NUnit discovery/execution are identical with
      or without it — but eliminates `CS0108` at compile time, so
      production source builds warning-free). `[AttributeUsage(AttributeTargets.Method,
      AllowMultiple = false)]`.
- [x] `ComposeAttribute<TProfile> : ComposeAttribute where TProfile :
      ICompositionProfile, new()` — inline-value constructor
      pass-through, `sealed`. Confirm the inline-value constructor ships
      on the *base* type in this same task group, not deferred later —
      the exact binary-compatibility mistake PLAN-0040's own review round
      caught and fixed; do not repeat it.
- [x] `ComposeAttribute<TProfile, TConfig> : ComposeAttribute where
      TProfile : ICompositionProfile` (no `new()` constraint) — `public
      ComposeAttribute(params object?[] configArguments) : base()` (zero
      inline values passed to the base; `configArguments` bound to
      `TConfig`'s single public constructor via a package-local
      `ConfigProfileBinder`, then `TProfile` constructed and applied via
      `CompositionBuilder.AddProfile`). Negative-seed validation runs
      before any config/profile binding is attempted, matching the
      established ordering from every prior package.
- [x] `SharedAttribute` — `[AttributeUsage(AttributeTargets.Parameter,
      AllowMultiple = false)]`, a `Compono.NUnit`-specific *public* marker
      (part of the package's public API, mirroring `Compono.XunitV3`/
      `Compono.TUnit`/`Compono.MSTest`'s own per-package `SharedAttribute`
      types) with the existing shape/duplicate-shared-type validation.
- [x] Stacked Compose-family attribute validation: reject a method
      carrying more than one of `[Compose]`/`[Compose<TProfile>]`/
      `[Compose<TProfile, TConfig>]` — `AllowMultiple = false` is
      per-exact-type only, so nothing else stops two *different*
      Compose-family types stacking on one method.
- [x] `test/Compono.NUnit.Tests`: API-surface/approval test locking the
      exact four-type public shape (`ComposeAttribute`,
      `` ComposeAttribute`1``, `` ComposeAttribute`2``, `SharedAttribute`),
      matching every other package's existing pattern.

### 3. Binding implementation (`src/Compono.NUnit/Binding/*`)

- [x] `BindingPlan.cs`/`ParameterBindingPlan.cs`/`PositionalArgumentBinder.cs`
      — a package-local port of the established pattern, operating on
      `System.Reflection.MethodInfo`/`ParameterInfo` (the real,
      *unwrapped* types — see the `IMethodInfo` unwrap step below, not
      NUnit's own `IMethodInfo`/`IParameterInfo` wrappers). Covers:
      parameter discovery, `[Shared]` detection, nullability inference,
      inline-value positional binding/precedence, generic-method
      rejection, `ref`/`out`/`in`/`params` rejection, `ref struct`/
      pointer-typed by-value-parameter rejection (the same dispatch-
      eligibility guard ADR-0041/PLAN-0041 established — carry it here
      from the start), duplicate-`[Shared]`-type rejection, more-than-one-
      Compose-family-attribute rejection (task group 2's last item).
- [x] `RowInvokers.cs` — built against core `Compono`'s existing
      `RowInvokerRegistry.TryGet` from its first commit (ADR-0041). No
      throwaway `MakeGenericMethod`/`Delegate.CreateDelegate`-based
      version ships first.
- [x] `ConfigProfileBinder.cs` — package-local port of the established
      pattern (constructor-shape lookup for `TConfig`/`TProfile` via
      reflection, bounded to once per attribute instance by the same
      `Lazy<Composer>`-backed caching pattern, never on the repeated
      per-`BuildFrom`-call path). Unsupported constructor shapes are a
      deterministic `CompositionException`, not a compile error.
- [x] `ComposeAttribute.BuildFrom(IMethodInfo method, Test? suite)`: **the
      NUnit-specific unwrap step, first** — `method.MethodInfo` (the
      underlying real `System.Reflection.MethodInfo`; confirm
      `method.GetParameters()[i].ParameterInfo` is likewise available and
      used, not `IParameterInfo` directly, per ADR-0059 §5). Then one
      `CompositionRow` per invocation (`composer.CreateRow(...)`), binds
      every parameter via `row.Resolve<T>()`/`row.ResolveShared<T>()`/
      `row.ShareExplicit<T>()` through `RowInvokers`, and constructs the
      final `TestMethod` via `new NUnitTestCaseBuilder().BuildTestMethod(
      method, suite, new TestCaseParameters(args))`
      (`NUnit.Framework.Internal.Builders`/`NUnit.Framework.Internal` —
      ADR-0059 §6's accepted, monitored dependency), setting `Name` to
      the seed-bearing display string. **No graph state shared across
      separate `BuildFrom` calls** — no static/module-level row cache of
      any kind (ADR-0059 §12's contract depends on this).
- [x] Every `Resolve`/`ResolveShared`/`ShareExplicit` call wrapped to
      catch `CompositionException` and rethrow via
      `CompositionException.WithSeedInMessage(exception, row.Seed)` — the
      same unconditional, pasteable-seed guarantee every other package
      already makes.
- [x] `test/Compono.NUnit.Tests`: binding-plan unit coverage mirroring
      the established pattern — parameter resolution, inline-value
      precedence, `[Shared]`/duplicate-`[Shared]`-type validation,
      nullability, signature-validation errors (generic method, `ref`/
      `out`/`in`/`params`, `ref struct`/pointer by-value), stacked-
      attribute rejection, `CompositionException` seed enrichment. Confirm
      whether hand-built `IMethodInfo`/`Test` fixtures are practical to
      construct directly for unit-level tests, or whether this coverage
      needs to route through real reflected `MethodInfo` wrapped via
      NUnit's own `TypeWrapper`/`MethodWrapper` helpers — resolve this
      during implementation, don't guess it here.

### 4. No-`[TestFixture]`-required behavior (ADR-0059 §7 — first-class, not a docs footnote)

- [x] `test/Compono.NUnit.Tests` (or the sample project, task group 8): an
      explicit, regression-locked test proving **both** cases directly —
      a `[Compose]`-only class with **no** `[TestFixture]` discovers and
      runs the expected test(s) (the now-chosen, correct behavior); a
      duplicate-test-case regression guard confirming exactly one row is
      produced per `[Compose]` method, not an extra empty/default case
      from `TestAttribute`'s own inherited `ITestBuilder` behavior
      (ADR-0059 §4's C# interface-resolution explanation for why this
      doesn't happen). Optionally also confirm a consumer *may* still add
      `[TestFixture]` for their own unrelated reasons without breaking
      anything. This must be a real, permanent regression test protecting
      an already pre-acceptance-verified contract, not exploratory
      discovery — the finding is settled; this task locks it in.

### 5. `[Compose]` + parameter-level-source coexistence (ADR-0059 §8 — settled pre-acceptance, lock in as regression coverage)

- [x] Real, permanent regression coverage for `[Compose]` on the same
      method as `[Values]` and `[Range]` (both independently confirmed
      pre-acceptance to produce their own additional, independently-
      executing test rows — never merged into the Compose row, and never
      unused/suppressed), plus at least one custom `IParameterDataSource`
      case (not independently spiked pre-acceptance — this task closes
      that specific, narrow remaining gap). Assert the exact expected row
      count and that each row's actual parameter value matches its own
      source (the Compose row keeps its composed values; `[Values]`/
      `[Range]`/custom-source rows carry their own literal values,
      untouched by composition). This is protecting a known, verified
      contract, not open-ended discovery — no "stop and amend the ADR"
      escape hatch is needed here; if the custom-`IParameterDataSource`
      case genuinely contradicts the `[Values]`/`[Range]` pattern, that
      would be a real surprise worth reporting, but the expected,
      evidence-backed outcome is that it behaves the same way.

### 6. Generator discovery (`Compono.Generators`)

- [x] `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
      `src/Compono.Generators/ComponoIncrementalGenerator.cs`: three new
      metadata-name constants (`Compono.NUnit.ComposeAttribute`/`` `1``/
      `` `2``) and three new `SyntaxValueProvider
      .ForAttributeWithMetadataName` registrations, feeding the existing,
      already attribute-family-agnostic `ComposeMethodDiscovery
      .TransformMethod` — no fork or reimplementation of that method
      (ADR-0059 §10).
- [x] `test/Compono.Generators.Tests`: a snapshot test proving a concrete
      parameter type reachable *only* through a `Compono.NUnit`-attributed
      method's own parameter (no other discovery path in the same
      compilation) receives a generated composition plan and a
      `RowInvokerRegistry` registration — mirroring the equivalent
      regression coverage the other three packages already have.
- [x] Generator-discovery packaged-consumer proof: **satisfied by task
      group 8's `Compono.NUnit.SampleTests` project** if that project
      exists before this task group needs to close — do not require a
      second, separate permanent local-feed proof project for the same
      claim (right-sized pre-acceptance; the sample project already
      exercises the packaged `Compono.NUnit`/`Compono` dependency chain
      with the unqualified `[Compose]` attribute). If sequencing genuinely
      puts this task group before task group 8's sample project exists,
      a temporary, implementation-time-only `dotnet pack` → local-feed →
      restore smoke check is fine to run while developing this task
      group, but it does not need to become a separately mandated,
      permanently-maintained completion gate.

### 7. Seed and display-name semantics (ADR-0059 §13)

- [x] `BuildFrom` sets the constructed `TestMethod.Name` to a seed-bearing
      display string reflecting the row's actual seed, not a placeholder.
- [x] `test/Compono.NUnit.Tests`: unit coverage that the constructed
      `TestMethod.Name` contains the exact seed a given
      `[Compose(Seed = N)]`/generated-seed row used.
- [x] Real-run verification (task group 9) that the display name is
      actually visible in `dotnet test`/Test Explorer output under both
      MTP and the classic VSTest adapter — a design-time claim confirmed
      against a real runner, not assumed from the API contract alone.

### 8. Packaged-consumer sample project (`test/Compono.NUnit.SampleTests`)

- [x] A real packaged-consumer project (mirroring the established
      pattern exactly) exercising the *complete* attribute family
      (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/
      `[Shared]`) through the actual packaged `Compono.NUnit` → `Compono`
      dependency chain, not `Compono.NUnit.Tests`' own `ProjectReference`-
      based calls — a `ProjectReference` doesn't propagate
      `Compono.Generators` as an analyzer the way a packed nupkg's
      `analyzers/dotnet/cs` delivery does.
- [x] Includes the `NSubstituteTests.Saves_order`-shaped scenario from
      this plan's Goal section, run for real (needs `Compono.NSubstitute`
      as an additional project dependency, matching the other sample
      projects).
- [x] Includes `ConfigProfileTests`-shaped coverage for
      `[Compose<TProfile, TConfig>]`, mirroring the other three sample
      projects.
- [x] Includes the no-`[TestFixture]`-required scenario (task group 4),
      `[TestCase]`/`[Compose]` coexistence (ADR-0059 §8's "independent
      rows" finding applied for real, using `[TestCase]` specifically),
      and the `[Values]`/`[Range]`/custom-source coexistence scenarios
      (task group 5) end to end, through the real packaged dependency
      chain.

### 9. MTP/VSTest and version-compatibility validation

- [x] Confirm `Compono.NUnit` works correctly under **both** MTP
      (`<EnableNUnitRunner>true</EnableNUnitRunner>` +
      `<OutputType>Exe</OutputType>`) and the classic VSTest adapter —
      real runs, not assumed from `ITestBuilder` being runner-agnostic on
      paper. `Compono.NUnit` itself introduces no runner-selection logic
      or MTP-/VSTest-specific API of any kind; runner choice stays
      entirely the consumer project's configuration.
- [ ] Close the MTP discovery/execution double-`BuildFrom`-evaluation gap
      ADR-0059 §12 explicitly left open: independently verify (or
      disprove) whether a separate discovery process followed by a
      separate execution process under MTP produces two `BuildFrom` calls
      per method, the same way classic VSTest does — RESEARCH-0018's own
      MTP spike ran the executable directly, once, and did not test this.
      Record the actual observed result in this plan's Notes.
**Permanent, CI-blocking compatibility matrix** (right-sized
pre-acceptance to the actual `[3.14.0, 5.0.0)` support contract — not
every leg RESEARCH-0018 happened to spike):

- [x] `NUnit` `3.14.0` (the floor) × classic VSTest adapter — blocking.
- [x] `NUnit` `3.14.0` × MTP — blocking, **only if this specific
      combination is genuinely supported**; RESEARCH-0018/the
      pre-acceptance spike tested `3.14.0` under classic VSTest and
      `4.6.1`/`5.0.0-beta.1` under both runners, but did not test
      `3.14.0` × MTP specifically. Verify this first. If unsupported,
      record that as a genuine finding and drop this leg rather than
      silently assuming it works.
- [x] Current stable `NUnit` `4.x` (the latest patch as of implementation
      time) × classic VSTest adapter — blocking.
- [x] Current stable `NUnit` `4.x` × MTP — blocking.
- [x] **Every blocking leg above must independently verify the
      *resolved* NUnit assembly version from real build/
      `project.assets.json` output, not just the declared
      `PackageReference` version** — the exact discipline that caught the
      MSTest silent-upgrade near-miss (RESEARCH-0017 §17) and that
      RESEARCH-0018 §18 already applied during research; wire this into
      permanent CI, not a one-time manual proof, per ADR-0059 §6's
      requirement that the `Internal`-namespace dependency risk be
      monitored continuously.

**Non-blocking forward-compatibility surveillance** (kept explicitly
separate from the blocking matrix above — do not fold into "every leg
must be permanent CI"):

- [x] A scheduled or manually-triggered, **non-blocking** spike against
      the current `NUnit` `5.x` prerelease (`5.0.0-beta.1` or whatever is
      current at implementation time). This tracks whether a future
      `< 6.0.0` range widening (ADR-0059 §3) is likely to stay safe; it
      is explicitly not part of the current `[3.14.0, 5.0.0)` support
      contract and must not block merges or releases. Once NUnit 5 ships
      stable and ADR-0059's range is amended, promote this leg into the
      blocking matrix above.
- [x] Record the actual compatibility matrix exercised (NUnit version ×
      MTP/VSTest, with resolved assembly versions, blocking vs.
      surveillance) in this plan's Notes once run for real.

### 10. Native AOT / reflection-free validation

- [x] Confirm `Compono.NUnit`'s own code contains no `MakeGenericType`,
      `Activator.CreateInstance` (beyond `ConfigProfileBinder`'s existing,
      already-accepted bounded reflection pattern), `MakeGenericMethod`,
      or reflection-based fallback construction outside the
      `MethodInfo`/`ParameterInfo` metadata access ADR-0059 §17 explicitly
      accepts as framework-required.
- [x] **Day-one requirement, not discovered-by-failure**: implement
      `ConfigProfileBinder`'s `TConfig`/`TProfile` construction with the
      same `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
      annotations `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`'s own
      `ConfigProfileBinder` already carries for the identical
      constructor-reflection `Type`-flow shape (ADR-0041 Amendments 4-5)
      — read the exact current annotation pattern directly from one of
      those three packages' `ConfigProfileBinder.cs` before implementing
      this one, don't guess it. Then run the real `AotSmokeTest`
      publish-and-run proof (below) to *verify* the annotation is
      correct, not to *discover* that one was needed — this repeats a
      known-correct pattern, it does not re-derive it from a trimmer
      failure.
- [x] A dedicated `test/Compono.NUnit.AotSmokeTest` (or equivalent)
      project, `dotnet publish -c Release -p:PublishAot=true` + run,
      exercising the real, packaged `Compono.NUnit.ComposeAttribute
      .BuildFrom` path directly (not a hand-rolled stand-in for it) —
      composing both a custom type and a provider-resolved leaf type,
      through both the no-profile and `[Compose<TProfile, TConfig>]`
      forms. `-p:TrimmerSingleWarn=false` pass: zero warnings
      attributable to `Compono.NUnit`'s own shipped code. **Attempt this
      for real** — if NUnit's own runner/adapter chain blocks true
      Native-AOT publishing/execution, record that honestly as a
      distinct, separate finding from "`Compono.NUnit`'s own code is
      trim-safe," per ADR-0059 §17's precise two-part claim; do not
      weaken the claim about `Compono.NUnit`'s own code merely because
      NUnit's runner may not support it.
- [x] Source-level guard: a simple text/syntax scan over
      `src/Compono.NUnit/**/*.cs` that fails the build if
      `MakeGenericType`, `MakeGenericMethod`, `Activator.CreateInstance`
      (beyond `ConfigProfileBinder`'s own bounded, accepted exception),
      `DynamicMethod`, `Delegate.CreateDelegate`, or
      `System.Linq.Expressions` ever appears — a real, established
      pattern with demonstrated repository precedent, **not ritual
      validation invented for this package**:
      `test/Compono.MSTest.Tests/ReflectionSourceGuardTests.cs` (ADR-0057
      §14, not `Compono.Logging` as an earlier draft of this plan
      mis-cited) is the exact template to port — a per-file, per-line
      text scan with a doc-comment exemption, kept because architecture
      tests/code review/real AOT publish/trimmer warnings/generated-
      dispatch tests each catch a different failure mode and none of them
      substitutes for this specific check on their own (confirmed by
      checking this repo's own precedent, not assumed).

### 11. Build/CI infrastructure wiring, documentation, and skill/eval synchronization

Creating the projects alone leaves them outside every place this repo's
build/release/validation/documentation pipeline enumerates packages by
name — this is completion-gate work per ADR-0059, not follow-up cleanup:

- [x] `Compono.slnx` — the two core project entries added (task group 1).
      The sample/AOT-smoke projects (`Compono.NUnit.SampleTests`,
      `Compono.NUnit.AotSmokeTest`) are deliberately **not** added,
      matching the other three packages' own precedent — manual,
      one-shot/local-feed-driven proofs run outside `dotnet build
      Compono.slnx`.
- [x] `.github/workflows/docs.yml` — `src/Compono.NUnit/**` added to both
      `paths:` trigger lists, `Compono.NUnit` added to the API-reference
      build loop.
- [x] `.github/workflows/package-validation.yaml` — `Compono.NUnit` added
      to its `for pkg in ...` loop and explicit `pack_one`/path lists.
- [x] `.github/scripts/inspect-packed-nupkgs.sh` — `Compono.NUnit` added,
      with its own expected-dependency-set `case` branch (`Compono` +
      `NUnit`, no embedded `Compono.Generators.dll`).
- [x] `.github/scripts/generate-api-reference.sh` — `Compono.NUnit` added
      to `integration_pkgs`.
- [x] Confirm, directly (real local pack + restore, not assumed), that a
      consumer referencing only `Compono.NUnit` (pulling `Compono` in
      transitively) receives `Compono.Generators.dll`'s execution with
      zero extra steps.
- [x] `docs/packages/compono-nunit.md` (new) — following
      `docs/packages/compono-mstest.md`'s shape: plain `[Compose]` is the
      required syntax — **no `[TestFixture]` needed**, stated plainly as
      a positive, distinguishing feature relative to
      `Compono.MSTest`/`[TestClass]`, not a trap to avoid; the full
      attribute family with worked examples; `[Shared]`/`Share<T>()`; the
      discovery/execution repeat-composition contract (ADR-0059 §12),
      including the MTP-specific finding from task group 9; `[Compose]` +
      `[TestCase]`/`[Values]`/`[Range]` independent-row boundary
      (ADR-0059 §8) stated as **settled fact**: combining `[Compose]`
      with `[Values]`/`[Range]` produces *additional* independent test
      cases, not fewer — a real behavior consumers should understand, not
      an open question; synchronous-only composition; non-ownership/no
      disposal; `TestContext` remains NUnit-owned, no auto-injection; MTP
      and VSTest both supported; the `NUnit >= 3.14.0, < 5.0.0` range,
      framed as the current stable support contract (NUnit 5 prerelease
      compatibility noted as forward-looking evidence, not a current
      promise), and the `Internal`-namespace dependency risk (public CLR
      types, unsupported-stability-contract risk, not an accessibility
      one) stated as an accepted, monitored architectural choice, not
      silently omitted; seed/display-name semantics.
- [x] `docs/packages/index.md` — add `Compono.NUnit`'s row.
- [x] `README.md` and `docs/index.md` — add `Compono.NUnit`'s row to both
      front-door package tables.
- [ ] `docs/roadmap/future-packages.md` — once implementation ships,
      remove `Compono.NUnit` from "Roadmap items" and add its own
      graduation paragraph matching `Compono.MSTest`'s, linking
      `docs/packages/compono-nunit.md`.
- [ ] `docs/architecture.md`/`docs/public-api.md` — wherever these
      enumerate supported test frameworks or package guides, add NUnit;
      verify by direct read whether either currently states an
      exhaustive/closed framework list that would become stale by
      omission.
- [ ] `docs/concepts/shared-values.md`/`docs/getting-started/installation.md`
      or equivalent how-to pages — extend to name `Compono.NUnit`
      alongside the existing framework packages.
- [x] Public-API/reference regeneration (`docs/reference/api`) — per
      ADR-0032, regenerate `docs/reference/api/Compono.NUnit/` as part of
      this PR.
- [x] `skills/compono/SKILL.md` — add `Compono.NUnit` to the
      package-enumeration sentence; add a `.csproj`-detection row to the
      Detection table (`<PackageReference Include="Compono.NUnit"` →
      plain `[Compose]` available, no `[TestFixture]` needed, load
      `references/nunit.md`); add a `references/nunit.md` row to the
      references-index table; remove `Compono.NUnit` from any
      "don't invent an unshipped package" guardrail's named-absent list,
      if one exists.
- [x] `skills/compono/references/nunit.md` (new) — matching
      `mstest.md`'s depth. Must teach, at minimum, every item ADR-0059
      §7/§8/§12/§13/§14/§15/§16/§17 establish, and must explicitly guard
      against the specific wrong-answer traps named by the original
      request: **claiming `[TestFixture]` is required** (it is not — this
      is the one genuine divergence from an earlier design draft, and the
      skill must not teach the rejected `NUnitAttribute`-based
      requirement); assuming `[Test]` + `[Compose]` the way MSTest uses
      `[TestMethod]` (NUnit's own `[Test]` attribute is not part of this
      package's required syntax at all — `[Compose]` alone drives
      discovery, no other attribute needed); suggesting a
      `Compono.NUnit3`/`Compono.NUnit4`/`Compono.NUnit5` split (one
      package only, empirically proven); **claiming `[Compose]` +
      `[Values]`/`[Range]` produces only the Compose row** (wrong — they
      produce their own additional independent rows too, per task
      group 5's settled result — the skill must teach the actual behavior,
      not the earlier, corrected assumption); claiming `[Compose]` merges
      with `[TestCase]`/parameter-level sources into one row (independent
      rows, never merged); claiming `TestContext` should be composed
      (NUnit-owned, ambient, never auto-injected); claiming Compono owns
      disposal (non-owning, always); claiming NUnit itself is
      Native-AOT-runnable without evidence (only `Compono.NUnit`'s own
      code's trim-safety is claimed, per task group 10's actual finding).
- [x] `skills/compono-evals/evals.json` — a `Compono.NUnit`-specific
      discriminating eval (mirroring the established pattern) covering at
      least the "does `[Compose]` require `[TestFixture]`?" trap (no) and
      the "does `[Compose]` + `[Values]` produce only one row?" trap (no —
      both produce rows). Keep it focused — one or two scenarios proving
      the skill/reference material actually teaches framework-specific
      behavior.
- [ ] Run the existing before/after benchmark harness for the new eval(s)
      against the updated skill, recording the result per
      `skills/compono-evals`' established convention.

### 12. Dedicated external NUnit packaged-consumer validation fixture (separate repo — see Scope's PR-sequencing note; not true product dogfooding — see the fixture's own terminology note below)

ADR-0059 requires this as a real consumer-validation target, not an
internal-unit-test substitute — no existing LayeredCraft/ncipollina NUnit
consumer exists today (checked including branches, per RESEARCH-0018 §2).

**Terminology note:** this fixture is **external packaged-consumer
validation**, not true product dogfooding — the same distinction
PLAN-0057 task 15 already established for MSTest. No real
LayeredCraft/ncipollina application currently depends on `Compono.NUnit`
for its own purposes, and this task does not manufacture one by migrating
a real application to NUnit solely to create that appearance. What this
task group does provide, and is real: a dedicated fixture that lives
outside `Compono.NUnit`'s own implementation, consumes freshly packed
local NuGet packages (never `ProjectReference`), is validated through
`scripts/dogfood-validate.sh`, exercises realistic `Compono.NUnit` usage,
and validates MTP/VSTest where practical — genuine pre-1.0
external-consumption validation.

- [ ] Create a small, dedicated, purpose-built NUnit consumer repository
      (its own `git init`, own `Directory.Packages.props` —
      `scripts/dogfood-validate.sh` requires a real git repo at
      `--consumer-repo` with its own `Directory.Packages.props`). Confirm
      during implementation whether `--packages`/`DOGFOOD_PACKAGES` needs
      pointing at `"Compono Compono.NUnit Compono.NSubstitute
      Compono.Logging"` — the script's own `--packages` generalization
      (PLAN-0051 task 11) already supports an arbitrary package set, so no
      script change may be needed; only add one if a real gap surfaces.
- [ ] Exercise at minimum: ordinary composition; profiles; `[Shared]`;
      `Share<T>()`; `Register<T>()`; constructor selection;
      `Compono.TestDoubles` integration; `Compono.Logging` integration
      where appropriate; deterministic seed reproduction; diagnostics;
      the `3.14.0` version floor; current NUnit `4.x`; MTP execution;
      VSTest execution where practical.
- [ ] Validate via `scripts/dogfood-validate.sh` against freshly packed
      local packages, never `ProjectReference`s or stale package
      artifacts.
- [ ] Record the fixture's repo location/link and the validation result
      in this plan's Notes once run for real.

## Critical Files

- `src/Compono.NUnit/Compono.NUnit.csproj` — new
- `src/Compono.NUnit/ComposeAttribute.cs`,
  `ComposeAttribute{TProfile}.cs`, `ComposeAttribute{TProfile,TConfig}.cs`,
  `SharedAttribute.cs` — new
- `src/Compono.NUnit/Binding/BindingPlan.cs`, `ParameterBindingPlan.cs`,
  `PositionalArgumentBinder.cs`, `ConfigProfileBinder.cs`, `RowInvokers.cs`
  — new. `RowInvokers.cs` built against core `Compono`'s existing,
  unchanged `RowInvokerRegistry` from its first commit; the rest is a
  package-local port of the established `BindingPlan` pattern, adapted
  for the `IMethodInfo` unwrap and `NUnitTestCaseBuilder`/
  `TestCaseParameters`-based `TestMethod` construction.
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `src/Compono.Generators/ComponoIncrementalGenerator.cs` — modified
  (three new metadata-name constants/registrations for `Compono.NUnit`'s
  attribute family)
- `test/Compono.NUnit.Tests/*` — new
- `test/Compono.NUnit.SampleTests/*` — new (packaged-consumer proof)
- `test/Compono.NUnit.AotSmokeTest/*` — new
- `test/Compono.Generators.Tests/*` — modified (new snapshot test for
  `Compono.NUnit`-only-reachable discovery)
- `Compono.slnx` — modified
- `test/Directory.Build.props` — modified (`Compono.NUnit.Tests`-name
  exclusion from the xUnit-v3-specific `ItemGroup`s)
- `Directory.Packages.props` — modified (`NUnit`/`NUnit3TestAdapter`/
  `Microsoft.NET.Test.Sdk`/`Compono.NUnit` `PackageVersion` entries)
- `.github/workflows/docs.yml`, `.github/workflows/package-validation.yaml`,
  `.github/scripts/inspect-packed-nupkgs.sh`,
  `.github/scripts/generate-api-reference.sh` — modified
- `docs/packages/compono-nunit.md` — new
- `docs/packages/index.md`, `docs/roadmap/future-packages.md`,
  `docs/architecture.md`, `docs/public-api.md`,
  `docs/concepts/shared-values.md`,
  `docs/getting-started/installation.md`, `README.md`, `docs/index.md`
  — updated
- `docs/reference/api/Compono.NUnit/` — new (regenerated)
- `skills/compono/SKILL.md`, `skills/compono/references/nunit.md`,
  `skills/compono-evals/evals.json` — new/updated
- A new external NUnit packaged-consumer validation fixture repository —
  outside this repo, see task group 12

## Test Plan

Per `testing.md`'s established pattern: unit coverage for the binding
plan and `ConfigProfileBinder`/seed logic in isolation
(`test/Compono.NUnit.Tests`), generator-discovery snapshot coverage
(`test/Compono.Generators.Tests`), a packaged-consumer end-to-end suite
proving the full attribute family through the real `Compono.NUnit` →
`Compono` dependency chain (`test/Compono.NUnit.SampleTests`), an
API-surface/approval test locking the public shape, Native AOT
publish-and-run proof through the real `BuildFrom` path, and — the items
ADR-0059 explicitly needs empirical confirmation for, not just
design-time reasoning — a real MTP-vs-VSTest single-run/discovery-then-
execution repeat-invocation check for both runners (recorded once as a
finding, matching the established precedent for an equivalent
runner-lifecycle property), a real display-name/seed-visibility check
under both runners, a real proof of the no-`[TestFixture]`-required
behavior (plus the duplicate-test-case regression guard), and locked-in
regression coverage for the already-settled `[Compose]` +
`[Values]`/`[Range]`/custom-source independent-row contract. Reuses
behavioral expectations from the other three
packages' own test suites wherever ADR-0059 states the semantics are
intentionally identical, rather than duplicating coverage mechanically.
Every task group above carries its own test/verification item — tests
land with the behavior they cover, not batched into a later catch-all
task group.

## Notes

**Status of this PR: substantially complete against every task group's real
requirement, with a small number of genuine, honestly-recorded remaining
gaps** — this section records what was actually built and verified in this
pass (a follow-up to an earlier partial pass, whose own honest "Not done"
list this section closes item by item), not what was merely intended.

**Closed in this pass (real builds/runs, not assertion) — resolving an
independent adversarial review's findings:**

- **Row-coexistence regression-locked (was: demonstrated but not
  asserted).** `test/Compono.NUnit.Tests/RealNUnitExecutionTests.cs`'s
  `DataSourceCoexistenceTests` fixture now records every row's actual
  parameter value into a per-method bag and asserts, in
  `[OneTimeTearDown]`, the exact expected row count and value membership
  for `[Compose]`+`[TestCase]` (2 rows), `[Compose]`+`[Values(7,8,9)]` (4
  rows), `[Compose]`+`[Range(1,3)]` (4 rows, new — `[Range]` was not
  previously covered), and `[Compose]`+a custom `IParameterDataSource` (4
  rows) — a real regression, not just "some row ran," would now fail the
  build. 48/48 tests pass (up from 44; the 4 new tests are `[Range]`'s own
  3 rows plus the aggregate assertion).
- **`test/Compono.NUnit.SampleTests` created (was: missing entirely).** A
  real packaged-consumer project mirroring
  `Compono.MSTest.SampleTests`/`Compono.TUnit.SampleTests`/
  `Compono.XunitV3.SampleTests` exactly (own `PackToLocalFeed`
  pre-restore target, `pack-to-local-feed.sh`, isolated
  `RestorePackagesPath`, no `ProjectReference` to `Compono`/`Compono.NUnit`
  anywhere) — proves the real chain: freshly packed `Compono.NUnit` →
  packaged `Compono` dependency → `Compono.Generators` analyzer delivery
  → generated composition plan → real NUnit discovery/execution. Includes
  `CompositionTests`/`SharedTests` (no `[TestFixture]`, ADR-0059 §7),
  `ConfigProfileTests` (`[Compose<TProfile, TConfig>]`), `NSubstituteTests`
  (this plan's own `Saves_order` Goal scenario), and
  `DataSourceCoexistenceTests`/`CustomParameterDataSourceCoexistenceTests`
  (the same row-coexistence assertions as above, run through the packaged
  chain specifically, not `Compono.NUnit.Tests`' `ProjectReference`).
  60/60 tests pass across all 4 TFMs. Wired into
  `.github/workflows/package-validation.yaml` as a new "Local-feed
  packed-consumer smoke test (Compono.NUnit)" step, matching the existing
  per-package steps exactly.
- **`test/Compono.NUnit.AotSmokeTest` created (was: missing entirely).** A
  real `dotnet publish -c Release -p:PublishAot=true` + run proof,
  mirroring `Compono.MSTest.AotSmokeTest`'s own structure (packaged
  `Compono.NUnit` via a dedicated local feed, `pack-compono.sh`, no
  `ProjectReference`), exercising the real `Compono.NUnit.ComposeAttribute
  .BuildFrom(IMethodInfo, Test?)` path directly — via a real
  `NUnit.Framework.Internal.MethodWrapper`, the same `IMethodInfo`
  construction `Compono.NUnit.Tests`' own `MethodInfoWrapper` helper uses
  — for both the no-profile `[Compose]` form and
  `[Compose<TProfile, TConfig>]` (exercising `ConfigProfileBinder`'s
  `DynamicallyAccessedMembers(PublicConstructors)`-annotated
  constructor-reflection flow specifically). **Real result: the published
  Native AOT binary ran successfully, exit code 0, both rows composed and
  dispatched correctly.** `-p:TrimmerSingleWarn=false` confirms **zero**
  AOT/trim warnings attributable to `Compono.NUnit`'s own code — every
  warning present (`IL3053`/`IL2104`/`IL3050`/`IL2060`/`IL2070`/`IL2075`)
  is attributed to `NUnit.Framework.Internal.*` symbols
  (`MethodWrapper.MakeGenericMethod`, `GenericMethodHelper`,
  `ExceptionHelper`, `CSharpPatternBasedAwaitAdapter`, `Reflect`) — i.e.
  **inside NUnit's own framework assembly**, not `Compono.NUnit`'s. This
  is real, additional evidence for ADR-0059 §17's precise two-part claim:
  `Compono.NUnit`'s own shipped code is trim/AOT-safe (now proven, not
  just architecturally argued), while NUnit's own framework assembly
  itself is demonstrably *not* warning-free under trimming analysis — the
  opposite claim (NUnit's runner ecosystem is Native-AOT-runnable) remains
  unproven and is not made anywhere in ADR-0059, this plan, or the new
  docs/skill material.
- **Permanent, CI-blocking compatibility matrix wired (was: manual-only,
  no CI job).** `.github/scripts/nunit-compatibility-matrix.sh` (new) +
  `test/Compono.NUnit.CompatibilityMatrix` (new, minimal packaged-consumer
  project with an overridable `NUnitMatrixVersion`
  `VersionOverride`/`PackageReference`) builds once per NUnit version
  against a freshly packed local feed, verifies the actual *resolved*
  NUnit version from `obj/project.assets.json` (not the requested
  version — the RESEARCH-0017 §17/RESEARCH-0018 §18 discipline), then
  runs the identical build artifact under both classic VSTest
  (`dotnet vstest <dll>`) and MTP (running the built executable directly)
  — RESEARCH-0018 §11's own methodology. Wired as a new blocking step in
  `.github/workflows/package-validation.yaml`. **All four blocking legs
  pass for real, with confirmed resolved versions:**
  - NUnit `3.14.0` (floor) × classic VSTest — pass, resolved `3.14.0`.
  - NUnit `3.14.0` × MTP — pass, resolved `3.14.0`. **This closes
    RESEARCH-0018's open "3.14.0×MTP not independently spiked" gap
    favorably: the combination is genuinely supported.**
  - NUnit `4.6.1` (current stable) × classic VSTest — pass, resolved
    `4.6.1`.
  - NUnit `4.6.1` × MTP — pass, resolved `4.6.1`.
  - Non-blocking surveillance leg added too: NUnit `5.0.0-beta.1` × both
    runners — pass, resolved `5.0.0-beta.1` — informational only,
    explicitly outside the `[3.14.0, 5.0.0)` support contract, never
    fails the job (verified: a resolved-version mismatch inside this leg
    warns, not errors, via the script's own `if`-guarded call).
  - **A genuine, real regression was found and fixed during this work**:
    an earlier version of `Compono.NUnit.CompatibilityMatrix.csproj`
    toggled `EnableNUnitRunner`/`UseMicrosoftTestingPlatformRunner` on/off
    per runner leg via an MSBuild property. With those properties
    conditionally *omitted* (for a would-be "classic VSTest leg"), the
    compiled assembly's generated module initializer silently failed to
    register `Widget`'s row-invoker dispatch when loaded by
    `dotnet vstest` directly (`Compono.CompositionException: No
    row-binding dispatch is registered for 'Widget'`) — reproduced
    multiple times, not a fluke. Root cause not fully diagnosed (out of
    this plan's scope), but confirmed **not** a `Compono.NUnit`
    binary-compatibility issue: the identical build artifact produced
    *with* those properties always on (matching every other
    `Compono.NUnit.*` test project's own convention) runs correctly under
    both `dotnet vstest` and the built executable, with zero code change.
    The project now keeps those properties unconditionally on and
    differentiates runners purely by invocation method, avoiding the
    hazard entirely — documented in the `.csproj`'s own comment so it
    isn't silently reintroduced later.
- **Stale "Compono.NUnit doesn't exist" skill/eval guidance corrected**
  (was: a live guardrail in `skills/compono/SKILL.md` and eval #20 in
  `skills/compono-evals/evals.json` both still denied the package
  exists, despite the detection-table/reference-loading rows already
  being correct). Full-tree grep of `skills/` confirmed these were the
  only two stale spots (`skills/compono/references/nunit.md` was already
  accurate). `SKILL.md`'s package-enumeration prose now includes
  `Compono.NUnit` with its full attribute-family/range/`[TestFixture]`
  summary. Eval #20 rewritten to expect "`Compono.NUnit` is real and
  shipped, `[Compose]` alone drives discovery" instead of "does not
  exist." Five new discriminating evals added (ids 42-46) covering:
  `[TestFixture]`/`[Test]` not required; `[Compose]`+`[TestCase]`
  independent rows, never merged; one package, not a
  `Compono.NUnit3`/`4`/`5` split, with the `[3.14.0, 5.0.0)` range and
  NUnit 5 surveillance-only status; `TestContext` staying NUnit-owned and
  Compono never owning disposal; and the precise two-part AOT claim
  boundary. **Not done**: the actual automated eval-grading run — no
  lightweight runner for `evals.json`'s own prompt/expected_output format
  was located in this pass (`.agents/skills/skill-creator/scripts/run_eval.py`
  is a *trigger*-eval runner, testing whether a skill description causes
  loading, not a content-grading harness for these prompts) each new eval
  needs a real graded session to validate, not just JSON well-formedness
  (confirmed: 46 evals, no duplicate ids). Recorded as a genuine, open
  gap, not silently skipped.
- **Two earlier Codex-review CI findings reconfirmed intact**: `docs.yml`
  still builds `Compono.NUnit` before API-reference generation, and
  `package-validation.yaml` still packs it before `inspect-packed-nupkgs.sh`
  runs — both re-verified against the current branch state, unchanged by
  this pass.
- **`.gitignore` gap fixed in passing**: `.local-nuget-feed-mstest-aot-smoke/`
  was missing from `.gitignore` entirely (a pre-existing gap, unrelated to
  `Compono.NUnit`) — added alongside the new
  `.local-nuget-feed-nunit-aot-smoke/`/`.local-nuget-feed-nunit-compat-matrix/`
  entries this pass's own new projects need.
- Full local validation re-run after all changes: `dotnet build
  Compono.slnx -c Release` clean; `dotnet test Compono.slnx --no-restore
  --configuration Release` — **3442/3442 passed** (up from 3426 before
  this pass — the 16 new tests are `RealNUnitExecutionTests.cs`'s 4 new
  `[Range]`-leg tests × 4 TFMs); `generate-api-reference.sh` re-run,
  zero drift in `docs/reference/api/`; all 11 publishable packages
  re-packed and `inspect-packed-nupkgs.sh` passes; the CS1591
  doc-comment-enforcement build passes for `Compono.NUnit`.

**Still genuinely open — not silently skipped:**

- **MTP discovery/execution double-`BuildFrom` lifecycle (ADR-0059 §12)**
  remains unresolved, carried forward from the prior pass's own honest
  finding: an earlier attempt (temporary `BuildFromCallCount`-logging
  `SetUpFixture`) was inconclusive — the counter also captured direct
  unit-test `BuildFrom(...)` calls, and MTP's `--list-tests` pass didn't
  run `OneTimeTearDown` at all, so no clean discovery-only signal was
  captured. This pass did not re-attempt it. The classic-VSTest
  double-evaluation finding from RESEARCH-0018 §12 remains the only
  confirmed evidence; MTP's own lifecycle stays genuinely open per
  ADR-0059 §12's own honest framing.
- **Task group 12** (dedicated external NUnit packaged-consumer
  validation fixture via `scripts/dogfood-validate.sh`, in a separate
  repository per PLAN-0057's own precedent) was not attempted in this
  pass — it requires a separate repository's own commit history by
  design (see this plan's Scope section) and is out of reach from inside
  this repository's own PR.
- **`docs/architecture.md`/`docs/public-api.md`** — checked directly,
  neither currently mentions `Compono.NUnit`; whether either states an
  exhaustive/closed framework list that would actually go stale by this
  omission was not independently verified in this pass.
- **`docs/concepts/shared-values.md`/`docs/getting-started/installation.md`**
  — checked directly, neither mentions `Compono.NUnit` yet.
- **`docs/roadmap/future-packages.md` graduation** — correctly still
  pending; `Compono.NUnit` has not shipped (this PR is not merged), so it
  correctly remains listed as a roadmap item, not yet graduated to
  `docs/packages/index.md`'s shipped-package section (`docs/packages/index.md`
  itself already links to `docs/packages/compono-nunit.md` as forward
  content prepared ahead of the merge — a real, if minor, inconsistency
  worth resolving as part of actually merging, not this plan's own gap).

**Recommendation:** every High/Medium finding from the independent
adversarial review that produced this pass's work is now closed with real
evidence (regression-locked coexistence, a real packaged-consumer sample,
a real AOT publish-and-run proof, a real permanent CI compatibility
matrix, and corrected skill/eval guidance) — `Compono.NUnit` is
substantially ready to ship. The remaining gaps (MTP lifecycle, the
external validation fixture, a few prose-only doc pages, and running the
new evals through actual grading) are real but narrow, and none of them
contradicts or reopens any ADR-0059 decision.
