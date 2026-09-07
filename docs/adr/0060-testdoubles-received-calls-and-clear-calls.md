# [ADR-0060] Compono.TestDoubles: `ReceivedCalls()` Retrospective Inspection and `ClearCalls()`

**Status:** Accepted

**Date:** 2026-09-06

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[RESEARCH-0025](../research/0025-compono-testdoubles-1.1-research.md)
identified argument capture/call inspection as the strongest remaining
`Compono.TestDoubles` 1.1 candidate, and
[RESEARCH-0026](../research/0026-compono-testdoubles-call-capture-design-investigation.md)
is the deep design pass behind this ADR. Its headline finding reframes the
problem: the per-member call log this investigation set out to design
**already exists and is already paid for at runtime**.

[ADR-0048](0048-testdoubles-argument-matching-and-call-verification.md)
added argument-filtered call verification for a defined eligible-member
set (single-overload members meeting five conditions — see "Eligibility
scope" below). To support `Verify().Member(Match.Is<T>(...)).Once()`, the
generator already emits, per eligible member
(`src/Compono.Generators/Templates/TestDouble.scriban:128-129`):

```csharp
internal readonly System.Collections.Generic.List<(T1, T2, ...)> {{ member.field_name }}_calls = [];
internal readonly object {{ member.field_name }}_lock = new();
```

Every dispatch to an eligible member appends its real argument values to
this list under `{{ member.field_name }}_lock`
(`TestDouble.scriban:275-277`, `:315-320`). `Verify().Member(...)`
acquires the same lock, iterates `_calls`, counts matches, and returns a
`Compono.CallVerifier` (`TestDouble.scriban:610-655`) — then the matched
data is discarded. **The list is retained for the double's entire
lifetime regardless of whether any test ever calls `Verify()` with an
argument matcher at all** — this ADR is about exposing that already-paid
storage as a first-class public capability, not about inventing a second
capture mechanism.

Argument-filtered `Verify()` reuses argument values by C# reference/value
semantics as received at the call site — no cloning. The one thing
consumers cannot currently do is inspect those retained values directly,
independent of a count assertion, or ask for them across multiple calls in
one strongly-typed shape.

Separately, a consumer sharing one double across multiple phases of a
test (configure → exercise phase 1 → verify → **clear observation
history** → exercise phase 2 → verify) has no first-class way to reset
call counts/history while keeping configured behavior. `ReturnConfig<T>`
(`src/Compono/ReturnConfig.cs`) already demonstrates the "clear this,
preserve that" pattern for a different pair of concerns —
`ClearConfiguredResponse()` (line 76) clears `Value`/`Exception`/`Sequence`
state *without* touching `CallCount`. This ADR needs the mirror
operation: clear `CallCount`/call history without touching configured
`Value`/`Exception`/`Sequence` state.

This ADR treats `ReceivedCalls()` and `ClearCalls()` as new public
consumer capabilities with their own API and lifetime semantics, and
references ADR-0048 for the underlying storage rather than restating it —
it is deliberately not an ADR-0048 amendment, because these are genuinely
new capabilities layered on that infrastructure, not a correction to
ADR-0048's own decision.

## Decision Drivers

- Every ADR-0043/ADR-0044/ADR-0048 driver still applies: no
  cross-generator dependency, no reflection, Native AOT/trimming safety,
  explicit two-gate activation, deterministic generated code, minimal
  runtime overhead.
- `Compono` values: low runtime overhead, minimal unnecessary allocations,
  deterministic generated code, explicit behavior, predictable
  concurrency, AOT/trimming compatibility ([RESEARCH-0026](../research/0026-compono-testdoubles-call-capture-design-investigation.md)'s
  measured evidence that the existing ADR-0048 storage already carries
  this cost for eligible members — this ADR adds no new per-call cost).
- Consumer expectations set by NSubstitute (`ReceivedCalls()`,
  `ClearReceivedCalls()`) and by Rocks' own retrospective-inspection
  surface, without copying either API for familiarity alone.
- `skills/compono/references/testdoubles.md`'s existing "matching is not
  capture" boundary (lines 520-540) is a real, documented limitation this
  ADR closes for the eligible-member subset — the skill content asserting
  this is unsupported becomes actively wrong the moment this ships and
  must be corrected as part of completion, not left to drift.
- A useful, conventional testing capability does not require a prior
  filed issue or dogfooding incident — real consumer value, clean package
  fit, and a sound design/cost profile are sufficient justification
  (established direction for this round of 1.1 scoping).

## Considered Options — capture/inspection model

RESEARCH-0026 compared four architectural models; this ADR does not
reopen model A/B (see that research doc for the full comparison) because
the eligibility-scoped answer is already resolved by ADR-0048's existing
storage, but records the option set for completeness:

1. **Always-recorded invocation history for every eligible member**
   (already true today, per ADR-0048 — no new decision needed here; this
   ADR only decides whether to *expose* it).
2. **Opt-in capture**, enabled per-member or per-double before exercising
   the SUT.
3. **Callback/observer-based capture** — already shipped as
   `ReturnsCallback` (ADR-0053).
4. **Generated received-call records** — a strongly typed accessor over
   the existing list.

**Chosen: Option 4, layered on the Option 1 storage that already exists.**
Option 2 is rejected: the storage already exists unconditionally for
every ADR-0048-eligible member (it has to, to support argument-filtered
`Verify()`), so an opt-in toggle would not reduce any real cost — it would
only add a branch and a "why are there no calls?" surprise for a consumer
who forgot to opt in, for zero performance benefit. Option 3
(`ReturnsCallback`) remains the answer for single-value, exercise-time
capture and is explicitly preserved as a distinct, complementary
mechanism — not superseded by this ADR. Both NSubstitute (`Arg.Do`) and
Rocks position their primary capture mechanism as callback-based too,
which is independent validation that a callback and a retrospective
history are two different, both-legitimate answers to two different
consumer scenarios (capture-as-it-happens vs. inspect-after-the-fact),
not competing designs where one should replace the other.

## Considered Options — bridge name

1. `ReceivedCalls()` — mirrors `Configure()`/`Verify()` as a third
   generator-emitted downcast bridge; mirrors NSubstitute's own
   `ReceivedCalls()` vocabulary, which lowers the migration-recognition
   cost this repo's skill/migration docs already care about.
2. `Calls()` — shorter, but ambiguous next to `Verify().Member().Exactly(n)`
   and the internal generated `_calls` field naming already in use
   internally; risks reading as "make a call" rather than "the calls that
   were made."

**Chosen: Option 1, `ReceivedCalls()`.** Discoverability and consistency
with `Configure()`/`Verify()`'s existing two-bridge naming pattern outrank
brevity, and the name change from the internal `_calls` field naming has
no consumer-facing consequence (that field is `internal`, never public).

```csharp
repository.ReceivedCalls().Save; // property or method per member, per "returned call type" below
```

## Considered Options — returned call type

1. **Snapshot of the existing unnamed tuple** (`(T1, T2, ...)`, or bare
   `T` for a single-parameter member) — this is the literal type already
   stored in `_calls` (`TestDoubleEmitter.cs:219-224`: `CallLogTypeText`
   is exactly this shape). Zero new generated type.
2. **A generated named record per eligible member**, with real parameter
   names (already tracked per-parameter in the same emitter model used to
   build matcher locals — `TestDoubleEmitter.cs:198-216` — as
   `EscapedName`/`OriginalName`), following the same per-interface
   hash-suffixed naming convention Requirement 3 already uses successfully
   for `<Hash>_DoubleVerifier` (`ADR-0044` line 331).

**Chosen: Option 2.** Confirmed directly in `TestDoubleEmitter.cs`: the
existing tuple has **no named elements** — `CallLogConstructExpression`
builds a positional `(a, b, c)`, and internal consumption reads it back
positionally (`CallLogAccessExpression`: `call.Item1`, `call.Item2`, ...,
`TestDoubleEmitter.cs:213`). Exposing that type verbatim to consumers
would mean `call.Item1`/`call.Item2` at every call site — a real
usability regression relative to the member's actual parameter names,
which the generator already has on hand and simply never threaded into
the tuple's shape (there was no need to, for internal positional matching
use). A generated named record correcting this needs no new metadata —
the parameter names already exist in the emitter's per-member model,
they've simply never been used to name anything before this ADR. No fresh
compiler spike is required to de-risk the type's *naming scheme*: it
reuses the exact per-interface hash-suffix mechanism Requirement 3 already
proved compiles cleanly for `<Hash>_DoubleVerifier`
(`docs/adr/0044-...md` lines 322-334) — this is the same generator
naming machinery applied to one more generated type, not new territory.
A small implementation-time spike is still warranted to confirm record
vs. `readonly record struct` (favoring the latter for a zero-allocation,
value-type snapshot) and to confirm no collision with a same-named
interface member (the existing hash-suffix scheme already handles this
class of collision generically, but the specific record-type case should
be spiked before implementation, not assumed).

## Considered Options — snapshot semantics

**Decision:** `ReceivedCalls().Member` acquires the existing
ADR-0048 per-member lock (`{{ member.field_name }}_lock`), copies the
current `_calls` entries into a new array/`IReadOnlyList<T>`, releases the
lock, and returns the copy. This is a direct reuse of the exact
lock-then-copy pattern `Verify().Member(...)`'s own scan already performs
(`TestDouble.scriban:610-655`) — no new synchronization primitive, no new
concurrency model.

**Ordering:** sequential calls preserve append order (the list is
appended-to in dispatch order under the lock). Under genuinely concurrent
invocations, ordering reflects whichever call acquired
`{{ member.field_name }}_lock` first to append its entry — this is the
same ordering guarantee (and the same lack of a stronger one) argument-
filtered `Verify()` already implicitly relies on today; this ADR adds no
new guarantee and documents none beyond what already holds. No timestamps,
no global sequence IDs — there is no concrete consumer scenario evidenced
that needs either, and adding them would be exactly the kind of
unrequested generality this round of 1.1 scoping is explicit about
avoiding.

**Capture semantics — explicit, no deep copy.** The retained call record
stores the same C# value/reference the caller passed:

- reference types, mutable objects, arrays, collections: the **same
  reference** is retained. If the caller mutates that object after the
  call returns, a later `ReceivedCalls()` inspection observes the mutated
  state, not a snapshot from invocation time.
- structs, `CancellationToken`, nullable value types: ordinary C#
  value-copy semantics — the record holds an independent copy of the
  struct's field values as they were at the call site.

No serialization, cloning, reflection, or general deep-copy machinery is
introduced. This is a direct, explicit consequence of the existing
ADR-0048 storage (it already retains arguments this way for matching
purposes) — this ADR does not change that behavior, only documents and
exposes it. This must be stated plainly in `Compono.TestDoubles`
documentation and the skill (see "Documentation requirements" below); a
consumer capturing a mutable argument and mutating it before inspection is
a real, foreseeable footgun that existing NSubstitute users likely already
have intuitions about (NSubstitute's `Arg.Do`/received-calls storage has
the identical reference-retention behavior), so this is consistent with
prior art, not a novel risk.

## Considered Options — eligibility scope

**Decision: `ReceivedCalls()` is available for exactly the ADR-0048
eligible-member set, unchanged, for 1.1.** That set
(`skills/compono/references/testdoubles.md:373-379`) requires a member to:
be the only overload of its name in the interface; have no real parameter
referencing the member's own open generic type parameter; have no
ref-like-typed parameter; have no derived internal field name colliding
with another member's; and not be a one-parameter `Equals`.

RESEARCH-0026 correctly flagged that this exclusion set is not
monolithic — some conditions are genuinely fundamental to *retrospective
capture* (a ref-like parameter, e.g. `Span<T>`, cannot be stored in a
`List<T>` element at all — a fundamental storage constraint, not specific
to matching), while others are specific to *argument matching's*
mechanism, not to capture itself (the single-overload restriction exists
because `Match<T>`-based configuration needs an unambiguous per-overload
discriminator name — ADR-0044 Requirement 1/ADR-0048's own
overload-discriminator interaction section — a constraint that plausibly
does not apply the same way to a purely retrospective, no-configuration-time
accessor). This ADR deliberately does **not** resolve that distinction
now: expanding eligibility (e.g. supporting overloaded members for
`ReceivedCalls()` even where argument-matched `Verify()`/`Configure()`
stays excluded) is real, plausible future work, but doing it in the same
pass as "expose the storage that already exists" would turn a
narrow, low-risk change into a second overload-eligibility redesign,
which is explicitly out of scope for this round. **For 1.1: reuse
ADR-0048's eligible set exactly, unchanged, unexpanded.** The
fundamental-vs-matching-specific distinction is recorded here as a named,
credible future extension, not as an open question this ADR needs to
answer to ship.

## Considered Options — `ClearCalls()` semantics

Adopted core principle (RESEARCH-0026, confirmed against real
`ReturnConfig<T>` field semantics): **`ClearCalls()` clears
observation/verification history and preserves configured behavior.**
Verified field-by-field against `src/Compono/ReturnConfig.cs`:

| State | Cleared? | Why |
|---|---|---|
| `CallCount` | **Cleared** (reset to 0) | Observation history — this is exactly what "how many times was this called" means. |
| ADR-0048 `_calls` list (captured arguments) | **Cleared** | Observation history — the retrospective record of what happened. |
| `Value`/`HasValue` (`Returns`) | Preserved | Configured behavior — what happens on the *next* call, unrelated to what already happened. |
| `Exception` (`Throws`) | Preserved | Configured behavior. |
| `ReturnsCallback`'s callback field | Preserved | Configured behavior. |
| `Sequence` (`ReturnsSequence` array) | Preserved | Configured behavior — the array itself is immutable once set (`ReturnConfig.cs:22-24`), unrelated to observation. |
| `SequenceOrdinal` | **Preserved — does not rewind.** | This is the one field that could plausibly be argued either way; resolved below. |
| Argument-matcher/multi-entry configuration (`Entries`, ADR-0050) | Preserved | Configured behavior, same category as `Value`/`Exception`. |
| Generic closed-instantiation configuration (ADR-0049 buckets) | Preserved | Configured behavior. |
| Property backing/configured state | Preserved | Configured behavior — a property's `Configure().Prop().Returns(x)` is arrange-phase state, not observation. |

**`SequenceOrdinal` does not rewind — confirmed, not merely assumed.**
Example: a member is configured with `ReturnsSequence(A, B, C)`; two
invocations return `A` then `B`; `ClearCalls()` runs; the next invocation
returns `C`, not `A`. Rationale: `SequenceOrdinal` is runtime *progress
through configured behavior* — the same category as `Value`/`Exception`
being "what will happen next" — not a record of *what was observed*.
Rewinding it on `ClearCalls()` would silently re-run part of a configured
sequence a consumer already exercised and moved past, which is a stronger
and more surprising side effect than a call-history reset should ever
have. This also keeps `ClearCalls()` cheap and simple: it only ever
zeroes/clears fields whose sole purpose is observation, never touches a
field that participates in *what response comes next*.

This principle is what keeps `ClearCalls()` from becoming `Reset()` — a
`Reset()`-shaped operation would need to also decide what happens to every
row in the table above, and getting that decision wrong (or leaving it
ambiguous) is exactly the failure mode a precise, narrow `ClearCalls()`
name and contract avoids.

### `ClearCalls()` receiver and scope

**Decision: `ClearCalls()` is a direct operation on the double itself,
clearing every member's observation state at once — no per-member
granularity in 1.1.**

```csharp
repository.ClearCalls();
```

Considered and rejected:

- `repository.Verify().ClearCalls()` — rejected: `Verify()`'s wrapper
  type exists specifically to host assertion terminals
  (`Once`/`Never`/`Exactly`/now `AtLeast`/`AtMost`); attaching a mutating
  operation to the same receiver blurs "assert" and "mutate" in one type,
  which is exactly the responsibility-blur this ADR's "API intent"
  section (below) is designed to avoid.
- `repository.ReceivedCalls().Clear()` — rejected for the same reason in
  the other direction: `ReceivedCalls()` is an inspection bridge; a
  `.Clear()` sitting on it reads as "clear the snapshot I just returned,"
  not "clear the double's history," which is a real and likely-common
  misreading given `ReceivedCalls()`'s snapshot semantics above.
- **Per-member `ClearCalls()`** (e.g. `repository.ClearCalls().Save()`,
  mirroring `Configure()`/`Verify()`'s per-member shape) — deferred, not
  rejected outright. The evidenced consumer scenario in RESEARCH-0026 and
  this ADR's own Context is phase-based reuse of a *whole* double across
  test phases, not selectively forgetting one member's history while
  keeping another's. Per-member granularity is real, plausible future
  work if a concrete scenario evidences it, but adding it now with no
  such scenario is exactly the "don't manufacture granularity" instruction
  this round of design work is explicit about. A single, whole-double
  `repository.ClearCalls()` is the smaller, more direct primitive that
  matches the actual evidenced scenario.

`ClearCalls()` applies uniformly across every generated member on the
double, including members that only maintain a scalar `CallCount` and are
outside the ADR-0048 eligible set (a member with no argument-aware call
log still has a `CallCount` worth resetting) — this is a deliberate
asymmetry with `ReceivedCalls()` (eligibility-scoped) and is called out
explicitly so a reader does not assume the two share one eligibility
rule.

### `ClearCalls()` concurrency

`ClearCalls()` must synchronize with the same storage `RecordCall()`/
argument-log-append and `Verify()`/`ReceivedCalls()` already use, per
member:

- **Scalar `CallCount`**: reset via
  `System.Threading.Interlocked.Exchange(ref CallCount, 0)` — the same
  primitive `RecordCall()`'s `Interlocked.Increment` already uses, so a
  concurrent increment and a concurrent clear can never tear the field;
  the only ambiguity is *ordering* (does an in-flight call's increment
  land before or after the clear), which is the same kind of ordering
  ambiguity that already exists between any two concurrent `RecordCall()`
  calls today — this ADR introduces no new class of race, just one more
  operation subject to the existing one.
- **ADR-0048 `_calls` list**: `ClearCalls()` acquires
  `{{ member.field_name }}_lock`, calls `_calls.Clear()`, releases —
  identical lock discipline to the existing append and the existing
  `Verify()` scan. A call whose dispatch is concurrently appending under
  the same lock either fully lands before or fully lands after the clear;
  there is no torn/partial list state, by construction of the existing
  lock.

**Simplest acceptable semantics, stated explicitly (no stronger promise
than the implementation provides):** a call either lands before or after
`ClearCalls()` from that member's synchronized perspective; there is no
global coordination across members (clearing member A's state has no
ordering relationship to member B's concurrent calls, which is fine — no
evidenced scenario needs cross-member atomicity); no torn state is ever
observable, because every mutation site already holds the relevant lock
or uses the relevant atomic primitive.

### `ClearCalls()` releases retained references

Because `_calls.Clear()` removes every element from the list, any argument
references retained only by that list become eligible for garbage
collection once no other reference exists — `ClearCalls()` is a real,
effective memory-release operation for a double that has captured many
mutable/large arguments over a long-running shared-fixture lifetime, not
merely a logical/observational reset.

## Performance

RESEARCH-0026's measured findings, adopted without modification for the
1.1 eligibility scope:

- Scalar `CallCount`-only recording (members outside the ADR-0048
  eligible set) remains effectively zero-allocation, unaffected by
  anything in this ADR.
- ADR-0048-eligible members already pay the unbounded `List<(args...)>`
  history cost today, unconditionally — `ReceivedCalls()` exposing that
  list adds no new per-call cost for the 1.1 scope; it is a pure read-side
  addition.
- Unbounded growth only becomes measurably expensive at call volumes far
  beyond realistic single-test usage (RESEARCH-0026's spike found ~80
  bytes/call and a ~20x slowdown only material at multi-million-call
  scale, driven by list-growth reallocation, not the per-call write).
- **No default cap, no ring buffer, no configuration knob for maximum
  captured calls in 1.1.** A bounded/ring-buffer default would introduce
  a genuinely surprising truncation semantic ("why did my 501st call
  disappear") for a cost that is not evidenced as a real problem at any
  realistic test scale. `ClearCalls()` is the correct, explicit answer to
  unbounded growth over a long-lived double, not an implicit cap.

## Public/Generated API Compatibility

- **Source compatibility:** additive only. `ReceivedCalls()` and
  `ClearCalls()` are new generated extension methods; no existing
  generated signature changes.
- **Binary compatibility:** additive only in core `Compono` (no new
  members on existing public types other than the ADR-0044-amendment's
  `CallVerifier` additions, tracked separately) and additive-only in
  generated per-interface code (new extension classes/methods, existing
  ones unchanged).
- **Generated-source compatibility:** every existing generated file's
  content is byte-for-byte unaffected for a member outside the newly
  eligible set; for an ADR-0048-eligible member, the generator emits
  additional source (the new record type and the two bridges) alongside
  the unchanged existing dispatch/`Verify()` code — no existing emitted
  line changes.
- **Analyzer/generator determinism:** the new record type's name is
  derived deterministically from the same per-interface hash-suffix
  scheme already used for `<Hash>_DoubleVerifier` — same determinism
  guarantee, no new nondeterminism source.
- **Native AOT/trimming:** no reflection, no dynamic code generation,
  identical trim-safety profile to the existing ADR-0048 storage this ADR
  exposes — validated the same way existing TestDoubles AOT smoke
  coverage already validates `Verify()`.
- **SemVer:** purely additive; safe for a 1.1 minor release under
  Compono's post-1.0 compatibility posture.

## Decision Outcome

**Chosen:** ship `ReceivedCalls()` (generated named-record accessor, over
ADR-0048's existing eligible-member set, snapshot-under-existing-lock
semantics, reference-retention capture semantics documented explicitly)
and `ClearCalls()` (whole-double, clears `CallCount` + captured argument
history only, preserves all configured behavior including sequence
ordinal progress) as two new `Compono.TestDoubles` public capabilities for
1.1.

### Positive Consequences

- Closes a real, named, currently-documented-as-unsupported gap
  (`skills/compono/references/testdoubles.md`'s "matching is not capture"
  section) for the eligible-member subset, without inventing new runtime
  machinery — the storage already exists and is already paid for.
- Keeps `Configure()`/arrange, `Verify()`/assert, and `ReceivedCalls()`/
  inspect as three clearly separated concerns, avoiding the responsibility
  blur of attaching captured data to `CallVerifier`.
- `ClearCalls()` closes a real phase-based-reuse gap with a narrow,
  precisely specified contract that cannot silently degrade into a
  `Reset()`.
- No measurable new runtime cost for the 1.1 scope; no new concurrency
  model introduced beyond reuse of ADR-0048's existing lock discipline.

### Negative Consequences

- `ReceivedCalls()` is scoped narrower than "every generated member" —
  consumers with an overloaded member of interest cannot use it there yet.
  Mitigation: this is the same eligible set ADR-0048's argument-matched
  `Verify()`/`Configure()` already impose, so it introduces no new,
  unfamiliar boundary — a consumer already living within ADR-0048's
  eligibility rules gains a capability, rather than a previously-unified
  surface fragmenting further.
- Reference-retention (not deep-copy) capture semantics are a real,
  documented footgun for mutable arguments. Mitigation: explicit
  documentation and skill coverage (below), consistent with how
  NSubstitute's own equivalent behavior is generally understood by
  consumers migrating from it.
- The generated named-record type adds one more generated type per
  eligible member to the emitted source — a real, if small, generated-code
  volume increase. Mitigation: this is bounded by the existing
  ADR-0048-eligible set, and the record type is a small, deterministic,
  zero-allocation-at-use (`readonly record struct`, pending implementation
  spike) shape.

## Documentation requirements

Named explicitly, not left as generic "update docs":

- `docs/packages/compono-testdoubles.md` (or whichever doc currently
  catalogs `Compono.TestDoubles` public capabilities) — add
  `ReceivedCalls()`/`ClearCalls()` alongside `Configure()`/`Verify()`.
- XML docs on the new generated bridges, the generated record type(s),
  and any new public core-`Compono` infrastructure type backing the
  snapshot (if one is introduced at implementation time), matching this
  repo's existing XML-doc density/style on `CallVerifier`/`ReturnConfig<T>`.
- Package samples/examples demonstrating: basic call inspection; multiple
  received calls across several invocations; `ClearCalls()` used between
  two phases of one test; the reference-retention footgun for a mutable
  argument (a deliberate "here's what NOT to assume" example).
- Migration guidance: where existing NSubstitute-migration material
  discusses `Received()`/`ReceivedCalls()`/`ClearReceivedCalls()`
  equivalence, update it to reflect that `Compono.TestDoubles` now covers
  this for the eligible-member set, with an explicit note on the
  eligibility boundary and reference-retention semantics.

## Skill requirements

`ReceivedCalls()`/`ClearCalls()` are public generated consumer
capabilities — the skill **must** be updated, not left to drift:

- `skills/compono/SKILL.md` — lines 101-105 currently state Compono
  "never" exposes `ReceivedCalls()` and that "true argument capture...
  [is] not supported"; both claims become false for the eligible-member
  set and must be corrected precisely (not blanket-reversed — the
  eligibility boundary must survive into the corrected text).
- `skills/compono/references/testdoubles.md` — lines 362-363 ("Still
  deliberately minimal... no `ReceivedCalls()`-style enumeration") and
  the entire "The #1 AutoFixture/NSubstitute-habit trap: matching is not
  capture" section (lines 520-540) must be revised to state precisely
  what is now supported (`ReceivedCalls()` for ADR-0048-eligible members)
  and what remains genuinely unsupported (call-order verification, strict
  mode, overloaded-member capture, classes/delegates/indexers/events).
  This is the single most consequential skill edit this ADR requires —
  it is the section most likely to actively mislead a consumer or an
  agent using the skill if left stale.
- Any capability-matrix-style table across `skills/compono/` asserting
  argument capture is unsupported must be found (grep for "capture",
  "ReceivedCalls", "matching is not capture") and corrected.

Because the skill changes, `skills/compono/evals/evals.json` **must** be
updated:

- Add/revise eval(s) exercising: discovering and correctly using
  `ReceivedCalls()` for an eligible member; correctly identifying the
  matching-vs-capture boundary post-change (i.e., the eval suite must not
  keep testing that the skill says capture is unsupported); `ClearCalls()`
  preserving configured behavior including sequence-ordinal progress; not
  hallucinating `ReceivedCalls()` support for an overloaded member (a
  negative eval, given the deliberately narrow eligibility scope);
  migration guidance mentioning the NSubstitute-equivalence where
  relevant.
- Run the established skill-evaluation workflow before treating the
  skill update as complete: snapshot/baseline the pre-change skill (or use
  this repo's established immutable-baseline mechanism if one already
  exists); update the skill; update `evals.json`; run the updated skill
  against the relevant evals in a clean agent context; run the
  baseline/old skill against the same evals in a clean agent context;
  compare results; inspect any regression; keep generated eval workspaces
  out of source control.

## ADR completion criteria

Implementation of this ADR is not complete until all of the following are
done together, not left to drift apart:

- Generator/runtime changes: the new bridge(s), the generated record
  type(s), `ClearCalls()`'s whole-double clearing logic, and (if needed)
  a new public core-`Compono` primitive analogous to
  `ReturnConfig<T>.ClearConfiguredResponse()` for the `CallCount`/history
  side.
- Tests: `Compono.TestDoubles.Tests` coverage for `ReceivedCalls()`
  (single call, multiple calls, ordering, reference-retention semantics
  for a mutable argument), `ClearCalls()` (all state-preservation/clearing
  rules in the table above, explicitly including the sequence-ordinal
  non-rewind case), and concurrency (concurrent invocation racing a
  `ClearCalls()` call, no torn state).
- AOT/trimming smoke coverage extended to exercise the new surface.
- Public API surface diff review (additive-only).
- Documentation changes listed above.
- Skill changes listed above, including the mandatory baseline-vs-updated
  skill evaluation comparison.
- Dogfooding via `scripts/dogfood-validate.sh` once implementation reaches
  the dogfooding stage — not an ad hoc validation process.

## Links

- [ADR-0048](0048-testdoubles-argument-matching-and-call-verification.md) —
  origin of the argument-aware call-log storage this ADR exposes; not
  restated in full here.
- [ADR-0044](0044-compono-testdoubles-v2-overloads-generics-verification.md) —
  Requirement 3's original `Verify()` bridge design and per-overload
  discriminator mechanism this ADR's `ReceivedCalls()` bridge follows the
  same pattern of; Amendment 22 (`CallVerifier.AtLeast`/`AtMost`), decided
  alongside this ADR, is a related but independent change.
- [ADR-0053](0053-testdoubles-invocation-aware-callback-responses.md) —
  `ReturnsCallback`, preserved as the distinct, complementary
  exercise-time capture mechanism this ADR does not replace.
- [ADR-0054](0054-testdoubles-sequential-call-count-based-responses.md) —
  the sequence/ordinal mechanism whose non-rewind behavior under
  `ClearCalls()` this ADR specifies.
- [RESEARCH-0025](../research/0025-compono-testdoubles-1.1-research.md),
  [RESEARCH-0026](../research/0026-compono-testdoubles-call-capture-design-investigation.md) —
  the research this ADR's decisions are drawn from.
- `src/Compono.Generators/Templates/TestDouble.scriban`,
  `src/Compono.Generators/Emitters/TestDoubleEmitter.cs`,
  `src/Compono/ReturnConfig.cs` — the real generator/runtime source every
  claim in this ADR was checked against.
- `skills/compono/SKILL.md`, `skills/compono/references/testdoubles.md`,
  `skills/compono/evals/evals.json` — updated at implementation time per
  "Skill requirements" above, not by this ADR directly.
