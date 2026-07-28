# Compono Architecture

## Overview

Compono is a modular test composition framework.

The architecture is centered on a `CompositionContext`.

The context represents one active composition operation and coordinates:

- The deterministic seed
- Random streams
- Scope and shared instances
- Registrations
- Profiles
- Providers
- Generated composition plans
- The current request path
- Diagnostics
- Cancellation and runtime state

The context is the internal center of the system even when the public API exposes simpler concepts such as `Composer`, `Create<T>()`, or a test-framework attribute.

## Architectural Shape

```text
Consumer API
    |
    v
Composer / Test Framework Integration
    |
    v
CompositionContext
    |
    +--> Explicit Values
    +--> Shared Scope
    +--> Registrations
    +--> Profile Rules
    +--> Semantic Value Providers
    +--> Test Double Providers
    +--> Built-in Value Providers
    +--> Generated Composition Plans
    |
    v
Composed Result or Diagnostic Failure
```

## Composition Context

A composition context should contain all state required to resolve a graph without relying on mutable global configuration.

Conceptually:

```csharp
public interface ICompositionContext
{
    T Resolve<T>(in CompositionRequestDescriptor descriptor);
}
```

`ICompositionContext` is `public` — generated plan code (in the consumer's
own assembly) calls it directly, so it has to be. Everything the context
*owns* (seed, scope, path, random source, active construction frames,
provider pipeline) is deliberately not exposed as properties on this
interface — the context is internal state generated code never touches
directly, per
[ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md).
Resolution is synchronous: every provider planned for the MVP is
in-memory/CPU-bound, and Milestone 1 already shipped a synchronous
`ICompositionPlan<T>.Compose`. A genuinely async provider need, if one
ever arises, gets its own distinct opt-in contract rather than reworking
this one.

### Context lifetime

A new root context should normally be created for:

- A call to `Create<T>()`
- A call to `CreateMany<T>()`
- One xUnit theory row
- One explicit composition scope

Nested requests should derive child contexts or child paths without losing the root seed, scope, or diagnostics.

## Composition Requests

Every value is resolved from a rich request rather than only a `Type` —
but generated code never constructs that rich request directly. Per
[ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md),
there are two distinct shapes:

- **`CompositionRequestDescriptor`** (`public`) — the small, compact,
  compile-time-constructible value generated plan code actually passes:
  ```csharp
  public readonly record struct CompositionRequestDescriptor(
      CompositionRequestKind Kind,   // ConstructorParameter | RequiredMember
      string Name,
      Nullability Nullability);
  ```
- **`CompositionRequest`** (`internal`) — the richer record the context
  expands a descriptor into, by appending a `PathSegment`
  ([ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md))
  derived from the descriptor to its own current path. This is what the
  internal provider pipeline actually operates on; generated code never
  sees or builds one.

Only fields with a real Milestone 2 consumer exist today — `RequestedType`,
`Nullability`, the descriptor's `Kind`/`Name` (folded into the request's
path segment), `Path`, `IsShared`. `CustomAttributes`, generic context,
requested lifetime, semantic hints, and "whether a test double is
acceptable" are deliberately not modeled yet — they get added once a
later milestone (xUnit inline values, Bogus hints, NSubstitute
eligibility) has an actual consumer for them, not speculatively now.

Generated plans avoid requiring runtime reflection merely to construct
this metadata — the descriptor is a plain, compiler-emittable value, and
path/type expansion happens entirely inside the context.

## Resolution Pipeline

The default resolution order is:

1. Explicit values
2. Shared or scoped values
3. Exact registrations
4. Profile rules
5. Semantic value providers
6. Test-double providers
7. Built-in value providers
8. Generated object composition plans
9. Diagnostic failure

This precedence is part of the product contract, and stage *order* is
fixed — not configurable, by users or by providers reordering themselves.
But per
[ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md),
not every stage is the same *kind* of thing:

| # | Stage | Kind |
|---|---|---|
| 1 | Explicit values | Context-owned deterministic check (no consumer until a later milestone's inline-value API exists) |
| 2 | Shared or scoped values | Context-owned deterministic check against the scope |
| 3 | Exact registrations | Context-owned deterministic lookup (Milestone 2: internal only, no public builder yet) |
| 4 | Profile rules | Ordered `ICompositionProvider` collection — empty until Milestone 3 |
| 5 | Semantic value providers | Ordered `ICompositionProvider` collection — empty until Milestone 6 (Bogus) |
| 6 | Test-double providers | Ordered `ICompositionProvider` collection — empty until Milestone 5 (NSubstitute) |
| 7 | Built-in value providers | Ordered `ICompositionProvider` collection, populated internally by `Compono` itself |
| 8 | Generated composition plans | Context-owned deterministic dispatch via `PlanCache<T>` — **not** an `ICompositionProvider` (see Source-Generated Composition Plans, below) |
| 9 | Diagnostic failure | Context-owned terminal stage |

Only stages 4/6/7 hold an actual ordered collection of providers in
Milestone 2 (and of those, only 7 has anything registered in it — 4/5/6
are wired but empty until their owning milestone). Provider order
*within* an extensible stage is registration order; no richer ordering
rule exists yet because no stage has more than one competing provider to
order.

## Providers

Providers satisfy composition requests within one of the extensible
pipeline stages above (4/5/6/7) — the context-owned stages (1/2/3/8/9)
are not providers and don't implement this interface.

A provider is independently replaceable and reports whether it:

- Did not apply (`NotHandled`)
- Successfully composed a value (`Success`)

Conceptually:

```csharp
internal interface ICompositionProvider
{
    CompositionResult TryCompose(
        CompositionRequest request,
        ICompositionContext context);
}
```

Ordinary providers **cannot** report `Failure` — the type only gives them
`NotHandled`/`Success` to return. `Failure` is reserved for the
context-owned authoritative stages (an exact registration whose factory
throws, generated-plan dispatch when a plan exists but fails or a
recursion cycle is detected) — the rule, per
[ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md):
`Failure` means "authoritative ownership was established, but resolution
could not complete," never a stronger form of `NotHandled`. This is what
stops a provider that merely can't produce *this* particular request from
accidentally blocking a later stage (or a generated plan) that could
have. This avoids exception-driven provider selection and preserves
meaningful failures.

## Source-Generated Composition Plans

Source generation is the preferred construction strategy.

For a constructible type, the generator should emit a plan that:

- Selects the constructor
- Requests constructor arguments
- Invokes the constructor directly
- Assigns required or configured members
- Preserves nullability and member context
- Produces diagnostic metadata
- Registers the plan with the runtime

Conceptually:

```csharp
internal sealed class CustomerCompositionPlan
    : ICompositionPlan<Customer>
{
    public Customer Compose(ICompositionContext context)
    {
        var firstName = context.Resolve<string>(
            new CompositionRequestDescriptor(
                CompositionRequestKind.ConstructorParameter,
                "firstName",
                Nullability.NotNull));

        var lastName = context.Resolve<string>(
            new CompositionRequestDescriptor(
                CompositionRequestKind.ConstructorParameter,
                "lastName",
                Nullability.NotNull));

        return new Customer(firstName, lastName);
    }
}
```

Generated code only ever calls `context.Resolve<T>(descriptor)` per
member — it never constructs a `CompositionRequest`, touches
`CompositionPath`, or manages recursion state directly. The context owns
all of that internally
([ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)),
which is what makes incorrect path propagation structurally difficult
rather than merely documented against. The final generated code may use
lower-level APIs for performance.

### Discovery and Dispatch

How the generator decides a type needs the plan above, and how
`Create<T>()` reaches it without reflection, is
[ADR-0004](adr/0004-composition-plan-discovery-and-dispatch.md): discovery
walks `Create<T>()`/`CreateMany<T>()` call sites and their types'
transitive constructor parameters, with `[Composable]` as an opt-in marker
for a type with no local call site — applied directly to a type this
compilation owns, or at assembly level
(`[assembly: Composable(typeof(SomeType))]`) for a type in a referenced
assembly that can't be annotated directly. Both forms are equivalent
plan-generation requests, deduplicated alongside call-site discovery.
"Registers the plan with the runtime"
above means a generated module initializer populates a closed-generic
static field (`PlanCache<Customer>.Instance = ...`) that `Create<T>()`
reads directly — not a `typeof(T)`-keyed dictionary lookup.

A generated plan never redispatches into itself directly — each
`context.Resolve<T>(descriptor)` call it makes is a fresh pipeline
evaluation for whatever type that member actually is, not a recursive
call back into the same plan. A genuinely self-referencing type (e.g. a
`Node` with a `Node` property) only becomes a problem if nothing earlier
in the pipeline (an explicit value, a shared value, a registration)
terminates it before generated-plan dispatch is reached a second time for
the same type while the first invocation is still on the stack — see
Recursion Detection, below.

### Generator responsibilities

The generator should identify:

- Accessible constructors
- Primary constructors
- Required members
- Init-only members
- Nullability metadata
- Unsupported types
- Ambiguous construction paths
- Cyclic compile-time dependencies where detectable

### Runtime responsibilities

The runtime should:

- Execute generated plans
- Resolve provider-backed values
- Manage scopes
- Manage deterministic random streams
- Track the composition path
- Produce runtime diagnostics

## Runtime Reflection Policy

The reflection policy is intentionally undecided.

Candidate approaches:

### Generated plans required

Composition fails when no generated plan exists.

Advantages:

- Predictable performance
- Strong trimming and AOT characteristics
- Simple runtime model

Tradeoffs:

- External or dynamically discovered types may require explicit support
- Some test scenarios may be less convenient

### Automatic reflection fallback

The runtime reflects when no generated plan exists.

Advantages:

- High compatibility
- Lower migration friction

Tradeoffs:

- More complex runtime
- Weaker AOT guarantees
- Performance becomes less predictable
- Reflection can hide source-generation gaps

### Opt-in compatibility package or mode

Reflection support is isolated from the default runtime.

Advantages:

- Keeps the core architecture clean
- Allows compatibility where necessary
- Makes performance tradeoffs explicit

This is the current leading compromise, but it is not yet an accepted decision.

## Scopes and Shared Values

A composition scope stores values that should be reused during an active composition.

Examples:

- A repository parameter shared with the system under test
- A fake clock reused throughout an object graph
- A substitute reused by multiple dependencies

Scope semantics must be explicit.

Resolved for Milestone 2 by
[ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md):
one scope per root composition operation (one `Create<T>()` call, or one
item of a `CreateMany<T>()` call — each item gets its own independent
scope, not a scope shared across the batch). Sharing is type-keyed only
for Milestone 2; name/qualifier-based sharing is deferred until a
Milestone 4 `[Shared]`-attribute use case needs it. A broader "test case"
or "user-created scope" lifetime is deferred until Milestone 4 has a
concrete consumer to design against, rather than building the general
menu of possible lifetimes below speculatively:

- Request
- Composition graph (Milestone 2's chosen lifetime)
- Test case
- User-created scope

The MVP should begin with one clear shared lifetime rather than a general-purpose dependency injection lifetime system.

### Recursion Detection

A repeated *type* appearing twice in a graph (two sibling properties of
the same type, or the same type reachable via two different paths) is
ordinary graph shape, not a cycle. A genuine cycle is a type whose
*construction* is still actively in progress when it's requested again.
[ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md)
keeps these deliberately separate: `CompositionPath` (below) records
every request edge for diagnostics and random forking, while a distinct
internal **active-construction-frame** stack is pushed only around
structural construction (generated-plan dispatch, stage 8) and checked
only there — after explicit values, shared/scoped values, and exact
registrations have already had a chance to terminate the graph. A
self-referencing type resolved by a registered or shared instance never
touches the recursion mechanism at all; only an actual in-progress
construction cycle does, and the resulting diagnostic reports the chain
of active frames — the request edges that formed the cycle — not just a
list of repeated types.

## Profiles

Profiles provide reusable configuration without mutable global state.

A profile may:

- Add providers
- Add registrations
- Configure collection sizes
- Configure nullability behavior
- Enable integration packages
- Add type or member rules

Profiles should be immutable after construction or compiled into immutable runtime configuration.

## Deterministic Randomness

The root context owns the seed.

Random sources should be forkable by stable keys:

```text
root seed
└── test parameter: command
    └── Customer
        └── Email
```

This reduces accidental changes when unrelated members are added elsewhere in a graph.

Resolved by [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md):
`CompositionPath` is a chain of structured `PathSegment`s — not just
types — so two constructor parameters or members of the same type
(`Customer(string FirstName, string LastName)`) fork independently
instead of colliding on an identical key. Forking hashes the structured
segment data directly (a per-kind tag plus its name or index) via FNV-1a,
never a formatted display string, which is what makes the fork key
collision-free by construction rather than by careful string-escaping.
That structured state feeds a small Compono-owned PRNG (not
`System.Random`), so the byte-for-byte output sequence is something
Compono controls rather than an inherited BCL implementation detail. The
stability guarantee is explicit: the same seed produces the same output
for a given `Compono` package version — cross-version stability across a
`Compono` upgrade is not promised.

`CreateMany<T>(count)` derives each item's independent root seed by
forking the batch's root seed through a stable `"CreateMany"` key, then
by the item's index — so item `i`'s output depends only on the batch root
and `i`, never on `count`: items 0–2 of `CreateMany<T>(3)` and
`CreateMany<T>(10)` (same root seed) are byte-for-byte identical.

## Diagnostics

Diagnostics should track:

- Root request
- Current request path
- Provider decisions
- Selected plan
- Constructor selection
- Scope reuse
- Registration matches
- Seed
- Failure reason
- Suggested remediation

Example:

```text
Unable to compose CreateOrderHandler.

CreateOrderHandler
└── IOrderProcessor processor
    └── OrderValidator validator
        └── IRuleProvider rules

No registration, semantic provider, test-double provider,
built-in provider, or generated plan could satisfy IRuleProvider.

Seed: 8492173
```

Per [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md),
this level of detail is not free to collect and must not cost anything on
the normal successful path: a context-owned, reusable, array-backed trace
buffer records a compact struct (stage, provider, outcome — no strings,
no allocation) per stage/provider attempt, and rewinds on success instead
of retaining anything. Only a failing request materializes its slice of
that buffer into the durable diagnostic above, before the buffer unwinds
further. This is allocation-free on success, not necessarily free —
worth confirming against a benchmark once implemented.

## Package Boundaries

### Compono

Owns:

- Composition context
- Runtime engine
- Requests and results
- Provider contracts
- Scopes
- Profiles
- Registrations
- Deterministic random
- Built-in providers
- Diagnostics
- Generated-plan contracts

### Compono.Generators

Potentially owns:

- Incremental source generator
- Generated plan registration
- Compile-time diagnostics

Whether this ships separately or is bundled as an analyzer dependency of `Compono` remains open.

### Compono.Xunit

Owns:

- xUnit v3 data integration
- Per-row composition contexts
- Inline value precedence
- Parameter attributes
- Seed reporting
- Profile selection

### Compono.NSubstitute

Owns:

- NSubstitute-backed test-double provider
- Interface support
- Optional abstract-class support
- NSubstitute-specific diagnostics

### Compono.Bogus

Owns:

- Bogus-backed semantic providers
- Locale configuration
- Member-name conventions
- Correlated value rules
- Integration with Compono's deterministic seed

## Package Dependency Diagram

```text
                     Compono.Generators
                  (netstandard2.0, IsPackable=false,
                 never independently published — see
                          ADR-0003)
                           |
                           | ProjectReference,
                           | OutputItemType="Analyzer"
                           v
                        Compono
                    (core engine, no
                   dependency on any
                  integration package;
                 packs Compono.Generators'
                 output into its own nupkg
                 under analyzers/dotnet/cs)
                           ^
                           |
        +------------------+------------------+
        |                  |                  |
   Compono.Xunit    Compono.NSubstitute   Compono.Bogus
        |                  |                  |
   xunit.v3           NSubstitute            Bogus
```

- `Compono` depends on nothing else *published* in this diagram — every
  arrow from an integration package points *into* it, never out, per the
  "core package must not know about integrations" rule
  (`design-decisions.md` rule 3). Its build-time-only relationship to
  `Compono.Generators` is a different kind of dependency (analyzer, not a
  normal reference) and doesn't violate that rule — see
  [ADR-0003](adr/0003-generator-package-distribution.md).
- Each integration package depends on `Compono` plus exactly one
  third-party library (`xunit.v3`, `NSubstitute`, or `Bogus`). Integration
  packages don't depend on each other.
- `Compono.Generators` is never published to NuGet on its own — its
  compiled output is packed directly into the `Compono` nupkg, so from a
  *consumer's* point of view it doesn't exist as a separate dependency at
  all ([ADR-0003](adr/0003-generator-package-distribution.md)).

## Open Architectural Decisions

- ~~Runtime reflection policy~~ — default direction resolved by
  [ADR-0001](adr/0001-source-generation-first.md); the exact opt-in
  mechanism for a future compatibility mode is still open.
- ~~Whether generated plans are required for external types~~ — resolved
  by [ADR-0004](adr/0004-composition-plan-discovery-and-dispatch.md):
  external/library types are fully supported via the `PlanCache<T>`
  registry dispatch mechanism, not required to be `partial` or
  Compono-owned.
- ~~Sync versus async provider contracts~~ — resolved by
  [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)
  (carried forward from the now-superseded ADR-0007): synchronous, with
  any future async need getting a distinct opt-in contract.
- ~~Public versus internal visibility of the core engine types~~ —
  resolved by
  [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md):
  `CompositionRequest`, `ICompositionProvider`, `CompositionResult`, and
  `IRandomSource` are `internal` in Milestone 2; only
  `CompositionRequestDescriptor` and `ICompositionContext` are `public`,
  since they're the two types generated code actually crosses the
  assembly boundary to use.
- Public versus internal use of `Type`
- Exact profile model
- ~~Scope lifetime model~~ — resolved for Milestone 2 by
  [ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md)
  (carried forward from the now-superseded ADR-0008): one scope per root
  composition operation, type-keyed sharing only.
- ~~Recursion detection timing~~ — resolved by
  [ADR-0011](adr/0011-composition-scope-shared-values-and-recursion-detection.md):
  checked only immediately before generated-plan dispatch, via a distinct
  active-construction-frame stack, after explicit/shared/registration
  stages have had a chance to terminate the graph.
- ~~Constructor selection rules~~ — resolved by
  [ADR-0002](adr/0002-constructor-selection-algorithm.md).
- ~~Composition path identity for random forking~~ — resolved by
  [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md):
  structured `PathSegment`s (constructor parameter/member name,
  collection index, dictionary key/value role), not a type-only chain.
- ~~Stability guarantees for deterministic output~~ — resolved by
  [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)
  (carried forward from the now-superseded ADR-0009): same seed/same
  output within a `Compono` version, not guaranteed across versions.
- ~~`CreateMany` seed derivation~~ — resolved by
  [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md):
  each item forks from the batch root seed by a stable `"CreateMany"` +
  index key, stable regardless of the requested `count`.
- ~~Collection generation semantics (default size, key uniqueness,
  ordering guarantees)~~ — resolved by
  [ADR-0013](adr/0013-collection-generation-semantics.md).
- ~~Whether source-generation contracts live in `Compono` or
  `Compono.Generators`~~ — resolved by
  [ADR-0003](adr/0003-generator-package-distribution.md).
