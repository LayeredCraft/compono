# Skill Benchmark: compono

**Model**: claude-sonnet-5
**Date**: 2026-08-07T14:55:57Z
**Evals**: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 (1 run each per configuration)

## Summary

| Metric | With Skill | Without Skill | Delta |
|--------|------------|---------------|-------|
| Pass Rate | 97% ± 12% | 58% ± 40% | +0.39 |
| Time | 0.0s ± 0.0s | 0.0s ± 0.0s | +0.0s |
| Tokens | 0 ± 0 | 0 ± 0 | +0 |

## Notes

- Single run per configuration (not 3) — pass-rate stddev reflects variance across the 18 different eval prompts, not repeated-run noise on the same prompt. Treat percentages as directional, not statistically tight.
- time_seconds and tokens are 0 for every run — no timing.json/metrics.json was captured per run in this pass, so those columns are not meaningful; do not read the 0s as "instant"/"free".
- Methodology gap flagged by multiple graders: without_skill (baseline) subagents retained full filesystem/tool access to this repo, even though instructed not to read the skill. At least one baseline (eval 9) still produced accurate Compono-specific terminology, most likely by exploring the repo directly rather than being told not to. This likely narrows the true with/without gap versus a baseline run in a genuinely repo-isolated environment.
- Several individual graders flagged specific assertions as weakly discriminating (pass regardless of skill use) — see grading.json eval_feedback fields for evals 1, 6, 8, 9, 10, 14, 15, 16, 17, 18. These are real signal for the next iteration of evals.json, not noise.