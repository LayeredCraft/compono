# Compono skill regression benchmark — 2026-08-27

Focused regression run for the AWS Secrets Manager Provider TestDoubles matching/capture skill fix.

## Scope

New evals added in `skills/compono-evals/evals.json`:

- 28 — `IConfigurationBuilder` argument-filtered verification
- 29 — `IAmazonSecretsManager` argument-matched configuration
- 30 — adversarial NSubstitute vocabulary trap
- 31 — true callback/capture boundary

## Runner

Executed with Pi print mode against the repo skill source:

```bash
pi --print --no-context-files --no-skills --skill <skill-path> --thinking minimal "<eval prompt>"
```

Claude Code was attempted first but was unavailable because the local account had hit its weekly limit. The attempted outputs contained only the limit message and were discarded.

## Baseline

This PR improves an existing skill, so per the skill-creator workflow each
`with_skill` run is paired against an `old_skill` baseline: `testdoubles.md`
was snapshotted at `644a5ad` (the commit before this PR's fix,
`5139507`/`caae88c`) and evals 28–31 were rerun against that snapshot with
the same runner and prompts. Outputs are in `outputs/old_skill/`.

## Results

| Eval | with_skill | old_skill | Evidence |
|---|---:|---:|---|
| 28 | Pass | **Fail** | `with_skill` uses `configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder)` and `configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(...)).Once()`, maps `Arg.Is` to `Match.Is`, no fake recommended. `old_skill` claims the argument predicate can't be asserted at all, uses zero-arg `Configure().Add()`/`Verify().Add().Once()`, and says the exact-argument assertion still needs NSubstitute. |
| 29 | Pass | Pass | `with_skill` uses `secretsManager.Configure().GetSecretValueAsync(Match.Is<GetSecretValueRequest>(...), Match.Any<CancellationToken>()).Returns(...)`; no fake/NSubstitute needed. `old_skill` independently reached the same correct shape for this prompt — the pre-fix doc gap didn't mislead this particular case. |
| 30 | Pass | **Fail** | `with_skill` maps `Arg.Is`/`Arg.Any`/`Received(1)`/`Received(2)`/`DidNotReceive` to `Match.Is`/`Match.Any`/`Verify().Once()`/`Exactly(2)`/`Never()` and rejects recording fakes solely for NSubstitute vocabulary. `old_skill` says `Arg.Any`/`Arg.Is` should "usually be removed" and lists argument-specific matching as explicitly unsupported. |
| 31 | Pass | **Fail** | `with_skill` distinguishes ordinary matching/filtering from invocation-aware callback/capture behavior and recommends a project-local fake for the unsupported callback boundary. `old_skill` reaches the same fake recommendation but for the wrong reason — it claims there is no `Match.Any<T>()`/`Match.Is<T>()` for generated doubles at all, contradicting the expectation that ordinary matching is supported. |

Focused new-regression pass rate: **with_skill 4/4 (100%)**, **old_skill 1/4 (25%)** —
the fix demonstrably corrects three of the four scenarios that the pre-fix
skill got wrong.

## Existing eval behavior

The existing eval definitions (1–27) were left unchanged; only evals 28–31 were appended. A full historical with/without-skill benchmark could not be rerun in this environment because Claude Code returned the local weekly-limit message. No comparable old-eval regression benchmark is recorded here.
