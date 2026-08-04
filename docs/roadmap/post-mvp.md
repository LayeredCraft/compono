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

## Current state: no roadmap candidates

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
not an exhaustive survey) — but there is nothing to list here as of this
milestone.
