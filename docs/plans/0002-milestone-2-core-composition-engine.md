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
(collection generation semantics)

These four ADRs supersede the plan's original ADR-0007/0008/0009 basis
after a deep design review of this plan's first draft — see each ADR's
Links section for the supersession chain.

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

### Phase 2 — Built-in value providers and collections (In Progress)

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
ADR-0010's Amendment 3. The task list below reflects the corrected shape.

- [x] Primitive/simple-type providers (`string`, `bool`, integral types,
      floating-point types, `decimal`, `Guid`, `DateTime`,
      `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`) — ordinary
      `ICompositionProvider`s in stage 7's provider collection, unchanged
      from the plan's original shape
- [x] Enum provider (random valid enum member, via `IRandomSource`) —
      ordinary stage-7 provider, unchanged
- [x] Nullable value type provider (composes the underlying type;
      nullable-generation *default* beyond that is a still-open
      `docs/mvp.md` item this phase doesn't resolve — see ADR-0013) —
      ordinary stage-7 provider, unchanged
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
- [ ] A duplicate-value retry-exhaustion test through an actual generated
      `HashSet<T>`/`Dictionary<TKey, ...>` plan against a genuinely
      low-cardinality element/key type, and a collection-index
      **path-construction** test (e.g. `List<Address>` with 3 elements)
      asserting each element's independent output at the runtime level —
      still open; `UniqueValueResolverTests` covers the retry/exhaustion
      *algorithm* directly (with a stub context), and the tag-collision/
      structural-independence guarantee `CollectionElement(i)` relies on
      is already covered by Phase 1's `RandomSourceTests`/
      `CompositionRandomIntegrationTests`, so this remaining item is
      narrower than originally scoped: an end-to-end runtime assertion
      through a real dispatched collection plan, not new coverage of
      previously-untested behavior
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

### Phase 3 — Scope, shared values, exact registrations, and recursion detection (Not Started)

- [ ] `CompositionScope` (type-keyed, one per root composition operation)
- [ ] Wire `IsShared` requests through the scope-check pipeline stage
      (stage 2)
- [ ] Internal test-only exact-registration store satisfying pipeline
      stage 3, exercised via `InternalsVisibleTo` from `Compono.Tests`
      (no public API yet, per ADR-0011's scope note)
- [ ] Authoritative null/type validation for stages 2 and 3
      ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)'s
      second amendment): a shared/scoped or registration-produced value
      that is `null` for a non-nullable request, or whose runtime type
      isn't assignable to `CompositionRequest.RequestedType`, is an
      authoritative `Failure` at that stage — never passed through as
      `NotHandled` to a later stage
- [ ] Active-construction-frame stack (`internal`, distinct from
      `CompositionPath`): pushed only immediately before stage 8 invokes
      a generated plan, popped in `finally`; a request for a type whose
      frame is already active is an authoritative recursion `Failure`
      carrying the chain of active frames
- [ ] Unit tests: a shared request resolves once and reuses the value for
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

### Phase 4 — `CreateMany<T>()` and diagnostics polish (Not Started)

- [ ] `Composer.CreateMany<T>(int count)` — `count` independent root
      composition operations (Execution Flow section, above), each
      item's root seed forked from the batch root via `"CreateMany"` +
      index (ADR-0012) — no cross-item scope reuse. Contract, per
      [ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)'s
      second amendment: `count < 0` throws via
      `ArgumentOutOfRangeException.ThrowIfNegative(count)`; `count == 0`
      returns an empty, materialized, non-null `IReadOnlyList<T>`; return
      type is `IReadOnlyList<T>` (not `IEnumerable<T>` — the batch is
      always fully, eagerly materialized, never deferred)
- [ ] The allocation-free-on-success trace buffer (ADR-0010): checkpoint
      on `Resolve<T>`/`ResolveRoot<T>` entry, append a compact
      `ProviderAttempt` per stage/provider tried, rewind on success,
      materialize into the durable diagnostic on failure before the
      buffer unwinds further. Per ADR-0010's amendment: because a
      sibling's checkpoint-rewind happens on its own success (before the
      next sibling is even attempted), the materialized trace on failure
      naturally contains only the active failing branch's own attempts —
      never an already-succeeded, already-rewound sibling's
- [ ] Structured diagnostic type surfaces root request, full path (as a
      display string derived from `CompositionPath`), the materialized
      trace, seed, and a human-readable remediation-oriented message,
      matching `docs/architecture.md`'s example format
- [ ] Unit test asserting the trace-retention property directly: a type
      with two successfully-resolved constructor parameters followed by a
      third that fails produces a diagnostic trace containing only the
      failing parameter's attempts (and its ancestors' own attempts) —
      not the two already-succeeded siblings'
- [ ] Benchmark (extending `Compono.Benchmarks` from Milestone 1, widened
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
- [ ] Exit-criteria pass: representative object graph (nested composable
      type + built-ins + a collection) composes deterministically end to
      end via `Create<T>()` and `CreateMany<T>()`, matching the Execution
      Flow section above exactly
- [ ] `CreateMany` stability test: items 0–2 of `CreateMany<T>(3)` and
      `CreateMany<T>(10)` (same explicit root seed) are byte-for-byte
      identical
- [ ] `CreateMany` argument/return contract tests: `count < 0` throws
      `ArgumentOutOfRangeException`; `count == 0` returns an empty,
      non-null `IReadOnlyList<T>`; a standalone `Create<T>()` (via
      `CreateRootForTesting`) called twice with the same explicit seed
      produces identical output, confirming no hidden per-call state
      beyond the seed itself

## Critical Files

- `src/Compono/CompositionRequestDescriptor.cs` (`Kind`, `Ordinal`,
  `Name`, `Nullability`), `src/Compono/CompositionRequestKind.cs` — new
  (`public`) — **Done (Phase 0)**
- `src/Compono/CompositionRequest.cs` — new (`internal`) — **Done (Phase 0)**
- `src/Compono/CompositionResult.cs` — new (`internal`) — **Done (Phase 0)**
- `src/Compono/ICompositionProvider.cs` — new (`internal`) — **Done (Phase 0)**
- `src/Compono/CompositionException.cs` — new (`public`); minimal message-only
  exception for now, enriched into the full structured diagnostic in
  Phase 4 — **Done (Phase 0)**
- `src/Compono/PathSegment.cs` (`Ordinal`/`Index`-keyed, `Name` for
  segments that have one), `src/Compono/CompositionPath.cs` — new
  (`internal`) — **Done (Phase 0, pulled forward from Phase 1's original
  scope; structural chain only, no FNV-1a hashing yet)**. `ToDisplayString()`
  (diagnostics-only, derived from segment `Name`s) — **Done (Phase 1)**.
- `src/Compono/CompositionContext.cs` — new (replaces the inline
  `PlaceholderCompositionContext` in `Composer.cs`); implements the
  public descriptor-based `Resolve<T>` and the internal `ResolveRoot<T>`;
  the internal test-seam constructor accepting explicit per-stage
  provider collections — **Done (Phase 0)**. Seed-aware constructors, the
  `_random` field forked/restored alongside `_path` in `ResolveCore`, and
  the internal `Random` test-observability property — **Done (Phase 1)**.
  The active-construction-frame stack and diagnostics trace buffer are
  still Phase 3/4 scope, not implemented yet.
- `src/Compono/CompositionScope.cs` — new (`internal`)
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
  **Done (Phase 1)**. `CreateMany<T>()` itself (with the
  negative-count/zero-count/`IReadOnlyList<T>` contract) is still Phase 4
  scope.
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
- `test/Compono.Tests/CompositionContextTests.cs`,
  `test/Compono.Tests/ComposerTests.cs` — new — **Done (Phase 0)**
- `test/Compono.Tests/RandomSourceTests.cs`,
  `test/Compono.Tests/CompositionRandomIntegrationTests.cs` — new —
  **Done (Phase 1)**
- `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs` +
  regenerated `Snapshots/*.verified.cs` — modified — **Done (Phase 0,
  unscoped fix)**
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
