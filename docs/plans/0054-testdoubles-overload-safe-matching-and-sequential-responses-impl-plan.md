# [PLAN-0054] Compono.TestDoubles: Overload-Safe Argument Matching and Sequential Responses

**Status:** Done. Phase 1 (sequential/call-count-based responses) and Phase 2 (overload-safe argument matching) are both fully implemented and validated. Full solution sweep: 820/820 on net10.0 (`Compono.Generators.Tests` 274/274 on both net10.0 and net11.0). A real Native AOT publish-and-run covering both phases' scenarios passed. Not yet done: the `dynamodb-distributed-lock` dogfood gate via `scripts/dogfood-validate.sh` - a separate, later gate per explicit instruction, not part of this plan's own scope.

**Implements:** [ADR-0044 Amendment 21](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-21-2026-08-27-argument-matching-for-overloaded-members-is-now-a-pre-10-product-requirement-amendment-18s-boundary-is-superseded-not-merely-evidenced-around), [ADR-0054](../adr/0054-testdoubles-sequential-call-count-based-responses.md)

## Context

`dynamodb-distributed-lock` dogfooding surfaced two real `Compono.TestDoubles` capability gaps against `Amazon.DynamoDBv2.IAmazonDynamoDB` — an overloaded member needing argument-content matching, and three retry-loop tests needing sequential (fail, fail, succeed) responses the SUT consumes internally, with no chance for the test to reconfigure between calls. Both are recorded as **Accepted pre-1.0 requirements** (API still `Proposed`) in the two ADRs above, per explicit product direction — this is not a "should we build this" plan, only a "how."

This plan spans both ADRs in one document (per `docs/plans/README.md`'s multi-ADR rule) because they were evidenced together, reviewed together, and their generated-code paths intersect at exactly one place (an overloaded member with a sequenced entry), which is easier to reason about as one plan than two that have to cross-reference each other's assumptions.

**Out of scope for this plan**, per explicit instruction:
- ADR-0053 (invocation-aware callbacks) — a different, unrelated capability.
- The final consumer dogfooding pass against `dynamodb-distributed-lock` — a separate, later gate using `scripts/dogfood-validate.sh`, run only once both phases below are merged and released to the local dogfood feed.

## Spike findings

Real investigation/spike work already happened across this session (uncommitted, not on a branch) before and after this plan was first written, per the instruction to preserve it as evidence. Distinguishing what it proved from what it changed. **Two rounds of API-safety spikes have now happened**; this section reflects the second (corrected) round — see each ADR's own "Links"/revision history for the first round's now-superseded findings, kept there rather than deleted.

### Confirms ADR assumptions

- **ADR-0054's "entry owns one response representation, no new parallel type" question is answered: no new type needed.** `ReturnConfig<T>` (`src/Compono/ReturnConfig.cs`) already IS the entry-owned response representation for both the plain single-field dispatch shape and each ADR-0050 `Entry.Config` field — extending it in place (two new fields: `SequenceOutcome<T>[]? Sequence`, `int SequenceOrdinal`; one new method: `NextSequenceOutcome()`) required zero changes to `Entry`'s own shape, `TestDoubleEmitter.cs`, or the ADR-0050 entry-append/lock machinery. A parallel `SequenceReturnConfig<T>` was never actually necessary.
- **Ordinal claiming is thread-safe with no lock**, using the exact same `Interlocked.Increment` primitive `RecordCall()`/`CallCount` already establishes as this codebase's chosen concurrency approach for this class of state — no new pattern introduced. Verified with a 500-iteration `Parallel.For` unit test and, separately, a real end-to-end generator test.
- **Call recording is independent of response consumption by construction**, not by new design — `RecordCall()` already runs unconditionally before any `HasConfigured*`/dispatch branching in every dispatch shape, sequence-aware or not.
- **Reconfiguration-resets-ordinal composes with the existing `Returns`/`Throws` last-configuration-wins contract** — extended `Returns`/`Throws` to also clear `Sequence`/`SequenceOrdinal`, and `ReturnsSequence` to clear `Value`/`Exception`/reset the ordinal, so exactly one of the three states is ever live, matching `ReturnConfigBuilder<T>`'s pre-existing documented contract.
- **The declared-return-type contract is preserved, not specially unwrapped for sequences** — per explicit product direction, `ReturnsSequence(params SequenceOutcome<T>[])` uses the same `T` `Returns(T value)` already uses (a `Task<PutItemResponse>` value for a `Task<PutItemResponse>`-returning member, not a bare `PutItemResponse`). No second mental model.
- **AOT/trimming: no reflection introduced anywhere**, in either phase's design — every mechanism spiked (implicit conversions, `Interlocked` ordinal claiming, ordinary generic method overloading) is plain compile-time-resolved code. A real `dotnet publish -p:PublishAot=true` + run against `Compono.TestDoubles.AotSmokeTest` (extended with a sequenced scenario) succeeded for Phase 1's mechanism (modulo the `SequenceOutcome<T>` correction below, not yet re-run against the corrected shape).

### Requires an ADR amendment / another design decision — resolved this round, recorded in both ADRs

**Capability 1 (overload-safe matching) — the API shape changed twice, now settled on real compiler evidence:**

1. *(First round, superseded)* A nested `.For<T1, ...>()` call under a nullary member-name property — rejected, `CS0102` (property/method name collision with the existing discriminator-only method).
2. *(Second round, this session, rejected)* A flattened, purely-generic overload of the member name (`Configure().PutItemAsync<T1, T2>()`, zero real parameters). Compiles and avoids (1)'s collision, but **is a real type-safety hole, confirmed by compile-and-run**: `self.PutItemAsync<int, System.DateTime>()` compiles cleanly and silently selects the real `(PutItemRequest, CancellationToken)` overload — the type parameters are pure, completely unenforced arity witnesses. Rejected as misleading regardless of whether a careful caller would ever actually pass the wrong types.
3. **(Recommended, this session, confirmed safe by compile-and-run against every family that made Amendment 18's original shape ambiguous):** a **separate, matching-specific member name taking real `Match<T>` parameters directly** — e.g. `Configure().DeleteItemAsyncMatching(Match<DeleteItemRequest> request, Match<CancellationToken> ct)`. Verified against numeric widening, base/derived, array-vs-`IEnumerable<T>`, a 3-member same-arity overload set with an overlapping type hierarchy, and the real `IAmazonDynamoDB`-shaped 2/3/4-arity family — every case resolves correctly when the caller supplies an already-`Match<T>`-typed argument (`Match.Any<T>()`/`Match.Is<T>(predicate)`). **Same-arity overloads are fully supported by this shape** — a materially better outcome than shape (2) or the original `.For<T1,...>()` sketch, both of which would have needed a same-arity exclusion/diagnostic. **Literal shorthand does not carry over when it's genuinely ambiguous** — confirmed by a real `CS0121`, and **corrected during Phase 2 implementation** (this claim was originally stated too broadly): a bare literal only reproduces the original ambiguity when two sibling overloads share the same `<Member>Matching` alias name AND the literal implicitly converts to both of their `Match<T>` parameter types (numeric widening - `Get(int)`/`Get(long)` sharing `GetMatching`, called as `GetMatching(5)`). When the alias group's sibling overloads have unrelated parameter types (no shared implicit conversion target - e.g. `DeleteItemAsync(DeleteItemRequest, CancellationToken)` vs `DeleteItemAsync(string, CancellationToken)`, whose aliases take `Match<DeleteItemRequest>` vs `Match<string>`), a bare literal compiles fine and becomes an ordinary equality matcher via `Match<T>`'s own implicit conversion (Amendment 18), exactly like the pre-existing non-overloaded matching-eligible surface - both shapes proven by real `GeneratorTestHelpers.CompileAndExecute` evidence in `TestDoubleOverloadMatchingExecutionTests.cs`.
   - **No `CMP0038` diagnostic is needed** — the same-arity exclusion it would have documented doesn't exist under this shape. This removes an entire diagnostic + its test coverage from Phase 2's scope relative to the first-round plan.
   - **Action taken:** ADR-0044 Amendment 21 has been corrected in place to this shape (superseded text preserved via the Amendment mechanic, not deleted) — see that Amendment's own body for the full reasoning and compiler-evidence table.
- **Overloaded members currently bypass ADR-0050's entries/matching machinery entirely** (confirmed by reading `TestDoubleAnalyzer.cs`'s `isEligibleForMatching` computation directly: `hasConfigurationSurface && !isOverloaded && ...` — a single unconditional exclusion) and the template (`TestDouble.scriban`'s `is_overloaded` branch: one plain `ReturnConfig<T>` field, no `Entry`/`Entries` class, no lock, no call log). Implementing the new matching-specific member name requires *adding* an entries-list dispatch path attached to that real overload — traced to the exact two call sites (`TestDoubleAnalyzer.cs`'s eligibility computation, `TestDoubleEmitter.cs`/`TestDouble.scriban`'s emission) that need to change; see Phase 2 below.

- **Architectural correction (this round, found in review, not by compiler spike): the "independent surface, no fallthrough needed" framing above was wrong.** `DeleteItemAsyncMatching(...)` is a name that exists **only** on the generated `Configure()`/`Verify()` API — the SUT never calls it. Production code still calls the real interface member,
  `IAmazonDynamoDB.DeleteItemAsync(DeleteItemRequest, CancellationToken)`. If `DeleteItemAsyncMatching` backed its *own*, separately-dispatched generated method, a `Configure().DeleteItemAsyncMatching(...)` call would never be consulted by the real dispatch at all — dead configuration. **The matching-specific member name must be a pure `Configure()`/`Verify()`-side alias that attaches its state to the *same* real overload's existing entries/call-recording state, not a second runtime-dispatched member.**
  - **Corrected shape: unify, don't parallel.** An overload that meets the (structural, arity-independent) eligibility conditions gets promoted, for that overload specifically, to the *exact* `Entry`/`Entries`/call-log/lock shape ADR-0050 already emits for a non-overloaded matching-eligible member — reusing `EntryClassName`/`EntriesFieldName` keyed off that overload's own `FieldName`, no new naming scheme. The **existing discriminator-only `Configure(realArgs)` method's implementation changes** (not its signature, not its observable behavior) from "write directly to a single `ReturnConfig<T>` field" to "append an always-matching `Entry`" — this is not a new idea, it is the *exact* migration ADR-0050 already performed for a non-overloaded member's own "compatibility" zero-argument `Configure()` (see `TestDouble.scriban`'s own comment on that overload: "under multi-entry, this no longer needs to null out prior matchers to reproduce 'last wins' — it just appends its own new, all-null-matcher (always-matching) entry"). The new `DeleteItemAsyncMatching(Match<T1>, ...)` method appends a *real*-matcher `Entry` to the **same** list. Dispatch for the real `DeleteItemAsync(DeleteItemRequest, CancellationToken)` explicit interface implementation becomes the *same* reverse-scan-under-lock ADR-0050 already uses — the discriminator-registered (always-matching) entry and any `.Matching()`-registered (real-matcher) entries live in one ordered list, so "a more specific match wins if registered after the broad default, otherwise fall through to the broad default" is the **existing** last-registered-wins scan, not new fallback logic. Discriminator-only `Verify()` also migrates the same way ADR-0050's own compatibility-`Verify()` already did: from `field.ConfiguredCallCount` to the call log's `Count` (an unfiltered read over the same log the new filtered `Verify().DeleteItemAsyncMatching(...)` scans) — both surfaces read from one shared call log, so a call is counted by *both* consistently, automatically.
  - **Consequence for the "byte-identical snapshot" acceptance criterion from the first-round plan: wrong, corrected below.** Every overload meeting the (now arity-independent) eligibility conditions gets *regenerated* to the entries/call-log shape **unconditionally** — structurally, the same way a non-overloaded member already does, whether or not any test in the compilation ever actually calls `.Matching()` against it. Generated code for such an overload *will* change shape. What stays true, and is the real invariant to test: **observable behavior for a consumer who only ever calls the existing discriminator-only `Configure(realArgs)`/`Verify(realArgs)` is unchanged** — same inputs, same outputs, same call counts — proven by execution tests, not by snapshot diffing.
  - **The invariant to hold onto, stated explicitly (per review):** the matching-specific member **name** is only a `Configure()`/`Verify()`-side API disambiguator (needed because C# can't otherwise name "the real-argument overload-selecting call" and "the real-`Match<T>`-parameter call" the same thing without recreating Amendment 18's own ambiguity) — the matching **state** (entries, call log, lock) belongs to, and is keyed by, the real interface overload, never the API name itself. Overload identity (today's existing `FieldName`/discriminator-suffix mechanism, unchanged) remains part of that key, so sibling overloads of the same member name never share entries/call-log state.

**Capability 2 (sequential responses) — `SequenceOutcome<T>`'s conversion design is corrected:**

- **The dual-implicit-conversion shape (`SequenceOutcome<T>` converting implicitly from both `T` and `Exception`) is rejected, confirmed unsafe by real compile-and-run, not by inspection.** For `T = InvalidOperationException`, `new InvalidOperationException(...)` compiles unambiguously but resolves to the *value* conversion (`IsException = false`) — silently the opposite of what a reader would likely expect. For `T = object`, it resolves to the *exception* conversion, and there is no way to express "return this exception as boxed data" for such a `T` at all. Both are real, legitimate return shapes a real interface member could have; an API whose meaning depends on which of two equally-plausible readings C#'s betterness rules silently prefer is unacceptable, independent of whether it happens to compile.
- **Fix, confirmed safe by real compile-and-run across every problematic `T` (`Exception`, `InvalidOperationException`, `object`, `Exception?`) plus `null`:** drop the implicit `Exception → SequenceOutcome<T>` conversion. Keep the single implicit `T → SequenceOutcome<T>` conversion (safe by construction, nothing competes with it). Add an explicit factory, `Compono.SequenceOutcome.Throw(Exception exception)`, returning a small non-generic marker type (`SequenceOutcome.ThrownOutcome`) with its own implicit conversion to `SequenceOutcome<T>` for every `T` — a distinct, non-generic source type can never collide with the `T`-conversion regardless of what `T` is. No explicit type argument is needed anywhere; `T` is still inferred from the surrounding `ReturnConfigBuilder<T>`/params-array context exactly as before.
  - **Action taken:** ADR-0054 has been corrected in place with the full compiler-evidence table and the corrected type sketch.
  - **Action needed in code (Phase 1, not yet done):** `src/Compono/SequenceOutcome.cs`/`ReturnConfigBuilder.cs` currently implement the *rejected* dual-conversion shape (shipped as spike code before this round of investigation) — must be corrected before Phase 1 is considered done. Every existing example (in this plan, in the AOT smoke test, in the generator/unit tests) that passes a bare `Exception` directly to `ReturnsSequence(...)` must be updated to `SequenceOutcome.Throw(...)`.
- **A default-struct hazard in `SequenceOutcome.ThrownOutcome`, flagged in review, not yet spiked — must be closed before the marker type ships.** `ThrownOutcome` is a public `struct`; `default(SequenceOutcome.ThrownOutcome)` (or `default(SequenceOutcome<T>.SomeField)` wherever one is stored) is always constructible regardless of constructor accessibility, and bypasses `SequenceOutcome.Throw(...)`'s own `ArgumentNullException.ThrowIfNull` — producing a `ThrownOutcome` whose internal `Exception` field is `null`. If that default value is ever implicitly converted to `SequenceOutcome<T>`, the result is a "configured to throw `null`" outcome that only fails, confusingly, whenever `NextSequenceOutcome()`/dispatch eventually reaches that array index — a deferred, hard-to-trace failure, not a clear one at the point the mistake was actually made. **Required fix:** `SequenceOutcome<T>`'s implicit conversion *from* `ThrownOutcome` must itself null-check `thrown.Exception` and throw immediately (an `ArgumentException`, matching this codebase's existing `ReturnsSequence`'s own empty-array guard) at the conversion call site — the smallest guard that turns a deferred, confusing failure into an immediate, clear one. Needs a unit test (`SequenceOutcome<T> x = default(SequenceOutcome.ThrownOutcome);` throws immediately) before Phase 1 is done.

### Implementation details that don't affect the public design

- The AOT smoke-test scenario I added initially had a test-authoring bug (reused a `gateway` double instance whose `Send(string)` overload had already recorded an unrelated call earlier in the same file — `Verify().Send("sequenced").Exactly(3)` failed with `4`, since overloaded-member `Verify()` is an unfiltered per-overload count, not filtered by the literal argument's value). Not a generator/runtime bug — a fresh `composer.Create<IGateway>()` instance per independent scenario fixes it. Worth remembering as a real trap for Phase 2's own AOT scenario too (multiple independent test scenarios must not share one double instance if they assert on `Verify()` counts).
- `ReturnConfigSequenceTests.cs`'s empty-sequence and `ReturnConfigBuilder<T>` tests needed to avoid capturing a `ref struct`/`ref`-parameter local inside a lambda (`CS8175`/`CS1628`) — a plain try/catch, not `Should().Throw` on a captured builder, is the working pattern for a `ref struct` API under this test style.

## Public API delta

Representative C# after both phases ship, corrected per this round's spikes:

```csharp
// 1. Overload-safe argument matching (Phase 2) - the real evidenced IAmazonDynamoDB case. A
//    separate, matching-specific member name - NOT the existing discriminator-only DeleteItemAsync.
dynamoDb.Configure().DeleteItemAsyncMatching(
        Match.Is<DeleteItemRequest>(r => r.ConditionExpression == expected),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));

dynamoDb.Verify().DeleteItemAsyncMatching(
        Match.Is<DeleteItemRequest>(r => r.ConditionExpression == expected),
        Match.Any<CancellationToken>())
    .Once();

// The existing discriminator-only surface is untouched and still works, unfiltered:
dynamoDb.Configure().DeleteItemAsync(new DeleteItemRequest(), CancellationToken.None)
    .Returns(Task.FromResult(response));

// 2. A normal (value-only) sequence (Phase 1) - unaffected by the SequenceOutcome<T> correction.
someDouble.Configure().TrySomething().ReturnsSequence(false, false, true);

// 3. Exception -> value.
dynamo.Configure().PutItemAsync(new PutItemRequest(), CancellationToken.None)
    .ReturnsSequence(
        SequenceOutcome.Throw(new ConditionalCheckFailedException("lock exists")),
        Task.FromResult(new PutItemResponse()));

// 4. Exception -> exception -> value (the real evidenced retry shape).
dynamo.Configure().PutItemAsync(new PutItemRequest(), CancellationToken.None)
    .ReturnsSequence(
        SequenceOutcome.Throw(new ConditionalCheckFailedException("lock exists")),
        SequenceOutcome.Throw(new ConditionalCheckFailedException("lock exists")),
        Task.FromResult(new PutItemResponse()));

// 5. Sequencing combined with ADR-0050 argument-distinguished entries - each entry owns its
//    own independent sequence/ordinal.
repository.Configure().Withdraw(Match.Is<string>(id => id == "acct-1"), Match.Any<decimal>(), Match.Any<bool>())
    .ReturnsSequence(false, true);
repository.Configure().Withdraw(Match.Is<string>(id => id == "acct-2"), Match.Any<decimal>(), Match.Any<bool>())
    .ReturnsSequence(true, false);
```

New public surface introduced across both phases (corrected from the first-round sketch):
- `Compono.SequenceOutcome<T>` — **corrected**: a single implicit conversion, from `T` only. No public constructor.
- `Compono.SequenceOutcome` (new, non-generic static class) — `Throw(Exception exception)`, returning `Compono.SequenceOutcome.ThrownOutcome` (a small marker type with its own implicit conversion to `SequenceOutcome<T>` for any `T`).
- `Compono.ReturnConfigBuilder<T>.ReturnsSequence(params SequenceOutcome<T>[])` (shipped in spike form, unaffected by the `SequenceOutcome<T>` correction).
- `Compono.ReturnConfig<T>.HasConfiguredSequence` / `NextSequenceOutcome()` (shipped in spike form — public because generated dispatch code in the consumer's own assembly reads it, same reasoning as every other `ReturnConfig<T>` accessor).
- A new generated (not core-library) `Configure()`/`Verify()` member name per overloaded member needing matching (illustrative convention: a `Matching` suffix, e.g. `DeleteItemAsyncMatching` — exact convention TBD at implementation time), taking real `Match<T>` parameters directly. **This name is a configuration/verification-side alias only** — it attaches its entries/call-log state to the *same real overload* the existing discriminator-only `Configure()`/`Verify()` methods already dispatch through; it is never itself an independently-invoked generated method. Phase 2, generated code only, no new core `Compono`/`Compono.TestDoubles` public type.
- **No new diagnostic code** — `CMP0038` is no longer needed (see "Spike findings" above); this is a reduction from the first-round plan, not an addition.

## Scope

**In scope:**
- Phase 1: sequential/call-count-based responses (`ReturnConfig<T>` extension, corrected `SequenceOutcome<T>`/`SequenceOutcome.Throw(...)` shape, template dispatch wiring, generator/runtime tests, AOT proof).
- Phase 2: overload-safe argument matching via a new matching-specific member name, for every overloaded member needing it (no arity restriction — same-arity overloads are fully supported, see "Spike findings") — discovery/eligibility, emitter/template, generator/runtime tests, AOT proof.

**Explicitly deferred** (per ADR-0054's own scope note and this session's instructions):
- Void/non-generic-`Task`/`ValueTask` sequence shapes beyond what Phase 1's implementation naturally covers (see Phase 1 Tasks — the AOT smoke test already exercises exception-only sequencing on a `void` member, which fell out for free; no further void-specific work is planned unless Phase 1 testing finds a real gap).
- A member that is both generic *and* overloaded, needing the new matching-specific surface (real boundary, not yet spiked — see Phase 2 Tasks).
- `ref`/`out` parameters interacting with the new matching-specific surface (real boundary, not yet spiked — see Phase 2 Tasks; `in` is already known-fine, no new behavior needed).
- ADR-0053 invocation-aware callbacks.
- The `dynamodb-distributed-lock` consumer dogfooding pass — separate gate, `scripts/dogfood-validate.sh`, after both phases are merged.

## Phase 1: Sequential/call-count-based responses

**Goal:** `ReturnConfigBuilder<T>.ReturnsSequence(...)` works end-to-end through the real generator, for both dispatch shapes it can reach (plain single-field member, ADR-0050 entries-list member), using the **corrected** `SequenceOutcome<T>`/`SequenceOutcome.Throw(...)` shape, with real generator, unit, and Native-AOT proof.

**Status:** Done. `SequenceOutcome<T>` rewritten to the corrected shape (single implicit conversion from `T`; `SequenceOutcome.Throw(Exception)` → `ThrownOutcome`, with a null-guard on the `ThrownOutcome → SequenceOutcome<T>` conversion against `default(ThrownOutcome)`). All acceptance criteria below met.

### Production changes

- `src/Compono/SequenceOutcome.cs` — **rewrite**, not just extend: `SequenceOutcome<T>` keeps only the implicit conversion from `T`; add the non-generic `SequenceOutcome` static class with `Throw(Exception)` → `ThrownOutcome`, and `SequenceOutcome<T>`'s second implicit conversion is from `ThrownOutcome`, not `Exception`.
- `src/Compono/ReturnConfig.cs` — unaffected by the `SequenceOutcome<T>` correction (already spiked: `Sequence`/`SequenceOrdinal` fields, `HasConfiguredSequence`, `NextSequenceOutcome()`).
- `src/Compono/ReturnConfigBuilder.cs` — unaffected by the correction (`ReturnsSequence(params SequenceOutcome<T>[])`; `Returns`/`Throws` also clear sequence state — already spiked).
- `src/Compono.Generators/Templates/TestDouble.scriban` — unaffected by the correction (already spiked: a `HasConfiguredSequence` check ahead of the existing `HasConfiguredException`/`HasConfiguredValue` checks at every `ReturnConfig<T>`-consuming dispatch site — plain void member, plain non-void member, property getter, ADR-0050 entries-list void loop, ADR-0050 entries-list non-void loop, closed-instantiation-eligible entries loop, closed-instantiation-eligible no-params ternary — 7 sites total).

### Tasks

- [x] Rewrite `src/Compono/SequenceOutcome.cs` to the corrected shape (`SequenceOutcome<T>` single implicit conversion from `T`; new `SequenceOutcome.Throw(Exception)`/`ThrownOutcome`).
- [x] Confirm the 7 template dispatch sites are exactly and only the sites touched (`grep -n "HasConfiguredException" TestDouble.scriban` before/after should show one new `HasConfiguredSequence` line per existing one) — confirmed unchanged (7 pairs, no new/missing sites); the `SequenceOutcome<T>` rewrite touched only `SequenceOutcome.cs`, invisible to the template.
- [x] `test/Compono.Tests/ReturnConfigSequenceTests.cs` — updated every existing case that passed a bare `Exception` to `ReturnsSequence(...)` to `SequenceOutcome.Throw(...)`; added new unit coverage for the corrected shape: `T = Exception` (value-conversion vs. `Throw` both resolve unambiguously), `T = InvalidOperationException` (value-conversion resolves as value, not throw), `T = object` (`Throw` still resolves as throw; value-conversion still works), `T = Exception?` and `T = string?` (`null` via the `T`-conversion resolves as a value, not a throw), and `default(SequenceOutcome.ThrownOutcome)` converted to `SequenceOutcome<int>` throws `ArgumentException` immediately at the conversion site. All prior coverage retained. 17/17 passing.
- [x] `test/Compono.Generators.Tests/TestDoubleSequentialResponseExecutionTests.cs` — updated to `SequenceOutcome.Throw(...)`; all 6 real end-to-end generator-execution tests passing (zero-parameter value sequence, zero-parameter mixed exception/value sequence, calls-still-count-toward-verification, matching-eligible member value sequence, two independent argument-matched entries with independent ordinals, reconfiguring an entry resets its ordinal).
- [x] Snapshot review: reran `test/Compono.Generators.Tests` (262/262 passing) — zero `.received.cs` files produced, confirming the `SequenceOutcome<T>` rewrite produced zero snapshot diffs as expected.
- [x] `test/Compono.TestDoubles.AotSmokeTest/Program.cs` — updated to `SequenceOutcome.Throw(...)`; real `pack-compono.sh` + `dotnet publish -c Release -f net10.0 -p:PublishAot=true` + direct run of the published binary printed `PASS` and exited 0 — confirms the mixed exception/value `Task<T>` sequence, exhaustion-repeats-final, `Verify().Exactly(n)` correctness across throwing calls, two independent ADR-0050 entries with independent ordinals, and the fresh-double-instance `void`-member exception-only sequence (the earlier interrupted re-verification is now complete) all survive Native AOT.
- [x] Full solution test sweep: `dotnet test -f net10.0` at the repo root — 808/808 passing across every `test/*.Tests` project (`Compono.Tests` 274, `Compono.Generators.Tests` 262, `Compono.TestDoubles.Tests` 6, `Compono.DependencyInjection.Tests` 17, `Compono.TUnit.Tests` 52, `Compono.Http.Tests` 29, `Compono.Bogus.Tests` 63, `Compono.NSubstitute.Tests` 23, `Compono.XunitV3.Tests` 70, plus the two sample test projects), zero failures.
- [x] `skills/compono/references/testdoubles.md` — replaced the "no sequential/call-count-based responses" language with a new "Sequential/call-count-based responses" section documenting `.ReturnsSequence(...)`/`SequenceOutcome.Throw(...)`, and removed the item from the unsupported-capabilities list.

### Test plan

Covered by the Tasks checklist above — unit (`Compono.Tests`), real generated-code execution (`Compono.Generators.Tests`), and real Native AOT publish-and-run (`Compono.TestDoubles.AotSmokeTest`), matching this repo's three-tier verification convention for a `Compono.TestDoubles` capability (see `TestDoubleVerificationExecutionTests.cs`'s own doc comment for why the generator-execution tier exists separately from unit tests of the runtime type alone).

### Acceptance criteria

- Every Tasks checkbox above is checked.
- `dotnet test` green across every `test/*.Tests` project on at least one TFM.
- The AOT smoke test's real `dotnet publish -p:PublishAot=true` binary runs and prints `PASS`.
- No `.received.cs` files left in `test/Compono.Generators.Tests/Snapshots/` after snapshot review.
- No production or test code anywhere passes a bare `Exception` directly to `ReturnsSequence(...)` — every exception outcome goes through `SequenceOutcome.Throw(...)`.

## Phase 2: Overload-safe argument matching

**Goal:** every overloaded member needing content-based argument matching (no arity restriction — same-arity overloads are fully supported, per "Spike findings") gets a new, matching-specific `Configure()`/`Verify()` **member name** taking real `Match<T>` parameters directly — but that name is purely a configuration-side alias: its state (entries, call log, lock) attaches to the **same real overload** the existing discriminator-only surface already dispatches through, so a call the SUT actually makes is visible to both surfaces consistently. The existing discriminator-only surface's *signature and observable behavior* are unaffected; its *generated implementation* is not, for any overload that becomes matching-eligible (see "Spike findings" — this replaces the first-round "byte-identical snapshot" claim, which was wrong).

**Status:** Done. Implemented exactly per the "Architecture (revised)" design below: `IsOverloadMatchingEligible`/`MatchingMemberName` in `TestDoubleAnalyzer.cs`, unified entries/call-log/lock state reused from ADR-0050 in `TestDouble.scriban`, real generator-execution and snapshot coverage, a real Native AOT publish-and-run, and the skill reference doc updated. All acceptance criteria below met; two corrections to the plan's own text found and recorded during implementation (the generic+overloaded extension needing `generic_suffix`, and the literal-shorthand claim being narrower than originally stated) - see "Spike findings" and the Tasks checklist above.

### Architecture (revised)

**State ownership — per real overload, not per API name:**

```csharp
// One Entry/Entries/call-log/lock set per matching-eligible OVERLOAD (keyed off that overload's
// own existing FieldName/discriminator suffix - unchanged identity mechanism).
internal sealed class __DeleteItemAsync_<hash>_Entry
{
    internal Match<DeleteItemRequest>? Matcher_request;       // set only by DeleteItemAsyncMatching(...)
    internal Match<CancellationToken>? Matcher_cancellationToken;
    internal ReturnConfig<Task<DeleteItemResponse>> Config;   // ADR-0054-capable: value, exception, or sequence
}
internal readonly List<__DeleteItemAsync_<hash>_Entry> __DeleteItemAsync_<hash>_entries = [];
internal readonly List<(DeleteItemRequest, CancellationToken)> __DeleteItemAsync_<hash>_calls = [];
internal readonly object __DeleteItemAsync_<hash>_lock = new();
```

- **`Configure().DeleteItemAsync(realRequest, realToken)`** (existing, signature unchanged) now appends an **always-matching** entry to this list — the exact migration ADR-0050 already performed for a non-overloaded member's own zero-argument "compatibility" `Configure()`.
- **`Configure().DeleteItemAsyncMatching(Match<DeleteItemRequest>, Match<CancellationToken>)`** (new) appends a **real-matcher** entry to the *same* list.
- **`Verify().DeleteItemAsync(realRequest, realToken)`** (existing, signature unchanged) reads the call log's unfiltered `Count` — the same migration ADR-0050's own compatibility-`Verify()` already performed.
- **`Verify().DeleteItemAsyncMatching(Match<DeleteItemRequest>, Match<CancellationToken>)`** (new) reads the call log filtered by the supplied matchers — the same filtered-scan ADR-0050 already implements for a non-overloaded matching-eligible member.

**Dispatch (the real `IAmazonDynamoDB.DeleteItemAsync(...)` explicit interface implementation) — the existing ADR-0050 reverse-scan, unchanged in shape:**

1. Under the shared lock: append the actual `(request, cancellationToken)` to the call log (so *both* `Verify()` surfaces see every real call, regardless of which entry — or none — ends up answering it).
2. Reverse-scan `entries` newest-to-oldest. For the first entry whose matchers all match the real arguments (an always-matching discriminator-entry matches unconditionally, exactly like today):
   - `HasConfiguredSequence` → `NextSequenceOutcome()` (ADR-0054, already wired into `ReturnConfig<T>`/the template — no new interaction code needed here, it's the same `Config` type every other entry already uses).
   - else `HasConfiguredException` → throw.
   - else `HasConfiguredValue` → return.
   - else (matched but not yet configured) continue the scan, exactly like ADR-0050's own "no `break`" rule.
3. If nothing configured is found anywhere in the list: existing default/configuration-required fallback, unchanged.

**Why no separate "fall back to the old field" step is needed:** there is no separate old field once an overload is promoted — the discriminator-only `Configure()` call *is* an entry in the same list. Registration order gives the correct precedence for free: register the broad discriminator response first, a specific `.Matching()` override second, and the reverse-scan finds the specific one first, falling through to the broad one for anything it doesn't match — the same idiom the AOT smoke test's own `Withdraw` scenario already demonstrates for a non-overloaded member.

**Overload identity stays part of the key:** each overload keeps its own independent `Entry`/`Entries`/call-log/lock set (already-existing per-overload `FieldName`-derived naming) — a call to a sibling overload of the same member name can never be recorded into, or matched against, another overload's state.

### Naming/collision policy for the matching-specific member name

The convention `<MemberName>Matching` was illustrative through this ADR/plan's earlier revisions, with the exact convention left "TBD at implementation time." Before committing to it, real compiler spikes checked what happens when the interface's own closure already contains a real member whose literal name equals the candidate alias name — the case the review raised:

```csharp
interface IFoo
{
    Result Get(Request request);
    Result Get(string id);       // overloaded Get -> wants an alias named "GetMatching"

    Result GetMatching(SomeOtherType value);   // a REAL, unrelated member with that exact name
}
```

**Findings (real `dotnet build`/`dotnet run`, not predicted):**

1. **No real collision (the common case):** when the real `GetMatching` member's own generated `Configure()` extension has a genuinely different parameter-type signature than either of the alias's own generated overloads (`GetMatching(Match<SomeOtherType>)` vs. `GetMatching(Match<Request>)`/`GetMatching(Match<string>)`), all three coexist as ordinary C# overloads, resolved correctly by argument type — confirmed by compile-and-run.
2. **Real collision:** when the real member's own generated extension signature is *identical* to one of the alias's own generated overloads (e.g. the real member happens to be `GetMatching(string value)`, whose own `Configure()` extension would be `GetMatching(Match<string> value)` — exactly the same signature the alias already generates for the `Get(string)` overload) — confirmed `CS0111` ("already defines a member with the same parameter types"). A real, if narrow, risk that must be detected and handled deterministically, not left to produce broken generated code.
3. **Inheritance-hierarchy variant of (1)/(2):** doesn't introduce a new case. Every member (real or alias) a generated double implements is flattened onto *one* `_DoubleConfiguration` static class regardless of which interface in the closure originally declared it (already true architecturally — confirmed by every existing generated-code example touching a multi-interface closure, e.g. `IRepository : IClock` in the AOT smoke test) — so the collision check is naturally interface-closure-wide, not per-declaring-interface, with no separate mechanism needed for the cross-interface case.
4. **A real *generic* member sharing the candidate name** (`GetMatching<T>(Match<T> value)`, alongside the non-generic alias overloads) — confirmed to **compile cleanly, no `CS0111`** (generic arity is part of C# overload identity, and the alias's own overloads are never artificially generic — see the rejected flattened-selector shape). **A softer, non-blocking finding worth documenting, not solving in this phase:** an ordinary (non-explicit-type-argument) call always prefers the non-generic overload when both are applicable — confirmed by compile-and-run — so if the real generic member's own closed instantiation for some `T` happens to share a signature with one of the alias's concrete overloads, that specific instantiation becomes reachable only via an explicit type argument on the real member's own name, never implicitly. Since generic-and-overloaded matching is already out of this phase's scope, this is recorded as a documented caveat for `references/testdoubles.md`, not new template logic.

**Policy, in the priority order requested:**

1. **Default:** generate the natural `<MemberName>Matching` name. This is safe and predictable for every case that doesn't hit finding (2) above — the overwhelming majority.
2. **On a detected real collision (finding 2):** fall back to a **deterministic alternate name**, not a diagnostic and not silently dropping matching support. This repo already has a proven, shipped mechanism for exactly this shape of problem — `TestDoubleAnalyzer`'s existing derived-name-collision detection (`derivedNameCollisionMembers`) and discriminator-suffix pre-pass, covered by the existing `OverloadSuffixCollidesWithDifferentlyNamedRealMember_GeneratesDoubleWithDistinctFieldNames` test: when a generated name collides with a real member's own literal name, lengthen/rehash the generated name until it's globally unique. Extend that **same** mechanism's reserved-name pool to include the candidate `<MemberName>Matching` name, with the same fallback shape (e.g. `<MemberName>Matching_<hash>`, using the existing `TestDoubleOverloadIdentity.StableHash`/`DiscriminatorSuffix` convention) — not a new mechanism, an extension of an existing one. Per the instruction to avoid hash-looking names "in the normal case": this fallback is exactly that — a rare, deterministic escape hatch for a proven real collision, not the default shape.
3. **No new diagnostic is needed.** Because the deterministic fallback in (2) always succeeds (the existing discriminator-suffix mechanism already has its own collision-handling for the astronomically rarer case of the *hash itself* colliding), there is no case where "no clean generated surface" actually occurs — so priority 3 ("an actionable diagnostic only if there is no clean generated surface") is never reached for this specific problem, consistent with Phase 2 needing no new `CMP0xxx` code anywhere.

**Documentation:** `references/testdoubles.md` can describe the rule in one sentence — "the matching-specific member name is `<Member>Matching`; in the rare case that collides with a real member of that exact name, Compono disambiguates automatically, the same way it already does for other generated names" — without needing to explain the mechanism's internals to a consumer, since the fallback is deterministic and (per finding 2) provably rare.

### Production changes

- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs`:
  - New `TestDoubleMemberInfo` flag (name TBD, e.g. `IsOverloadMatchingEligible`) = `hasConfigurationSurface && isOverloaded && <every other ADR-0048 eligibility condition `isEligibleForMatching` already checks: parameters.Count > 0, no ref-like parameter, no derived-name collision, not the `Equals`-collision shape, no open-type-parameter reference for a generic method>` — same condition list as `isEligibleForMatching`, minus the `!isOverloaded` guard. **No arity-uniqueness check** (same-arity is fully supported).
  - Matching-specific member name derivation: `<MemberName>Matching` by default; on a detected real-member-name collision (see "Naming/collision policy" above), fall back to the existing discriminator-suffix mechanism's hash-suffixed convention — extend `derivedNameCollisionMembers`'s existing reserved-name pool, don't invent a parallel check.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` / `Templates/TestDouble.scriban`:
  - For each `IsOverloadMatchingEligible` overload: **replace** its existing plain-`ReturnConfig<T>`-field emission with the `Entry`/`Entries`/call-log/lock shape above (reusing `EntryClassName`/`EntriesFieldName` keyed off that overload's own `FieldName`).
  - The real interface member's explicit-implementation dispatch body for that overload changes to the reverse-scan shape above (structurally identical to today's non-overloaded matching-eligible dispatch, not new logic).
  - `Configure()`/`Verify()` extensions for that overload: the **existing** discriminator-only method's body changes to "append an always-matching entry" / "read the call log's `Count`"; a **new** `Matching`-suffixed method is added, taking `Match<T1>, Match<T2>, ...` directly, appending a real-matcher entry / performing a filtered scan — mirroring the existing non-overloaded matching-eligible member's own two-method shape (zero-arg compatibility + real-matcher) as closely as possible, just under two *different* names instead of one overloaded name (forced by C#, since the real-argument and `Match<T>`-argument versions can't share a name without reopening Amendment 18's ambiguity).
  - An overloaded member that does **not** meet `IsOverloadMatchingEligible` (fails an eligibility condition — e.g. has a `ref`/`out` parameter, per Amendment 18's existing carve-out) keeps today's plain single-field shape, completely unaffected.
- `skills/compono/references/testdoubles.md` — replace the "overloaded members are discriminator-only, never matchers" language with the corrected boundary, and document that the discriminator-only surface's *underlying storage* changes for a matching-eligible overload even though its own call signature/behavior doesn't.

### Tasks

- [x] **Before any template work**: proven not via a separate standalone spike but by the fact that `GeneratorTestHelpers.Verify`'s own `outputCompilation.GetDiagnostics()` assertion (zero errors) passes for every one of the 265+ fixtures below — the discriminator-only and `Matching`-named methods coexist cleanly on the same `{{safe_identifier}}_DoubleConfiguration`/`_DoubleVerification` static classes in every case, including the dedicated `OverloadedMemberWithRealParameters_GeneratesMatchingSpecificMemberNameSharingSameOverloadState` snapshot test.
- [x] `TestDoubleAnalyzer.cs`: new `IsOverloadMatchingEligible` flag (condition list mirrors `IsEligibleForMatching` minus the `!IsOverloaded` guard, plus `!IsClosedInstantiationEligible`; no arity-uniqueness computation) + `MatchingMemberName` derivation with the hash-suffixed collision fallback (signature-based, per "Naming/collision policy").
- [x] `TestDoubleEmitter.cs`/`TestDouble.scriban`: each `IsOverloadMatchingEligible` overload promoted to the unified `Entry`/`Entries`/call-log/lock shape (reusing the existing `IsEligibleForMatching` state-declaration/dispatch blocks, gated on `is_eligible_for_matching || is_overload_matching_eligible`); the discriminator-only `Configure()`/`Verify()` bodies now append/scan the shared entries list instead of a removed single field; the new `Matching`-named `Configure()`/`Verify()` methods added; the real overload's dispatch reuses the existing reverse-scan shape unchanged.
- [x] Spike (real, kept as regression coverage - `GenericAndOverloadedMatchingEligibleMember_GeneratesGenericConfigureAndMatchingExtensions`): **yes** - a member that is both generic and overloaded (with a real parameter not referencing its own type parameter, so still matching-eligible-shaped) needs the SAME "extension becomes generic" treatment Amendment 1 already gives the discriminator-only surface, applied identically to the new `Matching`-named method (`{{ member.generic_suffix }}`/`{{ member.constraint_clauses_text }}` added to both). Found by a real snapshot regression sweep after the initial (ungeneric) template draft: a same-parameter-types generic/non-generic overload pair (e.g. `Process<T>(int, string)` / `Process(int, string)`) collided (`CS0111`) with a fixed, non-generic `ProcessMatching` signature until fixed - confirmed compiling cleanly (zero diagnostics) after the fix.
- [x] Spike (real, kept as regression coverage - `RefOutOverloadSibling_StaysFallbackOnly_MatchingEligibleSiblingsUnaffected`): a `ref`/`out` overload needs **no new rule** - `IsOverloadMatchingEligible` already requires `WouldGetConfigurationSurface`, which already excludes a `ref`/`out`/`in` parameter unconditionally (Amendment 5). Confirmed with a real three-way overload set (one `ref`/`out` sibling with no surface at all, two real-parameter siblings each independently promoted) - the `ref`/`out` sibling still reports `CMP0030` and is otherwise untouched; its two siblings each get their own `Matching`-named surface.
- [x] `test/Compono.Generators.Tests/TestDoubleVerifyTests.cs`-style snapshot coverage: `OverloadedMemberWithRealParameters_GeneratesMatchingSpecificMemberNameSharingSameOverloadState` (a matching-eligible overload generating both the unchanged-signature discriminator method and the new `Matching`-named method, both attached to one shared entries/call-log state); `OverloadMatchingAliasCollidesWithRealMemberOfThatName_FallsBackToHashSuffixedName` and `OverloadMatchingAliasNameMatchesButSignatureDoesNotCollide_KeepsNaturalName` (the naming-collision pair below); `RefOutOverloadSibling_StaysFallbackOnly_MatchingEligibleSiblingsUnaffected` (a non-matching-eligible overloaded member unaffected). Every one of the 77 Phase-1-touched snapshots was also reviewed and confirmed to change ONLY where the member is actually `IsOverloadMatchingEligible` - see "Meaningful generated-code changes" in the completion report.
- [x] Real end-to-end generator execution tests (`test/Compono.Generators.Tests/TestDoubleOverloadMatchingExecutionTests.cs`, mirroring Phase 1's `TestDoubleSequentialResponseExecutionTests.cs` pattern) — **the invariant proved throughout is that the matching-specific name only configures/observes; the SUT-visible dispatch is always through the real overload**:
  - **Coexistence/precedence** (user-specified example): proved by `CoexistencePrecedence_MatchingEntryOverridesDiscriminatorFallback_ForMatchingCallsOnly`.
  - **Sibling-overload independence**: proved by `SiblingOverloadIndependence_ConfiguringOneOverloadNeverAffectsTheOther`.
  - **Filtered verification**: proved by `FilteredVerification_CountsOnlyRealCallsMatchingThePredicate`.
  - **Discriminator verification unchanged**: proved by `DiscriminatorVerification_StillReportsTotalRealCallCount_BackedByTheCallLogNow`.
  - **Sequencing on a matching-eligible entry**: proved by `SequencingOnAMatchingEligibleEntry_EveryRealCallStillRecordedInTheSharedCallLog`.
  - **Literal shorthand — corrected finding, see "Spike findings"**: `LiteralShorthandOnTheMatchingNamedSurface_CompilesAndMatchesByEquality` proves a literal compiles and matches by equality when the sibling overloads' `Match<T>` types are unrelated (no shared implicit-conversion target); `LiteralShorthandAmbiguousAcrossSiblingOverloads_FailsToCompile` proves the real `CS0121` only when two siblings' `Match<T>` types are ambiguously literal-convertible (numeric widening). The plan's original text ("literal shorthand does not carry over") overstated this as a blanket rule; both shapes are now real, evidenced tests.
  - **Naming collision**: proved at the snapshot layer (`TestDoubleVerifyTests.cs`, above) rather than execution, since the fallback name itself is opaque/hash-derived and not meant to be called directly from a consumer's test.
- [x] `Compono.TestDoubles.AotSmokeTest`: extended `IGateway`'s existing overloaded-member coverage with coexistence/precedence (`SendMatching` narrower override vs. the broad `Send(...)` discriminator entry) and sibling-independence (`Send(string)` vs `Send(int, string)`) - proven via a real `pack-compono.sh` + `dotnet publish -c Release -f net10.0 -p:PublishAot=true` + run of the published binary, printed `PASS`, exit 0.
- [x] `skills/compono/references/testdoubles.md` update — new "Overload-safe argument matching (ADR-0044 Amendment 21)" section added under "Overloaded members (v2)": the `<Member>Matching` naming rule, the corrected (non-blanket) literal-shorthand behavior, the rare-collision fallback, and the documented generic-member soft-shadowing caveat.
- [x] Full solution test sweep green (see validation results in the completion report).

### Test plan

Same three-tier shape as Phase 1 (snapshot/compile-only coverage in `TestDoubleVerifyTests.cs`, real generated-code execution in `TestDoubleOverloadMatchingExecutionTests.cs`, real Native AOT proof) plus the coexistence/precedence, sibling-independence, filtered-verification, and sequence-interaction scenarios listed above — these are the tests that actually prove the corrected architecture (state attaches to the real overload) rather than the rejected one (an independently-dispatched alias member).

### Acceptance criteria

- Every Tasks checkbox above is checked.
- The real `IAmazonDynamoDB.DeleteItemAsync(Match.Is<DeleteItemRequest>(x => x.ConditionExpression == expected), Match.Any<CancellationToken>())` shape from the original dogfood investigation is provably expressible (a generator test using that exact real AWS SDK shape, or as close a stand-in as `Compono.Generators.Tests`' existing fixtures allow without taking a new external package dependency), via the new matching-specific member name, **and dispatches through the real overload** (proven by the coexistence test, not assumed).
- `dotnet test` green across every `test/*.Tests` project on at least one TFM.
- The AOT smoke test's real published binary runs and prints `PASS`.
- **Corrected criterion** (the first-round plan's "byte-identical snapshot" claim was wrong — see "Spike findings"): every matching-eligible overload's generated code changes shape (new entries/call-log emission); every **non**-matching-eligible overloaded member, and every non-overloaded member, is unaffected — verified by diffing the full snapshot set against Phase 1's already-clean baseline and confirming only matching-eligible-overload fixtures changed. Discriminator-only `Configure()`/`Verify()` **observable behavior** (not generated code shape) for a matching-eligible overload is unchanged — proven by execution tests, not snapshot diffing.

## Critical files

- `src/Compono/SequenceOutcome.cs` — rewritten to the corrected shape, including the `ThrownOutcome`-from-`default` guard (Phase 1).
- `src/Compono/ReturnConfig.cs`, `src/Compono/ReturnConfigBuilder.cs` — extended (Phase 1, unaffected by the `SequenceOutcome<T>` correction).
- `src/Compono.Generators/Templates/TestDouble.scriban` — extended in both phases (7 sites in Phase 1; Phase 2 **replaces**, not adds alongside, each matching-eligible overload's dispatch/`Configure()`/`Verify()` emission with the unified entries/call-log shape).
- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — extended (Phase 2 only: new eligibility flag + matching-specific member name derivation).
- `test/Compono.Tests/ReturnConfigSequenceTests.cs`, `test/Compono.Generators.Tests/TestDoubleSequentialResponseExecutionTests.cs` — new/updated (Phase 1) — the former gains the `ThrownOutcome`-default-guard test.
- `test/Compono.Generators.Tests/TestDoubleOverloadMatchingExecutionTests.cs` — new (Phase 2) — coexistence/precedence, sibling-independence, filtered-verification, and sequence-interaction tests (see Phase 2 Tasks).
- `test/Compono.TestDoubles.AotSmokeTest/Program.cs` — extended/updated in both phases.
- `test/Compono.Generators.Tests/Snapshots/*.verified.cs` — ~77 files touched by Phase 1 alone (mechanical, template-wide change; zero further diffs expected from the `SequenceOutcome<T>` correction, since it's invisible to generated code). Phase 2 touches every matching-eligible-overload fixture's snapshot (a real, not byte-identical, change — see Phase 2's corrected acceptance criterion) and leaves every non-matching-eligible overloaded member and every non-overloaded member untouched.
- `docs/adr/0044-...md`, `docs/adr/0054-...md`, `skills/compono/references/testdoubles.md` — documentation/ADR touches per phase (both ADRs already corrected in place for the API-shape questions; this round's dispatch-architecture correction lives in this plan only, per `tasks/design.md`'s ADR-records-what/why-plan-records-how split — no further ADR edit needed for it).

## Notes

(Empty at plan-writing time — this section fills in as implementation reveals divergence from what's scoped here, per this repo's own plan-maintenance convention.)
