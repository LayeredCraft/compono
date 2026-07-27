---
name: engineering-workflow
description: >
  One-stop engineering workflow and standards reference for the Compono
  repo. Make sure to use this skill for ANY non-trivial work on this
  codebase, even if the user doesn't name the skill or say "engineering
  workflow" explicitly: designing new composition-engine/source-generator
  features (including open-ended architecture/design dives with unclear
  scope, not just quick decisions), writing or reviewing C# code, deciding
  where a decision or doc belongs, writing tests, or writing any
  documentation (docs/, README, CONTRIBUTING, commit messages). Always
  consult this skill before proposing an architecture change, before
  writing new C# files, and before adding a new coding pattern not already
  established in the repo — even a request that just looks like "add a
  quick feature" or "fix this bug" should route through it first. Also
  covers designing a feature/architecture decision end-to-end (ADR + plan),
  implementing against an already-`Accepted` ADR, reviewing a PR/diff in
  this repo, and responding to feedback left on one (`tasks/` procedures,
  not just reference lookup) — see How to use this skill below. Trigger
  on: "how should I structure this", "coding standards", "is this
  secure", "where does this belong", "contributing", "code of conduct",
  "design this feature", "let's build/implement this", "review this PR",
  "review my changes", "address that feedback", "handle the review
  comments", "what's next on the roadmap", or any request to add a
  feature to `Compono` / `Compono.Xunit` / `Compono.NSubstitute` /
  `Compono.Bogus` / `Compono.Generators`.
---

# Compono Engineering Workflow

This skill is the source of truth for **how** work gets done in this repo —
process and standards. It is not a restatement of current architecture;
`docs/` holds that, and this skill tells you when and how to update it. If
code and this skill disagree, the code wins and this skill is stale — fix
the skill in the same PR you notice the drift.

Repo shape: `src/Compono` (core composition engine, source generator, the
only package scaffolded so far). `docs/mvp.md` describes the intended full
package set — `Compono.Generators`, `Compono.Xunit`, `Compono.NSubstitute`,
`Compono.Bogus` — none of which exist as separate projects yet; treat any
reference to them below as forward-looking, not current fact. There is no
`tests/` content yet either (`test/` exists but is empty) — `references/testing.md`
describes the intended convention, not an established one.

## How to use this skill

Two kinds of file live here, and they don't overlap:

- **`references/`** — topic knowledge. What's true about this repo's
  standards for a given topic (coding style, testing, security, docs,
  decision-making, conduct). No procedures, no step ordering — just the
  rules and the reasoning, one file per topic, so you only load what the
  current work actually needs.
- **`tasks/`** — procedures for a specific *kind of request* ("review this
  PR"). A task file doesn't restate reference content; it says which
  references to load for that request and what order to do things in. Add
  a new task file when a recurring request needs its own procedure, not
  when it just needs a reference lookup — most requests still route
  through the reference table below, not a task.

Read the file(s) that match the work in front of you *before* writing
code, docs, review output, or design output — don't guess from a section
title alone, the detail (specific patterns, rejected alternatives, naming)
lives in the file itself.

### Tasks

| When you're asked to... | Run |
|---|---|
| Design a feature/architecture decision (light or deep dive → ADR → plan) | `tasks/design.md` |
| Implement code against an already-`Accepted` ADR/plan | `tasks/implement.md` |
| Review a PR/diff in this repo | `tasks/pr-review.md` |
| Address/respond to feedback already posted on a PR | `tasks/respond-to-pr-feedback.md` |

`design.md` → `implement.md` → `pr-review.md` → `respond-to-pr-feedback.md`
is the full lifecycle of a non-trivial change in this repo, in order —
most requests only need one or two of these (e.g. a light-dive design
folded straight into implementation), but a genuinely new feature or
roadmap slice touches all four in sequence.

### References

| When you're about to... | Read |
|---|---|
| Decide where an architecture/feature decision belongs, run a design dive (light or deep) before writing code, write/reference an ADR (`docs/adr/`), or write/track a plan (`docs/plans/`) | `references/design-decisions.md` |
| Write or review any C# (naming, nullable, async, DI, error handling, file layout) | `references/coding-standards.md` |
| Add or change tests | `references/testing.md` |
| Handle anything touching the source generator's trust boundary, NuGet package supply chain, or secrets/config | `references/security.md` |
| Write or update `docs/*.md`, `README.md`, code comments, or commit messages | `references/documentation.md` |
| Review someone's work, or handle a disagreement about direction | `references/code-of-conduct.md` |
| Onboard a change end-to-end, or you're not sure what's expected of a PR | `references/contributing.md` |
| Post a comment or verdict on a PR review | `references/review-emoji-legend.md` |

Most non-trivial tasks touch more than one of these — e.g. adding a new
composition provider typically means reading `design-decisions.md` (where
does this decision live, is it a light or deep dive), `coding-standards.md`
(naming, DI), and `documentation.md` (update the subsystem doc in the same
PR). Read all of the ones that apply, not just the first match.
