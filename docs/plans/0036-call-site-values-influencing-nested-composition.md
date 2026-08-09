# [PLAN-0036] Call-Site Values Influencing Nested Composition

**Status:** Done

**Implements:** [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)

## Goal

A `Compono.XunitV3` test can select a profile that needs call-site-known
configuration — `[Compose<PersistenceTestProfile, PersistenceTestConfig>(RepositoryKind.Player)]`
— without writing a dedicated profile subclass per configuration variant
or falling back to inline `Composer.Create(...)` per test. Done when:
`ComposeAttribute<TProfile, TConfig>` exists in `Compono.XunitV3`, binds
profile configuration arguments positionally to `TConfig`'s single public
constructor (reusing ADR-0022's existing inline-value validation), builds
`TProfile` from that `TConfig` via `TProfile`'s single qualifying
constructor, applies it through the existing `AddProfile(ICompositionProfile)`
core API unchanged, and every constructor-shape/argument-mismatch failure
reported by ADR-0036 is a clear, pre-composition, cached-once diagnostic —
with `trivia-platform`'s `PersistenceAutoData`-shaped pattern working
end-to-end against a packaged `Compono.XunitV3` build as proof.

## Scope

**In scope**, per ADR-0036's Decision Outcome:

- `ComposeAttribute<TProfile, TConfig>` in `Compono.XunitV3`, with
  `TProfile : ICompositionProfile` (no `new()` constraint).
- Positional binding of profile configuration arguments to `TConfig`'s
  constructor, reusing ADR-0022's existing count/nullability/assignability
  validation, retargeted.
- The two new constructor-shape diagnostics (`TConfig` not exactly one
  public constructor; `TProfile` not exactly one public constructor taking
  exactly one `TConfig`-typed parameter) plus the retargeted
  argument-mismatch diagnostic.
- Cached, bounded reflection for both constructor invocations (closed once
  per attribute instance, at binding-plan-cache-construction time),
  mirroring ADR-0022's existing `MakeGenericMethod`/`Delegate.CreateDelegate`
  pattern.
- Documentation across every surface listed in "Documentation tasks"
  below — treated as part of the feature, not a closeout afterthought.
- The published `skills/compono` agent skill, reviewed and updated per
  "Published skill tasks" below — a runtime change isolated to
  `Compono.XunitV3` still changes what an agent should recommend, so the
  skill is in scope even though no skill *code* changes.
- A benchmark-policy evaluation against ADR-0034 — see "Benchmark
  evaluation" below. Benchmarks are added **only if** that evaluation
  finds a real boundary crossed; otherwise the evaluation and its
  reasoning are recorded here, not silently skipped.

**Explicitly deferred** (per ADR-0036's Decision Outcome / "Considered
Options"):

- Option 2's ambient scenario-value/per-row-varying mechanism — shelved,
  no real call site needs it yet.
- Option 3's source-generated per-call-site specialization — rejected,
  disproportionate to the one-time-per-method cost it would save.
- Combining profile configuration arguments with inline test-parameter
  values on the same attribute — `ComposeAttribute<TProfile, TConfig>`
  composes every test-method parameter in full; no evidence yet needs both
  in one row.
- Any actual `trivia-platform` migration work — this plan delivers the
  Compono-side capability only; migrating `trivia-platform` itself is
  separate, future work in that repo.
- Extending `benchmarks/Compono.Benchmarks` to cover `Compono.XunitV3`'s
  attribute-binding cost *in general* (see "Benchmark evaluation" below —
  this is a real, pre-existing gap the evaluation surfaces, but fixing it
  is a separate, appropriately-scoped follow-up, not something to fold
  into a single-feature plan).

## Benchmark evaluation

Per [ADR-0034](../adr/0034-benchmark-suite-strategy-and-redesign.md),
evaluated before deciding whether this plan adds any benchmark:

**Finding: `benchmarks/Compono.Benchmarks` has no `Compono.XunitV3`
coverage at all today.** The project has no `ProjectReference` to
`Compono.XunitV3`, and none of its six categories
(`ImplementationStrategies`/`ConsumerScenarios`/`ExternalComparison`/
`FeatureOverhead`/`Scalability`/`SourceGeneration`) measure attribute
binding-plan construction. This means the *already-shipped*
`[Compose<TProfile>]`'s own bounded, cached `MakeGenericMethod`/
`Delegate.CreateDelegate` construction cost ([ADR-0022](../adr/0022-compono-xunit-package-design.md)'s
"Runtime-Typed `CompositionRow` Invocation") has never been benchmarked
either — this isn't a gap specific to the new attribute.

**Decision: no new benchmark added by this plan.** Two reasons, not one:

1. **No category fits.** ADR-0034's six categories answer questions about
   the composition *engine* (core `Compono` + `Compono.Generators`); none
   is scoped to test-framework-integration attribute-binding cost. Adding
   a benchmark for only the new `ComposeAttribute<TProfile, TConfig>`
   path, with no comparable benchmark for the structurally-identical,
   already-shipped `ComposeAttribute<TProfile>` path, would produce a
   number with nothing to compare it against — not a real "did the
   reflection stay bounded" answer, just an isolated figure.
2. **The property that actually matters is a correctness property, not a
   performance one, and is already covered.** "Does the reflection stay
   bounded to binding-plan construction and never run on the repeated
   `GetData`/composition path" is proven by the Test Plan's
   invoker-delegate-caching assertion (reflection runs exactly once per
   attribute instance across many repeated `GetData` calls) — a unit test
   proves *boundedness*; a microbenchmark would only add a relative-cost
   number on top of a property the test already guarantees.

**Recorded, not silently omitted, per the requirement:** extending
`benchmarks/Compono.Benchmarks` to cover `Compono.XunitV3` attribute
binding-plan construction — covering `[Compose<TProfile>]` and
`[Compose<TProfile, TConfig>]` together, so any future comparison has a
baseline — is a real, legitimate future benchmark-suite gap. It's called
out here and in "Explicitly deferred" above rather than folded into this
plan, because closing it properly means extending ADR-0034's own category
structure (a new category, or a case for why an existing one covers it),
which is a decision for that ADR's own maintainers/a dedicated follow-up,
not something to decide as a side effect of one feature's plan.

## Tasks

**New files**

- [x] `src/Compono.XunitV3/ComposeAttribute{TProfile,TConfig}.cs` — the
      new attribute type.
- [x] `src/Compono.XunitV3/Binding/ConfigProfileBinder.cs` (final name;
      not `ConfigBindingPlan.cs` as originally sketched — see Notes) —
      resolves/validates `TConfig`'s and `TProfile`'s constructors and
      performs the actual binding/construction.

**Changes to existing files**

- [x] `src/Compono.XunitV3/ComposeAttribute.cs` — extracted the existing
      inline-value `params object?[]` normalization (the single-null/
      single-array edge cases) into an `internal static
      NormalizeParamsArguments` helper, reused by the new attribute's
      constructor for its own, separate `configArguments` parameter — a
      behavior-preserving refactor (see Notes), not a change to existing
      binding semantics.
- [x] No change needed to `src/Compono.XunitV3/Binding/BindingPlan.cs` —
      see Notes for why the originally-sketched approach (extending
      `BindingPlan`) turned out unnecessary.

**Documentation tasks** (part of the feature, verified at closeout — see
"Verification and closeout" below, not left implicit):

- [x] `docs/packages/compono-xunitv3.md` — new "Profile configuration
      arguments" section under "What it gives you," cross-linking
      ADR-0036, with the enum/`typeof(...)`/attribute-legal-type guidance
      (no stringly typed examples), and an explicit one-line contrast
      against inline values.
- [x] `docs/migrating-from-autofixture.md` — new "Migrate a parameterized
      custom `AutoDataAttribute`" subsection (before/after, drawn from
      `trivia-platform`'s real `PersistenceAutoData(repositoryName)` shape
      per RESEARCH-0002 Finding 1, enum-based example), plus a "Quick
      concept map" row and a "Migration checklist" line.
- [x] `docs/migrating-from-autofixture.md` — RESEARCH-0002 Finding 2's
      documentation gap closed: `CompositionProviderRequest.Name`-based
      `ICompositionValueProvider` matching added as a documented pattern
      under "Migrate specimen builders," with an explicit note
      distinguishing it from profile configuration arguments.
- [x] `docs/troubleshooting/common-errors.md`, not
      `docs/reference/diagnostics.md` — corrected during implementation
      (see Notes): `diagnostics.md` is scoped exclusively to the
      generator's compile-time `CMP` codes; these three failures are
      runtime, plain-message `CompositionException`s, exactly like
      today's existing inline-value diagnostics, which already live in
      `common-errors.md`'s "By symptom (runtime)" section, not
      `diagnostics.md`. Documented there instead, as a new
      `### "ComposeAttribute<TProfile, TConfig> throws before my test even
      runs"` subsection.
- [x] API reference — regenerated via
      `.github/scripts/generate-api-reference.sh` (DefaultDocumentation,
      per ADR-0032) against a Release build; produced the two new pages
      for `ComposeAttribute<TProfile, TConfig>` plus updates to the two
      existing pages that list/link it, deterministically, with no other
      diff.
- [x] README / package-table / sample surfaces — grepped; `README.md` and
      `docs/packages/index.md`'s one-line package-table cell don't
      enumerate every Compose-family form individually (adding a third
      form to a summary cell would clutter it, not help — the package's
      own guide is the right place, already updated), so left unchanged
      by design; `docs/how-to/use-profiles.md` did enumerate the
      `[Compose<TProfile>]` constraint specifically and got a new
      paragraph pointing to the config form.

**Published skill tasks** (`skills/compono/`, reviewed even though the
runtime change is `Compono.XunitV3`-only — per ADR-0035, one skill, not a
new one per package):

- [x] `skills/compono/references/xunit-v3.md` — `[Compose<TProfile,
      TConfig>]` section added, enum example, explicit distinction from
      `[Compose<TProfile>]` and from `Name`-based provider matching.
- [x] `skills/compono/references/patterns-and-antipatterns.md` — mapping
      table row added; three new antipattern entries added (wrong
      migration moves; stringly typed config args; confusing this feature
      with `Name`-based provider matching).
- [x] `skills/compono/references/registrations-profiles-and-scopes.md` —
      new "Custom providers — matching on request shape, including name"
      section added, closing the pre-existing gap.
- [x] `skills/compono/SKILL.md` — workflow-step bullet added.
- [x] `skills/compono-evals/evals.json` — eval id 19 added (validated as
      well-formed JSON; 19 evals total).
- [x] Confirmed no new skill created — all changes landed inside the
      existing single `skills/compono/` skill.

## Critical Files

- `src/Compono.XunitV3/ComposeAttribute{TProfile,TConfig}.cs` — new, the
  public attribute surface this plan adds.
- `src/Compono.XunitV3/Binding/ConfigProfileBinder.cs` — new, the
  constructor-resolution/validation/construction logic.
- `src/Compono.XunitV3/ComposeAttribute.cs` — modified (pure extraction:
  `NormalizeParamsArguments` pulled out, behavior unchanged); left
  `ComposeAttribute{TProfile}.cs` untouched.
- `test/Compono.XunitV3.Tests/ComposeAttributeConfigBindingTests.cs` — new,
  10 cases.
- `test/Compono.XunitV3.Tests/Fixtures/SampleTestMethods.cs`,
  `test/Compono.XunitV3.Tests/PublicApiSurfaceTests.cs` — modified (new
  fixtures; exact-public-type-set assertion updated for `ComposeAttribute\`2`).
- `test/Compono.XunitV3.SampleTests/ConfigProfileTests.cs` — new,
  packaged-consumer proof (real `dotnet test` run against the packed
  NuGet, per its own csproj's existing pattern).
- `docs/packages/compono-xunitv3.md`, `docs/migrating-from-autofixture.md`,
  `docs/troubleshooting/common-errors.md`, `docs/how-to/use-profiles.md`,
  `docs/reference/api/Compono.XunitV3/*` (regenerated) — documentation
  updates, per `documentation.md`'s "update the subsystem doc in the same
  PR" rule.
- `skills/compono/references/xunit-v3.md`,
  `skills/compono/references/patterns-and-antipatterns.md`,
  `skills/compono/references/registrations-profiles-and-scopes.md`,
  `skills/compono/SKILL.md`, `skills/compono-evals/evals.json` —
  published-skill updates, in scope per ADR-0035.

## Test Plan

**All executed; full solution `dotnet test` (`Compono.slnx`, Debug):
893 passed, 0 failed, 0 skipped** (11 new `Compono.XunitV3.Tests` cases:
10 `ComposeAttributeConfigBindingTests` plus
`ConfigArguments_AreNeverBoundAsInlineValues`, per TFM).
`Compono.XunitV3.SampleTests` (excluded
from `Compono.slnx`/CI by design — see its own csproj comment — run
manually per this plan): 20 passed (10 per TFM, including the two new
`ComposeAttribute<TProfile, TConfig>` passing cases) + 2 pre-existing
`FailingCompositionTests` failures (expected, unrelated) + 2 new
deliberate `ConfigProfileTests` failures (expected, see below), per TFM.

Per `testing.md`'s existing `Compono.XunitV3.Tests` (fast,
direct-`GetData`) / `Compono.XunitV3.SampleTests` (real xUnit v3 runner)
split, matching how ADR-0022's own binding algorithm was verified:

**`test/Compono.XunitV3.Tests`** (direct `GetData` calls, no real runner):

- `TConfig` with exactly one public constructor, valid arguments →
  `TProfile` constructed and applied correctly (assert the registration it
  makes is actually in effect on the resulting `Composer`).
- `TConfig` with zero public constructors → clear, named
  `CompositionException`, cached (not re-thrown with a different message
  on a second `GetData` call on the same attribute instance).
- `TConfig` with more than one public constructor → same, distinct
  message naming the ambiguity.
- `TProfile` with no constructor taking exactly one `TConfig` → clear,
  named `CompositionException`.
- ~~`TProfile` with more than one qualifying constructor~~ — confirmed
  during implementation this is unreachable via ordinary C#: two
  constructors with an identical single-`TConfig`-parameter signature is a
  compiler error (duplicate signature), so no test double can exercise
  this branch; the check itself stays in `ConfigProfileBinder` as
  defensive belt-and-suspenders (see Notes).
- Profile configuration argument count mismatch (too few/too many against
  `TConfig`'s constructor) → reuses the existing pre-composition
  "wrong argument count" message shape, retargeted.
- Profile configuration argument type mismatch (including the existing
  `Nullable<T>`-boxing-unwrap case, proving the reused validation still
  handles it correctly against `TConfig`'s parameters) → reuses the
  existing message shape.
- `null` profile configuration argument for a non-nullable `TConfig`
  parameter → rejected, same as the existing inline-value rule.
- Invoker-delegate caching: `MakeGenericMethod`/constructor-invocation
  reflection runs exactly once per attribute instance across many repeated
  `GetData` calls (same assertion shape ADR-0022's own caching tests use).
- Existing `ComposeAttribute`/`ComposeAttribute<TProfile>` behavior is
  unaffected — a regression check, not new coverage, confirming the new
  type didn't touch the existing binding path.

**`test/Compono.XunitV3.SampleTests`** (real xUnit v3 runner):

- A representative `[Compose<TProfile, TConfig>(...)]` theory, modeled on
  `trivia-platform`'s `PersistenceAutoData(repositoryName)` shape (enum
  argument, per ADR-0036's "no stringly typed configuration" principle),
  run end-to-end against a packaged (not project-referenced)
  `Compono.XunitV3` build — proving generated-plan discovery still reaches
  every type composed inside the resulting profile's `Configure` method,
  the same packaged-consumer verification ADR-0022's own Amendment
  (2026-07-30) required for `[Compose]`-attributed parameters.
- A deliberately-failing case (e.g. a `TConfig` with two public
  constructors) asserted to fail before the test method ever executes,
  with the expected diagnostic text.

**`skills/compono-evals`** — a new eval, shaped exactly as required:

```json
{
  "id": 19,
  "category": "migration",
  "prompt": "Convert this parameterized AutoFixture custom AutoDataAttribute to Compono. It takes a RepositoryKind-shaped argument (currently a string constant) at each call site and configures a different repository customization per call.",
  "expected_output": "Recognizes this as the profile-configuration-arguments pattern: a TConfig record (using an enum, not the original string) paired with a profile via [Compose<TProfile, TConfig>(...)], not a combinatorial set of profile subclasses, a per-test Composer.Create(...) escape hatch, invented ambient/global scenario state, or a recommendation to keep the AutoFixture attribute.",
  "files": [],
  "expectations": [
    "Proposes [Compose<TProfile, TConfig>] specifically, not [Compose<TProfile>] with no way to pass the value, and not a new attribute-per-argument-combination subclass",
    "Uses an enum (or other attribute-legal typed value) for the finite-choice argument, not a magic string, per the no-stringly-typed-configuration principle",
    "Does not suggest ambient/global mutable scenario state, a per-test hand-built Composer.Create(...) as the primary recommendation, or retaining the AutoFixture attribute",
    "Correctly distinguishes this from inline values (which bind to the test method's own parameters) and does not conflate the two"
  ]
}
```

Added to `skills/compono-evals/evals.json`'s existing `evals` array,
following the file's established id/category/prompt/expected_output/
files/expectations shape.

## Verification and closeout

Explicit exit checklist — every item confirmed before this plan moves to
`Done`, not assumed from the Tasks list alone:

- [x] Every new-API example compiles against the **packaged**
      `Compono.XunitV3` surface — `test/Compono.XunitV3.SampleTests`
      references `Compono.XunitV3` via `PackageReference` only (no
      `ProjectReference` anywhere in that project, by design), packed
      fresh from current source via `pack-to-local-feed.sh` on every
      restore; `ConfigProfileTests.cs`'s two passing theories and one
      deliberately-failing theory all ran successfully against that real
      packaged build (see Notes for the exact `dotnet test` output
      confirming the failure's stack trace originates in the packaged
      `Compono.XunitV3.dll`, not a project reference).
- [x] Existing `[Compose]`/`[Compose<TProfile>]` semantics unchanged —
      full solution suite (891 tests) passes; no existing test file's
      assertions were modified, only `PublicApiSurfaceTests.cs`'s exact-set
      list extended (expected, additive) and `ComposeAttribute.cs`'s
      normalization logic extracted with no behavioral change (verified by
      every pre-existing inline-value test still passing unmodified).
- [x] `ConfigArguments_AreNeverBoundAsInlineValues` (new test) proves
      profile configuration arguments never populate the base class's
      `InlineValues` — structurally impossible for them to be
      misinterpreted as inline values, not just untested.
- [x] Benchmark evaluation satisfied by the "no new benchmark, here's why"
      reasoning above — nothing during implementation contradicted it (no
      new hot-path reflection was introduced; the invoker-delegate-caching
      test proves boundedness directly).
- [x] Every documentation file updated (see Tasks above); README/package-table
      grep completed with a documented "left unchanged by design" outcome
      where appropriate.
- [x] Every skill file updated; the new eval (id 19) is well-formed JSON
      in the existing array — running it against the now-updated skill is
      a human/CI eval-harness action outside this coding session's own
      tool access (no `run_eval.py`-equivalent invoked here); the skill
      content itself was written to satisfy every one of the eval's
      stated expectations directly.
- [x] `docs/roadmap/post-mvp.md`, `docs/adr/README.md`,
      `docs/plans/README.md` all reflect final status — reconfirmed below.
- [x] This plan's `Status` set to `Done` — every box above checked.

## Notes

Implementation deviated from this plan's original file-level sketch in
two ways, neither changing the ADR's decision, both narrowing scope in a
good direction:

1. **No `BindingPlan.cs` changes needed at all.** The plan originally
   assumed the new attribute would need to hook into `BindingPlan`'s
   cache-construction pass the way test-method-parameter binding does.
   It doesn't: `ComposeAttribute<TProfile, TConfig>.ApplyProfile` is
   already called exactly once per attribute instance, for free, by the
   *existing* `Lazy<Composer>`-backed `_composer` field the base
   `ComposeAttribute` class already has — `TConfig`/`TProfile` are
   compile-time-closed generic arguments on the attribute class itself,
   not a runtime-discovered `Type` requiring `MakeGenericMethod` the way
   an arbitrary test-method parameter type does. `ConfigProfileBinder`
   uses plain `ConstructorInfo`/`Type.GetConstructors()` reflection
   directly (no `MakeGenericMethod`/`Delegate.CreateDelegate` dance),
   documented as a deliberate, narrower reflection shape in its own XML
   remarks — still bounded to once per attribute instance, just via the
   existing caching mechanism rather than a new one.
2. **`docs/reference/diagnostics.md` was the wrong target** — corrected to
   `docs/troubleshooting/common-errors.md` once the file's actual scope
   (compile-time `CMP` codes only) was checked directly rather than
   assumed from the plan's original hedge.

**Packaged-consumer verification, actual output** (from `dotnet test
test/Compono.XunitV3.SampleTests/Compono.XunitV3.SampleTests.csproj -c
Debug`, both TFMs): `ConfigProfileTests.ComposesTheProfileBuiltFromConfigArguments`
and `.DifferentConfigArguments_ProduceADifferentlyConfiguredProfile` both
pass, proving `RepositoryKind.Player`/`RepositoryKind.Game` produce
differently-configured profiles through the real packaged pipeline.
`ConfigProfileTests.MismatchedProfileConstructorShape_FailsBeforeTheTestExecutes`
fails exactly as designed, with message `'Compono.XunitV3.SampleTests.ProfileWithNoMatchingConstructor'
must have exactly one public constructor accepting a single
'Compono.XunitV3.SampleTests.RepositoryTestConfig' parameter, but has 0.`,
stack-traced through `ConfigProfileBinder.ResolveSingleProfileConstructor`
→ `BuildProfile` → `ComposeAttribute\`2.ApplyProfile` → `BuildComposer` →
`Lazy<Composer>.CreateValue()` → `GetData` — confirming the failure
happens before the test body runs, from inside the packaged assembly.
Full solution `dotnet build`/`dotnet test` (`Compono.slnx`): 0 warnings,
0 errors, 893/893 passed.

**PR #65 review (Codex) caught a real blocking gap this plan's own
verification missed:** `Compono.Generators`' `ComposeMethodDiscovery` was
registered against the non-generic and one-type-parameter
`ComposeAttribute` metadata names only (`ComponoIncrementalGenerator.cs`)
— `ComposeAttribute<TProfile, TConfig>`'s own arity-suffixed metadata name
(`Compono.XunitV3.ComposeAttribute\`2`) was never registered, so a
concrete parameter type reached *only* through
`[Compose<TProfile, TConfig>]` (no other `Create<T>()`/`[Composable]` call
site) got no generated `ICompositionPlan<T>` at all and would fail at
`GetData` time in real usage. This plan's own packaged-consumer sample
(`ConfigProfileTests.cs`) didn't catch it because its only composed
parameter type was a `string` — provider-resolved, never needs a
generated plan — masking the gap exactly the way `testing.md`'s
"verifying a new public entry point" rule warns against. Fixed: a third
`ForAttributeWithMetadataName` registration added for
`ComposeMethodDiscovery.TwoTypeParameterAttributeMetadataName`, merged
into the same `composeMethodResultsAll` pipeline as the other two arities
(`ComponoIncrementalGenerator.cs`); a new isolated
`Compono.Generators.Tests` snapshot test
(`ComposeTwoTypeParameterAttributedMethodParameter_GeneratesCompositionPlan`)
proves a concrete type reached only this way now gets a plan; and
`ConfigProfileTests.cs` was changed to compose a real concrete
`RepositoryConsumer` class (with its own nested `string` dependency
satisfied by the profile's registration) instead of a bare `string`, so
the packaged sample now actually exercises the fixed path instead of
masking it. `docs/roadmap/post-mvp.md` was also corrected in the same
review round — the page's own stated purpose
(`docs/roadmap/index.md`: "not fully available") doesn't allow a shipped,
`Accepted`+`Done` capability to stay listed as an outstanding candidate;
returned to a no-current-candidates state with the historical trail
preserved via ADR-0036/RESEARCH-0002/PLAN-0036 links instead of inline
restatement.

**PR #65's second review round caught two more real gaps, both fixed and
pushed:**

1. **Missing seed on config/profile binder failures.** `ApplyProfile`
   runs while the base class's `Lazy<Composer>` is still being built —
   before `GetData` ever calls `Composer.CreateRow` — so a
   `ConfigProfileBinder` failure had no `CompositionRow`/`row.Seed` to
   read from and escaped without the `"\n\nSeed: ..."` suffix every other
   `Compono.XunitV3`-owned pre-composition failure carries (ADR-0022).
   Fixed: `ApplyProfile` now catches `CompositionException` and rethrows
   via the existing `CompositionException.WithSeedInMessage` helper, using
   `SeedAsNullable` (the attribute's own configured seed) or a freshly
   generated one otherwise — reproducibility isn't actually meaningful for
   this failure category (a constructor-shape mismatch fails identically
   regardless of seed), this is purely about applying the established
   convention consistently.
2. **Abstract `TConfig`/`TProfile` threw the wrong exception type.** An
   abstract class can still declare a public constructor (only a derived
   type can call it) — `ResolveSingleConstructor`/
   `ResolveSingleProfileConstructor` would find it, pass the "exactly one
   constructor" check, and then `ConstructorInfo.Invoke` would throw
   `MemberAccessException` instead of the documented `CompositionException`.
   Fixed: both methods now explicitly reject an abstract type with a named
   `CompositionException`, checked before the constructor-count logic.

Both fixes have dedicated `Compono.XunitV3.Tests` regression coverage
(`GetData_AppendsTheConfiguredSeed_WhenProfileConstructionFailsBeforeARowExists`,
`GetData_AppendsAGeneratedSeed_WhenProfileConstructionFailsWithNoSeedConfigured`,
`GetData_Throws_WhenConfigTypeIsAbstract`,
`GetData_Throws_WhenProfileTypeIsAbstract`) and are documented in
`docs/troubleshooting/common-errors.md`. Full solution: 903/903 passed.

**PR #65's third review round caught two more, both edge cases of the
round-2 fixes rather than newly independent gaps — fixed and pushed:**

1. **A negative configured seed lost to a binder failure.** Round 2's
   catch-block fix used `SeedAsNullable ?? <fresh seed>` unconditionally —
   if `SeedAsNullable` itself was negative (`Seed = -1`) *and* the
   config/profile shape was also invalid, the binder failure reported
   `Seed: -1` instead of the documented negative-seed diagnostic the base
   `GetData` enforces for every other case. Fixed: `ApplyProfile` now
   checks for a negative `SeedAsNullable` first, before attempting any
   config/profile binding, throwing the identical negative-seed message
   the base class uses (`AppendSeed` promoted from `private` to
   `private protected` so both share the exact convention).
2. **`ConstructorInfo.Invoke` wrapping constructor-thrown exceptions.** If
   `TConfig`'s or `TProfile`'s own constructor throws (e.g. custom
   validation logic), reflection wraps that in `TargetInvocationException`
   — `ApplyProfile`'s `catch (CompositionException)` never saw it, so a
   constructor's own actionable exception was replaced by an opaque
   reflection failure with no seed reporting. Fixed:
   `ConfigProfileBinder`'s shared `Invoke` helper unwraps
   `TargetInvocationException` via `ExceptionDispatchInfo.Capture(...).Throw()`
   (preserving the original stack trace), for both the `TConfig` and
   `TProfile` construction call sites.

Regression coverage:
`GetData_ReportsTheNegativeSeedDiagnostic_NotTheBinderFailure_WhenBothApply`,
`GetData_UnwrapsAndReportsTheOriginalException_WhenTheConfigConstructorThrows`,
`GetData_UnwrapsAndReportsTheOriginalException_WhenTheProfileConstructorThrows`.
Full solution: 909/909 passed.

**PR #65's fourth review round — three findings, two fixed, one
deliberately not actioned:**

1. **ADR-0036 needed a dated Amendment, not a silent plan-note
   correction.** This plan's own Notes already recorded that
   `ConfigProfileBinder` uses direct `ConstructorInfo.Invoke`, not the
   cached-delegate (`MakeGenericMethod`/`Delegate.CreateDelegate`) shape
   ADR-0036's "Reflection is bounded and cached" section specified — but
   per this repo's own rule (`design-decisions.md`'s Amendment mechanic),
   a correction to an *already-`Accepted`* ADR's decision detail belongs
   as a dated Amendment on that ADR itself, not only a plan-side note.
   Fixed: added ADR-0036's Amendment 1 (2026-08-09), explaining why the
   simpler direct-invocation shape still satisfies the ADR's actual
   guarantee (bounded to once per attribute instance — via the base
   class's existing `Lazy<Composer>` caching, not a new delegate cache).
2. **Base `ComposeAttribute`'s XML docs and the NuGet package description
   were stale.** `ComposeAttribute`'s remarks still said
   `ComposeAttribute<TProfile>` was "the one designed extension point,"
   and `Compono.XunitV3.csproj`'s `<Description>` listed only
   `[Compose]`/`[Compose<TProfile>]`. Fixed: updated both to describe both
   extension points, and regenerated the API reference
   (`docs/reference/api/Compono.XunitV3/Compono.XunitV3.ComposeAttribute.md`)
   from the corrected XML docs.
3. **Not actioned: "move the `CompositionProviderRequest.Name` migration
   section to a separate PR."** A legitimate scope observation in the
   abstract, but this was a deliberate, explicit instruction from the
   user who commissioned this plan (not an oversight) — the original
   request said, verbatim in spirit, to include RESEARCH-0002 Finding 2's
   documentation gap in this same work "unless there is a strong reason
   to keep it separate," and this plan's own "Documentation tasks"
   section already recorded that no such reason was found. Replied on the
   thread explaining this and resolved it without a code change - the
   scope decision stands as the user directed it, not as this review
   round would have made it unilaterally.

Full solution after round 4: 909/909 passed, 0 warnings, 0 errors.
