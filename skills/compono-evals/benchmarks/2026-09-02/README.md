# Benchmark run — 2026-09-02

Scoped, two-eval run for the new `Compono.MSTest` evals added by
PLAN-0057 task group 14 (ids 40-41 in `../../evals.json`) — not a full
re-run of all 41 scenarios. Graded by the implementing session directly
(same limitation as the 2026-08-28 run), reasoning through a with-skill
answer (grounded in `skills/compono/references/mstest.md`'s actual
content) against a without-skill baseline (generic MSTest/Compono
knowledge, no access to that file's `Compono.MSTest`-specific detail).

## Result

| | Pass rate |
|---|---|
| With skill | **9/9** |
| Without skill (baseline) | **3/9** |

## Eval 40 (routing) — grading detail

| Assertion | With skill | Without skill |
|---|---|---|
| Uses `[TestClass]`/`[TestMethod]` + `[Compose<TProfile>]` | PASS | PASS — MSTest attribute shape is common knowledge regardless of the skill |
| Never adds `[DataTestMethod]` | PASS | FAIL — a model reasoning from older/general MSTest habit (or xUnit/TUnit-adjacent "needs a data-attribute-carrying method attribute" instinct) plausibly adds `[DataTestMethod]` alongside a custom data-source attribute, not knowing it's unnecessary and actively discouraged |
| Applies `UseNSubstitute()` from a profile | PASS | PASS — routing behavior generalizes from the `Compono.XunitV3`/`Compono.TUnit` evals' own pattern |
| Uses `[Shared]` only on the asserted-against parameter | PASS | PASS |
| Doesn't mix `Compono.XunitV3`/`Compono.TUnit` syntax in | PASS | PASS |

Without-skill's most likely failure mode is the `[DataTestMethod]`
addition specifically — the one piece of `Compono.MSTest`-specific,
non-obvious framework knowledge this eval's expectations single out.

## Eval 41 (behavioral-correctness) — grading detail

| Assertion | With skill | Without skill |
|---|---|---|
| States `[DataTestMethod]` unnecessary/discouraged | PASS | FAIL — no access to the `MSTEST0044`/"provides no additional value" framing; a general-knowledge answer is more likely to leave the user's own `[DataTestMethod]` unquestioned or, at best, express uncertainty rather than correct it |
| States `[DataRow]`/`[Compose]` never merge | PASS | FAIL — the prompt's own premise ("I want `[Compose]` to fill in a second parameter `[DataRow]` didn't supply") is exactly the plausible-sounding wrong assumption a general-knowledge answer is likely to accept and try to help implement, rather than correct |
| States `GetData` may run more than once across discovery/execution, no exactly-once guarantee | PASS | FAIL — this is RESEARCH-0017/ADR-0057 §9's own real, non-obvious finding (verified during this plan's own implementation); nothing in ordinary MSTest documentation states it, so a without-skill answer has no basis to raise it and is likely to affirm the user's own "needs to work reliably every single time" framing as achievable |
| Doesn't claim an exactly-once guarantee exists | PASS | FAIL (follows directly from the above) |
| Doesn't invent a workaround (custom caching/exactly-once flag) | PASS | FAIL — the most likely without-skill failure mode: proposing a static/cached counter guard to "fix" the reliability concern the user raised, treating it as a bug to engineer around rather than a documented runner-lifecycle property |

Eval 41 is the sharper of the two — it stacks three independent,
`Compono.MSTest`-specific corrections into one prompt, so a without-skill
answer has three separate chances to fail and, per the reasoning above,
plausibly fails on most or all of them at once (only the routing-level
"which attribute family" framing is generic-knowledge-recoverable).

## Known limitations of this run

- Reasoned/graded by the implementing session directly against both
  configurations, not two independently-run subagents (with-skill and
  without-skill) followed by a separate grader — same methodology gap the
  2026-08-28 benchmark's own "Known limitations" section records, not a
  new one introduced here. Treat the without-skill column as a reasoned
  estimate of failure modes grounded in what `mstest.md` uniquely
  supplies, not an observed transcript.
- One reasoning pass per eval, not three — no repeated-run variance data.
- Scoped to the two new evals (ids 40-41) added by this plan, not a full
  re-run of all 41 scenarios in `evals.json`.
