# `compono` skill eval workspace

Generated eval-execution evidence for the `compono` agent skill
(`skills/compono/`), kept alongside the skill directory per the
[Agent Skills eval-workspace convention](https://agentskills.io/skill-creation/evaluating-skills) —
authored eval definitions live in `skills/compono/evals/evals.json`;
everything here is generated output, never authored skill content.

## Structure

```
compono-workspace/
├── skill-snapshot/          # cp -r of skills/compono/ taken before an edit, when
│                             # comparing an iteration against the previous skill
│                             # revision rather than against no skill at all
├── iteration-N/
│   ├── eval-<case-name>/
│   │   ├── with_skill/
│   │   │   ├── outputs/     # files the run produced
│   │   │   ├── timing.json  # { "total_tokens": ..., "duration_ms": ... }
│   │   │   └── grading.json # assertion_results + summary, per evals.json's
│   │   │                     # "assertions"/"expectations" for that case
│   │   └── old_skill/       # or without_skill/ - see "Baseline choice" below
│   │       ├── outputs/
│   │       ├── timing.json
│   │       └── grading.json
│   ├── benchmark.json       # aggregated pass-rate/time/token stats for the iteration
│   └── feedback.json        # { "<eval-case-name>": "<human review note>", ... }
└── benchmarks/               # pre-convention historical runs (2026-08-07 through
                               # 2026-09-03) - kept as-is; each is its own dated
                               # README/benchmark.md, not restructured into the
                               # iteration-N/ shape above
```

## Baseline choice

`compono` is an established skill under active iteration, not a new skill
being evaluated from zero — per the convention above, a revision compares
against a `skill-snapshot/` of the previous version (`old_skill/`), not
against no skill (`without_skill/`), unless the run is specifically
measuring whether having the skill at all is worth it (in which case use
`without_skill/` and say so explicitly in that iteration's directory).

## Clean-context execution

Each with-skill/baseline run in an iteration starts from a fresh
subagent (the `Agent` tool's `general-purpose` type, or a fresh Claude
Code session if run outside this repo) — never a continuation of the
skill-development conversation. This is what makes a run's output
attributable to `SKILL.md` itself rather than to prior conversational
state. Each run is given: the skill path (or none, or the snapshot path),
the eval's `prompt`, its `files` (if any), and a distinct output directory
so concurrent with-skill/baseline runs can never collide.

## Grading

Grade each case's `assertions`/`expectations` (from `evals/evals.json`)
against that run's `outputs/`, with concrete evidence per PASS/FAIL —
per this repo's own established practice (see `benchmarks/`), grading is
currently done by direct reasoning against the skill's actual reference
content, not an automated content-grading harness;
`.agents/skills/skill-creator/scripts/run_eval.py` is a *trigger*-eval
runner (does the skill activate at all), not a content grader. Prefer a
deterministic script wherever an assertion is mechanically checkable
(valid syntax, a specific API name present/absent); reserve reasoning-based
grading for assertions that genuinely require it.
