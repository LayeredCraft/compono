# [PLAN-0066] Compono.XunitV3.Aot Package Architecture

**Status:** Done (Phase 1)

**Implements:** [ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md)

## Goal

A consumer can reference a new `Compono.XunitV3.Aot` package, write ordinary
`[Theory]` + `[Compose]` tests (plain form only — see Scope), and publish
that test project as Native AOT, with the resulting native binary
discovering and correctly running those tests. `Compono.XunitV3` and core
`Compono` are unmodified. Done when: `Compono.XunitV3.Aot` ships, a real
(not hand-written-stand-in) generator-emitted registration drives a real
`[Compose]` test through a real `dotnet publish -p:PublishAot=true` +
native-binary-execution proof, and that proof is wired into
`.github/workflows/aot-validation.yaml` as a permanent, CI-blocking leg.

## Scope

**In scope (Phase 1):** a new `Compono.XunitV3.Aot` package with its own
`ComposeAttribute` (marker-only, no inline values, no `[Shared]`, no
profile variants); `Compono.Generators` extended to detect
`Compono.XunitV3.Aot.ComposeAttribute` usages and emit
`RegisteredEngineConfig.RegisterTheoryDataRowFactory`-based registrations
for them; a permanent CI proof.

**Explicitly deferred to a later phase:** inline values
(`[Compose(42, "x")]`), `[Shared]` parameters, `[Compose<TProfile>]`,
`[Compose<TProfile, TConfig>]`. See ADR-0066's Decision Outcome for why
Phase 1 is scoped this way — the core generator-emission mechanism is
proven (RESEARCH-0032 §9), but reproducing `BindingPlan`'s inline-value
validation/`[Shared]` ordering and `ConfigProfileBinder`'s profile
construction as compile-time-generated code is real, separate work.

**Not in scope, ever, per this plan:** any change to `Compono.XunitV3` or
core `Compono` — both stay exactly as they are today (ADR-0066's
backward-compatibility decision).

## Phase 1: Core mechanism (plain `[Compose]` only)

**Status:** Done

### Tasks

- [x] Create `src/Compono.XunitV3.Aot/Compono.XunitV3.Aot.csproj` —
      `net9.0`/`net10.0`/`net11.0` (xUnit's own AOT floor), depends on
      `Compono` and `xunit.v3.extensibility.core.aot` (new
      `PackageVersion` entry in `Directory.Packages.props`).
- [x] `src/Compono.XunitV3.Aot/ComposeAttribute.cs` — thin marker
      `Compono.XunitV3.Aot.ComposeAttribute : Xunit.v3.DataAttribute`
      (note: `DataAttribute` lives in `Xunit.v3`, not `Xunit.Sdk`, in the
      `.aot` package family - confirmed by loading the real assembly and
      enumerating its exported types during implementation, correcting
      RESEARCH-0032 §9's own spike, which happened to import both
      namespaces and never isolated which one actually resolved it), no
      `GetData` override.
- [x] Extend `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`
      with `AotAttributeMetadataName` (delegates to
      `AotComposeMethodDiscovery.AttributeMetadataName` - one string, two
      discovery registrations, see next task), wired into the existing
      `composeMethodResultsAll` pipeline for ordinary `PlanCache<T>`/
      `RowInvokerRegistry` emission.
- [x] New, separate discovery (`Discovery/AotComposeMethodDiscovery.cs`) +
      model (`Models/AotComposeMethodInfo.cs`) + emitter
      (`Emitters/AotTheoryDataRowRegistrationEmitter.cs` +
      `Templates/AotTheoryDataRowRegistration.scriban`) producing, per
      `[Compose]`-attributed method, a `[ModuleInitializer]`-decorated
      registration calling
      `global::Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)`
      with compile-time-closed-generic `row.Resolve<T>(descriptor)` calls.
      Also emits **`CMP0040`** for a generic test method or ref/out/in/
      params parameter - no runtime fallback exists for this attribute
      family, so this has to be a compile-time diagnostic, unlike the
      other four `[Compose]` integrations' silent-skip-plus-runtime-throw
      pattern.
- [x] Confirmed: this needs its own discovery
      (`AotComposeMethodDiscovery`), not a share of
      `ComposeMethodDiscovery.TransformMethod` - that function's own
      result shape is a compilation-wide, type-deduped worklist that
      deliberately discards per-method identity and parameter order,
      exactly the two things this registration needs to preserve. Both
      discoveries run side by side off the same attribute metadata name.
- [x] `test/Compono.XunitV3.Aot.Tests` - `ProjectReference`-based real
      generated-code execution (3 tests, all passing, net10.0 and
      net11.0 both verified locally); `test/Compono.Generators.Tests`
      also gained dedicated snapshot coverage
      (`AotTheoryDataRowRegistrationVerifyTests`, a
      `CompositionPlanVerifyTests` case, an `IncrementalCachingTests`
      case) - 325/325 passing.
- [x] `test/Compono.XunitV3.Aot.SampleTests` - packaged-dependency-chain
      sample test (1 test, passing via `dotnet test`/JIT).
- [x] This same project doubles as the permanent real-pipeline AOT proof
      (Tier 3) rather than a separate project - see its own `.csproj`
      comment for why. Published as Native AOT (`-p:RuntimeIdentifier=
      osx-arm64 -p:SelfContained=true -p:PublishAot=true -p:UseAppHost=true`
      - **not** the `-r <rid> --self-contained` CLI shorthand, which
      reliably fails this project with `MSB3030` for reasons not fully
      isolated during implementation, see the csproj comment), native
      binary executed directly: xUnit v3's real AOT pipeline discovered
      and ran the real `[Theory]`+`[Compose]` test, real Compono
      composition happened, test passed, exit 0, zero IL2xxx/IL3xxx
      warnings.
- [x] Wired into `.github/workflows/aot-validation.yaml` as a new,
      dedicated `aot-xunitv3-aot-pipeline` job (not folded into the
      `aot-smoke` matrix, since it needs a materially different publish
      invocation and drives a real xUnit AOT pipeline, not a hand-dispatch
      console app) - `aot-gate` now depends on it too, with the same
      fail-closed applicability logic as the existing nine legs.

### Docs/skill tasks

- [x] New `docs/packages/compono-xunitv3-aot.md` Package Guide.
- [x] `docs/packages/compono-xunitv3.md` — cross-reference added.
- [x] `docs/roadmap/future-packages.md` — graduation recorded.
- [x] `compono` agent skill (`~/.agents/skills/compono/`) — new
      `references/xunitv3-aot.md`, detection-table row, `CMP0040`
      mentioned in the skill's own frontmatter description, "SCOPES TO"
      list, and attribute-detection row updated.
- [x] `evals.json` — 2 new eval cases (ids 56/57) authored and the
      required baseline-vs-after comparison run, per the skill-creator
      convention (reconstructed pre-change skill snapshot vs. current
      skill, both fed the same prompts via independent subagents). Result:
      the pre-change skill correctly declined to answer both new
      `Compono.XunitV3.Aot`-specific cases rather than inventing an API/
      diagnostic it doesn't document (its own "never claim/invent an
      unshipped package's API" guardrail correctly applied to a package it
      genuinely doesn't know about) - the expected, safe "before" behavior,
      not a failure to fix. The post-change skill answered both correctly
      and completely (right namespace, right Phase 1 limitations, right
      CMP0040 cause/fix). A third, pre-existing case (id 1, ordinary
      `Compono.XunitV3` usage) was also run against both versions as a
      false-positive check - neither version recommends
      `Compono.XunitV3.Aot` for an ordinary reflection-mode project,
      confirming no over-triggering regression. No skill content needed
      correction as a result.

## Critical Files

- `src/Compono.XunitV3.Aot/` — new package (`ComposeAttribute.cs`,
  `.csproj`).
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs` — new
  attribute-metadata-name constant.
- `src/Compono.Generators/` — new emission logic (exact file TBD).
- `Directory.Packages.props` — new `xunit.v3.extensibility.core.aot`
  (and any other `.aot`-suffixed transitive dependency) version pin.
- `.github/workflows/aot-validation.yaml` — new tenth leg.
- `test/Compono.XunitV3.Aot.Tests`, `test/Compono.XunitV3.Aot.SampleTests`,
  and the new real-AOT-pipeline proof project — new test/proof projects.

## Test Plan

Three-tier pattern per `references/testing.md`, matching every other
`Compono.Generators`-touching change in this repo:

1. Unit/snapshot tests of the new emission logic
   (`test/Compono.Generators.Tests`).
2. Real generated-code execution against the packaged dependency chain
   (`test/Compono.XunitV3.Aot.SampleTests`, `ProjectReference`-based
   inner-loop coverage).
3. Real `dotnet publish -p:PublishAot=true` + native-binary-execution
   proof, through the *real* generator-emitted registration (not a
   hand-written stand-in) — the new permanent proof project, both run
   locally during implementation and wired into CI as this plan's own
   task above.

## Notes

RESEARCH-0032 §9's proof spike (scratchpad, discarded) already validated
the core mechanism end to end using a hand-written stand-in for the
generator's emission. This plan's Phase 1 work made `Compono.Generators`
actually produce that same shape of code for real, then re-ran the
identical publish-and-run proof against the real generated output - now
done and green.

Two real corrections surfaced during implementation that the research
phase's own spike didn't catch (both now fixed and reflected in the
package/generator source and this plan's own task descriptions above):

- **`DataAttribute`/`ITheoryDataRow`/`TheoryDataRow` namespaces.**
  RESEARCH-0032's spike imported both `Xunit.Sdk` and `Xunit.v3` and never
  isolated which one actually resolved these types. Confirmed by directly
  loading `xunit.v3.core.aot.dll`/`xunit.v3.common.aot.dll` and enumerating
  exported types: `DataAttribute` lives in `Xunit.v3`; `ITheoryDataRow`/
  `TheoryDataRow` live in the plain `Xunit` namespace (not `Xunit.Sdk`, not
  `Xunit.v3`) - different from the reflection-mode package's own
  `Xunit.Sdk.DataAttribute` shape. Both `ComposeAttribute.cs` and the
  generated-code template use the corrected namespaces.
- **AOT publish invocation.** `dotnet publish ... -r <rid> --self-contained
  true ...` (the CLI shorthand form, used successfully throughout
  RESEARCH-0031/0032's own scratchpad spikes and by every existing
  `*.AotSmokeTest`/`*.SampleTests` project in this repo) reliably fails
  `Compono.XunitV3.Aot.SampleTests` specifically with `MSB3030: Could not
  copy ... obj/Release/net10.0/<rid>/apphost ... because it was not
  found`, reproducible from a clean `obj`/`bin`. Passing the same
  information as `-p:RuntimeIdentifier=<rid> -p:SelfContained=true
  -p:UseAppHost=true` MSBuild properties instead works cleanly. Root cause
  not fully isolated (candidates considered: interaction with this
  project's isolated `RestorePackagesPath`/`PackToLocalFeed` pre-restore
  target, or with this repo's root `Directory.Build.props`'s unconditional
  `<OutputPath></OutputPath>` reset) - recorded on the `.csproj` itself and
  in the new package guide/skill reference so this doesn't need
  rediscovering. CI's new `aot-xunitv3-aot-pipeline` job uses the working
  form.

The `evals.json` baseline-vs-after comparison for the `compono` skill
change has since been run (see the Docs/skill tasks checklist above) -
no outstanding items remain for Phase 1.
