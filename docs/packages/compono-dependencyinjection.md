# Compono.DependencyInjection

A configured-resolution `IServiceProvider` bridge over a `CompositionRow` —
`row.AsServiceProvider()` surfaces what Compono has explicitly registered
or can provide (exact registrations, configuration rules,
`Compono.TestDoubles`/`Compono.NSubstitute`-backed values) as a plain,
standard `IServiceProvider`, for any ecosystem that already knows how to
consume one. See
[ADR-0047](../adr/0047-compono-dependencyinjection-configured-resolution-bridge.md)
and [RESEARCH-0007](../research/0007-trivia-manager-bunit-dependency-injection.md)
for the full investigation this package's scope came from.

## When to install

You have a `CompositionRow` and want its composed/configured values
reachable through a plain `IServiceProvider` — most commonly as a
**fallback** source for an ecosystem's own DI container, so you don't have
to enumerate and manually register every dependency a system under test
might ask for:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.DependencyInjection --prerelease
```

This package is deliberately **not** framework-specific. It doesn't
reference bUnit, ASP.NET Core, or any hosting model, and it has no
third-party dependency of its own at all — `row.AsServiceProvider()`
returns a plain `System.IServiceProvider`, BCL, nothing more. The
`GetRequiredService<T>()` calls in the examples below are the standard
`Microsoft.Extensions.DependencyInjection.Abstractions` extension method —
your own app or test host almost certainly already references that
package (ASP.NET Core, a generic host, and bUnit all carry it
transitively); `Compono.DependencyInjection` doesn't need to reference it
itself just to hand back an interface those extensions already work
against.

## What it gives you

```csharp
var row = composer.CreateRow(typeof(QuestionFormTests));
var provider = row.AsServiceProvider();

var apiClient = provider.GetRequiredService<IApiClient>();
apiClient.Configure().GetQuestions().Returns(questions);   // Compono.TestDoubles
```

- **`row.AsServiceProvider()`** — the package's one public entry point, an
  extension method on `CompositionRow`. Returns a plain `IServiceProvider`;
  the adapter type behind it is internal, by design — there's no reason a
  consumer needs to name it.
- **Stable per-`Type` identity** — the first successful `GetService(Type)`
  call for a given type is cached by the returned provider instance; every
  later call for that same type returns the identical object. This is what
  lets a test configure a double once and have something else that
  resolves through the same provider (a rendered UI component, a second
  service that depends on it) observe that exact instance. A miss is
  **not** cached — a type unsatisfiable on one call can still be satisfied
  later if the row's own configuration changes.
- **Concurrent `GetService` calls are safe, but not fixed-seed
  deterministic between different types** — calls through the same
  provider never race or corrupt shared state, and two same-type callers
  never observe different instances. What isn't guaranteed: when two
  *different* types are requested concurrently for the first time, which
  one's underlying resolution runs first is scheduling-dependent, not
  fixed. For a randomness-dependent factory or provider (one that calls
  `ctx.DeriveSeed()` or performs nested composition), this means the
  derived value for a given type can differ across runs on the same fixed
  seed, specifically when two or more types are resolved concurrently for
  the first time. Sequential resolution — the common case — is
  unaffected. See ADR-0047 Amendment 5.
- **Provider-neutral** — works identically whether the underlying value
  came from `Compono.TestDoubles`, `Compono.NSubstitute`, an exact
  registration, or a configuration rule. Nothing in this package inspects
  or cares which one answered.
- **No disposal ownership** — the returned `IServiceProvider` never
  disposes anything it resolves and caches. If a resolved value implements
  `IDisposable`/`IAsyncDisposable`, disposing it is your own
  responsibility, exactly as it would be for a value you constructed by
  hand — `CompositionRow`/`Composer` have no disposal contract of their
  own, and this bridge doesn't add one.

## What it deliberately can't resolve

`row.AsServiceProvider()` is backed by `CompositionRow.TryResolveConfigured`
(core `Compono`), which reaches only:

- this row's existing scope values (already `[Shared]`/`ResolveShared`
  values elsewhere in the same row),
- exact registrations (`builder.Register<T>(...)`),
- configuration rules, semantic providers, and test-double providers
  (`Compono.TestDoubles`, `Compono.NSubstitute`, or a custom
  `ICompositionValueProvider`).

It deliberately does **not** reach:

- a configured `UseServiceProvider(...)` external provider — consulting it
  here could silently flatten a legitimately transient/scoped external
  registration into "cached forever by this adapter," a claim this package
  isn't entitled to make about a value it doesn't own;
- ordinary generated-plan composition of an arbitrary concrete type with no
  registration or provider — that dispatch requires the target type known
  at compile time (`PlanCache<T>`), which a runtime `Type` can't reach
  without reflection, ruled out by
  [ADR-0001](../adr/0001-source-generation-first.md)'s no-reflection
  default.

A `GetService(Type)` call for either of these returns `null` — the same
"nothing could handle it" outcome as any other unregistered type, not an
error. This is a deliberate, permanent scope boundary, not a gap scheduled
to close.

## Worked example: a fallback provider

The motivating use case (see ADR-0047/RESEARCH-0007) is an ecosystem that
already accepts a fallback `IServiceProvider` — its own `Services`/DI
container is tried first, and this provider is only consulted on a miss.
`Compono.DependencyInjection` has no framework-specific glue for this;
wiring it in is just handing the ecosystem a plain `IServiceProvider`:

```csharp
// Illustrative - bUnit's own Services/AddFallbackServiceProvider, not
// anything this package depends on or tests.
var row = composer.CreateRow(typeof(QuestionFormTests));
var provider = row.AsServiceProvider();

var apiClient = provider.GetRequiredService<IApiClient>();
apiClient.Configure().GetQuestions().Returns(questions);

ctx.Services.AddFallbackServiceProvider(provider);
var cut = ctx.Render<QuestionForm>();   // [Inject] IApiClient -> fallback -> same apiClient instance
```

The same pattern works with a generic host, ASP.NET Core, or any other
`IServiceProvider`-consuming ecosystem — there's nothing bUnit-specific in
`Compono.DependencyInjection` itself; bUnit is one worked example, not a
dependency.

## Next

- [ASP.NET API sample](../samples/aspnet-api.md) (`DependencyInjectionTests`)
  — `row.AsServiceProvider()` bridged into a real ASP.NET Core host's own
  `IServiceCollection`.
- [`Compono.TestDoubles`](compono-testdoubles.md) — the primary provider
  demonstrated above.
- [`Compono.NSubstitute`](compono-nsubstitute.md) — an equally supported,
  provider-neutral alternative.
- [Providers](../concepts/providers.md) — where exact registrations,
  configuration rules, and test-double providers sit in the resolution
  pipeline.
