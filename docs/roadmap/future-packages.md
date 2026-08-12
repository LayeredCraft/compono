# Future Packages

Compono's MVP package set is fully shipped (see
[Package Guides](../packages/index.md)): four independently installable
packages — `Compono`, `Compono.XunitV3`, `Compono.NSubstitute`, and
`Compono.Bogus` — plus `Compono.Generators`, which is not a fifth
installable package at all. It's `IsPackable=false`
([ADR-0003](../adr/0003-generator-package-distribution.md)) and ships
embedded inside `Compono`'s own `.nupkg` as an analyzer
(`analyzers/dotnet/cs`) — a consumer never references it directly, and
it never appears on nuget.org on its own. One additional package —
`Compono.TUnit` — is committed via an `Accepted` ADR
([ADR-0040](../adr/0040-compono-tunit-package-design.md)), with
[PLAN-0040](../plans/0040-compono-tunit-package-design.md) (`In Progress`)
tracking its implementation; see
[Roadmap items](#roadmap-items-cleared-gate-a-and-gate-b) below. No other
candidate on this page has cleared both gates yet.

## Admission model

[ADR-0039](../adr/0039-future-extension-package-admission-gate-and-release-sequence.md)
records a two-stage admission model for everything on this page:

1. **Gate A (architectural admission)** — is the candidate a legitimate,
   non-wrapper Compono extension at all? Evaluated once, recorded below as
   each candidate's disposition.
2. **Gate B (evidence admission, [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md))** —
   does real demand/dogfooding evidence exist yet?

A candidate that clears Gate A is an **admitted candidate**: architecturally
legitimate, but still just an idea until Gate B clears too and it becomes a
**roadmap item** with its own problem-focused `Proposed` ADR. A roadmap
item becomes **committed implementation work** only once that ADR itself
reaches `Accepted` (its own full design pass, not just the problem
statement) and a `Plan` moves `In Progress` against it — the same
ADR/Plan mechanics every other change in this repo goes through, per
`docs/adr/README.md`/`docs/plans/README.md`. `Compono.TUnit` (below) is
the one candidate that has reached **roadmap item** status and, as of
[PLAN-0040](../plans/0040-compono-tunit-package-design.md) moving
`In Progress`, committed implementation work too; nothing besides
`Compono.TUnit` is roadmap content at all.

## Roadmap items (cleared Gate A and Gate B)

- **`Compono.TUnit`** — cleared Gate A on TUnit's `IDataSourceAttribute`
  family (especially `UntypedDataSourceGeneratorAttribute`, which TUnit's
  own docs cite AutoFixture-shaped libraries as the motivating case for),
  its per-row `TestBuilderContext`, and its combinatorial interplay with
  `[Arguments]` — a real integration surface following `Compono.XunitV3`'s
  `CompositionRow`-based model
  ([ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md)).
  Cleared Gate A on that surface specifically, not because TUnit is
  source-generated — that architectural kinship is not, on its own,
  consumer value; see ADR-0039 for what was retired and why. Cleared
  Gate B via an explicit product-owner request (ADR-0039's real-demand
  trigger). [ADR-0040](../adr/0040-compono-tunit-package-design.md)
  (`Accepted`) records the resulting package design — method-parameter
  composition only for the first release, full parity with
  `Compono.XunitV3`'s scope; see that ADR for why constructor-dependency
  composition was investigated and deferred.
  [PLAN-0040](../plans/0040-compono-tunit-package-design.md) tracks
  implementation, phase by phase.

## Admitted candidates (cleared Gate A, no evidence yet)

Each follows the pattern `Compono.NSubstitute`/`Compono.Bogus` already
establish — a package built entirely on a public core extension point,
core itself unchanged:

- **`Compono.NUnit`** — NUnit's `IParameterDataSource` gives genuine
  per-parameter composition granularity `Compono.XunitV3`'s row model
  doesn't have; `ITestBuilder`/`IFixtureBuilder` cover the row/fixture-
  constructor cases.
- **`Compono.MSTest`** — MSTest's `ITestDataSource` is a stable,
  long-standing extension point; thinner than TUnit's or NUnit's (no
  per-row context, no combinatorial engine) but still real value over an
  in-body `Composer.Create<T>()` call. Weakest of the three test-framework
  candidates.

## Documentation-only ideas (do not clear Gate A as packages today)

- **FakeItEasy integration** — `FakeItEasy.Sdk.Create.Fake(Type)` is a
  real extension point, but a `Compono.FakeItEasy` package would be ~80%
  structurally identical to `Compono.NSubstitute`, and no dogfooding pass
  in this repo's history has surfaced friction pointing at FakeItEasy over
  NSubstitute. Recorded as a documentation recipe instead — "how to write
  your own `ICompositionValueProvider` for FakeItEasy," following
  `Compono.NSubstitute`'s published shape — not yet written. Promote back
  to a package candidate only if that recipe itself surfaces real demand.
- **A richer `Microsoft.Extensions.DependencyInjection` integration** — the
  only ideas that need real Compono-specific bridging (keyed-service
  resolution, DI-scope ownership for a composition) require a **core**
  concept that doesn't exist yet (a keyed/named composition request; a
  composition-scope-owns-DI-scope lifetime model) — itself a future
  core-extension ADR, not something a package's own design pass should
  invent. Every other idea (auto-registration sugar, descriptor-driven
  validation) is a few lines against the existing
  `UseServiceProvider(...)` fallback
  ([ADR-0019](../adr/0019-registrations-and-service-provider-injection.md))
  today and doesn't need a package. If the prerequisite core design ever
  happens, `Compono.DependencyInjection` remains the right name — ADR-0019
  already anticipated it.
- A reflection-based compatibility mode or package, for the still-open
  runtime-reflection question tracked in
  [Source Generation](../architecture/current/source-generation.md) — unchanged
  by ADR-0039, not evaluated against Gate A here.

## Deferred indefinitely

- **Moq integration** — blocked on maintenance health, not TFM
  compatibility (Moq's `netstandard2.0`/`netstandard2.1` assets are
  consumable from `net8.0`/`net9.0` via NuGet's own asset-compatibility
  fallback, per [ADR-0037](../adr/0037-netstandard2.1-compatibility-floor.md) —
  an earlier draft claimed otherwise and was corrected). Moq has shipped
  no release in roughly 23 months and carries durable reputational damage
  from the 4.20.0 SponsorLink incident. Re-evaluate if Moq resumes active,
  regular releases — this is a dependency-health block, not lost interest.

## No committed sequence

ADR-0039 records no candidate order. `Compono.TUnit` cleared Gate B
through an explicit product-owner request, not dogfooding evidence — the
two real dogfooding passes recorded in [Post-MVP](post-mvp.md) still
haven't produced a roadmap candidate of their own in this space. Ranking
the remaining admitted candidates (`Compono.NUnit`/`Compono.MSTest`)
against each other, or against a hypothetical next TUnit-style request,
still has no evidentiary basis. If more than one clears Gate B around the
same time, ADR-0039's non-binding heuristics (value relative to
maintenance cost; architectural-validation diversity over repeating an
already-proven pattern) apply — category completion (finishing all
test-framework integrations before starting a test-double one, or vice
versa) is explicitly rejected as a sequencing principle.

Any admitted candidate above becomes real roadmap content the moment real
demand and a concrete design exist — see [Post-MVP](post-mvp.md) for the
evidence-backed process, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md).
A future package gets its own [Package Guide](../packages/index.md) entry
the moment it ships.
