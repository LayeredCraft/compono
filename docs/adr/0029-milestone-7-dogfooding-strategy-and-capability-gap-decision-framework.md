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

The user's own stated objective sharpens this further: the practical goal
is to actually replace AutoFixture with Compono across their own
repositories, not merely to produce a research artifact. The milestone's
central question is therefore:

> Can Compono replace AutoFixture in this real repository while keeping
> the tests readable, maintainable, predictable, and reasonably concise?

That framing has two consequences this ADR's first design missed. First,
"Compono can technically do this a different way" is not a reason to
exclude something from the evidence — a gap is real evidence if it
prevents a clean replacement or makes the result materially less readable,
concise, or maintainable, even when a working (if less pleasant) Compono
equivalent exists. Second, the milestone needs to produce durable,
actionable artifacts from that evidence while it's fresh — a migration
guide and a roadmap — not just a backward-looking research record; see
Decision Outcome's "Required deliverables" below.

A third gap in this ADR's first design: `cosmere-tracker`'s AutoFixture
test kit has no `Compono.Bogus` analog at all — it never generates
semantic-looking data, only anonymous AutoFixture specimens. That absence
is not a reason to skip `Compono.Bogus` in this migration. Milestone 6
shipped `Compono.Bogus` but its only end-to-end verification so far is
`test/Compono.XunitV3.SampleTests`, a sample project built to exercise the
package, not a real, independently-motivated codebase. Milestone 7 is the
first chance to dogfood `Compono.Bogus` against a real domain the package
wasn't shaped around in advance — `cosmere-tracker`'s domain (books,
characters, worlds) skews away from the built-in convention allowlist's
person/contact bias (`FirstName`/`Email`/`StreetAddress`, etc.), which
makes it a genuinely useful test of ADR-0028's configurable
aliases/custom conventions, not just the happy path. See "Compono.Bogus
adoption is mandatory" below.

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
- The practical objective is a real AutoFixture replacement, not just a
  research exercise — friction is real evidence even where a technically-
  different Compono API already produces a working result (see Context's
  central question above).
- A "no change" or "acceptable alternative" outcome is only credible if the
  milestone also captures what Compono did well — a process that only
  records friction can't support a balanced "should this repo actually
  switch" conclusion.
- Migration decisions and their rationale need to be captured *while the
  context is fresh* — reconstructing "why we chose X over Y" from memory
  after the milestone closes produces a worse, less trustworthy record than
  capturing it in the PR that made the decision.
- Bugs are a distinct outcome from capability gaps — a scenario that was
  already intended to work but doesn't is a defect to fix through the
  normal engineering workflow, not a design question for the rubric.
- `docs/mvp.md`'s MVP success criterion 4 ("Bogus can provide deterministic
  semantic values through an ancillary package") deserves the same
  real-project validation the other packages get in this milestone — a
  package having no AutoFixture analog in the source project is not a
  reason to leave it undogfooded.

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
answered from evidence gathered during the real migration. The questions
inform which of the five classifications below applies; they are not a
pre-filter for whether something counts as evidence at all (per the
Context's central question and this ADR's Decision Drivers) — a gap that
survives question 3 (a workaround exists) is still recorded and classified,
not discarded.

1. **Observed frequency.** How many real, distinct places in
   `cosmere-tracker`'s test kit or tests actually needed this behavior —
   not "could plausibly use it," but did, in the code as it stood before
   migration.
2. **Was this scenario ever intended to work as AutoFixture's behavior
   suggests?** If Compono's documented/`Accepted`-ADR behavior already
   claims to support the scenario and it doesn't work, that's a bug, not a
   design question — see "Bug handling" below, and skip the remaining
   questions.
3. **Workaround cost.** Concretely, in the migrated code, what did
   Compono's existing explicit alternative cost — extra constructor/method
   parameters, extra lines, a `[Shared]`-typed parameter appearing in a
   test signature that previously had one fewer thing to read — shown as a
   real before/after snippet, not a hypothetical one. A low or zero cost
   here (the replacement stays pleasant) points toward "acceptable
   alternative"; a real, material cost points toward "roadmap candidate" or
   "intentional design difference," decided by question 4.
4. **Principle alignment.** Would satisfying this gap require reflection
   or hidden state that conflicts with
   [ADR-0001](0001-source-generation-first.md)'s no-reflection-by-default
   posture, or with this repo's explicit-over-implicit bias
   (`docs/manifesto.md`)? A gap that can only be closed by working against
   an existing constraint needs a much higher bar on frequency and cost to
   become a roadmap candidate rather than an intentional design
   difference.

### Gap classification

Every discovered finding — not only the three named gaps — gets exactly
one of five classifications, each with its own recording mechanism:

1. **Bug** — the scenario was already intended to work (per an existing
   `Accepted` ADR or documented behavior) but doesn't. Fixed through the
   normal engineering workflow (its own scoped PR in this repo, following
   `tasks/implement.md`/`pr-review.md` as usual), not the rubric. Still
   recorded in the Milestone 7 research doc, since it affected replacement
   suitability during migration. Does **not** need a new capability ADR —
   restoring already-intended behavior isn't a new design decision. See
   "Bug handling" below and PLAN-0007's Notes section.
2. **Roadmap candidate** — Compono genuinely needs a new capability. A new
   `Proposed` ADR records the problem only (per this ADR's own restraint
   below) for a future milestone's design pass.
3. **Acceptable Compono-native alternative** — a different API than
   AutoFixture's, but the replacement remains pleasant (low workaround
   cost, no material readability loss). Recorded in the research doc and
   the migration guide (see "Required deliverables" below); no ADR or
   Amendment needed — there's no decision to make, just a pattern to
   document for the next migrator.
4. **Intentional design difference** — supporting the AutoFixture behavior
   would conflict with Compono's principles or impose disproportionate
   complexity relative to its observed value. A dated Amendment to the
   governing existing ADR (ADR-0011/ADR-0022 for gaps 1/3, ADR-0025 for gap
   2, or whichever ADR governs a newly-discovered gap) records the evidence
   and the "no change" verdict.
5. **Migration-only friction** — the pain occurs during conversion but does
   not remain in the resulting Compono test suite (e.g., a one-time
   mechanical translation step). This does **not** mean the friction was
   excluded from consideration — it was evaluated like every other finding
   and only ended up here because the evidence showed it doesn't persist.
   Recorded in the research doc and, where it'll help the next migrator, as
   a tip in the migration guide; no ADR or Amendment needed.

There is no unclassified or dropped finding; Milestone 7's job is to close
every discovered gap out into exactly one of these five categories, even
where "roadmap candidate" defers the actual design work.

### Bug handling

A blocking bug discovered during migration does not have to be worked
around merely to preserve "Milestone 7 adds no product code" — that
statement was this ADR's original simplifying assumption, not a hard
constraint (see PLAN-0007's Test Plan). Concretely:

- A blocking bug may be fixed during Milestone 7 through its own scoped PR
  in this repo, following the normal `tasks/implement.md`/`tasks/pr-review.md`
  workflow — it is not gated behind ADR-0029/PLAN-0007's own review cycle.
- The bug and its impact on the migration are still documented in the
  Milestone 7 research doc, classification "Bug" (see "Gap classification"
  above).
- A bug that merely restores already-intended behavior does not need a new
  capability ADR — there is no new design decision, only a defect
  correction against an existing `Accepted` ADR.
- PLAN-0007's Notes section records any such implementation detour, linked
  to its issue/PR, so the plan's history stays accurate about *how* the
  milestone actually proceeded.

### Migration idiom

The migration prefers idiomatic Compono over mechanically recreating
AutoFixture's architecture with renamed APIs — the point of dogfooding is
to discover what a *good* Compono-based test kit looks like, not to produce
a literal translation. `CosmereTrackerAutoDataAttribute`,
`BaseFixtureFactory`, `HttpClientSpecimenBuilder`, and
`CosmereTrackerCustomization` are not automatically preserved if a profile,
a registration, a provider, or `[Compose<TProfile>]` makes the custom
abstraction unnecessary. This cuts the other way too: simplification is not
forced merely to make Compono look better — an abstraction is only removed
when its replacement is genuinely as good or better, and every removed
abstraction is documented in the migration guide alongside what replaced it
and why, so the decision is auditable rather than asserted.

### Compono.Bogus adoption is mandatory

Unlike the three named gaps above, `Compono.Bogus` isn't something
`cosmere-tracker`'s AutoFixture kit already does and Compono does
differently — it's a capability the source project simply never used, so
migration-driven evidence alone would never surface it (there's no AutoFixture
call site to migrate away from). This is the one deliberate exception to
"only migrate what's there": the migration **must** identify real
domain members in `cosmere-tracker`'s composed types (`src/Cosmere.Tracker.Shared/Models/**`
and friends — `BookItem`, `CharacterWorldEdgeItem`, etc.) that would
plausibly hold realistic string data, and adopt `UseBogus()`/member-level
`UseBogus(faker => ...)`/`UseBogus<T>()` for them in the migrated profile,
per [ADR-0027](0027-compono-bogus-package-design.md). Where
`cosmere-tracker`'s domain vocabulary (book titles, character names, world
names) doesn't match `Compono.Bogus`'s built-in person/contact-biased
convention allowlist, this is treated as a real opportunity to exercise
[ADR-0028](0028-configurable-bogus-member-name-conventions.md)'s
`BogusOptions.AddAlias`/`AddConvention` mechanism against a real domain
that mechanism wasn't designed with in mind — evidence from that usage is
itself a valid Milestone 7 finding (classified per "Gap classification"
above, same as any other), not exempt from the process just because it's
mandatory. If, after real investigation, no domain member in
`cosmere-tracker` can plausibly use `Compono.Bogus` at all, that finding
itself — recorded with the reasoning, not silently skipped — satisfies this
requirement; "mandatory" means the investigation and adoption attempt are
required, not a fabricated `UseBogus()` call bolted onto an unrelated
member just to check a box.

### Required deliverables

Two additional documents are first-class Milestone 7 deliverables, not
after-the-fact write-ups:

- **`docs/migration/migrating-from-autofixture.md`** — the migration guide,
  and the primary artifact a real AutoFixture user reaches for when moving
  to Compono. It is a **living document**, not a final-phase deliverable:
  its planned structure and the major AutoFixture concepts expected to be
  migrated (`Freeze<T>()`, `AutoDataAttribute`/customizations,
  `AutoNSubstituteCustomization`, recursion behaviors, specimen builders,
  and any other concept `cosmere-tracker`'s test kit exercises) are drafted
  **before migration begins** (PLAN-0007 Phase 0). It is then updated
  **alongside the code, in every migration PR** — every meaningful
  migration decision is captured while the context is fresh, not
  reconstructed from memory afterward. For each AutoFixture concept it
  covers: the AutoFixture approach, the Compono approach, why the Compono
  approach was chosen, whether the result is better/equivalent/a tradeoff,
  links to the relevant ADR(s)/research findings, and before-and-after code
  examples — drawn from the real `cosmere-tracker` migration wherever
  possible, not synthetic ones. By the time Milestone 7's Phase 4 closes,
  this guide is already substantially complete; only editorial cleanup
  remains.
- **`docs/roadmap/post-mvp.md`** — an evidence-backed roadmap generated
  directly from Milestone 7's findings, not a wish list. Only findings
  classified "roadmap candidate" (per "Gap classification" above) appear
  here — bugs get fixed, intentional design differences and acceptable
  alternatives do not become roadmap items. Every entry traces back to the
  migration guide, the research findings, and its capability-gap decision
  (the `Proposed` ADR that recorded it), and captures at minimum: the
  capability, why it matters, how frequently it occurred during migration,
  its impact on readability/maintainability, and a relative priority (high/
  medium/low confidence). This becomes the starting point for post-MVP
  planning rather than requiring a re-read of all the underlying research.

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

### Evidence to collect

Line counts and `dotnet test` run time alone under-measure the MVP's
"pleasant to maintain" success criterion (`docs/mvp.md`). Baseline and
post-migration comparison also record, per `docs/research/0001-autofixture-comparison.md`:

- Framework-specific concepts removed, and Compono-specific concepts
  introduced, with a rough count of each.
- Custom fixture infrastructure removed vs. retained (and why, per
  "Migration idiom" above).
- Number of reusable profiles/providers/registrations the migrated suite
  actually needed.
- How much setup is visible in individual test methods/signatures (the
  `[Shared] HttpMessageHandler`-in-every-signature question from gap 1 is a
  direct instance of this).
- Whether a given change made behavior more explicit in a genuinely helpful
  way, or just more verbose — a judgment call recorded with its reasoning,
  not just a verdict.
- Whether a new contributor to the migrated suite would need to understand
  more or fewer concepts than before.

This produces a **balanced assessment**, not only a friction log: the
research doc explicitly records where Compono improved the suite — e.g.
profiles replacing custom `AutoData` attribute classes, deterministic seed
reproduction, clearer dependency-path failures, explicit provider
precedence, fewer fixture-specific abstractions, simpler NSubstitute setup,
or readability gained by removing hidden fixture behavior — with the same
rigor as its friction findings, not as an afterthought.

### Final architectural conclusion

Milestone 7's closing phase (PLAN-0007 Phase 4) explicitly answers whether
dogfooding changed Compono's overall design direction, not just whether
each individual gap was decided:

- Whether any manifesto or design-principle language should change as a
  result.
- Whether the migration strengthened or weakened confidence in
  explicit-over-implicit as Compono's default posture.
- Whether profiles remained the right primary configuration mechanism for
  a real project's needs.
- Whether the public provider model (ADR-0024) was sufficient for real
  application-specific customization, or strained anywhere.
- Whether any MVP success criterion (`docs/mvp.md`) should be revised in
  light of real evidence.
- Whether Compono is now suitable as the default AutoFixture replacement
  for new tests in `cosmere-tracker` specifically.

### Evidence-driven restraint

This milestone still does not design the API for hidden shared values,
recursive substitute configuration, recursion omission, or any newly
discovered "roadmap candidate" capability — that restraint from this ADR's
original design is unchanged by the above. A roadmap-candidate outcome
produces only a problem-focused `Proposed` ADR; the actual API design
belongs in a later deep-design milestone, per `design-decisions.md`'s deep-
dive process. The one exception is a **blocking bug** (see "Bug handling"
above): fixing a bug that restores already-intended behavior is not API
design and is not deferred.

### Positive Consequences

- A gap can genuinely end in "no change" (either "acceptable alternative"
  or "intentional design difference") without that outcome being invisible
  or undiscoverable later — it's a real, indexed record.
- The rubric and five-way classification generalize to any further finding
  the migration turns up beyond the three named here, so this ADR doesn't
  need to be revisited for a new candidate discovered mid-migration.
- Reusing `design-decisions.md`'s existing mechanics (ADR, Amendment,
  research doc) means Milestone 7 needs no new process infrastructure
  beyond the two required deliverables (migration guide, roadmap).
- The migration guide and roadmap being living, phase-gated deliverables
  means Milestone 7 produces durably useful documentation as a byproduct
  of the work itself, not a separate write-up task competing for attention
  after the "real" work is done.
- Capturing positive findings alongside friction supports an honest answer
  to the milestone's central question, rather than a report that only ever
  argues for expanding Compono's surface area.

### Negative Consequences

- Migration-driven evidence means gap verdicts land at different points in
  the migration (whenever the relevant code is reached), not all at once —
  accepted, per the rationale above.
- A "roadmap candidate" outcome produces a `Proposed` ADR with no design
  content beyond the problem statement (per "Evidence-driven restraint"
  above) — that ADR will need its own later design pass (deep dive, per
  `design-decisions.md`) before it can move to `Accepted`. This is
  intentional, not a gap in this ADR.
- Requiring the migration guide to be updated in every migration PR adds
  real overhead to each PR in `cosmere-tracker` — accepted, because the
  alternative (reconstructing rationale after the fact) was explicitly
  rejected as producing a worse record.
- A blocking bug fixed mid-milestone means PLAN-0007's "no product code"
  framing doesn't hold universally — mitigated by "Bug handling" above
  making the exception explicit rather than leaving it to be discovered
  and worked around.

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

## Amendment 1 (2026-08-02): the `Compono.Bogus` experiment is mandatory, not its conclusion

"Compono.Bogus adoption is mandatory" (above) was reviewed against
PLAN-0007's Phase 0 output and clarified before migration began: what's
mandatory is running the real experiment — investigating
`cosmere-tracker`'s domain models and attempting `UseBogus()`/
`AddAlias`/`AddConvention` adoption against them — not a predetermined
"and it worked" outcome. The original wording already allowed "no member
can plausibly use it" as a valid recorded finding, but didn't say the same
about a partial or negative result once members *are* found: if
`Compono.Bogus` is adopted for some members and, after real investigation,
turns out to be a poor fit for others (or for the domain generally — e.g.
excessive alias/convention configuration for too little readability gain),
that is an equally valid, equally successful outcome of the dogfooding
exercise, not a shortfall against this ADR's mandate. `docs/migration/migrating-from-autofixture.md`'s
`Compono.Bogus` section and `docs/research/0001-autofixture-comparison.md`'s
findings must record, specifically: where semantic generation improved the
resulting tests, where it introduced friction, which members needed
`AddAlias`/`AddConvention`/member-level `UseBogus(faker => ...)` versus the
built-in allowlist, and a final recommendation for `Compono.Bogus`'s
continued use in `cosmere-tracker` — including a recommendation of
"don't use it for X" where the evidence supports that. This is a
clarification of "Compono.Bogus adoption is mandatory"'s existing intent,
not a reversal — the experiment was always the requirement; this Amendment
makes explicit that the rubric's evidence-driven restraint (a real "no" must
stay available, per this ADR's Decision Drivers) applies to `Compono.Bogus`
exactly as it does to the three named gaps.

## Amendment 2 (2026-08-02): removed concepts get their own explicit inventory, not just a count

"Evidence to collect"'s first bullet ("Framework-specific concepts removed,
and Compono-specific concepts introduced, with a rough count of each") is
sharpened: a rough count under-tells the story of reduced conceptual
complexity that dropping a concept *entirely* represents. Baseline and
post-migration comparison must name, not just count, every AutoFixture/
AutoFixture.AutoNSubstitute concept and every piece of `cosmere-tracker`-
specific fixture infrastructure that disappears entirely after migration
rather than being replaced by a Compono equivalent — for example (the
starting list, from Phase 0's baseline; the actual list is whatever Phase 1
finds, not limited to this one): `IFixture`, `ICustomization`,
`ISpecimenBuilder`, `IRequestSpecification`, the custom `AutoDataAttribute`/
`InlineAutoDataAttribute` subclasses (`CosmereTrackerAutoDataAttribute`,
`ClientAutoDataAttribute`), `BaseFixtureFactory` and other fixture-factory
infrastructure, and `NamedRequest`. `docs/research/0001-autofixture-comparison.md`
records this as an explicit named list (concept removed → what, if
anything, replaced it, or "nothing — no longer needed"), distinct from the
list of concepts that were merely replaced one-for-one with a Compono
equivalent. This is a clarification of "Evidence to collect," not a new
evidence category — "concepts removed" was already in scope; this Amendment
specifies the form that evidence must take.

## Amendment 3 (2026-08-02): the final architectural conclusion ends with an explicit recommendation

"Final architectural conclusion" (above) lists six questions Milestone 7's
closing phase must answer, but stopped short of requiring a synthesis of
them into a concrete answer to "given everything we learned, what should we
do now." This is clarified: PLAN-0007 Phase 4 closes with an explicit,
evidence-backed recommendation — not merely "Compono can replace
AutoFixture" as a capability statement, but a stated next action, for
example (illustrative, not exhaustive): Compono becomes the recommended
default for new `cosmere-tracker` test code; existing AutoFixture-based
tests migrate incrementally rather than in one pass; specific roadmap-
candidate findings should land before recommending migration more broadly;
or the current MVP is already sufficient for that recommendation without
waiting on any roadmap item. The recommendation must flow from this
milestone's actual findings (the five-way classifications, the baseline-
vs-post-migration comparison, Amendment 2's removed-concepts inventory) —
not be asserted independently of them. This is a clarification of "Final
architectural conclusion"'s existing intent (it already asked "whether
Compono is now suitable as the default... for `cosmere-tracker`
specifically"), not a reversal — this Amendment makes the expected output a
stated recommendation rather than leaving it as an implicit conclusion a
reader has to infer from the six answered questions.

## Amendment 4 (2026-08-03): documentation architecture becomes a required Milestone 7 deliverable

Milestone 7's scope, as originally accepted, produced two required
deliverables (the migration guide, the evidence-backed roadmap) and ended
at Phase 4's final architectural conclusion — documentation *design* (the
hierarchy, section purposes, how future content stays separated from
current) was left to Milestone 8, per `docs/mvp.md`'s original Milestone 8
scope ("README and getting-started guide, Architecture documentation,
Samples"). This is amended: dogfooding a real migration is also the
richest source of evidence this project will ever have for *how developers
actually learn and use Compono* — the real "how do I..." questions, the
concepts that confused a real AutoFixture user, the gap a real migration
surfaced (`CMP0001`) — and that evidence is expensive to reconstruct later
if documentation design waits until Milestone 8 starts from a blank page.
Milestone 7 now also produces:

- The complete documentation architecture — hierarchy, section purposes,
  audiences, ordering, and how forward-looking content stays separated from
  the primary learning path — decided in
  [ADR-0030](0030-compono-documentation-architecture.md) and recorded as a
  living reference in `docs/documentation-architecture.md`.
- The initial documentation skeleton matching that hierarchy (stub pages,
  correct nav structure) — not finished content, a scaffold Milestone 8
  writes into.
- The migration guide's promotion decision (ADR-0030's "where the migration
  guide lives") — the guide itself was already a required deliverable
  before this Amendment; what's new is that it graduates into the primary
  documentation hierarchy as a first-class entry, not an appendix.
- A concrete list of documentation work items that become Milestone 8's own
  deliverables, so Milestone 8 executes against a scoped backlog rather
  than re-deriving one.

This is a genuine scope extension, not a reversal of anything this ADR
already decided — Milestone 7's core dogfooding process (migration-driven
evidence, the gap rubric, the five-way classification, the two original
required deliverables) is unchanged; this Amendment adds a third required
deliverable category (documentation architecture) that draws on the same
evidence the rest of Milestone 7 already produces. `docs/mvp.md`'s
Milestone 7/Milestone 8 sections and [PLAN-0007](../plans/0007-milestone-7-dogfooding.md)
(a new Phase 5) reflect the resulting scope directly.

## Amendment 5 (2026-08-03): the migration guide's promotion is complete

Amendment 4 (above) decided that the migration guide graduates into the
primary documentation hierarchy as a first-class entry, per
[ADR-0030](0030-compono-documentation-architecture.md)'s "where the
migration guide lives." [PLAN-0007](../plans/0007-milestone-7-dogfooding.md)
Phase 5 has since executed that decision: the file physically moved from
`docs/migration/migrating-from-autofixture.md` (this ADR's original path,
preserved above exactly as accepted) to `docs/migrating-from-autofixture.md`,
content unchanged. This Amendment records the completed relocation as a
dated fact rather than editing the original "Required deliverables"/
Amendment 1/Links references above, which stay exactly as they read when
each was accepted — per this repo's own ADR-immutability rule, a later
fact about a path changing doesn't get retrofitted into earlier text, it
gets its own dated Amendment. Any reader following this ADR's original
references to `docs/migration/migrating-from-autofixture.md` should treat
this Amendment as the pointer to where that file actually lives today.

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
- [ADR-0027](0027-compono-bogus-package-design.md)/[ADR-0028](0028-configurable-bogus-member-name-conventions.md) —
  govern `Compono.Bogus`'s conventions and configurable aliases; the
  package this ADR mandates be dogfooded even though `cosmere-tracker`'s
  source AutoFixture kit has no equivalent usage to migrate from (see
  "Compono.Bogus adoption is mandatory" above)
- [ADR-0001](0001-source-generation-first.md) — the no-reflection-by-default
  constraint the rubric's "principle alignment" question checks against
- `design-decisions.md` — the Amendment mechanic and `docs/research/`
  convention this ADR reuses rather than reinventing
- `git@github.com:ncipollina/cosmere-tracker.git` — the real project being
  migrated; not part of this monorepo
- `docs/migration/migrating-from-autofixture.md` — the migration guide
  required by "Required deliverables" above (created in PLAN-0007 Phase 0,
  living through Phase 3)
- `docs/roadmap/post-mvp.md` — the evidence-backed roadmap required by
  "Required deliverables" above (created in PLAN-0007 Phase 3)
- [ADR-0030](0030-compono-documentation-architecture.md) — the
  documentation architecture decision Amendment 4 made room for within
  Milestone 7
