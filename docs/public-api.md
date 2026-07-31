# Compono Public API Design

## Purpose

This document describes the intended developer experience.

It is not a final API specification. Examples are design targets used to evaluate whether the underlying architecture remains approachable.

## API Goals

The public API should be:

- Easy to discover
- Small enough to learn
- Explicit about configuration
- Consistent between programmatic and test-framework usage
- Friendly to source generation
- Deterministic
- Free of mutable global state

## Programmatic Composition

Basic creation should be simple:

```csharp
var composer = Composer.Create();

var customer = composer.Create<Customer>();
var customers = composer.CreateMany<Customer>(3);
```

Configuration uses the same root type via a builder callback (shipped, Milestone 3
Phase 0 — [ADR-0017](adr/0017-immutable-composer-configuration-and-builder-model.md)).
`WithSeed`, `Register<T>`, `UseServiceProvider`, `AddProfile`, `WithCollectionSize`,
and the `.For<T>()` rule DSL below are all shipped (Phase 0/1/2/3 —
[ADR-0019](adr/0019-registrations-and-service-provider-injection.md),
[ADR-0018](adr/0018-composition-profiles.md),
[ADR-0020](adr/0020-composition-configuration-rules.md)):

```csharp
var composer = Composer.Create(builder => builder
    .WithSeed(4219)
    .Register<IClock>(_ => new FakeClock())
    .UseServiceProvider(app.Services)
    .AddProfile<CustomerProfile>());
```

A registration factory can call `ICompositionContext.Resolve<T>()` (no
descriptor) to compose its own nested dependencies manually, distinct from the
descriptor-based overload generated code uses — see
[ADR-0019](adr/0019-registrations-and-service-provider-injection.md).

`Composer` is the settled root type name — `Composer.Create()` (no configuration) and
`Composer.Create(builder => ...)` (explicit configuration) are the same method,
the latter with an empty callback for the former.

## Configuration

Configuration should read as a description of composition behavior:

```csharp
var composer = Composer.Create(builder => builder
    .WithSeed(4219)
    .WithCollectionSize(3)
    .Register<IClock>(_ => new FakeClock())
    .AddProfile<CustomerProfile>());
```

Integrations should add themselves through extension methods:

```csharp
var composer = Composer.Create(builder => builder
    .UseNSubstitute()
    .UseBogus());
```

The core package must not know those methods exist.

Service injection uses the BCL's own `System.IServiceProvider` — no core dependency
on `Microsoft.Extensions.DependencyInjection` or any container package:

```csharp
var composer = Composer.Create(builder => builder
    .UseServiceProvider(app.Services));
```

An exact `Register<T>(...)` always wins over the configured `IServiceProvider`; a
container miss (`null`) falls through to profile/type/member rules. See
[ADR-0019](adr/0019-registrations-and-service-provider-injection.md) for full
fallback semantics.

## Profiles

Profiles should make project-wide conventions reusable. A profile implements
`ICompositionProfile` — an interface, not a base class, per
[ADR-0018](adr/0018-composition-profiles.md):

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder
            .UseNSubstitute()
            .UseBogus(options => options.Locale = "en_US")
            .Register<IClock>(_ =>
                new FakeClock(
                    new DateTimeOffset(
                        2026, 1, 1, 0, 0, 0,
                        TimeSpan.Zero)));
    }
}
```

Profile composition should be supported:

```csharp
builder
    .AddProfile<DomainProfile>()
    .AddProfile<InfrastructureProfile>();
```

Profiles apply eagerly, in call order — that order *is* the precedence rule. A
conflicting registration or rule (from any combination of direct calls and
profiles) is a build-time `CompositionConfigurationException` naming every
conflicting source, not a silent override; a profile that (transitively) adds
itself is a build-time cycle diagnostic, not a stack overflow.

## Row Composition (Test-Framework Integrations)

Resolved by [ADR-0021](adr/0021-row-composition-entry-point-for-test-framework-integrations.md)
(`Accepted`, implemented — Milestone 4 Phase 0). A test-framework
integration that needs to compose several *sibling* top-level values in
one shared scope — e.g. one xUnit theory row's own method parameters —
uses `Composer.CreateRow`, not `Create<T>()`/`CreateMany<T>()`, which each
start a brand-new, independent scope per call:

```csharp
var composer = Composer.Create();
var row = composer.CreateRow(typeof(OrderServiceTests));

var repository = row.ResolveShared<IRepository>(repositoryDescriptor);
var service = row.Resolve<OrderService>(serviceDescriptor);
```

`CompositionRow` is the only public surface a test-framework integration
uses to reach the engine this way — `Compono` core's own
`CompositionContext` stays `internal`. It implements
`ICompositionContext`, so a composed value's own nested requests (a
generated plan's constructor parameters) are unaffected — generated code
always programs against `ICompositionContext`, never `CompositionRow`
directly.

- `Resolve<TValue>(descriptor)`/`ResolveCollectionSize()` — ordinary
  composition, forwarded straight to the wrapped context; no different
  from `Create<T>()`'s own resolution.
- `Resolve<TValue>()` (the descriptor-less overload `CompositionRow` only
  carries to satisfy `ICompositionContext`'s full interface shape) is
  **not** a usable direct row-composition entry point — it forwards to
  the manual-resolve seam meant for a registration/configuration-rule
  factory's own `context.Resolve<T>()` calls, which throws
  `InvalidOperationException` unless such a factory is actively being
  invoked. A `CompositionRow`-holding caller can never satisfy that
  condition (factories are always invoked with the raw internal context,
  never a `CompositionRow`), so calling this overload directly on a row
  always throws.
- `ResolveShared<TValue>(descriptor)` — composes `TValue` and additionally
  stores the result into this row's shared scope: a later request for the
  same type in this row — including one made by a nested generated plan,
  e.g. a SUT's own constructor parameter — transparently reuses it instead
  of composing an independent value. This is the mechanism `[Shared]`
  parameters (see xUnit v3 Experience, below) are built on.
- `ShareExplicit<TValue>(descriptor, value)` — stores an already-known
  value (an inline theory argument) directly into the row's shared scope,
  with no pipeline dispatch or random fork consumed.
- `Seed` — this row's deterministic root seed, an `int` matching
  `WithSeed(int)`'s own contract exactly, so a seed read here is always
  pasteable back into `WithSeed(...)`/`[Compose(Seed = ...)]` to reproduce
  the same row.
- Only one shared value per type is allowed per row — a second
  `ResolveShared`/`ShareExplicit` call for a type already shared in this
  row throws a `CompositionException` naming the type, rather than
  silently overwriting or reusing the first value.

## xUnit v3 Experience

Resolved by [ADR-0021](adr/0021-row-composition-entry-point-for-test-framework-integrations.md)/
[ADR-0022](adr/0022-compono-xunit-package-design.md) (`Accepted`, not yet
implemented — see [PLAN-0004](plans/0004-milestone-4-xunit-integration.md)).
`[Compose]`/`[Compose<TProfile>]` implement `Xunit.v3.DataAttribute`
directly; composition happens once per theory row, at execution time (not
discovery time — composed values, especially a future substitute or any
other non-serializable reference type, aren't safely enumerable before a
test actually runs).

A composed theory should be concise:

```csharp
[Theory]
[Compose]
public async Task Saves_order(
    [Shared] IOrderRepository repository,
    CreateOrderHandler handler,
    CreateOrder command)
{
    await handler.Handle(command);

    await repository.Received(1)
        .SaveAsync(
            Arg.Any<Order>(),
            Arg.Any<CancellationToken>());
}
```

Profile selection (method-level only for Milestone 4 — a profile that
itself needs to combine several others already can, via its own
`Configure` calling `AddProfile` again, per the existing Profiles section
above):

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void Creates_customer(Customer customer)
{
}
```

Inline values take precedence over composed ones — supplied directly on
`[Compose(...)]`'s own constructor, strictly positional from the first
parameter, rather than a separate attribute:

```csharp
[Theory]
[Compose("alice@example.com")]
public void Accepts_email(
    string email,
    Customer customer)
{
}
```

`email` is not composed; `customer` is. Combining `[Compose]` with an
ordinary `[InlineData]`/`[MemberData]` attribute on the same method is not
supported — xUnit treats each data attribute as an independent row
source rather than merging their values, so inline values belong on
`[Compose(...)]` itself.

## Shared Values

A shared parameter should be reused for compatible requests later in the same test composition:

```csharp
[Theory]
[Compose]
public void Uses_same_repository(
    [Shared] IRepository repository,
    OrderService service)
{
}
```

The repository injected into `OrderService` must be the same instance as the parameter.

The word `Shared` is currently preferred over `Frozen` because it describes lifetime semantics more directly.

Resolved by ADR-0022:

- Sharing is type-based only, matching Milestone 2's own scope semantics —
  no name/qualifier-based sharing yet, no concrete consumer for it.
- Declaration order does not gate which parameters are eligible to be
  `[Shared]` — every `[Shared]` parameter composes (or, if inline-supplied,
  is stored) before every non-shared parameter, regardless of where it
  sits in the parameter list. Order *does* matter among `[Shared]`
  parameters themselves: one may depend on an earlier-declared `[Shared]`
  sibling, never a later one.
- Two `[Shared]` parameters of the same type is a clear, pre-composition
  failure naming both parameters, not a silent last-wins.
- A shared value cannot be declared without exposing it as a test
  parameter in Milestone 4 — every shared value is visible in the
  method's own signature; a hidden/injected-only shared value isn't
  designed here.

## Registrations

Exact registrations, resolved by
[ADR-0019](adr/0019-registrations-and-service-provider-injection.md) — a factory
receives the same public `ICompositionContext` generated code uses, via a new
descriptor-less `Resolve<T>()` overload, plus a no-dependency convenience form:

```csharp
builder.Register<IClock>(
    _ => new FakeClock());

builder.Register<IClock>(
    () => new FakeClock());
```

Registering the same type twice — direct call, profile, or any combination — is a
build-time `CompositionConfigurationException`, not a last-wins override.

Open generic registrations may be added later:

```csharp
builder.RegisterOpenGeneric(
    typeof(IRepository<>),
    typeof(FakeRepository<>));
```

Open generic registration is not required for the MVP.

## Type and Member Rules

Explicit domain configuration should be possible without creating custom providers.
Resolved by [ADR-0020](adr/0020-composition-configuration-rules.md): a **member**
rule scopes to one member of one declaring type; a **type** rule (no `.Member(...)`
call) matches any request for exactly that type, and yields to a member rule when
both could apply to the same request.

```csharp
// Member rule
builder.For<Customer>()
    .Member(x => x.Status)
    .Use(CustomerStatus.Active);

// Type rule
builder.For<IClock>()
    .Use(_ => new SystemClock());
```

Generated semantic data:

```csharp
builder.For<Customer>()
    .Member(x => x.Email)
    .Use(context => context.Semantic.Email());
```

Collection size is configured the same way but is **not** a type/member rule
internally — it's queried configuration policy stage 7's collection machinery
reads directly, not a value a provider produces (ADR-0020):

```csharp
builder.WithCollectionSize(3);                     // global default
builder.For<Customer>()
    .Member(x => x.PastOrders)
    .WithCollectionSize(5);                          // member-scoped override
```

Type/member matching is exact (no assignability) for the MVP; two rules claiming the
identical key is a build-time conflict, the same as a duplicate registration.

## Bogus Integration

Basic activation:

```csharp
builder.UseBogus();
```

Locale:

```csharp
builder.UseBogus(options =>
{
    options.Locale = "en_US";
});
```

Explicit Bogus rules:

```csharp
builder.For<Customer>()
    .Member(x => x.FirstName)
    .UseBogus(faker => faker.Name.FirstName());
```

Correlated rules:

```csharp
builder.For<Customer>()
    .Member(x => x.Email)
    .DependsOn(x => x.FirstName, x => x.LastName)
    .UseBogus((faker, firstName, lastName) =>
        faker.Internet.Email(firstName, lastName));
```

Correlation syntax is a design goal, not an MVP commitment.

## NSubstitute Integration

Activation:

```csharp
builder.UseNSubstitute();
```

Default behavior:

- Compose interfaces as substitutes
- Optionally compose abstract classes
- Reuse substitutes through shared scope
- Avoid automatic recursive member configuration in the MVP

NSubstitute-specific configuration belongs in the integration package:

```csharp
builder.UseNSubstitute(options =>
{
    options.SubstituteAbstractClasses = false;
});
```

## Deterministic Reproduction

Explicit seed:

```csharp
var composer = Composer.Create(builder =>
    builder.WithSeed(8492173));
```

xUnit:

```csharp
[Theory]
[Compose(Seed = 8492173)]
public void Reproduces_failure(Order order)
{
}
```

Confirmed viable against xUnit v3's real extensibility surface (ADR-0022).

A composition failure's message ends with `Seed: {value}`, matching
`docs/architecture.md`'s existing Diagnostics example — a successful row
does not surface its seed anywhere by default, to keep passing-test output
unchanged.

## Diagnostics API

A standard composition exception should expose structured diagnostics:

```csharp
catch (CompositionException exception)
{
    Console.WriteLine(exception.Diagnostic);
}
```

`exception.Diagnostic` (when present) already renders its own `Seed: {value}`
line via `ToString()`, but not every `CompositionException` has a
`Diagnostic` - a plain-message one (e.g. a generated `HashSet<T>`/
`Dictionary` collection plan's unique-value-exhaustion failure) does not.
`CompositionException.WithSeedInMessage(original, seed)` is a static
factory for exactly this case - it returns a copy of `original` whose
`Message` has the seed appended directly, regardless of whether
`Diagnostic` is present:

```csharp
try
{
    composer.Create<Order>();
}
catch (CompositionException exception)
{
    throw CompositionException.WithSeedInMessage(exception, mySeed);
}
```

The returned exception's `Diagnostic` is copied through from `original`
unchanged (`null` stays `null`), and `original` itself becomes its
`InnerException` - never discarded. `Compono.XunitV3`'s own `[Compose]`
binding algorithm (ADR-0022) uses this to guarantee every composition
failure's `Message` carries a pasteable seed, not only ones that happen to
have a `Diagnostic`.

Potential debugging API:

```csharp
var explanation = composer.Explain<OrderService>();
```

This is a post-MVP possibility.

## Naming Vocabulary

Preferred concepts:

- Composer: long-lived immutable configuration and public entry point
- CompositionBuilder: mutable configuration accumulator, live only during
  `Composer.Create(builder => ...)`
- Composition: the process of satisfying a request
- CompositionContext: active runtime state
- CompositionRequest: one requested value
- CompositionPlan: generated construction logic
- CompositionScope: shared-instance lifetime
- ICompositionProfile: reusable configuration, applied by name
- CompositionProvider: extension point
- Shared: reuse within a scope

## API Design Rules

- Avoid `object`-based public pipelines where practical
- Avoid service-locator-style APIs in test bodies
- Avoid mutable global configuration
- Avoid exposing source-generator implementation details
- Prefer explicit extension methods for integrations
- Prefer immutable configuration after composer creation
- Prefer one obvious way to perform common operations
- Do not reproduce AutoFixture terminology solely for familiarity
