# How Do I Assert Nothing Logged at Error or Above Across a Generic Host?

Register a `LogCollector` on the host's logging pipeline, run the host,
then verify the collector. The collector is the test-owned capture
destination: it sees every category the host logs to, regardless of which
`ILogger<T>` produced the entry. See
[`Compono.Logging`'s Package Guide](../packages/compono-logging.md#host-wide-capture-logcollector--addcomponocapture)
for the full host-wide semantics.

## Basic case

```csharp
using Compono.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[Fact]
public async Task Startup_LogsNothingAtErrorOrAbove()
{
    var builder = Host.CreateApplicationBuilder();
    var logs = new LogCollector();

    builder.Logging.ClearProviders();
    builder.Logging.AddComponoCapture(logs);

    using var app = builder.Build();
    await app.StartAsync(TestContext.Current.CancellationToken);
    await app.StopAsync(TestContext.Current.CancellationToken);

    logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();
}
```

`AtLevelOrAbove(LogLevel.Error)` matches `Error`, `Critical`, and anything
else at or above that severity; `LogLevel.None` never matches. `Never()`
fails the test with a message naming the filter and the matching entries,
so a regression reads as:

```text
Expected exactly 0 call(s) to a log entry matching level Error or above, but received 1.
```

## Narrow it to one category

Add `InCategory` when only one subsystem should be checked. The match is
an exact, ordinal, case-sensitive comparison of the entry's `Category`:

```csharp
logs.Verify()
    .AtLevelOrAbove(LogLevel.Error)
    .InCategory("Payments.Gateway")
    .Never();
```

For a host entry, `Category` is exactly the string the logging pipeline
passed to `ILoggerProvider.CreateLogger` — normally the full name of the
`ILogger<T>` category type.

## What the host may not capture

Compono never changes the host's own filter rules. The default host
minimum level is `Information`, so `Debug` and `Trace` entries never reach
the collector unless the host is configured to allow them:

```csharp
    builder.Logging.SetMinimumLevel(LogLevel.Trace);
```

If the host filters a category out with `AddFilter`, that category is
absent from the collector too. This is intentional: the collector reports
what the host actually logged, not what any logger attempted to log.

## Keep the collector for the whole test

The test owns the `LogCollector`; the host only writes to it. Disposing the
host stops further capture through that host, but entries already captured
stay readable — so you can assert after `Dispose()`/`await using` without
copying anything out first. A collector can also be shared by more than one
host in the same test; each host keeps appending until it is disposed.

## Using `WebApplicationFactory`

The same registration works through `ConfigureLogging`:

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public LogCollector Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddComponoCapture(Logs);
        });
}
```

Then assert `factory.Logs.Verify().AtLevelOrAbove(LogLevel.Error).Never()`
after the request or startup path under test.

## See also

- [Host-wide capture](../packages/compono-logging.md#host-wide-capture-logcollector--addcomponocapture)
  — Category, filters, ownership, and cardinality rules.
- [Share a Value Across a Test](share-a-value-across-a-test.md) — when a
  service under test needs a specific `ILogger<T>` rather than host-wide
  capture.
