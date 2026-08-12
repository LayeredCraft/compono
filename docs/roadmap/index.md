# Roadmap

**Audience:** anyone asking "is X available, experimental, or planned?"
**Purpose:** the single, indexed home for everything not fully available
today.

- **Today.** Compono's shipped package set —
  [`Compono`](../packages/compono.md), [`Compono.XunitV3`](../packages/compono-xunitv3.md),
  [`Compono.NSubstitute`](../packages/compono-nsubstitute.md),
  [`Compono.Bogus`](../packages/compono-bogus.md), and
  [`Compono.TUnit`](../packages/compono-tunit.md) — covers the full MVP
  package set (`docs/mvp.md`'s "MVP Package Set") plus `Compono.TUnit`,
  the first candidate to graduate the whole way through
  [Future Packages](future-packages.md)' admission model
  ([PLAN-0040](../plans/0040-compono-tunit-package-design.md)). If a
  capability isn't documented in [Concepts](../concepts/index.md),
  [How-to Guides](../how-to/index.md), or a
  [Package Guide](../packages/index.md), it isn't available yet — see
  below for where it might be headed.
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
