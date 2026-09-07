# [RESEARCH-0027] Compono.CallVerifier: AtLeast/AtMost Cross-Package Investigation

**Status:** Done (research only; no ADR/amendment yet)

**Feeds:** a future ADR-0044 amendment decision (or a new ADR — see recommendation below) scoping `AtLeast`/`AtMost` additions to `Compono.CallVerifier` for the 1.1 release, and a small companion change to `Compono.Logging`'s `LogVerificationBuilder`.

## 1. Summary

`CallVerifier` (`src/Compono/CallVerifier.cs`) is no longer a `Compono.TestDoubles` implementation detail — it is directly reused, unmodified, by `Compono.Http` and `Compono.Logging` as their own count-verification primitive. All three packages' research documents (RESEARCH-0023/0024/0025) independently converged on the same missing capability: "at least N" / "at most N" call-count assertions, distinct from today's `Never()`/`Once()`/`Exactly(n)`.

This investigation finds:

- The original ADR-0044 decision to omit `AtLeast`/`AtMost` was **explicit and specific to those exact names**, not merely a rejection of call-order verification or a bigger DSL. The context has materially changed since then (see §2).
- `AtLeast(int)`/`AtMost(int)` are safe, additive, conventional count assertions that complete the existing abstraction without turning it into a verification framework. `Between`/`AtLeastOnce`/`AtMostOnce`/`Any`/`None` should **not** be added (see §4).
- The change is purely additive at the source/binary/API level in core `Compono`. It works immediately, with zero changes required, for `Compono.TestDoubles` and `Compono.Http`. It requires one small, mechanical companion change in `Compono.Logging`'s `LogVerificationBuilder` to actually surface the new methods through its fluent chain (see §3).
- Recommendation: **amend ADR-0044** (not a new ADR, not undocumented). See §6.

## 2. Revisiting the original decision

### 2.1 What ADR-0044 Requirement 3 actually said

`docs/adr/0044-compono-testdoubles-v2-overloads-generics-verification.md`, Requirement 3's Considered Options list three shapes (lines 128–146):

1. A dedicated `Verify()` bridge with `.Once()`/`.Never()`/`.Exactly(n)` — **chosen**.
2. Folding verification into `ReturnConfigBuilder<T>` via a raw `CallCount` property, verified with `Assert.Equal(...)`.
3. "A full `Received()`-equivalent — argument-aware call recording, sequence/order verification, `ReceivedCalls()`-style enumeration."

The Decision Outcome section states, verbatim (lines 393–398):

> "**Deliberately minimal, matching the explicit instruction:** `Never`/`Once`/`Exactly(n)` only — no `AtLeast`/`AtMost`, no argument-aware recording, no call-order verification, no `ReceivedCalls()`-style enumeration, no strict mode. `Interlocked.Increment` on a plain `int` field is the cheapest possible thread-safe counter — no allocation per call, no dictionary, matching the 'don't allocate just to support `Once()`' instruction directly."

This is the load-bearing sentence for this investigation. **`AtLeast`/`AtMost` were named and explicitly excluded**, not merely implied by a broader "no DSL" stance. This is a stronger prior than "the ADR only ruled out call-order verification" — it directly addressed count-range semantics and said no.

However, the *reasoning offered* for the exclusion is entirely about implementation cost and scope discipline, not about count-range semantics being conceptually wrong:

- The stated cost concern ("cheapest possible thread-safe counter — no allocation per call, no dictionary") is about the **counting mechanism** (`Interlocked.Increment` on an `int`), not about what assertions are built on top of that counter afterward. `AtLeast`/`AtMost` need no additional storage, no additional field, and no change to `RecordCall()` — they read the exact same `observedCount` int that `Exactly` already reads. The performance rationale that justified minimality does not apply to these two methods at all; it applies to the run-time counter, which is already built and already shipped.
- The scope discipline concern ("don't build a verification framework") is squarely about `Received()`-equivalents: argument-aware recording, call-order verification, and enumeration — all fundamentally different capabilities requiring new storage, new generated surface, and new semantics. `AtLeast`/`AtMost` require none of that; they are two more `if` branches inside a struct that already exists.
- Requirement 3's Option 3 (the "full `Received()`-equivalent") is the option genuinely rejected on architectural grounds. `AtLeast`/`AtMost` were bundled into that rejection by name, but not because they share Option 3's cost profile — they don't need per-call storage, per-call allocation, or generated-surface changes of any kind.

So the honest reading is: **ADR-0044 rejected `AtLeast`/`AtMost` primarily because nothing had evidenced a need for them yet, in the context of a decision that was simultaneously and correctly rejecting a much more expensive DSL.** It was written as one sweeping "no" alongside genuinely expensive asks, not as a considered verdict that count-range assertions themselves are undesirable.

### 2.2 What changed since

At the time of ADR-0044 (2026-08-14), `CallVerifier` had exactly one consumer: `Compono.TestDoubles`. Two things have changed:

1. **`CallVerifier` is now cross-package infrastructure.** `Compono.Http`'s `HttpResponseRegistration.Verify()` returns a `CallVerifier` directly (`src/Compono.Http/HttpResponseRegistration.cs:56`), and `Compono.Logging`'s `LogVerificationBuilder` builds one internally as its terminal step (`src/Compono.Logging/LogVerificationBuilder.cs:66-87`). Three independent research passes (RESEARCH-0023, -0024, -0025), done by three different investigators researching three unrelated packages, converged on the same missing capability without prompting each other. That is a materially different evidentiary posture than "one package's ADR author speculated about a nice-to-have."
2. **ADR-0048 (2026-08-21, seven days after ADR-0044) already extended `CallVerifier`'s consumption pattern** — not `CallVerifier` itself — by adding argument-filtered call verification (`Verify().Member(Match.Is<T>(...))`), reusing `CallVerifier` completely unmodified as the terminal count check after argument filtering. ADR-0048 never revisits or reopens Requirement 3's minimality decision; it treats `CallVerifier` as a fixed, reusable primitive and layers filtering in front of it. This confirms the architectural pattern this investigation proposes to extend (add richness to the terminal count check, not to what feeds it) is already the established shape, just not yet extended to counts themselves.

Neither of these facts overrides ADR-0044's explicit language — the ADR clearly said no to these two names. But both support the case that the *original constraint has been satisfied and outgrown*, not that it was wrong: the ADR's own words are about lacking evidence and avoiding scope creep, and both conditions have now materially changed in ways ADR-0044's author could not have accounted for when a single package was the only consumer.

## 3. Cross-package consistency — verified against real code, not illustrative examples

### 3.1 Compono.TestDoubles

Generated `Verify()` extensions return `global::Compono.CallVerifier` directly from every generated overload (`src/Compono.Generators/Templates/TestDouble.scriban:585-665`, ten distinct emission sites covering plain members, argument-matched members, and generic members). Because these are ordinary instance methods on a public struct, **any new public method added to `CallVerifier` is immediately callable on every one of these generated call sites with zero generator changes and zero regeneration requirement for existing consumers** (existing generated code is untouched; new consumer code simply calls a method that now exists on the struct it already receives).

Real shape confirmed:

```csharp
mediator.Verify().Send().Once();     // exactly 1 call
mediator.Verify().Send().AtLeast(1); // would work immediately once added
```

### 3.2 Compono.Http

`src/Compono.Http/HttpResponseRegistration.cs:56`:

```csharp
public CallVerifier Verify() => new(_matchedCallCount, _description);
```

`Verify()` returns `CallVerifier` itself — no wrapper type. `registration.Verify().AtMost(2)` would compile and work the instant `AtMost` exists on `CallVerifier`, with **zero changes to `Compono.Http`** required. The doc comment at line 51 already states the type is intentionally "unchanged" from core, so this is squarely in scope for a package that has explicitly deferred to core's verification vocabulary.

### 3.3 Compono.Logging — the one real gap

`Compono.Logging`'s `Verify()` does **not** return `CallVerifier`. It returns `LogVerificationBuilder` (`src/Compono.Logging/LogVerificationBuilder.cs:14`), a fluent filter chain (`AtLevel`, `WithEventId`, `WithException<T>`, `WithMessageContaining`, `Matching`) that only converts to a `CallVerifier` internally, at the last possible moment, inside a private `ToCallVerifier()` method (line 66). The public terminal methods (`Once()`, `Never()`, `Exactly(int)`, lines 49–58) are **thin one-line forwarders** written by hand:

```csharp
public void Once() => ToCallVerifier().Once();
public void Never() => ToCallVerifier().Never();
public void Exactly(int times) => ToCallVerifier().Exactly(times);
```

`CallVerifier` is deliberately never part of `LogVerificationBuilder`'s public surface (per its own class doc, lines 6–13). This means **adding `AtLeast`/`AtMost` to `CallVerifier` does not automatically expose them through `logger.Verify().AtLevel(...).AtLeast(1)`** — that specific illustrative call shape from the brief does not compile today and would not compile after only a core change. `Compono.Logging` needs its own two-line companion addition:

```csharp
public void AtLeast(int times) => ToCallVerifier().AtLeast(times);
public void AtMost(int times) => ToCallVerifier().AtMost(times);
```

This is mechanical, in the same file, following the exact existing pattern — not a design question, just a fact this investigation needs to record so the illustrative cross-package example in the brief is understood correctly: **it is not automatically true today; it requires one small, low-risk, same-shape companion change in `Compono.Logging`, in addition to the core change.** This companion change is additive to `Compono.Logging`'s public API and carries no design risk — it is the same forwarding pattern already used three times in the same class.

### 3.4 Do any package's semantics make counts misleading?

No. In all three packages, the underlying `observedCount` represents "number of times the matching condition was satisfied" — a call to a member (TestDoubles), a request matching a registration (Http), or a log entry matching accumulated filters (Logging). `AtLeast`/`AtMost` mean exactly the same thing in all three: a lower/upper bound on that same count. There is no package where "at least" or "at most" would need different semantics or would be misleading given how the count is produced.

## 4. API semantics

### 4.1 Signatures

```csharp
public void AtLeast(int times)
public void AtMost(int times)
```

Matching `Exactly(int times)`'s existing parameter name and type exactly (consistency, not incidental).

### 4.2 Argument validation

- **Negative counts**: `Exactly(int)` today performs **no argument validation at all** — `Exactly(-1)` simply can never pass (since `observedCount` is never negative) and produces a slightly confusing but harmless message ("Expected exactly -1 call(s)..."). For consistency with existing behavior and to avoid introducing a validation asymmetry between `Exactly` and the two new methods, `AtLeast`/`AtMost` should **not** add `ArgumentOutOfRangeException` guards either. A negative `AtLeast(-1)` is trivially always true (every count is `>= -1`); a negative `AtMost(-1)` can never pass. Both are harmless if slightly nonsensical inputs — exactly as `Exactly(-1)` is today. Adding validation to only the two new methods, while `Exactly` has none, would be an inconsistent, un-evidenced enhancement outside this investigation's scope.
- **`AtLeast(0)`**: Always trivially true (every observed count is `>= 0`). Not forbidden — a consumer might write it for symmetry/self-documentation in generated test scaffolding, and rejecting it would require validation `Exactly(0)`/`Never()` don't have either. Harmless to allow.
- **`AtMost(0)` vs. `Never()`**: `AtMost(0)` is semantically identical to `Never()` (a count can't be negative, so "at most 0" means "exactly 0"). This is not a reason to reject `AtMost(0)` — `Exactly(0)` already exists as a fully redundant spelling of `Never()` today, and no one has proposed removing it. Redundant-but-clear spellings are already the established convention in this type; `AtMost(0)` fits it rather than breaking new ground.
- **Exception type**: `TestDoubleVerificationException`, unchanged — same type `Exactly` already throws, consumed uniformly by all three packages already (Http and Logging both surface it via their own doc comments referencing this exact type).

### 4.3 Diagnostic wording

Matching the existing message convention exactly (verified against `test/Compono.Tests/CallVerifierTests.cs:28,49,60,81`, e.g. `"Expected exactly {times} call(s) to {memberDescription}, but received {observedCount}."`):

```csharp
public void AtLeast(int times)
{
    if (observedCount < times)
    {
        throw new TestDoubleVerificationException(
            $"Expected at least {times} call(s) to {memberDescription}, but received {observedCount}.");
    }
}

public void AtMost(int times)
{
    if (observedCount > times)
    {
        throw new TestDoubleVerificationException(
            $"Expected at most {times} call(s) to {memberDescription}, but received {observedCount}.");
    }
}
```

This is a direct, minimal-diff extension of `Exactly`'s existing shape and wording style — same sentence template, same clause order, only the comparison operator and the leading adjective change.

### 4.4 What NOT to add, and why

Per explicit instruction, evaluated and rejected:

- **`Between(int min, int max)`** — fully derivable by a consumer calling `AtLeast(min)` then `AtMost(max)` (or vice versa) in two lines; it is pure sugar over two primitives that themselves need to exist first. Adding it now would be exactly the "enlarge the vocabulary before evidence demands it" mistake ADR-0044's original author was rightly wary of. Revisit only if real consumer friction with the two-call spelling is ever evidenced.
- **`AtLeastOnce()` / `AtMostOnce()`** — trivial aliases for `AtLeast(1)` / `AtMost(1)`. NSubstitute has `Received()` (bare, no count, meaning "at least once") as a historical artifact of its fluent-`Received(n)` design, not because "at least once" is an independently meaningful concept worth a dedicated name in a count-based API shaped like this one. Adding both the general and the special-cased-to-1 spelling doubles the vocabulary for zero new expressive power.
- **`Any()` / `None()`** — `Any()` would mean "was called at least once," fully redundant with `AtLeast(1)`; `None()` is exactly `Never()` already. Pure duplication.

None of these are rejected because they'd be hard to implement — every one of them is a one-line trivial forward. They are rejected because `CallVerifier`'s entire design identity (per ADR-0044's own words: "one small, concrete... no dictionary, no boxing") is a deliberately narrow, unambiguous vocabulary. `AtLeast`/`AtMost` complete the natural mathematical set implied by `Exactly` (exactly / at least / at most is a complete, closed, mutually-orthogonal trio); every rejected name above is a convenience alias *on top of* that trio, not a missing member of it. Stopping at the trio is the same "don't build a framework" discipline ADR-0044 exercised, applied consistently rather than abandoned.

## 5. Compatibility assessment

- **Source compatibility**: Fully additive. No existing member signature changes. No existing call site needs modification.
- **Binary compatibility**: Adding two public instance methods to an existing public struct is binary-compatible — it does not change the struct's layout, does not change any existing member's metadata token, and does not affect any assembly compiled against the current `Compono.dll`. Existing compiled consumers (test assemblies, `Compono.Http.dll`, `Compono.Logging.dll`) continue to load and run unmodified; only assemblies wanting to call the new methods need to reference an updated `Compono` package.
- **Public API compatibility**: Purely additive to the public API surface. No public member is removed, renamed, or has its signature changed.
- **Consumers compiled against Compono 1.0**: Unaffected. They don't reference the new methods and never will unless recompiled against a newer `Compono`, at which point they gain access to `AtLeast`/`AtMost` for free with no other code change required.
- **Generated-source compatibility**: No generator changes required for `Compono.TestDoubles` (§3.1) — the generated `Verify()` surface already returns `CallVerifier` by value; existing generated code needs no regeneration and no template change. The `Compono.Http` case similarly needs no source changes (§3.2). `Compono.Logging` needs one small, additive, non-generated hand-written change (§3.3), which is not a generated-source compatibility concern at all — it's ordinary hand-written library code.
- **AOT/trimming**: No new reflection, no new virtual dispatch, no new generic instantiation pattern — the new methods use the exact same field reads and exception-construction pattern `Exactly` already uses, which is already proven AOT/trimming-safe in the existing package.
- **Package versioning/SemVer**: Purely additive — appropriate for a minor version bump (1.1.0) regardless of Compono's pre-/post-1.0 status; this is not a question that depends on SemVer stage at all, since additive API surface is never a breaking change under any SemVer interpretation.

## 6. ADR mechanism recommendation

**Recommendation: amend ADR-0044**, not a new ADR, and not "no ADR."

Reasoning:

- ADR-0044 Requirement 3 is the specific decision record that named `AtLeast`/`AtMost` and rejected them (§2.1). The natural, discoverable place to record that this specific, named exclusion is being revisited is the same decision record — a future reader investigating "why doesn't `CallVerifier` have `AtLeast`?" will find ADR-0044 first (it is `CallVerifier`'s originating ADR, referenced directly in the type's own XML doc comment: `"Deliberately minimal... per ADR-0044 Requirement 3"`). An amendment there closes the loop exactly where the original constraint lives, consistent with this repo's stated amendment convention (corrections/refinements to a still-valid decision, not reversals).
- This is not a **reversal** of ADR-0044's architecture — the "small, concrete, no dictionary, no boxing" principle stays fully intact; `AtLeast`/`AtMost` are two more branches on the same struct, not a different architecture. That rules out a Superseded status and supports Amendment over a full rewrite.
- A **brand-new ADR** would be disproportionate: there is no new architectural question here requiring its own Context/Decision Drivers/Considered Options framing distinct from what ADR-0044 already established (the struct's shape, its exception type, its "small and concrete" philosophy, its cross-package reuse are all already-settled facts this change operates entirely within). A new ADR would either duplicate ADR-0044's existing content or read strangely thin next to it.
- **"No ADR"** is not appropriate specifically *because* ADR-0044 made an explicit, named decision on this exact question ("no `AtLeast`/`AtMost`"). Even though the code change itself is small, silently contradicting a named decision without a documented amendment would leave the ADR record actively wrong/misleading for any future reader — this is exactly the scenario `design-decisions.md`'s amendment mechanism exists for (a correction to a still-valid decision, not a reversal, but one that must be visible in the record).

The amendment should also note the small `Compono.Logging` companion change (§3.3) as a consequence, since `LogVerificationBuilder`'s design (deliberately keeping `CallVerifier` out of its public surface) is itself documented in ADR-0055, not ADR-0044 — the amendment to ADR-0044 should cross-reference that ADR-0055 will need a small corresponding note/update when this is implemented, but that is ADR-0055's amendment to make, not ADR-0044's.

## 7. Final assessment

- **Recommended `CallVerifier` additions**: `AtLeast(int times)` and `AtMost(int times)` only, per §4.
- **ADR mechanism**: Amend ADR-0044 (§6); flag a small corresponding note for ADR-0055 regarding `Compono.Logging`'s companion forwarding methods.
- **Compatibility verdict**: Fully additive at every layer examined — source, binary, public API, generated-source, AOT/trimming, SemVer. No consumer breakage under any scenario.
- **Proceed to 1.1**: Yes. This is exactly the "conventional, useful, easy-to-understand count assertion that naturally completes an existing abstraction" the investigation was asked to test for — not a manufactured feature, not scope creep, and not blocked by any technical or compatibility risk.
