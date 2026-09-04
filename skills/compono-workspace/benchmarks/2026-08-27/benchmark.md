# Compono skill regression benchmark — 2026-08-27

Focused regression run for the AWS Secrets Manager Provider TestDoubles matching/capture skill fix.

## Scope

New evals added in `skills/compono/evals/evals.json`:

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
`with_skill` run is paired against an `old_skill` baseline, and both
configurations are launched together in the same batch rather than at
different times: the full `skills/compono` directory (`SKILL.md` +
`references/`, not just `testdoubles.md`) was snapshotted at `644a5ad`
(the commit before this PR's fix, `5139507`/`caae88c`), and all 8 runs
(evals 28–31 x with_skill/old_skill) were launched together against the
current skill and the `644a5ad` snapshot respectively, using the same
runner and prompts. Outputs are in `outputs/old_skill/`.

An earlier version of this benchmark *described* the baseline as covering
only `testdoubles.md` and ran `old_skill` in a separate pass after
`with_skill` had already been recorded — flagged in review as not a true
paired run and, separately, as under-scoped versus the fix (`5139507` also
changed `SKILL.md`, adding a guardrail section). The underlying snapshot
had in fact already captured the full directory including the pre-fix
`SKILL.md`, so only the description was wrong; the under-scoping concern
itself didn't hold,
but the not-launched-together concern was valid — this rerun replaces that
version.

## Results

| Eval | with_skill | old_skill | Evidence |
|---|---:|---:|---|
| 28 | Pass | **Fail** | `with_skill` uses `configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder)` and `configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(...)).Once()`, maps `Arg.Is` to `Match.Is`, no fake recommended. `old_skill` claims the argument predicate can't be expressed at all, uses zero-arg `Configure().Add()`/`Verify().Add().Once()`, and says asserting the argument type still requires keeping the test on NSubstitute. |
| 29 | Pass | **Fail** | `with_skill` uses `secretsManager.Configure().GetSecretValueAsync(Match.Is<GetSecretValueRequest>(...), Match.Any<CancellationToken>()).Returns(...)`; maps `Arg.Is`/`Arg.Any` directly to `Match.Is`/`Match.Any`. `old_skill` drops the argument matcher entirely, uses parameterless `Configure().GetSecretValueAsync().Returns(...)`, and states outright that `Compono.TestDoubles is argument-independent`. |
| 30 | Pass | **Fail** | `with_skill` maps `Arg.Is`/`Arg.Any`/`Received(1)`/`Received(2)`/`DidNotReceive` to `Match.Is`/`Match.Any`/`Verify().Once()`/`Exactly(2)`/`Never()` and rejects recording fakes solely for NSubstitute vocabulary. `old_skill` says `Arg.Any`/`Arg.Is` "usually disappears" and lists argument-specific returns/verification as behavior Compono.TestDoubles does not provide. |
| 31 | Pass | **Fail** | `with_skill` distinguishes ordinary matching/filtering from invocation-aware callback/capture behavior and recommends a project-local fake for the unsupported callback boundary. `old_skill` reaches the same fake recommendation but for the wrong reason — it claims there are "no argument matchers"/"no argument-aware behavior" at all, contradicting the expectation that ordinary matching is supported. |

Focused new-regression pass rate: **with_skill 4/4 (100%)**, **old_skill 0/4 (0%)** —
the fix demonstrably corrects all four scenarios the pre-fix skill got wrong.

## Existing eval behavior

The existing eval definitions (1–27) were left unchanged; only evals 28–31 were appended. A full historical with/without-skill benchmark could not be rerun in this environment because Claude Code returned the local weekly-limit message. No comparable old-eval regression benchmark is recorded here.
