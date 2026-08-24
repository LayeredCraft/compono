# [PLAN-0050] Compono.TestDoubles: Multi-Entry, Argument-Distinguished Response Configuration

**Status:** In Progress

**Implements:** [ADR-0050](../adr/0050-testdoubles-multi-entry-argument-distinguished-configuration.md)

## Goal

A `Compono.TestDoubles`-generated double, for an argument-matching-eligible
member (ADR-0048) or a closed-instantiation-eligible member (ADR-0049),
supports more than one simultaneously-active argument-distinguished response
configuration. `Configure()` appends an entry instead of overwriting the
member's one slot; dispatch scans entries in reverse registration order,
returning/throwing on the first matching entry; no match falls through to
ADR-0045's existing rule unchanged. Closes the false-pass correctness bug in
`ncipollina/trivia-platform`'s `LeaderboardServiceTests` and the two
NSubstitute/hand-written-fake workarounds it forced
(`CachedLeaderboardRepositoryTests`, `MultiStubLeaderboardRepository`).

Done when: the full `Compono.TestDoubles`/`Compono.Generators` test suite
passes with the new representation; `ReturnConfigBuilder<T>`, `ReturnConfig<T>`,
`Match<T>`, and `CallVerifier` remain unchanged (reused, not modified); both
ADR-0048's plain matching-eligible shape and ADR-0049's per-closed-`T` shape
use the same `Entry`/list representation; the single-`Configure()`-per-member
case is behaviorally unchanged for consumers; `docs/packages/compono-testdoubles.md`
documents the new capability; **and** the consumer-validation gate below is
green against freshly packed local packages — not merely against Compono's
own suite.

## Scope

Exactly ADR-0050's Decision Outcome:

- A generated reference-type `Entry` per member (or per closed-`T`, for
  ADR-0049 members), bundling one full matcher set plus its own
  `ReturnConfig<TSlot>`, held in an ordered `List<Entry>`.
- `Configure()` appends a new `Entry`; `ReturnConfigBuilder<T>` is
  constructed via `ref` into that entry's `Config` field, from the local
  variable, before the entry is added to the list — proven safe under list
  reallocation by the ADR-0050 spike, requiring **zero changes** to
  `ReturnConfigBuilder<T>`/`ReturnConfig<T>`.
- Dispatch walks the entry list in reverse registration order, first
  full-matcher-match wins; no match falls through to ADR-0045's existing
  deterministic-default/configuration-required rule, unchanged.
- Applies to **both** ADR-0048's plain matching-eligible members and
  ADR-0049's closed-instantiation members (the latter's `_State<T>` gets
  `List<Entry>` in place of its current single `Config`+`Matcher_*` pair) —
  per the spike, this composes with no new machinery, so both shapes move
  together in this one plan, not staged separately.
- The zero-argument `Configure()`/`Verify()` compatibility overloads
  (matching-eligible members) simplify under the new design (an always-
  matching entry, wins by recency) — including the `Verify()` counterpart's
  call-count read, which must move from the now-removed single field to the
  shared call-log list's `Count` (a real regression caught during the
  spike; explicit regression-test task below).
- `TestDoubleAnalyzer.cs`'s name-collision reservation pool learns the two
  new derived names (`{Field}_Entry`/`{Field}_entries`, and their ADR-0049
  nested-class equivalents).

Explicitly out, per ADR-0050 and the user's direction — do not implement any
of these while doing this plan's work, even if implementation makes one look
easy to add:

- Matcher-specificity ranking ("most specific wins") — last-matching-
  registration-wins only, no comparison between matchers.
- `Returns(Func<...>)` callback/sequential/call-count-based responses.
- `Received.InOrder`-style call-order verification.
- Overloaded-member argument-*value* matching (overloaded members stay on
  ADR-0044's discriminator-only shape, untouched — this plan's `Entry`
  mechanism only applies to `IsEligibleForMatching`/closed-instantiation
  members, which are non-overloaded by ADR-0048's own restriction).
- `SetContextDataAsync<T>`'s open-generic-parameter matching gap.
- `ISkillLocalizer`/cross-assembly generated-registry first-registration-
  wins limitation.
- Any change to `Match<T>`'s public shape or `CallVerifier`'s stateless
  count-only contract.
- Widening ADR-0049's member-*eligibility* rules (which generic members
  qualify as closed-instantiation-eligible) — only how an already-eligible
  member's per-closed-`T` state is stored changes.

## One implementation PR

Per the user's explicit direction: keep this as one coherent PR unless
implementation uncovers a real independently-shippable seam. The two shapes
(ADR-0048 plain members, ADR-0049 closed-instantiation members) share one
`Entry` abstraction and were proven to compose together in the spike — they
are not a natural split. If something genuinely separable turns up during
implementation (e.g. the `TestDoubleAnalyzer.cs` name-collision-pool
addition proving trivially independent of the template/dispatch change),
flag it here as a candidate seam rather than splitting unilaterally.

## Tasks

Grouped by concern, checked off as work proceeds.

### 1. Runtime types — confirm no change needed

- [x] Confirm `ReturnConfig<T>` and `ReturnConfigBuilder<T>`
      (`src/Compono/ReturnConfig.cs`, `ReturnConfigBuilder.cs`) require
      **zero source changes** — the `Entry`'s `Config` field is just another
      call site for the existing `ref`-based construction. If implementation
      finds a real reason to touch either type, stop and treat that as a
      signal the design spike missed something — do not silently expand
      their public shape.
- [x] Confirm `Match<T>` and `CallVerifier` require zero source changes.

### 2. Model (`TestDoubleMemberInfo.cs`)

- [x] Add whatever the template needs to name the generated `Entry` type and
      its backing list per member (e.g. `EntryClassName`, `EntriesFieldName`,
      mirroring the spike's naming) — reuse the spike's exact field names if
      they held up; don't invent new ones without reason.
- [x] For ADR-0049 members, extend `ClosedInstantiationStateClassName`'s
      companion model surface so the nested `_State<T>` class can also carry
      an `Entry` type name and a `List<Entry>` field name, scoped inside that
      state class.
- [x] Keep `HasConfigurationSurface`/`IsEligibleForMatching`/
      `IsClosedInstantiationEligible`'s existing eligibility computation
      completely unchanged — this plan changes storage shape only, never
      which members qualify for which shape.

### 3. Generated storage and dispatch (`TestDouble.scriban`, `TestDoubleEmitter.cs`)

- [x] Plain matching-eligible members: replace the single `__Member`
      (`ReturnConfig<TSlot>`) + `__Member_m_{param}` fields with a generated
      `__Member_Entry` class (one `Match<TParam>?` field per real parameter,
      one `ReturnConfig<TSlot> Config` field) and a `List<__Member_Entry>
      __Member_entries` field. Keep `__Member_calls`/`__Member_lock`
      unchanged — the call log stays shared across all entries, per ADR-0050.
- [x] `Configure(...)` extension: construct a new `Entry`, assign its matcher
      fields from the call site's arguments, `Add()` it to the list, return
      `new ReturnConfigBuilder<TSlot>(ref entry.Config)`. Operation order
      between `Add()` and taking the `ref` is not load-bearing: `Entry` is a
      reference type, so `entry.Config` lives at a fixed heap location for
      the object's lifetime regardless of when or whether the list's backing
      array reallocates, exactly as the spike proved.
- [x] Dispatch body: lock, record the call into the existing shared call log,
      then scan `__Member_entries` in reverse (`for i = Count-1; i >= 0;
      i--`), first entry whose full matcher set matches wins
      (`HasConfiguredException` → throw, `HasConfiguredValue` → return); no
      match falls through to the existing ADR-0045 default/throw branch,
      unmodified.
- [x] Zero-argument `Configure()` compatibility overload: append an entry
      with all matcher fields `null` (already-existing "`null` matcher always
      matches" semantics handles the always-match case with no special-
      casing needed).
- [x] Zero-argument `Verify()` compatibility overload: **must** read the
      shared call-log list's `Count`, not a per-entry field — this is the
      exact regression the spike hit (2 of 18
      `TestDoubleVerificationExecutionTests` broke before this was
      corrected). Add an explicit regression test naming this case (task 4).
- [x] ADR-0049 closed-instantiation members: inside the generated
      `__Member_State<T>` class, replace its single `Config`+`Matcher_*`
      fields with a nested `Entry` class + `List<Entry> Entries` field
      (identical shape to the plain-member case, just one level deeper,
      scoped per closed `T`). Extension-method call sites must fully qualify
      the nested `Entry` type (`global::{Double}.{StateClass}<T>.Entry`) —
      the mechanical friction point the spike identified.
- [x] `TestDoubleAnalyzer.cs`: extend the name-collision reservation pool
      (`derivedAuxiliaryNameOwners`/`usedFieldNames` and equivalents) to
      cover the new derived names (`{Field}_Entry`, `{Field}_entries`, and
      the ADR-0049 nested-class equivalents) — same pattern as the existing
      `_calls`/`_lock`/`_State`/`_buckets` reservations.

### 4. Runtime behavior tests (`Compono.Generators.Tests`, `Compono.TestDoubles.SampleTests`)

- [x] Two disjoint literal-argument entries on the same plain member — both
      dispatch their own configured value; a call matching neither falls
      through to the existing default/throw behavior.
- [x] `Match.Any<T>()` default entry + a later specific-literal entry — the
      later (specific) entry wins for the matching value, the default entry
      still answers everything else (proves reverse-scan last-wins, not
      just "last entry always wins regardless of match").
- [x] Same two cases, repeated for an ADR-0049 closed-instantiation member,
      within one closed `T`.
- [x] An ADR-0049 closed-instantiation member with two different closed
      `T`s, each independently holding its own multi-entry list — confirms
      the bucket/entry-list composition stays isolated per `T`.
- [x] Zero-argument `Configure()`/`Verify()` compatibility overloads: single
      call and repeated calls, confirming call count reads from the shared
      log correctly (the regression-test task named above).
- [x] `Once()`/`Never()`/`Exactly(n)` verification is unaffected by entry
      count — call log is shared, unchanged from today.
- [x] Regression: an overloaded member (ADR-0044 discriminator-only shape)
      is untouched by any of this — confirm its generated output doesn't
      change at all.
- [x] Regression: full existing `Compono.TestDoubles`/`Compono.Generators`
      test suite (including all pre-existing `TestDoubleVerifyTests`
      snapshot tests, which will need re-baselining for the new generated
      shape — expected and intentional, not a sign of breakage) passes
      completely.

### 5. AOT smoke test (`test/Compono.TestDoubles.AotSmokeTest`)

- [x] Extend (or add alongside) the existing smoke-test interface with a
      real multi-entry case exercised through the actual generator — not
      hand-written — covering at minimum: two disjoint entries on a plain
      member, and the `Match.Any` + literal-override case. Real `dotnet
      publish -c Release -f net10.0 -p:PublishAot=true`, zero warnings,
      correct output at runtime.

### 6. Documentation

- [x] `docs/packages/compono-testdoubles.md`: document multi-entry
      `Configure()` — the override idiom, last-matching-registration-wins
      semantics, and the explicit pre-1.0 semantic-correction note (a second
      `Configure()` call no longer discards the first).
- [x] `docs/adr/README.md` index: record ADR-0050 as an evidence-based
      reopening of ADR-0048's rejected option, per this repo's existing
      Amendment/reopening indexing convention.
- [x] `AGENTS.md`: already updated with the consumer-validation standing
      rule during the design phase — confirm it still accurately describes
      the finished `scripts/dogfood-validate.sh` behavior once implementation
      is done; correct if anything drifted.

### 7. Consumer-validation gate (required for completion — not optional, not satisfied by Compono's own tests alone)

The 783/783 result from the design-phase validation-script spike proves the
*validation mechanism* works. It does **not** satisfy this gate — it ran
against `0.7.0-preview.81`, before any ADR-0050 change existed. This task
must be re-run against packages built from the actual implementation.

- [x] Full Compono test suite green (`dotnet build` 0 warnings/errors,
      `dotnet test` fully green) — task 4/5's tests included.
- [x] `scripts/dogfood-validate.sh` run against the real ADR-0050
      implementation — packs fresh, uniquely-versioned local packages from
      the working tree; confirms trivia-platform's restore resolves exactly
      that version (not a stale cache hit).
- [x] Migrate the three acceptance cases in trivia-platform (this is
      consumer-repo work performed as part of this validation task, not
      optional cleanup):
  - [x] `CachedLeaderboardRepositoryTests` off NSubstitute, onto
        `.Configure()` expressing all three period/type/count-divergent
        cases directly.
  - [x] Remove `MultiStubLeaderboardRepository`; migrate its one call site
        (`LeaderboardServiceTests.RetrieveCurrentPlayerStatsAsync_LoadsWeeklyAndAllTimeStats`)
        onto `.Configure()`, resolved through `[Compose]` rather than
        hand-constructed.
  - [x] Tighten `LeaderboardServiceTests`'s `ZeroScoreProjection_DoesNotQueryRank`
        and `NegativeRankResult_OmitsRank` so they demonstrably exercise
        their intended production branches — not just "still passes."
        Concretely: assert on the field(s) (`HasScore`/`IsRanked` or
        equivalent) that the false-pass finding showed were previously
        unchecked, such that reverting to the old single-slot overwrite
        behavior would make these tests fail again. A passing test that
        can't detect the regression it was written against doesn't close
        this finding. **Proven, not assumed** — see Notes: both tests
        (plus the migrated `RetrieveCurrentPlayerStatsAsync_LoadsWeeklyAndAllTimeStats`
        and 3 `CachedLeaderboardRepositoryTests` cases) were run against a
        freshly packed pre-ADR-0050 build and confirmed to fail with
        `Expected result.Weekly.HasScore to be True, but found False`,
        then re-run against the real ADR-0050 implementation and confirmed
        to pass.
- [x] Full trivia-platform suite green against the freshly-packed local
      packages (not merely "783/783" repeated — confirm the count and that
      no test silently dropped out).
- [x] Document the exact command(s) run and their output in this plan's
      Notes section (below) as the completion evidence.
- [x] **Repeat this entire task (build, pack, validate, full trivia-platform
      suite) after every substantive PR feedback change** — a consumer run
      performed before the latest code change does not validate the revised
      code. Log each re-run in Notes with a date and a one-line summary of
      what changed since the prior run. (First repeat done 2026-08-24 after
      round-1 codex feedback, below; repeat again after any further
      substantive change.)

## Critical Files

- `src/Compono.Generators/Templates/TestDouble.scriban` — the `Entry` class
  emission and reverse-scan dispatch, both shapes.
- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — name-collision
  pool additions only; eligibility rules unchanged.
- `src/Compono.Generators/Models/TestDoubleMemberInfo.cs` — new naming
  properties for the `Entry` type/list.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` — collision-safe
  local-variable naming for the new `Configure()` body (mirrors existing
  local-naming patterns).
- `src/Compono/ReturnConfig.cs`, `ReturnConfigBuilder.cs`, `Match.cs`,
  `CallVerifier.cs` — expected **zero changes**; listed here as the
  explicit "did not touch" checklist, not as files to edit.
- `test/Compono.Generators.Tests/TestDoubleVerifyTests.cs` and its
  `Snapshots/` — re-baselined snapshots for both changed shapes.
- `test/Compono.Generators.Tests/TestDoubleVerificationExecutionTests.cs` —
  regression coverage for the zero-arg `Verify()` call-count fix.
- `test/Compono.TestDoubles.AotSmokeTest/Program.cs` — new multi-entry AOT
  case.
- `docs/packages/compono-testdoubles.md`, `docs/adr/README.md`,
  `AGENTS.md` — documentation.
- `scripts/dogfood-validate.sh` — unchanged by this plan; the gate it
  enforces, not a file this plan modifies.

## Test Plan

`dotnet build`/`dotnet test` in this repo (0 warnings/errors, fully green)
covers tasks 1–5. `scripts/dogfood-validate.sh` plus the trivia-platform
acceptance-case migration and its own `dotnet test` covers task 7. Both
required for completion — see Goal.

## Notes

### 2026-08-24 — Initial implementation, consumer-validation gate

- `dotnet build`: 0 warnings / 0 errors (full solution).
- `dotnet test`: 2384 total, 2378 succeeded, 6 failed — all 6 failures are
  in `test/Compono.NSubstitute.Tests/StaleArgMatcherLeakRepro.cs`, a
  pre-existing, unrelated investigation file present in the working tree
  before this implementation began (documents a separate NSubstitute/
  ADR-0049 interaction bug, not touched by ADR-0050). Confirmed pre-existing
  by stashing all ADR-0050 source changes and re-running: identical 6
  failures, same file, same assertions. Not part of this plan's scope.
- `scripts/dogfood-validate.sh` (no args, default consumer/solution):
  packed `0.0.0-local.20260824090254-4347-15950`, consumer resolved that
  exact version (asserted via `project.assets.json`), full trivia-platform
  suite: **783/783 passed**. Consumer git tree confirmed clean afterward
  (`git status --porcelain` — only the pre-existing intended
  `Directory.Packages.props` 0.6.0→0.7.0-preview.81 bump, identical
  before/after).
- Three acceptance-case migrations completed in trivia-platform:
  - `LeaderboardRepositoryProfile.cs`: removed the `Substitute.For<ILeaderboardRepository>()`
    NSubstitute-fallback registration and its now-obsolete rationale
    comment; `ILeaderboardRepository` now resolves through the generated
    double via `TriviaEngineProfile`'s default, same as every other
    repository interface.
  - `CachedLeaderboardRepositoryTests.cs`: rewritten from raw NSubstitute
    call syntax (`Arg.Any<T>()`/`.Returns(...)`/`inner.Received(n)`) onto
    `.Configure()`/`.Verify()` with `Match<T>`, including
    `RetrieveTopEntriesAsync_DifferentPeriod_DoesNotReuseCachedEntries` and
    `RetrieveTopEntriesAsync_DifferentType_DoesNotReuseCachedEntries` and
    `RetrieveTopEntriesAsync_DifferentCount_DoesNotReuseCachedEntries` —
    each configures two disjoint literal-argument entries on the same
    member in one test, the exact shape ADR-0050 adds.
  - `MultiStubLeaderboardRepository.cs` deleted (was untracked, never
    committed); its one call site
    (`LeaderboardServiceTests.RetrieveCurrentPlayerStatsAsync_LoadsWeeklyAndAllTimeStats`)
    rewritten onto `[Shared] ILeaderboardRepository leaderboardRepository`
    + `LeaderboardService sut` resolved through `[Compose]`, with four
    `.Configure()` calls (two `GetLeaderboardEntryAsync` entries, two
    `GetLeaderboardRankAsync` entries) replacing the hand-written fake's
    dictionary-backed stubbing.
  - `ZeroScoreProjection_DoesNotQueryRank` and `NegativeRankResult_OmitsRank`
    strengthened with `result.Weekly.HasScore.Should().BeTrue()` and a
    `Score`/`Verify().GetLeaderboardRankAsync(...).Once()` assertion (the
    latter test only) — proving the configured entry was actually
    returned, not silently replaced by the pre-ADR-0050 single-slot
    overwrite bug's `entry is null` fallback (which coincidentally also
    produces a null `Rank` and also never queries rank, the exact
    false-pass mechanism the finding named).

**False-pass proof (fail-old/pass-new), performed manually, not merely
asserted:**

1. Stashed all four ADR-0050 generator source changes
   (`TestDouble.scriban`, `TestDoubleEmitter.cs`, `TestDoubleMemberInfo.cs`,
   `TestDoubleAnalyzer.cs`), restoring the pre-ADR-0050 single-slot
   generator behavior.
2. Packed `Compono`/`Compono.NSubstitute`/`Compono.TestDoubles`/
   `Compono.XunitV3` at a throwaway unique version from that pre-ADR-0050
   tree.
3. Restored and ran
   `LayeredCraft.Alexa.TriviaEngine.Modules.Leaderboard.Tests` against it
   (temp `Directory.Packages.props`/`nuget.config` override, consumer's
   real file never touched): **6 of 105 tests failed**, including both
   strengthened tests and all four migrated cases —
   `ZeroScoreProjection_DoesNotQueryRank` and `NegativeRankResult_OmitsRank`
   failed with `Expected result.Weekly.HasScore to be True, but found
   False` (the exact false-pass mechanism), and
   `RetrieveCurrentPlayerStatsAsync_LoadsWeeklyAndAllTimeStats` plus 3
   `CachedLeaderboardRepositoryTests` cases failed on the second
   `Configure()` call overwriting the first, as expected.
4. Popped the stash, restoring the real ADR-0050 implementation, packed a
   fresh unique version from it, restored and re-ran the same project:
   **105/105 passed**.
5. Confirmed via the official `scripts/dogfood-validate.sh` run (above)
   that the full 783-test trivia-platform suite is green against the real
   implementation, and that the consumer's git tree was left clean
   throughout (no stray files, no reverted pre-existing changes).

No commits or pushes made in either repository. Awaiting review before
proceeding.

### 2026-08-24 — Re-run after round-1 codex feedback (PR #108)

Changed since the prior run: fixed the stale `_m_{param}` matcher-field
reservation in `TestDoubleAnalyzer.cs` (commit `a814b7e`); fixed
`dogfood-validate.sh`'s cleanup trap to restore from a pre-run file
snapshot instead of `git checkout` (commit `a814b7e`); then, after round-2
codex feedback on that same script, fixed lock-ownership tracking (no
longer releases another process's lock on timeout/interrupt), added the
missing `-c "$configuration"` to the `dotnet test` step, and made the
cleanup trap force a nonzero exit if the consumer repo can't be fully
restored to its pre-run state. Also added PLAN-0050 to `docs/plans/README.md`'s
index (was missing).

- `dotnet build`: 0 warnings / 0 errors (full solution, clean rebuild).
- `dotnet test`: 2376/2376 passed (count differs from the prior run's
  2378/2384 because the unrelated `StaleArgMatcherLeakRepro.cs` file — never
  part of this PR — was deleted from the working tree at the user's
  request; no other change to pass/fail composition).
- `scripts/dogfood-validate.sh` (default consumer/solution): packed
  `0.0.0-local.20260824093145-12982-6698`, consumer resolved that exact
  version, full trivia-platform suite: **783/783 passed**. Consumer git
  tree confirmed clean afterward (`git diff -- Directory.Packages.props`
  showed only the same pre-existing intended 0.6.0→0.7.0-preview.81 bump,
  nothing else).

No commits or pushes made beyond what's already on the PR branch at the
time of this run. Awaiting further review.
