# [ADR-0029] Milestone 7 Dogfooding Strategy and Capability-Gap Decision Framework

**Status:** Accepted

**Date:** 2026-08-02

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

`docs/mvp.md`'s Milestone 7 scope has always said "select one existing
real-world project, rewrite its tests using Compono, record missing
capabilities" — but left *how* to decide what a "missing capability"
becomes undesigned. Dogfooding has already started informally: Compono has
been compared against `ncipollina/cosmere-tracker`'s existing
AutoFixture-based test kit (`test/Cosmere.Tracker.TestKit`), and that
comparison surfaced three concrete candidate capability gaps, each with a
real call site in that project's `BaseFixtureFactory`/
`CosmereTrackerCustomization`:

1. **Hidden shared values.** AutoFixture's `Freeze<T>()` lets an
   infrastructure object participate in composition without appearing in
   every test method's signature.
   `HttpClientSpecimenBuilder.Create` resolves a frozen
   `HttpMessageHandler` by type from `ISpecimenContext` — it never appears
   as a parameter anywhere. Compono's equivalent today is an explicit
   `[Shared]` parameter ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md),
   [ADR-0022](0022-compono-xunit-package-design.md)) — correct, but it
   exposes an implementation detail (that a value is shared, and its type)
   in every test signature that needs it.
2. **NSubstitute `ConfigureMembers`.** `BaseFixtureFactory` applies
   `AutoNSubstituteCustomization { ConfigureMembers = true }` — every
   generated substitute has its members auto-configured (return values,
   nested substitutes) recursively. Compono's `Compono.NSubstitute`
   ([ADR-0025](0025-compono-nsubstitute-package-design.md)) deliberately
   returns a bare `Substitute.For<T>()` and never calls
   `context.Resolve<T>()` against it — an explicit non-goal
   ("recursive auto-configuration of substitute members") at the time.
3. **Recursion behavior.** `BaseFixtureFactory` removes
   `ThrowingRecursionBehavior` and adds `OmitOnRecursionBehavior` —
   AutoFixture silently omits a value on a construction cycle rather than
   failing. Compono detects a genuine construction cycle and reports a
   clear, path-annotated failure
   ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)).

Per `docs/manifesto.md`: "Compono is not intended to reproduce AutoFixture
feature-for-feature." None of these three should be assumed into the
roadmap just because AutoFixture has them and a real project happens to use
them — the milestone's job is to determine, from evidence, whether each one
genuinely improves Compono's API, readability, predictability, and
developer experience, or whether Compono's current, more explicit behavior
is the better long-term answer. This ADR settles the *process* for
answering that question — for these three gaps and any further ones the
migration surfaces — not the answer itself for any of them; each gap's
actual outcome is recorded separately (see Decision Outcome's "Where
outcomes get recorded").

## Decision Drivers

- `docs/manifesto.md`'s explicit non-goal of AutoFixture feature parity —
  the framework must make "should we add this" a real question with a
  real "no" available as an outcome, not a rubber stamp toward parity.
- The investigation must be grounded in a real migration, not synthetic
  examples invented to justify a predetermined answer — evidence has to be
  falsifiable (a gap that turns out not to matter in practice must be
  allowed to end in "no change").
- This repo's existing decision-record conventions
  (`design-decisions.md`'s ADR/Amendment/research-doc mechanics) should be
  reused, not reinvented, for recording each gap's outcome.
- `docs/adr/0001-source-generation-first.md`'s no-hidden-reflection-by-
  default posture and this repo's explicit-over-implicit bias are real
  constraints a "yes, add it" outcome has to survive, not just a
  cost/benefit tally.
- The three gaps above are the known starting set, not a closed list — the
  real migration may surface others; the framework has to generalize.

## Considered Options

### Relationship between the real migration and the three (or more) gap decisions

1. **Migration-driven evidence only.** Migrate `cosmere-tracker`'s test kit
   fully; each gap's decision is made only from what that migration
   actually shows (frequency of the friction, cost of the explicit
   workaround in that codebase). No synthetic spikes.
2. **Decoupled synthetic spikes, then migration.** Resolve each of the
   three gaps early via small, isolated synthetic examples (fast), then do
   the full migration afterward mainly to validate readability/
   performance rather than to decide the gaps.

### Where a gap's decided outcome gets recorded

1. **Reuse the existing ADR/Amendment/research-doc mechanics.** A
   `docs/research/*.md` write-up captures the evidence per gap. An outcome
   of "genuinely worth adding" becomes a new `Proposed` ADR sketching the
   problem (not the API — per the user's explicit instruction not to
   design the hidden-shared-values API yet) for a future milestone. An
   outcome of "intentional difference, no change" becomes a dated
   Amendment to whichever existing `Accepted` ADR governs that behavior
   (ADR-0011 for recursion, ADR-0022/ADR-0011 for `[Shared]`, ADR-0025 for
   NSubstitute), since it's a confirmation/clarification of that ADR's
   existing decision, not a reversal of it.
2. **A single informal wrap-up doc**, outside the ADR system, summarizing
   findings in prose.
3. **A new ADR per gap regardless of outcome**, including for gaps that end
   in "no change."

## Decision Outcome

### Chosen: Option 1 for both — migration-driven evidence, recorded via existing ADR/Amendment/research-doc mechanics

**Migration-driven evidence (Option 1 above).** Synthetic spikes were
considered because they'd produce faster answers, but were rejected: a
spike is, by construction, written to exercise the gap it's testing,
which makes it structurally likely to "prove" the gap matters even when
real usage wouldn't hit it often enough to justify a change. The whole
point of dogfooding is that `cosmere-tracker`'s test kit already has real
call sites for all three candidate gaps (`HttpClientSpecimenBuilder`,
`BaseFixtureFactory`'s `ConfigureMembers = true`, its
`OmitOnRecursionBehavior`) — evidence drawn from migrating those real call
sites is strictly more trustworthy than evidence drawn from an example
built to order. This does mean any given gap's verdict isn't available
until the migration reaches the code that exercises it; that's an accepted
cost of grounding the answer in reality.

**Recorded via existing mechanics (Option 1 above).** A dedicated research
document, `docs/research/0001-autofixture-comparison.md`, is created (the
first use of the `docs/research/` directory `design-decisions.md`
anticipated) — it captures the dogfooding narrative: baseline metrics on
`cosmere-tracker`'s test kit before migration, what changed during
migration, and, for each candidate gap, the evidence gathered and which
option below its outcome maps to. It closes with a `## Decisions` section
listing exactly which ADR(s)/Amendment(s) each gap's evidence fed into, per
the convention `design-decisions.md` already establishes for that
directory. Option 2 (a single informal wrap-up, outside the ADR system)
was rejected because it would leave the three gaps' outcomes undiscoverable
from the normal ADR index — anyone auditing "why doesn't Compono support
`Freeze`" would have no path from `docs/adr/README.md` to the answer.
Option 3 (a new ADR per gap regardless of outcome) was rejected because it
misuses the ADR mechanism for outcomes that are really "no decision was
made, the existing decision stands" — `design-decisions.md`'s Amendment
mechanic already exists for exactly that case (see ADR-0022's own
Amendments 1-6 for precedent), and using it keeps a "no" outcome from
looking like a new, independent design decision when it's actually a
confirmation of one that already exists.

### Gap decision rubric

Every candidate gap — the three named above, and any further one the
migration surfaces — is decided using the same four questions, all
answered from evidence gathered during the real migration:

1. **Observed frequency.** How many real, distinct places in
   `cosmere-tracker`'s test kit or tests actually needed this behavior —
   not "could plausibly use it," but did, in the code as it stood before
   migration.
2. **Workaround cost.** Concretely, in the migrated code, what did
   Compono's existing explicit alternative cost — extra constructor/method
   parameters, extra lines, a `[Shared]`-typed parameter appearing in a
   test signature that previously had one fewer thing to read — shown as a
   real before/after snippet, not a hypothetical one.
3. **Principle alignment.** Would satisfying this gap require reflection
   or hidden state that conflicts with
   [ADR-0001](0001-source-generation-first.md)'s no-reflection-by-default
   posture, or with this repo's explicit-over-implicit bias
   (`docs/manifesto.md`)? A gap that can only be closed by working against
   an existing constraint needs a much higher bar on the first two
   questions to justify it.
4. **Net readability/predictability delta.** Would removing this specific
   friction plausibly make the migrated tests easier to read and reason
   about by more than the explicitness it would cost — an actual judgment
   call on the real migrated code, not an abstract API-design preference.

The rubric's output is one of exactly two outcomes per gap — "roadmap
candidate" (a new `Proposed` ADR sketching the problem, left for a future
milestone to design the actual API) or "intentional design difference" (a
dated Amendment to the governing existing ADR, recording the evidence and
why Compono's current behavior stays as-is). There is no third "maybe"
outcome; Milestone 7's job is to close each gap out one way or the other,
even if the roadmap candidate outcome defers the actual design work.

### Where the migration happens

`cosmere-tracker` is a separate repository
(`git@github.com:ncipollina/cosmere-tracker.git`), not part of this
monorepo — the actual test-kit rewrite happens there, referencing Compono
packages built from this repo's current state. This repo's Milestone 7
artifacts are the research document, the evidence it records, and whatever
ADRs/Amendments that evidence produces here — not a copy of
`cosmere-tracker`'s code. `docs/research/0001-autofixture-comparison.md`
links to the specific `cosmere-tracker` commit(s)/branch the evidence was
drawn from, so the trail is followable without vendoring the other repo's
history into this one.

### Positive Consequences

- A gap can genuinely end in "no change" without that outcome being
  invisible or undiscoverable later — it's a real, indexed Amendment.
- The rubric generalizes to any further gap the migration turns up beyond
  the three named here, so this ADR doesn't need to be revisited for a new
  candidate discovered mid-migration.
- Reusing `design-decisions.md`'s existing mechanics (ADR, Amendment,
  research doc) means Milestone 7 needs no new process infrastructure.

### Negative Consequences

- Migration-driven evidence means the three gaps' verdicts land at
  different points in the migration (whenever the relevant code is
  reached), not all at once — accepted, per the rationale above.
- A "roadmap candidate" outcome produces a `Proposed` ADR with no design
  content beyond the problem statement (per the user's explicit
  instruction not to design the hidden-shared-values API yet) — that ADR
  will need its own later design pass (deep dive, per `design-decisions.md`)
  before it can move to `Accepted`. This is intentional, not a gap in this
  ADR.

## Pros and Cons of the Options

### Migration-driven evidence only (chosen)

- Good, because every verdict traces to a real call site that already
  existed before Compono was involved, not an example constructed to make
  a point.
- Good, because it directly answers "is this actually painful in
  real-world usage" — the user's own framing for gap 1 — rather than "is
  this painful in a scenario built to be painful."
- Bad, because it's slower — a gap's verdict isn't available until the
  migration reaches its call site.

### Decoupled synthetic spikes, then migration

- Good, because it produces answers faster, independent of migration
  sequencing.
- Bad, because a spike is written to exercise the thing being tested,
  making a "this matters" verdict more likely regardless of real-world
  frequency — the opposite of what a falsifiable evidence-driven process
  needs.

### Reuse existing ADR/Amendment/research-doc mechanics (chosen)

- Good, because it costs no new process and stays discoverable from the
  existing `docs/adr/README.md` index and `docs/research/`'s own
  `## Decisions` convention.
- Good, because an Amendment correctly frames a "no change" outcome as a
  confirmation of an existing decision, not a new one.
- Bad, because a reader has to follow research doc → Amendment/new ADR to
  get the full picture for any one gap, rather than one self-contained
  document — accepted, matching how every other ADR in this repo already
  layers context across linked documents.

### Single informal wrap-up doc

- Good, because it's the least ceremony.
- Bad, because it's invisible from the ADR index — the exact discoverability
  gap `docs/research/`'s `## Decisions` convention exists to prevent.

### New ADR per gap regardless of outcome

- Good, because every gap gets a uniform, symmetric artifact.
- Bad, because it misuses the ADR mechanism for a "no decision" outcome,
  duplicating what the Amendment mechanic already does for exactly this
  case.

## Links

- [docs/mvp.md](../mvp.md) — Milestone 7 scope/success measures this ADR
  turns into a concrete process
- [docs/manifesto.md](../manifesto.md) — "not intended to reproduce
  AutoFixture feature-for-feature," the principle this framework exists to
  protect
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  governs `[Shared]` semantics and recursion detection; the likely
  Amendment target for gaps 1 and 3 if they end in "no change"
- [ADR-0022](0022-compono-xunit-package-design.md) — governs `[Shared]`'s
  xUnit v3 surface; a possible Amendment target for gap 1 alongside
  ADR-0011
- [ADR-0025](0025-compono-nsubstitute-package-design.md) — governs
  `Compono.NSubstitute`'s "no recursive auto-configuration" non-goal; the
  likely Amendment target for gap 2 if it ends in "no change"
- [ADR-0001](0001-source-generation-first.md) — the no-reflection-by-default
  constraint the rubric's "principle alignment" question checks against
- `design-decisions.md` — the Amendment mechanic and `docs/research/`
  convention this ADR reuses rather than reinventing
- `git@github.com:ncipollina/cosmere-tracker.git` — the real project being
  migrated; not part of this monorepo
