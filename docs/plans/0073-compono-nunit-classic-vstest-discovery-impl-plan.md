# [PLAN-0073] Compono.NUnit classic-VSTest discovery-time module initialization (Issue #171)

**Status:** In Progress

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
  genuinely non-MTP artifact and runs it under `dotnet vstest`.
- Edge-case + concurrency/idempotency tests for the module-selection
  guarantee (inherited / nested / generic / cross-assembly / concurrent
  discovery).
- ADR-0059 Amendment 1; update `docs/packages/compono-nunit.md` and
  `docs/architecture/current/generated-plans-and-discovery.md`.
- Focused defect record: issue #171 and bead `compono-03n`.

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
- [x] Add `ModuleInitializationTests.cs` (inherited, nested, closed
      generic, cross-assembly, concurrent/idempotent).
- [x] Add `CrossAssemblyComposeFixtures` to `Compono.NUnit.SignatureFixtures`.
- [x] Implement the `NUnitMatrixRunner` property in the matrix csproj.
- [x] Rewrite the matrix script to build classic (non-MTP) + MTP artifacts
      per NUnit version.
- [x] Amend ADR-0059 (Amendment 1).
- [x] Update `docs/packages/compono-nunit.md` and
      `docs/architecture/current/generated-plans-and-discovery.md`.
- [x] File issue #171 and bead `compono-03n`.
- [x] Run the full validation gates (below) and record results in Notes.
- [ ] Commit, push, open PR to `main` (do not merge).

## Critical Files

- `src/Compono.NUnit/ComposeAttribute.cs`
- `.github/scripts/nunit-compatibility-matrix.sh`
- `test/Compono.NUnit.CompatibilityMatrix/Compono.NUnit.CompatibilityMatrix.csproj`
- `test/Compono.NUnit.Tests/ModuleInitializationTests.cs`
- `test/Compono.NUnit.SignatureFixtures/InvalidSignatureFixtures.cs`
- `docs/adr/0059-compono-nunit-package-design.md`
- `docs/packages/compono-nunit.md`
- `docs/architecture/current/generated-plans-and-discovery.md`

## Test Plan

- `test/Compono.NUnit.Tests/ModuleInitializationTests.cs`: module selection
  for inherited methods, nested types, closed generic fixtures, and
  cross-assembly methods; concurrent discovery idempotency (400 parallel
  `BuildFrom` calls).
- `.github/scripts/nunit-compatibility-matrix.sh`: NUnit `3.14.0` (floor)
  and current-stable `4.*`, each under classic VSTest (genuinely classic)
  and MTP; NUnit `5.0.0-beta.1` surveillance (non-blocking).

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
