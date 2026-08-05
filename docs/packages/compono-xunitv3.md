# Compono.XunitV3

xUnit v3 integration — theory data attributes that compose test parameters
directly, instead of hand-building `[MemberData]` rows or a custom
`AutoDataAttribute` wrapper.

## When to install

You write xUnit v3 tests (`xunit.v3` + the Microsoft Testing Platform
runner) and want theory parameters composed automatically:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.XunitV3 --prerelease
```

`Compono.XunitV3` doesn't add an xUnit v3 test host for you — it integrates
with an existing one. If your project targets NUnit, MSTest, or plain
xUnit v3 without composed data, you don't need this package; core `Compono`
still works standalone via `Composer.Create<T>()`.

## What it gives you

- **`[Compose]`** — every theory parameter is composed. See
  [Your First Composed Theory](../getting-started/first-test.md).
- **`[Compose<TProfile>]`** — same, with a specific
  [`ICompositionProfile`](../concepts/profiles.md) applied.
- **Inline + composed mixing** — `[Compose(42, "widget")]` binds inline
  values left-to-right; anything left over is composed. See
  [How Do I Write a Composed Theory?](../how-to/write-a-composed-theory.md).
- **`[Shared]`** — reuse one composed instance across every parameter (or
  nested dependency) in the same row that requests the same type. See
  [Shared Values](../concepts/shared-values.md).
- **`Compose(Seed = ...)`** — reproduce a specific composed row exactly;
  every row is also tagged with a `Compono.Seed` trait, and a failure's
  message includes the seed that produced it. See
  [Determinism and Seeding](../concepts/determinism-and-seeding.md).

## What it deliberately doesn't do

- **No stacking distinct Compose-family attributes on one method.** A test
  needing several inline rows *plus* composed parameters in each — the
  AutoFixture idiom of stacking multiple `[InlineAutoData(...)]` instances
  — has no direct equivalent here. Two different Compose-family attribute
  types (e.g. `[Compose]` and `[Compose<ProfileA>]`) compile without
  complaint, but `BindingPlan.ValidateSignature` throws a
  `CompositionException` at data-binding time once it sees more than one
  applied to the same method. Only the exact same closed attribute type
  twice is a compiler error (`AllowMultiple = false`). If you need this
  shape, pick one Compose-family attribute per method and supply the
  varying rows another way (e.g. inline `[Theory]`/`[InlineData]` rows for
  the parts that need no composition, per
  [Migrating from AutoFixture](../migrating-from-autofixture.md)).
- **No fixture object.** There's nothing analogous to AutoFixture's
  `IFixture` — configuration lives in a profile
  (`[Compose<TProfile>]`), applied per test method, not a shared mutable
  object.

## Next

- [Share a Value Across a Test](../how-to/share-a-value-across-a-test.md)
- [Use Profiles](../how-to/use-profiles.md)
- [Migrating from AutoFixture](../migrating-from-autofixture.md) — the
  full `AutoDataAttribute`/`InlineAutoDataAttribute` mapping, from a real
  migration.
