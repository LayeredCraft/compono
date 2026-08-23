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

**PR #107 Codex review, round 3 (2026-08-23)** caught two real gaps, both
the same root-cause class as rounds 1–2: a pre-pass computed *before* the
main per-member loop (needed for collision detection, which necessarily
runs ahead of knowing each member's final classification) had its own
inline copy of "is this member closed-instantiation-eligible" logic that
fell out of sync with the real rule.

- **Finding A**: the pre-existing ADR-0048 `derivedAuxiliaryNameOwners`
  pre-pass (reserving `_calls`/`_lock`/`_m_{param}` names) didn't exclude
  closed-instantiation-eligible candidates, so it wrongly reserved
  `__Get_calls` on behalf of a member like `Task<T?> Get<T>(string key)` —
  which never actually emits that name (its `Calls` field lives inside its
  own `__Get_State<T>` class instead). An unrelated sibling literally named
  `Get_calls` then collided with that phantom reservation, which fed into
  `isClosedInstantiationEligibleShape`'s own
  `!derivedNameCollisionMembers.Contains(method)` gate and incorrectly
  rejected the **whole interface**.
- **Finding B**: the `zeroArgExtensionSharers` collision-detection pre-pass
  (guarding against a real `CS0111` risk between a method and a
  differently-shaped same-named sibling) still assumed the pre-ADR-0049
  rule that a *solo* (non-overloaded) generic method's extension is always
  non-generic — so a solo closed-instantiation-eligible member's real
  generic arity was computed as `0`, indistinguishable from an unrelated
  zero-arg non-generic sibling (e.g. a `Get` property inherited from a
  different base interface). That false collision stripped the
  closed-instantiation member's configuration surface, and for an
  unconstrained `T Get<T>()` with no deterministic default, that meant
  whole-interface rejection.
- **A third instance of the same formula gap**, found while fixing B and
  not separately flagged by Codex: the same pre-pass forced `effectiveArity`
  to `0` for every non-overloaded method, assuming it always gets an
  ADR-0048-style zero-argument "compatibility" overload alongside any
  value-parameter one — untrue for a closed-instantiation-eligible member
  with real parameters, which gets *only* its real-parameter
  `Configure<T>()` (no compatibility overload). Fixed with the same
  conditional as finding B, so a with-parameters closed-instantiation
  member is now correctly excluded from zero-arg collision detection
  entirely, not just given the right arity.

Fixed by extracting the duplicated shape test into one shared
`IsClosedInstantiationEligibleCandidate` helper and rekeying all four call
sites (the two collision pre-passes, the ADR-0049-specific name-reservation
pre-pass, and the main loop's own eligibility check) off it, so the
definition can no longer drift between them the way it just had four times.
Two new regression tests reproduce Codex's own repros directly and assert
clean generation (`GeneratorTestHelpers.Verify`/`VerifyWithInfoDiagnostic`,
which both re-compile the real generated output). Full solution after this
fix: 2342/2342 tests passing, zero build warnings.

**PR #107 Codex review, round 4 (2026-08-23)** caught one more real gap in
`IsClosedInstantiationEligibleReturnShape`'s own BCL `Task<T>`/`ValueTask<T>`
identification: the check compared only `ContainingNamespace` and simple
`Name`, never `ContainingType` — a consumer's own nested type also named
`Task<T>`, declared inside some other type living in the
`System.Threading.Tasks` namespace (Codex's repro:
`System.Threading.Tasks.Container.Task<T>`), shares both of those with the
real BCL `Task<T>` (a namespace is the same regardless of nesting depth),
so it was misclassified as the supported shape. Downstream,
`TestDoubleDefaults` would then emit a real
`global::System.Threading.Tasks.Task.FromResult<T>(...)` default-value
expression for a member whose actual declared return type is the
unrelated nested type — a genuine type-mismatch compile error in
generated code. Fixed by additionally requiring `ContainingType is null`
(the real BCL `Task<T>`/`ValueTask<T>` are always top-level) — the minimum
fix Codex's own finding sanctioned, without needing to thread a
`Compilation` down to this static helper for a full
`GetTypeByMetadataName` comparison. A new regression test
(`GenericMethodReturningNestedTypeNamedLikeBclTask_ReportsUnsupportedGenericReturnShapeDiagnostic`)
reproduces the exact nested-type repro with `VerifyFailure`, proving it
now correctly falls back to whole-interface `CMP0031`. Note:
`TestDoubleDefaults.cs` has an identically-shaped, pre-existing check with
the same underlying imprecision (`ContainingNamespace`+`Name`, no
`ContainingType`) — not touched here, since it predates ADR-0049 and isn't
part of the shape this PR's own reachable code paths exercise; left as a
separate, unrelated latent issue outside this PR's scope, per this repo's
own deferral discipline for pre-existing issues merely surfaced by new
work. Full solution after this fix: 2344/2344 tests passing, zero build
warnings.

**PR #107 Codex review, round 5 (2026-08-23)** caught two more real gaps:

- **Finding 1 (P1)**: `TestDoubleDefaults`'s `ValueTask<T>` default-value
  expression used `new ValueTask<TResult>(inner)` — but `ValueTask<TResult>`
  has two constructors, `(TResult result)` and `(Task<TResult> task)`, and
  `inner` is frequently the bare `default` literal (any nullable-annotated
  reference or defaultable value type), which converts to *both* parameter
  types with no better-conversion tie-breaker — a real `CS0121`
  ambiguous-call compiler error in generated code. This was a **latent,
  pre-existing bug in `TestDoubleDefaults.cs` itself**, reachable by any
  defaultable `ValueTask<T>` member (not just a closed-instantiation
  one) — it had simply never been exercised by an existing test until
  ADR-0049 made `ValueTask<T>`/`ValueTask<T?>` the return type of a
  self-referencing generic member for the first time, unlike the
  otherwise-identical `Task<T>` branch (already unambiguous via
  `Task.FromResult<T>(...)`, a static method). Fixed by switching to the
  equally-unambiguous static `ValueTask.FromResult<TResult>(TResult)`
  factory — this one *was* fixed at the source for every `ValueTask<T>`
  default-generation call site, not scoped to closed-instantiation members
  only, since the bug itself was never scoped to them either.
- **Finding 2 (P2)**: round 4's `ContainingType is null` fix only ruled
  out a *nested* impostor sharing the BCL `Task<T>`'s namespace and simple
  name — it didn't cover a genuinely *top-level* consumer type reopening
  the same `System.Threading.Tasks` namespace with their own `Task<T>`
  (legal C# — a source-declared type is even permitted to shadow an
  imported one of the identical fully-qualified name, `CS0436`, a warning
  not an error). An interim fix comparing identity via the simpler,
  singular `Compilation.GetTypeByMetadataName` looked plausible but was
  **proven wrong by the regression test itself**: `GetTypeByMetadataName`
  follows the same "source wins" rule as ordinary C# name resolution
  rather than returning `null` for the ambiguity, so it silently returned
  the consumer's own shadow type — the fix appeared to do nothing, and
  running the test as `Verify()` (forcing a real recompile, not
  `VerifyFailure()`) caught a genuine `CS0029` in the generated code
  before the real fix was found. Fixed with a new `TaskWellKnownTypes`
  helper (mirroring the existing `CollectionWellKnownTypes` precedent)
  that resolves the real, externally-referenced BCL type via
  `Compilation.GetTypesByMetadataName` (plural — every candidate across
  every assembly) filtered to exclude any candidate declared in the
  current compilation's own assembly — the interface's own declared
  return type still resolves to the shadow (per the same source-wins
  rule), so the identity comparison now correctly fails and the member
  falls back to whole-interface `CMP0031`.

Both fixes verified with real regression tests that reproduce Codex's own
repros (one `Verify()`, proving the generated `ValueTask.FromResult<T?>`
expression actually compiles and dispatches; one `VerifyFailure()`,
proving the shadow-namespace shape correctly falls back). Full solution
after this fix: 2348/2348 tests passing, zero build warnings.

**PR #107 Codex review, round 6 (2026-08-23)** caught two more gaps, one
code, one docs:

- **Finding 1 (code)**: the generated state class's own type parameter
  can't be renamed away from the real method's type parameter identifier —
  it's baked verbatim into every pre-rendered type-string this candidate's
  slot/parameter types already use (Roslyn's `ToDisplayString`, not
  something this code chooses or can substitute). So when a consumer's own
  type parameter happens to be named identically to this file's derived
  state-class name (Codex's repro: `T Get<__Get_State>()` — the consumer's
  type parameter literally named `__Get_State`, coincidentally matching
  the derived name this PR's own convention produces for a member named
  `Get`), the generated `class __Get_State<__Get_State>` is `CS0694`
  ("type parameter has the same name as the type"), a real compiler error.
  Fixed by reserving the candidate's own type parameter name as an
  already-taken literal field name before the derived-name collision check
  runs — routes through the exact same collision-detection mechanism
  every other derived name in this file already uses, falling back to
  whole-interface `CMP0031` like any other derived-name collision, not a
  new exclusion mechanism.
- **Finding 2 (docs)**: `docs/packages/compono-testdoubles.md`'s "What it
  deliberately doesn't do" section still said, unchanged, that there was
  no support for "a generic method whose return type depends on its own
  type parameter" — directly contradicting the new "Per-closed-
  instantiation configuration" section just added earlier in the same
  file. Corrected to describe only the shapes still genuinely unsupported
  (deeper nesting, multiple type parameters), cross-referencing the new
  section and ADR-0049 for the now-supported narrower shape.

Regression test for finding 1 (`VerifyFailure`, reproducing Codex's exact
`__Get_State` repro) confirms the correct `CMP0031` fallback. Full
solution after this fix: 2350/2350 tests passing, zero build warnings.

**PR #107 Codex review, round 7 (2026-08-23)** caught three more gaps —
two of them direct follow-ons to round 6's own fix and round 5's own
declined-scope decision, both real:

- **Finding 1 (code, follow-on to round 6)**: round 6's fix for the
  `CS0694` self-collision reserved the candidate's own type parameter name
  into the *shared, interface-wide* `usedFieldNames` set — so an entirely
  unrelated method's own, differently-named type parameter merely
  happening to share that same string (not because anything actually
  collides, pure coincidence) wrongly poisoned the first method too
  (Codex's repro: `T Get<T>()` deriving `__Get_State`, alongside an
  unrelated `__Get_State Other<__Get_State>()` whose own type parameter
  literally has that name — `Other`'s own derived state-class name is
  `__Other_State`, never anywhere near `Get`'s declaration). Fixed by
  making the check strictly self-scoped — a candidate's own type
  parameter name is compared only against its *own* derived state-class
  name, never reserved into the shared pool at all. This is the third
  time this exact class of bug (a plausible-looking name-collision fix
  that's itself too broad or too narrow) has needed a follow-up round —
  worth naming as a pattern: any fix in this area needs a **second**
  regression test proving the fix doesn't *reject* something that should
  legitimately generate, not just one proving the original repro is
  caught.
- **Finding 2 (code, reopens round 4's declined scope)**: `TestDoubleDefaults.TryGetDefaultExpression`'s
  own `Task`/`ValueTask` identification had the identical
  namespace/simple-name-only imprecision the eligibility check in
  `TestDoubleAnalyzer` already had fixed (rounds 4–5) — and this function
  is reached by **any** member whose declared return type is
  `Task`/`Task<T>`/`ValueTask<T>`, not just a closed-instantiation-eligible
  one, so it was never actually gated behind ADR-0049's own new
  eligibility check at all. Round 4's notes explicitly declined to fix
  this exact function, reasoning it was "pre-existing... unrelated to the
  shape this PR's own reachable code paths exercise" — Codex's round-7
  finding directly refutes that framing with real evidence the function
  is reachable independent of ADR-0049 entirely, so it was fixed properly
  this time: `TaskWellKnownTypes` extended with `IsTask`/`IsTaskOfT`/`IsValueTaskOfT`
  granular checks, `TryGetDefaultExpression` now takes a `Compilation` and
  verifies real BCL identity for the `Task` (arity 0) and `Task<T>`/`ValueTask<T>`
  (arity 1) branches — the non-generic `ValueTask` branch needs no
  equivalent check, since its own default expression is the bare
  `default` literal, which references no type by name and target-types
  correctly against whatever the explicit implementation's own declared
  return type is, real or shadowed alike. A regression test proves this
  directly: an *ordinary, non-generic* member returning a shadowed
  `ValueTask<T>` now correctly falls through to the generic value-type
  `default` fallback (identity-agnostic, always safe) instead of the
  broken BCL-specific expression — `Verify()` (full recompile) passes
  clean.
- **Finding 3 (docs)**: `docs/reference/diagnostics.md`'s `CMP0031`
  section still described the diagnostic's cause using the *old*,
  pre-ADR-0049 wording (any self-referencing generic return, including
  the now-supported `T Get<T>()`/`Task<T> GetAsync<T>()` shapes) —
  directly contradicting both the new package-doc section and the actual
  implemented diagnostic boundary. Corrected to enumerate exactly the
  shapes still genuinely unsupported (deeper nesting, multiple type
  parameters, `allows ref struct`, ref-like/self-referencing real
  parameters, derived-name collisions), cross-referencing the
  now-supported narrower shape.

Full solution after this fix: 2354/2354 tests passing, zero build
warnings.

**PR #107 Codex review, round 8 (2026-08-23)** caught two more gaps, both
building directly on the last two rounds:

- **Finding 1 (code, second follow-on to round 6)**: fresh evidence beyond
  the state-class-name self-collision fixed in round 6/7 — a generic
  method's own name colliding with its own type parameter is `CS0694`
  too, not only a type's. Codex's repro: `__Get_Bucket Get<__Get_Bucket>()`,
  where the consumer's type parameter matches this file's derived
  *bucket method* name (not the state class name), producing `internal
  __Get_State<__Get_Bucket> __Get_Bucket<__Get_Bucket>()` — a method
  sharing its own name with its own type parameter. Fixed by extending
  the same self-scoped collision check (round 7's fix) to also cover the
  bucket method's derived name. The bucket dictionary *field*'s own name
  still needs no equivalent check — a field has no type parameter of its
  own to collide with.
- **Finding 2 (scope, not a bug)**: `Task<T?> Get<T>() where T : struct`
  (likewise `ValueTask<T?>`) reports `CMP0031`, not the closed-instantiation
  surface — for a value-type `T`, C# represents `T?` as the distinct
  generic type `System.Nullable<T>`, not as `T` with a nullable
  *annotation* (annotations apply only to reference types), so the
  eligibility check's direct symbol comparison against the method's own
  `T` never matches. Unlike every other round-6–8 finding, this is
  **safe fallback behavior, not broken generated code** — it correctly
  falls through to whole-interface `CMP0031`. Every real trivia-platform
  call site this ADR's evidence table cites is `where T : class`; per
  ADR-0029's evidence discipline, recognizing `Nullable<T>` would be new,
  unevidenced capability, not a bug fix to the shape actually designed
  and spiked. Resolved by clarifying scope rather than expanding
  capability: added ADR-0049 Amendment 1 naming this explicitly as an
  out-of-scope shape (same disposition as deeper nesting/multi-type-
  parameter returns), and corrected `docs/packages/compono-testdoubles.md`
  (both mentions) and `docs/reference/diagnostics.md`'s `CMP0031` section
  to state the `where T : class` precondition explicitly rather than
  implying `T?` works unconditionally.

Regression tests for both: finding 1 (`VerifyFailure`, reproducing
Codex's exact `__Get_Bucket` repro) confirms the correct `CMP0031`
fallback; finding 2 (`VerifyFailure`, `Task<T?> Get<T>() where T :
struct`) documents and locks in the current, correct, already-safe
behavior. Full solution after this fix: 2358/2358 tests passing, zero
build warnings.

**PR #107 Codex review, round 9 (2026-08-23)** caught three more real
gaps, all distinct from anything found before:

- **Finding 1 (code)**: the bucket lookup method's own local variable was
  hardcoded as `boxed` directly in the scriban template, never reserved
  collision-safely (unlike every other synthetic local, which is computed
  in the emitter via `SafeLocalName`). For `T Create<boxed>()`, the
  generated bucket method's own type parameter is *also* literally
  `boxed` — `out var boxed` inside a method whose own type parameter is
  `boxed` is `CS0412`. Fixed by computing this local's name in the
  emitter (`SafeLocalName("__boxed", ...)`, reserved against the method's
  own type parameter), matching every other synthetic local's precedent.
  This changed the generated identifier for **every** closed-
  instantiation member (not just the collision case — the default
  candidate name changed from `boxed` to `__boxed`, matching this repo's
  `__`-prefixed internal-identifier convention), so all 8 existing
  closed-instantiation snapshots needed re-accepting alongside the new
  regression test.
- **Finding 2 (code)**: `Task<T?> Get<T>()` with an *unconstrained* `T`
  (no `where` clause at all) reported the full closed-instantiation
  surface instead of `CMP0031` — the eligibility check's own newly-added
  Amendment 1 rule (`T?` requires `where T : class`, added in round 8)
  was never actually *enforced* in code, only documented. Unlike a
  value-type-constrained `T?` (Roslyn represents that as the distinct
  type `System.Nullable<T>`, already correctly rejected by the equality
  check failing outright), an *unconstrained* `T?` stays the *same*
  symbol `T` with a nullable annotation, which `SymbolEqualityComparer.Default`
  ignores — so the equality check silently passed, shipping the same
  unevidenced capability round 8 explicitly declined. Fixed by requiring
  `HasReferenceTypeConstraint` whenever the matched return position is
  nullable-annotated, for both the bare-`T` and `Task<T>`/`ValueTask<T>`-
  wrapped cases.
- **Finding 3 (code)**: the old, pre-ADR-0049 `derivedAuxiliaryNameOwners`
  pre-pass (over-approximate by design) didn't exclude a generic method
  whose own real parameter references its own type parameter
  (`void M<T>(T x)`) — categorically ineligible for ADR-0048 matching, so
  it will never emit *any* `_m_{param}` field for *any* parameter, not
  just the self-referencing one. Codex's repro: `void M<T>(T x_State)`'s
  phantom reservation `__M_m_x_State` (derived from a parameter merely
  named `x_State`, a name that member will never actually emit as a
  field) exactly matched an unrelated closed-instantiation-eligible
  member `U M_m_x<U>()`'s own real, actually-emitted derived state-class
  name — a false collision that rejected the whole interface even though
  the two members' generated names never conflict. Fixed by excluding a
  self-referencing-parameter generic method from this pre-pass entirely.

Regression tests for all three (`VerifyWithInfoDiagnostic`/`VerifyFailure`/
`VerifyWithInfoDiagnostic`, reproducing each of Codex's exact repros) —
the two `VerifyWithInfoDiagnostic` tests force a full recompile, proving
the generated code actually compiles, not just that the expected
diagnostic is present. Full solution after this fix: 2364/2364 tests
passing, zero build warnings.

**PR #107 Codex review, round 10 (2026-08-23)** caught two more real
gaps, both the same "hoisted/parallel check never updated for ADR-0049"
pattern as several earlier rounds:

- **Finding 1**: the self-scoped `CS0694` collision check (rounds 6–9)
  only compared the consumer's type parameter against the derived
  state-class/bucket-method *names* — not the state class's own *member*
  names. `Config Create<Config>()`'s type parameter matches the state
  class's own `Config` field literally (`internal ReturnConfig<Config>
  Config;` inside `class __Create_State<Config>`), a real declaration
  collision. Fixed by extending the same self-scoped check to also cover
  `Config` (always emitted) and, when the candidate has real parameters
  and isn't overloaded, `Calls`/`Lock`/`Matcher_{param}` (only emitted in
  that shape — checking against them for an overloaded/zero-parameter
  candidate would be a phantom check with nothing to actually collide
  with).
- **Finding 2**: `T ToString<T>() where T : class` reported `CMP0025`
  (object-member collision), not the closed-instantiation surface. A
  separate, *hoisted* copy of the object-collision check — evaluated
  earlier, specifically so a member with no deterministic default is
  still diagnosed correctly before `hasConfigurationSurface`/
  `defaultExpression` are computed — only exempted an *overloaded*
  generic member from the "solo member's extension is zero-arity,
  non-generic" object-collision assumption, mirroring an exemption this
  PR's very first commit already added to a *later*, separate copy of
  the same check. It was never updated to also exempt a *solo* closed-
  instantiation-eligible member, even though that member's own
  `Configure<T>()`/`Verify<T>()` extension is genuinely generic and
  distinguishable from `object.ToString()` — the same reasoning the later
  exemption already used. Fixed by mirroring that later exemption's exact
  shape in this hoisted copy too.

Regression tests for both (`VerifyFailure`/`VerifyWithInfoDiagnostic`,
reproducing Codex's exact repros) — the latter forces a full recompile,
proving the generated code actually compiles when calling the interface's
own generic `ToString<T>()` via an explicit type argument. Full solution
after this fix: 2368/2368 tests passing, zero build warnings.
