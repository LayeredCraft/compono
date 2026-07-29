# [ADR-0015] Provider Identity Deferred in `ProviderAttempt`

**Status:** Superseded by [ADR-0016](0016-provider-identity-restored-in-provider-attempt.md)

**Date:** 2026-07-29

**Decision Makers:** solo

## Context

[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
Diagnostics tracing decision describes the trace buffer's per-attempt
record as "a compact `ProviderAttempt` struct (stage id, provider id,
outcome enum — no strings, no reflection metadata, no diagnostic-object
allocation)". Milestone 2 Phase 4's implementation shipped
`ProviderAttempt` as `(PipelineStage Stage, CompositionAttemptOutcome
Outcome)` — no provider-id field — a real, intentional deviation from
that literal wording, recorded in
[PLAN-0002](../plans/0002-milestone-2-core-composition-engine.md)'s
Phase 4 notes at implementation time, not discovered after the fact.

A PR #13 review (Codex) correctly flagged the deviation against ADR-0010's
text and additionally warned that `ProviderAttempt` being a positional
`readonly record struct` makes adding a provider-id field later a breaking
change to its constructor/deconstruction contract — a real cost, not a
free option to defer indefinitely.

This ADR exists because that specific concern — should `ProviderAttempt`
carry provider identity now — is itself a decision `docs/adr/README.md`'s
own numbering/status rules say belongs in its own record, not folded back
into ADR-0010's text. [Design-decisions.md](../../.claude/skills/engineering-workflow/references/design-decisions.md)
is explicit that an accepted ADR's Decision/Rationale/Consequences don't
get edited in place, even for a narrow sub-decision — a rule this same
plan's Phase 2 work (see [ADR-0014](0014-generator-emitted-collection-plans.md)'s
own Context) already established a house pattern for: extract the
sub-decision into its own numbered ADR, and leave the original ADR's text
untouched, gaining only a `Links` pointer.

## Decision Drivers

- No stage in Milestone 2 (or any milestone scoped through Milestone 6)
  registers more than one competing provider yet —
  `docs/architecture.md`'s own Resolution Pipeline section already states
  this plainly ("no stage has more than one competing provider to order").
  A provider-id field has nothing to discriminate against today.
- `coding-standards.md`'s "don't design for hypothetical future
  requirements" principle: inventing an identity scheme (an integer index
  into a stage's provider list? a type name, which the ADR-0010 "no
  strings" rule already rules out? an opaque registration token that
  doesn't exist yet?) with zero real consumers risks shipping the *wrong*
  shape and having to break it again once a real second provider exists —
  worse than deferring the field until the identity scheme has a genuine
  answer.
- `ProviderAttempt` is `public` (`CompositionDiagnostic.Trace`'s element
  type) — any change to its shape is a compatibility decision, which
  raises the bar for adding a field speculatively, not lowers it.
- The trace buffer's own allocation-free-on-success budget
  ([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)):
  a wider `ProviderAttempt` struct (an `int`/token field) is one more
  `Array.Resize` byte-width increase, a real if small cost to accept
  speculatively.

## Considered Options

1. **Defer provider identity until a stage has a real second provider**
   (chosen) — ship `ProviderAttempt` as `(Stage, Outcome)` now; add
   provider identity in a later milestone (3/5/6, whichever first
   registers competing providers in one stage) once a concrete identity
   scheme has a real consumer to design against.
2. **Add an `int ProviderIndex` now** (the provider's position within its
   stage's `IReadOnlyList<ICompositionProvider>`) — cheap to add today,
   but not a stable identity: reordering a stage's provider registration
   (a real, expected operation once Milestone 3's builder lets a consumer
   register/reorder providers) silently changes what an already-recorded
   trace's index means, with no way to detect that a diagnostic was
   captured against a different provider order than the one currently
   registered.
3. **Add an opaque provider-identity token now**, requiring
   `ICompositionProvider` to expose a stable identity (a `Guid`, a
   `Type`-based key, or similar) — the most durable option, but invents
   both a new member on `ICompositionProvider` (a public contract change
   to an `internal` interface with no current implementers needing
   identity) and a token-allocation/uniqueness scheme, entirely to serve
   a field nothing reads yet.

## Decision Outcome

Chosen option: "Defer provider identity until a stage has a real second
provider" (Option 1). `ProviderAttempt` stays `(PipelineStage Stage,
CompositionAttemptOutcome Outcome)` through Milestone 2. Options 2 and 3
each solve a problem that doesn't exist yet (multiple providers actually
competing within one stage) by guessing at a shape now — Option 2's index
scheme is actively misleading once providers become reorderable, and
Option 3 pre-designs an `ICompositionProvider` identity contract with zero
real callers, the exact "hypothetical future requirement" this repo's
standards call out as a design smell.

### Positive Consequences

- `ProviderAttempt` stays the minimal shape ADR-0010's "no strings, no
  reflection metadata" budget wants, with no speculative field.
- The eventual provider-id shape gets designed against a real Milestone
  3/5/6 consumer's actual identity needs, not guessed at now.

### Negative Consequences

- Adding provider identity later is a breaking change to
  `ProviderAttempt`'s positional constructor/deconstruction contract —
  accepted as the cost of not guessing wrong now, per Considered Options
  above. `Compono` is pre-1.0 during Milestone 2, so this cost is lower
  today than it will be after a stable release.
- Until that later change ships, a trace with two `NotHandled` entries
  for the same stage (once a stage genuinely has competing providers)
  can't distinguish *which* providers those were — acceptable today since
  no stage has more than one provider to distinguish between.

## Pros and Cons of the Options

### Option 1 — Defer provider identity

- Good, because it doesn't guess at an identity scheme with no consumer.
- Good, because it keeps `ProviderAttempt` at ADR-0010's minimal budget.
- Bad, because the eventual addition is a breaking change to a public
  positional record's contract.

### Option 2 — Provider index now

- Good, because it's cheap to add immediately.
- Bad, because a stage's provider order isn't yet a stable identity
  (Milestone 3's builder will make providers reorderable), so an index
  captured today can silently mean something different once configuration
  exists.

### Option 3 — Opaque provider-identity token now

- Good, because it would be durable across reordering.
- Bad, because it requires a new `ICompositionProvider` member with zero
  current implementers needing it, entirely speculative.

## Links

- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  gains a `Links` entry pointing here; its Diagnostics tracing text
  (including "stage id, provider id, outcome enum") is left unedited, per
  the "an accepted ADR is a historical record" rule this ADR's own Context
  describes ADR-0014 as having already established the house pattern for.
- [PLAN-0002](../plans/0002-milestone-2-core-composition-engine.md) —
  Phase 4's implementation notes, where this deviation was first recorded
  at implementation time.
- `docs/architecture.md` — Resolution Pipeline section, "no stage has more
  than one competing provider to order" (the fact this decision rests on).
