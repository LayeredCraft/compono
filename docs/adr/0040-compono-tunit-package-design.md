# [ADR-0040] Compono.TUnit Package Design

**Status:** Accepted

**Date:** 2026-08-11

**Decision Makers:** Nick Cipollina, Claude (design deep dive)

## Context

[ADR-0039](0039-future-extension-package-admission-gate-and-release-sequence.md)
recorded `Compono.TUnit` as an **admitted candidate** — it clears Gate A
(TUnit's data-source extension surface gives it a real, non-wrapper
integration shape) but had no Gate B evidence
([ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md))
until now: the product owner explicitly requested this package be built,
which this ADR records as the real-demand trigger — a legitimate one, not
weaker than dogfooding-sourced evidence, per `future-packages.md`'s own
"real demand and a concrete design" bar.

This design dive investigated two questions before drafting anything:

1. **Does `Compono.TUnit` need a new core-extension ADR** (the ADR-0021/
   ADR-0022 split `Compono.XunitV3` needed), or can it build entirely on
   existing public surface? [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
   `Composer.CreateRow(Type declaringType)`/`CompositionRow` was
   deliberately designed framework-agnostic ("a hypothetical future
   non-xUnit test-framework integration reuses the identical mechanism"),
   and this dive confirms that holds for **runtime public API**: no new
   public `Compono` type or method is needed. **Correction**: this ADR's
   first draft over-stated that as "no core change is needed" at all —
   `Compono.Generators`' discovery component (`ComposeMethodDiscovery`,
   embedded in `Compono.nupkg` per [ADR-0003](0003-generator-package-distribution.md))
   hardcodes `Compono.XunitV3`'s three attribute metadata names today, and
   a type reached only through a `Compono.TUnit`-attributed method's
   parameter has no textual `Resolve<T>()` call site for the generator's
   ordinary discovery to find (the identical gap
   [ADR-0022](0022-compono-xunit-package-design.md)'s own Amendment fixed
   for `Compono.XunitV3`). Extending that discovery component for a second
   attribute family is real `Compono.Generators` work — see "Generator
   discovery" below. This is not a new core-extension ADR, though: ADR-0022
   made the identical extension as part of `Compono.XunitV3`'s own
   package-design ADR, not a separate ADR-0021-style core-extension one,
   because it's discovery-time/compile-time-only and adds no new *public
   runtime* surface — this ADR follows that same precedent, package-design-
   only in the ADR-0025/ADR-0027 sense the sentence below still means.
2. **What is TUnit's actual, current (verified against a real clone of
   `thomhurst/TUnit` at commit `c1830bf`, plus an empirical probe project
   against the published `TUnit.Core` 1.64.13) extension surface, and does
   it support composing the test class's own constructor dependencies in
   addition to method parameters** — a capability `Compono.XunitV3`
   structurally cannot offer, since xUnit v3 has no class-construction
   extension point. Verified findings, not assumed ones:
   - `IDataSourceAttribute.GetDataRowsAsync(DataGeneratorMetadata) →
     IAsyncEnumerable<Func<Task<object?[]?>>>` is the real root interface;
     `UntypedDataSourceGeneratorAttribute` (`protected abstract
     IEnumerable<Func<object?[]?>> GenerateDataSources(DataGeneratorMetadata)`)
     is the supported sync convenience layer over it, and is the same base
     TUnit's own `DependencyInjectionDataSourceAttribute<TScope>` derives
     from. Evaluated at test-build/discovery time
     (`TestBuilder.BuildTestsFromMetadataAsync`), not at execution time; a
     method-parameter factory `Func` is invoked exactly once.
   - `IClassConstructor.Create(Type, ClassConstructorMetadata)` is a real,
     documented extension point for composing a test class's constructor
     dependencies — but three findings make it a poor fit for a first
     release: (a) the `IClassConstructor` **instance** is created once per
     test-*method* and reused across every data row and repeat — TUnit's
     own docs claim otherwise ("each test gets its own attribute
     instance"), confirmed factually wrong by direct probe; (b) sharing a
     `CompositionRow` between `Create` and the method-parameter data
     source is technically possible (both observably read/write the same
     `TestBuilderContext.StateBag` for one test invocation) but that
     identity is an emergent property of `TestBuilder`'s internal capture
     order, not a documented guarantee; (c) TUnit's disposal graph
     (`ObjectGraphDiscoverer.CollectRootObjects`) roots test-class/method
     *arguments* but not anything constructed *inside* `Create` and merely
     passed to the constructor — composing through `IClassConstructor`
     silently takes on a cleanup responsibility method-parameter
     composition never has. `TestBuilderContext.Events.OnDispose` was
     directly reproduced as non-firing in 8/8 probe runs; `ITestEndEventReceiver`
     is the event mechanism that actually fires for both a data-source
     attribute and an `IClassConstructor`, and is TUnit's documented route
     for this.
   - No existing package builds constructor composition on
     `IClassConstructor`. The one precedent found,
     `AutoFixture/AutoFixture.TUnit` (unpublished to nuget.org, last
     commit 2026-01-28), composes constructor dependencies through a
     **class-level** `UntypedDataSourceGeneratorAttribute` branching on
     `DataGeneratorType.ClassParameters` instead — the same fully-supported
     mechanism TUnit's own `DependencyInjectionDataSourceAttribute` uses,
     with values tracked and disposed by TUnit like any other test
     argument.

## Decision Drivers

- `design-decisions.md` rule 3 — core `Compono` must never know
  `Compono.TUnit` exists; every mechanism this ADR uses is already public
  (`Composer.CreateRow`, `CompositionRow.Resolve`/`ResolveShared`/
  `ShareExplicit`).
- This repo's explicit-over-implicit bias
  (`docs/architecture/design-principles.md`) — a design that depends on
  undocumented internal capture order to correctly correlate two different
  TUnit extension-point invocations is exactly the kind of hidden-coupling
  risk that bias exists to reject, even when it's been empirically
  verified to currently work.
- Compono does not currently own or want a disposal-tracking
  responsibility for composed values (no such mechanism exists anywhere
  else in this codebase) — a design that silently creates one is a real
  architectural cost, not a minor implementation detail.
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
  `CompositionRow.Seed` (`int`, non-negative-when-unseeded) and
  `docs/mvp.md`'s reproducibility goal — any row this package creates must
  keep the same "seed printed identically, pasteable back" guarantee
  `Compono.XunitV3` already established.
- Minimal package surface/dependency footprint, matching
  `Compono.XunitV3`'s own `xunit.v3.extensibility.core`-only reference —
  `TUnit.Core` (not the full `TUnit`/`TUnit.Engine` meta-packages) is
  where every type this design needs actually lives.

## Considered Options

1. **Method-parameter composition only**, via a `Compono.TUnit`-owned
   `UntypedDataSourceGeneratorAttribute` subclass — direct parity with
   `Compono.XunitV3`'s existing scope (`[Compose]`/`[Compose<TProfile>]`/
   `[Compose<TProfile, TConfig>]`/`[Shared]`), reusing `CompositionRow`
   unmodified.
2. **Also compose the test class's own constructor dependencies**, via
   `IClassConstructor`/`[ClassConstructor<T>]`, sharing one
   `CompositionRow` across both extension points through
   `TestBuilderContext.StateBag`.
3. **Compose constructor dependencies through a class-level data-source
   attribute instead** (`DataGeneratorType.ClassParameters`, the same
   mechanism TUnit's own `DependencyInjectionDataSourceAttribute` and
   `AutoFixture.TUnit` use) rather than `IClassConstructor` — considered
   as the lower-risk alternative to Option 2, not built now.

## Decision Outcome

**Chosen: Option 1 — method-parameter composition only, full parity with
`Compono.XunitV3`'s existing scope.**

Option 2 (`IClassConstructor`) is rejected for this release on the
evidence in Context above: it rests on TUnit-internal capture-order
behavior no doc or test commits to, TUnit's own documentation about its
instance lifetime is directly contradicted by observed behavior, and it
moves composed objects outside TUnit's disposal graph, creating a cleanup
obligation Compono doesn't currently have anywhere else. None of that is
hypothetical caution — every claim was verified against real TUnit source
and an empirical probe, not inferred from documentation alone.

Option 3 is **not built now, but recorded as the correct future path** if
constructor composition is ever wanted: unlike Option 2, it is fully
supported API (the same `UntypedDataSourceGeneratorAttribute` base this
ADR already builds on, just applied at class level and branched on
`DataGeneratorMetadata.Type == DataGeneratorType.ClassParameters`), TUnit
tracks and disposes the produced values exactly like any other test
argument, and the class-level and method-level data-source factories
already observably share one `TestBuilderContext`/`StateBag` for a given
test case — so a single `CompositionRow` genuinely can span both without
resorting to `IClassConstructor` at all. This is deferred, not designed,
per the same restraint [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)
holds every other undesigned capability to — it doesn't get its own API
sketch here, only a named, lower-risk landing spot for a later design pass
if real use ever asks for it.

### Package shape

`Compono.TUnit` depends on `Compono` and `TUnit.Core` only, matching the
existing package-dependency diagram exactly. Public surface, mirroring
`Compono.XunitV3`'s own (`ComposeAttribute`/`ComposeAttribute<TProfile>`/
`ComposeAttribute<TProfile, TConfig>`/`SharedAttribute`, all in the
`Compono.TUnit` namespace — no collision with `Compono.XunitV3`'s
identically-named types, since a project referencing both would need to
`using` whichever one it's actually testing against, the same way any two
same-named types in different namespaces already coexist):

```csharp
namespace Compono.TUnit;

public class ComposeAttribute : UntypedDataSourceGeneratorAttribute, ITestDiscoveryEventReceiver
{
    // Non-negative only, matching Compono.XunitV3's own ComposeAttribute.Seed exactly - the
    // same "a reported seed is always pasteable back into this property unchanged" contract.
    public int Seed { get; set; }

    protected override IEnumerable<Func<object?[]?>> GenerateDataSources(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        // Lazily composes on each factory invocation - never before TUnit
        // actually calls the returned Func, matching TUnit's own
        // "defer real work into the Func" convention
        // (DependencyInjectionDataSourceAttribute does the same).
        yield return () =>
        {
            var declaringType = dataGeneratorMetadata.TestInformation!.Class.Type;
            var row = _composer.CreateRow(declaringType);

            // Rejected here, not left to the pipeline - matching Compono.XunitV3's own
            // row.Seed < 0 pre-composition check, so a negative *effective* seed (this
            // attribute's own Seed property, or a profile that itself calls
            // CompositionBuilder.WithSeed with a negative value) is caught before any
            // parameter composes, not partway through.
            if (row.Seed < 0)
                throw new CompositionException($"Compono.TUnit requires a non-negative seed, but the configured seed was {row.Seed}.

Seed: {row.Seed}");

            // ...bind each of dataGeneratorMetadata.TestInformation's method parameters
            // through row.Resolve<T>(descriptor)/row.ResolveShared<T>(descriptor),
            // exactly like Compono.XunitV3's BindingPlan already does for
            // MethodInfo/ParameterInfo - TUnit's ParameterMetadata[] exposes the
            // equivalent per-parameter metadata this needs.
            return boundArguments;
        };
    }

    public ValueTask OnTestDiscovered(DiscoveredTestContext context)
    {
        // Reads the seed this attribute instance's own GenerateDataSources stored into the
        // same row's TestBuilderContext.StateBag - see "Seed observability" below.
        return default;
    }
}

public sealed class SharedAttribute : Attribute;
```

Illustrative, not committed — the exact binding-plan mechanics
(delegate caching, inline-value precedence, profile resolution) are
implementation detail for `implement.md`'s own pass, following
`Compono.XunitV3`'s proven shape as the template, not reinvented from
scratch. **The `Seed` property and its non-negative validation, however,
are a committed requirement, not illustrative detail** — see "Seed input
and replay" immediately below; a first implementation that reports a seed
but has no way to feed it back in would violate this ADR's own
reproducibility driver.

### Seed input and replay

This ADR's own Decision Drivers promise the same "seed printed
identically, pasteable back" guarantee `Compono.XunitV3` already
established — but an earlier draft of this ADR never actually specified
the public input half of that promise: `Compono.XunitV3.ComposeAttribute`
has a public `Seed` property (non-negative only) that routes into
`BuildComposer` (via `CompositionBuilder.WithSeed`) and is checked against
`row.Seed < 0` before any parameter composes; nothing in this ADR's first
draft added the equivalent for `Compono.TUnit`, so an implementation
following it exactly could report a seed via `AddProperty("Compono.Seed",
...)` with no way for a consumer to actually paste it back as
`[Compose(Seed = ...)]`. **Corrected: `ComposeAttribute.Seed` (`int`,
required member of the public surface, not optional implementation
detail) is part of this ADR's Decision Outcome**, mirroring
`Compono.XunitV3.ComposeAttribute.Seed` exactly — same non-negative
constraint, same routing into the row's composer, same pre-composition
rejection for a negative *effective* seed (whether set directly on this
property or via a profile's own `CompositionBuilder.WithSeed` call).
`ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>`
(Package shape above) inherit it unchanged, exactly like
`Compono.XunitV3`'s own generic attributes do.

### Row-binding logic: duplicated, not extracted, for this release

`Compono.XunitV3`'s `Binding/` subfolder (delegate-caching over
`MethodInfo`/`ParameterInfo`) is the obvious candidate for extraction into
a shared location now that a second test-framework package exists — this
is exactly the "rule of three... well, rule of two now" moment
`docs/roadmap/future-packages.md`'s implementation-readiness note flagged.
This ADR deliberately **does not** extract it: TUnit hands a data source
`DataGeneratorMetadata` (its own parameter-metadata shape), not a
`MethodInfo`/`ParameterInfo` pair — the two packages' actual binding
inputs differ enough that the correct shared abstraction boundary isn't
obvious yet from one example on each side. Duplicating the well-understood,
self-contained delegate-caching pattern (~150 LOC in `Compono.XunitV3`)
is the lower-risk choice for a first `Compono.TUnit` release; revisit
extraction only if a **third** test-framework package (`Compono.NUnit`,
per ADR-0039's other admitted candidate) shows the same pattern a third
time with enough shape in common to generalize safely.

### Generator discovery

`Compono.Generators`' `ComposeMethodDiscovery` (`src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`)
is the component that closed this exact gap for `Compono.XunitV3`
(ADR-0022's Amendment, 2026-07-30, fix #2): a type reached only as a
`[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` method's
own parameter has no textual `Resolve<T>(...)` call site anywhere in the
consumer's source — `Compono.XunitV3`'s binding is entirely runtime
reflection (`MethodInfo.MakeGenericMethod`-based invoker caching), so
`Compono.Generators`' ordinary `CreateInvocationDiscovery` path (which
matches literal `Resolve<T>()` call-site expressions) never sees it.
`ComposeMethodDiscovery` exists specifically to generate a plan for every
eligible parameter type on a `[Compose]`-family-attributed method instead,
independent of whether that call site exists in source.

**`Compono.TUnit` hits the identical gap** — its own binding is likewise
entirely runtime reflection over `DataGeneratorMetadata`'s parameter
metadata, no textual `Resolve<T>()` call site. `ComposeMethodDiscovery`
today hardcodes exactly three metadata names, all `Compono.XunitV3`'s own
(`Compono.XunitV3.ComposeAttribute`, `` `1``, `` `2``), registered via three
separate `SyntaxValueProvider.ForAttributeWithMetadataName` calls in
`ComponoIncrementalGenerator.cs` (`Microsoft.CodeAnalysis.SyntaxValueProvider
.ForAttributeWithMetadataName` matches only an attribute's own exact
metadata name, not a base type's — the same reason `Compono.XunitV3`'s two
generic forms each need their own registration, arity-suffixed metadata
names being distinct per CLR arity).

**Required design**: three more constants and three more
`ForAttributeWithMetadataName` registrations, for `Compono.TUnit
.ComposeAttribute`/`` `1``/`` `2``, feeding the *same*
`ComposeMethodDiscovery.TransformMethod` — that method's own logic
(eligible-parameter filtering, `ref`/`out`/`in`/`params` exclusion,
generic-method exclusion) is already attribute-family-agnostic, operating
on `IMethodSymbol`/`IParameterSymbol` alone, not on anything
`Compono.XunitV3`-specific. This is real work inside `Compono.Generators`
(embedded in `Compono.nupkg` per [ADR-0003](0003-generator-package-distribution.md)) —
see "Correction" in Context above for why this doesn't need its own
core-extension ADR the way `CompositionRow` (ADR-0021) did: it's
discovery-time-only, adds no new public runtime type, and ADR-0022 already
established the precedent of making this exact kind of extension inside a
package's own design ADR. `Compono.Generators.Tests` needs a snapshot test
proving a concrete parameter type reachable *only* through a
`Compono.TUnit`-attributed method (no other discovery path in the same
compilation) actually gets a generated plan — mirroring whatever
regression test closed the equivalent gap for `Compono.XunitV3`. Without
this, `row.Resolve<T>()` fails at runtime with "no plan found" for any
type not otherwise discoverable, silently breaking the entire package for
exactly the cases this ADR's own Goal section exercises.

### Diagnostics, disposal, and seed observability

**Diagnostics.** This ADR's first draft claimed pipeline failures
propagate un-wrapped "matching `Compono.XunitV3`'s own choice" — that
description of `Compono.XunitV3` was wrong and is corrected here.
`Compono.XunitV3`'s real `ComposeAttribute` (`InvokeWithSeedOnFailure`)
catches every `CompositionException` a `Resolve`/`ResolveShared`/
`ShareExplicit` call throws and rewrites it via
`CompositionException.WithSeedInMessage(exception, seed)` before
re-throwing — specifically because a pipeline failure happens *before* a
completed row exists, so nothing else appends the seed for that case, and
a real test runner's failure display shows `Exception.Message`, not
`CompositionDiagnostic.ToString()`. Without that wrapping, a genuine
composition failure in `Compono.TUnit` would violate this same ADR's own
unconditional, pasteable-seed guarantee (the Seed observability
requirement below) exactly when it matters most — a failing row.
**Required design**: `ComposeAttribute` wraps every `Resolve`/
`ResolveShared`/`ShareExplicit` call the same way — catch
`CompositionException`, rethrow via `CompositionException.WithSeedInMessage`.
Only a plain-message `CompositionException` `Compono.TUnit` constructs
itself (a pre-composition signature-validation failure, which already has
the seed appended when constructed) has no separate pipeline exception to
rewrap. No new exception type either way.

**Disposal — Compono.TUnit adds no cleanup machinery, but "TUnit owns
100% of it" was an overclaim and is corrected here to what's actually
true: TUnit owns 100% of the *root, top-level returned arguments* — not
the whole composed object graph.** Verified directly, not assumed:
`grep`-ing every `.cs` file in `src/Compono` for `IDisposable`/
`IAsyncDisposable` returns zero matches. `CompositionRow` holds only a
`CompositionContext` reference and an `int` seed; `CompositionContext`
(`src/Compono/CompositionContext.cs`) and `CompositionScope`
(`src/Compono/CompositionScope.cs`, a plain `Dictionary<Type, object?>`)
hold no unmanaged resources, no event subscriptions, and nothing requiring
explicit teardown. Every value directly returned in `GenerateDataSources`'s
`object?[]` (bound to a top-level `[Compose]` method parameter — including
a `[Shared]` value, which per this ADR is always itself bound to a real
top-level parameter, never only a hidden row-internal reference) is
exactly what TUnit's `ObjectGraphDiscoverer` already roots and disposes —
confirmed empirically in this dive's probe (method-parameter values from a
data source: disposed by TUnit).

**What that root-level coverage does *not* extend to, confirmed by
re-reading `ObjectGraphDiscoverer`'s own traversal logic**: TUnit's
depth-1+ ("nested object") walk (`TraverseInitializerProperties`) is
scoped specifically to properties registered in
`InitializerPropertyRegistry` — "Registry for `IAsyncInitializer`
property metadata," a narrow TUnit lifecycle concept, not a general
reflection walk of arbitrary public properties. An ordinary nested
dependency a generated composition plan composes internally via
`context.Resolve<T>()` (e.g. `CreateOrderHandler`'s own `IOrderRepository`
constructor parameter, stored in a private field or an ordinary property
that doesn't implement `IAsyncInitializer`) — never itself a root
`[Compose]`/`[Shared]` parameter — is **not reachable by TUnit's
`ObjectGraphDiscoverer` at all**, and therefore never disposed by TUnit,
regardless of whether it implements `IDisposable`. This is not a new gap
`Compono.TUnit` introduces: it is the *same* "no automatic disposal for a
nested composed `IDisposable`, consumer's own responsibility" limitation
`Compono.XunitV3`'s own `GetData` remarks already accept for *every*
composed value (xUnit v3 disposes none of them, by design). `Compono.TUnit`
is narrower, not worse — it covers the root/top-level case xUnit v3
doesn't cover at all, and shares xUnit v3's existing accepted gap for the
nested case. **Accepted as-is, not solved**: building nested-dependency
disposal tracking would require Compono to gain provenance/reachability
information it has never had anywhere else in this codebase (the same
"Compono has no such mechanism" reasoning that ruled out the
`ITestEndEventReceiver` machinery below). A consumer whose composed test
class genuinely needs a nested `IDisposable` cleaned up should make that
dependency `[Shared]` (promoting it to a root, TUnit-covered argument) or
dispose it explicitly in the test body — the same two escape hatches
`Compono.XunitV3` consumers already have today.

This ADR's earlier draft proposed wiring `Compono.TUnit` into
`ITestEndEventReceiver` for row cleanup; that machinery is **removed** —
building it would have given Compono a disposal-tracking responsibility it
doesn't have anywhere else in this codebase, for the root-argument case
TUnit already disposes correctly on its own (and building it wouldn't
have reached the nested case above anyway, since `Compono.TUnit` itself
has no more provenance/reachability information about a nested dependency
than TUnit's own graph walker does).

**The root-level "TUnit disposes it" coverage above is correct for a
*fresh* value Compono itself composes — it is not automatically safe for
a root value that already had an external owner before Compono ever
touched it, and this ADR's first draft glossed over that distinction.**
`CompositionProviderResult`/
`CompositionResult` (ADR-0024) are deliberately opaque about provenance —
a value `UseServiceProvider(...)` or an exact `Register<T>(...)` factory
hands back is, by design, indistinguishable from one Compono's own
generated plan just constructed; `Compono.XunitV3`'s own `GetData` remarks
document this exact ambiguity and resolve it by simply never registering
a composed value with xUnit v3's `DisposalTracker` at all — disposal is
then entirely the consumer's own responsibility, always, for every
composed value. `Compono.TUnit` cannot take the same escape hatch: TUnit's
`ObjectTracker`/`ObjectGraphDiscoverer` are not opt-in per data-source
attribute — every value TUnit can reach as a root test argument
(confirmed in source: any `[Compose]`-composed value, since it's always
itself a top-level method parameter, whether marked `[Shared]` or not) is
tracked and, when its reference count reaches zero, disposed, regardless
of which attribute produced it. That reference count is genuinely
cross-test (a static, process-wide `ConcurrentDictionary<object, Counter>`
keyed by object identity — confirmed in `ObjectTracker.cs`) with no
provenance check (`ShouldSkipTracking` only tests `is IDisposable or
IAsyncDisposable`, nothing about origin) — so an externally-owned,
deliberately cross-test-shared disposable instance (a singleton returned
by `UseServiceProvider(...)`/an exact registration factory, composed as a
`[Compose]`/`[Shared]` parameter) is tracked exactly like a fresh value:
its count rises to 1 the first test that composes it, falls back to 0 and
gets **disposed** the moment that first test finishes — before any later
test that also needs the same shared instance ever runs. This directly
contradicts [ADR-0019](0019-registrations-and-service-provider-injection.md)'s
"the caller owns the provider and its entire lifetime; Compono is a pure
consumer" contract, and a freshly-constructed-and-never-reused
`IDisposable` test type (this ADR's own disposal-verification test,
PLAN-0040 Phase 0) cannot detect this failure mode at all — it only
manifests when the *same* disposable instance is deliberately reused
*across* tests, which that test doesn't exercise.

**Resolution: this is a real, documented non-goal for this release, not
silently left to accidentally work.** `Compono.TUnit` has no way to
distinguish a fresh value from an externally-owned one (the same
limitation `Compono.XunitV3` documents), and unlike `Compono.XunitV3` it
has no opt-out from TUnit's own disposal tracking to fall back on — so the
safe rule this ADR commits to is on the *consumer* side: **do not compose
an externally-owned, disposable, cross-test-shared instance (from
`UseServiceProvider(...)` or an exact `Register<T>(...)` factory returning
a shared/cached instance) as a `[Compose]`/`[Shared]` parameter under
`Compono.TUnit`.** A disposable value Compono itself freshly constructs
per composition is unaffected (its reference count is always exactly 1,
for exactly one test) — the constraint is specific to a value whose
lifetime is meant to outlive a single test. A resource that genuinely
needs cross-test sharing belongs in a mechanism TUnit itself owns the
lifetime of (its own `[ClassDataSource(Shared = ...)]`/assembly-fixture
model), not injected into a test through Compono composition. This
constraint is recorded here, in the Package Guide (PLAN-0040 Phase 0), and
as a `Compono.TUnit`-specific skill guardrail — a real, load-bearing
limitation, not an implementation footnote.

**Seed observability — a real TUnit-native equivalent exists, and this
ADR requires using it, not a degraded fallback.** TUnit's own first-party
mechanism for attaching discoverable, reportable metadata to a test case —
the same purpose `Compono.XunitV3`'s unconditional `Traits["Compono.Seed"]`
serves — is `ITestDiscoveryEventReceiver.OnTestDiscovered(DiscoveredTestContext)`
plus `DiscoveredTestContext.AddProperty(string key, string value)`
(`TUnit.Core`), the exact API TUnit's own built-in `PropertyAttribute`/
`CategoryAttribute` use, writing into `TestDetails.CustomProperties` — a
reporter-visible property bag, not an internal-only field. This is
reachable safely, by construction rather than emergent capture-order
behavior: `TestBuilder.CreateTestContextAsync` builds each row's
`TestContext` directly from the same `TestBuilderContext` instance
`GenerateDataSources` already receives via `dataGeneratorMetadata
.TestBuilderContext`, and `DiscoveredTestContext` wraps that exact
`TestContext` — so `TestContext.StateBag` (public `ITestStateBag`,
`TryGetValue<T>`/indexer over a `ConcurrentDictionary<string, object?>`)
is provably the same backing store at both points for one row, not two
separately-captured references whose identity merely happened to line up
(the property this ADR's `IClassConstructor` rejection above found
missing). **Required design**: `ComposeAttribute` also implements
`ITestDiscoveryEventReceiver`; inside `GenerateDataSources`'s deferred
`Func`, after composing the row, store the row's seed into
`dataGeneratorMetadata.TestBuilderContext.StateBag` under a
package-namespaced key (not an attribute-instance field — TUnit's own
`IClassConstructor` instance-reuse bug above is a standing reminder not to
trust per-invocation state on a reused attribute instance); in
`OnTestDiscovered`, read it back and call
`discoveredContext.AddProperty("Compono.Seed", seed.ToString())`. This
gives every row — pass or fail — a discoverable, reportable seed, the same
unconditional guarantee `Compono.XunitV3` already has, not a weaker
"failure-message-only" fallback. The one item still left to
`implement.md`: confirming this holds under TUnit's own retry/repeat
mechanisms (a retried test re-invoking the same row), which this dive did
not specifically probe.

### Positive Consequences

- No new public runtime `Compono` API required — `CompositionRow`'s
  framework-agnostic design (ADR-0021) validated by its first real second
  consumer, exactly as that ADR's own Positive Consequences anticipated.
  (`Compono.Generators`' discovery-time-only extension, below, is real
  work but adds no public surface — see "Generator discovery.")
- Full scope parity with `Compono.XunitV3` (profiles, inline values,
  `[Shared]`) from the first release, not a reduced MVP that needs a later
  parity pass.
- The disposal/lifecycle risk in `IClassConstructor`-based constructor
  composition is caught and avoided *before* it ships, not discovered
  after a real consumer hits a leaked/undisposed resource.
- Records a concrete, lower-risk landing spot (Option 3) for constructor
  composition if it's ever wanted, so a future design pass doesn't have to
  re-derive TUnit's extension surface from scratch.
- Full seed-observability parity with `Compono.XunitV3` — every row gets a
  discoverable, reportable seed via TUnit's own `AddProperty` mechanism,
  not a degraded fallback — and zero disposal machinery, since core
  `Compono` genuinely owns nothing that needs it. Both were verified
  directly against TUnit source rather than assumed either way.

### Negative Consequences

- No constructor-dependency composition in this release — TUnit's one
  genuinely distinctive capability relative to `Compono.XunitV3`
  (class-construction extensibility) is left on the table for now.
  Accepted: the evidence in Context shows Option 2's version of it isn't
  safe to build on yet, and Option 3's version is real future scope, not
  designed here.
- Duplicating `Compono.XunitV3`'s binding-delegate-caching pattern instead
  of extracting it is a deliberate, small maintenance cost (two similar-but-
  not-identical implementations to keep correct) — accepted per the "rule
  of three" reasoning above.
- The seed-observability design depends on `TestBuilder`'s
  `TestContext`/`TestBuilderContext` construction order (verified for the
  current TUnit version studied) continuing to hold — a smaller version of
  the same category of risk this ADR rejected `IClassConstructor` for, but
  accepted here because the correlation is direct construction (one method
  builds both objects from the same reference) rather than two separately-
  captured closures, and because `StateBag`/`AddProperty` are both public,
  documented API surfaces TUnit's own built-in attributes already depend
  on the same way.
- An externally-owned, cross-test-shared disposable instance composed as
  a `[Compose]`/`[Shared]` parameter is not safe under `Compono.TUnit`
  (see "Diagnostics, disposal, and seed observability" above) — a real
  constraint `Compono.XunitV3` doesn't share (xUnit v3's disposal is
  opt-in and `Compono.XunitV3` opts out entirely), because TUnit's own
  disposal tracking has no such opt-out to inherit. Accepted as a
  documented non-goal rather than solved, since Compono has no provenance
  information to solve it with — the same limitation `Compono.XunitV3`
  already documents, inherited here under a stricter runner.
- A nested, non-root `IDisposable` dependency a generated plan composes
  internally (never itself a `[Compose]`/`[Shared]` top-level parameter,
  never exposed via a TUnit `IAsyncInitializer`-typed property) is never
  disposed by TUnit or by Compono — `ObjectGraphDiscoverer`'s nested-object
  traversal is scoped to TUnit's own `IAsyncInitializer` property registry,
  not a general graph walk, confirmed by reading it directly. Accepted:
  this is the same "no automatic disposal for a nested composed value"
  limitation `Compono.XunitV3` already accepts for *every* composed value
  (it disposes none of them); `Compono.TUnit` is narrower, not worse — it
  additionally covers the root/top-level case xUnit v3 never covers at
  all. A consumer needing a nested dependency disposed promotes it to
  `[Shared]` (a root argument) or disposes it explicitly in the test body.

## Pros and Cons of the Options

### Method-parameter composition only (chosen)

- Good, because every mechanism it needs is already-verified, fully
  supported TUnit API with no undocumented coupling.
- Good, because it ships full parity with `Compono.XunitV3`'s proven
  scope, not a reduced one.
- Bad, because it doesn't use TUnit's one genuinely distinctive
  extension point (class construction) at all in this release.

### Constructor composition via `IClassConstructor`

- Good, because it would be more powerful than anything `Compono.XunitV3`
  could ever offer — one composed test class, dependencies and parameters
  sharing one `[Shared]` scope.
- Bad, because the cross-hook `CompositionRow` sharing it needs rests on
  `TestBuilder`-internal capture-order behavior no doc or test commits to.
- Bad, because TUnit's own documentation about `IClassConstructor`'s
  instance lifetime is directly contradicted by observed behavior — a
  fragile foundation to design a public Compono contract against.
- Bad, because it silently creates a disposal-tracking responsibility
  Compono has never taken on anywhere else in this codebase.

### Constructor composition via a class-level data-source attribute

- Good, because it's fully supported API — the same base class this ADR
  already builds on, and the same mechanism TUnit's own
  `DependencyInjectionDataSourceAttribute` and `AutoFixture.TUnit` use.
- Good, because TUnit tracks and disposes the produced values like any
  other test argument — no new disposal responsibility.
- Bad, because it adds real scope (a `DataGeneratorType.ClassParameters`
  branch, the double-factory-invocation quirk class-level data sources
  have) beyond what a first release needs — deferred, not rejected.

## Links

- [ADR-0039](0039-future-extension-package-admission-gate-and-release-sequence.md) —
  the admission gate `Compono.TUnit` cleared (Gate A) and the evidence
  trigger this ADR records (Gate B, explicit product-owner request).
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction framework this ADR's Option 3 deferral
  follows (a named future path, not a designed one).
- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md) —
  `CompositionRow`/`Composer.CreateRow`, the entry point this design
  reuses entirely unmodified; its own Positive Consequences anticipated
  exactly this reuse.
- [ADR-0022](0022-compono-xunit-package-design.md) —
  `Compono.XunitV3`'s package design, the template this ADR's scope and
  shape mirror (attribute family, diagnostics, seed policy).
- [ADR-0024](0024-public-provider-extensibility-model.md) — the
  "illustrative, not committed" framing this ADR's code sketch follows
  for the same reason: proving the design is buildable without locking in
  implementation micro-detail `implement.md` should still own.
- `docs/roadmap/future-packages.md` — the admitted-candidate entry this
  ADR promotes to real roadmap content once `Accepted`.

## Amendment 1 (2026-08-12): Row-binding dispatch mechanism revised for Native AOT

The "Package shape" section's illustrative sketch and PLAN-0040's Phase 0
originally assumed `Compono.TUnit.Binding.RowInvokers` would use the same
`MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` dispatch pattern
`Compono.XunitV3.Binding.RowInvokers` already ships with — not called out
explicitly here as a design decision because it was inherited, unexamined,
from the package this ADR's binding logic mirrors.

That pattern is not Native AOT-safe: `MakeGenericMethod` with a `Type`
computed at runtime has no statically-discoverable closed generic
instantiation for a Native AOT compiler to pre-compile. TUnit stakes a
headline claim on Native AOT support, making this gap a real release
concern for `Compono.TUnit` specifically, unlike for `Compono.XunitV3`
(whose consumers don't typically publish Native AOT). Compounded by
`publish-preview.yaml` auto-publishing every packable `src/` project on
every push to `main` — merging PLAN-0040's Phase 0 as originally drafted
would have auto-published a `Compono.TUnit` preview package containing
this gap.

[ADR-0041](0041-aot-safe-row-binding-dispatch.md) records the resulting
decision: a generator-emitted `RowInvokerCache<T>` in core `Compono`,
replacing reflection-based dispatch in both `Compono.TUnit` and
`Compono.XunitV3`. This ADR's own "Package shape" sketch and disposal/seed
sections are otherwise unaffected — this amendment changes *how*
`RowInvokers` gets its delegates, not the binding algorithm, seed policy,
or diagnostics behavior those sections describe. PLAN-0040's Phase 0 is
revised to build against `RowInvokerCache<T>` from the start, per
ADR-0041's own Decision Outcome.
