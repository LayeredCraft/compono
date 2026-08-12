# Compono.TUnit

Only relevant if the project references `Compono.TUnit`. Requires real
TUnit (`TUnit`/`TUnit.Core` + Microsoft Testing Platform runner). Depends
on `Compono` (the source generator flows through transitively).

This is PLAN-0040's first, method-parameter-only slice (Phase 0) — see
ADR-0040 for the full design and which forms ship in which phase.

## `[Compose]`

```csharp
[Test]
[Compose]
public async Task ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

[Test]
[Compose(42, "widget")]           // inline binds positionally left-to-right
public async Task InlineValuesAreUsedDirectly(int quantity, string productName) { }

[Test]
[Compose(42)]                     // quantity inline, productName composed
public async Task MixesInlineAndComposedValues(int quantity, string productName) { }

[Test]
[Compose(Seed = 4219)]
public async Task ReproducesTheSameComposedValues(Order order) { }
```

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately,
  before any row state is reported.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — a distinct attribute type from
  `Compono.XunitV3.SharedAttribute`, with identical binding rules
  (duplicated per ADR-0040's "Row-binding logic: duplicated, not
  extracted" section).
- A passing row reports its seed back as a `Compono.Seed` custom property
  (`TestContext.Current.Metadata.TestDetails.CustomProperties`) —
  TUnit's own place for this, distinct from `Compono.XunitV3`'s trait
  mechanism. Check it in test output before asking for a re-run.
- Composition happens at data-generation time, not a separate discovery
  pass.

## `[Compose<TProfile>]` / `[Compose<TProfile, TConfig>]`

Not part of this first slice — see PLAN-0040's later phases and
`references/xunit-v3.md` for the shape these will eventually mirror once
they land in `Compono.TUnit` too. Until then, `Compono.TUnit` has no
profile-application mechanism at all — a `[Compose]`-composed type that
needs a substitute, Bogus-generated data, or a custom registration can't
get one through this package yet.

## Disposal — read before assuming automatic cleanup

TUnit disposes a `[Compose]`-composed **root** method argument itself,
automatically, once the test completes. A non-`[Shared]` dependency
**nested** inside a composed argument is disposed by no one — TUnit's own
nested-object disposal tracking is scoped to `IAsyncInitializer`-registered
properties, not a general graph walk. Don't compose a cross-test-shared
disposable as `[Compose]`/`[Shared]` either — TUnit's shared-value
reference counting has no provenance awareness of where a value came
from. See ADR-0040's "Diagnostics, disposal, and seed observability"
section for the full reasoning — don't assume `Compono.XunitV3`'s own
disposal story (no automatic disposal at all, PR #24) carries over
unchanged; the two packages differ here because TUnit's own execution
model differs from xUnit v3's.

## Stacking Compose-family attributes: undefined, not rejected

Unlike `Compono.XunitV3` (which throws a clear `CompositionException` for
this shape), `Compono.TUnit`'s `BindingPlan.Build` does not currently
detect more than one Compose-family attribute stacked on the same method -
`MethodMetadata` doesn't expose the method's own attribute list the way a
raw `MethodInfo` does, and this check hasn't been added yet (a known v1
gap, tracked in PLAN-0040's Phase 1 checklist ("Stacked Compose-family attribute validation")). Don't stack Compose-family
attributes on one TUnit test method - the result is undefined, not a
documented failure mode; if you see it in review, flag it the same way
you'd flag any other unsupported shape.

## No fixture object

There's nothing like AutoFixture's `IFixture` to hold onto across a test
class.

## Real examples in this repo

- `test/Compono.TUnit.SampleTests/CompositionTests.cs` — a plain
  `[Compose]`-composed `OrderService` through the real packaged
  `Compono.TUnit -> Compono` dependency (not a `ProjectReference`).
- `test/Compono.TUnit.SampleTests/SharedTests.cs` — `[Shared] Repository
  repository, OrderService service`.
- `test/Compono.TUnit.SampleTests/DisposalTests.cs` — the root-disposed
  vs. nested-not-disposed proof, using a plain purpose-built
  `IDisposable` type, not a mocking-library substitute.
