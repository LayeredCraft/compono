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

**Note:** ADR-0059 is `Accepted` (2026-09-03). This plan is prepared for
implementation but has not started — `Status` above stays `Not Started`
until implementation work actually begins, not merely because the ADR is
accepted.

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

- [ ] `src/Compono.NUnit/Compono.NUnit.csproj` — `net8.0;net9.0;net10.0;net11.0`
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
- [ ] `Directory.Packages.props`: add
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
- [ ] `test/Compono.NUnit.Tests/Compono.NUnit.Tests.csproj` — this
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
- [ ] `Compono.slnx`: add both new projects.

### 2. Public API surface (ADR-0059 §4, frozen — no deviation without stopping to report)

- [ ] `ComposeAttribute : TestAttribute, ITestBuilder` (revised
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
- [ ] `ComposeAttribute<TProfile> : ComposeAttribute where TProfile :
      ICompositionProfile, new()` — inline-value constructor
      pass-through, `sealed`. Confirm the inline-value constructor ships
      on the *base* type in this same task group, not deferred later —
      the exact binary-compatibility mistake PLAN-0040's own review round
      caught and fixed; do not repeat it.
- [ ] `ComposeAttribute<TProfile, TConfig> : ComposeAttribute where
      TProfile : ICompositionProfile` (no `new()` constraint) — `public
      ComposeAttribute(params object?[] configArguments) : base()` (zero
      inline values passed to the base; `configArguments` bound to
      `TConfig`'s single public constructor via a package-local
      `ConfigProfileBinder`, then `TProfile` constructed and applied via
      `CompositionBuilder.AddProfile`). Negative-seed validation runs
      before any config/profile binding is attempted, matching the
      established ordering from every prior package.
- [ ] `SharedAttribute` — `[AttributeUsage(AttributeTargets.Parameter,
      AllowMultiple = false)]`, a `Compono.NUnit`-specific *public* marker
      (part of the package's public API, mirroring `Compono.XunitV3`/
      `Compono.TUnit`/`Compono.MSTest`'s own per-package `SharedAttribute`
      types) with the existing shape/duplicate-shared-type validation.
- [ ] Stacked Compose-family attribute validation: reject a method
      carrying more than one of `[Compose]`/`[Compose<TProfile>]`/
      `[Compose<TProfile, TConfig>]` — `AllowMultiple = false` is
      per-exact-type only, so nothing else stops two *different*
      Compose-family types stacking on one method.
- [ ] `test/Compono.NUnit.Tests`: API-surface/approval test locking the
      exact four-type public shape (`ComposeAttribute`,
      `` ComposeAttribute`1``, `` ComposeAttribute`2``, `SharedAttribute`),
      matching every other package's existing pattern.

### 3. Binding implementation (`src/Compono.NUnit/Binding/*`)

- [ ] `BindingPlan.cs`/`ParameterBindingPlan.cs`/`PositionalArgumentBinder.cs`
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
- [ ] `RowInvokers.cs` — built against core `Compono`'s existing
      `RowInvokerRegistry.TryGet` from its first commit (ADR-0041). No
      throwaway `MakeGenericMethod`/`Delegate.CreateDelegate`-based
      version ships first.
- [ ] `ConfigProfileBinder.cs` — package-local port of the established
      pattern (constructor-shape lookup for `TConfig`/`TProfile` via
      reflection, bounded to once per attribute instance by the same
      `Lazy<Composer>`-backed caching pattern, never on the repeated
      per-`BuildFrom`-call path). Unsupported constructor shapes are a
      deterministic `CompositionException`, not a compile error.
- [ ] `ComposeAttribute.BuildFrom(IMethodInfo method, Test? suite)`: **the
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
- [ ] Every `Resolve`/`ResolveShared`/`ShareExplicit` call wrapped to
      catch `CompositionException` and rethrow via
      `CompositionException.WithSeedInMessage(exception, row.Seed)` — the
      same unconditional, pasteable-seed guarantee every other package
      already makes.
- [ ] `test/Compono.NUnit.Tests`: binding-plan unit coverage mirroring
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

- [ ] `test/Compono.NUnit.Tests` (or the sample project, task group 8): an
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

- [ ] Real, permanent regression coverage for `[Compose]` on the same
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

- [ ] `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
      `src/Compono.Generators/ComponoIncrementalGenerator.cs`: three new
      metadata-name constants (`Compono.NUnit.ComposeAttribute`/`` `1``/
      `` `2``) and three new `SyntaxValueProvider
      .ForAttributeWithMetadataName` registrations, feeding the existing,
      already attribute-family-agnostic `ComposeMethodDiscovery
      .TransformMethod` — no fork or reimplementation of that method
      (ADR-0059 §10).
- [ ] `test/Compono.Generators.Tests`: a snapshot test proving a concrete
      parameter type reachable *only* through a `Compono.NUnit`-attributed
      method's own parameter (no other discovery path in the same
      compilation) receives a generated composition plan and a
      `RowInvokerRegistry` registration — mirroring the equivalent
      regression coverage the other three packages already have.
- [ ] Generator-discovery packaged-consumer proof: **satisfied by task
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

- [ ] `BuildFrom` sets the constructed `TestMethod.Name` to a seed-bearing
      display string reflecting the row's actual seed, not a placeholder.
- [ ] `test/Compono.NUnit.Tests`: unit coverage that the constructed
      `TestMethod.Name` contains the exact seed a given
      `[Compose(Seed = N)]`/generated-seed row used.
- [ ] Real-run verification (task group 9) that the display name is
      actually visible in `dotnet test`/Test Explorer output under both
      MTP and the classic VSTest adapter — a design-time claim confirmed
      against a real runner, not assumed from the API contract alone.

### 8. Packaged-consumer sample project (`test/Compono.NUnit.SampleTests`)

- [ ] A real packaged-consumer project (mirroring the established
      pattern exactly) exercising the *complete* attribute family
      (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/
      `[Shared]`) through the actual packaged `Compono.NUnit` → `Compono`
      dependency chain, not `Compono.NUnit.Tests`' own `ProjectReference`-
      based calls — a `ProjectReference` doesn't propagate
      `Compono.Generators` as an analyzer the way a packed nupkg's
      `analyzers/dotnet/cs` delivery does.
- [ ] Includes the `NSubstituteTests.Saves_order`-shaped scenario from
      this plan's Goal section, run for real (needs `Compono.NSubstitute`
      as an additional project dependency, matching the other sample
      projects).
- [ ] Includes `ConfigProfileTests`-shaped coverage for
      `[Compose<TProfile, TConfig>]`, mirroring the other three sample
      projects.
- [ ] Includes the no-`[TestFixture]`-required scenario (task group 4),
      `[TestCase]`/`[Compose]` coexistence (ADR-0059 §8's "independent
      rows" finding applied for real, using `[TestCase]` specifically),
      and the `[Values]`/`[Range]`/custom-source coexistence scenarios
      (task group 5) end to end, through the real packaged dependency
      chain.

### 9. MTP/VSTest and version-compatibility validation

- [ ] Confirm `Compono.NUnit` works correctly under **both** MTP
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

- [ ] `NUnit` `3.14.0` (the floor) × classic VSTest adapter — blocking.
- [ ] `NUnit` `3.14.0` × MTP — blocking, **only if this specific
      combination is genuinely supported**; RESEARCH-0018/the
      pre-acceptance spike tested `3.14.0` under classic VSTest and
      `4.6.1`/`5.0.0-beta.1` under both runners, but did not test
      `3.14.0` × MTP specifically. Verify this first. If unsupported,
      record that as a genuine finding and drop this leg rather than
      silently assuming it works.
- [ ] Current stable `NUnit` `4.x` (the latest patch as of implementation
      time) × classic VSTest adapter — blocking.
- [ ] Current stable `NUnit` `4.x` × MTP — blocking.
- [ ] **Every blocking leg above must independently verify the
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

- [ ] A scheduled or manually-triggered, **non-blocking** spike against
      the current `NUnit` `5.x` prerelease (`5.0.0-beta.1` or whatever is
      current at implementation time). This tracks whether a future
      `< 6.0.0` range widening (ADR-0059 §3) is likely to stay safe; it
      is explicitly not part of the current `[3.14.0, 5.0.0)` support
      contract and must not block merges or releases. Once NUnit 5 ships
      stable and ADR-0059's range is amended, promote this leg into the
      blocking matrix above.
- [ ] Record the actual compatibility matrix exercised (NUnit version ×
      MTP/VSTest, with resolved assembly versions, blocking vs.
      surveillance) in this plan's Notes once run for real.

### 10. Native AOT / reflection-free validation

- [ ] Confirm `Compono.NUnit`'s own code contains no `MakeGenericType`,
      `Activator.CreateInstance` (beyond `ConfigProfileBinder`'s existing,
      already-accepted bounded reflection pattern), `MakeGenericMethod`,
      or reflection-based fallback construction outside the
      `MethodInfo`/`ParameterInfo` metadata access ADR-0059 §17 explicitly
      accepts as framework-required.
- [ ] **Day-one requirement, not discovered-by-failure**: implement
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
- [ ] A dedicated `test/Compono.NUnit.AotSmokeTest` (or equivalent)
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
- [ ] Source-level guard: a simple text/syntax scan over
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

- [ ] `Compono.slnx` — the two core project entries added (task group 1).
      The sample/AOT-smoke projects (`Compono.NUnit.SampleTests`,
      `Compono.NUnit.AotSmokeTest`) are deliberately **not** added,
      matching the other three packages' own precedent — manual,
      one-shot/local-feed-driven proofs run outside `dotnet build
      Compono.slnx`.
- [ ] `.github/workflows/docs.yml` — `src/Compono.NUnit/**` added to both
      `paths:` trigger lists, `Compono.NUnit` added to the API-reference
      build loop.
- [ ] `.github/workflows/package-validation.yaml` — `Compono.NUnit` added
      to its `for pkg in ...` loop and explicit `pack_one`/path lists.
- [ ] `.github/scripts/inspect-packed-nupkgs.sh` — `Compono.NUnit` added,
      with its own expected-dependency-set `case` branch (`Compono` +
      `NUnit`, no embedded `Compono.Generators.dll`).
- [ ] `.github/scripts/generate-api-reference.sh` — `Compono.NUnit` added
      to `integration_pkgs`.
- [ ] Confirm, directly (real local pack + restore, not assumed), that a
      consumer referencing only `Compono.NUnit` (pulling `Compono` in
      transitively) receives `Compono.Generators.dll`'s execution with
      zero extra steps.
- [ ] `docs/packages/compono-nunit.md` (new) — following
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
- [ ] `docs/packages/index.md` — add `Compono.NUnit`'s row.
- [ ] `README.md` and `docs/index.md` — add `Compono.NUnit`'s row to both
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
- [ ] Public-API/reference regeneration (`docs/reference/api`) — per
      ADR-0032, regenerate `docs/reference/api/Compono.NUnit/` as part of
      this PR.
- [ ] `skills/compono/SKILL.md` — add `Compono.NUnit` to the
      package-enumeration sentence; add a `.csproj`-detection row to the
      Detection table (`<PackageReference Include="Compono.NUnit"` →
      plain `[Compose]` available, no `[TestFixture]` needed, load
      `references/nunit.md`); add a `references/nunit.md` row to the
      references-index table; remove `Compono.NUnit` from any
      "don't invent an unshipped package" guardrail's named-absent list,
      if one exists.
- [ ] `skills/compono/references/nunit.md` (new) — matching
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
- [ ] `skills/compono-evals/evals.json` — a `Compono.NUnit`-specific
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

**Status of this PR: substantial, real progress — not a complete
implementation of every task group above.** This section records what was
actually done, what was actually verified, and what remains, honestly,
rather than claiming full completion.

**Done and verified (real builds/test runs, not assertion):**

- Task groups 1-7 (package/project creation, public API surface,
  `Binding/*`, no-`[TestFixture]`-required behavior, parameter-source
  coexistence, generator discovery, seed/display-name semantics) are
  implemented and covered by 44 passing tests in
  `test/Compono.NUnit.Tests`, including real, undecorated (no
  `[TestFixture]`) NUnit classes actually discovered and executed by this
  project's own real NUnit test host (`RealNUnitExecutionTests.cs`), not
  merely direct `BuildFrom(...)` calls.
- **`[Compose]`+parameter-source coexistence (ADR-0059 §8) — settled,
  including the custom `IParameterDataSource` leg**: real execution
  confirms `[Compose]`+`[TestCase]`, `[Compose]`+`[Values]`, and
  `[Compose]`+a custom `IParameterDataSource` all produce independent,
  non-merged rows (`--list-tests` output: `ComposedAndValues_...(Compono,
  seed: ...)` plus separate `(7)`/`(8)`/`(9)` rows; same shape for the
  custom source).
- **NUnit version × runner compatibility — all four legs proven with real
  resolved assembly versions**, not just declared `PackageReference`:
  - NUnit 3.14.0 (the CPM range's natural floor resolution) × classic
    VSTest (`dotnet vstest`): 44/44 pass.
  - NUnit 3.14.0 × MTP (`dotnet test`, `UseMicrosoftTestingPlatformRunner`):
    44/44 pass. This closes RESEARCH-0018's open "3.14.0×MTP not
    independently spiked" gap favorably.
  - NUnit 4.6.1 (via a temporary `VersionOverride`, confirmed via
    `nunit.framework.dll`'s own embedded version string, then reverted -
    not committed) × classic VSTest: 44/44 pass.
  - NUnit 4.6.1 × MTP: 44/44 pass.
  - **Not done**: wiring these four legs into permanent, automated CI (no
    CI workflow file changes were made in this PR) or a stable-NUnit-5
    leg (still prerelease, correctly out of the blocking matrix per
    ADR-0059 §3).
- **A real, non-obvious discovery-mechanics finding this implementation
  surfaced, not present in RESEARCH-0018/ADR-0059**: `ComposeAttribute`
  derives from `TestAttribute`, so *any* `[Compose]`-attributed method in
  the actual NUnit test host assembly becomes a real, executing NUnit
  test — including deliberately-invalid-signature fixture methods meant
  only for `BindingPlan` reflection tests (e.g. "more than one
  Compose-family attribute"). Unlike `Compono.XunitV3`/`Compono.TUnit`/
  `Compono.MSTest` (where `[Compose]` alone never triggers discovery),
  these fixtures had to move to a separate, non-test assembly
  (`test/Compono.NUnit.SignatureFixtures`) the NUnit adapter never scans
  as a test container - see that project's own top-of-file comment. This
  is a real consequence of the `TestAttribute`-based seam worth folding
  into ADR-0059 itself in a future amendment if this pattern recurs.
- Package/nupkg validation: `.github/scripts/inspect-packed-nupkgs.sh`
  extended with a `Compono.NUnit` case (title, exact `Compono` pin,
  `NUnit` range sourced from `Directory.Packages.props`, no duplicated
  literal) and manually verified against a real `dotnet pack` output -
  the packed `.nuspec` advertises exactly `[3.14.0, 5.0.0)` for `NUnit`
  and `[1.0.0]` for `Compono`.
- API reference: `.github/scripts/generate-api-reference.sh` extended
  with `Compono.NUnit` and actually run - `docs/reference/api/Compono.NUnit/`
  is real, generated output, not a stub.
- Documentation/skills: `docs/packages/compono-nunit.md` (new package
  guide), `docs/packages/index.md`/`README.md` (package-count/table rows),
  `skills/compono/references/nunit.md` (new reference, covering every
  known wrong-answer trap from ADR-0059's design history), and
  `skills/compono/SKILL.md`'s detection table + reference-loading table
  rows.
- A `NUnitComposeAttributedMethodParameter_GeneratesCompositionPlan`
  generator snapshot test was added to
  `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs`, proving
  a type reachable only through an NUnit `[Compose]` method gets a real
  generated composition plan + `RowInvokerRegistry` entry - the plan's own
  task-group-6 requirement.

**Not done — genuine gaps, not silently skipped:**

- **Task group 8** (`test/Compono.NUnit.SampleTests` packaged-consumer
  sample project) and **task group 12** (dedicated external
  packaged-consumer validation fixture via `scripts/dogfood-validate.sh`)
  — neither was created. The four compatibility legs above were verified
  via `ProjectReference` + `VersionOverride`, not via a real packed
  local-feed consumer the way `Compono.MSTest`'s own PLAN-0057 task 8/15
  did it. This is the largest real gap in this PR.
- **Task group 10** (Native AOT): no `Compono.NUnit.AotSmokeTest` project
  was created. The two-part AOT claim in ADR-0059 §17 is preserved as an
  *architectural* claim (no `MakeGenericType`/`Activator.CreateInstance`/
  dynamic generic activation anywhere in `src/Compono.NUnit` -
  `ReflectionSourceGuardTests` enforces this), but is **not yet backed by
  a real trim/Native-AOT publish-and-run proof** the way the other three
  framework packages' own AOT smoke tests back theirs. Do not treat
  `Compono.NUnit` as AOT-validated until this exists.
- **MTP discovery/execution double-`BuildFrom` lifecycle (ADR-0059 §12)**:
  attempted via a temporary `BuildFromCallCount`-logging `SetUpFixture`
  comparing a combined `dotnet test` run against a separate
  `--list-tests` + `dotnet test` pair. Inconclusive: the counter also
  captures direct unit-test `BuildFrom(...)` calls from
  `ComposeAttributeBindingTests`/`ComposeAttributeConfigBindingTests`
  (not just NUnit-invoked ones), and MTP's `--list-tests` pass didn't run
  `OneTimeTearDown` at all, so no clean discovery-only signal was
  captured. The classic-VSTest double-evaluation finding from
  RESEARCH-0018 §12 stands as the only confirmed evidence; MTP's own
  lifecycle remains genuinely open, per ADR-0059 §12's own honest
  framing - not resolved by this PR.
- **CI workflow wiring**: no `.github/workflows/*.yml` changes were made
  to actually run `Compono.NUnit.Tests`, the compatibility matrix, or the
  new package-validation/API-reference-generation cases automatically.
  These scripts were run manually and verified to work; wiring them into
  CI is still needed before this is a complete definition of done.
- **`skills/compono/SKILL.md` prose sync**: the detection table and
  reference-loading table rows were added (the load-bearing, functional
  parts), but several prose passages elsewhere in the file that
  enumerate "`Compono.XunitV3`, `Compono.TUnit`, or `Compono.MSTest`"
  (e.g. around profile-selection guidance, migration guidance) were not
  individually updated to include `Compono.NUnit`. Not incorrect, just
  incomplete.
- **Discriminating evals**: none were added for `Compono.NUnit` (the
  established eval mechanism this repo uses for other packages was not
  located/exercised in this pass).
- **`ReflectionSourceGuardTests`**: reused unmodified from the design
  round's earlier work, ported from `Compono.MSTest.Tests` (real
  precedent, ADR-0057 §14) rather than invented for this package.

**Recommendation:** treat this PR as a strong, test-verified foundation
(core package, binding, generator wiring, discovery/coexistence
behavioral contracts, package/API-reference validation, and core
documentation are all real and passing) that still needs a follow-up
pass for the packaged-consumer sample/external-validation fixture, the
AOT smoke test, CI wiring, and the MTP lifecycle question before
`Compono.NUnit` should actually ship.
