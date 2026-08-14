# Providers

## What a provider is

A provider is a pluggable rule for satisfying a value that isn't covered by
an explicit [registration or rule](registrations-and-rules.md) — the
mechanism `Compono.NSubstitute` and `Compono.Bogus` use to add their
behavior without the core `Compono` package knowing either of them exists.
A provider implements one method:

```csharp
public interface ICompositionValueProvider
{
    CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context);
}
```

It returns `CompositionProviderResult.NotHandled` for anything it doesn't
apply to, or `CompositionProviderResult.Handled(value)` for a value it
produces — it's a "maybe I can help with this one" rule, not a required
handler for every type.

## The built-in providers

- **`Compono.Bogus`'s `BogusMemberNameProvider`** matches `string`-typed
  members by exact name against a known convention list (`FirstName`,
  `Email`, and similar), producing a deterministically-seeded, semantically
  realistic value instead of an arbitrary one.
- **`Compono.NSubstitute`'s `NSubstituteProvider`** matches any interface
  or delegate type (and, optionally, unsealed abstract classes), producing
  a configured `Substitute.For(...)` instead of requiring a hand-written
  fake implementation.
- **`Compono.TestDoubles`'s `GeneratedTestDoubleProvider`** matches an
  interface type `Compono.Generators` emitted a double for at compile time
  (the `ComponoGeneratedTestDoubles` opt-in), producing that generated,
  AOT-safe double instead of a runtime proxy. It runs at the same
  test-double stage as `NSubstituteProvider` — registration order between
  `UseGeneratedTestDoubles()` and `UseNSubstitute()` decides which one
  resolves an interface request first if both are installed. See
  [`Compono.TestDoubles`](../packages/compono-testdoubles.md).

None of these are wired in by default —
`UseBogus()`/`UseNSubstitute()`/`UseGeneratedTestDoubles()` add them to the
pipeline explicitly, which is also why the core `Compono` package has zero
dependency on `Bogus`, `NSubstitute`, or `Compono.TestDoubles`.

## Why two provider stages

`Compono.Bogus`'s convention matching and `Compono.NSubstitute`'s
substitute creation don't compete for the same values (a `string` member
and an interface dependency are never the same request), but they run as
two logically distinct stages precisely so that ordering between
`UseBogus()`/`UseNSubstitute()` calls never matters — a provider only
influences the stage it's registered into. The exact stage order relative
to registrations, rules, and generated default construction is
[The Provider Pipeline](../architecture/current/provider-pipeline.md)'s
concern, not this page's — what
matters here is that a provider is always a fallback, tried only once
nothing more specific (an exact registration, a type/member rule) already
claimed the value.

## Next

- Providers that ship today → [`Compono.NSubstitute`](../packages/compono-nsubstitute.md),
  [`Compono.Bogus`](../packages/compono-bogus.md),
  [`Compono.TestDoubles`](../packages/compono-testdoubles.md) Package Guides.
- Write your own → [The Provider Pipeline](../architecture/current/provider-pipeline.md)'s
  provider extensibility contract.
- The full pipeline a provider participates in → [The Provider Pipeline](../architecture/current/provider-pipeline.md).
