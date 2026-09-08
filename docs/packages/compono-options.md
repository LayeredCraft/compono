# Compono.Options

First-class `Microsoft.Extensions.Options` testing support —
`TestOptionsSource<T>`, a hand-written, reflection-free source of truth
that coherently backs `IOptions<T>`/`IOptionsSnapshot<T>`/
`IOptionsMonitor<T>` for one settings type from one test-configured
instance, wired via a single `UseOptions<T>()` composition call. See
[ADR-0061](../adr/0061-compono-options-testing-support.md) for the full
decision record and
[RESEARCH-0028](../research/0028-compono-options-configuration-admission-research.md)
for the admission investigation this package's shape came from.

## When to install

Your code under test depends on `IOptions<T>`, `IOptionsSnapshot<T>`, or
`IOptionsMonitor<T>` for a settings type, and the test wants one coherent,
correct source of truth for all of them — not three separately hand-wired
registrations that can silently drift inconsistent, and not a hand-rolled
`IOptionsMonitor<T>` fake (the community's own standard answer for this is
[demonstrably buggy](https://benfoster.io/blog/20200610-testing-ioptionsmonitor/):
named options silently broken, only one `OnChange` subscriber ever
honored, a no-op `IDisposable`):

```bash
dotnet add package Compono
dotnet add package Compono.Options
```

If your code under test only reads plain `IConfiguration` (no `Options`
interfaces at all), this package isn't what you want — see the
[Configuration Cookbook](../cookbook/compose-configuration-from-an-in-memory-collection.md) instead. There is
no `Compono.Configuration` package; ordinary `ConfigurationBuilder`
composition already covers that case with no dedicated package needed.

## What it gives you

```csharp
using Compono.Options;

var source = new TestOptionsSource<EmailServiceConfiguration>(
    new EmailServiceConfiguration { ApiKey = "test-key" });

var composer = Composer.Create(builder => builder.UseOptions(source));

var service = composer.Create<SkillService>();
```

- **`TestOptionsSource<T>`** — the one type you construct directly per
  settings type. Its constructor establishes the default value
  immediately — there's no separate setup call before your test can
  resolve `IOptions<T>`/`IOptionsMonitor<T>.CurrentValue`. It implements
  `IOptionsMonitor<T>` itself.
- **`CompositionBuilder.UseOptions<T>(source)`** — the one wiring call.
  Coherently satisfies all three Microsoft interfaces from `source`:
  - **`IOptions<T>`** — a fresh, frozen view captured once and shared for
    the whole composition graph. Never changes after that first
    resolution, matching real `IOptions<T>`'s actual "computed once"
    contract.
  - **`IOptionsMonitor<T>`** — `source` itself. `CurrentValue`/`Get(name)`
    always reflect `source`'s current state; `OnChange(...)` subscribes to
    live updates.
  - **`IOptionsSnapshot<T>`** — a fresh frozen view captured from
    `source`'s *current* state on every resolution. One resolved instance
    stays stable even if `source` changes afterward; a *later* resolution
    sees whatever's current at that later moment.
- **`.Change(value)`/`.Change(name, value)`** — the one mutation surface on
  `source`. Updates the stored value, then synchronously notifies every
  current `OnChange` subscriber — the new value is visible from inside the
  callback itself. Also how a named value is *first* established; there's
  no separate "add" API.
- **`OnChange(Action<T, string?> listener)`** returns a real, per-subscription
  `IDisposable` — disposing it actually unsubscribes (`-=` against the
  exact delegate), unlike the community fake's no-op. No per-subscriber
  exception isolation, matching real `OptionsMonitor<T>` exactly — a
  throwing subscriber blocks subsequent ones in that same invocation.

## Named options

```csharp
source.Change("secondary", new EmailServiceConfiguration { ApiKey = "secondary-key" });

monitor.Get("secondary");     // EmailServiceConfiguration { ApiKey = "secondary-key" }
monitor.Get("never-set");     // throws UnconfiguredNamedOptionException
```

Name comparison is **case-sensitive** (ordinal), matching the real
contract. `IOptions<T>` has no named-lookup surface at all — only
`IOptionsMonitor<T>.Get(name)` and `IOptionsSnapshot<T>.Get(name)` accept a
name.

## Unconfigured named options — an intentional divergence

Real `IOptionsFactory<T>.Create(name)` silently returns `new TOptions()`
for a name with no matching configuration — no exception, no signal
anything was unconfigured. `Compono.Options` diverges deliberately:
`Get(name)` for a name never established via `.Change(name, value)` throws
`UnconfiguredNamedOptionException`, naming the settings type and the
requested name. This is the same explicit-configuration-over-silent-default
tradeoff `Compono.TestDoubles` already made for configuration-required
members ([ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)) —
a test failure that names exactly what's missing, rather than a silently
wrong default settings object reaching your code under test.

## Inline and profile usage

`UseOptions<T>` is an ordinary `CompositionBuilder` call — it reads
identically inline or inside an `ICompositionProfile.Configure`:

```csharp
public sealed class EmailServiceTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.UseOptions(new TestOptionsSource<EmailServiceConfiguration>(
            new EmailServiceConfiguration { ApiKey = "test-key" }));
}
```

A real, before/after worked example from `alexa-vox-craft`'s
`MediatRTestProfile.cs`, this package's dogfooding validation target
(ADR-0061's "Dogfooding validation" section):

**Before** (the real shape `alexa-vox-craft`'s `MediatRTestProfile.cs` had
before adopting this package — two separately-maintained registrations for
one settings type, nothing structurally enforcing they agree):

```csharp
builder
    .Register<SkillServiceConfiguration>(_ => new SkillServiceConfiguration
    {
        SkillId = "amzn1.ask.skill.default-test-id",
        CustomUserAgent = "TestAgent/1.0",
    })
    .Register<IOptions<SkillServiceConfiguration>>(context =>
        Options.Create(context.Resolve<SkillServiceConfiguration>()));
```

**After** — one shared instance, one `UseOptions<T>` wiring call for the
`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` side; the plain
`SkillServiceConfiguration` registration stays (a separate consumer in this
same project needs the bare type, not an Options interface — `UseOptions<T>`
only wires the three Options interfaces, by design), now sourced from the
identical instance rather than a second independently-constructed one:

```csharp
var defaultSkillServiceConfiguration = new SkillServiceConfiguration
{
    SkillId = "amzn1.ask.skill.default-test-id",
    CustomUserAgent = "TestAgent/1.0",
};

builder
    .Register<SkillServiceConfiguration>(() => defaultSkillServiceConfiguration)
    .UseOptions(new TestOptionsSource<SkillServiceConfiguration>(defaultSkillServiceConfiguration));
```

Validated against the real `alexa-vox-craft` repository (dogfooding, in an
isolated worktree/branch, never committed against the real repo): the full
solution — 2564 tests across every project, not just `AlexaVoxCraft.MediatR.Tests`
— builds and passes against `Compono.Options` packed from this working
tree, restored as an ordinary `PackageReference` (no `ProjectReference`
bypass).

## Disposal

`TestOptionsSource<T>` does **not** implement `IDisposable`/
`IAsyncDisposable` — it owns no disposable resource (no file watcher, no
real `IChangeToken`, no DI scope). The `IDisposable` returned by
`OnChange(...)` is the only thing a test disposes, and only when it wants
to unregister that specific subscription.

## What this package doesn't do

- No real `IConfiguration`/change-token/file-watcher simulation, no
  `IOptionsFactory<T>` pipeline, no DI-scope/container simulation.
- No automatic, no-registration composition for an arbitrary settings
  type `T` — a consumer always constructs `TestOptionsSource<T>` and calls
  `UseOptions<T>` explicitly. A `T` reachable only through a nested
  `context.Resolve<T>()` call inside another factory isn't independently
  discoverable as a composition root
  ([ADR-0052](../adr/0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md)'s
  "Finding B") — this package doesn't solve that; it sidesteps it
  entirely by having the test supply the value directly.
- No `Compono.Configuration` package — see the
  [Configuration Cookbook](../cookbook/compose-configuration-from-an-in-memory-collection.md) for plain
  `IConfiguration` composition.
- No `CallVerifier`-based verification of `Change`/`OnChange` call counts.

## Next

- [Configuration Cookbook](../cookbook/compose-configuration-from-an-in-memory-collection.md) — plain
  `IConfiguration` composition, independent of this package.
- [ADR-0061](../adr/0061-compono-options-testing-support.md) — the full
  decision record.
