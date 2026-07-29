# [PLAN-0002] Milestone 2: Core Composition Engine

**Status:** In Progress

**Implements:** [ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)
(composition request/descriptor, synchronous provider pipeline, failure
semantics, diagnostics tracing),
[ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)
(scope, shared values, recursion detection, `CreateMany` semantics),
[ADR-0012](../adr/0012-composition-path-identity-and-deterministic-random-forking.md)
(path identity, deterministic random forking, `CreateMany` seed
derivation), [ADR-0013](../adr/0013-collection-generation-semantics.md)
(collection generation semantics),
[ADR-0014](../adr/0014-generator-emitted-collection-plans.md)
(reflection-free collection dispatch via generator-emitted plans)

These five ADRs supersede the plan's original ADR-0007/0008/0009 basis
after a deep design review of this plan's first draft — see each ADR's
Links section for the supersession chain. ADR-0014 was added mid-Phase-2
(PR #11 review) to hold the collection-dispatch redesign originally
recorded as ADR-0010's Amendment 3.

## Goal

`composer.Create<Customer>()` and `composer.CreateMany<Customer>(3)`
resolve a typical object graph (built-in primitives, enums, nullable
value types, common collections, nested composable types via generated
plans) through the real 9-stage resolution pipeline
(`docs/architecture.md`), deterministically from a seed, with shared
values reused correctly within one composition graph, recursive graphs
failing with a readable diagnostic that names the cycle's edges instead
of a stack overflow, and provider precedence covered by tests — per
`docs/mvp.md`'s Milestone 2 exit criteria.

## Scope

Per `docs/mvp.md`'s Milestone 2 section, mapped onto ADR-0010/0011/0012/
0013's concrete shape:

- The real `CompositionContext` (replacing Milestone 1's
  `PlaceholderCompositionContext`), the public
  `CompositionRequestDescriptor` generated code passes, and the internal
  `CompositionRequest` the context expands it into (ADR-0010).
- The hybrid pipeline model: context-owned deterministic stages
  (explicit values, shared/scoped values, exact registrations,
  generated-plan dispatch, diagnostic failure) plus extensible
  `ICompositionProvider` collection stages (profile rules, semantic
  providers, test-double providers, built-in providers) — only the
  built-in-providers stage has anything registered in Milestone 2
  (ADR-0010).
- `CompositionResult` (`NotHandled`/`Success` for ordinary providers;
  authoritative `Failure` reserved to context-owned stages).
- `CompositionPath` (structured `PathSegment` chain — constructor
  parameter/member name, collection index, dictionary key/value role) and
  a distinct active-construction-frame stack for recursion detection
  (ADR-0011, ADR-0012).
- `IRandomSource` — root `CompositionSeed`, FNV-1a-keyed structural
  forking over `PathSegment` data, a Compono-owned PRNG (not
  `System.Random`) (ADR-0012).
- Built-in value providers for every type `docs/mvp.md` lists (`string`,
  `bool`, integral/floating-point types, `decimal`, `Guid`, `DateTime`,
  `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, enums, nullable
  value types, arrays, `List<T>`, `IReadOnlyList<T>`, `HashSet<T>`,
  `Dictionary<TKey, TValue>`) — this list may shrink if implementation
  complexity threatens the milestone, per `docs/mvp.md`. Collection
  providers built against ADR-0013's semantics (default size 3, bounded
  duplicate-value retry for both `Dictionary<TKey, ...>` keys and
  `HashSet<T>` elements, no ordering guarantee for `HashSet<T>`/
  `Dictionary<TKey, TValue>`), dispatching element/key/value resolution
  through a cached generic-delegate bridge rather than per-value
  reflection (ADR-0010's amendment).
- `CompositionScope` (one per root composition operation, type-keyed
  sharing) and an internal, type-keyed exact-registration store the
  pipeline's stage 3 queries (mechanism only — no public builder API; see
  ADR-0011's scope note).
- The allocation-free-on-success diagnostics trace buffer (ADR-0010) and
  the structured diagnostic it materializes on failure.
- `Composer.Create<T>()` rewired onto the real `CompositionContext`
  (replacing the Milestone 1 placeholder), plus the new
  `Composer.CreateMany<T>(count)`.

Explicitly deferred (later milestones per `docs/mvp.md`):

- Any public configuration surface — `Composer.Create(builder => ...)`,
  `.Register()`, `.AddProfile()`, profiles, collection-size configuration
  — all Milestone 3. Milestone 2 exercises registrations/scope through an
  internal test seam only (ADR-0011).
- The `[Shared]` attribute and any xUnit-specific composition context —
  Milestone 4. Milestone 2 builds the scope mechanism `[Shared]` will
  attach to, not the attribute itself.
- NSubstitute/Bogus providers — Milestones 5/6. Their pipeline stages
  (4/5/6) exist and run as empty provider collections in Milestone 2.
- Name/qualifier-keyed sharing, semantic hints, custom-attribute-based
  request metadata, generic context, requested lifetime, "test double
  acceptable" — all deferred per ADR-0010/0011 until a later milestone
  has a concrete consumer.
- An async provider contract — explicitly out of scope per ADR-0010; add
  only if a real need appears, as a distinct opt-in contract.
- Promoting any internal engine type (`CompositionRequest`,
  `ICompositionProvider`, `CompositionResult`, `IRandomSource`) to
  `public` — deferred until the milestone that actually gives it a
  consumer (ADR-0010's Visibility decision).

## Execution Flow: `composer.Create<Customer>()`

Written out once, in full, so "does the root type go through the same
pipeline as nested types" and "how does a generated plan avoid
recursively redispatching itself" have one unambiguous answer instead of
being inferred per-phase. Assume:

```csharp
public sealed record Customer(string FirstName, string LastName, Address HomeAddress);
public sealed record Address(string Street, string City);
```

1. **Root request creation.** `Composer.Create<Customer>()` calls the
   **internal** `CompositionContext.ResolveRoot<Customer>()` — a distinct
   entry point from the public, descriptor-based `Resolve<T>()` generated
   code uses ([ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
   amendment). There is no root case on `CompositionRequestDescriptor`/
   `CompositionRequestKind` — a root request has no name and isn't
   generator-emitted, so it never needs the public member-request shape
   at all. `ResolveRoot<T>()` constructs the internal `CompositionRequest`
   directly (`RequestedType = typeof(Customer)`, root path node, no
   segment) and hands off to the same private pipeline-execution method
   the descriptor path uses.
2. **Scope and seed creation.** A fresh `CompositionScope` (empty, per
   ADR-0011 — one per root composition operation), a root
   `CompositionSeed` (explicit if one was configured — no builder exists
   yet in Milestone 2, so this is always the generated case for now — or
   generated once for this call), an empty root `CompositionPath` (just
   the `Customer` node, no segment), an empty active-construction-frame
   stack, and a fresh (empty) diagnostics trace buffer are all created
   together and held by one new `CompositionContext` instance scoped to
   this single `Create<T>()` call.
3. **Root resolution — same pipeline as any nested type.** `ResolveRoot<Customer>()`
   and a nested `Resolve<Address>()` call inside `Customer`'s own
   generated plan both funnel into the exact same private
   pipeline-execution method — the *entry point* differs (internal
   root call versus public descriptor expansion), but the pipeline they
   run is identical. There is no special-cased root path: stages 1–7 are
   tried in order (explicit value → shared/scoped → registration →
   profile → semantic → test-double → built-in), and for an ordinary
   domain type like `Customer` all seven return `NotHandled` (nothing
   claims a plain record type), falling through to stage 8.
4. **Generated-plan discovery.** Stage 8 reads `PlanCache<Customer>.Instance`
   (ADR-0004's direct field read). It's non-null (the generator produced
   a plan for `Customer`, reachable from this very `Create<Customer>()`
   call site). Before invoking it, the context checks the
   active-construction-frame stack for `typeof(Customer)` — empty, no
   cycle — pushes a `Customer` frame, and proceeds.
5. **Invocation of `ICompositionPlan<Customer>`.** `PlanCache<Customer>.Instance.Compose(context)`
   runs. For each constructor parameter, generated code calls
   `context.Resolve<TParam>(descriptor)` with a descriptor naming that
   parameter (`Kind = ConstructorParameter`, `Ordinal = 0`,
   `Name = "firstName"`, etc. — `Ordinal` is the identity the fork key
   and active-construction bookkeeping actually use; `Name` is carried
   for diagnostic display only, per
   [ADR-0012](../adr/0012-composition-path-identity-and-deterministic-random-forking.md)'s
   amendment) — never touching `CompositionPath` or the frame stack
   directly.
6. **Nested `Resolve<T>()` calls.** For `firstName`, the context expands
   the descriptor into an internal `CompositionRequest`, appends a
   `ConstructorParameter(Ordinal: 0, Name: "firstName")` segment to the
   path (now `Customer → firstName`, hashed on `Ordinal`), and runs the
   *same* 9-stage pipeline against it, fresh, as its own independent
   evaluation. On return (success or failure), the appended segment is
   popped in a `finally` — the path is back to just `Customer` before the
   next parameter is resolved.
7. **Provider resolution.** For `firstName` (`string`), stage 7's
   built-in string provider claims it (`Success`) — stages 1–6 all
   declined. No recursion check is involved at all for this request,
   because it never reaches stage 8 (strings have no generated plan).
   `lastName` resolves identically.
8. **Nested generated-plan dispatch.** For `homeAddress` (`Address`), the
   same expansion happens (path becomes `Customer → homeAddress`),
   stages 1–7 decline, stage 8 finds `PlanCache<Address>.Instance`,
   checks the frame stack (`Customer` is active, `Address` is not — no
   cycle), pushes an `Address` frame, and invokes
   `PlanCache<Address>.Instance.Compose(context)`. This is a genuinely
   independent, nested invocation of the *same* `Resolve<T>` machinery —
   not `Customer`'s plan calling itself. `Address`'s own constructor
   parameters (`street`, `city`) resolve the same way, with the path
   now `Customer → homeAddress → street` / `→ city`. When `Address`'s
   `Compose` returns, its frame is popped in `finally`.
9. **Path push/pop behavior, summarized.** Because `Resolve<T>` is an
   ordinary synchronous method calling itself recursively (through
   generated plans calling back into it), path segments and
   construction frames both push on entry and pop in `finally` on exit —
   entirely as a side effect of the call stack. Neither generated code
   nor any provider ever manipulates either structure directly; there is
   no way to "forget" a pop, because there's no manual bookkeeping to
   forget.
10. **Failure propagation.** If, say, `city`'s resolution fails (every
    stage returns `NotHandled`, reaching stage 9's diagnostic failure),
    that `Resolve<string>` call — which must return a `string`, not a
    wrapped result, since `ICompositionPlan<T>.Compose` returns a plain
    `T` — throws a `CompositionException` carrying the materialized
    diagnostic (path at time of failure: `Customer → homeAddress → city`;
    seed; the trace-buffer slice for this failed request). That
    exception propagates up through `Address`'s `Compose` (unwinding its
    `finally` blocks — the `Address` frame and the `homeAddress` segment
    both pop as the stack unwinds), through `Customer`'s `Compose`
    (`Customer`'s frame and the root pop too), and out of
    `Composer.Create<Customer>()` to the caller. The pipeline's internal
    `NotHandled`/`Success` return-value convention (ADR-0010) never
    itself throws — only the outward-facing `Resolve<T>`/`Create<T>()`
    boundary converts a terminal non-`Success` outcome into the thrown
    `CompositionException` `docs/public-api.md` already shows consumers
    catching.

`CreateMany<T>(count)` repeats steps 1–10 `count` times, each with its
own fresh scope/path/frame-stack/trace-buffer (ADR-0011's "fresh scope
per item"), differing only in step 2: each item's root `CompositionSeed`
is forked from the batch's root seed via `"CreateMany"` then the item's
index (ADR-0012), rather than being the batch's root seed directly.

## Phases

Ordered per the user's explicit preference during design review: seed,
path identity, and the random source come *before* built-in providers are
implemented, so providers are written against their real, final
randomness source from the start — never against ad hoc randomness that
a later phase has to rip out and rewire. Each phase either unblocks the
next or closes a real gap against the exit criteria. **Each phase ships
as its own PR** — don't bundle phases into one diff even if more than one
is finished by the time a PR gets opened.

### Phase 0 — Composition request, descriptor, and pipeline skeleton (Done)

Replaces Milestone 1's `PlaceholderCompositionContext` with the real
`CompositionContext`, wired to the hybrid pipeline model (context-owned
stages + extensible-provider collections). Stage 8 (generated-plan
dispatch via `PlanCache<T>`) is **already fully functional from Milestone
1** — this phase doesn't add it, it wires the *rest* of the pipeline
around it. Stages 1–3 (explicit values, shared/scoped, exact
registrations) are context-owned checks that are always `NotHandled` in
this phase (no scope or registration store exists until Phase 3); stages
4–6 (profile/semantic/test-double) are provider collections that are
always empty until Milestones 3/6/5 respectively; stage 7 (built-in
providers) is an empty collection until Phase 2. So in this phase, a
request resolves successfully only for a type with a generated plan
reachable from a `Create<T>()`/`CreateMany<T>()` call site (Milestone 1
behavior, now running through the real pipeline instead of the
placeholder) — everything else correctly reaches stage 9's diagnostic
failure until later phases populate the stages that would have handled
it.

- [x] `CompositionRequestDescriptor` (`public readonly struct` — plain,
      not a `record struct`; equality/`Deconstruct`/`ToString` aren't part
      of its contract, per
      [ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
      second amendment — with `Kind`, `Ordinal`, `Name`, `Nullability`;
      `Ordinal` is identity, `Name` is diagnostic-only) and
      `CompositionRequestKind` enum (`ConstructorParameter`,
      `RequiredMember` — no `Root` case; the root never uses this type,
      see `ResolveRoot<T>()` below)
- [x] `CompositionRequest` (`internal`: `RequestedType`, `Nullability`,
      `Path`, `IsShared` — expanded from a descriptor by the context)
- [x] `CompositionResult` (`internal`, `NotHandled`/`Success` only —
      no `Failure` case an ordinary provider can construct)
- [x] `ICompositionProvider` (`internal`, non-generic,
      `TryCompose(CompositionRequest, ICompositionContext)`)
- [x] `CompositionContext` implementing `ICompositionContext.Resolve<T>(in CompositionRequestDescriptor)`
      (descriptor expansion path, used by generated code) **and** an
      internal `ResolveRoot<T>()` (constructs the root `CompositionRequest`
      directly, bypassing the descriptor entirely —
      [ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
      amendment); both funnel into one private pipeline-execution method
      running stages 1–9 in order
- [x] Internal test-seam constructor/factory on `CompositionContext`
      accepting explicit per-stage provider collections for stages 4–7
      (ADR-0010's amendment), reachable from `Compono.Tests` via
      `InternalsVisibleTo` — this is how this phase's own pipeline-order
      tests inject fake `ICompositionProvider` test doubles into a
      specific stage without any public configuration surface existing
- [x] Rewire `Composer.Create<T>()` off `PlaceholderCompositionContext`
      onto the real `CompositionContext` (calling `ResolveRoot<T>()`)
- [x] Unit tests: pipeline tries stages in documented order (using the
      internal test-seam factory to inject fake providers at specific
      stages and assert call order); a stage returning `Success`
      short-circuits later stages; a type with a generated plan resolves
      successfully through the real pipeline (Milestone 1 behavior
      preserved); a type with no generated plan and nothing else able to
      handle it throws `CompositionException` with a structured
      diagnostic

**Notes on what actually happened, versus what was scoped:**

- `PathSegment` and `CompositionPath` (originally scoped to Phase 1) were
  pulled forward into this phase, minimally — `CompositionRequest.Path`
  needs a real type to compile, and push/pop needed to exist for
  `CompositionContext.Resolve<T>`'s recursive call structure to be
  correct from the start. What's implemented now is the structural chain
  only (an immutable, parent-pointing linked list — push/pop composes
  with the call stack, `Type`/`PathSegment` per node); the FNV-1a
  random-fork key derivation from that structure is still genuinely
  Phase 1 scope and hasn't been touched.
- `CompositionException` exists now as a minimal `Exception` subclass
  (constructor message only, naming the unresolvable type) — the full
  structured diagnostic (path display string, materialized provider
  trace, seed, remediation message) is still Phase 4 scope; this phase
  only needed *a* thrown exception at the pipeline's terminal stage, not
  the final diagnostic shape.
- The active-construction-frame stack and stages 1–3's real
  scope/registration checks are **not** implemented yet, exactly as
  scoped — every request in this phase either resolves via an extensible
  provider (stages 4–7, all empty except whatever a test injects) or
  stage 8's `PlanCache<T>` dispatch, or reaches stage 9's failure.
- Generator-integration testing gap discovered during implementation:
  `Compono.Tests` doesn't reference `Compono.Generators` as an analyzer
  (only `Compono.csproj` itself does — same as Milestone 1, per
  [PLAN-0001](0001-milestone-1-source-generation-foundation.md)'s Phase 5
  notes, which verified real generator dispatch via an external published
  package instead). Phase 0's tests exercise stage 8 dispatch by setting
  `PlanCache<T>.Instance` directly to a hand-written fake
  `ICompositionPlan<T>` — this is the correct unit-test-level boundary
  regardless (isolating `CompositionContext`'s dispatch logic from
  generator behavior, which `Compono.Generators.Tests` already covers
  separately), not a workaround for a gap.
- **Regression found and fixed mid-phase:** changing `ICompositionContext.Resolve<T>`'s
  public signature broke `Compono.Generators` — its Scriban template
  (`CompositionPlan.scriban`) still emitted the Milestone 1 call shape
  (`context.Resolve<T>(Nullability.NotNullable)`), so generated code no
  longer compiled and 46 `Compono.Generators.Tests` snapshot tests failed.
  Fixed in the same change, since a generator whose output doesn't
  compile against the runtime it targets isn't a deferred cleanup item:
  - `CompositionPlanEmitter`'s model now includes each constructor
    parameter's `Name` (previously dropped from the anonymous
    projection); the template emits
    `new global::Compono.CompositionRequestDescriptor(Kind, ordinal, "name", nullability)`
    for both constructor parameters and required members, using
    Scriban's `for.index` as the ordinal (parameters: the selected
    constructor's own parameter order, unchanged; required members: see
    next bullet).
  - `RequiredMemberCollector`'s ordering was **derived-type-before-base**
    (`EnumerateTypeAndBases` walked `type` before `type.BaseType`,
    keeping first-occurrence-wins for an override) — the opposite of
    ADR-0012's amendment 2 canonical algorithm (base-to-derived, base
    members get lower ordinals). Rewritten to walk base-to-derived,
    still deduping an override to its most-derived symbol (correct
    accessibility/type info) but assigning the ordinal from the name's
    *first* (base-most) appearance, with declaration order preserved
    within each type. Covered by a new test,
    `RequiredMembersOnBaseAndDerivedType_BaseOrdinalsPrecedeDerivedInDeclarationOrder`.
  - All 30+ existing `.verified.cs` generator snapshots were regenerated
    (content reviewed for correctness, e.g. the override-dedup case
    still emits its required member exactly once) — a normal
    Verify-workflow consequence of a template change, not scope creep.
  - Full suite green after the fix: 86 `Compono.Generators.Tests` (43 ×
    2 target frameworks), 20 `Compono.Tests`, `Compono.Benchmarks` builds
    clean.

### Phase 1 — Seed, path identity, and forkable random source (Done)

Built *before* any provider needs randomness, per the design review — no
provider in Phase 2 is ever written against temporary/ad hoc randomness.

**Reproducibility contract, stated once here since every task below
implements it:** a resolved value's random identity is derived
*exclusively* from its structural position in the composition graph — the
chain of `PathSegment` tags and `Ordinal`/`Index` values from the root —
and *never* from `CompositionRequest.RequestedType` or any other type
identity (`docs/adr/0012-...`'s second amendment). This is a guarantee to
test directly, not an incidental property of the implementation.

- [x] `PathSegment` hierarchy (`ConstructorParameter(int Ordinal, string Name)`,
      `RequiredMember(int Ordinal, string Name)`, `CollectionElement(int Index)`,
      `DictionaryKey(int Index)`, `DictionaryValue(int Index)`) and
      `CompositionPath` (an immutable, parent-pointing structural chain,
      wired into `CompositionContext.Resolve<T>`'s push-on-entry/
      pop-in-`finally` behavior) — **pulled forward into Phase 0**, since
      `CompositionRequest.Path` needed a real type to compile; only the
      structural chain exists so far
- [x] `IRandomSource` fork-key derivation actually consuming `PathSegment`'s
      `Ordinal`/`Index` (identity) via FNV-1a — the hashing this phase is
      actually about, per
      [ADR-0012](../adr/0012-composition-path-identity-and-deterministic-random-forking.md)'s
      amendment; `Name` stays diagnostic-only
- [x] Required-member ordinal assignment in `Compono.Generators`, per
      ADR-0012's second amendment's canonical algorithm — **already
      implemented in Phase 0** as an unscoped fix (see Phase 0's Notes:
      `RequiredMemberCollector` was rewritten base-to-derived while fixing
      the Scriban-template regression), confirmed against the canonical
      algorithm text and left unchanged this phase
- [x] `CompositionSeed` (root seed — explicit value or generated once per
      root composition operation)
- [x] `internal T Composer.CreateRootForTesting<T>(CompositionSeed seed)`
      and `internal IReadOnlyList<T> Composer.CreateManyForTesting<T>(int count, CompositionSeed seed)`
      ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)'s
      second amendment — replaces the earlier, ambiguous `CreateWithSeed`
      name) — the internal test seams this phase's own determinism tests
      (and Phase 4's `CreateMany` stability/end-to-end tests) use to
      exercise the real `Composer`/`CompositionContext` flow with an
      explicit seed for exactly one root operation or one batch, since the
      public `WithSeed(...)` builder doesn't exist until Milestone 3
- [x] `IRandomSource` (`internal`) with structural key-based forking:
      FNV-1a hash of each `PathSegment`'s tag + `Ordinal`/`Index` payload
      (never `Name`, never a formatted display string), combined with
      parent state to derive child state; type identity
      (`CompositionPathNode.RequestedType`) never feeds this hash
      (ADR-0012's amendment) — only ordinary, process-local `Type`
      equality is used elsewhere (provider dispatch, active-construction
      frames)
- [x] The Compono-owned PRNG value generator (not `System.Random`)
      backing `IRandomSource` — SplitMix64, used both to derive a node's
      value-stream state from its fork state and to advance that stream;
      see `RandomSource`'s remarks for why this is SplitMix64 alone rather
      than ADR-0009's "SplitMix64 feeding a xoshiro-family generator"
      phrasing (a deliberate scope simplification — Phase 2's built-in
      providers are the first real consumer of generated values, and
      swapping the generator later doesn't change `IRandomSource`'s
      internal contract)
- [x] A display-string derivation from `CompositionPath` (using each
      segment's `Name`), for diagnostics only — never consumed by the
      hashing path above
- [x] Unit tests (via `Composer.CreateRootForTesting`): same seed + same
      path produces identical output across two independent resolutions;
      two sibling requests at the same path depth with different
      `Ordinal`s (e.g. two same-typed constructor parameters) fork
      independently; renaming a constructor parameter/required member (no
      reordering) does *not* change its derived value, but reordering
      does; changing an unrelated member elsewhere in the graph doesn't
      change an already-resolved value's output
- [x] **Tag-collision test (ADR-0012's second amendment):** fork all five
      `PathSegment` kinds at `Ordinal`/`Index = 0` from the same parent
      state (`ConstructorParameter(0, "x")`, `RequiredMember(0, "x")`,
      `CollectionElement(0)`, `DictionaryKey(0)`, `DictionaryValue(0)`)
      and assert all five produce pairwise-distinct output — direct proof
      the per-kind tag byte actually discriminates kinds, not an inference
      from the design alone

**Notes on what actually happened, versus what was scoped:**

- `RandomSource` keeps two independent 64-bit states per node: a fixed
  "fork state" derived purely from the structural path (only ever used to
  derive children, never mutated) and a "value state" seeded from the fork
  state and advanced by `NextUInt64()`. This wasn't spelled out in the
  plan's task list but follows directly from the reproducibility contract:
  without the split, how many random values a node's own provider draws
  would perturb its children's derived state, which the contract
  explicitly forbids.
- `Fnv1a.Combine` folds the parent's state in as ordinary input bytes,
  always starting from FNV-1a's real offset basis, rather than using the
  parent's state as the initial hash accumulator directly. The first
  version did the latter and had a real bug caught in PR #10 review: state
  0, the `ConstructorParameter` tag (0), and an all-zero ordinal payload
  all mix to 0, so a seed of `0` followed by any number of
  `ConstructorParameter(0, ...)` forks collapsed every one of those
  distinct structural positions to the same derived state — a silent
  violation of ADR-0012's independent-forking guarantee. Fixed by folding
  the state in as data instead; regression test:
  `Fork_DoesNotCollapseToAFixedZeroState_FromAZeroSeedAtOrdinalZero`.
- `RandomSource.Fork` encodes each segment's `Ordinal`/`Index` as
  fixed big-endian bytes (`BinaryPrimitives.WriteInt32BigEndian`), not
  `BitConverter.GetBytes` — `BitConverter` uses host machine endianness,
  which would silently vary the fork key's byte sequence on a big-endian
  host and break "same seed, same output" across machines. Not called out
  explicitly in the plan, but a direct consequence of the reproducibility
  contract already stated there.
- `CompositionContext.ResolveCore`'s pre-existing null-`_path` check (used
  to detect "this is the root call") already had to double as the
  random-source root check too — a descriptor-based `Resolve<T>` called
  directly on a fresh context with no preceding `ResolveRoot<T>()` call
  (only reachable from `CompositionContextTests`' own unit test exercising
  the descriptor path in isolation, never from generated code) hits this
  case. Handled by keying both `_path`'s and `_random`'s root-vs-nested
  branch on the same `_path is null` check, rather than on `segment is
  null` for `_random` — otherwise that test throws a `NullReferenceException`.
- No changes to `CompositionRequest` this phase — Phase 2's real built-in
  providers are the first actual consumer of `IRandomSource`, so threading
  it through `CompositionRequest` (the natural place, alongside `Path`) is
  deferred to when Phase 2 needs it, per the "don't design for
  hypothetical future requirements" standard. Phase 1's own determinism
  tests reach the current node's random source instead via an internal
  `CompositionContext.Random` test-observability property, consumed by a
  capturing `ICompositionPlan<T>` test double
  (`CompositionRandomIntegrationTests`).

### Phase 2 — Built-in value providers and collections (Done, PR #11 merged)

Every provider here is written directly against Phase 1's real
`IRandomSource` — there is no ad hoc/temporary randomness at any point in
this plan.

**Corrected mid-phase, before implementation started, per
[ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
Amendment 3:** the originally-scoped reflection-based collection-dispatch
bridge (`MakeGenericMethod` + `CreateDelegate`) is **removed** — it
violated [ADR-0001](../adr/0001-source-generation-first.md)'s
no-reflection-by-default rule, caught before any code was written against
it. Collections are now built by `Compono.Generators`-emitted, strongly
typed collection plans (no runtime reflection anywhere in this phase), per
ADR-0014. The task list below reflects the corrected shape.

- [x] Primitive/simple-type providers (`string`, `bool`, integral types,
      floating-point types, `decimal`, `Guid`, `DateTime`,
      `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`) — ordinary
      `ICompositionProvider`s in stage 7's provider collection, unchanged
      from the plan's original shape
- [x] Enum provider (random valid enum member, via `IRandomSource`) —
      ordinary stage-7 provider, unchanged
- [x] Nullable value type provider (composes the underlying type when
      it's a primitive/enum/recognized BCL value type; nullable-generation
      *default* beyond that is a still-open `docs/mvp.md` item this phase
      doesn't resolve — see ADR-0013) — ordinary stage-7 provider,
      unchanged. `Nullable<T>` over any other (composable, custom-struct)
      `T` is not a stage-7 provider case at all — round 13 fixed
      `LeafTypeClassifier` to fall through to ordinary composable-type
      handling for that `T` instead of silently treating every
      `Nullable<T>` as resolved regardless of the underlying type; see the
      round 13 note below.
- [x] `CompositionRequestKind` gains `CollectionElement`/`DictionaryKey`/
      `DictionaryValue`; `CompositionContext.Resolve<TValue>`'s
      descriptor-to-segment switch extends to cover them (`Ordinal` maps
      to the segment's `Index`, `Name` unused) — ADR-0010 Amendment 3
- [x] `CollectionPlanCache<T>` (`public`, mirrors `PlanCache<T>` exactly —
      ADR-0004's zero-overhead closed-generic-field dispatch shape) and
      the `CompositionContext.ResolveCore<TValue>` direct field-read check
      for it, positioned at stage 7 immediately after the ordinary
      built-in provider collection declines and before stage 8's
      `PlanCache<TValue>` check
- [x] `UniqueValueResolver` (`public`, generic, reflection-free): the
      bounded duplicate-value retry helper (ADR-0013) generated
      `HashSet<T>`/`Dictionary<TKey, ...>` collection plans call once per
      element/key position; fork-derived deterministic retry indices
      (attempt 0 = the position unchanged; each retry attempt forks from a
      disjoint, deterministic index), bounded `MaxAttempts`, exhaustion
      reported by the generated plan throwing `CompositionException`
      naming the element/key type and requested count
- [x] `Compono.Generators`: `TransitiveClosureWalker` extended to
      recognize the five ADR-0013 shapes (`T[]`, `List<T>`,
      `IReadOnlyList<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`)
      wherever they appear in the walked graph, including nested inside
      another collection; a recognized shape is not walked as an ordinary
      composable type — its element/key type(s) feed back into the same
      eligibility walk instead
- [x] `Compono.Generators`: a new collection-plan emitter + template
      emitting one `file`-scoped `ICompositionPlan<TCollection>` per
      distinct closed collection type reached, each registering itself
      into `CollectionPlanCache<TCollection>.Instance` via a module
      initializer (same registration shape as an ordinary composition
      plan) — default size 3 (ADR-0013), each element/key/value its own
      `Resolve<T>()` call via a `CollectionElement`/`DictionaryKey`/
      `DictionaryValue` descriptor; `HashSet<T>`/`Dictionary<TKey, ...>`
      plans call `UniqueValueResolver.TryResolve<TValue>` per
      element/key; unsupported collection shapes are never classified as
      collections in the first place, so they fall through to ordinary
      composable-type handling (and, having no usable constructor, to
      that path's existing diagnostics) exactly like any other unhandled
      shape — no distinct "unsupported collection" error path, per
      ADR-0013's unchanged Decision Outcome; no ordering guarantee
      documented or tested for `HashSet<T>`/`Dictionary<TKey, TValue>`
- [x] Unit tests per provider type (`PrimitiveValueProviderTests`,
      `EnumValueProviderTests`, `NullableValueProviderTests`), plus a test
      confirming `CollectionPlanCache` dispatch only applies after the
      built-in provider collection and registration/profile/semantic/
      test-double stages have declined, and after it wins over stage 8's
      `PlanCache<TValue>` (`CollectionPlanCacheDispatchTests`); a
      duplicate-value retry/retry-exhaustion test at the
      `UniqueValueResolver` level, covering both a successful retry and
      full exhaustion (`UniqueValueResolverTests`)
- [x] A duplicate-value retry-exhaustion test through an actual generated
      `HashSet<T>`/`Dictionary<TKey, ...>` plan against a genuinely
      low-cardinality element/key type, and a collection-index
      **path-construction** test (e.g. `List<Address>` with 3 elements)
      asserting each element's independent output at the runtime level —
      closed via `GeneratorTestHelpers.CompileAndExecute` (compiles source
      plus real generated output to an in-memory assembly, loads it, and
      invokes it by reflection) and
      `GeneratedCollectionPlanExecutionTests`'s
      `HashSetRetryExhaustion_ThrowsCompositionException_ThroughARealDispatchedPlan`
      and `ListElements_EachGetsIndependentOutput_ThroughARealDispatchedPlan`;
      `UniqueValueResolverTests` covers the retry/exhaustion *algorithm*
      directly (with a stub context), and the tag-collision/
      structural-independence guarantee `CollectionElement(i)` relies on
      is already covered by Phase 1's `RandomSourceTests`/
      `CompositionRandomIntegrationTests` — this item added the remaining
      end-to-end runtime assertion through a real dispatched collection
      plan
- [x] `Compono.Generators.Tests`: snapshot coverage for at least one
      generated plan per collection shape (array, `List<T>`,
      `IReadOnlyList<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`), plus
      a nested-collection case (`List<List<int>>`) proving the walker
      recurses into a collection's element type correctly
      (`CollectionPlanVerifyTests`)

**Notes on what actually happened, versus what was scoped:**

- `Compono.Generators`'s discovery pipeline (`CreateInvocationDiscovery`,
  `ComposableAttributeDiscovery`, `ComposedTypeAnalyzer`,
  `TransitiveClosureWalker`) changed its return shape from
  `EquatableArray<DiscoveredTypeInfo>` to a new `TransitiveClosureResult`
  (`Types` + `Collections`) throughout, not just at the walker — every
  discovery entry point needed to carry collections alongside types for
  `ComponoIncrementalGenerator` to collect/dedupe/emit both. Not called
  out explicitly in the corrected task list above, but a direct
  consequence of "the walker discovers collections too."
- Collection-shape recognition (`CollectionWellKnownTypes`) is a distinct
  type from `WellKnownTypes`, not an extension of its enum table — that
  type's debug self-check (`AssertEnumAndTableInSync`) assumes a metadata
  name derivable from the enum member's own name via a simple
  underscore-to-dot transform, which doesn't produce the generic-arity
  backtick suffix (`` `1 ``/`` `2 ``) closed BCL generic types need.
  Adding generic-shape entries there would have required changing that
  self-check's transform; a small dedicated cache sidesteps it entirely.
- A real C# parser quirk, not caught until actually compiling generated
  output: `global::Some.Member` is **not** parsed as a qualified-alias
  member when it's the first token inside a string-interpolation hole
  (`$"...{global::Foo.Bar}..."` fails `CS0103`, "the name 'global' does
  not exist in the current context") — confirmed with a minimal repro
  outside this repo. Fixed by parenthesizing (`{(global::Foo.Bar)}`) in
  `CollectionPlan.scriban`'s two retry-exhaustion diagnostic messages,
  the only places a `global::`-qualified reference appears as the first
  token of an interpolation hole in generated code.
- `UniqueValueResolver`'s retry-index encoding (`RetryIndex`) uses a
  negative index for every retry attempt (`attempt >= 1`), disjoint by
  construction from any position's non-negative base index — simpler than
  an arithmetic scheme that could theoretically coincide with another
  position's own index, and avoids a false "this looks like position N's
  own value" reading in a fork-key trace.
- Collection dispatch is a hybrid within stage 7, not a third `Provider`
  collection: `CompositionContext.ResolveCore<TValue>` tries the ordinary
  `_builtInProviders` list first (primitives/enum/nullable, unchanged),
  then reads `CollectionPlanCache<TValue>.Instance` directly — the same
  reasoning ADR-0010 already used to keep stage 8's `PlanCache<T>` off
  the `ICompositionProvider` interface applies identically here.
- The same-closed-collection-type-discovered-twice-with-different-
  nullability case (the collection-shape analogue of `CMP0010`'s
  conflicting-composition-metadata check for ordinary types) was initially
  shipped as first-discovered-wins with no diagnostic, flagged during PR
  #11 review, and fixed in the same PR: `DiscoveredCollectionInfo` gained
  a `Diagnostics` field, and `ComponoIncrementalGenerator`'s collection
  dedupe now detects a genuine disagreement within a `FullyQualifiedCollectionTypeName`
  group and reports **CMP0011** (mirroring `CMP0010`'s synthetic-conflict-entry
  shape exactly) instead of picking one discovery arbitrarily — see
  `CollectionPlanVerifyTests.SameClosedListReachedWithDifferentElementNullability_ReportsConflictDiagnostic`.
- Two more PR #11 review findings, both fixed in the same PR: (1)
  `LeafTypeClassifier` never gained `DateOnly`/`TimeOnly` — those two
  types are new in Phase 2's built-in type list, but the generator's
  provider-resolved classification (Milestone 1 code) wasn't updated
  alongside `PrimitiveValueProvider`, so a composed type with a
  `DateOnly`/`TimeOnly` member failed constructor selection instead of
  reaching the new provider. Fixed by adding both to `WellKnownTypeData`/
  `LeafTypeClassifier.IsRecognizedBclValueType`. (2)
  `LeafTypeClassifier.IsBuiltInSimpleType` already classified
  `char`/`nint`/`nuint` as provider-resolved (Milestone 1), but
  `PrimitiveValueProvider`'s factory table never covered them, so a
  generated plan referencing any of the three compiled fine and then
  always failed at runtime with `CompositionException`. Fixed by adding
  factories for all three.
- A second round of PR #11 review caught two more real gaps, both fixed
  in the same PR: (1) **root-type discovery never applied
  `LeafTypeClassifier`/collection classification to the requested type
  itself** — only nested members got that check. `Composer.Create<Guid>()`,
  `Composer.Create<string>()`, and `Composer.Create<List<int>>()` all
  failed to *compile* (`CMP0001`, ambiguous constructor); `Composer.Create<int>()`/
  `Composer.Create<DayOfWeek>()` compiled but silently generated a dead
  `PlanCache<T>` entry that always produced `default(T)` (harmless only
  because stage 7's built-in provider always won first, but confusing
  generated output). Fixed by routing the root through the same
  classify-first logic as any member (`TransitiveClosureWalker.EnqueueRoot`),
  with one refinement beyond the review's own suggested fix: a root that's
  abstract or a delegate has no runtime provider either, so it must still
  reach constructor selection for its existing `CMP0003` diagnostic — a
  first pass that reused `LeafTypeClassifier.IsProviderResolved` verbatim
  for the root regressed three passing tests
  (`AbstractType_ReportsDiagnostic`, `DelegateType_ReportsDiagnostic`,
  `ComposableAttributeOnInterface_ReportsDiagnostic`) by silently skipping
  them instead. Fixed with a narrower `LeafTypeClassifier.IsRuntimeProviderResolved`
  (enums, built-in simple types, recognized BCL value types only) used
  solely for the root check. (2) `EnumValueProvider` called
  `Enum.GetValues(type)` — an allocating call — on every resolution;
  cached per enum type via a `ConcurrentDictionary<Type, Array>` instead.
- A third round of PR #11 review caught the same root-classification gap
  had one more hole: **array roots** (`Composer.Create<Address[]>()`)
  still failed to compile (`CMP0006`) — `ComposedTypeAnalyzer.Analyze`'s
  own `requestedType is not INamedTypeSymbol` check runs *before*
  `TransitiveClosureWalker.Walk` is ever called, rejecting an array root
  regardless of `EnqueueRoot`'s fix, since `IArrayTypeSymbol` is never an
  `INamedTypeSymbol`. Fixed by checking collection classification in
  `ComposedTypeAnalyzer.Analyze` itself, before the named-type check, and
  widening `TransitiveClosureWalker.Walk`/`EnqueueRoot`/`EnqueueMember`'s
  `parentType` parameter from `INamedTypeSymbol` to `ITypeSymbol`
  (defensively narrowed back at the one place — the composable-type
  fallback — that actually needs an `INamedTypeSymbol` to enqueue).
  `CompositionPlanVerifyTests.ArrayTypeArgument_ReportsDiagnostic` was
  renamed to `MultiDimensionalArrayTypeArgument_ReportsDiagnostic` and
  changed to a rank-2 array (`Customer[,]`, still genuinely unsupported —
  `CollectionWellKnownTypes` only classifies rank-1 arrays) since its
  original rank-1 array premise is now correct, supported behavior. Also
  fixed in the same round: `docs/architecture.md`'s stage-7 table row
  still described that stage as solely an ordered `ICompositionProvider`
  collection, silently stale against ADR-0010's third amendment (the
  `CollectionPlanCache<T>` hybrid dispatch) — updated to describe the
  actual current shape.
- A fourth round of PR #11 review found two more real gaps and one
  pre-existing, out-of-scope property: (1) **`EnumValueProvider` used
  `Enum.GetValues(Type)`** — the non-generic, `Type`-based overload,
  which is annotated `[RequiresDynamicCode]` (confirmed directly via
  reflection on the BCL method's attributes) and breaks under Native AOT
  — a real violation of ADR-0001's no-reflection-by-default rule, the
  same rule ADR-0010's third amendment retracted the reflection-based
  collection bridge over. Fixed by switching to
  `Enum.GetValuesAsUnderlyingType(Type)` + `Enum.ToObject(Type, object)`
  (neither carries the annotation, confirmed the same way) — with one
  subtlety caught before it shipped: a boxed *underlying-type* value
  (e.g. boxed `int`) unboxes correctly to a non-nullable enum type but
  throws `InvalidCastException` unboxing to `Nullable<TEnum>`
  specifically, so the fix must box via `Enum.ToObject` (boxed as the
  actual enum type), not hand back the underlying-type box directly —
  `NullableValueProviderTests.ComposeNullableEnum_ComposesTheUnderlyingType_NeverNull`
  (already existing) is the regression guard, and stayed green through
  the fix since it was never actually broken, only would have been by a
  more naive version of this fix. (2) **A pointer/function-pointer-element
  rank-1 array root or member** (`composer.Create<int*[]>()`) reached
  collection classification and a generated collection plan tried to
  emit `context.Resolve<int*>()` — a compiler error in generated code
  (pointer types can't be generic type arguments), confirmed directly.
  `List<T>`/`HashSet<T>`/`Dictionary<TKey, TValue>` can't have this
  problem (the C# compiler already rejects a pointer generic type
  argument before this code ever runs), so this was array-specific.
  Fixed in `CollectionWellKnownTypes.TryClassify`: a rank-1 array whose
  element type is `IPointerTypeSymbol`/`IFunctionPointerTypeSymbol` is
  left unclassified, falling through to the existing CMP0006 diagnostic
  path like any other unsupported shape. (3) The review's third finding
  — `CollectionPlanCache<T>`'s module initializer unconditionally
  overwriting `Instance` across multiple consuming assemblies in one
  process — was verified to be an unchanged property of `PlanCache<T>`
  itself (Milestone 1, ADR-0004), not a defect Milestone 2 introduced;
  `CollectionPlanCache<T>` deliberately mirrors `PlanCache<T>`'s exact
  registration shape. Deferred as a class-of-problem design question
  affecting both caches, not patched narrowly into just the new one —
  see `docs/architecture.md`'s Open Architectural Decisions, new
  "Cross-assembly plan-cache collision" entry.
- A fifth round of PR #11 review found one more real gap (fixed) and
  correctly flagged that this phase had never actually done the
  **required real manual verification** for source-generator-facing
  work (`AGENTS.md`'s Build and test section, `tasks/implement.md` step
  7) — every check so far had been generator snapshot tests plus direct
  `Compono.Tests`/`Compono.Generators.Tests` unit tests, never a real
  `dotnet pack` into a throwaway consuming project, unlike every phase
  of PLAN-0001 (Milestone 1). Doing that verification is what actually
  caught the real gap:
  - **`Nullable<T>` was never added to `LeafTypeClassifier`** — a real,
    previously-undiscovered bug no generator snapshot test had ever
    exercised (none used a nullable value type as a constructor
    parameter, and the existing nullable coverage was all
    `Composer.CreateRootForTesting<T>`-level runtime tests, which never
    go through the generator at all). Any nullable value type, root or
    member (`int?`, a nullable enum, etc.), reached
    `ConstructorSelector`, which sees `Nullable<T>`'s two accessible
    constructors (the parameterless one and `Nullable(T value)`) and
    reports `CMP0001` ambiguous construction — caught immediately by the
    real consuming-project verification (`Order`'s `Priority?`
    constructor parameter failed to compile) before anything else did.
    Fixed by adding a `Nullable<T>` case to both
    `LeafTypeClassifier.IsProviderResolved` and
    `IsRuntimeProviderResolved` (`named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T`),
    covering `Nullable<T>` **regardless of what `T` is** — a custom
    struct's `Nullable<T>` isn't runtime-satisfiable either, but
    `NullableValueProvider` already attempts and cleanly declines any
    unsupported `Nullable<T>` at runtime (reaching stage 9's diagnostic,
    correctly naming the request), so there's no compile-time
    distinction worth drawing between a supported and unsupported
    `Nullable<T>` here, the same reasoning already applied to
    `Enum`/built-in-simple/BCL-value-type roots. Added regression
    coverage: `CompositionPlanVerifyTests.NullableValueTypeRootType_GeneratesNoPlan`,
    `NullableValueTypeConstructorParameter_LeftAsResolveCallNotRecursedInto`.
  - Also fixed in the same round: `EnumValueProvider`'s per-enum-type
    cache used `ConcurrentDictionary<Type, Array>`, which strongly roots
    every enum `Type` ever composed for the process lifetime — a real
    concern for a long-lived host that loads/unloads consumer assemblies
    via a collectible `AssemblyLoadContext`. Swapped for
    `ConditionalWeakTable<Type, Array>`, whose keys don't root anything.
  - **Real manual verification, done for the first time this phase**:
    `dotnet pack`'d `Compono` into a local feed (clearing both the local
    feed and `~/.nuget/packages/compono` first, per PLAN-0001's Phase 2
    stale-cache lesson) and referenced it from a genuinely separate
    throwaway console project. Composed a representative nested graph
    (`Order` → `Address`, exercising every Phase 2 built-in: primitives,
    an enum, a nullable enum, `DateOnly`, and all five collection shapes
    including a nested `List<List<int>>`) plus four root-level
    `Create<T>()` calls (`int`, `Guid`, `List<int>`, `string[]`).
    Confirmed, in a real build/run rather than the test harness:
    `PlanCache<Order>`/`PlanCache<Address>` and every
    `CollectionPlanCache<T>` slot actually populated by generated module
    initializers; every collection produced exactly 3 elements (the
    ADR-0013 default size); the nullable enum was always set, never
    null; and every composed value was plausible (defined enum members,
    non-empty strings, in-range dates). All checks passed after the
    `Nullable<T>` fix above (the run before the fix failed to compile,
    confirming the bug was real and the fix's coverage). This satisfies
    `tasks/implement.md` step 7 for this phase, retroactively — should
    have been done alongside Phase 2's original implementation, not only
    once review flagged its absence.
- A sixth round of PR #11 review found one more real gap: **a
  private/protected collection element or key type**
  (`composer.Create<List<PrivateEnum>>()` called from inside
  `PrivateEnum`'s own containing type) compiled fine at the call site
  (private members are accessible within their own containing type) but
  failed inside the *generated collection plan* with `CS0122` — every
  generated collection plan is a top-level `file`-scoped type outside
  any containing type, so it can never reference a private/protected
  element/key type even when the original call site legitimately could.
  Confirmed directly before fixing. Fixed by checking
  `compilation.IsSymbolAccessibleWithin(elementType/keyType, compilation.Assembly)`
  in `TransitiveClosureWalker.ToDiscoveredCollectionInfo` — the same
  accessibility API `ConstructorSelector`/`RequiredMemberCollector`
  already use for constructor/required-member accessibility — reporting
  a new diagnostic, **CMP0012**, instead of letting an inaccessible type
  reach emission. `compilation`/`location` had to be threaded through
  `EnqueueRoot`/`EnqueueMember` to reach this check (previously only
  `Walk`'s top-level scope had them). Added regression coverage:
  `CollectionPlanVerifyTests.PrivateElementTypeInCollectionRoot_ReportsDiagnostic`.
  Scope note: the identical accessibility gap plausibly exists for an
  *ordinary* (non-collection) composable type's constructor-parameter
  type too (any generated composition plan is equally a top-level type),
  but constructing a legal, compiling repro for that case is
  significantly more constrained by C#'s own accessibility-consistency
  rules (CS0051/CS0053 already reject a public constructor exposing a
  less-accessible parameter type) — not fixed here since it wasn't what
  was flagged and a real repro wasn't confirmed; worth a follow-up look
  if it's ever hit in practice.
- A seventh round of PR #11 review found a real gap in round 6's own
  fix: when the **same** inaccessible collection type was discovered
  from **two different call sites**, each `DiscoveredCollectionInfo`
  carried the identical CMP0012 diagnostic but at a different
  `Location` — since `DiagnosticInfo.Equals` includes `Location`, the
  two entries counted as "distinct" and the collections merge step
  (added for CMP0011 in round 3) incorrectly synthesized a locationless
  CMP0011 "conflicting nullability" diagnostic instead, erasing both
  real, actionable CMP0012 failures. Confirmed directly before fixing
  (a two-call-site repro produced exactly the wrong CMP0011). This is
  the exact class of bug the *ordinary-type* merge already guarded
  against (`failures = distinct.Where(t => t.Diagnostics.Count > 0)`,
  present since round 3's original CMP0011 work modeled itself on it) —
  the guard just hadn't been ported to the newer collections merge
  branch when CMP0012 introduced the first real per-discovery
  collection failure. Fixed by adding the identical
  failures-preserved-before-conflict-detection branch to the
  collections merge in `ComponoIncrementalGenerator`. Added regression
  coverage: `CollectionPlanVerifyTests.SameInaccessibleCollectionFromTwoCallSites_ReportsCMP0012NotCMP0011`.
- An eighth round of PR #11 review found one real gap (fixed) and one
  legitimate process concern (also fixed, as a documentation
  reorganization): (1) **no test compiled, loaded, and actually
  executed a real generated collection plan** — every existing test
  either snapshotted/compiled generated source without running it
  (`CollectionPlanVerifyTests` et al.), or exercised the runtime
  pipeline against a hand-written test double
  (`UniqueValueResolverTests`, `CollectionPlanCacheDispatchTests` in
  `Compono.Tests`), so module-initializer registration, template
  output, and real fork-path integration regressions could all go
  undetected. This was already an explicitly-acknowledged open item in
  this plan (Phase 2's task list), not a newly-discovered gap, but
  review correctly pressed for it to actually close in this PR rather
  than stay deferred. Closed by adding
  `GeneratorTestHelpers.CompileAndExecute` (compiles source + real
  generated output to an in-memory assembly, loads it, and invokes a
  method via reflection) and a new
  `GeneratedCollectionPlanExecutionTests`: a `HashSet<bool>`
  retry-exhaustion test (bool's 2-value cardinality makes this
  deterministically fail regardless of randomness, exercising
  `UniqueValueResolver`'s exhaustion path through a real dispatched
  `HashSet<T>` plan) and a `List<Guid>` test asserting 3 genuinely
  distinct elements through a real dispatched `List<T>` plan. Both
  passed on the first run. (2) **ADR-0010's Amendment 3 (the
  collection-dispatch-bridge retraction) should have been its own ADR**,
  not appended in place to an already-`Accepted` ADR whose *other*
  decisions had already shipped in merged PRs (#9, #10) — the
  pre-implementation "amend in place" pattern ADR-0010's own Amendments
  1/2 used was correct for those (nothing in the whole ADR had been
  built yet), but no longer fit once real code existed against parts of
  the ADR, even though the specific sub-decision being corrected (the
  reflection bridge) had itself never been implemented either. Fixed by
  extracting the full redesign into
  [ADR-0014](../adr/0014-generator-emitted-collection-plans.md),
  reducing ADR-0010's Amendment 3 to a short pointer, and updating every
  cross-reference (ADR-0012, ADR-0013, `docs/architecture.md`, this
  plan's `Implements:` line, and the handful of source-comment
  references) that pointed at "ADR-0010's [third] amendment" to point at
  ADR-0014 instead — except this plan's own historical round-3/round-4
  narrative entries above, left as written since they're an accurate
  record of what was true *at that point in time*, not a live
  cross-reference.
- A ninth round of PR #11 review found one more real gap, worse than
  described: a `dynamic` collection element or key type
  (`composer.Create<HashSet<dynamic>>()`,
  `Dictionary<dynamic, int>`) doesn't just make the generated plan emit
  an illegal `typeof(dynamic)` (`CS1962`) — the generated plan class
  can't even **implement** `ICompositionPlan<HashSet<dynamic>>` at all
  (`CS1966`, "cannot implement a dynamic interface"), confirmed
  directly, so no template-body fix could have addressed this; the
  element/key type has to be rejected at classification time, before a
  collection plan is ever attempted. `dynamic` is a materially different
  case from round 4's pointer-element-array fix: a pointer can *only*
  ever appear as a rank-1 array element (never as a `List<T>`/
  `HashSet<T>`/`Dictionary<TKey, TValue>` type argument — the C#
  compiler already rejects `List<int*>` before Compono's code runs), but
  `dynamic` is legal as a type argument for **all five** shapes. Fixed
  in `CollectionWellKnownTypes.TryClassify`: refactored into a
  `FindShape` helper returning the shape candidate, plus one shared
  rejection check (`ElementType`/`KeyType.TypeKind` is
  `Pointer`/`FunctionPointer`/`Dynamic`) applied uniformly across all
  five shapes, rather than the array-only special case round 4 added.
  Rejected candidates fall through to the ordinary composable-type walk
  exactly like round 4's pointer-array fix — for `HashSet<dynamic>`/
  `Dictionary<dynamic, int>` specifically, that walk reaches `CMP0001`
  (ambiguous construction, since both types have multiple accessible
  constructors), not `CMP0006` — still a real Compono diagnostic
  instead of a compiler error inside generated code, which is what
  actually matters here. Added regression coverage:
  `CollectionPlanVerifyTests.DynamicHashSetElement_ReportsDiagnostic_NotACompilerErrorInGeneratedCode`,
  `DynamicDictionaryKey_ReportsDiagnostic_NotACompilerErrorInGeneratedCode`.
- A tenth round of PR #11 review found two items, both resolved without a
  behavior-changing code fix. First, a checklist/status mismatch: this
  plan's own "generated-plan execution coverage" item (above) was still
  unchecked even though round 8's `GeneratedCollectionPlanExecutionTests`
  had already closed it — checked off, with a pointer to the tests that
  closed it. Second, a genuine but narrower-than-first-glance leak
  concern in `CollectionPlanCache<T>`: when a collection's type arguments
  are entirely BCL types (`List<int>`, `Dictionary<Guid, string>`), the
  CLR homes that closed generic instantiation in the non-collectible
  default context regardless of which assembly's module initializer
  populates it, so a consuming assembly loaded into a collectible
  `AssemblyLoadContext` gets permanently rooted by its own plan instance
  - the same leak class the `EnumValueProvider` cache fix (round 5)
  closed elsewhere, but not fixable the same way here, since
  `CollectionPlanCache<T>.Instance` being a plain closed-generic static
  field (rather than a `Type`-keyed lookup) is exactly what makes stage 7
  dispatch a direct field read per ADR-0004 - any fix able to key off the
  consuming assembly/ALC instead of `T` would reintroduce a per-resolve
  lookup on every collection composition, undoing that. Verified the CLR
  loader-context claim directly against how `PlanCache<T>` avoids the
  same problem for ordinary composable types (a collectible type's own
  closed instantiation stays tied to its own collectible context, so no
  external root survives unload) before accepting the finding as real.
  Deferred alongside the pre-existing `PlanCache<T>`/`CollectionPlanCache<T>`
  cross-assembly-collision item (round 4) with the same reasoning:
  neither `docs/mvp.md`'s scope nor Compono's primary xUnit-test-runner
  consumer exercises collectible-ALC hosting today. Documented in
  `docs/architecture.md`'s Open Architectural Decisions and in
  `CollectionPlanCache<T>`'s own XML doc comment (which also had a stale
  "ADR-0010's third amendment" reference from before the round-8
  ADR-0014 extraction, fixed in the same pass).
- An eleventh round of PR #11 review found one more real gap in round
  9's `dynamic`-rejection fix: `CollectionWellKnownTypes.TryClassify`
  only checked the *immediate* element/key type's `TypeKind`, so
  `List<dynamic[]>` slipped through - `dynamic[]`'s own `TypeKind` is
  `Array`, not `Dynamic`, even though its element is `dynamic`.
  Confirmed directly before fixing: this reached the emitter and failed
  with `CS1966` (`cannot implement a dynamic interface`) on the
  **outer** `ICompositionPlan<List<dynamic[]>>` interface itself, not
  just on a `Resolve<dynamic[]>()` call inside the plan body -
  `dynamic` nested anywhere in an interface's constructed generic
  arguments triggers `CS1966`, not just at the top level. Fixed by
  replacing the shallow `TypeKind` check with a recursive
  `ContainsUnsupportedTypeKind` helper that looks through both array
  element types and generic type arguments for
  `Dynamic`/`Pointer`/`FunctionPointer` at any depth (a pointer array
  nested one level down, e.g. `List<int*[]>`, is equally legal C# and
  has the same problem class as the array case, so the fix covers both
  in one pass rather than patching `dynamic` alone). Rejected
  candidates fall through to the same `CMP0001` path round 9's direct
  `HashSet<dynamic>`/`Dictionary<dynamic, int>` fix already established.
  Added regression coverage:
  `CollectionPlanVerifyTests.DynamicArrayNestedInsideListElement_ReportsDiagnostic_NotACompilerErrorInGeneratedCode`
  (the array-recursion case that reproduces the actual finding) and
  `DynamicNestedInsideDictionaryValueTypeArgument_ReportsDiagnostic_NotACompilerErrorInGeneratedCode`
  (the generic-type-argument-recursion case, `Dictionary<int,
  List<dynamic>>`, proving the fix isn't array-specific).
- A twelfth round of PR #11 review found one real issue and one
  status question resolved as no-action. The real issue:
  `GeneratedCollectionPlanExecutionTests.ListElements_EachGetsIndependentOutput_ThroughARealDispatchedPlan`
  called `Composer.Create()` (a fresh, non-deterministic seed every
  run), so a failure couldn't be reproduced from a bug report, and the
  `OnlyHaveUniqueItems()` assertion carried a (astronomically small but
  nonzero) theoretical dependence on GUID-space collision. Fixed by
  switching the asserted call to `Composer.CreateRootForTesting<T>`
  with a fixed seed, the same seam `CompositionRandomIntegrationTests`
  already uses in `Compono.Tests` - which required granting
  `Compono`'s `InternalsVisibleTo` to `"TestsAssembly"`, the fixed name
  `GeneratorTestHelpers.CompileAndExecute` gives every in-memory
  compiled test assembly, alongside the existing `Compono.Tests` grant.
  Since the generator only discovers a type from a `Composer.Create<T>()`
  call-site pattern (`CreateInvocationDiscovery`), not from
  `CreateRootForTesting`, the test source keeps a discarded
  `composer.Create<List<Guid>>()` call purely to trigger plan
  generation/registration, then asserts against
  `CreateRootForTesting`'s fixed-seed result - both dispatch through the
  exact same generated, `CollectionPlanCache`-registered plan instance,
  so this is still real end-to-end dispatch, not a shortcut around it.
  The status question: whether Phase 2's header should already read
  `Done` now that every checklist item is checked - resolved as
  no-action, consistent with round 10's answer to the same question and
  with this plan's own established convention: Phase 0 and Phase 1 both
  stayed at their working status until their PRs (#10 and its
  predecessor) actually merged, only becoming `Done` at that point, per
  `AGENTS.md`'s "the prior phase's status/PR-merge state should be
  current before the next phase starts." PR #11 is still open, so
  Phase 2 stays `In Progress` until it merges - `Done` now would
  contradict that established pattern, not fix a real inconsistency.
- A thirteenth round of PR #11 review found three real issues, the
  largest of this whole review cycle. **Nullable custom struct silently
  never composed at runtime (P1).** `LeafTypeClassifier` treated every
  `Nullable<T>` as a provider-resolved leaf regardless of `T`, but
  `NullableValueProvider` only ever composes a primitive/enum/recognized
  BCL underlying type - a custom struct's `Nullable<T>` (e.g. `Money?`)
  compiled with zero diagnostics and then always threw at runtime
  (confirmed directly: "no registration, provider, or generated plan
  could satisfy the request"), for both a root `Composer.Create<Money?>()`
  and a constructor-parameter member. Fixed by making `IsNullableValueType`
  check the underlying type: only primitive/enum/recognized-BCL
  `Nullable<T>` stays a leaf; any other `T` now falls through to ordinary
  composable-type handling, exactly like any other concrete type. This
  does **not** add a new nullable-composition mechanism - `Nullable<T>`
  has two accessible constructors to Roslyn's symbol model (the implicit
  parameterless one and `Nullable(T value)`), so the *existing*
  `ConstructorSelector` ambiguity check reports the same `CMP0001`
  ambiguous-construction diagnostic any other multi-constructor type
  gets, converting the silent runtime failure into a real compile-time
  one for free, without new diagnostic code or a parallel dispatch
  architecture (rejected as disproportionate to this finding, and as
  "building ahead of the milestone" per `AGENTS.md`, given `docs/mvp.md`'s
  "Nullable value types" Initial Built-in Type sits directly alongside
  the primitive/enum list, not as its own arbitrary-custom-struct
  feature). Added regression coverage:
  `CompositionPlanVerifyTests.NullableCustomStructRootType_ReportsDiagnostic_NotASilentRuntimeFailure`
  and `NullableCustomStructConstructorParameter_ReportsDiagnostic_NotASilentRuntimeFailure`
  (both `CMP0001`, root and member); confirmed no regression for
  `Nullable<int>`/`Nullable<Priority>` (the existing
  `NullableValueTypeRootType_GeneratesNoPlan`/
  `NullableValueTypeConstructorParameter_LeftAsResolveCallNotRecursedInto`
  tests still pass unchanged). **Accepted-ADR immutability violated by
  this PR's own amendments (P1).** ADR-0010's "Amendment 3," ADR-0012's
  "Amendment 3," and ADR-0013's "Amendment 2" - all three added earlier
  in this PR (round 8/the original ADR-0014 extraction), not inherited
  from an earlier merged PR - edited already-`Accepted` ADRs' content in
  place, which `design-decisions.md` explicitly prohibits ("don't edit
  [an accepted ADR's] Decision/Rationale/Consequences... write a new
  ADR"), regardless of ADR-0010/0012/0013's own earlier, pre-existing
  Amendments (1/2, predating this PR) having already established that
  same drift from the documented rule. Fixed by removing all three
  sections' prose entirely and replacing each with a `Links`-section
  entry pointing to ADR-0014 instead (metadata/cross-reference, not
  decision content) - ADR-0014's own Context and Negative Consequences
  wording updated to match (it previously described these as "reduced to
  a pointer" rather than removed). ADR-0010/0012/0013's pre-existing
  Amendments (1/2) are untouched, since they predate this PR and are a
  separate, pre-existing concern out of this PR's scope. **Spoofable
  `InternalsVisibleTo` grant (P2, found against round 12's own fix).**
  Round 12's `InternalsVisibleTo Include="TestsAssembly"` grant
  authenticates only a simple assembly name (unsigned) - any real
  consumer could name their own assembly `TestsAssembly` and gain the
  identical internal access to the shipped `Compono` package. Fixed by
  reverting that grant entirely and switching
  `GeneratedCollectionPlanExecutionTests`'s fixed-seed test to reach
  `Composer.CreateRootForTesting`/`CompositionSeed` via reflection from
  within its compiled test source instead - already how
  `GeneratorTestHelpers.CompileAndExecute` invokes every test's entry
  point, so this adds no new public/friend surface to the shipped
  package at all.
- A fourteenth round of PR #11 review found two real issues, both
  small. **A rejected array-shaped member silently produced no
  diagnostic (P2).** A root `dynamic[]`/pointer-element array already
  reported `CMP0006` correctly via `ComposedTypeAnalyzer` (confirmed
  directly: `composer.Create<dynamic[]>()` does report it), but the
  equivalent *member* case didn't: `TransitiveClosureWalker.EnqueueMember`
  correctly declines to classify a rejected array as a collection (same
  reason as always - building `ICompositionPlan<dynamic[]>` would hit
  `CS1966`), but its fallback for a non-`INamedTypeSymbol` member
  (`if (memberType is not INamedTypeSymbol) return;`) silently dropped it
  instead of reporting anything - unlike a rejected *generic* collection
  shape (`HashSet<dynamic>` as a member), which is still an
  `INamedTypeSymbol` and so still reaches `ConstructorSelector`'s own
  `CMP0001` via the ordinary ("ambiguous constructor") path. Confirmed
  directly before fixing: a `dynamic[]` constructor parameter compiled
  with zero diagnostics, emitting `context.Resolve<dynamic[]>()` for a
  type nothing would ever register a plan for. Fixed by threading the
  walker's `results` list into `EnqueueMember` (and `EnqueueRoot`, for
  the recursive element/key calls it makes) and reporting the same
  `CMP0006` `ComposedTypeAnalyzer` already uses for the root case,
  reusing its `TypeArgumentFailure` helper rather than duplicating the
  diagnostic-construction logic. Added regression coverage:
  `CollectionPlanVerifyTests.DynamicArrayConstructorParameter_ReportsDiagnostic_NotACompilerErrorInGeneratedCode`.
  **`docs/architecture.md`'s `CompositionRequestKind` contract was stale
  (P2).** Its `CompositionRequestDescriptor` code sample still commented
  `kind` as `ConstructorParameter | RequiredMember` only, missing the
  three collection-plan cases (`CollectionElement`/`DictionaryKey`/
  `DictionaryValue`) ADR-0014 added, and its public-vs-internal-visibility
  discussion didn't mention `CollectionPlanCache<T>`/`UniqueValueResolver`
  joining `CompositionRequestDescriptor`/`ICompositionContext` as public
  generated-code call surface. Fixed by updating both spots. Also
  self-caught (not a review finding) while replying to this thread: seven
  source-code doc comments across both projects still cited "ADR-0010's
  third amendment" - the section round 13 had just removed entirely -
  repointed all seven to ADR-0014 directly.
- A fifteenth round of PR #11 review found one real issue: a regression
  introduced by round 14's own `CMP0006`-for-rejected-array-members fix,
  not a pre-existing gap. Reporting `CMP0006` for *every*
  non-`INamedTypeSymbol` member unconditionally also caught plain
  `dynamic` (not `dynamic[]`) - `dynamic` is likewise not an
  `INamedTypeSymbol`, but unlike an array/pointer/function-pointer it
  isn't a permanently-unsatisfiable shape: `Resolve<dynamic>()` is legal
  and is the established, intentional way to leave a member for a future
  registration/semantic provider (dynamic erases to `object`, so
  virtually anything could satisfy it). Confirmed directly before fixing:
  a plain `dynamic` constructor parameter, previously compiling clean
  with `context.Resolve<dynamic>()` left for a provider, started
  reporting `CMP0006` after round 14's fix - a real regression, not a
  restatement of round 14's finding. Fixed by narrowing the diagnostic to
  only `IArrayTypeSymbol`/`IPointerTypeSymbol`/`IFunctionPointerTypeSymbol`
  specifically, leaving `dynamic` (and anything else non-named) to fall
  through silently exactly as it did before round 14. Added regression
  coverage:
  `CollectionPlanVerifyTests.PlainDynamicConstructorParameter_GeneratesNoDiagnostic_StaysProviderResolved`,
  alongside the existing
  `DynamicArrayConstructorParameter_ReportsDiagnostic_NotACompilerErrorInGeneratedCode`
  proving the array case still correctly reports `CMP0006`.

### Phase 3 — Scope, shared values, exact registrations, and recursion detection (Done, PR #12 merged)

- [x] `CompositionScope` (type-keyed, one per root composition operation)
- [x] Wire `IsShared` requests through the scope-check pipeline stage
      (stage 2)
- [x] Internal test-only exact-registration store satisfying pipeline
      stage 3, exercised via `InternalsVisibleTo` from `Compono.Tests`
      (no public API yet, per ADR-0011's scope note)
- [x] Authoritative null/type validation for stages 2 and 3
      ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)'s
      second amendment): a shared/scoped or registration-produced value
      that is `null` for a non-nullable request, or whose runtime type
      isn't assignable to `CompositionRequest.RequestedType`, is an
      authoritative `Failure` at that stage — never passed through as
      `NotHandled` to a later stage
- [x] Active-construction-frame stack (`internal`, distinct from
      `CompositionPath`): pushed only immediately before stage 8 invokes
      a generated plan, popped in `finally`; a request for a type whose
      frame is already active is an authoritative recursion `Failure`
      carrying the chain of active frames
- [x] Unit tests: a shared request resolves once and reuses the value for
      a second shared request of the same type in the same scope; a
      non-shared request never reads from scope even if the same type was
      already shared; a registered/shared value legitimately terminates a
      self-referencing type without tripping recursion detection at all; a
      `null` shared/registered value against a non-nullable request, and a
      type-mismatched registered value, both fail authoritatively at their
      own stage rather than falling through; an actual construction cycle
      using `public sealed record Node(List<Node> Children);` (no
      terminating registration/shared value anywhere in the loop) fails
      with a diagnostic whose cycle-edge chain explicitly includes the
      collection-index segment (`Node → Children[0] → Node`, not just
      `Node → Node`) — moved here from Phase 2 since the
      active-construction-frame stack this test depends on doesn't exist
      until now

**Notes on what actually happened, versus what was scoped:**

- No public entry point can mark a request `IsShared: true` yet (the
  `[Shared]` attribute is Milestone 4) and `CompositionRequestDescriptor`
  deliberately wasn't widened to carry that flag early — so stage 2 is
  exercised through a new internal test seam,
  `CompositionContext.ResolveSharedForTesting<TValue>(ordinal, name)`,
  mirroring `Composer.CreateRootForTesting`'s existing "explicitly-named
  test seam per unimplemented public surface" pattern rather than
  overloading `Resolve<TValue>`.
- The exact-registration store is a small dedicated type,
  `CompositionRegistrations` (wrapping a type-keyed
  `IReadOnlyDictionary<Type, object?>`), rather than a bare dictionary
  field on `CompositionContext` — kept symmetrical with `CompositionScope`
  as its own named concept per the ADR's wording, and gives stage 3 a
  single `TryGet` call site.
- `CompositionResult` gained the `Failure` case ADR-0010 always reserved
  for context-owned authoritative stages but that no code had constructed
  yet — used by stages 2/3's null/type validation and stage 8's recursion
  check. A new private `CompositionContext.Authoritative<TValue>` helper
  converts a `Failure` into the same thrown `CompositionException`
  stage 9's terminal case already uses, keeping exactly one
  result-to-exception conversion point.
- The recursion diagnostic reuses `CompositionPath.ToDisplayString()`
  (already tracking every request edge, including collection-index
  segments) rather than building a separate message from the
  active-construction-frame stack directly — the frame stack only needed
  to answer "is this type already under construction," not carry its own
  display representation.
- A PR #12 review round (Codex) found one real gap: a shared value's
  *first* population (from a provider, a collection plan, or a generated
  plan) skipped `ValidateAuthoritativeValue` entirely — a null-for-
  non-nullable or type-mismatched value got cached into `_scope`
  unvalidated, so the initial request threw `InvalidCastException`/
  `NullReferenceException` instead of the documented
  `CompositionException`, and a later shared read of the same type hit
  the already-poisoned cache. Fixed by routing both shared-store paths
  (`StoreSharedAndReturn`, and `ResolveViaGeneratedPlan`'s shared branch)
  through the same validate-then-cache sequence stages 2/3 already use,
  scoped to `IsShared` requests only (an ordinary, non-shared provider's
  output is still validated only by its own contract, not the context —
  widening validation to every provider result wasn't part of this
  finding). Added regression coverage in `CompositionScopeTests`: the
  null case, the type-mismatch case, the same through a generated plan,
  and confirming an invalid first value doesn't poison the scope for a
  subsequent shared request.

### Phase 4 — `CreateMany<T>()` and diagnostics polish (In Progress)

- [x] `Composer.CreateMany<T>(int count)` — `count` independent root
      composition operations (Execution Flow section, above), each
      item's root seed forked from the batch root via `"CreateMany"` +
      index (ADR-0012) — no cross-item scope reuse. Contract, per
      [ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)'s
      second amendment: `count < 0` throws via
      `ArgumentOutOfRangeException.ThrowIfNegative(count)`; `count == 0`
      returns an empty, materialized, non-null `IReadOnlyList<T>`; return
      type is `IReadOnlyList<T>` (not `IEnumerable<T>` — the batch is
      always fully, eagerly materialized, never deferred)
- [x] The allocation-free-on-success trace buffer (ADR-0010): checkpoint
      on `Resolve<T>`/`ResolveRoot<T>` entry, append a compact
      `ProviderAttempt` per stage/provider tried, rewind on success,
      materialize into the durable diagnostic on failure before the
      buffer unwinds further. Per ADR-0010's amendment: because a
      sibling's checkpoint-rewind happens on its own success (before the
      next sibling is even attempted), the materialized trace on failure
      naturally contains only the active failing branch's own attempts —
      never an already-succeeded, already-rewound sibling's
- [x] Structured diagnostic type surfaces root request, full path (as a
      display string derived from `CompositionPath`), the materialized
      trace, seed, and a human-readable remediation-oriented message,
      matching `docs/architecture.md`'s example format
- [x] Unit test asserting the trace-retention property directly: a type
      with two successfully-resolved constructor parameters followed by a
      third that fails produces a diagnostic trace containing only the
      failing parameter's attempts (and its ancestors' own attempts) —
      not the two already-succeeded siblings'
- [x] Benchmark (extending `Compono.Benchmarks` from Milestone 1, widened
      mid-plan per PR #11 review — see this section's note below): confirm
      the trace buffer is actually allocation-free on the success path;
      if it measurably harms the hot path, fall back to shallow
      diagnostics by default with full tracing behind an explicit
      diagnostic-mode opt-in (ADR-0010's stated fallback). Milestone 1's
      `ArchitectureBenchmarks`/`EcosystemBenchmarks` only cover generated
      *construction* dispatch versus reflection — nothing in
      `Compono.Benchmarks` yet exercises the resolution pipeline itself
      (provider dispatch, random forking, collection generation), and
      Phase 4 is the first point a representative end-to-end graph
      (nested composable type + every Phase 2 built-in + a collection)
      actually exists to benchmark. Add that end-to-end coverage here
      rather than deferring it further:
      - `Create<T>()` and `CreateMany<T>(count)` throughput for a
        representative graph (the Execution Flow section's
        `Customer`/`Address` shape, extended with a collection member),
        alongside the existing reflection/generated-construction baseline
        — establishes Compono's actual per-call cost, not just its
        construction-dispatch cost
      - Allocations for that same graph, success path only — the trace
        buffer's allocation-free claim (above) is one contributor to this
        number, not the whole story once collections/providers are in the
        mix
      - `CreateMany<T>(count)` scaling behavior across a couple of `count`
        values, to catch anything unexpectedly super-linear (fork-key
        derivation, scope allocation) before it ships
- [x] Exit-criteria pass: representative object graph (nested composable
      type + built-ins + a collection) composes deterministically end to
      end via `Create<T>()` and `CreateMany<T>()`, matching the Execution
      Flow section above exactly
- [x] `CreateMany` stability test: items 0–2 of `CreateMany<T>(3)` and
      `CreateMany<T>(10)` (same explicit root seed) are byte-for-byte
      identical
- [x] `CreateMany` argument/return contract tests: `count < 0` throws
      `ArgumentOutOfRangeException`; `count == 0` returns an empty,
      non-null `IReadOnlyList<T>`; a standalone `Create<T>()` (via
      `CreateRootForTesting`) called twice with the same explicit seed
      produces identical output, confirming no hidden per-call state
      beyond the seed itself

**Notes on what actually happened, versus what was scoped:**

- `ProviderAttempt` ended up `(PipelineStage Stage, CompositionAttemptOutcome
  Outcome)` — no distinct "provider id" field, despite ADR-0010's own text
  describing one. A PR #13 review (Codex) flagged this as contradicting
  the accepted ADR; formalized as its own record rather than an in-place
  ADR-0010 edit (this repo's own established "extract a sub-decision into
  a new ADR" pattern, per ADR-0014's precedent) —
  [ADR-0015](../adr/0015-provider-identity-deferred-in-provider-attempt.md).
- `PipelineStage` and `CompositionAttemptOutcome` are `public` (not
  `internal`), and so, transitively, is `ProviderAttempt` — a public
  `CompositionDiagnostic.Trace` can't expose an internal element type.
  This is a deliberate, narrow widening of the public surface
  (`coding-standards.md`'s "as narrow as the actual callers require"),
  justified by `docs/public-api.md`'s own pre-existing Diagnostics API
  section already showing `exception.Diagnostic` as consumer-facing.
- `CompositionException`'s existing `Message` (the short, single-line
  reason - e.g. `"The shared value for 'Widget' is null..."`) is left
  exactly as-is; the new `CompositionDiagnostic.Message` carries the same
  string. `CompositionDiagnostic.ToString()` (what
  `Console.WriteLine(exception.Diagnostic)` renders, per
  `docs/public-api.md`) is the only place the full tree + seed text lives
  - keeping `Exception.Message` short avoids relitigating every existing
    `WithMessage("*...*")` test's substring assumption, and matches
    `docs/public-api.md`'s own example, which reads `exception.Diagnostic`
    separately from the base exception.
- The materialized `Trace` slice is always taken from checkpoint `0` (the
  whole operation), not the checkpoint of the `Resolve<T>` call that
  actually threw - the per-call `checkpoint` local is still used for each
  call's own `Rewind` on success, but a failing call reports the *entire*
  surviving buffer, which by construction (every already-succeeded
  sibling already rewound itself out) is exactly "the failing branch's
  own attempts and its ancestors'," per the task list wording above.
  Threading the local checkpoint into `BuildException` instead would have
  produced only the failing leaf's own attempts, silently dropping every
  ancestor's - caught while writing
  `CompositionDiagnosticsTests.ResolveRoot_DiagnosticTrace_RetainsOnlyTheFailingBranch_NotAlreadySucceededSiblings`
  before it was ever committed as a bug.
- `CompositionPath` gained `RootType` and `ToTreeString()` alongside the
  existing `ToDisplayString()` (kept unchanged, still used by the
  recursion-cycle message) rather than replacing it - the tree format is
  specific to `CompositionDiagnostic.Path`, and the dotted form's callers
  (`BuildCycleMessage`) have their own established test coverage
  (`CompositionRecursionTests`) that doesn't need to move.
- `Compono.Tests` doesn't reference `Compono.Generators` as an analyzer
  (Phase 0's note, still true) - every Phase 4 test (`CreateMany`
  contract/stability, the diagnostics trace-retention test, and the
  `Customer`/`Address` end-to-end exit-criteria test) uses a hand-written
  `ICompositionPlan<T>` test double via `PlanCache<T>.Instance`, the same
  pattern every earlier phase's tests already established.
- **Real manual verification** (`tasks/implement.md` step 7, this
  phase's own new public API surface): `dotnet pack`'d `Compono` into a
  clean local feed and referenced it from a genuinely separate throwaway
  console project (same pattern Phase 2's round 5 established) exercising
  `Create<T>()`, `CreateMany<T>(count)` (positive/zero/negative), and a
  real recursion failure's `exception.Diagnostic` through the real
  generator - not `Compono.Tests`' hand-written `PlanCache<T>` fakes. This
  caught one real gap: `CompositionPath.ToTreeString()`'s node labels used
  `Type.Name` directly, which renders a closed generic in its raw CLR
  form (`List\`1`) instead of a readable one - `Console.WriteLine(exception.Diagnostic)`
  for a `List<Node>` member printed `` List`1 Children `` instead of
  `List<Node> Children`. Fixed by adding `CompositionPath.FriendlyTypeName`
  (recurses through nested generic type arguments, e.g.
  `Dictionary<string, List<int>>`) and using it in both `ToTreeString()`
  call sites; `CompositionDiagnosticsTests.ResolveRoot_DiagnosticPath_RendersClosedGenericTypes_InCSharpStyleNotRawClrForm`
  is the regression guard. `ToDisplayString()` (the dotted form the
  recursion-cycle *message* still uses) was deliberately left alone - it
  already only reads `Name` values off `PathSegment`, never `RequestedType.Name`,
  so it was never affected by this gap.
- **Benchmark result (real, not extrapolated, `DefaultJob` — not a quick
  `--job short` smoke test):** three new `Compono.Benchmarks` classes,
  all against the same `Customer`/`Address` graph (including a
  `List<string>` collection member), run through the real generator via
  `dotnet run -c Release -- --filter "*Resolution*"`, mirroring
  `ArchitectureBenchmarks`/`EcosystemBenchmarks`' split - a review round
  flagged that the first version of this benchmark had no `new()`/
  reflection/AutoFixture comparison at all, unlike Milestone 1's
  benchmarks. `ResolutionBenchmarks` (`Create`/`CreateMany`) measured
  `Create<Customer>()` at ~2.73 KB allocated per call regardless of
  `CreateMany`'s batch size, and `CreateMany<T>(count)` scaling linearly
  with `count` (10.18× allocation at `count=10`, 101.47× at `count=100`,
  against the `count=1` baseline - no super-linear growth). This confirms
  the checkpoint/rewind trace buffer doesn't measurably harm the hot path;
  the shallow-diagnostics fallback ADR-0010 reserved for that case was not
  needed. `ResolutionEcosystemBenchmarks` (`Generated` vs `AutoFixture`)
  measured Generated at ~90.9x faster and ~2.75% of AutoFixture's
  allocation - `Customer` actually gives AutoFixture real
  randomized-value-generation work, unlike `Leaf`.
  `ResolutionArchitectureBenchmarks` (`Direct`/`Generated`/`Reflection`,
  the new `ReflectionComposer.ComposeRecursive<T>()`) is **not** a clean
  win/loss read like the `Leaf` comparison: the reflection baseline fills
  every field with a fixed placeholder value rather than replicating
  Compono's real random-value generation, so it's faster than `Generated`
  for doing categorically less work, not because reflection dispatch
  beats source-generated dispatch - documented explicitly in both the
  class' XML doc `<remarks>` and `docs/performance.md`, the mirror image
  of the AutoFixture caveat. Full tables and reproduction steps recorded
  permanently in
  [`docs/performance.md`](../performance.md#milestone-2-phase-4-resolution-pipeline-result)
  (`docs/architecture.md`'s Diagnostics section links there too, rather
  than duplicating the table).
- A PR #13 review round (Codex) found three real issues, all fixed in the
  same PR. **Trace buffer's own allocation was asserted, not measured
  (P1).** The Phase 4 benchmark result above reported total allocation
  without isolating what fraction the trace buffer itself contributes,
  so "allocation-free on success" read as a stronger claim than actually
  verified. Fixed by measuring `CompositionTraceBuffer` in isolation
  (`GC.GetAllocatedBytesForCurrentThread()` around 1,000,000 direct
  constructions, Release, .NET 10.0.3 arm64): 184 B/instance, ~6.6% of a
  real `Customer` composition's 2.73 KB - real, consistent with
  ADR-0010's "near-zero, not zero-cost" framing, not a violation of it.
  `docs/performance.md` and `docs/architecture.md` now state this
  precisely instead of the unqualified claim; a true zero-allocation
  design (pooling `CompositionTraceBuffer` across root operations) is
  recorded as a new deferred item in `docs/architecture.md`'s Open
  Architectural Decisions, not attempted as a same-PR fix. **`Success`
  recorded before composition actually completed (P2, worse than
  reported).** The review flagged one site
  (`CollectionPlanCache<TValue>.Compose(this)` recording
  `BuiltInProvider: Success` *before* `Compose` ran, so an element
  failure inside the collection left a false `Success` in the
  materialized diagnostic) - confirmed, and the identical bug pattern
  was present in five more places: the profile/semantic/test-double/
  built-in provider branches (recording `Success` before
  `StoreSharedAndReturn`'s authoritative shared-value validation, which
  can still throw) and `ResolveViaGeneratedPlan`'s shared path (same
  issue, after `Compose` but before `StoreSharedAndReturn`). Fixed at all
  six sites by moving each `_trace.Record(..., Success)` to strictly
  after the corresponding value has actually been returned/stored
  without throwing. Verified as a real regression, not just a
  theoretical one: temporarily reverted the fix and confirmed the new
  regression tests actually fail without it, then restored the fix -
  `CompositionDiagnosticsTests.ResolveRoot_DiagnosticTrace_DoesNotRecordSuccess_WhenACollectionPlanElementFails`
  (the exact collection scenario reported) and
  `..._WhenASharedProviderValueFailsValidation` (the broader pattern, at
  the cheapest site to exercise via the injectable-provider test seam).
  **Provider identity dropped from the public trace record (P1).**
  Already covered above and in [ADR-0015](../adr/0015-provider-identity-deferred-in-provider-attempt.md) -
  a real, but already-intentional and now-formally-recorded, deviation
  from ADR-0010's literal text, not a gap.
- The same PR #13 review round separately prompted rebuilding the
  resolution benchmark's reflection baseline, once its flawed comparison
  was noticed on inspection rather than via a specific finding:
  `ReflectionComposer.ComposeRecursive<T>()` originally filled every
  field with a fixed placeholder value (`"value"`, etc.), making
  `ResolutionArchitectureBenchmarks`' `Reflection` column faster than
  `Generated` for doing categorically less work - a comparison that
  invited exactly the wrong conclusion ("why not just use reflection").
  Rewritten to generate real random values matching Compono's own
  defaults (an 8-character alphanumeric string per `PrimitiveValueProvider`'s
  `StringLength`, a 3-element collection per ADR-0013's default
  collection size) via `System.Random.Shared` - the actual reflection-based
  alternative someone would reach for, not a dispatch-cost-only strawman.
  Re-run at `DefaultJob` after the rewrite; `docs/performance.md`'s
  Resolution architecture benchmark table reflects the new, honest
  numbers.
- A second PR #13 review round (`@codex review`, triggered after the
  first round's fixes landed) found three more real issues, all fixed in
  the same PR, plus a separately-requested optimization landed alongside
  them. **`CompositionRequest`: class → `readonly record struct` (the
  "easy win," requested directly rather than found by review).** It's
  never stored beyond the synchronous `ResolveCore` call that builds it -
  never captured, never crosses an async boundary - so the heap
  allocation was pure waste. Prototyped and measured before touching
  production code: 40 B → 0 B, 14.9 ns → 5.5 ns, constructed and consumed
  the same way `ResolveCore` actually does (passed by `in`, not boxed).
  Applied for real: `CompositionRequest` is now `internal readonly record
  struct`, `ICompositionProvider.TryCompose` and every internal consumer
  (`TryProviders`, `StoreSharedAndReturn`, `ValidateAuthoritativeValue`,
  `ResolveViaGeneratedPlan`) take it by `in`, matching
  `CompositionRequestDescriptor`'s existing convention. This is what
  moved every benchmark table in `docs/performance.md` to new, lower
  numbers - re-measured, not estimated. **A generic root type rendered in
  raw CLR form at two more sites (P2).** `CompositionDiagnostic.ToString()`'s
  heading (`RootType.Name`) and the stage-9 failure `Message` text
  (`requestedType.Name`) both had the identical bug
  `CompositionPath.FriendlyTypeName` was added to fix for the path tree in
  the first review round - just missed at these two call sites. Fixed by
  making `FriendlyTypeName` `internal` (not `private`) on `CompositionPath`
  and reusing it at both sites. **An ancestor's in-flight dispatch was
  silently absent from the trace on a nested failure (P2).** The first
  round's `Success`-after-not-before fix was correct but incomplete: an
  ancestor whose own `Compose` was still running when a descendant failed
  never got *any* trace entry for its stage-8/collection-plan attempt,
  since `Success` is (correctly) only recorded after `Compose` returns,
  which never happens for an ancestor still waiting on a failing
  descendant. Fixed by adding `CompositionAttemptOutcome.Pending`,
  recorded immediately before `Compose` runs at both dispatch sites -
  rewound away on success like every other entry, but surviving
  (correctly) when the eventual `Success` never gets recorded either.
  **The trace buffer's growth path was never benchmarked (P2).** Only the
  shallow, 2-level-deep `Customer` graph had been measured, which never
  gets deep enough to trigger `CompositionTraceBuffer`'s own
  `Array.Resize` (each active ancestor frame retains ~6 entries with the
  `Pending` fix above, so a 16-entry buffer resizes past depth ~2-3 -
  already true of `docs/architecture.md`'s own 4-level Diagnostics
  example). Fixed in two parts: bumped the initial capacity from 16 to 32
  (covers ~5 levels without resizing) and added `DeepGraphBenchmarks` (an
  8-level-deep chain, `DeepLevel1` through `DeepLevel8`, deep enough to
  guarantee a resize) so the growth cost is a real measured number in
  `docs/performance.md` rather than assumed away. All four changes
  verified against regression tests reverted-and-reran (same discipline
  as the first review round) before being restored — see
  `CompositionDiagnosticsTests.ResolveRoot_Throws_WithADiagnosticTree_MatchingTheNestedFailurePath`'s
  `Pending`-count assertion and
  `ResolveRoot_Throws_WithAFriendlyGenericRootName_NotTheRawClrForm`.
- A third PR #13 review round (`@codex review` again) found two more real
  gaps, plus surfaced a factual error in ADR-0015's own Context that
  needed correcting. **Stage 7's three built-in providers collapsed into
  one aggregate trace entry (P2).** `TryProviders` let its caller record
  a single `NotHandled` for the whole stage regardless of how many
  providers actually declined - `BuiltInProviders.Default` genuinely has
  three registered today (`PrimitiveValueProvider`, `EnumValueProvider`,
  `NullableValueProvider`), so a failing request's trace showed 1
  aggregate entry instead of the real count. This directly falsified
  ADR-0015's own Decision Drivers claim ("no stage in Milestone 2 ...
  registers more than one competing provider yet") - stage 7 already
  does, today, not hypothetically. Fixed by moving the `NotHandled`
  recording into `TryProviders` itself, one entry per provider actually
  tried (a new `PipelineStage stage` parameter); the winning provider's
  own outcome is still left for the caller to record once, after
  `StoreSharedAndReturn` validates it - unaffected by the earlier
  Success-ordering fix. Per `design-decisions.md`'s "an accepted ADR is a
  historical record" rule, ADR-0015's own text is left as originally
  written (an accurate record of the reasoning *at the time*, even though
  one of its cited facts turned out to be wrong) - the correction instead
  lands where it actually matters going forward: `docs/architecture.md`'s
  identical claim (a living doc, meant to be kept current) is fixed in
  place, and this note records the correction for anyone reading the plan
  forward. ADR-0015's actual Decision Outcome (defer provider *identity*,
  not attempt *counting*) still holds - this fix only restores accurate
  attempt counts, it doesn't add a way to tell *which* provider each
  entry belongs to, so the ADR's core deferral is unaffected by the
  correction. **`FriendlyTypeName` didn't recurse into array element
  types (P2).** `Type.IsGenericType` is `false` for an array type itself
  (array-ness and generic-ness are orthogonal in reflection), so
  `List<Missing>[]` fell straight through to the raw CLR form
  (`List\`1[]`) - the same defect class `FriendlyTypeName` exists to
  prevent, just not handled for arrays. Fixed by checking `IsArray` first
  and recursing into `GetElementType()`, reapplying the array's own rank.
  Both fixes verified against regression tests reverted-and-rerun before
  being restored, matching prior rounds' discipline; neither changed any
  benchmark number (re-measured to confirm - `docs/performance.md`'s
  tables are unchanged within normal noise, since both are trace-fidelity/
  formatting fixes, not allocation or timing changes).
- A fourth PR #13 review round found the most severe gap of the whole
  review cycle (P1): **`CreateMany<T>()` was never wired into the source
  generator's discovery, so the single most-advertised `CreateMany<T>`
  usage threw `CompositionException` at runtime.** A consumer calling
  only `composer.CreateMany<Customer>(3)` - never `Create<Customer>()`,
  no `[Composable]` attribute - got no generated plan for `Customer` at
  all: `CreateInvocationDiscovery.IsCandidate`/`Transform`
  (`src/Compono.Generators/Discovery/CreateInvocationDiscovery.cs`) still
  hardcoded the method name `"Create"` only. Notably, this was **not** a
  design gap - [ADR-0004](../adr/0004-composition-plan-discovery-and-dispatch.md)
  already specified "call-site driven (find `Create<T>()`/`CreateMany<T>()`
  invocations..." and `docs/architecture.md`'s Discovery and Dispatch
  section already said the same - the generator's implementation simply
  never caught up once Phase 4 shipped the real `CreateMany<T>()` method,
  since `CreateInvocationDiscovery` predates it (Milestone 1). Fixed by
  widening both the syntax-level `IsCandidate` pattern and the
  symbol-level `Transform` check to accept `"Create"` or `"CreateMany"` -
  a two-line change once located, per the ADR's already-correct design.
  Verified three ways, matching the bar for source-generator-facing
  changes (`tasks/implement.md` step 7): (1) a new generator snapshot
  test, `CompositionPlanVerifyTests.CreateManyOnlyInvocation_GeneratesCompositionPlan`,
  compiling source with a `CreateMany<Customer>(3)` call site and no
  `Create<Customer>()`/`[Composable]` anywhere, confirming a real plan
  gets generated; (2) reverted the fix and confirmed that test fails,
  restored and confirmed it passes; (3) `dotnet pack`'d `Compono` into a
  clean local feed and ran a genuinely separate throwaway console
  project calling only `composer.CreateMany<Customer>(3)`, confirming
  real end-to-end output (three distinct composed `Customer`s) through
  the actual published package, not just the in-memory test harness.
- A fifth PR #13 review round found four more real issues, the largest
  being a genuine reversal of ADR-0015's own decision. **Provider identity
  restored in `ProviderAttempt` (P1) - the user directed this explicitly
  ("Codex is pushing back on that provider identity decision. I'm
  inclined to agree. Let's add it back in") after independently agreeing
  with the review finding.** ADR-0015's deferral rested on "no stage has
  more than one competing provider yet" - already false for stage 7's
  three real built-in providers when ADR-0015 was written, and the round-3
  per-provider-trace fix only made the gap visible (three indistinguishable
  `(BuiltInProvider, NotHandled)` entries) rather than closing it. Per
  `design-decisions.md`'s ADR-immutability rule, this is a genuine decision
  reversal, not a factual correction like round 3's ADR-0015 issue - so it
  gets its own ADR ([ADR-0016](../adr/0016-provider-identity-restored-in-provider-attempt.md))
  superseding ADR-0015 (Status flipped, body left unedited), rather than
  editing ADR-0015 in place. `ProviderAttempt` gains a `Type? Provider`
  field - the provider's own concrete CLR type (`null` for a context-owned
  stage, which isn't an `ICompositionProvider` instance), chosen over a
  provider index (unstable once Milestone 3 allows reordering, per
  ADR-0015's own still-valid Option 2 reasoning) or an opaque token
  (needs a new `ICompositionProvider` member, per ADR-0015's own Option 3
  reasoning) - `Type` identity is already established elsewhere in this
  engine (`PlanCache<T>`, the active-construction-frame stack,
  `EnumValueProvider`'s cache), so this isn't a new pattern. Fixing this
  meant redesigning where outcomes get recorded: `TryProviders` now hands
  back the winning provider's `Type` via an `out` parameter, and
  `StoreSharedAndReturn` records the stage's real outcome (`Success` *or*
  `Failure`) itself, before `Authoritative` can throw - which also fixed
  **finding two of this round (P2): a shared request's validation failure
  left no trace entry for the provider that produced the bad value at
  all**, the same "recorded after, not before" ordering bug as earlier
  rounds, just at a site those rounds hadn't reached yet. **Finding three
  (P1): a `Links` entry I'd added to the already-Accepted ADR-0010 in an
  earlier round was itself a rule violation** - reviewed and agreed;
  removed entirely, restoring ADR-0010 to its pre-Phase-4 text. The
  cross-reference lives only in ADR-0015/ADR-0016's own `Links` sections
  and the ADR index now, never in ADR-0010 itself. **Finding four (P2):
  `CompositionException(CompositionDiagnostic)` dereferenced `diagnostic.Message`
  in its `base(...)` initializer before the constructor body could guard
  it**, so a `null` argument surfaced as `NullReferenceException` instead
  of `ArgumentNullException` - fixed by routing the null check through a
  static helper called from the initializer itself
  (`base(RequireDiagnostic(diagnostic).Message)`), since a guard clause in
  the body runs too late. All four fixes verified against regression tests
  reverted-and-rerun before being restored, matching every prior round's
  discipline. Re-measured benchmarks honestly this time: unlike round 3,
  this round *does* move every number in `docs/performance.md` - widening
  `ProviderAttempt` roughly doubles each trace entry's size, moving
  `Create<Customer>()` from 2.46 KB to 2.71 KB (+256 B, ~10%), confirmed
  attributable to the struct width by the isolated `CompositionTraceBuffer`
  measurement moving by almost exactly the same amount. Recorded as an
  accepted tradeoff (a real "which provider" answer for ~10% more
  allocation on the failure-adjacent path), not chased back down.
- A sixth PR #13 review round found one more real gap, the same
  ancestor-visibility class as the `GeneratedPlan`/collection-plan
  `Pending` fixes above, at a third dispatch site (P2). **`TryProviders`
  (stages 4-7) didn't record an in-flight attempt before calling
  `candidate.TryCompose(...)`.** `ICompositionProvider.TryCompose` is
  handed the live `ICompositionContext` specifically so a provider can
  recursively resolve part of its own value (no built-in provider does
  this today, but nothing about the contract forbids it, and it's exactly
  the extension point Milestone 3/5/6 profile/semantic/test-double
  providers are expected to use) - if that nested `Resolve<T>()` call
  throws, `candidate` was never recorded anywhere, since `TryProviders`
  only recorded `NotHandled` *after* `TryCompose` returned normally. The
  materialized diagnostic then permanently omitted the provider and stage
  that led to the failing child. Fixed by applying the same
  checkpoint/`Pending`/rewind pattern already used for stage 8 and the
  collection-plan branch: record `(stage, candidate.GetType(), Pending)`
  immediately before `TryCompose` runs, then rewind that marker away once
  `TryCompose` actually returns (success or decline) - if it throws
  instead, the rewind never executes and the `Pending` marker survives
  into the failing trace. Verified with a new regression test
  (`ResolveRoot_DiagnosticTrace_RecordsAPendingEntry_WhenAProviderRecursivelyResolvesAFailingNestedRequest`
  in `CompositionDiagnosticsTests.cs`) using the injectable-provider test
  seam and a fake provider whose `TryCompose` calls
  `context.Resolve<T>()` for an unsatisfiable nested type; reverted the
  fix and confirmed the test fails, restored and confirmed it and the
  full suite (152 `Compono.Tests`, 146 `Compono.Generators.Tests`) pass.
  Re-measured the allocation-per-`Create<T>()` benchmark before and after
  this specific fix in isolation (a throwaway test, deleted after
  measuring): identical 1384.00 B/op both times - the extra
  checkpoint/record/rewind calls stay well under the trace buffer's
  32-entry initial capacity for an ordinary graph, so they cost stack
  work only, no heap allocation. `docs/performance.md` is unchanged.

## Critical Files

- `src/Compono/CompositionRequestDescriptor.cs` (`Kind`, `Ordinal`,
  `Name`, `Nullability`), `src/Compono/CompositionRequestKind.cs` — new
  (`public`) — **Done (Phase 0)**
- `src/Compono/CompositionRequest.cs` — new (`internal`) — **Done (Phase 0)**.
  Converted from `sealed record` (class) to `internal readonly record
  struct`, all consumers taking it by `in` — **Done (Phase 4, PR #13
  review round 2)**
- `src/Compono/CompositionResult.cs` — new (`internal`) — **Done (Phase 0)**.
  `Failure` case added — **Done (Phase 3)**
- `src/Compono/ICompositionProvider.cs` — new (`internal`) — **Done (Phase 0)**
- `src/Compono/CompositionException.cs` — new (`public`); minimal message-only
  exception for now — **Done (Phase 0)**. Second constructor accepting a
  `CompositionDiagnostic` (the shape every pipeline-thrown instance uses)
  and the `Diagnostic` property — **Done (Phase 4)**. Null-`diagnostic`
  guard routed through a static helper called from the `base(...)`
  initializer itself, since a body-level guard clause runs too late —
  **Done (Phase 4, PR #13 review round 5)**
- `src/Compono/CompositionDiagnostic.cs`, `src/Compono/ProviderAttempt.cs`,
  `src/Compono/PipelineStage.cs`, `src/Compono/CompositionAttemptOutcome.cs`
  — new (`public`); `src/Compono/CompositionTraceBuffer.cs` — new
  (`internal`) — **Done (Phase 4)**. `CompositionAttemptOutcome.Pending`
  added, recorded before `Compose` at both generated-plan/collection-plan
  dispatch sites; `CompositionTraceBuffer`'s initial capacity bumped 16 →
  32; `CompositionDiagnostic.ToString()` renders `RootType` via
  `CompositionPath.FriendlyTypeName` instead of raw `.Name` — **Done
  (Phase 4, PR #13 review round 2)**. `ProviderAttempt` gained a
  `Type? Provider` field ([ADR-0016](../adr/0016-provider-identity-restored-in-provider-attempt.md),
  reversing [ADR-0015](../adr/0015-provider-identity-deferred-in-provider-attempt.md));
  `CompositionTraceBuffer.Record` takes the provider type alongside stage
  and outcome — **Done (Phase 4, PR #13 review round 5)**
- `src/Compono/PathSegment.cs` (`Ordinal`/`Index`-keyed, `Name` for
  segments that have one), `src/Compono/CompositionPath.cs` — new
  (`internal`) — **Done (Phase 0, pulled forward from Phase 1's original
  scope; structural chain only, no FNV-1a hashing yet)**. `ToDisplayString()`
  (diagnostics-only, derived from segment `Name`s) — **Done (Phase 1)**.
  `RootType` and `ToTreeString()` (the `CompositionDiagnostic.Path` tree
  format) — **Done (Phase 4)**. `FriendlyTypeName` widened from `private`
  to `internal` so `CompositionDiagnostic` and `CompositionContext` can
  reuse it for the heading/failure-message sites the first review round
  missed — **Done (Phase 4, PR #13 review round 2)**
- `src/Compono/CompositionContext.cs` — new (replaces the inline
  `PlaceholderCompositionContext` in `Composer.cs`); implements the
  public descriptor-based `Resolve<T>` and the internal `ResolveRoot<T>`;
  the internal test-seam constructor accepting explicit per-stage
  provider collections — **Done (Phase 0)**. Seed-aware constructors, the
  `_random` field forked/restored alongside `_path` in `ResolveCore`, and
  the internal `Random` test-observability property — **Done (Phase 1)**.
  Stages 2/3 (shared/scoped values, exact registrations), the
  active-construction-frame stack, and the `ResolveSharedForTesting<T>`
  test seam — **Done (Phase 3)**. The `CompositionTraceBuffer`
  checkpoint/record/rewind wiring and `BuildException` (materializes a
  `CompositionDiagnostic` from the current path/trace/seed) —
  **Done (Phase 4)**. `TryProviders` records a `Pending` marker for each
  candidate provider immediately before calling `TryCompose`, rewound on
  a normal return — closes the same ancestor-visibility gap already fixed
  for stage 8/collection-plan dispatch, for a provider that recursively
  resolves a nested request that fails — **Done (Phase 4, PR #13 review
  round 6)**
- `src/Compono/CompositionScope.cs`, `src/Compono/CompositionRegistrations.cs`
  — new (`internal`) — **Done (Phase 3)**
- `src/Compono/CompositionSeed.cs`, `src/Compono/IRandomSource.cs`,
  `src/Compono/RandomSource.cs`, `src/Compono/Fnv1a.cs`,
  `src/Compono/SplitMix64.cs` — new (`internal`) — **Done (Phase 1)**
- `src/Compono/Providers/*.cs` — new (`internal`, one file per built-in
  primitive/enum/nullable provider, per `coding-standards.md`'s
  one-public-type-per-file rule — applies to `internal` types too) —
  **Done (Phase 2)**
- `src/Compono/CollectionPlanCache.cs` — new (`public`); mirrors
  `PlanCache<T>`'s shape exactly (ADR-0010 Amendment 3) — **Done (Phase 2)**
- `src/Compono/UniqueValueResolver.cs` — new (`public`); the bounded
  duplicate-value retry helper generated `HashSet<T>`/
  `Dictionary<TKey, ...>` collection plans call (ADR-0010 Amendment 3) —
  **Done (Phase 2)**
- `src/Compono/CompositionRequestKind.cs` — modified: `CollectionElement`/
  `DictionaryKey`/`DictionaryValue` cases added (ADR-0010 Amendment 3) —
  **Done (Phase 2)**
- `src/Compono.Generators/Discovery/TransitiveClosureWalker.cs` —
  modified: recognizes the five ADR-0013 collection shapes, recursing
  into element/key type(s) instead of walking the collection itself as a
  composable type — **Done (Phase 2)**
- `src/Compono.Generators/Discovery/CollectionWellKnownTypes.cs` — new;
  classifies a symbol as one of the five supported closed collection
  shapes (or not), extracting element/key type(s) — **Done (Phase 2)**
- `src/Compono.Generators/Models/DiscoveredCollectionInfo.cs`,
  `src/Compono.Generators/Models/TransitiveClosureResult.cs` — new —
  **Done (Phase 2)**
- `src/Compono.Generators/Emitters/CollectionPlanEmitter.cs`,
  `src/Compono.Generators/Emitters/GeneratedFileNaming.cs` (hint-naming
  logic extracted out of `CompositionPlanEmitter` for reuse),
  `src/Compono.Generators/Templates/CollectionPlan.scriban` — new —
  **Done (Phase 2)**
- `src/Compono.Generators/ComponoIncrementalGenerator.cs`,
  `src/Compono.Generators/Discovery/{CreateInvocationDiscovery,ComposableAttributeDiscovery,ComposedTypeAnalyzer}.cs` —
  modified: return `TransitiveClosureResult` instead of a bare
  `EquatableArray<DiscoveredTypeInfo>` throughout, and the generator
  collects/dedupes/emits discovered collections alongside types —
  **Done (Phase 2)**
- `test/Compono.Tests/Providers/*.cs`, `test/Compono.Tests/UniqueValueResolverTests.cs`,
  `test/Compono.Tests/CollectionPlanCacheDispatchTests.cs`,
  `test/Compono.Generators.Tests/CollectionPlanVerifyTests.cs` — new —
  **Done (Phase 2)**
- `src/Compono/ICompositionContext.cs` — modified (`Resolve<T>` signature
  changed to `in CompositionRequestDescriptor`) — **Done (Phase 0)**
- `src/Compono/Composer.cs` — modified: `Create<T>()` rewired onto
  `ResolveRoot<T>()` — **Done (Phase 0)**. The internal
  `CreateRootForTesting<T>(CompositionSeed)`/
  `CreateManyForTesting<T>(int, CompositionSeed)` test-seam factories —
  **Done (Phase 1)**. Public `CreateMany<T>(int count)` (negative-count/
  zero-count/`IReadOnlyList<T>` contract), sharing its seed-derivation
  logic with `CreateManyForTesting` via a private `ComposeMany<T>` helper
  — **Done (Phase 4)**
- `src/Compono.Generators/Templates/CompositionPlan.scriban`,
  `src/Compono.Generators/Emitters/CompositionPlanEmitter.cs` — modified:
  emit `CompositionRequestDescriptor` (constructor-parameter name added
  to the emitter's model; ordinal is the emission-order index for both
  parameters and required members) instead of Milestone 1's bare
  `Nullability` argument — **Done (Phase 0, unscoped fix — see Phase 0
  Notes)**
- `src/Compono.Generators/Discovery/RequiredMemberCollector.cs` —
  modified: base-to-derived required-member ordering (was
  derived-to-base), per ADR-0012's amendment 2 — **Done (Phase 0,
  unscoped fix — see Phase 0 Notes; confirmed against ADR-0012 amendment
  2's canonical algorithm in Phase 1, no further changes needed)**
- `src/Compono.Generators/Discovery/CreateInvocationDiscovery.cs` —
  modified: `IsCandidate`/`Transform` recognize `CreateMany<T>()` call
  sites, not just `Create<T>()` — closes a real gap against
  [ADR-0004](../adr/0004-composition-plan-discovery-and-dispatch.md)'s
  already-correct design, discovered once `CreateMany<T>()` shipped as a
  real method — **Done (Phase 4, PR #13 review round 4)**.
  `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs`'s
  `CreateManyOnlyInvocation_GeneratesCompositionPlan` (+ its snapshot) —
  new — **Done (Phase 4, PR #13 review round 4)**
- `test/Compono.Tests/CompositionContextTests.cs`,
  `test/Compono.Tests/ComposerTests.cs` — new — **Done (Phase 0)**
- `test/Compono.Tests/RandomSourceTests.cs`,
  `test/Compono.Tests/CompositionRandomIntegrationTests.cs` — new —
  **Done (Phase 1)**
- `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs` +
  regenerated `Snapshots/*.verified.cs` — modified — **Done (Phase 0,
  unscoped fix)**
- `test/Compono.Tests/CompositionScopeTests.cs`,
  `test/Compono.Tests/CompositionRegistrationTests.cs`,
  `test/Compono.Tests/CompositionRecursionTests.cs` — new — **Done (Phase 3)**
- `test/Compono.Tests/CompositionTraceBufferTests.cs`,
  `test/Compono.Tests/CompositionDiagnosticsTests.cs`,
  `test/Compono.Tests/ComposerCreateManyTests.cs`,
  `test/Compono.Tests/CompositionEndToEndTests.cs` — new — **Done (Phase 4)**.
  `test/Compono.Tests/CompositionExceptionTests.cs` — new (round 5's
  null-`diagnostic` guard) — **Done (Phase 4, PR #13 review round 5)**
- `benchmarks/Compono.Benchmarks/ResolutionBenchmarkTypes.cs`
  (`Customer`/`Address`), `benchmarks/Compono.Benchmarks/ResolutionBenchmarks.cs`
  (`Create`/`CreateMany` scaling),
  `benchmarks/Compono.Benchmarks/ResolutionArchitectureBenchmarks.cs`
  (`Direct`/`Generated`/`Reflection`),
  `benchmarks/Compono.Benchmarks/ResolutionEcosystemBenchmarks.cs`
  (`Generated`/`AutoFixture`),
  `benchmarks/Compono.Benchmarks/DeepGraphBenchmarks.cs` (`DeepLevel1`-`DeepLevel8`,
  an 8-level-deep chain exercising `CompositionTraceBuffer`'s
  `Array.Resize` growth path) — new;
  `benchmarks/Compono.Benchmarks/ReflectionComposer.cs` — modified:
  `ComposeRecursive<T>()` added (fixed-placeholder-value recursive
  reflection composer, alongside the existing parameterless-only
  `Compose<T>()`) — **Done (Phase 4)**
- `test/Compono.Tests/**` — new tests per phase above

## Test Plan

Per `references/testing.md`'s established pattern in
`test/Compono.Tests`:

- Pipeline-order tests confirming the fixed 9-stage precedence, and the
  context-owned-vs-provider-collection split, are both honored (Phase 0),
  using `CompositionContext`'s internal test-seam constructor to inject
  fake providers at specific stages — no public configuration surface
  involved.
- Determinism tests (Phase 1) — via `Composer.CreateRootForTesting`, the
  internal seam that exercises the real `Composer`/`CompositionContext`
  flow with an explicit seed before Milestone 3's public builder exists —
  asserting byte-for-byte reproducibility across independent resolutions
  with the same seed, structural independence across differently-`Ordinal`ed
  forks (same-typed sibling parameters, collection elements, dictionary
  keys vs. values), that renaming (without reordering) a constructor
  parameter/required member doesn't change its derived value, and that
  all five `PathSegment` kinds fork to distinct output at ordinal/index 0
  (the tag-collision test).
- Provider-level unit tests for every built-in provider (Phase 2),
  keeping each provider narrowly testable per `coding-standards.md`'s "no
  God classes" guidance, plus collection-specific edge cases (duplicate-value
  retry exhaustion for both `Dictionary<TKey, ...>` keys and `HashSet<T>`
  elements; a non-recursive collection-index path-construction test).
- Scope/sharing/registration/recursion tests (Phase 3) exercising the
  internal registration seam via `InternalsVisibleTo`, including the
  "registered value legitimately terminates a cycle" case the original
  plan's recursion design would have rejected, authoritative null/type
  validation for shared/registered values, and the concrete
  `Node(List<Node> Children)` recursive-element case (moved from Phase 2)
  asserting the cycle diagnostic's edges include the collection index.
- `CreateMany` semantics, argument/return contract, and seed-stability
  tests (via `Composer.CreateRootForTesting`/`CreateManyForTesting`), a
  diagnostics-trace-retention test (active failing branch only, not
  completed siblings), and a benchmark suite (Phase 4) covering both the
  trace buffer's allocation-free claim and end-to-end `Create<T>()`/
  `CreateMany<T>()` throughput/allocations/scaling for a representative
  graph — the first point in this plan a graph exists that's
  representative enough to be worth benchmarking end to end, not just at
  the construction-dispatch layer Milestone 1's benchmarks already cover.
- An end-to-end `Create<T>()`/`CreateMany<T>()` test against the
  `Customer`/`Address` shape from the Execution Flow section (Phase 4),
  matching the Milestone 1 plan's "representative record or class" bar.

## Notes

Anything discovered mid-implementation that changes this plan's shape
from what's scoped above goes here, per `design-decisions.md`'s "a plan
being wrong about *how* doesn't require superseding anything."

This plan's first draft (context-owned-vs-provider mixing, unrestricted
`Failure`, type-only `CompositionPath`, before-every-request recursion
checking, Phase 3 randomness rewiring built-ins written in Phase 1) went
through a deep design review before any implementation started — see
ADR-0010 through ADR-0013's Context sections for exactly what each gap
was and why the revised design closes it.

A second review before Phase 0 began found ten more pre-implementation
gaps (root/descriptor mismatch, ordinal-vs-name identity, type-identity
hashing, diagnostics-trace retention scope, `HashSet<T>` uniqueness, a
vague recursion test, missing internal seams for seed injection and
pipeline-stage testing, incorrect Phase 0 wording about what already
works from Milestone 1, and the collection generic-dispatch bridge) — see
ADR-0010/0011/0012/0013's `## Amendment (2026-07-28)` sections.

A third review, still before any code was written, found ten further
refinements: `CompositionRequestDescriptor` as a plain struct (not a
record struct); the full canonical required-member ordinal algorithm
(partial declarations, base members, generator-produced members); an
explicit tag-collision test proving the five `PathSegment` kinds don't
collide at ordinal/index 0; the structural-position reproducibility
contract stated as a guarantee, not an implementation detail; strongly-
typed per-shape delegate caches for the collection-dispatch bridge
instead of an untyped `Delegate` cache; an explicit Native AOT/trimming
position for that bridge; the `CreateWithSeed`→`CreateRootForTesting`/
`CreateManyForTesting` rename for clarity; `CreateMany`'s
negative/zero/return-type contract; authoritative null/type validation
for stages 2/3; and moving the `Node(List<Node> Children)` recursion test
to Phase 3, where the recursion detector it depends on actually exists —
see ADR-0010/0011/0012's `## Amendment 2 (2026-07-28)` sections for the
full detail behind each.

A fourth review, mid-Phase-2 and before any Phase 2 code was written,
caught that the collection-dispatch bridge scoped by the third review
(`MakeGenericMethod`/`CreateDelegate`, cached per closed collection type)
is a genuine violation of ADR-0001's no-reflection-by-default rule, not
an acceptable bounded exception. Replaced with generator-emitted,
strongly typed collection plans dispatched via a new `CollectionPlanCache<T>` —
see ADR-0010's `## Amendment 3 (2026-07-28)` section, and this plan's
Phase 2 section above, for the corrected shape.
