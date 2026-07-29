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

A likely alternative is a builder:

```csharp
var composer = Compono.Create(builder => builder
    .WithSeed(4219)
    .WithCollectionSize(3));
```

The exact root type name remains open.

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

## xUnit v3 Experience

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

Profile selection:

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void Creates_customer(Customer customer)
{
}
```

Inline values should override generated values:

```csharp
[Theory]
[InlineComposeData("alice@example.com")]
public void Accepts_email(
    string email,
    Customer customer)
{
}
```

The final attribute names are not settled.

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

Questions still to resolve:

- Is sharing type-based only?
- Can sharing be keyed by name or qualifier?
- Does parameter order matter?
- Can a shared value be declared without exposing it as a test parameter?

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

The exact attribute capability depends on xUnit v3 extensibility.

A failed test should surface the seed in its diagnostic output.

## Diagnostics API

A standard composition exception should expose structured diagnostics:

```csharp
catch (CompositionException exception)
{
    Console.WriteLine(exception.Diagnostic);
}
```

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
