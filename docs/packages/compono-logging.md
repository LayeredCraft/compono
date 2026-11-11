# Compono.Logging

First-class `Microsoft.Extensions.Logging` testing support. `ILogger`/
`ILogger<T>` compose naturally through `UseLogging()`, backed by a
hand-written `CapturingLogger`/`CapturingLogger<T>` pair (real scope
tracking, thread-safe capture, reflection-free structured-property
extraction) and Compono-native `Verify()` verification reusing core
`Compono`'s `CallVerifier` unmodified. A test-owned `LogCollector`
registered with `AddComponoCapture()` captures a real Generic Host's
*host-wide* output — every category, not just loggers Compono composed —
so a test can assert something like "nothing at `Error` or above was
logged anywhere in the host." See
[ADR-0055](../adr/0055-compono-logging-testing-support-package.md) (and
its amendments) for the full decision record and
[RESEARCH-0013](../research/0013-compono-logging-testing-design-research.md)
for the design investigation this package's shape came from.

## When to install

Your code under test takes an `ILogger`/`ILogger<T>` constructor
dependency and the test wants to assert *what* was logged — level,
message, structured properties, exception, scope — not substitute an
unrelated application interface:

```bash
dotnet add package Compono
dotnet add package Compono.Logging
```

This package depends only on `Compono` and
`Microsoft.Extensions.Logging.Abstractions`; it does not add or require
`Microsoft.Extensions.Logging` (the concrete implementation package),
`Microsoft.Extensions.DependencyInjection`, or
`Microsoft.Extensions.Diagnostics.Testing`.

**Generation is on by default.** Installing `Compono.Logging` alone —
just the `PackageReference`, nothing else — is enough for `ILogger<T>`
composition to work. There is no separate MSBuild opt-in to remember, no
`<ComponoGeneratedLogging>true</ComponoGeneratedLogging>` to add. See
"How this actually runs," below, for what "on by default" means
mechanically and how to opt back out.

## What it gives you

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

[Theory]
[Compose<OrderServiceProfile>]
public void RetriesLogAWarning(ILogger<OrderService> logger, OrderService service)
{
    service.PlaceOrder(...);

    logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once();
}
```

```csharp
var entries = logger.GetCapturedEntries();
var failure = entries.Single(e => e.LogLevel == LogLevel.Error);
Console.WriteLine(failure.Properties?.First().Key);   // "OrderId", e.g.
```

- **`UseLogging(Action<LoggingOptions>? configure = null)`** — registers a
  stage-6 test-double provider so any `ILogger`/closed `ILogger<T>`
  request composes as a `CapturingLogger`/`CapturingLogger<T>`.
  `LoggingOptions.MinimumLevel` (default `LogLevel.Trace`) is the only
  setting.
- **`logger.GetCapturedEntries()` / `GetLastCapturedEntry()` /
  `ClearCapturedEntries()`** — direct inspection, no assertion framework
  required.
- **`logger.Verify()`** — the fluent entry point:
  `.AtLevel(level)`, `.AtLevelOrAbove(level)`, `.InCategory(category)`,
  `.WithEventId(id)`, `.WithException<TException>()`,
  `.WithMessageContaining(text)`, `.Matching(predicate)`, ending in
  `.Once()` / `.Never()` / `.Exactly(n)` / `.AtLeast(n)` / `.AtMost(n)`
  ([ADR-0044 Amendment 22](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-22-2026-09-06-callverifieratleastintatmostint-added-requirement-3s-minimality-preserved-not-reversed))
  — the exact same single-verb vocabulary `Compono.TestDoubles`/
  `Compono.Http` already use (`repository.Verify().Save().Once()`,
  `registration.Verify().Once()`), each a thin, one-line forward through
  the same shared count-verification semantics core `Compono`'s
  `CallVerifier` implements — the filter chain above narrows *which*
  captured entries count, then the terminal counts them exactly the same
  way regardless of which package it's reached through.
- **`CapturingLogger` / `CapturingLogger<T>`** — hand-written, publicly,
  directly constructible with no composition involved at all:
  ```csharp
  var logger = new CapturingLogger<OrderService>();
  var logger2 = new CapturingLogger<OrderService>(new LoggingOptions { MinimumLevel = LogLevel.Warning });
  ```
  A provider-composed logger and a directly-constructed one behave
  identically — one implementation, not two.

## Host-wide capture with LogCollector and AddComponoCapture

Composed loggers only see what Compono composed. A real Generic Host (or
`WebApplicationFactory`, or anything else with an
`Microsoft.Extensions.Logging.ILoggingBuilder`) also logs through
categories your test never composed — framework lifetime messages,
libraries, your own `ILogger<T>` resolved from `host.Services`. Register a
test-owned `LogCollector` to capture all of it:

```csharp
using Compono.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var logs = new LogCollector();
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();          // optional: drop console/debug noise
builder.Logging.AddComponoCapture(logs);
using var host = builder.Build();

// ... start the host, call the code under test ...

logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();

// One logger's category - the full name of its ILogger<T> type, matched exactly:
logs.Verify()
    .AtLevelOrAbove(LogLevel.Error)
    .InCategory("MyApp.Payments.PaymentGateway")
    .Never();
```

`AddComponoCapture` returns the same builder, so it chains; it also works
from `services.AddLogging(logging => logging.AddComponoCapture(logs))` and
`WebApplicationFactory.ConfigureLogging`. It is additive: it never changes
the host's filter rules.

```csharp
logs.GetCapturedEntries();
logs.GetLastCapturedEntry();
logs.ClearCapturedEntries();
logs.Verify().AtLevelOrAbove(LogLevel.Warning).AtLeast(1);
```

### `Category`

Every `CapturedLogEntry` carries `Category`:

- Host-captured entries: the exact string passed to
  `ILoggerProvider.CreateLogger(category)`.
- Composed/directly-constructed `CapturingLogger<T>`: the same string
  `LoggerFactory.CreateLogger<T>()` produces (full name, `.`-separated
  nesting, generic arity and arguments removed, C# keyword names for
  built-ins).
- Non-generic `CapturingLogger` and a bare composed `ILogger`: `""`.

`InCategory` is exact, ordinal, case-sensitive: `InCategory("MyApp.Payments")`
does **not** match an entry whose category is
`MyApp.Payments.PaymentGateway`. This differs from the host's own filter
rules — `AddFilter("MyApp.Payments", ...)` and `Logging:LogLevel:MyApp.Payments`
configuration match every category that *starts with* that prefix. Combined
with `Never()`, a category string that matches nothing passes without
checking anything, so use the full category name, or assert by prefix
explicitly:

```csharp
logs.Verify()
    .AtLevelOrAbove(LogLevel.Error)
    .Matching(entry => entry.Category.StartsWith("MyApp.Payments.", StringComparison.Ordinal))
    .Never();
```

> **Behavioral compatibility note.** `Category` is part of
> `CapturedLogEntry`'s record value equality: two entries identical in
> every other field but produced by different categories are **not** equal
> under `Equals`, `==`, or `!=`. This is an intentional behavioral change
> from earlier previews (not a source or binary breaking change), decided
> in [ADR-0071](../adr/0071-compono-logging-host-wide-log-capture.md).

### Filters

Two filters exist for the host-wide scenario and behave identically on a
composed `logger.Verify()` and a `LogCollector.Verify()`:

- `.AtLevelOrAbove(level)` — `Error` matches `Error` and `Critical`.
  `AtLevelOrAbove(LogLevel.None)` matches nothing.
- `.InCategory(category)` — exact ordinal match; `null` throws
  `ArgumentNullException`.

Both contribute to the failure text, e.g. `Expected exactly 0 call(s) to a
log entry matching level Error or above, but received 1.` The message
names the filters and the match count only — it does not list the matching
entries. Read them with `GetCapturedEntries()` when diagnosing a failure.

### Host filtering still applies

The host's own `LoggerFilterOptions` decides what reaches the collector;
`AddComponoCapture` does not override it. Under a default host (minimum
`Information`), `Debug`/`Trace` are not captured. Lower the host's own
level if you need them:

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Trace);
// or a category rule (prefix match: covers MyApp.Payments.*):
builder.Logging.AddFilter("MyApp.Payments", LogLevel.Debug);
```

The provider is registered under the `Compono` alias, so a rule that
targets only the Compono provider works on every supported target
framework (net8.0 through net11.0):

```json
{ "Logging": { "Compono": { "LogLevel": { "Default": "Warning" } } } }
```

`LogLevel.None` is never captured on any target framework.

### Host vs. composed: separate destinations

In this release the two capture destinations are deliberately separate:

- Host loggers (anything created by the host's `ILoggerFactory`, including
  a container-resolved `ILogger<T>`) write only to the registered
  `LogCollector`.
- Loggers composed by `UseLogging()` keep their own per-logger capture
  store, exactly as before.

`GetCapturedEntries()`/`Verify()` on a host `ILogger<T>` still throw
`InvalidOperationException` (it is not a Compono capturing logger); the
message now points at `LogCollector`/`AddComponoCapture`. Read and verify
through the collector instead. Opt-in forwarding is deferred.

### Ownership, lifetime, and cardinality

- The test owns the `LogCollector`. The host owns only its provider. The
  collector is never disposed, cleared, or detached by the host and stays
  readable after the host is disposed.
- Disposing the host stops new capture through its provider. Held loggers
  become disabled (`IsEnabled` is `false`, `Log` is a no-op, `BeginScope`
  still returns a usable disposable); entries captured before disposal
  remain, and `ClearCapturedEntries()` still works.
- Registering the **same** collector twice on one builder is idempotent:
  one provider, one copy of each entry. Duplicate detection follows the
  provider registrations themselves, so after `ClearProviders()` removes
  the provider, registering the same collector again adds it back. Different collectors on one builder
  each get their own provider and both receive every entry. The same
  collector across several hosts receives entries from all of them;
  disposing one host stops only that host.
- One private lock per collector guards appends, clears, and reads; reads
  return snapshots. There is no static/ambient state, so parallel tests with
  their own hosts and collectors do not interfere.
- Scopes come from the host's shared scope provider, not per logger: a
  scope opened with `BeginScope` on one host logger also appears in the
  `Scopes` of entries other categories log inside it on the same async
  flow — the standard `Microsoft.Extensions.Logging` behavior. Composed
  loggers keep a per-logger scope stack instead.
- Activity scopes configured through `LoggerFactoryOptions
  .ActivityTrackingOptions` appear in `Scopes` like any other scope.
- The collector keeps every entry until you call `ClearCapturedEntries()`;
  there is no size limit. A collector shared by many tests (for example a
  `WebApplicationFactory` fixture) accumulates entries across them — clear
  it at the start of each test, or give each test its own collector.

### When nothing is captured

Capture relies on the host's `ILoggerFactory` dispatching to registered
`ILoggerProvider`s. A host that **replaces** `ILoggerFactory` with one that
ignores registered providers bypasses the collector entirely — for
example Serilog's `UseSerilog()` without `writeToProviders: true`. Then
`Never()` passes because nothing was captured, not because nothing was
logged. Likewise, a `ClearProviders()` call that runs *after*
`AddComponoCapture` (for example in the application's own startup code)
removes the Compono provider. When in doubt, assert the collector saw a
known entry so an empty collector fails loudly. On a Generic Host with its
default lifetime, the `Microsoft.Hosting.Lifetime` "Application started"
entry after `StartAsync` works; under a `WebApplicationFactory`/ASP.NET
Core `TestHost`, prefer a deterministic entry your own code emits during
the operation under test — `TestHost` runs a different host lifetime, and a
shared fixture may clear startup entries between tests.

## Structured properties and `MessageTemplate`

`CapturedLogEntry` carries both the raw `State` (always present, the
escape hatch) and derived, reflection-free structured fields:

```csharp
public readonly record struct CapturedLogEntry
{
    public string Category { get; }                                         // ADR-0071; part of value equality
    public LogLevel LogLevel { get; }
    public EventId EventId { get; }
    public Exception? Exception { get; }
    public string Message { get; }                                          // pre-formatted, never re-derived
    public object? State { get; }
    public IReadOnlyList<KeyValuePair<string, object?>>? Properties { get; } // non-null only when State is structured
    public string? MessageTemplate { get; }                                  // Properties' "{OriginalFormat}" entry, by name
    public IReadOnlyList<object> Scopes { get; }                             // outermost-to-innermost
    public DateTimeOffset Timestamp { get; }
}
```

`Properties`/`MessageTemplate` extraction is a single, reflection-free
pattern match (`state is IReadOnlyList<KeyValuePair<string, object>>`),
confirmed to cover **both** an ordinary `logger.LogInformation("...", args)`
call and every `[LoggerMessage]` source-generated call identically — no
special-casing either style. A structured value that's legitimately
`null` is preserved as `null`, not stringified or dropped — this is why
`Properties`' value type is `object?`, not the BCL's own non-nullable
`object`: a Compono consumer gets the more truthful contract.

## Scope semantics

Real scope tracking via `Microsoft.Extensions.Logging.LoggerExternalScopeProvider`
— the same BCL mechanism a real logging provider (e.g. the console
logger) delegates to, not a custom stack and not a no-op:

```csharp
using (logger.BeginScope("Processing {OrderId}", orderId))
{
    logger.LogInformation("...");   // this entry's Scopes includes the scope above
}
```

`CapturedLogEntry.Scopes` is outermost-to-innermost, a snapshot fixed at
the moment of the log call — pushing or disposing a scope afterward never
retroactively changes an already-captured entry. Scopes flow correctly
across `await` (`AsyncLocal<>`-backed).

## `MinimumLevel` — real filtering, not just an `IsEnabled()` opinion

```csharp
var logger = new CapturingLogger<OrderService>(new LoggingOptions { MinimumLevel = LogLevel.Warning });
logger.LogInformation("filtered out - never appears in GetCapturedEntries()");
logger.LogWarning("captured");
```

An entry below `MinimumLevel` is never captured — it doesn't appear in
`GetCapturedEntries()`, is never returned by `GetLastCapturedEntry()`, and
never counts toward `Verify()`. `LogLevel.None` is never an enabled or
capturable level, regardless of `MinimumLevel`; setting
`MinimumLevel = LogLevel.None` disables capture entirely.

## Four distinct failure conditions — don't confuse them

Each throws for a genuinely different reason, at a genuinely different
time:

1. **`logger.GetCapturedEntries()`/`Verify()`/etc. on a non-Compono.Logging
   `ILogger`** (`InvalidOperationException`, at the call site, at runtime)
   — the `ILogger` you're calling this on isn't a `CapturingLogger`/
   `CapturingLogger<T>` at all (e.g. `NullLogger<T>.Instance`, an
   NSubstitute substitute, a `Compono.TestDoubles`-generated double — the
   latter only possible when `ComponoGeneratedLogging` is explicitly
   disabled, since it's otherwise excluded from generation, Amendment 4).
   Most common cause: `UseNSubstitute()` was registered *before*
   `UseLogging()` — see "Registration order," below. A container-resolved
   `ILogger<T>` from a real host hits this too: it is the host's logger,
   not a Compono one — capture it with `LogCollector`/`AddComponoCapture`
   instead.
2. **`LoggingProvider` recognized a closed `ILogger<T>` request but found
   no generated activation for it** (`InvalidOperationException`, at
   composition time, at runtime) — the category type isn't reachable from
   a real Compono composition root for the generator to discover. See
   "How this actually runs," below.
3. **`CMP0038`** (compile-time, `Info`) — `ComponoGeneratedLogging` is
   enabled but `Compono.Logging`'s own runtime types couldn't be
   resolved. Only happens if the property is forced `true` by hand
   without actually referencing `Compono.Logging` — an ordinary consumer
   who just installs the package never sees this.
4. **`CMP0039`** (compile-time, `Info`) — a closed `ILogger<T>`
   category type is private/protected and can't be named by the
   generated top-level activation. Composing `ILogger<T>` for it still
   compiles; only the generated activation is withheld, falling back to
   condition 2 above if actually requested at runtime.

## Registration order (stage-6 precedence)

`UseLogging()` registers into the same pipeline stage
`UseNSubstitute()`/`UseGeneratedTestDoubles()` do — stage 6, test-double
providers — and Compono's existing, `Accepted` first-registered-wins rule
(ADR-0024/ADR-0043) applies unchanged. Since ADR-0055 Amendment 4, this
splits into two cases depending on which other provider is involved:

- **`UseGeneratedTestDoubles()`** — when `ComponoGeneratedLogging` is
  enabled, `Compono.TestDoubles` never generates a double for
  `ILogger`/`ILogger<T>` at all (Amendment 4: `Compono.Logging` owns those
  types). `GeneratedTestDoubleProvider` therefore has no factory to offer
  for them regardless of order — registration order between
  `UseLogging()` and `UseGeneratedTestDoubles()` is **not observable** for
  `ILogger`/`ILogger<T>`.
- **`UseNSubstitute()`** — unaffected by Amendment 4; it can independently
  substitute `ILogger`/`ILogger<T>` without a generated factory, so
  registration order still decides the outcome here:

  ```csharp
  builder.UseLogging().UseNSubstitute();   // ILogger<T> -> CapturingLogger<T>
  builder.UseNSubstitute().UseLogging();   // ILogger<T> -> an NSubstitute substitute instead
  ```

  Register `UseLogging()` **before** `UseNSubstitute()` if you want
  `ILogger<T>` to resolve to a capturing logger. The reverse order is an
  explicit, documented consequence of registration order — not a bug, not
  diagnosed against.

`Compono.Logging` introduces no new precedence mechanism and no
provider-priority system; Amendment 4 only removes `GeneratedTestDoubleProvider`'s
ability to handle `ILogger`/`ILogger<T>` at all, it does not change how
stage-6 itself resolves ties.

## How this actually runs (generator ownership, gating, and opt-out)

`Compono.Logging` ships **no generator or analyzer DLL of its own**.
Closed `ILogger<T>` activation is generated by the existing, shared
`Compono.Generators` — the same assembly, already embedded in
`Compono.nupkg`, that generates ordinary composition plans and
`Compono.TestDoubles`' doubles. `CapturingLogger`/`CapturingLogger<T>`
themselves are entirely hand-written; the generator's only job is
activation glue — closing the generic `CapturingLogger<T>` for each
category type actually reachable from a real composition root
(`Composer.Create<T>()`/`CreateMany<T>()`, `[Composable]`, or a
`[Compose]`/`[Compose<TProfile>]` theory-row parameter), never a
compilation-wide scan.

This is gated by one MSBuild property, `ComponoGeneratedLogging` —
**defaulted to `true` by `Compono.Logging`'s own packed
`build`/`buildTransitive` props asset**, so installing the package is
enough. Opt out explicitly if you ever need to:

```xml
<PropertyGroup>
  <ComponoGeneratedLogging>false</ComponoGeneratedLogging>
</PropertyGroup>
```

An explicit setting — in your own `.csproj` or `Directory.Build.props` —
always wins over the package default. `Compono.TestDoubles`' own
`ComponoGeneratedTestDoubles` remains pure opt-in (unaffected by this
package) — a deliberate, separately-tracked pre-1.0 consistency question,
not something `Compono.Logging` changes.

`LoggingFactoryRegistry` is the `Type`-keyed registry the generated
activation calls into — it's real, public API (required, for the same
cross-assembly reason `Compono.TestDoubles`' own
`GeneratedTestDoubleRegistry` is public: generated code lives in *your*
assembly and needs to call something), but it's generator
infrastructure, not something you call directly. Normal usage is
`UseLogging()`, `CapturingLogger`/`CapturingLogger<T>`, direct
inspection, and `Verify()` — nothing else.

**Known limitation**: activation can only be generated for a category
type reachable through an ordinary constructor parameter or `[Compose]`
parameter. A category reached only through a hand-written
`Register<T>(...)` factory's own internal `context.Resolve<ILogger<T>>()`
call falls outside this — a documented, intentional gap
(ADR-0052's still-open "Finding B"), not a bug.

## `Share<T>()` and `[Shared]`

`Compono.Logging`'s own composed `ILogger<T>` participates in
[`Share<T>()`](../adr/0056-composition-builder-share-graph-wide-sharing.md)
like any other composed type — no logging-specific mechanism. Prefer
`Share<T>()`, declared once in a profile, over `[Shared]` on every theory
that needs to observe a captured logger: an ordinary, undecorated
`ILogger<OrderService> logger` parameter (as in the example above) then
receives the exact same instance `OrderService` itself resolves — no
`[Shared]` attribute anywhere. `[Shared]` still works unchanged for a
one-off case that doesn't warrant a profile; the two mechanisms aren't
mutually exclusive, but `Share<T>()` is the graph-wide, reusable answer to
the "needing a parameter purely to observe a composed instance" friction
this section used to describe as unsolved.

## v1 non-goals

Deliberately not in this package — see ADR-0055's Decision Outcome for
the rationale behind each:

- Composing/replacing a host's `ILoggerFactory`. (A host *provider* that
  captures into a test-owned `LogCollector` is supported — see "Host-wide
  capture," above.)
- Serilog-specific or other logging-provider-specific behavior.
- Test-runner output capture/routing (a different problem —
  `TUnit.Logging.Microsoft`'s concern, not behavior verification).
- DI integration.
- Cross-scope structured-property flattening/searching.
- `FakeLogger`-style per-level `ControlLevel` toggling — a single
  `MinimumLevel` threshold covers every real evidenced case.
- A category-string constructor for the non-generic `CapturingLogger`.
- Dependency on `Microsoft.Extensions.Diagnostics.Testing`,
  `Compono.TestDoubles`, or `Compono.NSubstitute`.

## Next

- [Basic Usage sample](../samples/basic-usage.md) (`LoggingTests`) — a
  concise `UseLogging()`/`Verify()` scenario in this repo's single clearest
  reference project.
- [`Compono.XunitV3`](compono-xunitv3.md)/[`Compono.TUnit`](compono-tunit.md)
  — `[Compose]`/`[Shared]` used throughout the examples above.
- [`Compono.NSubstitute`](compono-nsubstitute.md)/[`Compono.TestDoubles`](compono-testdoubles.md)
  — for any other ordinary interface dependency; mind the registration-order
  note above if a composed type depends on both an `ILogger<T>` and another
  interface.
- [ADR-0055](../adr/0055-compono-logging-testing-support-package.md) —
  the full decision record, including its amendments.
