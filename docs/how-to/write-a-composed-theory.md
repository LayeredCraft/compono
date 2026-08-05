# How Do I Write a Composed Theory?

This builds on [Your First Composed Theory](../getting-started/first-test.md)
with the variations you'll actually run into once your tests get past the
"every parameter is composed" basic case.

## Every parameter composed

```csharp
[Theory]
[Compose]
public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }
```

## Mixing inline and composed values

Positional inline values bind left-to-right, from the first parameter;
anything left over is composed:

```csharp
[Theory]
[Compose(42, "widget")]
public void InlineValuesAreUsedDirectly(int quantity, string productName)
{
    quantity.Should().Be(42);
    productName.Should().Be("widget");
}

[Theory]
[Compose(42)]
public void MixesInlineAndComposedValues(int quantity, string productName)
{
    quantity.Should().Be(42);      // inline
    // productName is composed
}
```

Use this when a test cares about one specific input (a boundary value, an
invalid quantity) but the rest of the parameters are incidental to what
you're testing.

## Applying a profile

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void UsesTheProfileConfiguredValue(NotificationSettings settings) { }
```

See [Use Profiles](use-profiles.md) for when to reach for this instead of
plain `[Compose]`.

## Reproducing a specific failure

```csharp
[Theory]
[Compose(Seed = 24601)]
public void ReproducesTheSameComposedValues(Order order) { }
```

Every composed row is tagged with a `Compono.Seed` trait regardless of
pass/fail, and a composition failure's message always includes the seed
that produced it — copy it into `Seed = ...` to get the exact same
composed values again. See
[Determinism and Seeding](../concepts/determinism-and-seeding.md).

## Sharing a value across parameters

```csharp
[Theory]
[Compose]
public void ServiceUsesTheSharedRepository([Shared] Repository repository, OrderService service) { }
```

See [Share a Value Across a Test](share-a-value-across-a-test.md) for the
full guide.

## Common mistakes

- Expecting `[Compose(42, "widget")]`'s inline values to bind by parameter
  name — they bind positionally, left to right.
- Combining `[Compose]` with a hand-rolled `[InlineData]`/`[MemberData]` on
  the same method — pick one data-generation attribute per theory method.

## Next

- The attribute's full contract → [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md).
- Narrow, copy/paste variants → [Cookbook](../cookbook/index.md).
- Precise API contract → [Reference](../reference/index.md).
