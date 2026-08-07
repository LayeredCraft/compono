# Learning Paths

Every page linked below already exists somewhere else in this site — this
page is pure navigation, a curated order through material that's organized
elsewhere by topic rather than by starting point. Pick the path that
matches why you're here.

## I'm new to Compono

1. [What is Compono?](index.md)
2. [Installation](installation.md)
3. [Your First Composed Theory](first-test.md)
4. [The Composition Model](../concepts/composition-model.md)
5. [Registrations and Rules](../concepts/registrations-and-rules.md)
6. [How-to Guides](../how-to/index.md) — pick the task closest to what
   you're building.

## I'm migrating from AutoFixture

1. [Migrating from AutoFixture](../migrating-from-autofixture.md)
2. [The Composition Model](../concepts/composition-model.md)
3. [Shared Values](../concepts/shared-values.md) — the `[Shared]` alternative
   to `Freeze<T>()`.
4. [Profiles](../concepts/profiles.md) — the alternative to
   `AutoDataAttribute` customizations.
5. [Determinism and Seeding](../concepts/determinism-and-seeding.md) — how
   reproducing a failure differs from AutoFixture's approach.

## I use xUnit

1. [Your First Composed Theory](first-test.md)
2. [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md)
3. [Write a Composed Theory](../how-to/write-a-composed-theory.md)
4. [Share a Value Across a Test](../how-to/share-a-value-across-a-test.md)
5. [Use Profiles](../how-to/use-profiles.md)

## I use NSubstitute

1. [Providers](../concepts/providers.md)
2. [`Compono.NSubstitute` Package Guide](../packages/compono-nsubstitute.md)
3. [Share a Value Across a Test](../how-to/share-a-value-across-a-test.md) —
   sharing a composed substitute across several parameters.

## I want realistic data

1. [`Compono.Bogus` Package Guide](../packages/compono-bogus.md)
2. [Customize a Member](../how-to/customize-a-member.md) — combining a
   `Compono.Bogus` convention with an explicit member rule.
3. [Cookbook](../cookbook/index.md) — narrow recipes like "generate a
   realistic email."

## I want to extend Compono

1. [Providers](../concepts/providers.md)
2. [The Provider Pipeline](../architecture/current/provider-pipeline.md) —
   the `ICompositionValueProvider` extensibility contract, and the
   resolution pipeline a custom provider participates in.

## Next

Not sure which path fits? [Next Steps](next-steps.md) branches out by what
you want to do next, rather than by who you already are.
