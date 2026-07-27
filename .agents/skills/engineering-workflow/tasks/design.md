# Task: Design

**Trigger:** asked to design a new feature, an architecture change, or
answer "how should I structure this" — including picking up the next item
off `docs/mvp.md`'s roadmap. Produces a decision (an ADR, and usually a
plan) that `tasks/implement.md` then builds against. This task **does not
write application code** — if you catch yourself editing a `.cs` file
mid-design, stop; that belongs in `tasks/implement.md` once the ADR is
`Accepted`.

## Load these references first

Always:
- `references/design-decisions.md` — this task is that file's process,
  turned into a checklist; read it in full, not just skimmed, since the
  light/deep-dive distinction and the ADR/plan mechanics are the actual
  content of this task
- `references/documentation.md`

Conditionally, based on what the design touches — load these *before*
generating alternatives, not after, since they constrain which
alternatives are even legal:
- `references/coding-standards.md` — the design will produce code; know
  the constraints (composition over inheritance, core-package-knows-
  nothing-about-integrations, DI-only, no static singletons) before
  proposing a shape that violates them
- `references/security.md` — touches the source generator's trust
  boundary, package supply chain, or config/secrets
- `references/testing.md` — the design has a non-obvious test strategy
  worth deciding up front (e.g. how something inherently seed-/
  determinism-sensitive gets covered)

## Procedure

1. **Check for existing precedent before assuming this is new.** Grep for
   the likely type/interface name (`Provider`, `Plan`, `Profile`) and skim
   the relevant `docs/*.md` (especially `architecture.md` and
   `public-api.md`, which already scope a fair amount of the intended
   shape) — a request phrased as "add X" is often "X is already scoped in
   the docs, here's where" (`design-decisions.md`'s opening rule). A design
   session grounded in what's already decided beats one that re-derives a
   solved problem.
2. **Decide light vs. deep**, per `design-decisions.md`:
   - **Light** — problem and solution shape are already clear (adding a
     provider following a pattern another provider already uses, adopting
     an established org-wide tooling convention, etc). Skip straight to
     step 4; most ADR sections compress to a sentence.
   - **Deep** — the problem is ambiguous, spans multiple packages, or has
     more than one plausible shape. Do step 3 first.
3. **Deep dive only: brainstorm before drafting anything.** Restate the
   problem in your own words. Surface the constraints that actually bind
   (the resolution-pipeline order, core-package-knows-nothing-about-
   integrations rule, no-reflection-by-default ADR — pull the specifics
   from whichever conditional references you loaded above, don't restate
   them generically here). Generate **at least two genuinely different
   alternatives** — not a strawman and a preferred option; if you can only
   think of one real shape, it probably wasn't a deep dive. Research
   existing prior art if relevant (how AutoFixture, another composition
   library, or a referenced paper solved the analogous problem) and record
   what's adopted vs. deliberately changed. **Talk it through with the
   user before committing to one** — if there's a genuine fork (e.g. "match
   an external reference's shape 1:1" vs. "a leaner approach fit to this
   repo's actual needs"), surface it explicitly (a direct question works
   better than silently picking one and writing the ADR around it) — the
   point of this phase is to catch a disagreement before it's baked into an
   `Accepted` ADR, not after.
4. **Write the ADR.** Copy `docs/adr/0000-adr-template.md` to
   `docs/adr/NNNN-kebab-case-title.md` (next sequential number — check
   `docs/adr/README.md`'s index for the last one used). Fill in Context,
   Decision Drivers, Considered Options, Decision Outcome, Consequences,
   Links — compressed to a sentence/"N/A" per section for a light dive,
   full detail for a deep one. Keep it to decision content only — no task
   checklist, no file list (that's the plan, step 6). Set `Status:
   Proposed` while still unsettled; once the user has actually confirmed
   the direction (the deep dive's step 3 conversation, or an explicit "yes,
   do that" for a light one), flip it to `Accepted` — that status change
   *is* the record of the decision being made.
5. **Index and cross-link — without describing unimplemented behavior as
   current.** Add a row to `docs/adr/README.md`'s index table. Subsystem
   docs describe *current* state (`documentation.md`), and this task
   hasn't written any code yet, so don't rewrite a doc's prose to describe
   the new decision as already true. The only doc touch that belongs here
   is a forward-reference — an Open Items entry (or equivalent) noting the
   gap now has a decision, linked to the ADR, e.g. "resolved by ADR-NNNN,
   not yet implemented — see `tasks/implement.md`." Actually describing the
   new current state is `tasks/implement.md`'s step 6, once the code
   backing it exists. If this ADR supersedes an earlier one, set that
   ADR's `Status` to `Superseded by ADR-XXXX` — don't edit its Context/
   Decision/Options, an accepted ADR is a historical record.
6. **Decide whether a plan is warranted.** Per `docs/plans/README.md`: not
   every ADR needs one — a light dive whose outcome is an obvious one-file
   change doesn't. Write one when the implementation is multi-file, spans
   more than one sitting, or has enough real sequencing that a checklist
   earns its keep (this covers most deep dives and some light ones). If
   warranted: copy `docs/plans/0000-plan-template.md` to
   `docs/plans/NNNN-kebab-case-title.md` (same number as the ADR it
   implements), fill in Goal/Scope/Tasks/Critical files/Test plan/
   Verification, `Status: Not Started`. A plan can be drafted alongside a
   still-`Proposed` ADR (designing the how often surfaces questions about
   the what) — but it stays `Not Started` until the ADR is `Accepted`; a
   plan moving to `In Progress` against a `Proposed` ADR means code is
   being written for an unsettled decision.
7. **Index the plan** in `docs/plans/README.md` if one was created, and
   — if this design is a slice of a broader roadmap item tracked in
   `docs/mvp.md` — note the ADR/plan link against that item.

## Output

An `Accepted` ADR (or a `Proposed` one, if genuinely still under
discussion and this task is being paused rather than finished), indexed,
cross-linked from the affected subsystem doc(s), with a `Plan` alongside
it if the implementation is non-trivial. That pair is the handoff to
`tasks/implement.md` — implementation shouldn't start (a plan shouldn't
move to `In Progress`) until the ADR reads `Accepted`.
