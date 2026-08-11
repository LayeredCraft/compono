# Plans

This directory is the execution tracker for non-trivial work in this repo —
see `.agents/skills/engineering-workflow/references/design-decisions.md`'s
"Writing a Plan" section for the full process of *when* and *how* to write
one. This file is just the mechanics: numbering, status, and the index.

## Numbering and lifecycle

- Files are `NNNN-kebab-case-title.md`, zero-padded to 4 digits,
  sequential (`0001-...`, `0002-...`, ...). `0000-plan-template.md` is the
  template, not a real plan — the first real plan is `0001`.
- Unlike an ADR, a plan is a **living document** — edit its Tasks section
  as work proceeds, checking items off or adjusting them if reality
  diverges from what was scoped. A plan being wrong about *how* something
  gets built doesn't require superseding anything, unlike an ADR being
  wrong about *what/why*.
- A plan usually implements a single ADR (same number, per
  `design-decisions.md`), but doesn't have to — a `docs/mvp.md` roadmap
  milestone that draws on more than one already-`Accepted` ADR (e.g.
  [PLAN-0001](0001-milestone-1-source-generation-foundation.md), which
  builds on both ADR-0002 and ADR-0003) gets its own plan number instead,
  with its `Implements` line listing every ADR it draws on. What a plan
  may **not** do is start `In Progress` against a decision that's still
  `Proposed` — every ADR it implements must already be `Accepted`.

## Status lifecycle

`Not Started` → `In Progress` → `Done`

- **Not Started**: drafted, but no code written against it yet. A plan can
  be drafted alongside a still-`Proposed` ADR (designing the how often
  surfaces questions about the what), but stays `Not Started` until every
  ADR it implements is `Accepted`.
- **In Progress**: at least one task is checked off, or work is actively
  underway.
- **Done**: every task checked off and the plan's Test Plan actually
  executed. The plan doesn't get deleted afterward — it settles into a
  historical record of how the work actually happened, same spirit as an
  accepted ADR, just for execution detail instead of decision detail.

## Index

| Plan | Title | Status |
|---|---|---|
| [0001](0001-milestone-1-source-generation-foundation.md) | Milestone 1: Source-Generation Foundation | Done |
| [0002](0002-milestone-2-core-composition-engine.md) | Milestone 2: Core Composition Engine | Done |
| [0003](0003-milestone-3-profiles-and-configuration.md) | Milestone 3: Profiles and Configuration | Done |
| [0004](0004-milestone-4-xunit-integration.md) | Milestone 4: xUnit v3 Integration | Done |
| [0005](0005-milestone-5-nsubstitute-integration.md) | Milestone 5: NSubstitute Integration | Done |
| [0006](0006-milestone-6-bogus-integration.md) | Milestone 6: Bogus Integration | Done |
| [0007](0007-milestone-7-dogfooding.md) | Milestone 7: Dogfooding | Done |
| [0008](0008-milestone-8-public-preview.md) | Milestone 8: Public Preview | Done |
| [0035](0035-compono-agent-skill-pack.md) | Compono Agent Skill Pack | Done |
| [0036](0036-call-site-values-influencing-nested-composition.md) | Call-Site Values Influencing Nested Composition | Done |
| [0037](0037-netstandard2.1-compatibility-floor.md) | netstandard2.1 Compatibility Floor | Superseded by PLAN-0038 |
| [0038](0038-net8-net9-explicit-multi-target.md) | net8.0/net9.0 Explicit Multi-Target | Done |
| [0039](0039-future-extension-package-admission-gate-and-release-sequence.md) | Future Extension Package Admission Gate and Release Sequence | In Progress |
