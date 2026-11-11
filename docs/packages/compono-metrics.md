# Compono.Metrics

First-class `System.Diagnostics.Metrics` testing support. `IMeterFactory`
composes naturally through `UseMetrics()`, backed by a hand-written
`CapturingMeterFactory` (`System.Diagnostics.Metrics` is in-box on every
target framework — there is no third-party dependency and no
`Microsoft.Extensions.Diagnostics.Testing`). A composed factory captures
every measurement of every instrument a SUT creates — counter,
up/down-counter, histogram, gauge, and observable instruments when polled
— and exposes Compono-native typed reads and `Verify<T>()` verification
reusing core `Compono`'s `CallVerifier` unmodified. See
[ADR-0072](../adr/0072-compono-metrics-testing-support-package.md) for the
full decision record and
[RESEARCH-0039](../research/0039-issue-165-metrics-testing-admission.md)
for the design investigation and the empirical BCL facts this package
relies on.

## When to install

Your code under test takes an `IMeterFactory` constructor dependency and
records measurements, and the test wants to assert *what* was recorded —
instrument names, values, and tags — not substitute an unrelated
application interface or hand-write a factory plus one
`MetricCollector<T>` per instrument:

```bash
dotnet add package Compono
dotnet add package Compono.Metrics
```

This package depends only on `Compono`. `System.Diagnostics.Metrics`
(`IMeterFactory`, `Meter`, `MeterListener`, `Counter<T>`, and so on) is
part of the shared framework on every supported target framework, so no
`PackageReference` is added — not `System.Diagnostics.DiagnosticSource`,
not `Microsoft.Extensions.Diagnostics`, not
`Microsoft.Extensions.Diagnostics.Testing`, not
`Microsoft.Extensions.DependencyInjection.Abstractions`.

**No MSBuild opt-in.** Unlike `Compono.TestDoubles`, there is nothing to
enable — `Compono.Metrics` ships no generator, analyzer, or props asset.
`UseMetrics()` works as soon as the package is referenced.

## What it gives you

```csharp
using Compono;
using Compono.Metrics;
using System.Diagnostics.Metrics;

var composer = Composer.Create(builder => builder.UseMetrics());
var service = composer.Create<OrderService>();   // OrderService(IMeterFactory meters, ...)
```

```csharp
public sealed class MetricsProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.UseMetrics().Share<IMeterFactory>();
}

[Theory]
[Compose<MetricsProfile>]
public void PlacingAnOrderCountsIt([Shared] IMeterFactory meters, OrderService sut)
{
    using var _ = meters;   // the test owns disposal; see "Disposal patterns"

    sut.PlaceOrder(new Order("eu", 3));

    meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();
    meters.Verify<double>("shop.orders.duration").Once();               // histogram: one recording
}
```

```csharp
var placed = meters.GetMeasurements<long>("Shop.Orders", "shop.orders.placed");
Assert.Equal(1, placed.Sum(m => m.Value));                              // value math via LINQ
```

- **`UseMetrics()`** — registers a stage-6 test-double provider so any
  `IMeterFactory` request composes as a new `CapturingMeterFactory`.
- **`meters.GetMeasurements<T>(instrumentName)`** /
  **`GetMeasurements<T>(meterName, instrumentName)`** — a snapshot list of
  the matching measurements. Direct inspection, no assertion framework
  required. `T` is one of `byte`, `short`, `int`, `long`, `float`,
  `double`, or `decimal`.
- **`meters.Verify<T>(...)`** — the fluent entry point: `.WithValue(value)`,
  `.WithTag(key, value)`, `.WithoutTag(key)`, `.Matching(predicate)`,
  ending in `.Once()` / `.Never()` / `.Exactly(n)` / `.AtLeast(n)` /
  `.AtMost(n)` — the exact same single-verb vocabulary
  `Compono.TestDoubles`/`Compono.Http`/`Compono.Logging` already use
  (`repository.Verify().Save().Once()`, `logger.Verify().AtLevel(...).Once()`),
  each a thin, one-line forward through the same shared count-verification
  semantics core `Compono`'s `CallVerifier` implements. The filter chain
  narrows *which* measurements count, then the terminal counts them exactly
  the same way regardless of which package it's reached through.
- **`meters.RecordObservableInstruments()`** — polls this factory's
  observable instruments. Reads never poll; see "Observable instruments."
- **`meters.ClearMeasurements()`** — empties the capture buffer. Order is
  otherwise capture order; see "Capture order."
- **`CapturingMeterFactory`** — hand-written and publicly, directly
  constructible with no composition involved at all:
  ```csharp
  using var meters = new CapturingMeterFactory();
  var sut = new OrderService(meters);
  ```
  A `UseMetrics()`-composed factory and a directly-constructed one behave
  identically — one implementation, not two.

## Factory semantics

`CapturingMeterFactory` has parity with the BCL's own `DefaultMeterFactory`:

- `Create(null)` throws `ArgumentNullException`. `Create` with a non-null
  `options.Scope` that isn't this factory throws `InvalidOperationException`.
  `Create` after `Dispose()` throws `ObjectDisposedException`.
- The caller's `MeterOptions` is never mutated. The meter is constructed
  from a copy whose `Scope` is this factory, so `meter.Scope` is
  reference-equal to the factory.
- Meters are cached: the same name, version, and tag set (order-insensitive,
  `object.Equals` on values) returns the **same** `Meter`. Anything else
  creates a new one.
- Thread-safe.

## Capture semantics

- **Listener.** Each factory owns one `MeterListener`, created on its first
  `Create` and started before the meter is returned. It enables an
  instrument only when `ReferenceEquals(instrument.Meter.Scope, this)`, so
  two factories never see each other's measurements even for identical
  meter and instrument names. There is no static or shared capture state.
  A factory that never creates a meter never creates a listener.
- **What's captured.** Every measurement of every enabled instrument kind:
  `Counter<T>`, `UpDownCounter<T>`, `Histogram<T>`, `Gauge<T>` (`net9.0+`),
  and observable instruments when polled. Raw measurements only — Compono
  doesn't aggregate, doesn't interpret kind (no delta-vs-last-value
  semantics), and doesn't read histogram bucket advice.
- **No timestamp.** Order is capture order (see "Capture order").
- **Isolation.** Two factories never see each other's measurements.

## Selection and matching

`GetMeasurements<T>` and `Verify<T>` resolve their selection with the same
algorithm, in this order. The first rule that applies decides the outcome.
`Verify<T>` resolves it when the builder is created, so a conflict throws
before any filter is added.

1. **Arguments.** A null or empty `instrumentName`/`meterName` throws
   `ArgumentException`. A `T` that isn't one of the seven BCL measurement
   types throws `ArgumentException` whether or not any instrument exists;
   the message names the requested type and lists the seven supported types.
2. **Candidates.** Every instrument this factory has published whose `Name`
   equals `instrumentName` and, with the two-argument overload, whose
   `Meter.Name` equals `meterName`. Both comparisons are ordinal and
   case-sensitive. The candidate set includes instruments of **every** value
   type, not just `T`.
3. **Unknown name.** No candidates yields an empty result.
   `Verify(...).Never()` passes. A failing `Once()`/`Exactly(n)`/`AtLeast(n)`
   message lists the instruments that do exist (meter name, instrument name,
   value type), so a typo is visible.
4. **Meter ambiguity** (one-argument overload only). Candidates from meters
   with different `Meter.Name` values throw `InvalidOperationException`. The
   message lists each candidate and points at the two-argument overload.
5. **Type conflict.** Candidates that record more than one value type throw
   `InvalidOperationException`, **even when one of the types is `T`**. This
   can happen inside one meter (`CreateCounter<int>("x")` and
   `CreateCounter<long>("x")` are distinct instruments) or across same-named
   meters with different versions or tags.
6. **Type mismatch.** Candidates that all record one value type other than
   `T` throw `InvalidOperationException` naming that type, for example
   `instrument "shop.orders.placed" records int, not long`.
7. **Merge.** Otherwise every candidate records `T`. Their measurements are
   merged in capture order.

Matching filters:

- **`WithValue(value)`** uses `EqualityComparer<T>.Default`. Floating-point
  equality is exact; use `Matching(...)` for tolerance.
- **`WithTag(key, value)`** matches a measurement that contains a tag with
  that key (ordinal) whose value satisfies `object.Equals(tagValue, value)`.
  Other tags are ignored. There is no numeric coercion: `200` (`int`) does
  **not** match `200L`. Chain calls for several tags.
- **`WithoutTag(key)`** matches a measurement with no tag with that key.
- **`Matching(predicate)`** covers exact tag-set and any other condition.

Failure messages follow `LogVerificationBuilder`'s style: the filters are
joined with `with`/`and`, and the count forwards to core `CallVerifier`'s
`Expected exactly N call(s) to ..., but received M.` wording. Numeric values
are formatted with the invariant culture. The message describes the filter
you asked for. A string tag value is shown quoted (`region = "eu"`); any
other value shows the runtime type of the value you passed to `WithTag` (for
example `200` renders as `200 (int)`), so it always matches the expectation
and never annotates the measurement's actual tag. For example:

```text
Expected exactly 1 call(s) to instrument "shop.orders.placed" (long) with tag region = "eu" and value 1, but received 0.
```

```text
Expected exactly 1 call(s) to instrument "shop.orders.placed" (long) with tag status = 200 (int), but received 0.
```

When nothing matched an unknown instrument name, the message names the
instrument that doesn't exist and lists the ones that do:

```text
Expected exactly 1 call(s) to instrument "shop.orders.placed" (long), but received 0. No instrument named "shop.orders.placed" has been published by this factory. Existing instruments: meter "Shop.Orders": instrument "orders.placed" (long).
```

These exceptions are `InvalidOperationException`/`ArgumentException` with
descriptive messages; no new public exception type is added.

## Observable instruments

- `RecordObservableInstruments()` calls this factory's listener's
  `RecordObservableInstruments()`. Callbacks run synchronously on the
  calling thread and only for this factory's observable instruments. Each
  observation appends measurements. Calling it twice records twice.
- Reads (`GetMeasurements`, `Verify`) never poll and have no side effects.
- If a callback throws, the BCL behavior stands: the other callbacks still
  run, then `AggregateException` propagates to the test. Compono doesn't
  catch it.
- Before the first `Create`, or after disposal, it is a no-op.

```csharp
meters.RecordObservableInstruments();
meters.Verify<int>("shop.queue.depth").WithValue(7).Once();
```

## Asynchronous measurements

Measurements recorded on other threads (background work, hosted services)
are captured thread-safely as they arrive. Reads are point-in-time
snapshots. Compono provides no wait or timeout API — the test awaits its
own completion signal before asserting.

## Disposal patterns

No ownership or disposal API is added. Composition is non-owning
(`UseMetrics()` composes a new factory per request and never disposes it).
`IMeterFactory` already extends `IDisposable`, and `Dispose()` disposes
every meter the factory created, then the listener. It is idempotent and
thread-safe, so a test framework that also disposes the argument is
harmless. The documented patterns:

1. **`[Compose]`-family tests: dispose the `[Shared]` parameter in the test
   body.**

   ```csharp
   [Theory, Compose<MetricsProfile>]
   public void PlacingAnOrderCountsIt([Shared] IMeterFactory meters, OrderService sut)
   {
       using var _ = meters;
       sut.PlaceOrder(new Order("eu", 3));
       meters.Verify<long>("shop.orders.placed").Once();
   }
   ```

   `[Shared]` guarantees the parameter is the same instance the SUT holds,
   so disposing it disposes everything the SUT created through it. The
   `using` declaration runs when the method exits, including when an
   assertion throws. Assertions can go anywhere in the body because
   captured data stays readable after disposal. In an `async` test,
   `using var` works the same way and runs after the last `await`.
   `IMeterFactory` isn't `IAsyncDisposable`, so `await using` doesn't apply.

2. **`Composer.Create` tests: construct the factory, then register it.**

   ```csharp
   using var meters = new CapturingMeterFactory();
   var composer = Composer.Create(b => b.Register<IMeterFactory>(() => meters));
   var sut = composer.Create<OrderService>();
   ```

   An exact registration takes precedence over `UseMetrics()`, so
   `UseMetrics()` isn't needed here. The test creates the instance, so it
   owns it.

3. **Generic Host / DI:** the same, registered as a singleton instance.

   ```csharp
   using var meters = new CapturingMeterFactory();
   var builder = Host.CreateApplicationBuilder();
   builder.Services.AddSingleton<IMeterFactory>(meters);   // last IMeterFactory registration wins
   builder.Services.AddSingleton<OrderService>();
   using var host = builder.Build();

   // ... start the host, call the code under test ...

   meters.Verify<long>("shop.orders.placed").Once();
   ```

   `Host.CreateApplicationBuilder()` already registers a default
   `IMeterFactory` as part of its built-in service setup, so `IMeterFactory`
   has two registrations once you add yours. The container resolves a
   **single** `IMeterFactory` to its **last** registration, so the host
   resolves `meters`. Don't use `TryAddSingleton` to install it: `TryAdd`
   keeps whatever is registered first, so it would leave the default factory
   in place and discard yours. An instance registration isn't disposed by the
   container, so the test's `using` owns it. Dispose the factory after the
   host has stopped, so measurements recorded during host shutdown are still
   captured.

Dispose after the SUT's work is done. Measurements recorded after disposal
aren't captured, so disposing while background work is still running can
drop measurements. Consequence of skipping disposal: an undisposed factory
that created meters stays referenced by the BCL's static meter list for the
process lifetime — the same cost as an undisposed `DefaultMeterFactory`. A
factory that never created a meter costs nothing.

## Snapshot lifetime

- `GetMeasurements<T>` returns a new list each call. Later captures,
  `ClearMeasurements()`, and `Dispose()` never change a list already
  returned.
- Each `Tags` list is a per-measurement copy taken in the callback; it never
  aliases the BCL's tag span or another measurement's tags. A measurement
  with no tags yields `Tags` that is empty, never null.
- `CapturedMeasurement<T>.Instrument` is the actual `Instrument` object the
  SUT created. Through `Instrument.Meter` it references the actual `Meter`,
  and through `Meter.Scope` the factory. That's ordinary object
  reachability, not a registration; it causes no capture.
- Snapshots stay valid after the factory is disposed. `Value`, `Tags`, and
  `Instrument`'s descriptive members remain readable; a disposed meter's
  instruments report `Enabled == false`, and recording through them is a
  no-op. Reading a snapshot never throws because of disposal.

## Capture order

- Capture order is the order in which measurements are successfully inserted
  into the factory's synchronized capture buffer.
- Each measurement gets a factory-wide sequence number, assigned inside the
  same lock acquisition that appends it. Merged selections and every
  snapshot are ordered by that sequence number.
- Measurements recorded sequentially on one thread are captured in that
  thread's program order. One `RecordObservableInstruments()` call appends
  in the order the BCL invokes the callbacks and their observations.
- **No chronological ordering is claimed across concurrent producers.** Two
  threads recording at about the same time may be captured in either order.
- `ClearMeasurements()` doesn't reset the sequence. Order is only meaningful
  within one factory.
- One private lock per factory guards appends, clears, and reads; the tag
  copy happens before the lock is taken.

## Registration order and misuse

- `UseMetrics()` registers a stage-6 provider that handles exactly
  `typeof(IMeterFactory)` and returns `NotHandled` for everything else.
  Precedence against `UseNSubstitute()`/`UseGeneratedTestDoubles()` follows
  the existing first-registered-wins rule
  ([ADR-0024](../adr/0024-public-provider-extensibility-model.md)).
  Register `UseMetrics()` first:
  ```csharp
  builder.UseMetrics().UseNSubstitute();   // IMeterFactory -> CapturingMeterFactory
  builder.UseNSubstitute().UseMetrics();   // IMeterFactory -> a substitute instead
  ```
- Every `MeterFactoryTestingExtensions` method throws
  `InvalidOperationException` when the `IMeterFactory` isn't a
  `CapturingMeterFactory`. The message names `UseMetrics()`, the
  registration-order requirement, and the host recipe. It's detected by a
  type check, not reflection.
- `Meter` constructor parameters are not composed by this package. Consumers
  that want a specific `Meter` register it from the shared factory in a
  profile.

## Native AOT and trimming

No reflection, no `MakeGenericType`, no dynamic code, no generated
activation. `IsAotCompatible=true`, so the library builds with the trim/AOT
analyzers on every target framework. The `Compono.Metrics.AotSmokeTest`
project publishes with `PublishAot=true` for `linux-x64` on `net10.0` in CI,
matching the existing smoke matrix.

## v1 non-goals

Deliberately not in this package — see ADR-0072's Decision Outcome for the
rationale behind each:

- A metrics framework, aggregation engine, exporter, or OpenTelemetry
  integration.
- Tracing (`ActivitySource`) testing.
- Composing `Meter` parameters, or a configurable meter name.
- An `IServiceCollection`/host extension (documentation recipe instead).
- Timestamps, `TimeProvider`, and wait/timeout APIs.
- Aggregate helpers (`Sum`, `LastValue`) and numeric tag coercion.
- Automatic disposal by composition.
- Version- or tag-level meter selection. Same-named instruments with
  conflicting value types in same-named meters can't be read at all; use
  `Matching(m => m.Instrument.Meter.Version == "2")` after the types agree.
- Replacing `Microsoft.Extensions.Diagnostics.Testing` for consumers who
  prefer it.

## Next

- [Basic Usage sample](../samples/basic-usage.md) (`MetricsTests`) — a
  concise `UseMetrics()`/`Verify<T>()` scenario in this repo's single clearest
  reference project.
- [Verify Metrics Across a Host](../how-to/verify-metrics-across-a-host.md)
  — the Generic Host recipe.
- [`Compono.XunitV3`](compono-xunitv3.md)/[`Compono.TUnit`](compono-tunit.md)
  — `[Compose]`/`[Shared]` used throughout the examples above.
- [`Compono.Logging`](compono-logging.md) — the same "composed double is the
  handle, `Verify()` forwards to `CallVerifier`" idiom for logging.
- [ADR-0072](../adr/0072-compono-metrics-testing-support-package.md) — the
  full decision record.
