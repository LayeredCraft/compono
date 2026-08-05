# Collections

Compono deals with "more than one" in two distinct senses — don't conflate
them, since they're configured differently and solve different problems.

## Many independent root compositions: `CreateMany<T>()`

`composer.CreateMany<T>(count)` composes `count` fully independent root
values of `T` — the collection-equivalent of calling `Create<T>()`
`count` times, not a single composed `List<T>` member somewhere inside a
graph:

```csharp
var composer = Composer.Create();
var customers = composer.CreateMany<Customer>(3);
```

Each item gets its own forked seed (derived from the composer's own seed
plus the item's index), so the items are independent of each other but the
whole batch is still reproducible from the composer's seed. `count: 0`
returns an empty list, never `null`; a negative count throws
`ArgumentOutOfRangeException` immediately.

## Collection-sized members: `WithCollectionSize`

When a composed type has a collection-typed member (`List<T>`, and similar
generated-collection shapes), Compono needs to decide *how many* elements
to generate for it — that's collection size, configured globally or
per-member:

```csharp
builder.WithCollectionSize(5); // global default (built-in default: 3)

builder.For<Order>().Member(x => x.LineItems).WithCollectionSize(2); // per-member override
```

A member-scoped `WithCollectionSize` always wins over the global default.
Like other configuration, a negative size throws immediately at the call
site, and setting the same global default twice is a configuration
conflict (`CompositionConfigurationException`), not last-write-wins.

## Which one do I want?

- Need several independent test *cases* — e.g. three separate `Customer`s
  to run the same theory logic against? → `CreateMany<T>()`.
- Need a composed *object*'s own collection-typed property/field to have a
  particular number of elements? → `WithCollectionSize`.

## Next

- Apply this to a real task → [Create an Object](../how-to/create-an-object.md).
- The configuration surface `WithCollectionSize` is part of →
  [Registrations and Rules](registrations-and-rules.md).
- Precise API contract → [Public API](../public-api.md).
