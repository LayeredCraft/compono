# Profiles

## What a profile is

A profile is a reusable bundle of `Composer` configuration — an
`ICompositionProfile` implementation whose `Configure(CompositionBuilder builder)`
method calls the exact same builder verbs a direct caller would
(`Register`, `For<T>()`, `UseNSubstitute()`, and so on). There's no
separate profile DSL to learn; a profile is just "the configuration I'd
otherwise repeat in every test, written once."

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder
            .UseNSubstitute()
            .UseBogus()
            .Register<IClock>(_ => new FakeClock(
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
}
```

```csharp
var composer = Composer.Create(builder => builder.AddProfile<ApplicationTestProfile>());
```

## When to reach for one

Reach for a profile once the same handful of `Register`/`For<T>`/`UseX()`
calls would otherwise be copy-pasted across several test classes — a
project-wide convention ("`IClock` is always frozen," "always compose
substitutes and Bogus data") belongs in a profile, not repeated per test.
A one-off customization that's genuinely local to a single test doesn't
need one — configure the `Composer`/`[Compose]` attribute directly instead.

Profiles compose with each other, too — `AddProfile<DomainProfile>()` and
`AddProfile<InfrastructureProfile>()` on the same builder both apply, in
the order added, letting you build up project-wide configuration from
smaller, focused profiles rather than one large one.

## What it isn't

A profile is not a base class, a lifecycle hook, or a place to put
per-test assertions — it's pure `Composer` configuration, applied once,
synchronously, when the `Composer` is created. A profile that tries to
apply itself again while already applying (directly, or through a nested
profile) is a configuration error (`CompositionConfigurationException`,
raised immediately), not a silently-ignored no-op — profile composition is
expected to be acyclic.

## Next

- Build one for xUnit v3 → [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md)
  and [Use Profiles](../how-to/use-profiles.md).
- The configuration verbs a profile actually calls →
  [Registrations and Rules](registrations-and-rules.md).
- Cookbook recipes that use a profile → [Cookbook](../cookbook/index.md).
- Precise API contract → [`ICompositionProfile` reference](../reference/api/Compono/index.md).
