# [PLAN-0043] Compono-Generated Test Doubles

**Status:** Not Started

**Implements:** ADR-0043 (design), ADR-0042 (admitted problem)

## Goal

`ComponoGeneratedTestDoubles=true` plus `builder.UseGeneratedTestDoubles()`
lets `composer.Create<T>()` automatically satisfy an otherwise-unresolvable
interface dependency with a generated, AOT-safe double — configurable via
`interfaceValue.Configure().Member(...).Returns(...)`/`.Throws(...)` (a
generator-emitted extension per discovered interface, per
[ADR-0043 Amendment 1](../adr/0043-compono-generated-test-doubles-design.md#amendment-1-2026-08-13-configure-must-be-generator-emitted-per-interface-not-a-runtime-generic-method)) —
with zero behavior change for any consumer who doesn't opt into both
gates, and a real `dotnet publish -p:PublishAot=true` execution test
proving the whole path is Native-AOT-safe.

## Scope

Builds exactly what [ADR-0043](../adr/0043-compono-generated-test-doubles-design.md)
decided — the generated code shape, the compile-time opt-in on
`LeafTypeClassifier`, `GeneratedTestDoubleProvider`, and the new
`Compono.TestDoubles` package. Explicitly deferred, per ADR-0042's
Non-Goals (unchanged by ADR-0043): verification, call recording, strict
mode, argument matchers beyond a minimal closed shape, callbacks,
sequential returns, class/protected-member/static-abstract-member support,
indexers/events/generic methods/`ref`/`out`/`in` parameters. Standalone
(non-Compono) usability is included only if it falls out at the cost
ADR-0043's "Standalone usability" section already found (near zero) — not
worth its own phase if it turns out to need more than that.

## Phases

### Phase 0 — Generator foundation

- [ ] Extend `LeafTypeClassifier` with the compile-time-gated third
      classification outcome (ADR-0043's "Generator architecture").
- [ ] Read `ComponoGeneratedTestDoubles` via `AnalyzerConfigOptionsProvider`;
      confirm zero generated-output diff when unset/`false` (a compile-diff
      regression test, not just a manual check).
- [ ] Emit the generated double type (explicit interface implementation),
      its per-member configuration extension methods, and its generated
      `Configure(this IRepository ...) => RepositoryDouble` bridge extension
      (`Compono.TestDoubles.Generated` namespace — ADR-0043 Amendment 1;
      this bridge cannot be a runtime-package generic method, it must be
      generated alongside the double type it downcasts to), deduplicated
      per distinct interface symbol across the compilation.
- [ ] Deterministic-default logic per ADR-0043's "Deterministic defaults"
      (primitives, nullable refs, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`,
      empty collections never `null`).
- [ ] Compile-time diagnostics for unsupported member shapes (indexers,
      events, generic methods, `ref`/`out`/`in`, static abstract members) —
      leaf still defers to the unchanged runtime-provider path.

### Phase 1 — Runtime package (`Compono.TestDoubles`)

- [ ] New `src/Compono.TestDoubles` project — `ReturnConfigBuilder<T>`,
      `GeneratedTestDoubleProvider : ICompositionValueProvider`,
      `UseGeneratedTestDoubles()` builder extension
      ([ADR-0024](../adr/0024-public-provider-extensibility-model.md)'s
      `AddTestDoubleProvider`, `NSubstituteProvider`-sized). **No**
      `Configure<T>(...)` method here — that bridge is generator-emitted
      per interface (Phase 0), not a runtime package member (ADR-0043
      Amendment 1).
- [ ] Package-level global `using Compono.TestDoubles.Generated;` shipped
      via `Compono.TestDoubles`'s own `.props`/`GlobalUsings` (matching
      TUnit.Mocks' own `TUnit.Mocks.Generated` global-using convention),
      so a consumer never hand-writes that `using`.
- [ ] Precedence documentation: `UseGeneratedTestDoubles()` before
      `UseNSubstitute()` when both are installed (ADR-0043's "Runtime
      activation and precedence") — a real sample/test proving registration
      order produces the documented result, not just prose.

### Phase 2 — End-to-end verification

- [ ] A real packaged-consumer sample (matching `Compono.XunitV3.SampleTests`/
      `Compono.TUnit.SampleTests`' existing pattern) exercising
      `composer.Create<T>()` with a generated double satisfying an
      interface dependency, `[Shared] IRepository` reuse into the SUT, and
      `repository.Configure().Member(...).Returns(...)`/`.Throws(...)`.
  - [ ] `dotnet publish -p:PublishAot=true` + real execution against that
        sample — the "prove it, don't assume it" standard `Compono.TUnit`
        (PLAN-0040) already set for this repo, applied here.
- [ ] Public-API-surface approval test for `Compono.TestDoubles`, matching
      `Compono.TUnit.Tests.PublicApiSurfaceTests`' pattern.

### Phase 3 — Docs and skill alignment

- [ ] `docs/packages/compono-testdoubles.md` (new Package Guide).
- [ ] `docs/packages/index.md` row.
- [ ] `skills/compono/SKILL.md` detection table + `references/testdoubles.md`
      (new reference file, following `references/nsubstitute.md`'s shape).
- [ ] `docs/roadmap/future-packages.md` — move this entry to shipped once
      the package exists, matching `Compono.TUnit`'s own graduation edit.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` status flips to `Done`.

## Critical Files

- `src/Compono.Generators/LeafTypeClassifier.cs` — the compile-time-gated
  third classification outcome.
- `src/Compono.Generators/` — new generated-code-emission logic for the
  double type, its per-member configuration extensions, and the
  generator-emitted `Configure(this IRepository ...)` bridge per interface
  (ADR-0043 Amendment 1 — `Configure` is generated per interface, not part
  of the runtime package).
- `src/Compono.TestDoubles/` — new project (`ReturnConfigBuilder<T>`,
  `GeneratedTestDoubleProvider`, `UseGeneratedTestDoubles()`, and the
  package-level global `using Compono.TestDoubles.Generated;` — no
  `Configure(...)` method here).
- `test/Compono.Generators.Tests/` — generator-output `Verify()` tests,
  including the "gate off → zero diff" regression test.
- `test/Compono.TestDoubles.Tests/`, `test/Compono.TestDoubles.SampleTests/` —
  new test projects.

## Test Plan

Matches `references/testing.md`'s existing pattern: `Verify()`-based
generator-output snapshot tests (including the opt-in-off no-op case),
unit tests for `GeneratedTestDoubleProvider`/`ReturnConfigBuilder<T>`
in isolation, a real packaged sample exercising the full
`composer.Create<T>()` path, and a real `PublishAot=true` execution test —
not a claim, a run, per this repo's established AOT-verification standard.

## Notes

Not started. ADR-0043 is `Accepted`; this plan stays `Not Started` until
implementation is explicitly requested.

Pre-implementation review caught a compile-validity defect in ADR-0043's
original `Configure<T>(...)` sketch (a runtime-package generic method
can't return a per-consumer generated type it never saw) — corrected via
[ADR-0043 Amendment 1](../adr/0043-compono-generated-test-doubles-design.md#amendment-1-2026-08-13-configure-must-be-generator-emitted-per-interface-not-a-runtime-generic-method)
before any code was written; this plan's task list above already reflects
the corrected shape.
