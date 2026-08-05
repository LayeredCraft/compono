---
title: Seed a Specific Failing Case for Reproduction
description: Reuse a reported seed to reproduce the exact same composed values a failure was reported against.
packages: [Compono, Compono.XunitV3]
concepts: [determinism-and-seeding]
---

# Seed a Specific Failing Case for Reproduction

## Problem

A composed test failed — in CI, or on a teammate's machine — and you need
the *exact* composed values that produced it, not a plausible-looking
re-creation.

## Solution

A composition failure's own message always ends with the seed that
produced it:

```text
Compono.CompositionException : No provider could satisfy IShippingCalculator.

Order -> IShippingCalculator

Seed: 24601
```

Paste that value straight into `[Compose(Seed = ...)]`:

```csharp
[Theory]
[Compose(Seed = 24601)]
public void ReproducesTheReportedFailure(Order order) { }
```

Running this reproduces the identical composed row that failed originally
— same values, same failure, every time.

## Discussion

For programmatic composition (no `Compono.XunitV3` theory involved), the
seed lives on the caught `CompositionException`'s `Diagnostic` property
instead of the message text directly:

```csharp
try
{
    composer.Create<Order>();
}
catch (CompositionException exception)
{
    Console.WriteLine(exception.Diagnostic); // ... Seed: 24601
}
```

Reuse it with `Composer.Create(builder => builder.WithSeed(24601))` — the
programmatic equivalent of `[Compose(Seed = ...)]`. The two seed paths
(row-scoped vs. programmatic) are independent derivations, not
interchangeable with each other — reproduce a `[Compose]` failure with
`[Compose(Seed = ...)]`, and a programmatic one with `WithSeed(...)`.

## See also

- [Determinism and Seeding](../concepts/determinism-and-seeding.md) — the
  full mechanics of how a seed drives every composed value, and why this
  reproduces exactly, not just approximately.
- [Diagnostics Reference](../reference/diagnostics.md) — every `CMP`
  diagnostic code, for the compile-time failure case seeding doesn't cover.
