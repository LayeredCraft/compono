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

## Profiles

Profiles should make project-wide conventions reusable:

```csharp
public sealed class ApplicationTestProfile : CompositionProfile
{
    protected override void Configure(
        CompositionBuilder builder)
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

Conflicting rules should produce deterministic precedence or a configuration diagnostic.

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

Exact registrations:

```csharp
builder.Register<IClock>(
    _ => new FakeClock());
```

Type registrations:

```csharp
builder.Register(typeof(IClock), context =>
    new FakeClock());
```

Open generic registrations may be added later:

```csharp
builder.RegisterOpenGeneric(
    typeof(IRepository<>),
    typeof(FakeRepository<>));
```

Open generic registration is not required for the MVP.

## Type and Member Rules

Explicit domain configuration should be possible without creating custom providers:

```csharp
builder.For<Customer>()
    .Member(x => x.Status)
    .Use(CustomerStatus.Active);
```

Generated semantic data:

```csharp
builder.For<Customer>()
    .Member(x => x.Email)
    .Use(context => context.Semantic.Email());
```

The precise fluent API should be designed after representative examples are collected.

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
- Composition: the process of satisfying a request
- CompositionContext: active runtime state
- CompositionRequest: one requested value
- CompositionPlan: generated construction logic
- CompositionScope: shared-instance lifetime
- CompositionProfile: reusable configuration
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
