# [ADR-0053] Compono.TestDoubles: Invocation-Aware Callback Responses

**Status:** Accepted

**Date:** 2026-08-25

**Decision Makers:** solo

## Context

`alexa-vox-craft`'s `AlexaVoxCraft.MediatR.Tests` migration (PLAN-0051 Task
10 slice 1, [RESEARCH-0011](../research/0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md))
surfaced a `Compono.TestDoubles` capability gap while converting
`Wrappers/RequestHandlerWrapperTests.cs`'s
`Handle_WithPipelineBehaviors_ExecutesBehaviorsInReverseOrder` off
NSubstitute. The pre-migration test configured two `IPipelineBehavior`
mocks with an invocation-aware callback:

```csharp
behavior1.Handle(handlerInput, Arg.Any<CancellationToken>(), Arg.Any<RequestHandlerDelegate>())
    .Returns(async call =>
    {
        executionOrder.Add("Behavior1-Start");
        var result = await call.Arg<RequestHandlerDelegate>()();
        executionOrder.Add("Behavior1-End");
        return result;
    });
```

`Compono.TestDoubles` deliberately has no `Returns(Func<CallInfo, T>)`-style
callback response - `docs/packages/compono-testdoubles.md`'s "What it
deliberately doesn't do" already documents "no `Returns(Func<...>)`
callbacks" as a non-goal, tracing to this package's own admission-level
scope decision ([ADR-0042](0042-compono-owned-source-generated-test-doubles.md)'s
Option 3, explicitly rejecting a general-purpose-mocking-framework Option 2
that named "callbacks" as out of scope). There is no way for a generated
double to invoke a captured delegate argument and record a side effect
around it, or otherwise compute a return value from the real invocation's
own arguments.

`IPipelineBehavior` is an ordinary interface `Compono.TestDoubles` otherwise
fully supports (unlike `RequestHandlerDelegate`/`SkillRequestFactory`, which
this same migration also hand-fakes, but for the unrelated, already-decided
reason that Compono.TestDoubles doesn't generate doubles for delegate types
at all, ADR-0042's Non-Goals) - the gap here is specifically the *response
kind* (a static configured value vs. an invocation-aware callback), not the
interface shape.

**Working project-local workaround** (applied, in place): a hand-written
`FakePipelineBehavior` class implementing `IPipelineBehavior` directly (10
lines, one member), used only by this one test
(`test/AlexaVoxCraft.MediatR.Tests/TestKit/FakeDelegates.cs`).

## Applying ADR-0029's Gap decision rubric

1. **Observed frequency.** Searched `alexa-vox-craft`'s complete git
   history for every NSubstitute callback-style pattern
   (`.Returns(async call =>`, `.Returns(x =>`, `call.Arg<T>()`,
   `ReturnsForAnyArgs`, `.When().Do()`). Found **exactly one** distinct
   scenario, ever: this one test, configuring the identical callback shape
   twice (`behavior1`/`behavior2`) within that single test method. No
   other project in the repo (InSkillPurchasing.Tests, Smapi.Tests, or any
   other) ever used this pattern.
2. **Was this ever intended to work?** No. `docs/packages/compono-testdoubles.md`
   already documents the non-goal; this is not a bug.
3. **Workaround cost.** Low. `FakePipelineBehavior` is a small,
   self-contained, readable class - `IPipelineBehavior` has exactly one
   member, so there's no interface-implementation boilerplate to speak of.
   Not a case of "every consumer would need to invent its own equivalent"
   - only one consumer, one test, has needed this so far.
4. **Principle alignment.** No conflict with
   [ADR-0001](0001-source-generation-first.md)'s no-reflection posture - a
   strongly-typed callback whose parameters mirror the real member's own
   signature (see "Design evidence for a future dive" below) is fully
   expressible via source generation, no `CallInfo`/reflection/boxing
   required.

Taken on its own, (1) and (3) would normally point toward "intentional
design difference" under ADR-0029's ordinary frequency/cost weighting - one
real site, cheap workaround. **That weighting does not apply here.**
[ADR-0042 Amendment 2](0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration)
already established a binding override for this exact category: `Compono.NSubstitute`
resolves an interface to a real `NSubstitute.Substitute.For<T>()` instance
(confirmed by reading `src/Compono.NSubstitute/NSubstituteProvider.cs` - it
is a thin provider with no capability restriction of its own), so it can
satisfy this callback shape natively, where `Compono.TestDoubles` cannot.
Per that Amendment, "any real, evidenced case... where `Compono.NSubstitute`
can satisfy an interface or member shape that `Compono.TestDoubles` cannot
is, by definition, a roadmap candidate... rarity is not, on its own, a
valid reason to classify [it] as acceptable alternative... One real
occurrence, in one real project, is sufficient evidence under this policy."

**Classification: Roadmap candidate**, per ADR-0042 Amendment 2's override
of ADR-0029's ordinary frequency discretion for this category - not per
ADR-0029's own general weighting, which alone would not have crossed the
bar on a single occurrence.

Per ADR-0029's own classification rules, this began as a roadmap candidate.
The design pass has now accepted the source-generated, member-specific
callback surface below.

## Decision Outcome

For every supported non-void method that already has a configuration surface,
the generator emits a collision-safe delegate matching the method's real
parameter list and declared return type, plus a member-specific configuration
builder. `Configure().Member(...)` returns that builder, which preserves
`Returns`, `Throws`, and `ReturnsSequence` and adds the explicit
`ReturnsCallback(...)` response kind.

The callback receives the invocation's actual arguments in declaration order
and returns the member's declared return type. A `Task<T>`/`ValueTask<T>` member
therefore accepts a callback returning that same task-like type; no bare-result
auto-wrapping overload is added. The explicit `ReturnsCallback` name avoids
ambiguity when the member itself returns a delegate.

Callback storage belongs to the same response owner as every other configured
response: the plain member slot, the matched ADR-0050 entry, or the ADR-0049
closed-instantiation state. It is mutually exclusive with value, exception,
and sequence responses under the existing last-configuration-wins contract.
Call recording happens before response dispatch. For matched entries, callback
selection happens under the existing entry lock, but user callback code runs
after that lock is released.

Properties and void methods retain `ReturnConfigBuilder<T>` unchanged.
Existing exclusions (`ref`/`out`/`in`, pointers, events, indexers, and generic
method shapes that cannot retain a callback delegate per closed type without a
new state model) remain unchanged. The existing ADR-0049 closed-instantiation
generic shape is supported because it already owns state per closed type; other
generic methods retain their existing static response surface. Explicitly typing the result of
`Configure().Member(...)` as `ReturnConfigBuilder<T>` is not preserved; the
pre-1.0 compatibility requirement covers the normal fluent chaining surface.

## Considered Options

- **Generated member-specific builder (chosen):** preserves a strongly typed,
  discoverable fluent configuration surface without reflection or boxing.
- **Separate callback configuration method:** less generated state, but splits
  one member's response configuration across two unrelated paths.
- **Overload `Returns`:** superficially matches NSubstitute, but becomes
  ambiguous when the declared return value is itself a delegate. The explicit
  `ReturnsCallback` name is predictable for every supported return type.

## Consequences

- Generated source grows by one delegate and one small builder per supported
  non-void method.
- The shared runtime response slot needs a public, generated-code-facing reset
  operation that clears response state without clearing verification count.
- Callback closures allocate according to ordinary C# delegate semantics; the
  dispatch path adds no reflection, `DynamicInvoke`, argument bag, or boxing.
- Callback exceptions and task faults preserve normal C# behavior.

## Design evidence retained from the original roadmap-candidate pass

Captured during the migration investigation; the accepted outcome above
resolves these original questions:

- A source-generated, strongly-typed callback appears technically
  feasible: since every real parameter type of a member is already known
  at generation time, a per-member callback delegate (conceptually
  `Func<TArg1, ..., TResult>`, or `Func<..., Task<TResult>>` for an async
  member) could be generated without reflection, `CallInfo`, or boxing -
  unlike NSubstitute's untyped argument bag.
- The callback would need a dedicated per-member field (it can't live in
  the existing shared generic `ReturnConfig<T>` struct, whose only type
  parameter is the return/slot type, not the member's real parameter
  types) - the same per-member-generated-shape precedent
  [ADR-0050](0050-testdoubles-multi-entry-argument-distinguished-configuration.md)
  already established for multi-entry response configuration.
- Sync result auto-wrapping and untyped generic callback state were rejected:
  callbacks return the declared member type exactly, and only generic shapes
  with ADR-0049's existing per-closed-type storage participate.
- `Match<T>`-based argument selection (ADR-0048) and multi-entry
  configuration (ADR-0050) both appear compatible in principle - a
  callback would simply be an additional response kind per registered
  entry, not a replacement mechanism.
- Call recording/`Verify()` semantics are unaffected either way -
  `RecordCall()` already happens independently of how the return value is
  produced.

The implementation plan and verification record live in
[PLAN-0058](../plans/0058-testdoubles-invocation-aware-callback-responses.md).

## Amendment 1 (2026-09-02): Compatibility scope and considered alternatives

Review clarified that the source-compatibility impact is broader than an
explicit `ReturnsCallback(...)` call. Every supported non-void method with a
configuration surface now returns its generated member-specific builder from
`Configure().Member(...)`, whether or not the caller uses callbacks. Ordinary
fluent calls (`Returns`, `Throws`, and `ReturnsSequence`) remain available, but
project-local helpers or extension methods explicitly typed as
`ReturnConfigBuilder<T>` no longer bind for those members. This is accepted for
the pre-1.0 release; it must be stated precisely in release and migration
material rather than described as callback-only breakage.

The fuller alternatives considered are:

- **Generated member-specific builder (chosen):** keeps all response kinds on
  one strongly typed member configuration path. It carries the member's real
  parameter types, so callbacks remain AOT-safe and avoid reflection, boxing,
  and an untyped invocation bag. Its cost is the source-compatibility break
  above and generated code per supported member.
- **Separate callback configuration path:** retain
  `Configure().Member(...) -> ReturnConfigBuilder<T>` and add a distinct
  generated callback entry point. That preserves helpers typed to the existing
  builder, but splits the same member's mutually-exclusive response state
  across two APIs and makes configuration discovery less direct.
- **Add `ReturnsCallback` to `ReturnConfigBuilder<T>`:** rejected because that
  shared builder knows only `T`, not a member's argument types. Supporting
  callbacks there would require an untyped argument bag, reflection, or boxing,
  contrary to the decision's source-generated and strongly-typed constraints.
- **Overload `Returns`:** rejected because a member returning a delegate makes
  the value and callback cases ambiguous. A separately named
  `ReturnsCallback` remains explicit for all supported return types.

The ADR-0049 closed-instantiation path remains part of the chosen design:
its generated builder carries the closed type argument and stores callback
state in that type's existing bucket.

## Links

- [RESEARCH-0011](../research/0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md) -
  the migration evidence this ADR formalizes.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) -
  the Gap decision rubric applied above.
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) and its
  Amendment 2 - the admission-level non-goal this finding sits against, and
  the override policy that classifies it a roadmap candidate despite low
  frequency.
- `docs/packages/compono-testdoubles.md` - the package guide describing the
  accepted callback surface and its intentional limits.
- `test/AlexaVoxCraft.MediatR.Tests/TestKit/FakeDelegates.cs`
  (`alexa-vox-craft`) - the motivating `FakePipelineBehavior` to replace
  during dogfood validation.
