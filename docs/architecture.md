# Compono Architecture

## Overview

Compono is a modular test composition framework.

The architecture is centered on a `CompositionContext`.

The context represents one active composition operation and coordinates:

- The deterministic seed
- Random streams
- Scope and shared instances
- Registrations
- Profiles
- Providers
- Generated composition plans
- The current request path
- Diagnostics
- Cancellation and runtime state

The context is the internal center of the system even when the public API exposes simpler concepts such as `Composer`, `Create<T>()`, or a test-framework attribute.

## Architectural Shape

```text
Consumer API
    |
    v
Composer / Test Framework Integration
    |
    v
CompositionContext
    |
    +--> Explicit Values
    +--> Shared Scope
    +--> Registrations
    +--> Profile Rules
    +--> Semantic Value Providers
    +--> Test Double Providers
    +--> Built-in Value Providers
    +--> Generated Composition Plans
    |
    v
Composed Result or Diagnostic Failure
```

## Composition Context

A composition context should contain all state required to resolve a graph without relying on mutable global configuration.

Conceptually:

```csharp
public interface ICompositionContext
{
    CompositionSeed Seed { get; }

    ICompositionScope Scope { get; }

    CompositionPath Path { get; }

    IRandomSource Random { get; }

    ValueTask<T> ResolveAsync<T>(
        CompositionRequest request,
        CancellationToken cancellationToken = default);
}
```

The exact public surface is not final. The important decision is that active composition state belongs to the context.

### Context lifetime

A new root context should normally be created for:

- A call to `Create<T>()`
- A call to `CreateMany<T>()`
- One xUnit theory row
- One explicit composition scope

Nested requests should derive child contexts or child paths without losing the root seed, scope, or diagnostics.

## Composition Requests

Every value should be resolved from a rich request rather than only a `Type`.

A request may contain:

- Requested CLR type
- Parameter name
- Member name
- Declaring type
- Nullability metadata
- Custom attributes
- Generic context
- Object graph path
- Requested lifetime
- Semantic hints
- Whether a test double is acceptable

Conceptually:

```csharp
public sealed record CompositionRequest(
    Type RequestedType,
    string? Name,
    MemberInfo? Member,
    Type? DeclaringType,
    NullabilityInfo? Nullability,
    CompositionPath Path);
```

Generated plans should avoid requiring runtime reflection merely to construct this metadata.

## Resolution Pipeline

The default resolution order is:

1. Explicit values
2. Shared or scoped values
3. Exact registrations
4. Profile rules
5. Semantic value providers
6. Test-double providers
7. Built-in value providers
8. Generated object composition plans
9. Diagnostic failure

This precedence is part of the product contract.

Providers should not silently reorder themselves. Extension packages may add providers only through documented phases or priorities.

## Providers

Providers satisfy composition requests.

A provider should be independently replaceable and should report whether it:

- Did not apply
- Successfully composed a value
- Failed while handling a request

Conceptually:

```csharp
public interface ICompositionProvider
{
    ValueTask<CompositionResult> TryComposeAsync(
        CompositionRequest request,
        ICompositionContext context,
        CancellationToken cancellationToken);
}
```

A result should distinguish between:

```text
NotHandled
Success(value)
Failure(diagnostic)
```

This avoids exception-driven provider selection and preserves meaningful failures.

## Source-Generated Composition Plans

Source generation is the preferred construction strategy.

For a constructible type, the generator should emit a plan that:

- Selects the constructor
- Requests constructor arguments
- Invokes the constructor directly
- Assigns required or configured members
- Preserves nullability and member context
- Produces diagnostic metadata
- Registers the plan with the runtime

Conceptually:

```csharp
internal sealed class CustomerCompositionPlan
    : ICompositionPlan<Customer>
{
    public Customer Compose(ICompositionContext context)
    {
        var firstName = context.Resolve<string>(
            Requests.Customer.FirstName);

        var lastName = context.Resolve<string>(
            Requests.Customer.LastName);

        return new Customer(firstName, lastName);
    }
}
```

The final generated code may use lower-level APIs for performance.

### Generator responsibilities

The generator should identify:

- Accessible constructors
- Primary constructors
- Required members
- Init-only members
- Nullability metadata
- Unsupported types
- Ambiguous construction paths
- Cyclic compile-time dependencies where detectable

### Runtime responsibilities

The runtime should:

- Execute generated plans
- Resolve provider-backed values
- Manage scopes
- Manage deterministic random streams
- Track the composition path
- Produce runtime diagnostics

## Runtime Reflection Policy

The reflection policy is intentionally undecided.

Candidate approaches:

### Generated plans required

Composition fails when no generated plan exists.

Advantages:

- Predictable performance
- Strong trimming and AOT characteristics
- Simple runtime model

Tradeoffs:

- External or dynamically discovered types may require explicit support
- Some test scenarios may be less convenient

### Automatic reflection fallback

The runtime reflects when no generated plan exists.

Advantages:

- High compatibility
- Lower migration friction

Tradeoffs:

- More complex runtime
- Weaker AOT guarantees
- Performance becomes less predictable
- Reflection can hide source-generation gaps

### Opt-in compatibility package or mode

Reflection support is isolated from the default runtime.

Advantages:

- Keeps the core architecture clean
- Allows compatibility where necessary
- Makes performance tradeoffs explicit

This is the current leading compromise, but it is not yet an accepted decision.

## Scopes and Shared Values

A composition scope stores values that should be reused during an active composition.

Examples:

- A repository parameter shared with the system under test
- A fake clock reused throughout an object graph
- A substitute reused by multiple dependencies

Scope semantics must be explicit.

Possible lifetimes:

- Request
- Composition graph
- Test case
- User-created scope

The MVP should begin with one clear shared lifetime rather than a general-purpose dependency injection lifetime system.

## Profiles

Profiles provide reusable configuration without mutable global state.

A profile may:

- Add providers
- Add registrations
- Configure collection sizes
- Configure nullability behavior
- Enable integration packages
- Add type or member rules

Profiles should be immutable after construction or compiled into immutable runtime configuration.

## Deterministic Randomness

The root context owns the seed.

Random sources should be forkable by stable keys:

```text
root seed
└── test parameter: command
    └── Customer
        └── Email
```

This reduces accidental changes when unrelated members are added elsewhere in a graph.

The exact stability guarantee must be documented. Compono should not promise that generated values remain identical across all library versions unless that guarantee can be maintained.

## Diagnostics

Diagnostics should track:

- Root request
- Current request path
- Provider decisions
- Selected plan
- Constructor selection
- Scope reuse
- Registration matches
- Seed
- Failure reason
- Suggested remediation

Example:

```text
Unable to compose CreateOrderHandler.

CreateOrderHandler
└── IOrderProcessor processor
    └── OrderValidator validator
        └── IRuleProvider rules

No registration, semantic provider, test-double provider,
built-in provider, or generated plan could satisfy IRuleProvider.

Seed: 8492173
```

## Package Boundaries

### Compono

Owns:

- Composition context
- Runtime engine
- Requests and results
- Provider contracts
- Scopes
- Profiles
- Registrations
- Deterministic random
- Built-in providers
- Diagnostics
- Generated-plan contracts

### Compono.Generators

Potentially owns:

- Incremental source generator
- Generated plan registration
- Compile-time diagnostics

Whether this ships separately or is bundled as an analyzer dependency of `Compono` remains open.

### Compono.Xunit

Owns:

- xUnit v3 data integration
- Per-row composition contexts
- Inline value precedence
- Parameter attributes
- Seed reporting
- Profile selection

### Compono.NSubstitute

Owns:

- NSubstitute-backed test-double provider
- Interface support
- Optional abstract-class support
- NSubstitute-specific diagnostics

### Compono.Bogus

Owns:

- Bogus-backed semantic providers
- Locale configuration
- Member-name conventions
- Correlated value rules
- Integration with Compono's deterministic seed

## Open Architectural Decisions

- Runtime reflection policy
- Whether generated plans are required for external types
- Sync versus async provider contracts
- Public versus internal use of `Type`
- Exact profile model
- Scope lifetime model
- Constructor selection rules
- Stability guarantees for deterministic output
- Whether source-generation contracts live in `Compono` or `Compono.Generators`
