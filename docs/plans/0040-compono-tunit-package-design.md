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
var composer = Composer.Create();

[Test]
[Compose]
public async Task Saves_order(
    [Shared] IOrderRepository repository,
    CreateOrderHandler handler,
    CreateOrder command)
{
    await handler.Handle(command);

    await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

runs end-to-end under TUnit: `repository`/`handler`/`command` are all
composed through one `CompositionRow`, `repository` is reused as the
exact same instance inside `handler`'s own composed constructor
parameter (mirroring `Compono.XunitV3`'s own `[Shared]` scenario, now
proven a second time under a structurally different test framework); the
row's seed is discoverable via TUnit's own reporting surface
(`TestContext`/`DiscoveredTestContext.AddProperty`) whether the test
passes or fails, not only on failure; no `Compono.TUnit` code disposes
anything, and a real TUnit test run confirms nothing leaks.

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
  mechanisms — ADR-0040 flags this as unverified; Phase 2's test suite
  investigates and records the actual behavior, but this plan does not
  block on a specific outcome (a documented limitation is an acceptable
  close, not just a passing test).

## Phases

Each phase ships as its own PR, per `design-decisions.md`'s phase rule.

### Phase 0: Package skeleton and unqualified `[Compose]`

**Status:** Not Started

- [ ] New `src/Compono.TUnit/Compono.TUnit.csproj` — `net8.0;net9.0;net10.0;net11.0`
      (matching every other package's TFM window per ADR-0038),
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
- [ ] Diagnostics: pipeline `CompositionException`s propagate un-wrapped,
      matching `Compono.XunitV3` exactly — no new exception type.
- [ ] Explicitly confirm during implementation: no `IDisposable`/
      `IAsyncDisposable`/`ITestEndEventReceiver` implementation anywhere in
      this package — ADR-0040's disposal conclusion is a hard constraint
      for this phase, not just a design note to remember.

### Phase 1: Profile variants

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

### Phase 2: Test suites and verification

**Status:** Not Started

- [ ] `test/Compono.TUnit.Tests`: binding-plan unit coverage (parameter
      resolution, inline-value precedence, `[Shared]` duplicate-type
      validation), mirroring `Compono.XunitV3.Tests`' existing coverage
      shape for the equivalent binding logic.
- [ ] End-to-end composition tests against a real `Composer` under a real
      TUnit test run (not just direct API calls) — the `[Shared] IOrderRepository`
      shape from this plan's Goal section, run for real.
- [ ] **Seed observability verification**: a real TUnit test run confirming
      `AddProperty("Compono.Seed", ...)` is actually visible on both a
      passing and a failing row (via TUnit's own reporting/TRX output, not
      just asserting the internal call happened) — this is the concrete
      check for the parity guarantee ADR-0040 requires, not an assumption.
      Also investigate (per ADR-0040's flagged open item) whether this
      holds under `[Retry]`; record the actual finding either way.
- [ ] **Disposal verification**: a real TUnit test run with a simple
      `IDisposable` domain/test type (a small purpose-built type recording
      whether `Dispose()` was called — not a `[Shared]` substitute or any
      other mocking-library-produced object) confirming TUnit disposes it
      without any `Compono.TUnit`-side cleanup code — the concrete check
      for ADR-0040's "TUnit owns 100% of it" conclusion, not just trusting
      the design analysis. A plain domain type keeps this test proving
      exactly one thing (TUnit disposes composed method arguments), not
      conflating it with a mocking library's own disposal semantics.
- [ ] An API-surface/approval test locking `Compono.TUnit`'s public shape
      (`ComposeAttribute` family, `SharedAttribute`, and nothing else),
      matching `Compono.XunitV3.Tests`'/`Compono.NSubstitute.Tests`'
      existing pattern.
- [ ] A real packaged-consumer sample project run (mirroring PLAN-0004
      Phase 3 / PLAN-0005 Phase 2's precedent exactly) — a
      `ProjectReference`-only build cannot surface a real packaging bug.

### Phase 3: Docs and skill alignment

**Status:** Not Started

- [ ] New `docs/packages/compono-tunit.md` Package Guide, matching
      `docs/packages/compono-xunitv3.md`'s shape and depth.
- [ ] `docs/packages/index.md`: add `Compono.TUnit`'s row (now five
      shipped packages).
- [ ] `docs/roadmap/future-packages.md`: move `Compono.TUnit` out of
      "Admitted candidates" — it's shipped, not a candidate anymore.
- [ ] `skills/compono/references/tunit.md`: new package-conditional
      reference file, matching `xunit-v3.md`'s shape.
- [ ] `skills/compono/SKILL.md`: new Detection-table row
      (`<PackageReference Include="Compono.TUnit"` → load
      `references/tunit.md`); remove `Compono.TUnit` from the "don't
      invent an unshipped package" guardrail's named-absent list (it's no
      longer absent); update the frontmatter `description`'s enumerated
      package list.
- [ ] `skills/compono-evals/evals.json`: retire or rewrite eval scenario
      20 (`Does Compono support NUnit?` — currently uses `Compono.TUnit`
      as its "still doesn't exist" illustrative negative case in the
      `expected_output`/prompt framing is actually about NUnit, but check
      it doesn't accidentally also assert `Compono.TUnit` doesn't exist
      anywhere in its expectations) and add a routing scenario confirming
      the skill only recommends `Compono.TUnit` guidance when that package
      is referenced, matching the existing NSubstitute/Bogus routing
      scenarios' shape.

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
confirmed disposed with zero `Compono.TUnit`-side cleanup code).

## Notes

(none yet)
