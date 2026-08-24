# [ADR-0050] Compono.TestDoubles: Multi-Entry, Argument-Distinguished Response Configuration

**Status:** Accepted

**Date:** 2026-08-24

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

ADR-0048 gave every argument-matching-eligible member exactly one storage
slot per member: one `ReturnConfig<TSlot>` field, one `Match<TParam>?` field
per real parameter. A second `Configure()` call on the same member
overwrites both fields in place — the first configuration is gone, not
shadowed. ADR-0048's own "Considered Options" section evaluated and
explicitly rejected an ordered, append-only, last-match-wins response chain
for exactly this shape, on the grounds that "zero real evidence at the time
motivates more than one configured response per member," and that such a
chain "introduces a real new failure mode a single-slot design doesn't
have."

Continued dogfooding against `ncipollina/trivia-platform`
(`chore/compono-testdoubles-migration` branch, `Compono.TestDoubles
0.7.0-preview.81`) now supplies that evidence — and it is stronger than a
mere ergonomic gap. Three independent real occurrences were found (see
`docs/research/0008-trivia-platform-multi-entry-testdoubles-dogfood.md` §1
for full detail; summarized here):

1. **`MultiStubLeaderboardRepository`** — a hand-written fake, deliberately
   bypassing both `Compono.TestDoubles` and `Compono.NSubstitute`, backing
   `ILeaderboardRepository.GetLeaderboardEntryAsync`/`GetLeaderboardRankAsync`
   with a real per-argument dictionary because `Compono.TestDoubles`'
   generated surface could not express two disjoint, divergent-value
   configurations on the same member in one test. NSubstitute itself can
   express this shape — `CachedLeaderboardRepositoryTests` (below) is direct
   evidence of that, which is exactly why that file stayed on NSubstitute
   instead of hitting this same limitation. `MultiStubLeaderboardRepository`
   is evidence of the gap and of a fake-based workaround the migration
   resorted to specifically because reverting to NSubstitute wasn't an
   option there (its one call site hand-constructs the repository directly,
   bypassing `[Compose]` entirely, rather than going through a profile that
   could fall back to NSubstitute).
2. **`CachedLeaderboardRepositoryTests`** — reverted to raw
   `Compono.NSubstitute`, because `RetrieveTopEntriesAsync` needs three
   different disjoint-literal-argument configurations (differing period,
   type, and count) across three tests, each registering the member twice.
3. **`LeaderboardServiceTests` — a live, currently undetected correctness
   bug, not merely an inconvenience.** Five tests configure
   `GetLeaderboardEntryAsync` twice per test (Weekly args, then AllTime
   args). Verified directly against the pinned package with an isolated
   probe (not assumed from documentation): the second `Configure()` call
   silently replaces the first matcher *and* return value. In two of the
   five tests (`ZeroScoreProjection_DoesNotQueryRank`,
   `NegativeRankResult_OmitsRank`), this causes the Weekly-args call to fall
   through to the member's computed default (`null`) instead of the
   configured `weeklyEntry` — so `LeaderboardService.BuildPlayerStatsAsync`
   takes its `entry is null` early-return branch instead of the branch the
   test was written to exercise (`entry.Score > 0`'s rank-lookup guard, and
   the rank-clamping logic downstream of it). **Both tests currently pass**
   — `dotnet test` on the working tree shows 17/17 green in this file —
   because neither test's assertions happen to check the field
   (`HasScore`/`IsRanked`) that would reveal the divergence. This is a
   false-pass: a real behavioral bug the test was written to catch is
   silently unreachable, and the test suite reports success anyway. The
   other three tests in the file happen to configure the same value
   (`null`) on both branches, so they're accidentally unaffected — not
   evidence the pattern is safe, evidence it's easy to get unlucky with.

All three occurrences are the same shape: one member, two-or-more disjoint
literal-argument configurations, needed simultaneously within one test. None
needs overlapping matchers (e.g. a `Match.Any` catch-all overridden by a
specific literal) — that shape falls out of the chosen design for free (see
Decision Outcome) but isn't itself separately evidenced here.

**This ADR formally amends ADR-0048's "Considered Options" rejection of an
ordered response chain**, on the strength of evidence ADR-0048 said at the
time it lacked. Every other decision ADR-0048 made — overload-scoped
exclusion from matching, `Match<T>`'s three-kind shape
(equality/`Any`/predicate), matcher and call-log fields living separately
from `ReturnConfig<T>`, `CallVerifier`'s stateless count-only contract — is
unaffected and unchanged by this ADR.

**Pre-1.0 framing.** `Compono.TestDoubles` is still pre-1.0. This ADR
introduces exactly one intentional semantic correction to existing shipped
behavior — a second `Configure()` call on the same member no longer
silently discards the first — documented explicitly below rather than
treated as a compatibility break to work around. The overwhelmingly common
case (one `Configure()` call per member) is unaffected: behaviorally
unchanged for consumers, even though the generated representation
underneath it changes.

## Decision Drivers

- The false-pass finding above: a silent, order-dependent overwrite that
  makes a test report success while exercising the wrong production code
  path is a correctness hazard, not just an expressiveness gap — this
  raises the bar above "nice to have."
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  evidence-over-prediction discipline — three independent real occurrences,
  not a hypothetical, and the design stays scoped to exactly what they
  evidence (disjoint literal-argument entries; no matcher-specificity
  ranking, no sequential/call-count-based returns, no callback responses).
- Reuse over invention, and reuse across previously-separate mechanisms
  where it composes naturally: ADR-0049's per-closed-`T` bucket already
  proved that a member can hold more than one simultaneously-active
  `ReturnConfig<TSlot>`, keyed by something chosen at the `Configure()`
  call site. This ADR generalizes that same idea to a second key (an
  ordered position in a list) rather than inventing an unrelated
  mechanism — and, per a spike proving it composes with no new machinery,
  applies to both ADR-0048's plain matching-eligible shape and ADR-0049's
  nested per-`T` state consistently, rather than leaving one shape newly
  inconsistent with the other's semantics.
- Explicit product direction: no matcher-specificity ranking ("most
  specific wins") without rigorous, predictable justification — not
  evidenced here, and its own complexity (a partial order over
  equality/predicate/`Any` matchers) isn't warranted by three
  disjoint-literal-argument occurrences.

## Considered Options

### Response-selection semantics

1. **Last matching registration wins, evaluated in reverse registration
   order (chosen).** `Configure()` appends a new entry; dispatch walks
   entries from most-recently-registered to least, returning/throwing on
   the first entry whose matcher set matches the real call. Gives a simple,
   predictable override idiom —
   ```csharp
   repo.Configure().Get(Match.Any<int>()).Returns(defaultValue);
   repo.Configure().Get(42).Returns(specialValue);
   // Get(42) -> specialValue; any other value -> defaultValue
   ```
   — using only registration order, not matcher comparison. Directly
   matches every real occurrence in §Context: whichever `Configure()` call
   is written last in the test is the one whose value actually reaches the
   assertion, exactly the intuition a reader has skimming top-to-bottom
   test setup code.
2. **First matching registration wins.** Rejected: inverts the intuitive
   reading order (later code in a test method would need to be read as
   *lower* priority than earlier code) and doesn't match how the existing
   single-slot bug already behaves (last write already wins today, just
   destructively) — choosing "first wins" here would be a second behavior
   change layered on top of the fix, not a minimal correction.
3. **Most-specific-match wins** (e.g. an exact-equality matcher outranks a
   predicate, which outranks `Match.Any`). Rejected per explicit product
   direction — no evidenced need, and it requires a new partial order over
   `Match<T>`'s three kinds with real edge cases (which of two different
   predicates over the same parameter is "more specific"?) that nothing in
   this repo's evidence trail motivates solving.

### Storage shape

1. **A generated, per-member `Entry` type bundling one full matcher set
   plus its own `ReturnConfig<TSlot>`, held in an ordered `List<Entry>`
   (chosen).** Directly generalizes today's fields (`__Member` +
   `__Member_m_{param}` per parameter) — a `List<Entry>` of length ≤ 1 is
   observably identical to today's single-field shape, which is what makes
   this a safe pre-1.0 correction rather than a rewrite. Reuses `Match<T>`,
   `ReturnConfig<T>`, and `CallVerifier` completely unchanged (see
   "Runtime types," below) — only the field layout the generator emits and
   the dispatch loop change.
2. **`Dictionary<ArgTuple, ReturnConfig<TSlot>>` keyed by argument
   values.** Rejected: matchers aren't equality-comparable in general
   (`Match.Any`/`Match.Is(predicate)` have no meaningful hash/equality), so
   a dictionary buys nothing over a list here and would force either boxing
   the matcher tuple or inventing a synthetic key — against this repo's
   no-reflection/no-incidental-boxing posture (ADR-0001, ADR-0043). Unlike
   ADR-0049's bucket (correctly keyed by `Type`, which *does* have cheap,
   correct equality via `typeof(T)`), there's no analogous natural key
   here.
3. **Keep the single-slot field, add a second "override" field for exactly
   one additional entry.** Rejected: arbitrarily caps real capability at
   two entries for no principled reason, and still needs the same dispatch-
   order/matcher-check logic a proper list would need — strictly more
   special-casing for strictly less capability.

### `ReturnConfigBuilder<T>` ownership across list growth

`Configure()` must hand back a builder that writes into a specific,
newly-appended entry's `Config` field — and a *later* `Configure()` call on
the same member, growing/reallocating the backing `List<Entry>`, must never
corrupt or misdirect an *earlier* `Configure()` call's still-unresolved
builder (i.e. `.Returns()`/`.Throws()` not yet called on it).

Two candidate representations were prototyped and executed against this
exact hazard (spike detail in
`docs/research/0008-trivia-platform-multi-entry-testdoubles-dogfood.md`
§6):

1. **Reference-type `Entry`, `ReturnConfigBuilder<T>` unchanged (chosen).**
   `Configure()` does
   ```csharp
   var entry = new __Member_Entry();
   entry.Matcher_x = ...;
   __Member_entries.Add(entry);
   return new ReturnConfigBuilder<TSlot>(ref entry.Config);
   ```
   The invariant is on the `Entry` object, not on operation order: `Entry`
   is a reference type, so its `Config` field lives at a fixed location on
   the heap for the object's lifetime. `List<T>.Add()` only copies the
   **reference** to `entry` into the list's backing array — it never
   touches or relocates the `Entry` object itself. A later `Configure()`
   call growing/reallocating that backing array only moves *pointers to*
   entries, never the entries' own heap storage, so a `ref` taken into an
   entry's `Config` field stays valid indefinitely regardless of how many
   more entries are later appended, and regardless of whether that `ref`
   is taken before or after the entry is added to the list. Proved by
   construction and
   confirmed empirically (4 execution-level spike tests configuring
   multiple entries per member before any dispatch reads them, zero
   corruption). Requires **zero changes** to `ReturnConfigBuilder<T>` or
   `ReturnConfig<T>` — both are reused exactly as ADR-0043 shipped them,
   just pointed at a different field's owner.
2. **Value-type `Entry` struct in `List<Entry>`, builder restructured to
   capture `(List<Entry> list, int index)` and index back in on
   `Returns`/`Throws`.** Not implemented past the design stage: correct in
   principle (an index survives reallocation, unlike a `ref` into a struct
   element would), but requires either re-resolving through
   `CollectionsMarshal.AsSpan` on every write or accepting a second,
   less-obvious correctness argument, plus restructuring
   `ReturnConfigBuilder<T>` to hold an index instead of a `ref`. Rejected
   per explicit product direction: `Configure()` runs at test-setup time,
   not on a hot path — one small `Entry` class allocation per `Configure()`
   call is a cost this repo has no reason to avoid at the expense of a
   simpler, already-correct, unmodified `ReturnConfigBuilder<T>`.

### Does this compose with ADR-0049's per-closed-`T` state, or does it need to stay out?

ADR-0049's closed-instantiation members already hold their own single
`Config` + `Matcher_*` fields — nested one level deeper, inside a
`__Member_State<T>` class reached via a `Dictionary<Type, object>` bucket —
but the same overwrite behavior this ADR fixes elsewhere. Left alone, this
ADR would produce an inconsistency: `Configure().Foo(1).Returns(a);
Configure().Foo(2).Returns(b);` works on a plain member, but the identical
pattern on a generic member (`Configure().Foo<T>(1).Returns(a);
Configure().Foo<T>(2).Returns(b);`, same closed `T` both times) would still
silently overwrite.

1. **Fold ADR-0049 members into the same fix — `_State<T>` holds
   `List<Entry>` instead of one `Config`+`Matcher_*` pair (chosen).**
   Spiked directly (see `docs/research/0008-...md` §6): the exact same
   `Entry` abstraction — matcher fields plus a `ReturnConfig<TSlot>` — was
   reused unmodified inside `_State<T>`, replacing its single `Config`/
   `Matcher_*` fields with `List<Entry> Entries`. Dispatch became the
   identical reverse-scan loop, just reading from the bucket's list instead
   of the member's own direct list. The only friction encountered was
   mechanical: extension methods (declared in a separate static class from
   the double) need the nested `Entry` type's fully-qualified name
   (`global::{Double}.{StateClass}<T>.Entry`) rather than the unqualified
   name that resolves fine from inside the double class itself — not a new
   generic constraint, not a divergent entry shape, not a new eligibility
   rule. Proved with 4 passing execution tests: plain-member multi-entry,
   plain-member `Match.Any` + literal override (last-wins), closed-
   instantiation member with 2 entries in one closed `T`, and closed-
   instantiation member with 2 independent closed `T`s each holding their
   own 2-entry list. Per this repo's evidence discipline (ADR-0029), since
   the fix falls out of the shared abstraction with no new machinery, it is
   folded in — leaving ADR-0049 members on the old single-slot semantics
   here would be choosing to *keep* a now-known-bad behavior in an adjacent
   shape for no reason beyond scope-timidity, not a genuinely smaller or
   safer change.
2. **Leave ADR-0049 members single-slot, fix only plain matching-eligible
   members.** Rejected — not because it's unsafe, but because the spike
   showed it isn't the smaller option either: it would require the *same*
   entry-list machinery to be built and then deliberately withheld from one
   of the two eligible shapes, producing exactly the inconsistency named
   above with no offsetting simplicity gain. Recorded as the fallback this
   ADR would have taken if the spike had found real friction — it did not.

This ADR does **not** broaden ADR-0049's member-eligibility rules (which
generic members qualify as closed-instantiation-eligible is unchanged) —
only how an already-eligible member's per-closed-`T` state is stored.

### Verification

**Unchanged.** `CallVerifier` stays stateless and count-only; the call-log
`List<(...)>` stays a single, member-scoped (or bucket-scoped, for ADR-0049
members) log shared across every entry — multiple response entries do not
imply multiple call histories. "What was actually invoked" (the call log)
and "what should this invocation return" (the entry list) remain
independent concerns, per ADR-0048's original separation, unchanged by
adding more than one entry to the response side.

### No-match behavior

**Unchanged.** If no entry's matcher set matches a real call, dispatch
falls through to ADR-0045's existing rule exactly as it does today for the
single-entry case: a deterministic default for members with one, or
`TestDoubleNotConfiguredException` for configuration-required members. No
new failure model invented; an empty or all-non-matching entry list
degenerates to exactly today's "unconfigured" behavior.

## Decision Outcome

Chosen option: an ordered `List<Entry>` per member (or per closed-`T`
bucket for ADR-0049 members), each `Entry` a small generated reference
type bundling one full matcher set and its own `ReturnConfig<TSlot>`;
`Configure()` appends; dispatch scans in reverse registration order,
returning/throwing on the first matching entry; no match falls through to
the existing ADR-0045 rule. `ReturnConfigBuilder<T>`, `ReturnConfig<T>`,
`Match<T>`, and `CallVerifier` are all reused completely unchanged.

Target API — the exact shape that fixes all three trivia-platform
occurrences:

```csharp
leaderboardRepository.Configure()
    .GetLeaderboardEntryAsync(userId, weeklyPeriod, LeaderboardType.Weekly, Match.Any<CancellationToken>())
    .Returns(Task.FromResult<LeaderboardEntry?>(weeklyEntry));
leaderboardRepository.Configure()
    .GetLeaderboardEntryAsync(userId, LeaderboardPeriods.AllTime, LeaderboardType.AllTime, Match.Any<CancellationToken>())
    .Returns(Task.FromResult<LeaderboardEntry?>(allTimeEntry));

await sut.GetLeaderboardEntryAsync(userId, weeklyPeriod, LeaderboardType.Weekly, ct);   // -> weeklyEntry
await sut.GetLeaderboardEntryAsync(userId, LeaderboardPeriods.AllTime, LeaderboardType.AllTime, ct); // -> allTimeEntry
```

Both configurations stay simultaneously active — neither call site's
`Configure()` discards the other. Override idiom, for the evidenced-safe
generalization noted in Decision Drivers:

```csharp
repo.Configure().Get(Match.Any<int>()).Returns(defaultValue);
repo.Configure().Get(42).Returns(specialValue);
// last matching registration wins: Get(42) -> specialValue, Get(anything else) -> defaultValue
```

### Generated shape (plain matching-eligible member, ADR-0048)

Replaces today's single-slot fields:

```csharp
internal sealed class __Member_Entry
{
    internal global::Compono.Match<TParam1>? Matcher_p1;
    internal global::Compono.Match<TParam2>? Matcher_p2;
    internal global::Compono.ReturnConfig<TSlot> Config;
}
internal readonly List<__Member_Entry> __Member_entries = [];
internal readonly List<(TParam1, TParam2)> __Member_calls = [];   // unchanged shape, shared across all entries
internal readonly object __Member_lock = new();
```

`Configure(Match<TParam1> p1, Match<TParam2> p2)`:

```csharp
lock (__Member_lock)
{
    var entry = new __Member_Entry { Matcher_p1 = p1, Matcher_p2 = p2 };
    __Member_entries.Add(entry);
    return new global::Compono.ReturnConfigBuilder<TSlot>(ref entry.Config);
}
```

Dispatch:

```csharp
lock (__Member_lock)
{
    __Member_calls.Add((arg1, arg2));
    for (int i = __Member_entries.Count - 1; i >= 0; i--)
    {
        var entry = __Member_entries[i];
        if ((entry.Matcher_p1 is not { } m1 || m1.Matches(arg1)) &&
            (entry.Matcher_p2 is not { } m2 || m2.Matches(arg2)))
        {
            if (entry.Config.HasConfiguredException) throw entry.Config.ConfiguredException;
            if (entry.Config.HasConfiguredValue) return entry.Config.ConfiguredValue;
        }
    }
    // fall through to ADR-0045's existing default/throw rule, unchanged
}
```

### Generated shape (ADR-0049 closed-instantiation member)

`__Member_State<T>` (nested inside the bucket, otherwise identical in
spirit to the plain shape above):

```csharp
internal sealed class __Member_State<T> where T : class
{
    internal sealed class Entry
    {
        internal global::Compono.Match<TParam>? Matcher_p;
        internal global::Compono.ReturnConfig<TSlot> Config;
    }
    internal readonly List<Entry> Entries = [];
    internal readonly List<(TParam,)> Calls = [];
    internal readonly object Lock = new();
}
```

`Configure<T>()`/dispatch follow the identical append/reverse-scan pattern,
scoped to whichever `__Member_State<T>` the bucket lookup resolves to —
each closed `T`'s entry list is completely independent of every other
closed `T`'s, exactly as ADR-0049 already guarantees for single-slot state
today; this ADR only changes what lives inside one bucket, not how buckets
themselves are selected or isolated.

### Compatibility (v1/v2 zero-argument-independent shape)

The plain, argument-independent `Configure()` overload (ADR-0043/0044,
non-matching-eligible members) is **unaffected by this ADR** — those
members were never given matcher fields in the first place, so there is no
overwrite behavior to correct there; they keep their existing single
`ReturnConfig<TSlot>` field unchanged.

For matching-eligible members' own zero-argument `Configure()`
compatibility overload (the one that previously had to explicitly null out
prior matchers to simulate "second call overwrites first," per ADR-0048's
implementation): this becomes **simpler**, not more complex, under the new
design — a zero-arg `Configure()` call just appends its own
always-matching entry (all matcher fields `null`, which the existing
`entry.Matcher_p is not { } m || m.Matches(arg)` check already treats as
"always matches"), and it naturally wins by recency like any other entry.
No special-casing needed to "simulate" overwrite semantics, because the
list already provides them for free through recency-ordering.

**Implementation gotcha, recorded because it was a real regression during
the spike, not a hypothetical:** the same zero-arg overload's `Verify()`
counterpart previously read a now-removed single field's
`ConfiguredCallCount`. It must instead read the shared call-log list's
`Count` (the same value, already lock-guarded) — missing this broke 2 of 18
existing `TestDoubleVerificationExecutionTests` during the spike until
corrected. Flagged explicitly for whoever implements this ADR for real: the
existing `TestDoubleVerificationExecutionTests` suite is the right
regression gate for this exact mistake.

### Positive Consequences

- Fixes a live, currently-undetected correctness bug
  (`LeaderboardServiceTests`'s false-passing tests), not just an
  expressiveness gap.
- Closes all three real trivia-platform occurrences with the identical
  mechanism — no per-occurrence special-casing.
- `MultiStubLeaderboardRepository` (hand-written fake) and
  `CachedLeaderboardRepositoryTests` (NSubstitute fallback) both become
  unnecessary — both can migrate to ordinary `.Configure()` calls.
- Composes with ADR-0049 with zero new machinery, closing an inconsistency
  that would otherwise have opened between the two eligible-member shapes.
- `ReturnConfigBuilder<T>`, `ReturnConfig<T>`, `Match<T>`, `CallVerifier`
  all reused completely unchanged — no new runtime types, no new
  exception type, no new fallback rule.
- The single-`Configure()`-per-member case (the overwhelming majority of
  real usage, per the trivia-platform-wide scan in
  `docs/research/0008-...md` §1 — exactly one file in the entire repo has
  same-member/same-test-method duplication outside the three occurrences
  already named) is behaviorally unchanged for consumers.

### Negative Consequences

- **One intentional pre-1.0 semantic correction**: a second `Configure()`
  call on the same member no longer silently discards the first — it
  accumulates as a second, higher-priority entry instead. No consumer
  source change is required either way, but any consumer code that
  currently relies on the old overwrite behavior to reset a member's
  configuration mid-test (not observed anywhere in trivia-platform, but not
  provably absent everywhere) would see a behavior change. Mitigated by
  loud release-note documentation, consistent with this repo's existing
  pre-1.0-correction precedent (ADR-0049's own Negative Consequences
  section for its own new state shape).
- One small heap allocation (`Entry`) per `Configure()` call, versus zero
  before. Judged acceptable per explicit product direction — `Configure()`
  is test-setup code, not a hot invocation path; dispatch itself (the
  actual hot path) does not allocate.
- Slightly more generated surface per matching-eligible member (an `Entry`
  class plus a `List<Entry>` field, versus two flat fields) — same
  trade-off ADR-0049 already made and justified (complexity lives in
  generator output, invisible to whoever writes `Configure().Returns(...)`).
- `TestDoubleAnalyzer.cs`'s name-collision reservation pool needs to learn
  the two new derived names (`{Field}_Entry`, `{Field}_entries`) — purely
  mechanical, same pattern as its existing `_calls`/`_lock`/`_State`/
  `_buckets` reservations, not a design risk, but a real implementation
  task, not automatic.

## Links

- [ADR-0048](0048-testdoubles-argument-matching-and-call-verification.md) —
  the "Considered Options" rejection this ADR formally amends and
  supersedes for the specific evidence above (disjoint literal-argument
  multi-entry); every other ADR-0048 decision is unaffected.
- [ADR-0049](0049-testdoubles-generic-return-closed-instantiation-configuration.md) —
  the per-closed-`T` bucket precedent this ADR's storage design
  generalizes, and whose own nested state now adopts the same multi-entry
  shape rather than staying inconsistently single-slot.
- [ADR-0045](0045-testdoubles-configuration-required-members.md) — the
  unconfigured/no-match fallback rule this ADR reuses unchanged.
- [ADR-0043](0043-compono-generated-test-doubles-design.md) —
  `ReturnConfig<T>`/`ReturnConfigBuilder<T>`, both reused completely
  unchanged by this ADR.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction discipline this ADR is a direct application
  of, including why matcher-specificity ranking and sequential/callback
  returns are explicitly out of scope.
- `docs/research/0008-trivia-platform-multi-entry-testdoubles-dogfood.md` —
  the full evidence inventory, spike results, and local consumer-validation
  workflow this ADR is drawn from.
- `scripts/dogfood-validate.sh` — the consumer-validation script that must
  pass (Compono tests green **and** trivia-platform's full suite green
  against freshly-packed local packages) before and after implementing this
  ADR, per the standing development rule this dogfood pass established.
