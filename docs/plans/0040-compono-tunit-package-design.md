# [PLAN-0040] Compono.TUnit Package Design

**Status:** Not Started

**Implements:** [ADR-0040](../adr/0040-compono-tunit-package-design.md)
(`Compono.TUnit` package: method-parameter composition only, no core
`Compono` changes, disposal owned entirely by TUnit, seed observability
via `ITestDiscoveryEventReceiver`/`DiscoveredTestContext.AddProperty`)

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
code disposes anything, and a real TUnit test run confirms nothing leaks.

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
- Extracting `Compono.XunitV3`'s binding-delegate-caching pattern into a
  shared location — ADR-0040 deliberately duplicates it for this release;
  revisit only if a third test-framework package needs the same pattern a
  third time.
- Verifying seed-observability behavior under TUnit's own retry/repeat
  mechanisms — ADR-0040 flags this as unverified; Phase 0's test suite
  investigates and records the actual behavior (it doesn't need profile
  support to check), but this plan does not block on a specific outcome
  (a documented limitation is an acceptable close, not just a passing
  test).

## Phases

Each phase ships as its own PR, per `design-decisions.md`'s phase rule.

### Phase 0: Package skeleton, unqualified `[Compose]`, its own tests and docs

**Status:** Not Started

- [ ] New `src/Compono.TUnit/Compono.TUnit.csproj` — `net8.0;net9.0;net10.0;net11.0`
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
- [ ] `Directory.Packages.props`: add `TUnit.Core`'s `PackageVersion` entry
      (a tested range, matching ADR-0031 Amendment 1's convention — see
      `xunit.v3.extensibility.core`'s own entry for the exact shape) —
      centrally-managed package references restore-fail without it, so
      this isn't optional polish, it's required for the csproj above to
      restore at all.
- [ ] **Generator discovery** (`src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
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
- [ ] `Compono.Generators.Tests`: a snapshot test proving a concrete
      parameter type reachable *only* through a `Compono.TUnit`-attributed
      method (no other discovery path in the same compilation) gets a
      generated plan — mirroring whatever regression test closed the
      equivalent `Compono.XunitV3` gap (ADR-0022's Amendment, fix #2).
- [ ] **Real packaged-consumer proof of the generator-discovery
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
- [ ] `ComposeAttribute : UntypedDataSourceGeneratorAttribute` — the
      no-profile entry point. Overrides `GenerateDataSources(DataGeneratorMetadata)`,
      returns a single deferred `Func<object?[]?>` that (inside the Func,
      not before it) calls `composer.CreateRow(declaringType)` and binds
      each of the method's parameters via `row.Resolve<T>(descriptor)`/
      `row.ResolveShared<T>(descriptor)`, following
      `Compono.XunitV3`'s `BindingPlan`/`ParameterBindingPlan` pattern
      (cached, reflection-once-per-parameter delegate construction, not
      re-reflected per row) — duplicated into this package per ADR-0040,
      not shared.
- [ ] `ComposeAttribute.Seed` (`int`, non-negative) — public property
      mirroring `Compono.XunitV3.ComposeAttribute.Seed` exactly, routed
      into `BuildComposer`'s `CompositionBuilder.WithSeed(...)` call. The
      row's effective seed (`row.Seed < 0`) is checked before any
      parameter composes, matching `Compono.XunitV3`'s own pre-composition
      check — required by ADR-0040's "Seed input and replay" section, not
      optional: without this property a reported seed can never actually
      be pasted back as `[Compose(Seed = ...)]`.
- [ ] `SharedAttribute` — package-local marker, mirroring
      `Compono.XunitV3.SharedAttribute`'s shape and duplicate-shared-type
      validation.
- [ ] Seed observability: `ComposeAttribute` also implements
      `ITestDiscoveryEventReceiver`. Inside the deferred `Func`, after
      `CreateRow` produces the row, store `row.Seed` into
      `dataGeneratorMetadata.TestBuilderContext.StateBag` under a
      package-namespaced key. In `OnTestDiscovered(DiscoveredTestContext)`,
      read it back and call `discoveredContext.AddProperty("Compono.Seed",
      seed.ToString())`. **Do not** store the seed as an attribute-instance
      field — ADR-0040's own `IClassConstructor` finding (a reused
      attribute/receiver instance across rows) is the standing reason.
- [ ] Diagnostics: every `Resolve`/`ResolveShared`/`ShareExplicit` call
      wrapped so a thrown `CompositionException` is rethrown via
      `CompositionException.WithSeedInMessage(exception, seed)` — matching
      `Compono.XunitV3`'s real `InvokeWithSeedOnFailure`, **not** left
      un-wrapped (ADR-0040's Diagnostics section originally mis-described
      `Compono.XunitV3`'s own behavior here and was corrected — see that
      ADR). A pipeline failure without this wrapping would violate this
      same plan's own unconditional seed-observability guarantee exactly
      when a row fails composition.
- [ ] Explicitly confirm during implementation: no `IDisposable`/
      `IAsyncDisposable`/`ITestEndEventReceiver` implementation anywhere in
      this package — ADR-0040's disposal conclusion is a hard constraint
      for this phase, not just a design note to remember.
- [ ] Document both disposal constraints from ADR-0040's "Diagnostics,
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
- [ ] `test/Compono.TUnit.Tests`: binding-plan unit coverage for the
      no-profile shape (parameter resolution, signature-validation errors —
      generic method, ref/out/in, params — `[Shared]` duplicate-type
      validation), mirroring `Compono.XunitV3.Tests`' existing coverage
      shape for the equivalent binding logic.
- [ ] Seed-observability verification, real TUnit run: `AddProperty
      ("Compono.Seed", ...)` actually visible on both a passing and a
      failing `[Compose]` row (via TUnit's own reporting/TRX output, not
      just asserting the internal call happened) — the concrete check for
      the parity guarantee ADR-0040 requires, not an assumption. Also
      investigate whether this holds under `[Retry]`; record the actual
      finding either way (ADR-0040's flagged open item — doesn't need
      profile support to check, so it belongs here, not Phase 2).
- [ ] Disposal verification, real TUnit run, two cases per ADR-0040's
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
- [ ] End-to-end `[Shared]` composition test against a real TUnit run,
      using the no-profile `[Compose]` shape (a plain composed domain
      object reused via `[Shared]`, not the NSubstitute scenario — that
      needs `[Compose<TProfile>]`, Phase 1's own scope; see Phase 1's own
      test item for the full Goal-section scenario).
- [ ] **Build/CI infrastructure wiring** — creating the project alone
      leaves it outside every place this repo's build/release/validation
      pipeline enumerates packages by name; each of the following
      hardcodes the current four-package list and needs `Compono.TUnit`
      added alongside them:
  - [ ] `Compono.slnx`: add `src/Compono.TUnit/Compono.TUnit.csproj` and
        `test/Compono.TUnit.Tests/Compono.TUnit.Tests.csproj`.
  - [ ] `.github/workflows/docs.yml`: add `src/Compono.TUnit/**` to both
        `paths:` trigger lists, and `Compono.TUnit` to the `for pkg in ...`
        build loop feeding the API-reference generator.
  - [ ] `.github/workflows/package-validation.yaml`: add `Compono.TUnit`
        to its `for pkg in ...` loop and the two explicit `pack_one`/path
        lists.
  - [ ] `.github/scripts/inspect-packed-nupkgs.sh`: add `Compono.TUnit` to
        its `for pkg in ...` loop and its own `case` branch (this
        package's own expected dependency set: `Compono` + `TUnit.Core`,
        no `Compono.Generators.dll` embedding since that's `Compono`-only
        per ADR-0003).
  - [ ] `.github/scripts/generate-api-reference.sh`: add `Compono.TUnit`
        to its `integration_pkgs` array so its public API gets generated
        reference docs and cross-link resolution like the other three
        integration packages.
- [ ] New `docs/packages/compono-tunit.md` Package Guide — covers
      `[Compose]`/`[Shared]` (what Phase 0 actually ships); Phase 1 extends
      it with the profile-attribute sections once they exist.
- [ ] `docs/packages/index.md`: add `Compono.TUnit`'s row.
- [ ] **Existing topic docs that become stale the moment `[Compose]`/
      `[Shared]` ships under a second framework** — found by rereading the
      actual current content, not assumed:
  - [ ] `docs/public-api.md` (tombstone) — its "Package Guides" bullet
        lists only `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`;
        add `Compono.TUnit`.
  - [ ] `docs/concepts/shared-values.md` — its "Scope and limits" section
        states "`[Shared]` only applies within `Compono.XunitV3`'s
        `[Compose]` row" as if that's the only such row; reword to name
        both packages (or speak generically about "a `[Compose]`-family
        row," now that a second one exists).
  - [ ] `docs/getting-started/installation.md`/relevant how-to pages: add
        a `Compono.TUnit` install example alongside the existing
        `Compono.XunitV3` one, so the installation path isn't implicitly
        xUnit-v3-only.
- [ ] `skills/compono/references/tunit.md`: new package-conditional
      reference file, covering `[Compose]`/`[Shared]` — matching
      `xunit-v3.md`'s shape; Phase 1 extends it.
- [ ] `skills/compono/SKILL.md`: new Detection-table row
      (`<PackageReference Include="Compono.TUnit"` → load
      `references/tunit.md`); remove `Compono.TUnit` from the "don't
      invent an unshipped package" guardrail's named-absent list (it's no
      longer absent); update the frontmatter `description`'s enumerated
      package list.

### Phase 1: Profile variants, their own tests and docs

**Status:** Not Started

- [ ] `ComposeAttribute<TProfile> : ComposeAttribute` — `new()`-constrained
      profile type parameter, mirroring `Compono.XunitV3`'s
      `ComposeAttribute<TProfile>` exactly (method-level only, matching
      that package's own original scope decision).
- [ ] `ComposeAttribute<TProfile, TConfig> : ComposeAttribute` — profile
      built from attribute-constructor-supplied config args, mirroring
      `Compono.XunitV3`'s `ComposeAttribute<TProfile, TConfig>`
      (ADR-0036) exactly, including its once-per-attribute-instance
      reflection bound and its seed/config value semantics.
- [ ] Inline value support: the attribute's own constructor `params
      object?[]`, strictly positional leading-parameters-only, matching
      `Compono.XunitV3`'s existing precedent (not a second attribute, not
      named-argument binding).
- [ ] Stacked Compose-family attribute validation: reject a test method
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
- [ ] `test/Compono.TUnit.Tests`: profile-binding unit/integration
      coverage (`ComposeAttribute<TProfile>`, `ComposeAttribute<TProfile,
      TConfig>` config binding), inline-value precedence coverage,
      mirroring `Compono.XunitV3.Tests`' `ComposeAttributeConfigBindingTests`/
      `InlineNullHandlingTests` shape. Includes stacked-attribute-rejection
      test cases for the generic and config-generic forms specifically
      (`[Compose]` + `[Compose<TProfile>]`, `[Compose<TProfile>]` +
      `[Compose<TProfile, TConfig>]`, etc.) — the case Phase 0 alone can't
      exercise, since it needs a second Compose-family type to exist.
- [ ] The full Goal-section scenario, run for real under TUnit: `[Shared]
      IOrderRepository` composed via `[Compose<NSubstituteTestProfile>]`,
      `UseNSubstitute()` wired through the profile, `repository` reused
      inside `handler`'s own composed constructor parameter — the
      `Compono.XunitV3.SampleTests.NSubstituteTests.Saves_order` scenario,
      reproduced under TUnit for real (this needs `Compono.NSubstitute` as
      an additional test dependency, matching how the xUnit v3 sample
      project references it).
- [ ] Extend `docs/packages/compono-tunit.md` with the profile-attribute
      sections (`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`,
      inline values).
- [ ] Extend `skills/compono/references/tunit.md` with profile-attribute
      guidance, matching `xunit-v3.md`'s equivalent sections.

### Phase 2: Verification requiring the completed attribute family

**Status:** Not Started

- [ ] A real packaged-consumer sample project run (mirroring PLAN-0004
      Phase 3 / PLAN-0005 Phase 2's precedent exactly) — extends Phase 0's
      own minimal local-feed consumer to exercise the *complete* attribute
      family (`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/
      `[Shared]`) through the actual packaged `Compono.TUnit` → `Compono`
      dependency chain, not `Compono.TUnit.Tests`' own `ProjectReference`-
      based calls.
- [ ] Final API-surface/approval test locking `Compono.TUnit`'s complete
      public shape (`ComposeAttribute` family, `SharedAttribute`, and
      nothing else), matching `Compono.XunitV3.Tests`'/
      `Compono.NSubstitute.Tests`' existing pattern — Phase 0/1 may have
      already started this file for their own incrementally-shipped shape;
      this phase closes it out against the full family.

### Phase 3: Docs and skill consistency close-out

**Status:** Not Started

- [ ] Re-read `docs/packages/compono-tunit.md`, `docs/packages/index.md`,
      `skills/compono/references/tunit.md`, and `SKILL.md`'s Detection
      table/guardrail/description end to end — confirm nothing Phase 0/1
      added is inconsistent or stale now that the full package exists (a
      pure consistency pass; Phase 0/1 already did the substantive writing
      per-behavior).
- [ ] `docs/roadmap/future-packages.md`: move `Compono.TUnit` out of
      "Roadmap items" — it's shipped, not a roadmap item anymore.
- [ ] `skills/compono-evals/evals.json`: retire or rewrite eval scenario
      20 (`Does Compono support NUnit?` — currently uses `Compono.TUnit`
      as an example of a package that doesn't exist; check it doesn't
      accidentally still assert that once `Compono.TUnit` ships) and add a
      routing scenario confirming the skill only recommends
      `Compono.TUnit` guidance when that package is referenced, matching
      the existing NSubstitute/Bogus routing scenarios' shape.

## Critical Files

- `src/Compono.TUnit/Compono.TUnit.csproj` — new
- `src/Compono.TUnit/ComposeAttribute.cs`,
  `ComposeAttribute{TProfile}.cs`, `ComposeAttribute{TProfile,TConfig}.cs`,
  `SharedAttribute.cs` — new
- `src/Compono.TUnit/Binding/*` — new (duplicated pattern, not shared)
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `src/Compono.Generators/ComponoIncrementalGenerator.cs` — modified
  (three new metadata-name constants/registrations for `Compono.TUnit`'s
  attribute family; see ADR-0040's "Generator discovery" section)
- `test/Compono.TUnit.Tests/*` — new
- `test/Compono.Generators.Tests/*` — modified (new snapshot test for
  `Compono.TUnit`-only-reachable discovery)
- A new or extended sample project proving real-package consumption
- `Compono.slnx` — modified (new project entries)
- `Directory.Packages.props` — modified (`TUnit.Core`/`TUnit`/`Compono.TUnit` `PackageVersion` entries)
- `.github/workflows/docs.yml`, `.github/workflows/package-validation.yaml`,
  `.github/scripts/inspect-packed-nupkgs.sh`,
  `.github/scripts/generate-api-reference.sh` — modified (five-package
  enumeration instead of four)
- `docs/packages/compono-tunit.md` — new
- `docs/packages/index.md`, `docs/roadmap/future-packages.md`,
  `docs/public-api.md`, `docs/concepts/shared-values.md`,
  `docs/getting-started/installation.md` — updated
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
