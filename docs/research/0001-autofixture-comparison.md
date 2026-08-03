# [RESEARCH-0001] AutoFixture vs. Compono: `cosmere-tracker` Dogfooding

**Status:** In Progress (Phase 0 baseline and Phase 1 migration complete, all
73 `cosmere-tracker` tests passing under Compono (72 migrated plus one new
capability test); Phase 2's formal
post-migration metrics/evidence dossier and Phase 3's classification still to
come — see
[the migration guide](../migrating-from-autofixture.md) for Phase
1's real before/after findings in the meantime)

**Feeds:** [PLAN-0007](../plans/0007-milestone-7-dogfooding.md), per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)

This document is the evidence record for Milestone 7's dogfooding pass:
migrating `ncipollina/cosmere-tracker`'s AutoFixture-based test kit
(`test/Cosmere.Tracker.TestKit`) to Compono. It follows
`design-decisions.md`'s `docs/research/` convention — baseline and
post-migration metrics, per-finding evidence, and a closing `## Decisions`
section mapping each finding to the ADR/Amendment/PR it fed into. Sections
below are filled in as each PLAN-0007 phase completes; this is a living
document through Phase 3, not a final write-up.

## Baseline (Phase 0)

Captured against `cosmere-tracker` commit `1bae0b6` ("chore(deps): bump
centrally-managed package versions", 2026-08-03), before any Compono
change. This commit is the previous baseline commit (`2dbd62e`) plus one
intentional, unrelated dependency-version refresh in
`Directory.Packages.props` (AWS SDK, FastEndpoints, Microsoft.Extensions.*,
OpenTelemetry, NSubstitute, DynamoMapper, etc.) that was already present
and intentional at capture time — committed here so the cited commit
exactly matches the working tree the baseline numbers were measured
against, rather than leaving the measurement dependent on an uncommitted
diff.

### Test kit inventory

`test/Cosmere.Tracker.TestKit` (218 lines across 8 `.cs` files, no test
project itself — `IsTestProject=false`, referenced by
`Cosmere.Tracker.Shared.TestKit`, which is in turn referenced by all three
consuming test projects):

| File | Lines | Purpose |
|---|---|---|
| `BaseFixtureFactory.cs` | 32 | Central `IFixture` factory: removes `ThrowingRecursionBehavior`, adds `OmitOnRecursionBehavior`, applies `AutoNSubstituteCustomization { ConfigureMembers = true }` |
| `Attributes/CosmereTrackerAutoDataAttribute.cs` | 24 | `AutoDataAttribute`/`InlineAutoDataAttribute` pair wiring `BaseFixtureFactory` + `CosmereTrackerCustomization` |
| `Attributes/ClientAutoDataAttribute.cs` | 24 | Same pair, additionally freezing `HttpMessageHandler` and adding `HttpClientSpecimenBuilder` |
| `Customizations/CosmereTrackerCustomization.cs` | 18 | Currently an empty extension point (commented-out examples only) |
| `SpecimenBuilders/HttpClientSpecimenBuilder.cs` | 36 | Resolves a frozen `HttpMessageHandler` by type via `ISpecimenContext`, wraps it in a configured `HttpClient` |
| `RequestSpecifications/HttpClientSpecification.cs` | 20 | `IRequestSpecification` matching `HttpClient`-typed requests/parameters |
| `Extensions/HttpMessageHandlerExtensions.cs` | 45 | NSubstitute extension method (`ReturnsResponse`) for stubbing `HttpMessageHandler.SendAsync` via reflection (`BindingFlags.NonPublic`) |
| `Requests/NamedRequest.cs` | 19 | Wrapper record letting specimen builders make name-aware decisions; consumed by `Cosmere.Tracker.Shared.TestKit`'s specimen builders, not by `Cosmere.Tracker.TestKit` itself |

A second, separate test kit, `test/Cosmere.Tracker.Shared.TestKit` (271
lines across 6 files: `SharedCustomization`, four domain-item specimen
builders for `BookItem`/`CharacterItem`/`WorldItem`/edge items, and
`SpecimenBuilderHash`), sits between `Cosmere.Tracker.TestKit` and the
three consuming test projects — none of the consuming test projects
reference `Cosmere.Tracker.TestKit` directly; they reference
`Cosmere.Tracker.Shared.TestKit`, which references `Cosmere.Tracker.TestKit`
transitively. `Cosmere.Tracker.Shared.Tests` additionally has its own
local, further test kit under `test/Cosmere.Tracker.Shared.Tests/TestKit/`
(`PersistenceAutoDataAttribute`, DynamoDB-options/response specimen
builders) for persistence-layer tests. This three-tier fixture stack
(base kit → shared kit → per-suite local kit) is itself a maintainability
data point worth carrying into Phase 2's comparison, not previously called
out in ADR-0029's Context.

### Call-site counts (18 test files, `Api.Tests`/`Shared.Tests`/`Seeder.Tests`)

- `[CosmereTrackerAutoData]`: 1 call site
- `[InlineCosmereTrackerAutoData]`: 7 call sites
- `[ClientAutoData]`/`[InlineClientAutoData]`: 0 call sites in these three
  projects (used only within `Cosmere.Tracker.TestKit`'s own definitions;
  no consuming test currently exercises the `HttpClientSpecimenBuilder`
  path directly through this attribute pair) — **confirmed during Phase 1**:
  zero real call sites anywhere in `cosmere-tracker` outside
  `Cosmere.Tracker.TestKit`'s own definition files. This is itself gap 1's
  Phase 1 finding; `HttpClientSpecimenBuilder`/`HttpClientSpecification`/
  `ClientAutoDataAttribute` were nonetheless migrated (as `ClientTestProfile`/
  `IHttpClientProvider`, by explicit request, since this is a capability
  needed for future tests) rather than dropped, surfacing a further real
  finding: `HttpClient` can't be composed directly as a Compono parameter at
  all (`CMP0001`). See the
  [migration guide](../migrating-from-autofixture.md) for the full
  evidence, including the real (and frequent) `[Frozen]`-for-substitute usage
  found elsewhere that gap 1's rubric evidence actually rests on.

### `dotnet test` baseline run

```
dotnet build Cosmere.Tracker.slnx -c Debug   →  0 errors, 66 warnings (pre-existing NuGet advisories/version-constraint warnings, unrelated to the test kit), ~2.5s (incremental)
dotnet test Cosmere.Tracker.slnx -c Debug --no-build
  Cosmere.Tracker.Api.Tests:    32 passed
  Cosmere.Tracker.Shared.Tests: 38 passed
  Cosmere.Tracker.Seeder.Tests:  2 passed
  total: 72, failed: 0, succeeded: 72, skipped: 0
  duration: 1s 346ms (test execution) / ~2.5s wall (incl. process startup)
```

No Docker/Testcontainers dependency was needed to run this suite (Docker
was unavailable in the environment the baseline was captured in, and the
full suite still passed) — persistence tests
(`BatchRepositoryTests`, `DynamoPartiqlClientTests`, etc.) rely on
specimen-built fakes/local DynamoDB abstractions rather than a live
container for this baseline run.

### Per-file readability notes

- **`BaseFixtureFactory.cs`** — small (32 lines) but dense: three distinct
  framework behaviors (recursion-behavior swap, NSubstitute
  auto-configuration, extensibility hook) are combined in one static
  factory method. A reader has to know what `OmitOnRecursionBehavior` and
  `AutoNSubstituteCustomization { ConfigureMembers = true }` each do
  globally — neither is visible at any individual test's call site.
- **`CosmereTrackerCustomization.cs`** — currently a no-op with
  commented-out examples; the abstraction exists but has never been used
  for its stated purpose (specimen builders/frozen dependencies). A
  candidate for being dropped entirely rather than migrated, per
  ADR-0029's "Migration idiom" (idiomatic Compono over mechanical
  translation) — to be confirmed once Phase 1 checks whether any consumer
  actually needs a project-wide profile hook here.
- **`HttpClientSpecimenBuilder.cs`** / **`HttpClientSpecification.cs`** —
  together implement "detect a request for `HttpClient`, resolve a frozen
  `HttpMessageHandler`, wrap it" — the mechanism is AutoFixture-idiomatic
  (an `ISpecimenBuilder` keyed off `IRequestSpecification`) but requires
  two files and an indirect `ISpecimenContext.Resolve` call to trace; nothing
  in a test signature indicates that a `HttpClient` parameter is specially
  constructed this way.
- **`HttpClientSpecification.cs`** — straightforward once found, but
  finding it requires already knowing `HttpClientSpecimenBuilder` delegates
  matching to a separate `IRequestSpecification` type; two hops to
  understand one behavior.

### Broader maintainability dimensions

- **Framework-specific concepts in play:** `IFixture`, `ICustomization`,
  `ISpecimenBuilder`/`IRequestSpecification`, `AutoDataAttribute`/
  `InlineAutoDataAttribute`, `Freeze<T>()`, `AutoNSubstituteCustomization`,
  `ThrowingRecursionBehavior`/`OmitOnRecursionBehavior` — 8 distinct
  AutoFixture/AutoFixture.AutoNSubstitute concepts a contributor needs to
  recognize before the test kit's behavior is fully legible, on top of
  reflection-based NSubstitute stubbing (`HttpMessageHandlerExtensions`,
  `BindingFlags.NonPublic`) that isn't an AutoFixture concept at all but is
  entangled with this kit.
- **Custom fixture infrastructure present:** yes, a three-tier stack (see
  "Test kit inventory" above) — `Cosmere.Tracker.TestKit` →
  `Cosmere.Tracker.Shared.TestKit` → per-suite local kits — each layer
  adding its own attributes/customizations/specimen builders.
- **Setup visible per test method:** minimal by design — `[CosmereTrackerAutoData]`/
  `[InlineCosmereTrackerAutoData]` hide all fixture configuration behind
  the attribute; a reader sees no indication in a test's own signature that
  NSubstitute members are auto-configured, that recursion is omitted rather
  than failing, or (for `ClientAutoData`, currently unused) that an
  `HttpMessageHandler` is frozen and shared.
  This is the direct AutoFixture-vs.-Compono readability question ADR-0029
  frames for gap 1: explicit `[Shared]` parameters trade this invisibility
  for a longer signature.
- **Concepts a new contributor needs today:** the 8 AutoFixture concepts
  above, plus knowing which of the three fixture-kit tiers to extend for a
  given change (base kit vs. shared kit vs. per-suite local kit), before
  writing a new test that needs custom data shape.

## Post-migration metrics (Phase 2)

_To be filled in after Phase 1's migration completes._

## Concepts removed entirely (Phase 2)

Per [ADR-0029 Amendment 2](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-2-2026-08-02-removed-concepts-get-their-own-explicit-inventory-not-just-a-count),
an explicit named inventory, not just a count — which of the following
disappeared entirely after migration versus were merely replaced
one-for-one, and what (if anything) replaced each:

- `IFixture`
- `ICustomization`
- `ISpecimenBuilder`
- `IRequestSpecification`
- The custom `AutoDataAttribute`/`InlineAutoDataAttribute` subclasses
  (`CosmereTrackerAutoDataAttribute`, `ClientAutoDataAttribute`)
- `BaseFixtureFactory` and other fixture-factory infrastructure
- `NamedRequest`
- Any other concept Phase 1 surfaces beyond this starting list

_To be filled in after Phase 1's migration completes._

## Per-finding evidence dossier (Phase 2)

_To be filled in — one entry per finding (the three named gaps, the
mandatory `Compono.Bogus` finding, and any further finding surfaced during
migration), each with frequency, before/after snippet, principle-alignment
note, and classification per ADR-0029's five-way taxonomy. Per
[Amendment 1](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-1-2026-08-02-the-componobogus-experiment-is-mandatory-not-its-conclusion),
the `Compono.Bogus` finding's dossier ends in a stated recommendation for
its continued use in `cosmere-tracker` — a negative or partial
recommendation is a valid, successful outcome, not a shortfall._

## Classifications (Phase 3)

_To be filled in per ADR-0029's five-way taxonomy (bug / roadmap candidate
/ acceptable Compono-native alternative / intentional design difference /
migration-only friction)._

## Decisions

_To be filled in — lists exactly which ADR(s)/Amendment(s)/bug-fix PR(s)
each finding fed into, per ADR-0029's "Recorded via existing mechanics."_

## Final architectural conclusion and recommendation (Phase 4)

_To be filled in — answers ADR-0029's "Final architectural conclusion"
questions and, per
[Amendment 3](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-3-2026-08-02-the-final-architectural-conclusion-ends-with-an-explicit-recommendation),
synthesizes them into one explicit, evidence-backed recommendation: a
stated next action for `cosmere-tracker` and Compono, not just a capability
statement._
