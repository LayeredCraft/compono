# [PLAN-0039] Future Extension Package Admission Gate and Release Sequence

**Status:** Done

**Implements:** ADR-0039

## Goal

ADR-0039's governance decision (a two-stage admission model — Gate A
architectural admission, Gate B evidence admission per ADR-0029 — no
committed release sequence, and explicit dispositions for all six original
candidates) is fully reflected across every doc/skill surface that
referenced the old three-item OR-gate or the old six-package sequence, with
no candidate accidentally converted into a commitment. This plan puts
ADR-0039's decision into effect — it does **not** build `Compono.TUnit`,
`Compono.NUnit`, `Compono.MSTest`, or any other candidate package.

Done means: every doc naming the old gate/sequence now reflects the new
one, `future-packages.md` carries an honest per-candidate disposition,
`skills/compono` doesn't misrepresent package-admission status to an
agent, and a final consistency pass finds no stale reference to the
three-item gate or the committed order anywhere in the repo.

## Scope

In scope: documentation, roadmap, and skill-guidance updates that record
and apply ADR-0039's decision. Explicitly out of scope, per ADR-0039's
own Decision Outcome and this ADR's user-facing constraints:

- Building any of the six candidate packages, or any other extension
  package.
- Writing a `Proposed` ADR for any individual candidate — none has cleared
  Gate B (evidence) yet, so none is roadmap content.
- Designing the prerequisite core capability the `Compono.DependencyInjection`
  disposition names (keyed/named composition requests, composition-scope-
  owns-DI-scope lifetime) — that's its own future core-extension ADR, not
  scoped here.
- Writing the `Compono.FakeItEasy` documentation recipe itself — recorded
  as an idea in `future-packages.md`; drafting the actual recipe content is
  optional follow-up work, not required for this plan's exit criteria (see
  Phase 3).

## Phase 0 — Current-state validation

- [x] Reconciled ADR-0039 (`Accepted`) against `future-packages.md`,
      `docs/roadmap/post-mvp.md`, ADR-0029, `docs/architecture/design-principles.md`,
      and the three shipped package boundaries (`Compono.XunitV3`,
      `Compono.NSubstitute`, `Compono.Bogus`) — no contradiction found;
      `future-packages.md`'s three-section candidate inventory matches
      ADR-0039's own candidate-by-candidate disposition text exactly.
- [x] Grepped the repo for every reference to ADR-0039's retired
      three-item OR-gate language and the retired six-package committed
      sequence. Real stale copy found and fixed:
      `docs/roadmap/proposed-adrs.md` still listed ADR-0039 as `Proposed`
      with the old "admission gate + candidate release order, both
      non-binding" description — rewritten to describe the `Accepted`
      two-stage model and link `PLAN-0039`. `docs/roadmap/index.md`
      checked — only references `future-packages.md`/`post-mvp.md`
      generically, no stale gate/sequence language of its own, no change
      needed. No `docs/packages/*.md` guide or `README.md` referenced the
      old gate/sequence text.
- [x] Old TUnit-first rationale (source-gen architectural kinship) checked
      repo-wide — the only remaining occurrence is inside ADR-0039's own
      Context section, deliberately preserved as the historical record of
      what this revision retired. No other doc restated it.
- [x] Incidental finding, fixed: `docs/architecture/decision-log.md` was
      already stale before this work (missing ADR-0035 through ADR-0038
      entries, unrelated to ADR-0039) — backfilled 0035-0039 in the same
      pass rather than adding 0039 next to a gap.

## Phase 1 — Admission policy

- [x] Confirmed ADR-0039's Decision Outcome section stands as the single
      canonical statement — verified directly against the file: five Gate
      A criteria present under "The two-stage admission model", Gate B
      deference to ADR-0029 stated explicitly and unduplicated, and the
      four-stage terminology section present verbatim
      ("### Terminology..."). No separate restatement added to
      `design-decisions.md` or the `engineering-workflow` skill —
      confirmed neither currently restates any future-package-specific
      criteria that would now conflict with ADR-0039's gate, so there's
      nothing to reconcile there.
- [x] Confirmed `docs/roadmap/future-packages.md`'s "Admission model"
      section states the two-gate flow (Gate A → admitted candidate;
      Gate B → roadmap item) in a short summary and links to ADR-0039 for
      the full five-criteria list — grepped the file directly and
      confirmed none of Gate A's five criteria names ("Compono-specific
      value," "Native ecosystem fit," etc.) are restated verbatim there,
      so the doc points rather than duplicates.
- [x] Checked `docs/roadmap/index.md` — it references `future-packages.md`/
      `proposed-adrs.md` generically and never describes the admission
      gate's mechanics itself, so there's no gate-specific staleness to
      fix. Noted, but explicitly out of scope for this phase: its one-line
      description of `proposed-adrs.md` ("design decisions still being
      discussed, not yet `Accepted`") is a pre-existing, minor imprecision
      against that page's own actual scope ("`Proposed`, or `Accepted` but
      not yet implemented") — predates ADR-0039's revision, not introduced
      or worsened by it.

## Phase 2 — Candidate inventory

- [x] Confirmed `future-packages.md`'s three sections against ADR-0039's
      candidate-by-candidate disposition text side by side — all six
      candidates' reasoning matches (TUnit/NUnit/MSTest as admitted;
      FakeItEasy/DI as documentation-only ideas; Moq deferred on
      maintenance-health grounds only, post the thread-2 fix above). No
      drift found.
- [x] Confirmed `docs/adr/README.md`'s index still ends at ADR-0039 —
      no ADR exists for any of the six candidate names (`ls docs/adr/`
      grepped for all six, zero matches).
- [x] Confirmed `docs/packages/index.md` lists exactly the four shipped
      packages (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
      `Compono.Bogus`) and makes no mention of any of the six candidates.

## Phase 3 — Documentation and skill alignment

- [x] Reviewed `skills/compono/SKILL.md` and every `skills/compono/references/*.md`
      file — grepped for all six candidate names. Only hit:
      `SKILL.md`'s existing "DO NOT USE FOR: ordinary xUnit/NUnit/MSTest..."
      guardrail line, which correctly scopes the skill *away* from
      non-Compono NUnit/MSTest work and makes no claim that a Compono
      package for them exists. Detection table already lists exactly the
      four shipped packages — no new row added, per ADR-0035's escape-hatch
      principle (a merely-admitted candidate isn't sufficient reason to
      touch the skill).
- [x] No existing guardrail said "don't invent a Compono package that
      hasn't shipped" — a real gap, not just a hypothetical one, given
      NUnit/MSTest/Moq are all plausible things a user could ask about.
      Added one guardrail bullet to `SKILL.md`'s Guardrails section naming
      the four real packages explicitly, stating none of
      `Compono.NUnit`/`Compono.MSTest`/`Compono.TUnit`/`Compono.FakeItEasy`/
      `Compono.Moq`/`Compono.DependencyInjection` exist today, and pointing
      at `docs/roadmap/future-packages.md` for current status.
- [x] Added `skills/compono-evals/evals.json` scenario 20
      (`behavioral-correctness`) — "Does Compono support NUnit?" — asserting
      the agent states plainly that `Compono.NUnit` doesn't ship, doesn't
      invent a plausible-looking API for it, and doesn't silently redirect
      to `Compono.XunitV3` as if it worked in NUnit. Not spot-run live
      against a subagent this pass (unlike PLAN-0035's Group 2) — flagged
      honestly rather than claimed; a future pass through this eval suite
      should include it.

## Phase 4 — Verification and closeout

- [x] Repo-wide grep for the retired OR-gate text and the retired
      six-package sequence — the only two hits are inside ADR-0039 itself
      (the Context section's historical record, and a Pros/Cons bullet
      quoting the retired phrase to explain why it was rejected), both
      deliberately preserved, not live claims.
- [x] Confirmed `docs/adr/README.md`'s index row for ADR-0039 reads
      `Accepted`. Confirmed every internal ADR-0039 cross-link (Post-MVP,
      ADR-0029, ADR-0024, ADR-0025, ADR-0021, ADR-0022, ADR-0019,
      ADR-0037, ADR-0038) resolves to a real file.
- [x] Confirmed `docs/plans/README.md`'s PLAN-0039 row and the plan
      file's own `Status` field matched throughout (`In Progress`) and
      both move to `Done` together in this same change.
- [x] Re-read `future-packages.md` end to end — grepped for
      commitment-implying phrasing ("will ship", "planned for", "is
      coming", "scheduled for") across the whole file: zero matches. Every
      candidate stays in idea/admitted-candidate/documentation-only/
      deferred language throughout.
- [x] `mkdocs build --strict` is wired into CI (`.github/workflows/docs.yml`,
      triggered on any PR touching `docs/**`) rather than run locally
      (mkdocs/uv not installed in this environment) — validated by CI on
      this phase's own PR rather than a local run; not claiming a local
      execution that didn't happen.
- [x] Set `Status: Done`.

## Critical Files

- `docs/adr/0039-future-extension-package-admission-gate-and-release-sequence.md` —
  already revised in place; `Status` already flipped to `Accepted`
  (the user confirmed the direction before this plan was written)
- `docs/adr/README.md` — index status row for ADR-0039
- `docs/roadmap/future-packages.md` — already rewritten with the
  three-section candidate inventory
- `docs/roadmap/index.md` — check for stale admission-process description
- `docs/packages/index.md` — check for accidental candidate-as-shipped
  implication
- `skills/compono/SKILL.md`, `skills/compono/references/*.md` — review
  only, likely no change (Phase 3)
- `skills/compono-evals/evals.json` — only if Phase 3 finds a real
  guardrail gap
- `docs/plans/README.md` — index row for this plan

## Test Plan

No `.cs`/runtime changes — this plan is documentation/governance only.
Verification is entirely the grep/consistency/link-resolution checks in
Phase 4, plus (if Phase 3 adds an eval scenario) a spot-check run of that
scenario the same way PLAN-0035 Group 2 validated new scenarios.

## Notes

**Phase 0 (2026-08-11)**: reconciliation and repo-wide grep found one real
stale reference beyond what the ADR revision itself touched —
`docs/roadmap/proposed-adrs.md` still described ADR-0039 as `Proposed`
with the retired gate/sequence language; fixed. Also found and fixed an
unrelated pre-existing gap in `docs/architecture/decision-log.md` (missing
ADR-0035 through ADR-0038, predating this work) while backfilling its
ADR-0039 entry. No contradiction found between ADR-0039's new text and
`future-packages.md`, `post-mvp.md`, ADR-0029, design-principles.md, or
the three shipped package boundaries.

**PR #69 Codex review (2026-08-11)**: 3 inline findings, all confirmed
real:
- 🐛-equivalent (Codex P2): ADR-0039's `Compono.Moq` disposition claimed
  "no `net8.0`/`net9.0` target at all" made Moq TFM-incompatible — false.
  Moq ships `netstandard2.0`/`netstandard2.1` assets, and this repo's own
  [ADR-0037](../adr/0037-netstandard2.1-compatibility-floor.md) already
  documents that NuGet's asset-compatibility fallback makes those
  consumable from `net8.0`/`net9.0` without issue. Fixed: the claim is
  retracted in ADR-0039, `future-packages.md`, and the ADR's own Links
  section; the Moq deferral is re-grounded solely in maintenance health
  (~23 months without a release, SponsorLink reputational damage) per the
  user's explicit direction to keep Moq deferred for that reason alone.
- ⚠️-equivalent (Codex P2): `docs/roadmap/proposed-adrs.md` still said
  PLAN-0039 was `Not Started` after this plan had already moved to
  `In Progress` with Phase 0 checked off — fixed.
- ⚠️-equivalent (Codex P1): flagged the ADR-0035–0038 decision-log backfill
  as an unrelated bundled change per this repo's own no-drive-by-rewrites
  rule. User decision: keep it bundled as-is (small, one-line-per-entry,
  directly adjacent to the ADR-0039 index edit in the same file) — not
  acted on, replied on the thread explaining the reasoning.

**Phase 1 (2026-08-11)**: pure verification pass, no doc changes needed.
All three items confirmed clean against the merged PR #69 state — ADR-0039
is already the single canonical statement of the gate/terminology,
`future-packages.md` already points at it rather than duplicating it, and
`docs/roadmap/index.md` has no gate-specific staleness (its one unrelated,
pre-existing imprecision is noted but out of scope here).

**Phases 2-4 (2026-08-11)**: run together in one PR rather than three
separate ones. Per `design-decisions.md`'s phase-per-PR rule ("a phase
that ships bundled with three others might as well not have been a
separate phase"), that rule targets phases whose independent
reviewability actually matters — here, Phase 2 was pure verification
(zero file changes), and Phases 3-4's total diff (one guardrail bullet,
one eval scenario, one roadmap-page rewrite reflecting the plan's own
completion) is small and tightly coupled to closing this plan out.
Splitting three phases this thin into three PRs would have been
fragmentation for its own sake, the same reasoning PLAN-0035 recorded for
its own single-PR delivery — explicit by user direction this time, not a
unilateral call.

**Phase 4 closeout finding**: `docs/roadmap/proposed-adrs.md` needed a
further update beyond the Phase 0/response-to-feedback fix already made —
now that PLAN-0039 is `Done`, ADR-0039 is fully implemented, so it comes
off the "Accepted, not yet implemented" list entirely. Rewrote the page to
reflect an empty list (matching `post-mvp.md`'s own precedent for
recording a "current state: none" finding explicitly rather than leaving
stale content).
