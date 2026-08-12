# [PLAN-0040] Compono.TUnit Package Design

**Status:** Done

**Implements:** [ADR-0040](../adr/0040-compono-tunit-package-design.md)
(`Compono.TUnit` package: method-parameter composition only, no new
public runtime `Compono` API (a discovery-time-only `Compono.Generators`
extension is required, see ADR-0040's "Generator discovery" section),
root-argument disposal owned by TUnit only — a nested composed dependency
is disposed by no one, and an externally-owned shared disposable composed
as a root parameter is unsafe, both documented limitations, not solved
problems — seed observability via `ITestDiscoveryEventReceiver`/
`DiscoveredTestContext.AddProperty`)

**Note:** ADR-0040 is `Accepted` as of this plan's creation (2026-08-11) —
implementation may begin.

## Goal

```csharp
// Applies UseNSubstitute() to this row's own CompositionBuilder, exactly like
// Compono.XunitV3.SampleTests.NSubstituteTestProfile does today.
public sealed class NSubstituteTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseNSubstitute();
}

[Test]
[Compose<NSubstituteTestProfile>]
public async Task Saves_order(
    [Shared] IOrderRepository repository,
    CreateOrderHandler handler,
    PlaceOrder command)
{
    await handler.Handle(command);

    await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

runs end-to-end under TUnit — the exact scenario `Compono.XunitV3.SampleTests
.NSubstituteTests.Saves_order` already proves for xUnit v3, reproduced here
with no `var composer = ...` local variable and no unqualified `[Compose]`
(an unqualified `[Compose]` builds its own default composer with no
registered providers, per `ComposeAttribute`'s own design — resolving
`IOrderRepository` needs `[Compose<TProfile>]` with a profile that calls
`UseNSubstitute()`, exactly like the real xUnit v3 test does): `repository`/
`handler`/`command` are all composed through one `CompositionRow`,
`repository` is reused as the exact same instance inside `handler`'s own
composed constructor parameter (mirroring `Compono.XunitV3`'s own
`[Shared]` scenario, now proven a second time under a structurally
different test framework); the row's seed is discoverable via TUnit's own
reporting surface (`TestContext`/`DiscoveredTestContext.AddProperty`)
whether the test passes or fails, not only on failure; no `Compono.TUnit`
code disposes the *root* composed values, and a real TUnit test run
confirms it — with the equally real, documented limitation that a nested
composed dependency is disposed by no one, and an externally-owned shared
disposable must never be composed as a root parameter (see ADR-0040's
disposal section).

## Scope

Per ADR-0040's Decision Outcome:

- New `Compono.TUnit` package: `ComposeAttribute`/
  `ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>`
  (full parity with `Compono.XunitV3`'s attribute family),
  `SharedAttribute`, the `UntypedDataSourceGeneratorAttribute`-based
  binding implementation, `ITestDiscoveryEventReceiver`-based seed
  observability.
- New test project: `test/Compono.TUnit.Tests`.
- A `Compono.Generators` discovery extension (three new metadata-name
  registrations feeding the existing `ComposeMethodDiscovery`) — real
  work inside the embedded generator, not new public runtime API; see
  ADR-0040's "Generator discovery" section for why this is required, not
  a nice-to-have.
- Build/CI infrastructure wiring: `Compono.slnx` and every workflow/script
  that currently enumerates the four shipped packages by name
  (`docs.yml`, `package-validation.yaml`, `inspect-packed-nupkgs.sh`,
  `generate-api-reference.sh`) need `Compono.TUnit` added alongside them.
- A real packaged-consumer sample project (mirroring PLAN-0004 Phase 3 /
  PLAN-0005 Phase 2's precedent — a `ProjectReference`-only build cannot
  surface a real packaging bug, only an actual `dotnet add package`-style
  consumer run can).
- Doc updates: new `docs/packages/compono-tunit.md` guide,
  `docs/packages/index.md`, `docs/roadmap/future-packages.md` (candidate →
  shipped), existing topic docs that go stale the moment a second
  `[Compose]`-family package ships (`docs/public-api.md`,
  `docs/concepts/shared-values.md`, installation/how-to pages),
  `skills/compono` (new `references/tunit.md`, Detection table row,
  `SKILL.md` description/guardrail update — this is the real trigger
  ADR-0035's escape hatch anticipated, a package that actually ships, not
  merely an admitted candidate per PLAN-0039 Phase 3's narrower scope).

**Docs and tests land in the phase that introduces the behavior, not a
later catch-all phase** — each phase below ships as its own PR
(`design-decisions.md`'s phase rule), and a phase that merges a new public
type or attribute without its own test coverage and doc update leaves the
repository's docs/tests behind reality for however long until a later
phase's PR lands. Phase 0 and Phase 1 each carry their own tests and doc
updates for what they introduce; Phase 2 is left for verification that
genuinely needs the completed attribute family (a real packaged-consumer
run, the final API-surface lock); Phase 3 is a closing consistency pass,
not the first time docs/skill content appears.

Explicitly deferred/non-goals — see ADR-0040's own Decision Outcome and
Negative Consequences:

- Constructor-dependency composition via `IClassConstructor` — rejected
  outright, not deferred, per ADR-0040's evidence.
- Constructor-dependency composition via a class-level
  `DataGeneratorType.ClassParameters` data source — named as the correct
  future path if real demand appears, not designed or built here.
- ~~Extracting `Compono.XunitV3`'s binding-delegate-caching pattern into a
  shared location~~ — **superseded by [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)/
  [PLAN-0041](0041-aot-safe-row-binding-dispatch.md).** ADR-0040's original
  duplication call stood only as long as the pattern being duplicated
  (`MethodInfo.MakeGenericMethod`-based dispatch) was itself acceptable to
  ship — ADR-0041 found it isn't (Native AOT-unsafe, a release requirement
  for `Compono.TUnit`), and its replacement (`RowInvokerRegistry`, per
  ADR-0041 Amendment 2) is a shared, framework-agnostic core mechanism by
  construction, not a duplicated per-package one. `src/Compono.TUnit/Binding/RowInvokers.cs`
  is built against `RowInvokerRegistry` from its first commit — never a
  duplicated `MakeGenericMethod` version. PLAN-0041 is scoped to core +
  generator + `Compono.XunitV3` only (buildable/completable on `main`
  alone - see that plan's own round-2 Notes for why); once it merges to
  `main`, this phase's own branch rebases onto it and implements the two
  explicit tasks below against the real, merged `RowInvokerRegistry`.
- Verifying seed-observability behavior under TUnit's own retry/repeat
  mechanisms — ADR-0040 flags this as unverified; Phase 0's test suite
  investigates and records the actual behavior (it doesn't need profile
  support to check), but this plan does not block on a specific outcome
  (a documented limitation is an acceptable close, not just a passing
  test).

## Phases

Each phase ships as its own PR, per `design-decisions.md`'s phase rule.

### Phase 0: Package skeleton, unqualified `[Compose]`, its own tests and docs

**Status:** Done

- [x] New `src/Compono.TUnit/Compono.TUnit.csproj` — `net8.0;net9.0;net10.0;net11.0`
      (matching every other package's TFM window per ADR-0038; verify
      during implementation whether TUnit.Core's own `net10.0` asset
      restores cleanly for the `net11.0` leg via NuGet's asset-
      compatibility fallback, since TUnit itself doesn't ship a
      `net11.0`-specific target yet — record what's actually found, don't
      assume),
      `PackageReference` to `Compono` (`PrivateAssets="none"`, per
      PLAN-0004 Phase 3's real packaging-bug lesson — do not repeat it) and
      `TUnit.Core` only (not the full `TUnit`/`TUnit.Engine` meta-packages,
      per ADR-0040's minimal-dependency driver). Also carries the same
      `PinProjectReferenceVersionsExact` MSBuild target every existing
      integration project's `.csproj` has (`Compono.XunitV3.csproj`'s own
      copy is the template) — without it, `dotnet pack`'s own
      `ProjectReference`-to-`Compono` version resolves to a bare,
      minimum-inclusive range instead of the bracket/exact syntax ADR-0031
      requires.
- [x] `Directory.Packages.props`: add `TUnit.Core`'s `PackageVersion` entry
      (a tested range, matching ADR-0031 Amendment 1's convention — see
      `xunit.v3.extensibility.core`'s own entry for the exact shape) —
      centrally-managed package references restore-fail without it, so
      this isn't optional polish, it's required for the csproj above to
      restore at all. **Also add `Compono.TUnit`'s own entry** (matching
      `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`'s existing
      `Version="1.0.0"` pattern) — this phase's own local-feed consumer
      task (below) restores a real `PackageReference` to `Compono.TUnit`,
      which `ManagePackageVersionsCentrally=true` rejects with no central
      version for it, same as the `TUnit.Core`/`TUnit` case.
- [x] **New `test/Compono.TUnit.Tests/Compono.TUnit.Tests.csproj`** — this
      phase's own `[Test]`-attributed test suite needs somewhere to
      actually run. `PackageReference` to `Compono.TUnit`
      (`ProjectReference` in-repo) and the **full `TUnit` meta-package**
      (not `TUnit.Core` alone — this project executes as a real TUnit test
      run, unlike `src/Compono.TUnit` itself, which only authors against
      `TUnit.Core`'s extensibility surface). `Directory.Packages.props`
      also needs `TUnit`'s own `PackageVersion` entry alongside
      `TUnit.Core`'s, immediately above. Matches `test/Compono.XunitV3.Tests`'
      own csproj shape (`IsTestProject`, MTP runner properties) as the
      template, substituting TUnit's own project-SDK/runner conventions
      where they differ — confirm exact required properties
      (`TestingPlatformDotnetTestSupport`-equivalent, if any) against a
      real TUnit sample project during implementation rather than
      guessing them here. Also needs a `Compono.TUnit.Tests`-name
      exclusion added to `test/Directory.Build.props`'s two
      `IsTestProject`-scoped `ItemGroup`s (the shared xUnit v3 test-runner
      packages and global `using Xunit;`/etc. it force-adds to every test
      project don't belong in a TUnit-run project — mixing two
      `Microsoft.Testing.Platform` entry points in one project doesn't
      work) — a genuinely new edit this phase makes, not something already
      in place.
- [x] **Generator discovery** (`src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
      `src/Compono.Generators/ComponoIncrementalGenerator.cs`): three new
      metadata-name constants (`Compono.TUnit.ComposeAttribute`/`` `1``/
      `` `2``) and three new `ForAttributeWithMetadataName` registrations
      feeding the existing, already attribute-family-agnostic
      `ComposeMethodDiscovery.TransformMethod` — see ADR-0040's "Generator
      discovery" section for why this is required, not optional (without
      it, a parameter type reachable only through a `Compono.TUnit`-
      attributed method has no generated plan and `row.Resolve<T>()` fails
      at runtime). This touches `Compono.Generators`, embedded in
      `Compono.nupkg` — real core-package work, done here per the same
      package-design-ADR precedent ADR-0022 already set for
      `Compono.XunitV3`'s equivalent extension, not a separate
      core-extension ADR.
- [x] `Compono.Generators.Tests`: a snapshot test proving a concrete
      parameter type reachable *only* through a `Compono.TUnit`-attributed
      method (no other discovery path in the same compilation) gets a
      generated plan — mirroring whatever regression test closed the
      equivalent `Compono.XunitV3` gap (ADR-0022's Amendment, fix #2).
- [x] **Real packaged-consumer proof of the generator-discovery
      extension, in this phase — not deferred to Phase 2.** Since Phase 0
      is what actually changes the embedded `Compono.Generators` analyzer,
      a snapshot test alone doesn't prove the real NuGet dependency chain
      works: a minimal local-feed consumer project (mirroring PLAN-0004
      Phase 3 / PLAN-0005 Phase 2's `dotnet pack` → local-feed → real
      restore pattern) using the unqualified `[Compose]` attribute against
      a type with no other discovery path, proving the packed
      `Compono.TUnit`/`Compono` dependency chain actually generates a plan
      for it. Phase 2's own packaged-consumer run then exercises the
      *complete* attribute family once Phase 1 exists — this Phase 0
      instance is the minimum needed to prove Phase 0's own change works
      for real, in its own PR, not left unverified until a later phase.
- [x] `ComposeAttribute : UntypedDataSourceGeneratorAttribute` — the
      no-profile entry point. Overrides `GenerateDataSources(DataGeneratorMetadata)`,
      returns a single deferred `Func<object?[]?>` that (inside the Func,
      not before it) calls `composer.CreateRow(declaringType)` and binds
      each of the method's parameters via `row.Resolve<T>(descriptor)`/
      `row.ResolveShared<T>(descriptor)`, following `Compono.XunitV3`'s
      `BindingPlan`/`ParameterBindingPlan` pattern — `BindingPlan.cs`/
      `ParameterBindingPlan.cs`/`PositionalArgumentBinder.cs` are
      duplicated into this package per ADR-0040's binding-logic decision,
      unaffected by ADR-0041. `RowInvokers.cs` is **not** duplicated - see
      the two explicit tasks below.
- [x] **`RowInvokers.cs` built against core `Compono`'s `RowInvokerRegistry`
      from its first commit — per [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)
      (Amendment 2)/[PLAN-0041](0041-aot-safe-row-binding-dispatch.md).**
      Blocked on PLAN-0041 merging to `main` first (core + generator +
      `Compono.XunitV3` only - not itself blocked on this package existing).
      Once merged, this phase's own branch rebases onto it: `RowInvokers.cs`
      calls `RowInvokerRegistry.TryGet(parameterType, ...)`, never
      `MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` - no
      throwaway reflection-based version ships first. `BindingPlan.cs`'s
      own signature validation additionally rejects a `ref struct`/
      pointer-typed by-value parameter with a clear `CompositionException`
      (PLAN-0041's own dispatch-eligibility-guard task adds the identical
      check to `Compono.XunitV3.Binding.BindingPlan` - mirror it here, not
      a new investigation).
- [x] **Full end-to-end Native AOT publish-and-run proof, through the real
      packaged `Compono.TUnit` dependency chain** - `test/Compono.TUnit.SampleTests`
      (or a dedicated AOT-only sibling project), `dotnet publish -c Release
      -p:PublishAot=true` + run, exercising a real `[Compose]`-composed
      custom type and a provider-resolved leaf-type parameter (e.g.
      `string`). PLAN-0041's own AOT proof only covers the shared
      mechanism in isolation (core + `Compono.XunitV3`, since
      `Compono.TUnit` doesn't exist on `main` when that plan is
      implemented) - this task is what actually proves the full
      `Compono.TUnit` package chain survives trimming/AOT, the deliverable
      ADR-0041's Native-AOT-as-a-release-requirement decision exists for
      in the first place. Update `docs/packages/compono-tunit.md`'s Native
      AOT claim to point at this proof once it exists, not before.
- [x] **`ComposeAttribute`'s constructor — `public ComposeAttribute(params
      object?[] inlineValues)`, with full inline-value binding, ships in
      this phase, not Phase 1.** Confirmed against `Compono.XunitV3`'s
      real source: inline values live on the *base* `ComposeAttribute`
      constructor (`Compono.XunitV3.ComposeAttribute<TProfile>`'s own
      constructor is `public ComposeAttribute(params object?[]
      inlineValues) : base(inlineValues)` — a pass-through, not a
      Phase-1-only concept). Shipping `Compono.TUnit.ComposeAttribute`
      with only an implicit parameterless constructor in this phase and
      adding the `params` constructor in Phase 1 would remove that
      implicit constructor (C# stops auto-generating it the moment any
      explicit constructor exists) — a binary-compatibility break for
      anything compiled against Phase 0's shipped assembly. The
      constructor shape must be final from this phase's first release.
      Positional, leading-parameters-only precedence over composition,
      matching `Compono.XunitV3`'s existing algorithm exactly (`Compono.XunitV3
      .ComposeAttribute`'s `NormalizeParamsArguments`/inline-value-validation
      logic is the template).
- [x] `ComposeAttribute.Seed` (`int`, non-negative) — public property
      mirroring `Compono.XunitV3.ComposeAttribute.Seed` exactly, routed
      into `BuildComposer`'s `CompositionBuilder.WithSeed(...)` call. The
      row's effective seed (`row.Seed < 0`) is checked before any
      parameter composes, matching `Compono.XunitV3`'s own pre-composition
      check — required by ADR-0040's "Seed input and replay" section, not
      optional: without this property a reported seed can never actually
      be pasted back as `[Compose(Seed = ...)]`.
- [x] `SharedAttribute` — package-local marker, mirroring
      `Compono.XunitV3.SharedAttribute`'s shape and duplicate-shared-type
      validation.
- [x] Seed observability: `ComposeAttribute` also implements
      `ITestDiscoveryEventReceiver`. Inside the deferred `Func`, after
      `CreateRow` produces the row, store `row.Seed` into
      `dataGeneratorMetadata.TestBuilderContext.StateBag` under a
      package-namespaced key. In `OnTestDiscovered(DiscoveredTestContext)`,
      read it back and call `discoveredContext.AddProperty("Compono.Seed",
      seed.ToString())`. **Do not** store the seed as an attribute-instance
      field — ADR-0040's own `IClassConstructor` finding (a reused
      attribute/receiver instance across rows) is the standing reason.
- [x] Diagnostics: every `Resolve`/`ResolveShared`/`ShareExplicit` call
      wrapped so a thrown `CompositionException` is rethrown via
      `CompositionException.WithSeedInMessage(exception, seed)` — matching
      `Compono.XunitV3`'s real `InvokeWithSeedOnFailure`, **not** left
      un-wrapped (ADR-0040's Diagnostics section originally mis-described
      `Compono.XunitV3`'s own behavior here and was corrected — see that
      ADR). A pipeline failure without this wrapping would violate this
      same plan's own unconditional seed-observability guarantee exactly
      when a row fails composition.
- [x] Explicitly confirm during implementation: no `IDisposable`/
      `IAsyncDisposable`/`ITestEndEventReceiver` implementation anywhere in
      this package — ADR-0040's disposal conclusion is a hard constraint
      for this phase, not just a design note to remember.
- [x] Document both disposal constraints from ADR-0040's "Diagnostics,
      disposal, and seed observability" section: (1) do not compose a
      cross-test-shared disposable instance (from `UseServiceProvider(...)`/
      an exact `Register<T>(...)` factory returning a shared instance) as a
      `[Compose]`/`[Shared]` parameter, since TUnit's reference-counted
      disposal has no provenance awareness and will dispose it after the
      first test that uses it; (2) a nested, non-root `IDisposable`
      dependency a generated plan composes internally is never disposed by
      TUnit or Compono at all — promote it to `[Shared]` or dispose it
      explicitly in the test body if that matters. Lands in the new
      Package Guide (below) and as `Compono.TUnit`-specific skill
      guardrails — real constraints, not footnotes to mention once and
      forget.
- [x] `test/Compono.TUnit.Tests`: binding-plan unit coverage for the
      no-profile shape (parameter resolution, signature-validation errors —
      generic method, ref/out/in, params — `[Shared]` duplicate-type
      validation), **plus inline-value precedence coverage** (now a Phase
      0 concern, per the constructor-shape fix above) — mirroring
      `Compono.XunitV3.Tests`' `InlineNullHandlingTests`/equivalent
      binding-logic coverage.
- [x] Seed-observability verification, real TUnit run: `AddProperty
      ("Compono.Seed", ...)` actually visible on both a passing and a
      failing `[Compose]` row (via TUnit's own reporting/TRX output, not
      just asserting the internal call happened) — the concrete check for
      the parity guarantee ADR-0040 requires, not an assumption. Also
      investigate whether this holds under `[Retry]`; record the actual
      finding either way (ADR-0040's flagged open item — doesn't need
      profile support to check, so it belongs here, not Phase 2).
- [x] Disposal verification, real TUnit run, two cases per ADR-0040's
      corrected (root-only) disposal claim: (1) a simple `IDisposable`
      domain/test type (a small purpose-built type recording whether
      `Dispose()` was called — not a `[Shared]` substitute or any other
      mocking-library-produced object) composed as a top-level `[Compose]`
      parameter, confirming TUnit disposes it without any `Compono.TUnit`-
      side cleanup code; (2) the same type composed as a *nested*,
      non-`[Shared]` constructor dependency of another composed value,
      confirming — and recording as documented, expected behavior, not a
      bug — that it is **not** disposed by anyone. Testing only case (1)
      would silently overclaim coverage case (2) never had.
- [x] End-to-end `[Shared]` composition test against a real TUnit run,
      using the no-profile `[Compose]` shape (a plain composed domain
      object reused via `[Shared]`, not the NSubstitute scenario — that
      needs `[Compose<TProfile>]`, Phase 1's own scope; see Phase 1's own
      test item for the full Goal-section scenario).
- [x] **Build/CI infrastructure wiring** — creating the project alone
      leaves it outside every place this repo's build/release/validation
      pipeline enumerates packages by name; each of the following
      hardcodes the current four-package list and needs `Compono.TUnit`
      added alongside them:
  - [x] `Compono.slnx`: add `src/Compono.TUnit/Compono.TUnit.csproj` and
        `test/Compono.TUnit.Tests/Compono.TUnit.Tests.csproj`.
  - [x] `.github/workflows/docs.yml`: add `src/Compono.TUnit/**` to both
        `paths:` trigger lists, and `Compono.TUnit` to the `for pkg in ...`
        build loop feeding the API-reference generator.
  - [x] `.github/workflows/package-validation.yaml`: add `Compono.TUnit`
        to its `for pkg in ...` loop and the two explicit `pack_one`/path
        lists.
  - [x] `.github/scripts/inspect-packed-nupkgs.sh`: add `Compono.TUnit` to
        its `for pkg in ...` loop and its own `case` branch (this
        package's own expected dependency set: `Compono` + `TUnit.Core`,
        no `Compono.Generators.dll` embedding since that's `Compono`-only
        per ADR-0003).
  - [x] `.github/scripts/generate-api-reference.sh`: add `Compono.TUnit`
        to its `integration_pkgs` array so its public API gets generated
        reference docs and cross-link resolution like the other three
        integration packages.
- [x] New `docs/packages/compono-tunit.md` Package Guide — covers
      `[Compose]`/`[Shared]` (what Phase 0 actually ships); Phase 1 extends
      it with the profile-attribute sections once they exist.
- [x] `docs/packages/index.md`: add `Compono.TUnit`'s row.
- [x] **Existing topic docs that become stale the moment `[Compose]`/
      `[Shared]` ships under a second framework** — found by rereading the
      actual current content, not assumed:
  - [x] `docs/public-api.md` (tombstone) — its "Package Guides" bullet
        lists only `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`;
        add `Compono.TUnit`.
  - [x] `docs/concepts/shared-values.md` — its "Scope and limits" section
        states "`[Shared]` only applies within `Compono.XunitV3`'s
        `[Compose]` row" as if that's the only such row; reword to name
        both packages (or speak generically about "a `[Compose]`-family
        row," now that a second one exists).
  - [x] `docs/getting-started/installation.md`/relevant how-to pages: add
        a `Compono.TUnit` install example alongside the existing
        `Compono.XunitV3` one, so the installation path isn't implicitly
        xUnit-v3-only.
  - [x] **Front-door package inventories** — `README.md` and
        `docs/index.md` each carry their own "## Packages" table listing
        exactly the four shipped packages (confirmed by direct read, both
        currently identical four-row tables). Add `Compono.TUnit`'s row to
        both — otherwise a reader never gets past either front door
        without being told the package doesn't exist, even once the
        Package Guide and package index (above) both advertise it.
- [x] `skills/compono/references/tunit.md`: new package-conditional
      reference file, covering `[Compose]`/`[Shared]` — matching
      `xunit-v3.md`'s shape; Phase 1 extends it.
- [x] `skills/compono/SKILL.md`: new Detection-table row
      (`<PackageReference Include="Compono.TUnit"` → load
      `references/tunit.md`); remove `Compono.TUnit` from the "don't
      invent an unshipped package" guardrail's named-absent list (it's no
      longer absent); update the frontmatter `description`'s enumerated
      package list.

### Phase 1: Profile variants, their own tests and docs

**Status:** Done

- [x] `ComposeAttribute<TProfile> : ComposeAttribute` — `new()`-constrained
      profile type parameter, mirroring `Compono.XunitV3`'s
      `ComposeAttribute<TProfile>` exactly (method-level only, matching
      that package's own original scope decision).
- [x] `ComposeAttribute<TProfile, TConfig> : ComposeAttribute` — profile
      built from attribute-constructor-supplied config args, mirroring
      `Compono.XunitV3`'s `ComposeAttribute<TProfile, TConfig>`
      (ADR-0036) exactly, including its once-per-attribute-instance
      reflection bound and its seed/config value semantics. **Correction
      to the previous round's fix**: unlike `ComposeAttribute<TProfile>`,
      this form's own constructor is **not** an inline-values pass-through
      — verified against the real source
      (`src/Compono.XunitV3/ComposeAttribute{TProfile,TConfig}.cs:62-64`):
      `public ComposeAttribute(params object?[] configArguments) :
      base()` calls the base constructor with **zero** inline values and
      stores `configArguments` in its own separate `_configArguments`
      field instead — a distinct binding target (`TConfig`'s own
      constructor, via `ConfigProfileBinder`) from ordinary inline values,
      which this form doesn't accept at all (every test method parameter
      is always fully composed under `[Compose<TProfile, TConfig>]`, per
      `Compono.XunitV3`'s own doc comment). `ComposeAttribute<TProfile>`
      (above) is the only generic form that inherits Phase 0's
      inline-value constructor unchanged; this one has its own,
      independent constructor and storage, duplicated from
      `Compono.XunitV3`'s exact shape, not shared with the other generic
      form.
- [x] Stacked Compose-family attribute validation: reject a test method
      carrying more than one of `[Compose]`/`[Compose<TProfile>]`/
      `[Compose<TProfile, TConfig>]` — `AllowMultiple = false` is enforced
      per exact attribute type by the compiler, not across the family, so
      nothing else stops two *different* Compose-family types stacking on
      one method (`Compono.XunitV3`'s own `BindingPlan.ValidateSignature`
      has this exact check, `composeAttributeCount > 1`). This can't reuse
      Phase 0's own `BindingPlan.Build(MethodMetadata)` input as-is — TUnit
      hands a data source `DataGeneratorMetadata`/`MethodMetadata`, which
      (per this ADR's own investigation) has no ready-made method-level
      attribute list — so this needs one reflection call to the method's
      own attributes via `ParameterMetadata.ReflectionInfo.Member` (a
      `MethodInfo`, available whenever the method has at least one
      parameter) or an equivalent lookup for a zero-parameter method;
      exact mechanism is `implement.md`'s call, this task is the
      requirement. Without it, TUnit runs both attributes' data sources
      independently and produces duplicate/conflicting rows despite
      ADR-0040 promising full `Compono.XunitV3` parity.
- [x] `test/Compono.TUnit.Tests`: profile-binding unit/integration
      coverage (`ComposeAttribute<TProfile>`, `ComposeAttribute<TProfile,
      TConfig>` config binding) plus inline-values-combined-with-a-profile
      coverage (Phase 0 already covers inline values alone; this phase
      only adds the combined case, once a profile exists to combine with),
      mirroring `Compono.XunitV3.Tests`' `ComposeAttributeConfigBindingTests`
      shape. Includes stacked-attribute-rejection
      test cases for the generic and config-generic forms specifically
      (`[Compose]` + `[Compose<TProfile>]`, `[Compose<TProfile>]` +
      `[Compose<TProfile, TConfig>]`, etc.) — the case Phase 0 alone can't
      exercise, since it needs a second Compose-family type to exist.
- [x] The full Goal-section scenario, run for real under TUnit: `[Shared]
      IOrderRepository` composed via `[Compose<NSubstituteTestProfile>]`,
      `UseNSubstitute()` wired through the profile, `repository` reused
      inside `handler`'s own composed constructor parameter — the
      `Compono.XunitV3.SampleTests.NSubstituteTests.Saves_order` scenario,
      reproduced under TUnit for real (this needs `Compono.NSubstitute` as
      an additional test dependency, matching how the xUnit v3 sample
      project references it).
- [x] Extend `docs/packages/compono-tunit.md` with the profile-attribute
      sections (`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`,
      inline values).
- [x] Extend `skills/compono/references/tunit.md` with profile-attribute
      guidance, matching `xunit-v3.md`'s equivalent sections.
- [x] **Native AOT gate on `ConfigProfileBinder` — a release requirement,
      not optional polish (ADR-0041 Amendment 1).** `[Compose<TProfile,
      TConfig>]`'s own `ConfigProfileBinder` needs the identical AOT
      analysis ADR-0041 already performed for row-binding dispatch:
      confirm whether its `ConstructorInfo.Invoke(object?[])`-based
      `TConfig` construction is Native AOT-safe (unlike
      `MakeGenericMethod`, `ConstructorInfo.Invoke` on an
      already-known/non-generic `Type` is a materially different, likely
      lower-risk case — but "likely" isn't good enough here; verify for
      real, the same way ADR-0041 refused to assume the `[Shared]`-
      detection reflection was safe without a real check). If it is not
      AOT-safe, design and implement the smallest AOT-safe replacement
      (per ADR-0041's own "smallest maintainable design" driver) *before*
      `[Compose<TProfile, TConfig>]` ships in this phase — this attribute
      does not merge until its own construction path clears the same bar
      row-binding dispatch already had to. Extend **this phase's own
      dedicated `Compono.TUnit` AOT project** (Phase 0's "Full end-to-end
      Native AOT publish-and-run proof" task, above) to also exercise
      `[Compose<TProfile, TConfig>]`, not just unqualified `[Compose]` -
      **not** PLAN-0041's own smoke test, which is explicitly scoped to
      the shared mechanism through `Compono.XunitV3` only (that plan must
      complete and merge before this phase even starts, and never runs
      anything through the real `Compono.TUnit` package chain at all - see
      PLAN-0041's own Scope). Extending the wrong harness would let this
      release gate get checked off without ever actually running
      `[Compose<TProfile, TConfig>]` through `Compono.TUnit` under Native
      AOT — the final `Compono.TUnit` Native AOT claim must cover every
      public Compose-family attribute shipped, not just the one Phase 0
      introduced, and only this phase's own TUnit-real harness can prove
      that.

### Phase 2: Verification requiring the completed attribute family

**Status:** Done

- [x] A real packaged-consumer sample project run (mirroring PLAN-0004
      Phase 3 / PLAN-0005 Phase 2's precedent exactly) — extends Phase 0's
      own minimal local-feed consumer to exercise the *complete* attribute
      family (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/
      `[Shared]`) through the actual packaged `Compono.TUnit` → `Compono`
      dependency chain, not `Compono.TUnit.Tests`' own `ProjectReference`-
      based calls. `test/Compono.TUnit.SampleTests/ConfigProfileTests.cs`
      (new) adds the missing `[Compose<TProfile, TConfig>]` leg, mirroring
      `Compono.XunitV3.SampleTests/ConfigProfileTests.cs` exactly — the
      other three forms were already covered by Phase 0/1's own sample
      files (`CompositionTests.cs`, `SharedTests.cs`, `NSubstituteTests.cs`).
- [x] Final API-surface/approval test locking `Compono.TUnit`'s complete
      public shape (`ComposeAttribute` family, `SharedAttribute`, and
      nothing else), matching `Compono.XunitV3.Tests`'/
      `Compono.NSubstitute.Tests`' existing pattern — new
      `test/Compono.TUnit.Tests/PublicApiSurfaceTests.cs`, confirming the
      exact four-type public surface (`ComposeAttribute`,
      `` ComposeAttribute`1``, `` ComposeAttribute`2``, `SharedAttribute`)
      Phase 0/1 already shipped, with no drift.

### Phase 3: Docs and skill consistency close-out

**Status:** Done

- [x] Re-read `docs/packages/compono-tunit.md`, `docs/packages/index.md`,
      `skills/compono/references/tunit.md`, and `SKILL.md`'s Detection
      table/guardrail/description end to end — confirm nothing Phase 0/1
      added is inconsistent or stale now that the full package exists (a
      pure consistency pass; Phase 0/1 already did the substantive writing
      per-behavior). Found and fixed four stale spots, all pre-dating
      Phase 1/2 shipping: `compono-tunit.md`'s and `tunit.md`'s own
      "PLAN-0040 Phase 0/1 have shipped" intro lines (now describe the
      full family, not a phase number); `docs/packages/index.md`'s
      `Compono.TUnit` row (still said "`[Compose]` data source attribute
      ... " only, no profile variants, unlike the `Compono.XunitV3` row
      immediately above it); `SKILL.md`'s Detection table row, its "Never
      claim or write code against..." guardrail, and its
      `references/tunit.md` file-index row (all three still said
      `Compono.TUnit` ships only `[Compose]`/`[Shared]`, "not
      `[Compose<TProfile>]` yet" — stale since Phase 1 merged).
- [x] `docs/roadmap/future-packages.md`: moved `Compono.TUnit` out of
      "Roadmap items" — it's shipped, not a roadmap item anymore. Reworded
      the intro (five shipped packages, not four-plus-one-committed), the
      Admission model section's `Compono.TUnit` paragraph (now describes
      the full admitted-candidate → roadmap-item → committed →
      **shipped-package** progression, past tense), and emptied the
      "Roadmap items" section itself with a pointer to
      [Package Guides](../packages/index.md).
- [x] `skills/compono-evals/evals.json`: eval 20 (`Does Compono support
      NUnit?`) was checked directly — it's about `Compono.NUnit`, not
      `Compono.TUnit`, and doesn't use `Compono.TUnit` as an example of a
      nonexistent package (that concern must have been addressed earlier
      than this phase; nothing to retire/rewrite there). Added eval 21 (a
      new `routing` scenario): a project referencing `Compono`,
      `Compono.TUnit`, and `Compono.NSubstitute` but *not*
      `Compono.XunitV3`, expecting TUnit's own `[Test]`/`[Compose<TProfile>]`
      shape (not xUnit v3's `[Theory]`) with `UseNSubstitute()`/`[Shared]`
      — mirrors eval 3's (`Compono.NSubstitute` routing) and eval 18's
      (negative-routing: a package NOT referenced) shape.

## Critical Files

- `src/Compono.TUnit/Compono.TUnit.csproj` — new
- `src/Compono.TUnit/ComposeAttribute.cs`,
  `ComposeAttribute{TProfile}.cs`, `ComposeAttribute{TProfile,TConfig}.cs`,
  `SharedAttribute.cs` — new
- `src/Compono.TUnit/Binding/*` — new. `BindingPlan.cs`/`ParameterBindingPlan.cs`/
  `PositionalArgumentBinder.cs` are a duplicated pattern from `Compono.XunitV3`'s own
  (ADR-0040's binding-logic decision, unaffected by ADR-0041). `RowInvokers.cs` is **not**
  duplicated — built against core `Compono`'s shared `RowInvokerRegistry` from the start, per
  [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)/[PLAN-0041](0041-aot-safe-row-binding-dispatch.md).
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `src/Compono.Generators/ComponoIncrementalGenerator.cs` — modified
  (three new metadata-name constants/registrations for `Compono.TUnit`'s
  attribute family; see ADR-0040's "Generator discovery" section)
- `test/Compono.TUnit.Tests/*` — new
- `test/Compono.Generators.Tests/*` — modified (new snapshot test for
  `Compono.TUnit`-only-reachable discovery)
- A new or extended sample project proving real-package consumption
- `Compono.slnx` — modified (new project entries)
- `test/Directory.Build.props` — modified (`Compono.TUnit.Tests`-name exclusion from the xUnit-v3-specific `ItemGroup`s)
- `Directory.Packages.props` — modified (`TUnit.Core`/`TUnit`/`Compono.TUnit` `PackageVersion` entries)
- `.github/workflows/docs.yml`, `.github/workflows/package-validation.yaml`,
  `.github/scripts/inspect-packed-nupkgs.sh`,
  `.github/scripts/generate-api-reference.sh` — modified (five-package
  enumeration instead of four)
- `docs/packages/compono-tunit.md` — new
- `docs/packages/index.md`, `docs/roadmap/future-packages.md`,
  `docs/public-api.md`, `docs/concepts/shared-values.md`,
  `docs/getting-started/installation.md`, `README.md`, `docs/index.md`
  — updated
- `skills/compono/SKILL.md`, `skills/compono/references/tunit.md`,
  `skills/compono-evals/evals.json` — new/updated

## Test Plan

Per `testing.md`'s established pattern: unit coverage for the binding
plan and seed-storage logic in isolation, end-to-end composition tests
against a real `Composer` under a real TUnit run (not simulated), an
API-surface/approval test locking the public shape, and — the two items
ADR-0040 explicitly calls out as needing empirical confirmation, not just
design-time reasoning — a real seed-observability check (TRX/reporting
output) and a real disposal check (an `IDisposable` composed value,
confirmed disposed with zero `Compono.TUnit`-side cleanup code). Tests for
a given behavior ship in the same phase/PR that introduces that behavior,
per this plan's own phase-per-PR structure — see each phase above.

## Notes

**PR #72 Codex review (2026-08-11)**: 5 findings, all confirmed real:
- 🐛-equivalent (P1): the Goal section's `[Compose]` (unqualified) with a
  disconnected local `Composer.Create()` variable cannot actually resolve
  `IOrderRepository` — an unqualified `[Compose]` builds its own default
  composer with no registered providers. Fixed: Goal now uses
  `[Compose<TProfile>]` with a profile calling `UseNSubstitute()`, matching
  the real, working `Compono.XunitV3.SampleTests.NSubstituteTests
  .Saves_order` pattern exactly.
- 🐛-equivalent (P1): ADR-0040's Diagnostics section incorrectly described
  `Compono.XunitV3`'s real behavior as "un-wrapped" pipeline-failure
  propagation. `Compono.XunitV3`'s actual `ComposeAttribute` wraps every
  `Resolve`/`ResolveShared`/`ShareExplicit` failure via
  `CompositionException.WithSeedInMessage` specifically to preserve the
  seed guarantee on a failing row. Fixed in both ADR-0040 and this plan's
  Phase 0.
- ⚠️-equivalent (P1): all tests were bundled into Phase 2, leaving Phase
  0/1's own introduced behavior unverified in their own PRs. Fixed:
  redistributed tests into the phase that introduces the corresponding
  behavior; Phase 2 now holds only verification that genuinely needs the
  completed attribute family.
- ⚠️-equivalent (P1): all docs/skill updates were bundled into Phase 3,
  leaving Phase 0/1's own shipped public surface undocumented until a
  later PR. Fixed: redistributed doc/skill updates into the phase that
  introduces the corresponding behavior; Phase 3 is now a closing
  consistency pass, not the first appearance of this content.
- ⚠️-equivalent (P2): `docs/roadmap/future-packages.md` left `Compono.TUnit`
  under "Admitted candidates (no evidence yet)" while separately stating
  it had cleared Gate B via ADR-0040 — internally contradictory. Fixed:
  moved to a new "Roadmap items" section; corrected the page's intro and
  admission-model prose accordingly.

**PR #72 Codex review, third round (2026-08-11)**: 2 findings, both
confirmed real:
- ⚠️-equivalent (P2): Phase 1 never listed a task for rejecting stacked
  Compose-family attributes (`[Compose]` + `[Compose<TProfile>]` on the
  same method), a validation `Compono.XunitV3` has and ADR-0040 promises
  parity with. Added the task and its generic/config-generic test
  coverage — flagged that it needs a reflection-based method-attribute
  lookup `DataGeneratorMetadata`/`MethodMetadata` doesn't hand over
  directly, deferring the exact mechanism to `implement.md`.
- ⚠️-equivalent (P2): `future-packages.md`'s "has made that full
  progression so far" claim still contradicted the page's own definition
  of committed implementation work (requires `Plan: In Progress`) even
  after the prior round's fix. Reworded to say `Compono.TUnit` reached
  roadmap-item status with an `Accepted` design ADR, not committed
  implementation work.

**PR #72 Codex review, fourth round (2026-08-11)**: 3 findings, all
confirmed real:
- 🐛-equivalent (P1): no generator-discovery task anywhere in this plan.
  Verified directly against `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`
  and `ComponoIncrementalGenerator.cs`: `ComposeMethodDiscovery` hardcodes
  exactly three `Compono.XunitV3` attribute metadata names — a parameter
  type reachable only through a `Compono.TUnit`-attributed method would
  have no generated plan, failing `row.Resolve<T>()` at runtime with "no
  plan found," exactly the gap ADR-0022's own Amendment fixed for
  `Compono.XunitV3`. Added a "Generator discovery" section to ADR-0040
  (correcting its Context section's "no core change needed" overstatement
  in the process) and the corresponding Phase 0 tasks (three new metadata
  registrations, a `Compono.Generators.Tests` snapshot test).
- ⚠️-equivalent (P2): the plan never wired the new package into
  `Compono.slnx` or the build/release/validation scripts that enumerate
  the four current packages by name (`docs.yml`,
  `package-validation.yaml`, `inspect-packed-nupkgs.sh`,
  `generate-api-reference.sh`, all confirmed by direct grep). Added an
  explicit Phase 0 task group for each.
- ⚠️-equivalent (P2): Phase 0 only touched the new Package Guide and
  index, leaving existing topic docs stale (confirmed by reading them
  directly: `docs/public-api.md`'s Package Guides list, and
  `docs/concepts/shared-values.md`'s "[Shared] only applies within
  Compono.XunitV3" line). Added Phase 0 tasks for both, plus an
  installation-guide addition.

**PR #72 Codex review, fifth round (2026-08-11)**: 3 findings, all
confirmed real:
- 🐛-equivalent (P1): ADR-0040's disposal claim ("TUnit owns 100% of it")
  overclaimed coverage. Re-read `ObjectGraphDiscoverer`'s own traversal
  logic directly: its nested-object walk is scoped to TUnit's own
  `IAsyncInitializer` property registry, not a general graph walk — an
  ordinary nested composed dependency (never itself a root `[Compose]`/
  `[Shared]` parameter) is never reached by TUnit's disposal at all.
  Corrected the ADR to scope the claim precisely to root/top-level
  returned arguments, added the nested-dependency gap as an accepted
  limitation (the same one `Compono.XunitV3` already has for every
  composed value, just narrower here), and split the Phase 0
  disposal-verification task into two cases so it doesn't silently
  overclaim coverage the second case never had.
- ⚠️-equivalent (P1): the only real packaged-consumer proof of Phase 0's
  own generator-discovery change was deferred to Phase 2, meaning Phase 0
  could ship a broken embedded-analyzer change unverified against a real
  NuGet dependency chain in its own PR. Added a minimal Phase 0 local-feed
  consumer task (unqualified `[Compose]` only); Phase 2 now extends it to
  the complete attribute family instead of being the first such run.
- ⚠️-equivalent (P2): the csproj task never mentioned adding `TUnit.Core`'s
  `PackageVersion` to `Directory.Packages.props` or the
  `PinProjectReferenceVersionsExact` MSBuild target every other
  integration project's csproj has — without both, the new project can't
  restore or pack correctly. Added both to the Phase 0 task list.

**PR #72 Codex review, sixth round (2026-08-11)**: 3 findings, all
confirmed real:
- 🐛-equivalent (P1): `ComposeAttribute`'s constructor shape would have
  changed between Phase 0 and Phase 1 — Phase 0 shipping only an implicit
  parameterless constructor, Phase 1 adding `params object?[] inlineValues`,
  which removes that implicit constructor (C# stops generating it once any
  explicit constructor exists) — a binary-compatibility break for anything
  compiled against Phase 0's shipped assembly. Verified against real
  `Compono.XunitV3` source that inline values are a *base*-class
  constructor concern, not a generic-subclass one
  (`ComposeAttribute<TProfile>`'s own constructor is `params object?[]
  inlineValues) : base(inlineValues)`, a pure pass-through) — moved the
  constructor and its binding logic entirely into Phase 0, where it
  actually belongs; Phase 1's generic forms now just inherit it.
- 🐛-equivalent (P1): Phase 0 added `TUnit.Core`'s package version but
  never a task to create `test/Compono.TUnit.Tests` itself or reference
  the full `TUnit` meta-package it needs to actually execute as a TUnit
  test run. Added the project-creation task, and caught my own claim that
  the `test/Directory.Build.props` exclusion was "already added" — it
  isn't, it only exists in this session's stashed, uncommitted work, not
  on this branch or `main` — corrected to describe it as a task this
  phase does, not a precondition already met.
- ⚠️-equivalent (P2): the plan's own header summary and Goal section still
  said disposal was "owned entirely by TUnit" and a run "confirms nothing
  leaks," both stale after the fifth round's root-vs-nested correction.
  Reworded both to match the corrected, scoped claim.

**PR #72 Codex review, seventh round (2026-08-11)**: 3 findings, all
confirmed real — one of them a mistake in the sixth round's own fix:
- 🐛-equivalent (P1): the sixth round's constructor-consolidation fix
  incorrectly claimed both generic forms (`ComposeAttribute<TProfile>`
  and `ComposeAttribute<TProfile, TConfig>`) inherit Phase 0's inline-value
  constructor unchanged. Verified against the real source
  (`ComposeAttribute{TProfile,TConfig}.cs:62-64`): the two-type-parameter
  form's own constructor is `params object?[] configArguments) : base()`
  — zero inline values passed to the base, `configArguments` stored in
  its own separate field entirely, bound to `TConfig`'s constructor via
  `ConfigProfileBinder`, not to test parameters at all. Corrected Phase
  1's task to describe this form's own independent constructor/storage/
  binding path instead of incorrectly grouping it with
  `ComposeAttribute<TProfile>`'s.
- ⚠️-equivalent (P2): Phase 0's local-feed consumer task restores a real
  `PackageReference` to `Compono.TUnit`, but the `Directory.Packages.props`
  task only added `TUnit.Core`'s central version, not `Compono.TUnit`'s
  own (needed the same way `Compono.XunitV3`/`Compono.NSubstitute`/
  `Compono.Bogus` already have one for their own sample-consumer
  projects). Added it.
- ⚠️-equivalent (P2): `README.md` and `docs/index.md` both carry their own
  four-package inventory table (confirmed identical, by direct read),
  neither included in the doc-update list even though Phase 0 ships
  `Compono.TUnit` as its own PR. Added both to Phase 0's stale-doc task
  group.

**Phase 0 implementation (2026-08-11)**: completed against the review-hardened
ADR-0040/PLAN-0040 text (post PR #72 merge). Key real findings/decisions made
during implementation, not just planned in advance:
- `TrackedWidget`-shaped composed custom types don't resolve inside
  `test/Compono.TUnit.Tests` at all — a plain `ProjectReference` doesn't
  propagate `Compono.Generators` as an analyzer (only a packed nupkg's
  `analyzers/dotnet/cs` delivery does), matching the same constraint
  `testing.md` already documents for `Compono.Tests`/`Compono.XunitV3.Tests`.
  `Compono.TUnit.Tests`' own binding/seed-observability unit tests use
  built-in-composable parameter types (`string`, `int`) instead, and the two
  real-TUnit-run verifications that need a genuine composed custom type
  (disposal, `[Shared]`) live in the new `test/Compono.TUnit.SampleTests`
  packaged-consumer project instead, alongside the generator-discovery
  packaged-consumer proof this phase's own task list already required.
- `BindingPlan.Build`/`ComposeAttribute.GetDataRowsAsync` unit tests hand-build
  a real `MethodMetadata`/`DataGeneratorMetadata` via `MethodMetadataFactory`/
  `ClassMetadata`/`AssemblyMetadata`'s own public factories (reflection-based,
  the same shape TUnit.Core's own internal `ClassMetadataHelper` uses) rather
  than a bare `MethodInfo` — TUnit's data-source pipeline hands attributes
  `DataGeneratorMetadata`, not `MethodInfo`, so there's no `MethodInfo`-based
  unit-test entry point the way `Compono.XunitV3.ComposeAttribute.GetData`
  has.
- The negative-seed case (seed-observability's "fail case", and
  `[Retry]` investigation) can't live as a permanent green `[Test]` — TUnit
  reports the row's own `CompositionException` as that test failing, not
  something the test body can assert against (composition happens before the
  body runs). Verified once by a real run (`Seed: -1` present in the failure
  message, confirming the negative-seed check precedes any state report and
  that `[Retry]` would just re-run the same deterministic failure), then
  recorded as a comment in `SeedObservabilityTests.cs` instead of a
  permanently-failing test — the same "prove once via a real run, then keep
  only a passing regression" pattern this phase's runner-wiring smoke test
  used.
- Full regression confirmed clean throughout: `Compono.Tests` 213/213,
  `Compono.Generators.Tests` 86/86, `Compono.XunitV3.Tests` 67/67,
  `Compono.NSubstitute.Tests` 23/23, `Compono.Bogus.Tests` 63/63,
  `Compono.TUnit.Tests` 15/15, `Compono.TUnit.SampleTests` 4/4 (including the
  real root-disposed/nested-not-disposed and `[Shared]` end-to-end proofs).

**PLAN-0041 merge + remaining two tasks (2026-08-12)**: `main` (carrying
PLAN-0041's `RowInvokerRegistry`) merged into this phase's branch. Three
merge conflicts, all mechanical (`ComposeMethodDiscovery.TransformMethod`'s
new `ComposeMethodDiscoveryResult` return type; both plan-status tables).
One real, previously-latent gap surfaced by the merge itself: the existing
`TUnitComposeAttributedMethodParameter_GeneratesCompositionPlan` snapshot
test had no verified file for the `RowInvokerRegistration.g.cs` output the
generator now also emits for `Compono.TUnit`-discovered parameters — added.
Then the two tasks this phase's own text had left blocked on that merge:
- `Compono.TUnit.Binding.RowInvokers.cs` rewritten against
  `RowInvokerRegistry.TryGet`, byte-for-byte mirroring
  `Compono.XunitV3.Binding.RowInvokers`'s already-merged rewrite - no
  `MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` left anywhere in
  `Compono.TUnit`. `BindingPlan.cs` gained the matching ref-struct/pointer
  by-value-parameter rejection (`Span<int>` sample method + regression
  test, mirroring `Compono.XunitV3.Tests`'s own).
- **Native AOT publish-and-run proof, new `test/Compono.TUnit.AotSmokeTest`
  project** (`Compono.AotSmokeTest`'s sibling, not `Compono.TUnit.SampleTests`
  itself - a real TUnit test host has its own generator/engine wiring that a
  one-shot publish-and-run proof doesn't need). Stronger proof than
  `Compono.AotSmokeTest`'s own: rather than standing in for the attribute and
  dispatching through `RowInvokerRegistry` manually (proving only the shared
  registry mechanism), this harness drives the real, packaged
  `Compono.TUnit.ComposeAttribute.GetDataRowsAsync` directly - proving
  `BindingPlan.Build` and `Compono.TUnit.Binding.RowInvokers.Build`
  themselves, not just what they call into, survive AOT. Needed
  `ComponoTestFramework=TUnit` (not `Compono.AotSmokeTest`'s
  `<Using Remove>` trick) to opt out of `test/Directory.Build.targets`' xUnit
  v3 package sweep, and `<NoWarn>TUnit0034</NoWarn>` - TUnit.Core's analyzer
  otherwise flags this project's own `Main` method, assuming any project
  referencing `TUnit.Core` is a real test host. `dotnet publish -c Release
  -p:PublishAot=true -r osx-arm64 --self-contained true` + run: PASS,
  composing both a custom type (`Widget`) and a provider-resolved leaf type
  (`string`) through the real dispatch path. A `-p:TrimmerSingleWarn=false`
  pass surfaced exactly two individual `IL2072` trim warnings, both inside
  this harness's own reflection-based `MethodMetadata`/`DataGeneratorMetadata`
  construction code (working around `TUnit.Core`'s own missing
  `DynamicallyAccessedMembers` annotations on `ParameterMetadata`'s
  constructor/`ClassMetadata.Type`'s setter) - zero from `Compono`'s or
  `Compono.TUnit`'s own shipped code.

Both tasks this phase's text had left open are now done - Phase 0 is
complete pending PR review/merge.

**Phase 1 implementation (2026-08-12)**: `ComposeAttribute<TProfile>` and
`ComposeAttribute<TProfile, TConfig>` (+ `ConfigProfileBinder`) both ported
byte-for-byte from `Compono.XunitV3`, adapted to `Compono.TUnit`'s base
class shape. Stacked-attribute rejection added to
`BindingPlan.ValidateSignature`, resolving the method's real `MethodInfo`
via a parameter's `ReflectionInfo.Member` (or a fallback lookup for a
zero-parameter method - see the PR #76 Codex review note below for why
that fallback isn't a plain `Type.GetMethod(name, Type.EmptyTypes)` call)
and counting `ComposeAttribute`-derived attributes on it.

**PR #76 Codex review, round 1 (2026-08-12)**: 2 findings, both confirmed
real, fixed in `e36facd`:
- The zero-parameter `ResolveMethodInfo` fallback originally used
  `Type.GetMethod(name, Type.EmptyTypes)`, which matches by parameter
  *types* only, not generic arity - a class declaring both a
  zero-parameter `Run()` and a zero-parameter-but-generic `Run<T>()`
  threw `AmbiguousMatchException` instead of reaching the existing
  generic-method `CompositionException`, crashing `BindingPlan.Build`
  entirely for that shape. Fixed by filtering `GetMethods()` on name,
  zero declared parameters, *and* `testInformation.GenericTypeCount`
  together. Added `AmbiguousZeroParameterMethod()`/
  `AmbiguousZeroParameterMethod<T>()` fixtures and two regression tests.
- `src/Compono.TUnit/Compono.TUnit.csproj`'s NuGet description still said
  the profile variants "ship in a later phase" despite this same PR
  shipping them - restored to describe the full family.

**PR #76 Codex review, round 2 (2026-08-12)**: 1 finding, confirmed real,
fixed in the same commit as this note - a code comment on
`ValidateSignature` (and this Notes entry, above) still described the
*original*, buggy `Type.EmptyTypes`-only reasoning after the arity-aware
fix replaced it, contradicting the actual implementation and risking
someone "simplifying" `ResolveMethodInfo` back to the broken version on a
future read. Both corrected to point at `ResolveMethodInfo`'s real,
three-part filter.

**Native AOT gate on `ConfigProfileBinder` (ADR-0041 Amendment 1) found a
real gap, not a formality.** Extending the Phase 0 AOT smoke test to also
exercise `[Compose<TProfile, TConfig>]` failed at runtime on first try:
`CompositionException: 'ProfileConfig' must have exactly one public
constructor to be used as profile configuration, but has 0` — the trimmer
strips a closed generic type argument's public constructors by default
unless something tells it they're reachable; "`ConstructorInfo.Invoke` on
an already-known/non-generic `Type` is likely lower-risk than
`MakeGenericMethod`" (this plan's own original hedge) was directionally
right but not sufficient on its own. Fixed with
`[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
annotations on `ConfigProfileBinder`'s `Type`/generic-type-parameter
inputs and on `ComposeAttribute<TProfile, TConfig>`'s own `TProfile`/
`TConfig` type parameters — re-running the same `dotnet publish -c Release
-p:PublishAot=true -r osx-arm64 --self-contained true` + run confirmed
both `[Compose]` and `[Compose<TProfile, TConfig>]` now pass, with zero
trim warnings from `Compono.TUnit`'s own code (`-p:TrimmerSingleWarn=false`
still shows only the same two pre-existing harness-only `IL2072` warnings
from Phase 0).

The Goal-section scenario now runs for real:
`test/Compono.TUnit.SampleTests/NSubstituteTests.cs` mirrors
`Compono.XunitV3.SampleTests/NSubstituteTests.cs` exactly, added
`Compono.NSubstitute` to that project's local-feed pack chain (relies on
`PackageReference`'s default transitive-dependency flow for `NSubstitute`
itself, same as the xUnit v3 sibling - no explicit `NSubstitute`
`PackageReference` needed). Passed under a real TUnit runner across all
four TFMs.

Docs (`docs/packages/compono-tunit.md`,
`skills/compono/references/tunit.md`) updated in the same change - the
former "not part of this slice"/"stacking is undefined" language replaced
with the shipped shape and the real stacked-attribute rejection behavior.

Phase 1 is complete - every task checked off, full solution build/test
green.

**Phase 2 implementation (2026-08-12)**: PR #76 (Phase 1) merged to `main`
first, per this plan's own phase-PR rule. Two tasks, both scoped
verification-only (no new `src/Compono.TUnit` public API):
`test/Compono.TUnit.SampleTests/ConfigProfileTests.cs` (new) adds the
`[Compose<TProfile, TConfig>]` leg the packaged-consumer sample project
was still missing - `RepositoryConsumer`/`RepositoryTestProfile`/
`RepositoryTestConfig`, mirroring `Compono.XunitV3.SampleTests
/ConfigProfileTests.cs` exactly - proving the fourth and last
attribute-family form through the real packaged `Compono.TUnit` ->
`Compono` dependency chain (the other three were already covered by
Phase 0/1's own sample files). `test/Compono.TUnit.Tests
/PublicApiSurfaceTests.cs` (new) locks the exact four-type public surface
(`ComposeAttribute`, `` ComposeAttribute`1``, `` ComposeAttribute`2``,
`SharedAttribute`) via a hand-rolled `IsPublic || IsNestedPublic`
reflection check, matching `Compono.XunitV3.Tests`' own file byte-for-byte
in structure. Both projects' full test suites pass:
`Compono.TUnit.Tests` 52/52 (net10.0, including the new API-surface
test), `Compono.TUnit.SampleTests` 7/7 (net10.0, through the real
packaged pipeline - up from 5, the two new `ConfigProfileTests` cases).
No doc updates in this phase - Phase 3 is the docs/skill closing
consistency pass, not this one.

Phase 2 is complete - every task checked off.

**Phase 3 implementation (2026-08-12)**: PR #79 (Phase 2) merged to `main`
first, per this plan's own phase-PR rule. A pure consistency pass, no
`src/Compono.TUnit` code changes - four stale-doc spots found by direct
rereading (not assumed), all pre-dating Phase 1/2's own shipping, listed
in Phase 3's own task checkboxes above. `docs/roadmap/future-packages.md`
got the larger rewrite the plan's task called for: `Compono.TUnit` moved
from "committed implementation work" language to past-tense "shipped
package" language throughout (intro, Admission model section, and the
"Roadmap items" section itself, now empty with a pointer to Package
Guides) - the five-package count (not four-plus-one) now matches
`docs/packages/index.md`'s own count exactly. `skills/compono-evals
/evals.json` eval 20 turned out already correct (it's an NUnit scenario,
not a TUnit one - the plan's own premise for that half of the task was
stale by the time this phase ran); added eval 21, a `Compono.TUnit` +
`Compono.NSubstitute`-without-`Compono.XunitV3` routing scenario.

Phase 3 is complete - every task checked off. **PLAN-0040 is complete -
all four phases done, `Compono.TUnit` is a shipped package.**
