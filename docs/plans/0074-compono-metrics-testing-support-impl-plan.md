# [PLAN-0074] `Compono.Metrics`: `System.Diagnostics.Metrics` Testing Support (Issue #165)

**Status:** In Progress

**Implements:** [ADR-0072](../adr/0072-compono-metrics-testing-support-package.md)
(issue [#165](https://github.com/LayeredCraft/compono/issues/165), child of
[#161](https://github.com/LayeredCraft/compono/issues/161)). ADR-0072 is
**Accepted** (product-owner final review, 2026-11-11), and so is this plan.
The design is approved. Implementation phases B1–B6 are complete and
independently reviewed with follow-up corrections applied (see
`## Notes`); the change is uncommitted on `docs/165-metrics-planning`,
awaiting the single combined PR.

## Goal

A test can write `builder.UseMetrics()`, take `[Shared] IMeterFactory meters`
next to a SUT that records metrics, dispose it with `using var _ = meters;`,
and assert `meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").Once()`.
Measurements are isolated per factory, observable instruments poll only on
`RecordObservableInstruments()`, and the package has no dependency beyond
core `Compono`. It builds and tests on `net8.0`–`net11.0`, packs,
package-validates, and passes the CI Native AOT smoke test (net10.0,
linux-x64). It ships with a package guide, a host recipe, one BasicUsage
sample, the API reference, and `compono` skill coverage.

## Scope

**In:** everything in ADR-0072's Decision Outcome.

**Out (do not build):** everything in ADR-0072's "Explicit non-goals", plus:

- Any change to core `Compono`, `Compono.Generators`, or any other `src/`
  package. If implementation finds that core needs a change, stop and
  raise it. It's a design question, not an implementation detail.
- Any `PackageReference` in `src/Compono.Metrics`.
- Any ownership/disposal API (ADR-0072 "Disposal patterns" is
  documentation only).
- A new public exception type (conflicts use `InvalidOperationException`).
- Extending the CI AOT smoke matrix beyond its existing net10.0/linux-x64
  shape.
- Package version bumps and NuGet publishing (release-owner action).

**Release:** ships in the #161 release, **v1.4.0**, alongside #164.

**Delivery (D8, revised):** the maintainer decided the feature ships in **one
combined PR**. The planning artifacts (RESEARCH-0039, ADR-0072 `Accepted`,
this plan) travel on the same branch as the implementation (B1–B6); there is
no separate planning-only PR and no planning-PR merge gate. The combined PR
also carries the `docs/plans/README.md` and `docs/adr/README.md` rows.

**Dogfood:** optional per repo policy (2026-11-04). Not planned. In-repo
pack-and-consume and the AOT smoke test cover the integration risk.

## Decisions already made (do not reopen during implementation)

From the maintainer interview and review (2026-11-11), recorded in ADR-0072:

| # | Decision |
|---|---|
| D1 | New package `Compono.Metrics`, no external dependency. |
| D2 | The composed `CapturingMeterFactory` is the handle (`[Shared] IMeterFactory`, extension methods). |
| D3 | One lazy `MeterListener` per factory, scope-filtered, disposed with the factory. No static state. |
| D4 | Typed reads: `CapturedMeasurement<T>` (plain `readonly struct`), `GetMeasurements<T>`, `Verify<T>`. |
| D5 | Selection follows ADR-0072's seven ordered rules: unsupported `T` → `ArgumentException`; unknown name → empty; ambiguous meter names → throw; **mixed value types → type-conflict throw, even if one is `T`**; single other type → mismatch throw; otherwise merge. |
| D6 | Observable instruments poll only on explicit `RecordObservableInstruments()`. |
| D7 | `Verify<T>` filters (`WithValue`, `WithTag`, `WithoutTag`, `Matching`) + `CallVerifier` counts. No aggregate helpers. |
| D8 | Docs PR first, then one implementation PR. |
| D9 | Host/DI path is a documentation recipe only. |
| D10 | `Meter` parameters aren't composed (documented recipe). |
| D11 | No wait API. No timestamp. Tag match is subset + `object.Equals`, no coercion. |
| D12 | Precedence is registration order (ADR-0024), documented. No generator exclusion. |
| D13 | Names are ordinal, case-sensitive. Type names: `CapturingMeterFactory`, `CapturedMeasurement<T>`, `MeterFactoryTestingExtensions`, `MeasurementVerificationBuilder<T>`. |
| D14 | Disposal is test-owned, through the three documented patterns. No ownership API. |
| D15 | Capture order is insertion order into the synchronized buffer. The sequence number is assigned under the append's lock. No cross-producer chronology. |
| D16 | Snapshots hold the real `Instrument`/`Meter` references and stay valid after disposal. |
| D17 | Release v1.4.0. One focused BasicUsage sample. |
| D18 | An unsupported `T` throws `ArgumentException`. The message names the requested type and lists the seven supported types. |
| D19 | The v1 limitation is accepted: same-named instruments with conflicting value types in same-named meters can't be read. No version or tag selectors. |
| D20 | `InvalidOperationException` for ambiguity, type conflicts, type mismatch, and misuse. No custom exception hierarchy. |
| D21 | Native AOT matrix as documented: analyzers on net8.0–net11.0, CI publish-and-run on net10.0 linux-x64 only. |

## Phases

### B0 — Acceptance (Done: design accepted)

- [x] Product-owner review of RESEARCH-0039, ADR-0072, and this plan.
      Refinements applied (2026-11-11).
- [x] Final review of the refinements. ADR-0072 `Status: Accepted`, and its
      `docs/adr/README.md` row updated (2026-11-11).
- [x] Delivery revised (2026-11-11): the planning artifacts
      (RESEARCH-0039, ADR-0072, PLAN-0074, both index rows, and the stale
      PLAN-0072 row in `docs/plans/README.md` corrected to `Done`) land in
      the **single combined PR** with the implementation, not a separate
      planning-only PR. The obsolete planning-PR merge gate is removed.

### B1 — Package skeleton and factory (Done)

- [x] `src/Compono.Metrics/Compono.Metrics.csproj`: TFMs
      `net8.0;net9.0;net10.0;net11.0`, `IsAotCompatible=true`,
      `ProjectReference` to `Compono` with the exact-version pin target
      (copy `Compono.Logging.csproj`'s), `InternalsVisibleTo` the test
      project. No `PackageReference`.
- [x] Add it to `Compono.slnx`.
- [x] `CapturingMeterFactory`: `DefaultMeterFactory` parity (null,
      foreign-scope, and disposed checks; options copy; meter cache by name
      + version + order-insensitive tags), lazy listener, idempotent
      thread-safe `Dispose` (meters, then listener).
- [x] `MetricsProvider` (internal, stage 6, `typeof(IMeterFactory)` only)
      and `UseMetrics()`.

### B2 — Capture, selection, verification (Done)

- [x] Typed per-instrument buffers as listener state. Seven numeric
      callbacks. Tag copy before taking the lock. One lock per factory.
      The factory-wide sequence number is assigned in the same lock
      acquisition as the append (D15).
- [x] `CapturedMeasurement<T>` as a plain `readonly struct`.
- [x] Selection exactly per ADR-0072's seven rules, in order (D5). The
      candidate set is never pre-filtered by `T`.
- [x] `MeterFactoryTestingExtensions`: `GetMeasurements` (new list per
      call), `Verify` (resolves the selection when the builder is
      created), `RecordObservableInstruments`, `ClearMeasurements`, misuse
      `InvalidOperationException` (names `UseMetrics()`, order, host
      recipe).
- [x] `MeasurementVerificationBuilder<T>` with `LogVerificationBuilder`-style
      failure messages, including the existing-instruments list on a
      failed count.

### B3 — Tests (Done)

See Test Plan. Bounded concurrency only (RESEARCH-0033). `test/Compono.Metrics.Tests`
passes on all four TFMs: net8.0 (77), net9.0 (78), net10.0 (78), net11.0 (78).

### B4 — Native AOT and packaging (Done)

- [x] `test/Compono.Metrics.AotSmokeTest`, targeting `net10.0` only, like
      every existing smoke test (copy the `Compono.Logging` layout:
      `pack-compono.sh`, `nuget.config`, `verify-packaging.sh`). Exercise
      composition, every instrument kind available on net10.0, observable
      polling, tags, a type-conflict diagnostic, and `Verify`.
- [x] Wire `Compono.Metrics` into `.github/workflows/aot-validation.yaml`
      (the `all_legs` list **and** the `src/Compono\.Metrics/` change
      filter), `package-validation.yaml`, `docs.yml`,
      `.github/scripts/inspect-packed-nupkgs.sh` (+ its `.tests.sh`),
      `generate-api-reference.sh`, and `scripts/dogfood-validate.sh`'s
      package list, following each file's `Compono.Logging` entry.
- [x] Package validation for a new package (no ApiCompat baseline yet).
      Follow how the most recent new package was first validated.
- [x] The packed `.nuspec` has exactly one dependency (`Compono`, exact
      version) per TFM group.
- [x] Optional, not a gate: a manual Native AOT publish of a consumer on
      net8.0/net9.0/net11.0. If done, record it in Notes as evidence and
      don't present it as CI coverage.

### B5 — Documentation, sample, and skill (Done)

- [x] `docs/packages/compono-metrics.md` package guide: setup, `[Shared]`,
      **disposal patterns (all three, with the "dispose after the SUT's
      work is done" caveat and the undisposed-leak consequence)**,
      instrument kinds, the selection rules (including type conflicts),
      tag matching, observables, capture order (no cross-producer
      chronology), snapshot lifetime, precedence, the `Meter` parameter
      recipe, the Native AOT validation matrix as stated in ADR-0072,
      non-goals, and migration from `MetricCollector<T>`.
- [x] `docs/how-to/` recipe: assert metrics from a Generic Host
      (`AddSingleton<IMeterFactory>(meters)`, test-owned disposal after
      the host stops).
- [x] `docs/troubleshooting/common-errors.md`: misuse, meter ambiguity,
      type conflict, type mismatch, and unsupported-`T` messages.
- [x] `docs/packages/index.md`, `docs/index.md`, `README.md` package lists,
      `mkdocs.yml` nav, `docs/architecture/current/provider-pipeline.md`
      (stage-6 provider list).
- [x] One focused sample in `samples/Compono.Samples.BasicUsage`: a
      `MetricsSampleProfile` (`UseMetrics()`), a small SUT that takes
      `IMeterFactory`, and one `[Compose<MetricsSampleProfile>]` test using
      `[Shared] IMeterFactory`, `using var _ = meters;`, and `Verify<T>`.
      Add the package reference the way the sample references
      `Compono.Logging`. Update `docs/samples/basic-usage.md`.
- [x] Regenerate `docs/reference/api` before pushing (memory: API
      reference drift).
- [x] `skills/compono/SKILL.md` detection table + `skills/compono/references/metrics.md`
      (must show the disposal pattern) + evals in `skills/compono/evals/evals.json`.

### B6 — Validation gate and closeout (Done)

- [x] `dotnet build` 0 warnings / 0 errors and `dotnet test` green on all
      four TFMs.
- [x] Pack-and-consume check from a throwaway console project on a local
      feed.
- [x] CI AOT smoke leg for `Compono.Metrics` green (net10.0, linux-x64,
      zero IL2xxx/IL3xxx).
- [x] Plan tasks checked and validation recorded. The combined PR (D8,
      revised) carries this plan and both index rows; the plan stays
      `In Progress` until that PR merges, then its index row moves to
      `Done`, #165 closes, and #161 is updated.

## Critical Files

New:

- `src/Compono.Metrics/` — `Compono.Metrics.csproj`,
  `CompositionBuilderExtensions.cs`, `MetricsProvider.cs`,
  `CapturingMeterFactory.cs`, `CapturedMeasurement{T}.cs`,
  `MeterFactoryTestingExtensions.cs`, `MeasurementVerificationBuilder{T}.cs`,
  internal capture buffer and selection types.
- `test/Compono.Metrics.Tests/`, `test/Compono.Metrics.AotSmokeTest/`.
- `docs/packages/compono-metrics.md`, a `docs/how-to/` host recipe,
  `skills/compono/references/metrics.md`, `docs/reference/api/Compono.Metrics/`.
- `samples/Compono.Samples.BasicUsage/` — `MetricsSampleProfile.cs`,
  `MetricsTests.cs`, a small SUT type (or an addition to `Domain.cs`).

Modified:

- `Compono.slnx`, `.github/workflows/{aot-validation.yaml,package-validation.yaml,docs.yml}`,
  `.github/scripts/{inspect-packed-nupkgs.sh,inspect-packed-nupkgs.tests.sh,generate-api-reference.sh}`,
  `scripts/dogfood-validate.sh`, `mkdocs.yml`, `README.md`,
  `docs/index.md`, `docs/packages/index.md`, `docs/samples/basic-usage.md`,
  `docs/architecture/current/provider-pipeline.md`,
  `docs/troubleshooting/common-errors.md`, `skills/compono/SKILL.md`,
  `skills/compono/evals/evals.json`,
  `samples/Compono.Samples.BasicUsage/Compono.Samples.BasicUsage.csproj`,
  `Directory.Packages.props` (only if the sample's package-reference
  pattern needs a version entry), `.gitignore` (if the AOT smoke test needs
  the same entries as Logging's).
- Both index rows (`docs/adr/README.md` and `docs/plans/README.md`) travel in the single combined PR (D8, revised); there is no separate planning-only PR.

Not modified: `src/Compono`, `src/Compono.Generators`, and every other
`src/` package.

## Test Plan

`test/Compono.Metrics.Tests` (xUnit v3 / MTP, all four TFMs). Each row is
an acceptance criterion for B3.

| Area | Cases |
|---|---|
| Factory parity | null options; foreign scope throws; caller options not mutated; same name/version/tags cached (tag order irrelevant); different version → new meter; `Create` after dispose throws `ObjectDisposedException`. |
| Composition | `UseMetrics()` composes `IMeterFactory` as `CapturingMeterFactory`; `[Shared]` gives SUT and test one instance; without `[Shared]` they differ; provider ignores other types; an exact `Register<IMeterFactory>` wins over `UseMetrics()`; order vs `UseNSubstitute()` (wrong order → misuse exception naming the cause). |
| Instrument kinds | `Counter`, `UpDownCounter`, `Histogram` on all TFMs; `Gauge<T>` under `#if NET9_0_OR_GREATER`; each of the seven value types. |
| Observables | nothing recorded before poll; one poll records once; two polls → two measurements; reads don't poll; only this factory's callbacks run (two factories); throwing callback → `AggregateException` and the other callbacks are still recorded; no-op before the first `Create` and after dispose. |
| Selection (rules 1–7, one test per rule) | unsupported `T` (e.g. `Guid`) → `ArgumentException` even with no instruments, and the message lists the seven supported types; null/empty names → `ArgumentException`; unknown name → empty, `Never()` passes, and a failed `Once()` lists existing instruments; different meter names with the one-argument overload → ambiguity throw, two-argument overload resolves it; **mixed types in one meter (`Counter<int>("x")` + `Counter<long>("x")`) → type-conflict throw for `<int>`, `<long>`, and `<double>`, the message lists both instruments, and nothing is silently ignored**; mixed types across same-named meters with different versions → type-conflict throw; single other type → mismatch throw naming it; same-named meters with different versions and the same `T` → merged in capture order; `Verify<T>` throws the conflict when the builder is created. |
| Matching | `WithValue` exact (incl. float); `WithTag` subset; `int` vs `long` doesn't match and the message shows the types; `WithoutTag`; chained filters; `Matching`; every `CallVerifier` terminal. |
| Tags | no-tag measurement → empty list (not null); tag order preserved; meter/instrument-level tags not merged; a returned `Tags` list isn't affected by later measurements. |
| Snapshot lifetime | a list returned before `ClearMeasurements()`/`Dispose()`/later captures is unchanged afterwards; after dispose `Value`, `Tags`, `Instrument.Name`, `Instrument.Meter.Name/Version` are readable and `Instrument.Enabled == false`; `Instrument` is reference-equal to the SUT's instrument. |
| Capture order | single-thread recordings across several instruments come back in program order (including a merged selection); one observable poll appends in callback order; under concurrency, sequence numbers are strictly increasing and gap-free per snapshot, with **no assertion of cross-thread chronology**. |
| Disposal patterns | pattern 1: a real `[Compose]`-style test body with `using var _ = meters;` disposes the shared factory on success **and** when an assertion throws (meters' instruments report `Enabled == false` afterwards); pattern 2: `Composer.Create` + `Register<IMeterFactory>(() => meters)` captures and disposes; dispose idempotent and concurrent-safe; data readable after dispose; `ClearMeasurements` before/after dispose; recording after dispose not captured. |
| Isolation | two factories, identical meter and instrument names, running in parallel: no cross-contamination. |
| Concurrency | bounded multi-thread recording on several instruments alongside reads and clears; no lost or torn measurements (count invariant); bounded writers per RESEARCH-0033. |
| Host recipe (pattern 3) | real Generic Host (test-only `Microsoft.Extensions.Hosting`): `AddSingleton<IMeterFactory>(meters)` before and after `AddMetrics()` → a SUT resolved from the host records into `meters`; the container doesn't dispose the instance; the test disposes it after the host stops. |
| Misuse | every extension method on a non-Compono `IMeterFactory` (hand-written stub) throws `InvalidOperationException`. |

Plus the BasicUsage sample test (runs in the normal test run), the
`Compono.Metrics.AotSmokeTest` CI leg, and the pack-and-consume check (B6).

Validation matrix (the same as ADR-0072's; only CI-backed cells are
claimed):

| Coverage | net8.0 | net9.0 | net10.0 | net11.0 |
|---|---|---|---|---|
| Library build + trim/AOT analyzers (`IsAotCompatible`), warnings as errors | CI | CI | CI | CI |
| `Compono.Metrics.Tests` (JIT) | CI | CI | CI | CI |
| `Gauge<T>` (sync) tests | n/a (BCL) | CI | CI | CI |
| Native AOT publish + run (`Compono.Metrics.AotSmokeTest`, linux-x64) | not covered | not covered | CI | not covered |

Test-framework neutrality comes from core composition. No framework
adapter changes. Disposal pattern 1 is framework-independent C#.

## Notes

- 2026-11-11: Planning drafted from the maintainer interview. BCL facts
  are in RESEARCH-0039 §5 (spike on SDK 11.0.100).
- 2026-11-11: Product-owner review. Disposal patterns, the type-conflict
  rule, snapshot lifetime, the capture-order definition, and the explicit
  AOT matrix were added. Open items resolved (D4, D13, D17; the PLAN-0072
  index fix is part of the same combined PR). One addition made while specifying selection:
  an unsupported `T` throws `ArgumentException` (rule 1), so the conflict
  and mismatch rules only deal with real measurement types.
- 2026-11-11: Final review. ADR-0072 and this plan accepted. D18–D21
  confirmed (unsupported-`T` diagnostic, the accepted conflicting-type
  limitation, `InvalidOperationException` only, the AOT matrix).
  Implementation is not started.
- 2026-11-11: Implementation (B1–B6) complete on `docs/165-metrics-planning`.
  Evidence:
  - `src/Compono.Metrics` builds with **0 warnings / 0 errors** on
    net8.0–net11.0 with the trim/AOT analyzers.
  - `dotnet build Compono.slnx -c Debug` → Build succeeded, 0 Warning(s),
    0 Error(s). `dotnet test Compono.slnx -c Debug` → total 7403, failed 0,
    succeeded 6902, skipped 501 (pre-existing platform/placeholder skips).
    `Compono.Metrics.Tests` per TFM: net8.0 77, net9.0 78, net10.0 78,
    net11.0 78 (net8.0 has no `Gauge<T>`, which is `net9.0+` BCL).
  - `samples/Compono.Samples.BasicUsage` (incl. the new `MetricsTests`)
    → 8 tests, all passing.
  - Packaged all 14 publishable packages at 1.0.0 into a temp folder and ran
    `.github/scripts/inspect-packed-nupkgs.tests.sh` (all regression tests
    passed) and `.github/scripts/inspect-packed-nupkgs.sh <folder>` (all
    package-contents assertions passed). `Compono.Metrics`-specific checks
    confirmed: the expected file shape, `<title>`
    `Compono — System.Diagnostics.Metrics Testing Support`, no `build/` or
    `analyzers/`, and `Compono` pinned at the exact version per TFM group.
  - Native AOT: `dotnet publish test/Compono.Metrics.AotSmokeTest -c Release
    -f net10.0 -r osx-arm64 --self-contained -p:PublishAot=true` produced
    zero IL2xxx/IL3xxx warnings, and the published binary ran to `PASS`
    (exit 0). CI publishes the same project for linux-x64 only (ADR-0072's
    matrix; not extended).
  - `scripts/dogfood-validate.sh` deliberately needed no change: its
    default set is the original four packages and `Compono.Logging` is
    likewise absent; external dogfood is optional per repo policy.
  - The implementation is left **uncommitted** on `docs/165-metrics-planning`
    for independent review; no PR, push, merge, package publish, release-draft
    edit, or issue close was performed. The feature ships in the single combined
    PR (D8, revised); the plan stays `In Progress` until that PR merges.
- 2026-11-11: Independent review (Claude) approved the architecture and public
  API and raised five follow-up corrections; all are applied and validated.
  Delivery changed to **one combined PR** (D8, revised) — the planning
  artifacts, implementation, docs, and both index rows travel on
  `docs/165-metrics-planning`; there is no separate planning-only PR and no
  planning-PR merge gate. Corrections:
  - **Gauge capture is TFM-independent.** `CaptureState.IsCapturedKind<T>` now
    classifies with `instrument is Instrument<T> or ObservableInstrument<T>`
    (both base types exist on every target), replacing the concrete-kind list
    with its `#if NET9_0_OR_GREATER` `Gauge<T>` branch. A net8.0 consumer whose
    runtime provides DiagnosticSource 9.x now records and verifies `Gauge<T>`.
    New `GaugeCaptureTests` (3 tests) prove it on all four TFMs; the net8.0
    test assembly references `System.Diagnostics.DiagnosticSource` 9.0.0
    **test-only** (never a production `PackageReference`, per ADR-0072).
  - **Meter-tag identity is a one-to-one bag match.** `TagSetsEqual` now
    requires each left entry to consume a distinct right entry, so duplicate
    keys are significant (`[k=1, k=1]` ≠ `[k=1, j=2]`). This is intentionally
    stricter than `DefaultMeterFactory`, whose duplicate-tag comparison is not a
    true multiset comparison; the empty-tag case still allocates nothing. Three
    `CapturingMeterFactoryTests` regression tests cover it
    (`Create_ReturnsSameInstance_ForDuplicateKeysWithIdenticalValues`,
    `Create_ReturnsDifferentInstance_ForDuplicateKeysWithDifferentSecondEntry`,
    `Create_ReturnsDifferentInstance_ForDuplicateKeysWithDifferentCount`).
  - **Host DI rule corrected.** A host's `HostApplicationBuilder` already
    registers a default `IMeterFactory`; a test factory wins single-service
    resolution by being the **last** `AddSingleton<IMeterFactory>` registration
    (never `TryAddSingleton`, which keeps the existing one). The extension's
    misuse message and the package guide, host recipe, and troubleshooting docs
    were corrected. Three `HostRecipeTests` prove the default is present, the
    later registration wins, and `TryAddSingleton` doesn't replace.
  - **Verification diagnostics.** Filters join with `with`/`and`; numeric
    values format with the invariant culture; a `WithTag` filter describes the
    **expected** value the test passed - a string stays quoted with no
    annotation, and every other non-null value shows its runtime type (e.g.
    `status = 200 (int)`), so the message never implies it reports the captured
    measurement's actual tag type; the unknown-instrument
    suggestion is appended after the core `CallVerifier` count sentence (and
    names the meter when the two-argument overload was used). `CallVerifier`
    itself is unchanged, and no public API changed.
  - **Regression coverage** added, each test identified by file and method:
    interleaved/merged capture ordering -
    `SnapshotAndDisposalTests.CaptureOrder_ForMergedSelection_OrdersByCaptureSequence_NotByMeterRegistration`
    (fails if the capture-sequence sort is dropped); observable callback that
    throws -
    `ObservableInstrumentTests.RecordObservableInstruments_ThrowingCallback_PropagatesAggregateException_AndRecordsOthers`;
    concurrent `Create`/`Dispose` -
    `CapturingMeterFactoryTests.CreateAndDisposeConcurrently_DisposesSafely_AndRejectsLaterCreate`;
    recording racing disposal -
    `IsolationAndConcurrencyTests.RecordingWhileDisposing_DoesNotThrow_AndStopsCapturingAfterDispose`;
    meaningful clear/record invariants -
    `IsolationAndConcurrencyTests.ConcurrentClearAndRecord_KeepsCaptureConsistent`;
    culture-invariant message formatting -
    `CultureInvarianceTests.FailureMessage_FormatsNumericValueAndTagWithInvariantCulture`;
    unknown meter/instrument diagnostics -
    `SelectionTests.UnknownInstrumentName_NeverPasses_AndFailingOnce_ListsExistingInstruments`
    and `SelectionTests.UnknownInstrumentNameInNamedMeter_ReportsTheMeterAndExistingInstruments`;
    expected-tag-value type annotation -
    `VerificationBuilderTests.FailureMessage_AnnotatesExpectedNonStringTagValueWithItsRuntimeType`.
    Snapshot stability after disposal was already covered by the pre-existing
    `SnapshotAndDisposalTests.Snapshot_StaysReadable_AfterDispose` (not
    re-added).
  - ADR-0072 records these as four dated Amendments; its original Decision text
    is unchanged.
  - Updated evidence: `Compono.Metrics.Tests` now passes **94** tests on
    net9.0/net10.0/net11.0 and **93** on net8.0 (the difference is the single
    in-box `Gauge<T>` test in `InstrumentCaptureTests`, `#if NET9_0_OR_GREATER`);
    `HostRecipeTests` 5/5; full-solution build/test, pack-all
    `inspect-packed-nupkgs.sh`, and the Native AOT publish-and-run are green
    after the corrections.
