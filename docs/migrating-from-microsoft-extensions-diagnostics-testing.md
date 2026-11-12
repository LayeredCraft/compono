---
title: Migrating from Microsoft.Extensions.Diagnostics.Testing
description: Map AddFakeLogging, FakeLogCollector, FakeLogger<T>, and MetricCollector<T> onto Compono.Logging's LogCollector/AddComponoCapture and Compono.Metrics' CapturingMeterFactory/UseMetrics.
---

# Migrating from Microsoft.Extensions.Diagnostics.Testing

`Microsoft.Extensions.Diagnostics.Testing` provides fake logging
(`AddFakeLogging`, `FakeLogCollector`, `FakeLogger<T>`) and metric
collection (`MetricCollector<T>`). [`Compono.Logging`](packages/compono-logging.md)
and [`Compono.Metrics`](packages/compono-metrics.md) cover the same ground
with composition built in, and neither depends on that package. You don't
have to migrate: if you prefer it, keep it.

Every Compono ("After") example below comes from a compiled, running test:

- Logging: `MigrationGuideExamplesTests` in `test/Compono.Logging.Tests`.
  The short category-filter lines are covered by `CategoryPrefixFilterTests`
  in the same project.
- Metrics `[Compose<MetricsProfile>]` example: `MigrationGuideMetricsTests` in
  `samples/Compono.Samples.BasicUsage`.
- Metrics host and `ClearMeasurements()` examples: `MigrationGuideExamplesTests`
  in `test/Compono.Metrics.Tests`.

The "Before" snippets show the `Microsoft.Extensions.Diagnostics.Testing`
code being replaced. They're for comparison and aren't compiled in this
repository.

## At a glance

| `Microsoft.Extensions.Diagnostics.Testing` | Compono |
|---|---|
| `builder.Logging.AddFakeLogging()` | `builder.Logging.AddComponoCapture(logs)` with a test-owned `var logs = new LogCollector();` |
| `host.Services.GetFakeLogCollector()` | The `LogCollector` instance you created. Nothing to resolve |
| `collector.GetSnapshot()` / `LatestRecord` / `Count` | `logs.GetCapturedEntries()` / `GetLastCapturedEntry()` / `GetCapturedEntries().Count` |
| `collector.Clear()` | `logs.ClearCapturedEntries()` |
| `FakeLogRecord.Level` / `Id` / `Category` / `Message` / `Exception` / `Scopes` | `CapturedLogEntry.LogLevel` / `EventId` / `Category` / `Message` / `Exception` / `Scopes` |
| LINQ over the snapshot, then assert | `logs.Verify().AtLevelOrAbove(...).InCategory(...).Never()` (or `.Once()`, `.Exactly(n)`, …) |
| `new FakeLogger<T>()` | A composed `ILogger<T>` under `UseLogging()`, or `new CapturingLogger<T>()` |
| A hand-written test `IMeterFactory` | `UseMetrics()` (composed), or `new CapturingMeterFactory()` |
| `new MetricCollector<T>(meterFactory, meterName, instrumentName)` | Nothing. The factory captures every instrument it creates |
| `collector.GetMeasurementSnapshot()` | `meters.GetMeasurements<T>(meterName, instrumentName)` |
| `collector.LastMeasurement.Value` / `.Tags["k"]` | `meters.Verify<T>(...).WithValue(v).WithTag("k", value).Once()` |
| `collector.RecordObservableInstruments()` | `meters.RecordObservableInstruments()` |
| `collector.Clear()` | `meters.ClearMeasurements()` |
| `collector.WaitForMeasurementsAsync(n)` | No equivalent. Synchronize on the SUT's own completion first |

## Logging

### Host-wide capture (`AddFakeLogging` → `AddComponoCapture`)

```csharp
// Before
builder.Logging.AddFakeLogging();
var logs = host.Services.GetFakeLogCollector();
Assert.DoesNotContain(logs.GetSnapshot(), r => r.Level >= LogLevel.Error);

// After
var logs = new LogCollector();
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddComponoCapture(logs);
builder.Services.AddHostedService<CheckoutWorker>();
using var host = builder.Build();

await host.StartAsync(cancellationToken);
await host.StopAsync(cancellationToken);

logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();
logs.Verify()
    .InCategory("Compono.Logging.Tests.CheckoutWorker")
    .WithMessageContaining("Checkout started for 42")
    .Once();
```

The collector is test-owned. You create it before the host and pass it in,
so there's nothing to resolve from the container afterwards. Assert only after
the code under test has actually logged. `CheckoutWorker` here is an
`IHostedService` that logs in `StartAsync`. A `BackgroundService` runs
`ExecuteAsync` on a background task (since .NET 10), so stopping the host
straight after `StartAsync` can cancel it before it logs. Await the worker's
own completion signal first. Host filter
rules still apply exactly as they do for any other provider. See
[Assert Nothing Was Logged at Error Across a Host](how-to/assert-nothing-logged-at-error-across-a-host.md).

### Category filters

`Category` is the exact string the logging pipeline passes to
`ILoggerProvider.CreateLogger`. For an `ILogger<T>` that's `T`'s full name,
with nested types joined by `.`. Matching is ordinal and case-sensitive:

```csharp
logs.Verify().InCategory("MyApp.Jobs.Scheduler").Once();                        // exact
logs.Verify().AtLevelOrAbove(LogLevel.Error).InCategoryPrefix("MyApp.Jobs.").Never();
logs.Verify().AtLevelOrAbove(LogLevel.Error).NotInCategoryPrefix("MyApp.Jobs.").Never(); // "nothing outside my code"
```

Prefixes are literal. End one with `.` so `MyApp.Jobs.` doesn't also match
`MyApp.JobsLegacy`.

### Inspecting and resetting

```csharp
var entries = logs.GetCapturedEntries();        // GetSnapshot()
var last = logs.GetLastCapturedEntry();         // LatestRecord
logs.ClearCapturedEntries();                    // Clear()
```

### A single logger (`FakeLogger<T>` → composed `ILogger<T>`)

```csharp
var composer = Composer.Create(builder => builder.UseLogging());
var logger = composer.Create<ILogger<CheckoutWorker>>();

logger.LogWarning("Payment retry {Attempt}", 2);

logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("Payment retry 2").Once();
```

In a `[Compose]` theory, take `[Shared] ILogger<T>` alongside the SUT to get
the SUT's own logger.

## Metrics

### Composed factory (`MetricCollector<T>` → `UseMetrics()`)

```csharp
// Before
using var meterFactory = new TestMeterFactory();          // hand-written
using var collector = new MetricCollector<long>(meterFactory, "Shop.Orders", "shop.orders.placed");
var sut = new OrderService(meterFactory);
sut.PlaceOrder("eu", 2);
Assert.Equal(1, collector.LastMeasurement!.Value);
Assert.Equal("eu", collector.LastMeasurement.Tags["region"]);

// After
[Theory, Compose<MetricsProfile>]                         // MetricsProfile: builder.UseMetrics()
public void PlacingAnOrderCountsIt([Shared] IMeterFactory meters, OrderService sut)
{
    sut.PlaceOrder("eu", 2);

    meters.Verify<long>("Shop.Orders", "shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();
    var placed = meters.GetMeasurements<long>("Shop.Orders", "shop.orders.placed");   // GetMeasurementSnapshot()
    Assert.Single(placed);
}
```

You don't create a collector per instrument. The factory captures every
instrument its meters publish. `[Shared]` makes the parameter the same
factory the SUT received. `CapturedMeasurement<T>.Tags` is an ordered list
of key/value pairs, not a dictionary, so use `WithTag`/`WithoutTag` to
filter.

### Generic Host

```csharp
using var meters = new CapturingMeterFactory();
var builder = Host.CreateApplicationBuilder();
builder.Services.AddSingleton<IMeterFactory>(meters);   // after the host's default: the last registration wins
builder.Services.AddSingleton<OutboxProcessor>();
using var host = builder.Build();

await host.StartAsync(cancellationToken);
host.Services.GetRequiredService<OutboxProcessor>().RunOnce();
await host.StopAsync(cancellationToken);

meters.Verify<long>("outbox.published").WithTag("queue", "orders").Once();
```

Don't use `TryAddSingleton`. It keeps the host's default factory. See
[Verify Metrics Across a Host](how-to/verify-metrics-across-a-host.md).

### Asserting on one pass (`collector.Clear()` → `ClearMeasurements()`)

```csharp
sut.RunOnce();
sut.RunOnce();

meters.ClearMeasurements();     // only the pass under test counts from here
sut.RunOnce();

meters.Verify<long>("outbox.published").WithValue(1).Once();
```

## Registration order

- **Stage-6 composition order.** `UseMetrics()` and `UseLogging()` are
  test-double providers, so they must come **before** `UseNSubstitute()` or
  any other catch-all provider. Otherwise a substitute takes
  `IMeterFactory`/`ILogger<T>`. Put them first in a shared base profile.
  Order against `UseGeneratedTestDoubles()` doesn't matter: both packages own
  their abstractions by default (`ComponoMetricsOwnsMeterFactory`,
  `ComponoGeneratedLogging`), so no generated double competes for them.
- **Host DI order.** A test `IMeterFactory` registered with
  `AddSingleton` must come after anything else that registers one. The
  container resolves a single service to its last registration.
  `AddComponoCapture` adds a logging provider alongside the others.
  `ClearProviders()` first if you want only Compono's capture.

## Disposal

- `LogCollector` holds no unmanaged state, so there's nothing to dispose.
- `IMeterFactory` is `IDisposable`. A factory you construct
  (`new CapturingMeterFactory()`, host recipe) is yours: `using var meters`.
  Dispose it after the host has stopped.
- A composed `[Shared] IMeterFactory` parameter: on `Compono.XunitV3`, xUnit
  disposes test-method arguments itself, so `using var _ = meters;` is
  optional. On `Compono.XunitV3.Aot` it's required. TUnit disposes composed
  root arguments according to
  [`Compono.TUnit` — Disposal](packages/compono-tunit.md#disposal)
  (ADR-0040; documented, not re-tested for this guide). NUnit and MSTest
  aren't verified, so keep it there.
  Compono never disposes composed values. See
  [Compono.Metrics — Disposal patterns](packages/compono-metrics.md#disposal-patterns).

## What has no direct equivalent

- **Waiting for measurements** (`WaitForMeasurementsAsync`) isn't provided.
  Await the SUT's own completion signal before asserting.
- **`FakeLogCollectorOptions` filtering** (by category or level at capture
  time). Capture everything the host's filters let through, and filter in
  `Verify()` instead.
- **Redaction and enrichment fakes** from the same package are out of scope
  for Compono. Keep using them if you need them.
