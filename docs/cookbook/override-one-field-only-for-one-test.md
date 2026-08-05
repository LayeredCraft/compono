---
title: Override One Field Only for One Test
description: Fix one composed member's value for a single test without changing every other composed value of that type.
packages: [Compono]
concepts: [registrations-and-rules]
---

# Override One Field Only for One Test

## Problem

A test needs one specific member of a composed type to hold a known
value (e.g. asserting on it directly, or making a test's intent obvious),
but every other member — and every other composed value of that member's
own type elsewhere in the graph — should stay ordinarily composed.

## Solution

```csharp
public sealed record Customer(string Name, string Email);
```

```csharp
[Fact]
public void ComposesACustomerWithAKnownEmail()
{
    var composer = Composer.Create(builder => builder
        .For<Customer>().Member(x => x.Email).Use("known@example.com"));

    var customer = composer.Create<Customer>();

    customer.Email.Should().Be("known@example.com");
    customer.Name.Should().NotBeNullOrWhiteSpace(); // still ordinarily composed
}
```

`For<Customer>().Member(x => x.Email).Use(...)` is scoped to this one
`Composer` — nothing outside this test is affected, and no other member of
`Customer` (or any other composed `string`) changes.

## Discussion

Reach for `.For<T>().Use(...)` (without `.Member(...)`) instead when the
override should apply to *every* composed value of a type, not just one
member of one parent type — a member rule only applies inside the specific
parent type it was declared against, by design.

Need the same override reused across many tests, not just one? Move it
into a [profile](../concepts/profiles.md) instead of repeating it per
`Composer.Create(...)` call.

## See also

- [Customize a Member](../how-to/customize-a-member.md) — the full member-
  vs-type-rule mechanics, including computed values and member-vs-type
  precedence.
- [Registrations and Rules](../concepts/registrations-and-rules.md) — where
  member rules sit among Compono's other configuration options.
