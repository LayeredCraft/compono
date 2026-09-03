# [RESEARCH-0020] Repository-Wide Comment Quality Audit

**Status:** Complete — no follow-up work warranted.

## Why this exists

The original pre-1.0 repository-wide cleanup audit (which produced
PLAN-0061) didn't evaluate comments as an explicit dimension. This is a
narrow, dedicated follow-up: audit every comment-bearing area of the
repository against `references/coding-standards.md`'s already-stated
policy ("Default to writing no comments. Only add one when the WHY is
non-obvious... Don't explain WHAT the code does") and classify what's
actually there.

## Method

Read-only. Comment-line inventory by area (grep-counted), followed by
in-context reading (not grep-snippet reading) of a representative sample
across categories most likely to surface a problem if one existed:
files with the heaviest ADR/PLAN/PR-number reference density, files with
unusually long comment blocks, and a control sample of ordinary
"workhorse" files unlikely to be special-cased. Targeted greps for
classic narration smells (`// This method...`, `// Loop through...`,
`// Set the...`, restated-trivial-property comments) were run across all
of `src/`, `test/`, `samples/`.

## Inventory (comment lines / total lines)

| Area | Comment / total lines | Density |
|---|---|---|
| `src/Compono` | 2563 / 5242 | ~49% |
| `src/Compono.Generators` | 2810 / 7410 | ~38% |
| `src/Compono.XunitV3` | 475 / 1021 | — |
| `src/Compono.TUnit` | 403 / 965 | — |
| `src/Compono.MSTest` | 350 / 919 | — |
| `src/Compono.NUnit` | 345 / 893 | — |
| `src/Compono.Bogus` | 234 / 459 | — |
| `src/Compono.Http` | 186 / 433 | — |
| `src/Compono.Logging` | 289 / 720 | — |
| `src/Compono.DependencyInjection` | 139 / 270 | — |
| `src/Compono.NSubstitute` | 43 / 97 | — |
| `src/Compono.TestDoubles` | 19 / 37 | — |
| `test/` | 7041 / 52257 | ~13.5% (largest absolute volume, lowest density) |
| `samples/` | 88 / 358 | — |
| `.github/workflows/` | 138 / 696 | — |
| `.github/scripts/` | 229 / 782 | — |

Density is highest exactly where invariants are subtlest (core engine,
generator) and lowest in samples and mechanical test bodies — consistent
with the stated policy being actually followed, not a random accumulation.

## Findings by classification bucket

- **Durable why** — the large majority of everything read. Representative
  examples: `PositionalArgumentBinder.cs:47-50` (a CLR nullable-boxing
  rule explaining why an unwrap-first step is required, not optional);
  `TransitiveClosureWalker.cs:111-114` (a defensive narrowing explicitly
  documented as intentional rather than an oversight a reader might
  "fix"); `generate-api-reference.sh:19-25` (a DefaultDocumentation
  cross-link fallback that would silently 404 if simplified away).
- **ADR/PLAN breadcrumb that earns its keep** — `TestDoubleAnalyzer.cs`'s
  ~135 `Codex review, PR #106/#108`-style references each sit next to a
  collision-safety rule that has been *wrong in review before* — the
  breadcrumb is exactly what stops a future maintainer (or a coding
  agent) from "helpfully" reverting to the buggy shape. Same reasoning
  applies to `MatchingTests.cs`'s per-fixture-interface comments — the
  fixture's entire purpose is regression-proofing one specific finding,
  so the comment *is* the information, not decoration.
- **ADR/PLAN breadcrumb that doesn't earn its keep** — none found in the
  sampled files.
- **Restates the code** — none found; the targeted narration-smell greps
  returned zero matches across the entire `src/`, `test/`, `samples/`
  tree.
- **Historical narration with no current value** — none found. Every
  "we tried X, it broke because Y" comment sampled was load-bearing: it
  explains why the *current* shape must stay, not merely what changed.
- **Large prose block obscuring simple code** — `TestDoubleAnalyzer.cs`
  is the one real candidate, at the file level (2310 lines, extremely
  dense). This is not a new finding — the original pre-1.0 cleanup audit
  already flagged this exact file as a NEEDS-SPIKE decomposition
  candidate specifically because of its hard-won correctness history.
  Any fix here is a structural decomposition question with real risk,
  not a comment-trimming exercise — see PLAN-0061's Explicitly Deferred
  list, unchanged by this audit.
- **Compensating for unclear naming/structure** — none found.

## Removal / shortening / retain lists

- **Remove**: none identified.
- **Shorten**: none identified beyond the `TestDoubleAnalyzer.cs` volume
  concern, which is structural (see above), not a comment-level edit.
- **Retain (representative, to calibrate what "good" looks like here)**:
  `PositionalArgumentBinder.cs:47-50`, `TransitiveClosureWalker.cs:111-114`,
  every `MatchingTests.cs` fixture-interface comment,
  `generate-api-reference.sh`'s header block.

## Assessment

This codebase already conforms tightly to its own stated comment policy.
This is not a repository that drifted and needs a cleanup pass — the
evidence (targeted greps returning zero hits for every classic narration
smell, no removal candidates surfacing anywhere in a deliberately
adversarial sample) reads as actively enforced, consistent with the
heavy, iterated review history (Codex rounds) already visible in the
comments themselves.

**How much of this is mechanical vs. judgment-requiring**: neither —
there is no backlog to apply either mode to. A "the repo is already
clean" finding is itself a complete, valid result of this audit, not an
absence of effort.

## Tier classification

**Tier 3 — leave alone, entirely.** No Tier 1 or Tier 2 findings anywhere
in this audit.

## Proposed implementation scope

None warranted. The only actionable item adjacent to this audit is the
already-known `TestDoubleAnalyzer.cs` decomposition spike carried forward
unchanged from the original pre-1.0 cleanup audit (PLAN-0061's
Explicitly Deferred list) — this is not new work this audit discovered.

## Relationship to 1.0

- Nothing here should block PLAN-0061 Phase 2.
- Nothing here should block 1.0.
- Nothing found here becomes harder or riskier to change after 1.0 —
  there is nothing to change.

## Links

- `references/coding-standards.md` — the comment policy this audit
  checked actual comments against (not a new policy authored here).
- [PLAN-0061](../plans/0061-pre-1-0-cleanup-and-consolidation.md) — the
  pre-1.0 cleanup plan this audit follows up on; `TestDoubleAnalyzer.cs`'s
  decomposition remains tracked there (Explicitly Deferred), not here.
