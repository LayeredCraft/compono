# How Do I Share a Value Across a Test?

Use `Share<T>()` or `[Shared]` when a composed parameter and a value used
*inside* another composed parameter need to be the exact same instance,
not two independently-composed look-alikes. Reach for `Share<T>()` when
the sharing intent is reusable across a suite of tests; reach for
`[Shared]` for a one-off, single-test case. See
[Shared Values](../concepts/shared-values.md) for the full comparison.

## Basic case — `Share<T>()`

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

`repository` is an ordinary, undecorated parameter — no `[Shared]`
attribute anywhere. `Share<T>()` makes every request for `Repository`
anywhere in this graph — this parameter, or a nested dependency several
levels deep — resolve to the same instance. It also works under a plain
`Composer.Create<T>()` call with no test framework involved at all.

## Basic case — `[Shared]`

For a one-off case not worth adding to a profile:

```csharp
[Theory]
[Compose]
public void ServiceUsesTheSharedRepository([Shared] Repository repository, OrderService service)
{
    service.Repository.Should().BeSameAs(repository);
}
```

`[Shared] Repository repository` composes `Repository` first; when
`OrderService`'s constructor needs a `Repository`, it reuses that exact
instance instead of composing a new one. `[Shared]` only exists inside a
`Compono.XunitV3`/`Compono.TUnit` `[Compose]` row — a plain, programmatic
`Composer.Create<T>()` call has no notion of a "row" to scope `[Shared]`
to, but `Share<T>()` above works there too, since it's a core `Compono`
concept, not a row-scoped attribute.

## Sharing a substitute so you can assert on it

The most common real use — compose a substitute, hand it to a dependent
service, and assert against it directly. Either mechanism works; `[Shared]`
shown here for the one-off case:

```csharp
[Theory]
[Compose<NSubstituteTestProfile>]
public async Task Saves_order([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
{
    await handler.Handle(command);
    await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
}
```

Without `[Shared]`, `repository` and the `IOrderRepository` composed inside
`handler` would be two separate substitutes — asserting `Received(1)` on
`repository` would fail even though `handler` genuinely called *a*
repository, just not this one.

## Common mistakes

- Expecting `[Shared]` to match by parameter name — sharing is type-keyed;
  every parameter or nested dependency requesting exactly that type in the
  same row reuses the value, regardless of name. `Share<T>()` is the same:
  type-keyed, not name-keyed.
- Declaring two `[Shared]` parameters of the same type on one method —
  there's no way to tell which is "the" shared instance, so this is a
  signature error.
- Assuming `Share<T>()` shares a value across two separate
  `Composer.Create<T>()` calls, or across `CreateMany<T>()` items — it
  doesn't; its lifetime boundary is one root composition graph.
- Adding `Share<T>()` to a profile several tests already reuse without
  considering the blast radius — it silently changes sharing semantics for
  every graph composed with that profile.

## Next

- Why sharing isn't the default, and the full `Share<T>()`/`[Shared]`
  comparison → [Shared Values](../concepts/shared-values.md).
- Composing a substitute in the first place →
  [`Compono.NSubstitute` Package Guide](../packages/compono-nsubstitute.md).
- Narrower recipes → [Cookbook](../cookbook/index.md).
