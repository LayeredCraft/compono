---
title: Layer Configuration Overrides in a Test
description: Start from a shared baseline configuration and override just the values one test cares about.
packages: [Compono]
concepts: [registration]
---

# Layer Configuration Overrides in a Test

## Problem

Most tests should share one baseline configuration, but a specific test
needs one or two values different — without duplicating every other key
just to change one.

## Solution

```csharp
using Microsoft.Extensions.Configuration;

IConfiguration BuildConfiguration(IDictionary<string, string?>? overrides = null)
{
    var builder = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Retry:MaxAttempts"] = "3",
            ["Retry:DelayMilliseconds"] = "100",
        });

    if (overrides is not null)
    {
        builder.AddInMemoryCollection(overrides);
    }

    return builder.Build();
}
```

```csharp
var composer = Composer.Create(builder => builder
    .Register<IConfiguration>(() => BuildConfiguration(
        new Dictionary<string, string?> { ["Retry:MaxAttempts"] = "0" })));

var client = composer.Create<RetryingApiClient>();
// client observes Retry:MaxAttempts = "0", Retry:DelayMilliseconds = "100" (unchanged baseline)
```

## Discussion

`AddInMemoryCollection`, like every `IConfigurationSource`, follows
`Microsoft.Extensions.Configuration`'s own later-source-wins layering — a
later `AddInMemoryCollection` call overrides a key an earlier one already
set, and leaves every key it doesn't mention untouched. This is real
`IConfiguration` behavior, not a Compono-specific mechanism; the same
pattern applies to `AddJsonFile`/`AddEnvironmentVariables`/any other real
source if a project's test setup needs them.

Keep the override dictionary scoped to exactly the keys one test needs to
differ — resist the temptation to duplicate the whole baseline "just to be
safe." A test that overrides one key should be legible as "the same
configuration as everywhere else, except this one thing."

## See also

- [Compose Configuration From an In-Memory Collection](compose-configuration-from-an-in-memory-collection.md)
- [Reuse Configuration Through a Profile](reuse-configuration-through-a-profile.md)
