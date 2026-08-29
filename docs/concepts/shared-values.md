# Shared Values

## The problem this solves

By default, each composed parameter is independent — two parameters of the
same type in the same test get two separate composed instances, even
though they look identical. Usually that's fine. Sometimes it isn't: a test
that composes both a repository *and* a service that internally depends on
"the same" repository needs to assert against the actual instance the
service used, not a look-alike.

## `Share<T>()`

`CompositionBuilder.Share<T>()` is core `Compono`'s own graph-wide sharing
declaration — see [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md)
for the full decision record. Declared once, typically in a profile, it
makes *every* request for `T` anywhere in one root composition graph — an
ordinary constructor parameter, a nested dependency several levels deep, a
`[Compose]` theory parameter — resolve to the same instance, with zero
`[Shared]` attribute required anywhere:

```csharp
public sealed class OrderProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Share<Repository>();
}

[Theory]
[Compose<OrderProfile>]
public void ServiceUsesTheSharedRepository(Repository repository, OrderService service)
{
    service.Repository.Should().BeSameAs(repository);
}
```

`repository` above is an ordinary, undecorated parameter — nothing marks
it as special. `Share<T>()` is lazy (the shared instance is created on
first request, not eagerly at configuration time) and idempotent (calling
it more than once for the same type, directly or via more than one
profile, changes nothing). It composes with `Register<T>()` in either
order — a registered value plus `Share<T>()` shares the registered
instance instead of a freshly generated one. Its lifetime boundary is one
root composition graph: one `Composer.Create<T>()` call, one item of a
`CreateMany<T>(count)` batch, or one `CompositionRow` — never shared
across independent `Create<T>()` calls or across `CreateMany` items.
Disposal is out of scope for `Share<T>()`, same as everywhere else in
`Compono` today.

## `[Shared]`

`Compono.XunitV3`'s and `Compono.TUnit`'s own `[Shared]` attribute marks a
single `[Compose]`-attributed parameter whose value is reused by
type for every other composed parameter (or nested dependency) in that
same test row that structurally requests the same type — a row-scoped,
per-test opt-in, as opposed to `Share<T>()`'s graph-wide, configured-once
declaration:

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

## When to reach for which

Reach for `Share<T>()` — declared once in a profile — when a type should
be shared everywhere it's requested across a whole suite of tests, or
whenever the only reason a test declares a parameter is to observe or
configure an instance it would otherwise have no way to reach; that's
exactly the friction an ordinary `Share<T>()`-configured parameter removes.

Reach for `[Shared]` for a one-off, single-test case that doesn't warrant
a profile change — most commonly a substitute (`Compono.NSubstitute`) you
want to both compose into a dependent service and set expectations on
directly, just for this one test:

```csharp
[Theory]
[Compose<NSubstituteTestProfile>]
public async Task SavesTheOrder([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
{
    await handler.Handle(command);
    await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

The two aren't mutually exclusive — a profile-level `Share<T>()` and a
test-local `[Shared]` for a different type can coexist in the same test.
Don't reach for either when two composed values of the same type are
supposed to be independent — that's the default, and it's correct far
more often than not (two composed `Customer`s in the same test usually
should be two different customers).

## Scope and limits

`Share<T>()` is a core `Compono` concept: it works under a plain
`Composer.Create<T>()`/`CreateMany<T>()` call with no test framework
involved at all, as well as under `CompositionRow`
(`Composer.CreateRow`/`Compono.XunitV3`'s and `Compono.TUnit`'s
`[Compose]` row binding).

`[Shared]` remains scoped to a `Compono.XunitV3`- or `Compono.TUnit`-owned
`[Compose]` row specifically — sharing is type-keyed, not name-keyed, every
parameter/nested dependency requesting exactly that type in the row shares
the value regardless of what it's called, and a method can't declare two
`[Shared]` parameters of the same type (there'd be no way to tell which one
"the" shared value is). The two packages' `[Shared]` attributes are
distinct types with identical binding rules (declaration order,
duplicate-type rejection, row-scoped visibility), duplicated rather than
shared across packages — see ADR-0040's "Row-binding logic: duplicated,
not extracted" section.

## Next

- The full `Share<T>()` decision record →
  [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md).
- Where sharing fits among `Compono.XunitV3`'s other attributes →
  [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md).
- Where sharing fits among `Compono.TUnit`'s own attributes →
  [`Compono.TUnit` Package Guide](../packages/compono-tunit.md).
- Apply it to a real test → [Share a Value Across a Test](../how-to/share-a-value-across-a-test.md).
- The independent-by-default composition each shared value overrides →
  [The Composition Model](composition-model.md).
