# Migrating from AutoFixture to Compono

This guide is based on migrating a real, multi-project .NET test suite
from AutoFixture, AutoFixture.Xunit3, and AutoFixture.AutoNSubstitute to
Compono. The before/after examples throughout are drawn from real
patterns encountered during that migration, not synthetic ones — see
[Real-World Migration Evidence](#real-world-migration-evidence) at the end
for the full evidence record, if you want it. You don't need to read that
record to complete your own migration; this guide is self-contained.

## Who this guide is for

You have an existing AutoFixture-based test suite — `[AutoData]`/
`[InlineAutoData]`, one or more `ICustomization`/`ISpecimenBuilder`
implementations, possibly `AutoNSubstituteCustomization` — and you want to
move it to Compono. This guide assumes you're already comfortable with
AutoFixture and want the fastest path to idiomatic Compono, not an
introduction to Compono from scratch (start with
[Getting Started](getting-started/index.md) for that).

## Migration mindset

The goal isn't a mechanical, one-for-one API translation. AutoFixture is
**specimen-oriented** — a pipeline of request-matching builders that can
intercept and reshape almost any request. Compono is
**composition-oriented** — a fixed, ordered pipeline of exact
registrations, type/member rules, and providers, with generated
construction underneath. Some AutoFixture infrastructure has no Compono
counterpart because it solved a problem Compono's design doesn't have in
the first place. Let obsolete fixture infrastructure disappear rather than
recreating it under a new name.

A few principles to migrate by:

- **Prefer profiles over custom data-attribute subclasses.** A profile
  (`ICompositionProfile`) replaces the pattern of subclassing
  `AutoDataAttribute` to bake in a fixture factory.
- **Prefer exact registrations for exact-type creation.** If an
  AutoFixture customization only ever built one specific type, it's a
  `Register<T>` call, not a general-purpose builder.
- **Prefer member/type rules for scoped customization.** If a
  customization only overrode one member of one type, reach for
  `.For<T>().Member(...)` instead of a full specimen builder.
- **Use providers only for genuinely pattern-based behavior.** Reserve a
  custom `ICompositionValueProvider` for the rare case that really needs
  to match on request *shape*, not a fixed type — most AutoFixture
  specimen builders don't actually need this.
- **Use `[Shared]` only when object identity actually matters.** Don't
  reach for it just because AutoFixture's `[Frozen]` was on the parameter
  before — audit whether the test actually depends on the same instance.
- **Don't recreate hidden AutoFixture behavior unless the test genuinely
  needs it.** `ConfigureMembers`-style auto-configuration and
  recursion-omission are common examples — see below.
- **Prefer explicit substitute setup over recursive member
  auto-configuration.** A test that depends on a substitute's return value
  should stub it, not rely on an implicit default.

## Install Compono

See [Package Guides](packages/index.md) for the full ecosystem map. Most
AutoFixture users migrating xUnit tests need:

- **`Compono`** — the core package, always required.
- **`Compono.XunitV3`** — if you use `[AutoData]`/`[InlineAutoData]` today.
- **`Compono.NSubstitute`** — if you use `AutoNSubstituteCustomization`.
- **`Compono.Bogus`** — if you want realistic fake data instead of
  anonymous values.

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.XunitV3 --prerelease
dotnet add package Compono.NSubstitute --prerelease
dotnet add package Compono.Bogus --prerelease
```

Install matching versions of every Compono package you add — mixing
versions across packages isn't supported; see
[Package Guides: Version Compatibility](packages/index.md#version-compatibility).
See [Installation](getting-started/installation.md) for the full setup,
including why `--prerelease` is required during public preview.

## Quick concept map

An orientation aid, not a claim that the two frameworks are identical —
each row is expanded into its own section below.

| AutoFixture usage | Compono approach |
|---|---|
| `fixture.Create<T>()` | `composer.Create<T>()` |
| `[AutoData]` | `[Compose]` |
| Custom `AutoDataAttribute` subclass | `[Compose<TProfile>]` |
| **Parameterized** custom `AutoDataAttribute` subclass (constructor args driving customization logic) | `[Compose<TProfile, TConfig>]` |
| `ICustomization` | `ICompositionProfile` |
| Exact-type specimen customization | `Register<T>()` |
| Exact-type `ISpecimenBuilder` | `Register<T>()` |
| Type/member customization | `.For<T>()` / `.Member(...)` |
| Pattern-based specimen builder | `ICompositionValueProvider` |
| `[Frozen]` | `[Shared]`, when identity is genuinely required |
| `AutoNSubstituteCustomization` | `UseNSubstitute()` |
| Semantic/realistic data | `UseBogus()` / `UseBogus<T>()` |
| `OmitOnRecursionBehavior` | No equivalent — Compono fails clearly instead |

## Migrate object creation

The baseline call maps directly:

```csharp
// Before
var order = fixture.Create<Order>();

// After
var order = composer.Create<Order>();
```

`composer` comes from `Composer.Create(builder => ...)`, built once and
reused — see [The Composition Model](concepts/composition-model.md) if
you haven't read it yet.

## Migrate `[AutoData]` and `[InlineAutoData]`

A project-specific `AutoDataAttribute` subclass that bakes in an
`IFixture` factory:

```csharp
// Before
public sealed class ProjectAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    internal static IFixture CreateFixture() =>
        new Fixture().Customize(new ProjectCustomization());
}

[Theory]
[ProjectAutoData]
public void Handles_Order(Order order) { }
```

becomes `[Compose<TProfile>]` applied directly — no wrapper attribute
subclass, since the profile does what the custom subclass used to do
implicitly:

```csharp
// After
[Theory]
[Compose<ProjectTestProfile>]
public void Handles_Order(Order order) { }
```

See [Migrate `ICustomization`](#migrate-icustomization) below for what
`ProjectTestProfile` looks like.

**A row where every value is supplied inline needs no Compono attribute at
all.** If a `[Theory]` row supplies every parameter inline, plain xUnit
`[InlineData]` already works and is simpler than routing it through a
Compose-family attribute:

```csharp
// Before
[Theory]
[InlineProjectAutoData(null!, "")]
[InlineProjectAutoData("Kaladin", "kaladin")]
public void Normalizes(string? input, string expected) { }

// After — no Compono attribute needed
[Theory]
[InlineData(null, "")]
[InlineData("Kaladin", "kaladin")]
public void Normalizes(string? input, string expected) { }
```

`[Compose]` is method-scoped, not parameter-scoped — a row with nothing
left to compose doesn't need it. For a row that mixes inline values with
composed ones, `[Compose(...)]` binds the inline values positionally and
composes the rest — see
[How Do I Write a Composed Theory?](how-to/write-a-composed-theory.md).

**Only one Compose-family attribute per test method is supported.**
AutoFixture's idiom of stacking multiple `[InlineAutoData(...)]` instances
for several rows, each with its own composed parameters, has no direct
Compono equivalent — pick one Compose-family attribute per method and
cover the rest with a separate `[Theory]`/`[InlineData]` method instead.
See [`Compono.XunitV3`'s Package Guide](packages/compono-xunitv3.md#what-it-deliberately-doesnt-do)
for the full mechanics of why stacking isn't supported.

## Migrate a parameterized custom `AutoDataAttribute`

A common, larger pattern than the previous section's simple wrapper: a
custom `AutoDataAttribute` subclass whose own **constructor** takes
arguments that change what the underlying fixture customization produces
— not just which type gets composed, but a value read *inside* the
customization logic itself. Real, frequent examples found migrating a
much larger AutoFixture test suite than this guide's other examples are
drawn from (`ncipollina/trivia-platform`'s `PersistenceAutoData(repositoryName)` —
around 45 call sites, each a different repository name driving a
different persistence setup — and an 8-parameter
`AnnouncementsAutoData(validConfig, gameOverEnabled, ...)`):

```csharp
// Before
public sealed class PersistenceAutoDataAttribute(string repositoryName)
    : AutoDataAttribute(() => CreateFixture(repositoryName))
{
    private static IFixture CreateFixture(string repositoryName)
    {
        var fixture = new Fixture();
        fixture.Customize(new PersistenceCustomization(repositoryName));
        return fixture;
    }
}

[Theory]
[PersistenceAutoData("PlayerRepository")]
public void Repository_Works(PlayerRepository sut) { }
```

Neither of the migration paths the previous sections cover fits cleanly
here: a plain `[Compose<TProfile>]` has no way to receive
`"PlayerRepository"` at all, and writing one profile subclass per
repository name doesn't scale to `AnnouncementsAutoData`'s combinatorial
8-flag argument space — nor does falling back to a hand-built
`Composer.Create(...)` per test, which reintroduces exactly the per-test
setup code the attribute-based idiom exists to eliminate. This is what
`[Compose<TProfile, TConfig>]` ([ADR-0036](adr/0036-parameterized-composition-profile-selection.md))
exists for — a **typed configuration object** paired with the profile,
bound from this attribute's own constructor arguments:

```csharp
// After
public enum RepositoryKind
{
    Player,
    Leaderboard,
}

public sealed record PersistenceConfig(RepositoryKind Repository);

public sealed class PersistenceProfile : ICompositionProfile
{
    public PersistenceProfile(PersistenceConfig config) => Config = config;

    public PersistenceConfig Config { get; }

    public void Configure(CompositionBuilder builder) =>
        builder.Register<IRepositoryOptions>(_ => RepositoryOptionsFactory.Create(Config.Repository));
}

[Theory]
[Compose<PersistenceProfile, PersistenceConfig>(RepositoryKind.Player)]
public void Repository_Works(PlayerRepository sut) { }
```

Note the enum, not a string — the original AutoFixture attribute took a
raw `string repositoryName`, but that string only ever had a handful of
valid values in practice (a finite, named choice). `params object?[]` is
a binding mechanism forced by C#'s attribute-argument-must-be-a-
compile-time-constant rule, not a license to carry the original
stringly-typed shape forward — see
[`Compono.XunitV3`'s Package Guide](packages/compono-xunitv3.md#profile-configuration-arguments)
for the full "prefer the strongest attribute-legal type" guidance
(`typeof(...)` for a CLR type, `bool`/numeric values where those already
carry the real meaning).

**Don't reach for `[Compose<TProfile, TConfig>]` for every parameterized
attribute, though.** If the "parameter" is really just a `[Frozen]`-style
substitute or a single fixed value that's the same for every call site in
practice, the simpler existing forms (`[Compose<TProfile>]`, an inline
value, a member rule) already cover it — reserve this form for the case
this section actually describes: a value that's genuinely different per
call site and needs to reach configuration logic running *inside* the
profile, not at the test method's own parameter list.

## Migrate `ICustomization`

```csharp
// Before
public sealed class ProjectCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new OrderSpecimenBuilder());
    }
}
```

```csharp
// After
public sealed class ProjectTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.Register(CreateOrder);
    }

    private static Order CreateOrder(ICompositionContext context) =>
        new(context.Resolve<string>());
}
```

**Simpler:** an empty or commented-out `ICustomization` with no real
customization logic doesn't need porting at all — delete it. A
customization that did real work becomes a profile of equivalent
`Register<T>`/rule calls, as above. A profile shared across projects can
be composed into another via `builder.AddProfile<TProfile>()` — see
[Profiles](concepts/profiles.md) for when a separate, composed-in profile
is worth it versus configuring everything in one place.

## Migrate `[Frozen]` and shared dependencies

By default, Compono composes each parameter independently — two
parameters of the same type get two separate instances, same as
unfrozen AutoFixture. `[Shared]` is the direct equivalent of `[Frozen]`,
but audit each real `[Frozen]` usage rather than translating it
mechanically: many turn out not to need it at all.

**Equivalent — real sharing.** Where a dependency composed as a test
parameter is also depended on by another composed parameter, and the test
needs to assert against or configure that exact instance, `[Shared]`
preserves the behavior directly:

```csharp
// Before
public async Task Repository_UsesTheConfiguredClient(
    [Frozen] IHttpClient client,
    OrderRepository sut) { }

// After
public async Task Repository_UsesTheConfiguredClient(
    [Shared] IHttpClient client,
    OrderRepository sut) { }
```

**Simpler — `[Frozen]` wasn't sharing anything.** A very common pattern is
`[Frozen]` used purely to obtain a substitute for an interface, with the
substitute never reused elsewhere in the same test. This needs no
annotation under Compono at all — composing an interface parameter
already produces a substitute automatically once `UseNSubstitute()` is
active:

```csharp
// Before
public async Task Handle_WhenInvalid_DoesNotCallRepository(
    [Frozen] IOrderRepository repository) { }

// After — [Frozen] wasn't sharing anything
public async Task Handle_WhenInvalid_DoesNotCallRepository(
    IOrderRepository repository) { }
```

Auditing every `[Frozen]` usage this way — rather than converting each one
to `[Shared]` by rote — is usually the single biggest simplification a
migration finds. See [Shared Values](concepts/shared-values.md) for the
full model.

## Migrate AutoNSubstitute

`AutoNSubstituteCustomization` becomes `builder.UseNSubstitute()`:

```csharp
public void Configure(CompositionBuilder builder)
{
    builder.UseNSubstitute();
}
```

**Tradeoff:** `AutoNSubstituteCustomization { ConfigureMembers = true }`
auto-configures every generated substitute's members with sensible return
values, including recursively-constructed objects for `Task<T>`-returning
members. `Compono.NSubstitute` has no equivalent — every substitute is a
bare `Substitute.For<T>()`. Most call sites that never depended on a
specific return value need no change at all. But watch for a test that
never stubs a substitute's member yet asserts the code under test doesn't
throw — it may have been passing only because auto-configuration supplied
a non-null value. Under Compono's bare substitute, the same unstubbed call
returns NSubstitute's own default (`null`/`default`, or
`Task.FromResult<T>(default)` for an async member); if your code
dereferences that result, you'll see a `NullReferenceException` where the
test previously passed silently:

```csharp
client.SendAsync(Arg.Any<HttpRequestMessage>())
    .Returns(new HttpResponseMessage(HttpStatusCode.OK));
```

This is a real, one-time migration cost for a suite that leaned on
auto-configuration — but it also makes a previously-hidden dependency
visible in the test body, which is the point: Compono favors explicit
setup over implicit magic throughout. See
[`Compono.NSubstitute`'s Package Guide](packages/compono-nsubstitute.md#what-it-deliberately-doesnt-do)
for the full rationale.

## Migrate specimen builders

`ISpecimenBuilder` served several distinct purposes in AutoFixture, and
doesn't map to one single Compono extension point — which one you need
depends on what the original builder actually did:

| What the specimen builder does | Compono mechanism |
|---|---|
| Creates one exact type | `Register<T>()` |
| Overrides one type or member | `.For<T>()` / `.Member(...)` |
| Matches open-ended request shapes | `ICompositionValueProvider` |
| Creates a complete, realistic object | `UseBogus<T>()` |

Most real specimen builders fall into the first case — dispatching on a
fixed `Type`/`ParameterInfo`/`NamedRequest` to build one specific type:

```csharp
// Before
public sealed class OrderSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        return request switch
        {
            Type t when t == typeof(Order) => CreateOrder(context),
            ParameterInfo p when p.ParameterType == typeof(Order) => CreateOrder(context),
            NamedRequest { InnerRequest: Type t } nr when t == typeof(Order) => CreateOrder(context),
            _ => new NoSpecimen(),
        };
    }

    private static Order CreateOrder(ISpecimenContext context) =>
        new(context.Create<string>());
}
```

```csharp
// After — no request-shape pattern-matching needed
builder.Register<Order>(context => new Order(context.Resolve<string>()));
```

Compono's registration is keyed by exact type, so there's no
`Type`/`ParameterInfo`/`NamedRequest` matching to write by hand for this
case. Reach for a custom `ICompositionValueProvider` only for the rarer
case that genuinely needs to match on request shape rather than a fixed
type — see [Providers](concepts/providers.md).

**A specimen builder that dispatches on the requesting *parameter/member
name*, not just its type** — several distinct values of the same
declared type, chosen by which parameter is asking — is the other real
case a custom `ICompositionValueProvider` covers cleanly.
`CompositionProviderRequest.Name` carries the requesting constructor
parameter/required member/test-method-parameter's own name for exactly
this:

```csharp
// Before
public sealed class UpsellPayloadSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context) => request switch
    {
        ParameterInfo { Name: "newGamePayload" } => new UpsellPayload("new-game"),
        ParameterInfo { Name: "lockedPackPayload" } => new UpsellPayload("locked-pack"),
        _ => new NoSpecimen(),
    };
}
```

```csharp
// After
public sealed class UpsellPayloadProvider : ICompositionValueProvider
{
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (request.RequestedType != typeof(UpsellPayload))
            return CompositionProviderResult.NotHandled;

        return request.Name switch
        {
            "newGamePayload" => CompositionProviderResult.Handled(new UpsellPayload("new-game")),
            "lockedPackPayload" => CompositionProviderResult.Handled(new UpsellPayload("locked-pack")),
            _ => CompositionProviderResult.NotHandled,
        };
    }
}
```

Registered via `builder.AddSemanticProvider(new UpsellPayloadProvider())`
(or `AddTestDoubleProvider`, depending on what it's producing — see
[Providers](concepts/providers.md)). This is a different question from
[Profile configuration arguments](packages/compono-xunitv3.md#profile-configuration-arguments) —
a `Name`-based provider is a **global rule** ("whenever anything asks for
`UpsellPayload` named `newGamePayload`, produce this"), evaluated for
every matching request across every test; a profile configuration
argument is a **per-invocation value** known only at one specific test's
`[Compose<TProfile, TConfig>(...)]` call site. Don't reach for one to
solve the other.

## Handle recursion behavior

**Intentional difference:** AutoFixture's default `ThrowingRecursionBehavior`
can be swapped for `OmitOnRecursionBehavior`, which silently omits a
member that would cause infinite recursion instead of throwing. Compono
has no equivalent to opt into — a genuine construction cycle always fails
fast with a path-annotated `CompositionException`
([ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md)),
the same way any other unsatisfiable composition does. If your object
graph is genuinely self-referencing, break the cycle explicitly with a
`Register<T>` factory that supplies the recursive member directly, rather
than relying on generated default construction. See
[Troubleshooting: Common Errors](troubleshooting/common-errors.md#runtime-composition-failures)
if you hit this during migration.

## Add realistic data with Bogus

Where AutoFixture only produces anonymous specimens,
`Compono.Bogus`'s `UseBogus<T>(Action<Faker<T>>)` builds a `Faker<T>`
already seeded from the current composition's own deterministic seed
before invoking your configuration callback — every `RuleFor` inside it is
automatically seed-consistent with the rest of the composition, with no
manual seeding required:

```csharp
public void Configure(CompositionBuilder builder)
{
    builder.UseBogus<Customer>(ConfigureCustomer);
}

private static void ConfigureCustomer(Faker<Customer> faker)
{
    faker.RuleFor(c => c.FullName, f => f.Name.FullName());
    faker.RuleFor(c => c.Email, f => f.Internet.Email());
}
```

For members that follow a common naming convention (`FirstName`, `Email`,
`PhoneNumber`, and similar), plain `UseBogus()` matches them automatically
with no per-type configuration — see
[`Compono.Bogus`'s Package Guide](packages/compono-bogus.md) for the full
built-in list and its member-name-matching limits.

## Concepts that disappear entirely

**Removed entirely — no replacement concept exists:**

| AutoFixture concept | Why nothing replaced it |
|---|---|
| `IFixture` | Composition is per-test-method via `[Compose<TProfile>]` — there's no fixture object, configured or otherwise |
| `IRequestSpecification`/`NamedRequest` | Registration is keyed by exact type; no separate request-matching type is needed |
| `AutoNSubstituteCustomization`'s member auto-configuration | Compono never auto-configures a substitute's members |
| `OmitOnRecursionBehavior` | A construction cycle always fails fast |

**Replaced one-for-one with a Compono equivalent:**

| AutoFixture concept | Compono equivalent |
|---|---|
| `ICustomization` | `ICompositionProfile` |
| `ISpecimenBuilder` (exact-type case) | `CompositionBuilder.Register<T>(...)` |
| Custom `AutoDataAttribute`/`InlineAutoDataAttribute` subclasses | `[Compose]`/`[Compose<TProfile>]`, applied directly |
| `AutoNSubstituteCustomization`'s substitute creation itself | `builder.UseNSubstitute()` |
| `Freeze<T>()`/`[Frozen]` | `[Shared]` |

## Known differences and limitations

**Composing an external or BCL type with ambiguous constructors fails.**
Compono's constructor-selection validation runs entirely at compile time,
from a type's constructor count — it has no visibility into a runtime
registration that might construct the type, so a type like `HttpClient`
(three accessible constructors) always fails with
[`CMP0001`](reference/diagnostics.md#cmp0001-ambiguous-construction-path),
even with an explicit registration for it. For an external or BCL type
with ambiguous constructors, compose an application-owned abstraction or
provider instead of the concrete type directly:

```csharp
public interface IHttpClientProvider
{
    HttpClient Create();
}

internal sealed class HttpClientProvider(HttpMessageHandler handler) : IHttpClientProvider
{
    public HttpClient Create() => new(handler) { BaseAddress = new Uri("https://localhost/") };
}

public sealed class ClientTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.Register<HttpMessageHandler>(_ => Substitute.For<HttpMessageHandler>());
        builder.Register<IHttpClientProvider>(context => new HttpClientProvider(context.Resolve<HttpMessageHandler>()));
    }
}
```

```csharp
[Theory]
[Compose<ClientTestProfile>]
public async Task UsesTheConfiguredResponse(
    [Shared] HttpMessageHandler handler,
    IHttpClientProvider clientProvider)
{
    // configure `handler` to return the response you want, then:
    var client = clientProvider.Create();
}
```

An interface is always resolved by a provider, never by constructor
selection, so it never reaches ambiguous-constructor validation. See
[Reference: Diagnostics](reference/diagnostics.md#cmp0001-ambiguous-construction-path)
for the full cause/fix detail.

For the rest of Compono's `0.x` known limitations (Compose-family
stacking, Bogus's member-name-matching limits, and more), see each
Package Guide's own "What it deliberately doesn't do" section, aggregated
in [Troubleshooting](troubleshooting/index.md#known-limitations).

## Migration checklist

- [ ] Remove AutoFixture package references.
- [ ] Add the required Compono packages at matching versions.
- [ ] Replace custom AutoData attributes with `[Compose]` or
      `[Compose<TProfile>]` — or, for one whose constructor arguments
      drive customization logic, `[Compose<TProfile, TConfig>]`.
- [ ] Convert real customizations into profiles.
- [ ] Delete empty or obsolete fixture abstractions.
- [ ] Audit every `[Frozen]` usage to determine whether identity is
      actually required before converting it to `[Shared]`.
- [ ] Add `UseNSubstitute()` to profiles that need substitutes.
- [ ] Add explicit stubs where tests relied on `ConfigureMembers`.
- [ ] Convert exact-type specimen builders to registrations.
- [ ] Use rules or providers only where their matching behavior is
      actually needed.
- [ ] Remove recursion-behavior configuration and run the suite.
- [ ] Introduce `Compono.Bogus` where semantic data improves readability.
- [ ] Run the complete test suite and inspect failures for hidden
      dependencies (most commonly, `ConfigureMembers`-shaped ones).
- [ ] Remove unused fixture infrastructure after migration.

## Real-world migration evidence

This guide's patterns were validated against a real migration of a
multi-project .NET test suite — not invented to illustrate a point in the
abstract. If you want the full evidence record — post-migration metrics,
every finding's classification, and the design decisions it fed into —
it's available, but not required reading to complete your own migration:

- [Research: AutoFixture vs. Compono Dogfooding](research/0001-autofixture-comparison.md) —
  the complete evidence dossier.
- [ADR-0002](adr/0002-constructor-selection-algorithm.md),
  [ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md),
  [ADR-0018](adr/0018-composition-profiles.md),
  [ADR-0025](adr/0025-compono-nsubstitute-package-design.md),
  [ADR-0026](adr/0026-deterministic-seed-derivation-for-providers.md) —
  the design decisions this guide's equivalents are drawn from.
- [Samples](samples/index.md) — complete, runnable projects.
- [Troubleshooting](troubleshooting/index.md) — if something in your own
  migrated tests doesn't behave the way you expect.
