# Future Packages

Compono's shipped package set (see [Package Guides](../packages/index.md))
is five independently installable packages — `Compono`, `Compono.XunitV3`,
`Compono.NSubstitute`, `Compono.Bogus`, and `Compono.TUnit` — plus
`Compono.Generators`, which is not a sixth installable package at all. It's
`IsPackable=false`
([ADR-0003](../adr/0003-generator-package-distribution.md)) and ships
embedded inside `Compono`'s own `.nupkg` as an analyzer
(`analyzers/dotnet/cs`) — a consumer never references it directly, and it
never appears on nuget.org on its own. `Compono.TUnit` graduated from this
page's roadmap once [PLAN-0040](../plans/0040-compono-tunit-package-design.md)
completed all its phases — see
[`Compono.TUnit`](../packages/compono-tunit.md) for what it ships. One
candidate — Compono-owned source-generated test doubles — has since cleared
both admission gates and is a roadmap item; see below.

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
`docs/adr/README.md`/`docs/plans/README.md`. `Compono.TUnit` made that full
progression — admitted candidate, roadmap item, committed implementation
work, and finally a shipped package once
[PLAN-0040](../plans/0040-compono-tunit-package-design.md) completed — and
is documented as a [Package Guide](../packages/compono-tunit.md) now, not
roadmap content. Compono-generated test doubles is the one other candidate
to reach roadmap-item status since — see below.

## Roadmap items (cleared Gate A and Gate B)

- **Compono-owned source-generated test doubles** — a fallback default-value
  generator for otherwise-unresolvable **interface** leaves in a composition
  graph (v1 scope, per [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md) —
  abstract-class and delegate leaves are not part of this item; PLAN-0043
  has no work for either), giving Compono an AOT-safe, zero-declaration
  alternative to `Compono.NSubstitute`'s runtime-proxy dependency for the
  common case. Cleared Gate A on the strength of a real, checked finding: no
  external source-generated mocking library (TUnit.Mocks, Imposter, Rocks —
  all researched directly) can preserve `composer.Create<T>()`'s
  zero-declaration UX, because none can observe another generator's output
  in the same compilation, and only `Compono.Generators` itself already
  performs the composition-graph discovery this capability depends on.
  Explicitly **not** admitted as a general-purpose mocking framework —
  see [ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)'s
  Non-Goals for the load-bearing scope boundary. Cleared Gate B via an
  explicit product-owner request, the same trigger shape that cleared
  `Compono.TUnit`'s Gate B, not dogfooding evidence. `Compono.NSubstitute`
  is not deprecated or replaced by this — see the ADR's Decision Drivers.
  Problem recorded in [ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)
  (`Accepted`). The deep-design pass is decided in
  [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md)
  (`Accepted`, two Amendments — both pre-implementation review corrections,
  no code written yet): a distinct-receiver-type control surface (no
  interception — proven viable by a real spike, but rejected as unnecessary
  once the generated double implements its interface directly), `[Shared]
  IRepository` unchanged plus a generator-emitted `Configure(...)` bridge
  per interface (zero `CompositionScope` changes), v1 including configured
  returns/exceptions — argument-independent, no argument matchers (not
  default-value-only) — and a package split: `ReturnConfigBuilder<T>` and a
  `[ModuleInitializer]`-populated registry live in **core `Compono`** (moved
  there by Amendment 2, fixing a cross-assembly reference the original
  design couldn't make), the compile-time-gated generator logic stays in
  core `Compono.Generators`, and a deliberately small optional package,
  **`Compono.TestDoubles`**, holds just the provider and
  `UseGeneratedTestDoubles()`. Not yet committed implementation work
  per [ADR-0039](../adr/0039-future-extension-package-admission-gate-and-release-sequence.md)'s
  terminology — that requires a `Plan` moving `In Progress`, which hasn't
  started yet.

`Compono.TUnit` was the first candidate to reach this status — see the
Admission model note above; it shipped as a package and moved to
[Package Guides](../packages/index.md).

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

ADR-0039 records no candidate order. `Compono.TUnit` and the
source-generated-test-doubles capability both cleared Gate B through an
explicit product-owner request, not dogfooding evidence — the two real
dogfooding passes recorded in [Post-MVP](post-mvp.md) still haven't
produced a roadmap candidate of their own in this space. Ranking the
remaining admitted candidates (`Compono.NUnit`/`Compono.MSTest`) against
each other, or against a hypothetical next explicit-request-driven item,
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
