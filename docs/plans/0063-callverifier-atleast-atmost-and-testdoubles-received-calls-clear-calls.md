# [PLAN-0063] `CallVerifier.AtLeast`/`AtMost`, `Compono.TestDoubles` `ReceivedCalls()` + `ClearCalls()`

**Status:** Done

**Implements:** [ADR-0044 Amendment 22](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-22-2026-09-06-callverifieratleastintatmostint-added-requirement-3s-minimality-preserved-not-reversed), [ADR-0060](../adr/0060-testdoubles-received-calls-and-clear-calls.md)

Own sequential plan number (not `0044` or `0060`) per `docs/plans/README.md`'s
multi-ADR rule — this plan spans two separately-numbered ADRs and is kept
as one plan deliberately: both land in the same 1.1 verification/inspection
theme, touch overlapping `Compono.TestDoubles` documentation, touch the
same Compono skill files, and share one `evals.json`/baseline-comparison
pass rather than three partial ones.

## Goal

`CallVerifier` gains `AtLeast(int)`/`AtMost(int)`, reachable through
`Compono.TestDoubles`, `Compono.Http`, and `Compono.Logging` with no
consumer-visible inconsistency between them; `Compono.TestDoubles` gains
`ReceivedCalls()` (retrospective, snapshot-based call inspection for the
ADR-0048-eligible member set) and `ClearCalls()` (whole-double observation
reset preserving configured behavior). Done means: all of the above ships
in one PR, with tests, docs, and skill/evals updated and the mandatory
baseline-vs-updated skill comparison run and recorded — not just the code
compiling.

## Scope

**In scope**, per ADR-0044 Amendment 22 and ADR-0060's Decision Outcomes —
this plan does not restate those ADRs' reasoning, only the resulting work:

- `CallVerifier.AtLeast`/`AtMost` (core `Compono`).
- `LogVerificationBuilder.AtLeast`/`AtMost` forwarders (`Compono.Logging`).
- `Compono.TestDoubles` generated `ReceivedCalls()` bridge + generated
  named call-record type, scoped to ADR-0048's existing eligible-member
  set.
- `Compono.TestDoubles` generated `ClearCalls()`, whole-double, clearing
  call counts + ADR-0048 call histories only.
- All documentation, skill, and eval work both ADRs name as mandatory
  completion criteria.
- Dogfooding via `scripts/dogfood-validate.sh` at the appropriate stage.

**Explicitly deferred / out of scope** (per both ADRs and the request that
opened this plan):

- `Compono.Logging` `WithMessageTemplate`/`WithProperty` (RESEARCH-0023,
  queued separately).
- `Compono.Http` async request/body matching, `RespondStream`.
- Overloaded-member `ReceivedCalls()` eligibility expansion.
- Per-member `ClearCalls()`.
- Call-order verification, strict mode, `Between`/`AtLeastOnce`/
  `AtMostOnce`/`Any`/`None`.
- Invocation timestamps/global ordering metadata, bounded/ring-buffer
  history, any configurable capture-history cap.
- Any change to `Exactly(int)`'s existing (non-)validation behavior.

## Tasks

### 1. Core — `CallVerifier.AtLeast`/`AtMost`

- [x] Add `AtLeast(int times)`/`AtMost(int times)` to
      `src/Compono/CallVerifier.cs`, matching `Exactly`'s existing
      structure and `TestDoubleVerificationException` message format
      exactly (per ADR-0044 Amendment 22's exact wording).
- [x] No new fields, no new validation on any count-taking method
      (`Exactly` included — must not change its existing behavior).
- [x] XML docs matching existing density/style on `Never`/`Once`/`Exactly`.

### 2. `Compono.Logging` — forwarders

- [x] Add `AtLeast(int times)`/`AtMost(int times)` to
      `src/Compono.Logging/LogVerificationBuilder.cs`, delegating through
      the existing private `ToCallVerifier()` — same one-line shape as
      `Once`/`Never`/`Exactly`.
- [x] Do not expose `CallVerifier` on `LogVerificationBuilder`'s public
      surface; do not otherwise touch its filtering logic.

### 3. Compiler spike — generated call-record shape (ADR-0060, mandatory before generator work)

- [x] Spike, against a representative multi-parameter ADR-0048-eligible
      member (at least one 2-arg and one 3-arg case, plus one nullable
      reference-type parameter and one generic method already covered by
      ADR-0049's closed-instantiation configuration), whether a
      `readonly record struct` generated per eligible member:
  - [x] compiles cleanly using the per-interface hash-suffix naming scheme
    already proven for `<Hash>_DoubleVerifier` (ADR-0044 Requirement 3);
  - [x] produces no name collision against the interface's own members/types;
  - [x] preserves parameter names and nullable annotations correctly using
    the generator's existing identifier-escaping conventions
    (`TestDoubleEmitter.cs`'s existing `EscapedName`/`OriginalName`
    handling);
  - [x] remains valid for a generic-method call record (confirmed: no
    ADR-0049 closed-instantiation-eligible member ever reaches this code
    path, since that classification is mutually exclusive with
    `IsEligibleForMatching` — see Notes. The legitimate generic case
    is a generic method with an unused own type parameter, spiked
    synthetically since no such member exists in real fixtures today).
- [x] Spike confirmed `readonly record struct` works cleanly: locked in,
      proceeding to Task 4. See Notes for full outcome record.

### 4. Generator/runtime — `ReceivedCalls()`

- [x] `TestDoubleEmitter.cs`/`TestDoubleMemberInfo.cs`: for each ADR-0048
      eligible member (`IsEligibleForMatching`, not
      `IsOverloadMatchingEligible` — ReceivedCalls() is scoped to exactly
      the non-overloaded eligible set), added `ReceivedCallClassName`
      (`{FieldName}_ReceivedCall`) alongside `EntryClassName`, reserved in
      `TestDoubleAnalyzer.AssignCallbackNameSuffixes`'s collision pool.
      Existing `CallLogTypeText`/`CallLogConstructExpression` unchanged —
      the record is an additional, public-facing representation, the
      internal tuple stays for internal matching use.
- [x] `TestDouble.scriban`: emitted the generated
      `internal readonly record struct {{ field }}_ReceivedCall(...)`
      nested in `_Double` (real parameter names via `EscapedName`, not the
      internal-splice-only `OriginalName`), plus the third bridge
      (`{{ safe_identifier }}_DoubleReceivedCalls` wrapper struct +
      `{{ safe_identifier }}_ReceivedCallsExtension.ReceivedCalls()` +
      `{{ safe_identifier }}_DoubleReceivedCallsAccess` per-member
      accessors), mirroring Requirement 3's `_DoubleVerifier`/
      `_VerifyExtension`/`_DoubleVerification` pattern exactly. Per-member
      accessor snapshots `_calls` under the existing `{{ field }}_lock`,
      maps each entry to the new record type, returns
      `IReadOnlyList<T>` (a freshly allocated array) — no live mutable
      collection ever returned.
- [x] No change to existing `Configure()`/`Verify()` emitted code paths —
      confirmed by the generated-source snapshot diff (Task 9): every
      pre-existing emitted line is byte-for-byte unchanged, only new lines
      added.

### 5. Generator/runtime — `ClearCalls()`

- [x] Added `ReturnConfig<T>.ClearObservedCalls()`
      (`src/Compono/ReturnConfig.cs`) mirroring `ClearConfiguredResponse()`'s
      shape — `Interlocked.Exchange(ref CallCount, 0)`, nothing else.
- [x] `TestDouble.scriban`: emitted `{{ safe_identifier }}_ClearCallsExtension.ClearCalls()`
      as a direct extension on the interface (not under `Verify()`/
      `ReceivedCalls()`), iterating every member with three cases:
      (a) `is_closed_instantiation_eligible` — iterate every closed-`T`
      bucket entry via a new non-generic `{{ safe_identifier }}_IClearableCallState`
      interface (implemented by every generated `*_State<T>` class), since
      `ClearCalls()` has no static knowledge of which closed `T`'s exist;
      (b) `is_eligible_for_matching || is_overload_matching_eligible` —
      `lock ({{ field }}_lock) { {{ field }}_calls.Clear(); }` (no separate
      `CallCount` to clear for this shape — `RecordCall()` was already
      removed from its dispatch by ADR-0050, the call log's own `.Count`
      is the count); (c) every other `has_configuration_surface` member —
      `{{ field }}.ClearObservedCalls()`.
- [x] **Real generator-fixture-driven correction found and fixed during
      implementation, not merely a hypothetical risk**: the ADR-0049
      matched-parameters closed-instantiation state class (multi-entry,
      `closed_instantiation_has_matched_parameters`) has **no top-level
      `Config` field at all** — `Config` lives on each nested `Entry`, and
      that shape's dispatch never calls `RecordCall()` on any `Entry.Config`
      either (the shared `Calls` list's own count is authoritative, same
      as the non-generic multi-entry shape). Initial implementation wrote
      `this.Config.ClearObservedCalls()` unconditionally in both
      closed-instantiation branches — a real `CS1061` compile error caught
      by `Compono.Generators.Tests`' real fixture suite (not merely
      inspection). Fixed: the matched-parameters branch's
      `ClearObservedCalls()` now locks `this.Lock`, loops `this.Entries`
      clearing each `Entry.Config.ClearObservedCalls()` (a currently-always-
      zero no-op given no `RecordCall()` call site exists for this shape,
      but the correct, forward-consistent behavior per the field's stated
      contract), and clears `this.Calls`. See "Important implementation
      caution" evidence class 4 in the request that started this
      implementation tranche — this is exactly the kind of storage-model
      trace that caution warned was necessary, and it was necessary in
      practice, not just in theory.
- [x] A second real compile error was also caught and fixed the same way:
      a generic-in-`T` closed-instantiation state class whose type
      parameter is itself literally named `Config`
      (`ClosedInstantiationMatchedParameterTypeParameterNamedAfterNestedEntryMember_GeneratesSupportedDouble`,
      an existing repo fixture covering exactly this collision) makes a
      bare `Config` reference inside that class resolve to the *type
      parameter*, not the field (`CS0704`). Fixed by qualifying every new
      reference as `this.Config`/`this.Lock`/`this.Calls`, which always
      resolves to the instance member regardless of a same-named type
      parameter.
- [x] Confirmed generated `ClearCalls()` never touches `Value`/`Exception`/
      `Sequence`/`SequenceOrdinal`/matcher entries/closed-instantiation
      `Entries` list membership/property-configured state — verified by
      generated-source inspection (Task 9) and the state-preservation test
      matrix (Task 7).

### 6. Tests — CallVerifier / cross-package

- [x] `Compono.Tests`: `AtLeast`/`AtMost` boundary tests (below/equal/above
      for both), negative-value behavior matching `Exactly`'s existing
      (non-)validation, `AtLeast(0)`, `AtMost(0)` vs. `Never()` equivalence,
      exact failure-message assertions. Also added `ClearObservedCalls()`
      unit tests (reset/preserves Value/Exception/does-not-rewind-Sequence).
- [x] `Compono.TestDoubles.Tests`/`Compono.TestDoubles.SampleTests`: proved
      `Verify().Member().AtLeast(...)`/`.AtMost(...)` reachable (via
      `IAccountRepository`/`ILedger` in the AOT smoke test and the
      SampleTests' `ReceivedCallsAndClearCallsTests`/existing `MatchingTests`
      coverage) with zero package-side code changes.
- [x] `Compono.Http.Tests`: `Verify_AtLeastAndAtMost_AreReachableWithNoPackageCodeChanges`
      (`TestHttpHandlerTests.cs`).
- [x] `Compono.Logging.Tests`: `AtLeast_CountsOnlyTheFilteredSubset_NotTheWholeCaptureBuffer`/
      `AtMost_...` (`LogVerificationBuilderTests.cs`) — proves filtering
      happens before the count terminal (four entries captured, two match
      the level filter, `AtLeast(3)`/`AtMost(1)` fail against the filtered
      count of 2, not the unfiltered 4).

### 7. Tests — `ReceivedCalls()` / `ClearCalls()`

- [x] `Compono.TestDoubles.SampleTests/ReceivedCallsAndClearCallsTests.cs`:
      single-call inspection, multiple-call inspection with order
      assertions, snapshot isolation (an earlier snapshot doesn't grow when
      a later call happens), reference-retention semantics (`IArchiver`/
      `MutableRecord` — mutate after the call, assert the captured record
      observes the mutation), value-type argument copy semantics (mutate a
      local `decimal` after the call, assert the captured value is
      unaffected), and the bare-`T`-not-a-tuple single-parameter shape
      (`Rename`).
- [x] `ClearCalls()`: reset-and-preserve-configured-Returns, preserves
      ADR-0050 multi-entry configuration, and the ADR-0060 worked example
      (`ReturnsSequence("A","B","C")` → two calls → `ClearCalls()` → next
      call → `"C"`, not `"A"`) via `ILedger`.
- [x] Concurrency: `ClearCalls_RacingConcurrentInvocations_NeverThrowsOrCorruptsState`
      — a `Barrier`-synchronized pair of tasks (200 iterations each) racing
      concurrent `Withdraw()` calls against concurrent `ClearCalls()` calls
      on the same double; asserts no exception and a legal (0..200) final
      count, not a specific one — deterministic, no sleeps.
- [x] Generated-source snapshot tests (`Compono.Generators.Tests`): all 98
      pre-existing `TestDouble.g.cs` snapshots reviewed (diffed line-by-line
      against their prior verified content — every diff purely additive,
      confirmed before accepting) and re-verified against the new
      `ReceivedCalls()`/`ClearCalls()`/`IClearableCallState` emitted shapes;
      313/313 tests pass on both net10.0 and net11.0.

### 8. AOT / trimming

- [x] Extended `Compono.TestDoubles.AotSmokeTest/Program.cs` (the existing
      project that already exercises `Configure()`/`Verify()`) to exercise
      `AtLeast`/`AtMost` (pass + a caught `TestDoubleVerificationException`
      for a deliberately-failing `AtLeast(5)`), `ReceivedCalls()` (asserts
      count and first captured `accountId`), `ClearCalls()` (asserts
      `Verify().Never()` and an empty `ReceivedCalls()` snapshot
      immediately after, then that multi-entry configuration survives), and
      the sequence-non-rewind invariant, all via `IAccountRepository`.
      Published with `dotnet publish -c Release -f net10.0 -p:PublishAot=true
      -r osx-arm64` and ran the resulting native binary directly (not a
      rerun of the pre-existing smoke path) — exit code 0, `PASS` printed,
      confirming no reflection/dynamic-code-generation path was introduced.

### 9. Compatibility validation

- [x] Public API surface diff review: `Compono.TestDoubles.Tests`/
      `Compono.NSubstitute.Tests`/etc.'s `PublicApiSurfaceTests` pattern
      locks only the *type* set of each package's own static assembly
      (`Compono.TestDoubles.dll` itself never contains generated-double
      types — those are emitted into each *consumer's* compiled assembly),
      so it is structurally unaffected by this change and needed no update;
      confirmed by rerunning it (still passes). No core `Compono`/
      `Compono.Http`/`Compono.Logging` equivalent test exists to update.
      Every change is additive: two new methods on `CallVerifier`, two new
      forwarders on `LogVerificationBuilder`, one new method on
      `ReturnConfig<T>`, and (per eligible interface) new generated
      extension classes/types alongside the unchanged existing ones — no
      existing signature changed.
- [x] Generated-source snapshot diff review: performed as part of Task 7 —
      every one of the 98 changed snapshots' diff was inspected before
      acceptance and contained only new lines; zero existing emitted lines
      changed for any member outside the newly-touched surface.

### 10. Documentation

- [x] `docs/packages/compono-testdoubles.md` — added "Retrospective call
      inspection: `ReceivedCalls()`" and "Resetting observation history:
      `ClearCalls()`" sections next to "Call verification"; covers
      snapshot semantics, ordering semantics, reference-retention
      semantics (with a mutable-argument code example), the ADR-0048
      eligibility boundary (overloaded members explicitly called out as
      unsupported for `ReceivedCalls()`, in both the new sections and "What
      it deliberately doesn't do"), whole-double `ClearCalls()` semantics,
      preserved-vs-cleared state, and the sequence-ordinal non-rewind
      worked example. Also corrected the top-of-file capability summary and
      "What it deliberately doesn't do"/"Next" sections' stale "argument
      capture... outside generated-double support" claims.
- [x] `docs/packages/compono-http.md` — added `.AtLeast(n)`/`.AtMost(n)`
      to the `registration.Verify()` bullet.
- [x] `docs/packages/compono-logging.md` — added `.AtLeast(n)`/`.AtMost(n)`
      to the `logger.Verify()` bullet, stating they forward through the
      same shared count-verification semantics as `Once`/`Never`/`Exactly`.
      Did not describe `WithMessageTemplate`/`WithProperty` — queued
      separately, out of scope for this plan.
- [x] XML docs on every new public/generated-facing type and member
      (`CallVerifier.AtLeast`/`AtMost`, `LogVerificationBuilder.AtLeast`/
      `AtMost`, `ReturnConfig<T>.ClearObservedCalls`, the generated
      `ReceivedCalls()`/`ClearCalls()` bridges' inline comments).
- [x] Samples/examples: `docs/packages/compono-testdoubles.md`'s new
      sections carry runnable-shaped code examples for basic inspection,
      multiple calls, snapshot isolation, the mutable-argument
      reference-retention footgun, and `ClearCalls()` across two phases
      including the sequence non-rewind case; `test/Compono.TestDoubles.SampleTests/ReceivedCallsAndClearCallsTests.cs`
      is this repo's established "sample-doubles-as-tests" mechanism
      (mirroring `MatchingTests.cs`/`VerificationTests.cs`) and exercises
      every one of these scenarios as real, running, packaged-consumer
      code — no separate dedicated samples project exists for
      `Compono.TestDoubles` to add a duplicate example set to.
- [x] NSubstitute migration guidance: `docs/packages/compono-nsubstitute.md`
      was checked (grep for `ReceivedCalls`/`ClearReceivedCalls`/
      `capture`/`migrat`) — it contains no existing statement that
      `ReceivedCalls()`/`ClearReceivedCalls()`-equivalent capability is
      unsupported, so there was nothing stale to correct there; the
      correction landed instead in `compono-testdoubles.md`'s own
      capability summary (above), which is where that claim actually lived.

### 11. Skill

- [x] Grepped the entire `skills/compono/` tree for: `ReceivedCalls`,
      `capture`, `matching is not capture`, `AtLeast`, `AtMost`, `Never`,
      `Exactly`, `call verification`, `overloaded`, `ClearCalls` — hits
      enumerated before editing (75 total, narrowed to the files below).
- [x] `skills/compono/SKILL.md` — corrected **two** stale capability
      summaries (not just the one at the plan's originally-cited
      lines 101-105 — a second, near-identical one existed further down
      the same file, at what were then lines ~172-183, and needed the
      identical fix): both now state what's supported (eligible-member
      `ReceivedCalls()`/`ClearCalls()`, `AtLeast`/`AtMost`) and what still
      isn't (overloaded-member `ReceivedCalls()`, call-order verification,
      strict mode), plus added `ReturnsCallback`-vs-`ReceivedCalls()`
      disambiguation (two distinct, complementary mechanisms) and
      NSubstitute `ReceivedCalls`/`ClearReceivedCalls` vocabulary mapping.
- [x] `skills/compono/references/testdoubles.md` — revised the "Still
      deliberately minimal" line (362-372 as renumbered) to state
      `AtLeast`/`AtMost` are supported and name what's still explicitly
      rejected (`Between`/`AtLeastOnce`/`AtMostOnce`/`Any`/`None`); added a
      new "Retrospective call inspection: `ReceivedCalls()`" section
      (mirroring the doc page); rewrote the "matching is not capture"
      section (renamed nothing, corrected content) to state plain capture
      is now supported for the eligible set and only overloaded-member
      capture/call-order verification/strict mode remain unsupported; also
      corrected a third, narrower stale claim ("does not expose an
      arbitrary call log") in the "Argument matching and filtered
      verification" intro paragraph.
- [x] `skills/compono/references/http.md` — added `AtLeast`/`AtMost` in
      both places `Once`/`Never`/`Exactly` were listed.
- [x] `skills/compono/references/logging.md` — added `AtLeast`/`AtMost`
      forwarding through the shared count-verification semantics; did not
      add `WithMessageTemplate`/`WithProperty` content.
- [x] No other file under `skills/compono/` surfaced a stale hit on the
      grep term list beyond the four files above.
- [x] Did not overstate: no claim of full NSubstitute call-inspection
      parity, no mention of call-order verification as supported, no
      mention of per-member `ClearCalls()` — each explicitly called out as
      still unsupported everywhere the topic comes up.

### 12. Evals

- [x] Snapshotted the pre-change skill via `git show HEAD:...` for
      `SKILL.md`, `references/testdoubles.md`, `references/http.md`,
      `references/logging.md` into a scratchpad baseline directory —
      equivalent to a pre-edit snapshot since none of Task 11's edits had
      been committed, so `HEAD` was still the exact pre-change content.
- [x] Updated `skills/compono/evals/evals.json` (46 → 52 evals): revised
      eval 30 (NSubstitute-migration vocabulary mapping) to include
      `ReceivedCalls`/`ClearReceivedCalls` → `ReceivedCalls().Member()`
      mapping and to stop calling capture a blanket-unsupported boundary;
      added eval 47 (`AtLeast`/`AtMost` usage), 48 (discovering/using
      `ReceivedCalls()` for an eligible member, named-record shape), 49
      (reference-retention semantics — not a bug, with a concrete fix), 50
      (discovering `ClearCalls()` for cross-phase reuse), 51 (`ClearCalls()`
      not rewinding a configured sequence — the exact ADR-0060 worked
      example, single objectively-correct answer "C"), and 52 (a negative
      eval: overloaded-member `ReceivedCalls()` correctly identified as
      unsupported, redirected to the existing `<Member>Matching` surface
      or `Compono.NSubstitute`).
- [x] Ran the **updated** skill (real repo files) against evals
      30/47-52 in a clean subagent context (general-purpose, no
      prior-conversation knowledge, reading only the four skill files):
      answered all seven correctly and precisely — `AtLeast`/`AtMost` as
      two independent terminals (correctly noting no `Between`);
      `ReceivedCalls().Withdraw()` returning named-field records;
      reference-retention explained as documented behavior with a concrete
      mitigation, not a bug; `ClearCalls()` as the whole-double receiver
      with configured behavior preserved; the sequence non-rewind case
      answered "C" with the correct rationale; overloaded-member
      `ReceivedCalls()` correctly declined with a `<Member>Matching`/
      `Compono.NSubstitute` redirect. No hallucinated APIs, no overclaiming.
- [x] Ran the **baseline** (pre-change) skill against the same seven evals
      in a clean subagent context, reading only the snapshot files: on
      every eval touching new 1.1 surface (47-52), it correctly reported
      the capability as **not present** in its source material and
      redirected to `Compono.NSubstitute`/a project-local fake/logging the
      gap as roadmap evidence — it never hallucinated `AtLeast`/`AtMost`,
      `ReceivedCalls()`, or `ClearCalls()` into existence, and where it
      reasoned about `ClearCalls()`'s hypothetical interaction with a
      sequence (eval 51) it explicitly caveated the answer as inference,
      not a confirmed API. Eval 30 (NSubstitute vocabulary mapping, the
      part of the scenario the baseline skill *does* cover) was answered
      correctly by both versions.
- [x] Comparison result: **no regressions** — the baseline's expected
      failures on evals 47-52 are exactly the intended evidence that the
      updated skill teaches new, real capability (not a skill defect being
      masked), and the baseline's own behavior on those evals was safe
      (declined/redirected) rather than wrong (hallucinated). No skill/eval
      iteration was needed.
- [x] Recorded the comparison above, in this plan's own Tasks section (per
      this repo's convention of tracking a plan's own execution detail
      inline, since no separate baseline-comparison-log document exists in
      this repo for skill evals).
- [x] No generated eval workspace files were created outside the two
      subagents' own transcripts (no local eval-runner artifact directory
      exists for this repo's skill-eval mechanism) — nothing to keep out
      of source control beyond the scratchpad baseline snapshot itself,
      which lives outside the repo.

### 13. Dogfooding / final validation

- [x] Ran `scripts/dogfood-validate.sh` (default package set: `Compono`,
      `Compono.NSubstitute`, `Compono.TestDoubles`, `Compono.XunitV3`)
      against the trivia-platform consumer repo — packed local version
      `99.0.0-local.20260906155600-93733-19331`, confirmed every resolved
      reference used that exact freshly-packed version (not a stale cache
      hit), full consumer suite: **783/783 passed, 0 failed**. Consumer's
      `git status --porcelain` confirmed byte-identical before and after
      (no edits to its `Directory.Packages.props` or anywhere else).
      Required starting a local Docker runtime (`colima start`) first, per
      the script's own documented Testcontainers prerequisite.
- [x] As expected, trivia-platform's existing test suite doesn't naturally
      exercise `ReceivedCalls()`/`ClearCalls()`/`AtLeast`/`AtMost` (it
      predates this plan) — per the plan's own instruction, relied on the
      generator/unit/SampleTests/AOT/eval coverage above for those APIs
      rather than standing up a new ad hoc consumer repo. Dogfooding's
      role here is exactly what it's for: proving the packaged artifact
      (analyzer, `PrivateAssets`, `CompilerVisibleProperty` flow) still
      works end-to-end for a real, unrelated consumer — no packaging
      regression, no generator crash processing a large real interface
      surface it had never seen before.

## Critical Files

- `src/Compono/CallVerifier.cs` — `AtLeast`/`AtMost`.
- `src/Compono/ReturnConfig.cs` — new observed-call reset primitive.
- `src/Compono.Logging/LogVerificationBuilder.cs` — two forwarders.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` — new generated
  record-type model per eligible member.
- `src/Compono.Generators/Templates/TestDouble.scriban` — `ReceivedCalls()`
  and `ClearCalls()` emission.
- `test/Compono.Tests/`, `test/Compono.TestDoubles.Tests/`,
  `test/Compono.Http.Tests/` (or equivalent), `test/Compono.Logging.Tests/`
  (or equivalent), `test/Compono.Generators.Tests/` — new coverage per
  Tasks 6-7.
- `docs/packages/compono-testdoubles.md`, `docs/packages/compono-http.md`,
  `docs/packages/compono-logging.md` (or equivalent existing doc paths).
- `skills/compono/SKILL.md`, `skills/compono/references/testdoubles.md`,
  `skills/compono/references/http.md`, `skills/compono/references/logging.md`,
  `skills/compono/evals/evals.json`.

## Test Plan

Per `references/testing.md`'s conventions: unit tests for `CallVerifier`
boundary/message behavior (core `Compono`), package-level reachability/
forwarding tests (TestDoubles/Http/Logging), generated-source snapshot
tests for the new emitted shapes, a deterministic concurrency test for
`ClearCalls()` racing invocation, and an AOT smoke extension. Skill changes
are validated by the mandatory baseline-vs-updated eval comparison (Task
12), not by unit tests. Dogfooding (Task 13) is the final real-consumer
check, scoped to what existing dogfood targets already exercise.

## Notes

**Task 3 compiler-spike outcome (2026-09-06): PASSED, no limitation found.**
Spiked (scratch console app, not committed) a hand-written stand-in for the
generator's `readonly record struct` output against: a 2-arg eligible
member, a 3-arg eligible member with a nullable reference-type parameter,
a 1-arg eligible member (the bare-`T`, non-tuple `CallLogTypeText` shape),
and a `ReceivedCalls()` bridge/wrapper-struct pair mirroring
`{{ safe_identifier }}_DoubleVerifier`'s exact pattern. All compiled
cleanly, preserved real parameter names, and the reference-retention
proof (mutate an `Order` after the call, confirm the later snapshot
observes the mutation) passed as ADR-0060 specifies. **Locked in:
`readonly record struct`, one per eligible member, named
`{{ safe_identifier }}_{{ InterfaceName }}_{{ member.escaped_name }}_ReceivedCall`
(or equivalent single deterministic scheme reusing the existing per-file
`safe_identifier` hash prefix — no separate new hash needed, since
`safe_identifier` is already unique per interface and the member name is
already unique within it after Requirement 3's own collision handling).

**Whether a legitimate ADR-0048-eligible generic member exists:** yes, but
only a narrow, currently-unfixtured shape. Tracing
`TestDoubleAnalyzer.cs:1435-1441`, `isEligibleForMatching` requires (among
other conditions) `!(IsGenericMethod && any parameter references the
method's own open type parameter)` — this does **not** exclude
`IsGenericMethod` outright, only a parameter that references the method's
own type parameter. A generic method whose type parameter is unused by any
real parameter (e.g. `bool TryLog<TMarker>(string message)`, `TMarker`
supplied only at the call site, never appearing in a parameter or return
type) legitimately satisfies `isEligibleForMatching` while
`IsGenericMethod` is `true`. No existing `Compono.Generators.Tests`
fixture exercises this shape today — it was spiked synthetically (see
above) rather than added as a real generator fixture, since the record
type's shape depends only on `Parameters` (identical code path regardless
of `IsGenericMethod`), so no generator/emitter special-casing is needed
for it and manufacturing a fixture purely to exercise an already-covered
code path would be exactly the "don't add a case merely to satisfy the
checklist" the ADR/plan warn against. If a future real interface needs
this shape, it is already correctly handled by the eligibility-set-scoped
implementation below — this is a documented fact, not an open risk.

**PR #134 Codex review round (2026-09-07): three real generator-correctness bugs found and fixed,
each with a real generator-fixture regression test** (`test/Compono.Generators.Tests/TestDoubleVerifyTests.cs`):
1. P1 - `ClearCalls()`/`ReceivedCalls()` are always-emitted, always-zero-argument bridge extensions
   exactly like `Configure()`/`Verify()`, but `TestDoubleAnalyzer`'s reserved-name collision check
   (CMP0023) only covered `"Configure"`/`"Verify"`. An interface declaring its own zero-argument
   `ClearCalls`/`ReceivedCalls` member would silently shadow the generated bridge (ordinary member
   lookup wins over an extension method) with no diagnostic. Fixed: widened the reserved-name set;
   `ClearCallsNamedMember_ReportsCollisionDiagnostic`/`ReceivedCallsNamedMember_ReportsCollisionDiagnostic`
   cover it.
2. P2 - an eligible member's generated `{FieldName}_ReceivedCall` record-class name could collide
   with an unrelated real sibling member's own natural field name (e.g. eligible `Foo` alongside a
   real member literally named `Foo_ReceivedCall`), producing a real CS0102 duplicate-declaration
   compile error - never caught by `AssignCallbackNameSuffixes`' later callback-only disambiguation
   pass. Fixed by feeding this derived name into the SAME earlier `derivedAuxiliaryNameOwners`
   pre-pass that already handles this class of collision for `_calls`/`_lock`/`_Entry`/`_entries`
   (demotes the colliding member out of matching eligibility rather than renaming, the pre-pass's
   established convention). `ReceivedCallRecordNameCollidesWithSiblingMember_FallsBackWithoutRejectingEligibleMember`
   covers it.
3. P2 - a parameter literally named the same as its own member's generated `_ReceivedCall` record
   type (e.g. `Foo(int __Foo_ReceivedCall)`) produced a positional record property sharing its
   enclosing type's name - CS0542. Fixed in `TestDouble.scriban`: that one parameter's declared
   name is suffixed `_Value` inside the record declaration only (positional construction elsewhere
   is order-based, not name-based, so nothing else needed updating). Two real parameters can never
   already share a name, so at most one parameter per record ever needs the suffix.

All three fixes are additive/narrow (no change to any previously-emitted line for a non-colliding
member, confirmed via the same purely-additive-comment snapshot diff review this plan's Task 9
already established as the review method) - the full 313-per-TFM `Compono.Generators.Tests` suite
plus these 4 new fixture tests (8 across net10.0/net11.0) all pass. Also fixed in the same round: a
pre-existing `.github/workflows/package-validation.yaml` gap (unrelated to this plan's own code,
but blocking this PR's checks) - its local validation-only pack never set `-p:Version`, always
defaulting to `1.0.0.0`, which started failing ApiCompat's CP0003 the moment nuget.org's real
published baseline crossed 1.0.0 (`1.1.0-preview.103`, published by PR #133's merge to `main`).
Fixed by pinning that pack's `Version` to the resolved baseline. Also regenerated
`docs/reference/api/` (API reference drift against this plan's own new public members - `CallVerifier.AtLeast`/`AtMost`,
`LogVerificationBuilder.AtLeast`/`AtMost`, `ReturnConfig<T>.ClearObservedCalls`), which had been
missed before the initial PR push.

**Generic-in-`T` closed-instantiation-eligible members (ADR-0049) are
mutually exclusive with `IsEligibleForMatching`**
(`TestDoubleMemberInfo.cs:118`, confirmed again at
`TestDoubleAnalyzer.cs:1429-1433`: `isClosedInstantiationEligible` is
computed first and directly excluded from `isEligibleForMatching`'s own
condition list). `ReceivedCalls()`/`ClearCalls()`'s `_calls`/`_lock`-based
storage never applies to that ADR-0049 bucket-based shape — no design
contradiction, no code path shared, so this plan's Task 4/5 emitter/
scriban changes only ever touch the `member.is_eligible_for_matching`
branch, never the `member.is_closed_instantiation_eligible` branches.
`ClearCalls()` (whole-double, Task 5) still resets a closed-instantiation
member's scalar `CallCount` inside its own per-`T` bucket state (see Task
5 implementation notes below), since that member still has *a* call count
worth clearing even though it has no `ReceivedCalls()` surface.
