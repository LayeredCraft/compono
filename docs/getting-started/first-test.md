# Your First Composed Theory

This walks through one real composed xUnit v3 theory, line by line, using
`Compono` and `Compono.XunitV3` (see [Installation](installation.md) if you
haven't added them yet).

## The types under test

```csharp
public sealed class Repository;

public sealed class OrderService(Repository repository)
{
    public Repository Repository => repository;
}

public sealed record CreateOrder(string ProductName, int Quantity);
```

Nothing here is Compono-specific — `OrderService` just takes a `Repository`
constructor dependency, and `CreateOrder` is a plain record. Compono composes
plain types; it doesn't require attributes, base classes, or interfaces on
the types it builds.

## The test

```csharp
using Compono.XunitV3;

public sealed class OrderServiceTests
{
    [Theory]
    [Compose]
    public void ServiceUsesTheComposedRepository(Repository repository, OrderService service, CreateOrder command)
    {
        service.Repository.Should().BeSameAs(repository);
        command.Should().NotBeNull();
    }
}
```

Line by line:

- `[Theory]` — a normal xUnit v3 theory attribute. Compono doesn't replace
  xUnit's test discovery, it supplies the theory's *data*.
- `[Compose]` — every theory parameter that isn't covered by an inline value
  gets composed. There are no inline values here, so all three parameters
  are composed.
- `Repository repository` — `Repository` has a parameterless constructor, so
  Compono constructs one directly.
- `OrderService service` — `OrderService`'s constructor needs a `Repository`.
  Compono resolves it the same way it resolved the `repository` parameter
  above — but as separate, independently-composed parameters, `repository`
  and the `Repository` inside `service` are two *different* instances by
  default (see [Shared Values](../concepts/shared-values.md) for how to make
  them the same one).
- `CreateOrder command` — a record with two constructor parameters
  (`string`, `int`); Compono composes both and constructs the record.
- The test body just asserts on the composed values — nothing about the
  arrange step differs from a hand-written `new OrderService(new Repository())`
  call, other than who wrote the `new` calls.

## Run it, then break it

Change the assertion to something that can't pass —
`service.Repository.Should().NotBeSameAs(repository)` — and run the test.
Compono doesn't produce a bare `NullReferenceException` or an unreadable
reflection stack trace on a composition failure; a failed *composition*
(not a failed assertion, which is ordinary xUnit output) reports a
tree-rendered path to the problem and a `Seed: <value>` you can paste back
into `[Compose(Seed = <value>)]` to reproduce the exact same composed values
again. See [Determinism and Seeding](../concepts/determinism-and-seeding.md)
for the full mechanics — this is why "deterministic by design" is one of
Compono's core goals, not an afterthought.

## Next

- Want the mental model behind what just happened? →
  [Concepts](../concepts/index.md), starting with
  [The Composition Model](../concepts/composition-model.md).
- Have a specific task in mind already (customize one member, register a
  type, share a value)? → [How-to Guides](../how-to/index.md).
- Want a curated path instead of picking pages yourself? →
  [Learning Paths](learning-paths.md).
