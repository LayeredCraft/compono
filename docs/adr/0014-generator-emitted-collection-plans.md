# [ADR-0014] Generator-Emitted Collection Plans Replace the Reflection-Based Dispatch Bridge

**Status:** Accepted

**Date:** 2026-07-28

**Decision Makers:** solo

## Context

[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
Amendment 2 specified a runtime collection-dispatch mechanism for the five
ADR-0013 collection shapes: a non-generic built-in `ICompositionProvider`
that, once it claimed a supported closed collection type, dispatched
element/key/value resolution through a cached, strongly-typed generic
delegate built once per closed collection type via
`MethodInfo.MakeGenericMethod` + `CreateDelegate`. That amendment argued
this was "categorically different from the arbitrary-user-type reflection
fallback [ADR-0001](0001-source-generation-first.md) prohibits" — a
narrow, fixed, cached exception, not the open-ended reflection fallback
ADR-0001 rules out.

A review before any Milestone 2 Phase 2 implementation had been written
against that design caught that this reasoning doesn't hold: a fixed,
bounded set of reflected shapes is still reflection introduced into
Compono's default runtime architecture, which the engineering-workflow
skill's `design-decisions.md` reference rule 4 (derived from ADR-0001)
doesn't carve an exception for. This ADR
retracts the collection-dispatch-bridge design and replaces it with a
generator-emitted mechanism instead — the same fix ADR-0001's own
Consequences section already named as the correct path for a shape the
generator doesn't yet cover, and the one Amendment 2 itself flagged as
"the specific, already-identified place to move to" if the reflection
bridge ever became unacceptable. It became unacceptable before any code
was written against it, so this ADR is that change, made pre-implementation
rather than as a later migration once real reflection-based code shipped.

This ADR was originally recorded as an in-place "Amendment 3" appended to
[ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)
itself. A second PR #11 review correctly flagged that as a violation of
this repo's actual documented rule (`design-decisions.md`: "don't edit
[an accepted ADR's] Decision/Rationale/Consequences... write a new ADR")
regardless of ADR-0010's own pre-existing Amendments 1/2, which predate
this PR and are themselves the same kind of drift from that rule, not a
sanctioned exception to it. This ADR is that correction done properly:
its own new, numbered, indexed record, with ADR-0010, ADR-0012, and
ADR-0013 each left completely untouched in their Decision/Amendment
content and only gaining a `Links`-section pointer here (metadata, not
decision content). ADR-0010's other decisions (the pipeline model, the
descriptor shape, diagnostics tracing) are unaffected and remain in
force; this ADR does not supersede ADR-0010 as a whole, only the
collection-dispatch-bridge sub-decision within it.

## Decision Drivers

- No reflection in the default runtime architecture
  ([ADR-0001](0001-source-generation-first.md)) — a fixed, bounded set of
  reflected shapes is still reflection, not an exception ADR-0001 grants.
- Native AOT/trimming safety — `MakeGenericMethod` over a value-type
  argument can require runtime code generation unavailable under Native
  AOT; Milestone 2's collection support shouldn't introduce a new AOT gap
  even though Native AOT certification itself is an MVP non-goal.
- Consistency with the dispatch mechanism [ADR-0004](0004-composition-plan-discovery-and-dispatch.md)
  already established for ordinary composable types (`PlanCache<T>`) — a
  closed-generic static field read, not a `Type`-keyed lookup or reflected
  invocation.
- Pipeline precedence must be preserved: explicit values, shared values,
  registrations, profile rules, semantic providers, and test-double
  providers must still get first refusal over collection handling
  ([ADR-0013](0013-collection-generation-semantics.md)'s "collections stay
  ordinary pipeline requests" decision).

## Considered Options

1. **Keep the reflection-based dispatch bridge** (ADR-0010 Amendment 2's
   original design) — `MakeGenericMethod` + `CreateDelegate`, cached per
   closed collection type, invoked from a non-generic built-in
   `ICompositionProvider`.
2. **Generator-emitted collection plans**, dispatched through a new
   closed-generic cache (`CollectionPlanCache<T>`) read directly inside
   `CompositionContext.ResolveCore<TValue>`, mirroring `PlanCache<T>`'s
   existing zero-reflection dispatch exactly.

## Decision Outcome

**Chosen: Option 2 — generator-emitted collection plans.**
`Compono.Generators` discovers the fixed Milestone 2 collection shapes in
the transitive composition graph and emits a strongly-typed collection
plan per closed shape, dispatched through a new closed-generic cache —
`CollectionPlanCache<T>` — read directly inside
`CompositionContext.ResolveCore<TValue>`, exactly the same zero-reflection
mechanism [ADR-0004](0004-composition-plan-discovery-and-dispatch.md)
already established for `PlanCache<T>`.

- **Discovery.** `TransitiveClosureWalker` (Milestone 1) is extended to
  recognize the five ADR-0013 shapes (`T[]`, `List<T>`, `IReadOnlyList<T>`,
  `HashSet<T>`, `Dictionary<TKey, TValue>`) wherever they appear as a
  constructor parameter or required member type in the walked graph —
  including nested inside another collection (`List<List<Address>>`), and
  including the *root* of `Composer.Create<T>()`/`[Composable]` itself,
  not only nested members. A recognized collection shape is **not**
  walked as an ordinary composable type (no constructor selection is
  attempted against `List<T>` itself); instead its element type (and key
  type, for `Dictionary`) is fed back into the same eligibility walk, so
  a composable element type still gets its own ordinary plan, a
  provider-resolved element type stays a bare `Resolve<TElement>()` call,
  and a nested collection element type recurses through this same
  collection-shape check.
- **Emission.** For each distinct closed collection type reached, the
  generator emits one `file`-scoped `ICompositionPlan<TCollection>`
  implementation (same `file`-scoping convention as an ordinary
  composition plan, per `coding-standards.md`'s "Generated code" section)
  whose `Compose` method builds the collection with ordinary, strongly
  typed C# — a `for` loop calling `context.Resolve<TElement>(descriptor)`
  per element/key/value — and registers itself via a module initializer
  into `CollectionPlanCache<TCollection>.Instance`, mirroring
  `PlanCache<T>`'s own registration shape exactly. No reflection, no
  `Activator`, no `Expression.Compile` appears anywhere in this path — the
  generator, not the runtime, is what turns a runtime-only-known `Type`
  into compile-time-known `TElement`/`TKey`/`TValue` type arguments,
  which is the same trick `PlanCache<T>` already relies on for ordinary
  composable types.
- **Dispatch stays at "stage 7."** `ICompositionProvider` is deliberately
  non-generic ([ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
  original Decision Outcome), so it cannot itself construct a
  `List<Address>` without either boxing/erasure or the reflection this
  ADR rejects — the same reason stage 8's `PlanCache<T>` dispatch was
  never modeled as a provider in the first place. Collection dispatch has
  the identical shape and gets the identical treatment: inside
  `CompositionContext.ResolveCore<TValue>`, immediately after the
  ordinary stage-7 `ICompositionProvider` collection (which still exists
  and still holds the primitive/enum/nullable-value providers) has
  declined, a direct `CollectionPlanCache<TValue>.Instance` field read is
  tried — an ordinary closed-generic field read, exactly like
  `PlanCache<TValue>.Instance`'s stage-8 read two lines below it — before
  falling through to stage 8. Stage 7 in the product-facing sense
  (`docs/architecture.md`'s 9-stage list) is unchanged: one stage, tried
  in the same position, still "built-in value providers, including
  collections." Only its internal implementation is now a provider
  collection *plus* one context-owned generic-dispatch check, the same
  hybrid shape stage 2/3-vs-4/5/6/7 already has at the model level.
  Because this check runs after stages 1–6, a registration, shared value,
  profile rule, semantic provider, or test-double provider can still
  claim a specific collection type ahead of the built-in collection plan,
  unchanged from [ADR-0013](0013-collection-generation-semantics.md)'s
  original "collections stay ordinary pipeline requests" decision —
  generated containing-type plans still call
  `context.Resolve<List<Address>>(descriptor)` exactly as they call
  `Resolve<TParam>()` for anything else; they never inline collection
  construction or know that a `CollectionPlanCache<T>` exists.
- **`CollectionElement`/`DictionaryKey`/`DictionaryValue` now flow through
  `CompositionRequestDescriptor`, reversing
  [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)/[ADR-0013](0013-collection-generation-semantics.md)'s
  original "provider constructs the segment directly" position.** Because
  the collection is now built by *generated* code living in the consumer
  assembly (not by an internal runtime provider), it only has the same
  public surface any other generated plan has —
  `ICompositionContext.Resolve<TValue>(in CompositionRequestDescriptor)`.
  `CompositionRequestKind` gains three cases — `CollectionElement`,
  `DictionaryKey`, `DictionaryValue` — and
  `CompositionContext.Resolve<TValue>` extends its descriptor-to-segment
  switch to cover them, using `Ordinal` as the segment's `Index` exactly
  as `ConstructorParameter`/`RequiredMember` already use it as their
  ordinal; `Name` is unused for these three kinds (`CompositionPath`'s
  existing display-string derivation never reads `Name` for them either).
  This is a direct correction to ADR-0012's Decision Outcome
  ("`CollectionElement`/`DictionaryKey`/`DictionaryValue` segments are
  never generator-supplied") and ADR-0013's Decision Outcome ("constructed
  directly by the collection provider itself... never via
  `CompositionRequestDescriptor`") — both written when a runtime provider,
  not generated code, was expected to build collections. `PathSegment`
  itself, its fork-key hashing, and the tag-collision test (ADR-0012
  Amendment 2) are unaffected: only *who constructs* the segment changed,
  not its shape or its role in hashing.
- **Bounded duplicate-value retry (`HashSet<T>` elements,
  `Dictionary<TKey, ...>` keys) lives in a small public runtime helper,
  `UniqueValueResolver`, callable from generated code.** The retry loop
  itself (ADR-0013's bounded-retry, fork-derived-per-attempt rule) can't
  live in the generator's emitted code directly without duplicating
  non-trivial logic into every generated collection plan — instead,
  `Compono` exposes one generic, reflection-free static method generated
  code calls once per element/key position:
  ```csharp
  public static class UniqueValueResolver
  {
      public const int MaxAttempts = 10;

      public static bool TryResolve<TValue>(
          ICompositionContext context,
          CompositionRequestKind kind,
          int position,
          Nullability nullability,
          HashSet<TValue> alreadyResolved,
          out TValue value);
  }
  ```
  `alreadyResolved` is the `HashSet<T>`/dictionary-key-tracking set being
  built — a successful call both returns the unique value and leaves it
  already added, so the generated plan doesn't separately re-add it. This
  is `public` for the same reason `CompositionRequestDescriptor`/
  `CompositionRequestKind`/`ICompositionContext` already are (ADR-0010's
  Visibility decision): it's a piece of the generated-code call surface,
  not part of the internal engine. Retry attempts stay deterministic:
  each attempt forks from a distinct, deterministic index derived from
  `(position, attempt)` — attempt `0` uses `position` unchanged (so a
  position that never collides forks identically to how it would with no
  retry mechanism at all, meaning tuning `MaxAttempts` never perturbs a
  non-colliding result), and every retry attempt (`attempt >= 1`) uses a
  negative, disjoint index — deterministic, and never coincides with any
  position's own base index — rather than a non-deterministic re-roll.
  Exhausting `MaxAttempts` is reported by the generated plan throwing
  `CompositionException` naming the element/key type and requested count,
  matching ADR-0013's original diagnostic wording exactly — this stays a
  thrown exception at the generated-plan/outward boundary (same shape as
  any other terminal pipeline failure) rather than a new
  `CompositionResult` case, since `ICompositionPlan<T>.Compose` returns a
  plain `T`, not a result type, just like any other generated `Compose`
  method.
- **Native AOT/trimming position.** No exception is needed: every
  collection plan is ordinary, fully AOT/trim-safe generated C#, same as
  any other composition plan.

### Positive Consequences

- Removes the one place Milestone 2's design would have introduced
  reflection into the runtime hot path — the whole engine stays
  consistent with ADR-0001's "no reflection by default" rule, not
  "no reflection by default, except this one bounded case."
- `CollectionPlanCache<T>`'s dispatch shape is identical to `PlanCache<T>`'s
  — one mental model for "how does Compono turn a runtime-only-known
  closed type into compile-time-typed construction," not two.

### Negative Consequences

- `TransitiveClosureWalker` picks up real new complexity (recursive
  collection-shape recognition, alongside its existing composable-type
  walk, and root-type collection classification) — accepted as the
  direct cost of moving this logic from a runtime bridge into the
  generator, where ADR-0001 says it belongs.
- `CollectionPlanCache<T>`, `UniqueValueResolver`, and three new
  `CompositionRequestKind` cases are new `public` surface — larger than
  the retracted bridge's shape, which kept everything `internal`. This is
  accepted the same way `CompositionRequestDescriptor`/`ICompositionContext`
  already are: generated code lives outside `Compono`'s assembly, so
  anything it must call is necessarily part of the public generated-code
  contract, not a discretionary API design choice.
- ADR-0012 and ADR-0013 are not re-opened or superseded by this ADR —
  both gain only a `Links`-section pointer to this ADR, with no Decision
  Outcome or Amendment content rewritten, per the "an accepted ADR is a
  historical record" rule; this ADR is the authoritative current shape
  for collection-segment construction and dispatch.
- `CollectionPlanCache<T>` inherits the same cross-assembly module-
  initializer collision property `PlanCache<T>` already has (last
  assembly's module initializer silently wins if two consuming
  assemblies in one process both discover a plan for the exact same
  closed type) — a pre-existing, unresolved property of the dispatch
  mechanism this ADR deliberately mirrors, not a new gap; see
  `docs/architecture.md`'s Open Architectural Decisions, "Cross-assembly
  plan-cache collision" entry.

## Pros and Cons of the Options

### Reflection-based dispatch bridge (rejected)

- Good, because it needed no generator changes — collection construction
  logic lives entirely in the runtime.
- Good, because it's the least new `public` surface (everything stays
  `internal`).
- Bad, because it introduces reflection (`MakeGenericMethod` +
  `CreateDelegate`) into the default runtime architecture, which
  ADR-0001 doesn't carve an exception for regardless of how narrow or
  cached the reflected surface is.
- Bad, because `MakeGenericMethod` over a value-type argument is not
  Native-AOT-safe in general, introducing an AOT gap this ADR's approach
  avoids entirely.

### Generator-emitted collection plans (chosen)

- Good, because it introduces zero reflection anywhere in the collection
  path — fully consistent with ADR-0001 and with `PlanCache<T>`'s
  existing dispatch shape.
- Good, because it's fully Native-AOT/trim-safe by construction, with no
  exception to document or later revisit.
- Bad, because it requires real new generator complexity
  (`TransitiveClosureWalker` collection-shape recognition, a new emitter
  and template) instead of confining the change to the runtime.
- Bad, because it needs new `public` runtime surface
  (`CollectionPlanCache<T>`, `UniqueValueResolver`, three
  `CompositionRequestKind` cases) that generated code depends on.

## Links

- [ADR-0001](0001-source-generation-first.md) — the no-reflection-by-default
  rule this ADR brings collection dispatch into compliance with.
- [ADR-0004](0004-composition-plan-discovery-and-dispatch.md) —
  `PlanCache<T>`'s zero-reflection closed-generic-field dispatch, the
  pattern `CollectionPlanCache<T>` mirrors exactly.
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  the pipeline stage model (stage 7) this ADR's dispatch check fits into;
  its own Amendment 2 originally specified the reflection-based bridge
  this ADR retracts and replaces. ADR-0010's other decisions are
  unaffected by this ADR.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  the active-construction-frame mechanism collection elements participate
  in without any collection-specific recursion logic (unchanged by this
  ADR).
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  `PathSegment`/`CompositionPath`, whose `CollectionElement`/
  `DictionaryKey`/`DictionaryValue` construction this ADR moves from a
  runtime provider to generated code.
- [ADR-0013](0013-collection-generation-semantics.md) — the collection
  generation semantics (default size, key uniqueness, ordering) this ADR
  implements the dispatch mechanism for, without changing any of them.
- `docs/plans/0002-milestone-2-core-composition-engine.md` — Phase 2,
  where this ADR's design was implemented.
