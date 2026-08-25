# Skill Benchmark: compono (constructor-selection scenarios)

**Model**: claude-sonnet-5
**Date**: 2026-08-25
**Evals graded**: 7 (composable/discovery, unaffected), 16 (HttpClient reflection refusal,
directly rewritten by this change), 23-27 (new constructor-selection evals)
**Method**: real with-skill / without-skill / independent-grader agent triples per eval,
per skill-creator's established process (see `.claude/skills/skill-creator/SKILL.md`).

Scope note: this is a focused re-run covering every eval this PR's SKILL.md/references/
evals.json changes could plausibly affect (the 5 new evals 23-27, plus eval 16 whose
expected answer changed because of this PR, plus eval 7 as an adjacent CMP00xx-diagnostics
control) - not a full re-run of all 27 evals in evals.json. The 2026-08-07 benchmark already
covers 1-18 pre-existing behavior; nothing in this PR touched evals 1-6, 8-15, 17-22's subject
matter.

## Summary

| Metric | With Skill | Without Skill |
|---|---|---|
| Pass rate (assertions) | 22/22 (100%) | 20/22 (91%) |

## Per-eval results

| Eval | With skill | Without skill | Discriminating? |
|---|---|---|---|
| 7 - [Composable] doesn't fix CMP0001 | 2/2 | 2/2 | No |
| 16 - HttpClient reflection refusal | 4/4 | 3/4 | **Yes** - baseline never mentions `UseConstructor`, routes only to the older `TestHttpHandler`/interface-wrapper pattern |
| 23 - CMP0001 -> UseConstructor (basic) | 3/3 | 3/3 | No |
| 24 - No `[CompositionConstructor]` attribute | 3/3 | 3/3 | No |
| 25 - BCL `Exception` UseConstructor | 3/3 | 2/3 | **Yes** - baseline doesn't ground the "attribute-free by design" reasoning in BCL non-ownership |
| 26 - UseConstructor vs. Register (specific instance) | 3/3 | 3/3 | No |
| 27 - CMP0033 vs. CMP0034 | 4/4 | 4/4 | No |

## Key result

**Zero failures for with_skill on any expectation**, across every scenario the user
explicitly asked to be checked "at minimum" - including the two adversarial "trap"
scenarios:
- Eval 26: with-skill correctly *rejects* `UseConstructor<...>()` for the specific-
  pre-configured-HttpClient-instance case and recommends `Register<T>()` instead.
- Eval 27: with-skill correctly distinguishes CMP0033 (compilation-wide conflict)
  from CMP0034 (no matching constructor) and never implies per-profile scoping is
  supported.

No eval revealed the skill recommending API behavior the implementation doesn't
actually support - every `UseConstructor<...>()` usage the skill recommended matches
the real, shipped `CompositionTypeRuleBuilder<T>.UseConstructor<...>()` overloads.

## Non-discriminating assertions (grader-flagged, real signal for a future evals.json pass)

- Eval 7, 23, 24, 26, 27: graders noted the without_skill baseline (full repo access
  minus the skill itself) reliably finds the same answer via `docs/reference/api/`,
  `docs/adr/`, and the generator test files - this repeats the same known baseline-
  isolation gap the 2026-08-07 benchmark's README already flagged. Not acted on here,
  consistent with that prior benchmark's own "recorded as scoped follow-up" precedent.
- Eval 23/26/27 baseline responses were, per the graders, comparably thorough to the
  with-skill responses in this run - the skill's marginal value on constructor-
  selection guidance is concentrated in evals 16 and 25 specifically (cases where the
  correct answer requires knowing to look at `registrations-profiles-and-scopes.md`'s
  UseConstructor section or the API reference doc, not just general repo exploration).

## Workspace

Full with_skill/without_skill responses, transcripts, and per-eval grading.json files
under this directory's `eval-*/` subdirectories.
