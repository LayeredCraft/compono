# [ADR-0053] Compono.TestDoubles: Invocation-Aware Callback Responses

**Status:** Proposed

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
[ADR-0042 Amendment 2](0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-componononsubstitute-substitutability-is-a-goal-not-an-aspiration)
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

Per ADR-0029's own classification rules, "Roadmap candidate... A new
`Proposed` ADR records the problem only... for a future milestone's design
pass" - that is exactly this ADR's scope, and no further. No API is decided
here.

## Design evidence for a future dive (not decided here)

Captured during this migration's own investigation, as raw material for
whichever future design pass takes this up - none of it is `Accepted`
surface:

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
- Sync (`Func<..., T>`, auto-wrapped) and real-async (`Func<..., Task<T>>`)
  overloads would likely both be needed for a `Task<T>`-returning member,
  so a consumer isn't forced into awkward nested `Task` construction merely
  because the real member is asynchronous - not yet spiked.
- Overload-ambiguity risk when the member's own return type is itself a
  `Func<...>`/delegate shape is a real open question, not yet resolved by
  a compiler spike.
- `Match<T>`-based argument selection (ADR-0048) and multi-entry
  configuration (ADR-0050) both appear compatible in principle - a
  callback would simply be an additional response kind per registered
  entry, not a replacement mechanism.
- Call recording/`Verify()` semantics are unaffected either way -
  `RecordCall()` already happens independently of how the return value is
  produced.

None of the above is an accepted design. A future deep dive
(`design-decisions.md`'s process) starts from this evidence, not from
scratch, but still owns the actual API decision, including whether the
sketch above survives contact with a real compiler spike.

## Links

- [RESEARCH-0011](../research/0011-alexa-vox-craft-mediatr-tests-testkit-migration-slice-1.md) -
  the migration evidence this ADR formalizes.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) -
  the Gap decision rubric applied above.
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) and its
  Amendment 2 - the admission-level non-goal this finding sits against, and
  the override policy that classifies it a roadmap candidate despite low
  frequency.
- `docs/packages/compono-testdoubles.md`'s "What it deliberately doesn't
  do" - the existing, still-accurate non-goal statement (unchanged by this
  ADR; this ADR records a roadmap candidate, not a decision to build
  anything yet).
- `test/AlexaVoxCraft.MediatR.Tests/TestKit/FakeDelegates.cs`
  (`alexa-vox-craft`) - `FakePipelineBehavior`, the accepted interim
  workaround while this roadmap item is unresolved.
