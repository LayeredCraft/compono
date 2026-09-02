# [PLAN-0058] Compono.TestDoubles: Invocation-Aware Callback Responses

**Status:** In Progress

**Implements:** [ADR-0053](../adr/0053-testdoubles-invocation-aware-callback-responses.md)

## Goal

Generated doubles can compute a supported non-void method's response from its
real invocation arguments through a strongly typed, AOT-safe
`ReturnsCallback(...)` configuration.

## Scope

Implement ADR-0053's accepted member-specific builder design. Properties, void
methods, unsupported parameter shapes, bare-result async convenience overloads,
and general-purpose `CallInfo`/argument-bag APIs remain out of scope.

## Tasks

- [x] Accept and index ADR-0053; create and index this plan.
- [x] Add response-state clearing that preserves call count.
- [x] Extend the generator model/emitter/template with callback delegates,
      fields, builders, and dispatch.
- [x] Add snapshot and execution coverage across plain, matched, overloaded,
      DIM, required, async, delegate-returning, and closed-generic members.
- [x] Extend the sample and Native AOT smoke tests.
- [x] Update package, roadmap, and API-reference documentation.
- [ ] Run full build/test, packaged consumer, AOT, and alexa-vox-craft dogfood
      validation.
- [ ] Record verification and mark this plan Done.

## Critical Files

- `src/Compono/ReturnConfig.cs` — response reset without verification reset.
- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — collision-safe callback identifier allocation.
- `src/Compono.Generators/Models/TestDoubleMemberInfo.cs` — resolved callback identifier projection.
- `src/Compono.Generators/Templates/TestDouble.scriban` — generated storage,
  member-specific builders, and dispatch.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` — collision-safe names
  and template projection.

## Test Plan

Snapshot generated shapes, compile and execute each response/precedence path,
exercise a real packaged sample and Native AOT binary, then run the full solution
and the relevant dogfood consumer against freshly packed packages.

## Notes

Implementation notes and final command results will be recorded here as work
proceeds.

### 2026-08-30 — implementation and local verification

- `ReturnConfig<T>.ClearConfiguredResponse()` clears a static response without
  changing its verification count. Generated builders use it when installing a
  callback; ordinary response methods clear the callback before delegating to
  the existing runtime builder.
- The generator emits callback state for plain slots, ADR-0050 entries, and
  ADR-0049 closed-instantiation state. Matched dispatch copies the delegate
  while locked and invokes it after releasing the lock.
- Focused execution tests cover sync, `Task<T>`, `ValueTask<T>`, matched
  entries, delegate-return values, all response transitions, null rejection,
  propagated exceptions with recorded calls, cross-thread reentrancy, and
  unchanged property/void configuration. Closed-generic callback storage has
  generator snapshot/compile coverage; its runtime test remains blocked by
  the harness rejecting the existing configuration-required informational
  diagnostic for that shape.
- `dotnet build --no-restore` completed with 0 warnings and 0 errors. Direct
  test hosts passed for every project/TFM reached; generator tests passed
  306/306 on net10.0 and net11.0; sample tests passed 64/64; the packaged
  Native AOT smoke binary passed.
- The full direct test-host matrix was interrupted only by the pre-existing
  net8.0 `Compono.Logging.Tests.ConcurrencyTests.ReadsConcurrentWithWrites_NeverThrowOrCorrupt`
  host after more than four minutes (57 tests had passed, none failed). It is
  unrelated to this change.
- Required alexa-vox-craft dogfood validation is blocked: the available checkout
  at `/Users/jonasha/Repos/CSharp/alexa-vox-craft` lacks `Directory.Packages.props`,
  which `scripts/dogfood-validate.sh` requires, and its pipeline test is still
  NSubstitute-based rather than the documented FakePipelineBehavior variant.

### 2026-09-02 — callback generated-name collision fix

- Callback delegate, builder, and plain-slot callback-field names now reserve the generated
  double's existing declaration names. A collision falls back to a deterministic hash suffix shared
  by the callback name set; a snapshot covers `Foo` alongside `Foo_Callback`, `Foo_Builder`, and
  `Foo_callback` siblings.
- The generator test project passed 308/308 on both net10.0 and net11.0. A freshly packed
  `Compono`/`Compono.TestDoubles` consumer compiled and executed `ReturnsCallback` for that
  collision shape.
- `scripts/dogfood-validate.sh --consumer-repo /Users/jonasha/Repos/CSharp/alexa-vox-craft
  --packages 'Compono Compono.TestDoubles'` passed on the refreshed `main` checkout: 2,784 tests
  passed and 32 were skipped, with both packages resolved to the same freshly packed local version.

### 2026-09-02 — review validation and compatibility record

- Added execution coverage for ADR-0049 closed instantiations. The test allows the expected
  non-blocking `CMP0032` configuration-required diagnostic, configures separate `int` and
  `string` callbacks, and proves each closed type uses its own callback state at runtime.
- Ran the motivating `alexa-vox-craft` pipeline scenario in an isolated `main`-based worktree
  against freshly packed local `Compono` and `Compono.TestDoubles` packages. Replacing both
  hand-written `FakePipelineBehavior` instances with generated doubles configured through
  `ReturnsCallback` compiled; the targeted net11.0 test passed (1/1). The complete consumer
  suite also passed: 2,784 tests passed and 32 were skipped, with both packages resolved to the
  shared freshly packed local version.
- Generated output grows by about 40 lines per simple supported non-void member: a delegate,
  member-specific builder, callback field, dispatch branch, and changed configuration-extension
  return type. Parameter and generic complexity can increase line length, but the added code is
  fixed per member and the dispatch path remains strongly typed with no reflection or boxing.
- ADR-0053 Amendment 1 records the broader pre-1.0 source-compatibility impact and the rejected
  alternatives that would have preserved `ReturnConfigBuilder<T>` return types.
