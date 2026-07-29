# Architecture Decision Records

This directory is the permanent, numbered record of every design decision
made in this repo — see `.agents/skills/engineering-workflow/references/design-decisions.md`
for the full process of *when* and *how* to write one. This file is just
the mechanics: numbering, status, and the index.

## Numbering and immutability

- Files are `NNNN-kebab-case-title.md`, zero-padded to 4 digits,
  sequential (`0001-...`, `0002-...`, ...). `0000-adr-template.md` is the
  template, not a real decision — the first real ADR is `0001`.
- An ADR is **immutable once its status leaves `Proposed`.** Don't edit an
  `Accepted` ADR's Decision/Context/Considered Options to reflect a change
  of mind later — write a new ADR that changes `Status` to `Superseded by
  ADR-XXXX` on the old one and references it from the new one's Context or
  Links section. The old ADR stays exactly as it was when accepted; that's
  the point of it being a record, not a living doc.
- Copy `0000-adr-template.md` for every new ADR rather than writing one
  from a blank file, so the section shape stays consistent across every
  decision in this repo.

## Status lifecycle

`Proposed` → `Accepted` → (`Deprecated` | `Superseded by ADR-XXXX`)

- **Proposed**: still being discussed — the deep-dive brainstorm phase
  described in `design-decisions.md` produces a `Proposed` ADR before
  anything gets built against it.
- **Accepted**: the decision this repo is actually operating under. Code
  should match an `Accepted` ADR; if it doesn't, that's drift — fix the
  code or supersede the ADR, the same "code and docs disagree" rule that
  governs the rest of this repo's documentation.
- **Deprecated**: no longer the guidance, but nothing formally replaced it
  (rare — most decisions that stop applying get superseded by whatever
  replaced them instead).
- **Superseded by ADR-XXXX**: a later ADR explicitly replaced this one.
  Follow the chain forward to the current answer rather than trusting a
  superseded ADR's Decision section.

## How this relates to other docs

- **`docs/*.md`** (architecture, public-api, mvp, manifesto,
  design-principles) describe *current or intended state* — what the
  system does or is meant to do. When a topic doc describes a decision, it
  should link to the ADR that made it rather than re-deriving the
  rationale inline. Several of these docs predate this ADR system and
  still carry open questions or decisions inline (the "Open Architectural
  Decisions"/"Open Decisions Before Implementation" lists) — resolving one
  of those into an ADR should also update the doc to link back here rather
  than leave the answer duplicated in both places.
- **`docs/research/*.md`**, if this repo ever needs one for a longer
  external-reference write-up, should capture what was looked at and why,
  with a `## Decisions` section pointing at the ADR(s) the research fed
  into — the research doc itself is never the system of record for what
  was decided.
- **`README.md`** stays the vision/intent document; an ADR that changes
  product-level direction gets referenced from `README.md`, not duplicated
  into it.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](0001-source-generation-first.md) | Source Generation First | Accepted |
| [0002](0002-constructor-selection-algorithm.md) | Constructor Selection Algorithm | Accepted |
| [0003](0003-generator-package-distribution.md) | Generator Package Distribution | Accepted |
| [0004](0004-composition-plan-discovery-and-dispatch.md) | Composition Plan Discovery and Dispatch | Accepted |
| [0005](0005-generator-implementation-conventions.md) | Source Generator Implementation Conventions | Accepted |
| [0006](0006-required-members-and-nullability-metadata.md) | Required Members and Nullability Metadata | Accepted |
| [0007](0007-composition-request-and-provider-pipeline.md) | Composition Request and Provider Pipeline | Superseded by ADR-0010 |
| [0008](0008-composition-scope-shared-values-and-recursion-detection.md) | Composition Scope, Shared Values, and Recursion Detection | Superseded by ADR-0011 |
| [0009](0009-deterministic-seed-and-forkable-random-source.md) | Deterministic Seed and Forkable Random Source | Superseded by ADR-0012 |
| [0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) | Composition Request, Provider Pipeline, Failure Semantics, and Diagnostics Tracing | Accepted |
| [0011](0011-composition-scope-shared-values-and-recursion-detection.md) | Composition Scope, Shared Values, and Recursion Detection | Accepted |
| [0012](0012-composition-path-identity-and-deterministic-random-forking.md) | Composition Path Identity, Deterministic Random Forking, and CreateMany Seed Derivation | Accepted |
| [0013](0013-collection-generation-semantics.md) | Collection Generation Semantics | Accepted |
| [0014](0014-generator-emitted-collection-plans.md) | Generator-Emitted Collection Plans Replace the Reflection-Based Dispatch Bridge | Accepted |
| [0015](0015-provider-identity-deferred-in-provider-attempt.md) | Provider Identity Deferred in `ProviderAttempt` | Accepted |
