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
pi --print --no-context-files --no-skills --skill skills/compono --thinking minimal "<eval prompt>"
```

Claude Code was attempted first but was unavailable because the local account had hit its weekly limit. The attempted outputs contained only the limit message and were discarded.

## Results

| Eval | Result | Evidence |
|---|---:|---|
| 28 | Pass | Output uses `configurationBuilder.Configure().Add(Match.Any<IConfigurationSource>()).Returns(configurationBuilder)` and `configurationBuilder.Verify().Add(Match.Is<IConfigurationSource>(...)).Once()`; maps `Arg.Is` to `Match.Is`; no fake recommended. |
| 29 | Pass | Output uses `secretsManager.Configure().GetSecretValueAsync(Match.Is<GetSecretValueRequest>(...), Match.Any<CancellationToken>()).Returns(...)`; explicitly says no NSubstitute substitute or hand-written fake is needed. |
| 30 | Pass | Output maps `Arg.Is`/`Arg.Any`/`Received(1)`/`Received(2)`/`DidNotReceive` to `Match.Is`/`Match.Any`/`Verify().Once()`/`Exactly(2)`/`Never()` and rejects recording fakes solely for NSubstitute vocabulary. |
| 31 | Pass | Output distinguishes ordinary matching/filtering from invocation-aware callback/capture behavior and recommends a project-local fake or NSubstitute seam for the unsupported callback boundary. |

Focused new-regression pass rate: **4/4 (100%)**.

## Existing eval behavior

The existing eval definitions (1–27) were left unchanged; only evals 28–31 were appended. A full historical with/without-skill benchmark could not be rerun in this environment because Claude Code returned the local weekly-limit message. No comparable old-eval regression benchmark is recorded here.
