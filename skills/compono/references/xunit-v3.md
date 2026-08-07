# Compono.XunitV3

Only relevant if the project references `Compono.XunitV3`. Requires real
xUnit v3 (`xunit.v3` + Microsoft Testing Platform runner) — not xUnit v2.
Depends on `Compono` (the source generator flows through transitively).

## `[Compose]`

```csharp
[Theory]
[Compose]
public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

[Theory]
[Compose(42, "widget")]           // inline binds positionally left-to-right
public void InlineValuesAreUsedDirectly(int quantity, string productName) { }

[Theory]
[Compose(42)]                     // quantity inline, productName composed
public void MixesInlineAndComposedValues(int quantity, string productName) { }

[Theory]
[Compose(Seed = 4219)]
public void ReproducesTheSameComposedValues(Order order) { }
```

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — see `registrations-profiles-and-scopes.md`.
- Every row carries a `Compono.Seed` xUnit trait unconditionally, pass or
  fail — check it in test output before asking for a re-run.
- Composition happens at execution time, not discovery time — there's no
  separate "composed values shown in the test explorer" pass.

## `[Compose<TProfile>]`

```csharp
[Theory]
[Compose<OrderTestProfile>]
public void Creates_service(
    [Shared] IOrderRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

Same behavior as `[Compose]`, but applies `TProfile.Configure` to the
row's builder first — this is how a theory picks up
`UseNSubstitute()`/`UseBogus()`/registrations for that specific test.

## Hard constraint: one Compose-family attribute per method

`[Compose]` and `[Compose<TProfile>]` are both `DataAttribute` subclasses.
Two **different** Compose-family attributes on one method (e.g.
`[Compose]` + `[Compose<ProfileA>]`) *compile* but throw
`CompositionException` at data-binding time, not compile time — the
signature is only validated once xUnit actually asks the attribute for
its row data. The identical attribute type twice on one method **is** a
compiler error (`AllowMultiple=false`).

**There is no equivalent of stacking multiple `[InlineAutoData(...)]`
rows on one method.** If a test needs several independent inline+composed
combinations, split into separate `[Theory]`/`[InlineData]` methods —
don't try to layer multiple Compose-family attributes to get that effect.

## No fixture object

There's nothing like AutoFixture's `IFixture` to hold onto across a test
class. Configuration is per-test via `[Compose<TProfile>]`; don't invent
a shared fixture-holder pattern to route around this.

## Real examples in this repo

- `test/Compono.XunitV3.SampleTests/SharedTests.cs` — `[Shared] Repository
  repository, OrderService service, CreateOrder command`.
- `test/Compono.XunitV3.SampleTests/NSubstituteTests.cs` —
  `[Compose<NSubstituteTestProfile>] async Task Saves_order([Shared]
  IOrderRepository repository, CreateOrderHandler handler, PlaceOrder
  command)`.
- `test/Compono.XunitV3.SampleTests/BogusTests.cs` — a profile combining
  `UseBogus().UseNSubstitute()`, composing a `Customer` with `required
  string FirstName/LastName/Email` matched via Bogus conventions.
- `test/Compono.XunitV3.SampleTests/FailingCompositionTests.cs` — a
  deliberately failing `[Compose(Seed = 24601)]` test, useful as a
  reference for what the real `dotnet test` failure output looks like.
