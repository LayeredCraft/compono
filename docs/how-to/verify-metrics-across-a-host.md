# How Do I Verify Metrics Recorded by a Service in a Generic Host?

Create the `CapturingMeterFactory`, register it as the host's
`IMeterFactory` singleton, run the host, then verify that factory. The
factory is the test-owned capture destination: it sees every measurement
recorded through the `IMeterFactory` its services resolve. See
[`Compono.Metrics`'s Package Guide](../packages/compono-metrics.md) for the
full capture, selection, and disposal semantics.

## Basic case

```csharp
using Compono.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.Metrics;

[Fact]
public async Task Checkout_RecordsAnOrder()
{
    using var meters = new CapturingMeterFactory();

    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddSingleton<IMeterFactory>(meters);   // last IMeterFactory registration wins
    builder.Services.AddSingleton<OrderService>();
    using var host = builder.Build();

    await host.StartAsync(TestContext.Current.CancellationToken);

    var service = host.Services.GetRequiredService<OrderService>();
    service.PlaceOrder(new Order("eu", 3));

    await host.StopAsync(TestContext.Current.CancellationToken);

    meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").Once();
}
```

`Host.CreateApplicationBuilder()` already registers a default
`IMeterFactory` as part of its built-in service setup, so `IMeterFactory` has
two registrations once you add yours. The container resolves a **single**
`IMeterFactory` to its **last** registration, so registering your instance
after the builder is created means the host resolves `meters`. Do **not** use
`TryAddSingleton` to install it: `TryAdd` keeps whatever is registered first,
so it would leave the default factory in place and silently discard yours.
The test creates the factory, so the test owns it —
the container does not dispose an instance registration. Dispose the
factory (here, via `using var` at the end of the test) **after** the host
has stopped: a measurement recorded after disposal isn't captured.

## Keep the factory for the whole test

Captured measurements stay readable after the factory is disposed, and
`ClearMeasurements()` keeps working, so you can assert after disposal:

```csharp
await host.StopAsync(TestContext.Current.CancellationToken);
meters.Dispose();

meters.Verify<long>("shop.orders.placed").Once();   // still valid
```

Reads (`GetMeasurements<T>`, `Verify<T>`) never record anything and have no
side effects, so asserting never perturbs capture.

## Observable instruments

Observable instruments are only recorded when polled. Call
`RecordObservableInstruments()` after the code under test has run and before
asserting:

```csharp
service.PlaceOrder(new Order("eu", 3));

meters.RecordObservableInstruments();
meters.Verify<int>("shop.queue.depth").WithValue(7).Once();
```

Each call records one observation per observable instrument; calling it
twice records twice. Before the first `Create`, or after disposal, it is a
no-op.

## Background and asynchronous work

Measurements recorded on other threads — a hosted service, a background
task — are captured thread-safely as they arrive. Compono provides no wait
or timeout API, so the test must await its own completion signal before
asserting. There is no chronological ordering guarantee across concurrent
producers; if a test needs a cross-thread order, establish it itself.

## What the host may not capture

`UseMetrics()` and `CapturingMeterFactory` don't change how the host
resolves services. A service that captures its own `Meter` through
`new Meter(...)` (rather than through the injected `IMeterFactory`) is not
scoped to this factory, so its measurements are never captured — its
`Meter.Scope` isn't the factory. Give the service the `IMeterFactory` and
let it create its meters from that, which is the shape this package targets.

## See also

- [`Compono.Metrics` Package Guide](../packages/compono-metrics.md) —
  selection rules, matching, disposal patterns, and snapshot lifetime.
- [Share a Value Across a Test](share-a-value-across-a-test.md) — when a
  composed service needs the same factory the test asserts against, without
  a host.
- [Troubleshooting: metrics](../troubleshooting/common-errors.md#verify-metrics-on-a-non-compono-imeterfactory-throws)
  — the misuse and selection diagnostics.
