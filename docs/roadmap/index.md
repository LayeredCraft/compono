# Roadmap

**Audience:** anyone asking "is X available, experimental, or planned?"
**Purpose:** the single, indexed home for everything not fully available
today.

- **Today.** Compono's shipped package set —
  [`Compono`](../packages/compono.md), [`Compono.XunitV3`](../packages/compono-xunitv3.md),
  [`Compono.NSubstitute`](../packages/compono-nsubstitute.md),
  [`Compono.Bogus`](../packages/compono-bogus.md),
  [`Compono.TUnit`](../packages/compono-tunit.md),
  [`Compono.TestDoubles`](../packages/compono-testdoubles.md),
  [`Compono.DependencyInjection`](../packages/compono-dependencyinjection.md), and
  [`Compono.Http`](../packages/compono-http.md)
  — covers the full MVP package set (`docs/mvp.md`'s "MVP Package Set")
  plus `Compono.TUnit` and `Compono.TestDoubles`, the two candidates to
  graduate the whole way through [Future Packages](future-packages.md)'
  admission model ([PLAN-0040](../plans/0040-compono-tunit-package-design.md),
  [PLAN-0043](../plans/0043-compono-generated-test-doubles.md)), plus
  `Compono.DependencyInjection` ([ADR-0047](../adr/0047-compono-dependencyinjection-configured-resolution-bridge.md)),
  which didn't graduate through that admission model at all — see that
  ADR and [RESEARCH-0007](../research/0007-trivia-manager-bunit-dependency-injection.md)
  for how it came about instead — and, the same way,
  `Compono.Http` ([ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)),
  which also didn't graduate from [Future Packages](future-packages.md)'
  own candidate list; it came from a dedicated admission research doc
  triggered by a real `alexa-vox-craft` dogfooding need — see
  [RESEARCH-0009](../research/0009-compono-http-admission-research.md).
  If a capability isn't documented in
  [Concepts](../concepts/index.md), [How-to Guides](../how-to/index.md),
  or a [Package Guide](../packages/index.md), it isn't available yet —
  see below for where it might be headed.
- **Experimental / under discussion.** [Proposed ADRs](proposed-adrs.md) —
  design decisions still being discussed, not yet `Accepted`.
- **Planned / candidate.** [Post-MVP roadmap](post-mvp.md) — evidence-backed
  capability gaps surfaced by real dogfooding, and
  [Future Packages](future-packages.md) — integration packages beyond the
  shipped set that could follow the same pattern
  [`Compono.NSubstitute`](../packages/compono-nsubstitute.md)/[`Compono.Bogus`](../packages/compono-bogus.md)
  already established, but aren't committed yet.

Each of these is deliberately a status-filtered *index*, not new
narrative content of its own — every candidate/proposal it lists is
backed by a real ADR, research finding, or explicit non-goal recorded
elsewhere, per this repo's evidence-over-prediction bias
([ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)).
