# [PLAN-0043] Compono-Generated Test Doubles

**Status:** Not Started

**Implements:** ADR-0043 (design), ADR-0042 (admitted problem)

## Goal

`ComponoGeneratedTestDoubles=true` plus `builder.UseGeneratedTestDoubles()`
lets `composer.Create<T>()` automatically satisfy an otherwise-unresolvable
interface dependency with a generated, AOT-safe double — configurable via
`interfaceValue.Configure().Member().Returns(...)`/`.Throws(...)` (a
generator-emitted extension per discovered interface, per
[ADR-0043 Amendment 1](../adr/0043-compono-generated-test-doubles-design.md#amendment-1-2026-08-13-configure-must-be-generator-emitted-per-interface-not-a-runtime-generic-method)) —
with zero behavior change for any consumer who doesn't opt into both
gates, and a real `dotnet publish -p:PublishAot=true` execution test
proving the whole path is Native-AOT-safe.

## Scope

Builds exactly what [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md)
(as corrected by
[Amendment 2](../adr/0043-compono-generated-test-doubles-design.md#amendment-2-2026-08-13-cross-assembly-bridge-generated-type-collision-safety-coreoptional-package-boundary-corrected-argument-matching-sample-struck))
decided — the generated code shape, the compile-time opt-in on
`LeafTypeClassifier`, the core registry/builder primitives,
`GeneratedTestDoubleProvider`, and the new `Compono.TestDoubles` package.
Explicitly deferred, per ADR-0042's Non-Goals (unchanged by ADR-0043):
verification, call recording, strict mode, argument matchers (struck
entirely by Amendment 2 — configuration is member-level and
argument-independent, not "a minimal closed shape" as the pre-Amendment
text once said), callbacks, sequential returns,
class/protected-member/static-abstract-member support,
indexers/events/generic methods/`ref`/`out`/`in` parameters. Standalone
(non-Compono) usability is included only if it falls out at the cost
ADR-0043's "Standalone usability" section already found (near zero) — not
worth its own phase if it turns out to need more than that.

## Phases

### Phase 0 — Core primitives and generator foundation

- [ ] **Core `Compono`** (not `Compono.TestDoubles` — Amendment 2 moved
      these to fix a cross-assembly reference the original design got
      backwards): `ReturnConfig<T>` (`internal` backing fields, `public`
      readonly accessors — `HasConfiguredValue`/`HasConfiguredException`/
      `ConfiguredValue`/`ConfiguredException` — for cross-assembly generated
      dispatch code to read; Amendment 3 Finding A), `ReturnConfigBuilder<T>`
      (a `public readonly ref struct` holding a `ref ReturnConfig<T>`, public
      constructor — Amendment 3 Finding A — whose `Returns` sets **both**
      `Value` and `HasValue`, Amendment 3 Finding B), and
      `GeneratedTestDoubleRegistry` (`RegisterFactory<T>(Func<T> factory)`/
      `TryCreate(Type requestedType, out object? value)`, `Type`-keyed,
      first-registration-wins — Amendment 3 Finding C documents this as a
      known v1 limitation for multi-assembly same-interface scenarios, not
      something this phase needs to solve) — always present in core, inert
      unless a factory is ever registered.
- [ ] Extend `LeafTypeClassifier` with the compile-time-gated third
      classification outcome (ADR-0043's "Generator architecture").
- [ ] Read `ComponoGeneratedTestDoubles` via `AnalyzerConfigOptionsProvider`;
      confirm zero generated-output diff when unset/`false` (a compile-diff
      regression test, not just a manual check).
- [ ] Emit, per discovered interface, **one single generated file**
      containing (Amendment 2's verified-by-spike shape — do not
      file-scope any of the first three; `CS9051` blocks a file-local type
      from appearing in any non-file-local member's signature, even
      co-located):
      - `internal sealed class <Hash>_Double : IRepository` — explicit
        interface implementation, one `ReturnConfig<T>` field per member.
      - `internal static class <Hash>_DoubleConfiguration` — per-member
        configuration extensions (`FindAsync()`/`Save()`, no parameters —
        argument-independent per Amendment 2 Finding 4).
      - `internal static class <Hash>_ConfigureExtension` — the
        `Configure(this IRepository)` bridge (Amendment 1, corrected
        target type by Amendment 2), whose cast-failure exception message
        names the multi-assembly-collision scenario explicitly (Amendment 3
        Finding C).
      - `file static class <Hash>_DoubleRegistration` — `[ModuleInitializer]`
        registering the double's factory into `GeneratedTestDoubleRegistry`
        (this one *can* stay `file`-scoped — never called by name).
      - `<Hash>` reuses `GeneratedFileNaming.HintNameFor`'s sanitized-name +
        FNV-1a-hash scheme (`src/Compono.Generators/Emitters/GeneratedFileNaming.cs`),
        applied to type names here too, not just `AddSource` hint names —
        the collision-safety mechanism this feature relies on instead of
        `file`-scoping.
      - Deduplicated per distinct interface symbol across the compilation
        (same `.Collect()` + `SymbolEqualityComparer` pattern used
        elsewhere in the generator).
- [ ] Deterministic-default logic per ADR-0043's "Deterministic defaults"
      (primitives, nullable refs, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`,
      empty collections never `null`).
- [ ] Compile-time diagnostics for unsupported member shapes (indexers,
      events, generic methods, `ref`/`out`/`in`, static abstract members,
      **overloaded members** — Amendment 3 Finding D: a zero-argument
      configuration extension can't disambiguate `Get(int)` from
      `Get(string)`, diagnose and reject rather than emit a duplicate-
      signature compile error) — leaf still defers to the unchanged
      runtime-provider path.
- [ ] Compile-time diagnostic for an interface that declares its own
      member named `Configure` with a colliding signature (Amendment 3
      Finding E — an instance member always wins over the generated
      extension in overload resolution, silently making the bridge
      unreachable) — leaf still defers to the unchanged runtime-provider
      path.

### Phase 1 — Runtime package (`Compono.TestDoubles`)

- [ ] New `src/Compono.TestDoubles` project — **only**
      `GeneratedTestDoubleProvider : ICompositionValueProvider` (reads the
      core `GeneratedTestDoubleRegistry`) and `UseGeneratedTestDoubles()`
      builder extension ([ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
      `AddTestDoubleProvider`, `NSubstituteProvider`-sized). No
      `ReturnConfigBuilder<T>`, no registry, no `Configure(...)` here —
      all three live in core `Compono` or are generator-emitted per
      interface (Amendment 2).
- [ ] Precedence documentation: `UseGeneratedTestDoubles()` before
      `UseNSubstitute()` when both are installed (ADR-0043's "Runtime
      activation and precedence") — a real sample/test proving registration
      order produces the documented result, not just prose.

### Phase 2 — End-to-end verification

- [ ] A real packaged-consumer sample (matching `Compono.XunitV3.SampleTests`/
      `Compono.TUnit.SampleTests`' existing pattern) exercising
      `composer.Create<T>()` with a generated double satisfying an
      interface dependency, `[Shared] IRepository` reuse into the SUT, and
      `repository.Configure().Member().Returns(...)`/`.Throws(...)` called
      from the *test* file — the real cross-file case Amendment 2's spike
      verified in isolation, now proven against the actual generator.
  - [ ] `dotnet publish -p:PublishAot=true` + real execution against that
        sample — the "prove it, don't assume it" standard `Compono.TUnit`
        (PLAN-0040) already set for this repo, applied here.
- [ ] Public-API-surface approval test for `Compono.TestDoubles` (now a
      much smaller surface post-Amendment-2: just the provider type and
      `UseGeneratedTestDoubles()`), matching
      `Compono.TUnit.Tests.PublicApiSurfaceTests`' pattern. Core `Compono`'s
      own public-API-surface test (if one exists) picks up
      `ReturnConfig<T>`/`ReturnConfigBuilder<T>`/`GeneratedTestDoubleRegistry`.

### Phase 3 — Docs and skill alignment

- [ ] `docs/packages/compono-testdoubles.md` (new Package Guide).
- [ ] `docs/packages/index.md` row.
- [ ] `skills/compono/SKILL.md` detection table + `references/testdoubles.md`
      (new reference file, following `references/nsubstitute.md`'s shape).
- [ ] `docs/roadmap/future-packages.md` — move this entry to shipped once
      the package exists, matching `Compono.TUnit`'s own graduation edit.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` status flips to `Done`.

## Critical Files

- `src/Compono/` — new core primitives: `ReturnConfig<T>`,
  `ReturnConfigBuilder<T>`, `GeneratedTestDoubleRegistry` (ADR-0043
  Amendment 2 — moved here from the originally-planned `Compono.TestDoubles`
  to fix a cross-assembly reference the generator couldn't otherwise make).
- `src/Compono.Generators/Discovery/LeafTypeClassifier.cs` — the
  compile-time-gated third classification outcome.
- `src/Compono.Generators/Emitters/GeneratedFileNaming.cs` — reused (not
  modified) for the new hash-suffixed collision-safe type names.
- `src/Compono.Generators/` — new generated-code-emission logic: one file
  per discovered interface containing the double, its configuration
  extensions, its `Configure(...)` bridge, and its module-initializer
  registration (ADR-0043 Amendments 1 and 2).
- `src/Compono.TestDoubles/` — new project, deliberately small:
  `GeneratedTestDoubleProvider`, `UseGeneratedTestDoubles()`.
- `test/Compono.Generators.Tests/` — generator-output `Verify()` tests,
  including the "gate off → zero diff" regression test, and a real
  cross-file compile test (generated code in one file, a hand-written
  consumer file calling `Configure()` in another) proving Amendment 2's
  verified shape actually works end-to-end through the real generator, not
  just the standalone spike.
- `test/Compono.TestDoubles.Tests/`, `test/Compono.TestDoubles.SampleTests/` —
  new test projects.

## Test Plan

Matches `references/testing.md`'s existing pattern: `Verify()`-based
generator-output snapshot tests (including the opt-in-off no-op case),
unit tests for `GeneratedTestDoubleProvider`/`ReturnConfigBuilder<T>`/
`GeneratedTestDoubleRegistry` in isolation, a real packaged sample
exercising the full `composer.Create<T>()` path (including cross-file
`Configure()` usage), and a real `PublishAot=true` execution test — not a
claim, a run, per this repo's established AOT-verification standard.

## Notes

Not started. ADR-0043 is `Accepted`; this plan stays `Not Started` until
implementation is explicitly requested.

Pre-implementation review (Codex, PR #82) caught three P1 defects and one
P2 defect in ADR-0043's original design, all corrected via
[ADR-0043 Amendment 2](../adr/0043-compono-generated-test-doubles-design.md#amendment-2-2026-08-13-cross-assembly-bridge-generated-type-collision-safety-coreoptional-package-boundary-corrected-argument-matching-sample-struck)
before any implementation code was written:

1. The runtime provider couldn't reach a lookup generated into the
   consumer's own compilation (same class of cross-assembly defect
   Amendment 1 already fixed once, this time in the opposite direction) —
   fixed by moving the registry into core `Compono`, populated via
   `[ModuleInitializer]`, the same pattern this repo's own TUnit.Mocks
   investigation already proved.
2. The core generator would have needed to hardcode an optional package's
   type shape (`Compono.TestDoubles.ReturnConfigBuilder<T>`) — fixed by
   moving `ReturnConfigBuilder<T>` into core alongside the registry.
3. The original file-scoped-types fix was drafted, then **experimentally
   disproven twice** before landing on the correct shape (`internal` +
   hash-suffixed collision-safe names, reusing `GeneratedFileNaming`) — see
   Amendment 2's own account of both failed attempts (`CS0246`, then
   `CS9051`) so neither gets rediscovered during implementation.
4. An `Arg.Any<Guid>()` sample contradicted the requester's own already-
   decided v1 scope (no argument matchers) — struck; configuration is
   member-level and argument-independent.

A second review pass on Amendment 2's own corrected sketches caught four
more P1s and one P2, corrected via
[ADR-0043 Amendment 3](../adr/0043-compono-generated-test-doubles-design.md#amendment-3-2026-08-13-public-cross-assembly-state-contract-overloadname-collision-diagnostics-documented-multi-assembly-registry-limitation),
still before any implementation code was written:

1. `ReturnConfig<T>`'s fields and `ReturnConfigBuilder<T>`'s constructor
   were `internal`, unreachable from the consumer assembly the generated
   code actually lives in (`CS0122`) — fixed with public readonly
   accessors for reads, a public constructor, mutable state still confined
   to core.
2. `Returns` never set `HasValue`, so every configured return would have
   silently fallen through to the default — fixed.
3. A registry keyed only by `System.Type` breaks if two consumer
   assemblies both generate a double for the same shared interface —
   confirmed with the requester as a documented v1 limitation
   (first-registration-wins, a named diagnostic message on cast failure),
   not a core-engine redesign.
4. The zero-argument configuration-extension shape can't disambiguate
   overloaded interface members — fixed by diagnosing and rejecting
   overloaded members, matching the existing unsupported-shape pattern.
5. An interface declaring its own `Configure` member silently shadows the
   generated bridge (instance members always win over extensions) — fixed
   by diagnosing the collision.

This plan's task list above already reflects the fully-corrected shape.
