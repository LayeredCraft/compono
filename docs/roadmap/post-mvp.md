# Post-MVP roadmap

Evidence-backed roadmap candidates surfaced by real dogfooding, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
capability-gap decision framework. This page exists per
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md) Phase 3's required
deliverable — it lists only findings classified **roadmap candidate**:
Compono genuinely needs a new capability, backed by real observed
frequency and workaround cost, each with a `Proposed` ADR recording the
problem.

## Current state: no roadmap candidates

Milestone 7's dogfooding pass (migrating `ncipollina/cosmere-tracker`'s
AutoFixture-based test kit to Compono — see
[RESEARCH-0001](../research/0001-autofixture-comparison.md) for the full
evidence record) surfaced ten findings. None were classified roadmap
candidate:

| Finding | Why it didn't qualify |
|---|---|
| Gap 1 — frozen shared values | `[Shared]` is already a direct, low-cost, pleasant substitute for every exercised case — acceptable Compono-native alternative, not a gap. |
| Gap 2 — NSubstitute `ConfigureMembers` | A real material cost exists, but restoring the AutoFixture behavior would conflict with Compono's explicit-over-implicit principle — intentional design difference (a "no change" verdict), not a capability Compono is missing. |
| Gap 3 — recursion behavior | Zero observed frequency — no construction-cycle failure was ever triggered by real `cosmere-tracker` code, in either direction. |
| `Compono.Bogus` mandatory dogfooding | Worked cleanly once called through its real API — acceptable Compono-native alternative, verging on a genuine improvement. |
| Finding 4 — Compose-family stacking constraint | A real, discovered constraint, but zero real call sites needed it — no observed frequency or workaround cost to back a roadmap item. |
| Finding 5 — `Compono.Bogus` exact member-name matching | The explicit-`RuleFor` workaround cost nothing extra since both affected types already needed their own rules regardless. |
| Finding 6 — `DynamoDbResponseSpecimenBuilder` zero call sites | Pre-existing unused AutoFixture infrastructure, dropped during migration — migration-only friction, not a Compono capability question. |
| Finding 7 — `CMP0001` (`HttpClient` construction) | The diagnostic only fired while porting a capability with zero real pre-migration call sites — a synthetic exercise, which ADR-0029 explicitly rejects as roadmap evidence on its own. |
| Finding 8 — three-tier fixture stack | Compono simplified the mechanism at every tier without a principle conflict — acceptable Compono-native alternative. |
| Finding 9 — pure-inline `[Theory]` cleanup | Project-local: the wrapper it removed was `cosmere-tracker`'s own choice, never an AutoFixture requirement — migration-only friction. |

Full per-finding frequency, before/after evidence, principle-alignment
reasoning, and classification: see
[RESEARCH-0001](../research/0001-autofixture-comparison.md)'s
per-finding evidence dossier and "Classifications (Phase 3)" section. Each
finding's recording mechanism (which ADR Amendment, if any) is listed in
that document's "Decisions" section.

## What this means

A dogfooding pass that surfaces zero roadmap candidates is itself a real,
evidence-backed outcome — not a shortfall in the process. ADR-0029 set out
to determine, from evidence, whether Compono's current explicit behavior
is already the better long-term answer for each candidate gap, or whether
AutoFixture's behavior should inform a new capability. For every finding
this migration produced, the evidence pointed toward Compono's existing
model (or a project-local fix, or an unexercised theoretical constraint),
not a missing capability. That doesn't mean Compono is "done" — a
different real-world project, or a future package, may surface findings
this one didn't (`cosmere-tracker`'s domain, scale, and test patterns are
one data point, not an exhaustive survey) — but there is nothing to list
here as of this milestone.

## Two candidates worth tracking without a `Proposed` ADR yet

Two findings identified a *plausible* future improvement without the
evidence to justify designing it now — both are recorded here for
visibility, not as roadmap candidates (no `Proposed` ADR exists for
either, per ADR-0029's evidence-driven restraint: a synthetic or
zero-frequency exercise doesn't earn one):

- **Generic disambiguation support for registered/external ambiguous
  types** (`CMP0001`, Finding 7) — `HttpClient`-shaped types (a BCL type
  the consumer can't annotate) can't be composed directly today; the
  interface-wrapper workaround closes the one real case this migration
  produced. See
  [ADR-0002 Amendment 1](../adr/0002-constructor-selection-algorithm.md#amendment-1-2026-08-04-cmp0001-observed-against-a-real-ambiguous-bcl-type-no-change-made).
- **Stacking distinct Compose-family attributes on one test method**
  (Finding 4) — no direct equivalent to AutoFixture's "stack multiple
  `[InlineAutoData(...)]` rows, each with a composed parameter" idiom.
  Zero real call sites needed it in this migration. See
  [ADR-0022 Amendment 7](../adr/0022-compono-xunit-package-design.md#amendment-7-2026-08-04-stacking-distinct-compose-family-attributes-stays-unsupported-no-real-call-site-found).

If a future dogfooding pass (a different real project, or a future
Compono package) surfaces a real pre-existing call site for either, that's
new evidence a future milestone can act on — write the `Proposed` ADR at
that point, not before.
