# Post-MVP roadmap

Evidence-backed roadmap candidates surfaced by real dogfooding, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
capability-gap decision framework. This page exists per
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md) Phase 3's required
deliverable — it lists **only** findings classified **roadmap candidate**:
Compono genuinely needs a new capability, backed by real observed
frequency and workaround cost, each with a `Proposed` ADR recording the
problem. Per ADR-0029: "bugs get fixed, intentional design differences and
acceptable alternatives do not become roadmap items" — this page is not a
general findings log, and non-candidate findings belong in the research
record and their governing ADR's Amendments, not here.

## Current state: one roadmap candidate

Milestone 7's dogfooding pass (migrating `ncipollina/cosmere-tracker`'s
AutoFixture-based test kit to Compono) surfaced ten findings. **None were
classified roadmap candidate** — every finding's evidence pointed toward
Compono's existing model already being the right answer, a project-local
fix, or an unexercised theoretical constraint, not a missing capability.

A dogfooding pass that surfaces zero roadmap candidates is itself a real,
evidence-backed outcome, not a shortfall in the process — see
[RESEARCH-0001](../research/0001-autofixture-comparison.md)'s
"Classifications (Phase 3)" and "Decisions" sections for the full
per-finding reasoning and which ADR Amendment (if any) recorded each
verdict. That doesn't mean Compono is "done": a different real-world
project, or a future package, may surface findings this one didn't
(`cosmere-tracker`'s domain, scale, and test patterns are one data point,
not an exhaustive survey) — and a second survey did.

A subsequent pre-migration capability survey of
`ncipollina/trivia-platform`'s (much larger) AutoFixture test kit — see
[RESEARCH-0002](../research/0002-trivia-platform-comparison.md) — surfaced
one finding classified roadmap candidate:

- **Call-site values influencing nested composition.** `trivia-platform`'s
  custom `AutoDataAttribute` subclasses overwhelmingly take runtime
  constructor arguments (e.g. `PersistenceAutoData(repositoryName)` — ~45
  call sites; `AnnouncementsAutoData` — 8 boolean/locale parameters, 18
  call sites) that change what a composition decision made *inside* the
  composed graph produces, not just which top-level type gets composed —
  distinct from the already-solved cases of resolution-site-name matching
  (`ICompositionValueProvider`) and fixed member overrides (`.Member()`).
  Compono has no documented way for a compile-time-constant value known at
  the test call site to reach that nested decision. **Impact:** high —
  every current workaround (a dedicated profile per configuration variant,
  or an inline `Composer.Create` call per test) trades away the concise,
  declarative attribute-based idiom. **Confidence:** high (real, high-frequency call sites; no identified
  principle conflict). **Recorded in:**
  [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)
  — a deep-design pass has since run and the ADR is now `Accepted`: a new
  `Compono.XunitV3`-only `ComposeAttribute<TProfile, TConfig>` attribute
  binds compile-time-constant **profile configuration arguments**
  positionally to a `TConfig` type, which then constructs an
  `ICompositionProfile` via the already-existing, unchanged
  `AddProfile(ICompositionProfile)` core API — zero changes to core
  `Compono`. Implemented and shipped —
  [PLAN-0036](../plans/0036-call-site-values-influencing-nested-composition.md)
  is `Done`; see [`Compono.XunitV3`'s Package Guide](../packages/compono-xunitv3.md#profile-configuration-arguments)
  for usage.
