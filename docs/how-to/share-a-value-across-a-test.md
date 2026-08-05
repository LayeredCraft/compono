# How Do I Share a Value Across a Test?

Use `[Shared]` when a composed parameter and a value used *inside* another
composed parameter need to be the exact same instance, not two
independently-composed look-alikes.

## Basic case

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
instance instead of composing a new one.

## Sharing a substitute so you can assert on it

The most common real use — compose a substitute, hand it to a dependent
service, and assert against it directly:

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
  same row reuses the value, regardless of name.
- Declaring two `[Shared]` parameters of the same type on one method —
  there's no way to tell which is "the" shared instance, so this is a
  signature error.
- Reaching for `[Shared]` on a core (non-`Compono.XunitV3`) `Composer` —
  `[Shared]` is scoped to a `Compono.XunitV3` `[Compose]` row; programmatic
  composition doesn't have this concept.

## Next

- Why sharing isn't the default → [Shared Values](../concepts/shared-values.md).
- Composing a substitute in the first place →
  [`Compono.NSubstitute` Package Guide](../packages/compono-nsubstitute.md).
- Narrower recipes → [Cookbook](../cookbook/index.md).
