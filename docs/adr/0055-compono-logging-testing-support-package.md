# [ADR-0055] `Compono.Logging`: First-Class `Microsoft.Extensions.Logging` Testing Support

**Status:** Accepted

**Date:** 2026-08-28

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

First-class `Microsoft.Extensions.Logging` (`ILogger`/`ILogger<T>`) testing
support is an **Accepted** Compono product requirement — this ADR does not
revisit whether it belongs, only what its architecture and public API
should be.

The full evidence trail — an audit of `LayeredCraft.StructuredLogging`'s
hand-rolled `TestLogger`/`TestingExtensions`, Microsoft's own
`FakeLogger<T>`/`FakeLogRecord` (`Microsoft.Extensions.Diagnostics.Testing`),
TUnit's `TUnit.Mocks.Logging`, real `alexa-vox-craft`/`structured-logging`
consumer friction, and four empirical pre-ADR validation spikes (scope
ordering, structured-state extraction across both `LogInformation` and
`[LoggerMessage]` call styles, stage-6 provider precedence, `CallVerifier`
verification ergonomics) — lives in
`docs/research/0013-compono-logging-testing-design-research.md`. This ADR
summarizes and settles the decision content from that research; it does
not re-derive the evidence.

**The core finding**: no existing Compono package reaches this well.
`Compono.TestDoubles`'s per-interface generated doubles model verification
as arbitrary-member argument matching — `ILogger`'s one real member
(`Log<TState>`) hides everything useful (level, message, structured
properties, scope) inside `TState`/`Func<TState,Exception?,string>`, which
argument-matching doesn't decode (confirmed structurally possible via the
`ILoggerLike` generator-stress-test mirror behind ADR-0044 Amendment 1, but
still short of logging semantics). `Compono.NSubstitute` gives a bare
substitute with no captured-entry model at all. Two real consumers
(`structured-logging`, `alexa-vox-craft`) already hand-roll or manually
`Register<ILogger<T>>(...)` around this gap. `Compono.Http`
([ADR-0051](0051-compono-http-handler-based-testing-package.md)) is the
established precedent for exactly this situation: a specialized BCL
testing seam gets a purpose-built, reflection-free, minimal-dependency
package rather than being forced through the generic double-generation
path.

## Decision Drivers

- No reflection by default ([ADR-0001](0001-source-generation-first.md)) —
  `ILogger<T>`'s member surface is fixed and small; a hand-written generic
  class covers every `T`, so neither reflection nor source generation is
  needed to reach it.
- Core `Compono` must never know about integration packages;
  `Compono.Logging` must not teach `Compono.TestDoubles`/`Compono.NSubstitute`
  anything about `ILogger`.
- Composition-over-inheritance for the object model
  (`design-decisions.md` rule 2) — rejects `LayeredCraft.StructuredLogging`'s
  `TestLogger<T> : TestLogger` inheritance shape.
- Minimal package graph, matching `Compono.Http`'s own driver: a consumer
  using only plain object composition shouldn't be forced to pull in
  `Microsoft.Extensions.Logging` concrete implementations,
  `Microsoft.Extensions.DependencyInjection`, or
  `Microsoft.Extensions.Diagnostics.Testing`.
- AOT/trimming compatibility, honestly represented, following
  `Compono.Http`'s precedent of setting `<IsAotCompatible>true</IsAotCompatible>`
  rather than leaving it silent.
- Real, repeated `alexa-vox-craft`/`structured-logging` friction
  (hand-constructed loggers never composed; manual per-closed-type
  `Register<ILogger<T>>(...)` workarounds) as the evidence bar, per
  RESEARCH-0013 §5.
- Reuse existing Compono primitives wherever the evidence supports it
  (`CallVerifier`, `[Shared]`, stage-6 `ICompositionValueProvider`,
  first-registered-wins precedence) rather than inventing parallel
  mechanisms — the standing rule this whole research/spike pass was run to
  protect.
- Naming precedent (`Compono.<Ecosystem>`) — `Compono.Logging`, matching
  `Compono.Http`/`Compono.Bogus`/`Compono.TestDoubles`, not a
  narrower/library-specific name.

## Considered Options

**Package/routing:**
1. Admit `Compono.Logging` — a hand-written, reflection-free package
   following `Compono.Http`'s shape, depending only on core `Compono` +
   `Microsoft.Extensions.Logging.Abstractions`.
2. Route `ILogger`/`ILogger<T>` through `Compono.TestDoubles`'s generated
   doubles.
3. Depend on `Microsoft.Extensions.Diagnostics.Testing`'s `FakeLogger<T>`
   directly and build only a thin Compono-composition/verify layer on top.

**Captured-entry model:**
1. Raw `object? State` only (`LayeredCraft.StructuredLogging`'s shape).
2. Raw `State` **plus** derived, first-class `Properties`/`MessageTemplate`
   (Microsoft's `FakeLogRecord` shape).
3. Structured-only — drop raw `State` entirely.

**Verification API shape:**
1. Two-verb chain: `logger.VerifyLog()....Verify().Once()`, exposing
   `CallVerifier` as a visible intermediate type.
2. One-verb chain: `logger.Verify()....Once()`, with `Once()`/`Never()`/
   `Exactly(n)` living directly on the filter builder as thin forwarders to
   `CallVerifier`.

**Scope tracking:**
1. No-op (`LayeredCraft.StructuredLogging`'s current shape).
2. Hand-roll a custom scope stack.
3. Reuse `Microsoft.Extensions.Logging.LoggerExternalScopeProvider`.

**Stage-6 precedence between `UseLogging()` and
`UseNSubstitute()`/`UseGeneratedTestDoubles()`:**
1. Follow the existing, `Accepted` registration-order rule
   ([ADR-0024](0024-public-provider-extensibility-model.md)/
   [ADR-0043](0043-compono-generated-test-doubles-design.md)) — document
   which order to register in, add no new mechanism.
2. Add provider priority/specificity scoring to the pipeline so
   `Compono.Logging` always wins for `ILogger`/`ILogger<T>` regardless of
   registration order.

## Decision Outcome

**Chosen, per axis**: package/routing — **Option 1** (admit
`Compono.Logging`, `Compono.Http`-shaped); captured-entry model —
**Option 2** (raw + derived); verification API — **Option 2** (one-verb
`Verify()` chain); scope tracking — **Option 3**
(`LoggerExternalScopeProvider` reuse); stage-6 precedence — **Option 1**
(existing registration-order rule, documented explicitly, no pipeline
change).

### Package identity and dependency graph

```
Compono.Logging
    -> Compono                                       (reuses CallVerifier — no generator dependency)
    -> Microsoft.Extensions.Logging.Abstractions      (ILogger/ILogger<T>/LogLevel/EventId/LoggerExternalScopeProvider)
```

- **Name: `Compono.Logging`**, matching the `Compono.<Ecosystem>` naming
  precedent `Compono.Http`/`Compono.Bogus` already established, not a
  narrower name tied to one BCL type.
- **No dependency on `Compono.TestDoubles` or `Compono.NSubstitute`** —
  confirmed unnecessary (RESEARCH-0013 §1); `ILogger<T>`'s fixed, small
  member surface needs neither generated doubles nor a runtime proxy.
- **No dependency on `Microsoft.Extensions.Diagnostics.Testing`** —
  independence, not adoption, per RESEARCH-0013 §3/§11: unconfirmed
  AOT/trimming story, heavier transitive dependency surface (the whole
  fake-metrics/fake-tracing family), and `Compono.Logging` needs to own
  its `CapturedLogEntry` shape to fit Compono's own verification idioms
  (`CallVerifier`, `[Shared]`) rather than adapt someone else's shape
  after the fact. `FakeLogRecord`'s raw+derived **model** is adopted; the
  **package** is not depended on — the adopted-vs-changed distinction
  `design-decisions.md` rule 5 requires.
- **No dependency on `Microsoft.Extensions.Logging` (the concrete
  implementation package)** or `Microsoft.Extensions.DependencyInjection`
  — `Abstractions` alone is sufficient and is what every real ASP.NET
  Core/worker-service/console-host consumer already transitively
  references.
- **No assertion-framework dependency** — direct inspection
  (`GetCapturedEntries()`) and `Verify()` both throw/report via core
  `Compono` types only, matching `LayeredCraft.StructuredLogging`'s
  existing (correct) choice.

### Core abstraction and public API

A hand-written, non-generic captor plus one hand-written generic captor —
not source-generated, not one-type-per-interface, because `ILogger<T>`'s
member surface is fixed and small (unlike `Compono.TestDoubles`'s
per-interface generation, which exists because arbitrary interfaces have
arbitrary members):

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
    public IReadOnlyList<KeyValuePair<string, object?>>? Properties { get; } // see "Properties nullability", below
    public string? MessageTemplate { get; }                                  // Properties' "{OriginalFormat}" entry, surfaced by name; null if Properties is null or that key is absent
    public IReadOnlyList<object> Scopes { get; }                             // outermost→innermost snapshot, active at the moment this entry was logged
    public DateTimeOffset Timestamp { get; }
}

// Implemented once; works for both ILogger and every closed ILogger<T> — no per-T generation.
public sealed class CapturingLogger : ILogger { /* ... */ }
public sealed class CapturingLogger<T> : ILogger<T> { /* composes a CapturingLogger internally, does not inherit it */ }

public static class LoggerTestingExtensions
{
    public static IReadOnlyList<CapturedLogEntry> GetCapturedEntries(this ILogger logger);
    public static CapturedLogEntry? GetLastCapturedEntry(this ILogger logger);
    public static void ClearCapturedEntries(this ILogger logger);

    public static LogVerificationBuilder Verify(this ILogger logger);
}

public sealed class LogVerificationBuilder
{
    public LogVerificationBuilder AtLevel(LogLevel level);
    public LogVerificationBuilder WithEventId(EventId eventId);
    public LogVerificationBuilder WithException<TException>() where TException : Exception;
    public LogVerificationBuilder WithMessageContaining(string text);
    public LogVerificationBuilder Matching(Func<CapturedLogEntry, bool> predicate);

    // Thin one-line forwarders to core Compono.CallVerifier, built from the
    // filtered match count right here. CallVerifier is never exposed as an
    // intermediate return type, and no new Times/count abstraction exists.
    public void Once();
    public void Never();
    public void Exactly(int times);
}
```

```csharp
// Composed, no manual registration:
var composer = Composer.Create(builder => builder.UseLogging());
var service = composer.Create<OrderService>();   // OrderService(ILogger<OrderService> logger, ...)

// Asserting against the composed instance, via existing [Shared]:
[Theory]
[Compose]
public void RetriesLogAWarning([Shared] ILogger<OrderService> logger, OrderService service)
{
    service.PlaceOrder(...);
    logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once();
}

// Direct inspection:
var entries = logger.GetCapturedEntries();
var failure = entries.Single(e => e.LogLevel == LogLevel.Error);
Assert.Equal("OrderId", failure.Properties?.First().Key);
```

**Verification API — one verb, not two.** The pre-ADR `CallVerifier`
ergonomics spike (RESEARCH-0013 §12 item 4) confirmed `CallVerifier`
(`src/Compono/CallVerifier.cs`) is a `readonly struct` built from an
already-computed count with `void`-returning terminals, and that the real
`Compono.TestDoubles` precedent uses "Verify" exactly once per chain
(`repository.Verify().Save().Once()`,
`test/Compono.TestDoubles.SampleTests/VerificationTests.cs:21-23`).
`Compono.Logging` follows that exactly: `logger.Verify()` is the entry
point; `AtLevel`/`WithEventId`/`WithException<T>`/`WithMessageContaining`/
`Matching` filter; `Once()`/`Never()`/`Exactly(n)` are thin forwarders
built from the filtered match count. This is confirmed still zero new
abstraction — three one-line delegations to a struct with three members —
and rejects the originally-proposed `VerifyLog()....Verify().Once()`
two-verb shape.

### Failure semantics for a non-`Compono.Logging` `ILogger`

`GetCapturedEntries()`/`GetLastCapturedEntry()`/`ClearCapturedEntries()`/
`Verify()` are extension methods on `ILogger`, deliberately, because a
composed dependency is normally typed `ILogger<T>`, never
`CapturingLogger<T>` (RESEARCH-0013 §7's usage examples). This means they
are callable on *any* `ILogger` — `NullLogger<T>.Instance`, an
`NSubstitute` substitute, a `Compono.TestDoubles`-generated double, a real
production logger, or any third-party implementation — not only on a
`CapturingLogger`/`CapturingLogger<T>`. Left unspecified, this is exactly
the failure mode this ADR's own stage-6 precedence decision (above) can
trigger silently: if `UseNSubstitute()`/`UseGeneratedTestDoubles()` wins
registration order over `UseLogging()`, a consumer's `logger.Verify()`
call would otherwise hit an `ILogger` with no Compono.Logging capture
state behind it at all.

**Decision**: calling any of the four extension methods on an `ILogger`
that is not a `Compono.Logging` capturing logger throws
`InvalidOperationException` immediately, with a message stating plainly
that the given `ILogger` is not a `Compono.Logging` capturing logger and
pointing the consumer at `UseLogging()` and the documented stage-6
registration-order requirement (the "Runtime activation and precedence"
section above) as the likely cause. This is a deliberate, diagnostic
failure — never an `InvalidCastException`, an empty/default result, or
silently-undefined behavior — precisely because the registration-order
precedence this ADR accepts can otherwise make a wrong-provider mistake
invisible until an assertion inexplicably fails or passes for the wrong
reason.

**Mechanism, kept internal, no new public abstraction**: `CapturingLogger`
and `CapturingLogger<T>` both implement one small internal marker
interface (working name `ICapturingLoggerFacade`, `internal` to
`Compono.Logging`) exposing read access to the shared `LogEntryCollector`
and the filtered-`Verify()` support it needs. Each of the four extension
methods pattern-matches its `ILogger` parameter against that internal
interface (`logger is ICapturingLoggerFacade facade`) and throws the
`InvalidOperationException` above on a non-match. This costs nothing
extra at the public API surface declared earlier in this ADR — no new
public type, no change to `CapturingLogger`/`CapturingLogger<T>`'s public
shape — it is purely how the existing extension methods locate their
already-declared internal state.

### `MinimumLevel` semantics

`LoggingOptions.MinimumLevel` was declared without specifying its complete
observable behavior. Left implicit, two readings are both plausible:
(a) `IsEnabled(level)` reports `false` below the threshold but
`Log<TState>` still records the entry regardless, or (b) `MinimumLevel` is
real filtering — nothing below it is ever captured.

**Decision: (b) — real filtering.** `CapturingLogger`/`CapturingLogger<T>`:

- `IsEnabled(LogLevel level)` returns
  `level != LogLevel.None && MinimumLevel != LogLevel.None && level >= MinimumLevel`
  (or another implementation equally clear about the same behavior) — not
  the bare `level >= MinimumLevel` an earlier draft of this section used.
  `LogLevel.None` is numerically greater than every ordinary level but
  semantically means "logging disabled," not "the highest level," so a
  naive `>=` comparison would incorrectly report `IsEnabled(LogLevel.None)`
  as `true` whenever `MinimumLevel` is anything below `None` (e.g. its
  default, `LogLevel.Trace`), and would incorrectly let `LogLevel.None`
  itself through as a capturable level rather than treating it as "nothing
  is enabled." The explicit rule: `LogLevel.None` is never enabled or
  captured, regardless of `MinimumLevel`; setting `MinimumLevel =
  LogLevel.None` disables all logging entirely (no ordinary level is
  enabled); otherwise, an ordinary level is enabled exactly when it is at
  or above `MinimumLevel`, unchanged from the original rule.
- `Log<TState>(...)` itself checks `IsEnabled(logLevel)` first and is a
  complete no-op (no `CapturedLogEntry` created, nothing appended to the
  collector) when it returns `false` — defense-in-depth inside the
  captor itself, not a reliance on every caller checking `IsEnabled`
  first. Most real `ILogger` extension methods (`LogInformation`, etc.)
  already check `IsEnabled` before calling `Log<TState>`, but `Log<TState>`
  can be, and in generated/manual code sometimes is, called directly —
  the captor must not depend on caller discipline for a "not captured"
  guarantee to hold.

This means an entry below `MinimumLevel` never appears in
`GetCapturedEntries()`, is never the "last" entry `GetLastCapturedEntry()`
can return, and is invisible to `Verify()`'s match count — `Once()`/
`Never()`/`Exactly(n)` all operate purely over what was actually captured.
`MinimumLevel` is therefore genuine logger filtering, matching real
`ILogger` provider behavior (a console/file logger below its configured
level truly never writes a line), not merely an `IsEnabled()` opinion
layered on top of an otherwise-complete capture stream. This is
consistent with, not contradicted by, `FakeLogger`'s own richer
per-level `ControlLevel` mechanism (RESEARCH-0013 §3) — `FakeLogger`'s
disabled levels are also not captured, just with more granular per-level
control than `Compono.Logging`'s single `MinimumLevel` threshold provides.
No `ControlLevel`-style per-level toggle is added here — out of scope,
unchanged from this ADR's existing "Explicit initial-package boundaries"
section.

**Validation expectations** the implementation must satisfy (focused
behavioral checks, not a full test plan — that belongs in the eventual
implementation plan):

- `MinimumLevel = LogLevel.Trace` (the default): ordinary levels
  (`Trace` through `Critical`) are all enabled and captured.
- `MinimumLevel = LogLevel.Warning`: `Trace`/`Debug`/`Information` are
  excluded — not enabled, not captured; `Warning`/`Error`/`Critical` are
  captured.
- `MinimumLevel = LogLevel.None`: no ordinary level is enabled; nothing is
  ever captured, regardless of what level is logged at.
- A direct `Log<TState>` call passing `LogLevel.None` as the level
  captures nothing — `LogLevel.None` is never enabled, independent of
  `MinimumLevel`'s own value, including when `MinimumLevel` is `Trace`.

### Construction semantics for `CapturingLogger`/`CapturingLogger<T>`

Whether these two public types are meant for direct consumer construction
or only ever produced by `LoggingProvider` was left unstated.

**Decision**: both are directly, publicly constructible —

```csharp
public sealed class CapturingLogger : ILogger
{
    public CapturingLogger(LoggingOptions? options = null);
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    public CapturingLogger(LoggingOptions? options = null);
}
```

reusing the already-declared `LoggingOptions` type (no new configuration
surface) rather than exposing `MinimumLevel`/other settings as separate
constructor parameters. `LoggingProvider` itself calls this same public
constructor internally — there is exactly one construction path, not a
composed-only path plus a separate internal-only path.

This follows Microsoft's own confirmed `FakeLogger<T>` precedent
(RESEARCH-0013 §3: `new FakeLogger<T>()` is a complete, standalone
`ILogger<T>`, "no factory needed for the common case") rather than
restricting construction to composition. It's the smallest coherent
choice available given the real evidence on both sides: `alexa-vox-craft`/
`structured-logging`'s current pattern is manual construction outside
Compono entirely (`new TestLogger()`), and while their intended
`Compono.Logging` migration path is `UseLogging()` composition
(RESEARCH-0013 §5), nothing about that migration requires *forbidding*
direct construction — a consumer testing a class in isolation without any
Compono composition at all (no `[Compose]`, no `Composer`) is a legitimate,
already-evidenced shape (every current `structured-logging` test does
exactly this today), and gating it behind composition would regress that
case rather than improve it.

This decision does **not** reopen the earlier rejection of a
category-string constructor for the non-generic `CapturingLogger`: its
public constructor takes only the existing `LoggingOptions?`, no `string
category` parameter — category remains unset for a directly-constructed
non-generic `CapturingLogger`, unchanged from this ADR's existing
"Non-generic `ILogger`" decision.

**`CapturingLogger<T>` composes rather than inherits `CapturingLogger`** —
a shared internal `LogEntryCollector` (lock + `List<CapturedLogEntry>` +
`LoggerExternalScopeProvider`) is held by both; the two public types are
thin `ILogger`/`ILogger<T>` façades over it. This is `design-decisions.md`
rule 2 applied directly, and deliberately deviates from
`LayeredCraft.StructuredLogging`'s `TestLogger<T> : TestLogger`
inheritance shape (RESEARCH-0013 §2 point 4, §11).

**Concurrency**: `LogEntryCollector` is thread-safe — a private lock around
mutation, `.ToArray()`/equivalent snapshot on every read
(`GetCapturedEntries()`, `Verify()`'s internal count, `Matching`'s
predicate evaluation) — following `Compono.Http`'s own already-established
`TestHttpHandler.Requests` pattern (RESEARCH-0013 §2 point 3). This fixes
`LayeredCraft.StructuredLogging`'s confirmed gap (a plain unlocked
`List<LogEntry>`, real data-race risk under concurrent logging).

### Properties nullability

RESEARCH-0013's spike confirmed the **runtime** BCL shape both
`FormattedLogValues` (ordinary `LogInformation(...)` calls) and
`LoggerMessageState` (every `[LoggerMessage]` source-generated call) use
is `IReadOnlyList<KeyValuePair<string, object>>` — the value slot itself is
declared non-nullable `object` in the BCL's own interface. Freezing
`Compono.Logging`'s own public signature to match that exactly
(`KeyValuePair<string, object>`) would assert a **stronger** guarantee
than is actually true for a consumer of `CapturedLogEntry`: a structured
logging call can legitimately pass a `null` argument
(`logger.LogInformation("User {UserId}", (int?)null)` or any
reference-typed value that happens to be `null`) — the BCL's `object` slot
still holds a boxed `null` reference in that case, C#'s non-nullable
annotation on an interface describes the *declared* type, not a runtime
non-null guarantee, and nothing in `FormattedLogValues`/`LoggerMessageState`
prevents it.

**Decision**: `CapturedLogEntry.Properties` is declared
`IReadOnlyList<KeyValuePair<string, object?>>?` — nullable value type, on
top of the already-nullable list itself (non-null only when `State`
implements the pattern-matched interface, per RESEARCH-0013 §7/§10). This
is a **public API/nullability judgment call this ADR makes**, not a
reopening of the structured-state architecture the spike already settled:
the extraction mechanism (`state is IReadOnlyList<KeyValuePair<string, object>> pairs`,
a single reflection-free pattern match, RESEARCH-0013 §12 item 2) is
unchanged and unaffected — only the public-facing annotation on the
already-boxed `object` values gains `?`, so a Compono consumer who does
`entry.Properties?.First().Value` gets a compiler-enforced null-check
reminder instead of a contract that quietly promises something the BCL
itself doesn't guarantee. This is the more truthful contract for
Compono's own nullable-reference-type-enabled consumers, at zero
implementation cost (the pattern-match's element type still assigns
without a cast either way — `object` values flow into an
`object?`-typed list positionally with no conversion needed).

### Non-generic `ILogger`

Real consumer evidence (RESEARCH-0013 §5) is **100% `ILogger<T>`** —
`structured-logging` and `alexa-vox-craft` both only ever construct or
compose a category-typed logger. No real evidence anywhere in RESEARCH-0013
demands a standalone, category-string-based non-generic `CapturingLogger`
constructor.

**Decision**: `CapturingLogger : ILogger` exists (it's the type
`CapturingLogger<T>` composes internally, and `LoggingProvider` produces
one directly for a bare `ILogger` composition request, category unset/
empty), but **no public category-string constructor is added for v1** —
deferred until a real non-generic `ILogger` consumer case surfaces,
consistent with this ADR's "smallest defensible decision" driver. Adding
one later, if evidence appears, is additive and non-breaking.

### `ILoggerFactory`: out of scope

No native `ILoggerFactory` support in v1. No real consumer evidence
(RESEARCH-0013 §5/§6) requests one — every real case is direct
constructor-injected `ILogger<T>`. Compono.Logging is not becoming general
logging infrastructure; a consumer needing an `ILoggerFactory` composes one
by hand or via `Compono.TestDoubles`/`Compono.NSubstitute` against the
plain single-method interface, exactly the pattern `Compono.Http`'s own
ADR established for `IHttpClientFactory`.

### Runtime activation and precedence

`UseLogging()` registers `LoggingProvider : ICompositionValueProvider`
into **stage 6** (test-double providers) via the existing
[ADR-0024](0024-public-provider-extensibility-model.md)
`AddTestDoubleProvider` extension point — zero new engine/pipeline
mechanism. `LoggingProvider` claims `RequestedType == typeof(ILogger)` or
any closed `ILogger<T>` (a static `Type`/`GetGenericTypeDefinition()`
check, the same pattern `NSubstituteProvider` already uses), returns
`NotHandled` otherwise.

**Precedence, if `Compono.Logging` and `Compono.NSubstitute`/
`Compono.TestDoubles` are all installed and enabled**: this is the same
structural situation ADR-0043 already settled between
`GeneratedTestDoubleProvider` and `NSubstituteProvider` — Compono has no
priority/specificity scoring anywhere in the pipeline
(`CompositionContext.TryProviders`,
`src/Compono/CompositionContext.cs:921-944`, confirmed by source and by
this ADR's own pre-ADR spike to be a hard first-registered-wins
short-circuit). `Compono.Logging` follows that exact, already-`Accepted`
pattern rather than inventing a second one: **`UseLogging()` must be
registered before `UseNSubstitute()`/`UseGeneratedTestDoubles()`** for
`ILogger`/`ILogger<T>` to resolve to a capturing logger. A consumer who
registers both in the opposite order gets NSubstitute/generated-double
behavior for `ILogger<T>` instead — an explicit, documented consequence of
registration order, consistent with ADR-0024's general rule and ADR-0043's
own precedent, not a new diagnostic or a pipeline defect.

**No provider priority/specificity mechanism is introduced by this ADR.**
The pre-ADR spike explicitly confirmed no pipeline/architecture change is
needed or warranted to resolve this — the existing pattern already
answers it.

### Captured-entry semantics

- **`Message`** is pre-formatted via the caller's own
  `formatter(state, exception)` — never re-derived by `Compono.Logging`,
  so it can't diverge from what a real logging provider would have
  produced (carried forward unchanged from `LayeredCraft.StructuredLogging`'s
  one already-correct design point, RESEARCH-0013 §2).
- **`State`** is always populated (the raw, boxed `TState`) — the escape
  hatch for a shape `Properties`/`MessageTemplate` doesn't cover.
- **`Properties`**/**`MessageTemplate`** are derived, not raw: a single
  `state is IReadOnlyList<KeyValuePair<string, object>> pairs` pattern
  match, confirmed (RESEARCH-0013 §12 item 2) to cover both ordinary
  `LogInformation(...)` calls (`FormattedLogValues`) and every
  `[LoggerMessage]` source-generated call (`LoggerMessageState` — one
  shared BCL type reused across every call site, not per-call-site) with
  no special-casing. `MessageTemplate` surfaces the `"{OriginalFormat}"`
  entry by name.
- **`Scopes`** is an outermost-to-innermost snapshot of every scope active
  at the moment the entry was captured — see next section.

### Scope semantics

`LogEntryCollector` owns one
`Microsoft.Extensions.Logging.LoggerExternalScopeProvider` instance
(namespace `Microsoft.Extensions.Logging`, confirmed public with a public
parameterless constructor, RESEARCH-0013 §9/§12 item 1).
`CapturingLogger`/`CapturingLogger<T>.BeginScope<TState>` calls
`scopeProvider.Push(state)` and returns its real `IDisposable` directly
(actual pop-on-dispose semantics, not the `LayeredCraft.StructuredLogging`
no-op). At `Log<TState>` time, before constructing the `CapturedLogEntry`,
`scopeProvider.ForEachScope(...)` snapshots every currently-active scope.

This is treated as **empirically settled, not an open design question**:
`ForEachScope` enumerates outermost-first (confirmed against 3 levels of
nesting, cross-checked directly against a real `FakeLogger<T>`/
`FakeLogRecord.Scopes`, which orders the same way);
`AsyncLocal<>`-backed isolation is real, not just documented (visible
across `await` in the same logical call, invisible to a sibling `Task.Run`
started before the scope push, visible to one started after — expected
forward flow, not a leak). `CapturedLogEntry.Scopes` is specified as
outermost-to-innermost with no remaining uncertainty. No custom scope
stack is implemented — reusing the BCL's own dependency-free, AOT-safe
mechanism is sufficient and matches what real logging providers (e.g. the
console logger) already delegate to.

### AOT/trimming and dependency implications

Zero reflection anywhere in this design. `LoggingProvider`'s match check
is a static `Type` comparison (the same pattern already proven AOT-safe as
`NSubstituteProvider`'s own match check). `CapturingLogger`/
`CapturingLogger<T>` are ordinary hand-written classes — no dynamic proxy
generation (unlike `Compono.NSubstitute`'s accepted, necessary exception to
the no-reflection default), no source generation (unlike
`Compono.TestDoubles`) — because `ILogger<T>`'s member surface is fixed
and small enough for one hand-written generic class to cover every `T`.
Structured-property extraction is a single reflection-free pattern match,
confirmed identical across both logging call styles (previous section).

`Compono.Logging.csproj` sets `<IsAotCompatible>true</IsAotCompatible>`
explicitly, following `Compono.Http`'s precedent — the mechanism that
makes the trim/AOT analyzer actually verify "no reflection anywhere" at
consumer call sites rather than take the claim on faith.

### `[Shared]` — used as-is, not redesigned here

`Compono.Logging` uses `[Shared]` exactly as it exists today
([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
for "get the exact composed logger instance back to assert against" — this
already works today against a manually-`Register`ed `ILogger<T>`
(`alexa-vox-craft`'s `PerformanceLoggingBehaviorTests.cs:15-21`,
RESEARCH-0013 §5), and a provider-resolved value participates in
shared-value scope identically to a registered or built-in one. No new
mechanism, no `Share<T>()`, no new attribute is introduced by this ADR.

This ADR does, however, **record** the ergonomics friction this pattern
carries as additional evidence for a broader, already-recognized, pre-1.0
Compono requirement: a consumer sometimes needs a parameter *purely* to
obtain the graph-owned instance for inspection
(`[Shared] ILogger<OrderService> logger, OrderService service` — `logger`
here exists only to observe, not because it's independently under test).
That broader ergonomics question is **explicitly deferred** to its own,
separate design investigation/ADR, immediately following this one.
`Compono.Logging` neither blocks on nor attempts to solve it locally.

### Explicit initial-package boundaries

Out of scope for v1, absent new evidence:

- `ILoggerFactory` composition.
- Serilog-specific or other logging-provider-specific behavior.
- Test-runner output capture/routing (a different problem —
  `TUnit.Logging.Microsoft`'s concern, not behavior verification).
- DI integration (`Microsoft.Extensions.DependencyInjection` is not a
  dependency).
- Cross-scope structured-property flattening/searching — a scope's own
  structured values are reachable via the same
  `IReadOnlyList<KeyValuePair<...>>` cast as message state; no bespoke API.
- `FakeLogger`-style per-level `ControlLevel` toggling — a single
  `LoggingOptions.MinimumLevel` threshold covers every real case seen in
  RESEARCH-0013 §5.
- Dependency on `Microsoft.Extensions.Diagnostics.Testing`.
- Dependency on `Compono.TestDoubles` or `Compono.NSubstitute`.

### Documentation and skill/reference synchronization (required, part of completion criteria)

`Compono.Logging` is **not complete** merely because its implementation
and tests pass. The eventual implementation plan **must** identify and
update, at minimum, the following concrete, already-existing surfaces
(inspected directly in this repo, not assumed):

- **`docs/packages/compono-logging.md`** (new) — a package doc following
  the existing `docs/packages/compono-http.md` shape (inventory row,
  when-to-use guidance).
- **`docs/packages/index.md`** — add `Compono.Logging`'s row to the
  package-discovery table (matches the existing `Compono.Http` row added
  under ADR-0051).
- **`README.md`** — add `Compono.Logging` to the package/badge table
  (matches the existing `Compono.Http` row).
- **`docs/architecture/current/provider-pipeline.md`** (or wherever
  stage-6 provider registration is currently documented) — document
  `UseLogging()`'s registration and the `UseLogging()`-before-
  `UseNSubstitute()`/`UseGeneratedTestDoubles()` precedence rule, matching
  how ADR-0043's precedence rule is already documented for the existing
  providers.
- **`docs/public-api.md`** — reflect the new `UseLogging()`/`CapturingLogger`/
  `Verify()` public surface, consistent with how other integration
  packages' public API is represented there.
- **Examples showing normal `ILogger<T>` composition and verification** —
  wherever this repo's existing per-package usage examples live (the
  `docs/packages/compono-http.md` precedent, and/or `samples/` if that's
  where `Compono.Http`'s own examples ended up — confirm the actual
  location during planning rather than assuming).
- **`skills/compono/SKILL.md`** — this is the shipped skill real coding
  agents load for Compono work. It currently enumerates
  `Compono.XunitV3`/`Compono.TUnit`/`Compono.NSubstitute`/`Compono.Bogus`/
  `Compono.TestDoubles`/`Compono.DependencyInjection`/`Compono.Http` (line
  8), has a per-package `.csproj`-detection table with a row per package
  (lines 50-54, e.g. `<PackageReference Include="Compono.Http"` →
  `references/http.md`), and a references-index table (lines 365-372).
  All three need a `Compono.Logging` row/entry, plus a new
  `references/logging.md` file (matching `references/http.md`'s existing
  shape) covering `UseLogging()`, `CapturingLogger`, structured
  properties/`MessageTemplate`, scope semantics, and `Verify()`.
- **`skills/compono-evals`** — this repo already runs graded before/after
  evals for the shipped skill (`skills/compono-evals/evals.json`,
  `benchmarks/`). The plan must add at least one eval exercising a
  Compono.Logging scenario (e.g. "compose an `ILogger<T>` and assert a
  warning was logged") and confirm it passes with the updated skill,
  proving the skill guidance actually reflects the shipped API rather than
  asserting it by description alone.
- **`docs/adr/README.md`** — index row for this ADR (mechanical, per
  `docs/adr/README.md`'s own rules).

Documentation/skill synchronization is part of `Compono.Logging`'s
validation gate — the implementation plan should not be considered ready
to mark `Done` with any of the above left unstarted.

### Dogfooding

`LayeredCraft.StructuredLogging` and `alexa-vox-craft` are identified as
the appropriate eventual dogfood targets — both already carry concrete,
evidenced friction this design targets directly (RESEARCH-0013 §5). This
ADR does not modify either repository. The eventual implementation plan
should validate against freshly packed local `Compono.Logging` packages
using this repo's established `scripts/dogfood-validate.sh` workflow, with
an explicit goal of proving that `structured-logging`'s hand-constructed
`TestLogger` and `alexa-vox-craft`'s manual
`Register<ILogger<PerformanceLoggingBehavior>>(...)`/`NullLogger<T>.Instance`
workaround can both be replaced by ordinary `UseLogging()` composition
without recreating consumer-specific abstractions on top.

### Positive Consequences

- Removes real, evidenced friction from two dogfood consumers: hand-built
  loggers that never compose (`structured-logging`), and manual
  per-closed-type `Register<ILogger<T>>(...)`/fallback-`NullLogger<T>`
  workarounds (`alexa-vox-craft`).
- Extends zero new core Compono mechanisms — `CallVerifier`, `[Shared]`,
  stage-6 `AddTestDoubleProvider`, and first-registered-wins precedence are
  all reused exactly as they exist today.
- Fixes concrete correctness gaps identified in
  `LayeredCraft.StructuredLogging`'s existing implementation: real scope
  tracking instead of a no-op, thread-safe capture instead of an unlocked
  `List<T>`, structured-property extraction instead of punting raw `object`
  state to the consumer.
- Keeps the dependency graph minimal — a consumer using only plain object
  composition pulls in nothing beyond `Compono` itself; a
  `Microsoft.Extensions.Logging.Abstractions`-only footprint matches what
  every real logging consumer already references.
- Confirms and documents, with empirical evidence rather than assumption,
  that BCL structured-state extraction is uniform across both ordinary and
  `[LoggerMessage]` source-generated logging — future Compono.Logging work
  doesn't need to special-case source-gen call sites.

### Negative Consequences

- `Compono.Logging` is Compono's second non-generated, hand-written-runtime
  integration package (after `Compono.Http`) — a deliberate,
  evidence-based choice, but its implementation/maintenance shape differs
  from `Compono.TestDoubles`' generator-emitted code; this should be
  called out in its own package documentation so it isn't mistaken for an
  inconsistency.
- The `UseLogging()`-before-`UseNSubstitute()`/`UseGeneratedTestDoubles()`
  registration-order requirement is an easy first-use mistake (a consumer
  who registers in the opposite order silently gets substitute/
  generated-double behavior for `ILogger<T>` instead of a capturing
  logger, with no diagnostic) — mitigated by explicit documentation
  (skill/reference/package-doc rows above), matching the same accepted
  trade-off ADR-0043 already made for `Compono.TestDoubles`/
  `Compono.NSubstitute`.
- `CapturedLogEntry.Properties`'s nullable-value annotation
  (`KeyValuePair<string, object?>`) is a deliberately more conservative
  contract than the BCL's own non-nullable-`object` runtime shape — a
  minor, accepted asymmetry favoring honesty about nullable structured
  values over exact BCL signature mirroring.
- No native `ILoggerFactory` support means a consumer with genuine
  multi-category factory usage gets no first-class help from
  `Compono.Logging` yet — deferred until real evidence demands it, the
  same trade-off `Compono.Http` accepted for `IHttpClientFactory`.
- The `[Shared]` ergonomics gap this design surfaces again is explicitly
  left unsolved by this ADR — a real, acknowledged rough edge until its
  own follow-up design work lands.

## Pros and Cons of the Options

### Package/routing

**Option 1 (admit `Compono.Logging`, chosen)**
- Good, because it matches an evidence-based, already-accepted precedent
  (`Compono.Http`) for exactly this kind of specialized-BCL-seam gap.
- Good, because the correct implementation needs zero reflection and is
  AOT-safe by construction — no architectural conflict.
- Bad, because it's a new package to design, ship, and maintain with no
  generator to lean on.

**Option 2 (route through `Compono.TestDoubles`)**
- Good, because it avoids a new package.
- Bad, because the generator's argument-matching verification model
  cannot decode `Log<TState>`'s logging semantics (level, message,
  structured properties) — proven structurally possible to generate a
  correct double shape (ADR-0044 Amendment 1's `ILoggerLike` case) but
  still short of what a consumer actually needs to assert.

**Option 3 (depend on `FakeLogger<T>` directly)**
- Good, because it reuses Microsoft's own, already-shipped captured-entry
  model instead of building one from scratch.
- Bad, because its AOT/trimming compatibility is unconfirmed and its
  dependency surface is heavier than logging alone needs (the whole
  fake-metrics/fake-tracing family); also forecloses shaping
  `CapturedLogEntry` around Compono's own verification idioms
  (`CallVerifier`, `[Shared]`) from the start.

### Captured-entry model

**Option 2 (raw + derived, chosen)**
- Good, because it matches Microsoft's own validated `FakeLogRecord` shape
  and keeps the raw escape hatch `LayeredCraft.StructuredLogging` already
  relies on, while fixing its central gap (no structured extraction at
  all).
- Bad, because it's a slightly larger type than either single-purpose
  alternative — judged worth it given both raw and derived access have
  real, distinct evidenced uses.

**Option 1 (raw only)**
- Good, because it's the smallest possible type.
- Bad, because it's exactly the gap this design exists to close — a
  consumer wanting a structured property still has to know the
  `FormattedLogValues`/`"{OriginalFormat}"` internals themselves.

**Option 3 (structured-only, no raw `State`)**
- Good, because it's a cleaner, single-purpose type.
- Bad, because it discards the escape hatch for any `TState` shape the
  pattern match doesn't cover, with no compensating benefit.

### Verification API shape

**Option 2 (one-verb `Verify()`, chosen)**
- Good, because it matches the real, already-shipped
  `Compono.TestDoubles`/`Compono.Http` "Verify" vocabulary exactly
  (`repository.Verify().Save().Once()`, `registration.Verify().Once()`),
  giving Compono one consistent verification idiom product-wide.
- Bad, because none identified — the original two-verb shape had no
  offsetting benefit once `CallVerifier`'s real, minimal shape was
  inspected.

**Option 1 (two-verb `VerifyLog()....Verify()`)**
- Good, because "VerifyLog" reads slightly more explicitly as
  logging-specific in isolation.
- Bad, because it exposes `CallVerifier` as a visible intermediate type
  for no reason, breaks the established one-verb product vocabulary, and
  was rejected outright once the pre-ADR spike inspected `CallVerifier`'s
  actual shape.

### Scope tracking

**Option 3 (`LoggerExternalScopeProvider` reuse, chosen)**
- Good, because it's the exact mechanism real logging providers already
  delegate to, is public, dependency-free, AOT-safe, and its ordering/
  isolation behavior is now empirically confirmed rather than assumed.
- Bad, because none identified relative to the alternatives.

**Option 1 (no-op, matching `LayeredCraft.StructuredLogging`)**
- Good, because it's the simplest possible implementation.
- Bad, because it's a confirmed, real gap in that library — a test cannot
  assert "this log happened inside scope X" at all.

**Option 2 (hand-rolled scope stack)**
- Good, because it would give full control over the exact shape.
- Bad, because it duplicates a BCL mechanism that already does this
  correctly, for no evidenced benefit, and risks subtly different ordering
  semantics than what real logging providers produce.

### Stage-6 precedence

**Option 1 (existing registration-order rule, chosen)**
- Good, because it's already `Accepted` (ADR-0024/ADR-0043), already
  documented, and already the exact structural situation between
  `NSubstituteProvider`/`GeneratedTestDoubleProvider` — reusing it costs
  nothing and keeps one precedence rule across the whole product.
- Bad, because it's an easy first-use mistake (wrong registration order
  silently produces a substitute instead of a capturing logger) —
  mitigated by documentation, the same trade-off already accepted
  elsewhere.

**Option 2 (add priority/specificity scoring)**
- Good, because it would remove the registration-order footgun entirely.
- Bad, because it's a pipeline-architecture change with no prior evidence
  it's needed anywhere in Compono, explicitly out of scope per this ADR's
  own pre-ADR spike instruction not to expand pipeline architecture to
  solve a problem the existing pattern already answers.

## Links

- `docs/research/0013-compono-logging-testing-design-research.md` — the
  full evidence trail and empirical spike results this ADR summarizes.
- [ADR-0001](0001-source-generation-first.md) — no-reflection-by-default
  constraint.
- [ADR-0024](0024-public-provider-extensibility-model.md) — stage-6
  `ICompositionValueProvider` extensibility and the general
  registration-order precedence rule this ADR reuses.
- [ADR-0043](0043-compono-generated-test-doubles-design.md) — the
  "Runtime activation and precedence" pattern this ADR copies verbatim for
  `UseLogging()` vs. `UseNSubstitute()`/`UseGeneratedTestDoubles()`.
- [ADR-0044](0044-compono-testdoubles-v2-overloads-generics-verification.md) —
  Amendment 1's `ILoggerLike` generic-method-overload evidence, cited for
  why routing through `Compono.TestDoubles` was considered and rejected.
- [ADR-0051](0051-compono-http-handler-based-testing-package.md) — the
  direct architectural precedent this ADR follows: a hand-written,
  reflection-free, minimally-dependent package for one specialized BCL
  seam, reusing `CallVerifier` unmodified.
- [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md) —
  `[Shared]` semantics this ADR relies on unchanged.
- `structured-logging` repo
  (`src/LayeredCraft.StructuredLogging/Testing/TestingExtensions.cs`) and
  `alexa-vox-craft` repo (`test/AlexaVoxCraft.MediatR.Tests/TestKit/MediatRTestProfile.cs`,
  `test/AlexaVoxCraft.MediatR.Tests/Pipeline/PerformanceLoggingBehaviorTests.cs`)
  — the dogfood evidence sources.
- `skills/compono/SKILL.md`, `skills/compono-evals/` — the coding-agent
  skill surface this ADR's implementation plan must synchronize.

## Amendment 1 (2026-08-28): Generated closed-generic activation for `ILogger<T>`

The original Decision Outcome text above (including its "Core abstraction
and public API," "Runtime activation and precedence," and "AOT/trimming
and dependency implications" sections) is left exactly as written, per
this repo's amendment convention — this section corrects a real
architectural gap found during implementation planning (PLAN-0055),
before any code was written against it, not a change of mind about
anything already decided.

**What the original ADR assumed.** The original text states
`LoggingProvider`'s match check is "a static `Type`/
`GetGenericTypeDefinition()` comparison" and that `Compono.Logging` needs
"no source generation... because `ILogger<T>`'s member surface is fixed
and small," concluding "zero reflection anywhere in this design." This
conflated two different questions: *recognizing* that a request is
`ILogger<T>`-shaped (a static `Type` comparison — true, unaffected by
this amendment) and *activating* a `CapturingLogger<T>` instance for the
specific, request-only-known `T` (a different problem the original text
never actually addressed).

**What evidence disproved it.** `ICompositionValueProvider.TryProvide`
(`src/Compono/CompositionContext.cs`'s `TryProviders` dispatch) is
irreducibly non-generic: a provider receives `CompositionProviderRequest.RequestedType`
as a bare `Type` and returns `object?`, cast back to the caller's
statically-known `TValue` only *after* the provider returns. A provider
can therefore recognize `ILogger<OrderService>` via `Type` comparison,
but cannot itself construct a `CapturingLogger<OrderService>` — an actual
instance of that closed generic type — without either `Type.MakeGenericType`
+ `Activator.CreateInstance` (reflection) or a source-generated bridge
that closes the generic ahead of time.

A real spike proved the reflection path is exactly the trap it looks
like: `Type.MakeGenericType` compiled and ran under the JIT, but under
`EnableAotAnalyzer`/`IsAotCompatible=true` it produced
`warning IL3050: Using member 'System.Type.MakeGenericType(params Type[])'
which has 'RequiresDynamicCodeAttribute' can break functionality when AOT
compiling` — directly contradicting this ADR's own AOT/trimming
requirement. Covariance of `ILogger<out TCategoryName>` does not rescue
this either — proved by compiling, not assumed: covariance only lets a
*more-derived* `T`'s logger flow to a *less-derived* one
(`ILogger<Derived>` → `ILogger<Base>`), never sideways between two
unrelated composed types, so per-`T` construction remains genuinely
required regardless.

Compono already has direct, `Accepted` precedent for exactly this problem
shape — [ADR-0014](0014-generator-emitted-collection-plans.md)
(`Generator-Emitted Collection Plans Replace the Reflection-Based
Dispatch Bridge`) retracted an analogous reflection bridge
(`MakeGenericMethod`/`CreateDelegate`) for closed collection types
(`List<T>`, `Dictionary<TKey,TValue>`, etc.) for the identical reason —
"a runtime-only-known `Type` needs a closed generic instance" — and
replaced it with generator-emitted code. This ADR follows that same
lesson for `ILogger<T>`, adapted to this package's own architecture
rather than reusing ADR-0014's specific mechanism (see "Rejected
alternatives" below).

### Decision: generated activation glue, not a generated logger

**`CapturingLogger`/`CapturingLogger<T>` remain exactly as originally
decided — hand-written, no generated member implementation, no change to
their public constructors or behavior.** What `Compono.Logging` actually
needs source generation for is narrower and different in kind: closing
`CapturingLogger<T>`'s activation for every closed `ILogger<T>` request
whose `T` is erased to a bare `Type` by the time `LoggingProvider` sees
it. This is activation glue, not logging behavior — the generated code
never decides *how* a `CapturingLogger<T>` behaves, only *that* one gets
constructed for a specific, statically-known `T`.

**Bare, non-generic `ILogger` needs no generated bridge at all** —
`LoggingProvider` constructs `new CapturingLogger(options)` directly,
since there is no generic parameter to close. Only closed `ILogger<T>`
activation goes through the mechanism below.

### Generator ownership

`Compono.Logging` owns a small incremental generator of its own, shipped
as an analyzer asset packed inside `Compono.Logging.nupkg` (the same
packaging model `Compono.csproj` already uses to embed
`Compono.Generators` into `Compono.nupkg` — a `netstandard2.0` project,
`IsPackable=false`, referenced via `<None Pack="true" PackagePath="analyzers/dotnet/cs">`).
**This logic does not move into core `Compono.Generators`.** Confirmed by
inspecting the actual precedent: `Compono.TestDoubles`' compile-time-gated
discovery (`LeafTypeClassifier.IsGeneratedTestDoubleEligible`) does live
in core, but it is entirely framework-agnostic — it recognizes "any
interface," never a specific third-party type name. Recognizing
`Microsoft.Extensions.Logging.ILogger`/`ILogger<T>` specifically is a
materially different, narrower kind of coupling that has no business in
core `Compono`, consistent with this ADR's own "core must never know
about integration packages" driver.

The generator needs **no `PackageReference` to
`Microsoft.Extensions.Logging.Abstractions`** — it recognizes `ILogger`/
`ILogger<T>` via `compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger")`
(and the generic `ILogger`1` form) against the *consumer's* compilation, an
ordinary, dependency-free Roslyn technique for an optionally-present
type: it returns `null`, and the check trivially short-circuits, for any
consumer that never references that package.

### Discovery model

The generator mirrors Compono's existing composition-discovery **model**,
not its code: start from real composition roots
(`Composer.Create<T>()`/`CreateMany<T>()` call sites, `[Composable]`, and
`[Compose]`/`[Compose<TProfile>]` theory-row parameters — the same roots
`TransitiveClosureWalker` already starts from), recurse through each
composed type's selected constructor's dependencies, and classify each as
a provider-resolved leaf or something to recurse into further — the same
leaf-vs-recurse distinction `LeafTypeClassifier.IsProviderResolved`
already embodies. When a dependency is a closed `ILogger<T>`, record `T`
and stop recursing through that dependency (an interface, never itself
walked structurally, same as any other provider-resolved leaf).

**This is deliberately not compilation-wide constructor scanning.**
Confirmed by inspecting the actual walker: Compono's established
discovery model has never scanned every constructor in a compilation
regardless of reachability from a real composition root, and this
generator doesn't introduce that as a new philosophy — scanning
everything would generate unsupported/unnecessary activators for types
nobody ever actually composes through Compono.

**This is bounded duplication, not a new coupling.**
`TransitiveClosureWalker`/`LeafTypeClassifier` remain `internal` to
`Compono.Generators`, with `InternalsVisibleTo` granted only to
`Compono.Generators.Tests`/`Compono.Benchmarks` — **this amendment does
not change that visibility**, and does not expose or share core's
internal discovery state across packages merely to avoid a small,
bounded amount of generator-side duplication (finding the same
root-call-sites and re-deriving the same leaf-vs-recurse rule). The
duplicated surface is small and well-understood, not open-ended.

**Known limitation, recorded, not solved here.** Source-generation
discovery can only emit activators for statically discoverable
composition dependencies — a constructor parameter or theory-row
parameter the walker can actually see. A dependency reached only through
a shape the walker doesn't cover (for example, a hand-written
`Register<T>(...)` factory that itself calls
`context.Resolve<ILogger<TSomething>>()` internally — precisely
[ADR-0052](0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md)'s
still-`Proposed`/deferred "Finding B," nested `context.Resolve<T>()`
discovery) falls outside this generator's reach today. This ADR does not
attempt to solve ADR-0052's open finding — a consumer who hits this gap
gets the diagnostic failure described below, not silent misbehavior.

### Activation registry

Conceptually: a `Type`-keyed registry maps a closed `ILogger<T>` request
to a generated, statically-closed activator that accepts the caller's
runtime `LoggingOptions` and returns `new CapturingLogger<T>(options)`.
Representative shape (the ADR freezes the semantics below, not this
exact collection type):

```csharp
internal static class LoggingFactoryRegistry
{
    public static void Register<TCategory>(Func<LoggingOptions, object> factory);
    public static bool TryCreate(Type requestedType, LoggingOptions options, out object? value);
}
```

Generated registration, per discovered closed category, mirrors
`GeneratedTestDoubleRegistry`'s existing `[ModuleInitializer]` shape —
except the registered delegate takes `LoggingOptions` as a parameter
rather than being zero-argument, since `UseLogging(configure)`'s options
are only known at composer-build time, long after any module initializer
has already run:

```csharp
Register<OrderService>(static options => new CapturingLogger<OrderService>(options));
```

Runtime lookup, from `LoggingProvider`, is a plain `Type`-keyed read:
`TryCreate(request.RequestedType, options, out value)`.

**Binding contracts** (the part of this section that's actually decided,
independent of the exact collection type used to implement it):

- `TCategory` is closed statically, inside generated code — `typeof(ILogger<TCategory>)`
  inside a compiled `Register<TCategory>` instantiation is an ordinary
  generic-token load, not `Type.MakeGenericType`.
- No `MakeGenericType`, no `Activator.CreateInstance`, no
  `DynamicMethod`/compiled-expression activation, anywhere in this path.
- Generated activator delegates are `static` and capture no
  composer-specific state — only `TCategory`, compiled in.
- The `LoggingOptions` instance passed to an activator is always the one
  belonging to the active `UseLogging()` configuration that's resolving
  the request, supplied as an ordinary argument at lookup time, never
  captured ahead of time.

### Missing generated activation

If `LoggingProvider` recognizes a request as closed-`ILogger<T>`-shaped
(the existing static `Type` comparison) but
`LoggingFactoryRegistry.TryCreate` finds no entry, **it throws
immediately — it does not return `NotHandled`.** Falling through would
let a later-registered `UseNSubstitute()`/`UseGeneratedTestDoubles()`
silently claim the request, masking a real generator/discovery gap behind
what looks like an ordinary substitute or generated double.

**Exception type: `InvalidOperationException`, with a message identifying
the closed `ILogger<T>`, stating that `Compono.Logging` recognized the
request but found no generated activation for it, and pointing at
source-generation/discovery coverage as the cause (including, where
relevant, ADR-0052's nested-`context.Resolve<T>()` gap as a known cause
of this specific shape) — not a new dedicated public exception type.**
This repo does have real precedent for dedicated diagnostic exception
types (`TestDoubleNotConfiguredException`, `UnmatchedHttpRequestException`),
but each of those names a condition a consumer might reasonably want to
catch or assert against as part of normal test authoring (e.g., "assert
this double throws because nobody configured it"). A missing generated
activation is different in kind — it signals a `Compono.Logging`
generator/discovery defect or coverage gap, not a condition a test author
is meant to write code against; a consumer's correct response is always
to fix the composition shape (or file a `Compono.Logging` bug), never to
catch and handle it programmatically. A new public exception type is not
justified for a condition with no legitimate catch use case — this keeps
the public surface at its current minimum, consistent with this ADR's
own "smallest defensible" framing throughout. `InvalidOperationException`
here is a *distinct* condition from the "non-Compono-logger" failure
semantics already recorded above (that one signals a wrong-provider
registration order; this one signals a generator coverage gap) — the two
messages must be clearly distinguishable at diagnosis time even though
they share an exception type.

### AOT/trimming — corrected

The architecture remains reflection-free and Native-AOT compatible, but
**not because source generation is unnecessary — because generated code
statically closes every discovered `CapturingLogger<T>` activation
composition actually needs**, the same reasoning already accepted for
`GeneratedTestDoubleRegistry`. `Type.MakeGenericType` + `Activator.CreateInstance`
was rejected specifically because it produced `IL3050` under
`EnableAotAnalyzer`/`IsAotCompatible=true` (spiked, not assumed — see
above), which is what makes a generated bridge the correct answer rather
than an unnecessary one. Direct consumer construction —
`new CapturingLogger<T>()`/`new CapturingLogger<T>(options)` — is
unaffected: `T` is statically known at that call site by ordinary C#
generics, so it needs no generator involvement at all, exactly as
originally decided.

### Rejected alternatives (this amendment)

- **`Type.MakeGenericType` + `Activator.CreateInstance` inside
  `LoggingProvider`.** Rejected — violates the reflection-free design
  driver, empirically produces `IL3050` under the AOT analyzer, and
  weakens the Native AOT guarantee this ADR requires.
- **An ADR-0014-style dedicated `LoggingPlanCache<TValue>` branch read
  directly inside `CompositionContext.ResolveCore<TValue>`.** Rejected —
  collections are core composition behavior, which is why ADR-0014's
  branch belongs inside core `Compono`; `Compono.Logging` is an optional
  integration package, and adding a logging-specific branch to core's
  `ResolveCore<TValue>` would couple core composition to an optional
  integration for no evidenced need — stage-6 provider semantics remain
  entirely viable once activation is supplied by a generated registry,
  so the stronger, more invasive mechanism isn't warranted.
- **Broad, compilation-wide constructor scanning** for any `ILogger<T>`
  shape regardless of composition-root reachability. Rejected —
  inconsistent with Compono's established root-and-transitive-closure
  discovery model, and would generate unsupported/unnecessary activators
  for types nobody actually composes.
- **Exposing or rearchitecting `TransitiveClosureWalker`/`LeafTypeClassifier`
  for cross-package reuse** so `Compono.Logging`'s generator could call
  into them directly. Rejected — this would create real cross-package
  coupling (core `Compono.Generators` gaining a public/shared surface
  purely for one downstream integration) solely to avoid a small, bounded
  amount of generator-side duplication; no evidence justifies that
  architectural expansion.

### Documentation/skill synchronization — extended

In addition to everything the original ADR text already requires, the
implementation plan must also document, at minimum in
`docs/packages/compono-logging.md` and
`skills/compono/references/logging.md` (plus any architecture/generator
doc the plan's own inspection determines is authoritative for shipped
generator behavior):

- that `Compono.Logging` ships its own source generator/analyzer asset,
  and what it is (and is not) responsible for,
- that `CapturingLogger`/`CapturingLogger<T>` remain hand-written — the
  generator produces activation glue only, never logger behavior,
- how closed `ILogger<T>` activation works conceptually (discovery →
  generated registration → runtime lookup → `LoggingOptions` passed at
  lookup time),
- the statically-discoverable-dependency limitation (and its
  ADR-0052-Finding-B example),
- the missing-generated-activation diagnostic and what it means,
- and that no reflection fallback exists anywhere in this path.

This extends, and does not weaken or replace, the original ADR's
documentation/skill/eval completion-gate requirement.

### Unchanged

Everything not named above is unchanged and not reopened by this
amendment: `UseLogging()`'s public shape, `CapturingLogger`/
`CapturingLogger<T>`'s public construction, `CapturedLogEntry` and its
`Properties` nullability, structured-logging semantics, scope semantics,
`MinimumLevel`/`LogLevel.None` semantics, `LoggerTestingExtensions`,
`Verify()`'s fluent API and `CallVerifier` reuse, stage-6 registration and
first-registered-wins provider precedence, `[Shared]` behavior and its
deferment, the `ILoggerFactory` exclusion, the package's runtime
dependency graph (`Compono` + `Microsoft.Extensions.Logging.Abstractions`
only — the new generator project adds a build-time-only analyzer asset,
no new runtime `PackageReference`), direct-construction behavior, and the
two dogfood targets.

## Amendment 2 (2026-08-28): Public generator-infrastructure registry for cross-assembly activation

Amendment 1's text is left exactly as written, per this repo's amendment
convention — this amendment corrects one accessibility detail Amendment
1 got wrong, found during implementation planning (PLAN-0055), before any
code was written against it.

**What Amendment 1 got wrong.** Amendment 1 correctly requires
consumer-generated `[ModuleInitializer]` code to register closed
`ILogger<T>` activators into a `LoggingFactoryRegistry`, but its
representative shape declared that registry `internal`. Generated code
registering an activator is compiled **into the consumer's own
assembly** — an ordinary CLR accessibility fact, not a subtle one: an
`internal` member is never visible outside its declaring assembly without
an explicit `InternalsVisibleTo` naming that specific assembly, which is
impossible here since consumer assembly names are arbitrary and unknown
at `Compono.Logging`'s build time. As written, Amendment 1's registry
could not have compiled against real consumer-generated code.

**This is not a new architectural problem — it's one Compono has already
solved, twice, exactly this way.** `src/Compono/GeneratedTestDoubleRegistry.cs`
is `public`, with `public static void RegisterFactory<T>(Func<T> factory)`
and `public static bool TryCreate(Type requestedType, out object? value)`
— confirmed the generator's own emitted code
(`src/Compono.Generators/Templates/TestDouble.scriban:610`) calls it
directly, cross-assembly, as ordinary public API. `src/Compono/RowInvokerRegistry.cs`
is the same shape and states the reasoning explicitly in its own doc
comment: it is "populated by a generated module initializer in the
consuming assembly (never by `Compono` itself), the same cross-assembly
reason `PlanCache<T>`'s own setter is `public` despite `coding-standards.md`'s
'no static singletons' rule." Both registries are also, per that same
comment, deliberately "left undecorated with no
`EditorBrowsableAttribute`... matching `PlanCache<T>`/`CollectionPlanCache<T>`,
its two closest precedents as 'generator infrastructure, not
consumer-facing' public types that carry no such attribute either."

### Decision

`LoggingFactoryRegistry` is deliberately `public`, matching this
established, already-shipped precedent exactly — not a separate internal
registry plus a distinct public "bridge" facade type:

```csharp
public static class LoggingFactoryRegistry
{
    public static void Register<TCategory>(Func<LoggingOptions, object> factory);
    public static bool TryCreate(Type requestedType, LoggingOptions options, out object? value);
}
```

**This is a genuine public API addition — it is not hidden, minimized-by-labeling,
or treated as merely an implementation detail that happens to carry the
`public` keyword.** It is, however, **generator infrastructure, not
ordinary consumer-facing usage surface**: normal `Compono.Logging`
consumers compose through `UseLogging()`, inspect through
`GetCapturedEntries()`/`Verify()`, and construct `CapturingLogger`/
`CapturingLogger<T>` directly when they want to bypass composition —
`LoggingFactoryRegistry` exists only so generated code can register
activators, the same relationship a consumer already has (or rather,
doesn't have) with `GeneratedTestDoubleRegistry`/`RowInvokerRegistry`
today. Documentation should treat it the same way those two are treated:
present, real, public — never presented alongside `UseLogging()`/
`CapturingLogger<T>()`/`CapturedLogEntry`/`Verify()` as something a
consumer is expected to call by hand.

**No `[EditorBrowsable(EditorBrowsableState.Never)]` is added.** This
follows the repo's own explicitly-documented convention
(`RowInvokerRegistry`'s doc comment, above) of leaving generator-
infrastructure public types undecorated, for consistency with
`PlanCache<T>`, `CollectionPlanCache<T>`, and `GeneratedTestDoubleRegistry`
— none of which carry that attribute. Decorating only `LoggingFactoryRegistry`
differently would itself be the inconsistency, not the fix for one.

**No separate public registration-hook type is introduced merely to keep
the actual registry `internal`.** Every existing Compono precedent for
this exact cross-assembly handshake uses the registry itself as the
public generator/runtime boundary — `GeneratedTestDoubleRegistry` and
`RowInvokerRegistry` are both the storage *and* the entry point, with no
intermediate facade — and no evidence surfaced during this investigation
suggests a second abstraction here would improve anything.

### Everything else from Amendment 1 is unchanged

The package-owned generator, stage-6 `LoggingProvider`, generated
statically-closed activation, runtime `LoggingOptions` flow, the
reflection-free/AOT-safe path, the missing-generated-activation
`InvalidOperationException`, and the discovery model and its documented
limitations are all unaffected by this amendment — only
`LoggingFactoryRegistry`'s accessibility keyword and its documentation
treatment change.

## Amendment 3 (2026-08-28): Logging activation generation moves into the existing `Compono.Generators`, gated by `ComponoGeneratedLogging`

Amendment 1's and Amendment 2's text are left exactly as written, per this
repo's amendment convention. This amendment **supersedes only Amendment
1's "Generator ownership" decision** (a dedicated `Compono.Logging`-owned
generator project) — every other decision in Amendments 1 and 2 (hand-
written `CapturingLogger`/`CapturingLogger<T>`, generated closed-generic
activation as the reflection-free/AOT-safe mechanism, `LoggingFactoryRegistry`
being public, the runtime `LoggingOptions` flow, the missing-activation
diagnostic, the discovery model's shape) is unchanged.

**What Amendment 1 got wrong.** Amendment 1 reasoned that `ILogger<T>`
recognition is integration-specific, and concluded from that alone that
the generator emitting its activation should therefore be a separate,
`Compono.Logging`-owned project. That conclusion doesn't follow: the
*recognition* is integration-specific (correctly kept out of core), but
the *generator infrastructure* needed to reach it — real composition-root
discovery, the transitive constructor-parameter walk, leaf-vs-recurse
classification — is not, and already exists, complete and working, inside
`Compono.Generators`. A separate generator project would have had to
independently re-derive that same walk (confirmed necessary during
PLAN-0055 drafting, and explicitly flagged there as bounded-but-real
duplication) for no benefit strong enough to justify a second Roslyn
generator/discovery stack that has to stay behaviorally synchronized with
the first. `Compono.TestDoubles` is the closer, stronger precedent this
ADR should have followed from the start: its compile-time-gated discovery
signal (`LeafTypeClassifier.IsGeneratedTestDoubleEligible`) already lives
inside core `Compono.Generators`, inert unless its own MSBuild opt-in is
set, reusing the exact same walk every other discovery path shares — not
a second project.

### Decision: reuse `Compono.Generators` directly, gated by a compiler-visible property

**No `Compono.Logging.Generators` project exists.** Logging activation
generation is a narrow addition inside the existing `Compono.Generators`
assembly:

- **Discovery reuses the existing pipeline directly, not a reimplementation
  of it.** `TransitiveClosureWalker` already visits every constructor
  parameter type reached from a real composition root
  (`Composer.Create<T>()`/`CreateMany<T>()`, `[Composable]`,
  `[Compose]`/`[Compose<TProfile>]` theory-row parameters) and, at each
  one, already calls `LeafTypeClassifier.IsGeneratedTestDoubleEligible`
  as a side-channel classification hung off that one walk. Logging
  activation hooks into the identical call site: an additional,
  independently-optional check — is this parameter type a closed
  instantiation of `ILogger<T>`? — recorded into its own discovery bucket
  alongside the existing test-double-eligible-leaf list. **No second
  walker, no duplicated root discovery, no duplicated leaf
  classification** — the exact duplication PLAN-0055 previously flagged
  as a real cost of the separate-project design is eliminated.
- **Recognition stays dependency-free.** `ILogger`/`ILogger<T>` are
  resolved via `Compilation.GetTypeByMetadataName` against the
  compilation this generator is running for — `Compono.Generators.csproj`
  gains **no** `Microsoft.Extensions.Logging.Abstractions` package
  reference, unchanged from Amendment 1's original reasoning, just
  relocated.
- **Emission is a dedicated, clearly separated addition** (a new emitter
  file plus its own template), mirroring how `Compono.TestDoubles`' own
  support (`TestDoubleAnalyzer`, `Templates/TestDouble.scriban`) is
  already a clearly separated addition inside the same assembly, not
  smeared into the core plan-emission code path core composable types
  use.
- **Core runtime `Compono` remains completely unaware of logging** — this
  amendment adds compile-time knowledge only to the generator assembly,
  which already carries package-specific knowledge for `Compono.TestDoubles`
  today; it adds nothing to `Compono`'s own runtime types.

### The feature switch: `ComponoGeneratedLogging`, default-enabled by the package itself

A new requirement, not present in Amendment 1: **installing
`Compono.Logging` should enable its generation behavior by default** — a
deliberate product decision to avoid the exact confusion
`ComponoGeneratedTestDoubles`' pure-opt-in shape already causes today
(`skills/compono/SKILL.md` itself documents forgetting to set it as *"the
most common setup mistake"* for `Compono.TestDoubles`). This ADR does
**not** change `Compono.TestDoubles`' own existing opt-in behavior —
see "Not part of this amendment," below.

**Mechanism** (a standard, safe NuGet/MSBuild pattern, confirmed by
import-order rules, not assumed):

- Core `Compono`'s existing `src/Compono/build/Compono.props` (packed
  into both `build/` and `buildTransitive/` inside `Compono.nupkg`,
  already declaring `ComponoGeneratedTestDoubles`) gains one more
  declaration:
  ```xml
  <CompilerVisibleProperty Include="ComponoGeneratedLogging" />
  ```
  This is inert for any consumer who never references `Compono.Logging`.
- `Compono.Logging.nupkg` ships its own new, tiny `build/Compono.Logging.props`
  (packed to both `build/` and `buildTransitive/`) containing only:
  ```xml
  <PropertyGroup>
    <ComponoGeneratedLogging Condition="'$(ComponoGeneratedLogging)' == ''">true</ComponoGeneratedLogging>
  </PropertyGroup>
  ```
  This is a small MSBuild props asset, not a second analyzer/generator —
  `Compono.Logging.nupkg` carries no generator DLL of any kind.
- **Explicit consumer configuration always wins over the package
  default.** NuGet package `.props` files import before a consumer's own
  project body evaluates; `CompilerVisibleProperty` values are captured
  from the property's *final* evaluated value after the whole project
  (body, all props, all targets) finishes evaluating. A consumer's own
  `.csproj` body setting always overwrites the package's conditional
  default; a consumer's own `Directory.Build.props` setting (even though
  it evaluates earlier) is still safe, because the package's own
  `Condition="'$(ComponoGeneratedLogging)' == ''"` guard correctly detects
  it's already non-empty and never overwrites it.

**Resulting behavior**:

| `ComponoGeneratedLogging` | `Compono.Logging` referenced | Outcome |
|---|---|---|
| absent/unset | no | off — no logging generation, no diagnostic (unchanged, ordinary Compono consumer) |
| default (`true`, from `Compono.Logging`'s own props) | yes | on — normal discovery/emission |
| explicit `false` | yes | off — consumer's explicit choice wins, no diagnostic |
| explicit `true` (forced by hand) | no | on, but required runtime symbols are missing — dedicated diagnostic, see below |

**Package presence is deliberately not the feature switch.** The
compiler-visible property alone decides whether the generator attempts
logging discovery at all — `Compono.Logging.LoggingFactoryRegistry`/
`CapturingLogger<T>`/`LoggingOptions` presence is checked only *after*
the property says "enabled," purely to validate the environment and
produce an honest diagnostic if something is wrong, never as the
trigger itself. This is a deliberate correction from this ADR's own
earlier draft reasoning (during PLAN-0055's drafting, before this
amendment), which had proposed gating purely on `LoggingFactoryRegistry`'s
presence — rejected because it conflates "is the feature enabled" with
"is the referenced package actually wired up," two genuinely different
questions with different correct responses (silently do nothing, vs.
report a real misconfiguration).

### Missing-symbols diagnostic

When `ComponoGeneratedLogging` is enabled (explicitly or by default) but
`Compono.Logging.LoggingFactoryRegistry`/`CapturingLogger<T>`/
`LoggingOptions` cannot all be resolved via `GetTypeByMetadataName`
against the compilation, the generator reports a dedicated diagnostic
(working id `CMP0038`, category `Compono.Logging`, `Info` severity —
matching `Compono.TestDoubles`' own `CMP0020`-`CMP0032` informational
convention rather than a hard build error, since nothing broken is
emitted; confirm the exact next-available id against
`AnalyzerReleases.Unshipped.md` at implementation time) and emits **no**
logging-registration source at all — never partial or broken generated
code referencing types that don't exist.

### Not part of this amendment: `Compono.TestDoubles`' own default

This amendment does **not** change `ComponoGeneratedTestDoubles`'
existing pure-opt-in behavior. A real, confirmed compatibility risk rules
out treating that as a mechanical follow-on here: any existing consumer
who references `Compono.TestDoubles`, calls both `UseGeneratedTestDoubles()`
and `UseNSubstitute()` (or another stage-6 provider), and never explicitly
set the flag today is currently, silently, served entirely by the other
provider (`GeneratedTestDoubleProvider` always returns `NotHandled` while
its registry is empty) — flipping that default would silently change
which provider satisfies such a consumer's requests, a genuine
behavior-change risk this amendment does not carry. Whether
`Compono.TestDoubles` should adopt the same default-enabled shape before
1.0 is tracked as a separate, future ADR-0043 amendment plus its own
small implementation plan, not decided or implemented here.

### Everything else from Amendments 1 and 2 is unchanged

The generated statically-closed activation mechanism, the runtime
`LoggingOptions` flow, the reflection-free/AOT-safe path, the
missing-generated-activation `InvalidOperationException` (a distinct
condition from this amendment's missing-*symbols* diagnostic — that one
fires at runtime inside `LoggingProvider` when a specific closed `T` was
never discovered; this one fires at compile time when the whole feature
is misconfigured), the discovery model's documented limitations
(including the ADR-0052 Finding-B cross-reference), and
`LoggingFactoryRegistry`'s public accessibility (Amendment 2, entirely
unaffected by *which* generator assembly emits the call to it) all carry
forward unchanged.

## Amendment 4 (2026-08-28): Logging owns generation for `ILogger`/`ILogger<T>` — excluded from `Compono.TestDoubles` generation when Logging generation is enabled

**Status:** Accepted

Amendments 1-3's text is left exactly as written. This amendment adds a new
rule; it does not revise any earlier decision.

### What PLAN-0055 task 18 dogfooding found

Dogfooding `Compono.Logging` against `alexa-vox-craft` (real consumer,
`AlexaVoxCraft.MediatR.Tests`, `PerformanceLoggingBehaviorTests.cs`) — the
first real-world project with **both** `ComponoGeneratedTestDoubles=true`
(needed for its other interface doubles) and `ComponoGeneratedLogging=true`
(Compono.Logging's own default, per Amendment 3) enabled on the same
`ILogger<T>` composition root — reproduced a genuine compile-time API
collision that no prior evidence (four pre-ADR spikes, 57 `Compono.Logging.Tests`,
13 `LoggingGeneratorTests`) had exercised, because none of it enabled both
features on the same category type simultaneously.

**Root cause, confirmed directly in source, not inferred:**

- `LeafTypeClassifier.IsGeneratedTestDoubleEligible` (`src/Compono.Generators/Discovery/LeafTypeClassifier.cs:77-78`)
  is `testDoublesEnabled && type is INamedTypeSymbol { TypeKind: TypeKind.Interface }`
  — **any** interface, unconditionally. It has no awareness of
  `Compono.Logging` or `ILogger`/`ILogger<T>` at all.
- `TransitiveClosureWalker.Walk`/`EnqueueMember` (`Discovery/TransitiveClosureWalker.cs:105-106,178-179`)
  call `TryRecordTestDouble` and `TryRecordLoggingCategory` back-to-back on
  the *same* type at the *same* call sites. Discovery is purely static
  (based on the parameter's declared type), never gated on which stage-6
  runtime provider will actually satisfy the request.
- For a closed `ILogger<PerformanceLoggingBehavior>` reachable this way,
  `Compono.TestDoubles`' generator therefore emits its own
  `Verify(this ILogger<PerformanceLoggingBehavior> self) : ..._DoubleVerifier`
  extension (`Templates/TestDouble.scriban:492`, exact-typed to the closed
  generic), *in addition to* `Compono.Logging`'s
  `Verify(this ILogger logger) : LogVerificationBuilder` (base-typed).
  Ordinary C# overload resolution prefers the exact-type match — **the
  TestDoubles extension silently wins and shadows Compono.Logging's**,
  regardless of which provider actually wins at runtime under stage-6
  first-registered-wins precedence (ADR-0024/ADR-0043, unchanged). Observed
  directly: `logger.Verify().AtLevel(...)` failed `CS1061` because
  `.Verify()` returned the TestDoubles `..._DoubleVerifier` type, which has
  no `AtLevel`.

**Why this wasn't caught earlier.** ADR-0055's whole precedence discussion
(Amendment 3 and the original decision) reasons entirely about *runtime*
stage-6 provider precedence. It never considered *compile-time* extension-
method collision between two generators that both independently recognize
the same static type and both emit a same-named `Verify()` surface for it.
This is not consumer misuse, and `alexa-vox-craft`'s uncommitted spike
changes were reverted to isolate the finding rather than worked around
there — see PLAN-0055 task 18's dogfooding report for the full evidence
trail.

### Decision: `Compono.Logging`, when its generation is enabled, owns generation for `ILogger`/`ILogger<T>` — `Compono.TestDoubles` excludes them

The rule is stated as **ownership**, not as "who generates something":
when `ComponoGeneratedLogging` is enabled (default or explicit),
`Microsoft.Extensions.Logging.ILogger` and every closed
`Microsoft.Extensions.Logging.ILogger<T>` become **Logging-owned
abstractions** and are excluded from `Compono.TestDoubles` generation —
`TransitiveClosureWalker` no longer records either shape as
`Compono.TestDoubles`-eligible. What "owned" produces differs by shape,
and that difference matters for the precedence correction below:

- **Closed `ILogger<T>`** — Logging-owned *and* receives generated closed-
  generic activation (`LoggingFactoryRegistry`, Amendments 1-3, unchanged).
- **Bare `ILogger`** — Logging-owned, but needs **no** generated
  activation at all; `LoggingProvider` already constructs a
  non-generic `CapturingLogger` directly (Amendment 1's original
  reasoning). Excluding it from `Compono.TestDoubles` is an ownership
  statement only, not a claim that Logging generates anything for it.

`Compono.TestDoubles`' own eligibility check is otherwise completely
unchanged for every other interface.

**Behavior matrix** (confirmed by spike, see below):

| `ComponoGeneratedTestDoubles` | `ComponoGeneratedLogging` | `ILogger<T>` outcome |
|---|---|---|
| off | off | neither generator touches it (unchanged) |
| off | on | Logging activation only (unchanged from Amendment 3) |
| on | off | TestDoubles double + `Verify()`, exactly as before this amendment (unchanged) |
| on | on | Logging activation only — TestDoubles silently declines this one type, no double, no competing `Verify()` |

**Bare (non-generic) `ILogger` needs no special-casing in practice.**
Investigated as a named open question before deciding scope: with
`ComponoGeneratedTestDoubles=true` and `ComponoGeneratedLogging=false`
(Logging making no claim at all), bare `ILogger` still gets **no**
TestDoubles double or `Verify()` extension — confirmed by reverting the
fix (`git stash`) and rerunning the same spike. This is pre-existing
behavior, unrelated to and unaffected by this amendment; the collision
this amendment fixes only ever existed for closed `ILogger<T>`. The fix
below still excludes bare `ILogger` for semantic consistency with
`ILogger<T>` (one rule, not two), but no consumer-visible behavior change
results from that half of the rule — it's provably a no-op given current
`Compono.TestDoubles` behavior.

**Stage-6 first-registered-wins itself is unchanged — but one of its
narrower, previously-stated consequences for `ILogger`/`ILogger<T>` is now
too broad and is corrected here.** The general rule (ADR-0024/ADR-0043:
whichever stage-6 provider is registered first wins) still governs
everything. What changes is which providers can actually produce
`ILogger`/`ILogger<T>` once this amendment lands:

- With `ComponoGeneratedLogging` enabled, `GeneratedTestDoubleProvider` no
  longer has a generated factory for `ILogger`/`ILogger<T>` (this
  amendment's whole point) — so registration order between
  `LoggingProvider` and `GeneratedTestDoubleProvider` is **no longer
  observable** for these types; `GeneratedTestDoubleProvider` simply has
  nothing to offer them regardless of order.
- This does **not** make `LoggingProvider` unconditionally win. `UseNSubstitute()`
  (`NSubstituteProvider`) — and any other stage-6 provider capable of
  independently producing an `ILogger`/`ILogger<T>` substitute without a
  generated factory — is untouched by this amendment and can still win if
  registered first. **`UseLogging()` must still precede such providers**
  when `Compono.Logging`'s capture/verification behavior is required; the
  earlier ADR text's blanket "`UseLogging()` must precede both
  `UseNSubstitute()` and `UseGeneratedTestDoubles()`" is superseded by this
  narrower statement for the `UseGeneratedTestDoubles()` half specifically.
- This changes no pipeline rule and introduces no provider-priority
  mechanism — it is a consequence of removing `GeneratedTestDoubleProvider`'s
  ability to handle these types at all, not a change to how stage-6 itself
  resolves ties.

**Pre-1.0 compatibility note.** A consumer with both features enabled
today who happens to already rely on a *generated TestDoubles double* for
an `ILogger<T>` category (as opposed to `Compono.Logging`'s
`CapturingLogger<T>`) would see that double stop being generated once this
lands — an intentional pre-1.0 correction, not a deprecation. No known
consumer currently does this (the real dogfooding case wanted
`Compono.Logging`'s behavior, which the collision was itself preventing).

**Scope: narrow, `ILogger`/`ILogger<T>`-specific — not a general
feature-ownership mechanism.** This amendment resolves the concrete
collision found, not a hypothetical future one. A generalized
generator-feature-priority/ownership system is explicitly **not**
introduced — nothing today justifies one, and building it ahead of a
second real collision would be speculative. If a future first-class
feature package (not `Compono.TestDoubles`/`Compono.Logging`) hits the
same shape of problem, that's a new amendment (here or elsewhere)
informed by that concrete case, not an extension of this one.

### Spikes performed (proof, not just reasoning)

All against a throwaway patch to `TransitiveClosureWalker` (a private
`IsLoggingOwned(type, ctx)` helper checked inside `TryRecordTestDouble`
before `LeafTypeClassifier.IsGeneratedTestDoubleEligible`'s existing
check) plus a throwaway `test/Compono.Generators.Tests/SPIKE_Amendment4CollisionTests.cs`
— **neither is committed**; both exist only to produce this evidence and
will be deleted/reverted regardless of this amendment's outcome, replaced
by a real implementation once accepted:

1. **Real-world reproduction, patched**: re-ran PLAN-0055 task 18's
   `scripts/dogfood-validate.sh` against `alexa-vox-craft`'s full solution
   (`Compono`+`Compono.Logging` freshly packed, `AlexaVoxCraft.MediatR.Tests`
   using both features on the same `ILogger<PerformanceLoggingBehavior>`)
   with the patch applied: `logger.Verify().AtLevel(...)` compiled and
   bound to `Compono.Logging`'s `LogVerificationBuilder` as intended; full
   solution — 2816 tests, 0 failed, 32 skipped (pre-existing, unrelated).
2. **`TestDoublesOn_LoggingOn_NoCompetingVerifyExtensionForILoggerOfT`** —
   generator-level: with both properties `true`, generated output contains
   `LoggingFactoryRegistry` (Logging activation present) and does **not**
   contain `ILogger_global__...OrderService` (no competing TestDoubles
   double/`Verify()` for that closed type). Pass.
3. **`TestDoublesOn_LoggingOff_ILoggerOfTStillGetsGeneratedDouble`** — same
   shape with `ComponoGeneratedLogging=false`: no `LoggingFactoryRegistry`,
   but `ILogger_global__...OrderService` **is** present — confirms the fix
   doesn't regress `Compono.TestDoubles`' existing `ILogger<T>` support
   when Logging isn't claiming it. Pass.
4. **`TestDoublesOn_LoggingOn_BareILoggerAlsoExcludedFromTestDoubles`** /
   **`TestDoublesOn_LoggingOff_BareILoggerNeverGetsGeneratedDouble_PreExistingBehavior`**
   — bare `ILogger`, both property combinations: no `ILogger_global__...`
   text in either case, confirming (per the compatibility note above) that
   bare `ILogger` was never actually at risk of this collision, with or
   without the fix. Pass.
5. Full existing `Compono.Generators.Tests` suite (584 tests, including
   the 13 `LoggingGeneratorTests` and every `Compono.TestDoubles`-focused
   test) re-run with the patch applied: 0 regressions.

### What this amendment does not do

Does not change stage-6 runtime provider precedence. Does not introduce a
generalized generator-feature-priority system. Does not change
`Compono.TestDoubles`' own `ComponoGeneratedTestDoubles` opt-in default
(separate, already-tracked question per Amendment 3's "Not part of this
amendment"). Does not touch `alexa-vox-craft` or `structured-logging` —
PLAN-0055 task 18 dogfooding resumes once this amendment is Accepted and
implemented. Implementation itself (the real, permanent
`LeafTypeClassifier`/`TransitiveClosureWalker` change, its own tests,
`AnalyzerReleases.Unshipped.md` entry if a diagnostic is warranted, and
`skills/compono/references/logging.md`/`testdoubles.md` updates so this
interoperability rule is documented rather than silent generator behavior)
is intentionally **not** part of this amendment — it happens under
`tasks/implement.md` once this reads `Accepted`.
