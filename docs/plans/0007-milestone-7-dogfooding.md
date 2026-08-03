# [PLAN-0007] Milestone 7: Dogfooding

**Status:** In Progress

**Implements:** [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)
(dogfooding strategy, migration-driven evidence, gap decision rubric and
five-way classification, required deliverables, bug handling, evidence-
driven restraint)

## Goal

`ncipollina/cosmere-tracker`'s AutoFixture-based test kit
(`test/Cosmere.Tracker.TestKit`) is fully, idiomatically migrated to
Compono; every discovered finding (the three known candidate gaps plus any
further one surfaced) is classified per ADR-0029 (bug / roadmap candidate /
acceptable alternative / intentional design difference / migration-only
friction) and recorded; `docs/migration/migrating-from-autofixture.md` is
substantially complete; `docs/roadmap/post-mvp.md` exists and traces every
entry to real evidence; and Phase 4's final architectural conclusion
answers whether dogfooding changed Compono's overall design direction.
`docs/mvp.md`'s Milestone 7 section reflects all of the above.

## Scope

Per ADR-0029's Decision Outcome and `docs/mvp.md`'s Milestone 7 section:

- Migrating `cosmere-tracker`'s `Cosmere.Tracker.TestKit` and its consuming
  test projects (`Cosmere.Tracker.Api.Tests`, `Cosmere.Tracker.Shared.Tests`,
  `Cosmere.Tracker.Seeder.Tests`) from AutoFixture/AutoFixture.AutoNSubstitute/
  AutoFixture.Xunit3 to Compono/`Compono.XunitV3`/`Compono.NSubstitute`/
  `Compono.Bogus`, favoring idiomatic Compono over a mechanical 1:1
  translation (ADR-0029's "Migration idiom") — this happens in the
  `cosmere-tracker` repo, not this one. `Compono.Bogus` adoption is
  **mandatory**, per ADR-0029's "Compono.Bogus adoption is mandatory" —
  unlike the other packages, there's no existing AutoFixture call site to
  migrate away from, so it requires deliberate investigation of
  `cosmere-tracker`'s domain models rather than falling out of the
  migration automatically. Per Amendment 1, what's mandatory is the
  experiment (the investigation and adoption attempt), not a predetermined
  positive conclusion — a recorded finding that `Compono.Bogus` is a poor
  fit for some or all of the surveyed members is an equally valid outcome.
- Recording quantitative and qualitative evidence — including positive
  findings, not only friction — for the three known gaps
  (`Freeze<HttpMessageHandler>` in `HttpClientSpecimenBuilder`,
  `AutoNSubstituteCustomization { ConfigureMembers = true }` in
  `BaseFixtureFactory`, `OmitOnRecursionBehavior` in the same factory) and
  any additional finding the migration turns up, per ADR-0029's "Evidence
  to collect."
- Applying ADR-0029's rubric to classify every finding and producing its
  recorded outcome in this repo (`docs/research/0001-autofixture-comparison.md`
  plus the resulting ADR(s)/Amendment(s)/bug-fix PR(s)).
- Writing and maintaining `docs/migration/migrating-from-autofixture.md` as
  a living document throughout the migration, not after it.
- Producing `docs/roadmap/post-mvp.md` from only the "roadmap candidate"
  findings.
- Updating `docs/mvp.md`'s Milestone 7 section with the outcome, including
  the Phase 4 final architectural conclusion.

Explicitly deferred (per ADR-0029's "Evidence-driven restraint"): designing
the actual API for any finding classified "roadmap candidate" — that
finding's `Proposed` ADR records the problem only, left for a future
milestone's own design pass. The one exception is a blocking bug, which may
be fixed in its own scoped PR per ADR-0029's "Bug handling."

## Phase 0: Baseline and migration-guide skeleton

**Status:** Done

- [x] In `cosmere-tracker`: capture a written baseline of the current
      AutoFixture-based test kit before any Compono change, per ADR-0029's
      "Evidence to collect" — file/line counts for `Cosmere.Tracker.TestKit`,
      count of `[CosmereTrackerAutoData]`/`[InlineCosmereTrackerAutoData]`
      call sites across the 18 test files, current `dotnet test` run time, a
      short readability note per existing fixture-related file
      (`BaseFixtureFactory`, `CosmereTrackerCustomization`,
      `HttpClientSpecimenBuilder`, `HttpClientSpecification`), and the
      broader maintainability dimensions (framework-specific concepts in
      play, custom fixture infrastructure present, setup visible per test
      method, concepts a new contributor would need to know today). Recorded
      in `docs/research/0001-autofixture-comparison.md`'s new "Baseline
      (Phase 0)" section — 72 tests passing in 1.346s test-execution time,
      8 files/218 lines in `Cosmere.Tracker.TestKit`, 1+7 AutoData call
      sites, plus one previously-undocumented finding: a three-tier fixture
      stack (`Cosmere.Tracker.TestKit` → `Cosmere.Tracker.Shared.TestKit` →
      per-suite local kits) not called out in ADR-0029's Context, and zero
      live call sites for `[ClientAutoData]`/`[InlineClientAutoData]` in the
      three consuming test projects (gap 1's `HttpClientSpecimenBuilder`
      path is only exercised from within the test kit's own definitions
      today — worth confirming during Phase 1).
- [x] Confirm the exact `cosmere-tracker` commit this baseline was taken
      against (for `docs/research/0001-autofixture-comparison.md`'s link
      back) — `2dbd62ec73a8d8ad64b865a22d7b34a056ca537d`.
- [x] Create `docs/migration/migrating-from-autofixture.md` in this repo
      with its planned structure and the major AutoFixture concepts
      expected to be migrated (`Freeze<T>()`, `AutoDataAttribute`/
      customizations, `AutoNSubstituteCustomization`, recursion behaviors,
      specimen builders, and any other concept `cosmere-tracker`'s test kit
      exercises) — drafted **before migration begins**, per ADR-0029's
      "Required deliverables." Reserve a section for `Compono.Bogus` even
      though it has no AutoFixture-side concept to contrast against — it
      documents an added capability, not a migrated one. Content per
      concept is filled in during Phase 1, not now. Also reserved sections
      for two concepts discovered during the baseline survey that ADR-0029
      didn't name: reflection-based NSubstitute stubbing
      (`HttpMessageHandlerExtensions`) and the multi-tier fixture stack.
- [x] Survey `cosmere-tracker`'s domain models
      (`src/Cosmere.Tracker.Shared/Models/**` and any DTOs under
      `src/Cosmere.Tracker.Api/Dtos/**`) for string-typed members that
      plausibly warrant realistic data (book titles, character names, world
      names, etc.) — the starting candidate list for Phase 1's mandatory
      `Compono.Bogus` adoption. Candidates: `BookItem.Title`/`BookDto.Title`,
      `CharacterItem.Name`/`CharacterDto.Name`, `WorldItem.Name`/
      `WorldDto.Name`, `WorldItem.SystemName`/`WorldDto.SystemName` —
      recorded in the migration guide's `Compono.Bogus` section, along with
      why `*Normalized`/`Id`/timestamp members were excluded.

## Phase 1: Migrate the test kit

**Status:** Done

- [x] Replace `Cosmere.Tracker.TestKit`'s AutoFixture package references
      with `Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/
      `Compono.Bogus` — `Compono.Bogus` inclusion is mandatory (ADR-0029),
      not conditional on the migration happening to need it. Referenced as
      published `alpha` prereleases from nuget.org (pinned in
      `cosmere-tracker/Directory.Packages.props`, currently
      `0.1.0-alpha.33`) — a local NuGet feed packed from this repo's source
      was tried first and rejected, since `cosmere-tracker`'s own GitHub
      Actions CI has no sibling `compono` checkout to pack from and would
      fail to restore.
- [x] Replace `CosmereTrackerAutoDataAttribute`/
      `InlineCosmereTrackerAutoDataAttribute` (and the three sibling
      per-project wrapper pairs: `ClientAutoDataAttribute`,
      `EndpointAutoDataAttribute`, `PersistenceAutoDataAttribute`) with
      `[Compose<TProfile>]`/`[Compose]`, per
      [ADR-0022](../adr/0022-compono-xunit-package-design.md)'s shape — all
      four removed entirely (ADR-0029's "Migration idiom"); documented in
      the migration guide. A further finding beyond scope: pure-inline
      `[Theory]` rows (`TextNormalizerTests`) need no Compono attribute at
      all, just plain `[InlineData]` — and `[Compose]`/`[Compose<TProfile>]`
      is `AllowMultiple = false`, so AutoFixture's "stack multiple
      `[InlineAutoData(...)]` rows, each with a composed parameter" idiom has
      no direct Compono equivalent (not hit by any real `cosmere-tracker`
      test, but recorded as a discovered constraint).
- [x] Port `CosmereTrackerCustomization`'s intent into an
      `ICompositionProfile` ([ADR-0018](../adr/0018-composition-profiles.md)) —
      turned out to be an empty stub with no real intent to port, deleted
      outright. `SharedCustomization` (real logic) became
      `SharedTestKitProfile`.
- [x] Migrate `HttpClientSpecimenBuilder`'s frozen-`HttpMessageHandler`
      pattern. **Confirmed zero real call sites** anywhere in
      `cosmere-tracker` outside `Cosmere.Tracker.TestKit`'s own definition
      files — this is gap 1's finding (per the rubric's question 1, "zero
      observed frequency"). Ported anyway, by explicit request, as
      `ClientTestProfile`/`IHttpClientProvider` (`Cosmere.Tracker.TestKit/Http/`,
      `Cosmere.Tracker.TestKit/Profiles/`) with a real capability test
      (`ClientTestProfileTests`) — zero frequency doesn't mean "delete," a
      capability the repo owner will need is kept working and documented, not
      dropped just because nothing uses it *yet*. This surfaced a real,
      further finding beyond gap 1's original framing: `HttpClient` cannot be
      composed directly as a Compono test parameter at all — its 3 accessible
      constructors trip `Compono.Generators`' compile-time `CMP0001`
      diagnostic regardless of any runtime registration/rule, since the
      generator has no visibility into `CompositionBuilder` registrations
      ([ADR-0002](../adr/0002-constructor-selection-algorithm.md)'s own
      anticipated `[CompositionConstructor]` disambiguation attribute was
      never implemented). Worked around by composing `IHttpClientProvider`
      (an interface, always a provider-resolved leaf) instead of `HttpClient`
      directly — full before/after and the workaround in the migration guide.
      Separately, real `[Frozen]`-for-substitute usage *was* found elsewhere
      (~30 call sites, see gap 2 below) — most needed no `[Shared]`
      equivalent at all (zero workaround cost), a handful of genuine
      cross-object-sharing call sites (`CosmereTrackerRepository` persistence
      tests) mapped directly to `[Shared]` at equally low cost. Recorded in
      the migration guide.
- [x] Migrate `AutoNSubstituteCustomization { ConfigureMembers = true }`
      usages to `UseNSubstitute()`. Real evidence found: most call sites had
      zero workaround cost (plain composed parameter replaces `[Frozen]`);
      genuine sharing call sites mapped to `[Shared]` at equally low cost;
      and two tests (`ListWorldsAsync_WhenSortEmpty_DefaultsToName`,
      `ListCharactersAsync_WhenSortEmpty_DefaultsToName`) surfaced a real,
      previously-hidden dependency on `ConfigureMembers`' auto-configured
      return values — fixed with an explicit `Returns`/`ReturnsForAnyArgs`
      stub each test should arguably have had regardless. All recorded with
      before/after snippets in the migration guide (gap 2's evidence).
- [x] Migrate away from `OmitOnRecursionBehavior` — **zero construction-cycle
      failures were ever triggered** during this migration; none of
      `cosmere-tracker`'s composed types form a self-referencing graph. This
      "zero observed frequency" is itself gap 3's Phase 1 finding, recorded
      in the migration guide.
- [x] Adopt `Compono.Bogus` against the Phase 0 candidate list
      (`BookItem.Title`, `CharacterItem.Name`, `WorldItem.Name`/
      `SystemName`). Real finding: `BogusMemberNameProvider`'s exact
      member-name matching can't disambiguate `CharacterItem.Name` (a
      person's name) from `WorldItem.Name` (a place name) sharing the
      literal member name `"Name"` — the built-in convention/alias/custom-
      convention path doesn't fit. Adopted instead via
      `builder.UseBogus<T>(faker => ...)` — Compono.Bogus's own whole-object
      sugar — per type, with `RuleFor` (including sibling-property access
      for `TitleNormalized`/`NameNormalized`/`UpdatedAt` derived fields). An
      earlier version of this task bypassed `UseBogus<T>()` for a hand-rolled
      `Register<T>` factory on the incorrect claim that its callback has no
      access to the resolving `ICompositionContext` — caught in PR review:
      `UseBogus<T>` already seeds the `Faker<T>` from `context.DeriveSeed()`
      internally before invoking the callback, so there was never a reason to
      bypass it, and doing so meant this task initially recorded successful
      dogfooding without ever calling a `Compono.Bogus` API. Corrected to use
      `UseBogus<T>()` directly. Recommendation recorded in the migration
      guide: a genuine win for semantic string fields, not just a tradeoff
      (ADR-0029 Amendment 1). Phase 0's candidate list also named the DTO
      side of each pair (`BookDto.Title`, `CharacterDto.Name`,
      `WorldDto.Name`/`SystemName`) — confirmed during Phase 1 that none of
      `Cosmere.Tracker.Api`'s DTOs are ever composed as a test parameter
      anywhere in `cosmere-tracker`; they're production API-response types
      built by mapping code from the already-Bogus-adopted `*Item` types, so
      there was no separate composition call site to adopt `Compono.Bogus`
      against. Recorded in the migration guide alongside the `*Item`
      findings.
- [x] Further findings recorded (beyond the three named gaps):
      `DynamoDbResponseSpecimenBuilder` had zero real call sites and was
      dropped entirely; `HttpClientSpecimenBuilder`'s equivalent
      (`ClientTestProfile`/`IHttpClientProvider`) also had zero real call
      sites but was ported anyway, by explicit request, and now has a real
      capability test (`ClientTestProfileTests`) exercising the previously-
      uncalled `HttpMessageHandlerExtensions` helper too; `HttpClient`
      cannot be composed directly as a Compono parameter at all (`CMP0001`,
      see above); `[Compose]`'s `AllowMultiple = false` constraint (see
      above). No blocking bug found — all 73 `cosmere-tracker` tests pass
      under Compono (`dotnet test Cosmere.Tracker.slnx`: 73 passed, 0
      failed, ~1.2s, matching Phase 0's baseline of 72 passed in 1.346s
      plus the one new capability test).
- [x] Updated `docs/migration/migrating-from-autofixture.md` with every
      section's real before/after content, in this same change.


## Phase 2: Evidence collection

**Status:** Not Started

- [ ] Post-migration metrics matching Phase 0's baseline shape (file/line
      counts, `dotnet test` run time, per-file readability notes, and the
      broader maintainability dimensions from ADR-0029's "Evidence to
      collect") — enough to compare directly against the baseline.
- [ ] Explicit named inventory of concepts that disappeared entirely during
      migration — not just a rough count — per Amendment 2: which of
      `IFixture`, `ICustomization`, `ISpecimenBuilder`,
      `IRequestSpecification`, the custom `AutoDataAttribute`/
      `InlineAutoDataAttribute` subclasses, `BaseFixtureFactory`,
      `NamedRequest`, and any other Phase 0/1-surfaced concept were dropped
      entirely versus merely replaced one-for-one with a Compono
      equivalent, and what (if anything) replaced each one.
- [ ] Per-finding evidence dossier (frequency, before/after snippet,
      principle-alignment note, classification per ADR-0029's five-way
      taxonomy) for each of the three known gaps plus any additional
      finding, including positive findings.

## Phase 3: Classify findings and produce the roadmap

**Status:** Not Started

- [ ] Finalize `docs/research/0001-autofixture-comparison.md` (created in
      Phase 0 as the first use of this directory, with its baseline section
      already filled in) — fill in the dogfooding narrative, Phase 2's
      post-migration metrics, positive findings, and every finding's
      evidence and classification.
- [ ] Classify every finding per ADR-0029's five-way taxonomy and record
      its outcome:
      - **Bug** — fixed via its own scoped compono PR (if not already done
        during Phase 1), documented here, no new capability ADR, linked
        from PLAN-0007's Notes.
      - **Roadmap candidate** — a new `Proposed` ADR recording the problem
        only.
      - **Acceptable Compono-native alternative** — documented here and in
        the migration guide; no ADR/Amendment.
      - **Intentional design difference** — a dated Amendment to the
        governing existing ADR (ADR-0011/ADR-0022 for gaps 1/3, ADR-0025
        for gap 2, or whichever ADR governs a newly-discovered gap).
      - **Migration-only friction** — documented here and, where useful, as
        a migration-guide tip; no ADR/Amendment.
- [ ] Close `docs/research/0001-autofixture-comparison.md` with a
      `## Decisions` section listing exactly which ADR(s)/Amendment(s)/
      bug-fix PR(s) each finding fed into.
- [ ] Create `docs/roadmap/post-mvp.md` from only the "roadmap candidate"
      findings — per finding: capability, why it matters, observed
      frequency, readability/maintainability impact, and a relative
      priority (high/medium/low confidence) — each entry tracing back to
      the migration guide, the research findings, and its `Proposed` ADR.

## Phase 4: Final conclusion, docs, and cleanup

**Status:** Not Started

- [ ] Answer ADR-0029's "Final architectural conclusion" questions in
      `docs/research/0001-autofixture-comparison.md` (or a dedicated
      closing section): manifesto/design-principle language changes,
      confidence in explicit-over-implicit, whether profiles remained the
      right primary mechanism, whether the public provider model was
      sufficient, any MVP success-criterion revisions, and whether Compono
      is now the default AutoFixture replacement for `cosmere-tracker`.
- [ ] Synthesize the above into one explicit, evidence-backed recommendation
      per Amendment 3 — a stated next action (e.g. Compono becomes the
      recommended default for new `cosmere-tracker` test code; existing
      tests migrate incrementally rather than in one pass; specific
      roadmap-candidate findings should land first; or the current MVP is
      already sufficient as-is), not just a capability statement that
      Compono *can* replace AutoFixture.
- [ ] `docs/migration/migrating-from-autofixture.md`: confirm it needs only
      editorial cleanup at this point, not new content reconstruction — if
      it doesn't, that's a sign Phase 1's "update alongside the code" rule
      wasn't followed and should be fixed before closing the milestone.
- [ ] `docs/mvp.md` Milestone 7 section: links ADR-0029, PLAN-0007,
      `docs/research/0001-autofixture-comparison.md`,
      `docs/migration/migrating-from-autofixture.md`, and
      `docs/roadmap/post-mvp.md`; states the outcome per finding; success
      measures checked against the real migration evidence (readability,
      understandability, profile-first setup, reproducible failures,
      performance); records the final architectural conclusion.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` index rows for any new
      ADR(s) opened in Phase 3 (already added for ADR-0029/PLAN-0007
      during the design phase).

## Critical Files

In `compono` (this repo):

- `docs/adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md` —
  the process this plan executes
- `docs/migration/migrating-from-autofixture.md` — new, drafted Phase 0,
  written incrementally through Phase 1, substantially complete by Phase 4
- `docs/research/0001-autofixture-comparison.md` — created Phase 0 (baseline
  section), the evidence record; finalized (post-migration metrics,
  classifications, `## Decisions`) in Phases 2-3
- `docs/roadmap/post-mvp.md` — new, the evidence-backed roadmap (Phase 3)
- New `docs/adr/00NN-*.md` for any "roadmap candidate" finding
- `docs/adr/0011-...md`/`docs/adr/0022-...md`/`docs/adr/0025-...md` — gain
  dated Amendments for any "intentional design difference" finding
- Any scoped bug-fix PR's changed files in this repo, if a blocking bug is
  found (per ADR-0029's "Bug handling") — linked from this plan's Notes,
  not enumerated here in advance since it isn't known yet
- `docs/mvp.md` — Milestone 7 section (Phase 4)

In `cosmere-tracker` (separate repo, not tracked by this plan's Critical
Files beyond noting where the work happens):

- `test/Cosmere.Tracker.TestKit/**` — the AutoFixture-based test kit being
  migrated
- `test/Cosmere.Tracker.Api.Tests/**`, `test/Cosmere.Tracker.Shared.Tests/**`,
  `test/Cosmere.Tracker.Seeder.Tests/**` — consumers of the test kit

## Test Plan

The migrated `cosmere-tracker` test suites passing under Compono, in that
repo, is itself the primary verification. This plan does not itself add
product code to the `compono` repo — but per ADR-0029's "Bug handling," a
blocking bug discovered during migration may be fixed here through its own
scoped PR, following that PR's own normal test plan
(`tasks/implement.md`/`testing.md`), tracked in this plan's Notes rather
than pre-declared here since it isn't known in advance. If a finding's
outcome is a "roadmap candidate" that later gets designed and implemented
in a future milestone, that future milestone's own plan carries its test
plan, per `testing.md`.

## Notes

Anything discovered mid-migration that changes this plan's shape from what
was originally scoped gets recorded here, not silently absorbed — a plan
being wrong about *how* doesn't require superseding anything, unlike an
ADR being wrong about *what/why*. Any blocking-bug detour (ADR-0029's "Bug
handling") is recorded here with a link to its issue/PR as soon as it
happens, not reconstructed later.

**Phase 1 (2026-08-03):** No blocking bug found; no compono product-code PR
needed. One further AutoFixture-era specimen builder discovered to have
zero real call sites beyond `HttpClientSpecimenBuilder` (gap 1's
originally-named case): `DynamoDbResponseSpecimenBuilder` in
`Cosmere.Tracker.Shared.Tests`, dropped entirely rather than migrated
(unlike `HttpClientSpecimenBuilder`'s own equivalent, which was ported as
`ClientTestProfile`/`IHttpClientProvider` despite the same zero-call-site
finding — see above; the two builders' zero-frequency evidence doesn't mean
the same disposition for both). `Cosmere.Tracker.Shared.Tests`'
`ListWorldsAsync_WhenSortEmpty_DefaultsToName`/
`ListCharactersAsync_WhenSortEmpty_DefaultsToName` required an explicit
NSubstitute stub they didn't previously need (gap 2 evidence — see the
migration guide's `AutoNSubstituteCustomization` section); fixed inline as
part of the migration itself, not a separate PR, since it's a test-only
change with no product-code impact. `test/Directory.Build.props`'
project-wide global usings (`AutoFixture`/`AutoFixture.Xunit3`/
`AutoFixture.Kernel`) were replaced with `Compono`/`Compono.XunitV3` — not
previously called out in this plan's Critical Files, added here for the
record. First attempt at package referencing used a local NuGet feed packed
from this repo's source (mirroring `Compono.XunitV3.SampleTests`); reverted
before commit once it was clear `cosmere-tracker`'s own CI can't reach a
sibling `compono` checkout — replaced with pinned published `alpha`
prereleases from nuget.org instead.
