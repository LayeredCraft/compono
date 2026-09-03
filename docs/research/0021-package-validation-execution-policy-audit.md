# [RESEARCH-0021] Package-Validation Execution-Policy Audit

**Status:** Complete. Two real findings recommended for a follow-up plan
(not implemented here); one findings area explicitly recommends leaving
the current design alone.

## Why this exists

PLAN-0061 Phase 1 touched `.github/workflows/package-validation.yaml` (the
publishable-package list consolidation) and, separately, built
`.github/workflows/aot-validation.yaml` as a new applicability-aware,
change-detection-driven CI gate. This raised the natural question: should
`package-validation.yaml`'s current "run everything, on every PR" policy
be redesigned the same way? This is a dedicated, read-only follow-up
audit answering that, plus two real defects the audit surfaced along the
way.

## Method

Read `package-validation.yaml` step-by-step, `aot-validation.yaml` (the
comparison precedent), `.github/scripts/inspect-packed-nupkgs.sh` +
its own test script, `.github/scripts/nunit-compatibility-matrix.sh`,
ADR-0031 (the package-readiness ADR this workflow implements, including
all 5 amendments), ADR-0059's NUnit compatibility-matrix rationale, and
`publish-preview.yaml`/`publish-release.yaml` (to determine whether any
release-time re-validation exists). Every load-bearing claim below was
independently re-verified against the actual files (not taken on an
initial pass's word alone) before being recorded here.

## Step-by-step findings

### 1. Restore
Trivial, no findings.

### 2. Resolve nuget.org baseline versions
Feeds check 3's API-compatibility baseline; no failure of its own beyond
network I/O. Structurally independent per package. Could be package-scoped
safely, but it's cheap (read-only HTTP) — low value in isolation.

### 3. Pack publishable packages (11 packages) + API-compatibility baseline check
Catches an unintentional public-API breaking change and any packing-time
failure. Requires the packed artifact specifically — `dotnet build`/`test`
never invoke the pack target or the baseline-compare MSBuild target.
Each package's own compat baseline is structurally independent (`dotnet
pack` here doesn't require sibling packages' `.nupkg`s to exist on disk).
**Value increases sharply post-1.0**: pre-1.0, ADR-0031's own `0.x`
compatibility policy already permits a labeled breaking-change PR to
bypass this; post-1.0, a false negative here is a real, uncommunicated
consumer-facing break.

### 4. Enforce XML doc comments (CS1591) — real finding, see dedicated section below.

### 5/6. `inspect-packed-nupkgs.tests.sh` + `inspect-packed-nupkgs.sh`
Catches packaging misconfiguration invisible to `dotnet build`/`test`: wrong
`PrivateAssets`, a missing `build/`/`analyzers/` asset, a non-lockstep
Compono dependency pin, an untested/unbounded dependency range drifting
from `Directory.Packages.props`. Requires the packed nupkg specifically
(unzips and inspects the real file listing/`.nuspec`). Per-package,
structurally independent — each package has its own `case` branch in the
script.

**Real finding, independently verified**: `inspect-packed-nupkgs.sh`'s
`main()` loop (line 189) iterates 10 packages —
`Compono Compono.XunitV3 Compono.NSubstitute Compono.Bogus Compono.TUnit
Compono.TestDoubles Compono.DependencyInjection Compono.Http Compono.MSTest
Compono.NUnit` — **`Compono.Logging` is missing entirely.** Confirmed by
direct read of the file: no `Compono.Logging` branch or loop entry exists
anywhere in the script. `Compono.Logging`'s packed nupkg content, manifest
fields, and lockstep dependency pin have zero content-inspection coverage
today. This is exactly the class of silent drift risk this cleanup gate
exists to catch.

### 7. Local-feed packed-consumer smoke tests (5 `*SampleTests` steps)
Catches a packaging defect only visible when consumed as a real external
package. Each of the 5 (XunitV3/TUnit/MSTest/NUnit/TestDoubles) is
structurally independent — XunitV3's proof says nothing about TUnit's
chain. Requires the packed artifact by design (confirmed unchanged via
`test/Compono.XunitV3.SampleTests/README.md`'s own classification as a
packaged-consumer validation fixture, not a user sample).

### 8. NUnit compatibility matrix
Catches a resolved-NUnit-assembly-version regression across the supported
`[3.14.0, 5.0.0)` range × VSTest/MTP runners — ADR-0059 §6's explicit,
accepted monitoring requirement for the NUnit-internal-namespace
dependency risk. Only `Compono.NUnit`'s own source, its `NUnit` version
range in `Directory.Packages.props`, or the matrix script itself can
invalidate it. This is the single most narrowly-scoped and most expensive
check in the job (4-5 full build+dual-runner-run legs).

## CS1591 — dedicated finding

**`Directory.Build.props` (line 82) already sets `GenerateDocumentationFile=true`
unconditionally**, with its own comment (lines 70-80) stating explicitly
that CS1591 is "deliberately left as a real build warning rather than
suppressed... so a missing doc comment on a new public member is caught
immediately." Confirmed by direct read: **no `WarningsAsErrors`/
`TreatWarningsAsErrors` exists anywhere in the repo.** `Directory.Build.targets`
adds `CS1591` to `NoWarn` only when `IsTestProject == true` (line 10).

**This means CS1591 is already an ordinary build warning on every
`pr-build.yaml` run for every non-test project today.**
`package-validation.yaml`'s "Enforce XML doc comments" step only promotes
that pre-existing warning to a hard error — via a **full second `dotnet
build` of all 11 packages**, solely to add `-p:WarningsAsErrors=CS1591`.

Independently verified every non-packable project (all samples,
benchmarks, and `test/*` projects — 30 csproj files checked) sets
`IsPackable=false` directly in its own csproj, not only via the
`IsTestProject`-derived default. This means a `Directory.Build.targets`
condition on `$(IsPackable) != 'false'` would apply `WarningsAsErrors`
for CS1591 **only** to the same 11 real publishable packages
`package-validation.yaml` already targets, with zero risk of newly
breaking any sample/benchmark/test/fixture project.

**Recommendation**: move CS1591 enforcement into `Directory.Build.targets`
(scoped `$(IsPackable) != 'false'`), delete the redundant "Enforce XML doc
comments" step from `package-validation.yaml`. This catches a missing doc
comment on the PR that introduces it — via ordinary `pr-build.yaml`, not
only at the separate package-validation gate — strictly earlier feedback,
and removes 11 redundant full rebuilds from the job. No ADR governs the
specific enforcement *mechanism* (ADR-0031 requires the XML-doc-coverage
outcome, not this particular CI shape), so this is a pure implementation
correction, not a policy change.

## Redundancy findings

None of `package-validation.yaml`'s checks duplicate `pr-build.yaml`
(build/test only, never packs), `docs.yml` (docs-only), or
`aot-validation.yaml` (Native-AOT runtime survival, a different guarantee
than packaging correctness). The CS1591 finding above is about a
duplicated *mechanism* (a second full rebuild), not duplicated coverage.

## Applicability-aware redesign — recommendation: partial

**Do it only for the NUnit compatibility matrix; leave every other check
universal.**

Evidence against broad selectivity: the whole job (11 packages + 5
`*SampleTests` + the NUnit matrix) already completes in roughly 3 minutes
(confirmed from PLAN-0061 Phase 1's own PR #128 CI run). An
applicability-aware redesign mirroring `aot-validation.yaml`'s
`changes`/`smoke`/`gate` structure would add per-leg
checkout/restore/setup-dotnet overhead across up to 16 legs (11 packages +
5 SampleTests) — plausibly costing *more* aggregate CI resource than it
saves on a job this size.

**The decisive evidence, independently confirmed**: `publish-preview.yaml`
and `publish-release.yaml` are both opaque `uses:` calls to the shared
external `LayeredCraft/devops-templates` reusable workflow. Read in full —
neither re-runs the API-compatibility baseline check, `inspect-packed-nupkgs.sh`,
CS1591 enforcement, any `*SampleTests` smoke test, or the NUnit
compatibility matrix. **`package-validation.yaml` is the only safety net
these invariants will ever get, at any point in the pipeline.**
`aot-validation.yaml`'s own applicability script was exactly the kind of
change-detection logic Codex's review of PR #128 caught a real fail-open
bug in — proving this class of script is genuinely bug-prone even when
carefully built. Introducing a new one here, for checks with no
release-time backstop if the script under-detects, is a real risk not
justified by the modest time savings on an already-fast job. This matches
the audit brief's own steer: prefer a simple, comprehensive gate over a
clever applicability system that can silently under-test changes.

The NUnit compatibility matrix is the one legitimate exception: narrowest
blast radius of any check here (only `Compono.NUnit`-relevant changes can
invalidate it), and it is also the slowest/most expensive leg — the
cost/benefit of scoping it is real, and an incorrect skip has a much
smaller blast radius (one framework's version-compat surveillance, not
core packaging correctness) than an incorrect skip anywhere else in this
job would.

## PR-validation vs. release-validation split — recommendation: not supported by evidence

The proposed two-tier framing ("PR validation: this change hasn't broken
package contracts it could plausibly affect" vs. "release validation:
every package about to publish is comprehensively validated") assumes a
release-time validation tier already exists to receive the "exhaustive"
half of that split. It doesn't. `publish-preview.yaml`/`publish-release.yaml`
trust the PR gate's prior result unconditionally and perform no
re-validation of their own. The honest framing isn't "split lightweight
PR checks from exhaustive release checks" — it's "`package-validation.yaml`
**is** the only validation, full stop, and it happens to run at PR time."
Building a genuine release-time exhaustive check would be new
infrastructure, not a refactor of the existing gate, and no evidence in
this repo suggests the current single-gate model has actually caused a
problem. **Recommend Tier 3 (leave alone)** unless/until a concrete
release-time gap is found in practice.

## Tier classification

- **Tier 1**: fix the missing `Compono.Logging` entry in
  `inspect-packed-nupkgs.sh`'s package loop (real, silent coverage gap,
  independently verified). Move CS1591 enforcement into
  `Directory.Build.targets` (scoped `$(IsPackable) != 'false'`); delete
  the now-redundant "Enforce XML doc comments" step from
  `package-validation.yaml`.
- **Tier 2**: make the NUnit compatibility matrix step conditionally
  skippable via a small, narrowly-scoped applicability check (only
  `Compono.NUnit`-relevant changes need to run it).
- **Tier 3**: leave the pack/baseline-compare/nuspec-inspection/
  `*SampleTests`-smoke steps universal — no applicability-aware redesign.
  Leave the PR-validation/release-validation split unbuilt — no
  release-time backstop exists to make partial PR-time coverage safe.

## Relationship to 1.0

- **Should this block PLAN-0061 Phase 2?** No — Phase 2 is sample
  coverage work, unrelated to package-validation mechanics.
- **Should this block 1.0?** No — the missing `Compono.Logging` inspection
  coverage and the CS1591 double-rebuild are both real but low-severity
  (neither currently causes an undetected failure; `Compono.Logging`
  simply isn't checked as deeply as its 10 siblings, and CS1591 is still
  enforced today, just via extra CI cost).
- **What becomes materially harder/riskier to change after 1.0?** The
  API-compatibility-baseline check (step 3) and the lockstep-pin/
  dependency-range nuspec inspection (steps 5/6, including the
  `Compono.Logging` gap) — a false negative in either is advisory pre-1.0
  (ADR-0031's `0.x` policy tolerates it) but becomes a real,
  uncommunicated consumer-facing break once 1.0.0 ships. This argues for
  fixing the `Compono.Logging` gap **before** 1.0, not after, even though
  it isn't a hard blocker.
- **What can safely wait until after 1.0?** The CS1591-relocation
  mechanism change (pure CI efficiency, no coverage change) and the NUnit
  matrix applicability scoping (a cost optimization, not a correctness
  fix) are both safe to defer past 1.0 if there's ever a reason to
  prioritize other work first.

## Unverified assumptions requiring a spike before implementation

- Whether any currently-`IsPackable=false` project has an undocumented
  public member that would newly trip CS1591-as-error if the proposed
  `Directory.Build.targets` condition were scoped even slightly wrong —
  the recommendation above is scoped specifically to avoid this
  (`IsPackable != 'false'` matches exactly the 11 packages
  `package-validation.yaml` already targets, verified against all 30
  non-packable csproj files), but a real CI run of the change should
  confirm no unexpected breakage across the whole solution before
  merging it.
- The CS1591 step's actual individual wall-clock cost wasn't measured in
  isolation (only the job's ~3-minute total is known) — worth confirming
  its removal produces a measurable time saving, not just a
  redundancy-on-paper argument.

## No ADR proposed

Nothing in this audit surfaces a genuine architectural or long-lived
policy decision that needs its own ADR. The `Compono.Logging` inspection
gap is a script bug (an omission from a loop), not a policy question. The
CS1591 relocation is an implementation-mechanism correction — ADR-0031
already establishes *that* XML docs are required; it never mandated *how*
that gets enforced in CI. The applicability-aware-redesign question is
answered by evidence (partial: NUnit matrix only) without needing a new
decision record — it's an application of the same judgment ADR-0041
Amendment 7 already exercised, not a new one.

## Recommended follow-up

[PLAN-0062](../plans/0062-package-validation-gap-fixes.md) — not yet
implemented, drafted alongside this research record as the smallest
scoped follow-up for the Tier 1/Tier 2 findings above.

## Links

- [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md) —
  the package-readiness ADR this workflow implements; its `0.x`
  compatibility policy is why the baseline-compare/nuspec-inspection
  findings above are framed as "advisory now, real risk post-1.0."
- [ADR-0059](../adr/0059-compono-nunit-package-design.md) — §6's
  monitoring requirement, the reason the NUnit compatibility matrix
  exists.
- `.github/workflows/aot-validation.yaml` — the applicability-aware
  precedent this audit evaluated `package-validation.yaml` against, and
  the source of the "a fail-open bug is realistic even when carefully
  built" evidence (the Codex-caught gap fixed on PR #128).
- [PLAN-0061](../plans/0061-pre-1-0-cleanup-and-consolidation.md) — the
  cleanup plan this audit follows up on.
