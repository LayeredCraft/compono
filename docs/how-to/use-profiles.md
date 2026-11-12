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

## A profile whose constructor takes runtime objects

`[Compose<TProfile, TConfig>]` arguments must be attribute constants. A
profile that needs runtime objects, such as interceptors, a configured
client, or an in-memory log, can't get them from an attribute. There are
two patterns, and both work on `Compono.XunitV3` and `Compono.XunitV3.Aot`.

**1. A parameterless wrapper profile per variant.** Name each variant once,
then use it like any other profile. The wrapper builds the arguments and
applies the real profile with `AddProfile(instance)`:

```csharp
public sealed class DbProfile(AuditInterceptor? interceptor) : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new OrderRepository(interceptor));
}

public sealed class AuditedDbProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        var log = new AuditLog();
        builder.Register(() => log);                              // so the test can assert on it
        builder.AddProfile(new DbProfile(new AuditInterceptor(log)));
    }
}

[Theory, Compose<AuditedDbProfile>]
public void SavingIsAudited(OrderRepository sut, AuditLog log)
{
    sut.Save("42");
    Assert.Equal(["saved 42"], log.Entries);
}
```

Prefer this over adding a parameterless constructor to `DbProfile` itself.
The variant gets its own name, and `DbProfile` keeps one constructor.

**2. A single-row `[ComposeData<TProfile, TConfig>]`.** When the arguments
are data (built in a static member rather than per variant), put them in a
`TConfig` and supply one row:

```csharp
public sealed record AuditConfig(AuditInterceptor Interceptor);

public static TheoryData<AuditConfig> Audited =>
    new() { new AuditConfig(new AuditInterceptor(new AuditLog())) };

[Theory]
[ComposeData<ConfiguredDbProfile, AuditConfig>(nameof(Audited))]
public void SavingIsAudited(OrderRepository sut, AuditLog log) { /* ... */ }
```

The test is a one-row theory. Both patterns are exercised by
`DocumentedRecipeTests` in `test/Compono.XunitV3.Tests` and
`test/Compono.XunitV3.Aot.Tests`. A dedicated "profile factory member"
attribute property isn't offered. It's deferred until these patterns prove
inadequate
([RESEARCH-0040](../research/0040-issue-161-1-4-dogfood-follow-up.md)
finding 6). Runtime values from an xUnit fixture can also be read inside a
profile with `TestContext.Current.RequireFixture<TFixture>()`; see the
[`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md).

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

If a profile's parameterless constructor throws, `AddProfile<TProfile>()`
surfaces **that** exception directly — its original type and stack, not a
`TargetInvocationException` wrapper — so the failure points at your profile
rather than at Compono's construction plumbing. This applies wherever
`AddProfile<TProfile>()` is used, including `[Compose<TProfile>]` and
`[ComposeData<TProfile>]`.

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
