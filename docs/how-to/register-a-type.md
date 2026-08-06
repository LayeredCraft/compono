# How Do I Register a Type?

Use `Register<T>` when a type needs an exact, universal factory — every
composed `T`, anywhere in the graph, should come from this one factory.

## Direct

```csharp
var composer = Composer.Create(builder => builder
    .Register<IClock>(_ => new FakeClock()));
```

## Resolving nested dependencies

```csharp
builder.Register<Order>(context => new Order(context.Resolve<Customer>(), context.Resolve<IClock>().UtcNow));
```

`context.Resolve<T>()` composes `T` through the same composer, the same
way a generated construction plan would — use it when your registration
factory needs its own composed dependencies rather than constructing them
by hand.

## Wiring it into a profile

A registration used across more than one test class belongs in a
[profile](../concepts/profiles.md), not repeated per test:

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IClock>(_ => new FakeClock(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
}
```

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void UsesTheFrozenClock(IClock clock) { }
```

## Falling back to an `IServiceProvider`

If you already have a DI container configured (e.g. from an ASP.NET Core
test host) and don't want to duplicate every registration by hand:

```csharp
builder.UseServiceProvider(app.Services);
```

An exact `Register<T>` always wins over the configured `IServiceProvider`
for the same type; a container miss falls through to the rest of the
pipeline rather than failing outright.

## Common mistakes

- Calling `Register<T>` for the same `T` twice (directly, or once directly
  and once via a profile) — this is a configuration conflict
  (`CompositionConfigurationException`), not last-write-wins. Use a
  [member rule](customize-a-member.md) instead if you only need to
  override one usage site.
- Reaching for `Register<T>` when you only want to override one member of
  one parent type — that's `For<T>().Member(...)`, not `Register<T>`.

## Next

- The full configuration model → [Registrations and Rules](../concepts/registrations-and-rules.md).
- Reuse this across every test → [Use Profiles](use-profiles.md).
- Precise API contract → [Reference](../reference/index.md).
