# [ADR-0007] Composition Request and Synchronous Provider Pipeline

**Status:** Superseded by ADR-0010

**Date:** 2026-07-28

**Decision Makers:** solo (sync-vs-async fork confirmed with user)

## Context

Milestone 1 shipped a deliberately minimal placeholder: `ICompositionContext.Resolve<TValue>(Nullability nullability)`
and a synchronous `ICompositionPlan<T>.Compose(ICompositionContext context)`.
Milestone 2 ("Core Composition Engine" per `docs/mvp.md`) has to replace the
placeholder with the real thing: a rich `CompositionRequest`, the
`ICompositionProvider` pipeline, and the fixed 8-stage resolution
precedence `docs/architecture.md` already documents (explicit values →
shared/scoped → exact registrations → profile rules → semantic providers →
test-double providers → built-in providers → generated plans → diagnostic
failure).

`docs/architecture.md`'s original conceptual sketch is fully async
(`ValueTask<T> ResolveAsync`, `ICompositionProvider.TryComposeAsync`), and
`references/coding-standards.md` already has language assuming that shape
(a `ValueTask` naming wrinkle carved out specifically for
`TryComposeAsync`/`ResolveAsync`). But the Milestone 1 code that's actually
`Accepted` and shipped is synchronous. Before any of the pipeline internals
can be designed, this contradiction has to be resolved one way, because it
decides the signature of every provider, the scope, `CreateMany<T>()`, and
the generated-plan contract for the rest of the MVP.

## Decision Drivers

- Milestone 1's `ICompositionPlan<T>.Compose` signature is already
  `Accepted` and shipped — changing it now is a breaking change to a
  decision made one milestone ago, not a green-field choice.
- Every provider planned for the MVP (`docs/mvp.md`'s built-in types,
  exact registrations, Bogus semantic values, NSubstitute test doubles) is
  in-memory/CPU-bound. Nothing in the MVP scope does I/O during
  resolution.
- `README.md`'s "Performance is a feature" / manifesto goals favor avoiding
  an async state-machine allocation on a path that runs once per resolved
  member of every composed object, when nothing on that path needs to
  suspend.
- `ADR-0001` (no reflection by default) requires `CompositionRequest`
  construction to be emittable by the generator without runtime
  reflection — it has to be a plain data shape the generator can construct
  with object-initializer syntax.

## Considered Options

1. Fully async (`ValueTask<T>` throughout) — `docs/architecture.md`'s
   original sketch.
2. Fully synchronous, with no async escape hatch anywhere in the core
   contract.
3. Synchronous core now; if a genuine async provider need appears later
   (e.g. a network-backed semantic data source), add it as a **distinct,
   separately opted-into** contract rather than reworking or contaminating
   the synchronous hot path.

## Decision Outcome

**Option 3.** `ICompositionContext.Resolve<TValue>(CompositionRequest request)`
and `ICompositionProvider.TryCompose(CompositionRequest request, ICompositionContext context)`
are both synchronous, returning `TValue` and `CompositionResult`
respectively — no `Task`/`ValueTask` anywhere in the core resolution path.
`ICompositionPlan<T>.Compose`'s existing signature is unchanged. Providers
are non-generic and dispatch on `CompositionRequest.RequestedType` (a
runtime `Type` comparison/pattern-match, which is ordinary type-directed
logic, not the reflection-based construction `ADR-0001` excludes);
`CompositionResult` carries a boxed `object?` for a successful value, with
`ICompositionContext.Resolve<TValue>` responsible for the unbox/cast back
to `TValue` at the generic boundary the generated plan calls into.

Synchronous composition must never block on an asynchronous provider — if
an async provider contract is added later, it gets its own opt-in
resolution path (a separate `ICompositionContext`-like entry point, not a
`.Result`/`.GetAwaiter().GetResult()` bridge bolted onto this one), so this
decision doesn't quietly reopen into a "sync-over-async" trap later. That
future contract is explicitly out of scope here.

`CompositionRequest` (a `sealed record`, per `coding-standards.md`'s DTO
guidance — required properties, not positional construction, so the
generator's emitted call sites don't need to track field order) carries
only the fields Milestone 2 has an actual consumer for:

- `RequestedType` (`Type`)
- `Nullability` (existing M1 type)
- `Name` / `Member` / `DeclaringType` — diagnostic and future
  Bogus-member-convention context
- `Path` (`CompositionPath`) — the current ancestor chain, needed for
  recursion detection (ADR-0008) and random forking (ADR-0009)
- `IsShared` (`bool`) — whether this request participates in scope reuse
  (ADR-0008)

`CustomAttributes`, `GenericContext`, `RequestedLifetime`, `SemanticHints`,
and "whether a test double is acceptable" from `docs/architecture.md`'s
full conceptual request are **not** added yet — nothing in Milestone 2
consumes them (they're Milestone 4/5/6 concerns: xUnit inline-value
precedence, NSubstitute eligibility, Bogus semantic hints). Adding them
now would be speculative. `CompositionRequest` can grow additively later
without breaking existing generated call sites, because required
properties are set by name, not position.

The pipeline itself is a fixed, non-extensible-in-ordering sequence
internal to `CompositionContext` — providers are tried in the 9-stage
order `docs/architecture.md` already specifies, stopping at the first
non-`NotHandled` result; a provider never sees or reorders the list of
other providers.

### Positive Consequences

- No async ceremony anywhere on the hot path; matches what Milestone 1
  already shipped, so no breaking change to `ICompositionPlan<T>`.
- `CompositionResult`'s `NotHandled`/`Success`/`Failure` shape (already
  documented in `docs/architecture.md`) fits naturally as a `switch`
  expression per `coding-standards.md`'s pattern-matching guidance.
- `CompositionRequest`'s trimmed field set avoids designing metadata with
  no consumer yet — later milestones add fields as they need them instead
  of guessing shapes now.

### Negative Consequences

- `docs/architecture.md`'s original async sketch and
  `coding-standards.md`'s `ValueTask`/`TryComposeAsync` carve-out are now
  stale and need correcting in the same change that accepts this ADR (see
  Links) — otherwise the docs actively mislead the next contributor.
- A genuinely async provider (not needed anywhere in the current MVP
  scope) requires a second, separately designed contract later rather
  than fitting into this one — accepted as the cost of not paying async
  overhead speculatively.

## Pros and Cons of the Options

### Fully async

- Good, because it matches the original architectural sketch and leaves
  room for an async provider without a second contract.
- Bad, because nothing in the MVP scope needs it — every planned provider
  is CPU-bound/in-memory.
- Bad, because it breaks Milestone 1's already-`Accepted`,
  already-shipped `ICompositionPlan<T>.Compose` signature.
- Bad, because it puts an async state machine on a path that runs once
  per resolved member of every composed object.

### Fully synchronous, no future escape hatch

- Good, because it's the simplest possible contract and matches Milestone
  1 exactly.
- Bad, because it never revisits the question — if a real async need
  shows up later (unlikely in-scope, but not impossible), there's no
  documented plan for how it gets added without either breaking the sync
  contract or reaching for a sync-over-async hack.

### Synchronous core, distinct future async contract if needed (chosen)

- Good, because it matches Milestone 1's shipped contract with zero
  breaking change.
- Good, because it names the escape hatch explicitly, so a future async
  need has a documented shape to reach for instead of improvising a
  sync-over-async bridge under deadline pressure.
- Bad, because it's a decision about a hypothetical — mitigated by
  keeping the future contract completely unspecified here rather than
  over-designing it now.

## Links

- `docs/architecture.md` — Resolution Pipeline, Providers, Composition
  Requests sections; updated alongside this ADR to drop the `ValueTask`/
  `Async` sketch.
- `references/coding-standards.md` — the `ValueTask` naming wrinkle and
  error-handling section's `ResolveAsync`/`TryComposeAsync` references,
  corrected alongside this ADR.
- [ADR-0001](0001-source-generation-first.md) — no-reflection-by-default,
  satisfied by `CompositionRequest` being a plain generator-constructible
  record.
- [ADR-0008](0008-composition-scope-shared-values-and-recursion-detection.md) —
  builds on `CompositionRequest.Path`/`IsShared` defined here.
- [ADR-0009](0009-deterministic-seed-and-forkable-random-source.md) —
  builds on `CompositionRequest.Path` for random forking keys.
- `docs/mvp.md`'s "Open Decisions Before Implementation" — resolves "Sync
  or async provider APIs."
