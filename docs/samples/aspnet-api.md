# Sample: ASP.NET API

A realistic but tightly scoped ASP.NET Core minimal-API application
demonstrating all four Compono packages working together — enough ASP.NET
structure to host the scenario, not an architecture showcase; Compono
usage stays the dominant content. Real, buildable code:
[`samples/Compono.Samples.AspNetApi`](https://github.com/LayeredCraft/compono/tree/main/samples/Compono.Samples.AspNetApi)
(the app) and
[`samples/Compono.Samples.AspNetApi.Tests`](https://github.com/LayeredCraft/compono/tree/main/samples/Compono.Samples.AspNetApi.Tests)
(the tests).

## What it demonstrates

- A reusable [profile](../concepts/profiles.md) (`ApiTestProfile`) applying
  `UseNSubstitute()` and `UseBogus()` together, exactly like an
  application's own `Program.cs` would.
- A `[Shared]` NSubstitute substitute (`IOrderRepository`) injected into
  the system under test, with explicit setup (`Returns(...)`) layered on
  top of the composed substitute, then verified with `Received(...)` — see
  the [`Compono.NSubstitute` Package Guide](../packages/compono-nsubstitute.md).
- A `Compono.Bogus`-generated `Customer` (`FirstName`/`LastName`/`Email`
  matched by member-name convention) composed alongside the NSubstitute
  substitute with zero interaction between the two packages — see the
  [`Compono.Bogus` Package Guide](../packages/compono-bogus.md).
- Inline plus composed theory values in the same row
  (`[Compose(5)]` fixing one parameter while the rest still compose).
- One integration-style endpoint test: a real ASP.NET Core host
  (`WebApplicationFactory<Program>`), with the composed `[Shared]`
  substitute swapped in for the app's own repository registration — the
  request travels through real minimal-API routing and model binding, not
  a direct method call.
- Deterministic [seed reproduction](../concepts/determinism-and-seeding.md)
  for the composed `Customer` data.

## Packages

`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus`.

## Running it

```bash
git clone https://github.com/LayeredCraft/compono.git
cd compono
dotnet test samples/Compono.Samples.AspNetApi.Tests/Compono.Samples.AspNetApi.Tests.csproj
```

## Next

- Haven't seen the simpler, core-only workflow yet? → [Basic Usage](basic-usage.md).
- Deciding whether to install `Compono.NSubstitute`/`Compono.Bogus` at all? →
  [Package Guides](../packages/index.md).
- One narrow, copy/paste answer instead of a full project? →
  [Cookbook](../cookbook/index.md).
