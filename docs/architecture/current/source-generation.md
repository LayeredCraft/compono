# Source Generation

Source generation is Compono's preferred construction strategy, resolved
by [ADR-0001](../../adr/0001-source-generation-first.md) — generated
composition plans are the expected execution path, not an optimization
layered on top of a reflection-based default. See
[Design Principles](../design-principles.md) for why this matters
philosophically; this page is *how* it actually works.

## What the generator does

For a constructible type, the generator identifies accessible
constructors, primary constructors, required members, init-only members,
nullability metadata, unsupported types, ambiguous construction paths, and
cyclic compile-time dependencies where detectable — then emits a plan
that selects the constructor, requests each argument, invokes the
constructor directly, assigns required/configured members, and preserves
nullability and member context.

A generated plan looks like this, conceptually:

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
all of that internally (see [The Provider Pipeline](provider-pipeline.md)),
which is what makes incorrect path propagation structurally difficult
rather than merely documented against.

At runtime, the engine executes generated plans, resolves provider-backed
values, manages scopes, manages deterministic random streams, tracks the
composition path, and produces diagnostics — see
[The Provider Pipeline](provider-pipeline.md) and
[Deterministic Seeding](deterministic-seeding.md) for those pieces in
depth, and [Generated Plans and Discovery](generated-plans-and-discovery.md)
for how a plan is actually found and dispatched to.

## Runtime reflection policy

Runtime reflection is intentionally **not** part of the default
architecture, and this remains an open decision — the exact opt-in
mechanism for a future compatibility mode is still undecided. Three
candidate approaches:

**Generated plans required.** Composition fails when no generated plan
exists. Predictable performance, strong trimming/AOT characteristics, a
simple runtime model — but external or dynamically discovered types may
need explicit support, and some test scenarios may be less convenient.

**Automatic reflection fallback.** The runtime reflects when no generated
plan exists. High compatibility and lower migration friction — but a more
complex runtime, weaker AOT guarantees, less predictable performance, and
reflection can hide real source-generation gaps instead of surfacing
them.

**Opt-in compatibility package or mode.** Reflection support is isolated
from the default runtime. Keeps the core architecture clean, allows
compatibility where necessary, and makes performance tradeoffs explicit —
this is the current leading compromise, but it is **not yet an accepted
decision**.

Whichever direction is chosen, reflection must never silently become the
fallback path — an explicit, compiler-visible opt-in (an MSBuild property
or a dedicated compatibility package) is the baseline requirement any of
the three candidates above already satisfies.
