# [PLAN-0069] Generator-Wide Per-Item Emission Failure Isolation

**Status:** Done

**Implements:** ADR-0068

## Goal

`ComponoIncrementalGenerator`'s six independent-item emission call sites
each catch an unexpected exception from their own item's emission work,
report `CMP0050` (`Compono.Generators`, Error) attributing it to that
item, and emit no source for that item - without touching
`loggingRuntimeSymbolsStatus`, without a shared/emitter-specific
diagnostic proliferation, and without any mutable static test-only
production state. Done when: `CMP0050` fires and no `CS8785` escapes for
a forced per-item failure in a focused test, every other item in the same
run still gets its generated source, the full existing test suite is
unchanged (green, no snapshot diffs) for the six real emitters' normal
success path, and `docs/architecture.md`'s diagnostics inventory (if one
exists) reflects `CMP0050`.

## Scope

Implements ADR-0068 exactly as accepted - see that ADR's Decision Outcome
for the full policy, the per-registration verification, the diagnostic
contract, and the rejected alternatives. This plan does not reopen any of
those decisions.

**In scope:**

- `CMP0050` / `GeneratedSourceEmissionFailed` diagnostic descriptor.
- `EmissionIsolation.TryEmit` helper.
- Wrapping all six accepted call sites in `ComponoIncrementalGenerator.cs`.
- Unit tests for `EmissionIsolation.TryEmit` in isolation.
- One generator-driver integration test via a test-project-only probe
  generator, wired to the real production helper.
- Full validation: targeted tests, full `Compono.Generators.Tests`, full
  solution build/test, package validation, relevant existing AOT smoke
  tests, dogfood validation.

**Explicitly out of scope** (ADR-0068 Non-Goals, unchanged):

- Issue #142 / Scriban `LoopLimit` (`20_000`) / `LimitToString`
  (`20_000_000`).
- Generated-comment duplication, multi-file generation.
- Any incremental-pipeline restructuring beyond wrapping the six existing
  call sites.
- Any public API, consumer configuration, or package dependency change.
- Any per-emitter diagnostic (`CMP0050` is the only new diagnostic).
- `loggingRuntimeSymbolsStatus`'s own registration (no per-item artifact
  to isolate, per ADR-0068).

## Tasks

### Diagnostics

- [x] Add `DiagnosticDescriptors.GeneratedSourceEmissionFailed` (`CMP0050`,
      category `Compono.Generators`, severity `Error`,
      `isEnabledByDefault: true`), title "Generated source emission failed
      unexpectedly", message:
      `"Compono could not emit generated {0} for '{1}' due to an unexpected internal error ({2}: {3}). Generated output for this item is unavailable."`
      (four args: artifact kind, item identity, exception type name,
      exception message - per ADR-0068's corrected message contract).

### Shared helper

- [x] Add `src/Compono.Generators/Emitters/EmissionIsolation.cs`:
      `internal static class EmissionIsolation` with
      `public static void TryEmit(SourceProductionContext context, string artifactKind, string itemIdentity, Action emit)`.
      Catches `Exception ex when (ex is not OperationCanceledException)`,
      reports `CMP0050` via `Diagnostic.Create(DiagnosticDescriptors.GeneratedSourceEmissionFailed, Location.None, artifactKind, itemIdentity, ex.GetType().Name, ex.Message)`,
      does not rethrow, does not call `AddSource` on failure. Success path
      is a transparent `emit()` call - no behavior change, no swallowed
      exception when `emit()` succeeds.

### Generator wiring

- [x] `ComponoIncrementalGenerator.cs`: wrap each of the six accepted call
      sites with `EmissionIsolation.TryEmit(...)`, one line each,
      preserving every existing diagnostic-reporting/early-return line
      immediately above the call unchanged:
      - `discoveredCollections` → `CollectionPlanEmitter.Generate` -
        `artifactKind: "collection plan"`, identity:
        `collection.FullyQualifiedCollectionTypeName`.
      - `discoveredTestDoubles` → `TestDoubleEmitter.Generate` -
        `artifactKind: "test double"`, identity:
        `testDouble.InterfaceFullyQualifiedName`.
      - `discoveredTypes` → `CompositionPlanEmitter.Generate` -
        `artifactKind: "composition plan"`, identity:
        `type.FullyQualifiedName`.
      - `rowInvokerTypes` → `RowInvokerRegistrationEmitter.Generate` -
        `artifactKind: "row-invoker registration"`, identity:
        `type.FullyQualifiedTypeName`.
      - `discoveredLoggingCategories` → `LoggingActivationEmitter.Generate` -
        `artifactKind: "logging activation"`, identity:
        `category.CategoryFullyQualifiedName`.
      - `aotComposeMethodsAll` → `AotTheoryDataRowRegistrationEmitter.Generate` -
        `artifactKind: "AOT theory-data-row registration"`, identity:
        `$"{method.FullyQualifiedTestClassName}.{method.MethodName}"`
        (`AotComposeMethodInfo` has no single combined identity property
        today - this composes its two existing fields rather than adding
        one).
- [x] Confirm `loggingRuntimeSymbolsStatus`'s registration is untouched.
- [x] Confirm no `WithTrackingName` stage, provider shape, or dedup/`Collect`/
      `SelectMany` logic changes anywhere in the file - the diff should be
      exactly the diagnostic descriptor file, the new helper file, and six
      one-line wraps.

### Tests - `EmissionIsolation.TryEmit` unit coverage

- [x] Success: `emit` runs, no diagnostic reported, return value/state
      confirms `emit` actually executed.
- [x] Non-cancellation exception (e.g. `InvalidOperationException`):
      `CMP0050` reported with the exact `artifactKind`/`itemIdentity`
      passed in and the thrown exception's type name/message; `TryEmit`
      itself does not rethrow.
- [x] `OperationCanceledException`: propagates out of `TryEmit` uncaught;
      no diagnostic reported.

### Tests - generator-driver integration coverage

- [x] New test-project-only probe `IIncrementalGenerator` (lives in
      `test/Compono.Generators.Tests/`, never part of the shipped
      `Compono.Generators` assembly) that registers two independent
      values through `context.RegisterSourceOutput`, wiring each through
      the real, production `EmissionIsolation.TryEmit` - one value's
      `emit` delegate throws unconditionally (a local lambda, not a
      static flag), the other's succeeds and calls `AddSource`.
- [x] Assert on `GeneratorDriverRunResult`: `CMP0050` present for the
      failing value, `CS8785` absent, `GeneratedTrees` contains the
      healthy value's source and nothing for the failing value.
- [x] Re-run the driver (or otherwise confirm determinism) and assert the
      same outcome both times.
- [x] No mutable static field anywhere in this test or in
      `EmissionIsolation` itself - the "failure" is a lambda local to the
      test.

### Docs

- [x] Update whatever doc currently indexes generator diagnostics (check
      `docs/architecture.md` and any diagnostics-reference doc for an
      existing `CMP00xx` list before assuming none exists) to include
      `CMP0050`, in the same PR, per `AGENTS.md`'s "update docs in the
      same PR" rule.
- [x] No change expected to `docs/public-api.md` (no public API surface
      added).

### Validation

- [x] Targeted helper unit tests pass.
- [x] Targeted generator-driver isolation test passes.
- [x] Full `Compono.Generators.Tests` passes on both target frameworks with
      zero snapshot diffs.
- [x] Full solution build and test pass, with no warning attributable to
      PLAN-0069.
- [x] Standard packed-package inspection passes for all publishable packages.
- [x] All four relevant packaged Native AOT smoke binaries publish and run;
      the existing `Compono.XunitV3.Aot.Tests` matrix and standalone
      `Compono.XunitV3.Aot.SampleTests` JIT/AOT gates also pass.
- [x] Compono dogfood validation passed; full consumer suite externally
      blocked by live Alexa endpoint failures. Fresh-package packing,
      exact-version resolution, consumer compilation/generation, and all
      non-external tests passed with no CMP0050, CS8785, or other Compono
      failure; see Notes.
- [x] Final working-tree hygiene checked.

## Critical Files

- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` - add
  `CMP0050`.
- `src/Compono.Generators/Emitters/EmissionIsolation.cs` - new file, the
  shared helper.
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` - six one-line
  wraps at the accepted call sites.
- `test/Compono.Generators.Tests/EmissionIsolationTests.cs` - new, unit
  coverage for the helper.
- `test/Compono.Generators.Tests/EmissionIsolationGeneratorDriverTests.cs`
  (or similarly named) - new, the probe-generator integration test.
- A generator-diagnostics-index doc, if one exists (confirm during
  implementation).

No changes expected to any `Discovery/*.cs` file, any `Models/*.cs`
record, any `.scriban` template, or any runtime (`Compono`,
`Compono.TestDoubles`, or integration package) project.

## Test Plan

Ordered per ADR-0068's validation expectations and this repo's normal
generator-change gate (`AGENTS.md`'s "Build and test" /
"Consumer/dogfood validation gate" sections):

1. **Targeted new tests** (above): `EmissionIsolationTests` (unit) and the
   generator-driver integration test, both green, proving the six
   properties ADR-0068's regression-test architecture calls for (success
   passthrough, non-cancellation → `CMP0050`, cancellation propagates, no
   `CS8785`, no source for the failed item, healthy sibling survives,
   deterministic).
2. **Full `Compono.Generators.Tests`**: every existing test green, with
   **zero snapshot diffs** - this is the proof that wrapping all six real
   emitters in `EmissionIsolation.TryEmit` is behavior-neutral for the
   success path (ADR-0068's "Property 7" reasoning: `TryEmit`'s
   non-throwing branch is a transparent call, so no existing generated
   output should change byte-for-byte).
3. **Full solution build/test**: `dotnet build`/`dotnet test` across the
   whole solution, zero errors, no warnings newly attributable to this
   change (pre-existing warnings are not this plan's concern, matching
   PLAN-0068's precedent for the same gate wording).
4. **Standard package validation**: `.github/scripts/inspect-packed-nupkgs.sh`
   against a fresh `dotnet pack` output for every publishable package
   whose generator-emitted output this plan touches
   (`Compono`/`Compono.Generators`/`Compono.TestDoubles`/`Compono.XunitV3`/
   `Compono.XunitV3.Aot`/`Compono.Logging` at minimum - confirm the exact
   set against which packages actually reference `Compono.Generators`
   during implementation) - proves the new `EmissionIsolation.cs` file
   and `CMP0050` descriptor don't change any package's shipped contents
   in an unexpected way (e.g. no new public type accidentally exposed).
5. **Relevant AOT/trimming validation via the repository's existing
   smoke-test projects** (not a new AOT consumer - `docs/adr/...`'s own
   convention, confirmed in-repo, is that `*.AotSmokeTest` projects are
   deliberately excluded from the normal solution build and run as their
   own one-shot `PublishAot` proof): run
   `Compono.AotSmokeTest`, `Compono.TestDoubles.AotSmokeTest`,
   `Compono.XunitV3.AotSmokeTest`, and `Compono.Logging.AotSmokeTest` -
   the four packages whose generator-emitted call sites this plan
   actually wraps (composition plans, test doubles, row-invoker
   registrations, logging activation) and that have an existing AOT
   smoke gate. `Compono.XunitV3.Aot`'s own AOT theory-data-row
   registration path (this plan's sixth wrapped site) is exercised by its
   existing `Compono.XunitV3.Aot.Tests` project in the normal solution and
   by the standalone `Compono.XunitV3.Aot.SampleTests` JIT/AOT gate (the
   sample project is not included in `Compono.slnx`, as confirmed during
   implementation).
6. **Dogfood validation** (`scripts/dogfood-validate.sh`, default
   consumer `trivia-platform`, default four-package set `Compono
   Compono.NSubstitute Compono.TestDoubles Compono.XunitV3` - this
   consumer's own composition/test-double/collection usage exercises
   five of the six wrapped call sites in one real build, more broadly
   than the narrower `lightsaber-skill` consumer PLAN-0068 used for the
   test-double-only #142 fix): packs the current working tree, restores
   `trivia-platform` against it, asserts exact-version resolution, runs
   its full test suite. This is a **success-path-only** proof, by design
   (per explicit instruction): it demonstrates that wrapping the six
   normal emission paths introduces no regression to real-consumer
   generation - it does not, and is not meant to, exercise `CMP0050`
   itself. `CMP0050`'s own behavior is proven exclusively by the focused
   unit/generator-driver tests in this plan, never by attempting to force
   a real emitter to fail inside a consumer's build. If unrelated live
   external-service tests prevent a fully green consumer run, this gate is
   successful when fresh-package resolution, compilation/generation, and
   every non-external test pass with no Compono-attributable failure, with
   the external limitation recorded exactly in Notes.
7. **Final working-tree hygiene**: `git status --short` clean of anything
   but the intended diff before commit; no temporary probe files, no
   stray local NuGet feed directories left tracked; `docs/adr/README.md`/
   `docs/plans/README.md` indices current.

## Notes

### Implementation takeover and corrections

Work resumed from an uncommitted partial implementation. The existing
working tree already contained the accepted descriptor, all six generator
wraps, the helper, analyzer-release inventory entry, ADR/Plan/research
records, and a partially-written helper test file. Those production changes
matched ADR-0068 except that the initial descriptor title omitted the ADR's
terminal period. Final cleanup investigated `RS1031` (`Define diagnostic title
correctly`): Microsoft.CodeAnalysis.Analyzers requires diagnostic titles to
contain no period, line return, or leading/trailing whitespace. The period had
no semantic purpose, so the descriptor and ADR/Plan title contract now follow
the standard convention without a suppression. The diagnostic message remains
unchanged.

The initial helper test file had an unused/non-working result helper,
referenced a generator-driver test class that did not yet exist, generated
xUnit analyzer warnings, and did not prove that cancellation produced no
CMP0050. It was narrowed to three focused tests. Cancellation is caught
*outside* `TryEmit` in the probe callback, proving the exact exception
propagates through the helper while still allowing inspection of the real
Roslyn run result for zero diagnostics. Added the separate two-value
probe-generator integration test required by ADR-0068. No mutable static
failure state exists in production or test code.

The helper itself was reduced to the accepted small, explicit implementation;
the partial version's extensive comments did not change behavior but obscured
the boundary. The six call sites and their identities are exactly those in
the accepted ADR, including
`$"{method.FullyQualifiedTestClassName}.{method.MethodName}"` for AOT theory
data rows. `loggingRuntimeSymbolsStatus`, provider shapes, tracking names,
collection/flattening operations, discovery models, and templates are
unchanged. Direct inspection reconfirmed each covered emitter has exactly one
`AddSource`, as its final operation after complete source construction.

`docs/reference/diagnostics.md` is the existing consumer diagnostic inventory
and now documents CMP0050. `AnalyzerReleases.Unshipped.md` also inventories
it. No public API documentation changed.

### Validation results (2026-09-15)

- Final RS1031 cleanup rerun: `Compono.Generators.csproj` built with 0 warnings
  and 0 errors; targeted helper tests passed 3/3 and the targeted
  generator-driver isolation test passed 1/1 on `net10.0`. No package/AOT/
  dogfood rerun was needed for this descriptor-title-only convention fix.
- Targeted helper tests: 3/3 passing (`net10.0`). Targeted generator-driver
  test: 1/1 passing (`net11.0`). The full project run subsequently exercises
  all four on both TFMs.
- `Compono.Generators.Tests`: 366/366 passing on `net10.0` and 366/366 on
  `net11.0`; no `.received` files and zero snapshot diffs.
- Full `dotnet build --no-restore`: succeeded, 0 errors. Its 51 `NU1902`
  warnings are the repository's pre-existing `Microsoft.Build.Tasks.Git`
  advisory repeated across TFMs; PLAN-0069's files produced no warnings.
- Full solution tests (`dotnet test --solution Compono.slnx --no-restore
  --no-build`): 4,007/4,007 passing.
- Fresh Release package inspection passed for all 13 publishable packages:
  `Compono`, `Compono.XunitV3`, `Compono.XunitV3.Aot`,
  `Compono.NSubstitute`, `Compono.Bogus`, `Compono.TUnit`,
  `Compono.TestDoubles`, `Compono.DependencyInjection`, `Compono.Http`,
  `Compono.Logging`, `Compono.MSTest`, `Compono.NUnit`, and `Compono.Options`.
  `Compono.Generators` remains correctly embedded in `Compono` rather than
  independently packed.
- Packaged Native AOT publish-and-run gates all printed `PASS`:
  `Compono.AotSmokeTest`, `Compono.TestDoubles.AotSmokeTest`,
  `Compono.XunitV3.AotSmokeTest`, and `Compono.Logging.AotSmokeTest` on
  `net10.0`/`osx-arm64`. The xUnit v3 smoke publish retained its known
  third-party `xunit.v3.core` `IL2104`; no PLAN-0069 AOT/trim warning appeared.
  The ordinary full-solution run also passed `Compono.XunitV3.Aot.Tests`
  6/6 on each of `net9.0`, `net10.0`, and `net11.0`. Contrary to the Test
  Plan's initial repository-structure assumption, `Compono.XunitV3.Aot.SampleTests`
  is not included in `Compono.slnx`; its freshly-packed standalone JIT run
  passed 3/3, and its `net10.0`/`osx-arm64` Native AOT publish and native-binary
  run also passed 3/3.
- Default `trivia-platform` dogfood was attempted twice. Both attempts packed
  and resolved the exact fresh four-package set (`Compono`,
  `Compono.NSubstitute`, `Compono.TestDoubles`, `Compono.XunitV3`). The first
  exact version was `99.0.0-local.20260915114543-40617-19282`; with Docker
  initially unavailable it failed 82/789 consumer tests. After starting
  Colima, a second run confirmed every requested package resolved to
  `99.0.0-local.20260915114704-41789-11917`; every non-acceptance module passed
  and the result improved to 753/789, but all 36 live Alexa acceptance tests
  failed because the remote invocation API returned empty response bodies
  (and occasional HTTP 429 throttling). No `CMP0050`, `CS8785`, compilation,
  package-resolution, or Compono-related failure appeared. The script restored
  the consumer working tree to its original clean state. **Compono dogfood
  validation passed; full consumer suite externally blocked by live Alexa
  endpoint failures.** This records success for PLAN-0069's Compono validation
  objective without claiming that all 789 consumer tests passed.
