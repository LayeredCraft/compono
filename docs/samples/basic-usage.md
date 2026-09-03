# Sample: Basic Usage

A minimal, complete project showing Compono's core workflow end to end —
the single clearest reference implementation of ordinary Compono usage,
deliberately not broad. Real, buildable code:
[`samples/Compono.Samples.BasicUsage`](https://github.com/LayeredCraft/compono/tree/main/samples/Compono.Samples.BasicUsage).

## What it demonstrates

- `Composer.Create()`/`Composer.Create<T>()` and `Composer.CreateMany<T>()`
  — the programmatic composition entry points, called directly (see
  [Concepts](../concepts/index.md) for what "composed" means).
- A reusable [profile](../concepts/profiles.md)
  (`SampleApplicationProfile`) combining a type
  [registration](../concepts/registrations-and-rules.md) (`Register<T>`)
  and a [member rule](../how-to/customize-a-member.md)
  (`For<T>().Member(...).Use(...)`).
- `[Shared]` reuse across a composed row's sibling parameters (the same
  pattern [Your First Composed Theory](../getting-started/first-test.md)
  walks through).
- A `[Compose<TProfile>]` xUnit v3 theory applying the reusable profile.
- Both of Compono's [seed-reproduction](../concepts/determinism-and-seeding.md)
  paths — `builder.WithSeed(...)` for programmatic composition, and
  `[Compose(Seed = ...)]` for a composed theory row — kept distinct rather
  than cross-compared, since each derives its own seed independently.
- A `Compono.Logging` scenario (`LoggingTests`): a `[Shared]`
  `ILogger<NotificationService>`, composed via `UseLogging()`, captures what
  `NotificationService` actually logged, asserted with `Verify()` — see the
  [`Compono.Logging` Package Guide](../packages/compono-logging.md).

## Packages

`Compono`, `Compono.XunitV3`, `Compono.Logging`.

## Running it

```bash
git clone https://github.com/LayeredCraft/compono.git
cd compono
dotnet test samples/Compono.Samples.BasicUsage/Compono.Samples.BasicUsage.csproj
```

## Next

- Never seen a composed theory before? Start with
  [Your First Composed Theory](../getting-started/first-test.md) — this
  sample assumes it.
- Want the full ecosystem (test doubles, semantic data, an API host) in
  one sample instead? → [ASP.NET API](aspnet-api.md).
- One narrow, copy/paste answer instead of a full project? →
  [Cookbook](../cookbook/index.md).
