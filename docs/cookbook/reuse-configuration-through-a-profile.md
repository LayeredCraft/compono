---
title: Reuse Configuration Through a Profile
description: Establish one project's baseline IConfiguration once, in a shared ICompositionProfile.
packages: [Compono]
concepts: [profiles, registration]
---

# Reuse Configuration Through a Profile

## Problem

Every test in a project needs the same baseline `IConfiguration` — copying
the same `ConfigurationBuilder`/`AddInMemoryCollection` call into every
test class is repetitive and drifts inconsistent over time.

## Solution

```csharp
using Microsoft.Extensions.Configuration;

public sealed class AppConfigurationProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder
        .Register<IConfiguration>(() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retry:MaxAttempts"] = "3",
                ["Retry:DelayMilliseconds"] = "100",
            })
            .Build());
}
```

```csharp
[Theory]
[Compose<AppConfigurationProfile>]
public void ReadsTheSharedBaselineConfiguration(IConfiguration configuration)
{
    configuration["Retry:MaxAttempts"].Should().Be("3");
}
```

Or applied to a hand-built `Composer` directly:

```csharp
var composer = Composer.Create(builder => builder.AddProfile<AppConfigurationProfile>());
```

## Discussion

`ICompositionProfile.Configure` is just an ordinary sequence of
`CompositionBuilder` calls — `Register<IConfiguration>` needs no special
profile-only mechanism to be reusable this way, the same as any other
registration.

Registering the same exact type more than once is a build-time conflict in
Compono, not last-write-wins or first-write-wins (`docs/adr/0019-registrations-and-service-provider-injection.md`) —
so a test can't compose `AppConfigurationProfile` (which already calls
`Register<IConfiguration>`) and *then* call `Register<IConfiguration>`
again itself to layer an override; that collides and throws
`CompositionConfigurationException`. A test that needs the shared baseline
plus one local override instead passes the override into the profile's own
constructor and applies it with the instance-based
`AddProfile(ICompositionProfile)` overload, so the profile itself is the
one and only place that calls `Register<IConfiguration>`:

```csharp
public sealed class AppConfigurationProfile(IDictionary<string, string?>? overrides = null) : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder
        .Register<IConfiguration>(() => BuildConfiguration(overrides));
}
```

```csharp
var composer = Composer.Create(builder => builder.AddProfile(
    new AppConfigurationProfile(new Dictionary<string, string?> { ["Retry:MaxAttempts"] = "0" })));
```

See [Layer Configuration Overrides in a Test](layer-configuration-overrides-in-a-test.md)
for `BuildConfiguration`'s own layering (multiple `AddInMemoryCollection`
calls inside one `ConfigurationBuilder`, not multiple `Register<T>` calls),
and [Composition Profiles](../concepts/profiles.md) for the full profile
mechanics.

## See also

- [Compose Configuration From an In-Memory Collection](compose-configuration-from-an-in-memory-collection.md)
- [Layer Configuration Overrides in a Test](layer-configuration-overrides-in-a-test.md)
- [Composition Profiles](../concepts/profiles.md)
