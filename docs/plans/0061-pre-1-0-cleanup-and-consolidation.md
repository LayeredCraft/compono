# [PLAN-0061] Pre-1.0 Cleanup and Consolidation Gate

**Status:** In Progress

**Implements:** [ADR-0041 Amendment 7](../adr/0041-aot-safe-row-binding-dispatch.md),
[ADR-0033 Amendment 2](../adr/0033-public-preview-samples-strategy.md)

## Goal

Close the concrete gaps found by the pre-1.0 repository-wide cleanup audit —
a correctness bug in three framework integrations, unreachable docs-site
navigation, a fragile CI package-list pattern, a permanent AOT regression
gate, and zero sample coverage for three shipped packages — without
manufacturing architectural work the audit didn't find evidence for. Done
when: the three negative-seed guards are fixed and regression-tested;
MSTest/NUnit are reachable from the docs nav; every publishable package has
an obvious working sample; AOT smoke coverage runs permanently in CI with a
precisely stated guarantee; the publishable-package list is declared once,
not three times, via a mechanism that actually survives across separate CI
steps; and full build/tests/package-validation/sample-validation/AOT-CI are
green.

**Revision note (2026-09-03):** this plan originally also scoped landing
ADR-0058 (generator-facing runtime hook `EditorBrowsable` policy) and its two
companion ADR-0041/ADR-0055 amendments. Jonas Ha's PR #126 merged that entire
slice directly to `main` (as ADR-0058, tracked to completion under its own
`PLAN-0060`) before this plan was finalized. That work is removed from scope
here — see "Revalidation against `origin/main`" below. This plan was
originally drafted as PLAN-0060; it was renumbered to 0061 once `main`
independently claimed 0060 for the now-superseded generator-hook-policy plan.

## Revalidation against `origin/main` (2026-09-03)

Re-checked every audit finding below directly against `main` after merging
PR #126 (commit `43a5e81`):

- **ADR-0058 generator-hook inventory / `EditorBrowsable` annotations**:
  fully landed and `Done` (`PLAN-0060` on `main`). Removed from this plan's
  scope entirely — no remaining task.
- **Negative-seed guard state**: unaffected. `Compono.XunitV3`,
  `Compono.MSTest`, `Compono.NUnit`'s `ComposeAttribute{TProfile}.cs` all
  still reduce to a bare `builder.AddProfile<TProfile>()` with no guard,
  confirmed by direct re-read post-merge. Still required.
- **`package-validation.yaml` structure**: unaffected by PR #126. Re-inspected
  its actual step structure (not assumed) — the nuget.org-baseline lookup,
  `pack_one` calls, and CS1591 enforcement are three **separate** `run:`
  steps (lines ~47, ~82, ~111), each its own shell process. A `PACKAGES=(...)`
  Bash array declared in one step does **not** survive to the next — this
  plan's original mechanism was wrong as written. Corrected below (Task 5).
- **AOT smoke-test projects / CI wiring**: unaffected. Still eight
  `test/*.AotSmokeTest` projects, still zero references from any `.github/workflows/*`
  or `scripts/*`. Still required.
- **Sample structure / package coverage**: unaffected. `samples/` still has
  exactly the same three projects (`Compono.Samples.AspNetApi`,
  `.AspNetApi.Tests`, `.BasicUsage`); `Compono.Http`/`Compono.DependencyInjection`/
  `Compono.Logging` still have zero example-level coverage. Still required.
- **`mkdocs.yml` navigation**: unaffected. "Package Guides" nav still lists 9
  of 11 packages, still missing `Compono.MSTest`/`Compono.NUnit`. Still
  required.
- **Generator helper duplication**: `GeneratorVersion` duplication across the
  five emitters is unaffected. `StableHash` duplication is now **worse** than
  originally audited — PR #126 did not introduce this, but a direct re-check
  found a **third** copy beyond the two the original audit found:
  `TestDoubleOverloadIdentity.StableHash` (`src/Compono.Generators/Emitters/TestDoubleOverloadIdentity.cs:166`),
  in addition to `GeneratedFileNaming.StableHash` and
  `TestDoubleIdentifierNaming.StableHash`. All three copies' own comments
  already cross-reference each other by name as "the same FNV-1a algorithm,"
  so this was already self-documented drift risk, not a new finding — Task 6
  below is corrected to cover three files, not two.
- **`docs.yml` stale package count**: unaffected. Line ~106 still says "eight
  publishable packages" against a loop that builds 11. Still required.
- **`Compono.NSubstitute` AOT-limitation doc gap**: unaffected. Still
  required.

No other audit finding, ADR inventory, or assumption changed. This plan does
not expand scope to cover anything from PR #126 beyond removing the work it
already completed.

## Scope

**In scope**, per the accepted audit findings and the product-owner scoping
exchange that followed it:

- Negative-seed guard fix in `Compono.XunitV3`/`Compono.MSTest`/`Compono.NUnit`
  + regression tests, using `Compono.TUnit`'s already-correct behavior as the
  contract.
- `mkdocs.yml` navigation fix for `Compono.MSTest`/`Compono.NUnit` (package
  guide + API reference).
- `docs.yml`'s stale "eight publishable packages" text.
- `Compono.NSubstitute`'s documented AOT limitation (ADR-0024) surfaced in its
  own package guide.
- Publishable-package-list consolidation in `package-validation.yaml` to a
  single authoritative declaration, using a mechanism that actually persists
  across that workflow's separate `run:` steps (see Task 5).
- `StableHash` (three copies) / `GeneratorVersion` (five copies)
  generator-helper deduplication (validated by the audit as byte-for-byte
  identical, internal-only, zero AOT/annotation difference).
- Permanent, CI-blocking AOT smoke validation per
  [ADR-0041 Amendment 7](../adr/0041-aot-safe-row-binding-dispatch.md), with
  conservative path-filtered triggering and a precisely scoped guarantee.
- Canonical sample coverage extension for `Compono.Http`,
  `Compono.DependencyInjection`, `Compono.Logging` per
  [ADR-0033 Amendment 2](../adr/0033-public-preview-samples-strategy.md).
- `Compono.XunitV3.SampleTests`' undocumented CI/filter requirement and its
  three stale `RealRunnerTests.cs` comment references (added 2026-09-03
  during the audit-to-plan traceability reconciliation — see Phase 1 Task
  below; documentation/comment correctness only, not a rename or
  sample-architecture change).
- The duplicated `"Compono.ComposableAttribute"` metadata-name literal
  (`WellKnownTypeData.cs` / `ComposableAttributeDiscovery.cs`, added
  2026-09-03 during the same reconciliation) — folded into the existing
  generator-helper consolidation task.
- `TrackingNames`' doc-comment/test-coverage gap (added 2026-09-03 during the
  same reconciliation) — see Phase 1 Task below; ADR-0005 actually requires
  this coverage (see "TrackingNames disposition" below), so this is a real
  missing-test gap, not a stale comment to merely correct.

**Explicitly deferred** (see the audit report for evidence):

- Framework-binder duplication across the four `[Compose]` integration
  packages — moved to [RESEARCH-0019](../research/0019-framework-binder-duplication-spike-scope.md),
  runs independently, non-blocking for 1.0. The research finding "keep the
  duplication" is a fully valid outcome, not merely a placeholder pending
  eventual consolidation.
- `TestDoubleAnalyzer.Analyze` decomposition, `CompositionBuilderExtensions`
  same-name-four-assemblies pattern, `*SampleTests` rename, `WellKnownTypes`
  five-class split, `dogfood-validate.sh`'s cosmetic success-message text,
  missing AotSmokeTest coverage for `Compono.NSubstitute` (already explained
  by ADR-0024)/`Compono.Bogus`/`Compono.DependencyInjection` — all Tier
  3/low-priority findings with no evidence of drift risk or correctness
  impact.
- **Four near-identical per-framework generator-registration blocks in
  `ComponoIncrementalGenerator.Initialize`** (added 2026-09-03 during the
  audit-to-plan traceability reconciliation). Explicitly deferred, not
  folded into the `StableHash`/`GeneratorVersion` helper consolidation:
  the duplication here is structural (each block wires a distinct framework
  integration's own discovery path into the pipeline), not a duplicated
  constant or a byte-for-byte helper function — consolidating it would mean
  introducing an abstraction over generator registration/lifecycle behavior
  itself, a materially larger and riskier change than centralizing one
  semantic operation. No concrete drift bug or maintenance failure has ever
  been tied to these four blocks, and four visually-similar blocks reducing
  to one abstraction is not, by itself, evidence of better maintainability.
  Revisit if a real cross-framework registration drift bug appears (e.g. a
  fix applied to one framework's block and missed in another's).
- ADR-0058's generator-hook `EditorBrowsable` policy and its landing on
  `main` — already `Done` via `PLAN-0060`, no longer this plan's concern.

## `TrackingNames` disposition (resolved 2026-09-03)

Evidence checked before adding this to Phase 1, per the instruction not to
manufacture tests from an unverified comment alone:
[ADR-0005](../adr/0005-generator-implementation-conventions.md) itself
requires `.WithTrackingName(...)` on every named incremental pipeline stage
"so incrementality (cache-hit behavior) can be asserted in tests later."
[PLAN-0001](0001-milestone-1-source-generation-foundation.md) (line ~415)
independently recorded, at the time tracking names were first added: "no
incremental-caching test exists yet to consume them (that's still open work,
not done in this pass)." **Disposition: A — this is an actual intended
invariant with a real, long-standing missing-test gap, not a stale or
aspirational comment.** Phase 1 adds the smallest test that proves the
promised invariant, rather than correcting the comment.

## Phases

### Phase 1 — Product correctness and repository quality gate

**Status:** In Progress

Ships as its own PR.

- [x] Add the negative-seed guard + try/catch (matching `Compono.TUnit`'s
      `ComposeAttribute{TProfile}.cs`) to
      `src/Compono.XunitV3/ComposeAttribute{TProfile}.cs`,
      `src/Compono.MSTest/ComposeAttribute{TProfile}.cs`,
      `src/Compono.NUnit/ComposeAttribute{TProfile}.cs`.
- [x] Port `Compono.TUnit.Tests/SeedObservabilityTests.cs`'s negative-seed
      test into `Compono.XunitV3.Tests`, `Compono.MSTest.Tests`,
      `Compono.NUnit.Tests` so this gap can't silently recur.
- [x] `mkdocs.yml`: add `Compono.MSTest`/`Compono.NUnit` rows under both
      "Package Guides" and "Reference → API Reference".
- [x] `docs/packages/compono-nsubstitute.md`: add a concise paragraph stating
      the existing Native AOT/trimming limitation (ADR-0024), pointing to
      that ADR.
- [x] `docs.yml`: fix line ~106's stale "eight publishable packages" text to
      match the loop's actual 11-package count (or de-numericize it).
- [x] **`Compono.XunitV3.SampleTests` CI/filter documentation + stale-comment
      correction** (repository/documentation correctness, not sample-
      architecture redesign — no rename, no relocation): document, inside
      the project itself (a `README.md` in its folder, or a prominent header
      comment directly on `FailingCompositionTests.cs`/
      `FailingConfigProfileTests.cs`), the actual
      `--filter-not-class "Compono.XunitV3.SampleTests.Failing*"`
      requirement `package-validation.yaml` already applies, so a bare
      `dotnet test` no longer reads as "this project is broken." Correct the
      three stale `RealRunnerTests.cs` references in
      `Compono.XunitV3.SampleTests.csproj`'s comments (that file was removed
      per PLAN-0004's own history). Confirm afterward that the project's
      classification as a **packaged-consumer/real-runner validation
      fixture** — not a user-facing canonical sample — is unambiguous from
      its own README/comments to a new reader.
- [x] `test/Compono.Generators.Tests`: add the smallest incremental-caching
      regression test ADR-0005/PLAN-0001 left as open work — assert at least
      one representative `TrackingNames`-tagged pipeline stage (e.g.
      `ComposableTypes` or `ComposeMethodsAll`) reports a cache hit
      (`IncrementalStepRunReason.Cached`/`Unchanged`) via
      `GeneratorDriverRunResult.Results[0].TrackedSteps[...]` on a second
      driver run after an unrelated, non-invalidating source edit. This
      proves the invariant the `TrackingNames` doc comment already promises;
      it does not attempt full incremental-caching coverage of every stage.
- [x] **`.github/workflows/package-validation.yaml` package-list
      consolidation, corrected mechanism**: the nuget.org-baseline lookup,
      `pack_one` calls, and CS1591 enforcement are three independent `run:`
      steps — a shell-local Bash array cannot cross that boundary. Declare
      the 11-package list once as a **job-level `env:` string**
      (`PACKAGES: "Compono Compono.XunitV3 Compono.NSubstitute Compono.Bogus Compono.TUnit Compono.TestDoubles Compono.DependencyInjection Compono.Http Compono.Logging Compono.MSTest Compono.NUnit"`,
      alongside the existing `BREAKING_CHANGE`/`PACK_OUTPUT` job-level `env:`
      entries) — GitHub Actions injects job-level `env:` into every step's
      process environment automatically, so this survives across steps with
      no extra plumbing. Each of the three steps loops
      `for pkg in $PACKAGES; do ... done` instead of repeating the literal
      list. No generalized manifest file, no dynamic project discovery, no
      cross-job output — the list stays a single, locally-readable line in
      the same workflow file.
- [x] Consolidate `StableHash` — now duplicated **three** times
      (`src/Compono.Generators/Emitters/GeneratedFileNaming.cs`,
      `Emitters/TestDoubleIdentifierNaming.cs`,
      `Emitters/TestDoubleOverloadIdentity.cs`, confirmed byte-for-byte
      identical FNV-1a implementations whose own comments already
      cross-reference each other) into one shared internal helper all three
      call.
- [x] Consolidate the `GeneratorVersion` fallback-chain logic (currently
      duplicated across `CompositionPlanEmitter`, `CollectionPlanEmitter`,
      `TestDoubleEmitter`, `LoggingActivationEmitter`,
      `RowInvokerRegistrationEmitter`) into one shared internal helper
      parameterized by the calling type/assembly.
- [x] Consolidate the duplicated `"Compono.ComposableAttribute"`
      metadata-name literal: `WellKnownTypeData.cs:22` and
      `Discovery/ComposableAttributeDiscovery.cs:22` both declare the exact
      same string for the same semantic identity (the `[Composable]`
      attribute's metadata name), consumed via two genuinely different
      paths — `ComposableAttributeDiscovery.AttributeMetadataName` already
      feeds `ForAttributeWithMetadataName` in
      `ComponoIncrementalGenerator.cs:60`, while `WellKnownTypeData.cs`'s
      copy feeds its own symbol-cache lookup — but both have the same reason
      to change (the attribute's fully-qualified name). Reuse the existing
      `ComposableAttributeDiscovery.AttributeMetadataName` constant from
      `WellKnownTypeData.cs` instead of its own literal; no new helper type,
      no metadata-name registry.
- [x] New `aot-validation.yaml` workflow: one reusable script + matrix job
      driving the existing eight `*.AotSmokeTest` projects' established
      pack → local-feed → publish `-p:PublishAot=true` → run pattern.
      Handle the three structural outliers (`Compono.Logging.AotSmokeTest`'s
      `verify-packaging.sh`, `Compono.Http.AotSmokeTest`'s `AnalyzerContract/`,
      core `Compono.AotSmokeTest`'s lack of a framework integration to call
      through) via a per-entry optional hook, not bespoke per-project jobs.
  - [x] **No `paths:` filter on the workflow's `pull_request` trigger** — the
        workflow always starts, so its required check always has something to
        report (a workflow skipped entirely by trigger-level `paths:` leaves a
        required check `Pending` and blocks the PR, per GitHub's required-check
        semantics — confirmed as the reason `docs.yml`'s own trigger-level
        `paths:` pattern must not be copied here).
  - [x] A first, inexpensive job computes which of the eight legs are
        applicable from the PR's changed files (a small repository-owned
        `git diff --name-only`-based script — no third-party changed-files
        action), publishing the result as a job output a dynamic matrix or
        per-leg `if:` conditions consume.
  - [x] That job marks **all eight legs applicable** on any change to
        `src/Compono/**`, `src/Compono.Generators/**`,
        `Directory.Packages.props`, `Directory.Build.props`/`.targets`
        affecting packed output, any `*.AotSmokeTest` project, or the
        `aot-validation.yaml` workflow/script itself.
  - [x] That job marks **only that package's leg** (plus core's own
        `Compono.AotSmokeTest` leg, already covered under the point above for
        any core-affecting change) applicable on a change scoped to one
        integration package's own `src/Compono.<X>/**`.
  - [x] Each leg's actual publish-and-run job runs behind an `if:` condition
        reading that output — an inapplicable leg reports a normal skipped
        conclusion, never a missing status.
  - [x] A final `if: always()` aggregation job depends on all eight leg jobs
        and is the one job named in branch protection/ruleset as the required
        check — fails if any applicable leg failed, succeeds if every
        applicable leg passed or none was applicable.
  - [x] No `merge_group` trigger is added — this repository does not use
        GitHub merge queues today; revisit only if that changes.
  - [x] The workflow's job/step names and any package-guide text referencing
        it state the exact guarantee from
        [ADR-0041 Amendment 7](../adr/0041-aot-safe-row-binding-dispatch.md)
        — "the packaged Compono package's exercised public API surface is
        callable from a Native-AOT-published, trimmed consumer application
        without runtime AOT/trimming failures" — covering core `Compono`
        as well as the integration packages, making no claim of exhaustive
        public-API coverage beyond what each smoke consumer actually
        exercises, and explicitly not claiming the test framework's own
        runner/host is Native-AOT compatible.
- [ ] Full `dotnet build`/`dotnet test Compono.slnx`, `package-validation.yaml`,
      and the new `aot-validation.yaml` all green.

### Phase 2 — Canonical samples

**Status:** Not Started

Ships as its own PR, after Phase 1 merges.

- [ ] `Compono.Samples.AspNetApi`: add a `Compono.Http` scenario (handler-based
      testing of an outbound HTTP-calling endpoint/service already present in
      the sample, or a small new one if none currently makes an outbound
      call) demonstrating realistic assertions a consumer would actually
      write, not merely a compiling reference.
- [ ] `Compono.Samples.AspNetApi`: add a `Compono.DependencyInjection`
      scenario — a DI-composed row provider registered into the host's
      `IServiceCollection`, exercised through a realistic test.
- [ ] `Compono.Samples.BasicUsage`: add a `Compono.Logging` scenario —
      compose an `ILogger<T>`-dependent type via `UseLogging()`, assert a
      captured log entry via `Verify()`.
- [ ] `docs/samples/*.md` overview pages for both samples: add a mention of
      their new scenarios (no new per-scenario page, per
      [ADR-0033 Amendment 2](../adr/0033-public-preview-samples-strategy.md)).
- [ ] `docs/packages/compono-http.md`, `compono-dependencyinjection.md`,
      `compono-logging.md`: link to their new sample scenario, if not already
      linked.
- [ ] `README.md`/`docs/packages/index.md`: confirm every publishable
      package's row links to a working example (sample or package guide),
      correcting any that don't.
- [ ] Full `dotnet build`/`dotnet test Compono.slnx` (both samples build/run
      as part of the solution today; confirm the new scenarios do too) and
      `package-validation.yaml` green.

## Critical Files

- `src/Compono.XunitV3/ComposeAttribute{TProfile}.cs`,
  `src/Compono.MSTest/ComposeAttribute{TProfile}.cs`,
  `src/Compono.NUnit/ComposeAttribute{TProfile}.cs` — negative-seed guard fix.
- `src/Compono.Generators/Emitters/GeneratedFileNaming.cs`,
  `Emitters/TestDoubleIdentifierNaming.cs`,
  `Emitters/TestDoubleOverloadIdentity.cs` — `StableHash` consolidation.
- `src/Compono.Generators/Emitters/CompositionPlanEmitter.cs`,
  `CollectionPlanEmitter.cs`, `TestDoubleEmitter.cs`,
  `LoggingActivationEmitter.cs`, `RowInvokerRegistrationEmitter.cs` —
  `GeneratorVersion` consolidation.
- `src/Compono.Generators/WellKnownTypes/WellKnownTypeData.cs`,
  `Discovery/ComposableAttributeDiscovery.cs` — `ComposableAttribute`
  metadata-name literal consolidation.
- `test/Compono.XunitV3.SampleTests/Compono.XunitV3.SampleTests.csproj`,
  its `FailingCompositionTests.cs`/`FailingConfigProfileTests.cs`, and a new
  `README.md` in that project's folder — CI/filter documentation +
  stale-comment cleanup.
- `test/Compono.Generators.Tests/` — new incremental-caching regression test
  for `TrackingNames`.
- `mkdocs.yml`, `.github/workflows/docs.yml`,
  `docs/packages/compono-nsubstitute.md` — docs sync.
- `.github/workflows/package-validation.yaml` — package-list consolidation
  via job-level `env:`.
- `.github/workflows/aot-validation.yaml` (new) — permanent AOT CI gate.
- `samples/Compono.Samples.AspNetApi/**`, `samples/Compono.Samples.BasicUsage/**` —
  Phase 2 sample scenarios.

## Test Plan

- New regression tests in `Compono.XunitV3.Tests`, `Compono.MSTest.Tests`,
  `Compono.NUnit.Tests` mirroring `Compono.TUnit.Tests/SeedObservabilityTests.cs`'s
  negative-seed-plus-throwing-profile case.
- Existing full solution test suite stays green throughout — helper
  consolidation changes are internal-only and must not change any observable
  behavior.
- New incremental-caching regression test in `Compono.Generators.Tests`
  proving at least one `TrackingNames`-tagged stage reports a cache hit on an
  unrelated second edit (see "`TrackingNames` disposition" above).
- After the `Compono.XunitV3.SampleTests` documentation fix, a fresh
  contributor following only the project's own README/comments (not
  `package-validation.yaml`) must be able to reproduce the correct
  `--filter-not-class` invocation and get a clean 48/48 pass.
- `aot-validation.yaml`'s own matrix run is the test plan for the AOT-CI gate
  itself — a deliberately-broken annotation on one leg (removed during local
  validation, not committed) should be used once to confirm the gate actually
  fails before merging it as passing.
- Phase 2's new sample scenarios each need at least one real assertion
  exercised by `dotnet test Compono.slnx`, not merely a compiling call.

## Notes

The framework-binder duplication research (RESEARCH-0019) is intentionally
absent from both phases' task lists — it runs independently and does not
gate either PR.
