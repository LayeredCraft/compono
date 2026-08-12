# Generated Plans and Discovery

Resolved by [ADR-0004](../../adr/0004-composition-plan-discovery-and-dispatch.md).
This page covers how the generator decides a type needs a plan (see
[Source Generation](source-generation.md) for what a plan itself looks
like), and how `Create<T>()` reaches it without reflection.

## Discovery

Discovery walks `Create<T>()`/`CreateMany<T>()` call sites and their
types' transitive constructor parameters. `[Composable]` is an opt-in
marker for a type with no local call site — applied directly to a type
this compilation owns, or at assembly level
(`[assembly: Composable(typeof(SomeType))]`) for a type in a referenced
assembly that can't be annotated directly. Both forms are equivalent
plan-generation requests, deduplicated alongside call-site discovery.

## Dispatch

"Registering the plan with the runtime" means a generated module
initializer populates a closed-generic static field
(`PlanCache<Customer>.Instance = ...`) that `Create<T>()` reads directly —
**not** a `typeof(T)`-keyed dictionary lookup. This is a zero-overhead
dispatch mechanism: no hashing, no lookup, just a direct field read.
[ADR-0014](../../adr/0014-generator-emitted-collection-plans.md) extends
the same mechanism to the five built-in collection shapes (array,
`List<T>`, `IReadOnlyList<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`)
via a parallel `CollectionPlanCache<T>`.

A generated plan never redispatches into itself directly — each
`context.Resolve<T>(descriptor)` call it makes is a fresh pipeline
evaluation for whatever type that member actually is, not a recursive
call back into the same plan. A genuinely self-referencing type (e.g. a
`Node` with a `Node` property) only becomes a problem if nothing earlier
in the pipeline (an explicit value, a shared value, a registration)
terminates it before generated-plan dispatch is reached a second time for
the same type while the first invocation is still on the stack — see
[The Provider Pipeline](provider-pipeline.md#recursion-detection).

## Open questions

**Cross-assembly plan-cache collision.** `PlanCache<T>` and
`CollectionPlanCache<T>` both register via an unconditional
`Instance = new ...Plan()` in a generated module initializer. If two
different consuming assemblies loaded into the same process both
discover a generated plan for the exact same closed type — most
plausible for `CollectionPlanCache<T>`, since a BCL collection type like
`List<Address>` is exactly the kind of type two independently compiled
assemblies could both legitimately reach if they share a library type —
whichever assembly's module initializer runs last wins silently: module
initializer order across assemblies isn't something either cache
controls or detects. This is a `PlanCache<T>`-level property unchanged
since [ADR-0004](../../adr/0004-composition-plan-discovery-and-dispatch.md),
not a new defect `CollectionPlanCache<T>` introduced — deferred as a
class-of-problem design question (assembly-qualified keys? last-wins-
with-a-diagnostic? something else?) affecting both caches uniformly, not
patched narrowly into just the newer one. Revisit if/when a real
multi-assembly collision is actually hit — no design has been chosen yet.

**`CollectionPlanCache<T>` rooting a collectible `AssemblyLoadContext`.**
For an ordinary composable type (`PlanCache<Customer>`), if `Customer` is
defined in a collectible ALC, the CLR ties the closed generic
instantiation `PlanCache<Customer>` itself to that same collectible
context (a closed generic's home context is the narrowest context
spanned by its generic definition and all of its type arguments), so the
static field disappears when the ALC unloads — no external root survives
it. `CollectionPlanCache<T>` breaks this for a collection whose type
arguments are *entirely* BCL types (`List<int>`,
`Dictionary<System.Guid, string>`): every type composing that closed `T`
lives in the non-collectible default context, so
`CollectionPlanCache<List<int>>`'s instantiation also lives there — but
its generated `[ModuleInitializer]`, running from the collectible
consumer assembly, still stores an instance of a plan class *defined in
that consumer assembly* into it. The default-context static field then
permanently roots the consumer assembly (and its whole ALC) for the
process's lifetime. Any weak-reference indirection able to key off the
consumer assembly/ALC instead of `T` would reintroduce a per-resolve
lookup on every collection, undoing the reason `CollectionPlanCache<T>`
mirrors `PlanCache<T>`'s shape in the first place. Deferred, consistent
with the collision item above: this only manifests for a collectible
`AssemblyLoadContext` unloading a consumer assembly that composes a
BCL-only-typed collection, which neither `docs/mvp.md`'s scope nor
Compono's primary xUnit-test-runner consumer currently exercises. Revisit
alongside the collision item if collectible-ALC hosting becomes an actual
target — no design has been chosen yet.

**`RowInvokerRegistry` broadens this beyond the BCL-only-typed edge
case above.** [ADR-0041](../../adr/0041-aot-safe-row-binding-dispatch.md)'s
`Type`-keyed `Dictionary<Type, ...>` (replacing reflection-based row-
binding dispatch) has no closed-generic-instantiation home-context tie at
all - a plain dictionary entry is an ordinary GC root reachable from a
static field regardless of which ALC its key `Type` or delegate-target
assembly came from. Every registered parameter type roots its generated
dispatch delegate (and the generated assembly defining it), not just the
narrow "every composing type is BCL" case `CollectionPlanCache<T>` above
is scoped to. Same disposition as the collision item and the case above:
deferred, for the same reason - neither `docs/mvp.md`'s scope nor
Compono's primary xUnit/TUnit-test-runner consumers currently exercise
collectible-ALC hosting. Revisit together if collectible-ALC hosting
becomes an actual target; no design has been chosen for any of the three
related items on this page.
