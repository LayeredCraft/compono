# Future Packages

Compono's shipped package set (see [Package Guides](../packages/index.md))
is eleven independently installable packages — `Compono`, `Compono.XunitV3`,
`Compono.NSubstitute`, `Compono.Bogus`, `Compono.TUnit`,
`Compono.TestDoubles`, `Compono.DependencyInjection`, `Compono.Http`,
`Compono.Logging`, `Compono.MSTest`, and `Compono.NUnit` — plus
`Compono.Generators`, which is not a twelfth installable package at all.
It's `IsPackable=false`
([ADR-0003](../adr/0003-generator-package-distribution.md)) and ships
embedded inside `Compono`'s own `.nupkg` as an analyzer
(`analyzers/dotnet/cs`) — a consumer never references it directly, and it
never appears on nuget.org on its own. `Compono.TUnit` graduated from this
page's roadmap once [PLAN-0040](../plans/0040-compono-tunit-package-design.md)
completed all its phases — see
[`Compono.TUnit`](../packages/compono-tunit.md) for what it ships.
`Compono.TestDoubles` graduated the same way once
[PLAN-0043](../plans/0043-compono-generated-test-doubles.md) completed all
its phases — see
[`Compono.TestDoubles`](../packages/compono-testdoubles.md) for what it
ships. `Compono.DependencyInjection` did **not** graduate from this page's
Gate A/Gate B pipeline the way those two did — it was never listed here as
an admitted candidate first. It came directly out of a gating investigation
for a different hypothesized package (`Compono.BUnit`, evaluated and
rejected) whose dogfooding evidence redirected toward this narrower,
general capability instead — see
[ADR-0047](../adr/0047-compono-dependencyinjection-configured-resolution-bridge.md)
and [RESEARCH-0007](../research/0007-trivia-manager-bunit-dependency-injection.md)
for the full account, and [`Compono.DependencyInjection`](../packages/compono-dependencyinjection.md)
for what it ships. See also the "richer `Microsoft.Extensions.DependencyInjection`
integration" entry below — that's a **different**, larger idea this
package's narrower scope deliberately didn't attempt. `Compono.Http`
likewise didn't graduate from this page — it was never listed here as an
admitted candidate first either. It came out of a dedicated admission
research doc triggered by a real `alexa-vox-craft` dogfooding need (an
existing reflection-based `HttpMessageHandler` fake with no Compono
equivalent), not this page's candidate pipeline — see
[ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md) and
[RESEARCH-0009](../research/0009-compono-http-admission-research.md) for
the full account, and [`Compono.Http`](../packages/compono-http.md) for
what it ships. `Compono.MSTest` graduated from this page's roadmap once
[PLAN-0057](../plans/0057-compono-mstest-package-design-impl-plan.md)'s
implementation was underway against
[ADR-0057](../adr/0057-compono-mstest-package-design.md) (`Accepted`,
including Amendment 1, which raised the supported `MSTest.TestFramework`
floor from `3.0.0` to `4.0.0` after implementation found the `3.x`/`4.x`
lines are binary-incompatible) — see
[`Compono.MSTest`](../packages/compono-mstest.md) for what it ships.
`Compono.NUnit` graduated from this page's roadmap once
[PLAN-0059](../plans/0059-compono-nunit-package-design-impl-plan.md)'s
implementation completed (`Done`) against
[ADR-0059](../adr/0059-compono-nunit-package-design.md) (`Accepted`
2026-09-03) — Gate A satisfied with stronger, spike-verified evidence
([RESEARCH-0018](../research/0018-nunit-integration-viability-research.md)
§2), Gate B satisfied by an explicit product-owner request (not
dogfooding evidence), the same mechanism that already gated
`Compono.TUnit` and Compono-owned source-generated test doubles — see
[`Compono.NUnit`](../packages/compono-nunit.md) for what it ships.

## Admission model

See [Capability & Package Admission](../architecture/capability-admission.md)
for the full, standalone process this page's dispositions are decided
against — this section is a short summary, not the operational reference.
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
roadmap content. Compono-owned source-generated test doubles made the same
full progression, shipping as `Compono.TestDoubles` once
[PLAN-0043](../plans/0043-compono-generated-test-doubles.md) completed —
see [`Compono.TestDoubles`](../packages/compono-testdoubles.md), also not
roadmap content anymore. `Compono.NUnit` made the same full progression and
graduated too, see above. `Compono.Options` — cleared Gate A and Gate B
([RESEARCH-0028](../research/0028-compono-options-configuration-admission-research.md),
reassessed 2026-09-07) via a dedicated admission research doc (like
`Compono.Http`/`Compono.DependencyInjection`, not this page's own candidate
pipeline), triggered by an explicit product-owner request and reassessed
once against Compono's own composition-ergonomics precedent (`Share<T>()`,
`Compono.Bogus`) before Gate A was confirmed — graduated from this page's
roadmap once [PLAN-0064](../plans/0064-compono-options-testing-support.md)'s
implementation completed against
[ADR-0061](../adr/0061-compono-options-testing-support.md) (`Accepted`
2026-09-08) — see [`Compono.Options`](../packages/compono-options.md) for
what it ships. A dedicated `Compono.Configuration` package was explicitly
evaluated and rejected (RESEARCH-0028 §3/§17) — see "Documentation-only
ideas" below for the Cookbook deliverable that survives instead. No
candidate currently sits at roadmap-item status.

## Roadmap items (cleared Gate A and Gate B)

None currently.

## Admitted candidates (cleared Gate A, no evidence yet)

None currently.

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
  today and doesn't need a package. **Note:** `Compono.DependencyInjection`
  itself already shipped, under exactly this name, but as a narrower thing
  than this entry describes — a configured-resolution `IServiceProvider`
  bridge (`row.AsServiceProvider()`), not keyed-service resolution or
  DI-scope ownership, and requiring only a small, honestly-scoped core
  primitive (`CompositionRow.TryResolveConfigured`), not the
  keyed/scope-ownership core concept this entry means. This entry stays
  open for that larger, still-undesigned idea — it is not retired by the
  package that now shares its name. See
  [`Compono.DependencyInjection`](../packages/compono-dependencyinjection.md)
  for what actually shipped.
- A reflection-based compatibility mode or package, for the still-open
  runtime-reflection question tracked in
  [Source Generation](../architecture/current/source-generation.md) — unchanged
  by ADR-0039, not evaluated against Gate A here.
- **Configuration composition (`IConfiguration`/`ConfigurationBuilder`) —
  documentation-only, not a package.** Evaluated alongside
  `Compono.Options` (RESEARCH-0028 §3/§12a) and explicitly rejected as a
  package, including under the sharper composition-ergonomics question
  that admitted `Compono.Options` itself — `Register<IConfiguration>(...)`
  already keeps a consumer fully inside Compono's composition model, with
  no multi-interface consistency risk analogous to Options. **Required
  Cookbook deliverable, tracked against [ADR-0061](../adr/0061-compono-options-testing-support.md):**
  basic in-memory `IConfiguration` composition, layered/override
  configuration, reusable configuration through a profile, and the
  routing guidance distinguishing "use ordinary Configuration +
  `Register<IConfiguration>`" from "use `Compono.Options` for the
  Options interfaces it owns" — explicitly not implying a
  `Compono.Configuration` package exists. This documentation survives as
  a defined deliverable of the `Compono.Options` effort even though no
  package will be created for it. **Fulfilled** — see
  [Compose Configuration From an In-Memory Collection](../cookbook/compose-configuration-from-an-in-memory-collection.md),
  [Layer Configuration Overrides in a Test](../cookbook/layer-configuration-overrides-in-a-test.md),
  and [Reuse Configuration Through a Profile](../cookbook/reuse-configuration-through-a-profile.md).

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

ADR-0039 records no candidate order. `Compono.TUnit`, the
source-generated-test-doubles capability, `Compono.NUnit`, and now
`Compono.Options` all cleared Gate B through an explicit product-owner
request, not dogfooding evidence — the two real ADR-0029 dogfooding
passes recorded in [Post-MVP](post-mvp.md) still haven't produced a
roadmap candidate of their own in this space; `Compono.Options` reached
this page through the same admission-research path as `Compono.Http`
and `Compono.DependencyInjection`, not this page's own candidate
pipeline. No admitted candidates currently remain on this page — one
roadmap item (`Compono.Options`) does, tracked above. If a future
candidate clears Gate B around the same time as another still-open one,
ADR-0039's non-binding heuristics (value relative to maintenance cost;
architectural-validation diversity over repeating an already-proven
pattern) apply — category completion (finishing all test-framework
integrations before starting a test-double one, or vice versa) is
explicitly rejected as a sequencing principle.

Any admitted candidate becomes real roadmap content the moment real
demand and a concrete design exist — see [Post-MVP](post-mvp.md) for the
evidence-backed process, per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md).
A future package gets its own [Package Guide](../packages/index.md) entry
the moment it ships.
