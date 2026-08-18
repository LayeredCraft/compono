# Post-MVP roadmap

Evidence-backed roadmap candidates surfaced by real dogfooding, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
capability-gap decision framework. This page exists per
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md) Phase 3's required
deliverable — it lists **only** findings classified **roadmap candidate**:
Compono genuinely needs a new capability, backed by real observed
frequency and workaround cost, each with at least a recorded ADR against
the problem — `Proposed` while a candidate is still awaiting its design
pass, `Accepted` once designed (a candidate can be listed either way; see
the fourth bullet below for one that's `Accepted` but not yet shipped).
Per ADR-0029: "bugs get fixed, intentional design differences and
acceptable alternatives do not become roadmap items" — this page is not a
general findings log, and non-candidate findings belong in the research
record and their governing ADR's Amendments, not here.

## Current state: no outstanding roadmap candidates

Per `docs/roadmap/index.md`, this page is a status-filtered index of
capability gaps that are **not yet available** — a shipped capability
doesn't stay listed here once it's implemented, even though the evidence
that motivated it remains a permanent part of the record elsewhere (the
ADR, the research doc, the plan). Five dogfooding passes have run so far:

- Milestone 7's pass (migrating `ncipollina/cosmere-tracker`'s
  AutoFixture-based test kit to Compono) surfaced ten findings, **none**
  classified roadmap candidate — every finding's evidence pointed toward
  Compono's existing model already being the right answer, a project-local
  fix, or an unexercised theoretical constraint, not a missing capability.
  See [RESEARCH-0001](../research/0001-autofixture-comparison.md)'s
  "Classifications (Phase 3)" and "Decisions" sections for the full
  per-finding reasoning.
- A subsequent pre-migration capability survey of
  `ncipollina/trivia-platform`'s (much larger) AutoFixture test kit — see
  [RESEARCH-0002](../research/0002-trivia-platform-comparison.md) —
  surfaced one finding classified roadmap candidate: **call-site values
  influencing nested composition**, motivated by `trivia-platform`'s
  parameterized custom `AutoDataAttribute` subclasses (e.g.
  `PersistenceAutoData(repositoryName)`, ~45 call sites). That finding is
  no longer a candidate — it's been designed, `Accepted`, and shipped:
  [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)
  records the decision, [PLAN-0036](../plans/0036-call-site-values-influencing-nested-composition.md)
  (`Done`) records the implementation, and
  [`Compono.XunitV3`'s Package Guide](../packages/compono-xunitv3.md#profile-configuration-arguments)
  is the current-state usage documentation — `ComposeAttribute<TProfile, TConfig>`
  is available today, not planned.

- A third pass — an explicit dogfooding attempt migrating
  `ncipollina/lightsaber-skill`'s test suite from `Compono.NSubstitute` to
  the newly-shipped `Compono.TestDoubles` v1 — surfaced one finding
  classified roadmap candidate: v1's interface-only, overload-free,
  generic-method-free, verification-free scope blocked the two interfaces
  (`IResponseBuilder`, `ILogger<T>`) that dominate the suite's substitution
  surface, plus two `Received(1)`-style assertions with no v1 equivalent.
  That finding was designed, `Accepted`
  ([ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md):
  overloaded-member support, a narrow class of generic-method support, and
  minimal `Never`/`Once`/`Exactly(n)` call verification), and **shipped**
  ([PLAN-0044](../plans/0044-compono-testdoubles-v2.md), `Done`) — all
  three capabilities are implemented and real today, not planned. **This
  finding is no longer listed here, but "shipped" is not the same as "the
  suite fully migrated"** — see the next bullet for why.
- A fourth pass — PLAN-0044 Phase 5's required re-dogfood of
  `lightsaber-skill` against the shipped v2 package
  ([RESEARCH-0004](../research/0004-lightsaber-skill-testdoubles-v2-dogfood.md))
  — confirmed the third pass's shipped capabilities work exactly as
  designed (`ILogger<T>` now generates, proving generic-method support),
  but found they weren't the suite's actual dominant blocker. Of the seven
  interfaces the suite depends on, six (`IResponseBuilder`, `IAmazonS3`,
  `ISkillMediator`, `IOptions<T>`, `ILambdaContext`, `IHandlerInput`) are
  still whole-interface-rejected — not by overloads or generics, but by
  `CMP0025` (a pre-existing v1 rule: a non-nullable-reference-returning
  member with no deterministic default rejects its entire interface).
  Practical result: **zero tests in the suite can drop
  `Compono.NSubstitute`**, since every test using `ILogger<T>` also uses a
  still-rejected interface. This finding is a roadmap candidate that's
  been designed and `Accepted`:
  [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)
  records the decision: a member with no deterministic default generates
  as configuration-required instead, throwing
  `TestDoubleNotConfiguredException` if invoked before `Returns(...)`/
  `Throws(...)` — *provided* it would otherwise have a real `Configure()`/
  `Verify()` surface. `CMP0025` still fires, unchanged, for the three
  genuinely unimplementable return shapes (by-ref, pointer, ref-like)
  **and** for that same non-nullable-reference case when the member also
  has no configuration surface for an unrelated reason (a diamond
  collision, a zero-argument-extension collision, a method-shaped
  object-member collision, or an overloaded `ref`/`out`/`in` parameter —
  a colliding property was, and remains, `CMP0024` regardless) — so no
  member ever ends up throwing unconditionally with no way to configure it
  (Amendments 3, 4, 6, and 7); [PLAN-0045](../plans/0045-testdoubles-configuration-required-members.md)
  (`Done`) tracks the implementation. **This finding is no longer listed
  here** — see the next bullet for the confirming dogfood's result.
- A fifth pass — PLAN-0045 Phase 4's required third `lightsaber-skill`
  dogfood
  ([RESEARCH-0005](../research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md))
  — confirms the fourth pass's shipped capability actually closes the gap:
  six of the suite's seven interfaces (`IResponseBuilder`, `ISkillMediator`,
  `IOptions<T>`, `ILambdaContext`, `IHandlerInput`, `ILogger<T>`) now
  generate and resolve cleanly, `CMP0025` didn't fire once, and four of
  five real test files fully migrated off `Compono.NSubstitute`
  (~44 NSubstitute call sites down to ~9). **This is a partial result, not
  full graduation**: `IAmazonS3` still can't drop `Compono.NSubstitute` —
  not because of anything ADR-0045 scopes, but because it declares a
  static abstract member (`CreateDefaultClientConfig`), a shape
  `Compono.TestDoubles` explicitly doesn't support
  ([ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
  Non-Goals). RESEARCH-0005 classifies that remaining gap **not a bug and
  not a new roadmap candidate** — a narrow, rare shape, observed exactly
  once across three dogfooding passes and two prior migration projects,
  already handled by `Compono.NSubstitute`'s documented fallback chain.
  A controlled before/after benchmark on this same suite (same commit,
  same hyperfine methodology, only the test-double provider changed)
  found no meaningful wall-clock difference (-1.05%, well inside
  run-to-run noise) — not a general Compono performance claim, just this
  one real suite's honest result. `Compono.NSubstitute` therefore stays a
  required package in `lightsaber-skill`'s test project; it did not
  "graduate" to a fully-generated-doubles suite, and RESEARCH-0005 records
  that honestly rather than overstating it.

That these five dogfooding passes together produced zero *outstanding*
roadmap items (the first surfaced none at all; the second and third each
surfaced one, and both have since shipped, the second fully and the third
with a documented, non-roadmap-worthy residual limit) is itself a real,
evidence-backed outcome, not a shortfall in the process — it doesn't mean
Compono is "done": a different real-world project, or a future package,
may surface a finding these five didn't (each is one data point, not an
exhaustive survey). The third, fourth, and fifth passes together are also
a concrete illustration of why that framing matters: shipping the third
pass's finding didn't retire the `lightsaber-skill` gap, it relocated it,
and shipping the fourth pass's finding closed most — but not all — of
what was left. `docs/research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md`
is the record of exactly how much.
