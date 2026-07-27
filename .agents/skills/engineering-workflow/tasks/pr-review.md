# Task: PR review

**Trigger:** asked to review a pull request or diff in this repo — "review
this PR", "review my changes", "look over this before I merge", "review
this before I push/commit". Distinct from the generic `/code-review`
skill, which hunts for correctness bugs and simplification/efficiency
cleanups in the diff itself — this task is about **this repo's own process
conformance** on top of that: does the change carry the decision trail
it's supposed to, does it follow this repo's established standards. Run
`/code-review` too for something non-trivial; this task doesn't replace
it, it wraps it with repo-specific context. **This task produces new
findings** — for handling feedback someone else already posted (including
replying to your own review from a prior run of this task), see
`tasks/respond-to-pr-feedback.md` instead.

**Two modes**, decided by whether a GitHub PR exists yet for the work
under review:
- **PR mode** — a real PR number exists. Confirmed findings get posted as
  inline PR comments, and the review ends with a submitted GitHub verdict
  (Approve/Request changes/Comment).
- **Local mode** — reviewing uncommitted work, a local commit, or a pushed
  branch with no PR opened yet ("review this before I push/commit"). There
  is nothing to post to on GitHub, and that's the point — this mode exists
  specifically to catch findings *before* they ever become PR comments.
  Confirmed findings get fixed directly in the working tree instead of
  posted, and the review ends with a plain readiness summary instead of a
  submitted verdict.

Both modes share steps 1–5 (get the diff, identify scope, find the
ADR/plan, build the complete findings list, confirm with the user) — the
review mechanics don't change, only what happens to a confirmed finding at
the end does.

## Load these references first

Always:
- `references/coding-standards.md`
- `references/design-decisions.md`
- `references/documentation.md`
- `references/code-of-conduct.md` (how to phrase findings)
- `references/review-emoji-legend.md` (every comment and the verdict need
  the right emoji — this is the only place that mapping is defined)

Conditionally, based on what the diff actually touches:
- `references/testing.md` — any new/changed behavior in `src/`
- `references/security.md` — the source generator's trust boundary,
  package supply chain, or config/secrets

## Procedure

A review is **complete** or it hasn't happened — don't stop after the
first finding and leave the rest of the diff for "next time." Walk every
changed file in one pass before posting anything, and use the findings
list built in step 4 as the single source of truth for what gets posted;
don't post opportunistically while still reading.

1. **Get the diff, and decide which mode you're in.** If reviewing a
   GitHub PR, `gh pr diff <number>` (or `gh pr view <number>` for the
   description/commits) — this is **PR mode**; note the PR number and the
   head commit SHA now (`gh pr view <number> --json headRefOid -q
   .headRefOid`), inline comments need it later. If reviewing local
   uncommitted/committed-but-unpushed work (no PR exists yet), `git
   diff`/`git log` against the base branch — this is **local mode**; there
   is no PR number or head SHA to fetch, and step 6 below branches on that.
2. **Identify what changed** — which package (`Compono` core, or an
   integration package like `Compono.Xunit`/`Compono.NSubstitute`/
   `Compono.Bogus` once they exist) and what kind of change (new feature,
   bug fix, refactor, docs-only). This decides which conditional
   references from above actually apply.
3. **Find the associated ADR/plan.** Look for an `ADR-NNNN`/`PLAN-NNNN`
   reference in the PR title, description, commit messages, or branch
   name. Per `design-decisions.md`'s light/deep-dive rule: if the change is
   non-trivial and no ADR exists, that's a real finding, not a nice-to-have
   — track it like any other. If an ADR is referenced, read it and its
   plan, and confirm the code actually matches the ADR's Decision Outcome —
   a diverging implementation is either a finding or a sign the ADR needs a
   follow-up; decide which and say so in the finding.
4. **Build the complete findings list before posting anything.** Walk
   every changed file/hunk against the loaded references:
   - `coding-standards.md` conformance — naming, nullable annotations,
     async patterns, DI/constructor style, error handling, file layout,
     parameter-count rule.
   - `testing.md` conformance — new composition-engine/provider/generator
     behavior has new tests, following this repo's xUnit v3 + NSubstitute +
     AwesomeAssertions conventions (deliberately no AutoFixture — see
     `testing.md`).
   - `documentation.md` conformance — relevant `docs/*.md` updated in the
     same PR, not deferred.
   - The conditional references from above, wherever the diff touches
     that territory (no plaintext secrets, generator input treated
     defensively, no core-to-integration-package reference leaking in,
     etc).
   - Also run `/code-review`-style correctness/simplification scanning
     over the same diff if that skill hasn't already been run separately.

   For each finding, record: file, line, a severity per
   `review-emoji-legend.md` (🐛/⚠️/📝/❓), and a one-line summary + concrete
   failure mode (per `code-of-conduct.md`'s voice — specific, not vague).
   Present the **entire list at once**, not one at a time, so the user sees
   the full picture before anything is posted.
5. **Confirm with the user before acting on anything.** For each finding
   in the list, get explicit agreement on what happens to it — in PR mode
   that means "post it or not"; in local mode that means "fix it now or
   not." Either way the user may accept all, reject some, or adjust a
   severity. Never post or fix a finding the user didn't confirm. Don't
   silently drop a finding the user didn't explicitly address, either —
   resolve every item in the list one way or another before moving on.
6. **PR mode: post each confirmed finding as an inline PR comment**,
   emoji-prefixed per `review-emoji-legend.md`:
   ```
   gh api repos/{owner}/{repo}/pulls/{pr}/comments \
     -f body="🐛 <finding text>" \
     -f commit_id="<head SHA from step 1>" \
     -f path="<file path>" \
     -F line=<line number> \
     -f side=RIGHT
   ```
   Post every confirmed finding this way before moving to the verdict —
   don't interleave posting with more review reading.

   **Local mode: fix each confirmed finding directly in the working tree**
   instead — there's no PR to post to, and the entire point of reviewing
   before pushing is to not carry the finding forward at all. Follow
   `coding-standards.md`/`testing.md`/`documentation.md` for the fix the
   same as any other code change, in one batch after every finding has a
   decision (mirrors `respond-to-pr-feedback.md`'s Phase 2) rather than
   fixing opportunistically mid-review. Build and run the test suite
   before telling the user it's ready.
7. **Determine the overall verdict.**
   - **PR mode**, per `review-emoji-legend.md`: 🎉 Approve if no 🐛
     findings are open, 🛑 Request changes if any are, 💬 Comment only in
     the rare case neither applies yet. Confirm the verdict (and its
     summary body) with the user before submitting.
   - **Local mode**: no GitHub verdict to submit — the "verdict" is a
     plain readiness summary (e.g. "3 findings fixed, 0 remaining — ready
     to commit" or "1 🐛 finding deferred, flagging before you commit
     anyway"). Report it in chat; there's nothing to confirm-and-submit
     since nothing was posted anywhere.
8. **PR mode: submit the verdict**, emoji-prefixed, summarizing what was
   found and linking back to the inline comments rather than repeating
   them in full:
   ```
   gh pr review <number> --approve -b "🎉 <summary>"
   gh pr review <number> --request-changes -b "🛑 <summary>"
   gh pr review <number> --comment -b "💬 <summary>"
   ```
   **Local mode**: nothing to submit — step 7's readiness summary is the
   final output. If the user then commits/pushes and opens a PR, that's a
   fresh, separate action, not part of this task.

## Output

**PR mode**: the record of a completed review *is* the posted inline
comments plus the submitted verdict — that's the deliverable, not a
private chat-only summary. If invoked as part of a formal review flow that
expects structured findings (e.g. the `ReportFindings` tool) in addition
to posting to GitHub, use that too.

**Local mode**: the deliverable is the fixed working tree (fixes applied,
tests passing) plus the chat readiness summary from step 7 — there is
nothing to post, since the whole point of this mode is to resolve findings
before they ever reach GitHub.
