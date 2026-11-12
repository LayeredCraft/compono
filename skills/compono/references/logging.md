# Compono.Logging

Only relevant if the project references `Compono.Logging`. `UseLogging()`
composes `ILogger`/`ILogger<T>` as a hand-written `CapturingLogger`/
`CapturingLogger<T>`, plus `CapturedLogEntry` and the fluent `Verify()`
surface. A test-owned `LogCollector` + `AddComponoCapture()` captures a
real Generic Host's *host-wide* output (every category, including
container-resolved `ILogger<T>` and framework logging).

```csharp
using Compono;
using Compono.Logging;
using Microsoft.Extensions.Logging;

var composer = Composer.Create(builder => builder.UseLogging());
var service = composer.Create<OrderService>();   // OrderService(ILogger<OrderService> logger, ...)
```

```csharp
public sealed class OrderServiceProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.UseLogging().Share<ILogger<OrderService>>();
}

[Theory, Compose<OrderServiceProfile>]
public void RetriesLogAWarning(ILogger<OrderService> logger, OrderService service)
{
    service.PlaceOrder(...);
    logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once();
}
```

**Composition failed naming `ILogger`/`ILogger<T>`?** If you meant
`Compono.Logging` to supply it, `UseLogging()` wasn't registered — see
`docs/troubleshooting/common-errors.md#runtime-composition-failures`.
`Register<ILogger<T>>()`/`UseServiceProvider` is equally valid. Provider
registration order is a separate concern (see below); a catch-all provider
that resolves the dependency prevents this missing-provider failure.

## When to use it

A composed type takes an `ILogger`/`ILogger<T>` constructor dependency
and the test wants to assert *what* was logged — level, message,
structured properties, exception, or scope. Concrete signs: the test
currently hand-rolls a fake `ILogger`, uses `NullLogger<T>.Instance` and
therefore can't assert anything about logging, or manually
`Register<ILogger<TSomething>>(...)`s a logger for one specific type.

Also use it for host-wide assertions against a real Generic Host/
`WebApplicationFactory` — "nothing logged at `Error` or above anywhere,"
framework lifetime messages, a container-resolved `ILogger<T>` — via
`LogCollector` + `AddComponoCapture()` (below).

## When NOT to use it

The test doesn't care about logging behavior at all — a bare
`NullLogger<T>.Instance`/an unconfigured `UseNSubstitute()` substitute is
fine when nothing in the test asserts against what was logged. Don't
introduce `Compono.Logging` just because a constructor happens to take an
`ILogger<T>` — only reach for it when the test actually needs to observe
logging behavior.

## Generation is on by default — do not suggest a manual opt-in

**This is the single most important thing to get right when this package
is referenced.** Unlike `Compono.TestDoubles` (pure opt-in via
`ComponoGeneratedTestDoubles`), `Compono.Logging` enables its generation
behavior automatically the moment the package is referenced — its own
packed `build`/`buildTransitive` MSBuild props asset defaults
`ComponoGeneratedLogging` to `true`. **Never tell a consumer to add
`<ComponoGeneratedLogging>true</ComponoGeneratedLogging>` to make
`UseLogging()` work** — if it isn't working, the cause is virtually never
a missing opt-in (there is none to add); check instead:

- Is `Compono.Logging` actually referenced (not just `Compono`)?
- Is the category type (`T` in `ILogger<T>`) reachable from a real
  composition root — an ordinary constructor parameter of a composed
  type, or a `[Compose]`/`[Compose<TProfile>]` theory-row parameter?
  Discovery is bounded to real roots, never a compilation-wide scan — an
  `ILogger<T>` reachable only through a hand-written `Register<T>(...)`
  factory's own internal `context.Resolve<ILogger<T>>()` call is a known,
  documented gap (ADR-0052 Finding B), not something to work around with
  a manual registration or reflection.
- Was `UseNSubstitute()` registered *before* `UseLogging()`? See
  "Registration order," below — `UseGeneratedTestDoubles()` ordering is
  never the cause, since it has no generated double to offer for
  `ILogger`/`ILogger<T>` while `ComponoGeneratedLogging` is enabled.

The **only** legitimate reason to write `ComponoGeneratedLogging` at all
is an explicit, deliberate opt-out:

```xml
<PropertyGroup>
  <ComponoGeneratedLogging>false</ComponoGeneratedLogging>
</PropertyGroup>
```

Generation happens inside the existing, shared `Compono.Generators`
(already embedded in `Compono.nupkg`) — `Compono.Logging` ships **no**
generator/analyzer DLL of its own. `CapturingLogger`/`CapturingLogger<T>`
are hand-written; the generator's only job is closing the generic
`CapturingLogger<T>` activation for each category type actually
discovered.

## Core usage vocabulary

- `builder.UseLogging(Action<LoggingOptions>? configure = null)` —
  registers the stage-6 provider. `LoggingOptions.MinimumLevel` (default
  `LogLevel.Trace`) is the only setting.
- `logger.GetCapturedEntries()` / `GetLastCapturedEntry()` /
  `ClearCapturedEntries()` — direct inspection, no assertion framework.
- `logger.Verify()` — fluent, one verb (matching
  `Compono.TestDoubles`/`Compono.Http`'s own `Verify()` vocabulary exactly
  — never suggest a two-verb `VerifyLog()...Verify()` shape, that was
  explicitly considered and rejected):
  `.AtLevel(level)`, `.AtLevelOrAbove(level)`, `.InCategory(category)`,
  `.InCategoryPrefix(prefix)`, `.NotInCategoryPrefix(prefix)`,
  `.WithEventId(id)`, `.WithException<TException>()`,
  `.WithMessageContaining(text)`, `.Matching(predicate)`, ending in
  `.Once()` / `.Never()` / `.Exactly(n)` / `.AtLeast(n)` / `.AtMost(n)` —
  each a thin forward through the same shared count-verification semantics
  `Compono.TestDoubles`/`Compono.Http` use, applied to the filtered match
  count, not the whole capture buffer.
- `new CapturingLogger<T>(options?)` / `new CapturingLogger(options?)` —
  direct construction, no composition required, identical behavior to a
  provider-composed instance.

## Host-wide capture — `LogCollector` + `AddComponoCapture`

Composed loggers only see Compono-composed output. For a real Generic Host
(or `WebApplicationFactory.ConfigureLogging`, or
`services.AddLogging(...)`), register a test-owned collector:

```csharp
var logs = new LogCollector();
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddComponoCapture(logs);
using var host = builder.Build();

logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();
// One category: the ILogger<T> type's FULL name, matched exactly.
logs.Verify().AtLevelOrAbove(LogLevel.Error).InCategory("MyApp.Payments.PaymentGateway").Never();
// A namespace: literal ordinal prefix - end it with "." for namespace-boundary intent.
logs.Verify().AtLevelOrAbove(LogLevel.Error).InCategoryPrefix("MyApp.Payments.").Never();
// Host guard: nothing at Error or above OUTSIDE your own code.
logs.Verify().AtLevelOrAbove(LogLevel.Error).NotInCategoryPrefix("MyApp.Jobs.").Never();
```

Key semantics to get right:

- `Category` is on every `CapturedLogEntry`. Host entries carry the exact
  `ILoggerProvider.CreateLogger` string; composed/direct `CapturingLogger<T>`
  matches `LoggerFactory.CreateLogger<T>()`; non-generic `CapturingLogger`
  and a bare composed `ILogger` carry `""`. `InCategory` is exact, ordinal,
  case-sensitive — **not** a prefix match, unlike the host's own
  `AddFilter`/`Logging:LogLevel:*` rules. Never write
  `InCategory("Payments")`/a namespace with `.Never()`: it matches nothing
  and passes without checking anything. Use the full category name, or
  `.InCategoryPrefix(prefix)`/`.NotInCategoryPrefix(prefix)` (ordinal,
  literal - `"MyApp.Jobs"` also matches `MyApp.JobsLegacy`, so recommend a
  trailing `.`). Don't hand-write `.Matching(e => e.Category.StartsWith(...))`
  for a single prefix any more; keep `.Matching` for multi-namespace or
  other custom conditions.
- `Never()`'s failure text names the filters and the match count only
  (`Expected exactly 0 call(s) to a log entry matching level Error or
  above, but received 1.`); it does not list entries — read them with
  `GetCapturedEntries()`.
- **Behavioral change**: `Category` participates in the record's value
  equality. Entries differing only by category are not equal. This is
  intentional (ADR-0071), not a bug to "fix."
- Host and composed capture are **separate destinations** in this release.
  A container-resolved `ILogger<T>` is the host's logger: calling
  `Verify()`/`GetCapturedEntries()` on it still throws
  `InvalidOperationException`, now pointing at `LogCollector`/
  `AddComponoCapture`. Never suggest forwarding host output into a composed
  logger — it doesn't exist yet.
- The host's own filters still apply: default minimum `Information` means
  `Debug`/`Trace` are not captured; lower it with
  `builder.Logging.SetMinimumLevel(LogLevel.Trace)` or `AddFilter`.
  `AddComponoCapture` never changes filter rules. A
  `Logging:Compono:LogLevel:*` provider-alias rule targets only the
  Compono provider, on every supported TFM (net8.0–net11.0; ADR-0071
  Amendment 2 — don't repeat the old "net10+ only" caveat).
- The test owns the collector; the host owns the provider. Disposing the
  host stops capture but never clears the collector — entries stay
  readable and `ClearCapturedEntries()` still works. Registering the same
  collector twice is idempotent (and re-registering after `ClearProviders()`
  adds it back); different collectors each receive every entry; one
  collector across hosts receives all (disposing one host stops only that
  host).
- No size limit: the collector keeps everything until
  `ClearCapturedEntries()`. A `WebApplicationFactory`/class-fixture
  collector is shared across tests — clear it at the start of each test.
- Host scopes come from the host's shared scope provider, so a scope opened
  on one host logger also appears on entries other categories log inside it.
- A vacuous pass is the main risk: if the app replaces `ILoggerFactory`
  (e.g. Serilog `UseSerilog()` without `writeToProviders: true`) or calls
  `ClearProviders()` after the test's registration, nothing is captured
  and `Never()` passes. Suggest a sanity assertion that the collector saw a
  known entry. On a Generic Host with its default lifetime,
  `logs.Verify().InCategory("Microsoft.Hosting.Lifetime").AtLeast(1)` after
  `StartAsync` works; under a `WebApplicationFactory`/ASP.NET Core
  `TestHost`, prefer a deterministic entry the code under test emits
  (don't rely on framework startup logging — `TestHost` runs a different
  host lifetime and a shared fixture may clear startup entries between
  tests).

## Structured properties and `MessageTemplate`

`CapturedLogEntry.Properties` (`IReadOnlyList<KeyValuePair<string, object?>>?`)
and `MessageTemplate` (the `"{OriginalFormat}"` entry, by name) are
derived from `State`, covering **both** an ordinary
`logger.LogInformation("...", args)` call and every `[LoggerMessage]`
source-generated call identically — never claim one style is unsupported
or needs special handling. A `null` structured value is preserved as
`null`, not stringified — that's why the value type is `object?`, not
the BCL's own non-nullable `object`.

## Scope semantics

Real scope tracking via `LoggerExternalScopeProvider` — not a no-op.
`CapturedLogEntry.Scopes` is outermost-to-innermost, a snapshot fixed at
capture time. Never claim scopes are unsupported or that
`BeginScope(...)` is a no-op in this package.

## `MinimumLevel` — real filtering

An entry below `MinimumLevel` is never captured at all — never claim it's
merely an `IsEnabled()` opinion layered over a complete capture stream.
`LogLevel.None` is never an enabled/capturable level regardless of
`MinimumLevel`; `MinimumLevel = LogLevel.None` disables capture entirely.
There is no `FakeLogger`-style per-level `ControlLevel` toggle — a single
threshold is the whole v1 surface, don't invent a richer one.

## Four distinct failure conditions — never conflate them

1. **Non-Compono.Logging `ILogger`** — `logger.Verify()`/etc. called on
   an `ILogger` that isn't a `CapturingLogger`/`CapturingLogger<T>`
   (`NullLogger<T>.Instance`, an NSubstitute substitute, a
   `Compono.TestDoubles` double, or a container-resolved host
   `ILogger<T>`) → `InvalidOperationException` at the call site. Usually
   means registration order is backwards (below), or the logger came from
   a host's `IServiceProvider` — then use `LogCollector`/
   `AddComponoCapture` instead.
2. **Missing generated activation at runtime** — `LoggingProvider`
   recognized a closed `ILogger<T>` request but no generator-discovered
   activation exists for that category → a *different*
   `InvalidOperationException`, thrown at composition time. Means the
   category type isn't reachable from a real composition root.
3. **`CMP0038`** (compile-time, `Info`) — `ComponoGeneratedLogging` is
   enabled but `Compono.Logging`'s own runtime types couldn't be
   resolved. Only happens if the property was forced `true` without the
   package actually referenced — never something an ordinary consumer
   who just installs the package hits.
4. **`CMP0039`** (compile-time, `Info`) — a closed `ILogger<T>` category
   type is private/protected and can't be named by the generated
   top-level activation (mirrors `CMP0012`'s identical collection-element
   accessibility check). Composition still compiles; only that one
   category's activation is withheld, falling back to condition 2 above
   if actually requested at runtime.

Never present these as the same condition, and never suggest a fix for
one when the actual symptom matches another.

## Registration order (stage-6 precedence)

`UseLogging()` shares stage 6 with `UseNSubstitute()`/
`UseGeneratedTestDoubles()` — the existing, `Accepted` first-registered-
wins rule applies unchanged (no new precedence mechanism). What differs is
which providers can actually produce `ILogger`/`ILogger<T>` at all, per
ADR-0055 Amendment 4:

- **`UseGeneratedTestDoubles()`** — when `ComponoGeneratedLogging` is
  enabled, `Compono.TestDoubles` never generates a double for
  `ILogger`/`ILogger<T>` at all (Amendment 4: those types are
  Logging-owned). `GeneratedTestDoubleProvider` therefore has nothing to
  offer them regardless of registration order — order between
  `UseLogging()` and `UseGeneratedTestDoubles()` is **not observable** for
  `ILogger`/`ILogger<T>`.
- **`UseNSubstitute()`** — untouched by Amendment 4; it can independently
  produce an `ILogger`/`ILogger<T>` substitute without any generated
  factory. Registration order still matters here:

  ```csharp
  builder.UseLogging().UseNSubstitute();   // ILogger<T> -> CapturingLogger<T>
  builder.UseNSubstitute().UseLogging();   // ILogger<T> -> an NSubstitute substitute instead
  ```

  If a composed type depends on both an `ILogger<T>` and another interface
  resolved by `UseNSubstitute()`, register `UseLogging()` first. The
  reverse order is an explicit, documented consequence — never diagnose it
  as a bug, and never suggest a priority/specificity mechanism that
  doesn't exist.

## `Share<T>()` and `[Shared]`

A composed `ILogger<T>` participates in
[`Share<T>()`](../../../docs/adr/0056-composition-builder-share-graph-wide-sharing.md)
like any other composed type — no logging-specific mechanism. Prefer
`Share<T>()`, declared once in a profile (as above), over `[Shared]` on
every theory that needs to observe a captured logger — an ordinary,
undecorated `ILogger<OrderService> logger` parameter then gets back the
exact composed instance for assertion, with **no `[Shared]` attribute**.
`[Shared]` still works unchanged for a one-off case that doesn't warrant a
profile. Never suggest `Share<T>()` shares across separate `Create<T>()`
calls or is composer-wide — it's graph-scoped, per ADR-0056.

## `LoggingFactoryRegistry` — generator infrastructure, not usage API

Real, public API (required for the same cross-assembly reason
`Compono.TestDoubles`' `GeneratedTestDoubleRegistry` is public — generated
code lives in the consumer's own assembly). Never present it as part of
normal usage — a consumer's code should never call it directly. If it
shows up in generated-API-reference output, that's expected; it does not
belong in an ordinary composition/verification example.

## v1 boundaries — do not invent capability beyond these

No *composing/replacing* a host's `ILoggerFactory` (a host *provider*
capturing into a `LogCollector` via `AddComponoCapture` **is** supported),
no Serilog/provider-specific behavior, no test-runner output
capture/routing, no DI-integration package beyond `AddComponoCapture`, no
cross-scope structured-property flattening/searching, no category-string
constructor for the non-generic `CapturingLogger`, no dependency on
`Microsoft.Extensions.Diagnostics.Testing`. If asked whether one of these
exists, say plainly that it doesn't rather than improvising a plausible-
looking API.
