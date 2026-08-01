# [PLAN-0006] Milestone 6: Bogus Integration

**Status:** In Progress

**Implements:** [ADR-0026](../adr/0026-deterministic-seed-derivation-for-providers.md)
(core capability: `ICompositionContext.DeriveSeed()`), [ADR-0027](../adr/0027-compono-bogus-package-design.md)
(`Compono.Bogus` package: `BogusMemberNameProvider`, `BogusOptions`, member-level
`UseBogus(faker => ...)` sugar, whole-object `UseBogus<T>(...)` sugar, coexistence
with `Compono.NSubstitute`), [ADR-0028](../adr/0028-configurable-bogus-member-name-conventions.md)
(configurable conventions: `BogusConvention`, `BogusOptions.AddAlias`/`AddConvention`,
scoped to a single `UseBogus(...)` call — a new ADR, not an amendment to ADR-0027)

**Note:** all three ADRs are `Accepted`. ADR-0026/ADR-0027 were accepted
2026-07-31; ADR-0028 (configurable conventions) was accepted 2026-08-01, after
its own design review — see that ADR for the alternatives considered and
rejected (notably: cross-call/cross-profile conflict detection, and the core
build-finalization hook it would have needed, both explicitly deferred).

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
- Configurable member-name conventions (ADR-0028): `BogusConvention` (public
  enum), `BogusOptions.AddAlias(string, BogusConvention)`/
  `AddConvention(string, Func<Faker, string>)`, the internal
  `BogusConventions` shared built-in lookup, and `BogusMemberNameProvider`'s
  constructor gaining a merged-conventions parameter. Scoped to a single
  `UseBogus(...)` call — no cross-call/cross-profile conflict detection (see
  ADR-0028's Non-Goals).
- New test project: `test/Compono.Bogus.Tests`.
- A `test/Compono.XunitV3.SampleTests` extension proving `Compono.Bogus` and
  `Compono.NSubstitute` compose in one real, packaged xUnit v3 consumer (this
  plan's own Goal scenario).
- Doc updates: `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md`.

Explicitly deferred/non-goals — see ADR-0027's own Decision Outcome/Negative
Consequences:

- `.DependsOn(...)` — a Compono-native member-dependency mechanism. Correlated
  values are satisfied via whole-object `Faker<T>` (`UseBogus<T>()`) instead.
- Cross-call/cross-profile alias/custom-convention conflict detection or
  merging, and the generic `CompositionBuilder` build-finalization capability
  it would need — evaluated and explicitly deferred by ADR-0028; each
  `UseBogus(...)` call's conventions are validated independently.
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
      `Compono.Bogus` `Version="1.0.0"` local-feed entry for Phase 3's future
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
          var faker = new Faker<T>(locale).UseSeed(context.DeriveSeed());
          configureFaker(faker);
          return faker.Generate();
      });
      ```
      **`UseSeed(...)` runs before `configureFaker`, not after** — corrected
      by [ADR-0027 Amendment 1](../adr/0027-compono-bogus-package-design.md#amendment-1-2026-08-01-useseed-must-run-before-configurefaker-not-after)
      (caught by PR #33 review): a `configureFaker` callback that eagerly
      reads randomness at configuration time (an already-evaluated
      `RuleFor(x => x.Id, faker.Random.Guid())`, not a lazy
      `f => f.Random.Guid()` factory) must still see this request's
      deterministic seed, not Bogus's own default unseeded `Randomizer`
      state — seeding first covers both that eager read and every lazy
      `RuleFor` factory `Generate()` evaluates afterward, since `UseSeed(...)`
      sets `Random` immediately and it persists across both calls.
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

### Phase 2: Configurable member-name conventions (ADR-0028)

**Status:** Not Started

Renumbered from the original 3-phase plan (Phase 2 "Test suites and
verification" → Phase 3, Phase 3 "Docs and cleanup" → Phase 4) so
implementation phases stay grouped together before the test-suite phase, per
ADR-0028's own design review (added 2026-08-01, after Phase 0 shipped and
Phase 1 was in review). This phase builds directly on Phase 1's
`BogusMemberNameProvider`/`BogusOptions`, so it has to land after Phase 1
merges, before Phase 3's test suite (which should cover the complete
`Compono.Bogus.Tests` surface — base package and configurable conventions
together — in one coherent pass, per ADR-0028's Links section).

- [ ] `BogusConvention` (public enum): `FirstName`, `LastName`, `FullName`,
      `Email`, `PhoneNumber`, `StreetAddress`, `City`, `State`, `PostalCode`,
      `CompanyName` — one member per existing built-in convention, no
      behavior beyond identity.
- [ ] `BogusConventions` (new internal static class): the shared built-in
      source of truth `BogusMemberNameProvider`'s hardcoded `Conventions`
      dictionary (Phase 1) moves to —
      `ByName: FrozenDictionary<string, Func<Faker, string>>` (collision
      checks, the default lookup) and
      `ByConvention: FrozenDictionary<BogusConvention, Func<Faker, string>>`
      (alias-target resolution), both derived from one underlying set so the
      ten generator delegates aren't duplicated.
- [ ] `BogusOptions.AddAlias(string aliasName, BogusConvention target)`/
      `AddConvention(string memberName, Func<Faker, string> generate)`:
      eager validation performed by `AddAlias`/`AddConvention` against
      `BogusConventions.ByName` plus this instance's own private accumulator —
      `ArgumentNullException.ThrowIfNull` for a null name/`generate` (matching
      this repo's own established guard convention, `coding-standards.md`),
      `ArgumentException` for an empty/whitespace name or any duplicate or
      collision (naming the conflicting member name, the existing mapping,
      and the attempted mapping), `ArgumentOutOfRangeException` for an
      undefined `BogusConvention` value. Both return `void` — matching
      `Locale`/`EnableMemberNameConventions`'s plain-property-setter shape, no
      fluent chaining.
- [ ] `BogusMemberNameProvider` gains a second, `internal` constructor
      overload — `(string locale, IReadOnlyDictionary<string, Func<Faker, string>> conventions)`,
      called only by `CompositionBuilderExtensions.UseBogus(...)`, freezing
      its own copy internally (`conventions.ToFrozenDictionary()` unless
      already one). **The existing `public BogusMemberNameProvider(string locale)`
      (Phase 1, already merged via `#33`) is untouched** — not a breaking
      change, no `breaking` label needed. Deliberately `internal`, not
      `public`: a public overload would let a caller construct the provider
      with an arbitrary dictionary that omits or remaps a built-in name,
      silently supporting the replace/remove-a-built-in capability this
      ADR declares a Non-Goal and bypassing `AddAlias`/`AddConvention`'s own
      eager validation entirely.
- [ ] `CompositionBuilderExtensions.UseBogus(Action<BogusOptions> configure)`:
      after `configure(options)` returns, merges `BogusConventions.ByName`
      with `options`'s own validated accumulator into one
      `FrozenDictionary<string, Func<Faker, string>>` (no further validation
      needed — `AddAlias`/`AddConvention` already guaranteed no collisions),
      then constructs `BogusMemberNameProvider` from that snapshot. Still
      gated entirely by `EnableMemberNameConventions` — `false` means no
      provider is registered at all, aliases/custom conventions included
      (ADR-0028's explicit all-or-nothing scope; no partial mode in this
      version).
- [ ] Explicitly **not** in this phase (ADR-0028 Non-Goals): cross-call/
      cross-profile conflict detection or merging across separate
      `UseBogus(...)` calls; any `CompositionBuilder` core change; replacing
      or removing a built-in convention; non-`string` custom conventions;
      any fuzzy/pattern/priority matching.

### Phase 3: Test suites and verification

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
      `CompositionConfigurationException`; **ADR-0027 Amendment 1 regression
      coverage** — a `UseBogus<T>()` `configureFaker` callback that eagerly
      reads randomness at configuration time (`RuleFor(x => x.Id, faker.Random.Guid())`,
      not a lazy factory) still produces a deterministic result for the same
      Compono seed, proving `UseSeed(...)` is applied before `configureFaker`
      runs, not after.
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
- [ ] Configurable-convention coverage (ADR-0028): `AddAlias(...)` resolves to
      the same value a direct call to the aliased `BogusConvention`'s own
      built-in generator would produce, for the same seed/path;
      `AddConvention(...)` produces the custom callback's value, seeded via
      `context.DeriveSeed()` exactly like the built-in/alias path; an alias
      or custom name colliding with a built-in name, an existing alias, or an
      existing custom convention throws `ArgumentException` immediately from
      the `AddAlias`/`AddConvention` call that introduced it (not deferred to
      `UseBogus(...)` returning); a null name or a null `generate` throws
      `ArgumentNullException`, an empty/whitespace name throws
      `ArgumentException`, an undefined `BogusConvention` value throws
      `ArgumentOutOfRangeException`; `EnableMemberNameConventions = false`
      means aliases and custom conventions configured in the same call are
      never registered, not just the built-in conventions;
      **the documented cross-call limitation**
      — two separate `UseBogus(...)` calls each defining the same
      alias/custom name for different values compose via ordinary
      registration-order/first-match-wins pipeline semantics, asserted
      directly so the behavior is explicit rather than accidental (ADR-0028's
      Negative Consequences).
- [ ] An API-surface/approval test locking `Compono.Bogus`'s public shape
      (now including `BogusConvention` and `BogusOptions.AddAlias`/
      `AddConvention`), matching `Compono.NSubstitute.Tests`'/
      `Compono.XunitV3.Tests`' existing pattern.
- [ ] A real end-to-end run through `test/Compono.XunitV3.SampleTests` (or a new
      sibling sample) proving this plan's own Goal scenario — `UseBogus()` and
      `UseNSubstitute()` composing one graph under a real xUnit v3 theory,
      packaged (not `ProjectReference`) — matching PLAN-0004 Phase 3/PLAN-0005
      Phase 2's real-packaged-consumer strategy, which has twice caught real
      packaging/compile-time bugs a `ProjectReference`-only build couldn't
      surface.

### Phase 4: Docs and cleanup

**Status:** Not Started

- [ ] `docs/mvp.md` Milestone 6 section: links ADR-0026/ADR-0027/ADR-0028/PLAN-0006,
      states implementation status per phase, matching Milestone 5's own
      phase-by-phase doc-update pattern (update in the PR that actually ships
      each phase, not deferred wholesale to this final phase).
- [ ] `docs/architecture.md`: `ICompositionContext`'s conceptual sketch gains
      `DeriveSeed()`; stage 5's Resolution Pipeline row and the stages-4/5/6/7
      summary paragraph stop describing stage 5 as unconditionally empty;
      `Compono.Bogus` Package Boundaries entry gains a real `Owns` list
      (including `BogusConvention`/`BogusConventions`), Design line, and
      implementation status, matching `Compono.NSubstitute`'s entry shape; the
      Open Architectural Decisions "public provider extensibility" entry
      notes both stage 5 and stage 6 now have real registrants.
- [ ] `docs/public-api.md`: Bogus Integration section replaced with the real
      three-model design (convention provider, member-level `UseBogus(faker => ...)`,
      whole-object `UseBogus<T>(...)`) — the `context.Semantic.Email()` sketch
      and the `.DependsOn(...)` sketch both removed/reframed per ADR-0027 —
      plus ADR-0028's configurable-conventions sketch (`AddAlias`/
      `AddConvention`) and its documented cross-call limitation; Naming
      Vocabulary gains `BogusMemberNameProvider`/`BogusOptions`/
      `BogusConvention` if warranted; Diagnostics/Deterministic Reproduction
      sections cross-reference `DeriveSeed()`.
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
- `src/Compono.Bogus/BogusConvention.cs` (new), `BogusConventions.cs` (new,
  internal), `BogusOptions.cs`/`BogusMemberNameProvider.cs`/
  `CompositionBuilderExtensions.cs` (modified — `AddAlias`/`AddConvention`,
  the merged-conventions constructor parameter) — Phase 2.
- `test/Compono.Bogus.Tests/` (new project) — Phase 3.
- `test/Compono.XunitV3.SampleTests/` — new coexistence test(s) — Phase 3.
- `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md` — Phase 4.

## Test Plan

Matches `testing.md`'s existing conventions (xUnit v3 on MTP v2,
Arrange-Act-Assert, fixed-seed determinism assertions, one test project per
`src` project). Per `testing.md`'s "verify a new public entry point in
isolation before the package that will really use it exists" rule,
`DeriveSeed()` gets its own `Compono.Tests` coverage (Phase 0) independent of
`Compono.Bogus`, mirroring PLAN-0005 Phase 0's treatment of
`ICompositionValueProvider`. `Compono.Bogus.Tests` (Phase 3) then covers the
package's own real behavior — the base package (Phase 1) and configurable
conventions (Phase 2) together, in one coherent pass — its coexistence with
`Compono.NSubstitute` in the same `Composer`, `UseBogus<T>()`'s per-request
`Faker<T>` lifetime under concurrent composition, and ADR-0028's own eager
validation/collision contract, plus one real-runner proof (a packaged
`test/Compono.XunitV3.SampleTests` run) since that specific shape has twice
caught real bugs a `ProjectReference`-only build couldn't (PLAN-0004 Phase 3,
PLAN-0005 Phase 2).

## Open Items

- No `Compono.Benchmarks` entry for a Bogus-composed graph is planned as part of
  this plan's own exit criteria — worth adding once `Compono.Bogus` ships, to
  characterize `Faker`/`Faker<T>` generation cost against `docs/performance.md`'s
  existing baselines, but not required to call this milestone done.
- `.DependsOn(...)` (ADR-0027's deferred member-dependency mechanism) is not
  designed here. Revisit only if Milestone 7 dogfooding surfaces a real need
  `Faker<T>`'s whole-object correlation doesn't already cover.
- Cross-call/cross-profile alias/custom-convention conflict detection, and
  the generic `CompositionBuilder` build-finalization capability it would
  need, are not designed here (ADR-0028 Non-Goals). Revisit only if a second,
  real integration-configuration need (beyond this one) justifies the cost of
  a genuine core capability with at least two real consumers.

## Notes

**Design addition (2026-08-01):** [ADR-0028](../adr/0028-configurable-bogus-member-name-conventions.md)
(configurable member-name conventions — `BogusConvention`, `BogusOptions.AddAlias`/
`AddConvention`) accepted after its own design review, proposed and confirmed
after Phase 0 shipped and while Phase 1 was in review. A **new** ADR, not an
amendment to ADR-0027 — ADR-0027's own accepted Decision Outcome is unchanged.
Added as a new Phase 2, renumbering the original Phase 2 ("Test suites and
verification") to Phase 3 and Phase 3 ("Docs and cleanup") to Phase 4, so
implementation phases stay grouped before the single, comprehensive test-suite
phase. The design review's most consequential moment was a considered-and-reversed
decision: cross-call/cross-profile conflict detection was initially requested,
investigated in depth (it would require either a new generic `CompositionBuilder`
build-finalization capability in its own core-extension ADR, or a
`ConditionalWeakTable`-keyed accumulator with weaker first-use-not-`Build()`-time
validation timing), then explicitly declined once that cost was weighed against
a single, milestone-scoped need — see ADR-0028's own Considered
Options/Decision Outcome for the full account.

**Phase 0 (Done):**

- Implemented in the same branch/PR as the design docs (ADR-0026, ADR-0027,
  this plan), per explicit user direction — mirrors PLAN-0005 Phase 0's same
  choice. Phases 1-4 remain separate PRs, per `design-decisions.md`'s phase
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
  `InternalsVisibleTo` for the not-yet-created `Compono.Bogus.Tests` (now
  Phase 3, after ADR-0028's Phase 2 insertion — see the design-addition note
  above).
  `Directory.Packages.props` also gained a `Compono.Bogus` `Version="1.0.0"`
  local-feed entry, matching `Compono.XunitV3`/`Compono.NSubstitute`'s
  existing pattern, ahead of that phase's own packaged-consumer test needing it.
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
- No test project yet (now Phase 3, after renumbering) —
  `BogusOptions`/`BogusMemberNameProvider`/`UseBogus()`/`UseBogus<T>()`/the
  member-rule `UseBogus(...)` sugar are implemented but only build-verified
  in this phase, not test-verified — matching PLAN-0005 Phase 1's own
  explicit precedent for the identical package-skeleton-then-tests split.
- **PR #33 review (Codex, one P2 finding) caught a real determinism defect in
  `UseBogus<T>()`'s own implementation, fixed before merge — see
  [ADR-0027 Amendment 1](../adr/0027-compono-bogus-package-design.md#amendment-1-2026-08-01-useseed-must-run-before-configurefaker-not-after)
  for the full account.** `configureFaker(faker)` ran before
  `faker.UseSeed(context.DeriveSeed())`, so a `configureFaker` callback that
  eagerly reads randomness at configuration time (rather than through a lazy
  `RuleFor` factory) drew from Bogus's own default, unseeded `Randomizer`
  state instead of this request's deterministic seed. Fixed by constructing
  `Faker<T>` and applying `UseSeed(...)` in the same statement, before
  `configureFaker` runs. `BogusMemberNameProvider`/the member-rule
  `UseBogus(...)` sugar (Models 1/2) were already correct — both apply
  `Random` via an object initializer before calling into user code, so this
  defect was scoped to Model 3 only. Regression coverage added to this
  plan's own Phase 3 task list above (not written yet — Phase 1 stays
  build-verified only, per this phase's own scope).
- A second stale-doc finding (`docs/mvp.md`'s Milestone 6 Exit Criteria still
  said "implementation has not started" after this phase's own earlier fix
  already said `Compono.Bogus` was implemented) — same doc-staleness pattern
  PLAN-0005's review rounds caught repeatedly; fixed in the same PR.

Phase 4 (docs/cleanup) hasn't started yet.
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
