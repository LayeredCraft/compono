# [RESEARCH-0005] `lightsaber-skill` Third Dogfood: Configuration-Required Members

**Status:** Done (dogfooding pass complete; migration merged to a local
branch in `lightsaber-skill`, not yet pushed/PR'd against that repo — see
"Scope" below)

**Feeds:** [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)
(`Accepted`) and [PLAN-0045](../plans/0045-testdoubles-configuration-required-members.md)
Phase 4 — see "Decisions" below

This document is the evidence record for PLAN-0045 Phase 4: re-running the
exact `ncipollina/lightsaber-skill` migration analysis RESEARCH-0004 ran,
against the shipped ADR-0045/`CMP0032` configuration-required dispatch
(`Compono`/`Compono.TestDoubles`/`Compono.NSubstitute`/`Compono.XunitV3`
`0.5.0-preview.73`). This is the third dogfooding pass against this real
project — the first (prose only, folded into ADR-0044's Context) found
`ILogger<T>` and two others blocked by overloads/generics; the second
(RESEARCH-0004) found those fixed but a new dominant blocker, `CMP0025`
whole-interface rejection; this third pass checks whether ADR-0045's fix
for that actually lets real tests drop `Compono.NSubstitute`, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-over-prediction bias.

## Scope

A real branch (`build/deps-bump-compono-preview-73` in `lightsaber-skill`,
commits `192d334` and `8078054`) bumped `Compono`/`Compono.NSubstitute`/
`Compono.XunitV3` from `0.1.0` to `0.5.0-preview.73`, added
`Compono.TestDoubles` at the same version with
`ComponoGeneratedTestDoubles=true`, and added a `GeneratedTestDoublesProfile`
(`ICompositionProfile` chaining `UseGeneratedTestDoubles()` then
`UseNSubstitute()`, per the documented provider-precedence rule). Every
interface the suite's ~44 original NSubstitute call sites touch was probed
with a real `[Compose<GeneratedTestDoublesProfile>]`-parameterized test
method and a `dotnet build -v:diag` run, reading the actual `CMP00xx`
diagnostics emitted. The five real production test files were then
actually migrated (not just probed) and the full 77-test suite run to
verify real, not just theoretical, success. The branch is committed
locally in `lightsaber-skill`, not merged/PR'd — this document is the
evidence record either way, matching RESEARCH-0004's own precedent of
recording a real branch's result without merging.

## Result: interface generation

Of the seven interfaces the suite depends on, **six now generate and
resolve cleanly** under `UseGeneratedTestDoubles()` alone — a reversal of
RESEARCH-0004's finding that only `ILogger<T>` worked:

| Interface | RESEARCH-0004 (v2, pre-ADR-0045) | This pass (post-ADR-0045) | Notes |
|---|---|---|---|
| `ILogger<T>` | generates | generates | unchanged (regression check passes) |
| `IResponseBuilder` | `CMP0025`-rejected | **generates, resolves** | `CMP0032`: 21 members configuration-required |
| `ISkillMediator` | `CMP0025`-rejected | **generates, resolves** | `CMP0032`: 1 member configuration-required (`Send`) |
| `IOptions<LightsaberOptions>` | `CMP0025`-rejected | **generates, resolves** | `CMP0032`: 1 member (`Value`) |
| `ILambdaContext` | `CMP0025`-rejected | **generates, resolves** | `CMP0032`: 12 members |
| `IHandlerInput` | `CMP0025`-rejected | **generates, resolves** | `CMP0032`: 3 members |
| `IAmazonS3` | `CMP0025`-rejected | generates (`CMP0021`), **fails to resolve alone** | needs `Compono.NSubstitute` fallback — see below |

`CMP0025` did not fire once across all seven interfaces in this pass —
direct confirmation that ADR-0045's configuration-required dispatch
closes the exact gap RESEARCH-0004 found.

## Result: `IAmazonS3`'s remaining blocker is a different, narrower one

`IAmazonS3` generates without diagnostic error (`CMP0021`, informational),
but composing it through `UseGeneratedTestDoubles()` alone throws
`Compono.CompositionException` at runtime: *"No registration, ...,
test-double provider, ... could satisfy 'IAmazonS3'."* Reading the
generated source
(`AlexaVoxCraft.MediatR.Response.IResponseBuilder_af73d88a.TestDouble.g.cs`-equivalent
for `IAmazonS3`) and the `CMP0021` message confirms why: `IAmazonS3`
declares a **static abstract member** (`CreateDefaultClientConfig`), which
`Compono.TestDoubles` explicitly doesn't support (`docs/packages/compono-testdoubles.md`'s
"What it deliberately doesn't do" section, backed by
[ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
Non-Goals). `CMP0021`'s "falls back to the ordinary runtime-provider path"
means the *entire interface* defers to whatever other provider is
registered — not that the other 20+ members generate while just that one
doesn't. Chaining `UseNSubstitute()` after `UseGeneratedTestDoubles()`
(the documented precedence pattern) resolves this: `IAmazonS3` requests
fall through to NSubstitute in full, exactly as before this pass, while
the other six interfaces still resolve via the generated provider.

This is a narrower, real, and different blocker than `CMP0025` was — a
static-abstract member is a genuinely unimplementable shape (matches
ADR-0042's own scope boundary), not a case ADR-0045 was meant to cover.
Classified **not a bug, not a new roadmap candidate** — see
"Classification" below.

## Result: real test migration

The five real production test files were migrated, not just probed:

| File | Result |
|---|---|
| `HandlerHelpers.cs` | fully migrated — `CreateDefaultMockResponseBuilder`/`CreateDefaultMockHandlerInput` now use `Composer.Create(...).Create<T>()` + `Configure()`, no `Substitute.For` |
| `ErrorHandlerTests.cs` | fully migrated — `[Compose<GeneratedTestDoublesProfile>]`, `Configure()`/`.Returns()`/`.Throws()` |
| `UnhandledMessageTests.cs` | fully migrated, same pattern |
| `LambdaHandlerTests.cs` | fully migrated — `ISkillMediator.Send` config via `Configure().Send().Returns(...)`; call-count verification via `Verify().Send().Once()` |
| `LightsaberHandlerTests.cs` | **partially migrated** — `IOptions<LightsaberOptions>` now uses `Configure().Value().Returns(...)`; `IAmazonS3` stays on raw `Substitute.For`/`Arg.Any`/`.Returns()`, unavoidably |

**4 of 5 test files fully dropped `Compono.NSubstitute`/`NSubstitute` API
usage.** Explicit NSubstitute call sites (`Substitute.For<T>()`,
`Arg.Any`/`Arg.Is`, `.Received(...)`, raw `.Returns()`/`.Throws()` outside
`Configure()`) dropped from **~44 to ~9**, all concentrated in
`LightsaberHandlerTests.cs`. All 77 tests pass after migration (verified
by running the built suite directly, not just compiling).

**`Compono.NSubstitute` cannot be removed from the project.**
`LightsaberHandlerTests.cs` composes `IAmazonS3` in three test methods and
one private helper (`CreateDefaultLightsaberHandler`); all four still need
a real NSubstitute-backed double. This is the honest limit: per PLAN-0045's
own acceptance criterion ("can real tests remove `Compono.NSubstitute`,"
not "do more interfaces generate"), the suite as a whole still requires
both providers side by side — `GeneratedTestDoublesProfile` chains both
for exactly this reason, and removing the `Compono.NSubstitute` package
reference would break the one file that still needs it.

One correctness pitfall surfaced during migration, worth recording:
`IResponseBuilder.Speak` and `.Reprompt` are each overloaded (a `string`
overload and a `params ISsml[]` overload). A zero-argument
`.Configure().Speak()` call compiles and *silently* selects the
`params ISsml[]` overload's configuration slot (zero arguments trivially
satisfies a `params` parameter) — not the `string` overload the real
handler code invokes, leaving that path armed with
`TestDoubleNotConfiguredException`. The compiler gives no warning; this
was only caught by actually running the migrated tests, not by the build
succeeding. Fixed by passing an explicit `string` discriminator
(`.Configure().Speak(default(string)!)`). This reinforces PLAN-0045's own
"generation succeeding proves nothing about correctness" caution, and
extends it: for an overloaded member, *building successfully* also proves
nothing about which overload got configured.

## Result: performance observation

Methodology: `hyperfine --warmup 3 --runs 15` against the built
Microsoft.Testing.Platform executable directly (`Lightsaber.Skill.Tests`,
Release config, build excluded from the timed command). Environment:
macOS 26.6.1 (25G76), Apple M3 Max (14 cores), .NET SDK
`11.0.100-preview.7.26381.103`, xunit.v3.mtp-v2 `3.2.2`, 77 tests (0
failed) in both configurations.

| | Baseline (`192d334`, NSubstitute) | Migrated (`8078054`, mostly TestDoubles) |
|---|---|---|
| Compono / Compono.NSubstitute / NSubstitute | `0.5.0-preview.73` / `0.5.0-preview.73` / `6.2.0` | same, + `Compono.TestDoubles 0.5.0-preview.73` |
| median | 3.938 s | 3.897 s |
| mean | 3.981 s | 3.876 s |
| stddev | 0.236 s | 0.122 s |
| min / max | 3.709 s / 4.619 s | 3.598 s / 4.054 s |

**Absolute difference (median): -0.041 s. Percent difference: -1.05%.**
This is not a meaningful difference — it is well inside the baseline's own
run-to-run noise (stddev 0.236 s is nearly 6x the observed delta), and two
baseline runs (4.401 s, 4.619 s) look like scheduler/thermal outliers
rather than signal. **This is also not a clean NSubstitute-vs-TestDoubles
comparison**: `Compono.NSubstitute` is still active and in use
(`IAmazonS3`) in the "migrated" configuration, so any difference — real or
noise — can't be attributed to test-double provider alone. No causal
performance claim is made beyond: on this real 77-test suite, replacing
~80% of NSubstitute call sites with Compono.TestDoubles produced no
observable wall-clock change. A suite this size is likely dominated by
process startup, JIT, and (for the infra tests) AWS CDK synthesis
overhead, not test-double dispatch cost — a different real project with a
larger, double-dispatch-heavy suite might show a different result. This
observation should not be read as a general Compono performance claim.

## Classification (per ADR-0029's five-way rubric) — original

**Not a bug, not a new roadmap candidate.** ADR-0045's fix works exactly
as designed — six of seven interfaces, zero `CMP0025` firings, real test
migration succeeding for 4 of 5 files. The one remaining blocker
(`IAmazonS3`'s static-abstract member) is a pre-existing, documented
non-goal (ADR-0042), not a gap ADR-0045 was ever meant to close, and not
evidence of a new capability boundary worth its own roadmap candidate —
static abstract members are a narrow, rare shape (this is the only one
observed across three dogfooding passes and two prior AutoFixture-migration
projects), and `Compono.NSubstitute`'s documented fallback chain already
handles it as designed. This is the "intentional design differences and
acceptable alternatives do not become roadmap items" case ADR-0029
describes.

## Reclassification (2026-08-18): roadmap candidate, per explicit product-owner acceptance criterion

The classification above applied ADR-0029's general "material improvement"
bar, and under that bar it was the right call — every metric moved sharply
in the right direction, and the residual gap is a narrow, previously-
documented non-goal. But the product owner's actual acceptance goal for
`lightsaber-skill` is stronger than that general bar:

> I need `Compono.TestDoubles` to be capable of completely replacing
> `Compono.NSubstitute` in `lightsaber-skill`.

Measured against *that* explicit, stated requirement — not the general
"did things get materially better" rubric — this pass's own evidence is
exactly what disqualifies the "not a roadmap candidate" call: `IAmazonS3`'s
static-abstract member is the *sole* remaining reason full removal isn't
possible, and it is now a single, precisely-identified, evidenced blocker
standing between the current state and a stated product requirement. That
combination — a real, observed, and now product-critical capability gap,
backed by frequency (blocks the last package reference in a real project)
and cost (blocks 100% of the removal goal, not a partial one) — is exactly
ADR-0029's roadmap-candidate rubric, once the acceptance bar being measured
against is the stronger, explicit one rather than the general default.
"Rare and previously a documented non-goal" was true and is still true; it
just isn't disqualifying once a real stakeholder has stated they need this
exact gap closed. **Reclassified: this is a roadmap candidate.** See
[ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)
for the design response.

## Decisions

`PLAN-0045` Phase 4 is complete: the third `lightsaber-skill` dogfood
confirms real tests can migrate and `CMP0025`'s whole-interface-rejection
gap RESEARCH-0004 found is closed. Per PLAN-0045's own instruction, this
is recorded as a **partial success, not overstated as full graduation** —
`docs/roadmap/post-mvp.md`'s ADR-0045/PLAN-0045 entry moves from
outstanding to a shipped-with-a-documented-limit state (4 of 5 files
migrated, `Compono.NSubstitute` still required project-wide because of one
interface's static-abstract member), rather than being removed outright as
if the suite fully dropped NSubstitute. Per the Reclassification above, a
new roadmap candidate **is** opened for the residual `IAmazonS3` gap,
tracked by [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)
— a narrow design question (can a generated double satisfy interface
conformance for a static abstract member without providing any mockable
static-member behavior), explicitly scoped to avoid becoming a general
static-mocking feature.

## Links

- [RESEARCH-0004](0004-lightsaber-skill-testdoubles-v2-dogfood.md) — the
  prior pass this one re-runs the same methodology against, and whose
  `CMP0025` finding this pass confirms is resolved.
- [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md) —
  the configuration-required dispatch design this pass validates.
- [ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md) —
  Non-Goals section covering the static-abstract-member scope boundary
  `IAmazonS3` hits.
- [PLAN-0045](../plans/0045-testdoubles-configuration-required-members.md)
  Phase 4 — the task this document satisfies.
- [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the dogfooding/classification framework this document follows.
