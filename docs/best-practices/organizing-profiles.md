# Organizing Profiles

## Build up from a few small, focused profiles

Prefer several small, focused profiles over one large one, and combine
them with `AddProfile<TProfile>()`/`[Compose<TProfile>]` as each test
actually needs:

```csharp
public sealed class DomainProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IClock>(_ => new FakeClock());
}

public sealed class InfrastructureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseNSubstitute();
}
```

```csharp
var composer = Composer.Create(builder => builder
    .AddProfile<DomainProfile>()
    .AddProfile<InfrastructureProfile>());
```

A small `DomainProfile` is reusable on its own in a test that has nothing
to do with infrastructure concerns — one large `ApplicationProfile`
covering everything forces every test that uses any part of it to pull in
configuration it doesn't need, and makes it harder to tell which
registration actually matters for a given test's failure.

## Name a profile after what it configures, not who uses it

`InfrastructureProfile`, `NotificationProfile`, `PaymentGatewayProfile` —
name profiles after the concern they configure, not the test class or
feature that happens to use them first. A profile named after its first
consumer (`OrderServiceTestsProfile`) implies a 1:1 relationship that
usually isn't real — the same infrastructure configuration is almost
always useful to more than one test class.

## Keep a profile pure configuration

A profile's `Configure` method should only call `CompositionBuilder`
methods — no per-test assertions, no mutable state, nothing that depends
on when or how many times it runs. `[Compose<TProfile>]` and
`AddProfile<TProfile>()` both construct a profile freshly and apply it
once; treating a profile as anything other than a declarative
configuration step is a sign the configuration it needs doesn't belong in
a profile at all.

## One profile applying itself again is a cycle, not a no-op

Compono raises `CompositionConfigurationException` immediately if a
profile (directly, or through a nested profile) tries to apply itself
again while already applying — this is deliberate, not a limitation to
work around. If you find yourself wanting that shape, the configuration
those two profiles share probably belongs in a third, smaller profile both
depend on instead.

## Next

- The full profile API and constraints → [Profiles](../concepts/profiles.md).
- Applying a profile to a test → [Use Profiles](../how-to/use-profiles.md).
- Sharing registrations across profiles without duplication →
  [Reusing Configuration](reusing-configuration.md).
