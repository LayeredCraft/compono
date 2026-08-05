# Compono.Bogus

Realistic fake data — [Bogus](https://github.com/bchavez/Bogus)-backed
values for member names that look like real-world data
(`Name`, `Email`, `PhoneNumber`, and similar), plus explicit `Faker<T>`
sugar for whole-object generation.

## When to install

You want composed string/data members to look like plausible real values
(`"Kaladin Stormblessed"`, `"kaladin@example.com"`) instead of anonymous
placeholder strings:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.Bogus --prerelease
```

If anonymous placeholder values are fine for your tests, you don't need
this package — core `Compono`'s built-in value provider already produces
values for every primitive without it.

## What it gives you

```csharp
var composer = Composer.Create(builder => builder.UseBogus());

var customer = composer.Create<Customer>();
// customer.Email looks like a real email address, not an anonymous string
```

- **`UseBogus()`** — enables member-name-convention matching. A member
  named `FirstName`, `LastName`, `FullName`, `Email`, `PhoneNumber`,
  `StreetAddress`, `City`, `State`, `PostalCode`, or `CompanyName` (exact
  match, case-sensitive) composes to a realistic Bogus-generated value
  instead of an anonymous one. This is a conservative, fixed allowlist —
  not a general NLP/fuzzy match — deliberately, so a member's generated
  value is predictable from its name.
- **`BogusOptions.AddAlias`/`AddConvention`** — extend the allowlist with
  your own member names, mapped to one of the built-in generators.
- **`UseBogus<T>(Action<Faker<T>> configureFaker)`** — whole-object sugar:
  builds a `Faker<T>` seeded from the current composition's own
  deterministic seed (`context.DeriveSeed()`) before invoking your
  callback, so every `RuleFor` inside it is already seed-consistent with
  the rest of the composition. See
  [Determinism and Seeding](../concepts/determinism-and-seeding.md).

## What it deliberately doesn't do

**No per-type name disambiguation.** Member-name matching is purely by
name, regardless of the requesting type — a `CharacterItem.Name` (a
person's name) and a `WorldItem.Name` (a place name) sharing the literal
member name `Name` can't get different generators from a single
package-wide `AddAlias`/`AddConvention` call. If two types need the same
member name to mean different things, use `UseBogus<T>(Action<Faker<T>>)`
for at least one of them instead of relying on the shared convention
table. See
[Migrating from AutoFixture](../migrating-from-autofixture.md) for the
real case this surfaced during dogfooding.

**No DTO/API-response-type opinion.** `Compono.Bogus` composes whatever
type you ask for — it has no special handling for production
mapping/DTO types versus domain types; the convention table applies
identically either way.

## Next

- [Determinism and Seeding](../concepts/determinism-and-seeding.md) —
  why `UseBogus<T>()`'s generated values are reproducible.
- [Registrations and Rules](../concepts/registrations-and-rules.md) — how
  member rules and semantic providers like this one fit together.
