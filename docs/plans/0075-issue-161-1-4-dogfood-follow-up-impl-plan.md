# [PLAN-0075] Issue #161 1.4 Dogfood Follow-Up

**Status:** Done (local; awaiting maintainer review before any PR)

**Implements:** [ADR-0072](../adr/0072-compono-metrics-testing-support-package.md)
Amendments 5–6 and [ADR-0055](../adr/0055-compono-logging-testing-support-package.md)
Amendment 7. Triage and decisions:
[RESEARCH-0040](../research/0040-issue-161-1-4-dogfood-follow-up.md).
Beads epic: `compono-gec`.

## Goal

The seven #161 follow-up findings are resolved as RESEARCH-0040 decided.
`IMeterFactory` never gets a generated double when `Compono.Metrics` is
referenced. `LogVerificationBuilder` has `InCategoryPrefix` and
`NotInCategoryPrefix`. The documentation-only items (ComposeData binding
and `TConfig` access, per-engine disposal, the `ClearMeasurements()`
multi-pass pattern, profile constructor-argument patterns, and a
migration guide from `Microsoft.Extensions.Diagnostics.Testing`) ship
with tested examples. Build and tests are green on every TFM, and the
relevant AOT smoke tests and pack-consume checks pass.

## Scope

In scope: findings 1, 2, 3, 4 (docs only), 5, 6 (docs only), and 7,
per RESEARCH-0040's summary table. One PR, because the changes are small,
all trace back to one dogfood report, and every behavioral change is
additive or pre-stable.

Explicitly deferred or rejected (RESEARCH-0040): metrics checkpoint,
`Since`, or scoped-capture APIs; `ProfileMember`/profile-factory
activation; implicit or named `ComposeData` binding of `TConfig`;
Compono-owned automatic disposal on any engine; provider-precedence
changes; a general log-filter negation combinator.

## Tasks

### C0: Research and decision records (`compono-gec.1`)

- [x] RESEARCH-0040, covering all seven findings, with the empirical xUnit
      disposal probe.
- [x] ADR-0072 Amendment 5 (generator exclusion, superseding "No generator
      exclusion is added") and Amendment 6 (per-engine disposal
      clarification).
- [x] ADR-0055 Amendment 7 (`InCategoryPrefix`/`NotInCategoryPrefix`).
- [x] This plan and its `docs/plans/README.md` row (`docs/research/` has no index).

### C1: Metrics owns `IMeterFactory` (`compono-gec.2`)

- [x] `GeneratorFeatureFlags` gains `MetricsOwnsMeterFactory`, read from
      `build_property.ComponoMetricsOwnsMeterFactory`.
- [x] `TransitiveClosureWalker` skips `IMeterFactory` in
      `TryRecordTestDouble` when the flag is on. The symbol is resolved
      once per walk context, only when the flag is on.
- [x] Core `src/Compono/build/Compono.props` declares the
      `CompilerVisibleProperty`.
- [x] New `src/Compono.Metrics/build/Compono.Metrics.props` defaults the
      flag to `true`, packed to `build/` and `buildTransitive/`.
- [x] `UseMetrics()` remarks and the `RequireCapturing` misuse message name
      `UseGeneratedTestDoubles()` and the ownership rule.
- [x] Generator tests: flag on, flag off, unset, unparseable, flag on
      with TestDoubles disabled, and a sibling interface still doubled
      (`MetricsTestDoubleOwnershipTests`). The "type unresolvable" case was
      dropped: the in-memory harness always references the BCL, and the code
      path is a `null` check on the resolved symbol.
- [x] `Compono.Metrics.Tests` runtime tests (TestDoubles generation
      enabled): both orders with `UseGeneratedTestDoubles()`;
      `UseGeneratedTestDoubles()` without `UseMetrics()`.
- [x] Pack-consume check: the packed `Compono.Metrics` props set the flag in
      a consumer.

### C2: Logging prefix filters (`compono-gec.3`)

- [x] `LogVerificationBuilder.InCategoryPrefix` / `NotInCategoryPrefix`.
- [x] Tests: positive, negative, the literal boundary (`MyApp.Jobs` vs
      `MyApp.JobsLegacy`, trailing dot), case sensitivity, `null`, empty
      prefix, a failure description, `LogCollector` host path.
- [x] API reference regenerated (`docs/reference/api`).

### C3: Documentation-only findings (`compono-gec.4`)

- [x] Finding 2: `ComposeData` leading-positional ordering and the
      register-the-config recipe, in the xUnit v3 package guides and the
      cookbook. A JIT test and an AOT test prove the recipe.
- [x] Finding 3: per-engine disposal guidance in the Metrics package guide
      (pattern 1) and the xUnit v3 guide.
- [x] Finding 4: `ClearMeasurements()` multi-pass example in the Metrics
      guide.
- [x] Finding 6: wrapper-profile and single-row `ComposeData` patterns in
      `how-to/use-profiles.md`, backed by a JIT test and an AOT test
      (`DocumentedRecipeTests` in both xUnit test projects; the AOT one also
      runs as a Native AOT-published binary).

### C4: Migration guide (`compono-gec.5`)

- [x] `docs/migrating-from-microsoft-extensions-diagnostics-testing.md`,
      linked from the `mkdocs.yml` nav, the Logging/Metrics package guides'
      "Next" sections, and the troubleshooting page.
- [x] Compiled examples in `Compono.Logging.Tests` and
      `Compono.Metrics.Tests`.

### C5: Skill (`compono-gec.6`)

- [x] `skills/compono/references/metrics.md`, `logging.md`, `xunit-v3.md`,
      and `registrations-profiles-and-scopes.md` updated.
- [x] `skills/compono/evals/evals.json` gets evals for the new guidance.

### C6: Validation (`compono-gec.7`)

- [x] `dotnet build`: 0 warnings, 0 errors.
- [x] `dotnet test`: full solution, every TFM.
- [x] Metrics/Logging/XunitV3.Aot AOT smoke tests, and pack-consume for
      the generator and props change.
- [x] Plan status, Beads, and the Notes section updated.

## Critical Files

- `src/Compono.Generators/Models/GeneratorFeatureFlags.cs`,
  `ComponoIncrementalGenerator.cs`, `Discovery/TransitiveClosureWalker.cs`:
  the exclusion.
- `src/Compono/build/Compono.props`, `src/Compono.Metrics/build/Compono.Metrics.props`,
  `src/Compono.Metrics/Compono.Metrics.csproj`: the flag wiring.
- `src/Compono.Metrics/CompositionBuilderExtensions.cs`,
  `MeterFactoryTestingExtensions.cs`: remarks and the message.
- `src/Compono.Logging/LogVerificationBuilder.cs`: the new filters.
- `test/Compono.Generators.Tests`, `test/Compono.Metrics.Tests`,
  `test/Compono.Logging.Tests`, `test/Compono.XunitV3.Tests`,
  `test/Compono.XunitV3.Aot.*`: tests.
- `docs/packages/compono-{metrics,logging,xunitv3,xunitv3-aot}.md`,
  `docs/how-to/use-profiles.md`, `docs/cookbook/vary-composed-configuration-per-theory-row.md`,
  the new migration guide, and `skills/compono/**`.

## Test Plan

Generator unit tests use the in-memory harness, the same pattern as
`LoggingTestDoubleOwnershipTests`. Runtime regressions use real generated
output in `Compono.Metrics.Tests`, the same pattern as
`LoggingTestDoubleOwnershipCompileRegressionTests`. Builder tests cover
the logging filters. Documentation recipes get compiled, running tests on
JIT, plus an AOT test or sample where the recipe touches AOT.

## Notes

- 2026-11-11: xUnit v3 4.0.1 JIT was confirmed empirically to dispose
  `IDisposable`/`IAsyncDisposable` method arguments once per argument slot
  (RESEARCH-0040 finding 3). The probe was a temporary test, deleted after
  the run.
- 2026-11-11: the `Compono.Metrics.Tests` ownership regressions were
  confirmed to bite. Rebuilding with `-p:ComponoMetricsOwnsMeterFactory=false`
  fails 3 of the 6 `MeterFactoryOwnershipTests`.
- 2026-11-11 validation (macOS arm64):
  - `dotnet build`: 0 warnings, 0 errors.
  - `dotnet test` (full solution, every TFM): 7,625 total, 0 failed, 501
    skipped.
  - `mkdocs build --strict`: clean.
  - `test/Compono.Metrics.AotSmokeTest/verify-packaging.sh`: PASS, including
    the new packed-consumer ownership check
    (`UseGeneratedTestDoubles().UseMetrics()` gives a `CapturingMeterFactory`,
    and the sibling interface is doubled).
  - Metrics and Logging AOT smoke publish-and-run (net10.0, osx-arm64): PASS.
  - `Compono.XunitV3.Aot.Tests` Native AOT publish-and-run: 281 total, 0
    failed; `DocumentedRecipeTests` 6/6.
- Scope note: `test/Compono.Metrics.AotSmokeTest/pack-compono.sh` now also
  packs `Compono.TestDoubles`, for the ownership consumer check.
  `.github/scripts/inspect-packed-nupkgs.sh` expects the new props files in
  `Compono.Metrics.nupkg`.
- 2026-11-12 review follow-up:
  - The migration-guide `[Compose<MetricsProfile>]` example now has a real
    test (`samples/Compono.Samples.BasicUsage/MigrationGuideMetricsTests.cs`).
  - Disposal wording is engine-specific in the troubleshooting page and evals
    39, 66, and 70.
  - TUnit root-argument disposal now cross-references ADR-0040 (documented
    behavior, not re-tested by Amendment 6).
- 2026-11-12 intermittent failure, root-caused:
  `Compono.Logging.Tests.MigrationGuideExamplesTests.HostWideCapture_*`
  failed once during the earlier review round (unidentified at the time).
  It failed again on net10.0 in the first TRX-captured full run:
  "Expected exactly 1 call(s) ... category Compono.Logging.Tests.CheckoutWorker
  ..., but received 0".
  - Cause: a race in the new test's own fixture. The example worker was a
    `BackgroundService`, and since .NET 10 `ExecuteAsync` runs on a background
    task, so `StopAsync` could cancel it before it logged.
  - Fix: the worker is now an `IHostedService` that logs in `StartAsync`, and
    the guide notes the `BackgroundService` caveat.
  - After the fix: 15 targeted runs (all 4 TFMs) had 0 failures. A full xUnit
    TRX run had 7,062 total, 0 failed, 47 TRX files, and no Failed outcomes.
    A plain full suite had 7,626 total, 0 failed, 501 skipped. TUnit/MSTest
    TRX runs passed.
  - NUnit's runner has no TRX extension here, so it's covered by the plain run.
- Not done: external dogfood (`scripts/dogfood-validate.sh`, optional per
  AGENTS.md) and xUnit 3.2.2 lower-bound reruns. Nothing was pushed, and no
  PR was opened.
