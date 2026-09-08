# Compono.Options

Only relevant if the project references `Compono.Options`. Two public
types: `TestOptionsSource<T>` (the source of truth) and
`UnconfiguredNamedOptionException`, plus the `UseOptions<T>()` builder
extension.

```csharp
var source = new TestOptionsSource<SkillServiceConfiguration>(
    new SkillServiceConfiguration { ApiKey = "test-key" });

var composer = Composer.Create(builder => builder.UseOptions(source));

var service = composer.Create<SkillService>();
```

## When to recommend it

The composed type depends on `IOptions<T>`, `IOptionsSnapshot<T>`, or
`IOptionsMonitor<T>` for a settings type and `Compono.Options` is
referenced. Recommend it over a hand-rolled `IOptionsMonitor<T>` fake —
the community's standard answer (Ben Foster's widely-cited
`TestOptionsMonitor<T>`) has real, documented bugs: `Get(name)` ignoring
`name` entirely, only one `OnChange` subscriber ever honored, a no-op
`IDisposable`. Recommend it over two/three separately hand-wired
registrations for the same settings type (`Register<SkillServiceConfiguration>`
+ `Register<IOptions<SkillServiceConfiguration>>(...)` written
independently) — nothing enforces those stay consistent; `UseOptions<T>`
does, structurally, from one source.

## `IConfiguration` vs. `Compono.Options` — the routing distinction

These are two different, independently-composable things. Never conflate
them or invent a `Compono.Configuration` package:

- The composed type depends on plain `IConfiguration` directly
  (`GetSection`, `GetValue<T>`, configuration binding) → ordinary
  `ConfigurationBuilder`/`AddInMemoryCollection`/`Register<IConfiguration>`
  composition. No dedicated Compono package exists or is needed for this —
  point at the Configuration Cookbook recipes
  (`docs/cookbook/compose-configuration-from-an-in-memory-collection.md`
  and its neighbors) rather than inventing one.
- The composed type depends on `IOptions<T>`/`IOptionsSnapshot<T>`/
  `IOptionsMonitor<T>` for a strongly-typed settings class →
  `Compono.Options`.

The two compose independently and their values need not agree with each
other — a test can configure `IConfiguration` and a `TestOptionsSource<T>`
with completely unrelated values; `Compono.Options` never reads real
`IConfiguration` at all.

## Core usage vocabulary

- **`TestOptionsSource<T>`** — construct directly per settings type. The
  constructor's `initialValue` establishes the default (`Options.DefaultName`)
  value immediately — no separate setup call is needed before resolving
  `IOptions<T>`/`CurrentValue`. Implements `IOptionsMonitor<T>` directly.
- **`CompositionBuilder.UseOptions<T>(source)`** — the one wiring call.
  Reads identically inline or inside `ICompositionProfile.Configure`.
- **`.Change(value)`** / **`.Change(name, value)`** — the one mutation
  surface. Establishes the value (first time or an update) then
  synchronously fires `OnChange` subscribers for that name. There is no
  separate "add"/"configure" API — never suggest one.
- **`OnChange(Action<T, string?> listener)`** returns a real,
  independently-disposable `IDisposable` per subscription.

## Identity model — what a consumer observes

- **`IOptions<T>`** — one frozen view, shared for the whole composition
  graph. Resolving it twice in the same graph returns the same instance;
  it never reflects a later `.Change(...)`.
- **`IOptionsMonitor<T>`** — `source` itself (stable identity, one per
  graph). `CurrentValue`/`Get(name)` always reflect `source`'s current
  state; `OnChange` subscribes to live updates.
- **`IOptionsSnapshot<T>`** — a **fresh** frozen view captured from
  `source`'s *current* state on **every** resolution — not shared. One
  resolved snapshot instance stays frozen even if `source` changes
  afterward; a *new* resolution after a change sees the new state. Never
  claim two `IOptionsSnapshot<T>` resolutions in the same graph return the
  same instance — they deliberately don't.

This mirrors real `Microsoft.Extensions.Options`: `IOptions<T>` and
`IOptionsSnapshot<T>` are the exact same behavior in real Microsoft code
(`OptionsManager<T>` implements both) — the only real difference is how
many instances exist, controlled there by DI registration lifetime
(Singleton vs. Scoped) and here by Compono's own `Share<T>()` vs. plain
`Register<T>()`.

## Named options

`IOptionsMonitor<T>.Get(name)` and `IOptionsSnapshot<T>.Get(name)` accept a
name; **`IOptions<T>` has no named-lookup surface at all** — only `Value`.
Never claim or write code implying a consumer holding only `IOptions<T>`
can request a named value. Name comparison is case-sensitive (ordinal).

## Unconfigured named options — intentional, not a bug

Unlike real `IOptionsFactory<T>.Create(name)` (which silently returns
`new TOptions()` for an unmatched name), `Compono.Options` throws
`UnconfiguredNamedOptionException` — naming the settings type and the
requested name — for a name never established via `.Change(name, value)`.
This is deliberate, the same explicit-configuration-over-silent-default
tradeoff `Compono.TestDoubles` already ships
(`references/testdoubles.md`'s configuration-required-members behavior).
When a user asks "why did my test throw `UnconfiguredNamedOptionException`,"
the answer is: that name was never given a value via `.Change(name, value)`
on the `TestOptionsSource<T>` — fix by calling `.Change(name, value)` for
that name before resolving it, not by treating the exception as a bug.

## Disposal

`TestOptionsSource<T>` does **not** implement `IDisposable`/
`IAsyncDisposable`. Never suggest disposing a `TestOptionsSource<T>` or
adding a `Dispose()`/"clear all subscribers" method — it owns no
disposable resource. The `IDisposable` `OnChange(...)` returns is the only
disposal surface, and it unregisters exactly one subscription.

## ADR-0052 Finding B — this package doesn't solve it

There is **no automatic, no-registration composition** for an arbitrary
settings type `T`. A consumer always constructs `TestOptionsSource<T>`
explicitly and calls `UseOptions<T>()` — the value comes from the test,
never from Compono resolving `T` itself. Never suggest "just declare an
`IOptions<T>`/`IOptionsMonitor<T>` constructor dependency with no
registration and Compono will figure it out" — that's exactly the Finding
B gap (`references/composition-model.md`/`references/diagnostics.md`)
this package sidesteps by design, not solves.

## What this package doesn't do

No real `IConfiguration`/change-token/file-watcher simulation, no
`IOptionsFactory<T>` pipeline, no DI-scope/container simulation, no
`Compono.Configuration` package, no `CallVerifier`-based verification of
`Change`/`OnChange` call counts. Never invent any of these.
