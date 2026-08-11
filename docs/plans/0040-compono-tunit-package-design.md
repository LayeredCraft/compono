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
- A real packaged-consumer sample project (mirroring PLAN-0004 Phase 3 /
  PLAN-0005 Phase 2's precedent — a `ProjectReference`-only build cannot
  surface a real packaging bug, only an actual `dotnet add package`-style
  consumer run can).
- Doc updates: new `docs/packages/compono-tunit.md` guide,
  `docs/packages/index.md`, `docs/roadmap/future-packages.md` (candidate →
  shipped), `skills/compono` (new `references/tunit.md`, Detection table
  row, `SKILL.md` description/guardrail update — this is the real trigger
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
      per ADR-0040's minimal-dependency driver).
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
- [ ] Document the externally-owned-disposable constraint from ADR-0040's
      "Diagnostics, disposal, and seed observability" section — do not
      compose a cross-test-shared disposable instance (from
      `UseServiceProvider(...)`/an exact `Register<T>(...)` factory
      returning a shared instance) as a `[Compose]`/`[Shared]` parameter,
      since TUnit's reference-counted disposal has no provenance
      awareness and will dispose it after the first test that uses it.
      Lands in the new Package Guide (below) and as a `Compono.TUnit`-
      specific skill guardrail — a real constraint, not a footnote to
      mention once and forget.
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
- [ ] Disposal verification, real TUnit run: a simple `IDisposable`
      domain/test type (a small purpose-built type recording whether
      `Dispose()` was called — not a `[Shared]` substitute or any other
      mocking-library-produced object) composed via `[Compose]`, confirming
      TUnit disposes it without any `Compono.TUnit`-side cleanup code — the
      concrete check for ADR-0040's "TUnit owns 100% of it" conclusion.
- [ ] End-to-end `[Shared]` composition test against a real TUnit run,
      using the no-profile `[Compose]` shape (a plain composed domain
      object reused via `[Shared]`, not the NSubstitute scenario — that
      needs `[Compose<TProfile>]`, Phase 1's own scope; see Phase 1's own
      test item for the full Goal-section scenario).
- [ ] New `docs/packages/compono-tunit.md` Package Guide — covers
      `[Compose]`/`[Shared]` (what Phase 0 actually ships); Phase 1 extends
      it with the profile-attribute sections once they exist.
- [ ] `docs/packages/index.md`: add `Compono.TUnit`'s row.
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
      Phase 3 / PLAN-0005 Phase 2's precedent exactly) — a
      `ProjectReference`-only build cannot surface a real packaging bug.
      Exercises the full attribute family (`[Compose]`/`[Compose<TProfile>]`/
      `[Compose<TProfile, TConfig>]`/`[Shared]`) through the actual
      packaged `Compono.TUnit` → `Compono` dependency chain, not
      `Compono.TUnit.Tests`' own `ProjectReference`-based calls.
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
- `test/Compono.TUnit.Tests/*` — new
- A new or extended sample project proving real-package consumption
- `docs/packages/compono-tunit.md` — new
- `docs/packages/index.md`, `docs/roadmap/future-packages.md` — updated
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
