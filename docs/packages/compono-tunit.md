# Compono.TUnit

TUnit integration — a `[Compose]` data source attribute that composes test
method parameters directly, instead of hand-building `[Arguments]` rows or a
custom data source generator.

## When to install

You write TUnit tests and want method parameters composed automatically:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.TUnit --prerelease
```

`Compono.TUnit` doesn't add a TUnit test host for you — it integrates with
an existing one. If your project targets xUnit v3, NUnit, or MSTest, see
[`Compono.XunitV3`](compono-xunitv3.md) instead (or, for NUnit/MSTest, core
`Compono` still works standalone via `Composer.Create()` and the resulting
composer's own `Create<T>()`).

## What it gives you (today)

This is the first, method-parameter-only slice of `Compono.TUnit` — see
[ADR-0040](../adr/0040-compono-tunit-package-design.md) for the full design
and [PLAN-0040](../plans/0040-compono-tunit-package-design.md) for what
ships in which phase.

- **`[Compose]`** — every method parameter is composed:

  ```csharp
  [Test]
  [Compose]
  public async Task Saves_order(OrderService service)
  {
      await Assert.That(service).IsNotNull();
  }
  ```

- **Inline + composed mixing** — `[Compose(42, "widget")]` binds inline
  values left-to-right; anything left over is composed.
- **`[Shared]`** — reuse one composed instance across every parameter (or
  nested dependency) in the same row that requests the same type. See
  [Shared Values](../concepts/shared-values.md).
- **`Compose(Seed = ...)`** — reproduce a specific composed row exactly;
  a passing row also reports the seed back as a `Compono.Seed` custom
  property (`TestContext.Current.Metadata.TestDetails.CustomProperties`),
  and a *composition* failure's message includes the seed that produced it.

`[Compose<TProfile>]` and `[Compose<TProfile, TConfig>]` — profile
selection and profile configuration arguments, matching
[`Compono.XunitV3`](compono-xunitv3.md#profile-configuration-arguments)'s
own shape — are not part of this first slice; see PLAN-0040's later phases.

## Disposal

TUnit disposes a `[Compose]`-composed **root** method argument itself,
automatically, once the test completes — `Compono.TUnit` needs no
`IDisposable`/`ITestEndEventReceiver` cleanup of its own for that case. A
non-`[Shared]` dependency **nested** inside a composed argument (e.g. a
constructor parameter one level down) is disposed by no one: TUnit's own
nested-object disposal tracking is scoped to `IAsyncInitializer`-registered
properties, not a general graph walk. Don't compose a cross-test-shared
disposable as a `[Compose]`/`[Shared]` parameter either — TUnit's reference
counting for shared values has no provenance awareness of where a value
came from. See ADR-0040's "Diagnostics, disposal, and seed observability"
section for the full reasoning.

## What it deliberately doesn't do

- **No stacking distinct Compose-family attributes on one method** — same
  reasoning and same `CompositionException` behavior as
  [`Compono.XunitV3`](compono-xunitv3.md#what-it-deliberately-doesnt-do).
- **No fixture object** — configuration lives in a profile, applied per
  test method, not a shared mutable object (once profile support ships).

## Next

- [Shared Values](../concepts/shared-values.md)
- [ADR-0040](../adr/0040-compono-tunit-package-design.md)
