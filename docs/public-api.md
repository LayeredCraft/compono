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
[ADR-0022](adr/0022-compono-xunit-package-design.md) (`Accepted`, implemented —
see [PLAN-0004](plans/0004-milestone-4-xunit-integration.md)). The one gap that
remained open past Phase 4 — an interface/abstract/delegate-typed
`[Compose]`-attributed parameter reported CMP0003 unconditionally, even when a
profile registration or runtime provider would satisfy it — is resolved by
[PLAN-0005](plans/0005-milestone-5-nsubstitute-integration.md) Phase 2, see
[ADR-0024's Amendment 2](adr/0024-public-provider-extensibility-model.md).
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

Generated semantic data — via `Compono.Bogus`'s own `UseBogus(...)` sugar over
`.Use(context => ...)` (there is no `context.Semantic` accessor; see Bogus
Integration, below, and [ADR-0027](adr/0027-compono-bogus-package-design.md)):

```csharp
builder.For<Customer>()
    .Member(x => x.Email)
    .UseBogus(faker => faker.Internet.Email());
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

Design: [ADR-0027](adr/0027-compono-bogus-package-design.md) — **implemented,
PLAN-0006 Phase 1** (build-verified; test coverage/end-to-end verification
pending Phase 3), [ADR-0028](adr/0028-configurable-bogus-member-name-conventions.md)
— **implemented, PLAN-0006 Phase 2** (build-verified; test coverage pending
Phase 3), built on [ADR-0026](adr/0026-deterministic-seed-derivation-for-providers.md)'s
`ICompositionContext.DeriveSeed()` (**implemented, PLAN-0006 Phase 0** — see
Deterministic Reproduction, below).
`Compono.Bogus` has three independent customization models, not one, and a real
profile typically uses more than one at once:

- **`UseBogus()`** — project-wide conventions: most `FirstName`/`Email`/etc.
  members across the graph should just look realistic, with zero per-type setup.
- **`.Member(...).UseBogus(faker => ...)`** — a handful of members need
  something the convention allowlist doesn't (or shouldn't) guess.
- **`UseBogus<T>()`** — a type's values are meaningfully correlated with each
  other (an email derived from a name), so the whole object is more naturally
  "Bogus owns this type" than several independent member rules.

**Basic activation** — enables the conservative member-name convention provider
(stage 5) by default:

```csharp
builder.UseBogus();
```

Locale and the convention provider's own on/off switch:

```csharp
builder.UseBogus(options =>
{
    options.Locale = "en_US";
    options.EnableMemberNameConventions = false; // opt out, keep explicit rules only
});
```

**Configurable conventions** — design: [ADR-0028](adr/0028-configurable-bogus-member-name-conventions.md)
— **implemented, PLAN-0006 Phase 2** (build-verified; test coverage pending
Phase 3). A domain that doesn't use the built-in allowlist's exact names, or
that has its own package-wide semantic name, can extend `UseBogus()` without
repeating a member-level rule at every call site:

```csharp
builder.UseBogus(options =>
{
    // An additional exact name that reuses a built-in generator.
    options.AddAlias("GivenName", BogusConvention.FirstName);
    options.AddAlias("Surname", BogusConvention.LastName);
    options.AddAlias("Zip", BogusConvention.PostalCode);

    // A custom exact-name convention, backed by a user callback.
    options.AddConvention("Sku", faker => faker.Commerce.Ean13());
});
```

Both are exact, case-sensitive matches, merged with the built-in allowlist
into a single lookup — a name can only ever map to one generator. `AddAlias`/
`AddConvention` perform eager validation when called: a null name throws
`ArgumentNullException` (matching this repo's own `ArgumentNullException.ThrowIfNull`
guard convention); an empty/whitespace name, or any duplicate or collision
with a built-in name, an existing alias, or an existing custom convention,
throws `ArgumentException` — immediately from the call that introduced it,
not deferred, and not silently overwritten.
Custom conventions are `string`-only (a non-`string` package-wide value needs
the member-level `.Member(...).UseBogus(faker => ...)` sugar below instead).
Replacing or removing a built-in convention isn't supported.

`EnableMemberNameConventions = false` is still all-or-nothing: it disables
the entire provider, including any aliases/custom conventions configured in
the same call — there's no way yet to keep custom conventions active while
turning off only the built-in guesses.

**This validation is scoped to one `UseBogus(...)` call** — a second, separate
call (e.g. from a different profile) that defines a colliding alias/custom
name is **not** detected; each call registers its own independent provider,
and ordinary pipeline registration-order/first-match-wins semantics decide
which one applies (ADR-0028's Non-Goals). Centralize Bogus configuration into
one `UseBogus(...)` call — typically inside one reusable profile — to avoid
relying on that fallback.

**Explicit member rules** (stage 4, sugar over the existing `.For<T>().Member(...).Use(...)`
mechanism — no `context.Semantic` accessor, no core change beyond `DeriveSeed()`):

```csharp
builder.For<Customer>()
    .Member(x => x.FirstName)
    .UseBogus(faker => faker.Name.FirstName());
```

Always wins over the convention provider for the same member, since stage 4
runs before stage 5 unconditionally.

**Whole-object generation** (purely ergonomic sugar over the existing
`Register<T>` registration mechanism — no hidden pipeline stage, no special
runtime behavior of its own, same duplicate-registration conflict rule as any
other `Register<T>` call):

```csharp
builder.UseBogus<Customer>(faker => faker
    .RuleFor(x => x.FirstName, f => f.Name.FirstName())
    .RuleFor(x => x.LastName, f => f.Name.LastName())
    .RuleFor(x => x.Email, (f, x) => f.Internet.Email(x.FirstName, x.LastName)));
```

`configureFaker` is `Action<Faker<T>>`, not a `Func` — it configures the
instance in place; `RuleFor`'s own fluent chaining is convenient here but not
load-bearing, since nothing needs to be returned back to Compono.

Correlated values (`Email` derived from `FirstName`/`LastName` above) are
satisfied entirely by Bogus's own `Faker<T>.RuleFor((faker, instance) => ...)` —
there is no separate Compono-native `.DependsOn(...)` member-dependency
mechanism; it was evaluated during Milestone 6's design review and explicitly
deferred (ADR-0027) in favor of `Faker<T>`, which already solves this problem
natively.

`UseBogus<T>()` is intentionally independent of `UseBogus()` — it never
requires `UseBogus()` to have been called, and never reads its
`BogusOptions.Locale`. `UseBogus<T>()` is purely ergonomic sugar over the
existing `Register<T>` registration mechanism (stage 3, an ordinary exact
registration); `UseBogus()` activates the stage-5 semantic provider. They're
different pipeline stages solving different problems — a consumer can call
`UseBogus<Customer>(...)` alone, with `UseBogus()` never called at all, and it
works exactly the same. Pass the locale explicitly (named argument recommended
for readability) if it should match `UseBogus()`'s own:

```csharp
builder
    .UseBogus(options => options.Locale = "fr")
    .UseBogus<Customer>(
        locale: "fr",
        configureFaker: faker => faker.RuleFor(x => x.FirstName, f => f.Name.FirstName()));
```

`locale` is a plain `string`, not an options type — deliberate: it's the only
per-registration setting `Faker<T>`'s own constructor takes today, and adding a
one-property options type would be speculative surface for a second option
nothing currently needs (ADR-0027).

Coexistence with `Compono.NSubstitute`: both packages can be activated in the
same profile, in either order, with no reference between them. Bogus's
convention provider only ever claims `string`-typed members; NSubstitute's
provider only ever claims interface/delegate/(optionally) abstract-class
requests — disjoint by construction, per ADR-0027's Coexistence section.

## Provider Extensibility

Implemented, per [ADR-0024](adr/0024-public-provider-extensibility-model.md) —
PLAN-0005 Phase 0, a Milestone 5 deliverable (see
[PLAN-0005](plans/0005-milestone-5-nsubstitute-integration.md) for phase status).
An integration package (or a consumer's own code) contributes open-ended,
pattern-matching composition logic to pipeline stage 5 (semantic value providers)
or stage 6 (test-double providers) — the cases a closed-set `.For<T>()` rule can't
express, like "any interface type":

```csharp
public interface ICompositionValueProvider
{
    CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context);
}

builder.AddSemanticProvider(new MySemanticProvider());
builder.AddTestDoubleProvider(new MyTestDoubleProvider());
```

`CompositionProviderRequest` exposes `RequestedType`/`DeclaringType`/`Name`/
`Nullability` — enough to match "any interface" (`RequestedType.IsInterface`) or
"any member literally named `Email`" (`Name == "Email"`) without exposing the
engine's own internal request/path types. A provider returns
`CompositionProviderResult.NotHandled` for anything it doesn't apply to (so a later
provider or pipeline stage still gets a chance) or `CompositionProviderResult.Handled(value)`.
`Compono.NSubstitute`'s `UseNSubstitute()` (below) is the first real consumer of
this contract; `Compono.Bogus`'s `UseBogus()` (below) is the second, registered
via `AddSemanticProvider` instead of `AddTestDoubleProvider`.

## NSubstitute Integration

Implemented and test-covered/end-to-end verified (PLAN-0005 Phase 2) per
[ADR-0025](adr/0025-compono-nsubstitute-package-design.md), built on the Provider
Extensibility contract above — `Compono.NSubstitute`'s `NSubstituteProvider`/
`NSubstituteOptions`/`UseNSubstitute()` are real, tested code, verified end-to-end
against a real packaged xUnit v3 consumer (`Compono.XunitV3.SampleTests`) running
this milestone's own Goal scenario: a `[Shared]` interface theory parameter
composed as a real substitute and reused by a nested constructor parameter of the
same type.

Activation:

```csharp
builder.UseNSubstitute();
```

Default behavior:

- Compose interfaces as substitutes
- Compose delegate types as substitutes
- Optionally compose abstract classes (on by default; `SubstituteAbstractClasses`)
- Reuse substitutes through shared scope (falls out of the engine's existing
  `[Shared]`/scope mechanism — `Compono.NSubstitute` contributes no code toward
  this specifically)
- Avoid automatic recursive member configuration in the MVP — a composed
  substitute is exactly what `Substitute.For<T>()` would produce; its `Returns`/
  `Received` configuration stays the consumer's own test-body concern

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

A provider or registration/configuration-rule factory that needs its own
deterministic randomness (`Compono.Bogus`'s `BogusMemberNameProvider`, or the
`UseBogus(...)`/`UseBogus<T>(...)` sugar) calls `context.DeriveSeed()` — an
`int` derived from the composer's root seed and the request currently being
resolved, reusing the same path-hash [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)
already uses internally, without exposing `IRandomSource` or path internals.
Resolved by [ADR-0026](adr/0026-deterministic-seed-derivation-for-providers.md) —
**implemented, PLAN-0006 Phase 0**.

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
- CompositionProvider: extension point (internal, engine-owned)
- ICompositionValueProvider: the public extension point stages 5/6 expose to
  integration packages — see Provider Extensibility, resolved by
  [ADR-0024](adr/0024-public-provider-extensibility-model.md)
- Shared: reuse within a scope
- `DeriveSeed()`: on-demand, path-derived deterministic seed a provider or
  factory calls for its own randomness — see Deterministic Reproduction,
  resolved by [ADR-0026](adr/0026-deterministic-seed-derivation-for-providers.md)
- `BogusConvention`: the fixed, closed set of Compono.Bogus's built-in
  member-name conventions — see Bogus Integration, resolved by
  [ADR-0028](adr/0028-configurable-bogus-member-name-conventions.md)

## API Design Rules

- Avoid `object`-based public pipelines where practical
- Avoid service-locator-style APIs in test bodies
- Avoid mutable global configuration
- Avoid exposing source-generator implementation details
- Prefer explicit extension methods for integrations
- Prefer immutable configuration after composer creation
- Prefer one obvious way to perform common operations
- Do not reproduce AutoFixture terminology solely for familiarity
