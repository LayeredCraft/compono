# [PLAN-0005] Milestone 5: NSubstitute Integration

**Status:** In Progress

**Implements:** [ADR-0024](../adr/0024-public-provider-extensibility-model.md)
(core public provider extensibility: `ICompositionValueProvider`,
`CompositionProviderRequest`/`CompositionProviderResult`,
`AddSemanticProvider`/`AddTestDoubleProvider`, the `PublicProviderAdapter`/
`ProviderType` diagnostics-identity mechanism), [ADR-0025](../adr/0025-compono-nsubstitute-package-design.md)
(`Compono.NSubstitute` package: `NSubstituteProvider`, `NSubstituteOptions`,
`UseNSubstitute`, substitutable-shape rules, diagnostics)

**Note:** both ADRs are `Accepted` as of the design review that produced this
plan (2026-07-31) — implementation may begin.

## Goal

```csharp
var composer = Composer.Create(builder => builder.UseNSubstitute());

[Theory]
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

runs end-to-end: `repository` is a real NSubstitute substitute, reused as the
exact same instance inside `handler`'s own composed `IOrderRepository`
constructor parameter; a request for an unsubstitutable type (a sealed
concrete class) still produces the engine's existing clear diagnostic naming
the type and path; no manual `Substitute.For<T>()` call appears anywhere in
the test.

## Scope

Per ADR-0024/ADR-0025's Decision Outcomes:

- New core `Compono` surface: `ICompositionValueProvider`,
  `CompositionProviderRequest`, `CompositionProviderResult`,
  `CompositionBuilder.AddSemanticProvider`/`AddTestDoubleProvider`, the
  internal `PublicProviderAdapter`, `ICompositionProvider.ProviderType`
  (default-interface member), and threading `CompositionConfiguration
  .SemanticProviders`/`TestDoubleProviders` through `Composer.Create<T>`/
  `CreateMany<T>`/`CreateRow` into `CompositionContext`'s already-existing
  (always-empty-until-now) `_semanticProviders`/`_testDoubleProviders`
  fields.
- New `Compono.NSubstitute` package: `NSubstituteProvider`,
  `NSubstituteOptions`, `CompositionBuilderExtensions.UseNSubstitute`.
- New test project: `test/Compono.NSubstitute.Tests`.
- Doc updates: `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md`.

Explicitly deferred/non-goals — see ADR-0024/ADR-0025's own Deferred
Decisions/Non-goals:

- Recursive auto-configuration of substitute members.
- NSubstitute API wrappers (`Arg`/`Received`/`Returns` stay NSubstitute's own
  surface).
- A delegate-based convenience overload for lightweight ad hoc custom
  providers (ADR-0024's contract-shape Pros/Cons notes this as a possible
  future addition, not designed here).
- Wrapping a provider's own thrown exception into a diagnostic-carrying
  `CompositionException` (ADR-0024's Provider Failure Semantics — an accepted
  asymmetry, not solved here).
- `Compono.Bogus`/Milestone 6 itself — ADR-0024 validates the contract against
  a Bogus sketch but does not build it.
- A `Compono.Benchmarks` entry for a substitute-composed graph (nice-to-have,
  tracked as an open item below, not required for this plan's exit criteria).

## Phases

Each phase ships as its own PR, per `design-decisions.md`'s phase rule.

### Phase 0: Core engine extension point (ADR-0024)

**Status:** Done

- [x] `CompositionProviderRequest` (public readonly struct):
      `RequestedType`/`DeclaringType`/`Name`/`Nullability`.
- [x] `CompositionProviderResult` (public readonly struct): `NotHandled`
      static property, `Handled(object? value)` static factory, internal
      `IsHandled`/`Value`.
- [x] `ICompositionValueProvider` (public interface): `TryProvide(in
      CompositionProviderRequest, ICompositionContext)`.
- [x] `ICompositionProvider.ProviderType` default-interface member
      (`=> GetType()`); `PublicProviderAdapter : ICompositionProvider`
      overriding it to report the wrapped provider's real type.
- [x] `CompositionContext.TryProviders`/`InvokeFactory`'s `provider`
      parameter passthrough updated to read `candidate.ProviderType`
      instead of `candidate.GetType()`.
- [x] `CompositionBuilder.AddSemanticProvider(ICompositionValueProvider)`/
      `AddTestDoubleProvider(ICompositionValueProvider)`: accumulate into
      ordered lists, same shape as existing rule-builder accumulators.
- [x] `CompositionBuilder.Build()`: wrap each accumulated provider (in
      registration order) into a `PublicProviderAdapter`, assign to two new
      `CompositionConfiguration` fields (`SemanticProviders`,
      `TestDoubleProviders`, both `IReadOnlyList<ICompositionProvider>`,
      defaulting to `[]`).
- [x] `Composer.Create<T>`/`CreateMany<T>`/`ComposeMany<T>`/`CreateRow`:
      thread `_configuration.SemanticProviders`/`TestDoubleProviders`
      through to `CompositionContext`'s existing constructor parameters of
      the same name (currently always passed `[]`) — no `CompositionContext`
      signature change, only what's passed in at each of these four call
      sites.
- [x] `Compono.Tests` coverage for this API in isolation, before any
      `Compono.NSubstitute` code exists (`testing.md`'s "verifying a new
      public entry point" rule): a hand-written `ICompositionValueProvider`
      test double registered via each of `AddSemanticProvider`/
      `AddTestDoubleProvider`, proving stage placement (5 vs. 6), ordering
      (registration order, first match wins), `NotHandled` falling through
      to stage 7/8, `ProviderAttempt.Provider` naming the real provider type
      through the adapter (not `PublicProviderAdapter` itself), a
      `[Shared]`-equivalent request's successful public-provider result
      reaching scope and being reused by a later request, and a thrown
      exception from a public provider propagating uncaught (per ADR-0024's
      Provider Failure Semantics).

### Phase 1: `Compono.NSubstitute` package (ADR-0025)

**Status:** Done

- [x] New `src/Compono.NSubstitute/Compono.NSubstitute.csproj` (matching
      `Compono.XunitV3.csproj`'s TFM/packaging shape — `PackageReference` to
      `Compono` + `NSubstitute`, `PrivateAssets="none"` on the `Compono`
      reference per PLAN-0004 Phase 3's real packaging-bug lesson).
- [x] `NSubstituteOptions`: `SubstituteAbstractClasses` (`bool`, default
      `true`).
- [x] `NSubstituteProvider : ICompositionValueProvider`: the
      `IsSubstitutable` static check (interface / delegate / conditionally
      unsealed-abstract-class), `Substitute.For(Type[], object[])` call for
      a match, `NotHandled` otherwise, no `context.Resolve<T>()` call
      anywhere in the type. Per ADR-0025 Amendment 1: the constructor
      **snapshots** `options.SubstituteAbstractClasses` into a `private
      readonly bool` field — it must never store the `NSubstituteOptions`
      reference itself, so a caller mutating that instance after
      registration can't change an already-registered provider's behavior
      (the guarantee [ADR-0024](../adr/0024-public-provider-extensibility-model.md)
      commits to: immutable, reusable provider registration). Delegate
      matching uses `requestedType.IsSubclassOf(typeof(MulticastDelegate))`,
      **not** `typeof(Delegate).IsAssignableFrom(requestedType)` — the
      latter wrongly matches the non-substitutable `Delegate`/
      `MulticastDelegate` base types themselves.
- [x] `CompositionBuilderExtensions.UseNSubstitute()`/
      `UseNSubstitute(Action<NSubstituteOptions>)` extension methods
      (C# 14 extension-block syntax, per `coding-standards.md`).

### Phase 2: Test suites and verification

**Status:** Not Started

- [ ] `test/Compono.NSubstitute.Tests`: `IsSubstitutable` unit coverage
      (interface / delegate / unsealed abstract class with the option on /
      unsealed abstract class with the option off / sealed class / struct /
      `string`); `NSubstituteProvider.TryProvide` returns a real NSubstitute
      substitute for each positive case (assert via `object is
      ICallRouterProvider`/NSubstitute's own "is this a substitute" check,
      not a hand-rolled heuristic); `UseNSubstitute()`/
      `UseNSubstitute(configure)` wiring a working `NSubstituteProvider`
      into a real `Composer`.
- [ ] **ADR-0025 Amendment 1 regression coverage:**
      - Negative `IsSubstitutable` cases for `typeof(Delegate)` and
        `typeof(MulticastDelegate)` themselves (the framework base types),
        alongside a positive case for a real custom `delegate` type — the
        exact distinction `IsSubclassOf(typeof(MulticastDelegate))` exists
        to draw.
      - A mutation test: construct `NSubstituteOptions`, pass it to
        `UseNSubstitute(...)` (or construct `NSubstituteProvider` directly),
        then mutate `SubstituteAbstractClasses` on that same instance
        afterward, and assert the already-registered provider's behavior is
        unaffected — proving the snapshot, not just that it compiles.
- [ ] End-to-end composition tests against a real `Composer`: an interface
      parameter composes as a substitute; an abstract class composes as a
      substitute only when the option allows it (and produces the engine's
      ordinary "could not satisfy" diagnostic, naming the type, when it
      doesn't); a delegate parameter composes as a substitute; a `[Shared]`
      substitute (via `CompositionRow`) is reused by a later nested
      constructor parameter of the same type — the `[Shared] IRepository`
      shape from this plan's own Goal section, run for real, not just
      asserted against a hand-written fake provider (Phase 0 already covers
      that in isolation).
- [ ] An API-surface/approval test locking `Compono.NSubstitute`'s public
      shape (`NSubstituteProvider`, `NSubstituteOptions`,
      `CompositionBuilderExtensions`, and nothing else), matching
      `Compono.XunitV3.Tests`' existing pattern (PLAN-0004 Phase 3).
- [ ] A real end-to-end run through `test/Compono.XunitV3.SampleTests` (or a
      new sibling sample project) proving `UseNSubstitute()` composes
      correctly under a real xUnit v3 theory, not just `Compono.NSubstitute.Tests`'
      own direct `Composer` calls — matching PLAN-0004 Phase 3's real-
      packaged-consumer testing strategy, since that's exactly where
      PLAN-0004 caught a real packaging bug (`PrivateAssets` on the
      `Compono` `ProjectReference`) that a `ProjectReference`-only build
      couldn't surface.

### Phase 3: Docs and cleanup

**Status:** Not Started

- [x] `docs/architecture.md`: Resolution Pipeline stage 5/6 rows, the
      stages-4/5/6/7 summary paragraph, and the "Public provider
      extensibility" Open Architectural Decisions entry all updated to
      stop claiming the core mechanism is "not yet implemented"/stages
      5/6 are unconditionally "empty" — done early, in response to PR #28
      review (Codex, P2): the design-phase wording became stale the
      moment Phase 0 merged. Still accurately says `Compono.NSubstitute`
      itself doesn't exist yet.
- [ ] `docs/architecture.md`: Providers section still needs the
      public/internal provider split write-up (`ICompositionValueProvider`
      alongside the existing internal `ICompositionProvider` sketch); a
      Package Boundaries entry for `Compono.NSubstitute`'s real `Owns`
      list — both deferred to this phase since they're additive
      documentation, not corrections of a false claim.
- [x] `docs/mvp.md` Milestone 5 section: links ADR-0024/ADR-0025/PLAN-0005,
      states ADR-0024's core mechanism is implemented (PLAN-0005 Phase 0)
      versus `Compono.NSubstitute` itself not yet — done early, same PR
      #28 review round. Delegate substitution already in the scope list
      from the design-phase draft.
- [ ] `docs/mvp.md`: mark this milestone's exit criteria met once
      `Compono.NSubstitute` actually ships (Phase 1/2) — still open.
- [x] `docs/public-api.md`: Provider Extensibility section now says
      "Implemented" rather than "design target" — done early, same PR #28
      review round. NSubstitute Integration section correctly still says
      "not yet implemented."
- [x] `docs/public-api.md`: Naming Vocabulary gains `ICompositionValueProvider`
      — already added during the design phase.
- [x] `docs/adr/README.md`/`docs/plans/README.md` index rows (already added
      alongside the ADRs/this plan, during the design phase).

## Critical Files

- `src/Compono/ICompositionValueProvider.cs`,
  `CompositionProviderRequest.cs`, `CompositionProviderResult.cs` (new) —
  Phase 0.
- `src/Compono/ICompositionProvider.cs`,
  `src/Compono/Providers/PublicProviderAdapter.cs` (new),
  `CompositionContext.cs`, `CompositionBuilder.cs`,
  `CompositionConfiguration.cs`, `Composer.cs` — Phase 0.
- `src/Compono.NSubstitute/` (new project) — `NSubstituteProvider.cs`,
  `NSubstituteOptions.cs`, `CompositionBuilderExtensions.cs` — Phase 1.
- `test/Compono.NSubstitute.Tests/` (new project) — Phase 2.
- `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md` — Phase 3.

## Test Plan

Matches `testing.md`'s existing conventions (xUnit v3 on MTP v2,
Arrange-Act-Assert, fixed-seed determinism assertions where relevant, one
test project per `src` project). Per `testing.md`'s "verifying a new public
entry point" rule, `ICompositionValueProvider`/`AddSemanticProvider`/
`AddTestDoubleProvider` need their own isolated `Compono.Tests` coverage
(Phase 0) with a hand-written test double, independent of
`Compono.NSubstitute` — the same "core entry point tested in isolation
before the package that will really use it exists" pattern PLAN-0004 Phase 0
already established for `CompositionRow`. `Compono.NSubstitute.Tests`
(Phase 2) then covers the package's own real behavior, plus one real-runner
proof (a sample xUnit v3 project, PLAN-0004 Phase 3's precedent) since that
specific testing shape is what caught a real packaging bug last milestone.

## Breaking-Change Handling During Alpha

Per [ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s Alpha
Compatibility Policy: `Compono` is pre-MVP-completion alpha software, and
`ICompositionValueProvider`/`CompositionProviderRequest`/`CompositionProviderResult`
(and `Compono.NSubstitute`'s own public surface, per ADR-0025) are public but
provisional — Milestone 6 and Milestone 7 are expected to validate them further,
and a breaking change to any of them is allowed during alpha when real
integration/dogfooding evidence justifies it. This plan's own Phase 0/1 work is
the *first* shipment of that contract, not a breaking change to an already-shipped
one — nothing below applies to this plan's initial implementation. It applies to
**any future PR** (in this plan's later phases, or after `Status: Done`) that
changes the shape of a contract this plan already shipped.

This repo's established breaking-change mechanism is Release Drafter
(`.github/release-drafter.yml`): its `version-resolver` maps the `breaking` (or
`major`) GitHub label directly to a major version bump, distinct from every other
category (`feat`/`fix`/`chore`/`refactor`/`deps`/`docs`/`ci`, which all resolve to
a minor or patch bump). There is currently no title/branch-pattern autolabeler rule
for `breaking` (unlike `feat`/`fix`/`docs`/etc., which autolabel from a
Conventional-Commit-style PR title prefix) — applying the `breaking` label to the
PR is a manual, deliberate step, which is itself the point: a breaking change to a
public contract should never be silently swept into a `feat`/`refactor` bump.

A PR that changes an already-shipped piece of this plan's public surface during
alpha must:

- Apply the `breaking` label to the PR (`.github/release-drafter.yml`'s
  `version-resolver.major` category) so Release Drafter's next draft release
  produces a major version bump, not a minor/patch one.
- Call out the breaking change explicitly in the PR description — what changed,
  what a consumer needs to do differently, not just "see diff."
- Update the relevant ADR (a dated `## Amendment N (YYYY-MM-DD)` section per
  `design-decisions.md`'s Amendment mechanic — never a silent edit to the ADR's
  original Decision Outcome text), `docs/public-api.md`, and this plan's own Notes
  section, all in the same PR — not as follow-up cleanup.
- No separate "migration notes" document exists in this repo yet
  (`docs/mvp.md` is pre-1.0 and has never needed one) — until one does, the PR
  description plus the ADR's Amendment section together are this repo's record of
  the change; a future PR is free to introduce a dedicated migration-notes doc if
  breaking changes during alpha turn out to be frequent enough to warrant one, but
  that's not designed here.

## Open Items

- No `Compono.Benchmarks` entry for a substitute-composed graph is planned
  as part of this plan's own exit criteria — worth adding once
  `Compono.NSubstitute` ships, to characterize NSubstitute's own proxy-
  generation cost against `docs/performance.md`'s existing baselines, but
  not required to call this milestone done.
- The "NSubstitute itself throws for a superficially-matching type" residual
  diagnostics gap (ADR-0025's Diagnostics section) is accepted, not solved,
  in this plan. Revisit only if real usage surfaces this as an actual pain
  point.

## Notes

**Phase 1 (Done):**

- Implemented exactly per ADR-0025's Amendment 1 corrected shapes — no
  deviation from the ADR's own code sketches for `NSubstituteOptions`,
  `NSubstituteProvider`, or `CompositionBuilderExtensions`.
- `Compono.NSubstitute.csproj` mirrors `Compono.XunitV3.csproj`'s shape:
  `net10.0;net11.0` TFMs, `ProjectReference` to `Compono` with
  `PrivateAssets="none"` (PLAN-0004 Phase 3's packaging lesson, applied
  proactively rather than rediscovered), `PackageReference` to
  `NSubstitute` (version centrally managed, already present in
  `Directory.Packages.props` from the test-project usage), and
  `InternalsVisibleTo` for the not-yet-created `Compono.NSubstitute.Tests`
  (Phase 2) so `IsSubstitutable` can stay `internal`.
  `docs/architecture.md`'s package-dependency diagram already lists
  `Compono.NSubstitute → Compono, NSubstitute` — matched exactly, no
  reference to any other package.
- Added to `Compono.slnx`. Whole-solution `dotnet build` green, no warnings.
- No test project yet (Phase 2) — `IsSubstitutable`/`NSubstituteProvider`/
  `UseNSubstitute` are implemented but only build-verified in this phase,
  not test-verified; that's Phase 2's job per the plan's own phase split.

**Phase 0 (Done):**

- Implemented in the same branch/PR as the design docs, per explicit user
  direction — Phase 1-3 remain separate PRs, per `design-decisions.md`'s
  phase rule.
- The 3-arg `CompositionContext(seed, registrations, serviceProvider)` test
  seam (used by `CompositionRegistrationTests`) also forwards into the
  extended constructor chain and needed its own `semanticProviders: []`/
  `testDoubleProviders: []` update — not called out explicitly in the
  plan's task wording (only the 5-arg/6-arg constructors were), but the
  same mechanical change threaded one level further down the existing
  constructor-forwarding chain.
- `CompositionValueProviderTests` (new file,
  `test/Compono.Tests/CompositionValueProviderTests.cs`) covers the public
  `Composer`-level surface (`AddSemanticProvider`/`AddTestDoubleProvider`,
  stage precedence, registration order, `NotHandled` fallthrough,
  diagnostics identity through the adapter, shared-scope reuse, uncaught
  provider exceptions) — the shared-scope-reuse test uses the internal
  `CompositionContext(...)` test seam directly (wrapping the test double in
  `PublicProviderAdapter` explicitly, since that seam takes internal
  `ICompositionProvider`, not the public `ICompositionValueProvider`) to
  reach `ResolveSharedForTesting`, the same seam
  `CompositionScopeTests`/`CompositionDiagnosticsTests` already use for
  scope-level assertions with no public `[Shared]` entry point available at
  the `Compono` core layer.
- Full suite green: `Compono.Tests` 408/408 (204 × 2 TFMs — 196 pre-existing
  + 8 new), `Compono.Generators.Tests` 166/166 (unaffected),
  `Compono.XunitV3.Tests` 94/94 (unaffected) — `dotnet build`/`dotnet test`
  against the whole solution, not just the new test file.
- **PR #28 review (Codex, two P1 findings) caught a real gap in this
  phase's first pass, fixed before merge — see ADR-0024 Amendment 1 for
  the full account.** A provider calling the descriptor-less
  `context.Resolve<T>()` threw unconditionally (no manual-resolve frame
  was ever pushed for public-provider dispatch), and a provider recursing
  on its own requested type had no cycle protection at all and ran until
  `StackOverflowException`. Fixed with a new internal
  `CompositionContext.InvokeProvider` method — structurally parallel to
  `InvokeFactory` but reentrance-keyed on `(provider instance, requested
  type)` rather than delegate identity, and deliberately **not** reusing
  `InvokeFactory`'s exception-wrapping catch block, to keep ADR-0024's
  Provider Failure Semantics decision (uncaught propagation) intact.
  `PublicProviderAdapter` now carries its own `PipelineStage` (set at
  construction in `CompositionBuilder.Build()`) so `InvokeProvider`'s
  reentrance-failure trace entry is recorded against the right stage.
  Two new regression tests added; the pre-existing
  `ThrownException_FromAPublicProvider_PropagatesUncaught` test continues
  to pass unchanged. Full suite re-verified green after the fix: 672/672
  across the whole solution (`Compono.Tests` 412/412 = 206 × 2 TFMs).
- **A second PR #28 review round (Codex, two P2 findings) caught doc/API
  wording that went stale the moment the fixes above merged.** (1)
  `docs/architecture.md`/`mvp.md`/`public-api.md` still described the
  provider extension point as "design only"/stages 5/6 as unconditionally
  "empty" — no longer true for the core mechanism, only for
  `Compono.NSubstitute` itself; pulled forward from this plan's own Phase
  3 task list rather than left contradicting the code until that phase
  lands (see Phase 3's task list above for exactly what was pulled
  forward versus what's still open there). (2)
  `ICompositionContext.Resolve<TValue>()`'s public XML doc and its
  `InvalidOperationException` message still described a factory-only
  contract, not mentioning that a provider's `TryProvide` (via the
  `InvokeProvider` fix above) is now also a valid caller — updated both,
  plus the same "factory-only" framing on `PathSegment.ManualResolve`/
  `CompositionRequestKind.ManualResolve`'s XML docs for consistency. Full
  suite re-verified green: 672/672.

[ADR-0024](../adr/0024-public-provider-extensibility-model.md)/
[ADR-0025](../adr/0025-compono-nsubstitute-package-design.md) reached
`Accepted` on 2026-07-31, after design review confirmed the provider
contract should be scoped to the architectural guarantees listed in
ADR-0024's Decision Outcome (named interface, small decoupled request/
result contract, deterministic stage placement/order, isolation from
internal engine types, logical-provider diagnostics identity, immutable
reusable registration, `NotHandled`/`Handled` semantics) rather than
over-designed for hypothetical post-alpha stability — see ADR-0024's Alpha
Compatibility Policy. No task below is checked off yet; implementation has
not started.
