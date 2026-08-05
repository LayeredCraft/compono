# How Do I Create an Object?

## Without any configuration

```csharp
var composer = Composer.Create();
var customer = composer.Create<Customer>();
```

`Composer.Create()` needs no configuration for plain types — Compono's
source generator produces the construction plan for `Customer` from its
constructor/required members directly.

## Several independent objects at once

```csharp
var customers = composer.CreateMany<Customer>(3);
```

See [Collections](../concepts/collections.md) for how this differs from a
collection-typed *member* of a composed object.

## With one-off configuration

```csharp
var composer = Composer.Create(builder => builder
    .For<string>().Use("acme-corp")
    .Register<IClock>(_ => new FakeClock()));

var customer = composer.Create<Customer>();
```

Reach for [Register a Type](register-a-type.md) or
[Customize a Member](customize-a-member.md) once you need to control a
specific dependency rather than every composed value of a type.

## From an xUnit v3 theory instead

If you're inside a `[Theory]` method, you don't call `Composer.Create`
yourself at all — `Compono.XunitV3`'s `[Compose]` attribute does it for
you, per parameter:

```csharp
[Theory]
[Compose]
public void UsesTheComposedCustomer(Customer customer) { }
```

See [Write a Composed Theory](write-a-composed-theory.md) for the full
picture, including mixing composed and inline values.

## Common mistakes

- Calling `Composer.Create` inside the test method itself, per-assertion —
  a `Composer` is meant to be created once (per test, or once and reused
  via a fixture/profile), not rebuilt for every `Create<T>()` call.
- Expecting two separately-composed parameters of the same type to be the
  same instance — they aren't, by default. See
  [Shared Values](../concepts/shared-values.md) if you need that.

## Next

- The mental model behind what just happened →
  [The Composition Model](../concepts/composition-model.md).
- A composed xUnit theory instead of programmatic creation →
  [Write a Composed Theory](write-a-composed-theory.md).
- Narrow recipes → [Cookbook](../cookbook/index.md).
