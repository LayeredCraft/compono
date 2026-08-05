# How Do I Customize One Member?

Use a member rule when you need to override one specific property/field of
a composed type, without changing how every other composed value of that
member's type is produced.

## Fixed value

```csharp
var composer = Composer.Create(builder => builder
    .For<Customer>().Member(x => x.Email).Use("known@example.com"));

var customer = composer.Create<Customer>();
customer.Email.Should().Be("known@example.com");
```

## Computed value

```csharp
builder.For<Order>().Member(x => x.PlacedAt).Use(context => context.Resolve<IClock>().UtcNow);
```

The `ICompositionContext` overload lets the member's value depend on
another composed dependency, resolved through the same composer.

## Member rule vs. type rule

A member rule always wins over a type-wide rule for the same underlying
type:

```csharp
var composer = Composer.Create(builder => builder
    .For<string>().Use("from-type-rule")
    .For<Customer>().Member(x => x.Email).Use("from-member-rule"));

var customer = composer.Create<Customer>();
customer.Email.Should().Be("from-member-rule"); // not "from-type-rule"
```

Reach for the type rule (`For<string>().Use(...)`) when you want the
override to apply everywhere a `string` is composed; reach for the member
rule when only one specific member of one specific type should be
affected.

## Overriding collection size for one member

```csharp
builder.For<Order>().Member(x => x.LineItems).WithCollectionSize(2);
```

See [Collections](../concepts/collections.md) for the global vs.
per-member distinction.

## Common mistakes

- Writing `.Member(x => x.Email.Length)` — the expression must be a
  *direct* property/field access. Anything more elaborate throws
  `ArgumentException` immediately at the `.Member(...)` call, not later.
- Reaching for a member rule when a type rule (`For<T>().Use(...)`,
  without `.Member(...)`) is what you actually want — a member rule only
  applies inside the specific parent type it was declared against.

## Next

- The broader configuration model this belongs to →
  [Registrations and Rules](../concepts/registrations-and-rules.md).
- Register a whole type instead of one member →
  [Register a Type](register-a-type.md).
- Realistic values instead of fixed ones → [`Compono.Bogus` Package Guide](../packages/compono-bogus.md).
