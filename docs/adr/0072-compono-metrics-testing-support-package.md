# [ADR-0072] `Compono.Metrics`: `System.Diagnostics.Metrics` Testing Support

**Status:** Accepted

**Date:** 2026-11-11

**Decision Makers:** product owner (maintainer interview, 2026-11-11), assisted by Claude Opus 5.5

## Context

Issue [#165](https://github.com/LayeredCraft/compono/issues/165) (child of
[#161](https://github.com/LayeredCraft/compono/issues/161), item 2): a SUT
takes `IMeterFactory` and records counter, histogram, and gauge
measurements. Tests need to compose that SUT and assert the measurements'
values, instrument names, and tags. Today Compono has no metrics support.
Consumers hand-write an `IMeterFactory` and wire one
`MetricCollector<T>` (`Microsoft.Extensions.Diagnostics.Testing`) per
instrument.

[RESEARCH-0039](../research/0039-issue-165-metrics-testing-admission.md)
ran capability admission. Gates A and B are met, and the product owner
chose a new package over a documentation-only recipe. It also records the
empirical BCL facts this ADR relies on (spike on SDK 11.0.100, `net8.0`
and `net11.0`). In short:

- Everything needed is in-box on every target TFM. No `PackageReference`.
- A per-factory `MeterListener` that enables only instruments whose
  `Meter.Scope` is that factory isolates same-named meters across factories,
  and its `RecordObservableInstruments()` polls only that factory's
  observable instruments.
- `Gauge<T>` exists on `net9.0+` only, but a generic measurement callback
  receives it without naming the type.
- `services.AddMetrics()` registers its factory with `TryAdd`.
- `DefaultMeterFactory` caches meters, doesn't mutate the caller's
  options, rejects a foreign scope, and throws `ObjectDisposedException`
  after disposal.

## Decision Drivers

- Real consumer scenario (#161 migration, `dynamodb-distributed-lock`)
  with the smallest maintainable public surface.
- No new dependency. No reflection, no generator work, Native AOT and
  trimming compatible (`IsAotCompatible=true`).
- Deterministic results: parallel tests never see each other's
  measurements, and reads have no side effects.
- Compono's non-owning composition model (ADR-0022 Amendment 4,
  RESEARCH-0015). No ownership ADR is required.
- No static or ambient capture state.
- Consistency with `Compono.Logging` (ADR-0055) where it fits: composed
  double is the handle, `[Shared]` reaches it, extension methods throw a
  diagnostic on a non-Compono instance, verification forwards to core
  `CallVerifier`. Different where metrics differ: values are typed and
  value-centric, and observable instruments need an explicit poll.
- Low allocation on the measurement callback path.

## Considered Options

**Axis 1, home:** (1) new `Compono.Metrics` package; (2) documentation-only
recipe (hand-written factory + `MetricCollector<T>`); (3) fold into an
existing package (`Compono.Logging`).

**Axis 2, handle:** (1) the composed `IMeterFactory` is the handle
(`[Shared] IMeterFactory`, extension methods); (2) a test-owned collector
passed to `UseMetrics(collector)` (ADR-0071 `LogCollector` style); (3) both.

**Axis 3, listener ownership:** (1) one lazily created listener per
factory, disposed with the factory; (2) one shared static listener that
routes by `Meter.Scope`, plus a transient listener for observable polls;
(3) block on a Compono disposal-ownership ADR.

**Axis 4, value typing:** (1) typed (`CapturedMeasurement<T>`); (2)
untyped (`object Value`); (3) normalized to `double`.

**Axis 5, observable instruments:** (1) explicit
`RecordObservableInstruments()`; (2) poll automatically on every read;
(3) explicit plus an opt-in auto-poll option.

**Axis 6, verification:** (1) snapshot reads plus a filter-then-count
`Verify` builder forwarding to `CallVerifier`; (2) option 1 plus
aggregate helpers (`Sum`, `LastValue`); (3) snapshot reads only.

**Axis 7, host/DI path:** (1) documentation recipe only; (2) an
`AddComponoMetrics()` `IServiceCollection` extension; (3) defer.

## Decision Outcome

Chosen per axis, all confirmed in the maintainer interview (2026-11-11):
Axis 1 option 1, Axis 2 option 1, Axis 3 option 1, Axis 4 option 1,
Axis 5 option 1, Axis 6 option 1, Axis 7 option 1. Supporting decisions
from the same interview: an unknown instrument name yields no
measurements (it doesn't throw); tag matching is subset + `object.Equals`;
no timestamp; no wait API; `Meter` constructor parameters are not
composed; stage-6 precedence follows registration order.

Settled in the product-owner review (2026-11-11): `CapturedMeasurement<T>`
is a plain `readonly struct` (not a record struct, so `==` never suggests
value equality over `Tags`); meter and instrument names match ordinal,
case-sensitive; the type names below are accepted as proposed; the
package ships in the #161 release (v1.4.0); disposal, type conflicts,
snapshot lifetime, capture order, and the Native AOT validation matrix are
specified explicitly below.

Accepted in the product-owner final review (2026-11-11), which confirmed:
unsupported measurement types are rejected with `ArgumentException`; the
same-named, conflicting-type instrument limitation is accepted for v1 with
no version or tag selectors; disposal stays explicit and test-owned, with
no ownership or disposal API; `InvalidOperationException` is used for
ambiguity, type conflicts, and misuse, with no custom exception hierarchy;
and the Native AOT validation matrix stands as documented.

### Package identity and dependency graph

```
Compono.Metrics
    -> Compono        (CompositionBuilder.AddTestDoubleProvider, ICompositionValueProvider, CallVerifier)
    (System.Diagnostics.Metrics: in-box shared framework on net8.0/net9.0/net10.0/net11.0)
```

- TFMs: `net8.0;net9.0;net10.0;net11.0`, matching the other packages.
- No `PackageReference`. Explicitly not added:
  `System.Diagnostics.DiagnosticSource` (package),
  `Microsoft.Extensions.Diagnostics`,
  `Microsoft.Extensions.Diagnostics.Testing`,
  `Microsoft.Extensions.DependencyInjection.Abstractions`. Test projects
  may reference `Microsoft.Extensions.Hosting`/`Microsoft.Extensions.Diagnostics`
  as test-only dependencies to prove the host recipe.
- No dependency on `Compono.Logging`, `Compono.TestDoubles`, or
  `Compono.NSubstitute`. No generator or MSBuild props of its own.
  `IMeterFactory` is non-generic, so the provider handles it directly.

### Public API

```csharp
namespace Compono.Metrics;

public static class CompositionBuilderExtensions
{
    // Stage-6 provider: composes IMeterFactory as a new CapturingMeterFactory per request.
    public static CompositionBuilder UseMetrics(this CompositionBuilder builder);
}

// Test IMeterFactory. Publicly constructible (CapturingLogger precedent); UseMetrics() uses the same constructor.
public sealed class CapturingMeterFactory : IMeterFactory
{
    public CapturingMeterFactory();
    public Meter Create(MeterOptions options);
    public void Dispose();
}

// Plain readonly struct (not a record struct): no synthesized value equality.
public readonly struct CapturedMeasurement<T> where T : struct
{
    public T Value { get; }
    public IReadOnlyList<KeyValuePair<string, object?>> Tags { get; }   // per-measurement copy; empty, never null
    public Instrument Instrument { get; }                              // the real instrument (and through it, its Meter); see "Snapshot lifetime"
}

public static class MeterFactoryTestingExtensions
{
    public static IReadOnlyList<CapturedMeasurement<T>> GetMeasurements<T>(
        this IMeterFactory factory, string instrumentName) where T : struct;
    public static IReadOnlyList<CapturedMeasurement<T>> GetMeasurements<T>(
        this IMeterFactory factory, string meterName, string instrumentName) where T : struct;

    public static MeasurementVerificationBuilder<T> Verify<T>(
        this IMeterFactory factory, string instrumentName) where T : struct;
    public static MeasurementVerificationBuilder<T> Verify<T>(
        this IMeterFactory factory, string meterName, string instrumentName) where T : struct;

    public static void RecordObservableInstruments(this IMeterFactory factory);
    public static void ClearMeasurements(this IMeterFactory factory);
}

public sealed class MeasurementVerificationBuilder<T> where T : struct
{
    public MeasurementVerificationBuilder<T> WithValue(T value);
    public MeasurementVerificationBuilder<T> WithTag(string key, object? value);
    public MeasurementVerificationBuilder<T> WithoutTag(string key);
    public MeasurementVerificationBuilder<T> Matching(Func<CapturedMeasurement<T>, bool> predicate);

    // One-line forwarders to core CallVerifier over the filtered measurement count.
    public void Once();
    public void Never();
    public void Exactly(int times);
    public void AtLeast(int times);
    public void AtMost(int times);
}
```

Consumer shapes:

```csharp
// Composed (any test framework). [Shared] makes the SUT and the test see the same factory.
[Theory, Compose<MetricsProfile>]
public void PlacingAnOrderCountsIt([Shared] IMeterFactory meters, OrderService sut)
{
    using var _ = meters;   // the test owns disposal; see "Disposal patterns"

    sut.PlaceOrder(new Order("eu", 3));

    meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();
    meters.Verify<double>("shop.orders.duration").Once();                       // histogram: one recording

    var placed = meters.GetMeasurements<long>("Shop.Orders", "shop.orders.placed");
    Assert.Equal(1, placed.Sum(m => m.Value));                                  // value math via LINQ
}

public sealed class MetricsProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseMetrics();
}

// Observable instruments: poll explicitly, then read.
meters.RecordObservableInstruments();
meters.Verify<int>("shop.queue.depth").WithValue(7).Once();

// Direct construction, no composition.
using var meters = new CapturingMeterFactory();
var sut = new OrderService(meters);

// Generic Host / DI (documentation recipe, consumer's own DI API; not Compono surface).
using var meters = new CapturingMeterFactory();
builder.Services.AddSingleton<IMeterFactory>(meters);   // AddMetrics() uses TryAdd, so this wins
```

### Factory semantics (`CapturingMeterFactory`)

Parity with `DefaultMeterFactory` (RESEARCH-0039 §5 facts 9-10):

- `Create(null)` throws `ArgumentNullException`. `Create` with a non-null
  `options.Scope` that isn't this factory throws
  `InvalidOperationException`. `Create` after `Dispose()` throws
  `ObjectDisposedException`.
- The caller's `MeterOptions` is never mutated. The meter is constructed
  from a copy whose `Scope` is this factory.
- Meters are cached: the same name, version, and tag set (order-insensitive,
  `object.Equals` on values) returns the same `Meter`. Anything else
  creates a new one.
- Thread-safe.

### Capture semantics

- **Listener:** each factory owns one `MeterListener`, created on its first
  `Create` and started before the meter is returned. Its
  `InstrumentPublished` enables an instrument only when
  `ReferenceEquals(instrument.Meter.Scope, this)`. Callbacks are registered
  for the seven numeric types the BCL supports (`byte`, `short`, `int`,
  `long`, `float`, `double`, `decimal`). The per-instrument state passed to
  `EnableMeasurementEvents` is a typed buffer, so measurements are never
  boxed. A factory that never creates a meter never creates a listener.
- **What's captured:** every measurement of every enabled instrument kind:
  `Counter<T>`, `UpDownCounter<T>`, `Histogram<T>`, `Gauge<T>` (`net9.0+`,
  no TFM split in the capture code), and observable instruments when
  polled. Raw measurements only. Compono doesn't aggregate, doesn't
  interpret kind (no delta-vs-last-value semantics), and doesn't read
  histogram bucket advice.
- **Tags:** the measurement's tags are copied into an array in callback
  order (`Array.Empty` when there are none). Meter-level and
  instrument-level tags are not merged in. They're reachable through
  `CapturedMeasurement<T>.Instrument`.
- **No timestamp.** Order is capture order (see "Capture order").
- **Isolation:** two factories never see each other's measurements, even
  for identical meter and instrument names. There's no static or shared
  capture state.

### Selection and matching

`GetMeasurements<T>` and `Verify<T>` resolve their selection with the
same algorithm, in this order. The first rule that applies decides the
outcome. `Verify<T>` resolves it when the builder is created, so a
conflict throws before any filter is added.

1. **Arguments.** Null or empty `instrumentName`/`meterName` throws
   `ArgumentException`. A `T` that isn't one of the seven BCL measurement
   types (`byte`, `short`, `int`, `long`, `float`, `double`, `decimal`)
   throws `ArgumentException`, whether or not any instrument exists. The
   message names the requested type and lists the seven supported types.
   No instrument can ever record such a `T`, so this catches the mistake
   deterministically.
2. **Candidates.** Every instrument this factory has published whose
   `Name` equals `instrumentName` and, with the two-argument overload,
   whose `Meter.Name` equals `meterName`. Both comparisons are ordinal and
   case-sensitive. The candidate set includes instruments of **every**
   value type, not just `T`. Instruments are never pre-filtered by `T`, so
   an instrument with an unexpected type can't be silently ignored.
3. **Unknown name.** No candidates yields an empty result.
   `Verify(...).Never()` passes. A failing `Once()`/`Exactly(n)`/
   `AtLeast(n)` message lists the instruments that do exist (meter name,
   instrument name, value type), so a typo is visible.
4. **Meter ambiguity** (one-argument overload only). Candidates from meters
   with different `Meter.Name` values throw `InvalidOperationException`.
   The message lists each candidate (meter name and version, instrument
   name, value type) and points at the two-argument overload.
5. **Type conflict.** Candidates that record more than one value type
   throw `InvalidOperationException`, **even when one of the types is
   `T`**. The message lists each candidate with its meter name and version
   and its value type, and says that one selection can't merge
   measurements of different types. This can happen inside one meter
   (`CreateCounter<int>("x")` and `CreateCounter<long>("x")` are distinct
   instruments) or across same-named meters with different versions or
   tags. A consumer resolves it by renaming an instrument in the SUT, or
   by narrowing with the two-argument overload when the conflicting
   instruments live in differently named meters. The package doesn't
   offer version- or tag-level meter selection (non-goal; use
   `Matching(m => m.Instrument.Meter.Version == "2")` after the types
   agree).
6. **Type mismatch.** Candidates that all record one value type other than
   `T` throw `InvalidOperationException` naming that type, for example
   `instrument "shop.orders.placed" records int, not long`.
7. **Merge.** Otherwise every candidate records `T`. Their measurements are
   merged in capture order. This covers same-named meters with a
   different version or tags, and repeated instances of one instrument
   name (RESEARCH-0039 fact 10).

These exceptions are `InvalidOperationException`/`ArgumentException`
with descriptive messages, matching `Compono.Logging`'s misuse diagnostics.
No new public exception type is added.
- **`WithValue(value)`:** `EqualityComparer<T>.Default`. Floating-point
  equality is exact; use `Matching(...)` for tolerance.
- **`WithTag(key, value)`:** the measurement contains a tag with that key
  (ordinal) whose value satisfies `object.Equals(tagValue, value)`. Other
  tags are ignored. No numeric coercion: `200` (`int`) doesn't match `200L`.
  The failure message shows each tag's actual runtime type. Chain calls for
  several tags.
- **`WithoutTag(key)`:** the measurement has no tag with that key.
- **Exact tag-set** and other matches: `Matching(...)`.
- Failure-message fragments follow `LogVerificationBuilder`'s style, for
  example `instrument "shop.orders.placed" (long) with tag region = "eu"
  (string) and value 1: expected exactly 1 measurement(s), observed 0`.

### Observable instruments

- `RecordObservableInstruments()` calls this factory's listener's
  `RecordObservableInstruments()`. Callbacks run synchronously on the
  calling thread and only for this factory's observable instruments. Each
  observation appends measurements. Calling it twice records twice.
- Reads (`GetMeasurements`, `Verify`) never poll and have no side effects.
- If a callback throws, the BCL behavior stands: the other callbacks still
  run, then `AggregateException` propagates to the test. Compono doesn't
  catch it.
- Before the first `Create`, or after disposal, it's a no-op.

### Asynchronous measurements

Measurements recorded on other threads (background work, hosted services)
are captured thread-safely as they arrive. Reads are point-in-time
snapshots. Compono provides no wait or timeout API. The test awaits its
own completion signal before asserting. `WaitForMeasurementsAsync` is
deferred, not rejected. It's additive if evidence asks for it.

### Lifecycle and ownership

- Composition stays non-owning. `UseMetrics()` composes a new factory per
  request. `[Shared] IMeterFactory` gives the SUT and the test the same one,
  exactly like `[Shared] ILogger<T>`.
- `IMeterFactory` already extends `IDisposable`. `Dispose()` disposes every
  meter the factory created (raising `MeasurementsCompleted`), then the
  listener. It's idempotent and thread-safe. A measurement racing with
  disposal may be captured or dropped, but never throws or corrupts state.
- After disposal, captured measurements stay readable, `ClearMeasurements`
  works, `RecordObservableInstruments` is a no-op, and measurements on the
  disposed meters' instruments aren't captured.
- Disposal is the test's responsibility. Consequence of skipping it: an
  undisposed factory that created meters stays referenced by the BCL's
  static meter list for the process lifetime (RESEARCH-0039 fact 8). Its
  meters, listener, and captured measurements are kept with it, and the
  listener stays registered. This is the same cost as an undisposed
  `DefaultMeterFactory`. A factory that never created a meter costs
  nothing.

#### Disposal patterns

No new ownership API is added. Compono composition never disposes the
factory (ADR-0022 Amendment 4). Whether a test framework disposes
`IDisposable` method arguments is framework-specific, and Compono doesn't
rely on it either way. `Dispose()` is idempotent, so a framework that
also disposes the argument is harmless. The documented patterns:

1. **`[Compose]`-family tests (every framework adapter): dispose the
   `[Shared]` parameter in the test body.**

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
   `IMeterFactory` isn't `IAsyncDisposable`, so `await using` doesn't
   apply. The parameter must be `[Shared]`: without it, the test's factory
   is a different instance from the SUT's, and the SUT's factory isn't
   reachable to dispose (or to assert against).
2. **`Composer.Create` tests: construct the factory, then register it.**

   ```csharp
   using var meters = new CapturingMeterFactory();
   var composer = Composer.Create(b => b.Register<IMeterFactory>(() => meters));
   var sut = composer.Create<OrderService>();
   ```

   An exact registration takes precedence over `UseMetrics()`, so
   `UseMetrics()` isn't needed here. The test creates the instance, so it
   owns it.
3. **Generic Host / DI:** the same, with
   `services.AddSingleton<IMeterFactory>(meters)`. An instance
   registration isn't disposed by the container, so the test's `using`
   owns it. Dispose the factory after the host has stopped. Disposal stops
   capture, so measurements recorded during host shutdown are only
   captured while the factory is alive.

Dispose after the SUT's work is done. Measurements recorded after disposal
aren't captured, so disposing while background work is still running can
drop measurements.

#### Snapshot lifetime

- `GetMeasurements<T>` returns a new list each call. Later captures,
  `ClearMeasurements()`, and `Dispose()` never change a list already
  returned.
- Each `Tags` list is a per-measurement copy taken in the callback. It
  doesn't alias the BCL's tag span or any other measurement's tags.
- `CapturedMeasurement<T>.Instrument` is the actual `Instrument` object the
  SUT created. Through `Instrument.Meter` it references the actual
  `Meter`, and through `Meter.Scope` the factory. A retained snapshot
  therefore keeps the instrument, its meter, and the factory (and that
  factory's captured measurements) reachable for as long as the test holds
  it. That's ordinary object reachability and ends when the snapshot is
  dropped. It isn't a registration and causes no capture.
- Snapshots stay valid after the factory is disposed. `Value`, `Tags`, and
  `Instrument`'s descriptive members (`Name`, `Unit`, `Description`,
  `IsObservable`, `Meter.Name`, `Meter.Version`, `Meter.Tags`) remain
  readable. A disposed meter's instruments report `Enabled == false`, and
  recording through them is a no-op. Reading a snapshot never throws
  because of disposal.

### Precedence and misuse

- `UseMetrics()` registers a stage-6 provider that handles exactly
  `typeof(IMeterFactory)` and returns `NotHandled` for everything else.
  Precedence against `UseNSubstitute()`/`UseGeneratedTestDoubles()`
  follows the existing first-registered-wins rule (ADR-0024). Register
  `UseMetrics()` first. No generator exclusion is added.
- Every `MeterFactoryTestingExtensions` method throws
  `InvalidOperationException` when the `IMeterFactory` isn't a
  `CapturingMeterFactory`. The message names `UseMetrics()`, the
  registration-order requirement, and the host recipe. It's detected by a
  type check, not reflection.
- `Meter` constructor parameters are not composed by this package.
  Consumers register them from the shared factory in a profile
  (documented recipe).

### Concurrency and allocation

- One private lock per factory guards appends, clears, and reads. The tag
  copy happens before the lock is taken. Reads return snapshot copies.

#### Capture order

- **Definition:** capture order is the order in which measurements are
  successfully inserted into the factory's synchronized capture buffer.
- Each measurement gets a factory-wide sequence number, assigned **inside
  the same lock acquisition that appends it**. Merged selections
  (selection rule 7) and every snapshot are ordered by that sequence
  number.
- Measurements recorded sequentially on one thread are captured in that
  thread's program order, because each append completes before the next
  recording starts. One `RecordObservableInstruments()` call appends in
  the order the BCL invokes the callbacks and their observations.
- **No chronological ordering is claimed across concurrent producers.**
  Two threads recording at about the same time may be captured in either
  order, and capture order can differ from the order the `Add`/`Record`
  calls began. Tests that need a cross-thread order must establish it
  themselves.
- `ClearMeasurements()` doesn't reset the sequence. Order is only
  meaningful within one factory.
- Per measurement: one tag array (none when the measurement has no tags)
  and an amortized list append. No boxing of values, no LINQ, no closure
  allocation in callbacks.

### Native AOT and trimming

No reflection, no `MakeGenericType`, no dynamic code, no generated
activation. The seven typed callbacks are ordinary generic instantiations
over value types that the compiler sees statically. `IsAotCompatible=true`.

Validation matrix. Only what CI actually runs is claimed:

| Coverage | net8.0 | net9.0 | net10.0 | net11.0 |
|---|---|---|---|---|
| Library build with trim/AOT analyzers (`IsAotCompatible`), warnings as errors | CI | CI | CI | CI |
| `Compono.Metrics.Tests` (JIT) | CI | CI | CI | CI |
| Native AOT publish + run of `Compono.Metrics.AotSmokeTest` (linux-x64) | not covered | not covered | CI | not covered |

The AOT row matches the existing `aot-validation.yaml` smoke matrix: every
`*.AotSmokeTest` targets `net10.0` and publishes for `linux-x64`. This ADR
doesn't extend that. On net8.0, net9.0, and net11.0, AOT compatibility is
supported by the analyzers alone, not by a runtime run. An optional manual
publish on other TFMs (as ADR-0071 Amendment 2 did) may be recorded in the
plan as evidence, but it isn't a CI guarantee. The smoke test must
publish with zero IL2xxx/IL3xxx warnings and exit successfully.

### Explicit non-goals

- A metrics framework, aggregation engine, exporter, or OpenTelemetry
  integration.
- Tracing (`ActivitySource`) testing.
- Composing `Meter` parameters, or a configurable meter name.
- An `IServiceCollection`/host extension (documentation recipe instead).
- Timestamps, `TimeProvider`, and wait/timeout APIs.
- Aggregate helpers (`Sum`, `LastValue`) and numeric tag coercion.
- Automatic disposal by composition.
- Replacing `Microsoft.Extensions.Diagnostics.Testing` for consumers who
  prefer it. They can keep using it.

### Positive Consequences

- `IMeterFactory` SUTs compose with one `UseMetrics()` call. One handle
  covers every instrument, isolated per factory.
- No dependencies at all. Combined with ADR-0071, the #161 suite no longer
  needs `Microsoft.Extensions.Diagnostics.Testing`.
- Deterministic observable collection and side-effect-free reads.
- The same verification idiom as `Compono.Logging`/`Compono.TestDoubles`.

### Negative Consequences

- An undisposed factory with meters leaks for the process lifetime, and
  disposal is one manual line per test (`using var _ = meters;`).
  Mitigation: the three documented disposal patterns, `using` in every
  example, the sample, and the skill reference. The leak is identical to
  an undisposed `DefaultMeterFactory`.
- Same-named instruments with different value types in same-named meters
  (for example, `Shop` v1 and v2) can't be read at all. Selection rule 5
  throws, and there's no version- or tag-level meter selector. Mitigation:
  the conflict message names every instrument. The scenario is unusual,
  and a selector is additive later if evidence asks for one.
- Typed reads require the test to know `T`. Mitigation: mismatches throw a
  message naming the real type.
- No wait API, so tests asserting on background work must synchronize
  themselves. Mitigation: documented, additive later.
- Wrong registration order against `UseNSubstitute()` produces a
  substitute. Mitigation: the misuse exception names the cause.

## Pros and Cons of the Options

### Axis 1 (home)

- **New package (chosen).** Good: `Compono.Http` precedent for a
  zero-dependency BCL seam, and a clear package guide and skill route.
  Bad: one more package to validate, document, and release.
- **Documentation recipe.** Good: no surface. Bad: keeps the heavy
  dependency set and per-instrument collectors, and every consumer
  re-derives isolation.
- **Fold into `Compono.Logging`.** Good: no new package. Bad: wrong
  ecosystem name, and forces `Logging.Abstractions` on metrics users.

### Axis 2 (handle)

- **Composed factory (chosen).** Good: works from `[Compose]` attributes
  and profiles with no test-body object, and matches ADR-0055. Bad: needs
  `[Shared]`, like logging.
- **Test-owned collector.** Good: matches ADR-0071. Bad: awkward where no
  test-body object exists, and adds a second type for no evidenced need.
- **Both.** Bad: more surface with no evidence.

### Axis 3 (listener)

- **Per-factory, lazy (chosen).** Good: no static state, observable polls
  are naturally scoped, unused factories cost nothing. Bad: one listener
  per active factory. Each costs a callback on every instrument publish in
  the process.
- **Shared static router.** Good: one listener total. Bad: static state,
  and a shared `RecordObservableInstruments()` would run every test's
  callbacks, so it needs a second transient-listener mechanism. It doesn't
  fix the meter leak either (fact 8).
- **Ownership ADR.** Bad: large, out of scope for #165 per RESEARCH-0015.

### Axis 4 (typing)

- **Typed (chosen).** Good: no boxing, compile-time value type, matches
  `MetricCollector<T>`. Bad: the test names `T`.
- **Untyped.** Bad: boxes every measurement, and moves `int`-vs-`long`
  `Equals` pitfalls into tests.
- **Double.** Bad: loses `decimal` precision and `long` exactness above 2^53.

### Axis 5 (observables)

- **Explicit (chosen).** Good: reads have no side effects, BCL name, and
  deterministic. Bad: one extra call.
- **Auto on read.** Bad: two reads record two observations, and SUT
  callbacks run inside assertions.
- **Opt-in auto.** Bad: option surface without evidence.

### Axis 6 (verification)

- **Snapshot + filter `Verify` (chosen).** Good: diagnostic failure
  messages, same shape as `LogVerificationBuilder`, value math stays in
  LINQ. Bad: count-centric `Verify` doesn't express sums directly.
- **Aggregate helpers.** Bad: generic math over seven types, and
  kind-dependent meaning (a sum of gauge values is meaningless).
- **Snapshot only.** Bad: loses Compono's diagnostic messages and idiom
  consistency.

### Axis 7 (host)

- **Recipe (chosen).** Good: one line of the consumer's own DI API, and no
  dependency added. Bad: no IntelliSense-discoverable Compono call.
- **`AddComponoMetrics()`.** Bad: adds `DependencyInjection.Abstractions`
  to a zero-dependency package for a one-liner.
- **Defer.** Bad: host tests are a likely next question.

## Links

- Issue [#165](https://github.com/LayeredCraft/compono/issues/165); parent
  [#161](https://github.com/LayeredCraft/compono/issues/161)
- [RESEARCH-0039](../research/0039-issue-165-metrics-testing-admission.md),
  [RESEARCH-0036](../research/0036-issue-161-test-composition-gaps-intake.md),
  [RESEARCH-0015](../research/0015-disposal-ownership-research.md),
  [RESEARCH-0033](../research/0033-compono-logging-tests-concurrency-hang-investigation.md)
- [ADR-0055](0055-compono-logging-testing-support-package.md),
  [ADR-0071](0071-compono-logging-host-wide-log-capture.md),
  [ADR-0051](0051-compono-http-handler-based-testing-package.md),
  [ADR-0024](0024-public-provider-extensibility-model.md),
  [ADR-0022](0022-compono-xunit-package-design.md) Amendment 4
- [PLAN-0074](../plans/0074-compono-metrics-testing-support-impl-plan.md)

## Amendment (2026-11-11): instrument capture classifies by base type, so `Gauge<T>` is captured on every TFM whose runtime provides it

Implementation review caught that the "Capture semantics" section's
enumerated instrument-kind list (`Counter<T>`, `UpDownCounter<T>`,
`Histogram<T>`, `Gauge<T>`, and the observable kinds) was implemented with a
`Gauge<T>`-recognizing branch gated behind `#if NET9_0_OR_GREATER`. That
compiles the branch out for a `net8.0`-targeted assembly, so a `net8.0`
consumer whose runtime provides `DiagnosticSource` 9.x (where `Gauge<T>`
exists) would have had its gauge instruments silently ignored — no buffer,
no capture, no diagnostic.

The capture code now classifies an instrument by its BCL base type instead
of by naming each concrete kind: an instrument is captured when it is an
`Instrument<T>` or an `ObservableInstrument<T>`. Every supported synchronous
kind (`Counter<T>`, `UpDownCounter<T>`, `Histogram<T>`, `Gauge<T>`) derives
from `Instrument<T>`, and every observable kind (`ObservableCounter<T>`,
`ObservableUpDownCounter<T>`, `ObservableGauge<T>`) derives from
`ObservableInstrument<T>`, so the set of captured kinds is unchanged; the
difference is that a kind the runtime introduces later is captured without a
compile-time branch. This is TFM-independent, needs no `PackageReference`,
and still boxes nothing. The original "what's captured" list is otherwise
unchanged. A `net8.0` test (compiled against `DiagnosticSource` 9.x,
test-only) records and verifies `Gauge<double>` as a regression guard.

## Amendment 2 (2026-11-11): meter-tag identity is a one-to-one (bag) match, so duplicate keys are significant

The "Factory semantics" section says the same name, version, and tag set
(order-insensitive, `object.Equals` on values) returns the same `Meter`. The
first implementation compared tag sets with "every left entry matches some
right entry", which treats `[k=1, k=1]` and `[k=1, j=2]` as equal (both
left entries find the `k=1` on the right).

Each entry on one side must now consume exactly one distinct entry on the
other side (a `bool[] matched` bag match), preserving order-insensitivity
while making duplicate keys significant: `[k=1, k=1]` equals `[k=1, k=1]`,
but not `[k=1, j=2]`, and `[k=1, k=1]` does not equal `[k=1]`. The common
case (no meter tags) still allocates nothing.

This is intentionally **stricter** than `DefaultMeterFactory`: its
duplicate-tag comparison is not a true multiset comparison, so
Compono.Metrics does not claim tag-set parity with it here. A duplicate
meter-tag key is significant — two sets with the same keys but different
duplicate multiplicities are never treated as the same meter — which is the
predictable, documented behavior this framework wants.

## Amendment 3 (2026-11-11): a test `IMeterFactory` wins host DI by being the *last* registration, not the first

The "Disposal patterns" section's Generic Host example registered the test
factory with a comment claiming `AddMetrics()` uses `TryAdd`, "so this
wins". That inference is wrong for `Host.CreateApplicationBuilder()`, which
already registers a default `IMeterFactory` as part of its built-in service
setup. `IServiceProvider` resolves a **single** service to its **last**
registration, so the test factory wins because it is registered *after* the
builder is created — not because it is registered first. `TryAdd`/
`TryAddSingleton` would in fact keep the existing (default) registration and
discard the test factory.

The documented recipe is therefore: `Host.CreateApplicationBuilder()`, then
`builder.Services.AddSingleton<IMeterFactory>(meters)`; never
`TryAddSingleton` to replace a factory. Diagnostic and prose wording across
the package guide, host recipe, troubleshooting page, and the
`RequireCapturing` misuse message were corrected to this rule. No public
API changed.

## Amendment 4 (2026-11-11): tag-value diagnostics describe the *expected* filter value, including its runtime type

The "Selection and matching" section's `WithTag` bullet said the failure
message "shows each tag's actual runtime type". The implementation annotated
the *expected* value's compile-time type (for example `region = "eu"
(string)`), which is not the measurement's actual runtime tag type and read
as if it were. That annotation was removed.

A later review restored a type annotation but made the wording unambiguous:
the message describes the **expected** value the test passed to `WithTag`,
never the captured measurement's tag. A string stays quoted without an
annotation (`region = "eu"` — the quotes already convey the type), `null`
renders as `null`, and every other value shows its runtime type, for example
`status = 200 (int)`. Because the type comes from the argument the test
supplied, the message can never claim a type the measurement does not have.

Failure messages also: join the filter descriptions with `with` (first) and
`and` (subsequent); format numeric values with the invariant culture; forward
the count to core `CallVerifier`'s `Expected exactly N call(s) to ...,
but received M.` wording; and, when an unknown instrument name matched
nothing, append a separate sentence naming the missing instrument and
listing the instruments the factory did publish (after the count sentence,
where it reads naturally). Example:
`Expected exactly 1 call(s) to instrument "shop.orders.placed" (long) with
tag region = "eu" and value 1, but received 0.` No public API changed, and
core `CallVerifier` is untouched.

## Amendment 5 (2026-11-11): Compono.Metrics owns `IMeterFactory` — excluded from `Compono.TestDoubles` generation

**Status:** Accepted (maintainer decision, 2026-11-11). **This amendment
explicitly supersedes the "Precedence and misuse" sentence *"No generator
exclusion is added."*** Everything else in that section stands, including
first-registered-wins against `UseNSubstitute()`.

### What dogfooding found

The #161 follow-up ([RESEARCH-0040](../research/0040-issue-161-1-4-dogfood-follow-up.md)
finding 1) migrated a 231-test suite with `ComponoGeneratedTestDoubles=true`.
A shared base profile called `UseGeneratedTestDoubles()` before
`UseMetrics()`. `Compono.Generators` emits a double for every interface it
reaches, so it had generated one for the SUT's `IMeterFactory`, and
`GeneratedTestDoubleProvider` won stage 6. 28 tests failed on measurement
assertions. Separately, `UseMetrics()`'s remarks and the misuse exception
named only `UseNSubstitute()`, never `UseGeneratedTestDoubles()`.

### Decision

Follow [ADR-0055](0055-compono-logging-testing-support-package.md)
Amendment 4's ownership precedent. When `Compono.Metrics` is referenced,
`System.Diagnostics.Metrics.IMeterFactory` is a **Metrics-owned
abstraction**, and `Compono.Generators` never generates a
`Compono.TestDoubles` double for it.

- **Signal:** a new MSBuild property, `ComponoMetricsOwnsMeterFactory`.
  `Compono.Metrics`'s packed `build/` and `buildTransitive/` props default
  it to `true` when it's unset, and core `Compono.props` declares it
  `CompilerVisibleProperty`. That's the same split `ComponoGeneratedLogging`
  uses (ADR-0055 Amendment 3). Package or type presence isn't the signal,
  per Amendment 3's rejection of symbol-presence gating. A consumer can set
  it to `false` to get a generated `IMeterFactory` double back.
- **Mechanism:** the existing `TransitiveClosureWalker.TryRecordTestDouble`
  exclusion check that already skips Logging-owned types. When the flag is
  on, it also skips the type `IMeterFactory` resolves to. Nothing else in
  discovery changes. If the type can't be resolved, the flag has no effect.
- **Unchanged:** stage-6 first-registered-wins (ADR-0024/ADR-0043), core
  composition, and provider precedence. `UseMetrics()` must still come
  before `UseNSubstitute()` or any other catch-all stage-6 provider. That
  guidance stays in the remarks, the misuse message, and the docs, which
  now also name `UseGeneratedTestDoubles()` and explain that, with
  `Compono.Metrics` referenced, order against it doesn't matter.

### Tradeoff (accepted)

With `Compono.Metrics` referenced, a composer that calls
`UseGeneratedTestDoubles()` but **not** `UseMetrics()` gets no
`IMeterFactory` double. The request falls through to any later stage-6
provider, or composition fails with the standard "no provider could
satisfy" `CompositionException`. Referencing a metrics-testing package
without calling `UseMetrics()` is almost always a mistake, and a loud
failure beats a silently uncapturing double. Opting out is one property.

### Verification

Generator tests cover flag on, flag off, a sibling interface still being
doubled, and the flag having no effect when the type can't be resolved.
`Compono.Metrics.Tests` runtime tests (real generated doubles enabled)
cover both registration orders with `UseGeneratedTestDoubles()` and the
referenced-without-`UseMetrics()` case. Tracked in
[PLAN-0075](../plans/0075-issue-161-1-4-dogfood-follow-up-impl-plan.md).

## Amendment 6 (2026-11-11): disposal patterns clarified per test engine — no ownership change

**Status:** Accepted (maintainer decision, 2026-11-11). This is a
clarification only. "Composition stays non-owning", "No new ownership API
is added", and the "Automatic disposal by composition" non-goal are
unchanged, and [ADR-0022](0022-compono-xunit-package-design.md)
Amendment 4 stands.

"Disposal patterns" pattern 1 prescribes `using var _ = meters;` in every
`[Compose]`-family test and says Compono doesn't rely on framework disposal
"either way". RESEARCH-0040 finding 3 checked this empirically on xUnit
v3 4.0.1 (JIT): xUnit itself disposes every `IDisposable`/`IAsyncDisposable`
test-method argument after the test case, once per argument slot
(RESEARCH-0038 §2.4, ADR-0070 contract item 12). A `[Shared] IMeterFactory`
parameter is the SUT's instance, so on `Compono.XunitV3` xUnit already
disposes it and the `using` line is redundant (harmless, because
`Dispose()` is idempotent). xUnit's AOT core doesn't dispose arguments. TUnit,
NUnit, and MSTest weren't re-tested here. TUnit's root-argument disposal is
already documented separately in
[ADR-0040](0040-compono-tunit-package-design.md) ("Diagnostics, disposal,
and seed observability") and in the `Compono.TUnit` package guide. This
amendment relies on that documented contract and doesn't re-verify it.

The documented guidance is now per engine: on `Compono.XunitV3` the
`using` line is optional. It's also optional on `Compono.TUnit`, per ADR-0040's
documented root-argument disposal. Keep it on `Compono.XunitV3.Aot`,
`Compono.NUnit`, `Compono.MSTest`, and `Composer.Create` tests. Compono still registers
nothing for disposal (`ComposeAttributeDisposalTests` still guards that),
and xUnit's argument disposal remains xUnit's behavior, not a Compono
contract.
