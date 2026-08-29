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
| [0015](0015-provider-identity-deferred-in-provider-attempt.md) | Provider Identity Deferred in `ProviderAttempt` | Superseded by ADR-0016 |
| [0016](0016-provider-identity-restored-in-provider-attempt.md) | Provider Identity Restored in `ProviderAttempt` | Accepted |
| [0017](0017-immutable-composer-configuration-and-builder-model.md) | Immutable Composer Configuration and Builder Model | Accepted |
| [0018](0018-composition-profiles.md) | Composition Profiles | Accepted |
| [0019](0019-registrations-and-service-provider-injection.md) | Registrations and Service Provider Injection | Accepted |
| [0020](0020-composition-configuration-rules.md) | Composition Configuration Rules | Accepted |
| [0021](0021-row-composition-entry-point-for-test-framework-integrations.md) | Row Composition Entry Point for Test-Framework Integrations | Accepted |
| [0022](0022-compono-xunit-package-design.md) | Compono.Xunit Package Design | Accepted |
| [0023](0023-rename-compono-xunit-to-compono-xunitv3.md) | Rename Compono.Xunit to Compono.XunitV3 | Accepted |
| [0024](0024-public-provider-extensibility-model.md) | Public Provider Extensibility Model | Accepted |
| [0025](0025-compono-nsubstitute-package-design.md) | Compono.NSubstitute Package Design | Accepted |
| [0026](0026-deterministic-seed-derivation-for-providers.md) | Deterministic Seed Derivation for Providers and Registration Factories | Accepted |
| [0027](0027-compono-bogus-package-design.md) | Compono.Bogus Package Design | Accepted |
| [0028](0028-configurable-bogus-member-name-conventions.md) | Configurable Bogus Member-Name Conventions | Accepted |
| [0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) | Milestone 7 Dogfooding Strategy and Capability-Gap Decision Framework | Accepted |
| [0030](0030-compono-documentation-architecture.md) | Compono Documentation Architecture | Accepted |
| [0031](0031-public-preview-release-and-versioning-policy.md) | Public Preview Release and Versioning Policy | Accepted |
| [0032](0032-api-reference-documentation-toolchain.md) | API Reference Documentation Toolchain | Accepted |
| [0033](0033-public-preview-samples-strategy.md) | Public Preview Samples Strategy | Accepted |
| [0034](0034-benchmark-suite-strategy-and-redesign.md) | Benchmark Suite Strategy and Redesign | Accepted |
| [0035](0035-compono-agent-skill-pack.md) | Compono Agent Skill Pack | Accepted |
| [0036](0036-parameterized-composition-profile-selection.md) | Call-Site Values Influencing Nested Composition | Accepted |
| [0037](0037-netstandard2.1-compatibility-floor.md) | netstandard2.1 Compatibility Floor | Superseded by ADR-0038 |
| [0038](0038-net8-net9-explicit-multi-target.md) | net8.0/net9.0 Explicit Multi-Target | Accepted |
| [0039](0039-future-extension-package-admission-gate-and-release-sequence.md) | Future Extension Package Admission Gate and Release Sequence | Accepted |
| [0040](0040-compono-tunit-package-design.md) | Compono.TUnit Package Design | Accepted |
| [0041](0041-aot-safe-row-binding-dispatch.md) | AOT-Safe Row-Binding Dispatch | Accepted |
| [0042](0042-compono-owned-source-generated-test-doubles.md) | Compono-Owned Source-Generated Test Doubles | Accepted |
| [0043](0043-compono-generated-test-doubles-design.md) | Compono-Generated Test Doubles: Design | Accepted |
| [0044](0044-compono-testdoubles-v2-overloads-generics-verification.md) | Compono.TestDoubles v2: Overloaded Members, Generic Methods, Minimal Call Verification | Accepted |
| [0045](0045-testdoubles-configuration-required-members.md) | Compono.TestDoubles: Configuration-Required Members for Non-Deterministic-Default Returns | Accepted |
| [0046](0046-static-abstract-member-conformance-only-generation.md) | Effective Interface Contract for Inherited Static Abstract Members | Accepted |
| [0047](0047-compono-dependencyinjection-configured-resolution-bridge.md) | Compono.DependencyInjection: Configured-Resolution IServiceProvider Bridge | Accepted |
| [0048](0048-testdoubles-argument-matching-and-call-verification.md) | Compono.TestDoubles: Argument Matching and Argument-Aware Call Verification | Accepted |
| [0049](0049-testdoubles-generic-return-closed-instantiation-configuration.md) | Compono.TestDoubles: Per-Closed-Instantiation Configuration for Generic Methods Whose Return Type Depends on Their Own Type Parameter | Accepted |
| [0050](0050-testdoubles-multi-entry-argument-distinguished-configuration.md) | Compono.TestDoubles: Multi-Entry, Argument-Distinguished Response Configuration | Accepted |
| [0051](0051-compono-http-handler-based-testing-package.md) | Compono.Http: Handler-Based HTTP Client Testing Package | Accepted |
| [0052](0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md) | Compile-Time Composition-Discovery Boundary for Registered and Nested-Resolved Types | Partially Accepted (Part B shipped; Part A Proposed) |
| [0053](0053-testdoubles-invocation-aware-callback-responses.md) | Compono.TestDoubles: Invocation-Aware Callback Responses | Proposed |
| [0054](0054-testdoubles-sequential-call-count-based-responses.md) | Compono.TestDoubles: Sequential/Call-Count-Based Responses | Accepted |
| [0055](0055-compono-logging-testing-support-package.md) | Compono.Logging: First-Class Microsoft.Extensions.Logging Testing Support | Accepted |
| [0056](0056-composition-builder-share-graph-wide-sharing.md) | `CompositionBuilder.Share<T>()`: Graph-Wide Sharing as a Core Composition Concept | Accepted |
| [0057](0057-compono-mstest-package-design.md) | Compono.MSTest Package Design | Accepted |
