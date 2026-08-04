# Compono MVP

## Objective

The MVP should prove that Compono can provide a coherent, fast, source-generated test composition experience across:

- Core object composition
- xUnit v3
- NSubstitute
- Bogus

The MVP is not an AutoFixture migration layer and does not aim for feature parity.

## Success Criteria

The MVP succeeds when:

1. A developer can compose typical modern .NET object graphs without runtime constructor reflection on the generated path.
2. An xUnit v3 theory can declare composed parameters.
3. A shared test-double parameter is injected into the system under test.
4. Bogus can provide deterministic semantic values through an ancillary package.
5. A failure produces a readable dependency path and reproducible seed.
6. One real test project can be rewritten to use Compono and remains pleasant to maintain.
7. The core package has no dependencies on test frameworks, mocking frameworks, or Bogus.

## MVP Package Set

```text
Compono
Compono.Generators
Compono.XunitV3
Compono.NSubstitute
Compono.Bogus
```

`Compono.Generators` may be shipped as a transitive analyzer dependency rather than a package users reference directly.

## Milestone 0: Product and Design Contract

### Deliverables

- Compono Manifesto
- Architecture document
- Public API design document
- MVP document
- Initial architecture decision records
- 20–30 desired usage examples
- Initial package dependency diagram

### Exit Criteria

- Core terminology is stable enough to begin implementation
- Open questions are explicitly recorded
- Representative examples cover all MVP packages

## Milestone 1: Source-Generation Foundation

### Scope

- Incremental source generator
- Discovery of constructible source types
- Constructor selection prototype
- Generated direct constructor invocation
- Generated request metadata
- Plan registration mechanism
- Compile-time diagnostics for unsupported or ambiguous construction
- Benchmark harness comparing generated construction with reflection baselines

### GitHub Issue Themes

- Create generator project
- Define generated-plan contract
- Discover constructors
- Generate plan registration
- Emit required-member assignments
- Emit nullability metadata
- Add generator snapshot tests
- Add benchmark project

### Exit Criteria

```csharp
var customer = composer.Create<Customer>();
```

uses a generated plan for a representative record or class.

## Milestone 2: Core Composition Engine

Design: [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)
(request/pipeline/failure/diagnostics),
[ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md)
(scope/shared values/recursion),
[ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)
(path identity/random forking/CreateMany seed),
[ADR-0013](adr/0013-collection-generation-semantics.md) (collections).
These supersede the earlier ADR-0007/0008/0009 after a deep design
review — see each ADR's Links section.
Tracked by [PLAN-0002](plans/0002-milestone-2-core-composition-engine.md).

### Scope

- `CompositionContext`
- Composition requests and paths
- Provider pipeline
- Deterministic seed
- Forkable random source
- Built-in primitive generation
- Enum and nullable generation
- Common collection generation
- Exact registrations
- Composition scopes
- Shared values
- Recursion detection
- Structured diagnostics
- `Create<T>()`
- `CreateMany<T>()`

### Initial Built-in Types

- `string`
- `bool`
- Integral numeric types
- Floating-point types
- `decimal`
- `Guid`
- `DateTime`
- `DateTimeOffset`
- `DateOnly`
- `TimeOnly`
- `TimeSpan`
- Enums
- Nullable value types
- Arrays
- `List<T>`
- `IReadOnlyList<T>`
- `HashSet<T>`
- `Dictionary<TKey, TValue>`

This list may be reduced if implementation complexity threatens the milestone.

### Exit Criteria

- Typical object graphs compose deterministically
- Shared instances are reused correctly
- Recursive graphs fail clearly
- Generated-plan execution is the preferred path
- Provider precedence is covered by tests

## Milestone 3: Profiles and Configuration

Design: [ADR-0017](adr/0017-immutable-composer-configuration-and-builder-model.md)
(immutable `Composer`/`CompositionBuilder`/`CompositionConfiguration` split,
build-time configuration validation), [ADR-0018](adr/0018-composition-profiles.md)
(`ICompositionProfile`, profile application order, recursion/provenance),
[ADR-0019](adr/0019-registrations-and-service-provider-injection.md) (exact
registrations, duplicate-registration conflicts, native `IServiceProvider` fallback
in stage 3), [ADR-0020](adr/0020-composition-configuration-rules.md) (type/member
value rules as internal stage-4 providers, collection-size as queried
configuration policy). [ADR-0019](adr/0019-registrations-and-service-provider-injection.md)
adds the `ManualResolve` path-segment kind (an additive extension to
[ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)'s
`Accepted` path-identity contract, not an edit to it) and a construction-cycle
guard around registration/rule factory invocation. Public
provider extensibility (how NSubstitute/Bogus/custom pattern-matching logic
eventually plug into stages 5/6) is deliberately **deferred to Milestone 5** — see
that milestone's section below.
Tracked by [PLAN-0003](plans/0003-milestone-3-profiles-and-configuration.md).

### Scope

- Immutable composer configuration (`Composer.Create(builder => ...)`, frozen
  `CompositionConfiguration`)
- Reusable profiles (`ICompositionProfile`, `AddProfile<T>()`/`AddProfile(instance)`,
  eager in-order application, cycle detection)
- Integration extension registration (ordinary C# extension methods on
  `CompositionBuilder` — no new core mechanism required)
- Collection-size configuration (global default + member-scoped override, queried by
  stage 7, not a stage-4 rule)
- Exact type registrations (`Register<T>(Func<ICompositionContext, T>)`/
  `Register<T>(Func<T>)`, duplicate registration is a build-time conflict)
- Native `IServiceProvider` fallback (`UseServiceProvider(IServiceProvider)`,
  folded into stage 3 after exact registrations)
- Type/member rule prototype (`.For<T>().Use(...)`, `.For<T>().Member(...).Use(...)`,
  exact-type matching, member rule beats type rule)
- Configuration conflict diagnostics (build-time `CompositionConfigurationException`
  for duplicate registrations/rules and profile cycles, naming every conflicting
  source)

### Exit Criteria

A project can define one reusable profile and use it for both programmatic and test-framework composition.

## Milestone 4: xUnit v3 Integration

Design: [ADR-0021](adr/0021-row-composition-entry-point-for-test-framework-integrations.md)
(core `CompositionRow`/`CompositionRequestKind.TestParameter` entry point, the
stage-2 shared-value read-gate change), [ADR-0022](adr/0022-compono-xunit-package-design.md)
(`Compono.XunitV3` package: `[Compose]`/`[Compose<TProfile>]`/`[Shared]`
attributes, inline/composed binding, profile selection, seed policy,
diagnostics, package dependencies), [ADR-0023](adr/0023-rename-compono-xunit-to-compono-xunitv3.md)
(the `Compono.Xunit` → `Compono.XunitV3` rename). Implemented across
Phases 0-4 (core entry point, attribute skeleton, binding algorithm, test
suites/verification, docs/cleanup) — see
[PLAN-0004](plans/0004-milestone-4-xunit-integration.md) for the phase-by-phase
account. The one gap that remained open past Phase 4 — an interface/abstract/
delegate-typed `[Compose]`-attributed parameter (including the `IRepository`
shape in the Example below) reported CMP0003 and failed to compile even when a
profile registration or a runtime provider would satisfy it — is now resolved
by [PLAN-0005](plans/0005-milestone-5-nsubstitute-integration.md) Phase 2, see
[ADR-0024's Amendment 2](adr/0024-public-provider-extensibility-model.md) and
PLAN-0004's Open Items for the full account.

### Scope

- xUnit v3 data attribute
- One composition context per theory row
- Parameter request metadata
- Inline values plus composed values
- Shared parameter support
- Profile selection
- Seed reporting
- Generator support for test methods if needed

### Example

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void Creates_service(
    [Shared] IRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

This shape compiles and runs today — an interface-typed `[Shared]` parameter
like `IRepository` is composed as a provider-resolved leaf
([ADR-0024's Amendment 2](adr/0024-public-provider-extensibility-model.md)),
satisfied at runtime by whatever `TProfile.Configure` registers (a plain
`Register<T>(...)`, or `Compono.NSubstitute`'s `UseNSubstitute()`).
`test/Compono.XunitV3.SampleTests`' original `[Shared]` theory still uses a
concrete `Repository` type (predating this fix); `NSubstituteTests.cs` in the
same project runs this exact interface-typed shape for real.

### Exit Criteria

Met, verified through `test/Compono.XunitV3.Tests` and a real xUnit v3 runner
against `test/Compono.XunitV3.SampleTests` (PLAN-0004 Phase 3), including a
bare interface/abstract/delegate-typed `[Compose]` parameter as of PLAN-0005
Phase 2:

- Composed parameters work under xUnit v3
- Inline values take precedence
- Shared values flow into composed systems under test
- Failure output includes a seed

## Milestone 5: NSubstitute Integration

Owns the public provider-extensibility design deferred by Milestone 3's design
review (`docs/adr/0018-composition-profiles.md`'s Context, and the M3 design
review's first fork): how an integration package contributes open-ended,
pattern-matching logic (e.g. "any interface type") into pipeline stages 5/6, as
opposed to the closed-set, per-type/per-member rules Milestone 3 already covers via
internal Compono-authored stage-4 providers
(`docs/adr/0020-composition-configuration-rules.md`). Deliberately not designed in
Milestone 3, since it had no real consumer there.

Design: [ADR-0024](adr/0024-public-provider-extensibility-model.md) (the core
public provider contract — `ICompositionValueProvider`, registration into stages
5/6, diagnostics identity — reusable by Milestone 6 without a redesign),
[ADR-0025](adr/0025-compono-nsubstitute-package-design.md) (`Compono.NSubstitute`
package: substitutable-shape rules including delegate types, `NSubstituteOptions`,
diagnostics). **ADR-0024's core engine extension point is implemented
(PLAN-0005 Phase 0)** — `builder.AddSemanticProvider(...)`/
`builder.AddTestDoubleProvider(...)` are real, tested public API today.
**`Compono.NSubstitute` itself (ADR-0025) is implemented and test-covered/
end-to-end verified (PLAN-0005 Phase 2)** — `NSubstituteProvider`/
`NSubstituteOptions`/`UseNSubstitute()` are real, tested code, verified both by
`Compono.NSubstitute.Tests` and by a real packaged `Compono.XunitV3.SampleTests`
run of this milestone's own Goal scenario. **This milestone is complete** —
see [PLAN-0005](plans/0005-milestone-5-nsubstitute-integration.md) for the
phase-by-phase tracker (all four phases `Done`) and
[ADR-0024's Amendment 2](adr/0024-public-provider-extensibility-model.md) for
a `Compono.Generators` compile-time check (`CMP0003`) Phase 2's real
verification found and fixed along the way — an interface/abstract-class/
delegate root is now correctly left for a provider to satisfy at runtime
instead of being rejected at compile time.

### Scope

- Test-double provider contract
- Interface substitutes
- Delegate substitutes (added during ADR-0025's design — NSubstitute supports
  this natively at negligible extra cost; not in this bullet list originally)
- Optional abstract-class substitutes
- Shared substitute reuse
- Integration-specific configuration
- Clear diagnostics when substitution is unsupported

### Non-goals

- Recursive auto-configuration of substitute members
- NSubstitute API wrappers
- Pinning NSubstitute versions in the core package

### Exit Criteria

A typical service test can receive a shared substitute, a composed system under test, and a composed request with no manual setup.

## Milestone 6: Bogus Integration

Owns the reference implementation of Milestone 5's stage-5/6 provider
architecture, and the one core gap that architecture didn't yet need to close:
Milestone 5's `Compono.NSubstitute` never needed randomness, so nothing exposed
deterministic, path-independent random values to a provider or registration
factory. Design: [ADR-0026](adr/0026-deterministic-seed-derivation-for-providers.md)
(core capability: `ICompositionContext.DeriveSeed()`, an on-demand, path-hashed
seed reusing [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)'s
existing mechanism, deliberately not exposing `IRandomSource` or path internals),
[ADR-0027](adr/0027-compono-bogus-package-design.md) (`Compono.Bogus` package:
three customization models — a conservative member-name convention provider
(stage 5), an explicit member-level `UseBogus(faker => ...)` rule (stage 4
sugar), and a whole-object `UseBogus<T>(...)` registration (stage 3 sugar);
correlated values satisfied via Bogus's own `Faker<T>` rather than a new
Compono-native `.DependsOn(...)` mechanism, which is explicitly deferred; verified
coexistence with `Compono.NSubstitute` via disjoint type claims, with zero
reference between the two packages in either direction), [ADR-0028](adr/0028-configurable-bogus-member-name-conventions.md)
(configurable conventions — `BogusConvention`, `BogusOptions.AddAlias`/
`AddConvention`, extending ADR-0027's fixed allowlist with consumer-defined
aliases and custom exact-name conventions; scoped to a single `UseBogus(...)`
call, with cross-call/cross-profile detection explicitly deferred; a new ADR,
not an amendment to ADR-0027). All three ADRs are `Accepted`; tracked by
[PLAN-0006](plans/0006-milestone-6-bogus-integration.md).
**ADR-0026's core capability is implemented (PLAN-0006 Phase 0)** —
`ICompositionContext.DeriveSeed()` is real, tested public API today.
**`Compono.Bogus` itself (ADR-0027) is implemented (PLAN-0006 Phase 1)** —
`BogusMemberNameProvider`/`BogusOptions`/`UseBogus()`/`UseBogus<T>()`/the
member-rule `UseBogus(...)` sugar are real code. **Configurable conventions
(ADR-0028) are also implemented (PLAN-0006 Phase 2)** — `BogusConvention`,
`BogusOptions.AddAlias`/`AddConvention`, and `BogusMemberNameProvider`'s
merged-conventions constructor overload are real code. **`Compono.Bogus` is
now test-covered and end-to-end verified (PLAN-0006 Phase 3)** —
`Compono.Bogus.Tests` (60 tests × 2 TFMs) covers the base package,
configurable conventions, `Compono.NSubstitute` coexistence (any call order),
and `UseBogus<T>()`'s per-request lifetime/concurrency contract, and a real
packaged `test/Compono.XunitV3.SampleTests` run proves this milestone's own
Goal scenario end-to-end — see the plan's phase-by-phase status.

### Scope

- Semantic value-provider contract
- Shared deterministic seed (`ICompositionContext.DeriveSeed()`, ADR-0026 —
  implemented, PLAN-0006 Phase 0)
- Bogus `Faker` access
- Locale configuration
- Conservative member-name conventions
- Configurable conventions — aliases and custom exact-name conventions on top
  of the built-in allowlist (ADR-0028)
- Explicit member rules
- Initial correlated-value experiment (satisfied via whole-object `Faker<T>`,
  ADR-0027 — not a new Compono-native dependency mechanism)

### Initial Conventions

- `FirstName`
- `LastName`
- `FullName`
- `Email`
- `PhoneNumber`
- `StreetAddress`
- `City`
- `State`
- `PostalCode`
- `CompanyName`

Ambiguous member names such as `Name` should not be guessed aggressively —
resolved by [ADR-0027](adr/0027-compono-bogus-package-design.md): the allowlist
above, exact match, case-sensitive, gated to `string`-typed members only.

### Exit Criteria

A composed customer can receive realistic, deterministic values without the core
package referencing Bogus — and `UseBogus()`/`UseNSubstitute()` compose in one
profile, any call order, with no special ordering or package-to-package
dependency, per [PLAN-0006](plans/0006-milestone-6-bogus-integration.md)'s Goal
scenario. **Met** as of PLAN-0006 Phase 3 — `Compono.Bogus.Tests`' coexistence
coverage and a real packaged `test/Compono.XunitV3.SampleTests` run
(`BogusTests.Saves_order`) both verify this exit criterion directly. Phase 4
(docs/cleanup) is also **Done** — see PLAN-0006. **Milestone 6 is complete.**

## Milestone 7: Dogfooding

Design: [ADR-0029](adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)
(dogfooding strategy: migration-driven evidence over synthetic spikes,
favoring idiomatic Compono over mechanical translation, a gap decision
rubric feeding a five-way classification — bug / roadmap candidate /
acceptable Compono-native alternative / intentional design difference /
migration-only friction — and two required living deliverables: a
migration guide and an evidence-backed roadmap). The selected real-world
project is `ncipollina/cosmere-tracker`'s AutoFixture-based test kit
(`test/Cosmere.Tracker.TestKit`), which already exercises three candidate
capability gaps against Compono's current design: `Freeze<T>()`-style
hidden shared values (`HttpClientSpecimenBuilder`'s frozen
`HttpMessageHandler`), NSubstitute `ConfigureMembers`
(`AutoNSubstituteCustomization { ConfigureMembers = true }` in
`BaseFixtureFactory`), and AutoFixture's `OmitOnRecursionBehavior` versus
Compono's fail-fast recursion detection. Per `docs/manifesto.md`, none of
these three is assumed into the roadmap merely because AutoFixture has
them — each is classified by ADR-0029's rubric from real migration
evidence, and friction counts as evidence even where a working (if
technically different) Compono alternative already exists.
`Compono.Bogus` adoption is the one deliberate exception to
migration-driven scoping — `cosmere-tracker`'s AutoFixture kit has no
semantic-data concept at all, but ADR-0029 mandates the migration adopt
`Compono.Bogus` anyway, since Milestone 6's package otherwise has no
real-project validation beyond its own sample project.
Tracked by [PLAN-0007](plans/0007-milestone-7-dogfooding.md).

### Scope

- Select one existing real-world project — `ncipollina/cosmere-tracker`
- Rewrite its tests using Compono, idiomatically rather than as a
  mechanical AutoFixture translation, adopting the full package set
  (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus`) —
  `Compono.Bogus` is mandatory even though the source project has no
  equivalent to migrate from
- Record missing capabilities and positive findings — the three candidate
  gaps above, plus any further finding the migration surfaces
- Measure performance and broader maintainability (concepts introduced/
  removed, setup visible per test, contributor-facing complexity)
- Measure API friction, including friction where a technically different
  Compono alternative already works
- Refine diagnostics
- Remove unnecessary abstractions (documented, not assumed)
- Classify every finding (bug / roadmap candidate / acceptable alternative
  / intentional design difference / migration-only friction) per ADR-0029's
  rubric, recorded in `docs/research/0001-autofixture-comparison.md` and
  the resulting ADR(s)/Amendment(s)/bug-fix PR(s)
- Produce `docs/migrating-from-autofixture.md` (living, drafted
  before migration starts, updated with every migration PR) and
  `docs/roadmap/post-mvp.md` (evidence-backed, roadmap-candidate findings
  only)
- Answer ADR-0029's final architectural conclusion questions in Phase 4
- Per [ADR-0029 Amendment 4](adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-4-2026-08-03-documentation-architecture-becomes-a-required-milestone-7-deliverable)/
  [ADR-0030](adr/0030-compono-documentation-architecture.md): design
  Compono's complete public-documentation architecture (developer-journey
  hierarchy, section purposes/audiences/ordering, how forward-looking
  content stays separate from the primary learning path) while the
  dogfooding evidence is fresh, recorded in `docs/documentation-architecture.md`;
  produce the initial documentation skeleton matching it; decide the
  migration guide's promotion into that hierarchy; and hand Milestone 8 a
  scoped documentation work-item backlog instead of a blank design problem

### Success Measures

- Tests are at least as readable as before
- The composition model remains understandable
- Most setup belongs in profiles rather than custom attributes
- Failures are reproducible
- Performance does not regress unacceptably
- Every discovered finding has a recorded classification — not left open or
  assumed into the roadmap by default
- The research findings are a balanced assessment, recording where Compono
  improved the suite as well as where it introduced friction
- `docs/migrating-from-autofixture.md` is substantially complete
  by the end of Phase 4, needing only editorial cleanup
- `docs/roadmap/post-mvp.md` exists and every entry traces to real migration
  evidence
- Phase 4's final architectural conclusion answers whether Compono is
  suitable as the default AutoFixture replacement for `cosmere-tracker`
- `docs/documentation-architecture.md` exists, covers every section named in
  ADR-0030, and Milestone 8 has a scoped backlog to execute against rather
  than its own design phase

### Outcome

Complete. All six [PLAN-0007](plans/0007-milestone-7-dogfooding.md)
phases (0, 1, 2, 3, 4, 5) done; full evidence in
[docs/research/0001-autofixture-comparison.md](research/0001-autofixture-comparison.md),
before/after detail in
[docs/migrating-from-autofixture.md](migrating-from-autofixture.md),
and the roadmap outcome in
[docs/roadmap/post-mvp.md](roadmap/post-mvp.md). Every success measure
above was checked against real migration evidence, not judgment calls —
most were met; "failures are reproducible" was not directly exercised
(zero real construction-cycle failures occurred to reproduce) and is
recorded as such rather than claimed met — see the research document's
"Final architectural conclusion and recommendation" section for the full
per-measure accounting.

Ten findings surfaced, every one classified per ADR-0029's five-way
taxonomy: zero bug, zero roadmap candidate, four acceptable
Compono-native alternative, four intentional design difference (recorded
as dated Amendments to [ADR-0002](adr/0002-constructor-selection-algorithm.md),
[ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md),
[ADR-0022](adr/0022-compono-xunit-package-design.md), and
[ADR-0025](adr/0025-compono-nsubstitute-package-design.md)), two
migration-only friction. `docs/roadmap/post-mvp.md` correctly lists zero
entries — no finding rose to the roadmap-candidate bar.

**Final architectural conclusion:** the migration strengthened confidence
in explicit-over-implicit (the one finding that tested it under real cost
pressure still confirmed the posture), profiles remained sufficient as
the primary configuration mechanism, the public provider model was
sufficient for everything this project needed (though not stress-tested
as its own design surface — zero custom providers were authored), and no
manifesto/design-principle language or MVP success criterion needed
revision.

**Recommendation:** Compono is the default for all `cosmere-tracker` test
code, effective immediately — every AutoFixture package reference is
already removed from that project's `test/` tree, so there is no
remaining migration to sequence incrementally and no roadmap-candidate
finding to wait on.

## Milestone 8: Public Preview

Per [ADR-0029 Amendment 4](adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-4-2026-08-03-documentation-architecture-becomes-a-required-milestone-7-deliverable)/
[ADR-0030](adr/0030-compono-documentation-architecture.md) (including
Amendment 2), this milestone executes against Milestone 7's documentation
architecture (`docs/documentation-architecture.md`) and its Phase 5
work-item backlog, rather than designing the documentation from scratch —
the hierarchy, section purposes/audiences/ordering, and migration-guide
placement are already decided; this milestone writes, refines, polishes,
reviews, and publishes the content. A dedicated deep-design pass beyond
Phase 5's backlog settled the remaining cross-cutting decisions the
backlog itself couldn't answer just by listing pages: release/versioning
policy ([ADR-0031](adr/0031-public-preview-release-and-versioning-policy.md)),
the API reference toolchain ([ADR-0032](adr/0032-api-reference-documentation-toolchain.md)),
the samples strategy ([ADR-0033](adr/0033-public-preview-samples-strategy.md)),
and `docs/documentation-architecture.md`'s remaining Open Items (Cookbook
navigation deferral, `public-api.md`/`manifesto.md` retirement, benchmark-
claims policy, contributor-governance scope — all recorded in
[ADR-0030 Amendment 2](adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items)).
Tracked by [PLAN-0008](plans/0008-milestone-8-public-preview.md), phased
0-9; see that plan for the full package-readiness checklist,
release-readiness checklist, public-preview acceptance checklist, and
exit criteria.

### Scope

- Publish `0.x` packages
- Review and update the repository-root `README.md` — a distinct artifact
  from `docs/getting-started/*` (the GitHub landing page vs. the docs
  site's onboarding section); not dropped by the documentation-architecture
  rewrite of this scope, just tracked alongside it
- Write every page in `docs/documentation-architecture.md`'s tree, per
  Phase 5's scoped backlog — Getting Started, Concepts, How-to Guides,
  Cookbook, Samples, Package Guides, Best Practices, Architecture,
  Troubleshooting, Reference, Roadmap
- Refine/polish `docs/migrating-from-autofixture.md` (promoted to its
  top-level path in Phase 5, `mkdocs.yml` nav entry already added) from
  "substantially complete" to publication-ready
- Resolve `docs/documentation-architecture.md`'s Open Items (API reference
  generation toolchain, Cookbook navigation/tagging at scale, where Sample
  applications physically live, versioning policy, contribution guidance,
  issue templates, `docs/public-api.md`/`docs/manifesto.md`'s eventual
  disposition)
- Benchmark results
- Explicit known limitations
- Update `mkdocs.yml`'s nav to match the published hierarchy exactly, and
  publish the site

## MVP Non-goals

- AutoFixture API compatibility
- AutoFixture migration tooling
- NUnit or MSTest support
- Moq or FakeItEasy support
- Native AOT certification
- Full reflection fallback
- Open generic registrations
- Source-generated test methods beyond what xUnit requires
- Analyzers beyond generator diagnostics
- Property-based testing
- Snapshot testing
- Database seeding
- Every collection type
- Every Bogus dataset
- Global mutable configuration
- Runtime plugin discovery
- Stable 1.0 API

## Open Decisions Before Implementation

- ~~Runtime reflection policy~~ — default direction resolved by
  [ADR-0001](adr/0001-source-generation-first.md); the exact opt-in
  mechanism for a future compatibility mode is still open.
- ~~Exact public root type name~~ — `Composer`, settled by Milestone 3 Phase 0's
  shipped `Composer.Create()`/`Composer.Create(Action<CompositionBuilder>)`.
- ~~Attribute names~~ — resolved by
  [ADR-0022](adr/0022-compono-xunit-package-design.md): `[Compose]`,
  `[Compose<TProfile>]`, `[Shared]`.
- ~~Shared-value matching rules~~ — type-based only for Milestone 2,
  resolved by [ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md);
  confirmed type-based only for Milestone 4's `[Shared]` too, by
  [ADR-0022](adr/0022-compono-xunit-package-design.md) — name/qualifier-based
  sharing remains deferred past Milestone 4, with no consumer yet.
- ~~Sync or async provider APIs~~ — resolved by
  [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md).
- ~~Constructor selection algorithm~~ — resolved by
  [ADR-0002](adr/0002-constructor-selection-algorithm.md).
- Required-member population rules
- Nullability generation defaults
- ~~Generator package distribution~~ — resolved by
  [ADR-0003](adr/0003-generator-package-distribution.md).
- ~~Deterministic output compatibility guarantees~~ — resolved by
  [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md).

## Suggested Initial GitHub Epics

1. Product design and ADRs
2. Source generator foundation
3. Core context and provider pipeline
4. Deterministic value generation
5. Object graph composition
6. Profiles and configuration
7. xUnit v3 integration
8. NSubstitute integration
9. Bogus integration
10. Diagnostics
11. Benchmarks
12. Dogfooding and public preview
