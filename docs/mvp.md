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
Compono.Xunit
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
configuration policy). [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)'s
Amendment 3 adds the `ManualResolve` path-segment kind these rely on. Public
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

### Exit Criteria

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

### Scope

- Test-double provider contract
- Interface substitutes
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

### Scope

- Semantic value-provider contract
- Shared deterministic seed
- Bogus `Faker` access
- Locale configuration
- Conservative member-name conventions
- Explicit member rules
- Initial correlated-value experiment

### Initial Conventions

Potential mappings:

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

Ambiguous member names such as `Name` should not be guessed aggressively.

### Exit Criteria

A composed customer can receive realistic, deterministic values without the core package referencing Bogus.

## Milestone 7: Dogfooding

### Scope

- Select one existing real-world project
- Rewrite its tests using Compono
- Record missing capabilities
- Measure performance
- Measure API friction
- Refine diagnostics
- Remove unnecessary abstractions

### Success Measures

- Tests are at least as readable as before
- The composition model remains understandable
- Most setup belongs in profiles rather than custom attributes
- Failures are reproducible
- Performance does not regress unacceptably

## Milestone 8: Public Preview

### Scope

- Publish `0.x` packages
- README and getting-started guide
- Architecture documentation
- Samples
- Versioning policy
- Contribution guidance
- Issue templates
- Benchmark results
- Explicit known limitations

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
- Exact public root type name
- Attribute names
- ~~Shared-value matching rules~~ — type-based only for Milestone 2,
  resolved by [ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md);
  name/qualifier-based sharing remains open for Milestone 4.
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
