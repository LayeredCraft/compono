---
title: Compose Configuration From an In-Memory Collection
description: Register a real IConfiguration for a test, built from plain in-memory key/value pairs.
packages: [Compono]
concepts: [registration]
---

# Compose Configuration From an In-Memory Collection

## Problem

Your code under test depends on `IConfiguration` directly (`GetSection`,
`GetValue<T>`, configuration-binding helpers) and the test wants a real,
predictable `IConfiguration` instance — not a hand-rolled fake, and no
external config file on disk.

## Solution

```csharp
using Microsoft.Extensions.Configuration;

IConfiguration BuildConfiguration() =>
    new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = "test-connection-string",
            ["Database:TimeoutSeconds"] = "5",
        })
        .Build();
```

```csharp
public sealed class DatabaseConfigurationProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IConfiguration>(BuildConfiguration);
}
```

```csharp
[Theory]
[Compose<DatabaseConfigurationProfile>]
public void ReadsTheConnectionStringFromConfiguration(IConfiguration configuration)
{
    configuration["Database:ConnectionString"].Should().Be("test-connection-string");
}
```

Or, wired directly through a hand-built `Composer` without a test-framework
attribute at all:

```csharp
var composer = Composer.Create(builder => builder
    .Register<IConfiguration>(BuildConfiguration));

var repository = composer.Create<OrderRepository>();
```

## Discussion

This is ordinary `Microsoft.Extensions.Configuration` composition —
`Compono` adds nothing beyond the standard `Register<T>` call.
`AddInMemoryCollection` accepts colon-separated keys (`"Database:TimeoutSeconds"`)
the same way a real `appsettings.json`'s nested sections flatten when
bound, so `configuration.GetSection("Database").GetValue<int>("TimeoutSeconds")`
and configuration-binding (`configuration.GetSection("Database").Get<DatabaseOptions>()`)
both work exactly as they would against a real configuration source.

There is no `Compono.Configuration` package — plain
`ConfigurationBuilder`/`Register<IConfiguration>` composition, as above, is
the whole answer for `IConfiguration` itself. If your code under test
instead depends on `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`
for a strongly-typed settings class, that's
[`Compono.Options`](../packages/compono-options.md)'s job, not this
recipe's — the two compose independently and don't need to agree with each
other's values.

## See also

- [Layer Configuration Overrides in a Test](layer-configuration-overrides-in-a-test.md)
- [Reuse Configuration Through a Profile](reuse-configuration-through-a-profile.md)
- [`Compono.Options` Package Guide](../packages/compono-options.md) — for
  `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` instead of plain
  `IConfiguration`.
