# [PLAN-0055] `Compono.Logging`: First-Class `Microsoft.Extensions.Logging` Testing Support — Implementation Plan

**Status:** Done

**Implements:** [ADR-0055](../adr/0055-compono-logging-testing-support-package.md) (including Amendment 1 — generated closed-generic activation for `ILogger<T>`; Amendment 2 — public `LoggingFactoryRegistry`; Amendment 3 — logging activation generation lives in the existing `Compono.Generators`, gated by `ComponoGeneratedLogging`)

## Goal

A new `Compono.Logging` package ships with `UseLogging()`, a
`CapturingLogger`/`CapturingLogger<T>` pair, `CapturedLogEntry`, direct
inspection (`GetCapturedEntries()`/`GetLastCapturedEntry()`/
`ClearCapturedEntries()`), and `Verify()`-based fluent verification —
exactly the public API ADR-0055 froze, including its amended contracts
(non-Compono-logger failure semantics, real `MinimumLevel` filtering with
correct `LogLevel.None` handling, direct-construction support, Amendment
1's generated closed-generic activation for `ILogger<T>`, and Amendment
3's `ComponoGeneratedLogging`-gated generation living inside the existing
`Compono.Generators`). Done means: `Compono.Logging.nupkg` (runtime
assembly + its tiny MSBuild props asset — **no** generator/analyzer DLL
of its own) and `Compono.nupkg` (its existing `Compono.Generators.dll`,
now also emitting logging activation) both build and pack, every
ADR-0055 behavioral contract — including Amendments 1/3's
activation/discovery/property-gating/diagnostic contract — has a passing
test proving it, `Compono.Logging` is AOT/trim-clean end-to-end through
the *generated* composition path (not only direct construction), every
documentation/skill surface ADR-0055 names is updated and consistent
with the shipped API, the `compono` skill's eval suite has a passing
Compono.Logging scenario exercising real `UseLogging()` composition, and
both dogfood targets (`LayeredCraft.StructuredLogging`, `alexa-vox-craft`)
have been validated against freshly packed local packages via
`scripts/dogfood-validate.sh` — see "Status contract" below for exactly
how that last item relates to this plan's own `Status: Done`.

## Scope

**In scope**: everything ADR-0055's Decision Outcome (including
Amendments 1-3) specifies — `Compono.Logging` package creation, logging
activation generation added to the *existing* `Compono.Generators`
(reusing its discovery pipeline directly, gated by
`ComponoGeneratedLogging`), `LoggingFactoryRegistry`, `LoggingProvider`/
stage-6 registration, the full public API surface, structured-state/
scope/concurrency/`MinimumLevel` semantics, direct construction, the
non-Compono-logger failure diagnostic, the missing-generated-activation
diagnostic, the missing-runtime-symbols compile-time diagnostic, and the
full documentation/skill/eval synchronization ADR-0055 makes part of
completion.

**Explicitly deferred** (per ADR-0055's own "Explicit initial-package
boundaries" and `[Shared]` sections, and per Amendments 1/3's own scope
limits — not this plan's job to solve): `ILoggerFactory`,
Serilog/provider-specific behavior, test-runner output capture/routing,
DI integration, cross-scope structured-property flattening,
`FakeLogger`-style per-level `ControlLevel`, a category-string
`CapturingLogger` constructor, any change to `[Shared]` or a new sharing
mechanism, any change to the composition pipeline/provider precedence
model, any change to `TransitiveClosureWalker`/`LeafTypeClassifier`'s
visibility, solving ADR-0052's still-open "Finding B" (nested
`context.Resolve<T>()` discovery), and **changing `Compono.TestDoubles`'
own `ComponoGeneratedTestDoubles` default** (see "Not in this plan:
`Compono.TestDoubles`' default," below).

**PR sequencing**: task groups 1-17 — the `Compono.Logging` runtime
package, the logging-activation addition inside `Compono.Generators`,
`LoggingFactoryRegistry`, every behavioral test group, Native
AOT/trimming validation, and the full documentation/skill/eval
synchronization — are **all** in-repo work and ship as **one coherent,
single PR**. ADR-0055 (with its amendments) still describes one package,
one decision; the generator addition is part of that one decision, not a
separately-scoped feature, so it does not get its own PR. Task group 18
(dogfood migration against `LayeredCraft.StructuredLogging`/
`alexa-vox-craft`) touches separate repositories and, per
`design-decisions.md`'s phase-per-PR rule, ships as its own follow-on
PR(s) against those repos after `Compono.Logging` itself has merged and
been published — the same sequencing PLAN-0051 used for its own
`alexa-vox-craft` migration (task group 10 there). See "Status contract"
below for how task group 18 relates to this plan reaching `Status: Done`.

**Status contract**: **PLAN-0055 follows PLAN-0051's real, inspected
precedent** — its own `Status: Done` requires task group 18 (dogfood
migration) to be substantially complete, exactly like task groups 1-17,
even though task group 18's actual code changes land as separate PRs in
`layered-craft/structured-logging`/`alexa-vox-craft`, not in this repo
(PLAN-0051 itself reached `Status: Done` only after its own
`alexa-vox-craft` dogfood migration, task group 10, was substantially
complete, despite that migration's commits landing in a separate repo).

**Not in this plan: `Compono.TestDoubles`' default.** ADR-0055 Amendment
3 explicitly declines to change `ComponoGeneratedTestDoubles`' existing
pure-opt-in behavior here — a real, confirmed compatibility risk (any
existing consumer who references `Compono.TestDoubles`, calls both
`UseGeneratedTestDoubles()` and `UseNSubstitute()`/another stage-6
provider, and never set the flag today is currently, silently, served
entirely by the other provider; flipping the default would silently
change that) rules out treating it as a mechanical follow-on. Tracked as
a separate, future ADR-0043 amendment plus its own small implementation
plan — not part of PLAN-0055.

## Tasks

### 1. Package/project creation (`Compono.Logging` runtime)

- [x] Create `src/Compono.Logging/Compono.Logging.csproj` —
  `TargetFrameworks` matching the rest of the repo (`net8.0;net9.0;net10.0;net11.0`,
  per [ADR-0038](../adr/0038-net8-net9-explicit-multi-target.md)),
  `LangVersion latest`, `ImplicitUsings enable`, `Nullable enable`.
- [x] `<Title>`/`<Description>` following `Compono.Http.csproj`'s
  precedent (a real, specific description, not boilerplate).
- [x] `<IsAotCompatible>true</IsAotCompatible>` — per ADR-0055's
  AOT/trimming section (as corrected by Amendment 1), following
  `Compono.Http`'s precedent exactly.
- [x] `ProjectReference` to `..\Compono\Compono.csproj` only, plus
  `PackageReference` to `Microsoft.Extensions.Logging.Abstractions` (the
  only new external **runtime** dependency ADR-0055 names) — no
  `Microsoft.Extensions.Logging`, no `Microsoft.Extensions.DependencyInjection`,
  no `Microsoft.Extensions.Diagnostics.Testing`, no `Compono.TestDoubles`,
  no `Compono.NSubstitute`.
- [x] **No `ProjectReference`/`OutputItemType="Analyzer"` of any kind** —
  per Amendment 3, `Compono.Logging` owns no generator project at all.
  Logging activation generation is emitted by the *existing*
  `Compono.Generators.dll`, already packed inside `Compono.nupkg`, which
  every `Compono.Logging` consumer already receives transitively through
  its mandatory `Compono` dependency (confirm this transitivity directly
  during task 17, don't just assume it).
- [x] **Pack the new `ComponoGeneratedLogging` default-value MSBuild
  props asset** (task 17's own subtask covers authoring it) into both
  `build/Compono.Logging.props` and `buildTransitive/Compono.Logging.props`
  package paths, mirroring exactly how `Compono.csproj` packs
  `build/Compono.props` today (two `<None Include=... Pack="true"
  PackagePath="build\Compono.Logging.props" />` /
  `PackagePath="buildTransitive\Compono.Logging.props"` entries pointing
  at the same source file).
- [x] Copy the `PinProjectReferenceVersionsExact` MSBuild target from
  `Compono.Http.csproj` (ADR-0031/PLAN-0008 Phase 0 pattern).
- [x] `InternalsVisibleTo` for `Compono.Logging.Tests`.
- [x] Add `src/Compono.Logging/Compono.Logging.csproj` to the solution
  file(s) alongside the other `src/Compono.*` projects.
- [x] Create `test/Compono.Logging.Tests` (mirrors
  `test/Compono.Http.Tests`'s project shape) and
  `test/Compono.Logging.AotSmokeTest` (mirrors
  `test/Compono.Http.AotSmokeTest`'s two-proof pattern — see task 15).

### 2. Logging activation generation inside the existing `Compono.Generators` (ADR-0055 Amendment 3)

**No `Compono.Logging.Generators` project exists.** This task adds a
narrowly scoped, clearly separated feature to the *existing*
`src/Compono.Generators` assembly, reusing its discovery pipeline
directly rather than reimplementing it.

- [x] **Well-known-type resolution**: extend `Compono.Generators`'
  existing `WellKnownTypes` infrastructure (the same pattern already
  used for `System.DateTime`/etc.) with optional, nullable entries for
  `Microsoft.Extensions.Logging.ILogger` and the open generic
  `Microsoft.Extensions.Logging.ILogger`1`, resolved via
  `Compilation.GetTypeByMetadataName` — both may legitimately be
  `null` for a consumer who never references
  `Microsoft.Extensions.Logging.Abstractions`. **`Compono.Generators.csproj`
  gains no `Microsoft.Extensions.Logging.Abstractions` package
  reference** — verify this compiles and behaves correctly (returns
  `null` cleanly) against a compilation that never references that
  package at all.
- [x] **`ComponoGeneratedLogging` compiler-visible property**: read once
  via `AnalyzerConfigOptionsProvider`, the same shape
  `ComponoGeneratedTestDoubles` already uses
  (`ComponoIncrementalGenerator.Initialize`'s existing
  `testDoublesEnabled` provider is the direct template to copy). This
  property is the **sole** enable/disable switch for this feature — see
  the property-declaration/default-value tasks under task 17. **Package/
  type presence is deliberately not the gate** — do not use
  `LoggingFactoryRegistry`'s resolvability (or any other
  `Compono.Logging` symbol) as the enabling signal; check that only
  after the property already says "enabled."
- [x] **Discovery reuses the existing pipeline, not a reimplementation of
  it.** Extend `LeafTypeClassifier`/`TransitiveClosureWalker`'s existing
  per-parameter-type classification (the exact call site
  `IsGeneratedTestDoubleEligible` already uses,
  `src/Compono.Generators/Discovery/TransitiveClosureWalker.cs:189`)
  with one more, independently-optional check: is this parameter type a
  closed instantiation of `ILogger<T>`? When `ComponoGeneratedLogging`
  is enabled and the type matches, record `T` into a new discovery
  bucket — **do not** add a second walker, do not duplicate root
  discovery or leaf classification. This reuses, unmodified, the same
  roots `TransitiveClosureWalker` already covers
  (`Composer.Create<T>()`/`CreateMany<T>()`, `[Composable]` in both
  forms, `[Compose]`/`[Compose<TProfile>]` theory-row parameters) and
  the same constructor-selection outcome every other discovery path
  already relies on.
- [x] Deduplicate: recording the same closed `T` more than once (reached
  via two different roots, or the same root twice, possibly across
  different syntax trees) emits exactly one registration for it.
- [x] **Required-runtime-symbols validation, only once the feature is
  enabled**: when `ComponoGeneratedLogging` is enabled and at least one
  closed `ILogger<T>` category was discovered (or, per the diagnostic
  matrix below, even if none was — the property being explicitly `true`
  with a `Compono.Logging` reference absent is itself the failure case),
  resolve `Compono.Logging.LoggingFactoryRegistry`,
  `Compono.Logging.CapturingLogger`1`, and `Compono.Logging.LoggingOptions`
  via `GetTypeByMetadataName`. All three resolvable → proceed to
  emission. Any missing → report the diagnostic below and emit **no**
  logging-registration source at all (never partial/broken generated
  code referencing types that don't exist).
- [x] **New diagnostic** (working id `CMP0038` — confirm the actual
  next-available id against `src/Compono.Generators/AnalyzerReleases.Unshipped.md`
  at implementation time, since other in-flight work may have already
  claimed a number; the working id here is a placeholder, not frozen),
  category `Compono.Logging`, `Info` severity (matching
  `Compono.TestDoubles`' own `CMP0020`-`CMP0032` informational
  convention — nothing broken is emitted, so this isn't a build error):
  "`ComponoGeneratedLogging` is enabled but `Compono.Logging`'s runtime
  types could not be resolved — is `Compono.Logging` referenced?" Add
  its `AnalyzerReleases.Unshipped.md` row alongside the existing
  `CMP0020`-`CMP0037` entries.
- [x] **Emitter, clearly separated**: a new emitter file (e.g.
  `Emitters/LoggingActivationEmitter.cs`) plus its own template (e.g.
  `Templates/LoggingActivation.scriban`), mirroring how
  `TestDoubleAnalyzer`/`Templates/TestDouble.scriban` are already a
  clearly separated addition inside this same assembly, not smeared into
  the core plan-emission code ordinary composable types use. Renders one
  `[ModuleInitializer]`-registering, `file`-scoped registration class
  (matching `GeneratedTestDoubleRegistry`'s own precedent for "never
  referenced by name, stays `file`-scoped" per this repo's "Generated
  code" coding standard) calling
  `global::Compono.Logging.LoggingFactoryRegistry.Register<{category}>(static options => new global::Compono.Logging.CapturingLogger<{category}>(options));`
  per discovered closed category — every type reference `global::`-qualified,
  per the same standard.
- [x] `ComponoIncrementalGenerator.Initialize` wires this feature in as
  one additional, clearly-labeled step alongside its existing
  test-double-eligibility step — not interleaved into unrelated logic.

### 3. `LoggingFactoryRegistry`

- [x] **`public static class LoggingFactoryRegistry`** in
  `src/Compono.Logging` — **`public`, not `internal`**, per ADR-0055
  Amendment 2: consumer-generated `[ModuleInitializer]` code (now emitted
  by core `Compono.Generators`, per Amendment 3, but still landing in the
  *consumer's own assembly*) cannot call an `internal` member across an
  assembly boundary for an arbitrary, unknowable consumer assembly name.
  This is deliberate, exact precedent, not a new pattern —
  `src/Compono/GeneratedTestDoubleRegistry.cs` and
  `src/Compono/RowInvokerRegistry.cs` are both `public` for this same
  reason, and this reasoning is entirely unaffected by *which* generator
  assembly emits the call (Amendment 3 changes ownership, not this).
  Shape:
  ```csharp
  public static void Register<TCategory>(Func<LoggingOptions, object> factory);
  public static bool TryCreate(Type requestedType, LoggingOptions options, out object? value);
  ```
  - **This is a real, deliberate public API addition** — record it as
    such (task 16's public-API validation, below), not as an
    implementation detail that happens to be `public`.
  - **Generator infrastructure, not ordinary consumer-facing API** —
    document it the same way `GeneratedTestDoubleRegistry`/
    `RowInvokerRegistry` are treated: real and public, but never
    presented alongside `UseLogging()`/`CapturingLogger<T>()`/
    `CapturedLogEntry`/`Verify()` in normal usage examples.
  - **No `[EditorBrowsable(EditorBrowsableState.Never)]`** — matches this
    repo's own explicitly-documented convention
    (`RowInvokerRegistry`'s doc comment: left undecorated, consistent
    with `PlanCache<T>`/`CollectionPlanCache<T>`/`GeneratedTestDoubleRegistry`,
    none of which carry that attribute).
  - **No separate public "bridge"/facade type** to keep the real registry
    `internal` — `LoggingFactoryRegistry` itself is both the storage and
    the cross-assembly entry point, matching
    `GeneratedTestDoubleRegistry`/`RowInvokerRegistry` exactly; no
    evidence justifies a second abstraction here.

  The exact backing collection (`ConcurrentDictionary<Type, ...>` or
  otherwise) is an implementation detail, not frozen by the ADR — choose
  based on what the concurrency tests (task 12) actually require, not by
  default assumption.
- [x] `Register<TCategory>`'s key is `typeof(ILogger<TCategory>)`,
  computed via an ordinary generic `typeof()` inside the (statically,
  per-`TCategory`) compiled `Register<TCategory>` method body — confirm
  this is genuinely a generic-token load, not `Type.MakeGenericType`,
  the same way `GeneratedTestDoubleRegistry.RegisterFactory<T>` already
  relies on `typeof(T)` safely.
- [x] Idempotent registration (a second `Register<TCategory>` call for a
  `TCategory` already present is a no-op, never a throw/overwrite) —
  matching `GeneratedTestDoubleRegistry`'s own established behavior for
  the same cross-module-initializer-ordering reason.
- [x] `TryCreate` is a plain `Type`-keyed lookup plus one delegate
  invocation, passing the caller-supplied `LoggingOptions` through —
  never captured ahead of time, always the specific `UseLogging()`
  configuration active for the request being resolved (task 11).
- [x] Test-only guard: nothing in this type or its generated call sites
  may reference `MakeGenericType`, `Activator.CreateInstance`,
  `DynamicMethod`, or `System.Linq.Expressions` — see task 15's source
  guard for how this gets enforced, not just asserted here.

### 4. `LoggingOptions`

- [x] `public sealed class LoggingOptions { public LogLevel MinimumLevel { get; set; } = LogLevel.Trace; }`
  — exactly as declared in ADR-0055, no additional members.

### 5. `CapturedLogEntry`

- [x] `readonly record struct CapturedLogEntry` with `LogLevel`,
  `EventId`, `Exception?`, `Message` (`string`), `State` (`object?`),
  `Properties` (`IReadOnlyList<KeyValuePair<string, object?>>?` — the
  ADR-amended nullable-value signature, **not** the BCL's own
  non-nullable-`object` shape), `MessageTemplate` (`string?`), `Scopes`
  (`IReadOnlyList<object>`), `Timestamp` (`DateTimeOffset`).
- [x] `Properties`/`MessageTemplate` derivation: a single
  `state is IReadOnlyList<KeyValuePair<string, object>> pairs` pattern
  match (reflection-free) — the value type on the right-hand side of the
  pattern match is the BCL's actual non-nullable `object`; the *public*
  `Properties` list is materialized as `object?`-valued pairs (a boxed
  `null` reference assigns without a cast either way, per ADR-0055's
  "Properties nullability" reasoning). `MessageTemplate` is
  `Properties`'s `"{OriginalFormat}"` entry by key, surfaced as
  `string?` (null if `Properties` is null or the key is absent).
- [x] `Scopes` is populated from `LoggerExternalScopeProvider.ForEachScope`
  at capture time (task 8) — never re-derived after the fact.

### 6. Internal `LogEntryCollector` + `ICapturingLoggerFacade`

- [x] `internal sealed class LogEntryCollector` — owns: a lock-guarded
  `List<CapturedLogEntry>`, one `LoggerExternalScopeProvider` instance,
  and the effective `LogLevel MinimumLevel` (from `LoggingOptions`, fixed
  at construction — no runtime mutation API).
- [x] `IsEnabled(LogLevel level)` — implements the exact ADR-0055 rule:
  `level != LogLevel.None && MinimumLevel != LogLevel.None && level >= MinimumLevel`.
- [x] `Record(LogLevel, EventId, object? state, Exception?, Func<object,Exception?,string> formatter)`
  — checks `IsEnabled` first and no-ops entirely (no `CapturedLogEntry`
  built or appended) when disabled; otherwise builds one `CapturedLogEntry`
  (formatted `Message` via the caller's own `formatter`, `Properties`/
  `MessageTemplate` via task 5's pattern match, `Scopes` via
  `ForEachScope`, `Timestamp` via a single consistent clock source) under
  the lock, appends it.
- [x] `PushScope<TState>(TState state) : IDisposable` — forwards to
  `LoggerExternalScopeProvider.Push`.
- [x] Read/clear surface: `GetEntries() : IReadOnlyList<CapturedLogEntry>`
  (snapshot, `.ToArray()`/equivalent under the lock), `GetLast()`,
  `Clear()`.
- [x] `internal interface ICapturingLoggerFacade` — the ADR-0055
  "Failure semantics" mechanism. Exposes exactly what
  `LoggerTestingExtensions` (task 9) needs from the collector (e.g. a
  `LogEntryCollector Collector { get; }` property) — no more. Implemented
  by both `CapturingLogger` and `CapturingLogger<T>` (task 7).

### 7. `CapturingLogger` / `CapturingLogger<T>`

Unchanged by any amendment — both remain hand-written, no generated
member implementation.

- [x] `public sealed class CapturingLogger : ILogger, ICapturingLoggerFacade`
  — holds one `LogEntryCollector`; `BeginScope<TState>`/`IsEnabled`/
  `Log<TState>` all forward to it.
- [x] Public constructor: `CapturingLogger(LoggingOptions? options = null)`
  — per ADR-0055's "Construction semantics" amendment. No category-string
  parameter (the already-settled "Non-generic `ILogger`" decision stands
  unchanged).
- [x] `public sealed class CapturingLogger<T> : ILogger<T>, ICapturingLoggerFacade`
  — composes an internal `LogEntryCollector` directly (does **not**
  contain or delegate to a `CapturingLogger` instance — composition over
  inheritance per ADR-0055, and there is no shared-base-class shortcut
  available here since both are `sealed`).
- [x] Public constructor: `CapturingLogger<T>(LoggingOptions? options = null)`.
- [x] Verify (test, not just code): both `LoggingProvider`'s direct
  bare-`ILogger` path (task 11) and every generated `LoggingFactoryRegistry`
  activator (task 2/3) construct captors via these exact same public
  constructors — one construction path, not a provider/generator-only
  internal path plus a consumer-facing public path, per ADR-0055's
  explicit requirement.

### 8. Scope capture

- [x] `LoggerExternalScopeProvider` (namespace
  `Microsoft.Extensions.Logging`) is the sole scope mechanism — no custom
  scope stack. `BeginScope<TState>` returns the real `IDisposable` from
  `Push`, unmodified.
- [x] `Record(...)` (task 6) snapshots via `ForEachScope` into
  `CapturedLogEntry.Scopes` **at the moment of the log call**, before the
  entry is appended — never re-queried later.

### 9. `LoggerTestingExtensions` + non-Compono-logger failure semantics

- [x] `GetCapturedEntries(this ILogger logger)`,
  `GetLastCapturedEntry(this ILogger logger)`,
  `ClearCapturedEntries(this ILogger logger)`: each pattern-matches
  `logger is ICapturingLoggerFacade facade`; on match, forwards to the
  facade's collector; on non-match, throws `InvalidOperationException`
  with a message naming the concrete `logger` type, stating it is not a
  `Compono.Logging` capturing logger, and pointing at `UseLogging()` and
  the stage-6 registration-order requirement as the likely cause.
- [x] `Verify(this ILogger logger) : LogVerificationBuilder` — same
  facade check and same failure behavior as the three methods above.
- [x] **Keep this exception distinct from task 11's
  missing-generated-activation exception** — same `InvalidOperationException`
  type, but the two must have clearly different messages/causes (this
  one: "not a Compono.Logging logger at all, likely wrong provider
  order"; task 11's: "recognized as `ILogger<T>` but no generated
  activation exists, likely a generator/discovery gap"). Cover both in
  the same focused test file so the distinction is obvious to a future
  reader (task 13).

### 10. `LogVerificationBuilder` + `CallVerifier` reuse

- [x] `AtLevel(LogLevel)`, `WithEventId(EventId)`,
  `WithException<TException>() where TException : Exception`,
  `WithMessageContaining(string)`, `Matching(Func<CapturedLogEntry, bool>)`
  — each accumulates a filter predicate (an internal composed
  `Func<CapturedLogEntry, bool>`, not a separate expression tree/matcher
  type); no filter is applied until a terminal call.
- [x] `Once()`/`Never()`/`Exactly(int)` — each snapshots the collector's
  current entries (task 6's `GetEntries()`), applies the accumulated
  filter, counts matches, and constructs one **existing**
  `Compono.CallVerifier` from that count plus a description built from
  the accumulated filters — then calls the corresponding `CallVerifier`
  member. No new count/Times abstraction anywhere in this type;
  `CallVerifier` itself is never part of `LogVerificationBuilder`'s
  public surface.

### 11. `LoggingProvider` + `UseLogging()`

- [x] `internal sealed class LoggingProvider(LoggingOptions options) : ICompositionValueProvider`
  — `TryProvide`:
  - **Bare `ILogger`** (`RequestedType == typeof(ILogger)`): returns
    `Success` with `new CapturingLogger(options)` directly — no
    registry, no generated bridge, since there's no generic parameter to
    close.
  - **Closed `ILogger<T>`** (`RequestedType.IsGenericType && RequestedType.GetGenericTypeDefinition() == typeof(ILogger<>)`
    — the same static `Type` check pattern `NSubstituteProvider` already
    uses): query `LoggingFactoryRegistry.TryCreate(request.RequestedType, options, out var value)`.
    - Found: returns `Success` with `value`.
    - **Not found: throws immediately** with the ADR-0055-specified
      `InvalidOperationException` naming the requested closed type,
      stating no generated activation was available, and pointing at
      source-generation/discovery coverage (including the ADR-0052
      Finding-B cross-reference where relevant) as the likely cause.
      **Never returns `NotHandled` for this case** — falling through
      would let a later `UseNSubstitute()`/`UseGeneratedTestDoubles()`
      registration silently mask a real generator/discovery gap.
  - **Anything else**: `NotHandled`, never throws.
- [x] `public static CompositionBuilder UseLogging(this CompositionBuilder builder, Action<LoggingOptions>? configure = null)`
  — builds a `LoggingOptions`, applies `configure` if given, calls
  `builder.AddTestDoubleProvider(new LoggingProvider(options))` (stage 6,
  [ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
  existing extension point — no new engine mechanism).
- [x] Each composed request gets its own independent
  `CapturingLogger`/`CapturingLogger<T>` by default (Compono's
  independent-by-default model) — confirm no accidental sharing across
  two distinct requests for the same closed type absent `[Shared]`.

### 12. Concurrency

- [x] `LogEntryCollector`'s backing store is lock-guarded; every read
  (`GetEntries()`, `GetLast()`, `Verify()`'s internal snapshot) takes a
  point-in-time copy, never a live view — matching `Compono.Http`'s
  already-established `TestHttpHandler.Requests` pattern.
- [x] `Clear()` and concurrent `Log<TState>` calls are safe under the
  same lock; a concurrency test (many parallel `Log<TState>` calls
  against one collector, task 13) proves no lost entries and no
  exception under contention.
- [x] `LoggingFactoryRegistry`'s own concurrent registration/lookup
  (many module initializers racing at assembly-load time, concurrent
  `TryCreate` lookups during composition) is safe — choose and justify
  the backing collection here, per task 3's note.

### 13. Tests (`test/Compono.Logging.Tests`)

Structured-state:
- [x] `logger.LogInformation("...", args)` — `FormattedLogValues` path:
  `Message`, `State`, `Properties`, `MessageTemplate`, original boxed CLR
  property types preserved (not stringified).
- [x] A `[LoggerMessage]`-source-generated logging call (a small
  partial-method fixture in the test project) — `LoggerMessageState`
  path: same five assertions as above, proving the single pattern-match
  covers both call styles with no special-casing.
- [x] A structured argument that is `null` (e.g.
  `LogInformation("User {UserId}", (int?)null)`) — `Properties`
  correctly carries a `null` value, no exception.

`MinimumLevel` (ADR-0055's explicit validation expectations):
- [x] `MinimumLevel = LogLevel.Trace` (default) — every ordinary level
  Trace-Critical is enabled and captured.
- [x] `MinimumLevel = LogLevel.Warning` — Trace/Debug/Information are
  neither enabled nor captured; Warning/Error/Critical are captured.
- [x] `MinimumLevel = LogLevel.None` — no ordinary level is enabled;
  nothing is ever captured regardless of what's logged.
- [x] A direct `Log<TState>` call passing `LogLevel.None` captures
  nothing, independent of `MinimumLevel`'s value (including
  `MinimumLevel = Trace`).
- [x] A disabled-level call does not appear in `GetCapturedEntries()`,
  is never returned by `GetLastCapturedEntry()`, and does not count
  toward `Verify()`'s match count for `Once()`/`Never()`/`Exactly(n)`.

Scope:
- [x] No active scopes — `Scopes` is empty.
- [x] Single scope — `Scopes` has exactly that one entry.
- [x] Nested scopes (3 levels) — `Scopes` is outermost-to-innermost.
- [x] Dispose/pop behavior — disposing an inner scope removes only that
  scope from subsequent entries; disposing the outer scope after the
  inner clears both.
- [x] Flow across `await` — a scope pushed before an `await` in the same
  logical call is still visible to a log call after the `await`.
- [x] Expected `ExecutionContext` flow — a sibling `Task.Run` started
  *before* the scope push does not see it; one started *after* does.
- [x] The scope snapshot on `CapturedLogEntry.Scopes` is fixed at the
  moment of the log call — a scope pushed or disposed afterward does not
  retroactively change an already-captured entry.

Verification API (full public shape):
- [x] `logger.Verify().AtLevel(...).WithEventId(...).WithException<T>().WithMessageContaining(...).Matching(...).Once()`
  — end-to-end, all five filters combined, matching exactly one entry.
- [x] `Never()` — zero matches passes, one or more matches throws.
- [x] `Exactly(n)` — exact-count match passes; any other count throws.
- [x] Each filter in isolation narrows correctly against a mixed set of
  captured entries.
- [x] Confirm (via the actual `Compono.CallVerifier` type, not a
  reimplementation) that `Once`/`Never`/`Exactly` failures throw
  `TestDoubleVerificationException` with the filter description in the
  message.

Provider/precedence/activation:
- [x] Bare `ILogger` composes through `UseLogging()` (direct-construct
  path, no registry involved).
- [x] `ILogger<T>` composes through `UseLogging()` for an arbitrary `T`
  **discovered by the (now-shared) generator** (task 2/14) — this is the
  primary, realistic end-to-end path and must be exercised via a real
  composed root, not a hand-called `LoggingProvider.TryProvide`.
- [x] `LoggingProvider.TryProvide` returns `NotHandled` for an unrelated
  type (e.g. `IOrderRepository`) — never throws.
- [x] `UseLogging()` registered before `UseGeneratedTestDoubles()`/
  `UseNSubstitute()` — `ILogger<T>` resolves to a `CapturingLogger<T>`.
- [x] Reverse registration order — `ILogger<T>` resolves to whatever the
  other provider produces — confirms the existing first-registered-wins
  pipeline behavior is unchanged, not a new diagnostic.
- [x] Calling `logger.Verify()`/`GetCapturedEntries()`/etc. on an
  `ILogger<T>` produced by the *other* provider (the reverse-order case
  above) throws task 9's `InvalidOperationException` ("not a
  Compono.Logging logger"), not an `InvalidCastException` or a silently
  empty result.
- [x] **Missing-generated-activation**: construct a `LoggingProvider`
  scenario where `RequestedType` is closed-`ILogger<T>`-shaped but
  `LoggingFactoryRegistry` has no entry for it — confirm it throws task
  11's distinct `InvalidOperationException`, and confirm it does **not**
  fall through to `NotHandled`/another provider.

Direct construction:
- [x] `new CapturingLogger()` and `new CapturingLogger(options)` both
  work standalone, no composition involved.
- [x] `new CapturingLogger<T>()` and `new CapturingLogger<T>(options)`
  likewise — this needs no generated activation bridge at all, since `T`
  is statically known at the call site (confirm this is exercised
  without ever touching `LoggingFactoryRegistry`).
- [x] A directly-constructed logger and a `LoggingProvider`/registry-
  produced logger for the same `T`/options exhibit identical
  `IsEnabled`/capture/scope/`Verify()` behavior — same code path, not
  two implementations.

Concurrency:
- [x] Many parallel `Log<TState>` calls against one collector — no lost
  entries, no exception, final count matches call count.
- [x] `GetCapturedEntries()`/`Verify()` reads concurrent with in-flight
  `Log<TState>` calls never throw and never return a corrupted/partial
  entry.

### 14. Logging-generator tests (existing `test/Compono.Generators.Tests`)

**No new test project.** These are new test cases added to the existing
`test/Compono.Generators.Tests` (mirroring its established shape:
compile a small in-memory `Compilation`, run
`ComponoIncrementalGenerator`, assert on emitted source and/or
diagnostics), covering the full `ComponoGeneratedLogging` gating matrix
from ADR-0055 Amendment 3 plus discovery correctness:

Property gating (the matrix Amendment 3 specifies exactly):
- [x] `ComponoGeneratedLogging` absent/unset, `Compono.Logging` **not**
  referenced → no logging-specific discovery or emission, no diagnostic.
- [x] `ComponoGeneratedLogging` absent/unset, `Compono.Logging`
  **referenced** with its packed default-value props simulated (test
  harness sets the property to `true`, matching what the package's own
  default would produce) → generation enabled, reachable `ILogger<T>`
  activation emitted.
- [x] Explicit `ComponoGeneratedLogging=false`, `Compono.Logging`
  referenced → generation disabled, no emission, no diagnostic — the
  explicit override wins over the (simulated) package default.
- [x] Explicit `ComponoGeneratedLogging=true`, `Compono.Logging`
  referenced, all three required symbols resolvable → normal
  discovery/emission.
- [x] Explicit `ComponoGeneratedLogging=true`, `Compono.Logging`
  **not** referenced (required symbols unresolvable) → the dedicated
  `CMP0038`-or-whatever-the-confirmed-id-is diagnostic reported, **no**
  generated registration source emitted at all (not partial, not
  broken).
- [x] `ILogger<T>` available in the compilation (i.e.
  `Microsoft.Extensions.Logging.Abstractions` referenced) but
  `ComponoGeneratedLogging` disabled → no logging emission, regardless
  of `ILogger<T>` presence.
- [x] A compilation that references neither
  `Microsoft.Extensions.Logging.Abstractions` nor sets the property at
  all → zero logging-generator output, zero errors — proves the
  `GetTypeByMetadataName` short-circuit actually works, not just
  compiles.

Discovery correctness (reusing the existing pipeline):
- [x] `ILogger<T>` directly on a composed root's constructor —
  registration emitted for `T`.
- [x] Multiple distinct `ILogger<T>` categories in one compilation — one
  registration per category, all emitted.
- [x] A nested, transitive constructor dependency (root → composable →
  `ILogger<Leaf>` parameter) — registration emitted for `Leaf`, proving
  the existing recursive walk correctly carries this new leaf shape.
- [x] `[Composable]`-marked root (both direct-attribute and
  assembly-level forms) reaching an `ILogger<T>` dependency.
- [x] `[Compose]`/`[Compose<TProfile>]` theory-row parameter typed
  `ILogger<T>` (or reaching one transitively).
- [x] `Composer.Create<T>()`/`CreateMany<T>()` roots reaching an
  `ILogger<T>` dependency.
- [x] The same closed `T` discovered via two different roots emits
  exactly one registration, not two.
- [x] An `ILogger<T>` constructor parameter on a type that is **not**
  reachable from any real composition root produces **no** registration
  for it — confirms this rides the existing "real roots only" discovery
  model, not a compilation-wide scan.
- [x] Bare, non-generic `ILogger` requires no generated registration at
  all.
- [x] Generated source uses a statically closed `CapturingLogger<T>`
  activation (`new CapturingLogger<T>(options)` inline in generated
  code) — inspect the emitted source text, not just that it compiles.
- [x] Generated activator delegate signature accepts `LoggingOptions` as
  a runtime parameter (not captured, not zero-argument) — inspect the
  emitted source text for this shape specifically.
- [x] **Boundary case, explicitly documented as intentionally
  unsupported, not accidentally missed**: an `ILogger<T>` dependency
  reachable only through a hand-written `Register<T>(...)` factory's own
  internal `context.Resolve<ILogger<TSomething>>()` call (ADR-0052
  Finding-B shape) produces no registration — assert this is the
  observed (expected) behavior with a comment/test name that makes clear
  this is a known, ADR-0052-cross-referenced limitation, not a bug to
  fix in this plan.
- [x] Confirm existing, unrelated `Compono.Generators.Tests` coverage
  (ordinary composition plans, collection plans, TestDoubles generation)
  is unaffected — this feature must be additive, never change any
  existing generator output for a compilation that doesn't reference
  `Compono.Logging`.

### 15. Native AOT/trimming validation (`test/Compono.Logging.AotSmokeTest`)

- [x] The AOT smoke consumer project must exercise the **generated
  composition path**, not only direct construction: a real composed
  root with an `ILogger<T>` constructor dependency, composed via
  `Composer.Create(builder => builder.UseLogging())`, such that the
  (shared) generator actually emits an activation for it. Direct
  `new CapturingLogger<T>()` construction alone does not exercise the
  thing this feature exists to prove.
- [x] Validate, for that consumer: generator output is present; the
  composed `ILogger<T>` resolves successfully at runtime;
  `GetCapturedEntries()`/`Verify()` work correctly against it.
- [x] `dotnet publish` with `PublishAot`/`IsAotCompatible=true` succeeds
  for that consumer.
- [x] Zero `IL2026`/`IL3050`/trim-analyzer warnings attributable to
  `Compono.Logging` anywhere in that build (mirroring
  `Compono.Http.AotSmokeTest`'s two-proof pattern: (a) the consumer
  build, (b) `Compono.Logging`'s own build with its analyzer turned on,
  proving no reflection-related warning exists there).
- [x] **Source-level guard** (not just "it built clean this time"): a
  focused check — a simple text/syntax scan over `src/Compono.Logging/**/*.cs`
  **and the new logging-activation files inside `src/Compono.Generators/**`**
  (not a separate project, per Amendment 3 — point this at the actual
  files added there) — that fails the build if `MakeGenericType`,
  `Activator.CreateInstance`, `DynamicMethod`, or
  `System.Linq.Expressions` ever appears in either location, so a future
  change can't silently reintroduce the reflection path this design
  specifically rejected.
- [x] Confirm no reflection fallback is reachable from any public entry
  point — there is no code path (not even an error path) in
  `LoggingProvider`/`LoggingFactoryRegistry` that falls back to
  reflection-based activation when the registry lookup misses; task 11
  already specifies it throws instead.

### 16. Documentation and skill/reference synchronization (completion-gate work, not cleanup)

Each item states what the surface must learn, not just "update it":

- [x] **`docs/packages/compono-logging.md`** (new) — following
  `docs/packages/compono-http.md`'s exact shape: an inventory/"when to
  install" section, a worked `UseLogging()` + `[Shared]` +
  `Verify()`/`GetCapturedEntries()` example (this is also where
  ADR-0055's usage examples live — see "Usage-example convention"
  below), structured-`Properties`/`MessageTemplate` behavior, scope
  semantics (outermost-to-innermost, async-safe), `MinimumLevel`/
  `LogLevel.None` filtering semantics, direct-construction support, the
  non-Compono-logger `InvalidOperationException` diagnostic, the
  explicit v1 boundary list (no `ILoggerFactory`, no Serilog-specific
  behavior, etc.), **and, per Amendments 1/3**: that installing
  `Compono.Logging` enables its generation behavior **by default**
  (`ComponoGeneratedLogging`, defaulting to `true` via the package's own
  MSBuild props asset), how to explicitly opt out
  (`<ComponoGeneratedLogging>false</ComponoGeneratedLogging>`), that the
  actual generation happens inside the shared `Compono.Generators`
  assembly (not a package-specific generator), that
  `CapturingLogger`/`CapturingLogger<T>` are hand-written (the generator
  emits activation glue only), that discovery follows reachable Compono
  composition roots (not a blanket scan), the static-discoverability
  limitation (with the ADR-0052 Finding-B example), the
  missing-generated-activation *runtime* diagnostic, the
  missing-runtime-symbols *compile-time* diagnostic (`CMP0038`, if
  `ComponoGeneratedLogging` is forced `true` without `Compono.Logging`
  referenced), and that no reflection fallback exists anywhere.
- [x] **`docs/packages/index.md`** — add a `Compono.Logging` row matching
  the existing table shape.
- [x] **`README.md`** — add a `Compono.Logging` row to the package/badge
  table (mirrors the existing `Compono.Http` row, lines 39-46).
- [x] **`docs/architecture/current/provider-pipeline.md`** — extend the
  stage-6 row (line 20) to name `Compono.Logging`'s `LoggingProvider`
  alongside `NSubstituteProvider`/`GeneratedTestDoubleProvider`, and add
  the explicit `UseLogging()`-before-`UseNSubstitute()`/
  `UseGeneratedTestDoubles()` precedence sentence.
- [x] **`docs/architecture/current/generated-plans-and-discovery.md`** —
  during planning, inspect whether this is the authoritative place to
  record that `Compono.Generators` now also emits logging activation
  (a second, gated feature alongside `ComponoGeneratedTestDoubles`), and
  if so, add a short cross-reference to ADR-0055 Amendment 3 and
  `docs/packages/compono-logging.md` rather than duplicating the
  discovery-model explanation. If a different existing doc is found to
  be the actual authoritative home, name that file instead.
- [x] **`docs/public-api.md`** — this file is a tombstone/redirect
  (ADR-0030 Amendment 2); its only relevant content is the bullet listing
  every package (line 18) — add `Compono.Logging` to that list.
- [x] **Usage examples — resolved location, see below** — no separate
  samples project; examples live inline in
  `docs/packages/compono-logging.md`, consistent with `Compono.Http`'s
  own convention.
- [x] **`skills/compono/SKILL.md`** — three concrete edits: (1) add
  `Compono.Logging` to the package-enumeration sentence (line 8); (2) add
  a `.csproj`-detection row to the definitive-signal table (lines 50-54)
  — `<PackageReference Include="Compono.Logging"` → `UseLogging()`
  available (generation on by default), load `references/logging.md`;
  (3) add a `references/logging.md` row to the references-index table
  (lines 365-372), matching the `references/http.md` row's phrasing.
- [x] **`skills/compono/references/logging.md`** (new) — covering, at
  minimum: `UseLogging()`, direct construction, structured
  `Properties`/`MessageTemplate` extraction, scope semantics,
  `Verify()`'s full fluent shape, `MinimumLevel`/`LogLevel.None`
  filtering, the `UseLogging()`-before-`UseNSubstitute()`/
  `UseGeneratedTestDoubles()` registration-order requirement, the
  non-Compono-logger `InvalidOperationException` diagnostic, the
  explicit v1 boundary list, **and, per Amendments 1/3**: default-on
  generation via `ComponoGeneratedLogging` and how to opt out, that
  generation happens inside the shared `Compono.Generators`, that closed
  `ILogger<T>` composition requires the type to be reachable from a real
  composition root, the missing-generated-activation *and*
  missing-runtime-symbols diagnostics' distinct meanings — matching
  `references/http.md`'s existing depth and structure.
- [x] **`skills/compono-evals/evals.json`** — add at least one new eval
  requiring **ordinary `ILogger<T>` composition through `UseLogging()`**
  (not merely direct `new CapturingLogger<T>()` construction), following
  the existing eval shape and asserting real API usage with no invented
  API or reflection-based workaround suggested. Add a second eval
  covering the registration-order precedence pitfall if the benchmark
  format supports a diagnosis-style prompt, matching eval #2's existing
  shape.
- [x] **`skills/compono-evals` benchmark/grading artifacts** — run the
  existing before/after benchmark harness for the new eval(s) against
  the updated skill, recording the result per that directory's
  established convention.
- [x] **`docs/adr/README.md`** — already updated (ADR-0055's own index
  row); no further change needed here.
- [x] **`LoggingFactoryRegistry` documentation treatment (ADR-0055
  Amendment 2)** — note it exists (real, public, generator
  infrastructure) but **must not** be presented alongside `UseLogging()`/
  `CapturingLogger<T>()`/`Verify()`/`GetCapturedEntries()` as something a
  consumer is expected to call directly. Do not add
  `[EditorBrowsable(Never)]` to compensate in code.
- [x] **Public-API/reference regeneration (`docs/reference/api`)** — per
  [ADR-0032](../adr/0032-api-reference-documentation-toolchain.md),
  regenerate `docs/reference/api/Compono.Logging/` as part of this PR so
  it includes every shipped public member, **explicitly including
  `LoggingFactoryRegistry.Register<TCategory>`/`TryCreate`**.
- [x] **`Compono.Generators`' own diagnostic docs** — wherever this
  repo's `CMP00xx` diagnostics are documented for consumers
  (`references/diagnostics.md` per `skills/compono/SKILL.md`'s own
  references-index table), add the new missing-runtime-symbols
  diagnostic alongside the existing `CMP0020`-`CMP0032` entries.

### 17. Package creation and MSBuild default-property asset

- [x] **Core `Compono`'s existing `src/Compono/build/Compono.props`**
  gains one more declaration, alongside the existing
  `ComponoGeneratedTestDoubles` one:
  ```xml
  <CompilerVisibleProperty Include="ComponoGeneratedLogging" />
  ```
  Inert for any consumer who never references `Compono.Logging`. No new
  file — extend the existing one.
- [x] **New `src/Compono.Logging/build/Compono.Logging.props`**,
  packed to both `build/Compono.Logging.props` and
  `buildTransitive/Compono.Logging.props` inside `Compono.Logging.nupkg`
  (mirroring `Compono.csproj`'s existing two-`<None>`-entries pattern for
  `build/Compono.props`), containing only:
  ```xml
  <PropertyGroup>
    <ComponoGeneratedLogging Condition="'$(ComponoGeneratedLogging)' == ''">true</ComponoGeneratedLogging>
  </PropertyGroup>
  ```
- [x] `Compono.Logging.nupkg` contains the runtime assembly, its normal
  dependencies (`Compono`, `Microsoft.Extensions.Logging.Abstractions`),
  and this tiny props asset — **no generator/analyzer DLL of any kind**.
  Confirm directly (e.g. inspect the built `.nupkg`'s contents / `.nuspec`),
  not by assumption.
- [x] `Compono.nupkg` remains the sole owner of `Compono.Generators.dll`
  — confirm its packed contents are unchanged in shape (still exactly
  one analyzer DLL, now with new logging-activation behavior inside it).
- [x] **Packaging tests** (new, part of this task group's own
  validation, not folded into task 13/14): confirm the packed
  `Compono.Logging.nupkg` contains the expected `build`/`buildTransitive`
  props assets and confirm it contains **no** `analyzers/` folder at
  all; confirm the packed `Compono.nupkg` still contains exactly the one
  shared `Compono.Generators.dll` analyzer asset (no duplicate, no
  second copy anywhere).
- [x] `<IsAotCompatible>true</IsAotCompatible>` preserved on
  `Compono.Logging.csproj` (task 1) through the packed artifact.
- [x] Confirm, directly (a real local pack + restore into a scratch
  consumer, not assumed), that a consumer referencing only
  `Compono.Logging` (which pulls `Compono` in transitively) receives
  both `Compono.Generators.dll`'s execution *and*
  `ComponoGeneratedLogging`'s default-`true` value with zero extra steps.

### 18. Dogfood migration (separate PR(s), after `Compono.Logging` ships — see "Status contract")

Pack `Compono` + `Compono.Logging` (and any other package a given
consumer already uses) into the local feed via
`scripts/dogfood-validate.sh --packages "Compono Compono.Logging ..."`
against each consumer's real test suite, using freshly built local
packages.

**`LayeredCraft.StructuredLogging`**:
- [x] Inventory every real use of `TestLogger`/`TestLogger<T>`/
  `TestingExtensions` (`WarningExtensionsTests.cs` and any sibling test
  files) before changing anything.
- [x] Where a test currently does `new TestLogger()` and could instead
  compose `[Compose] ILogger` (or `ILogger<T>`) via `UseLogging()`,
  migrate it — but only where doing so doesn't just recreate
  `TestingExtensions`' own assertion helpers under a different name.
- [x] Explicitly decide, and record the decision, on
  `LayeredCraft.StructuredLogging`'s own `Testing/TestingExtensions.cs`:
  deprecate/remove in favor of `Compono.Logging`, or keep it.

**`alexa-vox-craft`**:
- [x] `test/AlexaVoxCraft.MediatR.Tests/TestKit/MediatRTestProfile.cs`'s
  `Register<ILogger<PerformanceLoggingBehavior>>(() => new TestLogger<PerformanceLoggingBehavior> { ... })`
  → remove the manual registration; let `UseLogging()` compose it;
  confirm `PerformanceLoggingBehaviorTests.cs`'s assertions still pass,
  migrated to `Compono.Logging`'s `Verify()`/`GetCapturedEntries()` API.
- [x] `Register<ILogger<SkillMediator>>(() => NullLogger<SkillMediator>.Instance)`
  → confirm whether this fallback is still needed once `UseLogging()` is
  active.
- [x] Distinguish genuine remaining consumer-specific architecture from
  a missing `Compono.Logging` capability — report the latter rather than
  redesigning the consumer repo to force a fit.
- [x] Do not modify either consumer repository as part of this plan's
  drafting — this task group only executes once `Compono.Logging` has
  shipped and been packed.

### 19. `ILogger`/`ILogger<T>` TestDoubles-exclusion (ADR-0055 Amendment 4)

Found during task 18 dogfooding against `alexa-vox-craft`: with both
`ComponoGeneratedTestDoubles=true` and `ComponoGeneratedLogging=true` on the
same `ILogger<T>`, `Compono.TestDoubles`' generated, exact-typed
`Verify(this ILogger<T>)` extension wins C# overload resolution over
`Compono.Logging`'s `Verify(this ILogger)`, silently breaking
`Compono.Logging`'s verification API. Root-caused, proven via throwaway
spike (reverted — nothing from the spike is reused directly, this task
implements it for real), and resolved by ADR-0055 Amendment 4
(**Accepted**). This task group is the real, permanent implementation of
that amendment — do the narrow fix only; no generalized generator
feature-ownership/provider-priority mechanism.

- [x] `LeafTypeClassifier`/`TransitiveClosureWalker`: when
  `ComponoGeneratedLogging` is enabled, exclude
  `Microsoft.Extensions.Logging.ILogger` and any closed
  `Microsoft.Extensions.Logging.ILogger<T>` from
  `Compono.TestDoubles`-eligibility (`TryRecordTestDouble`), reusing
  `LoggingWellKnownTypes`/`ctx.LoggingWellKnown` already threaded through
  `WalkContext` for `TryRecordLoggingCategory`. No new well-known-type
  resolution needed. Closed `ILogger<T>` still gets Logging's generated
  activation exactly as before (Amendments 1/3, unchanged); bare
  `ILogger` gets no generated anything either way (Amendment 4's "no-op
  half" of the rule) — implement the ownership check uniformly across
  both shapes anyway, per the amendment's "one rule, not two" reasoning.
- [x] Permanent generator tests (`test/Compono.Generators.Tests/`,
  replacing the throwaway `SPIKE_Amendment4CollisionTests.cs` shape with
  real, kept tests) covering the four-way matrix:
  `ComponoGeneratedTestDoubles`×`ComponoGeneratedLogging` ∈
  {off,on}×{off,on}, for both closed `ILogger<T>` and bare `ILogger`.
  Assert generated-tree text presence/absence of `LoggingFactoryRegistry`
  registration vs. a TestDoubles `_DoubleVerifier`/`Verify()` extension for
  that exact type, matching the spike's four proven cases (TestDoubles+Logging
  on together → Logging activation only, no competing extension;
  TestDoubles on/Logging off → existing double behavior preserved
  unchanged; both off → neither; bare `ILogger` → no TestDoubles double
  either way, both before and after this task, recorded as a regression
  guard for the already-true no-op).
- [x] Compile-level regression (`test/Compono.Logging.Tests/` or a new
  test project mirroring `alexa-vox-craft`'s real shape, both
  `ComponoGeneratedTestDoubles=true` and `ComponoGeneratedLogging=true`,
  an interface *and* an `ILogger<T>` reachable from the same composition
  root) proving `logger.Verify().AtLevel(...)` actually compiles and binds
  to `Compono.Logging`'s `LogVerificationBuilder` — not just that the
  right generated text exists, but that a real consumer-shaped compilation
  resolves the overload correctly end to end.
- [x] Regression: existing `Compono.TestDoubles`-only behavior for
  `ILogger<T>` (no `Compono.Logging` referenced, or
  `ComponoGeneratedLogging=false`) is unchanged — reuse/extend
  `LoggingGeneratorTests`' existing property-gating tests rather than
  duplicating them.
- [x] Documentation/skill updates so this interoperability rule is
  documented, not silent generator behavior:
  - `skills/compono/references/logging.md`'s "Registration order (stage-6
    precedence)" section and `docs/packages/compono-logging.md`'s
    identically-named section: replace the blanket "`UseLogging()` must
    precede both `UseNSubstitute()` and `UseGeneratedTestDoubles()`"
    framing with Amendment 4's corrected statement — registration order
    between `LoggingProvider`/`GeneratedTestDoubleProvider` is no longer
    observable for `ILogger`/`ILogger<T>` once `ComponoGeneratedLogging`
    is enabled (no generated factory exists for those types anymore);
    `UseLogging()` must still precede `UseNSubstitute()` (and any other
    provider that can independently produce an `ILogger`/`ILogger<T>`
    substitute).
  - `skills/compono/references/testdoubles.md`: note the
    `ILogger`/`ILogger<T>` exclusion when `Compono.Logging`'s generation
    is enabled, cross-referencing ADR-0055 Amendment 4, so a reader of the
    TestDoubles reference isn't surprised these two types are absent from
    generation under that condition.
  - `docs/architecture/current/generated-plans-and-discovery.md`: extend
    the existing "Other compile-time-gated generation in this same
    assembly" paragraph to note the two discovery buckets are now
    mutually exclusive for `ILogger`/`ILogger<T>`, linking Amendment 4.
- [x] Re-run the full existing regression surface after the real change
  lands: `test/Compono.Generators.Tests` (all, not just the new/logging
  ones), `test/Compono.Logging.Tests`, `test/Compono.TestDoubles.Tests`
  (confirm no non-`ILogger` interface regresses), `test/Compono.Tests`.
- [x] Re-run PLAN-0055 task 18's dogfooding against `alexa-vox-craft` from
  fresh `scripts/dogfood-validate.sh`-packed local packages (not
  ProjectReferences, not stale packages) once the real fix is in — this is
  the task that was blocked by the collision; it resumes here rather than
  being separately re-authorized.

## Critical Files

New:
- `src/Compono.Logging/Compono.Logging.csproj`
- `src/Compono.Logging/build/Compono.Logging.props` (new — the default-
  value MSBuild props asset, task 17)
- `src/Compono.Logging/LoggingOptions.cs`
- `src/Compono.Logging/CapturedLogEntry.cs`
- `src/Compono.Logging/LogEntryCollector.cs` (internal)
- `src/Compono.Logging/ICapturingLoggerFacade.cs` (internal)
- `src/Compono.Logging/CapturingLogger.cs`
- `src/Compono.Logging/CapturingLogger{T}.cs`
- `src/Compono.Logging/LoggerTestingExtensions.cs`
- `src/Compono.Logging/LogVerificationBuilder.cs`
- `src/Compono.Logging/LoggingProvider.cs` (internal)
- `src/Compono.Logging/LoggingFactoryRegistry.cs` (public — generator
  infrastructure, per ADR-0055 Amendment 2; not consumer-facing usage
  surface)
- `src/Compono.Logging/CompositionBuilderExtensions.cs`
- `test/Compono.Logging.Tests/*`
- `test/Compono.Logging.AotSmokeTest/*`
- `docs/packages/compono-logging.md`
- `skills/compono/references/logging.md`

Modified (existing `Compono.Generators`, per ADR-0055 Amendment 3 — no
new generator project):
- `src/Compono.Generators/WellKnownTypes/` — new optional
  `ILogger`/`ILogger`1` resolution.
- `src/Compono.Generators/Discovery/LeafTypeClassifier.cs` and/or
  `TransitiveClosureWalker.cs` — the one new per-parameter-type check
  hooked into the existing walk.
- `src/Compono.Generators/Emitters/` — new `LoggingActivationEmitter.cs`
  (or similarly named), clearly separated from existing emitters.
- `src/Compono.Generators/Templates/` — new `LoggingActivation.scriban`.
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` — one new,
  clearly-labeled wiring step (property read, discovery hook, emission
  call).
- `src/Compono.Generators/Diagnostics/` — the new missing-runtime-symbols
  diagnostic descriptor.
- `src/Compono.Generators/AnalyzerReleases.Unshipped.md` — new row.
- `src/Compono/build/Compono.props` — new `ComponoGeneratedLogging`
  `CompilerVisibleProperty` declaration.
- `test/Compono.Generators.Tests/*` — new test cases (task 14), no new
  test project.

Modified (docs/skills):
- `docs/packages/index.md`, `README.md`, `docs/public-api.md`,
  `docs/architecture/current/provider-pipeline.md`, and (pending
  planning-time confirmation, task 16)
  `docs/architecture/current/generated-plans-and-discovery.md`
- `skills/compono/SKILL.md`, `skills/compono/references/diagnostics.md`,
  `skills/compono-evals/evals.json` and its benchmark/grading artifacts
- The solution file(s) referencing `src/Compono.*`/`test/Compono.*`
  projects (`src/Compono.Logging` and `test/Compono.Logging.Tests`/
  `Compono.Logging.AotSmokeTest` only — no second generator project)

Later (dogfood PRs, separate repos):
- `layered-craft/structured-logging`:
  `src/LayeredCraft.StructuredLogging/Testing/TestingExtensions.cs` and
  its test call sites.
- `ncipollina/alexa-vox-craft`:
  `test/AlexaVoxCraft.MediatR.Tests/TestKit/MediatRTestProfile.cs`,
  `test/AlexaVoxCraft.MediatR.Tests/Pipeline/PerformanceLoggingBehaviorTests.cs`.

## Test Plan

Per `references/testing.md`'s conventions: behavioral unit tests in
`test/Compono.Logging.Tests` covering every bullet in task 13
(structured-state, `MinimumLevel`/`LogLevel.None`, scope, verification
API, provider/precedence/activation, direct construction, concurrency);
new generator test cases added to the *existing* `test/Compono.Generators.Tests`
(task 14) covering the full `ComponoGeneratedLogging` property-gating
matrix plus discovery correctness; a dedicated AOT smoke project (task
15) proving the *generated* composition path is trim/reflection-clean,
plus a source-level guard against `MakeGenericType`/
`Activator.CreateInstance`/dynamic-code activation across both
`Compono.Logging` and the new logging-activation files inside
`Compono.Generators`; packaging tests (task 17) proving
`Compono.Logging.nupkg` carries no analyzer and `Compono.nupkg` carries
exactly one, unchanged `Compono.Generators.dll`; the repo's full solution
test sweep (`dotnet test` across every existing project) run before
considering this plan done, to catch any regression the new stage-6
registrant or generator addition introduces elsewhere; the
`compono-evals` before/after benchmark run for the new eval(s) (task 16);
and, per the Status contract above, `scripts/dogfood-validate.sh` runs
against both `LayeredCraft.StructuredLogging` and `alexa-vox-craft` using
freshly packed local `Compono`/`Compono.Logging` packages (task 18) as
part of this plan reaching `Status: Done`, even though that work lands as
separate PRs in those repos.

## Validation Gate (must all pass before `Status: Done`)

- [x] Focused unit tests (task 13) — all green.
- [x] Logging-generator tests in `Compono.Generators.Tests` (task 14) —
  all green, including the full property-gating matrix and the
  negative/unreachable-type case.
- [x] Integration/composition tests (provider/precedence/activation
  subset of task 13) — all green, including the real
  generator-discovered `ILogger<T>` end-to-end path, not only direct
  construction.
- [x] Concurrency tests (task 12/13) — all green.
- [x] Structured-state tests (both `LogInformation` and `[LoggerMessage]`
  paths) — all green.
- [x] Scope tests (ordering, dispose, async flow) — all green.
- [x] Provider-precedence tests (both registration orders, the
  wrong-provider `InvalidOperationException` case, and the
  missing-generated-activation `InvalidOperationException` case, clearly
  distinguished from each other) — all green.
- [x] Direct-construction tests — all green, and confirmed identical
  behavior to provider/registry-produced loggers.
- [x] Native AOT/trimming smoke (task 15) — zero warnings on both
  proofs, proving the *generated* path, plus the source-level reflection
  guard passing across both `Compono.Logging` and the new
  `Compono.Generators` files.
- [x] Full solution test sweep — no regression anywhere else in the
  repo, including every existing `Compono.Generators.Tests` case that
  doesn't touch logging.
- [x] Documentation verification — every file named in task 16 actually
  updated and internally consistent with the shipped API, including the
  default-on-generation/opt-out behavior.
- [x] Skill/reference synchronization — `skills/compono/SKILL.md` +
  `references/logging.md` + `references/diagnostics.md` reviewed
  against the real shipped API.
- [x] `compono-evals` Compono.Logging eval(s) passing against the
  updated skill, including the ordinary-`UseLogging()`-composition eval.
- [x] Packaging tests (task 17) — `Compono.Logging.nupkg` confirmed
  analyzer-free with its props asset present; `Compono.nupkg` confirmed
  unchanged in analyzer-asset shape.
- [x] `scripts/dogfood-validate.sh` passing against both
  `LayeredCraft.StructuredLogging` and `alexa-vox-craft` (task 18,
  separate PR(s) against those repos — **required for this plan's own
  `Status: Done`**, per the Status contract above and PLAN-0051's real
  precedent).

If any implementation step surfaces evidence that contradicts ADR-0055's
decisions (including its amendments — e.g. the shared generator's
existing walk turning out not to carry the new `ILogger<T>` leaf shape
cleanly, or `GetTypeByMetadataName` behaving unexpectedly for the generic
`ILogger<>` form, or the MSBuild default-property mechanism not actually
overriding as designed), **stop and report it** rather than silently
diverging from the accepted ADR.

## Usage-example convention (resolved during planning)

Inspected `docs/packages/compono-http.md` (the direct precedent for a
runtime, non-generated integration package) and the `samples/` directory
(`Compono.Samples.AspNetApi`, `Compono.Samples.AspNetApi.Tests`,
`Compono.Samples.BasicUsage` — the ADR-0033 public-preview samples
strategy, a separate concern from per-package API documentation).
`Compono.Http` has no entry in `samples/` — its usage examples live
entirely inline in `docs/packages/compono-http.md`. **`Compono.Logging`
follows the same, already-established convention**: its usage examples
live inline in the new `docs/packages/compono-logging.md`, not as a new
`samples/` project. No samples project is added by this plan.

## Notes

(Updated as implementation proceeds — empty at plan creation time.)
