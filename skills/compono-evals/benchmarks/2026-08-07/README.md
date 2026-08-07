# Benchmark run — 2026-08-07

A `/skill-creator`-*style* eval run against all 18 scenarios in
`../../evals.json` (superseding the 6-scenario manual spot-check recorded
earlier in PLAN-0035) — not `/skill-creator`'s full documented workflow;
see "Known limitations" below for exactly where it's lighter-weight.
With-skill and baseline (`without_skill`, no access to the skill's
SKILL.md/references) subagents ran independently for every scenario,
each graded by a separate grader subagent against that scenario's
`expectations`.

## Result

| | Pass rate |
|---|---|
| With skill | **97.4%** (38/39 individual assertions, across 18 scenarios) |
| Without skill (baseline) | **56.4%** (22/39 individual assertions) |

See `benchmark.json`/`benchmark.md` for the full breakdown, and
`grading/eval-<id>-<name>/{with_skill,without_skill}.json` for the
per-scenario, per-assertion evidence.

## Known limitations of this run

- **One run per configuration, not three.** `/skill-creator`'s default
  workflow runs each configuration 3× to distinguish real skill effect
  from run-to-run noise. This run is 1×18 per configuration — the 97%
  vs. 58% gap is a real, evidence-backed signal, but the stddev reported
  in `benchmark.md` reflects variance *across the 18 different prompts*,
  not repeated-run noise on the same prompt. Don't over-read precision
  into it.
- **Baseline subagents weren't repo-isolated.** The `without_skill` runs
  were told not to read the skill, but ran with full filesystem/tool
  access to this repo (same as the with-skill runs). At least one
  baseline (eval 9) still produced accurate Compono-specific terminology
  — most likely by exploring the repo directly rather than actually
  lacking the knowledge. This likely *understates* the skill's true
  marginal value relative to a genuinely repo-isolated baseline (e.g. a
  fresh consumer project with no access to Compono's own source).
- **No timing/token data.** `timing.json`/`metrics.json` were never
  captured, at any point — not merely omitted from what's committed.
  `benchmark.md`'s Time/Tokens rows are genuinely empty, not just
  unpublished.
- **`eval_metadata.json`/per-run `outputs/response.md` aren't retained.**
  They existed in ephemeral scratch space during the run but were never
  committed — `grading.json` (per scenario, per variant) and the
  aggregated `benchmark.json`/`benchmark.md` are the actual durable
  record here, not the full per-run artifact set `/skill-creator`'s
  workflow produces.

## Eval-quality feedback surfaced by graders

Several graders flagged specific assertions in `evals.json` as weakly
discriminating — passing regardless of whether the skill was used, or
passing "by omission" rather than by genuinely correct reasoning. This is
real signal for the next iteration of `evals.json`, not noise:

- **Eval 1**: without_skill passed all 3 assertions too — the public API
  shape here (`Composer.Create()`/instance `Create<T>()`) turned out to
  be inferable from repo access alone. Suggested tightening assertions to
  check details that are only documented in the skill's references (seed
  forking, `[Shared]` binding order), not commonly-inferable public API.
- **Eval 6**: the "no invented named-arg syntax" assertion passes
  vacuously for any response that avoids `[Compose(...)]` entirely.
  Suggested adding an assertion that explicitly checks the response uses
  Compono's real `[Compose]`/`[Compose<TProfile>]` attribute.
- **Eval 8, 10, 14, 15, 18**: several "does not do X" negative assertions
  pass trivially for a baseline that never considered doing X in the
  first place, indistinguishable from a response that deliberately
  declined. Suggested adding positive assertions that check for
  skill-attributed reasoning, not just absence of the wrong behavior.
- **Eval 9**: "does not mention Compono" penalizes a response that
  correctly names Compono while explaining it's out of scope — the same
  as it would penalize actually misapplying Compono. Reword to target
  the harmful behavior (recommending Compono APIs as if required), not
  the word itself.
- **Eval 16**: the "offers a legitimate alternative" assertion would also
  pass a response that buries one correct suggestion among invented,
  unverifiable ones. Suggested rewarding grounded specificity, not just
  presence of *a* legitimate-sounding option.
- **Eval 17**: without_skill independently reasoned its way to the same
  cautious, investigate-first framing without any skill guidance — the
  only observed differentiator was citing SKILL.md by name. Suggested a
  stronger assertion probing skill-specific diagnostic content instead.

None of these are acted on in this pass — recorded here as a scoped
follow-up for whoever next revises `evals.json`, per this repo's
"deferred work still gets tracked" convention.
