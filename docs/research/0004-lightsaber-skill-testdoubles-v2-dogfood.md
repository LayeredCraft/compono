# [RESEARCH-0004] `lightsaber-skill` Re-Dogfood Against `Compono.TestDoubles` v2

**Status:** Done (dogfooding pass complete; no migration merged — see
"Scope" below)

**Feeds:** [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)
(`Accepted`) and [PLAN-0045](../plans/0045-testdoubles-configuration-required-members.md)
(`Done`) — see "Decisions" below

This document is the evidence record for [PLAN-0044](../plans/0044-compono-testdoubles-v2.md)
Phase 5: re-running the exact `ncipollina/lightsaber-skill` migration
analysis that motivated [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md),
against the shipped v2 package (`Compono`/`Compono.TestDoubles`/
`Compono.NSubstitute`/`Compono.XunitV3` `0.5.0-preview.70`), following
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-over-prediction bias. Unlike the original v1 pass (recorded only
as prose in ADR-0044's Context, not its own research doc), this pass is
recorded formally because its result diverges from what ADR-0044
predicted, and that divergence is itself the finding.

## Scope

A real branch (`feat/compono-testdoubles-v2-dogfood`, not merged, deleted
after this pass) added `Compono.TestDoubles` `0.5.0-preview.70` to
`lightsaber-skill`'s test project, bumped the three already-referenced
Compono packages from `0.1.0`/`0.4.0` to the same preview version, set
`ComponoGeneratedTestDoubles=true`, and added a `GeneratedTestDoublesProfile`
(`ICompositionProfile` calling `UseGeneratedTestDoubles()`). Every interface
the suite's ~40 NSubstitute call sites touch was then probed with a real
`[Compose<GeneratedTestDoublesProfile>]`-parameterized test method and a
`dotnet build -v:diag` run, reading the actual `CMP00xx` diagnostics
emitted — not inferring support from the interface's declared shape by
eye. No production `lightsaber-skill` code changed; the experimental
branch was discarded once the diagnostics were captured, per the decision
recorded below.

## Result

Of the seven interfaces the suite depends on, **only `ILogger<T>` now
generates** a working double under v2:

| Interface | v1 (ADR-0044's claim) | v2 (measured) | Blocker |
|---|---|---|---|
| `ILogger<T>` | rejected (`CMP0021`, generic methods) | **generates** | none — Requirement 2 (generic-method support) works exactly as designed |
| `IResponseBuilder` | rejected (`CMP0022`, overloads) | still rejected | `CMP0025` — `Speak`/`Reprompt`/etc. return `IResponseBuilder` itself, a non-nullable reference type with no deterministic default |
| `IAmazonS3` | rejected (`CMP0022`, overloads) | still rejected | `CMP0025` — `GetPreSignedURL` returns non-nullable `string` |
| `ISkillMediator` | claimed to generate cleanly | still rejected | `CMP0025` — `Send` returns `Task<SkillResponse>`, and `SkillResponse` (a concrete class) has no deterministic default |
| `IOptions<LightsaberOptions>` | claimed to generate cleanly | still rejected | `CMP0025` — `Value` returns the non-nullable `LightsaberOptions` |
| `ILambdaContext` | claimed to generate cleanly | still rejected | `CMP0025` — `AwsRequestId` returns non-nullable `string` |
| `IHandlerInput` | not discussed | still rejected | `CMP0025` — `RequestEnvelope` returns non-nullable `SkillRequest` |

`CMP0025` ("Unsupported test-double return shape") is a **whole-interface
fallback** diagnostic that predates this ADR — it's ADR-0043 v1's own
Finding K (`docs/adr/0043-compono-generated-test-doubles-design.md`
Amendment 5), not something ADR-0044's overload/generic/verification work
touches. It fires whenever *any* member (property or method, including
through `Task<T>`/`ValueTask<T>`'s inner `T`) returns a non-nullable
reference type the generator has no deterministic default for, and rejects
the *entire* interface at generation time — regardless of whether the test
author would have configured that member with `Returns(...)`/`Throws(...)`
before ever invoking it.

**Practical consequence: zero tests in the suite can drop
`Compono.NSubstitute`.** Every test that composes `ILogger<T>` also
composes at least one still-`CMP0025`-rejected interface in the same
parameter list (`LambdaHandlerTests`: `ISkillMediator` + `ILambdaContext`;
`LightsaberHandlerTests`: `IAmazonS3` + `IOptions<T>`; `ErrorHandlerTests`/
`UnhandledMessageTests`: `IResponseBuilder`) — exactly the failure mode
ADR-0044's own Context section warned about ("even a fully-migratable
dependency sitting next to it in the same test parameter list buys
nothing"), just triggered by a different root cause than predicted.

**Correction to ADR-0044's Context:** its claim that "`ISkillMediator`,
`IOptions<T>`, `ILambdaContext`... would generate cleanly under v1 today"
was wrong — all three were already `CMP0025`-blocked under v1 (Finding K
predates ADR-0044). See ADR-0044 Amendment 17 for the formal correction.

## Classification (per ADR-0029's five-way rubric)

This is **not a bug** — `CMP0025`'s behavior matches its own design intent
(ADR-0043 Finding K: don't manufacture an arbitrary non-null value; reject
the whole leaf instead) exactly as documented. Whether that design intent
is still the *right* one, now that this real dogfooding pass shows it's
the dominant real-world blocker, is the open question below — which makes
this a **roadmap candidate**: real, evidenced, observed-frequency proof
that a specific existing capability boundary (not a missing feature)
blocks a meaningful fraction of real usage.

## Recommendation

Record a new roadmap candidate, distinct from and not folded into
ADR-0044/PLAN-0044 (whose own scope — overloaded members, return-type-
independent generic methods, minimal call verification — is fully
implemented and validated by this same pass; `ILogger<T>` generating
cleanly is direct proof Requirement 2 works). The candidate: reconsider
whether a non-nullable-reference-return member with no deterministic
default must reject the *whole interface* at generation time, or whether
the double could still generate with that *specific member* requiring
explicit `Returns(...)`/`Throws(...)` configuration before it's ever
invoked — throwing a clear `Compono`-owned configuration exception on an
unconfigured call, instead of falling back to the runtime-provider path
for the entire leaf.

This needs its own design deep dive (`tasks/design.md`) before an ADR
number is assigned, not a decision made inline in this research doc. Open
questions the design pass should resolve, in no particular order:

- Is "configuration-required member" the right semantic model, or is
  there a better one?
- Should the interface still generate when *only some* members need
  required configuration, with the rest keeping today's deterministic-
  default behavior unchanged?
- Should the required-configuration exception fire synchronously at
  invocation, consistent with today's `Throws(...)` semantics?
- Does this apply to properties (`IOptions<T>.Value`) the same way it
  applies to methods?
- Fluent self-returning interfaces (`IResponseBuilder`'s `Speak(...)`
  returning `IResponseBuilder`) — is `return this` a safe special-cased
  deterministic default, or should it still require explicit
  configuration like any other non-nullable reference return?
- Non-nullable `Task<T>`/`ValueTask<T>` results specifically (not just
  synchronous returns).
- Diagnostic-severity implications — does this shrink the whole-interface-
  fallback code list, or just change `CMP0025`'s own text from rejection
  to "requires configuration" guidance?
- Whether this makes whole-interface rejection unnecessary for *this*
  category specifically, while every genuinely unimplementable shape
  (pointers, unconstrained `T?`, ref-like returns, etc.) keeps rejecting
  exactly as today.
- AOT-safety implications (a thrown exception type is trivially AOT-safe,
  but confirm no reflection creeps in).
- Performance implications (an extra branch per generated member, likely
  negligible, but ADR-0034's benchmark policy applies if this ships).
- Documentation/skill updates this would require across
  `docs/packages/compono-testdoubles.md`,
  `skills/compono/references/testdoubles.md`, and both `diagnostics.md`
  files.
- A third `lightsaber-skill` dogfooding pass, once shipped, as the actual
  acceptance test for whether this closes the gap this document found.

## Decisions

The recommendation above fed a full design deep dive
(`tasks/design.md`), which produced
[ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)
(`Accepted`): a member with no deterministic default no longer rejects
its whole interface — it generates as **configuration-required**,
throwing a new `TestDoubleNotConfiguredException` if invoked before
`Configure().Member(...).Returns(...)`/`.Throws(...)`, provided it would
otherwise have a real configuration surface. `CMP0025` still fires,
unchanged, for the three genuinely unimplementable return shapes
(by-ref, pointer, ref-like) *and* for that same non-nullable-reference
case when the member also lacks a configuration surface for an unrelated
reason (a diamond collision, a zero-argument-extension collision, a
method-shaped object-member collision, or an overloaded `ref`/`out`/`in`
parameter — a colliding property was, and remains, `CMP0024` regardless,
Amendments 3/4/6/7) — a new `CMP0032` — scoped to one informational
diagnostic per interface, not per member, per Amendment 1 — covers the
ordinary configuration-required case. Manufacturing a value and
special-casing
fluent self-return were both considered and rejected; the full comparison
and every open question above is resolved in the ADR itself.
[PLAN-0045](../plans/0045-testdoubles-configuration-required-members.md)
(`Done`) tracks the implementation, phased so its own Phase 4 was the
third `lightsaber-skill` dogfooding pass this document's last bullet
calls for — see
[RESEARCH-0005](0005-lightsaber-skill-testdoubles-v2-third-dogfood.md)
for that pass's result, the same way this document is the second pass's.

## Links

- [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md) —
  the v2 work this pass validated (Requirement 2, generic methods) and
  corrected (the Context section's `CMP0025`-blocked-interfaces claim, via
  Amendment 17).
- [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md) Amendment 5,
  Finding K — the v1 origin of `CMP0025`'s deterministic-default
  requirement, unchanged by this pass.
- [PLAN-0044](../plans/0044-compono-testdoubles-v2.md) Phase 5 — the task
  this document satisfies.
- [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the dogfooding/classification framework this document follows.
