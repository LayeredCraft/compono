# [PLAN-0048] Compono.TestDoubles: Argument Matching and Argument-Aware Call Verification

**Status:** Done

**Implements:** [ADR-0048](../adr/0048-testdoubles-argument-matching-and-call-verification.md)

## Goal

A `Compono.TestDoubles`-generated double supports argument-matched
response configuration and argument-filtered call verification via a
unified `Compono.Match<T>` surface (literal equality, `Match.Any<T>()`,
`Match.Is<T>(predicate)`), for every member that is **both** non-overloaded
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

Exactly ADR-0048's Decision Outcome: `Compono.Match<T>` (with the implicit
`T -> Match<T>` equality-matcher conversion), per-parameter matcher fields
and a call log generated on the double class alongside the existing
`ReturnConfig<T>` field (not inside it), and an eligible member's
`Verify()` extension folding argument-filtering directly into the same
`Match<T>`-per-parameter shape as `Configure()` (no `.Matching()` step).
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
generator pass emits the `Match<T>`-typed `Configure()`/`Verify()`
signatures, the matcher fields, and the call log together for a given
eligible member, and none of the four is independently useful without the
others. The trivia-manager re-dogfood (its own section below) is a
separate PR in the *other* repo, after this one ships a real package
build — not a second PR here.

## Tasks

Grouped by concern, checked off as work proceeds.

### 1. `Compono.Match<T>` (core `Compono`)

- [x] `Match.Any<T>()`, `Match.Is<T>(Func<T, bool>)`, implicit `T -> Match<T>`
      conversion (equality matcher), and exactly one **public** operation
      for generated code to call: `public bool Matches(T value)`. No
      public delegate/`Predicate` accessor — `Match<T>`'s internal
      representation stays free to change without being a breaking change
      to generated output's dependency on it (the same
      cross-assembly-accessibility fix ADR-0044 Amendment 3 already made
      for `ReturnConfig<T>`; `internal` alone doesn't work here since
      generated code compiles into the *consumer* assembly, not `Compono`).
- [x] Internal three-case representation (`Equality`/`Any`/`Predicate`) so
      a literal argument allocates no closure — stores the value directly,
      compared via `EqualityComparer<T>.Default` inside `Matches`; only
      `Match.Is<T>(predicate)` allocates (the caller's own lambda).
- [x] Unit tests: construction of all three kinds, the implicit conversion,
      `Matches` evaluation in isolation (no generator involvement yet) -
      `test/Compono.Tests/MatchTests.cs`.
- [x] A literal-built `Match<T>` really doesn't allocate a delegate -
      `MatchTests.cs`'s `Literal_StoresNoPredicateDelegate`/
      `Any_StoresNoPredicateDelegate` reflect into the private `_predicate`
      field and assert it's null (a simple assertion, not a benchmark).

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
- [x] One `Match<TParam>?` field per real parameter (the `System.Nullable`
      wrapper around the whole `Match<TParam>` value, not an extracted
      delegate), generated on the double class alongside the
      `ReturnConfig<T>` field. `null` means "no matcher configured for
      this parameter" (dispatch treats it as always-matching); `HasValue`
      with an `Match<TParam>` built by `Match.Any<TParam>()` is a distinct,
      deliberately-configured state that happens to produce the same
      dispatch result — the two are never conflated in storage, only in
      their dispatch-time effect.
- [x] A `lock`-guarded call log: a generated tuple/record-shaped
      `List<(T1, T2, ...)>`, plus its own `lock` object.

### 4. `Configure()` for an eligible member

- [x] Generated extension signature: one `Match<TParam>` parameter per real
      parameter (not the real type directly).
- [x] Body: store each `Match<TParam>` argument directly into its
      corresponding `Match<TParam>?` field (no unwrapping — `Match<T>` has no
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

- [x] Generated extension signature: same `Match<TParam>`-per-parameter
      shape as `Configure()`, calling `.Matches(...)` against each logged
      call's recorded arguments (never a delegate extracted from the
      `Match<TParam>` argument — same accessibility constraint as task 4).
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

- [x] Extended `test/Compono.TestDoubles.AotSmokeTest` with
      `IAccountRepository.Withdraw` (mixed literal/`Match.Any<T>()`/
      `Match.Is<T>(predicate)` `Configure()`, argument-filtered `Verify()`);
      the existing `IGateway` (overloaded) and `ILoggerLike`
      (generic-scoped-out) interfaces already in that project continue to
      exercise the eligibility boundary itself, unaffected. Real
      `dotnet publish -c Release -f net10.0 -p:PublishAot=true` + running
      the published binary - exit 0, `PASS: ... Withdraw matching=True.`
- [x] No new benchmark added — ADR-0048 explicitly declines one absent
      evidence of an actual performance risk; none surfaced during
      implementation.

### 9. Documentation

- [x] `docs/packages/compono-testdoubles.md` updated: new "Argument
      matching and argument-filtered verification" section (`Match<T>`,
      the eligibility rule, why `Match` not `Arg`), corrected "Still
      deliberately minimal" and "What it deliberately doesn't do" sections
      to stop claiming zero argument-aware recording exists at all.

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

- `src/Compono/Match.cs` (new) — `Match<T>`, `Match.Any<T>()`/`Match.Is<T>(predicate)`,
  the implicit conversion, the public `Matches(T)` operation (no public
  delegate accessor). Named `Match` specifically to avoid colliding with
  `NSubstitute.Arg` (Finding 2 below).
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
`Match<T>` could wrap every parameter including overloaded members and
described this as an intentional pre-1.0 break; a real compiler spike
(ADR-0048's Decision Outcome) proved that design doesn't reliably compile,
and the corrected scope (non-overloaded members only) eliminated the break
entirely rather than requiring one — recorded here since it changed this
plan's Goal/Scope substantially before any task was checked off. Third
correction: the first two drafts had generated code reading an `internal
Predicate` accessor on `Match<T>` from the consumer assembly, which can't
work cross-assembly (the same defect class ADR-0044 Amendment 3 already
fixed for `ReturnConfig<T>`) — corrected before implementation started to
`Match<T>` exposing a public `Matches(T)` operation and generated fields
storing `Match<TParam>?` directly rather than an extracted delegate._

### Implementation pass (2026-08-21) - two real findings, one fixed, one blocking

Tasks 1-7 implemented and verified: `Match<T>`/`Match` in `src/Compono/Arg.cs`;
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
eligible member now generates BOTH the new `Match<TParam>`-per-parameter
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

**Finding 2 - NOT fixed at the time, blocking; resolved below.** As originally
implemented, this plan's new type was named `Compono.Arg`/`Compono.Arg.Any<T>()`/
`Compono.Arg.Is<T>()` - colliding by name with real `NSubstitute.Arg`/
`NSubstitute.Arg.Any<T>()`/`NSubstitute.Arg.Is<T>()`. Confirmed with a real
failing build, not assumed: building the full solution (`Compono.slnx`),
`samples/Compono.Samples.AspNetApi.Tests` failed -
`OrderServiceTests.cs`'s `repository.SaveAsync(Arg.Any<Order>(),
Arg.Any<CancellationToken>())` (real `NSubstitute` usage, unrelated to this
plan) resolved unqualified `Arg.Any<Order>()` to the new `Compono.Arg.Any<Order>()`
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
combination) hits this.

**Resolved:** product owner decision, rename Compono's new type from
`Arg`/`Arg<T>` to `Match`/`Match<T>` throughout (`Match.Any<T>()`,
`Match.Is<T>(predicate)`, `Match<TParam>?` storage) - same semantics, name
only, chosen specifically because it doesn't collide with any real
NSubstitute symbol and because "matching" is the actual Compono concept
being named (`Arg` is primarily NSubstitute's own vocabulary, and is
exactly what caused the collision). ADR-0048 and this plan were updated to
the `Match` naming before this rename was implemented in code (see the
next dated entry below for what that implementation covered, including the
explicit coexistence regression test proving `NSubstitute.Arg` and
`Compono.Match` compile and behave correctly side by side in a
`Compono`-nested namespace with no aliasing required for either).

Tasks 8 (AOT smoke test) and 9 (docs) not started as of this finding -
stopped here to report Finding 2 rather than build further on top of an
API surface that may still change shape.

### Rename and closing pass (2026-08-21)

`Arg`/`Arg<T>` renamed to `Match`/`Match<T>` throughout: `src/Compono/Arg.cs`
→ `src/Compono/Match.cs`; `TestDoubleAnalyzer.cs`'s comment,
`TestDoubleMemberInfo.cs`'s doc comment, and `TestDouble.scriban`'s three
`global::Compono.Arg<` emission sites updated to `Match<`; `ArgTests.cs` →
`MatchTests.cs` (plus two new tests, `Literal_StoresNoPredicateDelegate`/
`Any_StoresNoPredicateDelegate`, closing Task 1's last item via reflection
against the private `_predicate` field rather than a benchmark);
`ArgumentMatchingTests.cs` → `MatchingTests.cs`; 7
`Compono.Generators.Tests` snapshot `.verified.cs` files updated
(`global::Compono.Arg<` → `global::Compono.Match<`). Full solution build
clean; full regression re-run after the rename: `Compono.Tests` 256/256,
`Compono.Generators.Tests` 181/181, `Compono.TestDoubles.Tests` 6/6,
`Compono.TestDoubles.SampleTests` 32/32, `Compono.TUnit.SampleTests` 7/7,
`Compono.XunitV3.SampleTests` 10/12 (the 2 "failures" are
`FailingCompositionTests`/`FailingConfigProfileTests` - deliberately,
permanently failing fixtures consumed by `Compono.XunitV3.Tests`'
`RealRunnerTests`, unrelated to this plan; verified against their own
source comments, not assumed).

**`samples/Compono.Samples.AspNetApi.Tests` - the project that surfaced
Finding 2 - now builds and passes clean (6/6)**, including a new
`CoexistenceTests.cs`: real, unqualified `NSubstitute.Arg.Any<Order>()`
(via the project's existing `using NSubstitute;`) alongside a
`Compono.TestDoubles`-generated double's `Match.Any<T>()`/`Match.Is<T>()`,
in the same test method, same file, whose namespace nests under `Compono`
- no alias, no qualification, for either. Adding `Compono.TestDoubles` to
this project (a `ProjectReference`, matching its existing pattern for
`Compono`/`Compono.NSubstitute`/`Compono.Bogus`) surfaced one more real,
narrow issue along the way: `ComponoGeneratedTestDoubles`'s
`CompilerVisibleProperty` declaration only ships in `Compono`'s *packaged*
`build/Compono.props` (ADR-0043 Amendment 4 Finding F) - a `ProjectReference`
consumer bypasses packaged build assets entirely and never sees it, so no
double ever generated until this project declared the same
`CompilerVisibleProperty` itself. Not a new defect PLAN-0048 introduced -
`benchmarks/Compono.Benchmarks/Compono.Benchmarks.csproj` already carries
the identical fix for the identical situation; this project's `.csproj`
now has the same `ItemGroup`, with a comment pointing at that precedent.

AOT smoke test (Task 8): `test/Compono.TestDoubles.AotSmokeTest` extended
with `IAccountRepository.Withdraw` (mixed literal/`Match.Any<T>()`/
`Match.Is<T>(predicate)` `Configure()`, argument-filtered `Verify()`); the
project's existing `IGateway` (overloaded)/`ILoggerLike`
(generic-scoped-out) interfaces continue to prove the eligibility boundary
itself is unaffected. Real `dotnet publish -c Release -f net10.0
-p:PublishAot=true` (via `pack-compono.sh` + this project's local NuGet
feed, its existing pattern) then running the published native binary
directly - exit 0, `PASS: ... Withdraw matching=True.`

Docs (Task 9): `docs/packages/compono-testdoubles.md` gained a new
"Argument matching and argument-filtered verification" section (`Match<T>`,
the eligibility rule, why `Match` not `Arg`), and its "Still deliberately
minimal"/"What it deliberately doesn't do" sections were corrected - they
previously claimed zero argument-aware call recording exists at all, which
is no longer true for an eligible member.

All tasks checked off; every ADR-0048-scoped test suite green; the
blocking finding resolved with a real regression test, not just a rename.
Trivia-manager re-dogfood remains explicitly out of this plan's scope (its
own section above) - the next step, in that other repo, whenever picked up.

**Post-merge-review fixes (Codex, PR #106, all 7 findings real, fixed
before merge):** a fresh Codex review of the pushed diff caught seven real
generator-correctness bugs in the eligible-member codegen path, none of
which the pre-push test suite happened to exercise:

1. Both eligible-member dispatch-body branches (void and non-void) omitted
   `{{ member.generic_suffix }}` on the explicit interface implementation
   - a non-overloaded generic method with no parameter referencing its own
   type parameter (e.g. `void Log<T>(string message)`) is both
   configuration-surfaced and eligible-for-matching, but its generated
   double didn't actually implement the interface (`CS0535`). Fixed by
   restating the suffix, matching the sibling non-eligible branches.
2. `isEligibleForMatching` didn't exclude a ref-like (`Span<T>`-shaped)
   real parameter - fine argument-independently, illegal as a `Match<T>`
   type argument (`CS0306`). Fixed with an `IsRefLikeType` exclusion,
   falling back to the member's existing v1/v2 shape (same disposition as
   every other eligibility exclusion).
3. The zero-argument `Configure()` compatibility overload never cleared a
   matcher an earlier call had set, so "the second `Configure()` call
   overwrites" was only true when both calls went through the same
   overload. Fixed by nulling every matcher field in that overload too.
4. `TestDoubleMemberInfo.FieldName`'s derived suffixes (`_calls`/`_lock`/
   `_m_{param}`) weren't checked against `usedFieldNames` at all, so a
   member named e.g. `Foo_calls` could collide with `Foo`'s own derived
   call-log field name; separately, an `@`-escaped verbatim parameter name
   (`@event`) produced an outright invalid identifier when concatenated as
   a suffix. Fixed with a `usedFieldNames`-collision check added to
   eligibility itself (falls back to argument-independent for that one
   member, same disposition as every other exclusion - no new
   hashing/disambiguation scheme needed) and a new `OriginalName` on
   `TestDoubleParameterInfo` (unescaped, safe to splice mid-identifier)
   used everywhere a parameter name is a suffix fragment rather than a
   standalone token.
5. The new `Configure()`/`Verify()` extensions hardcoded `self` as the
   receiver instead of reusing `extension_receiver_name`/
   `SafeReceiverName`, unlike every other extension in this file - a real
   parameter named `self` produced a duplicate-parameter compile error.
   Fixed by widening `extensionReceiverName`'s existing
   `isOverloaded`-gated computation to also cover `isEligibleForMatching`.
6/7. The new dispatch/verification bodies declared locals (`__matches`,
   per-parameter `__m_{param}` pattern variables, `__count`, the `foreach`
   loop variable) v1/v2 never needed, with no collision-safety against a
   real parameter happening to share one of those names. Fixed with a new
   `TestDoubleEmitter.SafeLocalName` helper (same lengthening algorithm as
   `TestDoubleAnalyzer.SafeReceiverName`, computed in C# rather than
   Scriban per this file's existing `CallLogAccessExpression` precedent)
   allocating every one of these names collision-safely per member.

All seven fixed together (one coherent change, not seven unrelated ones),
each with a real, compiled-and-executed regression test in
`test/Compono.TestDoubles.SampleTests/MatchingTests.cs` (not just "it
compiles") - a generic eligible member, a ref-like-parameter fallback, the
zero-argument-Configure clearing behavior, and three collision-prone
interfaces (`self`, `__matches`/`__count`/`call`, `x`/`__m_x`). Full
regression: `Compono.Tests` 256/256, `Compono.Generators.Tests` 362/362
(7 snapshot files re-verified against the new, intentional output diff -
`self` -> `__self`, matcher-clearing added - and confirmed correct before
accepting), `Compono.TestDoubles.Tests` 24/24,
`Compono.TestDoubles.SampleTests` 152/152 (38 x 4 TFMs, up from 32),
`Compono.TUnit.SampleTests` 28/28, `samples/Compono.Samples.AspNetApi.Tests`
6/6, a fresh real Native AOT publish+run of
`Compono.TestDoubles.AotSmokeTest` (exit 0). `Compono.XunitV3.SampleTests`
shows its usual 40/48 - the 8 "failures" are `FailingCompositionTests`/
`FailingConfigProfileTests`, pre-existing deliberately-always-failing
fixtures a different project's real-runner test consumes on purpose,
unrelated to this work (confirmed against their own source comments,
same as the original PLAN-0048 pass).

### Second Codex review round (2026-08-22) - two more real findings, both fixed

The first fix round's own field-name-collision fix (finding 4 above) turned
out to be incomplete: it only checked a derived name against
`usedFieldNames`' literal top-level names, never against another member's
own independently-derived names. Two more real bugs surfaced:

A. Two unremarkable members can derive the identical auxiliary name from
   each other without either colliding with anything reserved so far in a
   single linear pass - a member `Foo(int x_calls)` derives matcher field
   `__Foo_m_x_calls`; a sibling `Foo_m_x(int z)` derives call-log field
   `__Foo_m_x_calls` from its own `FieldName` - same string, neither
   present in `usedFieldNames` when either member's eligibility was
   checked, so both passed and the generated class failed with `CS0102`.
   Fixed with a genuine two-pass approach: a new pre-pass computes every
   prospective auxiliary name every non-overloaded, config-surfaced
   candidate would produce (correctly excluding a same-named sibling that
   would never itself get a configuration surface, e.g. a ref/out/in
   overload - the same `WouldGetConfigurationSurface` filter
   `overloadedNames` already applies, which the first version of this
   fix omitted and had to be corrected before committing, caught by a
   real snapshot-test regression during implementation, not by review),
   flags any name more than one candidate claims, and excludes every
   member whose derived names appear in that set from eligibility -
   `derivedNameCollisionMembers`, checked in the real eligibility
   computation instead of three separate `usedFieldNames.Contains(...)`
   checks.
B. A non-overloaded member literally named `Equals` with exactly one
   parameter passed eligibility, but its would-be `Match<T>`-typed
   extension has the same real call-site arity as the inherited
   `object.Equals(object)` instance method (any `T` implicitly converts
   to `object`, boxing if needed) - C# always prefers an applicable
   instance method over an extension method regardless of conversion
   cost, so the generated extension was never actually reachable, and
   `Configure().Equals(...).Returns(...)` failed to compile (`Equals`
   resolving to `object.Equals`, returning `bool`, which has no
   `Returns`). This is a different arity shape than the existing
   `isObjectMemberCollisionShaped`/`TestDoubleObjectMemberCollision`
   check already handles (that one assumes a non-overloaded member's
   extension arity is always zero, true for every *pre-eligibility*
   surface but not for an eligible member's real-parameter-typed
   extension) - fixed with a dedicated `Equals`-arity-one exclusion in
   `isEligibleForMatching` itself, not by reusing the existing arity-zero
   check as-is. `ToString`/`GetHashCode`/`GetType` need no equivalent
   check - they only collide with their `object` counterpart at arity
   zero, and eligibility already requires at least one real parameter.

Both fixed together with real, compiled-and-run regression tests
(`AuxiliaryNameCollisionTests`, `ObjectMemberCollisionTests` in
`MatchingTests.cs`). Full regression after: `Compono.Generators.Tests`
362/362 (zero unintended snapshot diffs - the `WouldGetConfigurationSurface`
correction above was caught and fixed *before* this count, by a real
snapshot failure locally), `Compono.TestDoubles.SampleTests` 160/160 (40 x
4 TFMs, up from 152), full solution `dotnet test` 2322/2322, a fresh real
Native AOT publish+run of `Compono.TestDoubles.AotSmokeTest` (exit 0),
API-reference-drift re-checked (zero diff - neither fix touches public
API). Also folded in, per the product owner's standing instruction for
this review loop: an update to
`.claude/skills/engineering-workflow/references/coding-standards.md`'s
"Generated code" section, recording two durable lessons from across both
review rounds - reserve every name a generator could derive before
checking any of them (not linear-pass "check what's reserved so far"),
and check a new generated extension surface against inherited-member
collisions from the start; plus a new naming bullet on checking a new
public `Compono` symbol against well-known consumer-side vocabulary before
shipping it (the `Arg`/`NSubstitute.Arg` collision earlier in this PR).
