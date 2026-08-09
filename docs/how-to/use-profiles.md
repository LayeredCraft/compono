# How Do I Use Profiles?

## Define one

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder
            .UseNSubstitute()
            .UseBogus()
            .Register<IClock>(_ => new FakeClock());
}
```

## Apply it programmatically

```csharp
var composer = Composer.Create(builder => builder.AddProfile<ApplicationTestProfile>());
```

## Apply it to a composed xUnit theory

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void ComposesTheProfileConfiguredValue(NotificationSettings settings) { }
```

`TProfile` must implement `ICompositionProfile` and have a public
parameterless constructor — `[Compose<TProfile>]` enforces this at compile
time via a generic constraint.

**A profile that needs a value known only at a specific test's call
site** — not a fixed, default-constructed one — can't use
`[Compose<TProfile>]` at all, since it has no way to receive that value.
`[Compose<TProfile, TConfig>]` covers this: `TConfig` is a small,
strongly-typed configuration object, bound positionally from the
attribute's own constructor arguments and passed to `TProfile`'s
constructor. See
[`Compono.XunitV3`'s Package Guide](../packages/compono-xunitv3.md#profile-configuration-arguments)
for the full shape and
[Migrating from AutoFixture](../migrating-from-autofixture.md#migrate-a-parameterized-custom-autodataattribute)
for the AutoFixture pattern this replaces.

## Combining more than one profile

```csharp
var composer = Composer.Create(builder => builder
    .AddProfile<DomainProfile>()
    .AddProfile<InfrastructureProfile>());
```

Profiles apply in the order added. Build up project-wide configuration
from a few small, focused profiles rather than one large one — it's easier
to reuse a `DomainProfile` on its own in a test that doesn't need
`InfrastructureProfile`'s configuration.

## Applying an already-built instance instead of a type

```csharp
builder.AddProfile(new ApplicationTestProfile());
```

Use this over `AddProfile<TProfile>()` only when the profile itself needs
constructor arguments — most profiles don't, and `AddProfile<TProfile>()`
is the more common form.

## Common mistakes

- A profile that applies itself again while already applying (directly, or
  through a nested profile) — this is a cycle, raised immediately as
  `CompositionConfigurationException`, not a silently-ignored no-op.
- Putting per-test assertions or mutable state inside a profile — a
  profile is pure `Composer` configuration, applied once, synchronously.

## Next

- What a profile is and when to reach for one →
  [Profiles](../concepts/profiles.md).
- Using a profile from `Compono.XunitV3` specifically →
  [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md).
- Registering an individual type instead → [Register a Type](register-a-type.md).
