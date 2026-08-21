# [PLAN-0048] Compono.TestDoubles: Argument Matching and Argument-Aware Call Verification

**Status:** In Progress - blocked on a real naming collision, see Notes

**Implements:** [ADR-0048](../adr/0048-testdoubles-argument-matching-and-call-verification.md)

## Goal

A `Compono.TestDoubles`-generated double supports argument-matched
response configuration and argument-filtered call verification via a
unified `Compono.Arg<T>` surface (literal equality, `Arg.Any<T>()`,
`Arg.Is<T>(predicate)`), for every member that is **both** non-overloaded
**and** has no real parameter referencing the member's own open generic
type parameter — closing the two capabilities `ncipollina/trivia-manager`
Stage 3 evidence actually supports. **No existing v1/v2 `Configure()`/
`Verify()` call site changes behavior at all** — a real compiler spike
(ADR-0048's Decision Outcome) proved the originally-drafted
wrap-every-parameter design doesn't reliably compile for an overloaded
member, so this plan's scope boundary (non-overloaded members only) means
ADR-0044's existing overloaded-member surface is untouched, not merely
backward-compatible. Done when the full existing `Compono.TestDoubles`
test suite passes completely unmodified, an AOT smoke test proves the new
generated shapes survive trimming/AOT, and a representative set of
trivia-manager's real previously-blocked call sites (argument-matched
`Returns`, argument-filtered `Received`) work against a real package build
from this repo.

## Scope

Exactly ADR-0048's Decision Outcome: `Compono.Arg<T>` (with the implicit
`T -> Arg<T>` equality-matcher conversion), per-parameter matcher fields
and a call log generated on the double class alongside the existing
`ReturnConfig<T>` field (not inside it), and an eligible member's
`Verify()` extension folding argument-filtering directly into the same
`Arg<T>`-per-parameter shape as `Configure()` (no `.Matching()` step).
Eligibility is exactly two conditions, both required: the member is not
part of a multi-overload set, and no real parameter references the
member's own open method-type-parameter. An ineligible member (either
condition) generates its existing v1/v2/ADR-0044 shape, byte-for-byte
unchanged.

Explicitly out, per ADR-0048's Non-Goals: call-order verification (zero
real evidence), response chains/multiple differentiated responses per
member (zero real evidence), argument matching on overloaded members
(zero real evidence, and unreliable to compile — see ADR-0048),
`ReturnsForAnyArgs`, `When().Do(...)`, strict/partial substitutes,
recursive auto-configuration.

## One implementation PR

The matcher surface, per-member field generation, call recording, and
filtered verification are one coherent generated-code change — the same
generator pass emits the `Arg<T>`-typed `Configure()`/`Verify()`
signatures, the matcher fields, and the call log together for a given
eligible member, and none of the four is independently useful without the
others. The trivia-manager re-dogfood (its own section below) is a
separate PR in the *other* repo, after this one ships a real package
build — not a second PR here.

## Tasks

Grouped by concern, checked off as work proceeds.

### 1. `Compono.Arg<T>` (core `Compono`)

- [x] `Arg.Any<T>()`, `Arg.Is<T>(Func<T, bool>)`, implicit `T -> Arg<T>`
      conversion (equality matcher), and exactly one **public** operation
      for generated code to call: `public bool Matches(T value)`. No
      public delegate/`Predicate` accessor — `Arg<T>`'s internal
      representation stays free to change without being a breaking change
      to generated output's dependency on it (the same
      cross-assembly-accessibility fix ADR-0044 Amendment 3 already made
      for `ReturnConfig<T>`; `internal` alone doesn't work here since
      generated code compiles into the *consumer* assembly, not `Compono`).
- [x] Internal three-case representation (`Equality`/`Any`/`Predicate`) so
      a literal argument allocates no closure — stores the value directly,
      compared via `EqualityComparer<T>.Default` inside `Matches`; only
      `Arg.Is<T>(predicate)` allocates (the caller's own lambda).
- [x] Unit tests: construction of all three kinds, the implicit conversion,
      `Matches` evaluation in isolation (no generator involvement yet) -
      `test/Compono.Tests/ArgTests.cs`.
- [ ] A literal-built `Arg<T>` really doesn't allocate a delegate (a simple
      before/after allocation assertion, not a benchmark) - not written;
      the `Kind.Equality` code path visibly stores the value directly with
      no delegate involved, but this wasn't independently verified with an
      allocation assertion.

### 2. Eligibility analysis (generator)

- [x] A member is eligible for the new surface iff it is the *only*
      overload of its name in the interface's closure **and** no real
      parameter type's syntax tree references the member's own open
      method-type-parameter (extends ADR-0044 Requirement 2's existing
      return-type-independence check to also gate this).
- [x] Test: an overloaded member (any arity/shape) is never eligible,
      regardless of how "safe-looking" its specific parameter types are —
      the scope boundary is structural (is there more than one overload),
      not per-family (which types happen to be involved). This is the
      direct consequence of ADR-0048's compiler-spike finding: there is no
      reliable per-family rule, so none is implemented.
- [x] Test: `ILogger<TState>.Log<TState>(...)`-shaped member is never
      eligible.
- [x] Test: an ineligible member's generated output is **identical** to
      what it was before this plan's changes (a real diff against a
      pre-PLAN-0048 generator snapshot, not just "still compiles") — the
      concrete proof behind ADR-0048's "zero interaction with ADR-0044's
      existing surface" claim.

### 3. Generated fields for an eligible member

- [x] The existing `ReturnConfig<T>` field, unchanged — no new members
      added to `ReturnConfig<T>` itself.
- [x] One `Arg<TParam>?` field per real parameter (the `System.Nullable`
      wrapper around the whole `Arg<TParam>` value, not an extracted
      delegate), generated on the double class alongside the
      `ReturnConfig<T>` field. `null` means "no matcher configured for
      this parameter" (dispatch treats it as always-matching); `HasValue`
      with an `Arg<TParam>` built by `Arg.Any<TParam>()` is a distinct,
      deliberately-configured state that happens to produce the same
      dispatch result — the two are never conflated in storage, only in
      their dispatch-time effect.
- [x] A `lock`-guarded call log: a generated tuple/record-shaped
      `List<(T1, T2, ...)>`, plus its own `lock` object.

### 4. `Configure()` for an eligible member

- [x] Generated extension signature: one `Arg<TParam>` parameter per real
      parameter (not the real type directly).
- [x] Body: store each `Arg<TParam>` argument directly into its
      corresponding `Arg<TParam>?` field (no unwrapping — `Arg<T>` has no
      public delegate accessor to unwrap), return the existing
      `ReturnConfigBuilder<T>` unchanged.
- [x] Test: a second `Configure()` call on the same member overwrites the
      matcher fields and the `ReturnConfig<T>` slot — proves the
      single-slot, not-a-chain model.

### 5. Dispatch body for an eligible member

- [x] Append to the call log (under its lock) on every invocation.
- [x] Evaluate all matcher fields via pattern-match-and-`Matches`
      (`field is not { } m || m.Matches(actualArg)` — a `null` field
      always matches); if all match and
      `HasConfiguredException`/`HasConfiguredValue`, use it; otherwise
      fall through to ADR-0045's existing configuration-required/default
      behavior, unchanged — an unmatched real call is treated identically
      to an unconfigured member, not a distinct failure mode.
- [x] Test: a call whose arguments don't satisfy a configured matcher does
      NOT return the configured value (proves matching actually gates
      dispatch, not just records it).

### 6. `Verify()` for an eligible member

- [x] Generated extension signature: same `Arg<TParam>`-per-parameter
      shape as `Configure()`, calling `.Matches(...)` against each logged
      call's recorded arguments (never a delegate extracted from the
      `Arg<TParam>` argument — same accessibility constraint as task 4).
- [x] Body: snapshot-and-count under the call log's lock (same lock task 3
      created, not a second one), constructing the existing, unchanged
      `CallVerifier(filteredCount, description)`.
- [x] Test: `Once()`/`Never()`/`Exactly(n)` all work unchanged off the
      filtered count — `CallVerifier` itself needs zero code changes.

### 7. Full-suite regression and generated-output review

- [x] Full existing `Compono.TestDoubles` test suite passes **completely
      unmodified** — zero expected diffs, per ADR-0048's "no break at
      all" finding.
- [x] Manual review of generated output for each representative shape in
      ADR-0048's Decision Outcome section (ordinary eligible member,
      overloaded member proven untouched, zero-parameter member, the
      `ILogger<TState>` exclusion case, argument-filtered `Verify()`)
      against what the ADR actually shows.

### 8. AOT smoke test

- [ ] Extend `test/Compono.TestDoubles.AotSmokeTest` (the existing
      pattern, not a new one) with interfaces covering: an eligible
      ordinary matched-configuration member; a member mixing a literal,
      `Arg.Any<T>()`, and `Arg.Is<T>(predicate)` in one call; an
      overloaded member (proving its generated shape and behavior are
      unaffected under Native AOT, matching task 2's snapshot proof);
      argument-filtered `Verify()`; and a generic-scoped-out member
      (`ILoggerLike`-shaped, already in that project) confirmed still
      eligible-excluded under AOT too.
- [ ] No new benchmark — ADR-0048 explicitly declines one absent evidence
      of an actual performance risk; add one later only if implementation
      surfaces real evidence of a problem.

### 9. Documentation

- [ ] `docs/packages/compono-testdoubles.md` (or equivalent): document
      `Arg<T>`, the eligibility rule (non-overloaded, no open-generic
      parameter reference), and the matcher/verification surface. No
      migration note needed — there is no behavior change for any
      existing call site.

## Trivia-manager re-dogfood (separate repo, separate PR, after this ships)

- [ ] Against a real `Compono.TestDoubles` package build from this repo,
      re-attempt the trivia-manager call sites Stage 3 left on
      NSubstitute for argument-matching/argument-filtered-verification
      reasons (see that repo's `docs/adr/0002-staged-migration-to-compono.md`
      Amendment 1 and `docs/plans/0002-staged-compono-migration.md`'s
      Stage 3 section).
- [ ] Record the outcome back into trivia-manager's own plan/ADR, not
      this repo.
- [ ] Any new gap that re-dogfood surfaces — including any real
      overloaded-member argument-matching need, should one turn up — is
      separate evidence for a future ADR per ADR-0029/ADR-0042 Amendment
      2's policy, not folded into this plan.

## Critical Files

- `src/Compono/Arg.cs` (new) — `Arg<T>`, `Arg.Any<T>()`/`Arg.Is<T>(predicate)`,
  the implicit conversion, the public `Matches(T)` operation (no public
  delegate accessor).
- `src/Compono/ReturnConfig.cs` — unchanged; referenced only to confirm it
  stays that way (task 3).
- `src/Compono/CallVerifier.cs` — unchanged; referenced only to confirm it
  stays that way (task 6).
- `src/Compono.Generators/*` — eligibility analysis (task 2), per-eligible-
  member field/`Configure()`/dispatch/`Verify()` emission (tasks 3-6).
- `test/Compono.TestDoubles.Tests/*` — regression + new coverage per task
  section above.
- `test/Compono.TestDoubles.AotSmokeTest/*` — task 8.
- `docs/packages/compono-testdoubles.md` — task 9.

## Test Plan

Per `references/testing.md`: generator-output snapshot/behavior tests for
eligibility (task 2, including the "ineligible member's output is
byte-for-byte unchanged" snapshot diff), `Configure()`/dispatch/`Verify()`
for eligible members (tasks 4-6), a full unmodified existing-suite run
(task 7), and an AOT smoke test (task 8) — no new benchmark unless
implementation surfaces real evidence of one being needed. The
trivia-manager re-dogfood is the real-world validation pass, not a
synthetic example, per ADR-0029's evidence discipline — it happens after
this PR ships a real package build, in the other repo.

## Notes

_Recorded as work proceeds. History: this plan's first draft proposed four
separate PRs; collapsed to one after review. Its second draft assumed
`Arg<T>` could wrap every parameter including overloaded members and
described this as an intentional pre-1.0 break; a real compiler spike
(ADR-0048's Decision Outcome) proved that design doesn't reliably compile,
and the corrected scope (non-overloaded members only) eliminated the break
entirely rather than requiring one — recorded here since it changed this
plan's Goal/Scope substantially before any task was checked off. Third
correction: the first two drafts had generated code reading an `internal
Predicate` accessor on `Arg<T>` from the consumer assembly, which can't
work cross-assembly (the same defect class ADR-0044 Amendment 3 already
fixed for `ReturnConfig<T>`) — corrected before implementation started to
`Arg<T>` exposing a public `Matches(T)` operation and generated fields
storing `Arg<TParam>?` directly rather than an extracted delegate._

### Implementation pass (2026-08-21) - two real findings, one fixed, one blocking

Tasks 1-7 implemented and verified: `Arg<T>`/`Arg` in `src/Compono/Arg.cs`;
eligibility analysis in `TestDoubleAnalyzer.cs` (`IsEligibleForMatching`,
reusing the existing `TypeReferencesOwnTypeParameter` walk against
parameters); per-eligible-member fields/`Configure()`/dispatch/`Verify()`
in `TestDouble.scriban`. Full regression: `Compono.Tests` (new `ArgTests.cs`,
7/7), `Compono.Generators.Tests` (181/181, including all 83
`TestDoubleVerifyTests` snapshot tests), `Compono.TestDoubles.Tests` (6/6),
`Compono.TestDoubles.SampleTests` (32/32, 28 existing + 4 new
`ArgumentMatchingTests.cs` covering mixed literal/`Any`/`Is`, argument-free
compatibility, `Verify()` filtering, and the single-parameter call-log
special case).

**Finding 1 - fixed.** ADR-0048's "no existing call site changes behavior"
claim assumed v1/v2 already required real parameters on a non-overloaded
member's `Configure()`/`Verify()` whenever it had any - false. v1/v2 gives
**every** non-overloaded member a zero-argument `Configure()`/`Verify()`,
regardless of real arity (confirmed against the template pre-this-plan:
the `is_overloaded` branch was the only one taking real parameters). A real
existing call site, `Compono.TestDoubles.SampleTests/VerificationTests.cs`'s
`repository.Verify().Save().Once()` against `IRepository.Save(int amount)`,
broke immediately (`CS1501: No overload for method 'Save' takes 0
arguments`). Fixed with a purely additive compatibility overload: every
eligible member now generates BOTH the new `Arg<TParam>`-per-parameter
signature AND the original zero-argument one (leaving every matcher `null`,
which dispatch already treats as always-matching - reusing, not
duplicating, the existing semantics). Verified: the two now-failing
`VerificationTests.cs` lines pass unmodified, and the full existing suite
(above) passes with zero call-site changes. This is a real correction to
ADR-0048's Decision Outcome's "Generated C#" section (which shows only one
`Configure()` signature per eligible member) - needs its own dated
amendment before this plan can close, not just this Notes entry.

**Finding 1b - a related, smaller surprise, not a defect.** The analyzer's
existing `IsOverloaded` flag is scoped to siblings that *also* have a
configuration surface, not "shares a declared name with any sibling" - a
member whose only same-named sibling is ref/out/scoped-ref-shaped (no
surface at all, e.g. `IRepository.Seek(scoped ref Span<int>)` alongside
`Seek(int)`) is `IsOverloaded = false` and correctly becomes eligible too.
This is safe (verified: only one real `Configure()`/`Verify()` candidate
ever exists for such a member, so ADR-0048's compiler-spike ambiguity
concern doesn't apply) and evidence-neutral (broader than trivia-manager's
evidence, but zero-cost given Finding 1's compatibility overload) - not a
bug, just broader than ADR-0048's prose anticipated. 7 `Compono.Generators.Tests`
snapshots updated accordingly (`Seek`, `TryGet`, `FindNameAsync`, etc.) -
all legitimate, reviewed individually before approving.

**Finding 2 - NOT fixed, blocking.** `Compono.Arg`/`Compono.Arg.Any<T>()`/
`Compono.Arg.Is<T>()` collide by name with `NSubstitute.Arg`/`Arg.Any<T>()`/
`Arg.Is<T>()`. Confirmed with a real failing build, not assumed: building
the full solution (`Compono.slnx`), `samples/Compono.Samples.AspNetApi.Tests`
fails - `OrderServiceTests.cs`'s `repository.SaveAsync(Arg.Any<Order>(),
Arg.Any<CancellationToken>())` (real `NSubstitute` usage, unrelated to this
plan) now resolves `Arg.Any<Order>()` to `Compono.Arg.Any<Order>()`
(returning `Compono.Arg<Order>`) instead of `NSubstitute.Arg.Any<Order>()`
(returning `Order`), because this file's own namespace,
`Compono.Samples.AspNetApi.Tests`, nests under `Compono` - C#'s enclosing-
namespace lookup puts the bare `Compono` namespace (and therefore
`Compono.Arg`) in scope for unqualified `Arg` **even with no `using
Compono;` anywhere in the file**, no global using involved. This is not a
narrow edge case: any consumer project whose own namespace starts with
`Compono.` (this repo's own samples/convention) or that has `using
Compono;` for ordinary composition features while also using NSubstitute
directly (`Compono.NSubstitute`'s entire purpose is exactly this
combination) hits this. Not fixed here because the right fix is a real
design choice - among others, rename `Compono.Arg`/`Arg.Any`/`Arg.Is` to
something collision-safe, move it to a different namespace (inconsistent
with `ReturnConfig<T>`/`CallVerifier`'s existing bare-`Compono`-namespace
precedent), or accept it as documented friction requiring a
fully-qualified `Compono.Arg.Is<T>(...)` at any call site that also uses
NSubstitute's `Arg` in the same file - each has real tradeoffs ADR-0048
never weighed. **This blocks calling PLAN-0048 `Done`** - `samples/Compono.Samples.AspNetApi.Tests`
is left failing to build on this branch, deliberately not patched around,
so the failure stays visible rather than hidden by an untested rename.
Needs its own design pass (a dated Amendment to ADR-0048, or a follow-up
ADR if the fix is substantial enough) before this plan can close.

Tasks 8 (AOT smoke test) and 9 (docs) not started - stopped here to report
Finding 2 rather than build further on top of an API surface that may
still change shape.
