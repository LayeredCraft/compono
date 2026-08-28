# [ADR-0054] Compono.TestDoubles: Sequential/Call-Count-Based Responses

**Status:** Accepted (capability, API, and implementation) — implemented and validated per [PLAN-0054](../plans/0054-testdoubles-overload-safe-matching-and-sequential-responses-impl-plan.md) (2026-08-27).

**Date:** 2026-08-27

**Decision Makers:** solo

## Context

`dynamodb-distributed-lock` dogfooding (real migration, not a synthetic
case) surfaced a `Compono.TestDoubles` capability gap distinct from both
[ADR-0053](0053-testdoubles-invocation-aware-callback-responses.md)
(invocation-aware callbacks) and
[ADR-0044 Amendment 21](0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-21-2026-08-27-argument-matching-for-overloaded-members-is-now-a-pre-10-product-requirement-amendment-18s-boundary-is-superseded-not-merely-evidenced-around)
(overload-safe argument matching). Three real tests need
`IAmazonDynamoDB.PutItemAsync` to return a **predetermined sequence of
outcomes, consumed by invocation ordinal**, across calls the system under
test (`DynamoDbDistributedLock`'s own `ExponentialBackoffRetryPolicy`
integration) makes **internally**, inside one public SUT operation, with
no opportunity for the test to reconfigure the double between calls:

```csharp
// AcquireLockAsync_WhenRetryEnabledAndEventuallySucceeds_ShouldReturnTrue
var callCount = 0;
dynamo.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
    .Returns(ci =>
    {
        callCount++;
        if (callCount < 3)
            throw new ConditionalCheckFailedException("Lock exists");
        return new PutItemResponse();
    });

var result = await sut.AcquireLockAsync(resourceId, ownerId, ct); // one call; 3 PutItemAsync calls happen inside it
```

Real acceptance sequences from this migration: `exception, exception,
value` (twice, different exception types) and `exception, value`. The
general capability must also naturally express a simpler shape like
`false, false, true` for a sync member — not evidenced by this migration
directly, but the natural minimal generalization of what *is* evidenced
(a fixed, ordinal-indexed list of outcomes, some of which may be
exceptions).

### Why this is not ADR-0053

[ADR-0053](0053-testdoubles-invocation-aware-callback-responses.md) is
about computing a response from the *real invocation's own arguments*, or
invoking a captured delegate argument and recording side effects around
it — genuinely invocation-aware behavior. This capability needs neither:
every call in the evidenced sequences uses the same (or don't-care)
arguments; nothing about the response depends on what was passed. The two
capabilities are related only in that both are "the current single-slot
`ReturnConfig<T>` isn't enough," not in mechanism or in the API shape a
consumer would reach for. Per explicit product direction, this ADR
records sequential responses as its own capability rather than folding it
into ADR-0053's scope — their user-facing semantics should not become
callback-shaped merely because a callback could technically emulate a
sequence.

### Why this is not ADR-0050

[ADR-0050](0050-testdoubles-multi-entry-argument-distinguished-configuration.md)
explicitly and separately scoped sequential/call-count-based returns
*out* of its own multi-entry design ("no sequential/call-count-based
returns" is named alongside, not the same as, "no callback responses").
ADR-0050's multi-entry model selects *which* configured entry applies
based on the *arguments of a given call* (argument-distinguished
dispatch); this capability selects *which outcome within one already-
selected entry* applies based on *how many times that entry has already
fired* (ordinal-distinguished dispatch). The two compose, they don't
overlap — see "Entry interaction" below.

## Dogfood evidence discipline: what this ADR is not evidence for

A careful re-audit of the same migration's other apparent NSubstitute
usage (see ADR-0044 Amendment 21's own discipline note) found that
**most** call sites needed nothing beyond today's existing
discriminator-only `Configure()`/`Verify()` surface — only 3 of roughly
15 apparent sites genuinely need sequential responses, and all 3 are the
*same underlying shape* (the SUT's own internal retry loop). This ADR
scopes to exactly that evidenced shape, not a general "any
NSubstitute-vocabulary sighting justifies a new capability" reading — see
ADR-0042's Non-Goals, unaffected here.

## Applying ADR-0029's Gap decision rubric

1. **Observed frequency:** 3 real, distinct call sites, one real project,
   all the same underlying shape (an internal retry loop the test cannot
   intervene in).
2. **Was this ever intended to work?** No — `Compono.TestDoubles` has
   never claimed sequential responses; ADR-0050 explicitly named the
   exclusion. Not a bug.
3. **Workaround cost:** real — the 3 sites remain on a real NSubstitute
   substitute (`Register<IAmazonDynamoDB>(_ => Substitute.For<IAmazonDynamoDB>())`)
   for this reason alone, unable to move to `Compono.TestDoubles` even
   after the overload-matching gap (ADR-0044 Amendment 21) is closed.
4. **Principle alignment:** no reflection or hidden state required — a
   fixed array of outcomes plus an atomic ordinal counter, both entirely
   within existing source-generation and no-reflection constraints.

**Classification:** per
[ADR-0042 Amendment 2](0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration),
a real evidenced case where `Compono.NSubstitute` satisfies a shape
`Compono.TestDoubles` cannot is a roadmap candidate by policy regardless
of frequency. Per explicit product direction recorded here, resolved
further to **Accepted requirement, `Proposed` API** — the same split
status ADR-0052 and ADR-0044 Amendment 21 both carry.

## Accepted design direction (product-directed, API details still open)

The following properties are **Accepted** — not open questions for the
implementation dive, only their exact mechanism is:

- Sequence state belongs to the **matched ADR-0050 entry**, not to the
  member as a whole — `Configure().Foo(Match.Is(x => x.Id == 1))` and
  `Configure().Foo(Match.Is(x => x.Id == 2))` own independent sequences
  with independent ordinal counters.
- **Independent ordinal per entry**, consumed deterministically by
  invocation order.
- **Thread-safe, deterministic ordinal consumption** — no locks; the
  outcomes list is immutable once configured (single-writer at
  `Configure()` time), so an atomic increment (`Interlocked.Increment`,
  the same primitive `ReturnConfig<T>.RecordCall()` already uses for
  `CallCount`) to claim the next ordinal, then an index read, is
  sufficient. No new concurrency primitive introduced.
- **Call recording stays independent of response consumption** —
  `RecordCall()`/`CallCount` (and ADR-0050's argument-filtered `Verify()`
  built on it) already fire on every dispatch regardless of which
  response path executes; a sequence changes only what gets *returned*,
  never what gets *recorded*. No new design needed here — true by
  construction once sequence state is attached to the entry rather than
  replacing `RecordCall()`'s own mechanism.
- **Reconfiguration replaces the sequence and resets its ordinal** —
  reusing the already-documented `ReturnConfigBuilder<T>.Returns`/
  `.Throws` "last-configuration-wins" contract, extended naturally: a new
  `Returns(...)`/sequence call on what resolves to the same entry
  identity replaces that entry's whole response state (single value,
  exception, or sequence — whichever it holds) and resets any ordinal to
  0. No new diagnostic, no separate "reset" vs. "replace" concept.
- **Exhaustion repeats the final configured response** — matches
  NSubstitute's own long-established `Returns(a, b, c)` behavior (which
  repeats `c` on call 4+), the exact behavior this feature's own
  migration audience already expects. An explicit "throw when exhausted"
  variant was considered and is likely redundant with the already-shipped
  `Verify().Member(...).Exactly(n)`; not adopted without further evidence
  a real case needs it distinct from that existing assertion.

## Response representation: one open question resolved here, against product direction

An earlier design pass considered making a sequence's configured values
**logical**, unwrapped outcomes (e.g. a bare `PutItemResponse` for a
`Task<PutItemResponse>`-returning member), generator-wrapped
(`Task.FromResult(...)`) at dispatch time — diverging from today's
single-entry `Returns(T value)` contract, where `T` is the member's own
declared return type and the consumer already constructs the `Task`
directly. **Rejected, per explicit product direction**: introducing two
different "what do I pass" conventions for the same member depending on
whether it's configured via `Returns(...)` or a sequence API is exactly
the kind of inconsistency this repo's own `docs/manifesto.md`
explicit-over-implicit bias warns against — a consumer should not need to
learn a second mental model only because they reached for sequencing.

**Decision:** a sequence's configured outcomes use the **same
declared-return-type contract** `Returns(T value)` already uses today —
for a `Task<PutItemResponse>`-returning member, sequence entries are
`Task<PutItemResponse>` values (e.g. `Task.FromResult(response)`), not
bare `PutItemResponse`. If today's declared-return-type contract itself
deserves better async ergonomics (a real, separately-worth-investigating
question, since this migration's own timer-test fix needed
`Task.FromResult(...)`/a hand-written async helper to construct one), that
improvement — if pursued at all — must apply **consistently to both**
`Returns(...)` and any sequence API, not to sequences alone. **Not decided
by this ADR**; flagged as a distinct, optional follow-up candidate, not a
prerequisite for this capability.

## `SequenceOutcome<T>` representation — implicit dual conversion rejected, replaced with an explicit `Throw` factory

An earlier implementation pass gave `SequenceOutcome<T>` two implicit
conversions — one from `T` (the value case) and one from `System.Exception`
(the throw case), mirroring `Match<T>`'s own single-implicit-conversion
shape. **Rejected, confirmed unsafe by real compile-and-run checks, not by
inspection.** For any `T` where `System.Exception` is itself assignable to
or from `T` — `T = Exception` itself, a concrete subtype like
`InvalidOperationException`, `T = object`, or a nullable reference `T` —
both conversions become simultaneously applicable to the same argument,
and C#'s overload-resolution betterness rules pick one **silently and
deterministically**, with no compile error to flag the ambiguity to the
author:

| `T` | `SequenceOutcome<T> x = new InvalidOperationException(...)` resolves to |
|---|---|
| `InvalidOperationException` | the **value** conversion (`IsException = false`) — the exception is treated as *data*, not a signal to throw |
| `object` | the **exception** conversion (`IsException = true`) — and there is *no* way to express "return this exception boxed as a plain `object` value" through the implicit surface at all, for any `T` `Exception` is assignable to |
| `Exception` | the **exception** conversion |

Both outcomes are individually legitimate, real return shapes for a member
whose declared type actually is (or is assigned from) `Exception` — a
`GetLastFault(): Exception` member returning a value is a completely
ordinary shape. An API whose meaning silently depends on which of two
*equally plausible* readings the compiler's betterness rules happen to
prefer is exactly the "obscure user-defined-conversion resolution"
category rejected here, independent of whether any given case also
produces a genuine `CS0121` (some do; the table above shows cases that
compile cleanly to the *wrong* reading, which is worse, not better).

**Decision: drop the implicit `Exception → SequenceOutcome<T>` conversion.
Keep the single implicit `T → SequenceOutcome<T>` conversion** (safe by
construction — nothing else competes with it, confirmed unambiguous for
every `T` tested, including `T = Exception`/`InvalidOperationException`/
`object`/`Exception?`, and for a `null` reference-typed value). **Add an
explicit factory, `Compono.SequenceOutcome.Throw(Exception exception)`,
returning a small non-generic marker type with its own implicit
conversion to `SequenceOutcome<T>` for every `T`:**

```csharp
public readonly struct SequenceOutcome<T>
{
    public static implicit operator SequenceOutcome<T>(T value) => ...;
    public static implicit operator SequenceOutcome<T>(SequenceOutcome.ThrownOutcome thrown) => ...;
}

public static class SequenceOutcome
{
    public readonly struct ThrownOutcome { /* internal-only, carries the Exception */ }
    public static ThrownOutcome Throw(Exception exception) => ...;
}
```

Because `ThrownOutcome` is its own distinct, non-generic type — never
equal to `T` for any real member's return type — it can never compete with
the `T`-conversion, for any `T`, without requiring an explicit type
argument anywhere (`T` is inferred the same way it already is today, from
the surrounding `ReturnConfigBuilder<T>`/params-array context). Confirmed
by a real compile-and-run check across every `T` in the table above, plus
a mixed real-shaped sequence (`SequenceOutcome.Throw(ex1),
SequenceOutcome.Throw(ex2), Task.FromResult(response)`) and a `null`
reference-typed value — every case now resolves to exactly the intended
reading, with no ambiguity and no silent wrong answer. `false, false, true`
(the plain-value case this ADR's Context motivates) is unaffected — it
still reads as three bare literals, only a `throw`n entry needs the
explicit `SequenceOutcome.Throw(...)` wrapper.

**Corrected public shape:**

```csharp
someDouble.Configure().TrySomething().ReturnsSequence(false, false, true);

dynamo.Configure().PutItemAsync(new PutItemRequest(), CancellationToken.None)
    .ReturnsSequence(
        SequenceOutcome.Throw(new ConditionalCheckFailedException("lock exists")),
        SequenceOutcome.Throw(new ConditionalCheckFailedException("lock exists")),
        Task.FromResult(new PutItemResponse()));
```

This supersedes every earlier example in this ADR and in `PLAN-0054` that
showed a bare `Exception` value passed directly to `ReturnsSequence(...)`
— those examples are wrong as written and must be corrected to
`SequenceOutcome.Throw(...)` before Phase 1 is considered done. The
already-shipped spike code (`src/Compono/SequenceOutcome.cs`,
`ReturnConfigBuilder.cs`) implements the *rejected* dual-implicit-conversion
shape and must be corrected as part of Phase 1's remaining work, not
carried forward as-is.

## Model shape — resolved by spike: no new parallel type

Whether sequencing needs a wholly separate `SequenceReturnConfig<T>` type,
or whether the smaller, more natural model is for each ADR-0050 response
entry to hold **one response representation that is either a single
configured outcome or a sequence of configured outcomes**, was left open
for the implementation spike. **Resolved: no new type.** `ReturnConfig<T>`
(`src/Compono/ReturnConfig.cs`) — the type already backing every entry,
plain-field, and closed-instantiation-bucket dispatch shape — was extended
in place with two fields (`SequenceOutcome<T>[]? Sequence`, `int
SequenceOrdinal`) and one method (`NextSequenceOutcome()`), requiring zero
changes to `Entry`'s own shape or the ADR-0050 append/lock machinery.
`ReturnConfigBuilder<T>.ReturnsSequence(...)` sets these three fields the
same way `Returns`/`Throws` already set the other two, and — per the
already-shipped last-configuration-wins contract — clears them too, in
both directions. Confirmed against real generated code (not just the bare
runtime type): a real `Compono.Generators.Tests` end-to-end execution test
and a real Native AOT publish-and-run both exercise a sequence on an
ADR-0050 matching-eligible entry with no additional storage type involved.

## Scope: value-return and exception/value sequences on `Task<T>`-returning members, evidenced; other shapes not assumed

The evidenced need is exactly: value-returning sequences, mixed
exception/value sequences, on `Task<T>`-returning async members (the real
AWS SDK shape this migration hit). **Void, non-generic `Task`/`ValueTask`,
and every other conceivable return shape are not assumed in scope merely
for API symmetry.** The implementation spike must determine, and record
explicitly, which shapes fall out naturally from whatever model it
adopts versus which would add meaningful complexity with no evidenced
need — and leave the latter out, recording the boundary the same way
Amendment 18 recorded overloaded-member exclusions, rather than silently
under- or over-building.

## Links

- `dynamodb-distributed-lock` dogfood evidence report and re-audit (this
  session, 2026-08-27) — the 3 real call sites and the finding that
  nearly every other apparent NSubstitute site needed nothing new.
- [ADR-0050](0050-testdoubles-multi-entry-argument-distinguished-configuration.md) —
  the entry model this capability attaches to; its own explicit exclusion
  of "sequential/call-count-based returns" from its original scope.
- [ADR-0053](0053-testdoubles-invocation-aware-callback-responses.md) —
  the related-but-distinct invocation-aware-callback capability this ADR
  is deliberately not merged into.
- [ADR-0044 Amendment 21](0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-21-2026-08-27-argument-matching-for-overloaded-members-is-now-a-pre-10-product-requirement-amendment-18s-boundary-is-superseded-not-merely-evidenced-around) —
  the sibling pre-1.0 requirement from the same dogfood pass, same
  "Accepted requirement, `Proposed` API" split-status precedent.
- [ADR-0042 Amendment 2](0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration) —
  the classification policy this finding falls under.
- `src/Compono/ReturnConfig.cs` — the existing single-slot storage/
  `Interlocked`-based call-recording model this capability's concurrency
  design reuses rather than reinventing.
- Real compiler spikes (this session, 2026-08-27) proving the dual-implicit-
  conversion `SequenceOutcome<T>` shape silently resolves to the wrong
  reading for `T = InvalidOperationException`/`object`/`Exception`, and
  that a distinct, non-generic `SequenceOutcome.Throw(...)` marker type
  eliminates the ambiguity for every `T` tested, including `null`.
- [PLAN-0054](../plans/0054-testdoubles-overload-safe-matching-and-sequential-responses-impl-plan.md) —
  the implementation plan this ADR's corrected shape must be reflected in
  before Phase 1 is considered done.
