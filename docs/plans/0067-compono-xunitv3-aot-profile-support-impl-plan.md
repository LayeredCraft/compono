# [PLAN-0067] Compono.XunitV3.Aot Profile Support (`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`)

**Status:** Done

**Implements:** [ADR-0067](../adr/0067-compono-xunitv3-aot-profile-support.md)

## Goal

A consumer referencing `Compono.XunitV3.Aot` can write `[Compose<TProfile>]` and
`[Compose<TProfile, TConfig>]` theories - identical syntax to `Compono.XunitV3` - and publish the test
project as Native AOT, with the resulting native binary correctly applying the profile (and, for the
two-type-parameter form, the bound `TConfig`) before composing every test parameter. Done when: both
attribute forms ship in `Compono.XunitV3.Aot`, `Compono.Generators` emits real, compile-time-verified,
reflection-free registrations for them, a real `dotnet publish -p:PublishAot=true` + native-binary-
execution proof exists for both forms (including a negative case proving `CMP0041`/`CMP0042`/`CMP0043`
fire correctly), and that proof is wired into `.github/workflows/aot-validation.yaml`.

## Scope

**In scope:** `Compono.XunitV3.Aot.ComposeAttribute<TProfile>` (marker, `where TProfile :
ICompositionProfile, new()`); `Compono.XunitV3.Aot.ComposeAttribute<TProfile, TConfig>` (marker, `where
TProfile : ICompositionProfile`); `Compono.Generators` extended with two new metadata-name registrations
and compile-time `TConfig`/`TProfile` constructor-shape + argument validation; three new diagnostics
(`CMP0041`/`CMP0042`/`CMP0043`); generated-code changes to construct a profile (and, for the two-type-
parameter form, a `TConfig`) directly, with no reflection; permanent CI proof for both forms, including
negative/diagnostic cases.

**Explicitly out of scope:** inline values and `[Shared]` for the plain `[Compose]` form (still deferred
per PLAN-0066 - unrelated to profile support, not touched here); any change to `Compono.XunitV3` or core
`Compono` (per ADR-0067's backward-compatibility driver, unchanged); converting `alexa-vox-craft`'s
Native AOT validator (a post-implementation dogfooding step - see Dogfooding section below, not a task
of this plan's own Phase).

## Phase 1: Profile-based composition (`[Compose<TProfile>]` and `[Compose<TProfile, TConfig>]`)

**Status:** Done

### Tasks

- [x] `src/Compono.XunitV3.Aot/ComposeAttribute{TProfile}.cs` - `Compono.XunitV3.Aot.ComposeAttribute<TProfile>
      : Compono.XunitV3.Aot.ComposeAttribute where TProfile : ICompositionProfile, new()`, marker-only
      (no `GetData` override, no constructor logic - mirrors Phase 1's `ComposeAttribute.cs` exactly).
- [x] `src/Compono.XunitV3.Aot/ComposeAttribute{TProfile,TConfig}.cs` - `Compono.XunitV3.Aot.ComposeAttribute<TProfile,
      TConfig> : Compono.XunitV3.Aot.ComposeAttribute where TProfile : ICompositionProfile`, marker-only,
      constructor accepting `params object?[] configArguments` (so the C# compiler treats the supplied
      values as this attribute's own compile-time-constant constructor arguments, readable later via
      `AttributeData.ConstructorArguments` - the attribute itself does nothing with them at runtime).
- [x] `src/Compono.Generators/Discovery/AotComposeMethodDiscovery.cs` - add
      `GenericAttributeMetadataName = "Compono.XunitV3.Aot.ComposeAttribute\`1"` and
      `TwoTypeParameterAttributeMetadataName = "Compono.XunitV3.Aot.ComposeAttribute\`2"` constants,
      mirroring `ComposeMethodDiscovery`'s existing three-constant-per-family pattern exactly.
- [x] `src/Compono.Generators/ComponoIncrementalGenerator.cs` - two new
      `ForAttributeWithMetadataName` pipeline registrations for the new constants (both feeding
      `AotComposeMethodDiscovery`'s extended `TransformMethod`, and both also registered with
      `ComposeMethodDiscovery`'s existing pipeline for ordinary `PlanCache<T>` parameter-type discovery -
      a profile doesn't change which parameter types a test method composes, so this reuses the existing
      mechanism unchanged).
- [x] `src/Compono.Generators/Models/AotComposeMethodInfo.cs` - add a nullable `AotProfileInfo? Profile`
      field. New `AotProfileInfo` record: `FullyQualifiedProfileTypeName`, and (for the two-type-
      parameter form only) `FullyQualifiedConfigTypeName` + `EquatableArray<AotProfileConfigArgumentInfo>
      ConfigArguments` (rendered-literal-ready: each argument's C#-source-literal text, computed once at
      discovery time - see next task).
- [x] `src/Compono.Generators/Discovery/AotComposeMethodDiscovery.cs` - extend `TransformMethod` (or add
      a sibling entry point covering the two new metadata names) to:
  - For `[Compose<TProfile>]`: no new validation needed (`where TProfile : ICompositionProfile, new()`
        is a compile error at the use site for anything else) - just capture
        `FullyQualifiedProfileTypeName`.
  - For `[Compose<TProfile, TConfig>]`: resolve `TConfig`'s declared constructors
        (`INamedTypeSymbol.Constructors`, public, non-static) - exactly one, else `CMP0041`. Resolve
        `TProfile`'s declared constructors for exactly one accepting exactly one `TConfig`-typed
        parameter (`SymbolEqualityComparer.Default` against `TConfig`'s symbol, per this repo's
        established signature-comparison rule) - else `CMP0042`. Validate the attribute's
        `AttributeData.ConstructorArguments` (`TypedConstant`s) against `TConfig`'s single constructor's
        parameter count/nullability/assignability (mirrors `PositionalArgumentBinder.Validate`'s rules,
        Roslyn-side) - else `CMP0043`.
- [x] New literal-rendering helper (e.g. `src/Compono.Generators/Emitters/TypedConstantLiteralRenderer.cs`)
      - renders one `TypedConstant` back to valid C# source for a known target parameter type: a string
      via `SymbolDisplay.FormatLiteral`; a `System.Type`-typed argument (`TypedConstantKind.Type`) as
      `typeof({{ fully_qualified_name }})`; an enum member as a fully-qualified member-access expression
      (or a cast for a non-defined combined-flags value); a primitive via its literal form; a
      one-dimensional array via a bracketed literal list. Bounded, closed set - matches the set of
      attribute-legal argument types C# itself allows, nothing broader needs handling.
- [x] `src/Compono.Generators/Emitters/AotTheoryDataRowRegistrationEmitter.cs` +
      `src/Compono.Generators/Templates/AotTheoryDataRowRegistration.scriban` - extend the generated
      factory closure: no profile → unchanged `global::Compono.Composer.Create()`; `[Compose<TProfile>]`
      → `global::Compono.Composer.Create(b => b.AddProfile<{{ fully_qualified_profile_type_name }}>())`;
      `[Compose<TProfile, TConfig>]` → construct `config`/`profile` locals via the rendered-literal
      constructor calls, then `global::Compono.Composer.Create(b => b.AddProfile(profile))`.
- [x] `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` +
      `src/Compono.Generators/AnalyzerReleases.Unshipped.md` - `CMP0041`/`CMP0042`/`CMP0043`, continuing
      the `CMP004x: Compono.XunitV3.Aot diagnostics` series Phase 1's `CMP0040` started, same file
      region/comment block.
- [x] `test/Compono.Generators.Tests` - snapshot coverage for the extended emitter (no-profile
      unchanged-baseline case still passing; `[Compose<TProfile>]` case; `[Compose<TProfile, TConfig>]`
      success case covering at least a string, an enum, and a `typeof(...)` argument; `CMP0041`/
      `CMP0042`/`CMP0043` each triggering cases) plus an `IncrementalCachingTests` case for the new
      discovery path.
- [x] `test/Compono.XunitV3.Aot.Tests` - real generated-code execution (`ProjectReference`-based, JIT/
      `dotnet test`) for both new attribute forms, including a case proving `TProfile.Configure`'s own
      `Register<T>()` calls are honored (parity check against the equivalent `Compono.XunitV3` behavior -
      "JIT parity" per the brief's Testing requirements). No `[Shared]` interaction case - `[Shared]`
      remains entirely unimplemented in `Compono.XunitV3.Aot` (deferred alongside inline values, per this
      plan's own Scope section), so there is no syntax to exercise it with yet.
- [x] `test/Compono.XunitV3.Aot.SampleTests` - packaged-dependency-chain sample coverage for both forms,
      added to the existing project rather than a new one: it already doubles as the permanent Tier 3
      Native AOT proof project (PLAN-0066's own precedent), and the negative-diagnostic cases (below)
      turned out not to need to coexist with it at all - they're compile-time-only and belong with
      `CMP0040`'s own coverage in `test/Compono.Generators.Tests` instead.
- [x] Real Native AOT proof, both forms: `dotnet publish -p:PublishAot=true` (using the working
      `-p:RuntimeIdentifier=<rid> -p:SelfContained=true -p:UseAppHost=true` MSBuild-property form PLAN-
      0066's Notes section already recorded, not the `-r <rid> --self-contained` shorthand), native
      binary executed directly, zero IL2xxx/IL3xxx warnings, both the `[Compose<TProfile>]` and
      `[Compose<TProfile, TConfig>]` test bodies actually executing with the profile/config genuinely
      applied (not merely compiling) - the equivalent of RESEARCH-0032 §9's Phase 1 proof, for this
      phase's new mechanism. Run manually during implementation (`osx-arm64`); real for CI is the
      unmodified `aot-xunitv3-aot-pipeline` job (`linux-x64`), which now covers these same tests since
      they live in the same project - see the workflow task below.
- [x] Negative-case proof: snapshot tests in `test/Compono.Generators.Tests`
      (`AotTheoryDataRowRegistrationVerifyTests`), the same `GeneratorTestHelpers.VerifyFailure` +
      hand-written-stand-in-package mechanism `CMP0040`'s own coverage already uses - not a separate
      project. Each of `CMP0041`/`CMP0042`/`CMP0043` has its own case, asserting the diagnostic ID and
      snapshotting the real reported location/message text (proving it's anchored at the method's real
      declaration, not just internally exercised against the discovery method in isolation).
- [x] `.github/workflows/aot-validation.yaml` - no edit needed: the existing `aot-xunitv3-aot-pipeline`
      job already publishes and runs the whole `Compono.XunitV3.Aot.SampleTests` binary, which now
      contains the two new profile-form tests alongside the plain-`[Compose]` one, so it covers them
      automatically; `aot-gate`'s existing dependency on this job covers the extension too. Its trigger
      paths already include `src/Compono.Generators/` (via `core_or_shared_change`), which this plan's
      generator changes fall under.

### Docs/skill tasks

- [x] `docs/packages/compono-xunitv3-aot.md` - document `[Compose<TProfile>]`/`[Compose<TProfile,
      TConfig>]` support, and explicitly call out the compile-time-vs-runtime diagnostic-timing
      divergence from `Compono.XunitV3` (ADR-0067's accepted-divergence section) so it reads as
      documented behavior, not an inconsistency.
- [x] `docs/packages/compono-xunitv3.md` - cross-reference update if needed (mirrors Phase 1's own
      cross-ref task).
- [x] `compono` agent skill (`~/.agents/skills/compono/`) - `references/xunitv3-aot.md` rewritten for
      the two new attribute forms, the compile-time-vs-runtime diagnostic divergence, and `CMP0041`/
      `CMP0042`/`CMP0043`; `SKILL.md`'s frontmatter diagnostic range, detection table (`Compono.XunitV3.Aot`
      PackageReference row, `[Compose]`/`[Compose<...>]` row), and reference-file index all updated to
      drop the "Phase 1 plain `[Compose]` only" framing.
- [x] `evals.json` - eval 56 corrected (its "Phase 1 only supports plain `[Compose]`" reasoning was now
      stale - narrowed to the still-true inline-values/`[Shared]` limitation, plus an explicit
      expectation that the skill not claim profile support is unavailable); three new cases added (58:
      `[Compose<TProfile>]` activation, 59: `[Compose<TProfile, TConfig>]` shape question, 60: `CMP0043`
      diagnostic). Baseline-vs-after comparison run: the pre-change skill snapshot (saved before editing)
      explicitly documents Phase 1 as plain-`[Compose]`-only and explicitly instructs "if asked about
      [these forms], say plainly Phase 1 doesn't support it yet" - the expected, safe "before" behavior
      for cases 58-60 (same shape as PLAN-0066's own case-1 false-positive check: a documented
      instruction-following outcome, not one needing a live before/after model comparison to establish).
      The post-change skill was verified live (a fresh subagent loaded the real skill via the Skill tool
      and answered cases 56/58/59/60): all four passed self-check against their expectations - correct
      attribute forms, correct `TConfig`/`TProfile` constructor-shape guidance, correct `CMP0043`
      explanation (compile-time-by-design, not a bug, distinct from `CMP0041`/`CMP0042`), no false
      "unsupported" claims, no invented AOT-specific abstraction. No skill content needed correction as
      a result.

## Critical Files

- `src/Compono.XunitV3.Aot/` - two new attribute files (above).
- `src/Compono.Generators/Discovery/AotComposeMethodDiscovery.cs` - extended with two new metadata
  names and the compile-time validation logic.
- `src/Compono.Generators/Models/AotComposeMethodInfo.cs` - extended with `AotProfileInfo`.
- `src/Compono.Generators/Emitters/AotTheoryDataRowRegistrationEmitter.cs`,
  `src/Compono.Generators/Templates/AotTheoryDataRowRegistration.scriban` - extended generated-code
  shape.
- `src/Compono.Generators/Emitters/TypedConstantLiteralRenderer.cs` (new) - the one genuinely new piece
  of generator machinery this plan introduces (ADR-0067's "Negative Consequences").
- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs`,
  `src/Compono.Generators/AnalyzerReleases.Unshipped.md` - `CMP0041`/`CMP0042`/`CMP0043`.
- `.github/workflows/aot-validation.yaml` - extended `aot-xunitv3-aot-pipeline` job.
- `test/Compono.XunitV3.Aot.Tests`, `test/Compono.XunitV3.Aot.SampleTests`, and whatever new
  negative-diagnostic proof surface implementation determines is cleanest.

## Test Plan

Same three-tier pattern as PLAN-0066, plus the JIT-parity and negative-diagnostic requirements this
phase specifically calls for:

1. Unit/snapshot tests of the extended discovery/emission logic (`test/Compono.Generators.Tests`),
   including all three new diagnostics.
2. Real generated-code execution against the packaged dependency chain
   (`test/Compono.XunitV3.Aot.Tests`/`SampleTests`), including a `Compono.XunitV3`-vs-
   `Compono.XunitV3.Aot` parity case for at least one profile scenario.
3. Real `dotnet publish -p:PublishAot=true` + native-binary-execution proof for both attribute forms,
   through the real generator-emitted registration - extending `aot-xunitv3-aot-pipeline`.
4. A negative-case proof that `CMP0041`/`CMP0042`/`CMP0043` are real, correctly-located compile-time
   diagnostics, not just internally-unit-tested discovery-method behavior.

## Compatibility

- No change to `Compono.XunitV3`, core `Compono`, or Phase 1's plain-`[Compose]` mechanism.
- No new minimum xUnit v3 AOT or .NET version - this phase introduces no new xUnit API dependency beyond
  what Phase 1 already requires (`RegisteredEngineConfig.RegisterTheoryDataRowFactory`).
- No `[DynamicallyAccessedMembers]` annotations introduced anywhere in this phase's new code (ADR-0067).
- The compile-time-vs-runtime diagnostic-timing divergence from `Compono.XunitV3` (ADR-0067) is the only
  intentional behavioral difference; documented, not silent.

## Dogfooding

`alexa-vox-craft`'s `AlexaVoxCraft.NativeAot.ValidationApp` conversion to `Compono.XunitV3.Aot` is the
first post-Phase-2 dogfood target (per the `xunitv3aot-dogfood-sequencing` project memory) - **not a
task of this plan**. Once this plan's Phase 1 ships, use the repository's normal dogfooding workflow
(`scripts/dogfood-validate.sh`, per `CLAUDE.md`'s "Consumer/dogfood validation gate" section) rather than
an ad hoc validation pass, and revisit that repo's own deliberate no-test-infra-dependency policy (its
`AlexaVoxCraft.NativeAot.ValidationApp.csproj` comment, per plan 0003 Task Group 7) as its own explicit
product-owner decision, the same way the first dogfood attempt surfaced it.

## Notes

**Post-implementation review correction (this plan's own second pass):** the first implementation pass
unsealed `Compono.XunitV3.Aot.ComposeAttribute` and had `ComposeAttribute<TProfile>`/
`ComposeAttribute<TProfile, TConfig>` derive from it, mirroring `Compono.XunitV3.ComposeAttribute`'s
own deliberately-unsealed design superficially. Review caught that this mirroring didn't actually hold
up under scrutiny: `Compono.XunitV3.ComposeAttribute`'s generic siblings extend real, shared runtime
state (a cached `Composer`, cached binding delegates, a real `GetData` override) - inheritance is load-
bearing there. `Compono.XunitV3.Aot.ComposeAttribute` is a pure marker with zero functional state, and
AOT discovery matches purely by each closed attribute type's own fully qualified metadata name, never
by inheritance/assignability - so sharing a base class bought nothing functionally, while opening a real
hazard unique to this marker-only family: a consumer subclassing any of the three forms would compile
cleanly but never be discovered by `Compono.Generators` (its own metadata name wouldn't match any
registered provider), silently never running - exactly the failure mode ADR-0066's `CMP0040` exists to
prevent for every other unsupported shape. **Fixed:** `ComposeAttribute` reverted to `sealed` (its
original Phase 1 shape, unchanged); `ComposeAttribute<TProfile>`/`ComposeAttribute<TProfile, TConfig>`
now derive directly from `Xunit.v3.DataAttribute`, with no inheritance relationship to the non-generic
form - independent marker siblings unified only by naming convention and the shared `Compono.Generators`
discovery pattern, not by CLR inheritance. Recorded as ADR-0067 Amendment 1 (dated), since the ADR's own
"Shape" section originally showed the inheritance relationship. No discovery/codegen logic depended on
the inheritance relationship (confirmed: `AotComposeMethodDiscovery` matches purely on
`AttributeData.AttributeClass`'s metadata name), so this is a pure API-shape correction with zero
behavioral change to any already-passing test.

**Post-implementation review finding, resolved as "no gap exists":** review also asked whether a single
array-typed `TConfig` constructor argument (e.g. `TConfig(string[] items)`, supplied as
`[Compose<TProfile, TConfig>(new string[] { "a", "b" })]`) is a real `Compono.XunitV3` parity gap this
plan's `CMP0043` count-check papers over. Investigated directly against Roslyn (a standalone compile
probe, not guesswork): a single argument more specifically typed than this attribute's own declared
`object?[]` parameter type is **not legal C# attribute-argument syntax at all** -
`CS0182: An attribute argument must be a constant expression, typeof expression or array creation
expression of an attribute parameter type` rejects `[Compose<TProfile, TConfig>(new string[] { "a",
"b" })]` at compile time, identically for `Compono.XunitV3.ComposeAttribute<TProfile, TConfig>` - this
is a plain C# language restriction, not a choice either package makes.
`Compono.XunitV3.ComposeAttribute.NormalizeParamsArguments`'s own "single reference-array argument"
handling (the code this plan's `AotComposeMethodDiscovery` remarks originally cited as the JIT-mode
precedent for this shape) is exercised only by
`ComposeAttributeCachingTests.InlineValues_SingleReferenceArrayArgument_TreatedAsOneSuppliedArrayValue`,
which calls the `ComposeAttribute` constructor directly in ordinary C# code - never through a real
`[Compose(...)]` attribute annotation, which is the only way any theory attribute (JIT or AOT) is ever
actually applied to a test method. **Conclusion: there is no parity gap to close.** No code change was
needed; `AotComposeMethodDiscovery.cs`'s own comment on this was corrected to record the investigated
conclusion instead of an open "revisit if a real case surfaces" note.

One real design refinement, not a correction: `Compono.Generators.Tests` (the generator test project)
has a real `ProjectReference` to core `Compono`, so the real `Compono.CompositionBuilder.AddProfile<T>()`
generated code needs its test-double `TProfile` types to implement the *real*
`Compono.ICompositionProfile` - not a locally-declared fake one, the way
`CompositionPlanVerifyTests`'s own `Compono.XunitV3` stand-ins do (that suite only ever exercises
`ComposeMethodDiscovery`'s type-discovery concern, never the generated `AddProfile` call itself, so a
fake interface is enough there). `AotTheoryDataRowRegistrationVerifyTests`'s stand-in block leaves
`ICompositionProfile` unqualified instead, letting ordinary C# namespace-nesting resolve it to the real
`Compono.ICompositionProfile` - the identical mechanism the real production
`Compono.XunitV3.Aot.ComposeAttribute{TProfile}.cs`/`ComposeAttribute{TProfile,TConfig}.cs` files
themselves rely on (their own `namespace Compono.XunitV3.Aot;` declaration is nested under `Compono`,
so `ICompositionProfile`/`CompositionBuilder` resolve with no `using Compono;` needed at all).

Real Native AOT proof (Tier 3), run manually during implementation: `dotnet publish
test/Compono.XunitV3.Aot.SampleTests -c Release -f net10.0 -p:RuntimeIdentifier=osx-arm64
-p:SelfContained=true -p:PublishAot=true -p:UseAppHost=true`, then executing the published native
binary directly - `Test run summary: Passed! ... total: 3, failed: 0, succeeded: 3`, exit code 0, zero
`IL2xxx`/`IL3xxx` warnings in the publish output. All three tests (plain `[Compose]`,
`[Compose<TProfile>]`, `[Compose<TProfile, TConfig>]`) executed as real native code, with real profile/
config construction happening inside the native process - not merely compiling. **Re-run after
Amendment 1's inheritance correction** (both new generic attributes now derive directly from
`Xunit.v3.DataAttribute` rather than from the non-generic `ComposeAttribute`) - identical result:
build clean (0 warnings/errors, full solution), `Compono.Generators.Tests` 666/666,
`Compono.XunitV3.Aot.Tests` 15/15, `Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via the
re-published native binary, exit 0, zero `IL2xxx`/`IL3xxx` warnings - confirming the correction was a
pure type-hierarchy change with no behavioral effect, as predicted (discovery/codegen never depended on
the inheritance relationship).
