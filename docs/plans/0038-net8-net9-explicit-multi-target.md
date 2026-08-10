# [PLAN-0038] net8.0/net9.0 Explicit Multi-Target

**Status:** Done

**Implements:** [ADR-0038](../adr/0038-net8-net9-explicit-multi-target.md)

## Goal

All four packages (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
`Compono.Bogus`) build and restore cleanly targeting `net8.0` and `net9.0`,
in addition to their existing `net10.0;net11.0`, with zero shims/polyfills/
`#if` guards, and the packed-consumer smoke test proving a real `net8.0`
**and** `net9.0` consumer can restore the packed NuGet artifacts, select
the matching TFM asset, compile, and exercise Compono successfully.

## Scope

In scope, per [ADR-0038](../adr/0038-net8-net9-explicit-multi-target.md#decision-outcome):

- Add `net8.0;net9.0` to `TargetFrameworks` on all four packages' `.csproj`
  files.
- Update every CI workflow that installs/references .NET SDK versions
  (`package-validation.yaml`, `docs.yml`, `pr-build.yaml`,
  `publish-release.yaml`, `publish-preview.yaml`) to install `8.0.x`/`9.0.x`
  alongside the existing `10.0.x`/`11.0.x` — widened beyond the original
  plan scope, per direct request, to cover every workflow referencing a
  .NET version, not just the packed-consumer smoke test one.
- Widen the packed-consumer smoke test to real `net8.0`/`net9.0` legs, via
  the packed local-feed artifacts (not project references).
- Update `docs/packages/index.md`'s TFM claim, plus any other doc making
  the same "net10.0/net11.0 only" statement, to current state.

Explicitly deferred (not in this plan):

- Any TFM below `net8.0` (e.g. `net7.0`, `netstandard2.1`, `.NET
  Framework`) — not requested, not evidenced by a real blocked consumer;
  see ADR-0038's Negative Consequences for why this narrower scope was
  accepted over ADR-0037's broader (but non-shimmable) floor.
- Widening `Compono.Generators`'s target — already `netstandard2.0`,
  unaffected by this decision.
- Verifying the `structured-logging` repo specifically — this plan makes
  the package installable there; actually dogfooding in that repo is a
  separate follow-up, not this plan's deliverable.

## Tasks

**Core and integration packages**

- [x] `src/Compono/Compono.csproj` — add `net8.0;net9.0` to `TargetFrameworks`.
- [x] `src/Compono.XunitV3/Compono.XunitV3.csproj` — add `net8.0;net9.0`.
- [x] `src/Compono.NSubstitute/Compono.NSubstitute.csproj` — add `net8.0;net9.0`.
- [x] `src/Compono.Bogus/Compono.Bogus.csproj` — add `net8.0;net9.0`.
- [x] Verify the full solution (all four packages plus every test project)
      builds clean across all four TFMs with zero source changes, zero
      shims, zero new package dependencies — confirmed via
      `dotnet build Compono.slnx -c Release` (0 warnings, 0 errors, all
      TFM legs present in output). No `#if` guards or polyfills needed:
      every gap ADR-0037's audit found (`Random.Shared`,
      `ArgumentNullException.ThrowIfNull`, `required` members,
      `NullabilityInfoContext`, `FrozenDictionary`/`FrozenSet`,
      `DateOnly`/`TimeOnly`, `Enum.GetValuesAsUnderlyingType`) is natively
      available on both `net8.0` and `net9.0`.
- [x] `test/Directory.Build.props` — widened `TargetFrameworks` to
      `net8.0;net9.0;net10.0;net11.0` to match `Compono`'s own TFMs (this
      file's own stated intent: "run every test project against every TFM
      Compono itself ships"), so every unit-test project gets real
      net8.0/net9.0 coverage, not just a compile check.
- [x] `test/Compono.Generators.Tests/Compono.Generators.Tests.csproj` —
      overridden back to `net10.0;net11.0` only (local override wins over
      `test/Directory.Build.props`). This project drives
      `CSharpGeneratorDriver` directly against
      `Basic.Reference.Assemblies.NetXXX` reference-assembly packages that
      only exist for `net10.0`/`net11.0` in this repo's dependencies, and
      it tests `Compono.Generators` (`netstandard2.0`, unaffected by
      ADR-0038) — widening its own host TFM would test nothing new and had
      no matching reference-assembly package, confirmed by a real build
      failure (`CS0246: 'Basic' could not be found`) before this override
      was added.
- [x] Verified each integration package's third-party dependency
      (`xunit.v3.extensibility.core`, `NSubstitute`, `Bogus`) restores and
      resolves under `net8.0`/`net9.0` — confirmed by the same
      solution-wide build/restore above; no dependency issues surfaced.

**CI / packaging**

- [x] `.github/workflows/package-validation.yaml` — SDK install list
      widened to `8.0.x;9.0.x;10.0.x;11.0.x`. The packed-consumer smoke
      test step itself needed no code change: it runs `dotnet test` against
      `Compono.XunitV3.SampleTests.csproj` without an explicit `-f`, so
      widening that project's inherited `TargetFrameworks` (via
      `test/Directory.Build.props`, above) made it exercise all four TFMs,
      including `net8.0`/`net9.0`, automatically. Verified directly: ran
      the exact CI command locally after a clean `.local-nuget-feed`/
      restore-cache wipe — 4 TFM legs, 40/40 tests passed, each leg
      restoring the packed local-feed `.nupkg` artifacts (not a
      `ProjectReference`).
- [x] `.github/workflows/docs.yml` — SDK install list widened to match.
      Confirmed its "build for net10.0" API doc-generation step needs no
      `net8.0`/`net9.0` counterpart: diffed the public type-name surface
      between a `net8.0` and a `net10.0` build of `Compono.dll` directly
      (`strings ... | grep Compono\. | sort -u`, zero diff) — the public
      surface is identical across all four TFMs, so one canonical API
      reference generation remains correct.
- [x] `.github/workflows/pr-build.yaml`, `publish-release.yaml`,
      `publish-preview.yaml` — SDK install lists widened to match (beyond
      original plan scope; every workflow referencing a .NET version now
      installs all four SDKs, not just the two directly touched by the
      original Tasks list).
- [x] Confirmed `dotnet pack` output includes all four TFMs' assemblies in
      the resulting `.nupkg` — verified directly against `src/Compono`'s
      pack output (`lib/net8.0/Compono.dll`, `lib/net9.0/Compono.dll`,
      `lib/net10.0/Compono.dll`, `lib/net11.0/Compono.dll`, all present).

**Docs**

- [x] `docs/packages/index.md` — TFM claim updated to
      `net8.0`/`net9.0`/`net10.0`/`net11.0`, linking ADR-0038 for why.
- [x] `docs/getting-started/installation.md` — TFM claim updated to match.
- [x] `docs/contributing.md` — "solution targets" TFM claim updated to
      match, plus its "install both SDKs" wording corrected to "install
      all four SDKs."
- [x] `docs/packages/compono.md`, `compono-xunitv3.md`,
      `compono-nsubstitute.md`, `compono-bogus.md` — checked; none state
      a TFM claim of their own (they defer to `docs/packages/index.md`),
      no change needed.
- [x] `docs/roadmap/post-mvp.md` — checked; no stale cross-reference
      introduced (this finding didn't originate from the ADR-0029
      dogfooding framework, so it was never listed there).
- [x] `skills/compono/SKILL.md` and `skills/compono/references/*.md` —
      checked; no TFM claim present in either, no change needed.

**Tests**

- [x] Full solution test run (`dotnet test Compono.slnx -c Release`)
      passes on all four TFMs: 1645/1645 passed, 0 failed, 0 skipped (up
      from 913 pre-widen, since most unit-test projects now also run on
      `net8.0`/`net9.0` per the `test/Directory.Build.props` widen above).

## Critical Files

- `src/Compono/Compono.csproj`, `src/Compono.XunitV3/Compono.XunitV3.csproj`, `src/Compono.NSubstitute/Compono.NSubstitute.csproj`, `src/Compono.Bogus/Compono.Bogus.csproj` — `TargetFrameworks` widened to `net8.0;net9.0;net10.0;net11.0`. No other source changes.
- `test/Directory.Build.props` — `TargetFrameworks` widened to match, so every test project (except the one explicit override below) runs on all four TFMs.
- `test/Compono.Generators.Tests/Compono.Generators.Tests.csproj` — local `TargetFrameworks` override back to `net10.0;net11.0` only (reference-assembly package availability, see Tasks).
- `.github/workflows/package-validation.yaml`, `docs.yml`, `pr-build.yaml`, `publish-release.yaml`, `publish-preview.yaml` — SDK install lists widened to `8.0.x;9.0.x;10.0.x;11.0.x`.
- `docs/packages/index.md`, `docs/getting-started/installation.md`, `docs/contributing.md` — TFM claim updates.
- `docs/adr/0037-netstandard2.1-compatibility-floor.md` — `Status` changed to `Superseded by ADR-0038`.
- `docs/adr/0031-public-preview-release-and-versioning-policy.md` — Amendment 3 added, recording that ADR-0038 widens (not just floors) the rolling TFM window Amendment 2 described.

## Test Plan

- Full solution test run passes on all four TFMs unchanged (no behavior
  divergence anywhere — verified: 1645/1645 passed).
- Packed-consumer smoke test extended to `net8.0` and `net9.0` consumer
  legs, restoring the packed local-feed artifacts (not project
  references) — this is the test that actually proves the original
  blocker (`structured-logging`, .NET 8/9) is fixed end to end, not just
  that `Compono` compiles for those TFMs in isolation. Verified directly:
  40/40 tests passed across all four TFM legs.

## Notes

This plan implements ADR-0038, which superseded
[ADR-0037](../adr/0037-netstandard2.1-compatibility-floor.md) after that
ADR's own required implementation-time compatibility audit (run under
[PLAN-0037](0037-netstandard2.1-compatibility-floor.md)) found two
non-shimmable gaps (`DateOnly`/`TimeOnly`, `Enum.GetValuesAsUnderlyingType`)
that would have forced either a real public-surface divergence between
TFMs or reintroducing a reflection path ADR-0001/PR #11 had already
rejected. See ADR-0038's Decision Outcome for the full reasoning, and
PLAN-0037's Notes for how the audit unfolded.

The `TargetFrameworks` change and its zero-shim verification (a full
solution build across all four TFMs, 0 warnings/0 errors) were completed
as part of reaching this decision, before this plan's remaining CI/docs
tasks were carried out.

Mid-implementation scope widen: asked directly to update every `.github`
workflow file referencing a .NET version, not just
`package-validation.yaml` — `pr-build.yaml`, `publish-release.yaml`, and
`publish-preview.yaml` were added to Tasks/Critical Files/Scope as a
result, beyond the original plan's narrower CI section.
