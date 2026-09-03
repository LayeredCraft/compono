# [PLAN-0062] Package-Validation Gap Fixes

**Status:** In Progress

**Implements:** [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)
(implementation-correctness follow-up — no amendment; ADR-0031 requires the
outcomes below, this plan only corrects how CI enforces them)

## Goal

Close the two real gaps [RESEARCH-0021](../research/0021-package-validation-execution-policy-audit.md)
found in `package-validation.yaml` and its scripts: `Compono.Logging`'s
packed-nupkg content is never inspected, and CS1591 enforcement runs as a
redundant second full rebuild of all 11 packages instead of as part of the
ordinary build every PR already does. Done when `inspect-packed-nupkgs.sh`
checks all 11 publishable packages with package-specific invariants
correct for `Compono.Logging`'s real shape, and CS1591-as-error is enforced
via `Directory.Build.targets` with the standalone workflow step removed.

## Scope

**In scope — two substantive changes only:**
1. Correct `inspect-packed-nupkgs.sh`'s real `Compono.Logging` packed-nupkg
   inspection coverage gap — not merely add its name to the loop, but add
   the package-specific invariants its actual `.nupkg` shape requires
   (see "`Compono.Logging` inspection" below).
2. Move CS1591-as-error enforcement into the ordinary build for publishable
   packages (`Directory.Build.targets`, scoped `$(IsPackable) != 'false'`)
   and remove the redundant `package-validation.yaml` rebuild step.

**Explicitly deferred — NUnit compatibility-matrix applicability scoping**
(revised disposition, narrowing RESEARCH-0021's original Tier 2
recommendation): dropped from this plan's scope entirely, not merely left
optional. RESEARCH-0021 established NUnit as the *safest candidate* for
selective execution, but safety of the mechanism is not itself evidence
that building it now is warranted. There is no demonstrated CI-duration
problem — the whole `package-validation.yaml` job runs in ~3 minutes total,
and removing the redundant CS1591 rebuild (this plan's second change) may
shrink that further without adding any new change-detection machinery.
Building an applicability mechanism speculatively, with no measured
duration problem to justify it, is exactly the kind of premature
optimization this repo's cleanup work has repeatedly declined elsewhere.
**Recorded disposition: deferred/declined, absent future evidence that
`package-validation.yaml`'s duration becomes a meaningful problem in
practice.** Revisit only if that evidence appears.

**Explicitly deferred** (per RESEARCH-0021's own recommendation, with
evidence, unchanged from the original research record):
- No applicability-aware redesign of the pack/baseline-compare/
  nuspec-inspection/`*SampleTests`-smoke steps — they stay universal.
  `publish-preview.yaml`/`publish-release.yaml` perform zero re-validation
  of their own, so `package-validation.yaml` is the only safety net these
  invariants ever get; under-testing them via a fragile applicability
  script is a real risk with no backstop.
- No PR-validation/release-validation two-tier split — no release-time
  validation tier exists today to receive an "exhaustive" half, and no
  evidence in this repo shows the current single-gate model has caused a
  problem.

## `Compono.Logging` inspection — treated as a correctness bug, not cosmetic

Pre-1.0 compatibility policy (ADR-0031's `0.x` policy) may intentionally
permit public API evolution without triggering the baseline-compatibility
gate — that tolerance is scoped narrowly to API *shape* changes under a
`breaking-change` label. It says nothing about, and does not excuse,
malformed or incorrect *package contents*: a wrong dependency range, a
missing lockstep pin, or an incomplete file listing are packaging defects
regardless of what version line Compono is on. `Compono.Logging`'s missing
inspection coverage is treated accordingly — a real, silently-uncaught
correctness gap, not a nice-to-have consistency fix.

Verified (not assumed) against a real local pack of `Compono.Logging`,
which needs invariants distinct from every other integration package
already covered:
- **File listing**: `build/Compono.Logging.props` and
  `buildTransitive/Compono.Logging.props` (defaulting
  `ComponoGeneratedLogging` to `true`, per ADR-0055 Amendment 3) are extra
  files beyond the common template — but, unlike core `Compono`, **no**
  `analyzers/dotnet/cs/*.dll` entry: `Compono.Logging` ships no generator
  of its own (ADR-0055 Amendment 3 moved its generation into the existing
  `Compono.Generators`, embedded only in `Compono.nupkg`).
- **Third-party dependency range**: `Microsoft.Extensions.Logging.Abstractions`
  is the *only* third-party dependency in the whole package set whose
  `Directory.Packages.props` range is conditioned per `$(TargetFramework)`
  (net8.0/net9.0/net10.0 each track a different BCL version) — the
  existing `assert_dependency_range` function's single authoritative-value
  lookup can't see this. `net11.0` carries **no** such dependency entry at
  all in the packed nuspec (satisfied by net11.0's own shared framework) —
  a state that must be asserted explicitly, not left unchecked, so a
  regression in either direction is caught.
- **Lockstep `Compono` pin**: identical mechanism to every other
  integration package — `assert_exact_pin_dependency` applies unchanged.

## Tasks

- [x] `.github/scripts/inspect-packed-nupkgs.sh`: add a new
      `assert_dependency_range_per_tfm` function for the per-TFM-varying
      dependency case (re-evaluates `Directory.Packages.props` once per
      TFM via `dotnet msbuild -getItem:PackageVersion -p:TargetFramework=X`,
      and explicitly asserts the `net11.0` absence).
- [x] `.github/scripts/inspect-packed-nupkgs.sh`: add `Compono.Logging` to
      the `main()` package loop with its correct `extra_paths` (the two
      `.props` files, no analyzer) and `case` branch (title, lockstep pin,
      per-TFM range assertion).
- [x] `.github/scripts/inspect-packed-nupkgs.sh`: fix the stale header
      comment ("all seven publishable Compono packages" → accurate).
- [x] `.github/scripts/inspect-packed-nupkgs.tests.sh`: add regression
      coverage for `assert_dependency_range_per_tfm` — a passing case
      (real `Directory.Packages.props` values), a failing case (one TFM's
      range disagrees), and a failing case (an unexpected `net11.0` entry)
      — so this coverage cannot silently disappear or go stale again.
- [x] `Directory.Build.targets`: add
      `<PropertyGroup Condition="'$(IsPackable)' != 'false'"><WarningsAsErrors>$(WarningsAsErrors);CS1591</WarningsAsErrors></PropertyGroup>`.
- [x] `.github/workflows/package-validation.yaml`: remove the "Enforce XML
      doc comments (CS1591)" step; fix the two stale comments that
      referenced it (job-level `env:` comment, `BREAKING_CHANGE` comment
      context).
- [x] Positive enforcement proof: temporarily added an undocumented public
      member to a real packable package (`Compono.Http`), confirmed an
      ordinary `dotnet build` (and separately `dotnet pack`) fails with a
      real `CS1591` error, removed the temporary member.
- [x] Non-packable boundary proof: temporarily added an undocumented
      public member to a non-packable, non-test project with no local
      `NoWarn` override (`Compono.Samples.AspNetApi`), confirmed the build
      succeeds with `CS1591` remaining an ordinary warning (not promoted to
      an error), removed the temporary member.
- [x] Full solution build (`dotnet build Compono.slnx`) to confirm no
      sample/benchmark/test/fixture project unexpectedly regresses under
      the new `WarningsAsErrors` scoping.
- [x] Confirmed no subsystem doc/ADR describes the CS1591 enforcement
      step's specific CI shape (only the XML-doc-comment *requirement*
      itself, in `documentation.md`/ADR-0031, which is unchanged) — no
      documentation update needed beyond this plan/RESEARCH-0021 themselves.

## Critical Files

- `.github/scripts/inspect-packed-nupkgs.sh` — new
  `assert_dependency_range_per_tfm` function, `Compono.Logging` coverage.
- `.github/scripts/inspect-packed-nupkgs.tests.sh` — regression coverage
  for the new function.
- `Directory.Build.targets` — new scoped `WarningsAsErrors` for CS1591.
- `.github/workflows/package-validation.yaml` — removed the redundant
  step; fixed two stale comments.

## Test Plan

- `inspect-packed-nupkgs.tests.sh` covers `assert_dependency_range_per_tfm`
  directly (pass/fail/fail-on-unexpected-presence), plus the existing
  regression suite for every other function, run locally: all pass.
- A real local pack of all 11 publishable packages plus a full
  `inspect-packed-nupkgs.sh` run against them: all pass, including
  `Compono.Logging`'s new package-specific assertions.
- A full `dotnet build Compono.slnx` after the `Directory.Build.targets`
  change, confirming zero new warnings/errors on any project.
- Explicit positive proof (undocumented member in a packable package fails
  the build) and explicit negative/boundary proof (same in a non-packable
  project does not) — both performed and reverted, not merely asserted.
- A real `package-validation.yaml` CI run confirming: the removed step is
  gone with no coverage loss, `Compono.Logging`'s nupkg content is now
  correctly inspected, and every other existing check remains green.

## Notes

Drafted and implemented from
[RESEARCH-0021](../research/0021-package-validation-execution-policy-audit.md).
This plan does not fold into PLAN-0061 — it is an explicit follow-up
discovered after PLAN-0061 Phase 1 completed, not part of that plan's own
history.
