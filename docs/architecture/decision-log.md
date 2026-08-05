# Historical Decision Log

The public-facing index into [`docs/adr/`](../adr/README.md): every
`Accepted`/`Superseded` Architecture Decision Record, one line each, for a
reader who wants the full paper trail behind Compono's design. This is
**not** a duplicate of `docs/adr/README.md` (the engineering-process
index, which also tracks `Proposed` ADRs) — a status-filtered view of
those still under discussion lives in
[Roadmap: Proposed ADRs](../roadmap/proposed-adrs.md) instead. As this log
grows across years of decisions, it stays a pure historical record —
readers wanting "how it works today" belong in
[Current Architecture](current/source-generation.md), not here.

| ADR | Title | Status |
|---|---|---|
| [0001](../adr/0001-source-generation-first.md) | Source Generation First — generated composition plans, not reflection, are the default execution model | Accepted |
| [0002](../adr/0002-constructor-selection-algorithm.md) | Constructor Selection Algorithm | Accepted |
| [0003](../adr/0003-generator-package-distribution.md) | Generator Package Distribution — `Compono.Generators` packs into `Compono`'s own nupkg, never published independently | Accepted |
| [0004](../adr/0004-composition-plan-discovery-and-dispatch.md) | Composition Plan Discovery and Dispatch | Accepted |
| [0005](../adr/0005-generator-implementation-conventions.md) | Source Generator Implementation Conventions | Accepted |
| [0006](../adr/0006-required-members-and-nullability-metadata.md) | Required Members and Nullability Metadata | Accepted |
| [0007](../adr/0007-composition-request-and-provider-pipeline.md) | Composition Request and Provider Pipeline | Superseded by [ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md) |
| [0008](../adr/0008-composition-scope-shared-values-and-recursion-detection.md) | Composition Scope, Shared Values, and Recursion Detection | Superseded by [ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md) |
| [0009](../adr/0009-deterministic-seed-and-forkable-random-source.md) | Deterministic Seed and Forkable Random Source | Superseded by [ADR-0012](../adr/0012-composition-path-identity-and-deterministic-random-forking.md) |
| [0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md) | Composition Request, Provider Pipeline, Failure Semantics, and Diagnostics Tracing | Accepted |
| [0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md) | Composition Scope, Shared Values, and Recursion Detection | Accepted |
| [0012](../adr/0012-composition-path-identity-and-deterministic-random-forking.md) | Composition Path Identity, Deterministic Random Forking, and CreateMany Seed Derivation | Accepted |
| [0013](../adr/0013-collection-generation-semantics.md) | Collection Generation Semantics | Accepted |
| [0014](../adr/0014-generator-emitted-collection-plans.md) | Generator-Emitted Collection Plans Replace the Reflection-Based Dispatch Bridge | Accepted |
| [0015](../adr/0015-provider-identity-deferred-in-provider-attempt.md) | Provider Identity Deferred in `ProviderAttempt` | Superseded by [ADR-0016](../adr/0016-provider-identity-restored-in-provider-attempt.md) |
| [0016](../adr/0016-provider-identity-restored-in-provider-attempt.md) | Provider Identity Restored in `ProviderAttempt` | Accepted |
| [0017](../adr/0017-immutable-composer-configuration-and-builder-model.md) | Immutable Composer Configuration and Builder Model | Accepted |
| [0018](../adr/0018-composition-profiles.md) | Composition Profiles — `ICompositionProfile`, eager in-order application | Accepted |
| [0019](../adr/0019-registrations-and-service-provider-injection.md) | Registrations and Service Provider Injection | Accepted |
| [0020](../adr/0020-composition-configuration-rules.md) | Composition Configuration Rules — type/member value rules and collection-size policy | Accepted |
| [0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md) | Row Composition Entry Point for Test-Framework Integrations | Accepted |
| [0022](../adr/0022-compono-xunit-package-design.md) | Compono.Xunit Package Design | Accepted |
| [0023](../adr/0023-rename-compono-xunit-to-compono-xunitv3.md) | Rename Compono.Xunit to Compono.XunitV3 | Accepted |
| [0024](../adr/0024-public-provider-extensibility-model.md) | Public Provider Extensibility Model — `ICompositionValueProvider` for stages 5/6 | Accepted |
| [0025](../adr/0025-compono-nsubstitute-package-design.md) | Compono.NSubstitute Package Design | Accepted |
| [0026](../adr/0026-deterministic-seed-derivation-for-providers.md) | Deterministic Seed Derivation for Providers and Registration Factories | Accepted |
| [0027](../adr/0027-compono-bogus-package-design.md) | Compono.Bogus Package Design | Accepted |
| [0028](../adr/0028-configurable-bogus-member-name-conventions.md) | Configurable Bogus Member-Name Conventions | Accepted |
| [0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) | Milestone 7 Dogfooding Strategy and Capability-Gap Decision Framework | Accepted |
| [0030](../adr/0030-compono-documentation-architecture.md) | Compono Documentation Architecture | Accepted |
| [0031](../adr/0031-public-preview-release-and-versioning-policy.md) | Public Preview Release and Versioning Policy | Accepted |
| [0032](../adr/0032-api-reference-documentation-toolchain.md) | API Reference Documentation Toolchain | Accepted |
| [0033](../adr/0033-public-preview-samples-strategy.md) | Public Preview Samples Strategy | Accepted |
| [0034](../adr/0034-benchmark-suite-strategy-and-redesign.md) | Benchmark Suite Strategy and Redesign — replaces the accreted benchmark suite with a categorized, audience-driven design | Accepted |
