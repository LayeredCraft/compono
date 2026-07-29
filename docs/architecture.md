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
  compile-time-constructible value generated plan code actually passes.
  A plain `readonly struct`, not a `record struct` — equality,
  `Deconstruct`, and record-style formatting aren't part of its contract,
  per [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
  second amendment:
  ```csharp
  public readonly struct CompositionRequestDescriptor
  {
      public CompositionRequestDescriptor(
          CompositionRequestKind kind,   // ConstructorParameter | RequiredMember |
                                          // CollectionElement | DictionaryKey | DictionaryValue
          int ordinal,                    // stable identity - see Deterministic Randomness, below
          string name,                    // diagnostic display only, never identity
          Type declaringType,             // Milestone 3 - see Configuration Rules, below
          Nullability nullability);

      public CompositionRequestKind Kind { get; }
      public int Ordinal { get; }
      public string Name { get; }
      public Type DeclaringType { get; }
      public Nullability Nullability { get; }
  }
  ```
  `DeclaringType` ([ADR-0020](adr/0020-composition-configuration-rules.md),
  Milestone 3, not yet implemented — an additive extension to this
  `Accepted` descriptor shape, not a change to ADR-0010's own text) is the
  type whose constructor/required-member declares this parameter/member —
  generator-emitted alongside `Ordinal`/`Name`, meaningful only for
  `ConstructorParameter`/`RequiredMember` requests. It exists so a
  configuration rule can match by declaring type + member name directly
  off the request, rather than inferring the declaring type from path
  state — see Configuration Rules, below.
- **`CompositionRequest`** (`internal`) — the richer record the context
  expands a descriptor into, by appending a `PathSegment`
  ([ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md))
  derived from the descriptor to its own current path. This is what the
  internal provider pipeline actually operates on; generated code never
  sees or builds one.

Only fields with a real Milestone 2 consumer exist today — `RequestedType`,
`Nullability`, the descriptor's `Kind`/`Name` (folded into the request's
path segment), `Path`, `IsShared`. `DeclaringType` is Milestone 3's first
addition to this set (above). `CustomAttributes`, generic context,
requested lifetime, semantic hints, and "whether a test double is
acceptable" are still deliberately not modeled — they get added once a
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
4. Configuration rules
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
| 3 | Exact registrations | **Hybrid**, per [ADR-0019](adr/0019-registrations-and-service-provider-injection.md) (Milestone 3, not yet implemented): a context-owned deterministic lookup against the exact-registration table, then — only on a miss, if a consumer called `UseServiceProvider(...)` — a fallback `IServiceProvider.GetService(typeof(T))` call. Milestone 2 shipped this stage as internal-only with no public builder; Milestone 3 is the design for its real public shape. |
| 4 | Configuration rules | Ordered `ICompositionProvider` collection — empty until Milestone 3, per [ADR-0020](adr/0020-composition-configuration-rules.md) (not yet implemented). Renamed from "profile rules": populated by type/member value rules compiled from `builder.For<T>()...`, whether reached directly or via a profile's `Configure` — a profile is a reusable application mechanism over this stage, not its owner ([ADR-0018](adr/0018-composition-profiles.md)). Collection-size configuration does **not** populate this stage — see Configuration Rules, below. |
| 5 | Semantic value providers | Ordered `ICompositionProvider` collection — empty until Milestone 6 (Bogus). How an integration package populates this stage with open-ended, pattern-matching logic is deliberately undesigned until Milestone 5 gives it a real consumer — see Open Architectural Decisions, below. |
| 6 | Test-double providers | Ordered `ICompositionProvider` collection — empty until Milestone 5 (NSubstitute), same deferred-extensibility note as stage 5. |
| 7 | Built-in value providers | **Hybrid** (ADR-0014): an ordered `ICompositionProvider` collection (primitive/simple types, enums, nullable value types), populated internally by `Compono` itself, tried first — followed by a context-owned deterministic dispatch through `CollectionPlanCache<T>` for the five built-in collection shapes (array, `List<T>`, `IReadOnlyList<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`), the same closed-generic-field-read mechanism stage 8 uses, since `ICompositionProvider` can't itself construct a generic collection without reflection |
| 8 | Generated composition plans | Context-owned deterministic dispatch via `PlanCache<T>` — **not** an `ICompositionProvider` (see Source-Generated Composition Plans, below) |
| 9 | Diagnostic failure | Context-owned terminal stage |

Only stages 4/6/7 hold an actual ordered collection of providers in
Milestone 2 (and of those, only 7 has anything registered in it — 4/5/6
are wired but empty until their owning milestone). Provider order
*within* an extensible stage is registration order; stage 7 alone already
holds three real providers (`PrimitiveValueProvider`, `EnumValueProvider`,
`NullableValueProvider` — `BuiltInProviders.Default`), so "no stage has
more than one provider" is not actually true today, a stale claim
corrected during PR #13 review. No *richer* ordering rule (priority,
specificity, or similar) exists yet because none has been needed:
`PrimitiveValueProvider`/`EnumValueProvider`/`NullableValueProvider`
claim disjoint type sets, so plain registration order has never had two
providers genuinely compete for the same request — a richer rule becomes
a real question only once two providers could plausibly both claim the
same type differently. Stage 7's `CollectionPlanCache<T>` dispatch is
tried only after
its ordered provider collection has already declined, so a registration,
profile rule, semantic provider, or test-double provider (stages 1–6)
still gets first refusal over a collection request — collections stay
ordinary pipeline requests, per ADR-0013.

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
                0,
                "firstName",
                Nullability.NotNullable));

        var lastName = context.Resolve<string>(
            new CompositionRequestDescriptor(
                CompositionRequestKind.ConstructorParameter,
                1,
                "lastName",
                Nullability.NotNullable));

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

## Immutable Configuration Model

Resolved by [ADR-0017](adr/0017-immutable-composer-configuration-and-builder-model.md).
**The builder/configuration split and `WithSeed` are implemented (Milestone 3
Phase 0, [PLAN-0003](plans/0003-milestone-3-profiles-and-configuration.md)); every
other configuration verb below (`Register`, `AddProfile`, `UseServiceProvider`, the
`.For<T>()` rule DSL, `WithCollectionSize`) is still Milestone 3 scope, not yet
implemented, per that plan's later phases.** `CompositionBuilder` is a mutable
accumulator that exists only for the duration of the `Composer.Create(builder =>
...)` callback; when the callback returns, its accumulated state is validated and
frozen into an internal `CompositionConfiguration` — a `Composer` holds exactly one,
reused across every `Create<T>()`/`CreateMany<T>()` call it ever serves, with no
mutable state on that hot path at all. Every scalar configuration verb — `WithSeed`
(implemented), `WithCollectionSize`'s global default, `UseServiceProvider` (both
still pending) — may be set at most once per configuration, the same fail-fast rule
keyed configuration (registrations, rules) will follow once implemented: a second
call is a build-time conflict, never last-wins, so a scalar's effective value never
depends on `AddProfile` call order.

Two distinct failure moments both surface from `Composer.Create(...)`, but aren't
the same mechanism: a profile cycle ([ADR-0018](adr/0018-composition-profiles.md),
not yet implemented — Milestone 3 Phase 2) is detected **eagerly**, during
`AddProfile` itself, and throws immediately with exactly one error naming the cycle
— configuration stops right there, nothing further is aggregated. Every other
conflict (duplicate registrations, duplicate rules, duplicate scalars — a duplicate
`WithSeed` call is real today; the rest arrive with their own phases) is detected by
`Build()`'s single validation pass, which runs
only after the whole `configure(builder)` callback has already returned
successfully, and aggregates every conflict found across the complete accumulated
state into one `CompositionConfigurationException` — distinct from the per-value
`CompositionException` a running `Create<T>()` call can throw. That exception
carries a structured, inspectable list of errors (kind, affected type/member,
contributing sources) that its rendered message is derived from, not the other way
around — a test (or a consumer) can assert on the structured data directly rather
than parsing message text.

## Profiles

Resolved by [ADR-0018](adr/0018-composition-profiles.md) (Milestone 3, not yet
implemented). A profile is `ICompositionProfile` — one method,
`void Configure(CompositionBuilder builder)` — not an abstract base class: profiles
define behavior, not shared implementation, matching the repo's composition-over-
inheritance preference and AutoFixture's `ICustomization` prior art.

```csharp
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder
            .UseNSubstitute()
            .UseBogus(options => options.Locale = "en_US")
            .Register<IClock>(_ => new FakeClock(...));
    }
}
```

`builder.AddProfile<TProfile>()` (where `TProfile : ICompositionProfile, new()`) or
`builder.AddProfile(ICompositionProfile profile)` runs `Configure` **immediately**,
synchronously, against the same shared `CompositionBuilder` a direct call would use
— a profile is a named, reusable grouping over the builder's ordinary surface, not a
distinct configuration mechanism. Multiple profiles combine purely through call
order (`AddProfile<A>().AddProfile<B>()`); there is no separate merge step. A
profile calling `AddProfile` for another profile already being applied
(`ProfileA → ProfileB → ProfileA`) is a build-time
`CompositionConfigurationException` naming the cycle — detected via a type-keyed
stack pushed around `Configure`, mirroring the shape of the engine's own
active-construction-frame recursion check. Every accumulated registration/rule
entry retains its full source chain (direct, or the nested profile types that
applied it) so a conflict or cycle diagnostic can always name where each entry
actually came from.

## Registrations and Service Injection

Resolved by [ADR-0019](adr/0019-registrations-and-service-provider-injection.md)
(Milestone 3, not yet implemented). `builder.Register<T>(Func<ICompositionContext, T>
factory)` (plus a `Register<T>(Func<T> factory)` convenience overload) populates
pipeline stage 3's exact-registration table. A duplicate registration for the same
type — from any combination of direct calls and profiles — is a build-time
`CompositionConfigurationException` naming every conflicting source; there is no
last-wins override in Milestone 3.

`ICompositionContext` gains a second, descriptor-less `Resolve<T>()` overload
alongside the existing descriptor-based one, for hand-written registration/
configuration-rule factories that have no generated-plan position to describe. Each
factory invocation gets its own manual-resolve invocation frame (pushed before the
factory runs, popped in `finally` after it returns or throws); every descriptor-less
`Resolve<T>()` call made during that invocation shares and advances that frame's
counter (`PathSegment.ManualResolve`, a call-sequence ordinal, never the requested
type), while a nested factory invocation gets its own independent frame — defined in
[ADR-0019](adr/0019-registrations-and-service-provider-injection.md), verified
against [ADR-0012](adr/0012-composition-path-identity-and-deterministic-random-forking.md)'s
existing reproducibility contract without editing that ADR's `Accepted` text.

Service injection — this milestone's headline new capability — is a fallback *inside*
stage 3, not a new pipeline stage or a new public extensibility surface:
`builder.UseServiceProvider(IServiceProvider provider)` stores the BCL's own
`System.IServiceProvider` (no new package dependency for core `Compono`, since it
ships in `System`, not a NuGet package). On a stage-3 registration miss, the
configured provider is consulted before falling through to stage 4; `null` means
unresolved, an exception the provider throws is authoritative (surfaced as
`CompositionException` with the original preserved as `InnerException`, never
swallowed), and a non-null result is checked assignable to the requested type before
use. `Compono` never creates or disposes a scope — the caller owns the provider and
its lifetime entirely. A richer `Microsoft.Extensions.DependencyInjection`-specific
integration (`IServiceCollection` auto-registration, scoping, keyed services) is
explicitly out of scope for core — a future optional package, not designed yet,
would build on `UseServiceProvider` the same way `Compono.NSubstitute`/
`Compono.Bogus` build on core's other extension points.

## Configuration Rules

Resolved by [ADR-0020](adr/0020-composition-configuration-rules.md) (Milestone 3,
not yet implemented). Two structurally different mechanisms share one public
`builder.For<T>()` DSL:

- **Type and member value rules** (`.For<T>().Use(...)`,
  `.For<T>().Member(x => x.Y).Use(...)`) compile into small, internal,
  Compono-authored `ICompositionProvider` implementations registered into stage 4 —
  a user never implements `ICompositionProvider` directly. A member rule's matching
  identity is `(declaring type, member name)`, matched directly against the
  incoming request's `DeclaringType`/`Name` (Composition Requests, above, and
  [ADR-0020](adr/0020-composition-configuration-rules.md)) — never inferred from
  path state — with the rule's own key
  captured from the member-access expression at the point `.Member(...)` is called;
  a type rule matches any request for exactly that type (no assignability matching
  in Milestone 3). Member rules take precedence over type rules for the same
  effective request — specificity-based, not call-order-based. Two rules claiming
  the identical key is a build-time `CompositionConfigurationException`, the same as
  a duplicate registration.
- **Collection-size configuration** (`builder.WithCollectionSize(n)`,
  `.For<T>().Member(x => x.Y).WithCollectionSize(n)`) is **not** a stage-4 rule —
  it's immutable policy on `CompositionConfiguration`, queried directly by stage 7's
  collection dispatch. A single new `ICompositionContext.ResolveCollectionSize(in
  CompositionRequestDescriptor)` method (not a root/member pair — a root-level
  collection plan reads the configuration's global default directly from internal
  runtime code, with no public API call needed, since it never crosses the
  generated-code boundary) replaces
  [ADR-0013](adr/0013-collection-generation-semantics.md)'s previously-hardcoded
  default of `3`. This parameterizes ADR-0013's constant without reopening its
  retry/uniqueness/ordering semantics.

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
segment data directly (a per-kind tag plus its `Ordinal`/index — a
constructor parameter's position in the selected constructor, or a
required member's generator-assigned declaration-order index; never
`Name`, which exists on the segment for diagnostic display only) via
FNV-1a, never a formatted display string, which is what makes the fork
key collision-free by construction rather than by careful
string-escaping. This is a reproducibility *contract*, not an
implementation detail: renaming a constructor parameter or required
member (with no reordering) never changes its derived value — only
reordering does.
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
this level of detail is designed to cost as little as possible on the
normal successful path — "near-zero-allocation on success, not
zero-cost," in the ADR's own words: a context-owned, reusable,
array-backed trace buffer (`CompositionTraceBuffer`) records a compact
struct (`ProviderAttempt`: stage, provider type, outcome — no strings, no
per-append allocation) per stage attempt, and rewinds on success instead
of retaining anything. Only a failing request materializes its slice of
that buffer into the durable `CompositionDiagnostic` above
(`exception.Diagnostic`, `docs/public-api.md`'s Diagnostics API), before
the buffer unwinds further. `ProviderAttempt.Provider` is the concrete
`ICompositionProvider` type that made the attempt (`null` for a
context-owned stage, which isn't a provider instance at all) —
[ADR-0016](adr/0016-provider-identity-restored-in-provider-attempt.md)
restores this identity field after
[ADR-0015](adr/0015-provider-identity-deferred-in-provider-attempt.md)
deferred it on a premise (no stage has more than one provider) that was
already false for stage 7's three built-in providers.

`CompositionTraceBuffer` itself is not literally zero-allocation, though:
its backing `ProviderAttempt[32]` array is allocated once per root
`CompositionContext`, measured directly at **~536 B per instance** (32
entries; capacity bumped from an original 16 after a second PR #13
review round, and each entry's own size roughly doubled after a fourth
round restored `ProviderAttempt.Provider` —
[ADR-0016](adr/0016-provider-identity-restored-in-provider-attempt.md))
— see this page's Open Architectural Decisions entry below for the
precise breakdown and why pooling it entirely is deferred rather than
fixed as a same-PR change.

Confirmed via `Compono.Benchmarks`' `ResolutionBenchmarks` (Milestone 2
Phase 4, full `DefaultJob` numbers in [`docs/performance.md`](performance.md)):
composing the `Customer`/`Address` representative graph allocates ~2.71 KB
total (of which the trace buffer's ~536 B is ~20%) regardless of the
trace buffer's presence, and `CreateMany<T>(count)` scales linearly with
`count` (10.18× allocation at `count=10`, 101.48× at `count=100`, against
a `count=1` baseline) — no super-linear growth from checkpoint/rewind
bookkeeping. That graph is only 2 levels deep, though — never deep enough
to trigger `CompositionTraceBuffer`'s own growth path (each active
ancestor frame retains ~6 entries until its own child returns, so a
32-entry buffer holds ~5 levels before resizing); `docs/performance.md`'s
"Deep graph result" measures an 8-level-deep graph that does trigger a
real `Array.Resize`, rather than only benchmarking the shallow case. No
fallback to shallow diagnostics was needed.

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
  `IRandomSource` are `internal` in Milestone 2; `CompositionRequestDescriptor`,
  `CompositionRequestKind`, and `ICompositionContext` are `public`, since
  they're the generated-code call surface every plan crosses the assembly
  boundary to use. [ADR-0014](adr/0014-generator-emitted-collection-plans.md)
  extends that same surface for generated collection plans specifically:
  `CollectionPlanCache<T>` and `UniqueValueResolver` are also `public` for
  the identical reason, not a discretionary API design choice.
- Public versus internal use of `Type`
- ~~Exact profile model~~ — resolved by
  [ADR-0018](adr/0018-composition-profiles.md): `ICompositionProfile` interface
  (not an abstract base class), eager in-order application, type-keyed cycle
  detection.
- **Public provider extensibility (how integration packages contribute open-ended
  pattern-matching logic to stages 5/6)** — evaluated and explicitly deferred to
  Milestone 5 during the Milestone 3 design review: Milestone 3's own scope
  (registrations, profiles, type/member rules, `IServiceProvider` fallback) needs no
  such interface — type/member value rules compile into internal,
  Compono-authored providers ([ADR-0020](adr/0020-composition-configuration-rules.md)),
  and service injection folds into stage 3 as a context-owned fallback
  ([ADR-0019](adr/0019-registrations-and-service-provider-injection.md)) — neither
  needs a public `ICompositionProvider`-shaped contract. NSubstitute (Milestone 5)
  is expected to be the first real consumer that needs one (matching "any interface
  type," a pattern no closed-set rule can express), so the interface gets designed
  against that concrete need rather than speculatively now. No design has been
  chosen yet.
- **Richer `Microsoft.Extensions.DependencyInjection` integration** (`IServiceCollection`
  auto-registration, per-composition scoping, keyed services) — explicitly out of
  scope for `Compono` core per
  [ADR-0019](adr/0019-registrations-and-service-provider-injection.md); core only
  supports the BCL's own `System.IServiceProvider` via `UseServiceProvider(...)`. A
  future optional package could build MEDI-specific ergonomics on top of that
  extension point, but isn't designed here — no concrete requirement has motivated
  one yet.
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
- **Cross-assembly plan-cache collision** — `PlanCache<T>` (ADR-0004) and
  `CollectionPlanCache<T>` (ADR-0014) both register via
  an unconditional `Instance = new ...Plan()` in a generated module
  initializer; if two different consuming assemblies loaded into the same
  process both discover a generated plan for the exact same closed type
  (most plausible for `CollectionPlanCache<T>`, since a BCL collection
  type like `List<Address>` is exactly the kind of type two independently
  compiled assemblies could both legitimately reach if they share a
  library type), whichever assembly's module initializer runs last wins
  silently — module initializer order across assemblies isn't something
  either `PlanCache<T>` or `CollectionPlanCache<T>` controls or detects.
  Flagged during PR #11 review as a `CollectionPlanCache<T>` concern, but
  it's actually a `PlanCache<T>`-level property unchanged since Milestone
  1 (ADR-0004) that `CollectionPlanCache<T>` deliberately mirrors, not a
  new defect Milestone 2 introduced — deferred as a class-of-problem
  design question (assembly-qualified keys? last-wins-with-a-diagnostic?
  something else?) affecting both caches uniformly, not patched narrowly
  into just the newer one. Revisit if/when a real multi-assembly
  collision is actually hit — no design has been chosen yet.
- **`CollectionPlanCache<T>` rooting a collectible `AssemblyLoadContext`**
  — flagged during PR #11 review. For an ordinary composable type
  (`PlanCache<Customer>`), if `Customer` is defined in a collectible ALC,
  the CLR ties the closed generic instantiation `PlanCache<Customer>`
  itself to that same collectible context (a closed generic's home
  context is the narrowest context spanned by its generic definition and
  all of its type arguments), so the static field disappears when the ALC
  unloads — no external root survives it. `CollectionPlanCache<T>` breaks
  this for a collection whose type arguments are *entirely* BCL types
  (`List<int>`, `Dictionary<System.Guid, string>`): every type composing
  that closed `T` lives in the non-collectible default context, so
  `CollectionPlanCache<List<int>>`'s instantiation also lives there — but
  its generated `[ModuleInitializer]`, running from the collectible
  consumer assembly, still stores an instance of a plan class *defined in
  that consumer assembly* into it. The default-context static field then
  permanently roots the consumer assembly (and its whole ALC), the same
  leak class the `EnumValueProvider` cache fix (`ConditionalWeakTable<Type,
  Array>` in place of `ConcurrentDictionary<Type, Array>`) closed
  elsewhere. That fix doesn't transfer here: `CollectionPlanCache<T>.Instance`
  is a plain closed-generic static field precisely so stage 7 dispatch is
  one direct field read (ADR-0004's zero-overhead dispatch), not a
  `Type`-keyed lookup; any weak-reference indirection able to key off the
  consumer assembly/ALC instead of `T` would reintroduce a per-resolve
  lookup on every collection, undoing the reason `CollectionPlanCache<T>`
  mirrors `PlanCache<T>`'s shape in the first place. Deferred, consistent
  with the cross-assembly-collision item above: this only manifests for a
  collectible `AssemblyLoadContext` unloading a consumer assembly that
  composes a BCL-only-typed collection, which neither `docs/mvp.md`'s
  scope nor Compono's primary xUnit-test-runner consumer currently
  exercises. Revisit alongside the collision item if collectible-ALC
  hosting becomes an actual target — no design has been chosen yet.
- **`CompositionTraceBuffer`'s own array allocation, per root operation**
  — flagged during PR #13 review: `CompositionTraceBuffer`'s backing
  `ProviderAttempt[]` array (allocated eagerly in its constructor, one
  instance per `CompositionContext`) is a genuine, unconditional
  allocation on every `Create<T>()`/`CreateMany<T>()` item, not literally
  zero — measured directly (isolated from the rest of a real composition)
  at ~536 B per `CompositionTraceBuffer` instance (32-entry initial
  capacity), ~20% of a real `Customer`/`Address` representative
  composition's ~2.71 KB total (`ResolutionBenchmarks`, `docs/performance.md`)
  — consistent with [ADR-0010](adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)'s
  explicit "near-zero-allocation on success, not zero-cost" framing, not
  a violation of it, but real enough that this page and `docs/performance.md`
  now state the precise figure instead of an unqualified "allocation-free"
  claim. (The jump from an earlier-measured ~280 B is itself real, not a
  re-measurement artifact — a fourth PR #13 review round restored
  `ProviderAttempt.Provider`, per
  [ADR-0016](adr/0016-provider-identity-restored-in-provider-attempt.md),
  roughly doubling each trace entry's size; `docs/performance.md` records
  that as an accepted tradeoff, not a regression.)

  A second PR #13 review round found the *growth* path is real too, not
  just the fixed initial allocation: each active ancestor frame dispatching
  through stage 8 or a collection plan retains ~6 trace entries (5 declined
  stages plus a `CompositionAttemptOutcome.Pending` marker) until its own
  child returns, so a composable-type chain more than ~5 levels deep
  exceeds a 32-entry buffer and triggers a real `Array.Resize` — the
  shallow, 2-level-deep `Customer` graph the original benchmark used never
  exercised this at all. Fixed in two parts: the initial capacity was
  bumped from 16 to 32 (covering `docs/architecture.md`'s own 4-level
  Diagnostics example without resizing), and `docs/performance.md`'s
  `DeepGraphBenchmarks` now measures an 8-level-deep chain that does
  trigger a resize, so the growth cost is a real recorded number, not an
  assumed-away one.

  A true zero-allocation design would still need the buffer pooled/reused
  across root operations (`CompositionContext` isn't currently pooled or
  reset-and-reused at all) — a real architecture change, not a
  same-PR-sized fix. Deferred: revisit if a future benchmark shows this
  mattering at a scale `docs/mvp.md`'s scope actually exercises — no
  design has been chosen yet.
