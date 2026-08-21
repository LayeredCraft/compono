# Post-MVP roadmap

Evidence-backed roadmap candidates surfaced by real dogfooding, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
capability-gap decision framework. This page exists per
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md) Phase 3's required
deliverable — it lists **only** findings classified **roadmap candidate**:
Compono genuinely needs a new capability, backed by real observed
frequency and workaround cost, each with at least a recorded ADR against
the problem — `Proposed` while a candidate is still awaiting its design
pass, `Accepted` once designed (a candidate can be listed either way,
including `Accepted` but not yet shipped — none currently are, see below).
Per ADR-0029: "bugs get fixed, intentional design differences and
acceptable alternatives do not become roadmap items" — this page is not a
general findings log, and non-candidate findings belong in the research
record and their governing ADR's Amendments, not here.

## Current state: no outstanding roadmap candidates

Per `docs/roadmap/index.md`, this page is a status-filtered index of
capability gaps that are **not yet available** — a shipped capability
doesn't stay listed here once it's implemented, even though the evidence
that motivated it remains a permanent part of the record elsewhere (the
ADR, the research doc, the plan). Seven dogfooding passes have run so far:

- A seventh pass — a gating investigation for a hypothesized `Compono.BUnit`
  package, using `ncipollina/trivia-manager`'s real bUnit test suite as
  evidence (see [RESEARCH-0007](../research/0007-trivia-manager-bunit-dependency-injection.md))
  — found no bUnit-specific integration surface worth a dedicated package,
  but did find real, repeated friction (compose a test double, get it into
  a DI container) that Compono's existing public API couldn't serve well
  for hand-written consumer code. That redirected the outcome toward a
  general capability ADR-0019 had already named and deferred:
  [ADR-0047](../adr/0047-compono-dependencyinjection-configured-resolution-bridge.md)
  records the decision (`CompositionRow.TryResolveConfigured(Type, out
  object?)` in core, plus a new `Compono.DependencyInjection` package
  exposing `row.AsServiceProvider()`), tracked by
  [PLAN-0047](../plans/0047-compono-dependencyinjection-configured-resolution-bridge.md)
  (`Done`). **This finding is no longer listed here** — the package ships
  in the same change that records this entry, per
  [`Compono.DependencyInjection`](../packages/compono-dependencyinjection.md).

Six earlier dogfooding passes have also run:

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
  (~44 NSubstitute call sites down to ~9). The sole remaining blocker:
  `IAmazonS3` declares a static abstract member
  (`CreateDefaultClientConfig`), a shape `Compono.TestDoubles` explicitly
  doesn't support
  ([ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
  Non-Goals). RESEARCH-0005's own initial classification called this **not
  a roadmap candidate** — narrow, rare, already handled by
  `Compono.NSubstitute`'s documented fallback chain, under ADR-0029's
  general "material improvement" bar. It was reclassified the same day,
  once measured against a stronger, explicit stakeholder requirement:
  `lightsaber-skill`'s test project must be able to drop
  `Compono.NSubstitute` entirely, not just mostly. Against *that*
  criterion, one precisely-identified static-abstract-member blocker
  standing between the current state and full removal **is** real,
  evidenced, and product-critical, per ADR-0029's rubric. See
  RESEARCH-0005's "Reclassification" section for the full reasoning. This
  finding was a roadmap candidate, tracked by
  [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md).
  A controlled before/after benchmark on the same suite (baseline `192d334`,
  migrated `8078054` — consecutive commits on the same branch, same
  hyperfine methodology, only the test-double provider changed between
  them) found no meaningful wall-clock difference (-1.05%, well inside
  run-to-run noise) — not a general Compono performance claim, just this one real
  suite's honest result, unaffected by the reclassification above. **This
  finding is no longer listed here** — see the next bullet for the closing
  result.
- A sixth pass — PLAN-0046's own closing acceptance test, re-running
  `lightsaber-skill` against the shipped fix
  ([RESEARCH-0006](../research/0006-lightsaber-skill-testdoubles-gate-b-closing-dogfood.md))
  — confirms the fifth pass's blocker is fully closed, not just narrowed
  further: `IAmazonS3.CreateDefaultClientConfig()` turned out to be an
  analyzer bug, not a genuine capability gap — `IAmazonS3` itself already
  provides a concrete implementation for what its base interface
  (`IAmazonService`) only declares abstractly (C#'s own "most specific
  implementation" rule), and the old per-interface closure walk was
  inspecting the base interface's raw declaration in isolation, never
  noticing `IAmazonS3` had already resolved it.
  [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)
  records the corrected design (and, notably, the originally-accepted
  design — conformance-only throwing stubs — that got built and then
  withdrawn during implementation once two compile spikes proved it wrong
  and unreachable); [PLAN-0046](../plans/0046-static-abstract-member-conformance-only-generation.md)
  (`Done`) tracks the implementation. Once
  [compono#99](https://github.com/LayeredCraft/compono/pull/99) shipped as
  `Compono` `0.5.0-preview.74`, `lightsaber-skill` fully replaced
  `Compono.NSubstitute` with `Compono.TestDoubles`: `IAmazonS3` resolves
  through `UseGeneratedTestDoubles()` alone, `Compono.NSubstitute`/
  `NSubstitute` are removed from the project entirely (confirmed absent
  even transitively), and the full 77-test suite passes via the built
  test executable
  ([lightsaber-skill#108](https://github.com/ncipollina/lightsaber-skill/pull/108)).
  **This finding is no longer listed here** — Gate-B is met in full, not
  partially.

That the first, second-and-third (both since shipped),
fourth-fifth-and-sixth (together), and now seventh passes have all
resolved to zero *outstanding* roadmap items is itself real,
evidence-backed progress, not a shortfall in the process — it doesn't mean
Compono is "done": a different real-world project, or a future package,
may surface a finding these seven didn't (each is one data point, not an
exhaustive survey). The
third through sixth passes together are also a concrete illustration of
why the distinction between "shipped" and "fully closed" matters:
shipping the third pass's finding didn't retire the `lightsaber-skill`
gap, it relocated it; shipping the fourth pass's finding closed most of
what was left; the fifth pass's own evidence, measured against the
project's actual acceptance bar rather than a general one, kept one
narrow finding open a little longer; and the sixth pass closed it for
real, once the actual root cause (an analyzer bug, not a genuine gap) was
found. `docs/research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md`
and `docs/research/0006-lightsaber-skill-testdoubles-gate-b-closing-dogfood.md`
are the record of exactly how, and why the distinction mattered.
