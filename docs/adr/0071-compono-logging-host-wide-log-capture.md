# [ADR-0071] `Compono.Logging`: Host-Wide Log Capture (`LogCollector` + `AddComponoCapture`)

**Status:** Proposed

**Date:** 2026-11-10

**Decision Makers:** product owner (maintainer interview, 2026-11-10), assisted by Claude Opus 5.5

## Context

Issue [#164](https://github.com/LayeredCraft/compono/issues/164) (child of
[#161](https://github.com/LayeredCraft/compono/issues/161), item 1): a
Generic Host test needs to assert that *nothing* was logged at `Error` or
above in *any* category. That includes framework and third-party
categories (the hosting layer, a scheduler) that the test never composes.
The host's own `ILoggerFactory` creates those loggers, not Compono. Today
the consumer keeps `Microsoft.Extensions.Diagnostics.Testing`
(`AddFakeLogging()` + `FakeLogCollector`) for this one guard, which gives
the suite two log-capture models and two verification vocabularies.

What `Compono.Logging` does today, per
[ADR-0055](0055-compono-logging-testing-support-package.md) and
`src/Compono.Logging`:

- `UseLogging()` registers a stage-6 provider. That provider composes
  `ILogger`/`ILogger<T>` as `CapturingLogger`/`CapturingLogger<T>`.
- Each capturing logger owns a private, internal `LogEntryCollector`
  (lock + list + its own `LoggerExternalScopeProvider`). There is no
  collector shared across loggers.
- `CapturedLogEntry` has no category.
- There is no `ILoggerProvider`/`ILoggingBuilder` integration.
  ADR-0055's "`ILoggerFactory`: out of scope" boundary deferred this
  "until real evidence demands it". #161's migration is that evidence:
  capability admission Gate B is met per
  [RESEARCH-0036](../research/0036-issue-161-test-composition-gaps-intake.md).
  Gate A is met because this is a material extension of an already-admitted
  package's own scope, not a new capability or package.

The facts below were established empirically on 2026-11-10 with a
throwaway spike (class library on net8.0 + net11.0 referencing **only**
`Microsoft.Extensions.Logging.Abstractions`, `IsAotCompatible=true`,
driven by a real `Host.CreateApplicationBuilder()` on SDK 11.0.100):

1. `ILoggerProvider`, `ISupportExternalScope`, `ILoggingBuilder`, and
   `ProviderAliasAttribute` all live in
   `Microsoft.Extensions.Logging.Abstractions`.
   `ServiceDescriptor`/`IServiceCollection` registration helpers come
   transitively through `Microsoft.Extensions.DependencyInjection.Abstractions`,
   which `Logging.Abstractions` already depends on. A provider-based
   integration needs **no new package dependency**. Provider-specific
   filter rules (`AddFilter<TProvider>`, `LoggerFilterOptions`) and
   `AddProvider` live in the concrete `Microsoft.Extensions.Logging`
   package, which `Compono.Logging` deliberately doesn't reference.
2. The host's default minimum level is `Information`, so `Debug`/`Trace`
   never reach any provider unless the test lowers it. Host categories
   (`Microsoft.Hosting.Lifetime`, an arbitrary
   `ILoggerFactory.CreateLogger("Some.Framework.Category")`, and
   `ILogger<T>` resolved from the container) all reach the provider with
   their real category strings.
3. When a provider implements `ISupportExternalScope`, the factory hands
   it the factory's scope provider. `BeginScope` on a host
   `ILogger<T>` then pushes onto that provider, and
   `ForEachScope` enumerates outermost to innermost, still correct after
   an `await`. This matches ADR-0055's per-logger scope semantics.
4. A provider registered as a **ready-made instance** is never disposed
   by the host. A provider registered through a **factory** delegate is
   disposed on `host.Dispose()`.
5. `LoggerFactory.CreateLogger<T>()` uses this category name: the full
   type name with nested types joined by `.` (not `+`) and the generic
   arity and arguments removed. Built-in types use their C# keyword
   (`N.Outer+Inner` → `N.Outer.Inner`, `N.Gen<int>` → `N.Gen`,
   `List<N.Outer.Inner>` → `System.Collections.Generic.List`,
   `int` → `int`).

## Decision Drivers

- Satisfy the real consumer scenario (host-wide, cross-category,
  level-threshold guard) with the smallest maintainable public surface.
- Zero behavior change for existing `Compono.Logging` consumers: issue
  #164's acceptance criteria forbid breaking `CapturingLogger`,
  `CapturedLogEntry`, or `UseLogging()`.
- No new package dependency, and no new package (the issue intake already
  argued a separate integration package isn't justified).
- Native AOT and trimming: `IsAotCompatible` stays `true` and no
  reflection is used.
- Test isolation under parallel execution: no static or ambient capture
  state, and nothing is captured globally by accident.
- One verification vocabulary: reuse `CapturedLogEntry` and
  `LogVerificationBuilder` (and through it, core `CallVerifier`).
- Don't replace or wrap the host's `ILoggerFactory`. The host's filter
  pipeline stays the authority on what gets logged.

## Considered Options

**Axis 1: how the test reaches the captured host logs.**

1. An explicit, test-owned public `LogCollector`, passed to an
   `ILoggingBuilder` extension. The provider type stays internal.
2. A public `ILoggerProvider` type that is itself the handle
   (`AddProvider(provider)`, then `provider.Verify()`).
3. A collector registered as a DI singleton by
   `AddComponoCapture()` and resolved from `host.Services`
   (the `FakeLogCollector` pattern).

**Axis 2: whether composed loggers and host loggers share a destination.**

1. Separate in v1: host capture goes only to its `LogCollector`, and
   composed loggers stay per-logger exactly as today.
2. Opt-in forwarding (`UseLogging(o => o.Collector = logs)`): composed
   loggers also append to a given collector.
3. One store for everything (composed loggers share a default collector).

**Axis 3: level filtering for host capture.**

1. Host filter rules only. `LogCollector` takes no options.
2. Host filter rules, then an extra `LoggingOptions.MinimumLevel` gate
   on the collector.
3. Force-capture everything (a provider-specific `Trace` rule injected by
   `AddComponoCapture`). Not achievable without a new
   `Microsoft.Extensions.Logging` dependency (spike fact 1).

**Axis 4: cross-category verification vocabulary.**

1. Add `AtLevelOrAbove(LogLevel)` and `InCategory(string)` to
   `LogVerificationBuilder`.
2. Add `AtLevelOrAbove(LogLevel)` only.
3. No new filters. Use `Matching(...)`.

## Decision Outcome

Chosen per axis (all confirmed in the maintainer interview, 2026-11-10):
**Axis 1, Option 1** (explicit `LogCollector`); **Axis 2, Option 1**
(separate in v1); **Axis 3, Option 1** (host rules only); **Axis 4,
Option 1** (`AtLevelOrAbove` + `InCategory`). Category naming for
composed loggers matches `LoggerFactory` exactly (below).

### Package identity and dependency graph

Unchanged from ADR-0055. Everything ships in `Compono.Logging`:

```
Compono.Logging
    -> Compono
    -> Microsoft.Extensions.Logging.Abstractions   (already referenced; brings DI.Abstractions transitively)
```

No new `PackageReference` in `Compono.Logging.csproj`. Explicitly not
added: `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Hosting`,
`Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection`
(the implementation), or `Microsoft.Extensions.Diagnostics.Testing`.
Test projects may reference `Microsoft.Extensions.Hosting`/
`Microsoft.Extensions.Logging` as **test-only** dependencies to drive a
real host.

### Public API (additive only)

```csharp
namespace Compono.Logging;

// NEW. Test-owned, thread-safe destination for host-created loggers.
public sealed class LogCollector
{
    public LogCollector();

    public IReadOnlyList<CapturedLogEntry> GetCapturedEntries();   // snapshot, capture order
    public CapturedLogEntry? GetLastCapturedEntry();
    public void ClearCapturedEntries();
    public LogVerificationBuilder Verify();
}

// NEW. The only way to attach a LogCollector to a host's logging pipeline.
public static class ComponoLoggingBuilderExtensions
{
    public static ILoggingBuilder AddComponoCapture(this ILoggingBuilder builder, LogCollector collector);
}

// CHANGED (additive): one new property.
public readonly record struct CapturedLogEntry
{
    public string Category { get; }   // never null
    // ... every existing member unchanged ...
}

// CHANGED (additive): two new filters.
public sealed class LogVerificationBuilder
{
    public LogVerificationBuilder AtLevelOrAbove(LogLevel level);
    public LogVerificationBuilder InCategory(string category);
    // ... every existing member unchanged ...
}
```

Consumer shape:

```csharp
var logs = new LogCollector();

var builder = Host.CreateApplicationBuilder();
builder.Logging.AddComponoCapture(logs);
using var host = builder.Build();

await host.StartAsync();
// ... exercise the system ...
await host.StopAsync();

logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();
logs.Verify().InCategory("Quartz.Core.QuartzScheduler").AtLevel(LogLevel.Warning).Once();
```

The same call works anywhere an `ILoggingBuilder` exists, for example
`services.AddLogging(b => b.AddComponoCapture(logs))`, or
`WebApplicationBuilder.Logging`/`IHostBuilder.ConfigureLogging`. Those
are the consumer's own `Microsoft.Extensions.*` APIs, not Compono
surface.

**API rules the implementation must follow:**

- `LogCollector`'s four members have the same names, return types, and
  semantics as the existing `ILogger` extension methods in
  `LoggerTestingExtensions` (`GetCapturedEntries`/`GetLastCapturedEntry`/
  `ClearCapturedEntries`/`Verify`). Reads are snapshots, and
  `Verify()` builds a fresh `LogVerificationBuilder` over this
  collector's entries.
- `LogCollector` is not `IDisposable`, has no static state, and has no
  constructor options (Axis 3).
- `AddComponoCapture` throws `ArgumentNullException` for a null `builder`
  or `collector` and returns `builder` for chaining.
- No public `ILoggerProvider` type. The provider is `internal sealed`,
  implements `ILoggerProvider` and `ISupportExternalScope`, and carries
  `[ProviderAlias("Compono")]`, so a consumer's own configuration-based
  filter rules (`Logging:Compono:LogLevel:...`) can target it. The alias
  costs no dependency (spike fact 1). It's a host filtering mechanism,
  not a Compono option.
- `LogVerificationBuilder.AtLevelOrAbove(level)` matches an entry when
  `entry.LogLevel >= level` and `entry.LogLevel != LogLevel.None`.
  `AtLevelOrAbove(LogLevel.None)` therefore never matches. Its
  failure-message fragment reads `level {level} or above`.
- `LogVerificationBuilder.InCategory(category)` is an exact, ordinal,
  case-sensitive match on `CapturedLogEntry.Category`. A null argument
  throws `ArgumentNullException`. Its failure-message fragment reads
  `category "{category}"`. Prefix and wildcard matching are not provided,
  so use `Matching(...)` for those.
- Both new filters work identically on a composed logger's
  `logger.Verify()` and on `LogCollector.Verify()`.
- `ComponoLoggingBuilderExtensions` is named to avoid an ambiguous-type
  clash with `Microsoft.Extensions.Logging.LoggingBuilderExtensions` when
  a consumer imports both namespaces.

### `CapturedLogEntry.Category`

- **Host-captured entries**: the exact category string the host's
  `ILoggerFactory` passed to `ILoggerProvider.CreateLogger(category)`.
- **Composed or directly constructed `CapturingLogger<T>`**: the same
  string `LoggerFactory.CreateLogger<T>()` would produce for `T`
  (spike fact 5). That means full name, `.` as the nested-type
  delimiter, generic arity and arguments removed, and C# keyword aliases
  for built-in types. Computed without reflection beyond `Type` name
  properties (`Namespace`/`Name`/`DeclaringType`/`FullName`), which are
  trim- and AOT-safe. Exact parity with `LoggerFactory` is a required,
  tested property. It's computed once per logger instance, not per log
  call.
- **Non-generic `CapturingLogger`** (direct or composed bare `ILogger`):
  `""` (empty string). ADR-0055's "Non-generic `ILogger`" decision stands:
  no category-string constructor is added.
- Adding a property to a `readonly record struct` whose only constructor is
  `internal` is source- and binary-compatible. ApiCompat must report it
  as an addition only. Record value equality now also compares
  `Category`. That's an accepted, documented consequence: two entries that
  differ only by category were previously equal and no longer are.

### Composed vs. host capture: separate in v1

- Host-created loggers write only to the `LogCollector` they were
  registered with.
- Composed `ILogger`/`ILogger<T>` (via `UseLogging()`) and directly
  constructed `CapturingLogger`/`CapturingLogger<T>` keep their own
  per-logger store, exactly as today. Their only visible change is that
  their entries now carry `Category`.
- `UseLogging()`, `LoggingOptions`, `LoggingProvider`, and the generated
  activation path are unchanged.
- A host's `ILogger<T>` (from `host.Services`) is the host's own
  `Logger<T>`, not a Compono capturing logger. Calling
  `logger.Verify()`/`GetCapturedEntries()` on it keeps throwing the
  existing `InvalidOperationException`. That message gains one sentence
  pointing host-logger users at `LogCollector`/`AddComponoCapture`.
  The exception type and behavior don't change.
- Opt-in forwarding of composed entries into a `LogCollector` (Axis 2,
  Option 2) is deferred, not rejected. It's additive and can be added
  later without breaking anything, if real evidence asks for it.

### Filtering: host rules only

- The host's own filter pipeline (`LoggerFilterOptions`: the global
  minimum, category rules, and provider rules including the `Compono`
  alias) decides what reaches the provider. `AddComponoCapture` never adds,
  removes, or changes filter rules.
- The provider's loggers report `IsEnabled(level) == (level != LogLevel.None)`.
  `Log<TState>` also checks this first (defense in depth, matching
  ADR-0055's `MinimumLevel` decision), so `LogLevel.None` is never
  captured.
- Documented consequence: under a default host (minimum `Information`),
  `Debug`/`Trace` entries are not captured. A test that wants them lowers
  the host's own level (`builder.Logging.SetMinimumLevel(LogLevel.Trace)`
  or a `Compono`-alias configuration rule). This is the consumer's
  `Microsoft.Extensions.Logging` API, not Compono's.

### Structured state, exceptions, timestamps

Host-captured entries are built with **exactly** the same rules as
composed entries (ADR-0055 "Captured-entry semantics"): `Message` comes
from the caller's formatter. `State` is the boxed raw state. `Properties`
and `MessageTemplate` come from the single reflection-free
`IReadOnlyList<KeyValuePair<string, object>>` pattern match.
`Exception` is passed through. `Timestamp` is `DateTimeOffset.UtcNow` at
capture. The composed and host paths must share one internal
implementation of entry construction, never two copies that can drift.

### Scopes

- The provider implements `ISupportExternalScope`. Its loggers' scope
  snapshot comes from the scope provider the host factory supplies via
  `SetScopeProvider`. `BeginScope` on a provider-created logger pushes onto
  that same scope provider.
- If `SetScopeProvider` is never called, the provider falls back to its own
  `LoggerExternalScopeProvider`, so scopes still work.
- `CapturedLogEntry.Scopes` keeps ADR-0055's contract: outermost to
  innermost, a snapshot at capture time, and correct across `await`.
- Documented consequence: when the host enables `ActivityTrackingOptions`,
  the factory's scope provider also yields its activity scope object, and
  that appears in `Scopes` like any other scope. Compono doesn't filter it.

### Ownership, lifetime, disposal

- **The test owns the `LogCollector`.** It's a plain object that is never
  disposed, reset, or detached by Compono or the host. Reads remain valid
  after the host is disposed.
- **The host owns the provider.** `AddComponoCapture` registers the
  internal provider so the host's container disposes it on host disposal
  (factory-style registration, spike fact 4). That follows ADR-0022
  Amendment 4 / RESEARCH-0015: Compono doesn't take disposal ownership of
  host infrastructure. Provider disposal never clears or invalidates the
  collector. A provider-created logger that is still referenced and logs
  after host disposal still appends to the collector. That's documented
  and harmless.
- **Registration cardinality:**
  - Calling `AddComponoCapture(logs)` twice on the same builder with the
    **same** collector instance is idempotent: one provider, and no
    duplicate entries.
  - Calling it with **different** collectors on one builder gives each
    collector its own provider, and each receives every entry the host
    lets through.
  - Registering the **same** collector with several hosts is allowed:
    it receives entries from all of them. That's the test's explicit
    choice.
  - These are required behaviors. The registration mechanism is an
    implementation choice. Note that a naive `TryAddEnumerable` deduplicates
    by implementation type and would silently drop a second, distinct
    collector, so it must not be used unless the behavior above holds.

### Thread safety and isolation

- One private lock per `LogCollector` guards appends, clears, and reads.
  Reads return snapshot copies, following the existing
  `LogEntryCollector`/`Compono.Http` pattern. Entry construction
  (formatting, structured extraction, scope snapshot) happens outside the
  lock. Capture order is the order in which appends acquire the lock.
- There's no static, ambient, or global state. Each test's collector sees
  only the hosts that test explicitly registered it with, so parallel tests
  with separate hosts and collectors never share entries. Nothing is
  captured from loggers that aren't created by a host the collector was
  added to.
- The cost of contention on one lock shared by many categories is accepted.
  Test hosts log at test volume. The concurrency tests must exercise
  concurrent multi-category logging alongside reads and clears (bounded, per
  the CI-hang lesson recorded in RESEARCH-0033).

### Native AOT and trimming

No reflection, no `MakeGenericType`, and no dynamic code. The provider and
collector are ordinary hand-written classes with no generated activation
(unlike `ILogger<T>` composition), because the host, not Compono,
instantiates loggers by category string. Category naming for
`CapturingLogger<T>` uses only trim-safe `Type` name properties.
`IsAotCompatible` stays `true`, and `Compono.Logging.AotSmokeTest` must
prove host capture under a real Native AOT publish with zero IL2xxx/IL3xxx
warnings.

### Backward compatibility

Additive only: one new type, one new extension class, one new property,
and two new builder methods. No existing member changes signature,
nullability, or behavior. The one exception is the documented
`CapturedLogEntry` equality consequence above, plus one sentence appended
to an existing exception message. Package validation (ApiCompat against the
last stable baseline) must pass with no suppression.

### Relationship to ADR-0055

This ADR narrows ADR-0055's "`ILoggerFactory`: out of scope" boundary.
Host *provider* integration is now in scope. Composing or replacing an
`ILoggerFactory` is still out of scope, and so is everything else in
ADR-0055's "Explicit initial-package boundaries" (no Serilog or
provider-specific behavior, no runner-output routing, no
`ControlLevel`-style toggling, no dependency on
`Microsoft.Extensions.Diagnostics.Testing`). ADR-0055 Amendment 6 records
the pointer.

### Positive Consequences

- The #161 guard (`no Error+ in any category`) becomes a single Compono
  call, and `Microsoft.Extensions.Diagnostics.Testing` is no longer needed
  for logging. (#165 metrics still keeps it in that suite.)
- One entry model and one verification vocabulary across composed and
  host capture.
- No new package, no new dependency, and AOT-safe.
- Existing consumers are unaffected beyond gaining `Category`.

### Negative Consequences

- Two capture destinations exist (per-logger and `LogCollector`). A test
  that composes loggers *and* runs a host reads them from two places until
  opt-in forwarding exists. Mitigation: documented, and forwarding is
  additive later.
- Under default host filtering, `Debug`/`Trace` aren't captured.
  Mitigation: documented, with the one-line host-side fix.
- `CapturedLogEntry` equality now includes `Category`.

## Pros and Cons of the Options

### Axis 1

- **Option 1, explicit collector (chosen).** Good: smallest surface
  (provider stays internal), the test owns the handle and its lifetime, it
  works after disposal, and it needs no DI lookup. Bad: the test has to
  hold one more variable.
- **Option 2, public provider handle.** Good: familiar `AddProvider`
  idiom. Bad: makes `ILoggerProvider`/`ISupportExternalScope` members
  public Compono surface. `AddProvider` is a concrete-package API.
  Instance-registered providers are never disposed by the host.
- **Option 3, DI-resolved collector.** Good: mirrors `FakeLogCollector`.
  Bad: retrieval needs a live service provider, a singleton per container
  is awkward with several collectors, and it's less explicit.

### Axis 2

- **Option 1, separate (chosen).** Good: zero behavior change, and the
  real scenario is host-only. Bad: there are two places to read if a test
  mixes both.
- **Option 2, opt-in forwarding.** Good: covers #161's "both into one
  collector" suggestion. Bad: new `LoggingOptions` surface and a
  dual-write path with no real call site yet. Deferred, not rejected.
- **Option 3, one store.** Bad: changes what
  `logger.GetCapturedEntries()` returns today, which breaks an #164
  acceptance criterion.

### Axis 3

- **Option 1, host rules only (chosen).** Good: the host stays the single
  authority, and there's no option surface. Bad: no Compono-side
  threshold, but the host already has one.
- **Option 2, extra `MinimumLevel` gate.** Good: symmetric with
  `CapturingLogger(LoggingOptions?)`. Bad: two gates to reason about, for
  no evidenced need.
- **Option 3, force-capture.** Bad: needs a new
  `Microsoft.Extensions.Logging` dependency, and overrides the host's own
  configuration.

### Axis 4

- **Option 1 (chosen).** Good: readable failure messages for the two
  checks the scenario actually needs. Bad: two more public members.
- **Option 2.** Good: one member. Bad: category checks fall back to
  "a custom condition" messages.
- **Option 3.** Good: no surface. Bad: the scenario's headline assertion
  reads as a lambda with an opaque failure message.

## Links

- Issue [#164](https://github.com/LayeredCraft/compono/issues/164); parent
  [#161](https://github.com/LayeredCraft/compono/issues/161)
- [ADR-0055](0055-compono-logging-testing-support-package.md) (and its
  Amendment 6)
- [ADR-0022](0022-compono-xunit-package-design.md) Amendment 4,
  [RESEARCH-0015](../research/0015-disposal-ownership-research.md)
  (non-owning disposal)
- [RESEARCH-0013](../research/0013-compono-logging-testing-design-research.md),
  [RESEARCH-0033](../research/0033-compono-logging-tests-concurrency-hang-investigation.md),
  [RESEARCH-0036](../research/0036-issue-161-test-composition-gaps-intake.md)
- [PLAN-0072](../plans/0072-compono-logging-host-wide-log-capture-and-dotnet-11-ga-impl-plan.md)
