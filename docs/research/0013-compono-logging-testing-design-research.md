# [RESEARCH-0013] `Compono.Logging` Testing-Support Design Research

**Status:** Done (research/design recommendation, including a pre-ADR
empirical validation pass — §12 — covering scope-provider behavior,
structured-state extraction across both logging call styles, stage-6
provider precedence, and `CallVerifier` verification ergonomics; no ADR
written yet — stopping here for review per direction, matching
`design-decisions.md`'s rule that a deep dive's brainstorm/research
precedes drafting)

**Feeds:** [ADR-0055](../adr/0055-compono-logging-testing-support-package.md)
(`Proposed`) — `Compono.Logging`: First-Class `Microsoft.Extensions.Logging`
Testing Support.

**Product direction (given, not re-litigated here):** first-class
`Microsoft.Extensions.Logging` testing support is an **Accepted** Compono
product requirement. This document does not evaluate whether
`Compono.Logging` should exist — only what its smallest strong API should
look like.

---

## 1. Relevant current Compono architecture

- **Resolution pipeline** (`docs/architecture/current/provider-pipeline.md`,
  [ADR-0010](../adr/0010-composition-request-pipeline-and-diagnostics-tracing.md)):
  a fixed 9-stage order. Stages 4 (configuration rules), 5 (semantic value
  providers), 6 (test-double providers), and 7 (built-in providers) are the
  only extensible ones. **Stages 5/6 are the only ones a public package can
  register into** (`builder.AddSemanticProvider(...)` /
  `builder.AddTestDoubleProvider(...)`), via
  [ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
  `ICompositionValueProvider`:

  ```csharp
  public interface ICompositionValueProvider
  {
      CompositionProviderResult TryProvide(
          in CompositionProviderRequest request,
          ICompositionContext context);
  }
  ```

  A provider returns `NotHandled` for anything it doesn't claim (never
  throws for an ordinary "not mine" case — exceptions are reserved for
  actual bugs). Provider order *within* a stage is registration order —
  whichever of two providers that could both claim the same type is
  registered first wins.

- **`CompositionProviderRequest.RequestedType` is always closed** —
  composition requests are never for an open generic definition
  (`docs/adr/0024-public-provider-extensibility-model.md` "Open generic
  behavior. Does not arise"). `ILogger<OrderService>` reaches a provider as
  an ordinary closed generic `Type`; no open-generic-registration machinery
  is needed to claim it. A provider matches it exactly the way
  `NSubstituteProvider` matches any interface — a static `Type` check
  (`RequestedType == typeof(ILogger)` or
  `RequestedType.IsGenericType && RequestedType.GetGenericTypeDefinition() == typeof(ILogger<>)`).

- **Existing precedent for "specialized framework abstraction gets its own
  package" — `Compono.Http`**
  ([ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md),
  [RESEARCH-0009](0009-compono-http-admission-research.md)): a
  hand-written, reflection-free type (`TestHttpHandler`) purpose-built for
  one BCL seam (`HttpMessageHandler`), not routed through
  `Compono.TestDoubles`'s generic double-generation. It depends only on
  core `Compono` (for `Match<T>`/`CallVerifier` reuse), not on
  `Compono.NSubstitute` or `Compono.TestDoubles`. Its verification model
  reuses core `Compono`'s `CallVerifier` **unmodified** — `Verify().Once()`
  is the same type/API a registration handle and a generated test double
  both use. `Compono.Logging` should follow this shape, not the
  generic-double shape (see §6).

- **Why `Compono.TestDoubles`/`Compono.NSubstitute` don't fit well:**
  `Compono.TestDoubles` (`docs/packages/compono-testdoubles.md`,
  [ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md))
  generates one double *per discovered interface shape*, with
  `Configure()`/`Verify()` built around literal/`Match<T>` argument
  matching on arbitrary members — a model built for "does my code call
  this member with these arguments," not "what did my code log." `ILogger`
  has exactly one real member (`Log<TState>`) whose useful test surface
  (level, message, exception, structured properties, scope) is *encoded
  inside* the generic `TState`/`Func<TState,Exception?,string>` pair, not
  visible as ordinary strongly-typed arguments a generated double's
  argument-matching model already handles. `Compono.NSubstitute` would
  produce a bare `Substitute.For<ILogger<T>>()` with no captured-entry
  model at all — usable, but every consumer would have to hand-roll
  capture/inspection themselves (exactly what `alexa-vox-craft` already
  had to do, see §5). Neither package needs to change; `Compono.Logging`
  simply doesn't route through either. Note this isn't a *structural*
  limitation of the generator — `test/Compono.Generators.Tests/TestDoubleVerifyTests.cs:2454-2481`
  and `test/Compono.TestDoubles.SampleTests/GenericMemberTests.cs` already
  use a deliberately `ILogger`-mirroring `ILoggerLike` interface
  (`Log<TState>(...)`, `BeginScope<TState>(...)`) as the motivating case
  for ADR-0044 Amendment 1's generic-method-overload support — proving the
  generator *can* emit a structurally-correct double for `ILogger<T>`'s
  real generic-method shape. What it still wouldn't give for free is any
  decoding of `TState`/`Log<TState>` into logging semantics (level,
  EventId, message, structured properties) — its verification model is
  generic argument-matching, not "what was logged" — so the case for a
  purpose-built package stands regardless.

- **`[Shared]`** (`docs/concepts/shared-values.md`) is the existing,
  unmodified mechanism for "get the exact instance a composed dependency
  received, to assert against or configure directly" — the same mechanism
  `Compono.NSubstitute` consumers already use
  (`[Shared] IOrderRepository repository`). `Compono.Logging` needs no new
  mechanism here: `[Shared] ILogger<OrderService> logger` alongside
  `OrderService service` already gives back the exact composed logger
  instance, letting an extension method on it expose captured entries. See
  §5 for this working in a real consumer today (manually, via
  `Register<ILogger<T>>(...)`, without a real provider yet).

## 2. `LayeredCraft.StructuredLogging`'s existing testing implementation

Full file:
`/Users/ncipollina/source/repos/layered-craft/structured-logging/src/LayeredCraft.StructuredLogging/Testing/TestingExtensions.cs`
(one file, ~420 lines: `TestingExtensions` static class, `TestLogger`,
`LogEntry`, `TestLogger<T>`, `TestScope<TState>`).

**Inventory:**

| Type | Shape |
|---|---|
| `TestLogger : ILogger` | `List<LogEntry> LogEntries { get; }`, `LogLevel MinimumLogLevel { get; set; }` (default `Trace`), `BeginScope<TState>`, `IsEnabled`, `Log<TState>` |
| `TestLogger<T> : TestLogger, ILogger<T>` | Empty subclass — shares `LogEntries` via **inheritance** |
| `LogEntry` | `LogLevel`, `EventId`, `object? State` (raw), `Exception?`, `string? FormattedMessage`, `DateTimeOffset Timestamp` |
| `TestScope<TState> : IDisposable` | Holds `TState State`; `Dispose()` is a no-op comment: *"No cleanup needed for test scope"* |
| `TestingExtensions` | `GetLastLogEntry`, `GetLogEntry(index)`, `GetLogEntries(level)`, `GetLogEntriesContaining(text)`, `GetLogEntriesWithException()`/`<TException>()`, `HasLogEntry(level, msg?)`, `HasLogEntryWithException<T>(level?)`, `AssertLogEntry(level, msg?)`, `AssertLogEntryAt(i, level, msg?)`, `AssertLogCount(n)` / `AssertLogCount(level, n)`, `AssertNoLogEntries()` / `(level)`, `Clear()` |

**What's good, worth carrying forward:**

- **Assertion methods throw plain `InvalidOperationException`, not an
  assertion-framework type** — already satisfies "verification must not
  require an assertion-framework dependency." No FluentAssertions/xUnit
  coupling anywhere in this file.
- **Reasoning in logging concepts** (`AssertLogEntry(LogLevel.Warning,
  "retrying")`) rather than raw `Log<TState>` mechanics — the right level
  of abstraction to preserve.
- **`FormattedMessage`** (pre-computed via the caller's own
  `formatter(state, exception)`) is exactly right — never reformats or
  re-derives the message itself, so it can't diverge from what the real
  logger would have produced.
- Simple, in-memory, synchronous, dependency-free — the right *weight
  class* for a testing package.

**What's fragile or should change for a reusable Compono-native package:**

1. **`LogEntry.State` exposes the raw, untyped `object? State`** — exactly
   the concern flagged in the task brief. A consumer wanting to assert on
   a structured property (`OrderId`) has to know that `state` is (usually)
   a compiler-generated `FormattedLogValues : IReadOnlyList<KeyValuePair<string,object>>`
   internal type, cast to that interface themselves, and know that the
   last entry's key is the magic string `"{OriginalFormat}"`. Nothing in
   this file does that work for the consumer — it's punted entirely.
   `Compono.Logging` should do this extraction once, centrally (§7),
   the way `FakeLogRecord.StructuredState`/`GetStructuredStateValue` does
   (§3).
2. **Scopes are captured nowhere.** `BeginScope<TState>` constructs a
   `TestScope<TState>` and returns it, but nothing ever records that scope
   against `LogEntries`, and nothing tracks a scope *stack* — two nested
   `BeginScope` calls have no relationship to each other from the test's
   point of view. This is a **no-op scope implementation** in every sense
   that matters for testing: a test cannot assert "this log happened
   inside scope X." The task brief's "scope behavior should be
   deliberately designed rather than a no-op/dummy" concern is fully
   justified by this file.
3. **Not thread-safe.** `LogEntries` is a plain `List<LogEntry>`, mutated
   directly in `Log<TState>` with no lock, and iterated directly (not
   snapshotted) by every query method. Concurrent logging from two threads
   racing into `Log<TState>` is a real `InvalidOperationException`/data-race
   risk on the backing array. `Compono.Http`'s `TestHttpHandler.Requests`
   already establishes the fix pattern used elsewhere in this repo: a
   private lock plus `.ToArray()` snapshot on read.
4. **Inheritance for shared state** (`TestLogger<T> : TestLogger`) works,
   but conflicts with this repo's own coding standard
   (`design-decisions.md` rule 2: "prefer composition over inheritance").
   It also means `TestLogger<T>` *is-a* `TestLogger`, so two different
   `ILogger<A>`/`ILogger<B>` instances constructed independently don't
   share anything (correct), but a single collector object shared by
   composition (§7) is the more idiomatic shape here.
5. **No `EventId` querying, no exception-type+level combination beyond
   what's listed, no `Category`.** Minor — the query surface is a
   reasonable v1 shape, just incomplete relative to what real callers
   (§5) actually assert on (`EventId` matters more than this file
   assumes zero).
6. **`IsEnabled` is a single mutable `MinimumLogLevel` int comparison** —
   simple and fine for a v1; Microsoft's own `FakeLogger.ControlLevel`
   (§3) is a strictly richer version of the same idea, but not required to
   clear the bar.

## 3. Microsoft's official testing support: `FakeLogger<T>`

Package: **`Microsoft.Extensions.Diagnostics.Testing`** (assembly
`Microsoft.Extensions.Diagnostics.Testing.dll`, also shipped historically
as part of `Microsoft.Extensions.Telemetry.Testing.dll`), namespace
`Microsoft.Extensions.Logging.Testing`. Multi-targets back to
`netstandard2.0`/.NET Framework 4.6.2+ through net10/net11 — broad enough
to sit comfortably alongside Compono's own `netstandard2.1` floor
([ADR-0037](../adr/0037-netstandard2.1-compatibility-floor.md)).

**Type shape** (confirmed via Microsoft Learn API reference, current as of
v10.9.0):

```csharp
public class FakeLogger : ILogger          // non-generic base
public sealed class FakeLogger<T> : FakeLogger, ILogger<T>
```

- **No `ILoggerFactory` required** — `new FakeLogger<T>()` (or
  `new FakeLogger<T>(collector)`) is a complete, standalone `ILogger<T>`.
  This directly answers one of the brief's open questions: Microsoft's own
  answer is "no factory needed for the common case."
- **`FakeLogCollector`** — an explicit, separately-constructible collector
  object. `FakeLogger<T>`'s constructor optionally takes one; if omitted,
  a fresh one is allocated. **Multiple loggers can share one collector**
  (e.g. one collector across every category in a test), or each logger can
  own its own (the common, simpler case). `collector.GetSnapshot()`
  returns the captured records; `FakeLogCollectorOptions.FilteredLevels`
  can restrict what's collected at all.
- **`FakeLogRecord`** — the captured-entry model, and structured-logging
  it treats as first-class:

  ```csharp
  public class FakeLogRecord
  {
      public LogLevel Level { get; }
      public EventId Id { get; }
      public object? State { get; }                          // raw, same as LayeredCraft's
      public Exception? Exception { get; }
      public string Message { get; }                          // pre-formatted
      public IReadOnlyList<object> Scopes { get; }             // opaque scope objects, snapshot at log time
      public string? Category { get; }
      public bool LevelEnabled { get; }
      public DateTimeOffset Timestamp { get; }
      public IReadOnlyList<KeyValuePair<string, string>>? StructuredState { get; }   // flattened structured properties, when available
      public object? GetStructuredStateValue(string key);      // convenience lookup, e.g. "{OriginalFormat}"
  }
  ```

  This is the single most important piece of prior art for the brief's
  "should `LogEntry` expose raw state or first-class structured semantics"
  question: **Microsoft's own answer keeps the raw `State` around** (for
  the rare case a consumer legitimately needs it) **but adds a derived,
  first-class `StructuredState`/`GetStructuredStateValue` surface next to
  it**, rather than replacing one with the other. `Compono.Logging` should
  do the same — not choose between raw and structured.
- **`Scopes` is a flat, ordered snapshot of the opaque scope *state*
  objects** active at the moment of the log call (outermost to innermost,
  or the reverse — confirm empirically, §11), not a re-flattened
  dictionary. A consumer wanting a scope's structured properties does the
  same `IReadOnlyList<KeyValuePair<...>>` cast the message state gets, if
  the scope value itself was created via `BeginScope("Processing {OrderId}", id)`
  (which also produces a compiler-generated `FormattedLogValues`).
- **`ControlLevel(LogLevel, bool)`** — a strictly more granular version of
  `MinimumLogLevel`: individual levels can be toggled independently rather
  than only a single cutoff threshold.
- **Framework-agnostic** — no xUnit/NUnit/TUnit/assertion-library
  reference anywhere in this package; it is pure `Microsoft.Extensions.Logging`
  plus its own types. Confirms this whole space is expected to be
  assertion-framework-independent, same requirement Compono has.
- **Dependency footprint**: `Microsoft.Extensions.Diagnostics.Testing`
  pulls in the broader `Microsoft.Extensions.Diagnostics`/`Telemetry`
  family (it also carries fake metrics/tracing testing support, not just
  logging) — heavier than a `Compono.Logging` package would want as its
  *own* dependency if Compono is trying to keep each integration package's
  footprint minimal and purpose-specific (`Compono.Http`'s own precedent:
  "depends only on `Compono`; does not add or require
  `Microsoft.Extensions.Http`"). No explicit Native AOT/trimming
  compatibility statement was found in the package's public docs during
  this research pass — worth a direct spike (§11) rather than assuming
  either way before depending on it.

**Should `Compono.Logging` depend on it, wrap it, or stay independent?**
Recommend **learn from it, stay independent** — same posture
`Compono.Http` took relative to `System.Net.Http.Json`/existing HTTP
mocking libraries. Reasons: (a) unknown/unconfirmed AOT story, (b) heavier
dependency surface than logging alone needs, (c) `FakeLogger`'s own
verification/query surface is minimal (`GetSnapshot()` plus manual LINQ) —
the value `Compono.Logging` adds is precisely the query/verify/Compono-
composition layer on top, which needs designing regardless of which
concrete captor type sits underneath. The **shape** of `FakeLogRecord`
(raw `State` + derived `StructuredState` + `Scopes` + `GetStructuredStateValue`)
is exactly the model `Compono.Logging`'s own captured-entry type should
adopt — adopted-not-copied, per `design-decisions.md`'s rule 5.

## 4. TUnit's approach

TUnit ships two distinct, non-overlapping logging-adjacent capabilities:

1. **Test-output log routing** (`TUnit.Logging.Microsoft`,
   `logging.AddTUnit()`) — routes `ILogger` output *produced by the system
   under test* into TUnit's own console/test-output sink, so it shows up
   attributed to the right test in the IDE/CI output. This is output
   capture, not behavior verification — explicitly out of this project's
   scope per the brief ("Do not expand into test-runner output capture").
2. **`TUnit.Mocks.Logging`** (part of the beta `TUnit.Mocks` package,
   described as source-generated, Native-AOT-compatible, no runtime
   proxies) — the actually-relevant prior art:

   ```csharp
   var logger = Mock.Logger<CheckoutService>();
   logger.VerifyLog()
       .AtLevel(LogLevel.Warning)
       .ContainingMessage("retrying")
       .WasCalled(Times.Once);
   ```

   This is a **fluent, chained verification builder** ending in a
   `Times`-based terminal call — conceptually identical in shape to
   `Compono.Http`'s `registration.Verify().Once()`
   (`CallVerifier.Once()`/`.Exactly(n)`/`.Never()`), just spelled with an
   intermediate filter-builder stage (`AtLevel`, `ContainingMessage`)
   before the terminal call. `TUnit.Mocks.Logging` is evidence that a
   fluent level/message-filtered verification surface is a validated shape
   in this exact problem space (not just this project's own `Compono.Http`
   precedent) — but it's beta, evidence not specification: `Compono.Logging`
   should end its own fluent chain on the existing core `CallVerifier`
   type (already proven, already dependency-free) rather than inventing a
   parallel `Times` concept.

TUnit's docs did not surface any public structured-property or scope
inspection API for `Mock.Logger<T>` during this pass — its verification
surface appears narrower than `FakeLogRecord`'s (level + message +
call-count only). Treated as a validated *shape* for the fluent verify
API, not a template for the captured-entry model (§3's `FakeLogRecord` is
the stronger model there).

## 5. Evidence from real Compono consumer tests

**Compono's own repo has zero real `Microsoft.Extensions.Logging.ILogger`
usage anywhere** (production or test code, excluding a stale worktree) —
expected; core `Compono`/its integration-package tests don't themselves
depend on `Microsoft.Extensions.Logging`. The only `ILogger`-named hits are
the `ILoggerLike` generator-stress-test mirror (§1) and an unrelated
private `ILogger` fixture interface in
`test/Compono.Tests/ComposerEndToEndConfigurationTests.cs:82-111` used as
a generic "some interface type" for stage-3 fallback tests — coincidental
naming, not logging-relevant.

**Two real external consumers do, and both show the same gap:**

### `structured-logging` — hand-constructed, not composed

`test/LayeredCraft.StructuredLogging.Tests/WarningExtensionsTests.cs`
already uses `Compono.XunitV3` (`[Compose]`/`[Theory]`) for its ordinary
parameters, but every test still does:

```csharp
[Theory]
[Compose]
public void Warning_WithMessage_LogsAtWarningLevel(string message)
{
    var testLogger = new TestLogger();          // manual construction — not composed
    testLogger.Warning(message);
    testLogger.AssertLogEntry(LogLevel.Warning, message);
    testLogger.AssertLogCount(1);
}
```

`TestLogger` is `new`'d by hand in every single test, never composed —
because there is currently no way for Compono to know it should produce a
`TestLogger` for an `ILogger` parameter. This is the direct, concrete
demonstration of gap #1 in the brief ("ILogger and ILogger<T> should
compose naturally through Compono").

### `alexa-vox-craft` — the real friction, and the real workaround

`test/AlexaVoxCraft.MediatR.Tests/TestKit/MediatRTestProfile.cs:37-38`:

```csharp
// The only ILogger<T> this project's tests actually assert observable behavior
// against (AssertLogCount/HasLogEntry, from LayeredCraft.StructuredLogging.Testing) -
// a real TestLogger<T>, not a UseGeneratedTestDoubles() fake of the ILogger<T>
// interface shape (confirmed: every other ILogger<T> constructor dependency in this
// project is never itself asserted against, so the generated double suffices for
// those - no open-generic Register<ILogger<T>> rule needed).
.Register<ILogger<PerformanceLoggingBehavior>>(() =>
    new TestLogger<PerformanceLoggingBehavior> { MinimumLogLevel = LogLevel.Debug })
```

and, further down (line 72), a *second*, differently-resolved `ILogger<T>`:

```csharp
// ... no test anywhere in this project requests ILogger<SkillMediator> as its own
// theory parameter (a discovery root), so no generated test-double closure ever
// reaches it ... a plain NullLogger<T> is the right fallback, not a generated double.
.Register<ILogger<SkillMediator>>(() => Microsoft.Extensions.Logging.Abstractions.NullLogger<SkillMediator>.Instance)
```

And the consuming test,
`test/AlexaVoxCraft.MediatR.Tests/Pipeline/PerformanceLoggingBehaviorTests.cs:15-21`:

```csharp
[Theory]
[Compose<MediatRTestProfile>]
public async Task Handle_WithSuccessfulRequest_LogsDebugMessages(
    [Shared] ILogger<PerformanceLoggingBehavior> logger,
    PerformanceLoggingBehavior behavior,
    ...)
```

This is exceptionally direct evidence for the composition-mechanics
question the brief asks about: **`[Shared]` already works today** for
getting the composed logger instance back for assertions — no new
mechanism is needed there (§1). What's missing is everything upstream of
it: today, a consumer must (a) know to hand-write an exact
`Register<ILogger<ThatOneType>>(...)` per closed generic type they care to
assert against, (b) separately decide a fallback (`NullLogger<T>.Instance`,
or accept whatever `UseGeneratedTestDoubles()` produces) for every other
`ILogger<T>` in the graph that's *not* individually registered, and (c)
manually pick `TestLogger<T>` from an entirely different package
(`LayeredCraft.StructuredLogging`) because Compono has no logging-native
type of its own. `Compono.Logging`'s `LoggingProvider` (§6/§7) should make
every one of these three steps unnecessary: any `ILogger<T>` in a
composed graph should resolve to a capturing logger automatically, with
`[Shared]` unchanged for the "I want to assert against this specific one"
case, and a real `NullLogger<T>`-equivalent opt-out only needed for a
consumer who explicitly wants a silent logger.

## 6. Smallest recommended `Compono.Logging` capability set

Following `Compono.Http`'s admission shape (§1) directly, not
`Compono.TestDoubles`'s generated-double shape:

1. **`LoggingProvider : ICompositionValueProvider`**, registered via
   `builder.UseLogging()` into **stage 6** (test-double providers) —
   claims `RequestedType == typeof(ILogger)` and any closed
   `ILogger<T>`, produces a capturing logger, `NotHandled` otherwise.
   Same conceptual stage `NSubstituteProvider`/`GeneratedTestDoubleProvider`
   occupy; provider-order-wins-first-registered applies identically if a
   consumer also has `UseNSubstitute()`/`UseGeneratedTestDoubles()`
   active (document explicitly: register `UseLogging()` first if `ILogger`
   should resolve to a capturing logger rather than a generic substitute).
2. **A single non-generic captor (`CapturingLogger : ILogger`) and a
   single generic captor (`CapturingLogger<T> : CapturingLogger, ILogger<T>`
   — or composition-based equivalent, see §7)** — not source-generated,
   not one-type-per-interface. `ILogger<T>`'s only real member is
   `Log<TState>`; one hand-written generic class handles every closed `T`,
   unlike `Compono.TestDoubles`, which needs per-interface generation
   because arbitrary interfaces have arbitrary members.
3. **`CapturedLogEntry`** — the structured-first captured-entry model
   (§7), adopting `FakeLogRecord`'s "raw + derived" shape.
4. **Query surface** — direct `IReadOnlyList<CapturedLogEntry>` inspection,
   no assertion framework required (§8).
5. **Fluent `VerifyLog()`** ending in core `CallVerifier` (§8) — the
   "reason in logging concepts" and "Compono-style `Verify()`" requirements
   both land here.
6. **Real, `IExternalScopeProvider`-based scope tracking** (§9) — not a
   no-op, reusing a BCL type rather than inventing one.
7. **Thread-safe capture** — lock + snapshot, `Compono.Http`'s
   already-established pattern (§2 point 3).

Explicitly **not** in v1 (no concrete evidence for any of these in §5's
real consumer data, and each is a meaningfully separate feature):

- `ILoggerFactory` composition (nothing in `alexa-vox-craft`/
  `structured-logging` requests a factory; every real case is a
  constructor-injected `ILogger<T>`).
- Serilog/other provider-specific testing.
- Test-runner output capture (TUnit's `TUnit.Logging.Microsoft` concern,
  a different problem than behavior verification).
- Cross-scope structured-property flattening/searching (a scope's own
  structured values are reachable via the same `IReadOnlyList<KeyValuePair<...>>`
  cast as message state — no bespoke API needed for v1).
- `FakeLogger`-style per-level `ControlLevel` toggling — a single
  `MinimumLevel` threshold (LayeredCraft's existing, sufficient shape)
  covers every real case seen in §5.

## 7. Proposed public API

```csharp
namespace Compono.Logging;

public static class CompositionBuilderExtensions
{
    public static CompositionBuilder UseLogging(
        this CompositionBuilder builder, Action<LoggingOptions>? configure = null);
}

public sealed class LoggingOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
}

public readonly record struct CapturedLogEntry
{
    public LogLevel LogLevel { get; }
    public EventId EventId { get; }
    public Exception? Exception { get; }
    public string Message { get; }                                          // pre-formatted, via the caller's own formatter — never re-derived
    public object? State { get; }                                           // raw TState, boxed — always present, escape hatch
    public IReadOnlyList<KeyValuePair<string, object>>? Properties { get; }  // non-null only when TState implements IReadOnlyList<KeyValuePair<string, object>> — confirmed (§9 spike) both FormattedLogValues (ordinary extension-method calls) and LoggerMessageState (the one shared BCL type behind every [LoggerMessage] source-generated call) satisfy this identically; one code path covers both
    public string? MessageTemplate { get; }                                  // Properties' "{OriginalFormat}" entry, surfaced by name; null if Properties is null or that key is absent
    public IReadOnlyList<object> Scopes { get; }                             // outermost→innermost snapshot (confirmed, §9 spike — matches LoggerExternalScopeProvider.ForEachScope and Microsoft's own FakeLogRecord.Scopes ordering), active at the moment this entry was logged
    public DateTimeOffset Timestamp { get; }
}

// Implemented once; works for both ILogger and every closed ILogger<T> — no per-T generation.
public sealed class CapturingLogger : ILogger { /* ... */ }
public sealed class CapturingLogger<T> : ILogger<T> { /* composes a CapturingLogger, does not inherit it — see §7 rationale */ }

public static class LoggerTestingExtensions
{
    // Direct inspection - no assertion framework required.
    public static IReadOnlyList<CapturedLogEntry> GetCapturedEntries(this ILogger logger);
    public static CapturedLogEntry? GetLastCapturedEntry(this ILogger logger);
    public static void ClearCapturedEntries(this ILogger logger);

    // Fluent verification entry point - one verb, matching the real Compono.TestDoubles
    // precedent (`repository.Verify().Save().Once()`), not the two-verb VerifyLog()...Verify()
    // shape originally proposed here (revised per the CallVerifier ergonomics spike, §12 item 4).
    public static LogVerificationBuilder Verify(this ILogger logger);
}

public sealed class LogVerificationBuilder
{
    public LogVerificationBuilder AtLevel(LogLevel level);
    public LogVerificationBuilder WithEventId(EventId eventId);
    public LogVerificationBuilder WithException<TException>() where TException : Exception;
    public LogVerificationBuilder WithMessageContaining(string text);
    public LogVerificationBuilder Matching(Func<CapturedLogEntry, bool> predicate);

    // Terminal methods live directly on the builder - thin one-line forwarders to core
    // Compono.CallVerifier (constructed from the filtered match count right here), not a new
    // counting/Times abstraction. CallVerifier itself is never exposed as an intermediate type.
    public void Once();
    public void Never();
    public void Exactly(int times);
}
```

**Usage — composed, no manual registration:**

```csharp
var composer = Composer.Create(builder => builder.UseLogging());
var service = composer.Create<OrderService>();   // OrderService(ILogger<OrderService> logger, ...)
```

**Usage — asserting against the composed instance, via existing `[Shared]`:**

```csharp
[Theory]
[Compose]
public void RetriesLogAWarning([Shared] ILogger<OrderService> logger, OrderService service)
{
    service.PlaceOrder(...);

    logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once();
}
```

**Usage — direct inspection:**

```csharp
var entries = logger.GetCapturedEntries();
var failure = entries.Single(e => e.LogLevel == LogLevel.Error);
Assert.Equal("OrderId", failure.Properties?.First().Key);
```

**Why `CapturingLogger<T>` composes rather than inherits `CapturingLogger`**
(deviating deliberately from `TestLogger<T> : TestLogger`, §2 point 4): a
shared internal `LogEntryCollector` object (lock + `List<CapturedLogEntry>`
+ scope-provider reference) is held by both flavors; `CapturingLogger`/
`CapturingLogger<T>` are thin `ILogger`/`ILogger<T>` façades over it. This
follows `design-decisions.md` rule 2 (composition over inheritance for the
object model) and, more concretely, means a future "share one collector
across two differently-typed loggers" need (not required for v1, no
evidence demands it — §6) is a constructor option on the collector, not an
inheritance-shape change.

## 8. Composition/integration mechanics

- **`UseLogging()` → `AddTestDoubleProvider(new LoggingProvider(options))`**
  — stage 6, exactly where `Compono.NSubstitute`/`Compono.TestDoubles`
  already register (§1). No new pipeline stage, no engine change.
- **`[Shared]` needs no change** — already demonstrated working today
  against a manually-`Register`ed `ILogger<T>` (§5); a provider-resolved
  value participates in shared-value scope identically to a registered or
  built-in one (`docs/architecture/current/provider-pipeline.md`'s "Shared
  substitute reuse" note — existing engine behavior, zero new code, same
  as `Compono.NSubstitute`'s own admission).
- **Logger category representation**: `ILogger<T>`'s category is `T`'s
  full name by convention (matches real `Logger<T>`'s behavior); the
  non-generic `ILogger` captor takes an explicit `string category` when a
  consumer constructs one directly outside composition (rare — most real
  cases are the generic constructor-injected form, §5).
- **Should `ILogger` and `ILogger<T>` share captured state?** No, by
  default — each composed request gets its own independent
  `CapturingLogger`/`LogEntryCollector`, matching Compono's
  independent-by-default composition model
  (`docs/concepts/shared-values.md`: "two composed values of the same type
  are supposed to be independent... that's the default"). A consumer
  wanting two logger parameters to observe the same underlying entries
  uses `[Shared]`, same as any other type.
- **`ILoggerFactory` is out of scope for v1** (§6) — no real consumer
  evidence requests one; every real case in §5 is direct constructor
  injection of `ILogger<T>`.
- **Stage-6 precedence between `UseLogging()` and `UseNSubstitute()`/
  `UseGeneratedTestDoubles()` — investigated and resolved (§12 item 3).**
  `CompositionContext.TryProviders` (`src/Compono/CompositionContext.cs:921-944`)
  is a hard short-circuit: first provider in a stage to return `Success`
  wins, no specificity scoring. This is the same structural ambiguity that
  already exists between `NSubstituteProvider` (claims any interface
  unconditionally) and `GeneratedTestDoubleProvider` (claims only types the
  generator actually emitted a double for) — and Compono already has a
  standing answer for it, not a gap: **ADR-0043**'s "Runtime activation and
  precedence" section states this exact rule explicitly ("explicit
  registration → explicit `.For<T>()` rule → generated test double →
  NSubstitute — `UseGeneratedTestDoubles()` before `UseNSubstitute()`...
  consistent with ADR-0024's existing 'registration order' dispatch rule,
  made explicit... rather than left to registration-order accident. A
  consumer who registers both in the opposite order gets NSubstitute-first
  behavior... an explicit, documented consequence of registration order,
  not silent or diagnosed-against in v1"), and **ADR-0024** states the
  general rule ("no priority/specificity system... no richer ordering rule
  exists yet because none has been needed"). `Compono.Logging` should copy
  this exact pattern, not invent a new precedence mechanism or touch the
  pipeline: the eventual ADR gets its own "Runtime activation and
  precedence" section stating plainly that `UseLogging()` must be
  registered before `UseNSubstitute()`/`UseGeneratedTestDoubles()` for
  `ILogger<T>` to resolve to a capturing logger, with the reverse order
  documented as an explicit, accepted consequence rather than a bug.

## 9. Scope design — the concrete recommendation

Rather than inventing scope-stack tracking from scratch (LayeredCraft's
no-op, §2), reuse the BCL's own public, dependency-free, AOT-safe scope
mechanism: `Microsoft.Extensions.Logging.LoggerExternalScopeProvider`
(namespace is `Microsoft.Extensions.Logging`, not `.Abstractions`, despite
shipping in the `Abstractions` assembly — confirmed empirically, §12 item
1; implements `IExternalScopeProvider`, uses `AsyncLocal<>` internally —
the same type real logging providers like the console logger delegate to).

Recommended shape: `LogEntryCollector` owns one
`LoggerExternalScopeProvider` instance. `CapturingLogger`/`CapturingLogger<T>.BeginScope<TState>`
calls `scopeProvider.Push(state)` and returns the `IDisposable` it hands
back directly (real pop-on-dispose semantics, not a no-op). At `Log<TState>`
time, before constructing the `CapturedLogEntry`, call
`scopeProvider.ForEachScope((scopeState, entries) => entries.Add(scopeState), scratchList)`
to snapshot every currently-active scope into `CapturedLogEntry.Scopes`.
Because `AsyncLocal<>`-backed, this correctly flows across `await` points
within the same logical call — required, since the code under test in
every real §5 example is `async`.

**Confirmed empirically (§12 item 1) — no longer an open assumption:**
`LoggerExternalScopeProvider` is public with a public parameterless
constructor; `Push`/`Dispose` pop correctly through nested scopes
(`[outer,inner]` → dispose inner → `[outer]` → dispose outer → `[]`);
`ForEachScope` enumerates **outermost-first** (3-level nesting produced
`[L1, L2, L3]`); a side-by-side spike against Microsoft's own
`FakeLogger<T>`/`FakeLogRecord.Scopes` confirmed it also orders
outermost-first, so `CapturedLogEntry.Scopes` matches both the BCL
mechanism and Microsoft's own captor without needing to reverse anything.
`AsyncLocal<>` isolation is real, not just documented: a scope pushed
inside an async method remains visible after an `await` in the same
logical call, a sibling `Task.Run` **started before** the push does not
see it (true isolation), and a sibling `Task.Run` **started after** the
push does see it (expected `ExecutionContext`-capture-at-creation-time
forward flow, not a leak).

## 10. AOT/trimming and dependency implications

- **Zero reflection anywhere in this design.** `LoggingProvider`'s match
  check is a static `Type`/`Type.GetGenericTypeDefinition()` comparison
  (already the exact pattern `NSubstituteProvider` uses, already proven
  AOT-safe in this repo). `CapturingLogger`/`CapturingLogger<T>` are
  ordinary hand-written classes — no dynamic proxy generation (unlike
  `Compono.NSubstitute`'s necessary, accepted exception to the
  no-reflection default, §1/[ADR-0025](../adr/0025-compono-nsubstitute-package-design.md)),
  no source generation needed (unlike `Compono.TestDoubles`) — because
  `ILogger<T>`'s member surface is fixed and small, one hand-written
  generic class covers every `T`.
- **Structured-property extraction stays reflection-free**: the `Properties`
  derivation is a single `state is IReadOnlyList<KeyValuePair<string, object>> list`
  pattern-match — no `TState`-shape reflection, and confirmed empirically
  (§12 item 2) to be the *same* single pattern-match for both ordinary
  extension-method calls (`FormattedLogValues`) and `[LoggerMessage]`
  source-generated calls (`LoggerMessageState` — one shared BCL type
  reused across every `[LoggerMessage]` call site, not a bespoke
  per-call-site struct as originally guessed) — no separate code path
  needed for the source-gen style.
- **Dependency footprint**: `Compono` (core) +
  `Microsoft.Extensions.Logging.Abstractions` only (no
  `Microsoft.Extensions.Logging` concrete implementation, no
  `Microsoft.Extensions.DependencyInjection`, no
  `Microsoft.Extensions.Diagnostics.Testing` — see §3's independence
  recommendation). This is the minimal footprint the whole `ILogger`
  ecosystem is built on; every ASP.NET Core/worker-service/console-host
  consumer already transitively references `Abstractions`.
- `Directory.Build.props`-level AOT/trimming settings already established
  in this repo apply unchanged — `Compono.Logging` needs no new AOT
  opt-out, unlike `Compono.NSubstitute`'s accepted reflection exception.
  Should follow `Compono.Http`'s csproj precedent and set
  `<IsAotCompatible>true</IsAotCompatible>` explicitly — currently the
  *only* package in the repo that sets it (core `Compono` and
  `Compono.TestDoubles` leave it unset, which is silence, not a negative
  signal, since neither has a `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`-
  annotated member to gate). Setting the flag is what makes the trim/AOT
  analyzer actually enforce those attributes at consumer call sites rather
  than silently suppress the warning — worth doing here precisely because
  this design's core claim is "no reflection anywhere," and the flag is
  the mechanism that makes the analyzer verify that claim rather than take
  it on faith.

## 11. Alternatives considered and rejected

- **Route `ILogger`/`ILogger<T>` through `Compono.TestDoubles`'s generated
  doubles instead of a purpose-built provider.** Rejected — §1 and §5
  demonstrate real friction (`alexa-vox-craft`'s explicit comment: a
  generated double for `ILogger<T>` is *not* what's needed for the one
  logger a test actually asserts against; a generic double gives no
  message/level/structured-property inspection surface at all, only
  argument-matched configuration of an opaque `Log<TState>` call). Compono
  already has one precedent (`Compono.Http`) for "this BCL seam deserves
  purpose-built support, not the generic path" — this is the second.
- **Depend on `Microsoft.Extensions.Diagnostics.Testing`'s `FakeLogger<T>`
  directly, and build only the Compono-composition/verify layer on top.**
  Considered seriously (§3) — rejected for v1 on two grounds: (a)
  unconfirmed AOT/trimming compatibility and heavier transitive dependency
  surface (the whole fake-metrics/fake-tracing family, not just logging);
  (b) it forecloses `Compono.Logging` owning its own `CapturedLogEntry`
  shape suited exactly to Compono's own verification idioms
  (`CallVerifier` reuse, `[Shared]` integration) rather than adapting
  `FakeLogRecord`'s shape after the fact. Its **captured-entry model** is
  adopted (§3/§7); its **package** is not depended on.
- **`TestLogger<T> : TestLogger` inheritance for shared state**, matching
  `LayeredCraft.StructuredLogging` exactly. Rejected in favor of
  composition over a shared `LogEntryCollector` (§7) — consistent with
  `design-decisions.md` rule 2, and leaves the "share one collector across
  two differently-categorized loggers" question open as a constructor
  option rather than a type-hierarchy decision, should real evidence ever
  demand it.
- **A `MinimumLevel`-only filter vs. `FakeLogger`'s per-level
  `ControlLevel` toggling.** `MinimumLevel` (LayeredCraft's existing shape)
  is kept for v1 — every real §5 use case is satisfied by a single
  threshold; `ControlLevel`'s richer per-level control has no concrete
  demand behind it yet (matches this project's own "smallest strong API"
  framing).
- **Register `LoggingProvider` as a stage-5 semantic-value provider
  instead of stage 6.** Rejected — `ILogger`/`ILogger<T>` are interface
  requests receiving a purpose-built fake, the same conceptual shape stage
  6's existing registrants (`NSubstituteProvider`,
  `GeneratedTestDoubleProvider`) already occupy; stage 5 is reserved for
  "pattern-matching value semantics" providers like `Compono.Bogus`'s
  member-name provider, a different kind of concern.
- **Two-verb `logger.VerifyLog()....Verify().Once()` chain, exposing
  `CallVerifier` as a visible intermediate return type.** Rejected after
  the CallVerifier ergonomics spike (§12 item 4) — `CallVerifier`
  (`src/Compono/CallVerifier.cs:12-34`) is a `readonly struct` built from
  an already-computed `int` count plus a description, with `void`-returning
  terminal methods (`Once`/`Never`/`Exactly`); it does no filtering itself,
  so nothing about it demands `Verify()` appear twice in one chain. The
  real `Compono.TestDoubles` precedent (`repository.Verify().Save().Once()`,
  `test/Compono.TestDoubles.SampleTests/VerificationTests.cs:21-23`) uses
  "Verify" exactly once: `Verify()` is the *entry point*, and a filter/
  member-selector chain leads straight to `CallVerifier`'s terminal
  methods. `Compono.Logging`'s public API (§7) now matches that shape —
  `logger.Verify().AtLevel(...).WithMessageContaining(...).Once()` — with
  `LogVerificationBuilder` exposing `Once()`/`Never()`/`Exactly(n)`
  directly as thin forwarders to a `CallVerifier` built from the filtered
  count, rather than returning `CallVerifier` as a visible type partway
  through the chain. This is still zero new abstraction: three one-line
  delegations to a struct that only has three members.

## 12. Pre-ADR validation spikes — results

All four spikes below were run empirically (real code, not reasoned from
memory) before this document was updated; none required a change to the
recommended architecture, and none surfaced a need for a broader
cross-cutting Compono design decision beyond what's already noted for
`[Shared]` (item 5).

1. **`LoggerExternalScopeProvider` accessibility and `ForEachScope`
   enumeration order — resolved, confirmed empirically (§9).**
   `Microsoft.Extensions.Logging.LoggerExternalScopeProvider` (note:
   namespace `Microsoft.Extensions.Logging`, not `.Abstractions`, though it
   ships in that assembly) is public with a public parameterless
   constructor. `Push`/`Dispose` pop correctly through nested scopes.
   `ForEachScope` enumerates **outermost-first** — confirmed against 3
   levels of nesting, and cross-checked directly against a real
   `FakeLogger<T>`/`FakeLogRecord.Scopes`, which orders outermost-first
   too. `AsyncLocal<>` isolation confirmed genuine: visible across `await`
   in the same logical call, invisible to a sibling `Task.Run` started
   before the push, visible to one started after (expected
   `ExecutionContext`-capture forward flow, not a leak). `CapturedLogEntry.Scopes`
   is now specified as outermost-first in §7 with no remaining
   uncertainty.
2. **`{OriginalFormat}` / structured-state reliability across both common
   logging call styles — resolved, confirmed empirically (§9).** Ordinary
   `logger.LogInformation("...", args)` produces a `FormattedLogValues`
   state; `[LoggerMessage]`-source-generated calls produce a
   `LoggerMessageState` state — a single shared BCL type reused across
   *every* `[LoggerMessage]` call site (not a bespoke per-call-site struct,
   contrary to this document's original guess). Both implement
   `IReadOnlyList<KeyValuePair<string, object>>` (non-nullable `object`,
   not `object?` — §7's `CapturedLogEntry.Properties` signature corrected
   accordingly), both carry `"{OriginalFormat}"` as an entry with the raw
   template string, and structured values are preserved as their original
   boxed CLR type in both paths (ints/strings round-trip via unboxing, not
   stringified). One uniform extraction code path
   (`state is IReadOnlyList<KeyValuePair<string, object>> pairs`) covers
   both call styles with no special-casing.
3. **Stage-6 provider-precedence semantics — resolved, verdict (A) from
   the design-decisions.md framing in the request (§8).**
   `CompositionContext.TryProviders` (`src/Compono/CompositionContext.cs:921-944`)
   is confirmed by source to be a hard short-circuit: first provider in a
   stage to return `Success` wins, no specificity scoring exists anywhere
   in the pipeline. This exact structural situation — a specialized
   provider vs. a generic fallback provider both able to claim the same
   type — already exists today between `NSubstituteProvider` (claims any
   interface unconditionally, `src/Compono.NSubstitute/NSubstituteProvider.cs:31-46`)
   and `GeneratedTestDoubleProvider` (claims only types the generator
   actually emitted a double for, `src/Compono.TestDoubles/GeneratedTestDoubleProvider.cs:13-16`),
   and Compono already has a standing, `Accepted`, documented answer for
   it: **ADR-0043**'s "Runtime activation and precedence" section states
   plainly that registration order is the tiebreak, that reversing it is
   an explicit accepted consequence and not a bug, and **ADR-0024**
   confirms the general rule ("no priority/specificity system... no
   richer ordering rule exists yet because none has been needed"). This is
   not a gap Compono.Logging exposes — it's an already-settled pattern the
   design should copy verbatim (own "Runtime activation and precedence"
   ADR section, §8), and importantly: **no pipeline/architecture change is
   needed or warranted** — confirming the request's instruction not to
   expand scope into a pipeline redesign was the right call going in.
4. **`VerifyLog()`/`CallVerifier` ergonomics — resolved, API revised
   (§7/§11).** `CallVerifier`'s real shape (a `readonly struct` from an
   already-computed count, `void`-returning terminals) and the real
   `Compono.TestDoubles` precedent (`Verify()` used exactly once per
   chain, `test/Compono.TestDoubles.SampleTests/VerificationTests.cs:21-23,30`)
   together settle this: `Compono.Logging`'s public API is revised from
   `logger.VerifyLog()....Verify().Once()` to
   `logger.Verify()....Once()`, with `Once()`/`Never()`/`Exactly(n)` living
   directly on `LogVerificationBuilder` as thin forwarders to
   `CallVerifier` — matching the one-verb precedent exactly, still zero
   new counting abstraction.

**Remaining open items for the ADR itself** (not spikes — judgment calls
that don't need more empirical work):

5. **Exact non-generic `ILogger` construction API** — real §5 evidence is
   100% `ILogger<T>` (constructor-injected, category = a type). Whether
   `CapturingLogger`'s standalone (non-generic) constructor needs a
   `string category` parameter at all for `Compono.Logging` v1, or can be
   deferred entirely until a real non-generic `ILogger` consumer case
   surfaces, is worth a final judgment call in the ADR rather than in this
   research document.
6. **`[Shared]` ergonomics — explicitly out of scope for `Compono.Logging`,
   flagged for its own future investigation.** The proposed
   `[Shared] ILogger<OrderService> logger, OrderService service` usage
   (§7) is another concrete instance of an already-recognized `[Shared]`
   gap: a consumer sometimes needs a parameter *purely* to obtain the
   graph-owned instance for inspection, not because the parameter itself
   is under test. `Compono.Logging` uses `[Shared]` exactly as it exists
   today (§8) and does not attempt to solve this locally — e.g. no
   `Share<T>()` helper, no new attribute. This is recorded here as
   evidence for that broader, separately-tracked design question, not as
   something this package's ADR should resolve.
7. **AOT/trimming compatibility of `Microsoft.Extensions.Diagnostics.Testing`**
   itself remains unconfirmed either way (§3/§10) — not blocking, since
   `Compono.Logging`'s recommendation is independence from that package
   regardless of its AOT status; only matters if a later revision
   reconsiders depending on it, and should not be silently assumed either
   way in that hypothetical ADR's Context section.
