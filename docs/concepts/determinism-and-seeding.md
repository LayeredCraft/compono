# Determinism and Seeding

## What "deterministic by design" means for you

Every composed value Compono produces — a generated default construction,
a `Compono.Bogus` fake, anything derived from randomness anywhere in the
pipeline — comes from a seed. Given the same seed and the same
configuration, a composition produces the same values every time. In
practice, that means a *test author* never has to reconstruct "what values
did it use when this failed" by guesswork — the seed that produced a
failure is always available, and reusing it reproduces that exact failure
again.

You don't manage this by hand for the common case — a `Composer` picks a
fresh seed automatically unless you set one. Setting a seed is something
you reach for deliberately, not something every test needs to do.

## Setting a seed explicitly

```csharp
var composer = Composer.Create(builder => builder.WithSeed(4219));
```

```csharp
[Theory]
[Compose(Seed = 4219)]
public void ReproducesTheSameComposedValues(Order order) { }
```

The same seed produces the same output for a given version of Compono
(not guaranteed across versions — a new release can change generated
values for the same seed, the same way any library's internal generation
details can shift between versions).

## Reproducing a failure

A composition failure — not a failed assertion, a failure to *build* the
requested graph at all — always makes the seed that produced it available,
though exactly where depends on how you're composing:

- **Programmatic `Composer.Create<T>()`** — `CompositionException.Message`
  itself doesn't include the seed; inspect the exception's
  `Diagnostic` property instead (nullable — not every failure produces one,
  e.g. `HashSet<T>`/`Dictionary` unique-value-exhaustion failures don't). Its
  rendered form includes a tree-rendered path to exactly where composition
  couldn't proceed, and the seed:

  ```csharp
  catch (CompositionException exception)
  {
      Console.WriteLine(exception.Diagnostic);
      // Unable to compose Order.
      //
      // Order -> IShippingCalculator
      //
      // No provider could satisfy IShippingCalculator.
      //
      // Seed: 24601
  }
  ```

- **A composed `Compono.XunitV3` theory row** — `[Compose]` rewrites the
  thrown exception's own `Message` to append `Seed: ...` regardless of
  whether a `Diagnostic` is present, so a failing test's output always ends
  with a pasteable seed without needing to inspect `Diagnostic` separately.

Paste that seed back into `[Compose(Seed = 24601)]` (or
`builder.WithSeed(24601)` for programmatic composition) to reproduce the
exact same composed values on a subsequent run — the same mechanism whether
you're debugging locally or looking at a CI failure someone else reported.

## Why this matters more than it sounds like it should

A flaky-looking test failure that only reproduces "sometimes" is one of
the more expensive categories of bug to chase down, because the input that
triggered it is usually gone by the time anyone looks. Compono treats every
composed value as reproducible by construction, so a composition-related
failure is never a "works on my machine, can't repro" report — the seed
*is* the repro.

## Next

- The pipeline stage that actually derives per-value randomness from a
  seed → [Deterministic Seeding](../architecture/current/deterministic-seeding.md).
- Reproduce a specific failing xUnit theory row →
  [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md).
- Diagnostic codes and messages → [Diagnostics Reference](../reference/diagnostics.md).
