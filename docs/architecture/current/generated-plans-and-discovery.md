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

## Other compile-time-gated generation in this same assembly

`Compono.Generators` also emits two other opt-in outputs from this same
discovery walk, neither described further here (each has its own home):
`Compono.TestDoubles`' generated interface doubles
(`ComponoGeneratedTestDoubles`, [ADR-0043](../../adr/0043-compono-generated-test-doubles-design.md)),
and `Compono.Logging`'s closed `ILogger<T>` activation
(`ComponoGeneratedLogging`, defaulted to `true` by that package's own
props asset — [ADR-0055](../../adr/0055-compono-logging-testing-support-package.md)
Amendment 3, [`docs/packages/compono-logging.md`](../../packages/compono-logging.md)).
Both are narrowly isolated additions to the same walk this page describes
— no second walker, no change to ordinary composition-plan discovery or
dispatch. The two discovery buckets are mutually exclusive for
`Microsoft.Extensions.Logging.ILogger`/`ILogger<T>`: when
`ComponoGeneratedLogging` is enabled, those two types are excluded from
`Compono.TestDoubles`-eligibility entirely, recorded only in the Logging
bucket — [ADR-0055](../../adr/0055-compono-logging-testing-support-package.md)
Amendment 4, added after real dogfooding found both buckets independently
claiming the same closed `ILogger<T>` and emitting two incompatible
`Verify()` extensions for it.

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

**`RowInvokerRegistry` broadens this beyond the BCL-only-typed edge case
above.** [ADR-0041](../../adr/0041-aot-safe-row-binding-dispatch.md)/
[PLAN-0041](../../plans/0041-aot-safe-row-binding-dispatch.md) replaced
`Compono.XunitV3.Binding.RowInvokers`' reflection-based row-binding
dispatch (`MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate`) with a
non-generic, `Type`-keyed `RowInvokerRegistry` in core `Compono`, backed by
a `ConcurrentDictionary<Type, ...>` populated via an atomic `GetOrAdd`
(never a throwing or blind-overwrite registration - required, not just an
implementation detail, per ADR-0041 Amendment 3). `RowInvokerRegistry` has
no closed-generic-instantiation home-context tie at all - a plain
dictionary entry is an ordinary GC root reachable from a static field
regardless of which ALC its key `Type` or delegate-target assembly came
from. Every registered parameter type roots its generated dispatch
delegate (and the generating assembly defining it), not just the narrow
"every composing type is BCL" case `CollectionPlanCache<T>` above is scoped
to. Same disposition as the collision item and the case above: deferred,
for the same reason - neither `docs/mvp.md`'s scope nor Compono's primary
xUnit/TUnit-test-runner consumers currently exercise collectible-ALC
hosting. Revisit together if collectible-ALC hosting becomes an actual
target; no design has been chosen for any of the four related items on
this page.

**`GeneratedTestDoubleRegistry` has the identical shape and consequence as
`RowInvokerRegistry` above.** [ADR-0043](../../adr/0043-compono-generated-test-doubles-design.md)
Amendment 5 Finding M/[PLAN-0043](../../plans/0043-compono-generated-test-doubles.md)
introduced a plain `Type`-keyed dictionary in core `Compono`, populated via
a `[ModuleInitializer]`-registered `RegisterFactory<T>(Func<T> factory)`
per generated double. Like `RowInvokerRegistry`, it has no closed-generic-
instantiation home-context tie at all - a dictionary entry is an ordinary
GC root regardless of which ALC its key `Type` or factory-delegate target
assembly came from, so every registered generated double roots its factory
delegate (and the generating consumer assembly) for the process's
lifetime. Same disposition as the three items above: deferred, for the
same reason - neither `docs/mvp.md`'s scope nor Compono's primary
xUnit/TUnit-test-runner consumers currently exercise collectible-ALC
hosting. Revisit together if collectible-ALC hosting becomes an actual
target; no design has been chosen for any of the four related items on
this page.
