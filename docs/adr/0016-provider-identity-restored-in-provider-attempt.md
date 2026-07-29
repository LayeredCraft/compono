# [ADR-0016] Provider Identity Restored in `ProviderAttempt`

**Status:** Accepted

**Date:** 2026-07-29

**Decision Makers:** solo

## Context

[ADR-0015](0015-provider-identity-deferred-in-provider-attempt.md)
deferred adding a provider-identity field to `ProviderAttempt`, reasoning
that "no stage in Milestone 2 ... registers more than one competing
provider yet." That premise was false the moment it was written: stage 7
(`BuiltInProviders.Default`) already registers three real providers
(`PrimitiveValueProvider`, `EnumValueProvider`, `NullableValueProvider`).
A PR #13 review round confirmed this directly and pushed back on the
deferral. This ADR reverses that decision.

The immediate trigger was a separate, already-fixed bug: an earlier round
of the same review found that a failing request's trace collapsed every
provider a stage tried into one aggregate entry, regardless of how many
were actually consulted. Fixing that (recording one entry per provider
tried) made the underlying problem visible rather than solving it: for an
unsupported type, stage 7's trace now correctly shows three attempts, but
all three are `(BuiltInProvider, NotHandled)` - indistinguishable from
each other. Knowing "three things were tried" without knowing *which*
three is only half of what ADR-0010's original Diagnostics tracing design
asked for ("a compact `ProviderAttempt` struct: stage id, **provider
id**, outcome enum").

Per [design-decisions.md](../../.claude/skills/engineering-workflow/references/design-decisions.md)'s
ADR-immutability rule, ADR-0015 is not edited - its Status changes to
"Superseded by ADR-0016" and its Decision/Rationale/Consequences text
stays exactly as written, an accurate record of the reasoning *at the
time*, including the premise that turned out to be wrong.

## Decision Drivers

- The deferral's own stated condition ("no stage has more than one
  competing provider") is false today, not hypothetically - stage 7 has
  three. Continuing to defer means shipping a public diagnostics API that
  can't answer "which provider" for the one stage that already needs it.
- `docs/architecture.md`'s Resolution Pipeline table already described
  stage 7 as holding three providers; the "no stage has more than one"
  claim was a drafting error against the codebase's own existing state,
  not a considered simplification.
- `ProviderAttempt` is still pre-1.0, `public`, and has exactly one real
  consumer relationship to break (nothing external depends on its current
  two-field shape yet) - the cost ADR-0015's Negative Consequences
  flagged for "adding this later" is at its lowest right now, not lower
  by waiting further.
- ADR-0015's own Option 2 (a provider index) is rejected again here for
  the same reason: not a stable identity once Milestone 3's builder makes
  providers reorderable. That reasoning wasn't wrong and doesn't need
  revisiting - only the "defer entirely" premise did.

## Considered Options

1. **A provider index** (position within the stage's provider list) -
   still rejected, per ADR-0015's own Option 2 reasoning: not stable once
   Milestone 3's builder allows reordering.
2. **An opaque provider-identity token on `ICompositionProvider`** - still
   rejected, per ADR-0015's own Option 3 reasoning: requires a new
   interface member with zero implementers needing it beyond identity.
3. **The provider's own concrete CLR `Type`** (`provider.GetType()`) as
   the identity, stored as a `Type?` field on `ProviderAttempt` (chosen).
4. **Keep deferring** - rejected: the premise it rested on is already
   false, and continuing to defer under a false premise isn't a
   defensible reason to leave a known gap open.

## Decision Outcome

Chosen option: "The provider's own concrete CLR `Type`" (Option 3).
`ProviderAttempt` becomes `(PipelineStage Stage, Type? Provider,
CompositionAttemptOutcome Outcome)` - `Provider` is `null` for a
context-owned stage (shared/scoped values, exact registrations,
collection-plan/generated-plan dispatch), which aren't
`ICompositionProvider` instances at all, and the concrete `provider.GetType()`
for an ordinary provider attempt.

This isn't the "reflection metadata" ADR-0010's original phrasing warned
against, and isn't a new precedent for this codebase: `Type` identity is
already load-bearing elsewhere in the exact same engine -
`PlanCache<T>`/`CollectionPlanCache<T>`'s closed-generic-field dispatch,
the active-construction-frame stack's `Type`-keyed lookup
(`_activeFrames.Contains(requestedType)`), and `EnumValueProvider`'s own
`ConditionalWeakTable<Type, Array>` cache. Holding a `Type` reference is
not a runtime *reflection operation* (no `MakeGenericMethod`, no
`Invoke`, no member enumeration) - it's the same kind of cheap, already-
resident identity comparison [ADR-0001](0001-source-generation-first.md)'s
no-reflection-by-default rule was never aimed at. `provider.GetType()` on
an already-constructed instance allocates nothing and costs nothing
beyond a field read.

Options 1 and 2 are rejected for the same reasons ADR-0015 already gave
them - this ADR doesn't relitigate those, only the "defer entirely"
premise that turned out to rest on a false claim.

### Positive Consequences

- The trace can now actually answer "which provider" for stage 7 today,
  not just "how many were tried" - closing the gap PR #13 review found.
- No new `ICompositionProvider` member, no index-stability problem to
  solve later, no string/allocation cost on the hot path.
- Consistent with `Type`-based identity already used elsewhere in this
  engine - not a new pattern this codebase has to learn.

### Negative Consequences

- `ProviderAttempt`'s constructor/deconstruction shape changes again (a
  second breaking change to this type within Milestone 2) - accepted
  since `Compono` is still pre-1.0 and no external consumer exists yet;
  this is exactly the "lowest cost is now, not later" driver above.
- A future opaque-token identity scheme (if `ICompositionProvider` ever
  needs one for an unrelated reason - e.g. a Milestone 3 builder wanting
  to reference "the provider registered at index N" by a stable handle)
  would still need its own design; `Type` doesn't solve that class of
  problem, only "which concrete provider type produced this attempt."

## Pros and Cons of the Options

### Option 3 — Provider's own concrete `Type` (chosen)

- Good, because it requires no new `ICompositionProvider` member.
- Good, because it's already an established identity pattern in this
  engine (`PlanCache<T>`, active-construction frames, `EnumValueProvider`'s
  cache).
- Good, because it costs nothing at runtime - a field read on an
  already-constructed instance.
- Bad, because two providers of the exact same concrete type registered
  twice in one stage (not possible today - `BuiltInProviders.Default` is
  a fixed, non-duplicated list) would be indistinguishable from each
  other. Not a real scenario in Milestone 2's scope; revisit if a later
  milestone's builder ever allows registering the same provider type
  more than once in one stage.

### Option 1 — Provider index

- Bad, because it isn't stable once providers become reorderable
  (Milestone 3) - unchanged from ADR-0015's own assessment.

### Option 2 — Opaque provider-identity token

- Bad, because it requires a new `ICompositionProvider` member with no
  real consumer beyond identity - unchanged from ADR-0015's own
  assessment.

## Links

- [ADR-0015](0015-provider-identity-deferred-in-provider-attempt.md) -
  superseded by this ADR. Its Decision/Rationale/Consequences are left
  unedited, an accurate record of the reasoning at the time, including
  the premise (later found false) it rested on.
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) -
  the original Diagnostics tracing design this ADR restores alignment
  with ("stage id, provider id, outcome enum"). Per the "an accepted ADR
  is a historical record" rule, ADR-0010's own text is not edited by this
  ADR either - this file is the sole cross-reference.
- [PLAN-0002](../plans/0002-milestone-2-core-composition-engine.md) -
  Phase 4's implementation notes, where this reversal is recorded.
- `docs/architecture.md` - Resolution Pipeline section, corrected in a
  prior PR #13 review round to no longer claim "no stage has more than
  one competing provider."
