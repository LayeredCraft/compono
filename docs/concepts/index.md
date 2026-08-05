# Concepts

This section builds the mental model Compono's other sections assume —
what each piece is and when to reach for it, not the implementation details
behind it (that's [Architecture](../architecture.md)'s job).

- [The Composition Model](composition-model.md) — what "composing" a graph
  means in Compono's terms.
- [Profiles](profiles.md) — grouping configuration into a reusable unit.
- [Registrations and Rules](registrations-and-rules.md) — the configuration
  surface: `Register<T>`, `For<T>().Use(...)`, member rules.
- [Shared Values](shared-values.md) — `[Shared]`, reusing one instance
  across a composition.
- [Providers](providers.md) — what a provider is and does.
- [Determinism and Seeding](determinism-and-seeding.md) — reproducible
  failures.
- [Collections](collections.md) — `CreateMany<T>()` and collection-size
  policy.

Read these in order if you're new — each one builds on the last. If you
already know what you're looking for, jump straight to the page above, or
to [How-to Guides](../how-to/index.md) to apply a concept to a task.

## Next

- Apply a concept to a specific task → [How-to Guides](../how-to/index.md).
- See which package a concept lives in → [Package Guides](../packages/index.md).
- Want a narrow recipe instead of the full model? → [Cookbook](../cookbook/index.md).
- Need the precise API contract? → [Reference](../reference/index.md).
