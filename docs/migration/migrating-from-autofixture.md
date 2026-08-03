# Migrating from AutoFixture to Compono

**Status:** Skeleton (drafted in PLAN-0007 Phase 0; filled in per concept
during Phase 1, alongside the real `cosmere-tracker` migration)

This guide is a living deliverable of Milestone 7's dogfooding pass
([ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md),
[PLAN-0007](../plans/0007-milestone-7-dogfooding.md)) — it exists to help a
real AutoFixture user move to Compono, drawn from an actual migration
(`ncipollina/cosmere-tracker`'s `test/Cosmere.Tracker.TestKit`), not
synthetic examples. Each section below is filled in as its concept is
actually migrated, with a real before/after snippet from that project —
not written up front from theory.

For each concept, an entry covers: the AutoFixture approach, the Compono
approach, why the Compono approach was chosen, a better/equivalent/tradeoff
verdict, links to the relevant ADR(s)/research findings, and a real
before/after code example.

## `Freeze<T>()` and hidden shared values

AutoFixture's `Freeze<T>()` lets one resolved instance be reused across a
composition without appearing as a parameter anywhere a test can see.
`cosmere-tracker`'s `HttpClientSpecimenBuilder`/`ClientAutoDataAttribute`
freezes an `HttpMessageHandler` this way. Compono's explicit alternative is
a `[Shared]` parameter ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md),
[ADR-0022](../adr/0022-compono-xunit-package-design.md)). This is ADR-0029's
named "gap 1" — content filled in during Phase 1 once the migration reaches
this call site.

## `AutoDataAttribute`/`InlineAutoDataAttribute` and customizations

AutoFixture's `[AutoData]`/`[InlineAutoData]` pair, wrapped here in
`cosmere-tracker`-specific subclasses (`CosmereTrackerAutoDataAttribute`,
`ClientAutoDataAttribute`) that bake in an `IFixture` factory. Compono's
idiomatic shape is `[Compose<TProfile>]` plus inline parameters
([ADR-0022](../adr/0022-compono-xunit-package-design.md)). Content filled in
during Phase 1 alongside the attribute migration.

## `ICustomization` and composition profiles

AutoFixture's `ICustomization` (here, `CosmereTrackerCustomization`) versus
Compono's `ICompositionProfile`
([ADR-0018](../adr/0018-composition-profiles.md)). Content filled in during
Phase 1.

## `AutoNSubstituteCustomization` (`ConfigureMembers`)

`BaseFixtureFactory` applies `AutoNSubstituteCustomization { ConfigureMembers
= true }`, recursively auto-configuring every generated substitute's
members. Compono's `Compono.NSubstitute`
([ADR-0025](../adr/0025-compono-nsubstitute-package-design.md)) deliberately
returns a bare `Substitute.For<T>()` with no recursive auto-configuration.
This is ADR-0029's named "gap 2" — content filled in during Phase 1.

## Recursion behaviors (`OmitOnRecursionBehavior` vs. fail-fast)

`BaseFixtureFactory` swaps AutoFixture's default
`ThrowingRecursionBehavior` for `OmitOnRecursionBehavior`, silently omitting
a value on a construction cycle. Compono detects a genuine construction
cycle and fails with a path-annotated error
([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)).
This is ADR-0029's named "gap 3" — content filled in during Phase 1.

## Specimen builders and request specifications

AutoFixture's `ISpecimenBuilder`/`IRequestSpecification` pattern (here,
`HttpClientSpecimenBuilder`/`HttpClientSpecification`, and
`cosmere-tracker`'s `Cosmere.Tracker.Shared.TestKit` domain-item specimen
builders) versus Compono's registration/provider model
([ADR-0024](../adr/0024-public-provider-extensibility-model.md)). Content
filled in during Phase 1.

## `Compono.Bogus`: realistic domain data

`cosmere-tracker`'s AutoFixture kit has no equivalent concept — it only
ever produces anonymous specimens, never semantic-looking data. This
section documents an *added* capability rather than a migrated one, per
ADR-0029's "Compono.Bogus adoption is mandatory."
Phase 0's candidate domain members (string members that plausibly warrant
realistic data, surveyed from `src/Cosmere.Tracker.Shared/Models/**` and
`src/Cosmere.Tracker.Api/Dtos/**`):

- `BookItem.Title` / `BookDto.Title` — book title
- `CharacterItem.Name` / `CharacterDto.Name` — character name
- `WorldItem.Name` / `WorldDto.Name` — world name
- `WorldItem.SystemName` / `WorldDto.SystemName` — star-system name (optional)

Excluded from the candidate list: `*Normalized` members (`TitleNormalized`,
`NameNormalized`) are derived deterministically from the members above
rather than independently generated, and `Id`/`CreatedAt`/`UpdatedAt`/edge
`*Id` foreign keys are identifiers/timestamps, not free-form vocabulary.
None of these names (book title, character name, world/system name) match
`Compono.Bogus`'s built-in person/contact-biased convention allowlist
([ADR-0027](../adr/0027-compono-bogus-package-design.md)) — this is
expected to exercise
[ADR-0028](../adr/0028-configurable-bogus-member-name-conventions.md)'s
`BogusOptions.AddAlias`/`AddConvention` mechanism, or member-level
`UseBogus(faker => ...)`, rather than the allowlist matching automatically.
Actual adoption and findings filled in during Phase 1. Per
[ADR-0029 Amendment 1](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-1-2026-08-02-the-componobogus-experiment-is-mandatory-not-its-conclusion),
what's mandatory is running this experiment, not a predetermined positive
result — this section closes with a stated recommendation for
`Compono.Bogus`'s continued use in `cosmere-tracker`, including "don't use
it for X" where the evidence supports that.

## Reflection-based NSubstitute stubbing (`HttpMessageHandlerExtensions`)

Not an AutoFixture concept, but entangled with the test kit being migrated:
`HttpMessageHandlerExtensions.ReturnsResponse` uses reflection
(`BindingFlags.NonPublic`) to stub `HttpMessageHandler`'s protected
`SendAsync`. Whether this survives the migration unchanged, gets replaced,
or interacts with the `Compono.NSubstitute` gap-2 migration is determined
during Phase 1.

## What disappeared entirely

Per [ADR-0029 Amendment 2](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-2-2026-08-02-removed-concepts-get-their-own-explicit-inventory-not-just-a-count),
this section names, rather than just counts, every concept from the
sections above that disappeared entirely rather than being replaced by a
Compono equivalent — the story of reduced conceptual complexity, not just
1:1 API translation. Starting candidates from Phase 0's baseline
(confirmed or revised once Phase 1 reaches each one): `IFixture`,
`ICustomization`, `ISpecimenBuilder`, `IRequestSpecification`, the custom
`AutoDataAttribute`/`InlineAutoDataAttribute` subclasses, `BaseFixtureFactory`,
`NamedRequest`. Filled in during Phase 1.

## Multi-tier fixture stacks

`cosmere-tracker`'s AutoFixture setup is layered across three tiers
(`Cosmere.Tracker.TestKit` → `Cosmere.Tracker.Shared.TestKit` → per-suite
local kits under individual test projects). Whether Compono's profile
model collapses this into fewer tiers, and what that means for
maintainability, is recorded here once Phase 1's migration reaches each
tier.
