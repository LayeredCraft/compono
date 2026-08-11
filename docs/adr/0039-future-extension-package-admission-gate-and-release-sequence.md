# [ADR-0039] Future Extension Package Admission Gate and Release Sequence

**Status:** Proposed

**Date:** 2026-08-11

**Decision Makers:** solo

## Context

[Future Packages](../roadmap/future-packages.md) lists four natural
candidate extension packages that aren't designed or committed yet:
additional test-framework integrations (NUnit, MSTest), additional
test-double integrations (FakeItEasy, Moq), and a richer
`Microsoft.Extensions.DependencyInjection` integration. None of these has
a concrete design or a real-demand trigger yet — per
[ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-over-prediction bias, a candidate only becomes real roadmap
content once real demand and a concrete design exist.

That doesn't mean there's nothing to decide yet, though. Two questions are
worth settling ahead of any individual package's own design pass:

1. **Admission criteria** — what should qualify something as a Compono
   extension package at all, so the package set doesn't accumulate entries
   that are little more than branding around an existing library
   (`Compono.TUnit` wrapping TUnit without actually composing anything
   TUnit-specific, for example).
2. **A candidate release order** — if/when these candidates do move
   forward, which one first, and does completing one category (test
   frameworks) before starting another (test doubles) make more sense than
   an arbitrary or demand-driven order.

Recording these as a `Proposed` ADR — rather than only as prose in
`future-packages.md` — gives this proposal a stable, linkable identity and
the decision trail this repo already holds every other design decision to,
without pretending either question is settled yet.

## Decision Drivers

- `docs/manifesto.md`'s bias against scope creep — the package set should
  grow deliberately, not by accretion.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  evidence-over-prediction bias — this ADR proposes criteria and an order,
  it does not commit to building any of these packages; each still needs
  its own real-demand trigger and design pass before implementation
  starts.
- `docs/design-principles.md`'s "modular architecture" principle —
  `Compono.NSubstitute`/`Compono.Bogus` already establish the pattern a
  new integration package follows (register into the core's public
  extension points from the outside); the admission gate below is about
  *whether* a package earns a place in that pattern, not a new pattern
  itself.

## Considered Options

1. No formal admission gate — evaluate each future package candidate
   independently, whenever real demand surfaces, with no standing
   criteria.
2. An admission gate (below) applied to every future candidate, plus a
   candidate release sequence.
3. An admission gate only, with no proposed sequence — let real demand
   alone decide order.

## Decision Outcome

Chosen option: **Option 2** — an admission gate, plus a candidate release
sequence, both explicitly non-binding.

**Admission gate.** Before any future extension package is treated as
real roadmap content, it must clear at least one of:

- Supply composed values, or
- Expose composed values naturally to a test framework, or
- Bridge an established registration system.

A candidate that does none of these is branding around an existing
library, not a Compono extension — it doesn't get a `Proposed` ADR of its
own regardless of demand.

**Candidate sequence, if pursued:**

1. `Compono.TUnit`
2. `Compono.NUnit`
3. `Compono.MSTest`
4. `Compono.FakeItEasy`
5. `Compono.Moq`
6. `Compono.DependencyInjection`

Rationale: complete test-framework coverage before adding another
test-double integration. `Compono.TUnit` first — TUnit's source-generated
test model is the closest architectural match to Compono's own
source-generated composition, and the strongest demonstration that
Compono is meaningfully different from an AutoFixture replacement rather
than another one.

Option 3 was rejected because the sequence itself carries real reasoning
(test-framework coverage before test-double breadth, TUnit's architectural
fit) worth recording even while non-binding — dropping it would lose that
rationale, not just the ordering.

### Positive Consequences

- The admission gate gives every future package proposal a concrete bar
  to clear before its own design pass starts, instead of re-litigating
  "should this be a package at all" each time.
- The sequence records the reasoning behind TUnit-first even though no
  package is committed yet, so a future design pass doesn't have to
  reconstruct it.

### Negative Consequences

- Neither the gate nor the sequence is binding — real demand (per
  ADR-0029) could still reorder or bypass this proposal entirely once a
  specific package's design pass actually starts. This is accepted: the
  point of recording it now is the decision trail, not a commitment.

## Pros and Cons of the Options

### No formal admission gate

- Good, because it defers all criteria to each candidate's own future
  design pass, when more context exists.
- Bad, because without a standing bar, a low-value candidate (a thin
  branding wrapper) could reach a design pass before its lack of value is
  obvious.

### Admission gate + candidate sequence (chosen)

- Good, because it records both the qualification bar and the sequencing
  rationale in one place, with a stable linkable identity.
- Good, because it's explicit about being non-binding, avoiding the
  appearance of a commitment ADR-0029's evidence-over-prediction bias
  would reject.
- Bad, because a sequence proposed this far ahead of any real design pass
  may not survive contact with actual demand.

### Admission gate only

- Good, because it avoids recording an order that might not hold up.
- Bad, because it drops the sequencing rationale (test-framework coverage
  before test-double breadth, TUnit's architectural fit) that's worth
  keeping even in a non-binding form.

## Links

- [Future Packages](../roadmap/future-packages.md) — the roadmap page this
  ADR is proposed from and links back to.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction bias this proposal is scoped not to
  violate.
- [ADR-0024](0024-public-provider-extensibility-model.md),
  [ADR-0025](0025-compono-nsubstitute-package-design.md) — the existing
  extension-point pattern any future package would build on.
