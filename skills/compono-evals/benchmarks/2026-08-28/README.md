# Benchmark run — 2026-08-28

Scoped, single-eval run for the new `Share<T>()` eval added by PLAN-0056
Task 6 (id 34 in `../../evals.json`) — not a full re-run of all 34
scenarios. With-skill and without-skill (`without_skill`, told not to
read `skills/compono/`) subagents ran independently against eval 34's
prompt, graded manually against its `expectations` (5 assertions).

## Result

| | Pass rate |
|---|---|
| With skill | **5/5** |
| Without skill (baseline) | **2/5** |

## Grading detail

| Assertion | With skill | Without skill |
|---|---|---|
| Recommends `Share<T>()` declared once (e.g. in a profile), not `[Shared]` repeated on every theory | PASS | FAIL — never mentions `Share<T>()`; recommends a hand-rolled `Register<T>(() => _logger)` field instead |
| Shows the retrieving parameter as an ordinary, undecorated parameter with no `[Shared]` | PASS | FAIL — no such parameter shown at all |
| Explicitly states `Share<T>()` does NOT share across separate `Create<T>()` calls / `CreateMany<T>()` items | PASS | FAIL (by omission) — correctly says two `Create<T>()` calls don't share by default, but never addresses `Share<T>()` specifically since it never introduces it |
| Does not describe `Share<T>()` as a service locator / ambient retrieval | PASS | PASS (vacuous — never mentions the mechanism) |
| Does not conflate `Share<T>()` and `[Shared]` | PASS | PASS (vacuous — never mentions `Share<T>()`) |

With-skill's response correctly used `builder.UseLogging().Share<ILogger<OrderService>>()` in a profile, showed the ordinary undecorated parameter, flagged the profile-level blast-radius caveat unprompted, and explicitly corrected the second question (`Share<T>()` is graph-scoped, not composer-wide, with a code example showing two independent `Create<T>()` calls *not* sharing).

Without-skill's response has no access to `Share<T>()` at all (it doesn't exist without the skill's documentation surfacing it prominently — it correctly reasoned about `Create<T>()`'s per-call graph-isolation from first principles, but solved the actual ask with a manual instance-pinning workaround rather than the real API), which is exactly the gap this eval was written to detect.

## Known limitations of this run

- One run per configuration, not three — same caveat as the 2026-08-07
  benchmark; a single-prompt gap this large (5/5 vs. 2/5, 3 non-vacuous
  assertion failures) is real signal, not noise, but no repeated-run
  variance data exists for it.
- Graded by the implementing session directly, not an independent grader
  subagent — unlike the 2026-08-07/2026-08-25 runs' separate grader step.
- Scoped to the one new eval (id 34) added by this plan, not a full
  re-run of all 34 scenarios in `evals.json`.
