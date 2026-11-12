---
title: Vary Composed Configuration per Theory Row
description: Drive a table of rows through one composed theory, giving every row its own profile configuration and expected values with ComposeData<TProfile, TConfig>.
packages: [Compono, Compono.XunitV3]
concepts: [profiles]
---

# Vary Composed Configuration per Theory Row

## Problem

A `[Theory]` needs several cases, and the *collaborators themselves* must be
built differently for each case — not just the input values. A price
calculator, for example, applies a different discount rate per case, and that
rate lives in the profile that registers the calculator. `[InlineData]` can't
carry a composed collaborator, and a single `[Compose<TProfile, TConfig>]`
attribute fixes one configuration for the whole method.

## Solution

Reach for `[ComposeData<TProfile, TConfig>]`. It reads a table of rows from a
public static member — exactly the shape xUnit's `[MemberData]` expects — and
composes each row against a **fresh profile built from that row's own
`TConfig`**. Element 0 of every row is the row's `TConfig`; the remaining
elements are the method's values, bound left to right.

```csharp
public sealed record PricingConfig(decimal DiscountRate);

public sealed class PriceCalculator
{
    private readonly decimal _discountRate;

    public PriceCalculator(PricingConfig config) => _discountRate = config.DiscountRate;

    public decimal ApplyDiscount(decimal price) => price * (1 - _discountRate);
}

public sealed class PricingProfile : ICompositionProfile
{
    private readonly PricingConfig _config;

    public PricingProfile(PricingConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new PriceCalculator(_config));
}

public sealed class PricingTests
{
    // [config, ...row values] - one row per case.
    public static readonly object?[][] Rows =
    [
        [new PricingConfig(0.10m), 100m, 90m],
        [new PricingConfig(0.25m), 80m, 60m],
    ];

    [Theory]
    [ComposeData<PricingProfile, PricingConfig>(nameof(Rows))]
    public void Applies_discount(decimal price, decimal expected, PriceCalculator calculator) =>
        calculator.ApplyDiscount(price).Should().Be(expected);
}
```

`PriceCalculator` is composed once per row, from that row's `PricingConfig` —
two rows, two distinct discount rates, one test method.

## Discussion

The three ComposeData forms cover the cases around this one:

- `[ComposeData(nameof(Rows))]` — no profile, every element is a row value;
  remaining parameters are composed normally (the default composition).
- `[ComposeData<TProfile>(nameof(Rows))]` — one fixed `TProfile`, applied
  afresh to every row; every element is a row value.
- `[ComposeData<TProfile, TConfig>(nameof(Rows))]` — above: element 0 of each
  row is its `TConfig`.

Prefer the **strongest type** for row values — an `enum`, not a magic
string; `typeof(...)` for a CLR type — the same guidance as
`[Compose<TProfile, TConfig>]`'s profile configuration arguments.

**Binding is strict and positional.** Row value `i` binds to parameter `i`;
there is no numeric widening (an `int` row value won't fill a `long`
parameter) and no xUnit `TypeHelper` conversion. Null is allowed only for a
nullable value type or a `?`-annotated reference. Put the row values first in
the parameter list; composed parameters follow.

**Need the row's `TConfig` in the test body?** Don't repeat its values as
extra row elements. Have the profile register the config it already
receives, `builder.Register(() => config)`, and add a composed `TConfig`
parameter after the row values. It resolves to that row's own instance.
See [Reading the row's `TConfig`](../packages/compono-xunitv3.md#per-row-configuration).

**Optional: a consumer-side base profile.** If many profiles repeat that
line, you can reduce the boilerplate with your own small base type — this
is a convenience in your test project, not a Compono type or API, and
Compono won't ship it:

```csharp
public abstract class ConfigProfile<TConfig>(TConfig config) : ICompositionProfile
    where TConfig : class
{
    protected TConfig Config => config;

    public void Configure(CompositionBuilder builder)
    {
        builder.Register(() => config);
        ConfigureCore(builder);
    }

    protected abstract void ConfigureCore(CompositionBuilder builder);
}
```

`PricingProfile` could derive from `ConfigProfile<PricingConfig>` and put
only its own registrations in `ConfigureCore`. A derived profile must not
also call `Register(() => config)` for `TConfig` — that's a duplicate
registration and fails composition (a `CompositionException` wrapping a
`CompositionConfigurationException`).

**`[Shared]` still works.** Mark a parameter `[Shared]` and, when a row
supplies a value at that position, that value becomes the single instance
every other parameter in the row resolving that type receives; otherwise
Compono resolves it. Sharing is scoped to a single row.

**The row member runs at execution time**, after fixtures are initialized, so
it may read `TestContext.Current.RequireFixture<TFixture>()`. See
[Use xUnit Fixture Values in a Profile](use-xunit-fixture-values-in-a-profile.md).

**One seed, no override.** The attribute's `Seed` pins every row, or a
profile's `WithSeed(...)` does; setting both is a configuration error. To
reproduce a failing row, paste the seed from the failure message into
`[ComposeData<...>(nameof(Rows), Seed = 24601)]`.

Under Native AOT, the same `[ComposeData<TProfile, TConfig>]` source works
unchanged via `Compono.XunitV3.Aot`; a few shapes JIT can run are rejected at
compile time on AOT instead.

## See also

- [Composed theory rows (`[ComposeData]`)](../packages/compono-xunitv3.md#composed-theory-rows-composedata) —
  the full reference: row/collection shapes, metadata merge, seeds, disposal,
  and failure granularity.
- [Seed a Specific Failing Case for Reproduction](seed-a-specific-failing-case-for-reproduction.md) —
  the seed mechanics ComposeData shares.
- [Migrating from AutoFixture](../migrating-from-autofixture.md) — mapping
  `[InlineAutoData]`/`[MemberAutoData]` tables onto `[ComposeData]`.
