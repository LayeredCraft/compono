# Compono.NSubstitute

Automatic substitute composition — interface, delegate, and (optionally)
abstract-class parameters compose to real
[NSubstitute](https://nsubstitute.github.io/) substitutes instead of
failing with "no accessible constructor."

## When to install

Your composed types depend on interfaces (or delegates, or unsealed
abstract classes) that you'd otherwise create with `Substitute.For<T>()` by
hand:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.NSubstitute --prerelease
```

If none of your composed types have interface/delegate/abstract-class
dependencies, you don't need this package.

## What it gives you

```csharp
var composer = Composer.Create(builder => builder.UseNSubstitute());

var handler = composer.Create<CreateOrderHandler>();
// handler's IOrderRepository dependency is a real NSubstitute substitute
```

- **`UseNSubstitute()`** — registers the substitute provider as a pipeline
  stage. Once active, any interface or delegate-typed parameter composes
  to `Substitute.For<T>()` automatically — no per-type registration
  needed.
- **`SubstituteAbstractClasses`** (`NSubstituteOptions`, default `true`) —
  also substitutes unsealed abstract classes, not just interfaces and
  delegates. Set to `false` via `UseNSubstitute(o => o.SubstituteAbstractClasses = false)`
  if you want abstract classes to compose through ordinary constructor
  selection instead.
- **Combine with `[Shared]`** (`Compono.XunitV3`) to assert against, or
  configure, the exact substitute instance a composed dependency received
  — see [Shared Values](../concepts/shared-values.md).

## What it deliberately doesn't do

**No member auto-configuration.** Every substitute is a bare
`Substitute.For<T>()` — Compono never pre-configures a member's return
value (no AutoFixture-style `ConfigureMembers = true` equivalent). An
unstubbed call on a `Task<T>`-returning member returns NSubstitute's own
default (`Task.FromResult<T>(default)`), not a recursively-composed value.
If your code depends on a specific return value, stub it explicitly:

```csharp
repository.GetAsync(Arg.Any<Guid>()).Returns(Task.FromResult(order));
```

This is a deliberate design choice, not a missing feature — an implicit
auto-configured return value hides a test's true dependency on that
value's shape. See
[Migrating from AutoFixture](../migrating-from-autofixture.md) for a real
case where this surfaced a genuine hidden dependency during migration, and
[ADR-0025](../adr/0025-compono-nsubstitute-package-design.md) for the full
rationale.

## Next

- [Shared Values](../concepts/shared-values.md) — asserting against a
  composed substitute.
- [Providers](../concepts/providers.md) — where the substitute provider
  sits in the resolution pipeline.
