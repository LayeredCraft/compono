# Registrations and Rules

This is the configuration surface itself — the verbs you call inside
`Composer.Create(builder => ...)` (or inside a [profile](profiles.md)) to
tell Compono how to satisfy a type, distinct from the *mental model* of
composition itself ([The Composition Model](composition-model.md)).

## Registrations: `Register<T>`

A registration is an exact, type-keyed factory — the strongest, most
specific way to say "whenever anything needs a `T`, build it like this."

```csharp
builder.Register<IClock>(_ => new FakeClock());
builder.Register<IClock>(context => new FakeClock(context.Resolve<DateTimeOffset>()));
```

The `ICompositionContext` overload lets a registration resolve its own
nested dependencies through the same composer, rather than constructing
them by hand — use it when the registered value itself depends on
something else composable. A duplicate `Register<T>` for the same `T`
(including one contributed by a profile) is a configuration conflict,
raised as `CompositionConfigurationException` — not a silent
last-write-wins.

## Type and member rules: `For<T>()`

`For<T>()` is more targeted than a registration in one direction (it can
scope down to a single *member* of `T`, not just `T` as a whole), and
one-off rather than universal in another (a type rule for `string` doesn't
change what a `Register<string>` would, but a *member* rule for
`Customer.Email` only applies inside a composed `Customer`, not to every
composed `string`).

```csharp
builder
    .For<string>().Use("from-type-rule")
    .For<Customer>().Member(x => x.Email).Use("from-member-rule");
```

When both a type rule and a member rule could apply to the same value, the
more specific member rule wins — in the example above, a composed
`Customer.Email` gets `"from-member-rule"`, while every other composed
`string` gets `"from-type-rule"`. `.Member(...)` takes a direct
property/field-access expression (`x => x.Email`); anything more elaborate
(`x => x.Email.Length`) is rejected immediately at the `.Member(...)` call,
not deferred to a build-time failure.

`.Use(...)` accepts either a fixed value or an `ICompositionContext`-aware
factory, the same shape `Register<T>` does:

```csharp
builder.For<Order>().Member(x => x.PlacedAt).Use(context => context.Resolve<IClock>().UtcNow);
```

## How this fits with everything else in the pipeline

Registrations and rules are two of several stages Compono tries in order
before falling back to generated default construction — a configured
`IServiceProvider` ([`UseServiceProvider`](../public-api.md)), semantic
providers, and test-double providers ([Providers](providers.md)) also
participate. Registrations and type/member rules are the ones you reach for
directly, most often; the rest are usually wired in through a package
extension (`UseNSubstitute()`, `UseBogus()`) rather than called by hand.

## Next

- Reuse the same set of registrations/rules across tests →
  [Profiles](profiles.md).
- Keep one instance consistent across several composed parameters →
  [Shared Values](shared-values.md).
- Apply this to a real task → [Register a Type](../how-to/register-a-type.md),
  [Customize a Member](../how-to/customize-a-member.md).
- Precise API contract → [Public API](../public-api.md).
