# [PLAN-0039] Future Extension Package Admission Gate and Release Sequence

**Status:** In Progress

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

- [ ] Confirm `future-packages.md`'s three sections (Admitted candidates;
      Documentation-only ideas; Deferred indefinitely) are internally
      consistent with ADR-0039's own candidate-by-candidate disposition
      text — no drift between the ADR's prose and the roadmap page's
      summary of it.
- [ ] Confirm no `Proposed` ADR exists (or gets created by this plan) for
      any of the six candidates — Gate B hasn't cleared for any of them,
      so none is roadmap content yet. This is a negative-verification
      step: the exit criterion is that `docs/adr/README.md`'s index still
      ends at ADR-0039, with no new candidate-specific ADR added as a
      side effect of this plan.
- [ ] Confirm `docs/packages/index.md` (the current package-guide index)
      is not implying any of the six candidates already exist or are
      imminent — it should continue to describe only the four shipped
      packages.

## Phase 3 — Documentation and skill alignment

- [ ] Review `skills/compono/SKILL.md` and `skills/compono/references/*.md`
      for any claim about package admission, future packages, or which
      integrations Compono supports/plans to support. The skill's job is
      to help an agent work with what's *actually shipped* — confirm it
      doesn't invent or imply support for `Compono.TUnit`/`Compono.NUnit`/
      `Compono.MSTest`/etc., and doesn't need a new Detection-table row
      until one of them actually ships (per ADR-0035's escape-hatch
      principle, restated in PLAN-0035 Notes item 3 — a new candidate
      being merely *admitted* is not sufficient reason to touch the skill).
- [ ] If the skill has any guardrail language about "don't invent a
      Compono package/API that doesn't exist," confirm it's still accurate
      and, if useful, add a one-line pointer that `future-packages.md`
      is where an agent can check current admission status rather than
      guessing — optional, only if it meaningfully reduces a real
      confusion risk (an agent asked "does Compono support NUnit"
      hallucinating a package instead of reporting "admitted candidate,
      not yet built").
- [ ] Add or update `skills/compono-evals/evals.json` with one scenario
      only if Phase 3's guardrail review above finds a real gap (e.g. an
      agent currently invents `Compono.NUnit` usage when asked about NUnit
      support) — do not add a scenario speculatively if the existing
      guardrails already cover it.

## Phase 4 — Verification and closeout

- [ ] Full-repo grep confirming no remaining reference to the retired
      three-item OR-gate text or the retired six-package committed
      sequence outside of ADR-0039's own Context section (where the
      original text is deliberately preserved as historical record).
- [x] Confirm `docs/adr/README.md`'s index row for ADR-0039 reads
      `Accepted` — already flipped. Confirm every internal ADR-0039 cross-link (Post-MVP, ADR-0029, ADR-0024,
      ADR-0025, ADR-0021, ADR-0022, ADR-0019, ADR-0038) resolves.
- [ ] Confirm `docs/plans/README.md` has a row for this plan
      (PLAN-0039), status matching this plan's own `Status` field.
- [ ] Verify no candidate was accidentally converted into a commitment:
      re-read `future-packages.md` end to end and confirm every sentence
      about the six candidates stays in "idea"/"admitted candidate"/
      "deferred" language, never "will ship" or "planned for."
- [ ] `mkdocs build --strict` (or this repo's current doc-link-check
      equivalent) clean, if the doc site build is wired up to catch
      cross-reference breakage.
- [ ] Set `Status: Done`.

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
