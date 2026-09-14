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

**PR #140 Codex review round 1** found five real gaps in the initial implementation, all fixed - three
new diagnostics (`CMP0044`/`CMP0045`/`CMP0046`) and two rendering-correctness fixes, none requiring an
ADR change:

- **`CMP0044`** — `TProfile`/`TConfig`/a `typeof`-or-enum-typed argument's own type wasn't checked for
  accessibility from the generated top-level registration before this pass. A `private` nested
  profile/config type (legal at the `[Compose<...>]` use site, common for a test-local fixture type)
  would have reached code generation and failed with `CS0122` in the generated file instead of a clear
  diagnostic at the real attribute use site - the same class of gap `CMP0013` already guards against
  for ordinary composed parameter types, now applied here too.
- **`CMP0045`** — `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` share no common base
  class (Amendment 1), so nothing stopped stacking two different forms on one method the way
  `BindingPlan.ValidateSignature`'s single reflection query catches this for `Compono.XunitV3`'s three
  forms. Left unchecked, both would reach `AddSource` with the identical class-and-method-derived hint
  name and **crash the whole generator** (not just fail this one method) - now caught first, before any
  other processing, mirroring the JIT-mode check's own message text. (Known minor cosmetic effect,
  accepted rather than fixed further: the diagnostic is reported once per matching attribute form on the
  stacked method - e.g. twice for two stacked attributes - since each has its own independent discovery
  registration; `Compono.XunitV3`'s own JIT-mode equivalent has the analogous "reported once per
  attribute instance's `GetData` call" behavior for the same underlying cause, so this isn't a new
  inconsistency.)
- **TConfig non-named-type guard** — `TConfig` carries no generic constraint at all, so `TConfig =
  string[]` (or any other non-named type) is legal `[Compose<TProfile, TConfig>]` syntax; the initial
  pass's unconditional `(INamedTypeSymbol)` cast on both type arguments threw `InvalidCastException`
  inside the generator for this shape instead of reporting `CMP0041`. Fixed by treating a non-named
  (or abstract) type argument as "zero constructors" for both `TConfig` and `TProfile` - reuses the
  existing `CMP0041`/`CMP0042` diagnostics rather than adding new ones, no crash either way now.
- **`double.NaN`/`PositiveInfinity`/`NegativeInfinity`/negative-zero rendering** — these are real,
  legal compile-time-constant attribute arguments (confirmed by a direct compile probe, not assumed);
  `TypedConstantLiteralRenderer`'s original `Convert.ToString(...)`-based cast rendering produced the
  bare identifiers `NaN`/`Infinity`/`-Infinity` (not valid C# syntax - `(double)NaN` doesn't compile)
  for the first three, and silently discarded the sign of `-0.0` for the fourth (`Convert.ToString(-0.0)`
  → the digit string `"-0"` → `(double)-0` casts the *int* literal `-0` = `0`, losing the negative-zero
  bit entirely - a silent wrong-value bug, not a compile failure). Fixed with dedicated
  `double.NaN`/`double.PositiveInfinity`/`double.NegativeInfinity` (and `float` equivalents) rendering,
  and an explicit `-(double)0`/`-(float)0` unary-negation rendering for negative zero (IEEE 754 negation
  of a real zero value correctly produces negative zero, unlike negating the digit string first).
- **`CMP0046`** — the selected `TConfig`/`TProfile` constructor can leave a `required` member
  unsatisfied (no `[SetsRequiredMembers]`), which the initial pass's shape checks (constructor *count*
  only) never caught - the generated direct `new` call would fail with `CS9035`. Deliberately a
  compile-time rejection, not an attempt to auto-compose required members the way core Compono's own
  `RequiredMemberCollector` does for ordinary composed types: `TConfig`/`TProfile` are built from
  literal attribute arguments, not Compono's provider pipeline, so there's no sensible composed value to
  auto-supply a required member with - inventing one would be a confusing, undocumented semantic, not a
  "smallest correct fix."

All five: new/updated snapshot coverage in `test/Compono.Generators.Tests`
(`AotTheoryDataRowRegistrationVerifyTests` - `CMP0044`/`CMP0045`/`CMP0046` each get their own
`VerifyFailure` case; the non-named-`TConfig` case proves `CMP0041` fires without crashing; the
floating-point case is a `Verify` - compiles-and-runs, not just a snapshot - proving the rendered
literals are real valid C#). Re-validated after these fixes: full build 0 warnings/errors,
`Compono.Generators.Tests` 676/676, `Compono.XunitV3.Aot.Tests` 15/15,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**PR #140 Codex review round 2** found three more real gaps and one false positive:

- **`TConfig`/`TProfile` constructor selection accepted `ref`/`out`/`in` parameters.**
  `IParameterSymbol.Type` strips the ref modifier, so a `Profile(ref TConfig config)` or
  `TConfig(ref int value)` constructor matched the "exactly one usable constructor" check by type
  alone. For `ref`/`out`, the generated direct `new`/`AddProfile(profileInstance)` call (a plain
  argument, no `ref`/`out` keyword, since it's built from a literal value with no addressable
  variable to take a reference to) fails with `CS1620`; for `in` specifically, the generated call
  actually *compiles* (the `in` modifier is call-site-optional in C#) - a real, silent behavioral
  divergence from `Compono.XunitV3`'s JIT-mode `ConfigProfileBinder`, which reflects the parameter's
  real by-ref runtime type (`TConfig&`) and never matches this shape as a candidate constructor at
  all. Fixed by requiring `RefKind.None` on every parameter of a candidate `TConfig` constructor, and
  on `TProfile`'s own `TConfig`-typed parameter, before counting it as "usable" - reuses the existing
  `CMP0041`/`CMP0042` diagnostics (reported as "0 usable constructors"), no new diagnostic needed.
- **`TypedConstantKind.Error` reached the renderer's unhandled default arm.** During an
  incomplete/erroneous compilation (most commonly: live IDE analysis mid-edit), Roslyn can hand back a
  `TypedConstant` of `Kind = Error` whose `Type` is still the parameter's own declared type -
  `TypedConstantMatcher`'s `ClassifyConversion` call would find a trivial identity conversion and
  report `Valid`, reaching `TypedConstantLiteralRenderer.Render`'s `_ => throw
  NotSupportedException(...)` default arm, an unhandled exception that crashes the whole generator
  invocation (not just this one method) rather than the clean "ignore/diagnose, never crash" behavior
  every other unsupported shape in this series gets. Fixed by checking `constant.Kind ==
  TypedConstantKind.Error` in `TypedConstantMatcher.Validate` before attempting conversion
  classification at all, reporting it as an ordinary `CMP0043` type mismatch. New test drives the real
  `GeneratorDriver` directly (mirroring `IncrementalCachingTests`' own pattern, since
  `GeneratorTestHelpers.Verify`/`VerifyFailure` both assume a compilation with no *other* pre-existing
  errors) against source containing a genuinely undefined identifier as the attribute argument, and
  asserts `driver.GetRunResult()` doesn't throw - the property under test is generator crash-resilience,
  not that this deliberately-invalid source compiles (it categorically can't).
- **Rejected as a false positive, with evidence, not fixed: "unsafe pointer/function-pointer `typeof`
  argument needs an unsafe context in the generated file."** The finding claimed `typeof(int*)` (or a
  function-pointer type) as a `[Compose<TProfile, TConfig>]` argument would fail to compile in the
  generated top-level file with `CS0214` since that file isn't declared `unsafe`. Verified directly
  with two standalone compile probes before touching anything: `typeof(int*)` used as a real attribute
  argument, and `typeof(delegate*<int, void>)` used as an ordinary local-variable initializer in a
  ordinary (non-`unsafe`, no `<AllowUnsafeBlocks>`) method body - both compile cleanly, 0 errors. A
  `typeof(...)` expression over a pointer or function-pointer type is exempt from C#'s unsafe-context
  requirement entirely (the restriction applies to actually using/dereferencing a pointer value, not to
  naming a pointer *type* via `typeof`) - the premise of this finding doesn't hold, so `int*`/function-
  pointer `System.Type` profile-configuration arguments already render and compile correctly with no
  change needed. Replied on the thread with both probe results; left the thread open (unresolved) rather
  than resolving it, since nothing was actually changed in response to it.

All three real fixes: new tests (`TwoTypeParameterAttribute_TConfigConstructorHasByRefParameter_
ReportsCmp0041`, `TwoTypeParameterAttribute_TProfileConstructorHasByRefParameter_ReportsCmp0042`,
`TwoTypeParameterAttribute_ErroneousArgumentExpression_DoesNotCrashGenerator`). Re-validated: full
build 0 warnings/errors, `Compono.Generators.Tests` 682/682, `Compono.XunitV3.Aot.Tests` 15/15,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**PR #140 Codex review round 3** found two more real, confirmed parity divergences against
`Compono.XunitV3.Binding.ConfigProfileBinder.ResolveSingleConstructor` - both verified empirically with
standalone probes before touching code, given round 2 had already produced one false positive:

- **`TConfig` constructor counting excluded ref/out/in constructors *before* counting, not after.**
  Round 2's fix filtered ref/out/in-parameter constructors out of the candidate set first, then
  counted what remained - so a `TConfig` with one ordinary and one ref/out/in-parameter public
  constructor silently succeeded here (filtered count = 1), while
  `Type.GetConstructors(Public | Instance)` (confirmed by a direct reflection probe) returns *both*
  constructors, so JIT-mode's `ResolveSingleConstructor` sees count = 2 and rejects the identical
  `TConfig` as ambiguous - a real, confirmed divergence, not just a wording nicety
  (ADR-0067's own stated design intent: "performs, at compile time, the same three checks
  `ConfigProfileBinder` performs at runtime"). Restructured into the same two sequential gates
  JIT-mode actually has: raw public-constructor count first (matches `ResolveSingleConstructor`'s own
  gate exactly, reported with the true raw count), then - only once exactly one constructor exists -
  whether *that* constructor is usable for AOT's direct-construction codegen (a ref/out/in parameter
  still can't be satisfied by a generated literal argument, unlike JIT's reflection-based
  `ConstructorInfo.Invoke`, which a second direct probe confirmed actually *succeeds* for a ref
  parameter given a plain boxed argument - reflection marshals by-ref parameters transparently, a
  capability AOT's compile-time-generated direct `new` call structurally cannot replicate). Both gates
  reuse the existing `CMP0041` diagnostic (the raw count, or `0` for "exists but unusable"), matching
  the existing abstract/non-named-type convention - no new diagnostic. (`TProfile`'s own constructor
  selection needed no equivalent change: JIT-mode's `ResolveSingleProfileConstructor` already filters
  by `parameters[0].ParameterType == configType` as part of its *matching* criteria, not as a separate
  count-then-filter step, and a ref/out/in `TConfig`-typed parameter's reflected `ParameterType` is a
  distinct byref type that never equals `configType` - so JIT-mode's own matching logic already
  excludes it structurally, and this repo's filter-then-count approach for `TProfile` already agreed
  with that.)
- **A struct `TConfig` with no explicitly declared constructor.** Roslyn's `INamedTypeSymbol.Constructors`
  includes the compiler-synthesized public parameterless constructor for this shape
  (`IsImplicitlyDeclared = true`, confirmed by a direct Roslyn probe), so the "exactly one public
  constructor" count previously included it and let this `TConfig` succeed (emitting `new
  TConfig()`). `Type.GetConstructors(Public | Instance)` (confirmed by a direct reflection probe)
  returns *zero* constructors for exactly this shape - the implicit constructor isn't reflectable at
  all - so JIT-mode's binder rejects the identical `TConfig` with "has 0". Fixed by excluding
  `IsImplicitlyDeclared` constructors from the counted set.
- **Rejected as a false positive, with evidence, not fixed: "`typeof(int*)`/function-pointer profile
  configuration arguments need an unsafe context in the generated file."** This was round 2's
  finding, not round 3's - listed here only for completeness of the false-positive count (two
  standalone compile probes in round 2 already showed `typeof(...)` over a pointer/function-pointer
  type is exempt from C#'s unsafe-context requirement entirely; that thread remains open, unresolved,
  by design).

Two new tests: `TwoTypeParameterAttribute_TConfigHasAmbiguousConstructorsIncludingByRef_
ReportsCmp0041WithRawCount` (proves the raw count, not the filtered count, is what gets reported -
"has 2", matching JIT exactly), `TwoTypeParameterAttribute_TConfigIsStructWithOnlyImplicitConstructor_
ReportsCmp0041` (a struct `TConfig` with zero explicit constructors is rejected, not silently
constructed via `new TConfig()`). Re-validated: full build 0 warnings/errors,
`Compono.Generators.Tests` 686/686, `Compono.XunitV3.Aot.Tests` 15/15,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**PR #140 Codex review round 4** found three more real gaps - the first a genuine regression in
round 3's own fix, the other two a further-generalized form of round 1's/round 2's "didn't recurse
into array elements" limitation, which the implementation had already flagged as a known gap when it
shipped:

- **Round 3's `IsImplicitlyDeclared` exclusion was scoped too broadly - it also rejected an ordinary
  `class` with no explicit constructor.** A *class*'s own implicit default constructor is
  `IsImplicitlyDeclared = true` in Roslyn too, same as a struct's, but (unlike a struct's) it's real,
  reflectable IL - `Type.GetConstructors(Public | Instance)` returns 1 for a no-explicit-ctor class,
  confirmed by a direct probe - so `ConfigProfileBinder` succeeds constructing it, while the round-3
  fix's blanket exclusion made this same, entirely ordinary `TConfig` shape fail here with "has 0".
  Fixed by scoping the exclusion to `configType.IsValueType` specifically - the exact condition that
  distinguishes the two cases (confirmed real by direct reflection probe before touching anything, the
  same discipline every prior round's finding got).
- **Nested `TypedConstantKind.Error` inside an array argument reached the renderer unguarded.** The
  round-2 fix (`TypedConstantMatcher.Validate`) only checked the *outer* constant's `Kind` - for a
  malformed array argument (`new int[] { UndefinedIdentifier }`), Roslyn reports a well-typed outer
  `Array` constant whose *element* is `Kind = Error`, which the outer check never saw, letting it reach
  `TypedConstantLiteralRenderer.Render`'s recursive `RenderArray` call and crash the generator the same
  way the round-2 case did. Fixed with a `HasError` helper that walks the whole constant tree
  (recursing through `Kind = Array`'s own `Values`), replacing the single `Kind == Error` check.
- **Nested inaccessible types inside array arguments bypassed `CMP0044` the same way.** The round-1
  `CMP0044` fix only inspected the top-level argument's own `Kind` (`Type`/`Enum`) - an array of a
  private nested enum's values (e.g. `new PrivateKind[] { PrivateKind.Value }` bound to an
  `object`-typed `TConfig` constructor parameter - confirmed real and legal attribute syntax by a
  direct probe, since the array-creation-expression's declared parameter type is what `CS0182` actually
  checks, not the outer `params object?[]`'s own type) has `Kind = Array` at the top level, so the
  private `PrivateKind` type embedded in each element slipped through undetected, and the renderer
  would have emitted an inaccessible-type reference (`CS0122`) the same way a top-level `typeof`/enum
  argument already would have without the round-1 fix. Fixed with an `EmbeddedTypes` helper that
  recurses through array elements the same way `HasError` does, replacing the single-level `switch`.

Three new tests: `TwoTypeParameterAttribute_TConfigClassImplicitCtor_Succeeds` (an ordinary
no-explicit-ctor `class TConfig` must still succeed, proving the round-3 fix didn't regress the
common case), `TwoTypeParameterAttribute_NestedErroneousArrayElement_DoesNotCrashGenerator` (drives the
real `GeneratorDriver` directly, same pattern as the round-2 equivalent, with a malformed array
argument), `TwoTypeParameterAttribute_InaccessibleTypeInsideArrayArgument_ReportsCmp0044`. Re-validated:
full build 0 warnings/errors, `Compono.Generators.Tests` 692/692, `Compono.XunitV3.Aot.Tests` 15/15,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**PR #140 Codex review round 5** found two more real, confirmed bugs - the second a genuine crash
(worse than described, an outright `NullReferenceException`, not merely an unhandled edge case):

- **Generated `TConfig` construction could silently resolve to a different, more-specific accessible
  constructor than the one `Compono.Generators` selected and validated.** The generated registration
  is a `file`-scoped type living in the *consumer's own assembly* - so if `TConfig` has one public
  constructor (the one `CMP0041` selects) and an `internal` sibling constructor with a more specific
  parameter type (e.g. public `TConfig(object)` + internal `TConfig(string)`), the generated
  `new TConfig("value")` call is genuinely ambiguous to the C# compiler, which resolves it to the
  more-specific `internal TConfig(string)` overload via ordinary overload resolution - silently
  constructing `TConfig` differently than what was validated. `Compono.XunitV3.Binding
  .ConfigProfileBinder` never has this problem: `ConstructorInfo.Invoke` invokes the *exact*
  `ConstructorInfo` it already resolved, with no overload resolution involved at all. Fixed by
  wrapping every rendered constructor argument in an explicit cast to the *selected* constructor's own
  declared parameter type (`(object)"value"`, not the bare `"value"` `TypedConstantLiteralRenderer`
  already rendered) - `AotProfileConfigArgumentInfo` now also carries
  `FullyQualifiedParameterTypeName` for this. Confirmed as a real, reachable divergence by reasoning
  through C# overload-resolution rules (an internal sibling constructor genuinely is accessible from
  the generated file), then proven concretely with a real, JIT-executed test
  (`TConfigWithMoreSpecificInternalOverload_UsesSelectedPublicConstructor` in
  `Compono.XunitV3.Aot.Tests`, not just a generator snapshot) - each constructor sets a different
  observable value, and the test asserts the *public* one actually ran.
- **`EmbeddedTypes`' array-element recursion (round 4's own `CMP0044` fix) crashed on a legitimately
  null argument.** A bare `[Compose<P, C>(null)]` targeting a nullable `TConfig` constructor parameter
  is valid, common usage - `NormalizeConstructorArguments` already handles it (the whole params array
  binds as `IsNull = true`, normalized to "one supplied argument, whose value is null"), and
  `TypedConstantMatcher.Validate` already correctly reports it `Valid` for a nullable parameter. But
  that same null `TypedConstant` still reports `Kind = Array` (matching the params array's own
  declared type), and accessing its `.Values` property throws `NullReferenceException` outright -
  confirmed by a direct probe, worse than the "empty array" framing the finding used. `EmbeddedTypes`
  didn't guard `IsNull` before recursing into `.Values`, so this real, reachable shape crashed the
  generator. Fixed with an `IsNull` guard at the top of `EmbeddedTypes`, returning no embedded types
  for a null constant (mirroring the same guard `TypedConstantMatcher.Validate`/
  `TypedConstantLiteralRenderer.Render` already had, just missing from this newer helper).

Three new tests: `TwoTypeParameterAttribute_ArgumentCastToSelectedConstructorParameterType` (generator
snapshot proving the cast is emitted), `TConfigWithMoreSpecificInternalOverload_
UsesSelectedPublicConstructor` (real JIT execution in `Compono.XunitV3.Aot.Tests`, described above),
`TwoTypeParameterAttribute_NullArgumentForNullableParameter_DoesNotCrash`. Re-validated: full build 0
warnings/errors, `Compono.Generators.Tests` 696/696, `Compono.XunitV3.Aot.Tests` 18/18,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**Process correction, round 5/6 boundary:** round 5's own fix commit was pushed against only two of
round 5's three actual Codex findings - an unpaginated `gh api .../comments` query silently truncated
the result set and a third finding (below) was missed entirely. This was caught during round 6's
re-check (a full `gh api` review-threads query showed more unresolved threads than the single expected
false-positive one), not by round 6 itself being clean - the initial round-6 check also under-queried
and briefly concluded "no actionable findings" before a paginated (`?per_page=100`) re-query surfaced
the two real round-6 findings below. All `gh api .../comments` queries from this point on use
`?per_page=100` (or full pagination) to avoid repeating this.

- **The missed round 5 finding - `EmbeddedTypes` never checked an array's own declared element type,
  only its elements.** An *empty* array of an inaccessible type (`new PrivateKind[] { }`) has no
  elements for the existing per-element recursion to walk, so it slipped past `CMP0044` entirely even
  though `TypedConstantLiteralRenderer` still emits the array's declared element type in the rendered
  literal (`new global::Ns.PrivateKind[] { }`), which would fail `CS0122` in the generated top-level
  file. Fixed by having the `TypedConstantKind.Array` case in `EmbeddedTypes` also `yield return` the
  constant's own `IArrayTypeSymbol.ElementType` unconditionally, alongside (not instead of) the existing
  per-element recursion into `.Values` (which still independently catches a *value* embedding some
  other type, e.g. a `typeof(...)` element inside an accessible `Type[]` array).
- **A `dynamic`-typed `TConfig` constructor parameter passed every existing check and would have
  generated a `(dynamic)"literal"` cast.** `TypedConstantMatcher.Validate`'s accepted-conversion
  criteria (`IsImplicit && IsReference`) is - correctly, for every *other* shape it needs to accept -
  satisfied by `ClassifyConversion(string, dynamic)` (confirmed by a direct Roslyn probe), so the
  validator itself needed no change; the actual defect was that a `dynamic`-parameter constructor should
  never have been in the *usable-constructor* set to begin with. A `(dynamic)` cast at the generated
  call site binds through `Microsoft.CSharp.RuntimeBinder`, the C# runtime dynamic binder - not
  Native-AOT/trim-safe, a direct violation of ADR-0067's zero-reflection guarantee. Unlike a ref/out/in
  parameter, this is not a JIT-parity gap (`ConstructorInfo.Invoke` can satisfy a `dynamic` parameter
  fine, it's `object` at the metadata level) - it's an AOT-only restriction. Fixed by extending the same
  second-gate usability filter that already excludes ref/out/in parameters
  (`BuildTwoTypeParameterProfile`'s `configConstructors` filter) to also exclude any constructor with a
  `TypeKind.Dynamic` parameter, folded into the same `CMP0041` "0 usable constructors" diagnostic rather
  than a new one, consistent with the ref/out/in precedent.
- **A selected `TConfig`/`TProfile` constructor marked `[Obsolete("...", error: true)]` passed every
  shape/accessibility/required-members check but produces an uncompilable generated call.** Such a
  constructor is otherwise completely ordinary and JIT-reflectable - `ConstructorInfo.Invoke` doesn't
  care about `[Obsolete]` at all - so this isn't folded into the existing "0 usable constructors" gates
  the way ref/out/in and `dynamic` are; it's purely that the generated registration's direct
  `new T(...)` call site can't use it, confirmed by a direct compile probe showing `CS0619`. Added a new
  diagnostic, `CMP0047`, and a post-selection check (mirroring `ConstructorSatisfiesRequiredMembers`'s
  own placement, right after the `CMP0046` required-members checks) applied to both the selected
  `configConstructor` and `profileConstructor`, looking for `[ObsoleteAttribute]` with its `error`
  constructor argument `true`. (`[Obsolete("...")]`/`[Obsolete("...", error: false)]` - a mere `CS0618`
  warning, not an error - is left alone; the generated registration still compiles.)

Three new tests, all `VerifyFailure` generator-snapshot tests (no real-execution proof needed - each is
a rejection, not a construction-correctness question):
`TwoTypeParameterAttribute_EmptyArrayOfInaccessibleElementType_ReportsCmp0044`,
`TwoTypeParameterAttribute_TConfigConstructorHasDynamicParameter_ReportsCmp0041`,
`TwoTypeParameterAttribute_TConfigConstructorIsObsoleteAsError_ReportsCmp0047`. Re-validated: full
`Compono.Generators` build 0 warnings/errors (including the new `CMP0047` `AnalyzerReleases.Unshipped.md`
entry, no `RS2000`), `Compono.Generators.Tests` 702/702, `Compono.XunitV3.Aot.Tests` 18/18,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**PR #140 Codex review round 7** found two more real gaps, both in round 6's own `CMP0047` fix:

- **`CMP0047`'s check only recognized `[Obsolete(error: true)]`, not
  `[System.Diagnostics.CodeAnalysis.Experimental("...")]`** - a second, independent standard attribute
  with the same underlying problem: any *use* of a constructor it marks is a compiler error (its own
  diagnostic ID, e.g. `EXP001`, at default severity Error - unlike `Obsolete`, there's no `error: false`
  equivalent to opt out of), confirmed by a direct compile probe. Generalized the round-6 `IsObsoleteAsError`
  helper into `ProhibitedCallSiteAttribute`, which now recognizes either attribute and returns the
  rendered attribute syntax found (`"[Obsolete(error: true)]"` or `"[Experimental(\"EXP001\")]"`) for the
  diagnostic message - `CMP0047`'s descriptor message was generalized to name whichever attribute was
  actually found rather than hardcoding `[Obsolete(error: true)]`/`CS0619`, since the specific compiler
  error differs by attribute (`CS0619` vs. the attribute's own `DiagnosticId`). Same diagnostic ID
  (`CMP0047`) and same post-selection placement (right after the `CMP0046` checks) - this is a widened
  detection surface for the same problem class, not a new problem class.
- **The round-6 `CMP0047` test only exercised the `TConfig` branch of the check, never the separate
  `TProfile` branch** - `ProhibitedCallSiteAttribute(configConstructor)` and
  `ProhibitedCallSiteAttribute(profileConstructor)` are two independent call sites in
  `BuildTwoTypeParameterProfile`; if the `TProfile` one were removed or broke, the existing test suite
  would stay green. Added a dedicated test with only the `TProfile` constructor marked obsolete.

Two new tests, both `VerifyFailure` generator-snapshot tests (same rejection-only reasoning as round 6's
own three):
`TwoTypeParameterAttribute_TProfileConstructorIsObsoleteAsError_ReportsCmp0047`,
`TwoTypeParameterAttribute_TConfigConstructorIsExperimental_ReportsCmp0047`. Re-validated: full
`Compono.Generators` build 0 warnings/errors, `Compono.Generators.Tests` 706/706,
`Compono.XunitV3.Aot.Tests` 18/18, `Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a
re-published Native AOT native binary, exit 0, zero `IL2xxx`/`IL3xxx` warnings.

**PR #140 Codex review round 8** found three more real gaps - the third the most severe of any round so
far, a genuine silent-wrong-construction bug in round 5's own overload-hijack fix:

- **A constructor marked `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` compiles and runs fine
  under JIT but produces a real `IL3050`/`IL2026` warning for any `PublishAot=true`/trim-analyzed
  consumer of the generated direct call** - confirmed by direct probe. Unlike the `CMP0047` family
  (a hard compiler error), this is only an analyzer-surfaced warning - but it directly contradicts this
  package's own "zero `IL2xxx`/`IL3xxx` warnings" Native AOT proof, so it's treated the same way
  `dynamic` was in round 6: excluded from the usable-constructor set entirely (folded into
  `CMP0041`/`CMP0042`), applied to both the `TConfig` and `TProfile` constructor filters.
- **`CMP0047`'s check only recognized `[Obsolete(error: true)]` and `[Experimental(...)]`, not
  non-optional `[CompilerFeatureRequired("...")]`** - a third attribute in the same "any use is a
  compiler error" bucket (`CS9041` specifically). Source code can never apply this attribute directly
  (`CS8335` blocks it), but a constructor imported from *referenced* metadata (e.g. compiled by a
  future/different compiler) can carry it, and Roslyn reports it identically to any other constructor
  attribute on the imported symbol - confirmed by building exactly such a constructor via
  `System.Reflection.Emit.PersistedAssemblyBuilder` and referencing it. `ProhibitedCallSiteAttribute`
  generalized again to recognize this third shape.
- **Round 5's overload-hijack fix (casting every rendered argument to the selected constructor's own
  declared parameter type) does not defend against
  `[System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute]`.** Confirmed by direct probe:
  an accessible sibling constructor marked with a higher priority value still wins ordinary overload
  resolution *even with the explicit cast in place*, because C#'s priority-pruning happens before
  applicability/conversion-quality comparison is ever reached - unlike the plain accessible-internal-
  sibling shape round 5 fixed (where the cast genuinely does disambiguate), there is no codegen shape
  that can defeat this. Added a new diagnostic, `CMP0048`, and a conservative cross-constructor check
  (`FindHigherPriorityAccessibleSibling`): if *any* accessible sibling constructor of `TConfig`/`TProfile`
  carries an explicit priority strictly greater than the selected constructor's own (default 0), reject -
  regardless of whether that sibling's parameter shape would actually apply to the rendered arguments,
  since correctly computing real overload applicability at compile time here would mean reimplementing
  overload resolution itself (consistent with round 3's "diagnostic over cleverness" precedent for
  ref/out/in ambiguity).

Four new tests: `TwoTypeParameterAttribute_TConfigConstructorRequiresDynamicCode_ReportsCmp0041`,
`TwoTypeParameterAttribute_TProfileConstructorRequiresUnreferencedCode_ReportsCmp0042` (round 7's lesson
- test both the `TConfig` and `TProfile` branches, not just one),
`TwoTypeParameterAttribute_TConfigConstructorSupersededByOverloadPriority_ReportsCmp0048`, and
`TwoTypeParameterAttribute_TConfigConstructorIsCompilerFeatureRequired_ReportsCmp0047` (the last built
against a real IL-emitted reference assembly via `PersistedAssemblyBuilder`, the only way to legally
reproduce a `[CompilerFeatureRequired]`-marked constructor at all). Re-validated: full `Compono.Generators`
build 0 warnings/errors, `Compono.Generators.Tests` 714/714, `Compono.XunitV3.Aot.Tests` 18/18,
`Compono.XunitV3.Aot.SampleTests` 3/3 (JIT) then 3/3 again via a re-published Native AOT native binary,
exit 0, zero `IL2xxx`/`IL3xxx` warnings.
