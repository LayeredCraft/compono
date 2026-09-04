# Benchmark run — 2026-08-25

A focused with-skill/without-skill/grader run (same methodology as the
2026-08-07 run, `../2026-08-07/README.md`) covering the eval scenarios this
PR's changes could plausibly affect: the 5 new explicit-constructor-selection
evals (23-27, ADR-0002 Amendment 3/ADR-0052 Part B), eval 16 (its
`expected_output` was rewritten by this PR since `UseConstructor<...>()` is
now the primary fix for the CMP0001-ambiguous-HttpClient scenario it poses),
and eval 7 as an adjacent CMP00xx-diagnostics control. **Not** a full re-run
of all 27 evals — evals 1-6, 8-15, 17-22's subject matter is untouched by
this PR; their 2026-08-07 results still stand.

## Result

| | Pass rate |
|---|---|
| With skill | **100%** (22/22 assertions, across 7 scenarios) |
| Without skill (baseline) | **91%** (20/22 assertions) |

See `benchmark.md` for the per-eval breakdown.

## Key result

Zero failures for the with-skill variant on any expectation — including both
adversarial scenarios the driving product direction specifically asked to be
checked: eval 26 (with-skill correctly refuses `UseConstructor<...>()` for a
specific-pre-configured-`HttpClient`-instance case and recommends
`Register<T>()` instead) and eval 27 (with-skill correctly keeps `CMP0033`
and `CMP0034` distinct and never implies per-profile constructor-selection
scoping). No eval surfaced the skill recommending API behavior the shipped
implementation doesn't actually support.

## Known limitations (same as 2026-08-07's run, still true)

Baseline (`without_skill`) subagents retain full filesystem/tool access to
this repo — instructed not to read the skill, but several still reached
correct answers via `docs/reference/api/`, `docs/adr/`, and the generator
test files directly. This narrows the true with/without gap versus a
genuinely repo-isolated baseline; graders flagged evals 7, 23, 24, 26, and 27
as non-discriminating for this reason in this run specifically. The skill's
measured marginal value here concentrates in eval 16 (finding
`UseConstructor` at all, rather than only the older `TestHttpHandler`/
interface-wrapper pattern) and eval 25 (grounding the "no attribute
mechanism, ever" reasoning in BCL non-ownership rather than asserting it
unsupported).
