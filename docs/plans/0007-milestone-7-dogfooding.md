# [PLAN-0007] Milestone 7: Dogfooding

**Status:** Not Started

**Implements:** [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)
(dogfooding strategy, migration-driven evidence, gap decision rubric,
where outcomes get recorded)

## Goal

`ncipollina/cosmere-tracker`'s AutoFixture-based test kit
(`test/Cosmere.Tracker.TestKit`) is fully migrated to Compono, evidence is
collected per ADR-0029's rubric, and each of the three known candidate
gaps (hidden shared values, NSubstitute `ConfigureMembers`, recursion
omission) — plus any further gap the migration surfaces — has a recorded
outcome: either a new `Proposed` ADR (roadmap candidate) or a dated
Amendment to the governing existing ADR (intentional design difference).
`docs/mvp.md`'s Milestone 7 section reflects the outcome.

## Scope

Per ADR-0029's Decision Outcome and `docs/mvp.md`'s Milestone 7 section:

- Migrating `cosmere-tracker`'s `Cosmere.Tracker.TestKit` and its consuming
  test projects (`Cosmere.Tracker.Api.Tests`, `Cosmere.Tracker.Shared.Tests`,
  `Cosmere.Tracker.Seeder.Tests`) from AutoFixture/AutoFixture.AutoNSubstitute/
  AutoFixture.Xunit3 to Compono/`Compono.XunitV3`/`Compono.NSubstitute`
  — this happens in the `cosmere-tracker` repo, not this one.
- Recording quantitative and qualitative evidence for the three known gaps
  (`Freeze<HttpMessageHandler>` in `HttpClientSpecimenBuilder`,
  `AutoNSubstituteCustomization { ConfigureMembers = true }` in
  `BaseFixtureFactory`, `OmitOnRecursionBehavior` in the same factory) and
  any additional gap the migration turns up.
- Applying ADR-0029's rubric to each gap and producing its recorded outcome
  in this repo (`docs/research/0001-autofixture-comparison.md` plus the
  resulting ADR(s)/Amendment(s)).
- Updating `docs/mvp.md`'s Milestone 7 section with the outcome.

Explicitly deferred (per the user's own framing and ADR-0029): designing
the actual API for any gap that ends in "roadmap candidate" — that gap's
`Proposed` ADR records the problem only, left for a future milestone's own
design pass.

## Phase 0: Baseline

**Status:** Not Started

- [ ] In `cosmere-tracker`: capture a written baseline of the current
      AutoFixture-based test kit before any Compono change — file/line
      counts for `Cosmere.Tracker.TestKit`, count of `[CosmereTrackerAutoData]`/
      `[InlineCosmereTrackerAutoData]` call sites across the 18 test files,
      current `dotnet test` run time, and a short readability note per
      existing fixture-related file (`BaseFixtureFactory`,
      `CosmereTrackerCustomization`, `HttpClientSpecimenBuilder`,
      `HttpClientSpecification`).
- [ ] Confirm the exact `cosmere-tracker` commit this baseline was taken
      against (for `docs/research/0001-autofixture-comparison.md`'s link
      back).

## Phase 1: Migrate the test kit

**Status:** Not Started

- [ ] Replace `Cosmere.Tracker.TestKit`'s AutoFixture package references
      with `Compono`/`Compono.XunitV3`/`Compono.NSubstitute` (add
      `Compono.Bogus` only if the migration finds a real use for semantic
      values; not assumed up front).
- [ ] Replace `CosmereTrackerAutoDataAttribute`/
      `InlineCosmereTrackerAutoDataAttribute` with `[Compose<TProfile>]`/
      inline-plus-composed parameters, per
      [ADR-0022](../adr/0022-compono-xunit-package-design.md)'s shape.
- [ ] Port `CosmereTrackerCustomization`'s intent into an
      `ICompositionProfile` ([ADR-0018](../adr/0018-composition-profiles.md)).
- [ ] Migrate `HttpClientSpecimenBuilder`'s frozen-`HttpMessageHandler`
      pattern using Compono's current explicit mechanism — a `[Shared]
      HttpMessageHandler` parameter plus a registration/rule producing the
      configured `HttpClient` — recording exactly what this costs relative
      to the original `Freeze<T>()` call (gap 1's evidence).
- [ ] Migrate `AutoNSubstituteCustomization { ConfigureMembers = true }`
      usages to `UseNSubstitute()`, recording every call site where a test
      previously relied on an auto-configured substitute member and now
      needs an explicit `Returns`/`When` setup instead (gap 2's evidence).
- [ ] Migrate away from `OmitOnRecursionBehavior`, recording every place
      Compono's construction-cycle failure actually fires during migration
      and what it took to resolve (restructure the graph, add an explicit
      registration, etc.) — gap 3's evidence.
- [ ] Record any further capability gap surfaced along the way that isn't
      one of the three named above.

## Phase 2: Evidence collection

**Status:** Not Started

- [ ] Post-migration metrics matching Phase 0's baseline shape: file/line
      counts, `dotnet test` run time, per-file readability notes — enough
      to compare directly against the baseline.
- [ ] Per-gap evidence dossier (frequency, before/after snippet, principle-
      alignment note) for each of the three known gaps plus any additional
      one found, per ADR-0029's rubric.

## Phase 3: Gap decisions

**Status:** Not Started

- [ ] Create `docs/research/0001-autofixture-comparison.md` (first use of
      this directory) with the dogfooding narrative, Phase 0/2's baseline
      and post-migration metrics, and each gap's evidence.
- [ ] Apply ADR-0029's rubric to each gap; for each, either:
      - open a new `Proposed` ADR recording the problem only (roadmap
        candidate), or
      - append a dated Amendment to the governing existing ADR (ADR-0011/
        ADR-0022 for gaps 1/3, ADR-0025 for gap 2) recording the evidence
        and the "no change" verdict.
- [ ] Close `docs/research/0001-autofixture-comparison.md` with a
      `## Decisions` section listing exactly which ADR(s)/Amendment(s)
      each gap fed into.

## Phase 4: Docs and cleanup

**Status:** Not Started

- [ ] `docs/mvp.md` Milestone 7 section: links ADR-0029, PLAN-0007, and
      `docs/research/0001-autofixture-comparison.md`; states the outcome
      per gap; success measures checked against the real migration
      evidence (readability, understandability, profile-first setup,
      reproducible failures, performance).
- [ ] `docs/adr/README.md`/`docs/plans/README.md` index rows for any new
      ADR(s) opened in Phase 3 (already added for ADR-0029/PLAN-0007
      during the design phase).

## Critical Files

In `compono` (this repo):

- `docs/adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md` —
  the process this plan executes
- `docs/research/0001-autofixture-comparison.md` — new, the evidence
  record (Phase 3)
- New `docs/adr/00NN-*.md` for any "roadmap candidate" gap outcome
- `docs/adr/0011-...md`/`docs/adr/0022-...md`/`docs/adr/0025-...md` — gain
  dated Amendments for any "intentional design difference" gap outcome
- `docs/mvp.md` — Milestone 7 section (Phase 4)

In `cosmere-tracker` (separate repo, not tracked by this plan's Critical
Files beyond noting where the work happens):

- `test/Cosmere.Tracker.TestKit/**` — the AutoFixture-based test kit being
  migrated
- `test/Cosmere.Tracker.Api.Tests/**`, `test/Cosmere.Tracker.Shared.Tests/**`,
  `test/Cosmere.Tracker.Seeder.Tests/**` — consumers of the test kit

## Test Plan

The migrated `cosmere-tracker` test suites passing under Compono, in that
repo, is itself the primary verification — there is no new automated test
added to the `compono` repo by this plan (it produces documentation and
decision records, not product code). If a gap's outcome is a "roadmap
candidate" that later gets designed and implemented in a future milestone,
that future milestone's own plan carries its test plan, per `testing.md`.

## Notes

Anything discovered mid-migration that changes this plan's shape from what
was originally scoped gets recorded here, not silently absorbed — a plan
being wrong about *how* doesn't require superseding anything, unlike an
ADR being wrong about *what/why*.
