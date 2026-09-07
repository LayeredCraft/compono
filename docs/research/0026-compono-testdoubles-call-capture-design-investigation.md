# [RESEARCH-0026] Compono.TestDoubles Call-Capture and ClearCalls Design Investigation

**Status:** Done (research only; no ADR yet)

**Feeds:** a future ADR on `Compono.TestDoubles` retrospective call-capture/inspection and `ClearCalls()`

**Related:** [RESEARCH-0025](0025-compono-testdoubles-1.1-research.md) (this package's broader 1.1 admission research — this document is the deep-dive on its strongest candidate), [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md) (`ReturnConfig<T>`/`CallVerifier`/`Verify()` bridge), [ADR-0048](../adr/0048-testdoubles-argument-matching-and-call-verification.md) (argument matching + argument-filtered verification — the ADR whose implementation turns out to already contain most of the infrastructure this investigation needs)

## 0. Headline finding — read this first

**The generated per-member call log this investigation was asked to design already exists.** ADR-0048 (accepted 2026-08-21/22) added, as a private implementation detail of argument-filtered `Verify()`, a per-eligible-member, lock-guarded, strongly-typed `List<(T1, T2, ...)>` that records every call's real argument values for the member's lifetime. Today this list is used for exactly one purpose — `Verify().Member(matchers...)` locks it, scans it, counts entries whose arguments satisfy the supplied `Match<T>` predicates, and discards the scan result into a plain `int` handed to `CallVerifier`. The list itself, and every argument value in it, is thrown away after every `Verify()` call.

This reframes the investigation from "should we build call capture" to "should we expose and manage the lifetime of infrastructure we already pay for on every eligible member." That change of framing drives most of the recommendations below: this is much closer to Option D (generated received-call records) than a green-field design, the eligibility rule is very likely to be reused rather than reinvented, and the marginal cost of *exposing* the log is near zero — the cost already exists.

## 1. Consumer scenarios

Four scenarios, per the brief:

1. **Argument capture** — inspect an argument from a call already made, after exercising the SUT. Already representable in principle by iterating the existing call log; not exposed publicly today.
2. **Multiple-call inspection** — inspect arguments across several invocations, e.g. distinguishing call 1's argument from call 3's. Requires real history, not a single capture slot; the existing call log already retains every call in order.
3. **Verify-then-inspect** — assert "called exactly once" and then read that one call's arguments without re-stating the predicate. Not supported by any existing mechanism: `CallVerifier` (core `Compono`) is constructed from a bare `int` and has no reference back to the call log (a deliberate ADR-0048 decision, see §3.5 below), so today a consumer cannot get from "verification passed" to "here are the arguments" in one step.
4. **`ClearCalls()` across phases of one test** — clear observation history between an exercise phase and a later exercise phase on the same shared double, without losing configured `Returns`/`Throws`/`ReturnsSequence`. No such method exists today. `ReturnConfig<T>.ClearConfiguredResponse()` exists but does the *opposite* of what's wanted here (clears configuration, leaves `CallCount` untouched) — see §1.1.

### 1.1 `ClearConfiguredResponse()` already sets the naming/semantics precedent, in the wrong direction

`src/Compono/ReturnConfig.cs:71-80`:

```csharp
public void ClearConfiguredResponse()
{
    HasValue = false;
    Value = default;
    Exception = null;
    Sequence = null;
    SequenceOrdinal = 0;
}
```

This method (added for ADR-0053's `ReturnsCallback`, per its doc comment) clears *configuration* and explicitly resets `SequenceOrdinal` to 0 — but it is invoked only when a builder is *replacing* one response mechanism with another (e.g. `Returns` → `ReturnsCallback`), not as a consumer-facing test-lifecycle primitive. It does not touch `CallCount` at all. A future `ClearCalls()` is the mirror image of this: it should clear observation state and leave configuration (including `SequenceOrdinal`) untouched. The existing method is useful evidence that "clear half the state, leave the other half" is already an accepted pattern in this codebase, not a new kind of complexity — but its *direction* (clear config, keep count) is the opposite of what `ClearCalls()` needs (clear count/log, keep config), so it cannot be reused or generalized as-is. Naming it precisely (`ClearCalls`, not `Reset`) avoids the confusion of two same-named methods doing opposite things.

## 2. Existing architecture relevant to call observation

### 2.1 Two independent recording mechanisms already exist, for different member shapes

| Member shape | What's recorded today | Where |
|---|---|---|
| Argument-independent (generic-parameter-referencing, overloaded, ref-like-parameter, `Equals(T)`, or simply not yet touched by ADR-0048) | `ReturnConfig<T>.CallCount` only (`Interlocked.Increment`), no arguments | `src/Compono/ReturnConfig.cs:68`, e.g. `TestDouble.scriban:364,377` |
| ADR-0048-eligible (see §5 for the 5 conditions) | `CallCount`-equivalent (the log's `.Count`) **and** every call's real, strongly-typed argument tuple, in a `List<(...)>` guarded by a dedicated `lock` object, one pair of fields per eligible member | `TestDouble.scriban:127-129`, `:203-211`, ADR-0048 generated-shape example |

Concretely, per eligible member the generator already emits (ADR-0048's own generated-shape example, `docs/adr/0048-...md`):

```csharp
internal readonly global::System.Collections.Generic.List<(string CognitoSub, string GameName, CancellationToken Ct)> __getPlayerByCognitoSub_calls = [];
private readonly object __getPlayerByCognitoSub_lock = new();
```

and dispatch:

```csharp
lock (__getPlayerByCognitoSub_lock) { __getPlayerByCognitoSub_calls.Add((cognitoSub, gameName, ct)); }
```

and `Verify()` (`TestDouble.scriban:611-618`):

```csharp
lock (self.Instance.__getPlayerByCognitoSub_lock)
{
    foreach (var call in self.Instance.__getPlayerByCognitoSub_calls)
        if (cognitoSub.Matches(call.CognitoSub) && gameName.Matches(call.GameName) && ct.Matches(call.Ct))
            count++;
}
return new(count, "GetPlayerByCognitoSubAsync");
```

**The list is never cleared, never bounded, and never exposed.** Its only consumer is this filtered `Verify()` count. `count`-only `Verify()` (no arguments) also exists on eligible members and, per `TestDouble.scriban:625-630`, reads `.Count` off the *same* list under the *same* lock rather than a separate counter — meaning the plain-`Verify()`-with-no-arguments path on an eligible member is **already** paying the list's storage cost even though it only ever wants a count. This is an existing, accepted cost (ADR-0048's "Negative Consequences": "Materially larger generated-code volume per eligible member... versus v1/v2's single scalar field — accepted").

### 2.2 Concurrency model already established (ADR-0048, "Allocation and concurrency model")

- Append and filtered-count-read use the *same* lock — "a filtered count is a snapshot-and-count under that lock, not a separate unlocked read, avoiding a collection-enumeration race for negligible cost at unit-test call volumes."
- This was itself a bug fix: `TestDouble.scriban:203-208` documents (Codex review, PR #108 round 5) that an earlier split-lock shape — short lock around `Add()`, then an unlocked scan — let a concurrent `Configure()`/dispatch mutate the list's backing array mid-scan. The fix folds the append and the scan into the same lock acquisition.
- `Configure()` itself stays unsynchronized against concurrent invocation, unchanged from v1/v2 — matcher fields are plain fields, not volatile/interlocked. "Concurrent verification while calls are still in flight is unsupported/undefined, matching today's `CallCount` semantics."
- Sequence ordinal claiming (`ReturnConfig<T>.NextSequenceOutcome`, ADR-0054) uses `Interlocked.Increment` on a separate `int`, independent of the call log's lock — two entirely separate concurrency primitives already coexist on one generated double member with no interaction between them.

This is directly reusable: exposing the log for read access needs no new concurrency primitive, only a decision about what "reading" means to a consumer (see §7).

### 2.3 `CallVerifier` deliberately has no path back to the log (ADR-0048's "Matching API shape")

ADR-0048 explicitly considered and rejected giving `CallVerifier` (or an intermediate wrapper) continued access to the call log after construction:

> "Rejected: requires `CallVerifier` (or an intermediate wrapper) to retain access to the call log after construction, reopening exactly the architectural question this ADR's review caught — `CallVerifier` cannot perform matching after construction because it no longer has access to the call log."

This is why scenario 3 (verify-then-inspect) has no answer today: it was a known, named architectural boundary, not an oversight. Any design in this investigation that wants verify-then-inspect in one fluent chain must either (a) change what `Verify()` returns for eligible members specifically, while leaving `CallVerifier` itself untouched for the argument-independent case, or (b) treat inspection as a fully separate accessor consulted before or independently of `Verify()` (§7 recommends (b)).

## 3. Design alternatives

### 3.A Always-recorded invocation history

This is what already exists for ADR-0048-eligible members (§2.1) — the "always record" decision was already made and shipped. The open question is not whether to record (that ship sailed for eligible members) but whether to (a) expose what's already recorded, (b) bound it, and (c) extend eligibility to more member shapes to record for them too.

**Cost, measured** (§6): recording itself (a lock + tuple write into a list) is cheap per call, but *unbounded* growth is not — list doubling/reallocation dominates at scale. A capped/bounded structure removes nearly all of the cost. See §6 for numbers.

**Retained references, not deep copies:** the list stores the argument *values* as passed — for a reference type, that's the same reference the SUT passed, not a snapshot. See §5.4 for the consequences (a consumer mutating a captured object after the call sees the mutation reflected in the "captured" value too, exactly like NSubstitute's `Received()`/`Arg.Do` behave, and exactly what "capture" can mean without an unbounded, impossible deep-copy obligation).

**GC pressure when the consumer never inspects history:** for eligible members, this cost is already paid unconditionally today (every call appends, regardless of whether any test ever calls `Verify()` on that member). Widening eligibility widens this "pay whether or not you look" cost to more member shapes. This is the central tension the recommendation in §8 resolves.

### 3.B Opt-in capture/history

Investigated and rejected as the primary mechanism, for reasons specific to this codebase's existing design, not in the abstract:

- **It cannot be retroactive.** Per the brief's own concern: "prevents retrospective inspection because capture must be requested before exercising the SUT." Compono's whole `Configure()`/exercise/`Verify()` flow already assumes verification is retrospective — a consumer calls `Verify()` *after* exercising the SUT with no advance declaration that they intend to verify. An opt-in capture toggle would need to run *before* the SUT executes, breaking the one convention every existing verification path (`Once`/`Never`/`Exactly`/ADR-0048's filtered `Verify()`) already relies on. This is a bigger ergonomic regression than the allocation cost it would save.
- **It adds branching cost that approaches the always-on cost anyway.** A per-call `if (captureEnabled)` check plus the field to hold that flag is real generated-code and real branch-prediction cost sitting directly in the hot dispatch path of *every* call, not just captured ones — for the eligible-member case, this doesn't beat "always record," it just adds a branch in front of a cost that (per §3.A) is already dominated by *unboundedness*, not the write itself.
- **It duplicates a decision ADR-0048 already made.** ADR-0048 chose "single slot, one configured response" over "ordered, append-only chain" for *configuration*, reasoning that unevidenced generality is a real cost. The same reasoning applies here: there is no consumer scenario in this brief that needs to declare capture in advance rather than always having it available retrospectively for eligible members.

Opt-in is not recommended anywhere in this design.

### 3.C Callback/observer-based capture

`ReturnsCallback` already exists (ADR-0053) and already solves single-value, this-invocation capture cleanly:

```csharp
repository.Configure().Save(Match.Any<Item>(), Match.Any<CancellationToken>()).ReturnsCallback((item, ct) => captured = item);
```

Per `TestDouble.scriban:15-42`, the callback builder's `ReturnsCallback` clears any configured `Returns`/`Throws`/`ReturnsSequence` on that slot (`_config.ClearConfiguredResponse()`) and stores the callback in a separate field — it is mutually exclusive with a configured response, which the callback itself must now supply by returning a value (for non-void members).

- **Void members:** already supported — the void-member dispatch template (`TestDouble.scriban` void-member block) calls the callback the same way as the value-returning path; a `void`-returning delegate works today. No gap here.
- **Solves single-value capture well** — this is Rocks's own chosen design too (see §9: Rocks's `Callback()` is documented specifically as how you "capture method argument values," with no separate call-history API). It is the industry-precedented answer to "I want this one argument."
- **Users can trivially build history themselves** on top of it: `.ReturnsCallback((item, ct) => history.Add(item))` is a two-line pattern any consumer can write today with zero new API. This is real prior art for "don't build what a one-line workaround already covers cleanly" — but it is *not* equivalent to the log-based approach for scenario 3 (verify-then-inspect in one step) because it requires the consumer to have set up the callback *before* the exercise phase, which is exactly the opt-in framing rejected in §3.B, just opted into voluntarily by a consumer who wants it rather than mandated by the API.
- **Interaction with `Returns`/`Throws`/`ReturnsSequence`:** mutually exclusive today (`ReturnsCallback` clears the others; configuring one of the others after `ReturnsCallback` would need to clear the callback field too — worth double-checking as an existing-code correctness item, out of scope for this investigation but flagged as a candidate follow-up: does `Returns()`/`Throws()`/`ReturnsSequence()` clear a previously configured callback field? A grep of `ReturnConfigBuilder.cs` (§ shape above) shows `Returns`/`Throws`/`ReturnsSequence` clear each other's state but say nothing about the callback field, which lives outside `ReturnConfig<T>` entirely on the double class. This may be a pre-existing gap unrelated to this investigation.)
- **Ordering:** no complication — a callback executes once, synchronously, in the dispatch path, no different from any other member body statement.
- **Does not by itself provide scenario 2 (multi-call, cross-invocation inspection) or scenario 4 (`ClearCalls`)** — a consumer-built `List<T>` via callback has no first-class relationship to `Verify()` and no `ClearCalls()` equivalent; the consumer owns and must manage that list's lifetime entirely themselves.

**Conclusion:** `ReturnsCallback` is already the right, complete answer for scenario 1 (single capture) and needs no new API. It's real, working prior art that a *pure* capture-and-inspect API doesn't need to duplicate. It does **not** cover scenarios 2–4, which is exactly the residual gap this investigation should focus a new capability on.

### 3.D Generated received-call records

This is what already exists as private infrastructure (§2.1). The remaining design work is:

1. What to expose (a raw tuple? A named record? An enumerable view?).
2. Whether exposing it changes storage shape (does the internal representation need to become richer, e.g. adding metadata like a monotonic sequence number or timestamp, or is the existing bare argument tuple sufficient?).
3. Where it's exposed from (`Verify()`? A new accessor? See §7).
4. Snapshot-vs-live semantics for whatever is returned.

On (2): the existing tuple (`(string CognitoSub, string GameName, CancellationToken Ct)` in the ADR-0048 example) already gives strongly-typed, named-field access — `call.CognitoSub` reads naturally. No metadata (ordinal, timestamp) is recorded today. Scenario 2 ("call 1 → A, call 2 → B, call 3 → C") is satisfiable by the list's own insertion order under the lock — the brief's concurrency section asks what ordering under concurrent calls should mean; see §6.2. A generated, per-member named record type (rather than a bare `ValueTuple`) would give better call-site ergonomics (`call.CognitoSub` reads the same either way, but a `record` gives a nameable type for a consumer to use in a signature, e.g. a local helper method taking `IReadOnlyList<GetPlayerByCognitoSubCall>`) at the cost of one more generated type per eligible member. This is a real but small ergonomics-vs.-generated-surface-area tradeoff, not a correctness question — recommended in §8 as a `record` rather than a raw tuple, specifically because a raw `ValueTuple` return type is unnamed at the consumer's use site beyond field names inferred from the member signature, and this repo already prefers named, generated types over raw tuples elsewhere (`SequenceOutcome<T>`, `ReturnConfig<T>` itself) for exactly this reason.

On semantics of "capture" per the brief's explicit list:

| Argument kind | What "capture" means here |
|---|---|
| Reference types (classes) | The same reference passed at call time, stored in the list slot. No copy. A consumer inspecting it later sees the object's *current* state, not its state at call time, if the SUT (or anything else) mutates it after the call returns. |
| Mutable objects | Same as above — explicitly a reference, not a snapshot. This must be documented plainly (see §12) since it is the one genuinely surprising semantic a consumer could trip on. |
| Arrays / collections | Same reference-retention rule — an array passed by reference is stored as that same array reference; if the caller mutates the array's contents after the call, the "captured" value reflects the mutation. No `.ToArray()`/`.ToList()` defensive copy is taken automatically. |
| `CancellationToken` | A `struct`, copied by value into the tuple/record slot naturally (already true today, per the generated example storing `Ct` directly) — no special handling needed, this already works. |
| Structs generally | Copied by value automatically (C# value semantics) — already correct and free, needs no design decision. |
| Nullable values | `Nullable<T>`/reference-nullable arguments store exactly whatever was passed, including `null` — no special-casing needed; `Match<T>.Matches` already handles this today for the filtering case (`EqualityComparer<T>.Default.Equals` handles `null` correctly), so the storage side needs no new null-handling logic either. |

**Deep-copying arbitrary arguments is confirmed undesirable/impossible**, matching the brief's own expectation: Compono has no generic serialization/cloning mechanism, deliberately (no reflection-based fallback per ADR-0001), and forcing one in for this feature would be a large, unjustified addition for a narrow benefit. The reference-retention semantics NSubstitute and Rocks both accept implicitly (their `Arg.Do`/`Callback` callbacks receive the live argument, not a copy) is the same trade this repo should make, and it should be stated as plainly as NSubstitute's own docs do for `Received()`'s argument matching (which has the identical "the substitute stores what was passed, not a clone of it" property, just never surfaced as a call-out because `Received()` doesn't hand the argument back to the consumer the way retrospective capture would).

## 4. Eligibility and member-shape analysis

### 4.1 The existing ADR-0048 five-condition rule (ADR-0048 Amendment 1, `TestDoubleAnalyzer.cs`'s `isEligibleForMatching`)

1. Not part of an overload set.
2. No real parameter references the member's own open method-type-parameter.
3. No real parameter is a ref-like type (`Span<T>`, any other `ref struct`) — can't be a generic type argument (`Match<Span<int>>?`, or a tuple element) — `CS0306`.
4. No derived-auxiliary-name collision (implementation-level naming concern, not conceptually about capture).
5. `Equals` with exactly one parameter excluded (arity collision with `object.Equals(object)`).

### 4.2 Should retrospective capture reuse this rule exactly, or diverge?

The brief explicitly asks not to blindly reuse ADR-0048's restrictions. Walking each:

- **Overloaded members (condition 1):** ADR-0048's reason for excluding these from *argument matching* was a **compiler-proven `Match<T>`-wrapping ambiguity** (the `CS0121` spike) — that reasoning is specific to giving *`Configure()`/`Verify()` parameters* the `Match<T>` type. Retrospective capture does **not** need to change any parameter's type — it only needs a place to *store* what was already passed through the existing, unmodified overload-discriminator signature. **This restriction does not mechanically apply to capture and is worth revisiting** — a per-overload call log (keyed the same way `ReturnConfig<T>` is already keyed per-overload today, per ADR-0044 Requirement 1) is plausible without touching the `Match<T>`-ambiguity problem at all, since capture storage never needs a `Match<T>`-typed parameter. This is a genuine expansion opportunity a future ADR should evaluate, not dismiss by inheritance from ADR-0048.
- **Generic methods whose parameters reference the method's own type parameter (condition 2):** this restriction is **not** about `Match<T>` ambiguity — it's that "a per-member call log cannot hold `TState` — it exists only per closed invocation, not per member declaration" (ADR-0048's own words). This limitation is structural, not a `Match<T>`-specific artifact, and **does** mechanically apply to capture for the same reason: there is nowhere to declare `List<(TState, ...)>` as a member field when `TState` isn't known until each call site. Capture cannot do better than argument-filtered verification here without inventing erased/boxed storage, which ADR-0048 already rejected for the same member shapes on AOT-safety/complexity grounds ("no real evidence justifies the added complexity"). **Reuse this exclusion as-is.**
- **Ref-like parameters (condition 3):** this is a hard CLR/generics constraint (`Span<T>` cannot be a generic type argument), not an ADR-0048 policy choice — it applies identically to any storage mechanism, including a hand-written non-generic record shape, unless the record avoids using `T` as a generic parameter and instead has a concretely-typed field per parameter (which the generator already does — the tuple/record element types are the parameters' real, closed types, not a generic `T`). A `Span<T>` argument specifically still cannot be stored in *any* field for *any* purpose past the call's own stack frame (`Span<T>` cannot escape as a field of a heap-allocated object at all, full stop) — this exclusion is unconditionally structural and must be kept for capture too, independent of ADR-0048.
- **`Equals(T)` arity collision (condition 5):** purely an extension-method-resolution artifact of the *generated public API shape* colliding with `object.Equals(object)` — applies identically to a capture-accessor extension method for the same reason (any public extension method with matching arity has the identical `object.Equals` shadowing problem). **Reuse as-is** for any accessor shaped as an extension method; moot if capture is instead exposed as an instance member or through the existing `Verify()`/`Configure()` wrapper types rather than a raw extension on the double.

**Net eligibility conclusion:** capture eligibility should be **derived independently**, not inherited wholesale. It converges with ADR-0048's rule on 3 of 5 conditions (generic-method-type-parameter exclusion, ref-like exclusion, `Equals` arity exclusion) for genuinely structural reasons, but the overload exclusion is an ADR-0048-specific artifact that a future ADR should re-evaluate on its own evidence rather than copy forward by default. This matters concretely: **if capture reuses the ADR-0048 log verbatim (piggybacking on the same list already generated for eligible members), overloaded members get no capture, same as today** — that's the pragматic near-term answer (§8's recommended scope), and it's a real, honest scope limit to document, not a silent gap.

### 4.3 Properties, property setters, inherited members, default interface members, async methods

- **Properties (get-only, get/set, get/init):** per `TestDouble.scriban`'s property block, `get`/`set`/`init` accessors already call `RecordCall()` on the same field-per-member pattern as methods. A property getter has zero parameters (nothing to capture — `CallCount` only, already exists). A property **setter/init** has exactly one parameter (the assigned value) — this is structurally identical to a one-parameter method and **is** capturable using the same mechanism, with no new design question. Not evidenced as a real consumer need in this investigation's scope, but mechanically free to include if ever prioritized.
- **Inherited members:** no special interaction found — the generator resolves the full interface member set (including inherited interface members) uniformly today; nothing about ADR-0048's log or eligibility rule is inheritance-specific (member declarations are already flattened before analysis).
- **Default interface members (DIMs):** the DIM-fallback path (`member.is_dim_fallback_target`) forwards to a helper object rather than dispatching to a slot at all when the member is unconfigured — but ADR-0048's argument matching/logging applies to a DIM the same as any other member *once it's eligible and has a slot*; the DIM-fallback path is orthogonal (it only fires for the *unconfigured* case). No new exclusion needed.
- **Async methods:** `Task`/`Task<T>`/`ValueTask`/`ValueTask<T>`-returning members are already handled identically to any other return type by `ReturnConfig<T>` (`T` is simply `Task<Player?>` in the ADR-0048 example) — recording happens synchronously at the point of the (synchronous) dispatch call, before any `await` the caller applies. No async-specific capture design is needed; the call is recorded the instant the double's method body runs, which is exactly when arguments are available, regardless of what the caller does with the returned awaitable afterward.

### 4.4 Discoverable, natural API shape — evaluating the brief's candidates

The brief asks whether inspection belongs under `Verify()` at all, and floats several conceptual shapes. Evaluated:

- **`repository.Verify().Save(...).Calls`** — folds inspection into the same wrapper `Verify()` already returns. Problem: per §2.3, `CallVerifier` (returned by `Verify().Member(...)`) is deliberately *not* the thing with log access — the *per-member extension* has log access, and it currently returns `CallVerifier` directly, by design, specifically to avoid `CallVerifier` needing continued log access. Retrofitting a `.Calls` property onto `CallVerifier` reopens exactly the question ADR-0048 closed. Not recommended as stated, though seeding `CallVerifier` with an optional call-log reference is not impossible — it's a real compatibility question flagged in §12.
- **`repository.ReceivedCalls().Save`** — a *third* generated bridge type, parallel to `Configure()`/`Verify()`, dedicated to inspection. Clean separation of concerns (configuration vs. assertion vs. inspection are three different consumer intents, and NSubstitute's own `ReceivedCalls()` name is a real, evidenced precedent for exactly this separation — see §9). This is the shape recommended in §7/§8.
- **`repository.Calls().Save`** — same shape as `ReceivedCalls()`, shorter name. A naming choice, not an architectural one; `ReceivedCalls()` is recommended for its direct NSubstitute-migration-ergonomics precedent (a real evidenced concern per this repo's own migration-boundary framing — the Compono skill's own docs describe "matching is not capture" as a major NSubstitute migration boundary, i.e. NSubstitute consumers already know to reach for a `ReceivedCalls()`-shaped concept).

**Recommendation: inspection does not belong under `Verify()`.** `Verify()` communicates assertion semantics — a pass/throw contract — and folding data retrieval into it blurs that contract exactly as the brief anticipated. A **third bridge**, `ReceivedCalls()`, mirroring `Configure()`/`Verify()`'s existing two-bridge pattern, is the cleanest fit: it's additive (new extension methods only), doesn't touch `CallVerifier`'s existing shape or ADR-0048's existing "no log access after `Verify()` construction" boundary, and gives capture its own home consistent with the "one small, concrete... extension, no dedicated verification API" precedent ADR-0044 itself used to justify `Verify()`'s own existence as a bridge distinct from `Configure()`.

## 5. `ClearCalls()` semantics

Answering the brief's precise checklist, working from the existing storage split (`ReturnConfig<T>`'s configuration fields vs. the ADR-0048 call log, which are **already separate generated fields today** — this split is not something this investigation has to invent, it already exists):

| State | Cleared by `ClearCalls()`? | Why |
|---|---|---|
| Call counts (`CallCount`, or the eligible-member log's `.Count`) | **Yes** | This *is* the observation/verification history the brief's own stated principle targets. |
| Captured argument history (the ADR-0048 log's contents) | **Yes** | Same list backs both the count and the arguments — clearing one without the other isn't representable given the current single-list storage (§2.1), and conceptually both are "what happened," not "what's configured." |
9| Configured `Returns` | **No** | Configuration, not observation. Matches NSubstitute's `ClearReceivedCalls()` precedent exactly (§9): "will not clear any results set up for the substitute." |
| Configured `Throws` | **No** | Same reasoning as `Returns`. |
| `ReturnsCallback` | **No** | It's configured behavior, structurally identical to `Returns`/`Throws` in `ReturnConfig<T>`'s model (mutually exclusive alternative response mechanism) — clearing it would silently change future dispatch behavior, which `ClearCalls()` must not do per the brief's own stated boundary against becoming `ResetDouble()`. |
| `ReturnsSequence` configuration (the `Sequence` array itself) | **No** | It's configured behavior — the array of outcomes to hand out. |
| Current sequence ordinal (`SequenceOrdinal`) | **No — continues at its current position.** | This is the brief's own stated hypothesis, and the evidence supports it directly: `ReturnConfig<T>.NextSequenceOutcome()`'s own doc comment describes the ordinal as tracking "the first call... index 0, the second... index 1" — i.e., it's **runtime progress through configured behavior**, not a record of what was observed for assertion purposes. It lives in `ReturnConfig<T>` (the configuration struct), physically adjacent to `Sequence` itself, not in the ADR-0048 call log. If a consumer configures a 3-entry sequence, calls the member twice (consuming entries 0 and 1), then calls `ClearCalls()`, the next call should return entry 2 — clearing observation history doesn't rewind configured behavior any more than it would silently reset a `Returns`-configured constant value's future behavior. Resetting it would make `ClearCalls()` observably change *future* dispatch outcomes, not just clear *past* observation — exactly the `ResetDouble()` scope-creep the brief warns against. |
| Argument-specific configuration entries (the per-parameter `Match<T>?` fields) | **No** | Configuration, unchanged by definition — these are what a *future* call is compared against, set by `Configure()`, structurally parallel to `Returns`/`Throws`. |
| Generic closed-instantiation configuration entries (ADR-0044 Requirement 2's dictionary-free per-instantiation slots) | **No** | Same reasoning — configuration, not observation, regardless of which generic-instantiation bucket it lives in. |
| Property backing state (the value a property getter would currently return) | **No** | This is `ConfiguredValue`/`HasConfiguredValue` — the same `ReturnConfig<T>` configuration fields properties already share with methods (§2.1's table shows properties use the identical field shape). No separate design question here; it falls out of "don't touch `ReturnConfig<T>`'s configuration fields" already stated above. |

**Recommended contract, stated plainly:** *`ClearCalls()` clears everything a `Verify()` call on this double could observe (counts and, where present, captured argument history) and nothing a `Configure()` call set up (responses, exceptions, sequences, sequence position, argument matchers). It changes what the double remembers having seen, not what it will do next.*

This is **exactly** NSubstitute's own `ClearOptions.ReceivedCalls` flag semantics (§9) — "clear all the received calls," independently toggleable from `ReturnValues`/`CallActions` — which is strong, direct external validation that this observation/configuration split is the conventional, expected contract for this kind of primitive, not a Compono-specific invention.

### 5.1 Should `ClearCalls()` release retained references?

Yes, and this falls out for free: since the call log is a `List<T>` (or the record-based equivalent recommended in §3.D/§8), `list.Clear()` (or reassigning to a fresh empty list) drops the list's references to previously-captured arguments, making them eligible for collection the moment nothing else in the test holds them — no special handling needed beyond calling `.Clear()` (or equivalent) under the existing lock.

## 6. Performance and allocation findings

### 6.1 Baseline vs. always-record vs. bounded — measured

A throwaway, read-only spike (not committed, run under `/tmp`, deleted after use — no `src/`/`test/` changes) compared three shapes at 5,000,000 iterations, Release config, .NET 9, workstation GC:

| Scenario | Elapsed | Allocated | Bytes/call |
|---|---:|---:|---:|
| Baseline — `Interlocked.Increment` only (today's argument-independent `RecordCall()`) | 15.8 ms | 40 B (fixed, not scaled — the harness's own overhead) | ~0.00 |
| Always-record, **unbounded** `List<(string, int, CancellationToken)>` growth under a `lock` (mirrors the current ADR-0048 shape exactly, at unrealistic 5M-call scale) | 328.4 ms | 402,653,616 B | 80.53 |
| Always-record, **bounded** ring buffer (pre-sized array, `lock` + overwrite, no growth) | 24.1 ms | 0 B | ~0.00 |

**Interpretation:** the expensive part of "always record" is not the per-call write — it's *unbounded growth* (`List<T>`'s doubling reallocation and copy, dominant at scale). A capped/bounded structure removes essentially all of the measured overhead relative to the existing zero-alloc baseline. At realistic unit-test call volumes (a handful to a few hundred calls per test method, not 5 million), even the unbounded shape's absolute cost is trivially small — this spike deliberately used an unrealistic iteration count specifically to make the *asymptotic* behavior (growth-driven, not per-call-driven) visible; it is not a claim that any real test suite will notice a difference at either end of this table.

**Consequence for design:** ADR-0048's existing choice ("grows for the double's lifetime (one test method), never trimmed — matches every real site's scale") is fine as-is for real test-method call volumes, and this investigation found no performance reason to change it. Bounding is a real, cheap option available if a future ADR wants to guard against a pathological case (e.g. a double called in a tight loop by a bug in the SUT), but the measured evidence doesn't show it as *necessary* for correctness or acceptable performance at realistic scale — it's a defensive option, not a requirement. **Recommendation: do not bound by default.** Bounding creates a real, surprising semantic (which call gets silently dropped when the buffer wraps?) for a cost that isn't evidenced as a real problem, which is exactly the kind of unevidenced complexity ADR-0044/ADR-0048 both explicitly avoid elsewhere.

### 6.2 Opt-in and callback-only comparative cost

Not separately benchmarked — reasoned analytically instead, since the mechanisms are architecturally rejected/already-existing rather than open design questions:

- **Opt-in (§3.B):** would add exactly one branch (`if (captureEnabled)`) in front of the same write the always-on path already does — strictly *more* generated-code cost than always-on for eligible members, since eligible members already always-record unconditionally today; opt-in would only reduce cost for member shapes that *don't* currently record at all, at the cost of the ergonomic regression in §3.B. Not pursued further.
- **Callback-only (§3.C):** zero additional generated storage — `ReturnsCallback` already exists and costs exactly what a delegate invocation costs, which is less than a list append. But it only covers scenario 1, not 2–4 (§3.C's conclusion). Its cost profile is not a reason to prefer or reject it; its *scope* is.

### 6.3 Performance verdict

**Acceptable.** For the member shapes already eligible under ADR-0048 (which already always-record), exposing the existing log adds **zero additional runtime cost** — the recording already happens; only a read-side accessor and a `Clear()` are new. For any expansion of eligibility (e.g. to overloaded members, per §4.2's open question), the added cost is the same shape already accepted for ADR-0048-eligible members today, and the same "accepted, same real generated-code-volume-is-an-expected-cost precedent" reasoning ADR-0048 itself used applies without needing new justification.

## 7. Recommended conceptual API

```csharp
// Existing, unchanged:
repository.Configure().GetPlayerByCognitoSubAsync(Match.Any<string>(), Match.Any<string>(), Match.Any<CancellationToken>()).Returns(player);
repository.Verify().GetPlayerByCognitoSubAsync(Match.Is<string>(s => s == cognitoSub), Match.Any<string>(), Match.Any<CancellationToken>()).Once();

// New — a third bridge, parallel to Configure()/Verify(), for eligible members only:
var calls = repository.ReceivedCalls().GetPlayerByCognitoSubAsync();   // IReadOnlyList<GetPlayerByCognitoSubAsyncCall>, snapshot at read time
Assert.Equal(expectedCognitoSub, calls[0].CognitoSub);

// New — ClearCalls(), likely hung off the Verify() bridge (it's the "reset what Verify() would see" operation)
// or a fourth minimal bridge — exact receiver type is an ADR-level decision, not resolved here:
repository.Verify().ClearCalls();          // or: repository.ClearCalls();          — see open question in §14
```

Key properties of this shape:

- **`ReceivedCalls()` is scoped to ADR-0048-eligible members only** (§4.2), same restriction surface as today's argument-filtered `Verify()`, with the overload-exclusion question flagged as open (§4.2) rather than settled.
- **Returns a generated, per-member named type** (a `record` with named properties matching the parameter names, e.g. `GetPlayerByCognitoSubAsyncCall(string CognitoSub, string GameName, CancellationToken Ct)`) rather than a raw `ValueTuple`, for the naming/ergonomics reasons in §3.D.
- **Snapshot semantics on read:** the accessor takes the same lock ADR-0048's filtered `Verify()` already takes, copies the current list contents into an array/list returned to the caller, and releases the lock — the same "snapshot-and-count under the lock" pattern ADR-0048 already established for filtered counting, just returning entries instead of a count. This avoids handing a consumer a live, lock-free-iterated reference to internal generated state (which could otherwise race against a concurrent call still appending).
- **Does not touch `CallVerifier`.** Scenario 3 (verify-then-inspect) is answered by calling `ReceivedCalls()` after `Verify()...Once()` passes, rather than by threading data through `CallVerifier` itself — two separate, composable calls rather than one fused one. This keeps ADR-0048's "`CallVerifier` never needs access to the call log at all" invariant fully intact (a true zero-risk-to-existing-code option), at the cost of the consumer writing two lines instead of one chained expression. Given `CallVerifier` is a **public, core-`Compono`, cross-package-reused type** (per this session's Investigation 2, also in flight), *not* touching its shape at all for this investigation is the conservative, clearly-correct choice, and is recommended specifically because it does not entangle this investigation's outcome with Investigation 2's.

## 8. Recommended scope for 1.1 (if admitted)

1. Expose `ReceivedCalls()` for exactly the members already eligible under ADR-0048's five-condition rule (unchanged scope) — zero new runtime cost, infrastructure already exists and is already paid for.
2. Add `ClearCalls()` with the semantics in §5 (clears counts/log, preserves all configuration including sequence ordinal).
3. **Do not** expand eligibility to overloaded members in this same pass, even though §4.2 found no `Match<T>`-ambiguity reason blocking it — that's a real, separate design question (how is a per-overload call log keyed, does it reuse ADR-0044 Requirement 1's per-overload `ReturnConfig<T>` field pattern) that deserves its own compiler-spike-backed pass, matching this repo's own "do not assume, run the compiler" standard used throughout ADR-0048. Flag it explicitly as a follow-up, not a rejection.
4. **Do not** bound the call log (§6.1's finding: unbounded is fine at real scale; bounding adds a surprising semantic for an unevidenced problem).
5. **Do not** change `CallVerifier`'s shape as part of this work (§7 — keep this investigation decoupled from Investigation 2).

## 9. External comparison

| Library | Received-call inspection | Argument capture | Clear received calls | Retained history |
|---|---|---|---|---|
| **NSubstitute** | `Received(n)`/`DidNotReceive()` (assertion only, no data returned) | `Arg.Do<T>(action)` — callback-based, same shape as Compono's own existing `ReturnsCallback` | **`ClearReceivedCalls()`** — "will not clear any results set up for the substitute," i.e. observation-only, config preserved. `ClearOptions` enum makes this explicit: `ReceivedCalls` / `ReturnValues` / `CallActions` are independently toggleable flags, `All` clears everything — direct precedent for this investigation's exact configuration/observation split (§5). | Implementation detail, not documented as a first-class retrospective-inspection API (`Received()` is assertion-only; no documented `ReceivedCalls()`-as-data-accessor found in current docs) |
| **Rocks** (source-generated, AOT-friendly, the closest architectural peer) | `Verify()` — assertion only (strict-mock style, fails with `VerificationException`) plus `ExpectedCallCount()` for count-based assertion | **`Callback()`** — explicitly documented as the mechanism to "capture method argument values" — i.e., Rocks made the *same* choice this investigation recommends keeping for single-value capture (§3.C): callback-based, not retained-history-based, as its primary public capture story | Not found documented | Not found documented as a public retrospective-history API |

**Reading these findings:** neither of the two most relevant peers (one API-mature and widely used, one source-generated/AOT-focused like Compono itself) treats "iterate every captured call as strongly-typed data" as a prominent, separately-branded public feature — NSubstitute's public capture story is callback-based (`Arg.Do`), and so is Rocks's (`Callback()`). This is a genuine signal against over-building: **the callback mechanism (§3.C, already shipped in Compono as `ReturnsCallback`) is doing exactly what the two most relevant peer libraries treat as their primary capture mechanism.** What neither peer's public docs prominently expose is exactly scenario 2/3 (multi-call history, verify-then-inspect) — which is the residual, narrower gap `ReceivedCalls()` is recommended to fill, not a re-implementation of what `ReturnsCallback` already covers. This also means the case for `ReceivedCalls()` should be argued on its own merits (a natural, evidence-independent completion of a fluent surface the package already has three-quarters of — Configure/Verify/[Received]) rather than on "consumers expect this because NSubstitute has it," since NSubstitute's own most-visible, most-documented capture path is the same callback shape Compono already ships.

`ClearReceivedCalls()`'s `ClearOptions` flag design, however, is unambiguous, strong, directly-applicable precedent for §5's `ClearCalls()` semantics specifically — worth citing directly in any future ADR.

## 10. Rejected alternatives, summarized

- **Opt-in capture (§3.B):** rejected — breaks the retrospective-verification convention every existing Compono.TestDoubles verification path relies on, and doesn't clearly reduce cost versus always-on for already-eligible members.
- **Folding `.Calls` onto `CallVerifier`/`Verify()`'s return value (§4.4):** rejected — reopens an architectural boundary ADR-0048 deliberately closed (`CallVerifier` has no log access after construction), and blurs `Verify()`'s single-purpose assertion contract.
- **Bounded/ring-buffer call log by default (§6.1/§8):** rejected as a default — measured cost of unbounded growth at realistic test-method scale is negligible; bounding trades a real, understood cost for a new, surprising "which call got silently dropped" semantic.
- **A new, second full history mechanism independent of ADR-0048's existing log (rather than exposing the existing one):** rejected — would duplicate storage (two lists per eligible member) for no benefit; the existing log already has everything scenario 1/2/3 need.

## 11. AOT/trimming implications

None found beyond what ADR-0048 already established and shipped (this feature reuses that exact storage, adding no new generic/reflection-based mechanism):

- No reflection, no boxing — the recommended `record` return type has real, closed, compile-time-known field types (same types the existing tuple already uses).
- No new trimming-unsafe surface — `List<T>.Clear()`/enumeration and constructing a `record` from already-known-type fields are all fully trim-safe, ordinary generic code, matching every other Compono.TestDoubles mechanism.
- `ReceivedCalls()`/`ClearCalls()` are ordinary generated extension methods and instance-state mutators — no new generator-only-resolvable indirection is introduced.

## 12. Compatibility implications

- **Source/binary compatibility:** fully additive. New generated members (a `ReceivedCalls()` bridge type, `ClearCalls()`), no change to any existing generated signature, no change to `CallVerifier`, `ReturnConfig<T>`, or `ReturnConfigBuilder<T>`'s existing public shape.
- **Generated-source compatibility:** every existing eligible/ineligible member keeps generating byte-for-byte the same code for its *existing* surface (`Configure()`/`Verify()`/dispatch) — the new bridge and `ClearCalls()` are pure additions to the generated output, not modifications of existing output. This matches the same "does not modify, break, or supersede any part of [the prior ADR's] existing generated surface" standard ADR-0048 itself met relative to ADR-0044.
- **Behavioral/SemVer:** additive, 1.1-appropriate. No existing consumer code changes behavior; a consumer not using the new members observes nothing different.
- **The one real compatibility-adjacent open question:** if a future ADR ever *does* decide to widen `CallVerifier` itself (Investigation 2, separate track) to carry optional log access, that would be a `CallVerifier`-shape decision made independently, on Investigation 2's own merits — this investigation's recommended design in §7 explicitly does not require or block that; they are compatible but decoupled tracks.

## 13. Whether this should be admitted into 1.1

**Yes, as scoped in §8.** Rationale, applying this session's evidence standard directly (no filed consumer issue is required):

- It is a conventional testing capability (retrospective call inspection is standard across the category, per §9, even though the *specific mechanism* each peer favors varies) that naturally completes a fluent surface Compono.TestDoubles already has two-thirds of (`Configure()`, `Verify()`).
- Its cost, for the scope recommended in §8, is genuinely zero additional runtime overhead for already-eligible members — the infrastructure already exists and is already paid for; this is as close to "free" as an additive feature gets in this codebase.
- It closes a real, named architectural gap the package's own migration-facing documentation calls out ("matching is not capture" as a stated NSubstitute-migration boundary, per the original brief) — not a manufactured feature, but also not one that required a filed complaint to justify; it was already visible as a documented gap.
- The scope in §8 deliberately excludes the two genuinely open-ended sub-questions (overload eligibility expansion, any `CallVerifier` shape change) rather than trying to resolve them in the same pass — keeping the admitted scope small and evidenced, consistent with this repo's stated aversion to unevidenced generality.

## 14. Exact decisions a future ADR needs to lock

1. **Bridge shape and name:** confirm `ReceivedCalls()` (vs. `Calls()` or another name) as the third bridge, parallel to `Configure()`/`Verify()`.
2. **Return type:** confirm a generated `record` per eligible member (vs. reusing the raw `ValueTuple` the internal log already stores) — this investigation recommends `record` for ergonomics but did not compiler-spike it; a spike should confirm no naming/generation conflicts analogous to ADR-0048 Amendment 1's derived-name-collision class of bugs.
3. **Snapshot semantics:** confirm "lock, copy to a new list/array, release lock, return" as the read contract (this investigation recommends it directly from ADR-0048's own established pattern, but it's the ADR's decision to ratify).
4. **`ClearCalls()` receiver:** decide whether it hangs off `Verify()`'s wrapper type, a new minimal wrapper, or the double instance directly (self-`this` extension) — this investigation deliberately left this open (§7) since it's a naming/receiver-ergonomics choice, not a semantics one; the semantics (§5) are the load-bearing decision, already answered.
5. **`ClearCalls()` scope relative to `ReceivedCalls()` eligibility:** confirm `ClearCalls()` applies uniformly to *all* members (clearing `CallCount` for argument-independent members too, not just the ADR-0048 log for eligible ones) rather than only to capture-eligible members — this investigation assumes uniform applicability (§5's table doesn't distinguish member shape) but a future ADR should state this explicitly since it changes the generated surface for *every* member, not just eligible ones.
6. **Overload eligibility for `ReceivedCalls()`:** explicitly deferred (§4.2, §8) — needs its own compiler-spike-backed evaluation, not inherited from ADR-0048 by default.
7. **Whether to also widen `ReceivedCalls()`/argument-matching eligibility to any other currently-excluded shape** (e.g., should the derived-name-collision-driven exclusions in ADR-0048 Amendment 1 be revisited for capture specifically, given they're implementation artifacts rather than fundamental limits) — flagged as a smaller, lower-priority open question, not expected to block 1.1 admission.
