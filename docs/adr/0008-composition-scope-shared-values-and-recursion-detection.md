# [ADR-0008] Composition Scope, Shared Values, Recursion Detection, and CreateMany Semantics

**Status:** Superseded by ADR-0011

**Date:** 2026-07-28

**Decision Makers:** solo (CreateMany scope semantics confirmed with user)

## Context

`docs/mvp.md`'s Milestone 2 scope lists "Composition scopes," "Shared
values," "Exact registrations," "Recursion detection," and
`CreateMany<T>()`. `docs/architecture.md` already narrows the shared-value
question to one lifetime rather than a general DI lifetime system ("The
MVP should begin with one clear shared lifetime rather than a
general-purpose dependency injection lifetime system"), but doesn't say
which lifetime that is, how a value gets marked shared without the
`[Shared]` attribute (which belongs to `Compono.Xunit`, per package
boundaries — not built yet), what recursion detection actually does when
it fires, or what `CreateMany<T>(count)` does with that scope across its
`count` instances. `docs/public-api.md` also leaves open whether sharing
is type-based only or can be keyed by name/qualifier.

## Decision Drivers

- `docs/architecture.md`'s explicit steer against a general-purpose
  lifetime system — the MVP needs exactly one shared lifetime, not a
  menu.
- No test-framework integration exists yet (`Compono.Xunit` is Milestone
  4) — the `[Shared]` attribute itself can't be built in Milestone 2, but
  the underlying scope mechanism it will attach to has to exist now so M4
  isn't redesigning the core to fit it later.
- `docs/architecture.md`'s diagnostic philosophy ("Recursive graphs fail
  clearly") — a cyclic object graph must produce a readable diagnostic,
  not a `StackOverflowException`.
- Confirmed with the user: `CreateMany<T>(count)` must be semantically
  identical to calling `Create<T>()` `count` times — no implicit
  cross-item sharing.

## Considered Options

**Scope lifetime:**
1. A single scope per root composition operation (one `Create<T>()` call,
   or one item of a `CreateMany<T>()` call, per the CreateMany decision
   below) — matches architecture.md's "composition graph" lifetime.
2. No scope/sharing mechanism at all in Milestone 2; defer entirely to
   Milestone 4 alongside the `[Shared]` attribute.
3. Build the full multi-lifetime menu now (request / composition graph /
   test case / user-created scope) that `docs/architecture.md` lists as
   *possible* lifetimes.

**Sharing key:**
1. Type-based only (`CompositionScope` keyed by `RequestedType`).
2. Type + name/qualifier keyed, to support disambiguating two shared
   values of the same type.

**CreateMany scope semantics:**
1. Fresh scope per instance (chosen — confirmed with user).
2. One shared scope across all `count` instances.

## Decision Outcome

**Scope lifetime: Option 1.** A `CompositionScope` is created once per
root composition operation and lives for that operation's entire object
graph. `Create<T>()` creates exactly one. `CreateMany<T>(count)` creates
`count` independent scopes — one per item — per the CreateMany decision
below. There is no broader "test case" or "user-created scope" lifetime in
Milestone 2; that's explicitly deferred (see Scope of this decision,
below) until Milestone 4 has a concrete consumer (a per-theory-row scope)
to design against, rather than guessing its shape now.

**Sharing key: Option 1, type-based only.** A request participates in
scope reuse when `CompositionRequest.IsShared` is `true` (ADR-0007); the
scope stores at most one resolved value per `RequestedType`. A second
`IsShared` request for the same type within the same scope returns the
already-stored value instead of resolving again. Name/qualifier-based
sharing is explicitly deferred — `docs/public-api.md`'s own open question
list already flags this as unresolved, and Milestone 2 has no consumer
that needs to disambiguate two shared values of the same type yet (that's
a Milestone 4 `[Shared]`-attribute-on-multiple-parameters question).
Non-shared requests (`IsShared == false`, the default) are never cached in
scope, even if another shared request already resolved the same type —
this keeps ordinary (non-`[Shared]`) resolutions independent by default,
avoiding accidental aliasing.

**Recursion detection.** `CompositionPath` (already referenced by
`CompositionRequest.Path` in ADR-0007) carries the chain of
`RequestedType`s currently being resolved, root to current node. Before a
provider is tried for a request, the context checks whether
`request.RequestedType` already appears in the active ancestor chain
(excluding the request's own node); if so, resolution fails immediately
with a diagnostic showing the full path — matching
`docs/architecture.md`'s diagnostic example format — instead of recursing
until a `StackOverflowException`. This is a hard failure, not a
`NotHandled` result a later-stage provider could still satisfy: an
in-flight cycle is never resolvable by trying more providers.

**CreateMany semantics: Option 1, fresh scope per instance (per the user's
explicit decision).** `CreateMany<T>(count)` is implemented as `count`
independent calls to the same root-composition machinery `Create<T>()`
uses — each item gets its own `CompositionScope`, its own
`CompositionPath` root, and its own forked random stream (ADR-0009). A
`[Shared]`-equivalent value is reused only within one item's graph, never
across two items in the returned collection. Cross-item sharing (e.g. "all
N items should see the same fake clock") is out of scope for `CreateMany`
itself — it would need an explicit outer-scope API, which isn't designed
here because Milestone 2 has no consumer requesting it.

**Exact registrations (scope note).** `docs/mvp.md` lists "Exact
registrations" under both Milestone 2 and Milestone 3 ("Exact type
registrations"). Milestone 2 builds the *resolution-pipeline mechanism*
only — an internal type-keyed registration store the pipeline's "Exact
registrations" stage (stage 3) can query — exercised through an
internal/test-only seam, since there is no public `Composer.Create(builder
=> ...)` configuration surface until Milestone 3 ("Immutable composer
configuration" is explicitly Milestone 3 scope). `docs/mvp.md`'s Milestone
2 exit criterion "Provider precedence is covered by tests" is satisfied
this way — tests exercise the pipeline directly, not through end-user
builder syntax that doesn't exist yet.

### Positive Consequences

- One lifetime, one mental model — matches the architecture doc's
  explicit steer and doesn't force a redesign later if Milestone 4's
  `[Shared]` attribute turns out to need something different (it attaches
  to the same per-root-operation scope either way).
- `CreateMany<T>()`'s "N independent `Create<T>()` calls" semantics is
  the least surprising default and was explicitly confirmed rather than
  assumed.
- Recursion detection reuses `CompositionPath`, already required for
  diagnostics and random forking — no separate bookkeeping structure.

### Negative Consequences

- Deferring name/qualifier-keyed sharing means Milestone 4's `[Shared]`
  attribute can't disambiguate two same-typed shared parameters when it
  arrives — accepted, since no current example in `docs/public-api.md`
  needs that, and it's additive (a qualifier field on `CompositionRequest`
  later) rather than a breaking change to this decision.
- Splitting "exact registrations" into an M2 mechanism + M3 public surface
  means the M2 plan needs an internal test seam that doesn't map directly
  to a `docs/public-api.md` example — worth calling out plainly in the
  Milestone 2 plan so a reviewer doesn't expect `builder.Register(...)` to
  work yet.

## Pros and Cons of the Options

### Scope lifetime — single per-root-operation scope (chosen)

- Good, because it matches the explicit "one clear shared lifetime"
  steer in `docs/architecture.md`.
- Good, because it's the natural unit `CreateMany`'s per-item semantics
  already needs.
- Bad, because Milestone 4 might discover it needs a slightly different
  shape for a per-theory-row scope — mitigated by not being a breaking
  change (a theory row can just be "one root composition operation").

### Scope lifetime — no scope mechanism in M2

- Good, because it's the least work right now.
- Bad, because it contradicts `docs/mvp.md`'s explicit Milestone 2 scope
  list ("Composition scopes," "Shared values" are named M2 deliverables,
  not M4 ones).

### Scope lifetime — full multi-lifetime menu now

- Good, because it covers every lifetime `docs/architecture.md` lists as
  *possible* up front.
- Bad, because it directly contradicts the same doc's explicit steer
  against a general-purpose lifetime system for the MVP.

### Sharing key — type-based only (chosen)

- Good, because it matches every current `docs/public-api.md` example
  (`[Shared] IRepository repository`) exactly.
- Good, because it's the smaller mechanism, deferring a real decision
  (qualifier syntax) until Milestone 4 has a concrete case.
- Bad, because two shared values of the same type can't coexist yet —
  accepted, no current example needs it.

### Sharing key — type + qualifier

- Good, because it's more capable and wouldn't need revisiting later.
- Bad, because it's speculative design with no consumer in Milestone 2 or
  any documented Milestone 4 example.

### CreateMany — fresh scope per instance (chosen)

- Good, because it matches "N independent `Create<T>()` calls," the least
  surprising default, and was explicitly confirmed by the user.
- Good, because two items in a composed list never accidentally share a
  substitute or generated value.
- Bad, because a genuine "N siblings sharing one dependency" scenario
  needs a different, not-yet-designed API — acceptable, no MVP example
  needs it.

### CreateMany — one shared scope across all N

- Good, because it would support siblings sharing infrastructure (e.g.
  same fake clock) without extra API surface.
- Bad, because it's a surprising default that diverges from "N
  independent `Create<T>()` calls," and was explicitly rejected by the
  user for that reason.

## Links

- `docs/architecture.md` — Scopes and Shared Values, Diagnostics sections;
  updated alongside this ADR to record the resolved lifetime.
- [ADR-0007](0007-composition-request-and-provider-pipeline.md) —
  defines `CompositionRequest.Path`/`IsShared`, which this ADR builds on.
- `docs/public-api.md`'s open questions on `Shared` — "Is sharing
  type-based only?" resolved here (yes, for Milestone 2); "Can sharing be
  keyed by name or qualifier?" explicitly deferred to Milestone 4.
- `docs/mvp.md` Milestone 2 and Milestone 3 scope lists — the "exact
  registrations" split this ADR records.
