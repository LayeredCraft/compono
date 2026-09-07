# [RESEARCH-0023] Compono.Logging 1.1 Research

Status: Done (research only; no ADR yet)

Feeds: a future ADR-0055 amendment or a new ADR, if 1.1 scope is accepted. Does not itself change any code, ADR, or plan.

This is research only. No production code, ADR, or plan was created or modified. Scope is explicitly **not** GitHub issue #132 (generated-logging-ownership-under-parallel-MSBuild correctness bug) — that is a separate, already-filed defect; it is mentioned below only where it independently surfaced as an architectural observation.

## 1. Current package responsibility

`Compono.Logging` (`src/Compono.Logging/Compono.Logging.csproj`) gives first-class `Microsoft.Extensions.Logging` testing support inside Compono composition: `ILogger`/`ILogger<T>` compose as a hand-written, reflection-free `CapturingLogger`/`CapturingLogger<T>` pair, with direct inspection (`GetCapturedEntries()`/`GetLastCapturedEntry()`/`ClearCapturedEntries()`) and fluent verification (`Verify()...Once()/Never()/Exactly(n)`) reusing core `Compono`'s `CallVerifier` unmodified. It is documented in [ADR-0055](../adr/0055-compono-logging-testing-support-package.md) (5 amendments) and [RESEARCH-0013](0013-compono-logging-testing-design-research.md), shipped in 1.0 (`52af46c`, PR #116), and has had exactly one follow-up commit since (`43a5e81`, an unrelated generator-runtime-hook-policy chore). No feature work has touched it since 1.0 shipped.

## 2. Current architecture

Eleven files, `src/Compono.Logging/`:

| File | Role |
|---|---|
| `CapturedLogEntry.cs` | `readonly record struct` — the captured-entry model (raw + derived, §4). |
| `CapturingLogger.cs` / `CapturingLogger{T}.cs` | `ILogger`/`ILogger<T>` implementations. Each composes its own `LogEntryCollector`; `CapturingLogger<T>` does **not** wrap or inherit `CapturingLogger` (composition-over-inheritance, deliberately breaking from `LayeredCraft.StructuredLogging`'s `TestLogger<T> : TestLogger`). |
| `LogEntryCollector.cs` | `internal`. Owns the lock-guarded entry list, one `LoggerExternalScopeProvider`, and the effective `MinimumLevel`. All capture/filter/scope logic lives here. |
| `LoggingOptions.cs` | One setting: `MinimumLevel` (default `LogLevel.Trace`), fixed at construction. |
| `LogVerificationBuilder.cs` | Fluent filter chain (`AtLevel`/`WithEventId`/`WithException<T>`/`WithMessageContaining`/`Matching`) ending in three one-line forwarders to `Compono.CallVerifier` (`Once`/`Never`/`Exactly`). |
| `LoggerTestingExtensions.cs` | Public extension methods on `ILogger` (`GetCapturedEntries`, `GetLastCapturedEntry`, `ClearCapturedEntries`, `Verify`) — throws `InvalidOperationException` if the `ILogger` isn't a Compono.Logging capturing logger. |
| `ICapturingLoggerFacade.cs` | `internal` dispatch interface so the extension methods above can reach either concrete logger's `LogEntryCollector` without a public downcast. |
| `CompositionBuilderExtensions.cs` | `UseLogging(Action<LoggingOptions>?)` — registers `LoggingProvider` as a stage-6 `ICompositionValueProvider`. |
| `LoggingProvider.cs` | `internal`. The stage-6 provider: bare `ILogger` → `new CapturingLogger(options)` directly; closed `ILogger<T>` → looks up `LoggingFactoryRegistry.TryCreate`. |
| `LoggingFactoryRegistry.cs` | `public` (cross-assembly generator-infrastructure reasons, ADR-0055 Amendment 2) `Type`-keyed registry of generated `CapturingLogger<T>` activators, populated by a `[ModuleInitializer]` the shared `Compono.Generators` emits in the *consumer's own assembly*. |
| `build/Compono.Logging.props` | Defaults `ComponoGeneratedLogging` to `true` (packed to both `build/` and `buildTransitive/`). |

**Generator integration** (`src/Compono.Generators/`): `Compono.Logging` ships **no generator/analyzer DLL of its own** (ADR-0055 Amendment 3). Logging activation generation was moved into the existing, already-packed `Compono.Generators`:
- `WellKnownTypes/LoggingWellKnownTypes.cs` — a dedicated `ILogger`/`ILogger<T>` symbol resolver, deliberately carrying **no** `Microsoft.Extensions.Logging.Abstractions` package reference itself; `Compilation.GetTypeByMetadataName` returns `null` cleanly for a consumer who never referenced `Compono.Logging`, keeping the shared generator project dependency-free.
- `Models/DiscoveredLoggingCategoryInfo.cs`, `Models/LoggingRuntimeSymbolsStatus.cs`, `Models/GeneratorFeatureFlags.cs` — discovery/status models.
- `Templates/LoggingActivation.scriban`, `Emitters/LoggingActivationEmitter.cs` — emits, per discovered closed `ILogger<T>` category, a `[ModuleInitializer]`-registered call to `LoggingFactoryRegistry.Register<TCategory>(...)` that closes `new CapturingLogger<TCategory>(options)`.
- Gated by `ComponoGeneratedLogging` (default `true`, unlike `Compono.TestDoubles`'s pure-opt-in `ComponoGeneratedTestDoubles` — a deliberate product decision, ADR-0055 Amendment 3).
- Per ADR-0055 Amendment 4: when `ComponoGeneratedLogging` is enabled, `Compono.TestDoubles` generation *excludes* `ILogger`/`ILogger<T>` entirely — Logging owns generation for those types; there is no dual-ownership collision by construction (a real collision `PLAN-0055` task 18 dogfooding found and this amendment fixed).

## 3. Current dependency graph

`Compono.Logging.csproj`:
```xml
<ProjectReference Include="..\Compono\Compono.csproj" PrivateAssets="none" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```
No dependency on `Compono.TestDoubles`, `Compono.NSubstitute`, `Microsoft.Extensions.Logging` (the concrete package), `Microsoft.Extensions.DependencyInjection`, or `Microsoft.Extensions.Diagnostics.Testing` — all deliberate, ADR-0055-recorded exclusions.

**What actually reaches into core `Compono`, file by file** (confirmed by grep across every `.cs` file in `src/Compono.Logging/`, not just the csproj):
- `CompositionBuilderExtensions.cs` — `CompositionBuilder`, `AddTestDoubleProvider` (registration/config).
- `LoggingProvider.cs` — `ICompositionValueProvider`, `CompositionProviderResult`, `CompositionProviderRequest`, `ICompositionContext` (stage-6 provider integration).
- `LogVerificationBuilder.cs` — `CallVerifier` only, one `new CallVerifier(count, description)` call plus its three-method surface (`Once`/`Never`/`Exactly`, which itself throws core `TestDoubleVerificationException`).

**What does *not* reach into core `Compono` at all**: `CapturedLogEntry.cs`, `CapturingLogger.cs`, `CapturingLogger{T}.cs`, `LogEntryCollector.cs`, `LoggingOptions.cs`, `ICapturingLoggerFacade.cs`, `LoggerTestingExtensions.cs`, `LoggingFactoryRegistry.cs` — every one of these compiles against `Microsoft.Extensions.Logging.Abstractions` alone. Confirmed empirically, §11.

Categorizing the dependency by kind, as requested:
- **Compile-time / runtime type reuse**: `CallVerifier` (verification terminal) — runtime, not generator.
- **Registration/config integration**: `CompositionBuilder`/`ICompositionValueProvider`/stage-6 pipeline — this is the actual "why does Logging depend on core Compono at all" answer for most of the surface. `UseLogging()` has to plug into the same `Composer.Create(builder => ...)` pipeline every other integration package plugs into.
- **Generator integration**: none at the `Compono.Logging` project level (it ships no generator). The generator dependency is one-directional the *other* way — `Compono.Generators` (packed into `Compono.nupkg`, not `Compono.Logging.nupkg`) knows how to look for `ILogger`/`ILogger<T>` symbols *if present*, gated safely by `LoggingWellKnownTypes.TryCreate` returning `null` when they aren't.
- **Shared infra**: none beyond `CallVerifier` and the stage-6 provider contract — no shared exception hierarchy, no shared `CompositionPath`/diagnostics helper reuse (`LoggingProvider.FriendlyTypeName` is a deliberately re-implemented local helper specifically because `CompositionPath.FriendlyTypeName` is `internal` to core `Compono` with no `InternalsVisibleTo` grant).
- **Merely packaging**: none — every core reference is load-bearing, not vestigial.

## 4. Existing 1.0 public contract

Confirmed by reading the actual types (`src/Compono.Logging/*.cs`), not just the ADR's illustrative snippet:

```csharp
public static class CompositionBuilderExtensions
{
    public static CompositionBuilder UseLogging(this CompositionBuilder builder, Action<LoggingOptions>? configure = null);
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
    public string Message { get; }
    public object? State { get; }
    public IReadOnlyList<KeyValuePair<string, object?>>? Properties { get; }
    public string? MessageTemplate { get; }
    public IReadOnlyList<object> Scopes { get; }
    public DateTimeOffset Timestamp { get; }
    // constructor is internal — a consumer inspects, never fabricates, an entry
}

public sealed class CapturingLogger : ILogger
{
    public CapturingLogger(LoggingOptions? options = null);
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    public CapturingLogger(LoggingOptions? options = null);
}

public static class LoggerTestingExtensions   // extension methods on ILogger
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
    public void Once();
    public void Never();
    public void Exactly(int times);
}

public static class LoggingFactoryRegistry   // generator infrastructure, [EditorBrowsable(Never)] on Register<T>
{
    public static void Register<TCategory>(Func<LoggingOptions, object> factory);
    public static bool TryCreate(Type requestedType, LoggingOptions options, out object? value);
}
```

`CallVerifier` itself (`src/Compono/CallVerifier.cs`) is core, reused unmodified: `readonly struct CallVerifier(int observedCount, string memberDescription)` with exactly `Never()`/`Once()`/`Exactly(int)` — **no `AtLeast`/`AtMost`/ordering**, "deliberately minimal... per ADR-0044 Requirement 3." This is a load-bearing fact for §8 below: any `AtLeast(n)`/`AtMost(n)` on `LogVerificationBuilder` cannot be a one-line forward to `CallVerifier` the way `Once`/`Never`/`Exactly` are — `CallVerifier` doesn't have those members, and ADR-0044 deliberately kept it that way for the whole `Compono.TestDoubles` surface, not just Logging.

## 5. Consumer ergonomics review

What a call like `logger.LogInformation("Processed order {OrderId} for {CustomerId}", orderId, customerId)` produces in `CapturedLogEntry`, verified by reading `LogEntryCollector.Record`/`ExtractStructuredState` (`src/Compono.Logging/LogEntryCollector.cs:36-119`):

| Captured today | Publicly accessible today | Notes |
|---|---|---|
| `LogLevel` | Yes (`.LogLevel`, `Verify().AtLevel(...)`) | |
| `EventId` | Yes (`.EventId`, `Verify().WithEventId(...)`) | Only equality filter exists on `Verify()`; no "any EventId with this Id ignoring Name" partial-match helper. |
| `Exception` | Yes (`.Exception`, `Verify().WithException<T>()`) | Type-only filter; no message/property predicate on the exception itself beyond `Matching(...)`. |
| Formatted message (`"Processed order 42 for 7"`) | Yes (`.Message`, `Verify().WithMessageContaining(...)`) | Only a `Contains` filter; no exact-match, prefix, `StringComparison` choice, or regex helper on `Verify()` itself (`Matching` covers it, but is unnamed/undescribed in verification failure output — see §6). |
| Message template (`"Processed order {OrderId} for {CustomerId}"`) | Yes, but **only via direct inspection** — `.MessageTemplate` on `CapturedLogEntry`. **Not reachable through `Verify()` at all.** | A consumer wanting `Verify().WithMessageTemplate("...")` must drop to `.Matching(e => e.MessageTemplate == "...")`. |
| Structured property names/values (`OrderId=42`, `CustomerId=7`) | Yes, but **only via direct inspection** — `.Properties` on `CapturedLogEntry`. **Not reachable through `Verify()` at all.** | Same gap: `Verify().WithProperty("OrderId", 42)` doesn't exist; must use `.Matching(...)`. |
| Scopes | Yes (`.Scopes`) | **Not reachable through `Verify()` at all** — no `Verify().InScope(...)`/`WithScopeContaining(...)`. Must use `.Matching(...)` against `e.Scopes`. |
| Call count semantics | `Once`/`Never`/`Exactly(n)` | No `AtLeast(n)`/`AtMost(n)`/`Between(min,max)` — see §4's `CallVerifier` constraint. |
| Retrieving *which* entries matched a `Verify()` chain | **No.** `Verify()` is fluent-only and terminal (`void`-returning `Once`/`Never`/`Exactly`) — a consumer who wants the matched entries themselves (e.g., to assert further on `Properties` after confirming `Once()`) must re-filter `GetCapturedEntries()` by hand with the same predicate logic already written into the `Verify()` chain. | This is the most concrete "info captured internally, hard to access" gap: `LogVerificationBuilder.ToCallVerifier()` (`src/Compono.Logging/LogVerificationBuilder.cs:66-87`) already computes the exact matching entries internally (it iterates and applies every filter) but discards everything except the count before constructing `CallVerifier`. |
| Ordering across entries (e.g. "the warning happened before the retry log") | Entries are `IReadOnlyList<CapturedLogEntry>` in append order (`LogEntryCollector._entries`, oldest-first) via `GetCapturedEntries()`, so ordering *is* inspectable directly. | No `Verify()`-level ordering assertion exists (deliberately out of scope for `CallVerifier`-family verification generally, ADR-0044 Requirement 3 — not Logging-specific). |
| Negative verification | `Never()` | Already covered by existing `CallVerifier.Never()` reuse. |

Bottom line: **nothing about a `LogInformation(...)`/`[LoggerMessage]` call is uncaptured.** Every piece of information this research's brief asked about (message template, structured properties, EventId, exception, scopes) is already extracted and present on `CapturedLogEntry` in 1.0. The actual gap is entirely in `Verify()`'s filter vocabulary and in `Verify()`'s inability to hand back its matched-entry set — a fluent-ergonomics gap, not a capture gap.

## 6. Observed or likely friction

1. **Structured-property/template verification requires dropping to `.Matching(...)` with no message-shaping.** A consumer asserting `OrderId == 42` today writes `logger.Verify().Matching(e => e.Properties?.Any(p => p.Key == "OrderId" && Equals(p.Value, 42)) == true).Once()` — verbose, and on failure `LogVerificationBuilder.Describe()` (`LogVerificationBuilder.cs:89-103`) renders it as the generic `"a custom condition"`, losing the specific property/value in the failure message that `AtLevel`/`WithEventId`/etc. already produce (`"level Warning"`, `"event id ..."`). This is the single clearest, most concrete ergonomic gap: named filters get descriptive failure text; the escape hatch doesn't.
2. **No way to get the matched entries back after `Verify()`.** A test that wants both "exactly one warning was logged" and "that warning's OrderId property was 42" must write the filter predicate twice — once inside `Verify().Matching(...)`, once again against `GetCapturedEntries()` — because `Once()`/`Never()`/`Exactly()` are `void`. `LogVerificationBuilder` already has the matched list in hand internally and throws it away.
3. **`AtLeast(n)`/`AtMost(n)` don't exist**, for a real reason (`CallVerifier` doesn't have them, ADR-0044), but this is a plausible ask a consumer used to `TUnit.Mocks.Logging`'s `Times.AtLeastOnce`-style API or `FakeLogger`'s manual-LINQ flexibility might reach for.
4. **No skill/doc/eval evidence of live consumer complaints** — `git log --oneline -- src/Compono.Logging` shows exactly one substantive commit (the original PR #116) since 1.0 shipped, with no follow-up bug reports, no reopened ADR-0055 amendment, and no dogfooding-surfaced friction beyond what Amendment 4 already fixed (the generation-ownership collision, unrelated to consumer-facing verification ergonomics). The friction identified here is derived from reading the actual `LogVerificationBuilder`/`CapturedLogEntry` code against the stated capture set, not from an observed complaint — flagged honestly rather than manufactured as urgent.

## 7. Candidate improvements

1. **`WithProperty(string key, object? value)` / `WithMessageTemplate(string template)` filters on `LogVerificationBuilder`** — closes the §5/§6-#1 gap directly: named, described filters for the two pieces of `CapturedLogEntry` (`Properties`, `MessageTemplate`) that are captured but only reachable through `.Matching(...)` today.
2. **A way to retrieve the entries a `Verify()` chain matched**, e.g. a non-terminal `.Entries()`/`.ToList()` returning `IReadOnlyList<CapturedLogEntry>` alongside (not instead of) the existing `Once()`/`Never()`/`Exactly()` terminals — closes §6-#2.
3. **`AtLeast(int)`/`AtMost(int)` terminals on `LogVerificationBuilder`.**
4. **A `WithMessage(string)` exact-match filter** alongside the existing `WithMessageContaining`, and/or a `StringComparison` overload on `WithMessageContaining`.
5. Standalone-package split (`Compono.Logging` + `Compono.Logging.Composition` or similar) — evaluated as its own axis, §9-§10, not folded into the ranked list.

## 8. Detailed analysis of each serious candidate

**Candidate 1 — `WithProperty`/`WithMessageTemplate` filters.**
- Additive: two new methods on `LogVerificationBuilder`, same shape as the five that already exist (`Add(description, predicate)`), zero change to any existing member, zero change to `CapturedLogEntry` or `CapturingLogger`.
- Directly closes the concretely-identified gap (§6-#1): captured-but-only-`Matching`-reachable information gets first-class, described filters.
- `WithProperty` needs an equality semantics decision (ordinal/structural `Equals`, `null`-value handling — `Properties`' value slot is already nullable per ADR-0055's "Properties nullability" decision) but no new abstraction; mirrors `WithEventId`'s existing `Equals`-based filter shape almost exactly.
- `WithMessageTemplate` is a straight `entry.MessageTemplate == template` predicate, ordinal string compare — as simple as `AtLevel`.
- Low risk, no ADR-0055 boundary crossed (§4's "structured property names/values" was always captured; this only extends `Verify()`'s vocabulary to reach what's already on `CapturedLogEntry`).

**Candidate 2 — retrieve matched entries from `Verify()`.**
- Additive if designed as a new non-terminal method (`.Entries()` or similar) that reads the same internal filtered list `ToCallVerifier()` (`LogVerificationBuilder.cs:66-87`) already computes, called *instead of* (not chained after) `Once()`/`Never()`/`Exactly()` — the existing terminals stay `void`, unmodified, so nothing about the 1.0 contract changes.
- Real, concrete value: eliminates the double-predicate-writing friction in §6-#2 without inventing a second verification concept — it's direct inspection of the same collector, filtered, which is philosophically identical to what `GetCapturedEntries()` already offers, just pre-filtered.
- Naming risk: must not read as a second "verification" terminal (that would blur `LogVerificationBuilder`'s "filter-then-assert" contract, which ADR-0055 was careful to keep as one verb, `Verify()`, ending in one of three assertion terminals). A getter-shaped name (`.Entries()`, `.Matches()`) rather than an assertion-shaped one avoids that.

**Candidate 3 — `AtLeast(int)`/`AtMost(int)`.**
- **Not a one-line `CallVerifier` forward** like `Once`/`Never`/`Exactly` are — `CallVerifier` has no such members, by ADR-0044's deliberate design ("no call-order verification... deliberately minimal," `src/Compono/CallVerifier.cs:4-6`), a decision that applies to the entire `Compono.TestDoubles`/`Compono.Http`/`Compono.Logging` verification family, not something Logging can unilaterally reinterpret for itself.
- Two honest paths: (a) implement `AtLeast`/`AtMost` locally inside `LogVerificationBuilder` without going through `CallVerifier` (straightforward — `ToCallVerifier()`'s `matchCount` is already computed; a local `if (matchCount < n) throw new TestDoubleVerificationException(...)` is a few lines), which is additive to Logging alone and doesn't touch core; or (b) propose extending `CallVerifier` itself, which is an ADR-0044-boundary change affecting `Compono.TestDoubles`/`Compono.Http` too and is explicitly out of this research's scope (that would need its own ADR amendment against ADR-0044, not a Logging-only 1.1 change).
- Recommendation if pursued: path (a) only — keep it Logging-local, additive, and out of `CallVerifier`'s established minimal contract. This avoids relitigating ADR-0044 to ship a Logging-specific ergonomic want.
- Genuinely useful but the least differentiated of the three — `Once`/`Never`/`Exactly` already cover most real assertions, and no consumer evidence (§6-#4) specifically asks for it.

**Candidate 4 — `WithMessage` exact-match / `StringComparison` overload.**
- Small, real, but marginal: `WithMessageContaining` already covers the dominant real-world case (substring assertion tolerant of exact wording drift), and an exact-match variant mostly matters for brittle tests that arguably shouldn't assert exact formatted-message text anyway (message templates change wording; `WithMessageTemplate`, candidate 1, is the more future-proof answer to "assert on message content precisely"). Included for completeness, not ranked.

## 9. Standalone-package feasibility

**Empirically confirmed** (§11 experiment): the capture core — `CapturedLogEntry`, `CapturingLogger`, `CapturingLogger<T>`, `LogEntryCollector`, `LoggingOptions`, `ICapturingLoggerFacade` — has **zero** references to any core `Compono` type today. It compiles standalone against `Microsoft.Extensions.Logging.Abstractions` alone. This is not a discovered accident; it follows directly from ADR-0055's original architecture (§2-§3 above): the hand-written logger pair was always designed to be "directly, publicly constructible... composing through `UseLogging()` is not required" (doc comments on both `CapturingLogger` and `CapturingLogger<T>`).

What actually ties `Compono.Logging` to core `Compono` is narrow and named exactly in §3:
1. `CompositionBuilderExtensions.UseLogging`/`LoggingProvider` — the `Composer.Create(builder => ...)` composition integration.
2. `LogVerificationBuilder`'s three terminals forwarding to `CallVerifier`.

`LoggerTestingExtensions` (`GetCapturedEntries`/`GetLastCapturedEntry`/`ClearCapturedEntries`/`Verify`) touches core only transitively, through `Verify()`'s return type (`LogVerificationBuilder`) — the first three members have no core dependency at all.

**Conclusion: the capture-and-inspect half of `Compono.Logging` is already, today, a coherent standalone experience independent of Compono composition** — a consumer could `new CapturingLogger<OrderService>()`, pass it to `OrderService`'s constructor by hand (no `Composer.Create` involved), and call `GetCapturedEntries()`/`GetLastCapturedEntry()`/`ClearCapturedEntries()` with no core `Compono` type ever touched at runtime (only at compile time, transitively, through the `Compono.Logging.csproj` `ProjectReference` that exists for `Verify()`/`CallVerifier`, which the standalone consumer simply wouldn't call). This isn't a hypothetical architecture proposal — it's already true of the shipped 1.0 code, and the docs already say so explicitly (ADR-0055 xmldoc on both logger types; RESEARCH-0013 §3's "no factory needed for the common case" framing was itself modeled on `FakeLogger<T>`'s identical standalone-constructibility).

The only genuinely coupled piece is `Verify()` — and even that coupling is a single `CallVerifier` struct with three members, not a deep dependency.

## 10. Architectural options for independence

Given §9's finding, the real question is not "can capture be decoupled" (it already is, at the type level) but "is a **package-level** split (`Compono.Logging` + `Compono.Logging.Composition`) worth doing":

- **Option A — status quo (one package, already-decoupled types).** The csproj-level `ProjectReference` to core `Compono` exists, but nothing forces a consumer who never calls `UseLogging()`/`Verify()` to pay any *runtime* cost for it — the JIT/AOT compiler only pulls in what's actually called. The only cost is a compile-time transitive reference to `Compono.dll` even for a consumer who only wants direct construction. Given `Compono.Logging` already requires `Compono` as a `PackageReference` anyway (documented "install both" in `docs/packages/compono-logging.md`), this transitive reference costs nothing a consumer wasn't already paying.
- **Option B — split `Compono.Logging` (capture+inspect, zero core dependency) from `Compono.Logging.Composition` (adds `UseLogging()`/`Verify()`/`CallVerifier` reuse, depends on core `Compono`).** This would let a consumer who wants only `CapturingLogger<T>` + `GetCapturedEntries()` (no Compono composition anywhere in their test suite) install one package with **zero** transitive `Compono` reference. Evaluated against real value below.
- **Option C — depend on `Microsoft.Extensions.Diagnostics.Testing`'s `FakeLogger<T>` for capture, keep only a thin Compono-composition/`Verify()` layer.** Already considered and rejected in ADR-0055 itself (Option 3 under "Package/routing," Decision Outcome §"Package identity and dependency graph") for unconfirmed AOT story and heavier transitive surface — nothing in this research changes that calculus; not re-litigated further here.

**Is Option B worth it?** No — for three concrete reasons, not a vague "keep it simple":
1. **No real consumer asks for `Compono.Logging` without `Compono`.** `Compono.Logging` is a Compono integration package by name and by every real usage example in the repo (`docs/packages/compono-logging.md`, `skills/compono/references/logging.md`, `samples/Compono.Samples.BasicUsage/LoggingTests.cs`) — every one of them composes through `Composer.Create`/`UseLogging()`. A "just give me `CapturingLogger<T>` with no Compono at all" consumer is a *possible* standalone-testing-library use case but is not what anyone installing `Compono.Logging` is actually doing today (there is zero evidence — no issue, no consumer request, no dogfooding finding — that anyone wants this).
2. **A split would create exactly the "awkward behavior around generated `ILogger<T>` ownership" the brief asked to check for.** `LoggingFactoryRegistry`/generated activation (ADR-0055 Amendments 1-3) exists specifically to let `Composer.Create` resolve a closed `ILogger<T>` request through Compono's stage-6 provider pipeline. If capture+inspect lived in a package with no reference to `CompositionBuilder`/`ICompositionValueProvider` at all, `UseLogging()` and `LoggingProvider` would have nowhere to live except the "Composition" half — meaning the generated-activation machinery (`LoggingFactoryRegistry`, the `[ModuleInitializer]`, `Compono.Generators`' `LoggingActivationEmitter`) would *also* need to move to, or straddle, the Composition package, since it exists purely to serve `LoggingProvider`. This doesn't cleanly cut along the "capture vs. composition" seam the brief hypothesized — the generated-activation registry is composition-integration infrastructure, not capture infrastructure, even though it currently lives in the same physical project as the capture types. Splitting would either (a) leave `LoggingFactoryRegistry` in the capture-only package for no architectural reason (it's meaningless without `LoggingProvider`), or (b) move it to the Composition package, which then needs `[ModuleInitializer]`-generated code (compiled into the *consumer's* assembly) to reference a package the consumer may not have installed if they only wanted plain capture — a real, not hypothetical, dependency-direction problem.
3. **The cost being solved for is already close to zero (§9's Option A analysis)** — a compile-time-only transitive `ProjectReference`/`PackageReference` to `Compono`, for a package whose entire raison d'être (per its own name, `Compono.<Ecosystem>`, and every one of its docs) is Compono composition integration. Splitting trades a real, working, already-decoupled-at-the-type-level design for two packages, two release cadences, two sets of docs, and a real architectural seam mismatch (#2), to save a cost that doesn't meaningfully exist for the audience this package serves.

**Conclusion: do not split.** The standalone-viable *design* (§9) should be named and preserved as an explicit, documented property of the existing single package — worth a doc callout (§13/§17) — but a package-level split has no real consumer value and one concrete architectural cost.

## 11. Experiments performed

**Standalone-capture compile spike** (proves/disproves §9's claim directly, rather than reasoning from the dependency graph alone): copied `CapturedLogEntry.cs`, `CapturingLogger.cs`, `CapturingLogger{T}.cs`, `LogEntryCollector.cs`, `LoggingOptions.cs`, `ICapturingLoggerFacade.cs` unmodified into a throwaway project (`/private/tmp/.../scratchpad/standalone-test/CapturingCore/`) referencing only `Microsoft.Extensions.Logging.Abstractions` — no `Compono` reference at all:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
  </ItemGroup>
</Project>
```

Result: **`dotnet build` succeeded, 0 warnings, 0 errors.** Confirms §9's claim by direct compilation, not just by absence-of-reference grep — the capture core genuinely has no hidden core-`Compono` coupling (e.g., via a shared `GlobalUsings.cs` or implicit base type) that a csproj-level grep alone might have missed. Scratch files were not committed and are not part of any src/ change.

## 12. External research

- **`FakeLogger`/`FakeLogRecord`** (`Microsoft.Extensions.Diagnostics.Testing`, current per Microsoft Learn and NuGet as of this research pass): `FakeLogger<T>` is directly, publicly constructible with no `ILoggerFactory` — the same "no factory needed" ergonomics `Compono.Logging` already adopted. `FakeLogRecord` keeps raw `State` **and** a derived `StructuredState`/`GetStructuredStateValue(key)` surface side by side — the same "raw + derived" shape `CapturedLogEntry` already adopted (RESEARCH-0013 §3, re-confirmed here). Critically for §7-#3: **`FakeLogCollector` itself has no `Times`/`AtLeast`/`AtMost` verification API at all** — its entire query surface is `GetSnapshot()` plus manual LINQ. This means Compono.Logging's existing `Once`/`Never`/`Exactly` fluent terminals are already *ahead* of Microsoft's own official capture library on verification ergonomics; adding `AtLeast`/`AtMost` would extend that lead, not catch up to a gap.
- **`TUnit.Mocks.Logging`** (beta, per RESEARCH-0013 §4, re-confirmed as still the relevant shape): `logger.VerifyLog().AtLevel(...).ContainingMessage(...).WasCalled(Times.Once)` — a fluent filter-then-count-terminal shape structurally identical to `Compono.Logging`'s own `Verify()...Once()`. TUnit's own public surface, per that research, does **not** expose structured-property or scope filters either — narrower than `Compono.Logging`'s `CapturedLogEntry`, not broader. No new evidence from this pass changes that assessment; `Compono.Logging`'s captured-entry model remains the more complete of the two.
- Net effect on ergonomics ideas: external prior art validates the *existing* 1.0 design more than it suggests unmet capability — neither `FakeLogger` nor `TUnit.Mocks.Logging` has a richer verification vocabulary than what candidates 1-2 in §7 would add to `Compono.Logging`. This is inspiration confirming direction, not a parity gap to close.

Sources: [FakeLogger Class (Microsoft.Extensions.Logging.Testing) — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.testing.fakelogger), [Testing logging code with Microsoft.Extensions.Logging and FakeLogger](https://blog.elmah.io/testing-logging-code-with-microsoft-extensions-logging-and-fakelogger/), [NuGet Gallery — Microsoft.Extensions.Diagnostics.Testing](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics.Testing/).

## 13. Compatibility implications

- **Source compatibility**: all three ranked candidates (§16) are pure additions — new methods on `LogVerificationBuilder`, no existing signature changed, no existing method removed or given new required parameters. No consumer code that compiles against 1.0 stops compiling against 1.1.
- **Binary compatibility**: additive members on a `sealed class` are binary-compatible additions (no interface implementation changes on `LogVerificationBuilder` — it's a concrete sealed type, not an interface). `CapturedLogEntry` is untouched by every ranked candidate.
- **Generated-source compatibility**: none of the ranked candidates touch `Compono.Generators`' `LoggingActivationEmitter`/`LoggingActivation.scriban` or `LoggingWellKnownTypes` — the generated `[ModuleInitializer]`/`LoggingFactoryRegistry.Register<T>` shape is entirely unaffected. No regeneration-triggering change.
- **Analyzer/generator behavior**: unaffected — `CMP0038`/`CMP0039` diagnostics are about generated-activation discovery, not verification API surface.
- **Runtime behavior**: `WithProperty`/`WithMessageTemplate`/an `.Entries()`-style accessor only ever run inside a new code path a consumer opts into by calling the new member; no existing `Verify()` chain's behavior changes.
- **Native AOT / trimming**: every ranked candidate is ordinary generic-free, reflection-free C# (predicate delegates and struct/list operations, the same shape `AtLevel`/`WithEventId`/`Matching` already use) — no new reflection, no new dynamic dispatch, consistent with `Compono.Logging`'s existing `<IsAotCompatible>true</IsAotCompatible>` claim (ADR-0055's own AOT requirement, following `Compono.Http`'s ADR-0051 precedent).
- **Deterministic builds**: unaffected — no new source-generation-time nondeterminism introduced (no ranked candidate touches the generator).
- **Package dependency changes**: none of the three ranked candidates change `Compono.Logging.csproj`'s dependency list. The rejected standalone-split idea (§9-§10) would have been the one candidate with real package-dependency-graph implications; it's rejected specifically so this section stays "none."
- **SemVer**: all three ranked candidates are additive-only — correctly a **minor** version bump (1.1.0), not a major one. No existing public member's signature, nullability, or return type changes.

## 14. AOT/trimming/generator implications

Covered in §13 above; restated briefly because the brief calls it out as its own numbered item: no ranked candidate adds reflection, dynamic proxying, or source generation of any kind. `LoggingActivationEmitter`/`LoggingWellKnownTypes`/`LoggingFactoryRegistry` (the actual generator-touching surface) are untouched by every ranked candidate — none of the three candidates changes what gets generated, when, or how activation is discovered.

## 15. Rejected ideas

- **`AtLeast`/`AtMost` implemented by extending core `CallVerifier`** — rejected as *this* research's recommendation (not rejected outright as an idea): it's a real ADR-0044 boundary change affecting `Compono.TestDoubles`/`Compono.Http` too, out of scope for a Logging-only 1.1, and would need its own ADR-0044 amendment with its own consumer-evidence bar. If pursued at all, path (a) in §8 (Logging-local implementation, bypassing `CallVerifier`) is the additive, in-scope version.
- **Package split (`Compono.Logging` + `Compono.Logging.Composition`)** — rejected, §10, on concrete grounds (no consumer evidence, real generated-activation-ownership seam mismatch, near-zero cost being solved for), not "keep it simple" hand-waving.
- **`ILoggerFactory` composition support** — already an explicit ADR-0055 v1 non-goal ("no native `ILoggerFactory` support in v1... not becoming general logging infrastructure") with no new evidence in this pass to revisit it; correctly out of a 1.1 scope focused on closing existing-capability ergonomics gaps, not expanding responsibility.
- **A category-string constructor for the non-generic `CapturingLogger`** — an explicit ADR-0055/skill-documented v1 boundary (`skills/compono/references/logging.md`'s "v1 boundaries" list); no new evidence surfaced here to revisit it, and it would blur `CapturingLogger` vs. `CapturingLogger<T>`'s clean split.
- **Per-level `ControlLevel`-style toggle (replacing the single `MinimumLevel` threshold)** — already an explicit ADR-0055 non-goal ("No `ControlLevel`-style per-level toggle is added here — out of scope"); FakeLogger has it, but no real consumer evidence from either the original ADR-0055 research or this pass asks for it, and a single threshold has covered every real case documented so far.
- **Ordering assertions across log entries** (e.g. "warning happened before retry") — `GetCapturedEntries()` already exposes append order directly for a consumer who needs this; adding an ordering-assertion DSL to `Verify()` would be new abstraction on top of information already retrievable by hand, and no consumer evidence asks for it — manufacturing scope, not closing a gap.

## 16. Ranked recommendations

1. **`WithProperty(string, object?)` and `WithMessageTemplate(string)` filters on `LogVerificationBuilder`** (§7 candidate 1, §8 detail). Strongest candidate: closes a concretely-identified, code-verified gap (§5/§6-#1) — structured properties and message templates are *already captured* on `CapturedLogEntry` in 1.0 but only reachable through `Verify()`'s generic `.Matching(...)` escape hatch, which also degrades failure-message quality (`"a custom condition"` vs. a named, specific description). Purely additive, zero core-Compono touch, zero AOT/generator implication, mirrors the exact existing pattern of every other filter method on the same class.
2. **A non-terminal accessor to retrieve `Verify()`'s matched entries** (§7 candidate 2, §8 detail), e.g. `.Entries()` returning `IReadOnlyList<CapturedLogEntry>`, additive alongside the existing `Once`/`Never`/`Exactly` terminals. Closes the double-predicate-writing friction (§6-#2) that's structurally present today — `LogVerificationBuilder` already computes the matched set internally and discards it. Needs careful naming so it reads as inspection, not a second assertion verb, to protect ADR-0055's one-verb `Verify()` contract.
3. **`AtLeast(int)`/`AtMost(int)` terminals, implemented Logging-locally (not via `CallVerifier`)** (§7 candidate 3, §8 detail). Real and additive, but ranked third: less concretely evidenced than 1-2 (no specific captured-but-unreachable data point behind it, just a plausible ergonomic want validated only by TUnit's `Times.AtLeastOnce`-shaped prior art, §12), and it must be implemented carefully to avoid quietly extending `CallVerifier`'s deliberately-minimal ADR-0044 contract.

`WithMessage`/`StringComparison` overload (§7 candidate 4) and the standalone-package split (§9-§10) are explicitly **not** ranked — the former is too marginal against the already-strong `WithMessageContaining`+`WithMessageTemplate` combination, the latter is a rejected architectural idea (§15), not a feature.

## 17. Recommended 1.1 scope

Ship candidates 1 and 2 together (`WithProperty`/`WithMessageTemplate` filters plus a matched-entries accessor) as the core of a `Compono.Logging` 1.1: both are small, additive, evidence-backed extensions of the exact same `LogVerificationBuilder` class, touch no other package, and require no ADR-0044 boundary discussion. Candidate 3 (`AtLeast`/`AtMost`) is a reasonable stretch addition to the same release if there's appetite, implemented Logging-locally per §8's path (a) — but it's the more discretionary of the three and could just as easily wait for real consumer demand.

Separately, document (not code) that `CapturingLogger`/`CapturingLogger<T>` are already, today, usable with zero Compono composition and zero core-`Compono` runtime dependency — this is true now, is already partially stated in the two types' own xmldoc, but isn't called out anywhere a consumer evaluating "do I need to buy into Compono composition to use this" would see it (`docs/packages/compono-logging.md`'s "When to install" section leads with composition). A documentation clarification, not an ADR or code change — flagged here per the brief's item 13, left as a note for whoever picks up 1.1 rather than actioned in this research pass.

Do not pursue the standalone-package split (§9-§10) — it addresses a cost that doesn't meaningfully exist for `Compono.Logging`'s actual audience, and introduces a real generated-activation-ownership seam mismatch that doesn't cleanly resolve.

## 18. Questions or evidence still unresolved

- No real, filed consumer request (issue, dogfooding finding, or otherwise) drives any of candidates 1-3 — this research derived them from reading `CapturedLogEntry`/`LogVerificationBuilder` against the stated capture set and cross-checking against `FakeLogger`/`TUnit.Mocks.Logging` prior art, not from an observed friction report. Worth validating against `alexa-vox-craft`/`structured-logging` (the two real consumers ADR-0055's own research cited) before committing 1.1 scope, the same way ADR-0055's original research did for v1.
- `WithProperty`'s exact equality semantics (structural vs. reference, how to handle a `null` captured value against a non-null expected value or vice versa) needs a small design decision before implementation — flagged, not resolved, here.
- The naming of the matched-entries accessor (`.Entries()` vs. `.Matches()` vs. something else) needs a decision that protects the "one verb, `Verify()`" contract ADR-0055 was explicit about — this research flags the risk but doesn't pick a name.
- Whether `AtLeast`/`AtMost` belongs on `LogVerificationBuilder` at all, versus waiting for a broader `CallVerifier`-family ADR-0044 amendment that would give `Compono.TestDoubles`/`Compono.Http` the same capability consistently, is a real open product question this research surfaces but does not resolve — recommending the narrower, Logging-local path (§8) is a scoping choice for *this* research pass, not a claim that the broader question is settled.
