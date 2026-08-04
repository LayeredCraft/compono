# [ADR-0011] Composition Scope, Shared Values, and Recursion Detection

**Status:** Accepted

**Date:** 2026-07-28

**Decision Makers:** solo (recursion-timing fork confirmed with user)

## Context

[ADR-0008](0008-composition-scope-shared-values-and-recursion-detection.md)
settled scope lifetime (one `CompositionScope` per root composition
operation), sharing key (type-based only), and `CreateMany` semantics
(fresh scope per item) — all of which remain correct and are carried
forward unchanged by this ADR. But its recursion-detection design —
"check the ancestor chain before trying any provider" — was flagged in a
design review as too blunt: it would reject a graph that an explicit
value, a shared value, or a registration could have legitimately
terminated (e.g. a self-referencing node type where the recursive
dependency is satisfied by a registered/shared instance rather than by
recursively constructing a new one).

This ADR supersedes ADR-0008's recursion-detection content only. Scope
lifetime, sharing key, and `CreateMany` semantics are restated here
without change, so this ADR is a complete, self-contained record of the
whole scope/sharing/recursion topic rather than requiring a reader to
cross-reference a superseded document for half of it.

## Decision Drivers

- Registrations, explicit values, and shared values are all supposed to
  be *terminating* mechanisms — the whole point of registering a fake for
  an interface, or sharing an instance, is to stop the graph from
  recursing further at that point. A recursion check that fires before
  those stages get a chance defeats their purpose for exactly the graphs
  where they'd matter most.
- `docs/architecture.md`'s diagnostic philosophy: "Recursive graphs fail
  clearly" — the failure has to explain *which* request edges formed the
  cycle, not just list repeated types with no structural context.
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)
  already distinguishes `CompositionPath` (every request edge, kept for
  diagnostics and random forking) from a narrower recursion concept — this
  ADR has to define that narrower concept precisely rather than reuse
  `CompositionPath` for both jobs.
- The mechanism should generalize past "generated plans are the only
  thing that can recurse" — a future authoritative provider that itself
  performs recursive composition (hypothetically, Milestone 5/6) should
  be able to participate in the same cycle-detection machinery instead of
  each provider inventing its own.

## Considered Options

**Recursion detection timing:**
1. Before every resolution request, unconditionally (ADR-0008's original
   design).
2. Only immediately before generated-plan expansion / structural
   construction — after explicit/shared/registration stages have already
   had a chance to terminate the graph.
3. After explicit/shared/registration stages, but as a blanket check on
   every remaining request (not scoped specifically to structural
   construction).

**Recursion bookkeeping:**
1. Reuse `CompositionPath` directly for cycle detection (check if the
   requested type already appears anywhere in the path).
2. A distinct "active construction frame" stack, separate from
   `CompositionPath`, pushed/popped only around structural construction.

## Decision Outcome

**Timing: Option 2.** Recursion is checked only immediately before a
structural construction operation begins — in Milestone 2, that means
exactly one place: generated-plan dispatch (stage 8,
[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)).
Stages 1–7 (explicit values, shared/scoped values, exact registrations,
profile rules, semantic providers, test-double providers, built-in
providers) all get a chance to satisfy a request — and legitimately
terminate a cycle — before recursion is ever checked. A registered fake
for a self-referencing node type resolves it at stage 3, never reaching
stage 8, never touching the recursion mechanism at all.

**Bookkeeping: Option 2, a distinct active-construction-frame stack.**
Two separate concepts, kept deliberately apart:

- **`CompositionPath`** ([ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md))
  records *every* request edge for the life of one root composition
  operation — used for diagnostics and random forking. It grows and
  shrinks with every `Resolve<T>()` call, shared/registered or not.
- **Active construction frames** — an internal stack on `CompositionContext`,
  pushed only when a structural construction operation is about to begin
  (generated-plan dispatch today; designed as a general internal boundary
  so a future authoritative provider that performs recursive composition
  can participate in the same mechanism, not hardcoded to "generated
  plans" specifically). Each frame records the type under construction.
  Before stage 8 invokes `PlanCache<T>.Instance.Compose(context)`, the
  context checks whether `typeof(T)` already has an active frame; if so,
  this is an authoritative recursion failure (`CompositionResult`'s
  reserved-for-authoritative-stages `Failure`, per
  [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)),
  carrying the chain of active frames (the request edges that formed the
  cycle) rather than just the repeated type. If not, the frame is pushed,
  `Compose` is invoked, and the frame is popped in a `finally` block —
  symmetric with `CompositionPath`'s own push/pop, and equally immune to
  a caller forgetting to clean up, since both live entirely inside
  `CompositionContext.Resolve<T>`'s own call frame.

A repeated *type* in `CompositionPath` (e.g. two sibling properties of
the same type, or a `List<Customer>` containing another `Customer`
reference elsewhere) is not, by itself, a cycle — it's ordinary graph
shape. Only a type whose *construction* is still actively in progress
(an active frame) constitutes a genuine cycle. This is exactly why the
two concepts can't be collapsed into one: `CompositionPath` alone (ADR-
0008's original design) can't distinguish "this type appears twice in an
acyclic graph" from "this type's construction is recursively calling
itself," and conflating them is what produced the original false-positive
risk.

**Scope lifetime, sharing key, and `CreateMany` semantics (carried
forward from ADR-0008, unchanged):**

- One `CompositionScope` per root composition operation (one `Create<T>()`
  call, or one item of a `CreateMany<T>()` call — each item gets its own
  independent scope).
- Sharing is type-keyed only for Milestone 2 (`CompositionScope` stores at
  most one value per `RequestedType`, populated only by a request whose
  `IsShared` is `true`); name/qualifier-based sharing remains deferred to
  Milestone 4.
- `CreateMany<T>(count)` is `count` independent root composition
  operations — no cross-item scope reuse.
- "Exact registrations" in Milestone 2 is the pipeline mechanism only (an
  internal type-keyed store, exercised via an internal test seam) — the
  public `builder.Register(...)` surface is Milestone 3.

### Positive Consequences

- Registrations/shared/explicit values work exactly as a consumer would
  expect for terminating a self-referencing graph — no false-positive
  recursion failures for a graph that's actually resolvable.
- The active-construction-frame concept is general enough that a future
  authoritative provider performing recursive composition doesn't need
  its own bespoke cycle-detection mechanism.
- The diagnostic for a genuine cycle can report the actual chain of
  frames that formed it, not just "type X appeared twice."

### Negative Consequences

- Two bookkeeping structures (`CompositionPath` and the frame stack)
  instead of one is more state for `CompositionContext` to own and keep
  synchronized correctly — mitigated by both being scoped to the same
  push-on-entry/pop-in-finally pattern around the same `Resolve<T>` call
  structure, so they can't drift independently.
- A request that never reaches stage 8 (fully satisfied by an earlier
  stage) never gets cycle-checked at all — this is intentional (an
  earlier stage terminating the graph is exactly the case this ADR exists
  to allow), but it does mean recursion detection is not a general
  graph-validity check, only a structural-construction guard.

## Pros and Cons of the Options

### Timing — before every request (ADR-0008's original)

- Good, because it's the simplest possible rule and catches every cycle,
  structurally resolvable or not.
- Bad, because it rejects graphs a registered/shared/explicit value could
  have legitimately terminated — the false positive this ADR exists to
  fix.

### Timing — after explicit/shared/registration, before structural construction (chosen)

- Good, because it lets exactly the mechanisms designed to terminate a
  graph do so, before recursion is ever considered.
- Good, because it scopes the check to the only place unbounded
  recursion can structurally occur in Milestone 2 (generated-plan
  dispatch), keeping the mechanism narrow and easy to reason about.
- Bad, because it requires distinguishing "structural construction" from
  "ordinary resolution" as a concept — more design surface than a blanket
  check.

### Timing — after explicit/shared/registration, but blanket on all remaining requests

- Good, because it still lets terminating stages go first.
- Bad, because it checks scalar/collection-element requests that can
  never structurally recurse on their own (a `string` property can't
  cause a cycle), adding bookkeeping overhead with no corresponding
  benefit over scoping the check to stage 8 specifically.

### Bookkeeping — reuse `CompositionPath`

- Good, because it's one less structure to maintain.
- Bad, because it can't distinguish "repeated type in an acyclic graph"
  from "type under active construction" — conflating diagnostics/forking
  concerns with cycle detection is exactly what produced ADR-0008's
  original false-positive risk.

### Bookkeeping — distinct active-construction-frame stack (chosen)

- Good, because it cleanly separates "every request edge" (path) from
  "what's currently being structurally constructed" (frames) — each
  structure does one job.
- Good, because it generalizes past generated plans to any future
  authoritative construction mechanism.
- Bad, because it's a second stack to keep correctly synchronized with
  the call structure — mitigated by both living inside the same
  `Resolve<T>` recursive call frame.

## Amendment (2026-07-28): internal test seam extends to seed injection

A final pre-implementation review of PLAN-0002 asked how Phase 1's
determinism tests and Phase 4's `CreateMany` stability/end-to-end tests
assert "same seed → same output" when there is no public seed-configuration
API in Milestone 2 (`WithSeed(...)` is Milestone 3). The answer is the
same internal-test-seam principle this ADR already established for exact
registrations, extended to cover the root seed too:

```csharp
internal static Composer CreateWithSeed(CompositionSeed seed);
```

An `internal` factory overload alongside the public `Composer.Create()`,
reachable only from `Compono.Tests` via `InternalsVisibleTo` — the exact
same mechanism, and the exact same reasoning (`coding-standards.md`:
"use `InternalsVisibleTo`... don't widen a member to `public` just so a
test can reach it"), already applied to the registration store. This is
the one seam PLAN-0002's Phase 1/4 determinism and `CreateMany`-stability
tests exercise the real `Composer`/`CompositionContext` flow through,
rather than testing `CompositionSeed`/`IRandomSource` in isolation from
`Composer` entirely.

## Amendment 2 (2026-07-28): seam naming, `CreateMany` contract, and authoritative validation

A final pre-Phase-0 review of PLAN-0002 found the seam above ambiguous
and two authoritative-stage behaviors undefined:

**1. `CreateWithSeed` renamed and split — it was ambiguous whether the
seed configured a reusable `Composer`, one root operation, or a
`CreateMany` batch.** Milestone 2 has no persistent, reusable `Composer`
configuration at all (`Composer.Create()` takes no builder until
Milestone 3) — the seed is inherently scoped to **one composition call**,
never to a `Composer` instance's future calls. The single ambiguous
factory above is replaced with two explicitly-scoped internal methods,
each unambiguous about what it seeds:

```csharp
internal T CreateRootForTesting<T>(CompositionSeed seed);
internal IReadOnlyList<T> CreateManyForTesting<T>(int count, CompositionSeed seed);
```

`CreateRootForTesting<T>` performs exactly one root composition operation
(what `Create<T>()` does) with an explicit seed instead of a
freshly-generated one. `CreateManyForTesting<T>` performs one
`CreateMany<T>(count)` batch with an explicit **batch root** seed (each
item still forks from it per
[ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)).
Neither configures anything beyond its own single call — there is no
seeded `Composer` instance these return, only a seeded result. Both stay
`internal`, reachable only from `Compono.Tests` via `InternalsVisibleTo`,
same as the registration seam.

**2. `CreateMany<T>(count)` argument and return contract, stated
explicitly.** `count < 0` throws (`ArgumentOutOfRangeException.ThrowIfNegative(count)`,
matching `coding-standards.md`'s guard-clause convention). `count == 0`
returns an empty, materialized, non-null collection — not a thrown
exception, not `null`. The return type is `IReadOnlyList<T>`: per
`coding-standards.md`'s "Collection types on API surfaces" guidance
("Use `IReadOnlyList<T>`... when the caller needs indexed access... 
without being able to mutate"), and because `CreateMany` is inherently an
eager, fully-materialized batch (every item is deterministically resolved
up front, per this ADR's "fresh scope per item" — there's no lazy/deferred
evaluation semantics to preserve, so `IEnumerable<T>` would understate
what's actually returned and risk re-enumeration concerns that don't
apply here).

**3. Authoritative null/type validation for shared values and exact
registrations.** Stages 2 (shared/scoped) and 3 (exact registrations,
[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md))
are context-owned authoritative stages — per ADR-0010's Failure
semantics, invocation of one implies exclusive ownership of the request.
This ADR did not previously state what happens when the value they
produce is invalid. Made explicit now: if a shared/scoped value or a
registration-produced value is `null` for a request whose `Nullability`
is not-null, or its runtime type is not assignable to
`CompositionRequest.RequestedType`, that is an **authoritative `Failure`**
at that stage — never silently treated as `NotHandled` and passed through
to a later stage (profile rules, built-ins, or a generated plan would
either mishandle a mismatched type or produce a confusingly-unrelated
diagnostic instead of the actual problem: "the registration/shared value
for this type is invalid"). This is a direct application of ADR-0010's
existing rule ("`Failure` means authoritative ownership was established,
but resolution could not complete"), not a new failure category —
stated explicitly here because stages 2/3's validation behavior was
previously left implicit.

## Links

- Supersedes [ADR-0008](0008-composition-scope-shared-values-and-recursion-detection.md)
  (kept as a historical record; its scope/sharing/`CreateMany` decisions
  are restated here unchanged).
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  stage 2/3 (scope/registrations), stage 8 (generated-plan dispatch, the
  recursion checkpoint), and the authoritative-`Failure` semantics a
  detected cycle reports through.
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  `CompositionPath`, kept distinct from the frame stack defined here.
- [ADR-0004](0004-composition-plan-discovery-and-dispatch.md) —
  `PlanCache<T>` dispatch, the structural-construction operation this
  ADR's frame check gates.

## Amendment 3 (2026-08-04): dogfooding confirms fail-fast recursion detection, zero real-world exercise either way

Milestone 7's dogfooding pass (`ncipollina/cosmere-tracker`, per
[ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md),
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md), gap 3 of
[RESEARCH-0001](../research/0001-autofixture-comparison.md)) compared this
ADR's fail-fast construction-cycle detection against AutoFixture's
`OmitOnRecursionBehavior` (silently omit a value on a cycle rather than
failing) — the migrated project's own `BaseFixtureFactory` had opted into
that AutoFixture behavior. **No construction-cycle failure was ever
triggered during the migration**, in either direction: none of
`cosmere-tracker`'s composed types form a self-referencing object graph
(edge entities reference other entities by string id, not by object
reference), so this ADR's fail-fast diagnostic was never exercised,
positively or negatively, by real test code.

**No change to this ADR's decision.** Per ADR-0029's evidence-driven
restraint, an unexercised gap isn't grounds for revisiting a design
decision — this migration produced no case where AutoFixture's
silent-omission alternative would have been needed or missed, so there's
no evidence pointing toward reintroducing an opt-in recursion-behavior
switch. This Amendment exists to record that absence as real evidence
(a genuine "not exercised" result, not a gap this ADR failed to
consider), per the dossier's `Compono.Bogus` precedent that a
negative/neutral finding is itself a valid, recorded outcome.
