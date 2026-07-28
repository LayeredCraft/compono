# [ADR-0010] Composition Request, Provider Pipeline, Failure Semantics, and Diagnostics Tracing

**Status:** Accepted

**Date:** 2026-07-28

**Decision Makers:** solo (four forks confirmed with user before drafting)

## Context

[ADR-0007](0007-composition-request-and-provider-pipeline.md) settled the
sync-vs-async fork and sketched a `CompositionRequest`/`ICompositionProvider`
shape, but a design review of the Milestone 2 plan surfaced real gaps it
left unresolved:

1. `docs/architecture.md`'s 9-stage precedence list mixes fundamentally
   different mechanisms (explicit values, shared/scoped values, exact
   registrations, profile rules, semantic providers, test-double
   providers, built-in providers, generated plans, diagnostic failure)
   under one "provider pipeline" label. Not all of these are naturally an
   ordered collection of interchangeable `ICompositionProvider`
   implementations.
2. ADR-0007 didn't define when a provider may return `Failure` versus
   `NotHandled` — a provider that merely can't produce *this* particular
   request could accidentally short-circuit the pipeline before a later
   stage (or a generated plan) gets a chance.
3. ADR-0007 said generated code would build a `CompositionRequest`
   directly; a design review flagged that as pushing path-correctness
   onto every generated call site instead of centralizing it.
4. Nothing decided whether `CompositionRequest`, `ICompositionProvider`,
   `CompositionResult` should be `public` (an extension contract) or
   `internal` (M2 has no public configuration surface to plug them into
   yet — that's Milestone 3/5/6).
5. `docs/mvp.md`'s "Structured diagnostics" exit criterion ("provider
   decisions" tracked) implies recording per-provider attempts, which
   risks allocating on every resolution if done naively — in tension with
   the synchronous hot-path performance driver ADR-0007 already
   established.

This ADR supersedes ADR-0007's pipeline/request/failure content. ADR-0007's
sync-vs-async decision is carried forward unchanged (still correct, still
binding) — only the request shape, pipeline model, and failure semantics
are being revised.

## Decision Drivers

- `docs/architecture.md`'s 9-stage precedence is part of the product
  contract — stage *order* isn't up for revision here, only how each
  stage is actually implemented.
- A provider's job should be narrow and honest about what it claims —
  `coding-standards.md`'s "no God classes" and the "exceptions are for
  the unexpected, expected outcomes are return values" error-handling
  principle both push toward providers that report `NotHandled` rather
  than overclaiming ownership of a request they can't fully satisfy.
- Generated code should be as close to "can't get this wrong" as
  possible — ADR-0002/0004/0005's generator conventions already favor
  pushing complexity into the runtime rather than into every generated
  call site.
- Pre-1.0, narrowing visibility later costs nothing; widening
  `internal` → `public` later is non-breaking, but shipping `public` now
  and later needing to redesign it is a breaking change to a real
  compatibility surface (`coding-standards.md`'s access-modifier
  guidance).
- The "normal successful path should remain inexpensive" performance
  driver from ADR-0007 must survive contact with a real diagnostics
  requirement, not just be asserted.

## Considered Options

**Pipeline stage model:**
1. One flat, ordered `List<ICompositionProvider>` — every stage is just a
   provider, including "explicit values" and "generated plans."
2. A hybrid model: some stages are deterministic, context-owned checks
   (single answer, no provider list); other stages hold an ordered
   collection of `ICompositionProvider` that extension packages populate.

**Failure semantics:**
1. Any provider may return `Failure`; it halts the whole pipeline.
2. Any provider may return `Failure`; it halts only its own stage.
3. Ordinary providers in an extensible stage may only return
   `NotHandled`/`Success`; `Failure` is reserved for context-owned
   authoritative stages (exact registration, generated-plan dispatch)
   where invocation itself implies exclusive ownership.

**Generated call-site shape:**
1. Generated code constructs a full `CompositionRequest` (including path)
   per call.
2. Generated code passes a small, compact, compile-time descriptor; the
   context owns path state and expands the descriptor into the full
   internal request.
3. A fluent scoped child-resolver (`context.Member(...)` returning a
   bound sub-context).

**Visibility:**
1. `public` now, to establish the extension contract early.
2. `internal` until a milestone actually gives the type a real consumer.

**Diagnostics tracing:**
1. Always collect a full trace into an allocation, every request.
2. Shallow diagnostics — record only current path/seed/failing stage, no
   per-provider trace.
3. A context-owned, reusable, array-backed trace buffer: providers/stages
   append cheap structs as they're tried; on success the buffer rewinds
   to its checkpoint (no allocation, no durable record kept); on failure
   the active slice is materialized into the durable structured
   diagnostic.

## Decision Outcome

**Pipeline stage model: Option 2, hybrid.** `CompositionContext.Resolve<TValue>`
runs the fixed 9-stage sequence from `docs/architecture.md`, where each
stage is one of two kinds:

| # | Stage | Kind |
|---|---|---|
| 1 | Explicit values | Context-owned deterministic check (no consumer in M2 — no builder/inline-value API exists yet; the slot exists so later milestones don't reorder anything) |
| 2 | Shared or scoped values | Context-owned deterministic check against `CompositionScope` ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)) |
| 3 | Exact registrations | Context-owned deterministic lookup against an internal type-keyed store (M2: internal test seam only, no public builder — [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)) |
| 4 | Profile rules | Ordered `ICompositionProvider` collection — empty in M2 (Milestone 3 populates it) |
| 5 | Semantic value providers | Ordered `ICompositionProvider` collection — empty in M2 (Milestone 6/Bogus populates it) |
| 6 | Test-double providers | Ordered `ICompositionProvider` collection — empty in M2 (Milestone 5/NSubstitute populates it) |
| 7 | Built-in value providers | Ordered `ICompositionProvider` collection — populated internally by `Compono` itself in M2, not user-extensible |
| 8 | Generated composition plans | Context-owned deterministic dispatch via `PlanCache<T>` (ADR-0004) — **not** an `ICompositionProvider`; see below |
| 9 | Diagnostic failure | Context-owned terminal stage — always converts to a thrown `CompositionException` (see Failure semantics) |

Stage *order* is fixed and not configurable — it's the documented product
contract. Provider *order within* an extensible stage (4/5/6/7) is
registration order; nothing in M2 needs a richer ordering rule since only
stage 7 has any providers registered at all, and they're all
Compono-owned. A future milestone populating stage 4/5/6 designs its own
ordering rule when it has a real multi-provider case to justify one — not
speculatively here.

Stage 8 (generated-plan dispatch) is deliberately **not** modeled as an
`ICompositionProvider`: it's a direct `PlanCache<T>.Instance` field read
(ADR-0004's zero-overhead dispatch), gated by the active-construction-frame
recursion check ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)),
not a `TryCompose(request, context)` call through an interface. Modeling
it as a provider would mean either boxing/generic-erasure to fit the
non-generic `ICompositionProvider` shape, or making the interface generic
(defeating the whole point of `PlanCache<T>`'s closed-generic-field
dispatch). Keeping it a distinct context-owned stage preserves ADR-0004's
performance property untouched.

**Failure semantics: Option 3.** Ordinary `ICompositionProvider`
implementations occupying an extensible stage (4/5/6/7) may only return
`NotHandled` or `Success` — the interface itself only exposes those two
outcomes to implementers (see `CompositionResult`'s shape below).
`Failure` is reserved for context-owned authoritative stages: an exact
registration (stage 3) whose factory throws, or generated-plan dispatch
(stage 8) when a plan exists but its `Compose` call fails or a recursion
cycle is detected. The rule in one sentence: **`Failure` means
"authoritative ownership was established, but resolution could not
complete" — never a stronger form of `NotHandled`.** A heuristic-match
provider (a semantic provider that recognizes a member name but not the
member's type, say) that can't produce a value must return `NotHandled`
so later stages still get a chance.

```csharp
internal abstract record CompositionResult
{
    private CompositionResult() { }

    internal sealed record NotHandled : CompositionResult;
    internal sealed record Success(object? Value) : CompositionResult;
}
```

`ICompositionProvider.TryCompose` returns this two-case type — there is no
`Failure` case an ordinary provider can construct. The context-owned
authoritative stages use a separate, internal-only failure signal (not
exposed through `ICompositionProvider`'s return type at all) when they
need to report "claimed, but broken."

**Generated call-site shape: Option 2.** `ICompositionContext.Resolve<TValue>`
takes a small `public` descriptor the generator constructs at compile
time; the context owns all path state and expands the descriptor into the
richer `internal` `CompositionRequest` used by the pipeline:

```csharp
// public — crosses the assembly boundary into generated consumer code
public readonly record struct CompositionRequestDescriptor(
    CompositionRequestKind Kind,   // ConstructorParameter | RequiredMember
    string Name,
    Nullability Nullability);

// public — ICompositionPlan<T> is public, so its Compose parameter must be
public interface ICompositionContext
{
    T Resolve<T>(in CompositionRequestDescriptor descriptor);
}
```

Generated plan code never constructs or touches `CompositionPath` —
it only ever calls `context.Resolve<TValue>(descriptor)` per member,
exactly the shape it already uses today (minus the `Nullability`-only
parameter, replaced by the richer-but-still-compact descriptor). One
`Resolve<T>()` overload handles both constructor parameters and required
members — `CompositionRequestKind` distinguishes them for diagnostic
phrasing (e.g. "constructor parameter 'firstName'" vs. "required member
'Email'") without needing separate methods, since both kinds share
identical runtime resolution behavior.

Internally, `CompositionContext.Resolve<T>` expands the descriptor into
the richer `internal CompositionRequest` — appending a `PathSegment`
derived from the descriptor's `Kind`/`Name` to its own current
`CompositionPath` ([ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)
owns that structure) — before running the 9-stage pipeline. Because
`Resolve<T>` is an ordinary synchronous method that recurses through
nested `Resolve<T>` calls (one per generated plan's constructor
parameters), path push/pop falls directly out of the call stack: push on
entry, pop in a `finally` on exit. No caller — generated code or an
internal provider — ever manipulates the path directly, which is what
makes incorrect path propagation structurally difficult rather than just
documented-against.

Per the user's explicit note: **the eventual `public` provider-facing
request/result types (Milestone 5/6, when third-party providers are first
implemented) are not assumed to be identical to this internal engine
shape.** `CompositionRequestDescriptor` is the only piece that's
necessarily `public` today (it crosses into generated code); the richer
internal `CompositionRequest`/`CompositionResult`/`ICompositionProvider`
stay `internal` per the Visibility decision below, and can be redesigned
before ever being exposed.

**Visibility: Option 2, internal.** `CompositionRequest` (the rich
internal record), `ICompositionProvider`, `CompositionResult`, and
`IRandomSource` ([ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md))
are all `internal` in Milestone 2. `CompositionRequestDescriptor` and
`ICompositionContext` stay `public` — they're the two types that
genuinely cross the assembly boundary into generated consumer code today.
Milestone 3 (configuration/builder), 5 (NSubstitute), and 6 (Bogus)
promote exactly the minimum surface each actually needs when they have a
real consumer, rather than the whole internal engine shape being
grandfathered into the public API by default.

**Diagnostics tracing: Option 3, with the user's correction that this is
allocation-free/near-zero-allocation on success, not zero-cost.** The
context owns one reusable, array-backed trace buffer per composition
operation (never shared across concurrent operations — one per
`CompositionContext` instance, matching its one-root-operation lifetime
per [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)).
Each `Resolve<T>` call records a checkpoint (the buffer's current length)
on entry; as stages/providers are tried, a compact `ProviderAttempt`
struct (stage id, provider id, outcome enum — no strings, no reflection
metadata, no diagnostic-object allocation) is appended. On successful
completion, the buffer rewinds to that call's checkpoint before returning
— a nested call's successful attempts never pollute a later sibling's
trace, and nothing survives past a successful root `Resolve<T>`. On
failure, the active slice (checkpoint to current length) is materialized
into the durable structured diagnostic *before* the buffer rewinds during
stack unwinding — the failing frame captures its trace before any
`finally` further up the stack can reset it.

This needs to be validated against a real benchmark (`Compono.Benchmarks`,
already established for the generator in Milestone 1) once implemented:
if per-attempt struct writes measurably harm the hot path despite being
allocation-free, the fallback is shallow diagnostics by default with full
provider tracing behind an explicit opt-in diagnostic mode — that
decision is deferred to the benchmark result, not assumed to be free
just because it doesn't allocate.

### Positive Consequences

- The pipeline model matches what `docs/architecture.md` actually implies
  (some stages are single deterministic decisions, not extension points)
  instead of forcing every stage into the same provider-list shape.
- `Failure`'s narrowed meaning removes an entire class of "a heuristic
  provider accidentally blocks a working built-in/generated-plan path"
  bugs before any provider code exists to have the bug.
- Path correctness becomes a property of `CompositionContext`'s own
  recursive call structure, not something every generated call site or
  every future provider author has to get right independently.
- `internal`-by-default keeps Milestone 2's review surface and
  compatibility commitment small while the engine shape is still being
  validated against real implementation.
- Diagnostics tracing is designed against a stated cost budget (allocation-
  free on success) instead of being bolted on as "whatever's easiest."

### Negative Consequences

- The context-owned-vs-provider-collection split is more upfront design
  than a flat provider list — accepted, since the flat model was
  identified as actively misleading about what stages 1/2/3/8/9 really
  are.
- Reserving `Failure` for authoritative stages means a genuinely
  ambiguous "I recognize this but it's invalid" case from an ordinary
  provider (say, a future Bogus rule with an internally inconsistent
  configuration) has no way to signal that distinctly from `NotHandled` —
  it either has to be treated as `NotHandled` (falls through, possibly to
  a worse outcome) or as a thrown exception at the provider level. This is
  deferred to whichever milestone (5/6) first hits a real case, not solved
  speculatively here.
- The pooled trace buffer adds real implementation complexity (checkpoint/
  rewind semantics across nested `Resolve<T>` calls) that a naive
  allocate-always trace wouldn't have — accepted as the cost of the stated
  performance goal, with a benchmark gate before committing to it as the
  final shape.

## Pros and Cons of the Options

### Pipeline — flat provider list

- Good, because one interface, one collection, minimal ceremony.
- Bad, because it mischaracterizes deterministic single-answer stages
  (explicit/shared/registration/generated-plan/failure) as if they were
  interchangeable, extensible providers — exactly the gap this design
  review was asked to close.

### Pipeline — hybrid context-owned + provider-collection stages (chosen)

- Good, because it matches what each stage actually *is* — a single
  deterministic check versus a genuine extension point.
- Good, because stage 8's `PlanCache<T>` dispatch keeps ADR-0004's
  zero-overhead field-read property, which a provider-interface shape
  would have compromised.
- Bad, because two different stage "kinds" is more to document and get
  right than one uniform shape.

### Failure — any provider, halts whole pipeline

- Good, because it's the simplest rule.
- Bad, because it's exactly the bug the review flagged: a
  partial/heuristic match can block a working downstream path.

### Failure — any provider, halts own stage only

- Good, because it lets a stage say "nothing else here should try either"
  without blocking unrelated downstream stages.
- Bad, because it's a subtler rule than "authoritative stages only," and
  still lets an ordinary provider incorrectly claim `Failure` for a
  request it doesn't actually own exclusively.

### Failure — reserved for authoritative stages (chosen)

- Good, because it maps `Failure` to an actual invariant ("this stage's
  invocation implies exclusive ownership") rather than a provider's
  subjective judgment call.
- Good, because it can't regress accidentally — the type system only
  gives ordinary providers `NotHandled`/`Success` to return.
- Bad, because a genuinely ambiguous provider-level "invalid" case has no
  clean outlet yet (Negative Consequences, above).

### Generated call site — full request per call

- Good, because it needs the least new context-side API.
- Bad, because it pushes path correctness onto every generated call site
  — the exact risk flagged in the design review.

### Generated call site — compact descriptor, context owns path (chosen)

- Good, because path correctness lives in one place (`CompositionContext`),
  not N generated call sites.
- Good, because the descriptor is small enough to be a
  `readonly record struct` — cheap to construct, no heap allocation.
- Bad, because it requires more context-side plumbing (descriptor→request
  expansion) than the naive option.

### Generated call site — fluent scoped child-resolver

- Good, because push/pop is visually explicit at each call site via a
  scope object.
- Bad, because it implies an `IDisposable` (or equivalent scope-exit
  mechanism) on the hot path, in tension with ADR-0007's synchronous,
  allocation-conscious hot-path goal.

### Visibility — public now

- Good, because integrators eventually implementing `ICompositionProvider`
  don't need a later visibility-widening PR.
- Bad, because it commits a real compatibility surface before Milestone 2
  has validated the shape against actual implementation, and before any
  milestone can actually configure or consume it.

### Visibility — internal until consumed (chosen)

- Good, because `internal` → `public` is non-breaking; the reverse isn't.
- Good, because it keeps Milestone 2's review surface honest about what's
  actually usable today.
- Bad, because Milestone 5/6 will need a deliberate promotion step later
  — accepted as cheap relative to shipping the wrong public shape now.

### Diagnostics — always allocate full trace

- Good, because it's the simplest code, richest diagnostics
  unconditionally.
- Bad, because it allocates on every resolution, contradicting the
  stated "normal successful path should remain inexpensive" goal.

### Diagnostics — shallow (path/seed/failing stage only)

- Good, because it's the cheapest possible approach, no pooled buffer.
- Bad, because it doesn't satisfy `docs/architecture.md`'s "provider
  decisions" diagnostic example, which the Milestone 2 exit criteria
  reference.

### Diagnostics — pooled trace buffer, materialize on failure (chosen)

- Good, because it's allocation-free on the success path while still
  capturing full provider-attempt fidelity on failure.
- Good, because checkpoint/rewind composes naturally with the same
  recursive call-stack structure that already owns path push/pop.
- Bad, because checkpoint/rewind correctness across nested calls is real
  implementation complexity, and needs a benchmark to confirm the
  "allocation-free" claim actually holds in practice.

## Amendment (2026-07-28): pre-implementation refinements

A final review of PLAN-0002 before Phase 0 began surfaced five gaps in
this ADR's Decision Outcome, addressed here before any code was written
against it (no implementation exists yet — this amends the same design
cycle's record rather than reversing a decision already built against).

**1. Root resolution doesn't use `CompositionRequestDescriptor`.**
`CompositionRequestKind` only ever has `ConstructorParameter`/
`RequiredMember` — there is no `Root` case, because the root request
never goes through the public descriptor path at all.
`Composer.Create<T>()` is hand-written runtime code (not generator-emitted)
living in the same assembly as `CompositionContext`, so it calls a
distinct **internal** entry point directly:

```csharp
internal T ResolveRoot<T>()
{
    var request = new CompositionRequest(typeof(T), Nullability.NotNullable, path: CompositionPath.Root(typeof(T)), IsShared: false);
    return (T)RunPipeline(request)!;
}
```

`ResolveRoot<T>()` and the descriptor-expanding `Resolve<T>(in CompositionRequestDescriptor)`
both funnel into the same private `RunPipeline(CompositionRequest)` — the
9-stage sequence, active-construction-frame check, and path push/pop are
identical either way, which is what makes "the root type goes through the
same pipeline as nested types" (PLAN-0002's Execution Flow section)
literally true rather than true "in spirit." `CompositionRequestDescriptor`
stays exactly what it was: the compact, generator-facing shape for member
requests, with no root-shaped case bolted on for a caller that never uses
it.

**2. Stable identity uses ordinal, not name; names are diagnostic-only.**
`CompositionRequestDescriptor` gains an `Ordinal` (shown here as a plain
`readonly struct`, not a `record struct` — see "Amendment 2" below, which
settles that specific point; the shape otherwise unchanged from this
amendment):

```csharp
public readonly struct CompositionRequestDescriptor(
    CompositionRequestKind Kind,
    int Ordinal,
    string Name,
    Nullability Nullability);
```

For `ConstructorParameter`, `Ordinal` is the parameter's position in the
constructor [ADR-0002](0002-constructor-selection-algorithm.md) selected
— stable as long as that constructor's parameter order doesn't change,
which is exactly the kind of source edit expected to perturb derived
values, per [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md)'s
amendment. For `RequiredMember`, `Ordinal` is a generator-assigned index
— see ADR-0012's amendment for the exact rule. `Name` is carried
**only** for diagnostic display (`"constructor parameter 'firstName'"` in
a failure message) and is never an input to path/random-forking identity
— see ADR-0012's amendment for the full hashing consequence.

**3. Diagnostics trace retains the active failing branch, not completed
siblings.** To remove any ambiguity in how "checkpoint on entry, rewind
on success" (this ADR's original Decision Outcome) behaves across nested
calls: because a sibling call's checkpoint-rewind happens **immediately
on that sibling's own success** — before its next sibling is even
attempted — a completed sibling's trace entries are already gone from the
buffer by the time a *later* sibling fails. The materialized diagnostic
therefore naturally contains only the currently-active call chain's
entries: each not-yet-rewound ancestor's own stage/provider attempts,
root to the failure point — i.e., exactly the request edges that were
still "in flight" when the failure occurred, never an already-succeeded
and already-rewound sibling branch. This is a direct consequence of the
existing design, not a new mechanism, but it's now stated as an explicit
property Phase 4's tests must assert (PLAN-0002), not left to be
correctly inferred from "checkpoint/rewind" alone.

**4. Internal test seam for Phase 0's pipeline-order tests.**
`CompositionContext` needs an **internal** (not public) constructor/
factory overload that accepts explicit per-stage provider collections for
stages 4–7, alongside the production factory
`Composer.Create()` uses (which always passes empty collections for
4/5/6 and the real built-in set for 7, until Milestone 3/5/6 exist):

```csharp
internal CompositionContext(
    CompositionSeed seed,
    IReadOnlyList<ICompositionProvider> profileProviders,
    IReadOnlyList<ICompositionProvider> semanticProviders,
    IReadOnlyList<ICompositionProvider> testDoubleProviders,
    IReadOnlyList<ICompositionProvider> builtInProviders);
```

`Compono.Tests` reaches this via `InternalsVisibleTo` (the same mechanism
already established for the registration test seam,
[ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md)),
constructing a `CompositionContext` directly with fake `ICompositionProvider`
test doubles injected into a specific stage to verify ordering/
short-circuit behavior — no public builder, no `Composer.Create(configure
=> ...)`, and nothing in `Composer`'s own public surface changes.

**5. Non-generic collection providers bridge to generic element
resolution via a cached, construction-only delegate — never
`Activator.CreateInstance`/`MethodInfo.Invoke` on the hot path.**
Standard C# has no way to invoke a generic method with a type argument
discovered only as a runtime `Type` (e.g. "this request is for
`List<Address>`, so `TElement = Address`") without *some* reflection —
this is a real language constraint, not a gap in this design. Per the
confirmed decision: collection requests stay in the normal pipeline
(generated code still calls `Resolve<List<Address>>()` like any other
request, so registrations and higher-precedence stages can still override
built-in collection behavior — no collection-expansion loop is ever
emitted into a generated plan). Once the built-in collection provider
(stage 7) claims a supported closed collection shape, it dispatches
through a strongly-typed generic delegate built **once per distinct
closed collection type** via `MethodInfo.MakeGenericMethod` +
`CreateDelegate` (not `Expression.Compile`, to avoid complicating a future
Native AOT/trimming path), cached in a `ConcurrentDictionary<Type, Delegate>`
keyed by the closed collection type — every subsequent resolution of that
same closed type reuses the cached delegate with zero further reflection.
This is a narrow, explicitly documented exception scoped to Compono's own
fixed set of 5 BCL collection shapes — categorically different from the
arbitrary-user-type reflection fallback [ADR-0001](0001-source-generation-first.md)
prohibits, which remains prohibited. If a future strict-AOT requirement
makes even this bounded, cached reflection unacceptable, the fallback is
generator-emitted registration of closed collection bridges discovered in
the transitive closure (not a collection-expansion loop in the containing
plan) — noted here as a validation requirement for Milestone 8's AOT
pass, not solved now.

## Amendment 2 (2026-07-28): descriptor type, delegate signatures, and an explicit AOT position

A final pre-Phase-0 review of PLAN-0002 asked for three more corrections
before implementation begins:

**1. `CompositionRequestDescriptor` is a plain `readonly struct`, not a
`record struct`.** Equality, `Deconstruct`, and `ToString` formatting are
not part of this type's intended contract — it's a compact, write-once,
generator-constructed argument passed `in` to `Resolve<T>`, never
compared, deconstructed, or printed as a record. A `record struct`
compiles member-wise `Equals`/`GetHashCode`/`ToString`/`Deconstruct` the
type doesn't need, for no benefit over a plain struct with the same
fields:

```csharp
public readonly struct CompositionRequestDescriptor
{
    public CompositionRequestKind Kind { get; }
    public int Ordinal { get; }
    public string Name { get; }
    public Nullability Nullability { get; }

    public CompositionRequestDescriptor(CompositionRequestKind kind, int ordinal, string name, Nullability nullability)
    {
        Kind = kind;
        Ordinal = ordinal;
        Name = name;
        Nullability = nullability;
    }
}
```

**2. The collection-dispatch bridge uses strongly-typed cached delegate
signatures, one per collection shape — not `ConcurrentDictionary<Type, Delegate>`.**
The original Decision Outcome's untyped `Delegate` cache would need an
unsafe cast back to a specific delegate shape at every call site.
Instead, one explicitly-typed delegate per collection shape, each with
its own dedicated cache:

```csharp
internal delegate object ListFactory(CompositionContext context, CompositionRequest request);
internal delegate object ArrayFactory(CompositionContext context, CompositionRequest request);
internal delegate object HashSetFactory(CompositionContext context, CompositionRequest request);
internal delegate object DictionaryFactory(CompositionContext context, CompositionRequest request);

internal sealed class CollectionDispatchBridge
{
    private readonly ConcurrentDictionary<Type, ListFactory> _listFactories = new();
    private readonly ConcurrentDictionary<Type, ArrayFactory> _arrayFactories = new();
    private readonly ConcurrentDictionary<Type, HashSetFactory> _hashSetFactories = new();
    private readonly ConcurrentDictionary<Type, DictionaryFactory> _dictionaryFactories = new();
    // GetOrAdd per shape returns the already-correctly-typed delegate — no cast at the call site.
}
```

Since there are only 5 supported shapes (ADR-0013), 4–5 small dedicated
caches is a fixed, bounded amount of code, not an open-ended set — the
type safety this buys (a `ListFactory` can never be accidentally invoked
against a `Dictionary` cache entry) is worth the small amount of
repetition.

**3. Explicit Native AOT/trimming position.** `MakeGenericMethod` +
`CreateDelegate` is **not** trim-safe or Native-AOT-safe in general —
`MakeGenericMethod` over a value-type argument can require runtime code
generation that doesn't exist under Native AOT. This is accepted for the
Milestone 2 MVP because `docs/mvp.md`'s own "MVP Non-goals" list already
states **"Native AOT certification"** is out of scope — this ADR isn't
introducing a new AOT gap, it's using budget the product scope already
allocated. If Compono pursues AOT certification post-MVP, this bridge is
the specific, already-identified place that needs to change — to the
generator-emitted registration fallback this ADR's original Decision
Outcome already named, not a new investigation. Until then, the bridge's
JIT/reflection cost is paid once per distinct closed collection type
(typically a handful of types across a whole test run), not per resolved
value, on runtimes where JIT is available at all.

## Links

- Supersedes [ADR-0007](0007-composition-request-and-provider-pipeline.md)
  (kept as a historical record; its sync-vs-async decision remains
  binding and is carried forward here unchanged).
- `docs/architecture.md` — Resolution Pipeline, Providers, Composition
  Requests sections; updated alongside this ADR.
- [ADR-0004](0004-composition-plan-discovery-and-dispatch.md) —
  `PlanCache<T>` dispatch, whose performance property stage 8 preserves.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  stages 2/3 (scope/registrations) and the active-construction-frame
  recursion check gating stage 8.
- [ADR-0012](0012-composition-path-identity-and-deterministic-random-forking.md) —
  `PathSegment`/`CompositionPath`, which `CompositionRequest` carries.
- [ADR-0013](0013-collection-generation-semantics.md) — built-in
  collection providers occupying stage 7, and how they interact with
  `Failure` semantics and path identity.
