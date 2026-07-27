# [ADR-0004] Composition Plan Discovery and Dispatch

**Status:** Accepted

**Date:** 2026-07-27

**Decision Makers:** solo

## Context

`docs/public-api.md`'s public API is a single generic method —
`composer.Create<Customer>()` — called against a plain type with **no**
attribute or opt-in marker on `Customer` itself. Two questions have to be
answered together before the generator (Milestone 1,
[PLAN-0001](../plans/0001-milestone-1-source-generation-foundation.md))
can be built at all, and they're coupled: whatever the generator
*discovers* needs a plan has to be *reachable* from `Create<T>()` at
runtime without reflection (per
[ADR-0001](0001-source-generation-first.md)) or a `typeof(T)` branch.

1. **Discovery** — how does the generator know which types need a
   generated construction plan, given there's no attribute anchor like
   `dynamo-mapper`'s `[DynamoMapper]` to scan for?
2. **Dispatch** — once a plan exists for `Customer`, how does the generic
   `Create<T>()` call reach `Customer`'s specific generated code?

`dynamo-mapper` (see its `DynamoMapperGenerator`/`ForAttributeWithMetadataName`
pipeline) solves a related-but-different problem: its consumer explicitly
declares a `partial` mapper class and attributes it, so discovery is a
flat attribute scan and dispatch is just "the consumer calls the named
generated method directly" — there's no generic-method-to-generated-code
indirection to solve at all. Compono doesn't have that anchor, so this
decision can't just copy that shape; it has to solve the generic-dispatch
problem `dynamo-mapper` never faced.

## Decision Drivers

- `docs/public-api.md`'s existing examples are zero-attribute — a
  solution that requires decorating every composable type contradicts
  documentation already published, not just a hypothetical goal.
- `docs/manifesto.md`'s "Modular by design" / "Performance is a feature" —
  the dispatch mechanism runs on every `Create<T>()` call, so its
  per-call cost matters, not just its one-time setup cost.
- Must work for **external/library types** Compono doesn't own — a
  solution that requires the composed type to be `partial` (so the
  generator can add members to it) is a non-starter for the common case
  of composing a DTO or entity type defined in a referenced assembly.
- No runtime reflection, no `typeof(T)` branching at the call site
  ([ADR-0001](0001-source-generation-first.md)).

## Considered Options

**Discovery:**
1. Attribute opt-in (`[Composable]` on every composable type, mirroring
   `dynamo-mapper`'s `[DynamoMapper]`).
2. Call-site driven (find `Create<T>()`/`CreateMany<T>()` invocations,
   resolve `T`, recursively walk its constructor parameter types).
3. Hybrid — call-site driven by default, with an optional attribute as an
   escape hatch.

**Dispatch:**
1. `static abstract` interface member on the composed type itself.
2. `JsonSerializerContext`-style explicit consumer-declared context class.
3. Generic-static-field registry (`PlanCache<T>`), populated by a
   generated module initializer.

## Decision Outcome

**Discovery: Option 3, hybrid.** The generator's primary discovery path
walks `Create<T>()`/`CreateMany<T>()` call sites via a syntax-provider
predicate, resolves the closed generic type argument through the semantic
model, and recursively walks that type's constructor parameter types to
build the full transitive closure of types needing a plan — matching
`docs/public-api.md`'s existing zero-attribute examples exactly. An
explicit attribute (name TBD — a Milestone 1 implementation detail, not a
new architectural decision) is available as an escape hatch for a type
that needs a plan generated without a local `Create<T>()` call site in the
same compilation (e.g. a type only ever composed generically from another
assembly).

**Dispatch: Option 3, generic-static-field registry.**
`static class PlanCache<T> { public static ICompositionPlan<T>? Instance; }`
(or equivalent), populated once via a generator-emitted module initializer
per discovered type. `Composer.Create<T>()` reads `PlanCache<T>.Instance`
directly — a closed generic static field is one field per closed generic
type in the CLR, so after the module initializer runs this is a direct
field read, not a dictionary lookup keyed by `typeof(T)`, and it requires
zero ceremony from the consumer and works identically for a type Compono
owns or a type from any referenced assembly.

Together: call-site discovery decides *what* gets a plan; the
`PlanCache<T>` registry is *how* `Create<T>()` finds it, without either
requiring the composed type to be `partial` (ruling out the
`static abstract` option) or requiring a new consumer-visible type in the
public API (ruling out the context-class option).

### Positive Consequences

- Zero required ceremony on a composed type — matches the already-published
  zero-attribute API exactly, and works for external/library types.
- `PlanCache<T>.Instance` is a direct static field read on the hot path
  after initialization — no dictionary lookup, no reflection, no boxing.
- The attribute escape hatch means a genuinely cross-assembly composition
  need doesn't require restructuring the discovery mechanism later — it's
  additive, not a fallback that changes the primary path's behavior.

### Negative Consequences

- Call-site discovery is a materially bigger generator pipeline to build
  than a flat attribute scan (a transitive constructor-parameter walk,
  not a single-pass `ForAttributeWithMetadataName` filter) — accepted as
  the necessary cost of the zero-ceremony requirement.
- A type only ever referenced dynamically (no local `Create<T>()` call
  site anywhere in the compiled graph, and no escape-hatch attribute) has
  no plan generated for it and fails at composition time — this is the
  correct behavior per `docs/architecture.md`'s diagnostic-over-magic
  philosophy, not a gap, but it does mean a consumer hitting this needs to
  understand the escape hatch exists.
- The registry population mechanism (a generated module initializer) is a
  less immediately obvious mechanism to explain/diagnose than "the
  consumer calls a named generated method directly" — worth a clear
  diagnostic message if `PlanCache<T>.Instance` is ever read before
  population, or for a type with no plan at all.

## Pros and Cons of the Options

### Discovery — Attribute opt-in

- Good, because it's the simplest possible generator pipeline (flat
  attribute scan, proven by `dynamo-mapper`).
- Bad, because it requires boilerplate on every composable type.
- Bad, because it directly contradicts `docs/public-api.md`'s existing
  zero-attribute examples.

### Discovery — Call-site driven

- Good, because it requires zero ceremony on composed types.
- Good, because it matches the already-published API exactly.
- Bad, because it's a materially bigger pipeline (transitive graph walk,
  not a flat scan).
- Bad, because a type never reached from any `Create<T>()` call site in
  the compiled graph gets no plan at all, with no escape hatch on its own.

### Discovery — Hybrid (chosen)

- Good, because it keeps the zero-ceremony default while still covering
  the cross-assembly/no-local-call-site case.
- Good, because the escape hatch is purely additive — doesn't change the
  primary path's behavior or performance.
- Bad, because it's two discovery mechanisms to build and document
  instead of one (mitigated by the attribute being a genuinely rare
  escape hatch, not a parallel primary path).

### Dispatch — `static abstract` interface member

- Good, because it's the theoretically lowest-overhead option (fully
  static dispatch, no registry, no module initializer).
- Bad, because it requires the composed type to be declared `partial` —
  breaks entirely for any external/library type, which is most of what
  `Create<T>()` needs to support.

### Dispatch — `JsonSerializerContext`-style context class

- Good, because it's a proven pattern for exactly this problem (System.Text.Json
  solves the identical generic/reflection-avoidance problem this way).
- Good, because it works for external types (the context class, not the
  composed type, carries the generated members).
- Bad, because it introduces a new required consumer-visible type not
  present in any current `docs/public-api.md` example, and changes the
  shape of `Create<T>()` from "just call it" to "declare and construct a
  context first."

### Dispatch — Generic-static-field registry (chosen)

- Good, because it requires zero new consumer-visible types.
- Good, because it works identically for Compono-owned and external types.
- Good, because a closed generic static field read is effectively free
  after first population — no dictionary, no boxing.
- Bad, because the registration mechanism (generated module initializer)
  is a step removed from "just call the generated method," making it a
  less obvious mechanism to explain than the rejected alternatives.

## Links

- `docs/public-api.md` — the zero-attribute `Create<T>()`/`CreateMany<T>()`
  examples this decision is grounded in.
- `docs/architecture.md`'s "Open Architectural Decisions" —
  "Whether generated plans are required for external types" is resolved
  by this ADR (they are not required to be `partial`/internally owned;
  the registry dispatch mechanism works for any type).
- [ADR-0001](0001-source-generation-first.md) — no-reflection-by-default,
  the constraint this decision has to satisfy.
- [PLAN-0001](../plans/0001-milestone-1-source-generation-foundation.md) —
  the milestone this decision unblocks.
- Prior art: `dynamo-mapper`'s `DynamoMapperGenerator` (attribute-driven
  discovery, considered and rejected for Compono's zero-attribute API);
  `System.Text.Json`'s `JsonSerializerContext` (considered and rejected
  dispatch precedent for the same generic-dispatch problem).
