# [PLAN-0073] Compono.NUnit classic-VSTest discovery-time module initialization (Issue #171)

**Status:** Done

**Implements:** [ADR-0059](../adr/0059-compono-nunit-package-design.md)
Amendment 1 (issue [#171](https://github.com/LayeredCraft/compono/issues/171))

## Goal

Make `Compono.NUnit` genuinely work under the classic VSTest test host by
ensuring the consumer test assembly's generated `[ModuleInitializer]`s have
run before NUnit discovers/builds `[Compose]` test cases, and correct the
NUnit compatibility matrix so its classic leg actually exercises the classic
`DotnetTestHostManager` rather than silently falling back to MTP.

## Scope

**In:**

- Force the test method's declaring module constructor at the top of
  `ComposeAttribute.BuildFrom`, before binding-plan construction
  (`RuntimeHelpers.RunModuleConstructor`).
- Correct `.github/scripts/nunit-compatibility-matrix.sh` +
  `test/Compono.NUnit.CompatibilityMatrix` so the classic leg builds a
  genuinely non-MTP artifact, runs it under `dotnet vstest`, and positively
  asserts which host actually ran.
- Matrix coverage for the discovery-time guarantee across `[Compose]`
  shapes (plain, inherited, nested); the genuine classic-VSTest matrix leg
  is the authoritative cold-initialization regression test.
- ADR-0059 Amendment 1; update `docs/packages/compono-nunit.md` and
  `docs/architecture/current/generated-plans-and-discovery.md`.
- Focused defect record: issue #171, the MSTest-under-classic-VSTest
  follow-up issue, and bead `compono-03n`.

**Out:**

- No change to any other package, no change to `Compono.NUnit`'s public API.
- No reflection fallback (ADR-0001); no runtime assembly scanning.
- Not part of the .NET 11 GA migration PR; does not touch the v1.4.0 draft.

## Implementation specification

See ADR-0059 Amendment 1. Summary:

1. `src/Compono.NUnit/ComposeAttribute.cs` — in `BuildFrom`, immediately
   after `var reflectedMethod = method.MethodInfo;`, call
   `RuntimeHelpers.RunModuleConstructor(reflectedMethod.DeclaringType!.Module.ModuleHandle);`
   (`using System.Runtime.CompilerServices;`). Idempotent, AOT-safe,
   reflection-free, no new public surface.
2. Compatibility matrix — implement the documented-but-unimplemented
   `NUnitMatrixRunner` property (`MTP` default / `VSTest`). The `VSTest`
   build sets `OutputType=Library`, turns off
   `EnableNUnitRunner`/`UseMicrosoftTestingPlatformRunner`/
   `TestingPlatformDotnetTestSupport`, and turns on
   `CopyLocalLockFileAssemblies`/`GenerateRuntimeConfigurationFiles`. The
   script builds the classic artifact first, runs `dotnet vstest`, then
   builds the MTP artifact and runs the exe.

## Tasks

### Bead 1 (`compono-03n`): discovery-time module initialization + matrix correction

- [x] Implement `RuntimeHelpers.RunModuleConstructor` in
      `ComposeAttribute.BuildFrom`.
- [x] Implement the `NUnitMatrixRunner` property in the matrix csproj.
- [x] Rewrite the matrix script to build classic (non-MTP) + MTP artifacts
      per NUnit version.
- [x] Extend the matrix's own tests with plain, inherited, and nested
      `[Compose]` shapes so the genuinely-classic leg is the authoritative
      cold-initialization regression test.
- [x] Positively verify the host each matrix leg ran under
      (`TestHostVerificationTests` + `COMPONO_NUNIT_MATRIX_EXPECTED_HOST`),
      failing the leg if it silently ran under MTP.
- [x] Amend ADR-0059 (Amendment 1).
- [x] Update `docs/packages/compono-nunit.md` and
      `docs/architecture/current/generated-plans-and-discovery.md`.
- [x] File issue #171 and bead `compono-03n`.
- [x] File the MSTest-under-classic-VSTest follow-up issue.
- [x] Run the full validation gates (below) and record results in Notes.
- [x] Commit, push, open PR to `main` (do not merge).

### Review remediation (PR #172)

- [x] Remove `ModuleInitializationTests.cs` and revert the
      `Compono.NUnit.SignatureFixtures` addition: those tests run under MTP
      (the test assembly is the entry assembly, so its module initializers
      have already run), so they cannot exercise cold initialization; the
      genuine classic-VSTest matrix leg is the authoritative regression
      test instead.
- [x] Remove the unsupported "`Compono.MSTest` is unaffected" claim from
      `generated-plans-and-discovery.md`; narrow the ADR-0059 Amendment 1
      module-selection wording with the open-type-parameter evidence.
- [x] Address the actionable GitHub Codex review findings.

## Critical Files

- `src/Compono.NUnit/ComposeAttribute.cs`
- `.github/scripts/nunit-compatibility-matrix.sh`
- `test/Compono.NUnit.CompatibilityMatrix/Compono.NUnit.CompatibilityMatrix.csproj`
- `test/Compono.NUnit.CompatibilityMatrix/MatrixTests.cs`
- `docs/adr/0059-compono-nunit-package-design.md`
- `docs/packages/compono-nunit.md`
- `docs/architecture/current/generated-plans-and-discovery.md`

## Test Plan

- `.github/scripts/nunit-compatibility-matrix.sh`: NUnit `3.14.0` (floor)
  and current-stable `4.*`, each under classic VSTest (genuinely classic,
  positively verified) and MTP; NUnit `5.0.0-beta.1` surveillance
  (non-blocking). The matrix project's own `MatrixTests` cover the plain,
  inherited, and nested `[Compose]` shapes plus the per-leg host
  verification; this is the authoritative cold-initialization regression
  test.
- `test/Compono.NUnit.Tests` runs under MTP, where the test assembly is the
  entry assembly and its module initializers have already run, so it cannot
  exercise cold initialization; no test there claims to.

## Validation commands

- **V1:** `dotnet build` + `dotnet test` (full solution, all TFMs) —
  0 warnings / 0 errors, all green.
- **V2:** `dotnet test test/Compono.NUnit.Tests/Compono.NUnit.Tests.csproj -c Release`.
- **V3:** `.github/workflows/aot-validation.yaml` legs
  (`dotnet publish -f net10.0 -p:PublishAot=true -r osx-arm64` on
  `test/Compono.NUnit.AotSmokeTest`, then run); confirm no new
  Compono-attributable IL/AOT warnings.
- **V4:** `.github/scripts/nunit-compatibility-matrix.sh`.
- **V5:** `.github/scripts/inspect-packed-nupkgs.sh` (+ ApiCompat via the
  repository's package-validation path).

## Notes

- 2026-11-10 — Root cause established: the classic VSTest test host builds
  NUnit test cases before the consumer assembly's generated
  `[ModuleInitializer]`s run; `RowInvokerRegistry` is empty at discovery.
  Not a .NET 11 GA regression. Prior "classic VSTest" matrix leg was a false
  green (`vstest.console` 18.10.0 auto-routed the MTP-enabled artifact to
  `MtpTestRuntimeProvider`; 18.11.0+ selects the real classic host).
- 2026-11-10 — Fix validated end-to-end: matrix green for NUnit 3.14.0 and
  4.6.1 under both classic VSTest (real classic host, `VSTest version
  18.12.0`) and MTP; Native AOT publish+run for `[Compose]` and
  `[Compose<TProfile,TConfig>]`; only pre-existing `nunit.framework`
  IL3053/IL2104 warnings. `test/Compono.NUnit.Tests` 232 passed / 0 failed
  across net8/9/10/11.
- 2026-11-10 — V1 full solution build: 0 warnings / 0 errors. V2 full
  solution test: 6823 total, 0 failed, 6322 passed, 501 skipped.
  V4 matrix: blocking legs 3.14.0 + 4.6.1 (classic VSTest + MTP) green,
  5.0.0-beta.1 surveillance green. V5: all 13 packages packed and inspected
  (`inspect-packed-nupkgs.tests.sh` + `inspect-packed-nupkgs.sh` both pass;
  `Compono.NUnit.nuspec` still exact-pins `Compono` and constrains NUnit to
  `[3.14.0, 5.0.0)`), and package validation / ApiCompat produced no `CP`
  errors against the `1.4.0-preview.123` baseline.
- 2026-11-10 — PR [#172](https://github.com/LayeredCraft/compono/pull/172)
  opened against `main` (not merged; pending maintainer review). Status stays
  `In Progress` until it merges.
- 2026-11-10 — PR review remediation: removed the MTP-warm
  `ModuleInitializationTests.cs` and its `Compono.NUnit.SignatureFixtures`
  helper — running under MTP they cannot exercise cold initialization — and
  replaced them with plain/inherited/nested coverage in the matrix project
  plus a positive per-leg host assertion (`TestHostVerificationTests` +
  `COMPONO_NUNIT_MATRIX_EXPECTED_HOST`); the genuine classic-VSTest matrix
  leg is now the authoritative regression test. Removed the unsupported
  "`Compono.MSTest` is unaffected" claim and narrowed the ADR-0059
  Amendment 1 module-selection wording with the open-type-parameter evidence
  (`ComposedTypeAnalyzer.IsRowInvokerShapeEligible`); filed the
  MSTest-under-classic-VSTest follow-up issue
  ([#173](https://github.com/LayeredCraft/compono/issues/173)).
- 2026-11-10 — PR review remediation validated: V1 full solution build
  0 warnings / 0 errors; V2 full solution test 6791 total, 0 failed, 6290
  succeeded, 501 skipped; V4 matrix green for NUnit 3.14.0 and 4.6.1 under
  both classic VSTest (`VSTest version 18.12.0`, genuine
  `DotnetTestHostManager`) and MTP — 5/5 per leg including the per-leg host
  assertion — plus NUnit 5.0.0-beta.1 surveillance; V3 Native AOT
  publish+run PASS for `[Compose]` and `[Compose<TProfile,TConfig>]` with
  only pre-existing `nunit.framework` IL3053/IL2104; V5 all 13 packages
  packed and inspected with no `CP` errors; API-reference drift check clean
  (no public-surface change).
- 2026-11-10 — Finding dispositions: the cross-assembly inherited *generic*
  fixture scenario is not a wrong-module defect — the generator never emits a
  row-invoker registration for a parameter typed as an open type parameter
  (`ComposedTypeAnalyzer.IsRowInvokerShapeEligible` returns false), so it
  fails identically on every host; no supported shape needs a module other
  than the `[Compose]` method's declaring module. `DeclaringType` is always
  non-null for a `[Compose]` method (a method attribute cannot be applied
  without a declaring type), so no null guard is required.
- 2026-11-11 — PR [#172](https://github.com/LayeredCraft/compono/pull/172)
  merged into `main` (merge commit `3b87b6f`, 2026-11-11T01:17:08Z); issue
  [#171](https://github.com/LayeredCraft/compono/issues/171) auto-closed by
  the merge; bead `compono-03n` closed. Status set to `Done`.
