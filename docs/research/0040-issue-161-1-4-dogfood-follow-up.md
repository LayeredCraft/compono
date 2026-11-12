# RESEARCH-0040: Issue #161 1.4 Dogfood Follow-Up — Seven Usability Findings

**Status:** Complete. Every finding is triaged below. The four material
design decisions (findings 1, 4, 5, 6) were confirmed by the maintainer on
2026-11-11. Execution is tracked by
[PLAN-0075](../plans/0075-issue-161-1-4-dogfood-follow-up-impl-plan.md).

**Trigger:** the maintainer's follow-up comment on
[#161](https://github.com/LayeredCraft/compono/issues/161#issuecomment-6810922325)
(2026-11-12 UTC). It reports migrating a real .NET 11 / xUnit v3 worker
service (231 tests: unit, Testcontainers persistence, Generic Host) to
`1.4.0-preview.127` with all six packages. All four original #161
workarounds were removed, with no defects and no `CMP` diagnostics. Seven
usability findings remain. None blocked the migration.

**Release state when researched:** `main` at `9281cdc` (PR #179,
`Compono.Metrics`, merged). `v1.4.0` is a draft release, so
`Compono.Metrics` and the #162–#165 features haven't shipped stable yet.
Any change to their contracts in this follow-up is still pre-stable.

## Summary

| # | Finding | Kind | Outcome |
|---|---|---|---|
| 1 | `UseMetrics()` order vs `UseGeneratedTestDoubles()` | Defect (docs/message) + enhancement | **Implement**: generator exclusion for `IMeterFactory` (ADR-0072 Amendment 5), fix the docs and misuse message |
| 2 | `ComposeData` binding and `TConfig` access | Documentation | **Document**. No binding change |
| 3 | `[Shared]` disposables disposed by the test | Documentation (incorrect guidance) | **Document**. xUnit v3 JIT already disposes arguments. No ownership change (ADR-0072 Amendment 6) |
| 4 | Metrics checkpoints | Enhancement | **Defer**. Document the `ClearMeasurements()` multi-pass pattern |
| 5 | Logging category prefixes | Enhancement | **Implement** `InCategoryPrefix`/`NotInCategoryPrefix` (ADR-0055 Amendment 7) |
| 6 | Profile constructor arguments | Enhancement | **Document + defer**. Wrapper-profile pattern and single-row `ComposeData` |
| 7 | Migration guide from `Microsoft.Extensions.Diagnostics.Testing` | Documentation | **Document** |

## Finding 1: `UseMetrics()` depends on order against `UseGeneratedTestDoubles()`

**Consumer pain.** In a shared base profile, `UseGeneratedTestDoubles()`
came before `UseMetrics()`. A generated double took `IMeterFactory`, and
28 tests failed on measurement assertions. The misuse exception pointed at
the cause, but it's an easy trap.

**Root cause (confirmed in source).**

- `UseMetrics()` and `UseGeneratedTestDoubles()` both call
  `CompositionBuilder.AddTestDoubleProvider`, which appends to one stage-6
  list. Stage-6 providers are tried in registration order, and the first
  `Handled` wins (`CompositionBuilder.cs`, ADR-0024).
- With `ComponoGeneratedTestDoubles=true`, `Compono.Generators` emits a
  double for **every** interface it reaches
  (`LeafTypeClassifier.IsGeneratedTestDoubleEligible` is
  `testDoublesEnabled && TypeKind.Interface`). A SUT constructor parameter
  of `IMeterFactory` is reached, so a double is generated and registered
  into `GeneratedTestDoubleRegistry`. `GeneratedTestDoubleProvider` then
  claims `IMeterFactory` whenever it's ahead of `MetricsProvider`.
- **Documentation defect:** `UseMetrics()`'s XML remarks and the
  `RequireCapturing` misuse message mention only `UseNSubstitute()`. The
  trap the consumer actually hit (`UseGeneratedTestDoubles()`) isn't named
  anywhere a developer would look first.

**Existing behavior and ADRs.** ADR-0024 (first-registered-wins), ADR-0043
("Runtime activation and precedence"), and ADR-0072 "Precedence and
misuse", which said: *"Register `UseMetrics()` first. No generator
exclusion is added."* The precedent for the opposite choice is ADR-0055
Amendment 4: when `ComponoGeneratedLogging` is enabled,
`ILogger`/`ILogger<T>` are **Logging-owned** and excluded from
`Compono.TestDoubles` generation. Logging made that call because of a
compile-time `Verify()` collision. Metrics has no compile-time collision
(`Verify<T>(string)` and the generated `Verify()` have different
signatures), but it has the same ownership question at run time.

**Options considered.**

1. *Change provider precedence* (for example, specificity-based stage 6).
   That's a core pipeline change for every integration. Rejected: it
   reverses ADR-0024 for one package.
2. *Configuration-time detection.* `CompositionBuilder` doesn't expose its
   provider list, and providers are opaque until probed. This needs new
   core API. Rejected.
3. *Docs and message only.* Fix the omission. Ordering stays a trap.
4. *Generator exclusion* (ADR-0055 Amendment 4 pattern). Referencing
   `Compono.Metrics` makes `IMeterFactory` Metrics-owned, so no generated
   double exists, and order against `UseGeneratedTestDoubles()` stops
   mattering. Order against `UseNSubstitute()` (a catch-all with no
   generator involvement) still matters.

**Decision (maintainer, 2026-11-11): option 4 plus option 3.**
Requirements recorded from that decision: exclude `IMeterFactory` from
generated doubles when `Compono.Metrics` is referenced, keep stage-6
first-registration-wins, no core composition change, keep the ordering
guidance for `UseNSubstitute()` and other catch-all providers, verify and
document the "referenced but `UseMetrics()` not called" case, add
regression tests for both orders with `UseGeneratedTestDoubles()`, and
record the change as an ADR-0072 amendment that explicitly supersedes its
original no-exclusion sentence.

**Public API impact.** No new C# API. One new MSBuild property,
`ComponoMetricsOwnsMeterFactory`, defaulted to `true` by
`Compono.Metrics`'s packed `build`/`buildTransitive` props and declared
`CompilerVisibleProperty` by core `Compono.props` (the same split
`ComponoGeneratedLogging` uses). A consumer can set it to `false` to get a
generated `IMeterFactory` double back.

**Compatibility.** Only consumers who reference `Compono.Metrics` **and**
enable `ComponoGeneratedTestDoubles` see a change, and `Compono.Metrics`
hasn't shipped stable. The tradeoff: with `Compono.Metrics` referenced, a
composer that calls `UseGeneratedTestDoubles()` but not `UseMetrics()` no
longer gets a generated `IMeterFactory` double. The request falls through
to the next stage-6 provider (for example NSubstitute), or composition
fails with the usual "no provider" `CompositionException`. Referencing a
metrics-testing package and not calling `UseMetrics()` is almost always a
mistake, so the failure is a clearer signal than a silent double.

**AOT / source generation.** A generator-only change on the existing
discovery path. One extra `GetTypeByMetadataName` per compilation, only
when the flag is on. No runtime reflection. Generated output for every
other interface is unchanged.

**Tests.** Generator tests: flag on → no `IMeterFactory` double; flag off
→ double emitted; a sibling interface is still doubled; flag on without
the `System.Diagnostics.Metrics` type resolvable is inert. Runtime tests in
`Compono.Metrics.Tests` (with real generated doubles enabled): both
registration orders with `UseGeneratedTestDoubles()` produce a
`CapturingMeterFactory`; `UseGeneratedTestDoubles()` without `UseMetrics()`
fails clearly while a sibling interface still gets its double.

**Complexity.** Small to medium (one flag, one exclusion check, props
wiring, tests, docs).

## Finding 2: `ComposeData` row binding and `TConfig` access

**Consumer pain.** (a) Row values bind to the leading parameters, so
signatures had to be reordered to put row data before `[Shared]` and
composed parameters. (b) There was no way to read the row's `TConfig` in
the test body, so one theory repeated `daysBefore`/`daysAfter` in both the
config record and the row values.

**Root cause.** (a) is the contract, not a bug: ADR-0022's
leading-positional rule, reused by ADR-0070 behavior contract item 5. It's
the same rule `[Compose(42)]` uses. It's stated in the ADR and package
docs, but not prominently in the `ComposeData` usage material. (b) Element
0 of a `ComposeData<TProfile, TConfig>` row is consumed by the profile
constructor and isn't bound to a parameter (ADR-0070 item 4).

**Options considered.**

- *Bind `TConfig` to row element 0 when parameter 0 is `TConfig`.*
  Ambiguous: a row whose element 1 is also assignable to `TConfig` has two
  readings. It also breaks the "element 0 is config, 1..n are values"
  layout that ADR-0070 fixed for both engines. Rejected.
- *Named binding.* Rejected in ADR-0070 (Option 5) for conflicting with
  ADR-0022, and it adds new vocabulary. Nothing new here changes that.
- *Explicit configuration injection, which already works.* The profile
  already receives the config, so it can register it:
  `builder.Register(() => config)`. A composed `TConfig` parameter then
  resolves at stage 3 (exact registration) to the row's own instance.
  That's explicit, needs no new API, uses no reflection, and is
  AOT-compatible (it's an ordinary composed parameter).
- *Better docs.* Make the leading-positional rule and the recipe above
  prominent.

**Recommendation: Document.** The test needs no new API. Cover the
ordering rule and the register-the-config recipe in the package guides,
the cookbook, and the skill, with a real test on JIT and on AOT proving
the recipe.

**Public API / compat / AOT.** None.

**Complexity.** Small.

## Finding 3: `[Shared]` disposables must be disposed by the test

**Consumer pain.** Every test with `[Shared] IMeterFactory` carries
`using var _ = meters;`, because ADR-0072 and the package guide prescribe
it.

**Finding (empirical, 2026-11-11, xUnit `xunit.v3.mtp-v2` 4.0.1, net11.0,
JIT `Compono.XunitV3`).** A probe theory with
`[Theory, Compose<Profile>] Probe([Shared] D value, D again,
AsyncD a, Holder holder)`, where `D : IDisposable` and
`AsyncD : IAsyncDisposable`, was run with a file side-channel and no
`using` in the body:

```
body same=True holderSame=True
async disposed
sync disposed
sync disposed
```

xUnit itself disposes every `IDisposable`/`IAsyncDisposable` test-method
argument after the test case. It disposes once **per argument slot**, so a
`[Shared]` instance bound to two parameters is disposed twice. That
matches RESEARCH-0038 §2.4 (`DisposalTracker.AddRange(data)` per row on
3.2.2 and 4.0.1) and ADR-0070 contract item 12. **xUnit's AOT core
doesn't** (RESEARCH-0038 §2.4). Compono registers nothing with the
`DisposalTracker` (ADR-0022 Amendment 4, guarded by
`ComposeAttributeDisposalTests`).

**So on `Compono.XunitV3` (JIT) the consumer's `using var _ = meters;` is
redundant.** It's harmless, because `CapturingMeterFactory.Dispose()` is
idempotent. ADR-0072's guidance to always write it was conservative, and it
didn't distinguish engines.

**Ownership analysis.**

- *Values Compono created vs externally supplied.* Compono still can't tell
  them apart (RESEARCH-0015 §5 Model B, re-confirmed: no provenance between
  construction and return). xUnit's disposal ignores provenance. That's
  xUnit's existing behavior, already documented with a warning not to pass
  fixture-owned disposables as parameters.
- *Shared values.* `[Shared]` makes the parameter the SUT's instance, so
  xUnit's disposal of the parameter disposes the SUT's factory. Without
  `[Shared]`, the SUT's factory isn't a parameter and nothing disposes it.
- *Other frameworks.* Not re-tested here. TUnit's composed root-argument
  disposal is documented separately (ADR-0040, `Compono.TUnit` guide). It's not
  established for NUnit, MSTest, or
  xUnit AOT. Keep the explicit `using` guidance there.
- *Sync vs async.* xUnit prefers `DisposeAsync` when both are implemented.
  `IMeterFactory` is sync-only.
- *Double disposal.* Already happens today whenever a test also writes
  `using`, and whenever a `[Shared]` value appears in two parameter slots.
  Disposable types passed as composed parameters must be idempotent, as
  .NET guidance requires.

**Options considered.** (a) Compono.XunitV3 registers `[Shared]`
disposables with the tracker: redundant on JIT (xUnit already does it) and
reintroduces the ADR-0022 Amendment 4 ownership hazard as Compono's own
contract. Rejected. (b) The AOT package registers disposal: the same
ownership hazard, plus a JIT/AOT difference Compono would then own.
Rejected without a disposal-ownership ADR (RESEARCH-0015 Outcome C).
(c) Document per engine. **Chosen.**

**Recommendation: Document** (ADR-0072 Amendment 6 records the
clarification). On `Compono.XunitV3` the `using` line is optional. Keep it
on `Compono.XunitV3.Aot`, TUnit, NUnit, MSTest, and `Composer.Create`
tests. No automatic disposal, and no ownership-contract change.

**Complexity.** Small.

## Finding 4: asserting on one pass of a multi-pass test

**Consumer pain.** Outbox tests run several passes and assert on the last.
`ClearMeasurements()` before the step under test works, but the consumer
felt it hides the intent.

**Existing behavior.** `ClearMeasurements()` (ADR-0072) discards captured
measurements and keeps the sequence counter. Every captured measurement
carries an internal factory-wide sequence number (`StoredMeasurement`), so
a checkpoint would be cheap to build later.

**Options.** (a) Keep `ClearMeasurements()`. (b) A checkpoint token plus
`Verify(...).Since(mark)`. (c) A scoped `BeginCapture()`.

**Assessment.** (a) is the same idiom as `Compono.TestDoubles`
`ClearCalls()` (ADR-0060) and `Compono.Logging` `ClearCapturedEntries()`
(ADR-0071), so the ecosystem has one reset idiom. (b) is non-destructive
and cheap given the sequence numbers, but it's a new idiom that no other
package has, and the only evidence is readability. Nobody needs to assert
both before and after a point. (c) adds overlapping-scope concurrency
questions and the most surface.

**Decision (maintainer, 2026-11-11): Defer, and document the
`ClearMeasurements()` multi-pass pattern.** No new public API. Revisit
checkpoints only when real consumer evidence needs measurements from
several phases kept and asserted independently.

## Finding 5: `InCategory` is an exact match

**Consumer pain.** Host-level guards filter by namespace prefix, for
example "no errors outside `MyApp.Jobs.*`", which is a **negated** prefix.
Today that needs `Matching(e => !e.Category.StartsWith(...))`.

**Existing behavior.** `LogVerificationBuilder` has AND-only named filters.
`InCategory` is ordinal and exact, and `Matching` is the escape hatch. No
negated filter exists. Microsoft.Extensions.Logging's own filter rules use
case-insensitive prefix matching, but Compono's category filter is already
ordinal and case-sensitive, so consistency inside the builder wins.

**Options.** `InCategoryPrefix` only, a boundary-aware `InNamespace`, a
prefix plus negated pair, or docs only.

**Decision (maintainer, 2026-11-11): add `InCategoryPrefix(string prefix)`
and `NotInCategoryPrefix(string prefix)`.** `StringComparison.Ordinal`;
the prefix is literal, with no implicit `.` separator, and the docs show a
trailing `.` to avoid matching similarly named categories (`MyApp.Jobs.`
doesn't match `MyApp.JobsLegacy`); `ArgumentNullException` for `null`,
matching `InCategory`; existing `InCategory` unchanged; no general negation
framework. Recorded as ADR-0055 Amendment 7, because the builder's surface
is ADR-0055's.

**Edge semantics.** An empty prefix is allowed and literal:
`InCategoryPrefix("")` matches every entry and `NotInCategoryPrefix("")`
matches none. That's consistent with `string.StartsWith("")`, and it isn't
special-cased.

**AOT.** Plain string comparison. **Complexity.** Small.

## Finding 6: profile constructor arguments in `[Compose<TProfile>]`

**Consumer pain.** A profile that takes interceptors needed an extra
parameterless constructor so it could be named in `[Compose<TProfile>]`.
Variants that do need interceptors still use `Composer.Create(...)`.

**Existing mechanisms.** `[Compose<TProfile>]` requires
`TProfile : new()`. `[Compose<TProfile, TConfig>(args)]` builds `TConfig`
from attribute constants. `ComposeData<TProfile, TConfig>` supplies
`TConfig` from a static member's rows, including non-constant objects.
`CompositionBuilder.AddProfile(ICompositionProfile)` composes profiles.

**Patterns that already work with no new API.**

1. **Wrapper profile:** a parameterless profile per variant that calls
   `builder.AddProfile(new DbProfile(new AuditInterceptor()))`. It names
   the variant, works with `[Compose<TProfile>]` on every framework
   adapter, and works on AOT (`new()` constraint, no reflection).
2. **Single-row `ComposeData<TProfile, TConfig>`:** a static member returns
   one row whose `TConfig` carries the runtime objects. That's a
   `[Theory]` with one row on `Compono.XunitV3` and `Compono.XunitV3.Aot`.

**Options.** A `ProfileMember`/`ProfileFactory` static-member property on
the Compose-family attributes would touch JIT reflection, AOT generator
emission, new `CMP` diagnostics, and four framework adapters, and it would
create a second way to supply profile state next to `TConfig`.

**Decision (maintainer, 2026-11-11): Document + defer.** Document both
patterns, with examples that compile and run on JIT and Native AOT. No
`ProfileMember` or other activation mechanism. Revisit only on evidence
that these patterns are materially inadequate.

## Finding 7: migration guide from `Microsoft.Extensions.Diagnostics.Testing`

**Recommendation: Document.** The new page is
`docs/migrating-from-microsoft-extensions-diagnostics-testing.md`. It maps
`AddFakeLogging`/`FakeLogCollector` to
`LogCollector`/`AddComponoCapture`, and `MetricCollector<T>` to
`CapturingMeterFactory`/`UseMetrics()`/`Verify<T>`/`GetMeasurements<T>`.
It covers registration order (stage-6 order, plus host DI "last
registration wins" from ADR-0072 Amendment 3), explicit disposal, and
`ClearMeasurements()` for multi-pass tests. Its examples are backed by a
compiled test class in the logging and metrics test projects.

## Bugs vs enhancements

- **Bugs (documentation/diagnostic defects):** finding 1's omission of
  `UseGeneratedTestDoubles()` from the `UseMetrics()` remarks and the
  misuse message; finding 3's engine-agnostic "always dispose in the body"
  guidance, which is redundant on xUnit JIT.
- **Enhancements:** finding 1's generator exclusion, finding 5's filters.
- **Documentation gaps:** findings 2, 4, 6, 7.
- **No runtime defect was found** in any #161 feature.

## Links

- [#161 follow-up comment](https://github.com/LayeredCraft/compono/issues/161#issuecomment-6810922325)
- [ADR-0022](../adr/0022-compono-xunit-package-design.md) Amendment 4 (no automatic disposal)
- [ADR-0024](../adr/0024-public-provider-extensibility-model.md) (stage-6 order)
- [ADR-0055](../adr/0055-compono-logging-testing-support-package.md) Amendments 4 and 7
- [ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md) (row layout, disposal item 12)
- [ADR-0072](../adr/0072-compono-metrics-testing-support-package.md) Amendments 5 and 6
- [RESEARCH-0015](0015-disposal-ownership-research.md), [RESEARCH-0038](0038-issue-163-composed-theory-data-rows.md)
- [PLAN-0075](../plans/0075-issue-161-1-4-dogfood-follow-up-impl-plan.md)
