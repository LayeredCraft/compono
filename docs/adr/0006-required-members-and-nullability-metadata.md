# [ADR-0006] Required Members and Nullability Metadata

**Status:** Accepted

**Date:** 2026-07-28

**Decision Makers:** solo (nullability shape confirmed with the user via `AskUserQuestion`)

## Context

[PLAN-0001](../plans/0001-milestone-1-source-generation-foundation.md)'s
Phase 3 covers two remaining gaps in generated construction:

1. A type with `required` properties/fields whose selected constructor
   isn't `[SetsRequiredMembers]`-annotated currently fails with
   `CMP0007` (`ConstructorSelector.HasUnassignedRequiredMembers`) —
   Phase 0 explicitly deferred object-initializer emission rather than
   support it. Phase 3 removes that gap: emit the required-member
   assignments instead of rejecting the type.
2. Every generated `context.Resolve<T>()` call today is emitted from a
   `FullyQualifiedTypeName` built via
   `SymbolDisplayFormat.FullyQualifiedFormat`, which does not carry C#'s
   nullable-reference-type annotation. That's fine for Milestone 1's
   placeholder `ICompositionContext` (it never inspects the annotation),
   but it's a one-way information loss for Milestone 2's real
   `CompositionContext`: nullable-reference annotations are erased from
   the runtime generic type argument entirely (`Resolve<string>()` and a
   hypothetical `Resolve<string?>()` are the same closed generic method
   at runtime), so if the information isn't captured explicitly at
   codegen time, Milestone 2 can never recover "was this parameter
   `string` or `string?`" later. It has to be carried as data now or not
   at all.

## Decision Drivers

- `ICompositionContext` (`src/Compono/ICompositionContext.cs`) is
  documented as a deliberate Milestone 1 placeholder expected to be
  replaced by Milestone 2's real composition engine — a breaking
  signature change now is explicitly in-scope, not a compatibility
  concern.
- Reference-type nullable annotations are compile-time-only and erased
  from runtime generic instantiation — this is a hard constraint, not a
  design preference: whatever shape is chosen must carry the information
  as an explicit value, not rely on the generic type argument alone.
- Required-member emission should reuse the same
  discovery/validation/recursion machinery constructor parameters already
  go through (`ConstructorSelector`, `TransitiveClosureWalker`,
  `LeafTypeClassifier`) rather than a parallel path, per this repo's
  general preference for one validated pipeline over several.

## Considered Options

**Required members:**
1. Emit an object initializer after the constructor call for every
   required property/field the selected constructor doesn't already
   satisfy via `[SetsRequiredMembers]`.
2. Continue rejecting types with required members (status quo).

**Nullability metadata on `Resolve<T>()` calls** (all discussed with the
user):
1. Add a `Nullability` enum parameter: `Resolve<TValue>(Nullability
   nullability)`.
2. A parallel `ResolveNullable<TValue>()` method alongside the existing
   `Resolve<TValue>()`.
3. Defer any `ICompositionContext` signature change — track nullability
   in the generator's internal pipeline model only, still emit plain
   `Resolve<TValue>()` for now.

## Decision Outcome

**Required members** — chosen option: **emit an object initializer**.
`ConstructorSelector.HasUnassignedRequiredMembers` no longer rejects a
type outright; instead, every required property/field not covered by a
`[SetsRequiredMembers]` constructor is collected (walking the type and
its base types, same as today's check), validated with the same
parameter-kind rules `ValidateParameterKinds` already applies to
constructor parameters (no ref-like types, no pointers — `CMP0004`
reused for both), and each one's declared type goes through the same
`TransitiveClosureWalker`/`LeafTypeClassifier` treatment a constructor
parameter type does. The plan emits `new T(ctorArgs...) { Member1 =
context.Resolve<...>(...), ... }`. `CMP0007` is narrowed rather than
removed: it now fires only when a required member itself has an
unsupported shape that isn't a "reachable from an object initializer"
problem the reused parameter-kind diagnostics already cover (e.g., a
required member whose accessible init/set isn't reachable from the
consuming assembly).

**Nullability metadata** — chosen option: **add a `Nullability`
parameter to `ICompositionContext.Resolve<TValue>()`**, confirmed with
the user over a bare `bool`:

```csharp
namespace Compono;

public enum Nullability
{
    NotNullable,
    Nullable,
}

public interface ICompositionContext
{
    TValue Resolve<TValue>(Nullability nullability);
}
```

Every generated `Resolve<...>()` call site (constructor parameters and
required-member assignments alike) passes an explicit `Nullability`
argument derived from the parameter/member's Roslyn `NullableAnnotation`
(`Annotated` → `Nullability.Nullable`, anything else →
`Nullability.NotNullable`). The generic type argument itself stays
unannotated (`Resolve<string>(Nullability.Nullable)`, not
`Resolve<string?>(Nullability.Nullable)`) — the enum is the single source
of truth for the request's nullability rather than splitting it across
two places that could disagree, and it sidesteps `Resolve<T?>()` for a
value-type `TValue`, where a bare `?` means something else entirely
(`Nullable<T>`) than it does for a reference type.

`Nullability` is a new public two-value enum on the core `Compono`
package (not `Compono.Generators`) since `ICompositionContext` is public
API generated code compiles against. The user separately flagged a
richer future shape — a single `Resolve<TValue>(in CompositionRequest
request)` carrying nullability plus whatever else Milestone 2 needs — as
the likely eventual direction once the real composition engine exists;
this ADR deliberately takes the smaller incremental step suited to the
current placeholder interface rather than speculatively designing that
struct now. A Milestone 2 ADR revisiting `ICompositionContext`'s full
shape should treat that as prior art, not start from zero.

### Positive Consequences

- Required members (a very common shape for records/DTOs with
  init-only/required properties) stop being rejected outright, closing a
  real Milestone 1 exit-criteria gap (`docs/mvp.md`'s "including one with
  nested composable properties" implicitly covers required-property
  shapes too).
- Nullability information that would otherwise be permanently lost at
  codegen time is preserved for Milestone 2 to actually use, without
  guessing or re-deriving it from a generic type argument that can't
  carry it.
- Reusing `TransitiveClosureWalker`/`LeafTypeClassifier`/parameter-kind
  validation for required members means no second, divergent validation
  path to maintain.

### Negative Consequences

- `ICompositionContext.Resolve<TValue>()` is a breaking signature change
  for anyone who already implemented the Milestone 1 placeholder
  interface by hand — accepted per the Decision Drivers above; the
  interface's doc comment already sets this expectation.
- Every generated call site becomes marginally more verbose
  (`Resolve<string>(Nullability.NotNullable)` vs. `Resolve<string>()`) —
  accepted as the direct cost of not being able to recover the
  information later.

## Pros and Cons of the Options

### Object initializer for required members (chosen)

- Good, because it reuses the existing constructor-parameter pipeline
  end to end.
- Good, because it closes a real construction-shape gap rather than
  permanently excluding a common C# pattern.
- Bad, because it adds a second emission shape (initializer block) to
  the template beyond the constructor-argument list.

### `Nullability` enum parameter (chosen)

- Good, because generic type arguments can't carry the information at
  all (erasure) — an explicit value is the only option that actually
  works.
- Good, because it scales cleanly if more per-request metadata is needed
  later (additional enum values, or superseded by a request struct per
  the Links note below).
- Bad, because it's a breaking, slightly more verbose signature change to
  a (documented-placeholder) public interface.

### `ResolveNullable<TValue>()` parallel method

- Good, because it avoids changing `Resolve<TValue>()`'s existing shape.
- Bad, because the user's stated rationale is more accurate: nullability
  is metadata about a single kind of request, not a fundamentally
  different resolution operation, and two methods forces generated code
  and any future provider-pipeline logic to branch on which one was
  called rather than reading a value.

### Defer signature change, model-only

- Good, because it's the least invasive to `ICompositionContext` today.
- Bad, because it doesn't actually solve the Context's stated problem —
  Phase 3 exists specifically so a *future* `ICompositionContext` can
  observe this. Deferring the emitted call shape means Milestone 2 still
  has no way to recover per-call nullability from already-generated code
  without a regeneration, defeating the point of doing this in Milestone
  1 at all.

## Links

- [PLAN-0001](../plans/0001-milestone-1-source-generation-foundation.md) —
  Phase 3.
- [ADR-0002](0002-constructor-selection-algorithm.md) — constructor
  selection and parameter-kind validation this decision extends to
  required members.
- [ADR-0005](0005-generator-implementation-conventions.md) —
  implementation conventions (templating, pipeline models) this work
  follows.
- `src/Compono/ICompositionContext.cs` — the placeholder interface this
  ADR changes.
