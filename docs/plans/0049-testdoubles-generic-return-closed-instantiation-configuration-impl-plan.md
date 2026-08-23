# [PLAN-0049] Compono.TestDoubles: Per-Closed-Instantiation Configuration for Generic Methods Whose Return Type Depends on Their Own Type Parameter

**Status:** Done

**Implements:** [ADR-0049](../adr/0049-testdoubles-generic-return-closed-instantiation-configuration.md)

## Goal

A `Compono.TestDoubles`-generated double supports independent
`Configure()`/`Verify()` state per closed `T` for a generic method whose
return type is exactly `T`, or the sole type argument of `Task<T>`/
`Task<T?>`/`ValueTask<T>`/`ValueTask<T?>`, for a single method-type-parameter —
closing the real `ncipollina/trivia-platform` gap
(`IConversationalContextManager.GetContextDataAsync<T> : Task<T?>`) ADR-0049
designs. Done when: the eligibility check no longer routes this shape to
whole-interface `Failure()`; `Configure<T>()`/`Verify<T>()` for two
different closed `T`s on the same double instance are provably independent;
both of ADR-0045's existing dispatch branches (deterministic-default,
configuration-required) compose correctly through the new bucket
mechanism; the full existing `Compono.TestDoubles` test suite passes
unmodified; a generator-driven (not hand-written) AOT smoke test proves the
real generated shape survives Native AOT; and `docs/packages/compono-testdoubles.md`
documents the new capability and its scope boundary.

## Scope

Exactly ADR-0049's Decision Outcome: a `Dictionary<Type, object>` bucket
keyed by `typeof(T)`, valued by a generator-emitted nested class generic in
`T` (a `ReturnConfig<TSlot>` field, one `Match<TParam>?` field per real
non-`T` parameter, a `lock`-guarded call log), reusing `Match<T>`/
`CallVerifier` unchanged. Scoped to a single method-type-parameter,
referenced only as the method's direct return type or the sole type
argument of `Task`/`ValueTask`.

Explicitly out, per ADR-0049 and this repo's evidence discipline:

- `T` nested deeper in the return type (`Task<List<T>>`,
  `Task<Dictionary<string, T>>`, etc.) — stays whole-interface-rejecting,
  unchanged.
- More than one method-type-parameter on a self-referencing-return method
  — stays whole-interface-rejecting, unchanged.
- `SetContextDataAsync<T>`-shaped members (`T` in a *parameter*, not the
  return type) — **not touched by this plan at all**, including the
  argument-aware-matching gap ADR-0049's design pass separately surfaced
  and explicitly declined to fold in. That gap is its own future ADR, not
  a task here.
- Any change to `ReturnConfig<T>`, `ReturnConfigBuilder<T>`, `Match<T>`, or
  `CallVerifier`'s own public shape — this plan reuses all four completely
  unchanged.

## One implementation PR

Per the user's explicit direction: this capability is one coherent
generated-code change (eligibility analysis, model, template, dispatch,
`Configure<T>()`/`Verify<T>()` extensions all only make sense together) and
should not be artificially split across phases/PRs. There is no genuine
technical reason to stage it — unlike PLAN-0044's real multi-milestone
scope, everything here lands in `Compono.Generators` plus the runtime
already exists (`Match<T>`, `ReturnConfig<T>`, `CallVerifier` are all
unchanged, pre-existing types).

## Tasks

Grouped by concern, checked off as work proceeds.

### 1. Eligibility analysis (`TestDoubleAnalyzer.cs`)

- [x] At the existing `method.IsGenericMethod && TypeReferencesOwnTypeParameter(method.ReturnType, method)`
      check (`TestDoubleAnalyzer.cs:410`, today's unconditional
      whole-interface `Failure(...)`), add a narrower classification
      *before* that check fires: does the return type match exactly one of
      `T`, `Task<T>`, `Task<T?>`, `ValueTask<T>`, `ValueTask<T?>` (`T` the
      method's sole type parameter, appearing nowhere else in the return
      type's syntax tree beyond that one direct position)? If so, mark the
      member "closed-instantiation eligible" and continue instead of
      returning `Failure`. Any other shape referencing the method's own
      type parameter in the return type (deeper nesting, multiple type
      parameters) keeps today's exact `Failure(...)` behavior, unchanged.
- [x] **Overloaded members are eligible, not excluded — verified with a
      real compiler/Native AOT spike, not assumed.** An earlier draft of
      this plan excluded overloaded members here, reasoning by false
      analogy to ADR-0048's Match<T> exclusion. That reasoning was wrong:
      ADR-0048 excludes overloaded members specifically from *argument
      matching* (`Match<T>`-wrapped parameters caused real `CS0121`
      ambiguity for 3 of 5 realistic overload-parameter-type families —
      ADR-0048's own Decision Outcome), not from generics or from having
      independently-configurable state at all. The **bucket-by-closed-T**
      mechanism this ADR adds is an orthogonal axis to the **overload-
      discriminator** mechanism ADR-0044 Requirement 1 already ships (real,
      un-wrapped parameter types as pure discriminators, `generic_suffix`
      already supported per-overload — proven in production today by
      `IWidget.Process<T>(T)`/`Process<T>(IEnumerable<T>)` in
      `Compono.TestDoubles.SampleTests/GenericMemberTests.cs`). A spike
      (two overloads of the same generic-return-depends-on-own-type-
      parameter method, discriminated by real parameter arity, each with
      its own discriminator-suffixed bucket dictionary and nested state
      class) compiled, ran correctly under JIT, and survived a real
      `dotnet publish -c Release -f net10.0 -p:PublishAot=true` + native
      run with zero warnings — proving per-overload buckets stay fully
      independent of each other (including when the *same* closed `T` is
      used on both overloads) and that ordinary overload resolution +
      generic type inference route each `Configure<T>()`/`Verify<T>()`
      call to the right overload's own bucket. **The generated
      `Configure<T>()`/`Verify<T>()` signature for an overloaded closed-
      instantiation-eligible member therefore mirrors ADR-0044's existing
      discriminator shape exactly** (real parameter types, `generic_suffix`,
      no `Match<T>` wrapping) — it simply doesn't get ADR-0048's argument-
      matching capability, identical to any other overloaded member today,
      not a new restriction this plan introduces.
- [x] Gate the new eligibility on: no ref-like real parameter (mirrors
      ADR-0048 Amendment 1 — a ref-like type can never be a generic type
      argument, so it can't appear in the bucket's own generic-in-`T` state
      class either way); no real parameter itself referencing the method's
      own type parameter (that combination is unevidenced and out of
      scope, same discipline as the deeper-nesting exclusion above).
      **Not** gated on overload status — see above.
- [x] Test: `Task<List<T>> Get<T>()` still produces whole-interface
      `Failure` with `UnsupportedTestDoubleGenericReturnShape`, byte-for-
      byte the same diagnostic as before this plan.
- [x] Test: `Task<TResult> Get<TKey, TResult>(TKey key)` (return depends on
      one of two type parameters) still produces whole-interface `Failure`,
      unchanged.
- [x] Test: `void Log<TState>(int, TState)` (ADR-0044's existing supported
      generic shape, return independent of own type parameter) is
      completely unaffected — same generated output as before this plan,
      a real diff against a pre-plan snapshot, not just "still compiles"
      (mirrors PLAN-0048 task 2's own regression-proof pattern).
- [x] Test: `SetContextDataAsync<T>`-shaped member (`T` in a parameter, not
      the return type) is completely unaffected — same generated output as
      before this plan.

### 2. Model (`TestDoubleMemberInfo.cs` and/or a new closed-instantiation-specific model type)

- [x] Carry whatever the template needs for a closed-instantiation-eligible
      member: the return-shape template (`T`/`Task<T>`/`Task<T?>`/
      `ValueTask<T>`/`ValueTask<T?>`, expressed so the template can splice
      the state class's own `T` in), the real (non-`T`) parameters (same
      shape `TestDoubleParameterInfo` already carries for an ordinary
      ADR-0048-eligible member), and whether the return shape is nullable
      (drives the ADR-0045 default-vs-configuration-required dispatch
      branch — reuse the exact same nullability/default-expression check
      ADR-0045's existing logic already runs, just applied to the
      substituted-`T` shape instead of a fully concrete type).
- [x] Keep `HasConfigurationSurface`/`IsEligibleForMatching`'s existing
      meaning unchanged for every other member shape — a closed-
      instantiation-eligible member is a **third**, new classification, not
      a repurposing of either existing flag (avoids the kind of ambiguous-
      meaning defect ADR-0044's own Amendments repeatedly had to correct).

### 3. Generated storage and dispatch (`TestDouble.scriban`, `TestDoubleEmitter.cs`)

- [x] Per closed-instantiation-eligible member, emit the nested generic-in-
      `T` state class (`ReturnConfig<TSlot>` field, one `Match<TParam>?`
      field per real parameter, `lock`-guarded call log — exactly ADR-0049's
      Decision Outcome code block), the `Dictionary<Type, object>` bucket
      field, and the `lock`-guarded bucket-lookup-or-create method — all
      `internal`, matching this repo's existing generated-field visibility
      convention.
- [x] Emit the real interface member's dispatch body routed through the
      bucket: record the call, evaluate matchers, then **reuse ADR-0045's
      existing configured-value/configured-exception/default-or-
      configuration-required branch verbatim** (reading from the bucket's
      `ReturnConfig<TSlot>` field instead of a direct member field) — no
      new dispatch shape, no new exception type.
- [x] For a **non-overloaded** closed-instantiation-eligible member, emit
      `Configure<T>()`/`Verify<T>()` extensions generic in `T`, with
      `Match<TParam>` parameters for each real parameter (same signature
      shape ADR-0048-eligible members already get), looking up the bucket
      for the caller's closed `T` and returning the existing
      `ReturnConfigBuilder<TSlot>`/`CallVerifier` — both already-existing,
      unmodified types.
- [x] For an **overloaded** closed-instantiation-eligible member, reuse
      ADR-0044 Requirement 1's existing overload-discriminator machinery
      unchanged: each overload gets its own `generic_suffix`-discriminated
      bucket field and its own `Configure<T>()`/`Verify<T>()` extension,
      resolved by ordinary overload resolution against the real (non-`T`)
      parameter types — **not** `Match<TParam>`-wrapped (task 1's finding:
      overloaded members keep the plain-parameter discriminator shape they
      already have today, they just don't gain ADR-0048 argument matching).
      No new discriminator logic — the existing per-overload naming the
      generator already emits for non-generic overloads applies here
      verbatim.
- [x] Test: generated-output review for each of the five evidenced return
      shapes (`T`, `Task<T>`, `Task<T?>`, `ValueTask<T>`, `ValueTask<T?>`)
      against ADR-0049's Decision Outcome code block.
- [x] Test: generated-output review for an overloaded closed-instantiation-
      eligible member (two overloads, e.g. differing real-parameter arity)
      — each overload's `Configure<T>()`/`Verify<T>()` carries its own
      discriminator suffix and its own bucket field, byte-for-byte matching
      ADR-0044's existing overload-discriminator output shape.

### 4. Runtime behavior tests (`Compono.TestDoubles.Tests` / `Compono.TestDoubles.SampleTests`)

- [x] Two closed `T`s configured/verified independently on the same double
      instance — `Configure<TypeA>()` provably doesn't affect `TypeB`'s
      state (mirrors the AOT spike's own proof, now against real generator
      output).
- [x] ADR-0045 deterministic-default branch: an unconfigured closed `T` on
      a nullable-return member (`Task<T?>`) returns the real default
      (`null`), not a throw.
- [x] ADR-0045 configuration-required branch: an unconfigured closed `T`
      on a non-nullable-return member (`Task<T>`) throws
      `TestDoubleNotConfiguredException`.
- [x] Argument mismatch against a correctly-configured `T` falls through to
      that same `T`'s own default/configuration-required behavior — not
      the configured value, and not another `T`'s state.
- [x] `Match.Any<TParam>()`/`Match.Is<TParam>(predicate)`/literal-equality
      all work against the real (non-`T`) parameters, scoped per bucket.
- [x] `Once()`/`Never()`/`Exactly(n)` all work per closed `T`, independent
      of another `T`'s call count.
- [x] Regression/composition: an overloaded closed-instantiation-eligible
      member (mirrors the AOT spike's `IReproOverloaded.GetDataAsync<T>`
      shape) — `Configure<T>()`/`Verify<T>()` on each overload only affects
      that overload's own bucket, proven with the **same** closed `T` used
      on both overloads (e.g. `GetDataAsync<UpsellPayload>(id)` vs.
      `GetDataAsync<UpsellPayload>(id, version)`) to rule out any
      cross-overload bucket-key collision, not just different-`T` isolation
      (that's the prior item). Proves ADR-0044's overload-discriminator
      mechanism and this ADR's bucket-by-closed-T mechanism compose
      correctly, per the user's explicit request for this regression test.
- [x] Regression: an interface containing both a closed-instantiation-
      eligible member and ordinary members (mirrors
      `IConversationalContextManager`'s real shape — a closed-instantiation
      member alongside `TransitionContextAsync`/`GetCurrentContextAsync`-
      shaped members) generates a real double for **every** member, not
      just the new shape — the whole-interface-`Failure()` consequence
      ADR-0049's Context section named is actually gone, not merely
      theorized.
- [x] Full existing `Compono.TestDoubles` test suite passes completely
      unmodified — zero expected diffs, same standard PLAN-0048 held itself
      to.

### 5. AOT smoke test (`test/Compono.TestDoubles.AotSmokeTest`)

- [x] Add a real interface (through the actual generator this time, not
      hand-written) mirroring the ADR-0049 spike's shape — a nullable
      closed-instantiation member and a non-nullable one, proving both
      ADR-0045 branches survive Native AOT through real generated code, not
      just the hand-written proof-of-concept.
- [x] Real `dotnet publish -c Release -f net10.0 -p:PublishAot=true` +
      running the published binary — zero `IL2xxx`/`IL3xxx`/AOT warnings,
      exit 0, `PASS` with every assertion holding.

### 6. Documentation

- [x] `docs/packages/compono-testdoubles.md`: new section for this
      capability — the supported return shapes, the single-type-parameter
      scope boundary, a `Configure<T>()`/`Verify<T>()` example, and an
      explicit note that `SetContextDataAsync<T>`-shaped members (`T` in a
      parameter) are unaffected and remain a separate, undecided question.
- [x] ADR-0044 Amendment 19 — already written during ADR-0049's design
      pass (recording that Requirement 2's exclusion is reopened, scoped
      to this evidenced shape, pointing to ADR-0049). No further edit
      needed unless implementation surfaces a real correction to record.

## Critical Files

- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — the
  eligibility classification (task 1) and dispatch-branch reuse (task 3).
- `src/Compono.Generators/Models/TestDoubleMemberInfo.cs` (and/or a new
  model type) — carries the closed-instantiation shape info to the
  template (task 2).
- `src/Compono.Generators/Templates/TestDouble.scriban` — the new
  generated-code branch: nested state class, bucket, dispatch,
  `Configure<T>()`/`Verify<T>()` (task 3).
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` — wiring the new
  model data into the template, if the existing wiring doesn't already
  generalize.
- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` — confirm
  `UnsupportedTestDoubleGenericReturnShape` still fires correctly for the
  still-unsupported shapes (no new descriptor expected — this is a
  narrowing of when the existing one fires, not a new diagnostic).
- `test/Compono.Generators.Tests/*` — eligibility + generated-output tests
  (task 1, task 3).
- `test/Compono.TestDoubles.Tests/*`, `test/Compono.TestDoubles.SampleTests/*` —
  behavior tests (task 4).
- `test/Compono.TestDoubles.AotSmokeTest/Program.cs` — task 5.
- `docs/packages/compono-testdoubles.md` — task 6.
- `docs/adr/0044-compono-testdoubles-v2-overloads-generics-verification.md` —
  Amendment 19, already written (task 6, checked).

## Test Plan

Per `references/testing.md`: generator-output classification/snapshot
tests for the narrowed eligibility check and the three shapes that must
stay unaffected (deeper-nested `T`, multi-type-parameter, `T`-in-parameter)
(task 1); generated-output review against ADR-0049's Decision Outcome code
block for all five evidenced return shapes (task 3); real behavior tests
proving independent per-closed-`T` state, both ADR-0045 branches, and
ADR-0048 matcher/verification reuse (task 4); a full unmodified existing-
suite run (task 4); and a generator-driven AOT smoke test (task 5) — the
hand-written spike proved the mechanism is AOT-safe in principle, this is
the proof that the *real* generated code is too. No new benchmark unless
implementation surfaces real evidence one is needed, matching PLAN-0048's
own disposition.

## Notes

Implementation (2026-08-23) surfaced two real defects a compiler-driven
build/test loop caught, neither anticipated by ADR-0049's own spike (which
never exercised them):

- **A constrained (`where T : class`) closed-instantiation-eligible
  member's explicit interface implementation cannot spell its return type
  with the nullable annotation intact.** `Task<T?> IFoo.GetAsync<T>(...)`
  fails to compile (`CS9334`/`CS0453`/`CS0452` cascade) — an explicit
  interface implementation can never restate `where T : class` (`CS0460`),
  and without it the compiler can't tell whether `T?` means a
  nullable-annotated reference or `System.Nullable<T>`. Reproduced with a
  minimal hand-written repro before touching the generator. Fixed by
  declaring the explicit implementation's return type with the type
  parameter's own `?` stripped (`Task<T>`, not `Task<T?>` — the same CLR
  type either way) and suppressing the resulting `CS8616`/`CS8619`
  nullability-mismatch warnings with `#pragma warning disable/restore`
  around just that member. Deliberately pragma-based, not `#nullable
  disable`/`restore` — `#nullable restore` reverts to the *project's*
  default annotation context, not back to this generated file's own
  leading `#nullable enable`, which silently left every later member in
  the same file oblivious (a real `CS8669` regression this task's own
  `Compono.TestDoubles.SampleTests` build caught on the first attempt).
- **An overloaded closed-instantiation-eligible member's generated state
  class must not declare `Matcher_*`/`Calls`/`Lock` fields at all** — those
  only exist for the non-overloaded, `Match<TParam>`-wrapped shape; an
  overloaded member's `Configure<T>()`/`Verify<T>()` never writes them
  (`CS0649` unused-field warning), and its `Verify<T>()` already reads
  `Config.ConfiguredCallCount` directly instead of walking a call log. Split
  the emitter's single "has real parameters" flag into two: one for the
  Configure/Verify zero-vs-real-parameter signature split (unaffected by
  overload status) and a narrower one (`!IsOverloaded && Parameters.Count
  > 0`) gating the state class's matcher machinery and the dispatch body's
  matcher-evaluation loop.

Both were caught by this task's own "build and run the full test suite
after each major step" discipline (`references/testing.md`), not by
assumption — every `dotnet build`/`dotnet test` run after each fix was
warning-clean before moving on. Full solution: 2334/2334 tests passing,
zero build warnings. `Compono.Generators.Tests`: 370/370 (14 new tests: 2
still-unsupported-shape regressions, 2 new-capability generator-output
tests, 1 real Native AOT-mirroring solo test).
`Compono.TestDoubles.SampleTests`: 208/208 (12 new closed-instantiation
runtime-behavior tests). `Compono.TestDoubles.AotSmokeTest`: real
`dotnet publish -c Release -f net10.0 -p:PublishAot=true` + native run,
zero AOT/trim warnings, exit 0, `PASS` with every assertion holding
(including two independently-configured closed `T`s and both ADR-0045
dispatch branches through the real generator, not the hand-written spike).

**PR #107 Codex review (2026-08-23)** caught one real gap the spike and
implementation both missed: a type parameter declared `where T : allows
ref struct` (C# 13's ref-like-capable anti-constraint) matched the
closed-instantiation-eligible return shape (`T` itself is neither ref-like
as a symbol nor caught by any existing ref-like-*parameter* guard, since
this is a constraint on the type parameter, not a parameter type) — but the
generated state class's `ReturnConfig<T>`/`ReturnConfigBuilder<T>` fields
(Compono's existing, unmodified runtime types) declare no `allows ref
struct` on their own `T`, so a real caller closing this method's `T` over
an actual ref struct would fail to compile with `CS9244` inside generated
code instead of the clean `CMP0031` whole-interface-fallback diagnostic
every other unsupported shape gets. Fixed at the single eligibility choke
point (`IsClosedInstantiationEligibleReturnShape`, excluding
`AllowsRefLikeType` type parameters, falling back to whole-interface
rejection like every other no-constructible-body shape), with a new
regression test proving `T Create<T>() where T : allows ref struct` still
produces `CMP0031`. The same review also caught two unawaited `ThrowAsync`
assertions in `ClosedInstantiationTests.cs` (a false-pass risk — the test
could finish before observing whether the assertion held) — fixed by
making both tests `async Task` and awaiting the assertion. Full solution
after both fixes: 2336/2336 tests passing, zero build warnings.

**PR #107 Codex review, round 2 (2026-08-23)** caught a follow-on gap in
the round-1 fix: the nullable-annotation-stripping fix for a `where T :
class`-constrained explicit interface implementation was keyed off
`IsClosedInstantiationEligible`, which requires `HasConfigurationSurface`.
But a member can match ADR-0049's closed-instantiation return shape and
still end up with **no** configuration surface for an unrelated reason —
concretely, ADR-0044 Amendment 5's ref/out/in overload-set-internal
fallback (`Task<T?> Get<T>(ref int x) where T : class` alongside a sibling
overload). That member still gets a real explicit interface implementation
(a deterministic-default-only fallback body), and it hit the identical
`CS9334`/`CS0453` cascade the round-1 fix didn't cover, since the fallback
branch of the template used the member's plain, unmodified return-type
text. Fixed by adding a new model field,
`IsClosedInstantiationEligibleShape` — the same return-shape test,
deliberately independent of `HasConfigurationSurface` — and keying the
emitter's nullable-stripping computation and the template's fallback-body
return-type spelling off that instead. A new regression test
(`ClosedInstantiationShapedRefParameterOverloadFallback_CompilesWithoutConfigurationSurface`)
uses `VerifyWithInfoDiagnostic`, which re-compiles the real generated
output and asserts zero compiler errors — exactly the check that would
have caught this cascade. Full solution after this fix: 2338/2338 tests
passing, zero build warnings.
