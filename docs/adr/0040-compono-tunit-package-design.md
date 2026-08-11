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
   and this dive confirms that holds: no core change is needed. This ADR
   is package-design-only, the same shape as
   [ADR-0025](0025-compono-nsubstitute-package-design.md)/[ADR-0027](0027-compono-bogus-package-design.md).
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

public class ComposeAttribute : UntypedDataSourceGeneratorAttribute
{
    protected override IEnumerable<Func<object?[]?>> GenerateDataSources(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        // Lazily composes on each factory invocation - never before TUnit
        // actually calls the returned Func, matching TUnit's own
        // "defer real work into the Func" convention
        // (DependencyInjectionDataSourceAttribute does the same).
        yield return () =>
        {
            var row = _composer.CreateRow(dataGeneratorMetadata.TestClassType);
            // ...bind each of dataGeneratorMetadata's method parameters
            // through row.Resolve<T>(descriptor)/row.ResolveShared<T>(descriptor),
            // exactly like Compono.XunitV3's BindingPlan already does for
            // MethodInfo/ParameterInfo - TUnit's DataGeneratorMetadata
            // exposes the equivalent parameter metadata this needs.
            return boundArguments;
        };
    }
}

public sealed class SharedAttribute : Attribute;
```

Illustrative, not committed — the exact binding-plan mechanics
(delegate caching, inline-value precedence, profile resolution) are
implementation detail for `implement.md`'s own pass, following
`Compono.XunitV3`'s proven shape as the template, not reinvented from
scratch.

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

**Disposal — Compono.TUnit adds no cleanup machinery. TUnit owns 100% of
it.** Verified directly, not assumed: `grep`-ing every `.cs` file in
`src/Compono` for `IDisposable`/`IAsyncDisposable` returns zero matches.
`CompositionRow` holds only a `CompositionContext` reference and an `int`
seed; `CompositionContext` (`src/Compono/CompositionContext.cs`) and
`CompositionScope` (`src/Compono/CompositionScope.cs`, a plain
`Dictionary<Type, object?>`) hold no unmanaged resources, no event
subscriptions, and nothing requiring explicit teardown — every object they
reference is either GC-reclaimable once the row itself becomes
unreachable, or *is* one of the composed values already returned in the
argument array. There is nothing left for Compono to own once
`GenerateDataSources`'s deferred `Func` returns its `object?[]`: every
value in that array (including a `[Shared]` value, which is always itself
bound to a real top-level method parameter, never only a hidden
row-internal reference) is exactly what TUnit's `ObjectGraphDiscoverer`
already roots and disposes — confirmed empirically in this dive's probe
(method-parameter values from a data source: disposed by TUnit). This
ADR's earlier draft proposed wiring `Compono.TUnit` into
`ITestEndEventReceiver` for row cleanup; that machinery is **removed** —
building it would have given Compono a disposal-tracking responsibility it
doesn't have anywhere else in this codebase, for values TUnit already
disposes correctly on its own.

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

- No core `Compono` change required — `CompositionRow`'s framework-agnostic
  design (ADR-0021) validated by its first real second consumer, exactly
  as that ADR's own Positive Consequences anticipated.
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
