# Compono (Core)

The core composition engine — `Composer`, the resolution pipeline, and the
source generator. Every other Compono package depends on this one; this one
depends on nothing else in the ecosystem — the core package never
references or knows about an integration package (see
[Design Principles](../architecture/design-principles.md#guiding-principles),
"modular architecture").

## When to install

Always — it's the one package every Compono project needs, and it's a
dependency of `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`, so
`dotnet add package` will pull it in transitively even if you never add it
directly.

```bash
dotnet add package Compono
```

## What it gives you

- **`Composer`** — the immutable entry point. `Composer.Create()` and
  `Composer.Create(builder => ...)` build a reusable composer once; see
  [The Composition Model](../concepts/composition-model.md) for the full
  picture.
- **The resolution pipeline** — registrations, type/member rules, semantic
  and test-double providers (contributed by `Compono.Bogus`/
  `Compono.NSubstitute` when installed), built-in value providers, and
  generated construction plans, tried in that order. See
  [Providers](../concepts/providers.md) and
  [Registrations and Rules](../concepts/registrations-and-rules.md)
  (including explicit constructor selection for a type with more than one
  accessible constructor — `For<T>().UseConstructor<...>()`, ADR-0002
  Amendment 3/ADR-0052).
- **The source generator**, embedded as a Roslyn analyzer inside
  `Compono.nupkg` (`analyzers/dotnet/cs`, containing
  `Compono.Generators.dll`) — not a separate package or opt-in step.
  Referencing `Compono` is the only action needed to enable source
  generation.
- **`[Shared]`-independent primitives** — `[Composable]`,
  `ICompositionProfile`, `CompositionException`/`CompositionDiagnostic`,
  and deterministic seeding (`WithSeed`,
  [Determinism and Seeding](../concepts/determinism-and-seeding.md)).

## What it deliberately doesn't do

`Compono` has no dependency on any test framework, mocking framework, or
Bogus. It has no opinion on xUnit v3 vs. any other test framework, no
built-in test-double support, and no realistic-fake-data generation — those
are each their own package
([`Compono.XunitV3`](compono-xunitv3.md)/[`Compono.NSubstitute`](compono-nsubstitute.md)/
[`Compono.Bogus`](compono-bogus.md)), added independently so a project that
only needs plain object composition never pulls in a test-framework or
mocking-library dependency it doesn't use.

## No other setup required

Adding the `Compono` `PackageReference` is the only step — no
`nuget.config` entry beyond your normal NuGet feed, no MSBuild property to
opt in, and no separate generator package to reference. See
[Installation](../getting-started/installation.md#no-other-setup-required)
for a verification snippet that confirms the generator is actually wired
up.
