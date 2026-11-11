# How Do I Assert Nothing Logged at Error or Above Across a Generic Host?

Register a `LogCollector` on the host's logging pipeline, run the host,
then verify the collector. The collector is the test-owned capture
destination: it sees every category the host logs to, regardless of which
`ILogger<T>` produced the entry. See
[`Compono.Logging`'s Package Guide](../packages/compono-logging.md#host-wide-capture-with-logcollector-and-addcomponocapture)
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

`AtLevelOrAbove(LogLevel.Error)` matches `Error` and `Critical`;
`LogLevel.None` never matches. `Never()` fails the test with a message
naming the filter and how many entries matched:

```text
Expected exactly 0 call(s) to a log entry matching level Error or above, but received 1.
```

The message doesn't list the offending entries. To see them, read the
collector — for example
`logs.GetCapturedEntries().Where(entry => entry.LogLevel >= LogLevel.Error)`
— in the failing test or a debugger.

## Narrow it to one category

Add `InCategory` when only one subsystem should be checked. The match is
an exact, ordinal, case-sensitive comparison of the entry's `Category`:

```csharp
logs.Verify()
    .AtLevelOrAbove(LogLevel.Error)
    .InCategory("MyApp.Payments.PaymentGateway")
    .Never();
```

For a host entry, `Category` is exactly the string the logging pipeline
passed to `ILoggerProvider.CreateLogger` — for an `ILogger<PaymentGateway>`,
the type's full name (`MyApp.Payments.PaymentGateway`), not its namespace
or a short name.

`InCategory` never matches by prefix, unlike the host's own `AddFilter`
and `Logging:LogLevel:*` rules. A category string that matches nothing
makes `Never()` pass without checking anything. To cover a whole
namespace, match the prefix explicitly:

```csharp
logs.Verify()
    .AtLevelOrAbove(LogLevel.Error)
    .Matching(entry => entry.Category.StartsWith("MyApp.Payments.", StringComparison.Ordinal))
    .Never();
```

## What the host may not capture

Compono never changes the host's own filter rules. The default host
minimum level is `Information`, so `Debug` and `Trace` entries never reach
the collector unless the host is configured to allow them:

```csharp
    builder.Logging.SetMinimumLevel(LogLevel.Trace);
```

If the host filters a category out with `AddFilter`, that category is
absent from the collector too. To filter only what the collector sees, use
the provider's `Compono` alias in configuration, for example
`Logging:Compono:LogLevel:Default`. This is intentional: the collector reports
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

A factory shared across tests (an xUnit class or collection fixture)
shares one collector, and the collector keeps every entry with no size
limit. An error logged by one test would then fail another test's
`Never()`. Clear it at the start of each test:

```csharp
public sealed class CheckoutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Checkout_LogsNothingAtErrorOrAbove()
    {
        factory.Logs.ClearCapturedEntries();

        var client = factory.CreateClient();
        await client.PostAsync("/checkout", null, TestContext.Current.CancellationToken);

        factory.Logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();
    }
}
```

## When the collector stays empty

If the application replaces the host's `ILoggerFactory` with one that
ignores registered providers — for example Serilog's `UseSerilog()`
without `writeToProviders: true` — or calls `ClearProviders()` *after* the
test registered the collector, nothing reaches the collector and `Never()`
passes without checking anything. Guard against that by also asserting
something you know the host logs:

```csharp
logs.Verify().InCategory("Microsoft.Hosting.Lifetime").AtLeast(1);
```

## See also

- [Host-wide capture](../packages/compono-logging.md#host-wide-capture-with-logcollector-and-addcomponocapture)
  — Category, filters, ownership, and cardinality rules.
- [Share a Value Across a Test](share-a-value-across-a-test.md) — when a
  service under test needs a specific `ILogger<T>` rather than host-wide
  capture.
