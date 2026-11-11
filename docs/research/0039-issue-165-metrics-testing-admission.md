# RESEARCH-0039: Issue #165 Metrics Testing — Capability Admission and BCL Facts

**Status:** Complete. The product owner confirmed the admission outcome
(new `Compono.Metrics` package) in the design interview on 2026-11-11.
The decision is recorded in the Accepted
[ADR-0072](../adr/0072-compono-metrics-testing-support-package.md).

**Governing process:**
[`docs/architecture/capability-admission.md`](../architecture/capability-admission.md).
**Trigger:** [#165](https://github.com/LayeredCraft/compono/issues/165),
child of [#161](https://github.com/LayeredCraft/compono/issues/161) (item 2).
[RESEARCH-0036](0036-issue-161-test-composition-gaps-intake.md) classified
it as a missing capability with Gate A uncertain and asked for an
admission pass before any ADR. This document is that pass.

## 1. Concrete problem (Step 1)

A SUT takes `IMeterFactory` and records counter, histogram, and gauge
measurements. The test needs to compose the SUT and assert the values,
instrument names, and tags it recorded. Two real consumers have the need:

- **#161 migration** (xUnit v3, .NET 11, Compono 1.3.2): hand-written test
  `IMeterFactory` in a profile, plus one `MetricCollector<T>` per
  instrument from `Microsoft.Extensions.Diagnostics.Testing`.
- **`dynamodb-distributed-lock`** (RESEARCH-0015 §3): `Register<Meter>(...)`
  with 13 `[Shared] Meter` usages and a hand-written aggregator.

## 2. How it's solved today (Step 2)

- **Compono:** nothing. No source or doc mentions `IMeterFactory`,
  `MeterListener`, or `MetricCollector<T>`.
- **BCL:** `IMeterFactory`, `Meter`, `MeterOptions.Scope`, and
  `MeterListener` are in-box on every target TFM. The default
  `IMeterFactory` implementation (`DefaultMeterFactory`) is internal and
  reachable only through `services.AddMetrics()` in
  `Microsoft.Extensions.Diagnostics`, so a composed-only test has no
  factory to use without writing one.
- **First-party testing:** `MetricCollector<T>` captures **one instrument**
  per collector (constructed from an `Instrument<T>`, or from
  `meterScope + meterName + instrumentName`). A SUT with N instruments
  needs N collectors, each disposed. The package brings
  `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Telemetry.Abstractions`,
  and Options packages with it.

The workaround is therefore roughly 15 lines of factory, plus per-instrument
collector wiring in every test, plus a heavy dependency set. That is
composition ceremony that drifts per consumer, not just a long call.

## 3. Gate A (Step 3)

| Criterion | Verdict | Evidence |
|---|---|---|
| 1. Compono-specific value | Pass | `UseMetrics()` makes `IMeterFactory` compose with no hand-written factory. One scope-isolated handle covers every instrument the SUT creates (not one collector per instrument). It reuses the existing `[Shared]` and profile mechanisms and one verification vocabulary with `Compono.Logging`/`Compono.TestDoubles` (`CallVerifier`). |
| 2. Native ecosystem fit | Pass | Built on `IMeterFactory`'s own contract (`MeterOptions.Scope`, meter caching, `IDisposable`) and `MeterListener`. Selection is by meter and instrument name and measurement tags, which are the BCL's identity terms. `RecordObservableInstruments` keeps the BCL name. |
| 3. Meaningful abstraction | Pass (narrowly) | Collapses factory + per-instrument collectors + disposal + isolation into one composed concept. A consumer couldn't get scope-filtered, all-instrument, deterministic observable collection in an afternoon without re-deriving §5's facts. |
| 4. Architectural fit | Pass | Existing public stage-6 extension point (`CompositionBuilder.AddTestDoubleProvider`, as `UseLogging()` uses). Non-generic `IMeterFactory` needs no generator work. No reflection, no static state. |
| 5. Package boundary | Pass | Doesn't belong in core (core stays a composition engine; this is a test double for a specific BCL seam). Doesn't fit `Compono.Logging` (would force `Logging.Abstractions` on metrics users and misname the ecosystem). `Compono.Http` is the precedent for a zero-dependency package around an in-box BCL seam. |

## 4. Gate B (Step 4)

Met. Real migration evidence (#161) plus a second independent consumer
(`dynamodb-distributed-lock`), plus an explicit product-owner request
(#165 and this interview).

**Outcome:** roadmap item → new package `Compono.Metrics`
(ADR-0072). The documentation-only alternative (a cookbook recipe with
`MetricCollector<T>`) was considered and rejected by the product owner:
it leaves the dependency set and per-instrument wiring in place.

## 5. BCL facts (spike, 2026-11-11)

A throwaway console app on SDK 11.0.100, `net8.0` and `net11.0`,
`IsAotCompatible=true`, no package references on `net8.0`:

1. `IMeterFactory`, `MeterOptions.Scope`, `Meter.Scope`, and `MeterListener`
   compile and run on `net8.0` from the shared framework alone. A metrics
   package needs **no `PackageReference`**.
2. A factory that sets `options.Scope = this` produces meters whose
   `Meter.Scope` is that factory (reference-equal).
3. Two factories each create a meter named `"Shop"` and a counter named
   `"orders"`. A listener per factory that enables only instruments whose
   `instrument.Meter.Scope` is its own factory sees only its own
   measurements. Same-named meters don't cross-contaminate.
4. `RecordObservableInstruments()` on factory A's listener invoked A's
   observable callback once and B's zero times. Scoping observable
   collection per factory is deterministic.
5. `Gauge<T>` (synchronous) exists on `net9.0+` only. A listener's generic
   `SetMeasurementEventCallback<T>` receives `Gauge<T>` measurements without
   the listener naming the type, so the capture code needs no TFM split.
6. Disposing a factory's meters raises `MeasurementsCompleted` for each
   instrument. Measurements recorded on those instruments afterwards are
   not delivered.
7. `services.AddMetrics()` registers `DefaultMeterFactory` with `TryAdd`.
   A test factory registered as an `IMeterFactory` singleton before
   `AddMetrics()` survives, so a host path is a one-line consumer
   registration.
8. Undisposed meters stay in the BCL's static meter list and keep
   `Meter.Scope` (the factory) alive. That is true regardless of how the
   listener is owned, so listener design can't remove the leak. Only
   disposing the factory does.

`DefaultMeterFactory` parity checks (same spike, `services.AddMetrics()`,
`Microsoft.Extensions.Diagnostics` 11.0.0, on `net8.0` and `net11.0`):

9. `Create(options)` doesn't mutate the caller's `MeterOptions` (its
   `Scope` stays `null`). It returns the **same** `Meter` for the same name
   (and version and tags), and a different one when the version differs.
   `Create` with a foreign non-null `options.Scope` throws
   `InvalidOperationException`. `Create` after disposal throws
   `ObjectDisposedException`.
10. With `DiagnosticSource` 11.x, creating the same instrument name twice
    on one meter returns the same instrument instance. Older in-box
    versions may return distinct instances, so selection must not assume
    one instrument per name.
11. When one observable callback throws during
    `MeterListener.RecordObservableInstruments()`, the other callbacks
    still run and the call then throws `AggregateException` wrapping the
    callback's exception.

## 6. Prior art: adopted vs. changed

From `MetricCollector<T>`:

- **Adopted:** scope-based isolation (`meterScope`), typed measurements
  (`CollectedMeasurement<T>`: value + tags), explicit
  `RecordObservableInstruments()`, snapshot reads.
- **Changed:** one handle per factory across all instruments, not one per
  instrument. No timestamp or `TimeProvider`. No
  `WaitForMeasurementsAsync`. No dependency on the package (same
  independence reasoning as ADR-0055 for `FakeLogger`). Verification uses
  Compono's `CallVerifier` counts with filters, not a separate assertion
  surface.

## Links

- [#165](https://github.com/LayeredCraft/compono/issues/165),
  [#161](https://github.com/LayeredCraft/compono/issues/161)
- [RESEARCH-0036](0036-issue-161-test-composition-gaps-intake.md),
  [RESEARCH-0015](0015-disposal-ownership-research.md)
- [ADR-0072](../adr/0072-compono-metrics-testing-support-package.md),
  [ADR-0055](../adr/0055-compono-logging-testing-support-package.md),
  [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)
