# The Composition Model

## What "composing" means

Given a root type, Compono constructs its full object graph — the root
type's constructor dependencies, and theirs, and so on — by walking a
resolution pipeline that tries, in order: an explicit
[registration](registrations-and-rules.md) (falling back to a configured
`IServiceProvider` on a miss — the same stage, not a separate one), a
type/member rule, a semantic provider (e.g. `Compono.Bogus`'s member-name
matching), a test-double provider (e.g. `Compono.NSubstitute`), a built-in
provider for primitives/enums/nullable value types and built-in collection
shapes (`List<T>`, arrays, and similar — what actually satisfies a bare
`Create<int>()` or a composed type's generated `List<T>` member when
nothing more specific claimed it), and finally the source-generated default
construction plan for the type itself. "Composing" is this whole walk, not
just constructing one object —
the same term covers producing a `Customer`, the `IEmailSender` substitute
`Customer` depends on, and the `Faker`-generated email string that
substitute's method returns.

Two types stand at the center of this:

- **`Composer`** — the immutable, built-once entry point.
  `Composer.Create()` (no configuration) and
  `Composer.Create(builder => ...)` (explicit configuration) are the same
  method, the latter with an empty callback for the former. Once created, a
  `Composer` is reused across `Create<T>()`/`CreateMany<T>()` calls — it
  doesn't get reconfigured mid-test.
- **`ICompositionContext`** — what a registration factory or a custom
  provider receives to resolve its *own* nested dependencies
  (`context.Resolve<T>()`), rather than constructing them by hand.

```csharp
var composer = Composer.Create(builder => builder
    .UseNSubstitute()
    .UseBogus());

var customer = composer.Create<Customer>();
var customers = composer.CreateMany<Customer>(3);
```

## Configuration is declared once, up front

A `Composer`'s configuration is fixed at `Create` time — there's no method
to mutate an already-built `Composer`'s rules afterward. This is a
deliberate consequence of Compono's immutable-by-design goal (no mutable
global state, per `docs/public-api.md`'s API goals): the composer you get
back from `Composer.Create(...)` behaves the same way on every call, for
every test that reuses it, with nothing to accidentally leak between tests
via shared mutable state.

Configuration conflicts (two `WithSeed` calls, two `Register<T>` for the
same type, and similar) aren't resolved last-write-wins — they're collected
across the whole configuration callback and raised once, together, as a
`CompositionConfigurationException` when `Create` returns. A configuration
mistake surfaces immediately and completely, not as a silently-overwritten
rule discovered later from a confusing composed value.

## Composition is generated, not reflected

The construction code a `Composer` runs for a given type is produced by
Compono's source generator at compile time, from your configuration and the
type's own shape (constructor, required members). There's no
`Activator.CreateInstance`/reflection-based fallback in the default path —
what actually executes in your test is ordinary, debuggable C#, which is
also why a composition failure can report a precise, tree-rendered path to
the exact member that couldn't be satisfied (see
[Determinism and Seeding](determinism-and-seeding.md)) instead of a generic
reflection exception.

## Next

- See the configuration surface itself → [Registrations and Rules](registrations-and-rules.md).
- Reuse one instance across several composed parameters →
  [Shared Values](shared-values.md).
- Group configuration into a reusable unit → [Profiles](profiles.md).
- The deeper "how" behind the resolution pipeline →
  [Architecture](../architecture.md).
