# [PLAN-0006] Milestone 6: Bogus Integration

**Status:** In Progress

**Implements:** [ADR-0026](../adr/0026-deterministic-seed-derivation-for-providers.md)
(core capability: `ICompositionContext.DeriveSeed()`), [ADR-0027](../adr/0027-compono-bogus-package-design.md)
(`Compono.Bogus` package: `BogusMemberNameProvider`, `BogusOptions`, member-level
`UseBogus(faker => ...)` sugar, whole-object `UseBogus<T>(...)` sugar, coexistence
with `Compono.NSubstitute`)

**Note:** both ADRs are `Accepted` as of the design review that produced this plan
(2026-07-31) — implementation may begin.

## Goal

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder
            .UseNSubstitute()
            .UseBogus();
}

[Theory]
[Compose<ApplicationTestProfile>]
public async Task Saves_order(
    [Shared] IOrderRepository repository,
    CreateOrderHandler handler,
    CreateOrder command,
    Customer customer)
{
    // customer.FirstName/LastName/Email etc. are realistic, deterministic Bogus values
    // repository is a real NSubstitute substitute, reused by handler's own constructor parameter
    await handler.Handle(command);
    await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

runs end-to-end, packaged (not `ProjectReference`), proving: fixed pipeline
precedence (semantic before test-double) needs no special ordering between
`UseBogus()`/`UseNSubstitute()`; Bogus never claims an interface/delegate/
abstract-class request and NSubstitute never claims a semantic scalar/member
request; an explicit registration or configuration rule wins over both; the same
seed reproduces the same `customer` values across runs; a `[Shared]` substitute
composed by NSubstitute coexists in the same graph as Bogus-supplied scalar
values with no interaction between the two packages' code.

## Scope

Per ADR-0026/ADR-0027's Decision Outcomes:

- New core `Compono` surface: `ICompositionContext.DeriveSeed()`, backed by
  ADR-0012's existing path-hash mechanism with a distinct salt.
- New `Compono.Bogus` package: `BogusOptions`, `BogusMemberNameProvider`,
  `CompositionBuilderExtensions.UseBogus()`/`UseBogus(Action<BogusOptions>)`/
  `UseBogus<T>(Action<Faker<T>>)`/`UseBogus<T>(string, Action<Faker<T>>)`,
  `MemberRuleExtensions.UseBogus(Func<Faker, TMember>, string)` on the existing
  member-rule builder.
- New test project: `test/Compono.Bogus.Tests`.
- A `test/Compono.XunitV3.SampleTests` extension proving `Compono.Bogus` and
  `Compono.NSubstitute` compose in one real, packaged xUnit v3 consumer (this
  plan's own Goal scenario).
- Doc updates: `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md`.

Explicitly deferred/non-goals — see ADR-0027's own Decision Outcome/Negative
Consequences:

- `.DependsOn(...)` — a Compono-native member-dependency mechanism. Correlated
  values are satisfied via whole-object `Faker<T>` (`UseBogus<T>()`) instead.
- Any change to `Compono.NSubstitute` — this plan touches `Compono.Bogus` and
  core only; coexistence is verified, not implemented, on the NSubstitute side.
- A `Compono.Benchmarks` entry for a Bogus-composed graph — nice-to-have, not
  required for this plan's exit criteria (mirrors PLAN-0005's own deferral of
  the equivalent NSubstitute benchmark).

## Phases

Each phase ships as its own PR, per `design-decisions.md`'s phase rule.

### Phase 0: Core `DeriveSeed()` capability (ADR-0026)

**Status:** Done

- [x] `ICompositionContext.DeriveSeed()`: derives an `int` from the context's
      root seed, the request path currently being resolved, and a fixed salt
      (`RandomSource.DeriveSeedTag`) distinct from ADR-0012's own internal
      per-`PathSegment`-kind fork tags — reusing the existing FNV-1a path-hash
      (`IRandomSource.DeriveSeed()`/`RandomSource.DeriveSeed()`, combining the
      node's already-forked `_forkState` with the new tag), not a new
      algorithm. The 64-bit result is folded into an `int` by XORing its two
      halves (`raw ^ (raw >> 32)`), not truncated to the low 32 bits alone.
      `CompositionRow` forwards to the wrapped `CompositionContext`, matching
      its existing `Resolve<T>()`/`ResolveCollectionSize()` forwarding shape.
- [x] Callable exactly where the descriptor-less `Resolve<T>()` already is: mid-
      `TryProvide` (via `InvokeProvider`, ADR-0024 Amendment 1) and mid-factory
      (via `InvokeFactory`, stage 3/4 — `.For<T>().Use(...)`/`.Member(...).Use(...)`
      rules compile into `TypeRuleProvider`/`MemberRuleProvider`, both of which
      invoke their factory through this exact same `InvokeFactory` method, so
      `Register<T>` coverage below exercises the identical code path). Throws
      `InvalidOperationException` when called outside an active request
      (`_manualResolveFrames.Count == 0`), matching `Resolve<T>()`'s existing
      guard exactly.
- [x] Idempotent within one active request (repeated calls return the same
      value, since it's a pure read of the current node's already-forked state
      via `IRandomSource.DeriveSeed()`); never advances or mutates
      `NextUInt64()`'s own value-state stream.
- [x] `Compono.Tests` coverage in isolation, before `Compono.Bogus` exists
      (`testing.md`'s "verify a new public entry point independently" rule) —
      `DeriveSeedTests.cs`, 7 tests × 2 TFMs: same seed + same path → same
      derived value; sibling requests inside one factory → independent values;
      renaming a constructor parameter without reordering doesn't change its
      derived value, reordering does (ADR-0012's guarantee, re-verified for
      this new entry point); repeated calls within one active request are
      idempotent; calling outside an active request throws; a call from
      inside a public provider's `TryProvide` is deterministic for the same
      seed; concurrent `Create<T>()` calls against the same shared `Composer`
      all land on the same, independently-verified-correct value (no shared
      mutable state bleeding between concurrent calls).

### Phase 1: `Compono.Bogus` package (ADR-0027)

**Status:** Done

- [x] New `src/Compono.Bogus/Compono.Bogus.csproj` (matching
      `Compono.NSubstitute.csproj`'s TFM/packaging shape — `ProjectReference` to
      `Compono` with `PrivateAssets="none"`, `PackageReference` to `Bogus`
      (version `35.6.5`, added to `Directory.Packages.props`, alongside a
      `Compono.Bogus` `Version="1.0.0"` local-feed entry for Phase 2's future
      packaged-consumer test)).
- [x] `BogusOptions`: `Locale` (`string`, default `"en"`),
      `EnableMemberNameConventions` (`bool`, default `true`).
- [x] `BogusMemberNameProvider : ICompositionValueProvider`: exact-match,
      case-sensitive lookup against the documented allowlist (`FirstName`,
      `LastName`, `FullName`, `Email`, `PhoneNumber`, `StreetAddress`, `City`,
      `State`, `PostalCode`, `CompanyName`), backed by a `FrozenDictionary`
      (an immutable, built-once lookup table, not a mutable `Dictionary` —
      this is fixed, read-only library data), gated to `RequestedType ==
      typeof(string)`, `NotHandled` for anything else (including `Name`
      itself, deliberately absent from the allowlist). Constructs a fresh
      `Faker`/`Randomizer` per handled request, seeded via
      `context.DeriveSeed()` — never a shared instance across requests.
- [x] `CompositionBuilderExtensions.UseBogus()`/`UseBogus(Action<BogusOptions>)`:
      registers `BogusMemberNameProvider` via `AddSemanticProvider` when
      `EnableMemberNameConventions` is `true`.
- [x] `CompositionBuilderExtensions.UseBogus<T>(Action<Faker<T>> configureFaker) where T : class`/
      `UseBogus<T>(string locale, Action<Faker<T>> configureFaker) where T : class`
      (the `class` constraint matches `Faker<T>`'s own; the parameter is named
      `configureFaker`, not `configure`, since it's now an `Action`, not a
      `Func`). `configureFaker` is `Action<Faker<T>>`, not
      `Func<Faker<T>, Faker<T>>` — it configures the instance in place
      (`faker.RuleFor(...)` as a statement, discarding the fluent return),
      rather than requiring the caller to return the same instance back.
      Compiles to purely ergonomic sugar over the existing `Register<T>`
      registration mechanism — no hidden pipeline stage, no special runtime
      behavior of its own:
      ```csharp
      builder.Register<T>(context =>
      {
          var faker = new Faker<T>(locale);
          configureFaker(faker);
          return faker.UseSeed(context.DeriveSeed()).Generate();
      });
      ```
      A fresh `Faker<T>` is constructed **inside the factory**, once per `T`
      resolution — the factory closure itself is captured once at `Build()`
      time (same as any other `Register<T>` factory), but no `Faker<T>`
      instance is ever retained or reused across requests, so concurrent
      `Create<T>()` calls for the same `T` never share one. Caching a
      configured `Faker<T>` across requests was considered and rejected — see
      ADR-0027's Model 3 section for why (`Faker<T>` carries mutable
      generation state with no documented concurrent-`Generate()` safety
      guarantee). Fully independent
      of `UseBogus()`/`BogusOptions.Locale` — no ordering dependency, defaults
      to `"en"` on its own.
- [x] `MemberRuleExtensions.UseBogus(Func<Faker, TMember>, string locale = "en")`
      on the existing `.For<T>().Member(x => x.Y)` builder (via a generic
      C# 14 extension block, `extension<TParent, TMember>(CompositionMemberRuleBuilder<TParent, TMember> builder)`):
      compiles to `.Use(context => configure(new Faker(locale) { Random = new Randomizer(context.DeriveSeed()) }))`.
      No `context.Semantic` accessor, no core change beyond Phase 0's
      `DeriveSeed()`.

### Phase 2: Test suites and verification

**Status:** Not Started

- [ ] `test/Compono.Bogus.Tests`: `BogusMemberNameProvider` unit coverage (each
      allowlisted name against `string`, each allowlisted name against a
      non-`string` type declines, `Name` itself declines, an unlisted name
      declines); `UseBogus()`/`UseBogus(configure)` wiring a working provider
      into a real `Composer`; member-level `UseBogus(faker => ...)` sugar
      overriding the convention provider for the same member; whole-object
      `UseBogus<T>(...)` producing a fully Bogus-generated instance, including a
      correlated `RuleFor((f, x) => ...)` rule; duplicate `UseBogus<T>()`
      registration for the same `T` hits the existing
      `CompositionConfigurationException`.
- [ ] Determinism regression coverage (ADR-0026's contract, exercised through
      real Bogus usage): same seed reproduces the same convention-provider
      value and the same `UseBogus<T>()`-generated object; adding an unrelated
      Bogus-backed member elsewhere in the graph doesn't perturb an existing
      one; `CreateMany<T>(n)` produces independently-seeded items for a
      `UseBogus<T>()`-registered type, matching ADR-0012's existing
      `CreateMany` seed-derivation contract.
- [ ] Coexistence tests against a real `Composer` with both `UseBogus()` and
      `UseNSubstitute()` registered, **any call order**: a string member
      resolves via Bogus, an interface/delegate/abstract-class request resolves
      via NSubstitute, neither provider is ever attempted for the other's
      claimed shape (asserted via diagnostics trace, not just outcome); an
      explicit `Register<T>`/`.For<T>().Use(...)` for a type/member either
      package could otherwise touch wins over both; a `[Shared]` NSubstitute
      substitute and Bogus-supplied scalar values coexist correctly in one
      row's scope.
- [ ] `UseBogus<T>()` lifetime/concurrency coverage, proving the corrected
      per-request-`Faker<T>` design (ADR-0027) actually holds: the `configure`
      callback runs once per resolved object, not once at registration time
      (assert an invocation counter increments once per `Create<T>()` call);
      two separate `Create<T>()` calls receive two distinct `Faker<T>`
      instances (no instance identity/state leaks between requests); a
      parallel `Create<T>()`/`CreateMany<T>()` run for a `UseBogus<T>()`-
      registered type produces correct, non-corrupted results with no shared
      mutable `Faker<T>` state observable across threads (a positive
      determinism-under-concurrency test, not a race characterization); the
      same seed and request path reproduce the same generated object across
      separate runs.
- [ ] An API-surface/approval test locking `Compono.Bogus`'s public shape,
      matching `Compono.NSubstitute.Tests`'/`Compono.XunitV3.Tests`' existing
      pattern.
- [ ] A real end-to-end run through `test/Compono.XunitV3.SampleTests` (or a new
      sibling sample) proving this plan's own Goal scenario — `UseBogus()` and
      `UseNSubstitute()` composing one graph under a real xUnit v3 theory,
      packaged (not `ProjectReference`) — matching PLAN-0004 Phase 3/PLAN-0005
      Phase 2's real-packaged-consumer strategy, which has twice caught real
      packaging/compile-time bugs a `ProjectReference`-only build couldn't
      surface.

### Phase 3: Docs and cleanup

**Status:** Not Started

- [ ] `docs/mvp.md` Milestone 6 section: links ADR-0026/ADR-0027/PLAN-0006,
      states implementation status per phase, matching Milestone 5's own
      phase-by-phase doc-update pattern (update in the PR that actually ships
      each phase, not deferred wholesale to this final phase).
- [ ] `docs/architecture.md`: `ICompositionContext`'s conceptual sketch gains
      `DeriveSeed()`; stage 5's Resolution Pipeline row and the stages-4/5/6/7
      summary paragraph stop describing stage 5 as unconditionally empty;
      `Compono.Bogus` Package Boundaries entry gains a real `Owns` list, Design
      line, and implementation status, matching `Compono.NSubstitute`'s entry
      shape; the Open Architectural Decisions "public provider extensibility"
      entry notes both stage 5 and stage 6 now have real registrants.
- [ ] `docs/public-api.md`: Bogus Integration section replaced with the real
      three-model design (convention provider, member-level `UseBogus(faker => ...)`,
      whole-object `UseBogus<T>(...)`) — the `context.Semantic.Email()` sketch
      and the `.DependsOn(...)` sketch both removed/reframed per ADR-0027;
      Naming Vocabulary gains `BogusMemberNameProvider`/`BogusOptions` if
      warranted; Diagnostics/Deterministic Reproduction sections cross-reference
      `DeriveSeed()`.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` index rows (already added
      during the design phase).

## Critical Files

- `src/Compono/ICompositionContext.cs`, `src/Compono/CompositionContext.cs`
  (`DeriveSeed()` public surface/implementation), `src/Compono/IRandomSource.cs`,
  `src/Compono/RandomSource.cs` (`DeriveSeed()` on the fork-state layer, the
  new `DeriveSeedTag`), `src/Compono/CompositionRow.cs` (forwarding
  implementation) — Phase 0.
- `test/Compono.Tests/DeriveSeedTests.cs` (new), `test/Compono.Tests/UniqueValueResolverTests.cs`
  (its hand-written `ICompositionContext` test fake updated for the new
  interface member) — Phase 0.
- `src/Compono.Bogus/` (new project) — `BogusOptions.cs`,
  `BogusMemberNameProvider.cs`, `CompositionBuilderExtensions.cs`,
  `MemberRuleExtensions.cs` — Phase 1.
- `Directory.Packages.props` (`Bogus`, `Compono.Bogus` `PackageVersion`
  entries), `Compono.slnx` (new project entry) — Phase 1.
- `test/Compono.Bogus.Tests/` (new project) — Phase 2.
- `test/Compono.XunitV3.SampleTests/` — new coexistence test(s) — Phase 2.
- `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md` — Phase 3.

## Test Plan

Matches `testing.md`'s existing conventions (xUnit v3 on MTP v2,
Arrange-Act-Assert, fixed-seed determinism assertions, one test project per
`src` project). Per `testing.md`'s "verify a new public entry point in
isolation before the package that will really use it exists" rule,
`DeriveSeed()` gets its own `Compono.Tests` coverage (Phase 0) independent of
`Compono.Bogus`, mirroring PLAN-0005 Phase 0's treatment of
`ICompositionValueProvider`. `Compono.Bogus.Tests` (Phase 2) then covers the
package's own real behavior, its coexistence with `Compono.NSubstitute` in the
same `Composer`, and `UseBogus<T>()`'s per-request `Faker<T>` lifetime under
concurrent composition, plus one real-runner proof (a packaged `test/Compono.XunitV3.SampleTests` run) since that
specific shape has twice caught real bugs a `ProjectReference`-only build
couldn't (PLAN-0004 Phase 3, PLAN-0005 Phase 2).

## Open Items

- No `Compono.Benchmarks` entry for a Bogus-composed graph is planned as part of
  this plan's own exit criteria — worth adding once `Compono.Bogus` ships, to
  characterize `Faker`/`Faker<T>` generation cost against `docs/performance.md`'s
  existing baselines, but not required to call this milestone done.
- `.DependsOn(...)` (ADR-0027's deferred member-dependency mechanism) is not
  designed here. Revisit only if Milestone 7 dogfooding surfaces a real need
  `Faker<T>`'s whole-object correlation doesn't already cover.

## Notes

**Phase 0 (Done):**

- Implemented in the same branch/PR as the design docs (ADR-0026, ADR-0027,
  this plan), per explicit user direction — mirrors PLAN-0005 Phase 0's same
  choice. Phases 1-3 remain separate PRs, per `design-decisions.md`'s phase
  rule.
- Implemented exactly per ADR-0026's Decision Outcome: `IRandomSource`/
  `RandomSource` gained `DeriveSeed()` (a pure read of the node's own
  `_forkState` combined with a new, distinct `DeriveSeedTag`, via the same
  `Fnv1a.Combine` `RandomSource.Fork` already uses — no new hashing
  algorithm); `ICompositionContext`/`CompositionContext` gained the public
  `int DeriveSeed()`, guarded by the exact same `_manualResolveFrames.Count == 0`
  check the descriptor-less `Resolve<T>()` overload already uses, so both
  share one notion of "is a factory/provider invocation currently active."
  `CompositionRow` needed a one-line forwarding addition to stay a complete
  `ICompositionContext` implementation.
- `test/Compono.Tests/UniqueValueResolverTests.cs`'s hand-written `StubContext
  : ICompositionContext` test fake needed a `DeriveSeed()` member (throwing
  `NotSupportedException`, matching its existing `Resolve<TValue>()`/
  `ResolveCollectionSize()` stubs) to keep compiling against the now-larger
  interface — the only other `ICompositionContext` implementation in the
  codebase besides `CompositionContext`/`CompositionRow` themselves.
- `DeriveSeedTests.cs` (7 tests × 2 TFMs = 14) covers the full contract
  entirely through the public `Composer`/`Register<T>`/`AddTestDoubleProvider`
  surface — no new internal test seam was needed. The "callable from inside a
  factory" and "callable from inside a provider" cases are exercised via
  `Register<T>` and a hand-written `ICompositionValueProvider`, respectively;
  a separate `.For<T>().Use(...)` test was judged unnecessary since that rule
  compiles into `TypeRuleProvider`/`MemberRuleProvider`, both of which invoke
  their factory through the exact same `CompositionContext.InvokeFactory`
  method `Register<T>` already exercises — testing it a second time would
  cover the identical code path, not a new one.
- Full suite green: `Compono.Tests` 426/426 (213 × 2 TFMs — 206 pre-existing +
  7 new), whole-solution `dotnet build`/`dotnet test` 734/734, no warnings.

**Phase 1 (Done):**

- Implemented exactly per ADR-0027's Decision Outcome — no deviation from the
  ADR's own code sketches for `BogusOptions`, `BogusMemberNameProvider`,
  `CompositionBuilderExtensions`, or `MemberRuleExtensions`.
- `Compono.Bogus.csproj` mirrors `Compono.NSubstitute.csproj`'s shape:
  `net10.0;net11.0` TFMs, `ProjectReference` to `Compono` with
  `PrivateAssets="none"` (PLAN-0004 Phase 3's packaging lesson, applied
  proactively), `PackageReference` to `Bogus` (version centrally managed,
  `35.6.5` — the latest stable release at the time of this phase), and
  `InternalsVisibleTo` for the not-yet-created `Compono.Bogus.Tests` (Phase 2).
  `Directory.Packages.props` also gained a `Compono.Bogus` `Version="1.0.0"`
  local-feed entry, matching `Compono.XunitV3`/`Compono.NSubstitute`'s
  existing pattern, ahead of Phase 2's own packaged-consumer test needing it.
- `MemberRuleExtensions.UseBogus(...)` is a **generic** C# 14 extension block
  (`extension<TParent, TMember>(CompositionMemberRuleBuilder<TParent, TMember> builder)
  where TMember : notnull`) — the first generic extension block in this
  codebase; `CompositionBuilderExtensions`' own `UseBogus<T>(...)` overloads
  are ordinary generic methods inside a non-generic `extension(CompositionBuilder builder)`
  block, which is a different (already-established) shape.
- XML-doc `<see cref="...">` pointing at a sibling method inside the *same*
  `extension(...)` block doesn't resolve (`CS1574`) — the compiler can't look
  up another extension member by simple name from inside its own block yet.
  Fixed by following `Compono.NSubstitute`'s own existing precedent exactly:
  a plain `<c>UseBogus()</c>`-style code-formatted reference instead of
  `<see cref>` for that one cross-reference case, not a suppression.
- Added to `Compono.slnx`. Whole-solution `dotnet build` green, 0 warnings.
- No test project yet (Phase 2) — `BogusOptions`/`BogusMemberNameProvider`/
  `UseBogus()`/`UseBogus<T>()`/the member-rule `UseBogus(...)` sugar are
  implemented but only build-verified in this phase, not test-verified —
  matching PLAN-0005 Phase 1's own explicit precedent for the identical
  package-skeleton-then-tests split.

Phase 3 (docs/cleanup) hasn't started yet.
ADR-0026/ADR-0027 reached `Accepted` on 2026-07-31, after a design review that
resolved (in order): how Bogus's
randomness should relate to ADR-0012's path-independence guarantee (a new,
narrow, on-demand `DeriveSeed()` capability, not an eager field and not exposing
`IRandomSource`); how explicit member rules should access that determinism
(sugar over the existing stage-4 `.Use(context => ...)` mechanism, replacing
`docs/public-api.md`'s stale `context.Semantic` sketch); whether the built-in
convention provider is on by default (yes, matching `Compono.NSubstitute`'s own
precedent); whether correlated values need a new Compono mechanism (no —
`Faker<T>` already solves it, `.DependsOn(...)` is explicitly deferred); whether
whole-object `Faker<T>` generation needs a first-class API (yes, but
implemented as purely ergonomic sugar over the existing `Register<T>`
registration mechanism, not a new pipeline concept or special runtime
behavior); and whether that whole-object API should share package-wide
locale state with `UseBogus()` (no — kept fully independent, no hidden
call-order coupling).
