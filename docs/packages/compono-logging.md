# Compono.Logging

First-class `Microsoft.Extensions.Logging` testing support — `ILogger`/
`ILogger<T>` compose naturally through `UseLogging()`, backed by a
hand-written `CapturingLogger`/`CapturingLogger<T>` pair (real scope
tracking, thread-safe capture, reflection-free structured-property
extraction) and Compono-native `Verify()` verification reusing core
`Compono`'s `CallVerifier` unmodified. See
[ADR-0055](../adr/0055-compono-logging-testing-support-package.md) (and
its three amendments) for the full decision record and
[RESEARCH-0013](../research/0013-compono-logging-testing-design-research.md)
for the design investigation this package's shape came from.

## When to install

Your code under test takes an `ILogger`/`ILogger<T>` constructor
dependency and the test wants to assert *what* was logged — level,
message, structured properties, exception, scope — not substitute an
unrelated application interface:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.Logging --prerelease
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
  `.AtLevel(level)`, `.WithEventId(id)`, `.WithException<TException>()`,
  `.WithMessageContaining(text)`, `.Matching(predicate)`, ending in
  `.Once()` / `.Never()` / `.Exactly(n)` — the exact same single-verb
  vocabulary `Compono.TestDoubles`/`Compono.Http` already use
  (`repository.Verify().Save().Once()`, `registration.Verify().Once()`),
  reusing core `Compono`'s `CallVerifier` unchanged.
- **`CapturingLogger` / `CapturingLogger<T>`** — hand-written, publicly,
  directly constructible with no composition involved at all:
  ```csharp
  var logger = new CapturingLogger<OrderService>();
  var logger2 = new CapturingLogger<OrderService>(new LoggingOptions { MinimumLevel = LogLevel.Warning });
  ```
  A provider-composed logger and a directly-constructed one behave
  identically — one implementation, not two.

## Structured properties and `MessageTemplate`

`CapturedLogEntry` carries both the raw `State` (always present, the
escape hatch) and derived, reflection-free structured fields:

```csharp
public readonly record struct CapturedLogEntry
{
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
   `UseLogging()` — see "Registration order," below.
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

- `ILoggerFactory` composition.
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

- [`Compono.XunitV3`](compono-xunitv3.md)/[`Compono.TUnit`](compono-tunit.md)
  — `[Compose]`/`[Shared]` used throughout the examples above.
- [`Compono.NSubstitute`](compono-nsubstitute.md)/[`Compono.TestDoubles`](compono-testdoubles.md)
  — for any other ordinary interface dependency; mind the registration-order
  note above if a composed type depends on both an `ILogger<T>` and another
  interface.
- [ADR-0055](../adr/0055-compono-logging-testing-support-package.md) —
  the full decision record, including its three amendments.
