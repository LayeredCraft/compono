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
```

Or, when the registered value itself needs a composed dependency:

```csharp
builder.Register<IClock>(context => new FakeClock(context.Resolve<DateTimeOffset>()));
```

(Shown here as two separate examples, not one combined snippet — both
register the same `IClock` type, and a real `Composer.Create(...)` call
only ever takes one `Register<T>` per type; a second one for the same type
is a configuration conflict, not an override.)

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

## Constructor selection: `For<T>().UseConstructor<...>()`

A type with exactly one accessible constructor never needs any of this —
Compono selects it automatically. A type with more than one accessible
constructor and no further configuration is `CMP0001` (ambiguous
construction path) — a compile-time diagnostic, never a guess (see
[ADR-0002](../adr/0002-constructor-selection-algorithm.md)'s
"predictability over magic" reasoning). `UseConstructor<...>()` resolves
that ambiguity by naming the constructor's own parameter types, in order:

```csharp
public sealed class Foo
{
    public Foo() { }
    public Foo(IBar bar, IBaz baz) { }
}

builder.For<Foo>().UseConstructor<IBar, IBaz>();
```

Compono still composes `IBar` and `IBaz` itself, exactly as it would for
an unambiguous type's own constructor parameters — recursively, through
the same discovery graph, resolving their own nested dependencies too.
The generated code is ordinary, direct construction:

```csharp
var bar = context.Resolve<IBar>(/* ... */);
var baz = context.Resolve<IBaz>(/* ... */);
return new Foo(bar, baz);
```

No delegate is stored or invoked at runtime — `UseConstructor<...>()`'s
own method body does nothing; the type arguments are read once, at
compile time, by the source generator, which selects the matching
constructor for the single composition plan it generates for `Foo`. If no
accessible constructor's parameter types exactly match what you requested,
that's `CMP0034`, not a silent fallback to a different constructor.

### `Register<T>` vs. `UseConstructor<...>()` — not the same capability

|  | `Register<T>` | `For<T>().UseConstructor<...>()` |
|---|---|---|
| Who builds `T` | You — a real runtime factory you write | Compono — the generated composition plan |
| What you say | "Here's the whole value" | "Use *this* constructor" |
| Use when | You need to call other code, wrap an existing instance, or supply a value Compono could never compose on its own | `T` just has more than one constructor and Compono should keep composing it normally |

Don't reach for `Register<T>` merely to work around `CMP0001` when you
actually want Compono to keep constructing `T` — that's exactly what
`UseConstructor<...>()` is for, and it saves you from re-implementing
`T`'s own composition by hand. Conversely, `UseConstructor<...>()` cannot
express "build `T` from this specific pre-configured value" — that's
`Register<T>`'s job. A real example of the distinction: a `Register<HttpClient>`
that builds `HttpClient` from a specific pre-configured
`HttpMessageHandler` a test fixture already owns is not something
`UseConstructor<...>()` could express — there is no parameter-type list
that produces "the exact handler my fixture already configured."

### Scope: compilation-wide, not per-profile

A generated composition plan is one plan per type, shared by every
composition path that reaches it — not a per-profile variant. A
`UseConstructor<...>()` selection made anywhere in the compilation applies
everywhere `T` is composed, regardless of which profile made the call. A
second, *different* selection for the same `T` anywhere in the
compilation is a compile-time conflict (`CMP0033`); calling the identical
selection more than once is harmless (idempotent, not a conflict).
Per-profile constructor selection — the same type constructed differently
depending on which profile is active — is not currently supported.

## How this fits with everything else in the pipeline

Registrations and rules are two of several stages Compono tries in order
before falling back to generated default construction — a configured
`IServiceProvider` ([`UseServiceProvider`](../reference/api/Compono/Compono.CompositionBuilder.UseServiceProvider(System.IServiceProvider).md)), semantic
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
- Precise API contract → [Reference](../reference/index.md).
