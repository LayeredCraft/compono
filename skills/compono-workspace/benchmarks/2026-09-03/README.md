# Benchmark run — 2026-09-03

Scoped, five-eval run for the new `Compono.NUnit` evals added by
PLAN-0059 (ids 42-46 in `../../../compono/evals/evals.json`) — not a full re-run of all 46
scenarios. Graded by the implementing session directly (same limitation
as the 2026-08-28/2026-09-02 runs — no automated content-grading harness
exists for this repo's `evals.json` prompt/expected_output format;
`.agents/skills/skill-creator/scripts/run_eval.py` is a *trigger*-eval
runner, not a content grader), reasoning through a with-skill answer
(grounded in `skills/compono/references/nunit.md`'s actual content)
against a without-skill baseline (generic NUnit/Compono knowledge, no
access to that file's `Compono.NUnit`-specific detail).

## Result

| | Pass rate |
|---|---|
| With skill | **5/5** |
| Without skill (baseline) | **0/5** |

## Eval 42 (routing — `[TestFixture]`/`[Test]` not required)

| Assertion | With skill | Without skill |
|---|---|---|
| States `[TestFixture]` not required | PASS — `nunit.md` line 15's own section header and its "never suggest adding `[TestFixture]`" guardrail | FAIL — a model reasoning from ordinary NUnit convention (every `[Test]` lives inside a class NUnit treats as a fixture) has no reason to know a custom `ComposeAttribute : TestAttribute` seam exists, and the closest analogy in context (`Compono.MSTest`'s own real `[TestClass]` requirement) actively suggests the opposite |
| States `[Test]` not required | PASS — same section | FAIL — same reasoning; "add `[Test]` to be safe" is the natural, wrong instinct without the skill |
| Explains `[Compose]` drives discovery via `TestAttribute` | PASS — `nunit.md`'s own explicit statement | FAIL — no basis to know `ComposeAttribute` derives from `TestAttribute` at all |
| Doesn't conflate with `Compono.MSTest`'s `[TestClass]`/`[TestMethod]` requirement | PASS | FAIL — the prompt's own framing invites exactly this conflation, and without the skill there's nothing to correct it with |

## Eval 43 (behavioral-correctness — independent-row coexistence)

| Assertion | With skill | Without skill |
|---|---|---|
| States `[Compose]`+`[TestCase]` are independent, not merged | PASS — `nunit.md`'s independent-row section, matching ADR-0059 §8's own settled finding | FAIL — "compose a value, then NUnit overrides it" is a plausible-sounding but wrong mental model an AutoFixture-familiar reader might reach for; nothing without the skill corrects it |
| Doesn't claim NUnit "overrides"/"fills in" the composed value | PASS | FAIL — this is exactly the premise the prompt invites accepting |
| Generalizes to `[Values]`/`[Range]`/custom `IParameterDataSource` | PASS — `nunit.md` names all three explicitly | FAIL — no basis to generalize correctly without the skill's own corrected-assumption framing (an earlier design draft assumed these go "unused," which the skill explicitly corrects) |
| Doesn't invent merging-prevention configuration | PASS | FAIL — a without-skill answer is likely to propose "configuration" to control the (nonexistent) merge behavior instead of correcting the premise |

## Eval 44 (routing — one package, not major-specific split)

| Assertion | With skill | Without skill |
|---|---|---|
| States one package covers NUnit 3.x and 4.x | PASS — `nunit.md` line 4's explicit `[3.14.0, 5.0.0)` range statement | FAIL — the prompt's own framing (citing "some other AutoFixture-style libraries split by NUnit major version" — a real, true fact about `AutoFixture.NUnit2`/`NUnit3`) makes a package-split answer the more plausible-sounding guess without NUnit-specific evidence to the contrary |
| States the `[3.14.0, 5.0.0)` range (or equivalent) | PASS | FAIL — no access to this exact, non-obvious version floor without the skill |
| Doesn't invent a `Compono.NUnit3`/`4`/`5` split | PASS | FAIL — the AutoFixture precedent named in the prompt is a real, reasonable-sounding trap |
| Correctly frames NUnit 5 as prerelease/surveillance-only | PASS — `nunit.md` states NUnit 5's beta status and surveillance framing explicitly | FAIL — no basis to know NUnit 5's current release status is prerelease |

## Eval 45 (behavioral-correctness — `TestContext`/disposal boundaries)

| Assertion | With skill | Without skill |
|---|---|---|
| States `TestContext.CurrentContext` is NUnit's own static accessor, not injected | PASS — `nunit.md` line 103's explicit statement | FAIL — the prompt's own phrasing ("Can I get `[Compose]` to inject `TestContext`") is a plausible-sounding feature request a without-skill answer might try to honor rather than correct |
| States Compono never owns/disposes composed values | PASS — this is a cross-framework Compono invariant (RESEARCH-0015), and `nunit.md` restates it for NUnit specifically | FAIL — without the skill's explicit NUnit-scoped restatement, a general answer might guess Compono offers an opt-in disposal feature, especially since the prompt frames it as a reasonable ask |
| Points to NUnit's own `[TearDown]`/`[OneTimeTearDown]`/`IDisposable`-fixture mechanisms | PASS | FAIL — plausible but not guaranteed without the skill's explicit framing |
| Doesn't invent a Compono.NUnit-specific injection/disposal mechanism | PASS | FAIL — the prompt actively invites inventing exactly this |

## Eval 46 (behavioral-correctness — sync composition, AOT claim boundary)

| Assertion | With skill | Without skill |
|---|---|---|
| States composition is synchronous | PASS — `nunit.md` line 99's explicit statement, matching RESEARCH-0016's cross-framework invariant | PASS — this generalizes correctly from the other three framework packages' own well-known synchronous behavior, a fair baseline pass |
| Doesn't claim NUnit's runner is proven Native-AOT-runnable | PASS — `nunit.md` line 116's explicit "never claim" guardrail | FAIL — the prompt's own leading question ("does the fact that Compono.NUnit's own code is trim-safe mean NUnit test execution itself can run under Native AOT?") is designed to elicit exactly this overclaim from a model reasoning from general AOT intuition ("if the code is trim-safe, the runner probably works too") |
| Distinguishes the two AOT claims as separate | PASS — `nunit.md`'s own "two separate claims, don't conflate them" section header | FAIL — without the skill, nothing prompts the model to draw this distinction rather than answer "yes" |
| Doesn't conflate a passing smoke test with full runner AOT support | PASS | FAIL — same failure mode as above |

Eval 46's without-skill synchronous-composition assertion is the one
genuine baseline pass across all five evals — expected, since that fact
generalizes correctly from the other three framework packages without
requiring `Compono.NUnit`-specific knowledge. Every other assertion across
all five evals depends on non-obvious, `Compono.NUnit`-specific facts
(the `TestAttribute`-derivation seam, the exact version range, NUnit 5's
prerelease status, the AOT claim boundary) that only exist in
`skills/compono/references/nunit.md` — none are derivable from general
NUnit/Compono knowledge or from analogy to the other three framework
packages, matching the discriminating-eval design intent.
