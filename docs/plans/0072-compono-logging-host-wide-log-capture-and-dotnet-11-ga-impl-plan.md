# [PLAN-0072] `Compono.Logging` Host-Wide Log Capture (Issue #164) + .NET 11 GA Migration

**Status:** In Progress

**Implements:** [ADR-0071](../adr/0071-compono-logging-host-wide-log-capture.md)
(issue [#164](https://github.com/LayeredCraft/compono/issues/164), child of
[#161](https://github.com/LayeredCraft/compono/issues/161)), plus the
.NET 11 GA migration, an infrastructure workstream that needs no ADR.
ADR-0071 is **Accepted** (2026-11-10). The maintainer approved ADR-0071 and
this plan, including the review-round-1 clarifications in `768af68`, and
B0 is closed. Architectural approval is complete. The accepted planning
artifacts now live on `main` (merged via PR #175). Implementation has
started: B1 (PR 1, .NET 11 GA) is in progress.

## Goal

1. **Workstream B (.NET 11 GA):** the repo builds, tests, packs,
   package-validates, and Native-AOT-validates on the **.NET SDK 11.0.100
   GA**. It has no remaining .NET 11 preview/RC SDK or package references,
   and the net8.0, net9.0, net10.0, and net11.0 targets are unchanged.
2. **Workstream A (#164):** a test can write
   `builder.Logging.AddComponoCapture(logs)` and then
   `logs.Verify().AtLevelOrAbove(LogLevel.Error).Never()`, capturing every
   category a real Generic Host creates. Captured entries expose
   `Category`, and existing `Compono.Logging` behavior is unchanged
   apart from that additive property. Native AOT, ApiCompat, docs, the API
   reference, and the distributed `compono` skill all reflect the shipped
   API.

## Scope

**In:** everything in ADR-0071's Decision Outcome, plus Workstream B below.

**Out (do not build):**

- Opt-in forwarding of composed loggers into a `LogCollector` (ADR-0071
  Axis 2, deferred). No `LoggingOptions` changes.
- Any `LogCollector` constructor options or Compono-side level gate
  (ADR-0071 Axis 3).
- A public `ILoggerProvider` type. Composing, replacing, or wrapping
  `ILoggerFactory`.
- Prefix or wildcard category matching, and `ControlLevel`-style toggling.
- Any new `PackageReference` in `src/Compono.Logging`, any change to core
  `Compono`, `Compono.Generators`, or any other `src/` package.
- Metrics (#165), #166, and any unrelated dependency bump (for example
  `Basic.Reference.Assemblies.Net110` stays at `1.8.12`, `Microsoft.NET.Test.Sdk`
  stays at `18.10.1`, and the Roslyn `4.11.0` floor stays unchanged).
- Package version changes, NuGet publishing, and edits to the v1.4.0 draft
  release.
- Real-consumer dogfood (product-owner decision, 2026-11-10: skipped.
  In-repo Generic Host tests, pack-and-consume, and the AOT smoke test
  replace it).

**Release:** both workstreams ship in the #161 release (v1.4.0). There is
no interim release.

## Decisions already made (do not reopen during implementation)

From the maintainer interview (2026-11-10), recorded in ADR-0071:

| # | Decision |
|---|---|
| D1 | Explicit, test-owned `LogCollector`. Attach it with `ILoggingBuilder.AddComponoCapture(LogCollector)`. The provider is `internal`. |
| D2 | Composed loggers and host capture are **separate** in v1. |
| D3 | `LogVerificationBuilder` gains `AtLevelOrAbove(LogLevel)` + `InCategory(string)`. |
| D4 | `CapturedLogEntry.Category`. `CapturingLogger<T>` gets exact parity with `LoggerFactory.CreateLogger<T>()`. Non-generic gets `""`. |
| D5 | Type name is `LogCollector`. The extension class is `ComponoLoggingBuilderExtensions`. Both live in namespace `Compono.Logging`. |
| D6 | Filtering is host rules only. `LogCollector()` is parameterless. The provider has `[ProviderAlias("Compono")]`. |
| D7 | .NET 11 GA ships as its **own PR, first**. #164 is built on top of it. |
| D8 | Dogfood is skipped. |

Behavioral requirements that ADR-0071 pins down and tests must prove:
registration cardinality (same collector registered twice is idempotent,
distinct collectors fan out, one collector across hosts is allowed), the
disposal model (the host disposes the provider, and disposal **stops
subsequent capture through that provider**, including through loggers
that callers still hold, with no exceptions; entries captured before
disposal stay readable through the collector, which is never cleared),
the intentional `CapturedLogEntry` equality change (record equality now
includes `Category`, kept and documented as a behavioral compatibility
change), scope sourcing (`ISupportExternalScope`
with a fallback), `IsEnabled == level != None`, and one shared
entry-construction path for the composed and host paths.

**Left to engineering judgment:** internal type names and file layout,
how `LogEntryCollector` is refactored so both paths share entry
construction and a store, how `LogVerificationBuilder` gets its entry
source (for example an internal snapshot delegate or interface), the
registration mechanism that meets the cardinality rules, and how category
names are computed (as long as parity tests pass).

## PRs and passes

| PR | Branch (implementer creates from `main`) | Beads |
|---|---|---|
| **PR 1**: `chore: migrate to .NET 11 GA SDK and packages` | `chore/dotnet-11-ga` | B1 |
| **PR 2**: `feat(logging): host-wide log capture (#164)` | `feat/164-host-wide-log-capture`, created from `main` **after PR 1 merges** | B2–B7 |

Beads (local-only tracker): epic `compono-yce`. B0 = `compono-yce.1`,
B1 = `.2`, B2 = `.3`, B3 = `.4`, B4 = `.5`, B5 = `.6`, B6 = `.7`,
B7 = `.8`.

PR 2 is one cohesive feature PR. Its passes are internal checkpoints, the
same model as PLAN-0071: no docs-less or AOT-unvalidated partial merge.
Every pass leaves the branch at `dotnet build` 0 warnings / 0 errors with
the pass's focused tests green. The **full** suite runs only at B1 and B7
(and wherever a pass says so), not after every small change. Commits,
pushes, and PRs need product-owner approval.

```
B0 (PO accepts ADR-0071/PLAN-0072)
 └─ B1 .NET 11 GA (PR 1, merged)
     └─ B2 Category + verify filters + shared entry construction
         └─ B3 LogCollector + provider + AddComponoCapture (+ core host tests)
             ├─ B4 Edge-case / concurrency / isolation tests
             │   └─ B5 Native AOT + package compatibility
             └─ B6 Docs, API reference, skill + evals
                  (B6 may run in parallel with B4/B5)
B7 Final integration + release-readiness gate   (needs B4, B5, B6)
```

---

## Workstream B: .NET 11 GA (PR 1)

### Verified versions (authoritative sources, checked 2026-11-10)

| Item | Current | Target | Source |
|---|---|---|---|
| .NET SDK | `11.0.100-preview.7.26381.103` | **`11.0.100`** | `builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json`: `latest-sdk` 11.0.100, `latest-runtime` 11.0.0, release date 2026-11-10, `support-phase` active |
| `Microsoft.Extensions.Logging.Abstractions` (net11.0) | `[11.0.0-preview.6.26359.118, 12.0.0-a)` | **`[11.0.0, 12.0.0)`** | nuget.org flat container: `11.0.0` is the newest 11.x |
| `Microsoft.Extensions.Options` (net11.0) | `[11.0.0-preview.7.26381.103, 12.0.0-a)` | **`[11.0.0, 12.0.0)`** | nuget.org: `11.0.0` |
| net8/9/10 ranges | unchanged | unchanged | not part of this migration |

Both 11.0.0 packages ship an **empty** `net11.0` dependency group, so the
packed `.nuspec` for net11.0 still declares no dependency. The existing
`inspect-packed-nupkgs.sh` "absent for net11.0" assertion must keep
passing unchanged.

With a stable lower bound, NuGet no longer considers prereleases, so the
`12.0.0-a` sentinel upper bound and the comment justifying it go away. Use
the same `[X.0.0, (X+1).0.0)` shape as net8/9/10.

### B1: .NET 11 GA migration

**Scope / files:**

- `global.json`: change `sdk.version` to `11.0.100` and keep
  `rollForward: latestFeature` (the existing policy, unchanged).
- `.github/workflows/package-validation.yaml`: change the SDK entry
  `11.0.100-preview.7.26381.103` to the floating `11.0.x` (product-owner
  decision, 2026-11-11: match `pr-build.yaml`/`publish-preview.yaml`/
  `publish-release.yaml`/`docs.yml`). Retire the
  exact-pin-in-lockstep-with-global.json policy and rewrite the comment so it
  no longer describes a preview pin. Keep the RC.1 incident as history.
- `.github/workflows/aot-validation.yaml`: fix the stale comment saying
  "global.json pins a preview 11.0.x SDK". `11.0.x` stays.
- `pr-build.yaml`, `publish-preview.yaml`, `publish-release.yaml`, and
  `docs.yml` need **no change**, because floating `11.0.x` now resolves to GA.
  Confirm this, don't edit it.
- `Directory.Packages.props`: change the two net11.0 ranges above and
  rewrite their prerelease-lower-bound comments.
- Current-state docs: grep `README.md`, `docs/getting-started`,
  `docs/packages`, `docs/how-to`, `docs/troubleshooting`,
  `docs/contributing.md`, and `skills/` for .NET 11 preview/RC wording,
  and fix any you find. (A 2026-11-10 sweep found none, so this should be a
  confirm-only step.) **Do not** edit ADRs, research docs, or completed plans.
  Their preview references are historical record.

**Acceptance criteria:**

- `git grep -nE "11\.0\.[0-9]+-(preview|rc)"` outside `docs/adr`,
  `docs/research`, `docs/plans`, and `docs/reference/api` returns only the
  intentional RC.1 history line in `package-validation.yaml`'s comment.
- `dotnet --version` at the repo root prints `11.0.100`.
- `dotnet build` is 0 warnings / 0 errors and `dotnet test` is fully green,
  across all four TFMs.
- `obj/project.assets.json` for `src/Compono.Logging` and
  `src/Compono.Options` resolves `11.0.0` (not a preview) for net11.0.
- Packed `Compono.Logging`/`Compono.Options` `.nuspec` net8/9/10 ranges
  are unchanged, and net11.0 still has no dependency entry
  (`inspect-packed-nupkgs.sh` and `inspect-packed-nupkgs.tests.sh` pass).
- The NUnit compatibility matrix passes on 11.0.100 in **both** the classic
  VSTest and MTP legs, and the classic leg genuinely runs the classic host
  (PR #172's host-identity assertion). Resolved risk: the earlier
  classic-VSTest "No row-binding dispatch is registered for Widget" failure
  was a pre-existing Compono.NUnit defect that newer VSTest hosts exposed
  (not caused) — fixed in PR #172 / ADR-0059 Amendment 1, not an RC.1 or GA
  regression.
- Package validation (ApiCompat against the nuget.org baselines, all 13
  packages) passes with no new suppressions.
- AOT validation passes for every leg on the GA SDK with zero IL/AOT
  warnings.
- Generator tests are green on the GA SDK's compiler, with
  `Basic.Reference.Assemblies.Net110` left at 1.8.12. If they fail
  **because of** the reference-assembly package, stop and ask before
  bumping it (it's an unrelated dependency).

**Validation:** V1, V3 (all legs), V6, V7, V8 below.

**Deliverable / checkpoint:** PR 1 opened (with product-owner approval),
CI green, and merged. Record the CI run links and the matrix outcome in
this plan's Notes. Tick B1.

- [x] B1.1 SDK pin (`global.json`, `package-validation.yaml`) and comment fixes
- [x] B1.2 net11.0 package ranges (`Directory.Packages.props`)
- [x] B1.3 Current-state docs sweep (confirm or fix)
- [x] B1.4 Full validation (V1, V3, V6, V7, V8) on 11.0.100, results recorded in Notes
- [ ] B1.5 PR 1 merged

---

## Workstream A: host-wide log capture (PR 2)

### B2: `Category`, verification filters, shared entry construction

**Scope / files:** `src/Compono.Logging/CapturedLogEntry.cs`,
`LogEntryCollector.cs` (refactor),
`CapturingLogger.cs`/`CapturingLogger{T}.cs`,
`LogVerificationBuilder.cs`, `LoggerTestingExtensions.cs` (one sentence
added to the not-a-capturing-logger message),
`test/Compono.Logging.Tests/*`.

**Work:**

- Add `public string Category { get; }` to `CapturedLogEntry` and to
  its internal constructor.
- Refactor so that **one** internal component builds a `CapturedLogEntry`
  from `(category, level, eventId, state, exception, formatter, scope
  snapshot)`. B3's provider must reuse it. It must stay reflection-free,
  and the existing `ExtractStructuredState`/scope-snapshot behavior must be
  unchanged.
- `CapturingLogger<T>`: category is computed once at construction, with
  exact parity to `LoggerFactory.CreateLogger<T>()`. Non-generic
  `CapturingLogger` gets `""`. No public constructor changes.
- `LogVerificationBuilder.AtLevelOrAbove` and `InCategory`, following
  ADR-0071's exact semantics and failure-message fragments.
- `LoggerTestingExtensions`' `InvalidOperationException` message: append
  a sentence telling host-created loggers to use `LogCollector` +
  `AddComponoCapture`. Keep the type and the existing text.

**Tests (required):**

- Category parity theory: for a set of `T`, `new CapturingLogger<T>()`'s
  captured `Category` equals the category a real `LoggerFactory` (test-only
  `Microsoft.Extensions.Logging` reference) passes to a probe
  `ILoggerProvider` for `CreateLogger<T>()`. The set must include: a plain
  namespaced class, a nested class, a nested-in-nested class, a closed
  generic (`Gen<int>`), a generic nested in a generic, a framework generic
  (`List<Nested>`), a type in the global namespace, built-ins (`int`,
  `string`, `object`, `bool`), and an array if `ILogger<T[]>` is
  constructible. The expected values come from the real factory at test
  time and are never hard-coded.
- Composed `ILogger<T>` via `UseLogging()` carries the same parity
  category. Composed bare `ILogger` and `new CapturingLogger()` give `""`.
- `AtLevelOrAbove`: boundary at each level, `None` never matches, combined
  with other filters, and failure-message text.
- `InCategory`: exact match, case-sensitivity, a near-miss prefix
  doesn't match, null throws, and failure-message text.
- **Equality regression test (required, targeted):** two entries that are
  identical except for `Category` (for example from `CapturingLogger<A>` and
  `CapturingLogger<B>` logging the same call) are **not** equal under
  `Equals`/`==`. Two entries that are identical including `Category` (same
  logger, deterministic fields) compare equal on every field the test
  controls. The test name or comment cites ADR-0071's "intentional
  behavioral compatibility change" so nobody "fixes" it later.
- **Regression:** every existing `Compono.Logging.Tests` test passes
  unmodified, except tests that assert the exact full text of the
  not-a-capturing-logger message, if any (update the assertion to the new
  text only).

**Acceptance:** build 0/0, `dotnet test test/Compono.Logging.Tests` green,
and `test/Compono.Generators.Tests`' logging tests green (activation
unchanged).

- [ ] B2.1 `Category` + shared entry construction refactor
- [ ] B2.2 MEL-parity category naming
- [ ] B2.3 `AtLevelOrAbove` / `InCategory`
- [ ] B2.4 Exception-message sentence
- [ ] B2.5 Tests above, focused suites green

### B3: `LogCollector`, internal provider, `AddComponoCapture`

**Scope / files:** new `src/Compono.Logging/LogCollector.cs`,
`ComponoLoggingBuilderExtensions.cs`, an internal provider + logger type,
`test/Compono.Logging.Tests` (new host-integration test file(s)), and
`Directory.Packages.props` (test-only `Microsoft.Extensions.Hosting` +
`Microsoft.Extensions.Logging`, per-TFM, see below).
`test/Compono.Logging.Tests/Compono.Logging.Tests.csproj` gets the
test-only references.

**Work:** implement ADR-0071's public API, scopes, filtering, ownership,
and registration-cardinality rules exactly. **`src/Compono.Logging.csproj`
gains no `PackageReference`.**

**Test-only package versions:** pin each TFM to the newest stable patch in
that major, verified on nuget.org on 2026-11-10. Re-verify when you
implement, and don't guess:

| TFM | `Microsoft.Extensions.Hosting` | `Microsoft.Extensions.Logging` |
|---|---|---|
| net8.0 | 8.0.1 | 8.0.1 |
| net9.0 | 9.0.23 | 9.0.23 |
| net10.0 | 10.0.15 | 10.0.15 |
| net11.0 | 11.0.0 | 11.0.0 |

Use the existing per-TFM conditional `ItemGroup` pattern in
`Directory.Packages.props`, with a comment saying the references are
test-only and never shipped. Exact versions (not ranges) are fine, since
these never reach a `.nuspec`.

**Tests (core, required here):**

- A real `Host.CreateApplicationBuilder()` + `AddComponoCapture(logs)`.
  Entries arrive from `ILoggerFactory.CreateLogger("Arbitrary.Category")`,
  from a container-resolved `ILogger<T>` (its category matches the parity
  rule), and from the host's own `Microsoft.Hosting.Lifetime` on
  start/stop.
- The headline scenario:
  `logs.Verify().AtLevelOrAbove(LogLevel.Error).Never()` passes on a clean
  run and fails with a readable message when some never-composed category
  logs an `Error`.
- Every entry field is populated correctly for host entries: level, event
  id, message, exception, state, properties, message template (both the
  `LogInformation` and `[LoggerMessage]` styles), scopes, and timestamp.
- Scopes: host `BeginScope` nesting is outermost to innermost and correct
  across `await`. A dictionary scope's structured values are reachable.
  Sibling-task isolation behaves as ADR-0055 documents.
- Filtering: under a default host, `Debug`/`Trace` are not captured. After
  `SetMinimumLevel(Trace)` they are. A host category filter rule excludes a
  category. A `Logging:Compono:LogLevel` configuration rule (the alias)
  targets only the Compono provider. `LogLevel.None` is never captured.
- `services.AddLogging(b => b.AddComponoCapture(logs))` without a host
  works.
- `GetLastCapturedEntry`, `ClearCapturedEntries`, and an empty collector.
- `AddComponoCapture` null-argument guards, and it returns the same
  builder.

**Acceptance:** build 0/0, `dotnet test test/Compono.Logging.Tests` green
on all TFMs, and no new `PackageReference` in the shipped project
(`git diff src/Compono.Logging/Compono.Logging.csproj` shows none).

- [ ] B3.1 `LogCollector`
- [ ] B3.2 Internal provider/logger (`ISupportExternalScope`, `[ProviderAlias("Compono")]`)
- [ ] B3.3 `AddComponoCapture` with the cardinality rules
- [ ] B3.4 Test-only package wiring
- [ ] B3.5 Core host tests green

### B4: edge cases, concurrency, isolation, negative tests

**Scope / files:** `test/Compono.Logging.Tests/*`. Production code changes
only to fix defects these tests reveal (note any in Notes).

**Tests (required):**

- **Cardinality:** the same collector twice on one builder gives one copy
  of each entry. Two collectors on one builder both receive every entry.
  One collector across two hosts receives both hosts' entries, and their
  categories are distinguishable.
- **Disposal regression test (required, explicit):** log N entries
  through a host, then `host.Dispose()`. Then, through a **still-held**
  container `ILogger<T>` and a still-held `CreateLogger("X")` logger:
  `IsEnabled` returns `false` for every level, logging at every level
  (including `Error`/`Critical` and a direct `Log<TState>` call) appends
  **nothing** and doesn't throw, and `BeginScope` doesn't throw. The
  collector still returns exactly the N pre-disposal entries
  (`GetCapturedEntries`, `GetLastCapturedEntry`, and `Verify` counts are
  unchanged), and `ClearCapturedEntries` still works. Also: disposal is
  idempotent (a double dispose doesn't throw), and the provider is disposed
  by the host (observable through an internal seam). For one collector
  registered with two hosts, disposing host A stops A's capture while
  host B keeps capturing into the same collector.
- **Isolation:** two hosts with two collectors logging concurrently share
  no entries, and there's no static state (a test runs both in parallel
  and asserts exact disjoint sets).
- **Concurrency:** many categories log concurrently while other threads
  call `GetCapturedEntries`/`Verify`/`ClearCapturedEntries`. There are no
  exceptions, no torn entries, and the counts are consistent after the
  writers join. **Bounded:** fixed iteration counts and no unbounded
  writer loop (RESEARCH-0033 / the PR #141 CI-hang lesson).
- **Composed vs. host separation:** a test that uses both `UseLogging()`
  composition and a host with a collector. The composed logger's
  `Verify()` never sees host entries, and the collector never sees composed
  entries.
- **Host loggers + extension methods:** calling `GetCapturedEntries()`/`Verify()`
  on a host `ILogger<T>` throws `InvalidOperationException`, and the
  message mentions `LogCollector`.
- **Activity scopes:** with `ActivityTrackingOptions` enabled and an
  `Activity` started, the activity scope appears in `Scopes` and doesn't
  break extraction (this documents the ADR consequence).
- **Exceptions:** an entry with an exception and no message args, plus an
  `AggregateException`, are captured by reference.

**Acceptance:** `dotnet test test/Compono.Logging.Tests` green; then
**full** `dotnet build && dotnet test` green (the first full run on PR 2).

- [ ] B4.1 Cardinality + disposal
- [ ] B4.2 Isolation + bounded concurrency
- [ ] B4.3 Separation, host-logger misuse, activity scopes, exceptions
- [ ] B4.4 Full suite green

### B5: Native AOT + package compatibility

**Scope / files:** `test/Compono.Logging.AotSmokeTest/Program.cs` +
`.csproj` (test-only `Microsoft.Extensions.Logging` or
`Microsoft.Extensions.Hosting` reference for net10.0 only, matching the
project's TFM), `verify-packaging.sh` if needed, and
`.github/scripts/inspect-packed-nupkgs.sh` (only if a "no new dependency"
assertion is missing for `Compono.Logging`).

**Work / checks:**

- The AOT smoke test drives a real `LoggerFactory` (or host) with
  `AddComponoCapture`, logs across several categories with a scope and a
  structured message, and asserts category, level, properties, scopes,
  `AtLevelOrAbove`, and `InCategory`. It must also assert the
  `CapturingLogger<T>` category parity under AOT.
- `dotnet publish -p:PublishAot=true` produces zero `IL2xxx`/`IL3xxx`
  warnings, and the published binary exits 0.
- Pack-and-consume: pack `Compono` + `Compono.Logging` to a local feed and
  consume them from a scratch console/test project outside the repo
  (JIT), using a real Generic Host. Run the headline scenario.
- ApiCompat: `dotnet pack src/Compono.Logging -c Release
  -p:PackageValidationBaselineVersion=<latest stable on nuget.org>`
  reports **additions only**, with no suppression file changes.
- `.nuspec` dependency groups for `Compono.Logging` are byte-for-byte the
  same set as on `main` (no new dependency on any TFM).

**Acceptance:** V3 (Logging leg), V5, V6, V7 pass, with results recorded in
Notes.

- [ ] B5.1 AOT smoke test extended, publish clean, binary passes
- [ ] B5.2 Pack-and-consume
- [ ] B5.3 ApiCompat + nuspec dependency check

### B6: docs, examples, API reference, `compono` skill

**Scope / files:**

- `docs/packages/compono-logging.md`: a new "Host-wide capture" section
  (Generic Host, `services.AddLogging`, `WebApplicationFactory`-style
  `ConfigureLogging`), `Category`, the two new filters, separate-destination
  semantics, host-filtering consequences (Debug/Trace + `SetMinimumLevel`,
  the `Compono` alias), ownership/disposal (disposal stops capture, and
  pre-disposal entries stay readable), cardinality, and activity scopes.
  Add a clearly labelled **behavioral compatibility note**:
  `CapturedLogEntry` equality now includes `Category`. Draft a matching
  release-note line in PLAN-0072 Notes for the v1.4.0 release author.
  Don't edit the draft release itself. Change the "`ILoggerFactory` composition" boundary bullet to say
  host *provider* capture is supported, but composing an `ILoggerFactory`
  isn't.
- Cookbook/how-to: one recipe, "Assert nothing logged at Error+ across a
  Generic Host" (place it following existing cookbook conventions).
- Troubleshooting: "`Verify()` on a host `ILogger<T>` throws" and
  "Debug entries are missing from `LogCollector`".
- `docs/public-api.md` / `docs/architecture.md`: update only if they
  enumerate the `Compono.Logging` surface.
- `docs/adr/0071-...`: Status stays `Accepted` (set at B0). Add an
  amendment only if implementation changes a decision, and stop and ask
  before doing that.
- API reference: regenerate `docs/reference/api` **before push** (memory
  rule: regenerate whenever public XML docs change).
- `skills/compono/references/logging.md`: host capture, `LogCollector`,
  the new filters, `Category`, separation, and filtering. Update the
  "No `ILoggerFactory` composition" boundary line.
  `skills/compono/SKILL.md`: update only if its routing or summary
  mentions logging boundaries.
- `skills/compono/evals/evals.json`: add at least two evals. (1) A Generic
  Host "no Error+ in any category" prompt whose expected answer uses
  `LogCollector` + `AddComponoCapture` + `AtLevelOrAbove`, not
  `FakeLogCollector` or a composed logger. (2) A "why does
  `logger.Verify()` throw on my host `ILogger<T>`" prompt. Follow the
  existing eval schema (`id`, `category`, `prompt`, `expected_output`,
  `expectations`, `files`).
- Every public member added in B2/B3 has XML docs.

**Acceptance:** V4 shows no uncommitted API-reference drift after
regeneration, V8 passes, every relative link resolves, and the docs
describe only shipped behavior.

- [ ] B6.1 Package doc + recipe + troubleshooting
- [ ] B6.2 API reference regenerated
- [ ] B6.3 Skill reference + evals
- [ ] B6.4 Link and validation checks

### B7: final integration and release-readiness gate

**Preconditions:** B1 merged (PR 2's branch is based on post-B1 `main`),
and B4, B5, and B6 done.

**Checks:**

- Rebase or confirm on the latest `main`. `dotnet --version` is
  `11.0.100`.
- V1 full: `dotnet build` 0/0 and `dotnet test` green on all TFMs.
- V3 AOT for the Logging leg (and any leg whose inputs changed).
- V6 ApiCompat for `Compono.Logging`, plus V7 nuspec/package inspection.
- V8 skill validation.
- Cross-check every #164 acceptance criterion against a test or doc
  (table in Notes):
  1. any-category capture (B3)
  2. `Category` plus every existing field (B2/B3)
  3. `Verify()` vocabulary including the level threshold (B2/B3)
  4. no breaking change for existing capture (B2 regression + B4
     separation), with the one intentional equality change pinned by B2's
     equality regression test and documented in B6
  5. relationship documented (B6)
  6. no new dependency (B5)
  7. AOT/trimming (B5)
- `git diff main --stat` touches only `src/Compono.Logging`,
  `test/Compono.Logging.Tests`, `test/Compono.Logging.AotSmokeTest`,
  `Directory.Packages.props` (test-only entries), docs, `docs/reference/api`,
  and `skills/`. There are no core, generator, or other-package changes.
- Set PLAN-0072 to `Done`, tick all tasks, and close the beads. The PR
  references #164 (it closes #164, not #161).

- [ ] B7.1 Full validation green
- [ ] B7.2 Acceptance-criteria cross-check table in Notes
- [ ] B7.3 Plan closeout, PR 2 opened with product-owner approval

### B0: product-owner acceptance (precedes everything)

- [x] Product owner reviews ADR-0071, PLAN-0072, and the beads, then
  flips ADR-0071 to `Accepted` (and updates the `docs/adr/README.md` row and
  ADR-0055 Amendment 6's "(Proposed)" note). Done 2026-11-10 (maintainer
  approval; bead `compono-yce.1` closed).

## Critical Files

- `global.json`, `Directory.Packages.props`,
  `.github/workflows/package-validation.yaml`, `.github/workflows/aot-validation.yaml` (B1)
- `src/Compono.Logging/CapturedLogEntry.cs`: `Category` (B2)
- `src/Compono.Logging/LogEntryCollector.cs`: shared entry construction (B2)
- `src/Compono.Logging/CapturingLogger{T}.cs`, `CapturingLogger.cs`: category (B2)
- `src/Compono.Logging/LogVerificationBuilder.cs`: two filters, entry source (B2)
- `src/Compono.Logging/LoggerTestingExtensions.cs`: message sentence (B2)
- `src/Compono.Logging/LogCollector.cs`: new (B3)
- `src/Compono.Logging/ComponoLoggingBuilderExtensions.cs`: new (B3)
- `src/Compono.Logging/<internal provider>.cs`: new (B3)
- `test/Compono.Logging.Tests/**`, `test/Compono.Logging.AotSmokeTest/**` (B2–B5)
- `docs/packages/compono-logging.md`, cookbook/troubleshooting pages,
  `docs/reference/api/**`, `skills/compono/references/logging.md`,
  `skills/compono/evals/evals.json` (B6)

## Test Plan

Per `references/testing.md`: xUnit v3 on MTP, handwritten explicit test
data, and every TFM. B2 covers unit-level behavior: category parity,
filters, and the regression run. B3 covers real Generic Host integration.
B4 covers edge cases, bounded concurrency, isolation, and disposal. B5
covers Native AOT and packaged consumption. Expected category values always
come from a real `LoggerFactory` at test time.

## Validation commands

Run from the repo root. `$SCRATCH` is any directory outside the repo.

```bash
# V1 full
dotnet --version            # expect 11.0.100
dotnet build && dotnet test

# V2 focused (iteration)
dotnet test test/Compono.Logging.Tests
dotnet test test/Compono.Generators.Tests

# V3 Native AOT, Logging leg (CI: aot-validation.yaml runs every changed leg)
bash test/Compono.Logging.AotSmokeTest/pack-compono.sh
dotnet publish test/Compono.Logging.AotSmokeTest -c Release -f net10.0 -r osx-arm64 \
  --self-contained true -p:PublishAot=true \
  -o "$SCRATCH/logging-aot" 2>&1 | tee "$SCRATCH/logging-aot.log"   # CI uses linux-x64
grep -E "warning (IL|AOT)[0-9]+" "$SCRATCH/logging-aot.log" && echo FAIL || echo OK
"$SCRATCH/logging-aot/Compono.Logging.AotSmokeTest"
bash test/Compono.Logging.AotSmokeTest/verify-packaging.sh
# B1 only: repeat the pack/publish/run for every AotSmokeTest leg.

# V4 API reference
dotnet tool restore && .github/scripts/generate-api-reference.sh
git status --short docs/reference/api

# V5 pack-and-consume: pack Compono + Compono.Logging to a local feed and consume from
#   $SCRATCH (Generic Host + AddComponoCapture headline scenario).

# V6 package validation
dotnet pack src/Compono.Logging -c Release -p:PackageValidationBaselineVersion=<latest stable>
#   B1: every package in package-validation.yaml's PACKAGES list.

# V7 package inspection
bash .github/scripts/inspect-packed-nupkgs.tests.sh
#   plus inspect-packed-nupkgs.sh against freshly packed nupkgs (see package-validation.yaml)

# V8 NUnit compatibility matrix (B1 only; classic VSTest + MTP legs)
.github/scripts/nunit-compatibility-matrix.sh

# V9 skill (B6/B7)
python3 -c "import json;json.load(open('skills/compono/evals/evals.json'))"
#   plus quick_validate.py from skill-creator, and every relative ](...) link under skills/compono exists
```

## Notes

- 2026-11-10 (design): a spike (class library referencing only
  `Logging.Abstractions`, net8.0 + net11.0, `IsAotCompatible`, real host
  on 11.0.100) confirmed every factual premise in ADR-0071's Context:
  no new dependency, host default `Information`, external scopes,
  instance- vs. factory-registered disposal, the `ProviderAlias` location,
  and `LoggerFactory` category naming (`N.Outer+Inner` → `N.Outer.Inner`,
  `Gen<int>` → `Gen`, `int` → `int`).
- 2026-11-10 (ADR review round 1, product owner): architecture and plan
  approved, subject to two clarifications, both now applied. (1) Disposing
  the provider stops subsequent capture through it, while pre-disposal
  entries stay readable. B4 has an explicit regression test for this. (2)
  `CapturedLogEntry` equality including `Category` is kept as an intentional
  behavioral compatibility change, with a B2 regression test and B6 docs
  and release-note text. Duplicate-registration semantics are unchanged and
  not expanded.
- 2026-11-10 (final planning approval): the maintainer approved ADR-0071
  and PLAN-0072, including `768af68`. ADR-0071 → `Accepted`, its index row
  and ADR-0055 Amendment 6 were updated, and B0 is closed. Plan status
  stays `Not Started` until B1 begins.
- 2026-11-11 (B1 resume): resolved the planning-branch integration — the
  accepted ADR-0071/PLAN-0072/ADR-0055-Amendment-6 artifacts were merged to
  `main` via PR #175 (merge commit 4f56a03). Reconstructed `chore/dotnet-11-ga`
  from `origin/main` (4f56a03) carrying only the four intended B1 edits, so the
  B1 diff is B1-only (no planning or unrelated doc commits). Reconciled the CI
  SDK policy to the approved form: `global.json` pins `11.0.100` with
  `rollForward: latestFeature`; `package-validation.yaml` floats `11.0.x`.
- 2026-11-11 (risk resolution): the RC.1-era classic-VSTest NUnit failure
  ("No row-binding dispatch is registered for Widget") was NOT an SDK
  regression — it was a pre-existing Compono.NUnit discovery-time defect,
  fixed in PR #172 (ADR-0059 Amendment 1); PLAN-0073 is Done. The matrix's
  classic leg now also asserts the real classic host (PR #172's host-identity
  check), so V8 is a genuine classic-host gate.
- 2026-11-11 (B1.4 full validation on the GA SDK 11.0.100 — all green):
  - V1 build `dotnet build Compono.slnx -c Release`: 0 warnings, 0 errors.
  - V1 test `dotnet test Compono.slnx -c Release --no-build`: 6791 total,
    0 failed, 6290 succeeded, 501 skipped.
  - V8 matrix `.github/scripts/nunit-compatibility-matrix.sh` (exit 0):
    NUnit 3.14.0 and 4.6.1 each under genuine classic VSTest (VSTest 18.12.0)
    and MTP at 5/5 per leg — including the host-identity assertion — plus
    5.0.0-beta.1 surveillance 5/5 under both hosts.
  - V3 AOT: all nine `test/*AotSmokeTest` legs pack + publish + run PASS
    (net10.0). Only pre-existing third-party trim/AOT warnings surfaced
    (`nunit.framework` IL3053/IL2104, `xunit.v3.core` IL2104, Compono.TUnit
    harness's own IL2072); none attributable to Compono. B1's diff touches no
    net10.0-affecting source (the `Directory.Packages.props` edits are strictly
    `net11.0`-conditional), so every AOT leg is byte-identical to `main`.
  - V6 ApiCompat: all 13 publishable packages pack against the
    `1.4.0-preview.123` baseline with 0 `CP` errors.
  - V7 packed-nupkg inspection: `inspect-packed-nupkgs.tests.sh` and
    `inspect-packed-nupkgs.sh` both pass (net11.0 stays dependency-free for the
    framework-provided `Microsoft.Extensions.*` packages).
  - API-reference drift: `generate-api-reference.sh` +
    `git diff --exit-code -- docs/reference/api` clean.
  - Preview/RC grep: the only tracked match outside `docs/adr`, `docs/research`,
    `docs/plans`, `docs/reference/api` is the intentional RC.1 history line in
    `package-validation.yaml`.
