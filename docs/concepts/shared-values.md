# Shared Values

## The problem this solves

By default, each composed parameter is independent — two parameters of the
same type in the same test get two separate composed instances, even
though they look identical. Usually that's fine. Sometimes it isn't: a test
that composes both a repository *and* a service that internally depends on
"the same" repository needs to assert against the actual instance the
service used, not a look-alike.

## `[Shared]`

`Compono.XunitV3`'s and `Compono.TUnit`'s own `[Shared]` attribute each
mark a `[Compose]`-attributed parameter whose value is reused by name-of-type
for every other composed parameter (or nested dependency) in that same test
row that structurally requests the same type:

```csharp
[Theory]
[Compose]
public void ServiceUsesTheSharedRepository([Shared] Repository repository, OrderService service)
{
    service.Repository.Should().BeSameAs(repository);
}
```

Without `[Shared]`, `repository` and the `Repository` inside `service`
would be two different composed instances. With it, every other
composition of `Repository` in this row reuses the exact instance bound to
the `[Shared]` parameter.

`[Shared]` parameters resolve first, in declaration order, before any
non-shared parameter composes — so a later parameter that structurally
needs a `Repository` always finds the shared one already available, never
a race against composition order.

## When to reach for it

Reach for `[Shared]` when a test needs to assert against, or configure, the
*same instance* a composed dependency received — most commonly a
substitute (`Compono.NSubstitute`) you want to both compose into a
dependent service and set expectations on directly:

```csharp
[Theory]
[Compose<NSubstituteTestProfile>]
public async Task SavesTheOrder([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
{
    await handler.Handle(command);
    await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

Don't reach for it when two composed values of the same type are supposed
to be independent — that's the default, and it's correct far more often
than not (two composed `Customer`s in the same test usually should be two
different customers).

## Scope and limits

Sharing is type-keyed, not name-keyed — every parameter/nested dependency
requesting exactly that type in the row shares the value, regardless of
what it's called. A method can't declare two `[Shared]` parameters of the
same type (there'd be no way to tell which one "the" shared value is), and
`[Shared]` only applies within a `Compono.XunitV3`- or `Compono.TUnit`-
owned `[Compose]` row — it's not a core-`Compono` concept, since a plain
`Composer.Create<T>()` call has no notion of "this test's row" to scope a
shared value to. The two packages' `[Shared]` attributes are distinct types
with identical binding rules (declaration order, duplicate-type rejection,
row-scoped visibility), duplicated rather than shared across packages — see
ADR-0040's "Row-binding logic: duplicated, not extracted" section.

## Next

- Where sharing fits among `Compono.XunitV3`'s other attributes →
  [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md).
- Where sharing fits among `Compono.TUnit`'s own attributes →
  [`Compono.TUnit` Package Guide](../packages/compono-tunit.md).
- Apply it to a real test → [Share a Value Across a Test](../how-to/share-a-value-across-a-test.md).
- The independent-by-default composition each shared value overrides →
  [The Composition Model](composition-model.md).
