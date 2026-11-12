# Compono.Metrics

Only relevant if the project references `Compono.Metrics`. `UseMetrics()`
composes `IMeterFactory` as a hand-written `CapturingMeterFactory`, which
captures every measurement a SUT records, isolated per factory. Tests assert
with typed `GetMeasurements<T>()`/`Verify<T>()`. The package depends only on
core `Compono` and `System.Diagnostics.Metrics` (in-box on every target
TFM) — no `PackageReference`, no reflection, Native AOT/trimming compatible.

```csharp
using Compono;
using Compono.Metrics;
using System.Diagnostics.Metrics;

var composer = Composer.Create(builder => builder.UseMetrics());
var service = composer.Create<CheckoutService>();   // CheckoutService(IMeterFactory meters, ...)
```

```csharp
public sealed class CheckoutProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseMetrics();
}

[Theory, Compose<CheckoutProfile>]
public void PlacingAnOrderCountsIt([Shared] IMeterFactory meters, CheckoutService service)
{
    using var _ = meters;   // optional on Compono.XunitV3 (xUnit disposes args); required on XunitV3.Aot; keep on NUnit/MSTest

    service.Place(new Order("eu", 3));

    meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();
}
```

**Composition failed naming `IMeterFactory`?** If you meant
`Compono.Metrics` to supply it, `UseMetrics()` wasn't registered — see
`docs/troubleshooting/common-errors.md#runtime-composition-failures`.
`Register<IMeterFactory>()`/`UseServiceProvider` is equally valid. Provider
registration order is a separate concern (see below); a catch-all provider
that resolves the dependency prevents this missing-provider failure.

## When to use it

A composed type takes an `IMeterFactory` constructor dependency and the test
wants to assert *what* it recorded — instrument name, value, or tags.
Concrete signs: the test hand-rolls an `IMeterFactory`, wires one
`MetricCollector<T>` (`Microsoft.Extensions.Diagnostics.Testing`) per
instrument, or substitutes the factory away and so can't observe anything.

## When NOT to use it

The test doesn't care about the recorded measurements — an unconfigured
`UseNSubstitute()` substitute is fine when nothing asserts against metrics.
Don't introduce `Compono.Metrics` just because a constructor happens to take
an `IMeterFactory`.

## No generation, no MSBuild opt-in

Unlike `Compono.Logging` (`ComponoGeneratedLogging`) and
`Compono.TestDoubles` (`ComponoGeneratedTestDoubles`), `Compono.Metrics`
ships **no generator or analyzer**, and consumers never set an MSBuild
property to turn `UseMetrics()` on. The package ships a
`build`/`buildTransitive` props asset that defaults
`ComponoMetricsOwnsMeterFactory` to `true`, which tells
`Compono.Generators` not to emit a `Compono.TestDoubles` double for
`IMeterFactory`; consumers can override it (set it to `false`) but don't
configure it manually. It relies only on
the in-box `System.Diagnostics.Metrics` BCL types. Never suggest a
compile-time generation opt-in for it — there is none.

## Core usage vocabulary

- `builder.UseMetrics()` — registers the stage-6 provider that composes
  `IMeterFactory` as a new `CapturingMeterFactory` per request. An exact
  `Register<IMeterFactory>(() => meters)` takes precedence.
- `new CapturingMeterFactory()` — direct construction, no composition,
  identical behavior to a provider-composed instance.
- `meters.GetMeasurements<T>(instrumentName)` and
  `GetMeasurements<T>(meterName, instrumentName)` — typed snapshot reads,
  a new list each call. `T` is one of the seven BCL measurement types:
  `byte`, `short`, `int`, `long`, `float`, `double`, `decimal`.
- `meters.Verify<T>(instrumentName)` / `Verify<T>(meterName, instrumentName)`
  — the same `Verify()` idiom as `Compono.Logging`/`Compono.TestDoubles`:
  `.WithValue(value)`, `.WithTag(key, value)`, `.WithoutTag(key)`,
  `.Matching(m => ...)`, ending in `.Once()` / `.Never()` / `.Exactly(n)` /
  `.AtLeast(n)` / `.AtMost(n)`, each a forward to the shared count
  verification applied to the filtered measurement count.
- `meters.RecordObservableInstruments()` — explicit poll of *this* factory's
  observable instruments. Idempotent per call (twice records twice); a no-op
  before the first `Create` or after `Dispose`.
- `meters.ClearMeasurements()` — clears captured measurements (doesn't reset
  capture order's sequence). This is **the** supported way to assert on one
  pass of a multi-pass test: call it immediately before the step under test,
  then `Verify`. Take a `GetMeasurements<T>` snapshot first if an earlier
  pass must also be asserted. Don't invent `Checkpoint()`/`Since(...)`/
  `BeginCapture()` APIs; they were considered and deferred (RESEARCH-0040).

Never suggest a two-verb `VerifyMetrics()...Verify()` shape or an aggregate
helper (`Sum`, `LastValue`) — both were considered and rejected (ADR-0072).
Value math stays in LINQ over `GetMeasurements<T>()`.

## Capture semantics

- Every measurement of every enabled instrument kind is captured:
  `Counter<T>`, `UpDownCounter<T>`, `Histogram<T>`, `Gauge<T>` (net9.0+ —
  in-box; no TFM split in capture code), and observable instruments when
  polled. Raw measurements only: Compono doesn't aggregate or interpret
  kind (no delta-vs-last-value), and doesn't read bucket advice.
- Tags are copied per measurement in callback order (`Array.Empty` when
  none); meter- and instrument-level tags are **not** merged in (reach them
  through `CapturedMeasurement<T>.Instrument`). No timestamp.
- One private lock per factory guards appends, clears, and reads; the tag
  copy happens before the lock. Two factories never share capture state —
  identical instrument names stay isolated.
- Capture order is insertion order into the synchronized buffer (a
  factory-wide sequence assigned inside the append lock). Sequentially
  recorded measurements keep program order; **no cross-thread chronological
  order is claimed** for concurrent producers.

## Selection and matching rules

`GetMeasurements<T>`/`Verify<T>` resolve their selection with the same
ordered algorithm; the first rule that applies decides. `Verify<T>`
resolves at builder creation, so a conflict throws before any filter.

1. **Arguments.** Null/empty names → `ArgumentException`. A `T` outside the
   seven supported types → `ArgumentException` naming the requested type and
   listing all seven.
2. **Candidates.** Every published instrument whose `Name` matches, and, for
   the two-argument overload, whose `Meter.Name` matches. Both ordinal and
   case-sensitive. Candidates include **every** value type, not just `T`.
3. **Unknown name.** No candidates → empty result; `Never()` passes. A
   failing count message lists the instruments that do exist.
4. **Meter ambiguity** (one-argument overload only) → `InvalidOperationException`
   naming the candidates and pointing at the two-argument overload.
5. **Type conflict.** Candidates recording more than one value type →
   `InvalidOperationException` **even when one of them is `T`**. Happens for
   `CreateCounter<int>("x")` + `CreateCounter<long>("x")` in one meter, or
   same-named meters with different versions/tags. Resolve by renaming, or
   narrow with the two-argument overload. No version/tag meter selector
   exists (use `Matching(m => m.Instrument.Meter.Version == "2")` after the
   types agree).
6. **Type mismatch.** Candidates all recording one type other than `T` →
   `InvalidOperationException`, e.g. `instrument "shop.orders.placed"
   records int, not long`.
7. **Merge.** Otherwise every candidate records `T`; their measurements merge
   in capture order.

Matching (never invent a coercion):

- `WithValue(value)` uses `EqualityComparer<T>.Default` — floating-point
  equality is **exact**; use `Matching(...)` for tolerance.
- `WithTag(key, value)` requires a tag whose key matches (ordinal) and whose
  value satisfies `object.Equals`. Other tags are ignored. No numeric
  coercion: `200` (`int`) doesn't match `200L`.
- `WithoutTag(key)` requires no such tag.
- Exact tag-set and other matches use `Matching(...)`.

## Asynchronous measurements

Measurements recorded on other threads are captured thread-safely as they
arrive; reads are point-in-time snapshots. There is **no wait/timeout API**
(`WaitForMeasurementsAsync` is deferred, not rejected). The test awaits its
own completion signal before asserting.

## Disposal patterns

`IMeterFactory` already extends `IDisposable`. Compono composition never
disposes the factory. `Dispose()` (idempotent, thread-safe) disposes every
meter the factory created, then its listener. After disposal: captured
measurements stay readable, `ClearMeasurements` works,
`RecordObservableInstruments` is a no-op, and recording on the disposed
meters' instruments isn't captured. Three documented patterns:

1. `[Compose]` tests — dispose the `[Shared]` parameter: `using var _ = meters;`.
   On `Compono.XunitV3` (JIT) the line is optional: xUnit itself disposes every
   `IDisposable`/`IAsyncDisposable` test-method argument after the test case
   (once per argument slot), which is xUnit's behavior, not a Compono
   contract. Keep it on `Compono.XunitV3.Aot` (xUnit's AOT core doesn't
   dispose arguments), NUnit, and MSTest (not verified). TUnit disposes composed
   *root* method arguments per `Compono.TUnit`'s documented contract (ADR-0040),
   which ADR-0072 Amendment 6 didn't re-test; there the line is optional and
   harmless. Never tell a user Compono
   disposes composed values; it doesn't (ADR-0072 Amendment 6).
2. `Composer.Create` tests — construct the factory, then
   `Register<IMeterFactory>(() => meters)` (which also beats `UseMetrics()`).
3. Generic Host/DI — `services.AddSingleton<IMeterFactory>(meters)`. The
   builder already registers a default `IMeterFactory` (for example from
   `AddMetrics()`), and a single-service resolve returns the **last**
   registration, so add yours after anything else that registers one; never
   use `TryAddSingleton` to replace a factory (it keeps the existing one).
   An instance registration isn't disposed by the container, so the test's
   `using` owns it. Dispose after the host stops.

An undisposed factory that created meters stays referenced by the BCL's
static meter list for the process lifetime — the same cost as an undisposed
`DefaultMeterFactory`. A factory that never created a meter costs nothing.

## Snapshot lifetime

`GetMeasurements<T>` returns a new list each call; later captures,
`ClearMeasurements()`, and `Dispose()` never change a returned list. Each
`Tags` list is a per-measurement copy. `CapturedMeasurement<T>.Instrument`
is the real instrument, reachable through to its `Meter` and the factory —
ordinary object reachability, not a registration. Snapshots stay valid after
disposal: `Value`, `Tags`, and the instrument's descriptive members remain
readable; a disposed meter's instruments report `Enabled == false`.

## Registration order (stage-6 precedence) and misuse

`UseMetrics()` shares stage 6 with `UseNSubstitute()` and any other catch-all
provider — the existing first-registered-wins rule (ADR-0024) applies
unchanged:

```csharp
builder.UseMetrics().UseNSubstitute();   // IMeterFactory -> CapturingMeterFactory
builder.UseNSubstitute().UseMetrics();   // IMeterFactory -> an NSubstitute substitute instead
```

Register `UseMetrics()` first, especially in a shared base profile.

**Order against `UseGeneratedTestDoubles()` does not matter** (ADR-0072
Amendment 5): referencing `Compono.Metrics` defaults the MSBuild property
`ComponoMetricsOwnsMeterFactory` to `true`, so `Compono.TestDoubles` never
generates an `IMeterFactory` double; both orders compose a
`CapturingMeterFactory`, and other interfaces still get generated doubles.
Consequences to explain when relevant: `UseGeneratedTestDoubles()` *without*
`UseMetrics()` now fails composition with a `CompositionException` naming
`IMeterFactory` (fix: add `UseMetrics()`), and a project that genuinely wants
a generated `IMeterFactory` double sets
`<ComponoMetricsOwnsMeterFactory>false</ComponoMetricsOwnsMeterFactory>`.
A `ProjectReference` (not `PackageReference`) to Compono.Metrics doesn't get
the packed default — set the property explicitly there.

Every `MeterFactoryTestingExtensions` method
throws `InvalidOperationException` when the `IMeterFactory` isn't a
`CapturingMeterFactory` (detected by a type check, not reflection); the
message names `UseMetrics()`, the catch-all registration-order requirement,
the `ComponoMetricsOwnsMeterFactory` ownership rule, and the host recipe.
Migrating from `MetricCollector<T>`? See
docs/migrating-from-microsoft-extensions-diagnostics-testing.md. `Meter` constructor parameters are **not** composed by this
package — register them from the shared factory in a profile.

## `Share<T>()` and `[Shared]`

A composed `IMeterFactory` participates in `Share<T>()` like any other type
— no metrics-specific mechanism. Use **one or the other**: `[Shared]
IMeterFactory meters` for per-test intent (resolved first, any parameter
position) gives the SUT and test body the same factory; `Share<IMeterFactory>()`
declared in a profile for profile-wide intent, an undecorated
`IMeterFactory` parameter, or several consumers in one `Composer.Create`
graph. Both is redundant but harmless. Without either, the test's factory
is a different instance from the SUT's and can't be asserted against or
disposed — each `CapturingMeterFactory` captures only its own meters.

## Native AOT and trimming

No reflection, no `MakeGenericType`, no dynamic code. The seven typed
callbacks are ordinary generic instantiations over value types the compiler
sees statically. `IsAotCompatible=true`; the package publishes and runs under
Native AOT (`Compono.Metrics.AotSmokeTest` on net10.0/linux-x64 in CI).

## v1 boundaries — do not invent capability beyond these

No metrics framework/aggregation/exporter/OpenTelemetry integration, no
`ActivitySource`/tracing testing, no composing `Meter` parameters or
configurable meter name, no `IServiceCollection`/host extension (a
documentation recipe only — `AddSingleton<IMeterFactory>(meters)`), no
timestamps/`TimeProvider`/wait APIs, no aggregate helpers, no numeric tag
coercion, no automatic disposal by composition, and no version- or tag-level
meter selection. It doesn't replace
`Microsoft.Extensions.Diagnostics.Testing` for consumers who prefer it.
