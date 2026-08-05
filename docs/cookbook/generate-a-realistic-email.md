---
title: Generate a Realistic Email
description: Compose an Email-shaped member as a realistic value instead of an anonymous string.
packages: [Compono, Compono.Bogus]
concepts: [providers, determinism-and-seeding]
---

# Generate a Realistic Email

## Problem

A composed `string` member named `Email` (or similar) comes back as an
anonymous string like `"a3f9c1"` — fine for most tests, but unrealistic
where the value is asserted on shape (`Contains("@")`) or shown in a
failure message.

## Solution

```csharp
public sealed class Customer
{
    public required string Email { get; init; }
}
```

```csharp
var composer = Composer.Create(builder => builder.UseBogus());

var customer = composer.Create<Customer>();

customer.Email.Should().Contain("@");
```

`UseBogus()` activates `BogusMemberNameProvider`, which matches a
member's *name* (`Email`, case-insensitively, plus common variants) against
a built-in convention table and generates a real-looking value via
[Bogus](https://github.com/bchavez/Bogus) instead of Compono's default
anonymous string. No attribute, no per-member configuration — this applies
to every `Email`-named member composed once `UseBogus()` is active.

## Discussion

The same seed always produces the same email for the same member — Bogus
generation goes through Compono's own deterministic pipeline, not an
unseeded `Faker` instance, so a failing test's reported seed reproduces the
exact same generated value.

Need a specific value instead of a realistic one? Use a
[member rule](../how-to/customize-a-member.md) — it always wins over a
semantic provider for the same member.

## See also

- [`Compono.Bogus` Package Guide](../packages/compono-bogus.md) — the full
  convention table (`FirstName`, `LastName`, `PhoneNumber`, `StreetAddress`,
  and more) and how to add a custom one.
- [Providers](../concepts/providers.md) — where semantic providers sit in
  the resolution pipeline.
