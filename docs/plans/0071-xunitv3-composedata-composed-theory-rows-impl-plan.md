# [PLAN-0071] xUnit v3 Composed Theory Rows (`[ComposeData]`, Issue #163)

**Status:** In Progress

**Implements:** [ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md)
(issue [#163](https://github.com/LayeredCraft/compono/issues/163)).
ADR-0070 is **Accepted** (2026-11-05). Implementation may start at B1 once
the product owner says to begin.

## Goal

`Compono.XunitV3` and `Compono.XunitV3.Aot` both ship `[ComposeData]`,
`[ComposeData<TProfile>]`, and `[ComposeData<TProfile, TConfig>]`. Each row
from a public static member (sync or async) is composed at execution time
with its own profile instance, composer, seed, and `[Shared]` scope. Row
values bind leading-positionally, and failures identify the row. JIT and
Native AOT behave identically, with real Native AOT proof. Docs, cookbook,
API reference, and the distributed `compono` skill describe the shipped
API.

## Scope

**In:** everything in ADR-0070's Decision Outcome. That covers the public
API (both packages), the shared runtime helper (linked source), the JIT
implementation, JIT generator discovery, AOT generator discovery,
diagnostics, and emission (including `[Shared]`, row values, `TConfig` per
row, seeds, and row metadata), tests, samples, docs, the skill, and
validation.

**Deferred / out of scope:**

- Member arguments, generic member methods (`Cases<T>()`; rejected on
  both engines: a runtime failure on JIT, `CMP0052` on AOT), and per-row
  seed overrides.
- Dynamic per-derived-class member selection for inherited test methods on
  AOT (see "B4 specification amendment" below: AOT resolves on the
  method's declaring class, as xUnit's own AOT `MemberData` does).
- TUnit, NUnit, and MSTest equivalents.
- AOT `[Compose]` inline values and `[Shared]`. The emission components are
  built reusable but **not** wired into `[Compose]`. Record the follow-up in
  `docs/packages/compono-xunitv3-aot.md`.
- AOT `[Compose]` seed reporting (#166). See the coordination checkpoint in
  B5.
- Any disposal ownership (ADR-0022 Amendment 4 stands).
- Discovery-time enumeration.
- #164, #165, #166 implementation.
- Core `Compono` changes, **except one approved additive API** (ADR-0070
  Amendment 1): a public `CompositionException(string message,
  CompositionDiagnostic? diagnostic, Exception innerException)` constructor so
  the shared helper can build the R0 row-failure wrapper while preserving the
  cause as `InnerException` and the exact `Diagnostic` reference. Any
  *further* core change still stops and raises.

**Release:** ships in the single #161 release with #162–#166. No interim
release.

## Work categories (where each bead sits)

| Category | Beads |
|---|---|
| Public API | B2 (attributes, both packages), B1 (shared helper surface: internal on JIT, `EditorBrowsable(Never)` public on AOT) |
| JIT implementation | B3 |
| Native AOT implementation | B4 (discovery and diagnostics), B5 (emission) |
| Tests | Every implementation bead carries its own tests. B6 adds real-runner, sample, and Native AOT tests |
| Documentation | B7 |
| Skill (mandatory) | B8 |
| Validation | B9 (plus per-bead gates) |

## Phases and PRs

**One cohesive feature PR** (product-owner decision, review round 1). The
work happens on one feature branch, `feat/163-composedata`, which the B1
implementer creates from `main` after ADR-0070 is `Accepted`. A single PR
covers JIT, AOT, tests, docs, and the mandatory skill update. **No partial
feature is merged to `main`**: no JIT-only, AOT-only, or docs-less PR.

The phases below are **internal checkpoints**, not PR boundaries. Each
bead must leave the branch building with 0 warnings and 0 errors and with
all tests green, and is reviewed at its checkpoint (local commits are fine,
but only when the product owner approves committing):

- **Phase 1 checkpoint: shared helper, public API, JIT.** B1 → B2 → B3.
- **Phase 2 checkpoint: Native AOT.** B4 → B5 → B6.
- **Phase 3 checkpoint: docs, skill, validation.** B7 → B8 → B9. The PR
  opens only after B9, and only with product-owner approval.

This intentionally relaxes the repo's phase-per-PR default for this
feature: the phases aren't independently shippable, because a JIT-only
`ComposeData` would break the parity promise. Nothing is released until
the whole #161 set is done.

## Specification details

### Shared helper (`src/Shared/Compono.XunitV3/ComposeDataRows.cs`)

One file linked into both projects, like `FixtureContextExtensions.cs`. It
uses `#if COMPONO_XUNITV3_AOT` for the namespace and for accessibility:
`internal static class` on JIT, `public static class` with
`[EditorBrowsable(EditorBrowsableState.Never)]` on every member on AOT
(ADR-0058). It **never composes**. The names below are suggestions, but the
responsibilities and messages are normative.

- **Collection classification** (ADR-0070 item 3, RESEARCH-0038 §2.7):
  the awaited member result must be an `IEnumerable` other than `string`,
  or an `IAsyncEnumerable<T>` (the async loop itself lives in the JIT code
  or the generated code, since the helper is non-generic over the
  collection). Anything else is D4. `null` is D5.
- **Row normalization:** take `object? row`. Return the values array and an
  optional source `ITheoryDataRow`. Classify in this order: `ITheoryDataRow`
  (`GetData()`), then `object?[]` (as-is), then `ITuple` (indexer, which
  flattens 8+ elements). `null` or any other type is a row error. No
  reflection.
- **Config extraction (`TConfig` form):** require element 0 to be present,
  non-null, and `is TConfig`. The values become elements 1..n.
- **Value validation:** `T BindValue<T>(object?[] values, int index, bool
  parameterIsNullable, string parameterName, …)`. This uses the same rule
  and the same message text as `PositionalArgumentBinder`/`ComposeAttribute`
  (null-not-allowed, type mismatch). It's generic over the closed parameter
  type, so it's AOT-safe. Values arrive boxed (`Nullable<T>` as `T`/`null`).
  There's no widening or `TypeHelper` conversion. A reference parameter in
  an oblivious nullable context is non-nullable on both engines: the JIT
  rule in `BindingPlan.GetNullability`, and on AOT the generator maps
  `NullableAnnotation.None` to non-nullable. The JIT path may keep using
  `PositionalArgumentBinder` directly, but must produce identical text.
- **Metadata merge (single rule, ADR-0070 "Row and attribute metadata"):**
  the input is the source row's metadata plus the attribute's metadata.
  Skip is **grouped**: the row's `(Skip, SkipType, SkipUnless, SkipWhen)` if
  `row.Skip != null`, else the attribute's group if `attr.Skip != null`,
  else none. `Label`/`TestDisplayName`/`Timeout`/`Explicit`/
  `DisableParallelization` are row ?? attribute. `Traits` is row ∪ attribute
  pairs ∪ `Compono.Seed`. Engine-specific types sit behind `#if`. On JIT,
  conditions are `string` names and `SkipType` is passed through. On AOT,
  conditions are `Func<bool>` (the attribute-level ones are rendered by the
  generator as `() => global::T.Prop` on `SkipType ?? test class`).
  **Don't** route through xUnit's `ConvertDataRow` (JIT, which drops row
  conditions) or `DataAttributeRegistration.CreateDataRow` (AOT, which
  merges field by field).
- **Output row:** `new TheoryDataRow(args)` with the merged metadata.
  JIT's `DisableParallelization` (4.x-only, and the package compiles against
  3.2.2) is read from the source row and set on the output row through
  cached `PropertyInfo`s (resolved once per process, null on 3.2.2, where
  the step is a no-op). It's narrowly scoped to this one property in
  `Compono.XunitV3`, and approved (RESEARCH-0038 §9.2 S1).
- **B5 additions (ADR-0070 Amendment 4 §3, PLAN B5.5):**
  `MemberReadFailure`, `NegativeAttributeSeed`, and `NegativeRowSeed`
  (shared; the JIT attribute switches to them), plus the AOT-only
  `ResolveGenericTestClassType`. They're the only helper members B5 adds.
- **Failure construction:** wrap the cause into `CompositionException`,
  keeping `InnerException` = the cause and `Diagnostic` copied when the
  cause is a `CompositionException` (reuse
  `CompositionException.WithSeedInMessage` semantics), with the formats
  below.

### Messages (byte-identical on both engines, except that single-sentence compile-time diagnostics omit the trailing period per RS1032 - see the `RS1032` note under the diagnostics table; `{M}` = `Namespace.Type.Member`, `{T}` = test method display name as `BindingPlan.MethodDisplayName`)

- **D1, member not found:** `ComposeData member '{M}' for '{T}' was not found. Expected a public static field, property, or parameterless method.`
- **D2, not public static:** `ComposeData member '{M}' for '{T}' must be public and static`
- **D3, needs arguments:** `ComposeData member '{M}' for '{T}' requires arguments, which ComposeData does not support`
- **D4, unsupported return:** `ComposeData member '{M}' for '{T}' returned unsupported type '{type}'. Expected IEnumerable, Task/ValueTask of IEnumerable, or IAsyncEnumerable of ITheoryDataRow, object?[], or ITuple rows.`
- **D5, null result:** `ComposeData member '{M}' for '{T}' returned null.`
- **D6, no rows:** let xUnit's own "No data found" behavior stand. Don't
  invent a Compono error.
- **R0, row wrapper:** `ComposeData row {i}{ [label]} from '{M}' for '{T}' failed: {cause message}`. `{ [label]}` is ` [<label>]` when the
  source row has a non-null label, otherwise empty. The text then ends
  with a seed line per the seed rule below.
- **Row causes** (the `{cause message}` inside R0): `row is null`;
  `row has unsupported type '{type}'; expected ITheoryDataRow, object?[], or ITuple`;
  `the first row element must be a non-null '{TConfig}' profile configuration value` (null, missing, or wrong type);
  `too many values: {n} supplied, but the method has {p} parameter(s)`;
  the existing inline null and type-mismatch texts (with "Inline value"
  replaced by "Row value"); a pipeline `CompositionException` message; or a
  profile constructor or `Configure` exception message.
- **Seed rule (ADR-0070 "Effective seed"; never fabricated):**
  - D1–D5 (member level): **no** seed line.
  - R0 before `CreateRow` (row shape, `TConfig`, profile constructor,
    `Configure`, `Composer.Create` including a duplicate-seed conflict):
    `\n\nSeed: {Seed}` if attribute `Seed` is set, otherwise
    `\n\nSeed: not assigned (the failure occurred before the row's composition started)`.
  - R0 from `CreateRow` on: `\n\nSeed: {row.Seed}`.
  - Negative attribute `Seed`: the existing negative-seed message, checked
    before the member is read. A negative profile seed is caught by the
    `row.Seed < 0` check right after `CreateRow` and wrapped in R0.
  - Per-row order: normalize the row, extract `TConfig`, build the profile
    and `Composer`, `CreateRow`, negative check, value validation,
    `[Shared]`, then the rest.
  - Attribute `Seed` plus profile `WithSeed` is core's
    `DuplicateConfigurationOption` error (no override). Assert it in tests.

### Diagnostics (AOT, `CMP0051`–`CMP0057`, added to `AnalyzerReleases.Unshipped.md`)

Numbering is final (B4 specification amendment, 2026-11-06). Every ID is
`Error`, category `Compono.Usage`. Each new ID has a JIT runtime counterpart
for the same condition (D1–D4/R0, or JIT's member-read/ambiguity
`CompositionException`). `{0}` is the lookup type's display name plus
`.Member`; `{1}` is the test method's display name (the existing AOT
`methodDisplayName` convention: compile-time messages use Roslyn display
names; only the runtime R0 text must be CLR-style, see B4.3).

| ID | Condition | Message text (one descriptor per ID; the reason varies) |
|---|---|---|
| `CMP0051` | member not found; member ambiguous | `ComposeData member '{0}' for '{1}' was not found. Expected a public static field, property, or parameterless method.` / `ComposeData member '{0}' for '{1}' is ambiguous: more than one inherited or hidden static member matches. Set MemberType to disambiguate, or rename the member.` (JIT's texts) |
| `CMP0052` | not public static; requires arguments; generic member method | `ComposeData member '{0}' for '{1}' must be public and static` / `… requires arguments, which ComposeData does not support` / `… is a generic method; ComposeData cannot supply type arguments` |
| `CMP0053` | unsupported return type or row element type | `ComposeData member '{0}' for '{1}' has unsupported {return/row element} type '{2}'. Expected IEnumerable, Task/ValueTask of IEnumerable, or IAsyncEnumerable of ITheoryDataRow, object?[], or ITuple rows.` |
| `CMP0054` | open-generic `MemberType` | `ComposeData MemberType '{0}' for '{1}' is an open generic type; supply a closed type` |
| `CMP0055` | `TConfig` statically incompatible with the typed row's element 0 | `ComposeData member '{0}' for '{1}': row element 0 type '{2}' can never be the '{3}' profile configuration value` |
| `CMP0056` | statically known too many values; a typed row value that can never be assigned to its parameter | `ComposeData member '{0}' for '{1}' supplies {n} row value(s), but the method has {p} parameter(s)` / `ComposeData member '{0}' for '{1}': row value {i} type '{2}' can never be assigned to parameter '{name}' of type '{3}'` |
| `CMP0057` | invalid **active** attribute-level skip condition (B4.9) | `[ComposeData…] on '{1}' sets both SkipUnless and SkipWhen` / `[ComposeData…] on '{1}': {SkipUnless/SkipWhen} '{prop}' must be a public static bool property on '{holder}' or one of its base types` |

> **`RS1032` note:** single-sentence diagnostic messages omit the trailing period (all three `CMP0052` texts, and likewise `CMP0054`, `CMP0055`, and both `CMP0056`/`CMP0057` texts). Multi-sentence texts (`CMP0051`, `CMP0053`) keep it. This is the only intentional deviation from the JIT runtime texts, which append a period.

Reused IDs, through **twin `DiagnosticDescriptor`s** (same ID, severity,
and category as the existing descriptor, with ComposeData-specific
wording; the existing `[Compose]` descriptors, their message formats, and
their 18 existing Verify snapshots stay byte-identical, product-owner
decision C-3):

- `CMP0040` (twin): the existing signature rules (generic test method,
  ref/out/in, `params`, a non-composable parameter shape), plus **duplicate
  `[Shared]` parameter types** (C5; same rule and reason text as JIT
  `BindingPlan`). No new ID.
- `CMP0042`, `CMP0046`, `CMP0047`, `CMP0048` (twins): **`TProfile`
  constructor only**, for `ComposeData<TProfile, TConfig>`.
- `CMP0044` (twin): `TProfile`, `TConfig`, the **member holder types**
  (lookup type and the member's containing type), and the **active
  `SkipType`/skip-condition holder**, whenever not accessible from the
  generated top-level registration (C-2; JIT accepts private nested
  holders, xUnit's own AOT `MemberData` silently drops them, Compono
  reports instead).
- `CMP0049` (twin): `ComposeData<TProfile>`'s parameterless constructor.
- `CMP0045` (twin): used whenever at least one `ComposeData` form is among
  the stacked attributes; names all six forms (JIT `BindingPlan` text).
  `[Compose]`-only stacking keeps the existing descriptor and text.
- **Never** `CMP0041` or `CMP0043` for `ComposeData`: `TConfig` comes from
  the row and is never constructed by generated code (C2).

**B5 additions (ADR-0070 Amendment 4 §2, exact text in B5.4):** more
`ComposeData` twins:

- `CMP0054`: a member or active skip holder declared on an open generic
  type
- `CMP0052`: a static abstract or static virtual interface member
- `CMP0047`: a prohibited member, getter, skip property, or skip getter

`CMP0053` (existing descriptor) also covers a member type whose
`IAsyncEnumerable<T>` implementations give no deterministic JIT choice
(B5.4).

Statically unknown row element types (`object?[]`, `ITheoryDataRow`,
`ITuple`, `object`, non-unique enumerable element types) are validated at
runtime through the shared helper.

## Tasks

Execution is sequential per phase. Bead IDs are in the local beads database
(epic `compono-lsx`). Every bead ends with V1 green (0 warnings / 0 errors,
all tests passing) unless stated otherwise.

Dependency graph: B0 → B1 → B2 → {B3, B4}. B4 + B1 → B5. B5 + B3 → B6.
B3 + B6 → B7 → B8 → B9. After B2, B3 (JIT) and B4–B5 (AOT) can proceed in
parallel in separate worktrees if wanted. Sequential in one tree is the
default.

### Gate

#### B0 (`compono-lsx.10`): product-owner review

- [x] ADR-0070 reviewed and flipped to `Accepted` (2026-11-05)
- [x] RESEARCH-0038 §10 open items resolved and recorded here in Notes (JIT row-level `DisableParallelization` approach, theory-level skip edge handling, strict value typing, #166 ordering)

### Phase 1: shared helper, public API, JIT

#### B1 (`compono-lsx.1`, after B0): shared runtime helper, linked into both packages

- [x] `src/Shared/Compono.XunitV3/ComposeDataRows.cs` per the specification, with full XML docs
- [x] Link it in both `.csproj`s (`<Compile Include Link>`). Confirm `COMPONO_XUNITV3_AOT` is already defined in the AOT project
- [x] Unit tests, `test/Compono.XunitV3.Tests/ComposeDataRowsTests.cs`:
  - collection: `TheoryData<…>`, `TheoryDataRow<…>[]`, `List<object?[]>`, `object[]` of `object[]`, `(T1,T2)[]`; rejected: `string`, a single `TheoryDataRow`, `object[]` of scalars, non-enumerable, `null`
  - row: `ITheoryDataRow` before `object?[]` before `ITuple` (a type that's both `ITheoryDataRow` and `ITuple` classifies as `ITheoryDataRow`); 9-element `ValueTuple` flattened; nested array value stays one value; `null` row
  - values: `int?` row → `int?` param (value and `null`); `null` → `int` param (error); boxed `int` → `long` param (error, no widening); `string?` vs `string` vs oblivious reference param; `DateTimeOffset`; subclass instance → base-type param (OK)
  - config extraction: missing, `null`, wrong type, struct `TConfig`
  - metadata merge: the full grouped-skip matrix (row Skip ± condition × attribute Skip ± condition, including the `SkipType` passthrough on JIT), row ?? attribute for Label/TestDisplayName/Timeout/Explicit, trait union plus `Compono.Seed`
  - messages: exact D1–D5/R0 text; every seed-line variant (none, `Seed: n`, `not assigned`, `row.Seed`); `InnerException`/`Diagnostic` preservation
- [x] Mirror a minimal subset in `test/Compono.XunitV3.Aot.Tests` to prove the AOT compilation (public accessibility, `EditorBrowsable`)

#### B2 (`compono-lsx.2`, after B1): public attribute API, both packages

- [x] `src/Compono.XunitV3/ComposeDataAttribute.cs`, `ComposeDataAttribute{TProfile}.cs`, `ComposeDataAttribute{TProfile,TConfig}.cs`: shapes per ADR-0070, with `DynamicallyAccessedMembers` annotations and XML docs. The `GetData` body lands in B3. Until then, throw `NotSupportedException` with a clear message (never released)
- [x] `src/Compono.XunitV3.Aot/ComposeData*.cs`: sealed, independent markers, same public shapes
- [x] **Correction during B4 (ADR-0070 Amendment 3):** B2's AOT marker set omitted the `[Shared]` parameter marker. `src/Compono.XunitV3.Aot/SharedAttribute.cs` was added under B4 with the same public contract as the JIT marker, plus the public-API surface test, a focused marker contract test, and the package docs. Actual AOT `[Shared]` execution remains B5's (see B5)
- [x] `PublicApiSurfaceTests` exact-set updates (JIT, and AOT if present, or add an equivalent)
- [x] Package `<Description>` updates in both `.csproj`s
- [x] Package validation / ApiCompat: an additive-only check against the previous release baseline (`-p:PackageValidationBaselineVersion=<prior>`)

#### B3 (`compono-lsx.3`, after B2): JIT implementation

- [x] `ComposeDataAttribute.GetData`: cached per attribute (`LazyInitializer`) member accessor (reflection: static field, property, or parameterless method on `MemberType ?? testMethod.ReflectedType ?? DeclaringType`, `FlattenHierarchy`) and cached `BindingPlan`. Per call: invoke the member, await `Task`/`ValueTask`, enumerate sync or `IAsyncEnumerable` (with `TestContext.Current.CancellationToken`, checked between rows). Per row: normalize, validate, build a fresh profile/composer (`ConfigProfileBinder.BuildProfile` with a constructor lookup cached once), `CreateRow`, negative-seed check, `[Shared]` first, then the rest, then the output row with the seed trait. Wrap failures per R0/D1–D5
- [x] `SupportsDiscoveryEnumeration() => false`
- [x] Preserve B2's `MemberName` constructor null guard, and fail clearly for a consumer subclass of `ComposeDataAttribute` that is not one of the three supported forms (B2 review finding)
- [x] `BindingPlan` exclusivity: count `ComposeAttribute` **and** `ComposeDataAttribute`. Update the message to name all six forms
- [x] Metadata: build output rows through the shared merge. Pass attribute values from this attribute instance (`ExplicitAsNullable`/`TimeoutAsNullable` are accessible as protected members). Implement JIT `DisableParallelization` through the approved cached-reflection lookup. Test it on xUnit 4.x (honored) and on 3.2.2 (V5: no failure, no-op)
- [x] `ComposeMethodDiscovery` (generator): add `Compono.XunitV3.ComposeDataAttribute` plus the `` `1 ``/`` `2 `` metadata names, so parameter types get plans and row invokers
- [x] Tests (`test/Compono.XunitV3.Tests/ComposeData*Tests.cs`; GetData-level plus real-runner):
  - multiple rows, each distinct seed/trait, values bound, rest composed
  - per-row `TConfig` (`DateTimeOffset`) reaches the profile, with no cross-row leakage (profile instance and `Configure` per row, asserted with an instance counter)
  - `[Shared]` with a row-supplied value and with a composed value; shared ordering
  - nullable params; too many values; type mismatch; null row; unsupported row; config null or wrong type
  - sync/`Task`/`ValueTask`/`IAsyncEnumerable` members; `MemberType`; inherited static member
  - fixture-dependent values: class and collection fixture through `RequireFixture` in a profile factory, and inside the member
  - parallel isolation: several classes in parallel, each with its own fixture, rows composed independently
  - seeds: fresh distinct per row by default; trait equals the seed actually used; attribute `Seed` pins all rows; profile `WithSeed` pins all rows; attribute `Seed` + profile `WithSeed` → core duplicate-configuration error (no override); negative attribute seed rejected before the member is read; negative profile seed rejected through R0; seed-line variants for pre-`CreateRow` failures (`Configure` throws with and without attribute `Seed`, which must never print a random seed), member-level failures (no seed line), post-`CreateRow` failures (`row.Seed`, then re-run with `Seed = that` reproduces the same composed values)
  - metadata, GetData level (asserting the exact `ITheoryDataRow` the runner consumes): attribute `TestDisplayName`; row/attribute trait union; `Timeout`; `DisableParallelization` (row and attribute, on 4.x); attribute `SkipUnless`/`SkipWhen`/`SkipType` merge; grouped-skip precedence. **Product-owner decision (2026-11-05): for B3 these are sufficient at the GetData level — no runner-level verification is claimed for them.**
  - metadata, real runner: label gives a deterministic display name; row `Skip`; attribute `Skip`; row-level and attribute-level conditional `SkipUnless`/`SkipWhen` (true and false) including `SkipType` referencing a different condition-holder class; grouped-skip precedence (an unconditional row `Skip` overrides an attribute conditional skip); theory-level `[Theory(Skip = …, SkipUnless/SkipWhen = …)]` interaction with unconditional row `Skip`; `Explicit`
  - shapes end to end: one theory per collection/row shape from B1
  - mixed `[ComposeData]` + `[InlineData]` on one method: InlineData rows uncomposed and independent
  - exclusivity: `[Compose]` + `[ComposeData]` → `CompositionException`
  - failure in row k: message contains member, index, label, seed; `InnerException` preserved; theory fails as a unit
  - existing `[Compose]` tests unchanged and green (regression)
  - generator test: `ComposeData` methods discovered (row-invoker registration emitted)

### Phase 2: Native AOT

#### B4 (`compono-lsx.4`, after B2): AOT discovery and diagnostics

B4 is **discovery and validation only**. It emits no `ComposeData`
execution source. The contract below is the B4 specification amendment
(2026-11-06, product-owner decisions C1–C6 and C-1–C-4; evidence: a
reflection probe that reproduces JIT `BuildMemberAccessor` exactly, and
decompiled `xunit.v3.core.aot` 4.0.2 plus its generators, recorded in
Notes).

**B4.1 Two separate pipelines (raw discovery vs. validated methods).**

- *Plan generation (raw, unvalidated):* three `ComposeMethodDiscovery`
  providers for `Compono.XunitV3.Aot.ComposeDataAttribute`, `` `1 ``, and
  `` `2 ``, using the existing attribute-family-agnostic
  `ComposeMethodDiscovery.TransformMethod`, folded into
  `composeMethodResultsAll` exactly like the AOT `[Compose]` and JIT
  `ComposeData` providers. **Rule (the existing generator behavior, kept
  unchanged):** every eligible parameter type of every attributed method
  gets its plan and row-invoker registration **whether or not the method
  passes ComposeData validation**, because plans and row invokers are
  type-keyed, deduplicated across the compilation, and independent of any
  one method (AOT `[Compose]` behaves the same way today with `CMP0040`–
  `CMP0049`). Ref/out/in and `params` parameters and non-eligible shapes are
  skipped, as today. This pipeline's own diagnostics (for example `CMP0013`)
  are unchanged and may appear alongside a ComposeData diagnostic. A
  ComposeData diagnostic never removes a plan another method needs.
- *Validated ComposeData methods:* three per-method providers (one per
  metadata name) that produce the B4.2 model. A method with **any**
  diagnostic gets **no** `ComposeData` execution registration (B5 must
  check `Diagnostics.Count == 0`, as the AOT `[Compose]` output step does).
  In B4 no registration is emitted for any method.

**B4.2 Model** (`AotComposeDataMethodInfo`, a `sealed record`; strings,
`bool`s, `int?`s, and `EquatableArray<T>` only; no `ISymbol`,
`Location`, or `Compilation`):

- form (arity 0/1/2); `TProfile` and `TConfig` fully qualified names
- `TestDeclaringTypeFqn`: the method's containing type (registration key
  and `CreateRow` type, as AOT `[Compose]` and JIT `CreateRow` use)
- `LookupTypeFqn`: `MemberType ?? TestDeclaringType`
- `MemberContainingTypeFqn`: the member's own containing type (the target
  of the generated call; never the lookup type, see B4.4)
- `MemberName`, `MethodName`
- member kind (`Field`/`Property`/`Method`); return shape (`Sync`,
  `TaskOf`, `ValueTaskOf`, `AsyncEnumerable`)
- row typing: `Typed` with the element type FQNs in order, or `Untyped`
- parameters: `Name`, `Fqn`, `Ordinal`, `IsNullable` (the existing
  `AotComposeMethodDiscovery.IsNullable` rule: oblivious reference =
  non-nullable), `IsShared`
- `int? Seed`: non-null only when the `Seed` named argument is present. An
  explicit `Seed = 0` is `0`; a negative value is stored as is (runtime
  rejection, no compile-time diagnostic, B2 review finding)
- attribute metadata, **presence-tracked**: `Label`, `Skip`,
  `TestDisplayName` (`string?`), `Explicit` (`bool?`), `Timeout`
  (`int?`), `Traits` (`null` when absent or empty, matching JIT),
  `DisableParallelization` (`bool?`, raw presence; B5 decides rendering)
- active skip conditions only (B4.9): `SkipUnlessHolderFqn` +
  `SkipUnlessProperty`, `SkipWhenHolderFqn` + `SkipWhenProperty`
- `Diagnostics` (`EquatableArray<DiagnosticInfo>`)

**B4.3 `{M}`/`{T}` names.** B4 stores only the FQNs above. B5 renders the
runtime R0 names on the failure path as
`typeof(<Lookup>).FullName + "." + MemberName` and
`typeof(<TestDeclaring>).FullName + "." + MethodName`. That reproduces JIT's
CLR-style text exactly (including `+` for nested types and the
assembly-qualified closed-generic form Roslyn can't produce statically).
B6 verifies it under Native AOT. Compile-time CMP messages use Roslyn
display names, as every existing AOT diagnostic does.

**B4.4 Member resolution.** It copies JIT reflection, pinned by the probe
table in Notes (rows 01–29).

- **Lookup type:** `MemberType` when present; otherwise the test method's
  **declaring class** (C-1). JIT defaults to the *concrete reflected* test
  class instead, so an inherited test method whose member is declared only
  on, or hidden by, a derived class differs between engines. xUnit's AOT
  runtime keys data factories by the declaring type and gives the factory
  no derived-class identity, which is exactly how xUnit's own AOT
  `MemberData` behaves. Setting `MemberType` names one **fixed** holder
  class for every derived class. It does **not** reproduce JIT's
  per-derived-class selection. It's the workaround only where a fixed
  holder is appropriate. (ADR-0070 Amendment 2 §1.)
- **Order:** fields, then properties, then parameterless methods, each
  across the whole base-type chain. Interfaces' base interfaces are not
  walked.
  - *Fields:* the most-derived public static field named `MemberName`.
  - *Properties:* public static, non-indexer properties named `MemberName`.
    A more-derived property of the **identical** type
    (`SymbolEqualityComparer.Default`) hides a base one. Two or more
    remaining → **ambiguous**. If the selected property's getter isn't
    public, fall through to methods.
  - *Methods:* the most-derived type that declares any public static
    zero-parameter method named `MemberName` (generic or not). Two or more
    in that type → **ambiguous** (for example `Cases<T>()` + `Cases()`).
    A single generic method → `CMP0052` generic method (C-2).
  - Instance members and non-public members never take part in selection.
- **Failure classification** (when nothing was selected):
  - no member named `MemberName` on the lookup type itself, and none that
    is non-private on a base type → **not found** (`CMP0051`; a base
    class's private members are invisible, as in reflection)
  - otherwise, any method candidate with one or more parameters (including
    optional-only and `params`-only) → **requires arguments** (`CMP0052`)
  - otherwise → **not public static** (`CMP0052`; includes `internal`,
    `protected`, instance members, and a non-public getter)
- `MemberName` null or empty → `CMP0051` not found. An open-generic
  `MemberType` → `CMP0054`.
- **Generated-call target** is `MemberContainingTypeFqn`. Probe rows
  10/11/21 show that `global::<Lookup>.Member` can bind to a different
  member (a derived method group or instance member).

**B4.5 Return shape** (from the member's **declared** type):

- `IAsyncEnumerable<T>` (or a type implementing it) is checked first, so it
  wins over `IEnumerable`
- non-generic `Task`/`ValueTask` → `CMP0053`
- `Task<X>`/`ValueTask<X>`: `CMP0053` only when `X` can provably never be a
  non-`string` `IEnumerable` (`string`, or a sealed or value type that
  doesn't implement `IEnumerable`). Otherwise it's valid and checked at
  runtime (`Task<object>` is valid)
- sync: the declared type must implement `IEnumerable` and not be
  `string`. A declared `object` → `CMP0053` (JIT rejects the declared type
  when building the accessor)

**B4.6 Element types.**

- *Typed* rows are a `TheoryData<…>` collection (walking base types, so
  subclasses count) or a collection whose unique `IEnumerable<T>`/
  `IAsyncEnumerable<T>` element is `TheoryDataRow<…>`. The element FQNs
  are the generic arguments.
- Otherwise the element type is the unique `IEnumerable<T>`/
  `IAsyncEnumerable<T>` element, or *untyped* when there isn't one.
- `CMP0053` (row element) only when the element type is provably never
  `ITheoryDataRow`, `object?[]`, or `ITuple`: a sealed or value type that
  implements none of them. A reference-type array (`string[]`) **is** an
  `object?[]` row (array covariance); `int[]` is not.

**B4.7 Static assignability** (C1, typed rows only):

- Unwrap `Nullable<T>` on **both** sides, then require identity, an
  implicit or explicit reference conversion, boxing, or unboxing
  (`Compilation.ClassifyConversion` on the unwrapped types, ignoring
  nullable annotations). Otherwise `CMP0056` (value) or `CMP0055`
  (`TConfig` vs element 0).
- Too many values: typed arity (minus 1 for the `TConfig` form) greater
  than the parameter count → `CMP0056`. Fewer is fine; the remaining
  parameters are composed.
- Null values, and all untyped rows, are checked at runtime (shared
  helper).

**B4.8 Profile checks** (C2, twins per "Diagnostics"):

- `ComposeData<TProfile>`: `CMP0044` (`TProfile`), `CMP0049`.
- `ComposeData<TProfile, TConfig>`: `CMP0042` (exactly one usable public
  `TProfile(TConfig)` constructor, the existing rules), `CMP0044`
  (`TProfile` and `TConfig`), then `CMP0046`/`CMP0047`/`CMP0048` on the
  **`TProfile` constructor only**. Never `CMP0041`/`CMP0043`. `TConfig`
  may be any type (`DateTimeOffset`, an interface, a record with several
  constructors).

**B4.9 Skip conditions** (C4, option A; matches the shared helper's grouped
merge, `ComposeDataRows.CreateDataRow`):

- A condition is **active** only when the attribute's `Skip` named
  argument is present and non-null (`""` counts as set, matching the
  helper's `is { }`).
- For each active `SkipUnless`/`SkipWhen`: walk `SkipType ?? test declaring
  class` and its base types for a property declared `Public`, `static`, of
  type `bool` (xUnit's own AOT `TestMethodDetails` rule). Both set, or not
  found / not static / not public / not `bool` → `CMP0057`. An inaccessible
  holder → `CMP0044`.
- Inactive conditions (no attribute `Skip`) produce no diagnostic and are
  **not recorded**, so B5 can never render a reference to a nonexistent
  property. `SkipType` without a condition is inert.
- Accepted residual (C-4): when the attribute `Skip` is set but every row
  sets its own `Skip`, or the member returns no rows, JIT never evaluates
  the attribute's conditions while AOT still reports `CMP0057`.

**B4.10 `[Shared]`** (C5): a parameter's marker is the AOT
`Compono.XunitV3.Aot.SharedAttribute`, matched on that fully qualified
metadata name (ADR-0070 Amendment 3 - a minimal public marker added under
this phase because B2 omitted it; identical public shape to the JIT
`Compono.XunitV3.SharedAttribute`). Two or more `IsShared` parameters whose
types share the same CLR identity - `Compilation.ClassifyConversion(a.Type,
b.Type).IsIdentity` - are a duplicate. That comparison erases
nullable-reference annotations (`string`/`string?` are identical), treats
`dynamic` as `object`, and erases tuple element names, while keeping a
nullable value type distinct from its underlying type (`Box<int?>` is not
`Box<int>`) → `CMP0040` (twin) with JIT's reason text.
B4 discovers and validates the marker only; the generated sharing behavior
is B5's.

**B4.11 Diagnostic location and precedence.**

- Every B4 diagnostic is reported at the test method's location
  (`LocationInfo.From(method)`, as `CMP0040`–`CMP0049` are).
- **First failure wins**, so at most one ComposeData diagnostic per method,
  except the existing per-pipeline duplicate of `CMP0045`. The fixed order:
  1. `CMP0045` (more than one of the six Compose-family forms; both the
     `[Compose]` and `ComposeData` per-method pipelines count all six forms
     and both suppress their registration)
  2. `CMP0040` (method signature, then duplicate `[Shared]`)
  3. profile, in the existing helper's order with the `TConfig`
     constructor gates removed. `ComposeData<TProfile>`: `CMP0044`
     (`TProfile`), then `CMP0049`. `ComposeData<TProfile, TConfig>`:
     `CMP0044` (`TConfig`), `CMP0042`, `CMP0044` (`TProfile`), `CMP0046`,
     `CMP0047`, `CMP0048` (the last three on the `TProfile` constructor)
  4. `CMP0054` (open-generic `MemberType`)
  5. `CMP0051` / `CMP0052` (member resolution, B4.4)
  6. `CMP0044` (member holder types)
  7. `CMP0053` (return shape, then element type)
  8. `CMP0055`, then `CMP0056` (count before per-value type)
  9. `CMP0057`, then `CMP0044` (skip holder)
- Suppression applies only to the ComposeData execution registration. The
  raw plan-generation pipeline (B4.1) is never suppressed.

**B4.12 Out of B4 scope:** all emission (B5), `{M}`/`{T}` rendering (B5),
`DisableParallelization` rendering (B5), negative-seed message placement
(B5), `CMP0050` emission isolation (B5), the #166 checkpoint (B5), and every
runtime and cross-engine parity check (B6).

**B4 acceptance checklist**

- [x] Plan pipeline: the three `ComposeMethodDiscovery` AOT `ComposeData` providers exist. A composed custom parameter type of an AOT `[ComposeData]` method gets a generated plan, **including when the method fails validation**. A plan for a type shared with a valid `[Compose]` method is still emitted when the `ComposeData` method is invalid
- [x] A JIT-namespace `[Compono.XunitV3.ComposeData("Missing")]` method (the `SeedSampleMethods` shape) produces no `CMP0051`–`CMP0057` and no AOT `ComposeData` model
- [x] Incremental-caching test: an unrelated edit leaves the `ComposeData` per-method steps `Cached`/`Unchanged` (`TrackingNames`)
- [x] Model tests cover every B4.2 field, including `Seed` unset → `null`, `Seed = 0` → `0`, `Seed = -1` → `-1` with no diagnostic, and every metadata field present vs. absent
- [x] Member resolution: one generator test per probe row 01–29 (Notes, 2026-11-06), asserting the outcome recorded there. Valid rows also assert `MemberContainingTypeFqn` (rows 10/11/21 → the base class). Inherited test method with a base-declared member: valid. Derived-only member without `MemberType`: `CMP0051`. With `MemberType`: valid
- [x] Return/element shapes (B4.5/B4.6). Valid: `Task<object>`, `ValueTask<TheoryData<…>>`, `IAsyncEnumerable<object[]>`, `List<string[]>`, a `TheoryData` subclass, `IEnumerable<object>`. `CMP0053`: `string`, a single `TheoryDataRow`, non-generic `Task`, `Task<int>`, a declared `object`, `IEnumerable<int>`, `IEnumerable<string>`. `CMP0054`: `typeof(Holder<>)`
- [x] Assignability (B4.7). Valid: `int?`→`int`, `int`→`int?`, `string?`→`string`, subclass→base, element `object`, struct `TConfig` with element `TConfig?`. `CMP0056`: `int`→`long`, too many values with and without `TConfig`. `CMP0055`: unrelated `TConfig`
- [x] Profile (B4.8): `CMP0042`/`CMP0044`/`CMP0046`/`CMP0047`/`CMP0048`/`CMP0049` through the ComposeData twins (wording asserted). **No** diagnostic for `TConfig` = `DateTimeOffset`, an interface, or a two-constructor record
- [x] `CMP0040` twin: generic test method, ref/out/in, `params`, duplicate `[Shared]` (`string` + `string?` → diagnostic; `int` + `int?` → none)
- [x] `CMP0045`: every pairing among the six AOT forms (ComposeData twin text whenever a `ComposeData` form is involved); no ComposeData model emitted as valid. `[InlineData]`/`[MemberData]` alongside `[ComposeData]` → no diagnostic
- [x] Skip (B4.9): valid active conditions on the test class, a base class, and a `SkipType` class → no diagnostic. Inactive conditions (no attribute `Skip`), including a missing property → no diagnostic and nothing recorded. Active: both set, missing, instance, non-public, non-`bool` → `CMP0057`
- [x] `CMP0044` twin for a private nested `MemberType` holder, a holder reached only through a private nested type, and an active private `SkipType` holder. Internal holders are valid
- [x] Precedence (B4.11): one test with several simultaneous faults per adjacent pair in the order, asserting only the first is reported
- [x] **All existing `CMP0040`–`CMP0049` Verify snapshots byte-identical** (twins, C-3). Existing AOT `[Compose]` snapshots unchanged
- [x] `AnalyzerReleases.Unshipped.md`: `CMP0051`–`CMP0057` added; the `CMP0040`/`CMP0042`/`CMP0044`–`CMP0049` notes say they also apply to `ComposeData`
- [x] `dotnet build` 0 warnings / 0 errors; `dotnet test` green

#### B5 (`compono-lsx.5`, after B4 and B1): AOT emission

**Status:** In progress. **Design approval gate passed.** S0 is committed
at `5db86fc`, S1 at `4b9d12e`, S2 at `bb67950`, S3 at `0680dc2`, and S4 at
`3fcc5d4`; each is verified, approved, and (S3, S4) independently reviewed,
with S4 receiving a final **APPROVE** (Notes, 2026-11-09). S5–S7 are pending.
B5 remains in progress.

- The B5 design owner specified this on 2026-11-06.
- On the same day the product owner approved
  [ADR-0070 Amendment 4](../adr/0070-xunitv3-composedata-composed-theory-rows.md),
  including the narrowed §2e, and approved this specification for
  implementation.

B5.1–B5.11 are normative unless a passage is explicitly marked
*suggested*: suggested structure (helper and method decomposition, local
names) may change, normative behavior (order, exception filters, texts,
the values each call receives) may not. The implementer makes **no**
architectural decision. If the specification doesn't cover a case, or a gate can't be met
as written, stop and report to the design owner with the exact evidence
(compiler output, test failure, decompiled xUnit source). Don't improvise a
workaround.

**Evidence base:** the B5 architecture-review probes (Notes, 2026-11-06).

##### B5.1 Ownership and coordination

- **#166 checkpoint.** At specification time (2026-11-06), #166 is OPEN
  with no ADR or plan. B5 therefore owns the seeded-row scaffold (B5.7).
  Re-check #166 when S2 starts. If it has gained an `Accepted` design by
  then, stop and report; don't adapt to it silently. Record the outcome in
  Notes. Posting to #166 needs product-owner approval.
- **AOT `[Shared]` execution** (ADR-0070 Amendment 3) is B5's: the
  `ShareExplicit<T>`/`ResolveShared<T>` ordering in B5.7.
- **AOT `[Compose]` is untouched:** `AotTheoryDataRowRegistration.scriban`,
  `AotTheoryDataRowRegistrationEmitter`, `AotComposeMethodDiscovery`, and
  their snapshots stay byte-identical. The emission components are built to
  be liftable but are **not** wired into `[Compose]`.
- **JIT is touched only by the S1 helper extraction** (B5.5). No other
  change to `Compono.XunitV3`.

##### B5.2 Discovery corrections and model additions (slice S0)

1. **`TConfig` constructor identity.** In
   `AotComposeDataMethodDiscovery.FindUsableProfileConstructors`, replace
   `SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, configType)`
   with `compilation.ClassifyConversion(c.Parameters[0].Type, configType).IsIdentity`.
   Pass in the `Compilation`. Keep the `RefKind.None` and prohibited-attribute
   filters unchanged.
   - Regression tests: valid with no `CMP0042` for `P((int A, int B))` with
     `TConfig = ValueTuple<int, int>`, `P(dynamic)` with `TConfig = object`,
     and `P(List<dynamic>)` with `TConfig = List<object>`.
   - Negative control: `P(int?)` with `TConfig = int` still gives `CMP0042`
     with 0 usable constructors.
   - Don't touch the AOT `[Compose]` copy in `AotComposeMethodDiscovery`
     (ADR-0070 Amendment 4 §6).
2. **New checks:** the B5.4 diagnostics, at the B5.4 precedence.
3. **Model additions** to `AotComposeDataMethodInfo`. They hold strings
   and `bool`s only, take part in record equality, and are populated for
   every model, failing ones included:

   | Field | Value |
   |---|---|
   | `TestClassIndex` | xUnit's declared-type index for the test method's containing type (B5.3). Equal to `FullyQualifiedTestClassName` whenever no type in the containment chain is generic |
   | `IsGenericTestClass` | `true` when the containing type or any containing type of it has `Arity > 0` |
   | `IsTestClassAccessible` | `compilation.IsSymbolAccessibleWithin(containingType, compilation.Assembly)` |
   | `LookupIsTestClass` | `true` when the `MemberType` named argument is absent |
   | `AsyncRowElementIsValueType` | `AsyncEnumerable` shape only: `true` exactly when the declared member type implements **one** `IAsyncEnumerable<T>` and `T.IsValueType`. `false` for every other shape, including the supported mixed case in B5.4 (one reference element plus value elements) |

4. **Incremental caching:** the existing B4 `TrackingNames` caching test
   must still show the per-method steps as `Cached`/`Unchanged` after an
   unrelated edit.

##### B5.3 Generic test-class bases (ADR-0070 Amendment 4 §1)

- **Index renderer** (one internal static method, unit-tested). Start with
  `global::` plus the containing namespace rendered with
  `SymbolDisplayFormat.FullyQualifiedFormat` minus its `global::` prefix.
  A type in the global namespace gets no namespace segment. Then render
  each type in the containment chain, outermost first, joined with `.`:
  - each type's name is escaped exactly as `FullyQualifiedFormat` escapes
    keywords (`@` prefix);
  - a type with `Arity > 0` is followed by `<` + `(Arity − 1)` commas + `>`.

  Don't use `FullyQualifiedFormat` on the type itself (it renders `<T>`),
  and don't use `ConstructUnboundGenericType()` (undefined for a non-generic
  type nested in a generic one).

  | Declaring type | Required index (verified against xUnit 4.0.2) |
  |---|---|
  | `Ns.GenericBase<T>` | `global::Ns.GenericBase<>` |
  | `Ns.Two<T1, T2>` | `global::Ns.Two<,>` |
  | `Ns.OuterPlain.InnerGen<T>` | `global::Ns.OuterPlain.InnerGen<>` |
  | `Ns.Outer<T>.InnerBase` | `global::Ns.Outer<>.InnerBase` |
  | non-generic chain (incl. a keyword-named type and a global-namespace type) | byte-identical to `FullyQualifiedFormat` (the existing AOT `[Compose]` key) |

- **Registration:** `RegisterTheoryDataRowFactory(<TestClassIndex literal>, <MethodName literal>, disableDiscoveryEnumeration: true, factory)`.
- **`testClass` local**, computed **once per factory execution** (never at
  registration, never per row):
  - non-generic: `typeof(<FullyQualifiedTestClassName>)`
  - generic: `global::Compono.XunitV3.Aot.ComposeDataRows.ResolveGenericTestClassType(typeof(<TestClassIndex>))`

  The index text is valid inside `typeof` for all four shapes. `testClass`
  is the only value used for `CreateRow`, every descriptor's
  `DeclaringType`, `{T}`, and `{M}` when `LookupIsTestClass`.
- **No open-generic fallback** anywhere (Amendment 4 §1).
- **Always register** for a generic declaring type. Add no special
  handling for the `xUnit1063`/`xUnit1032` shapes.
- **Inaccessible test class** (`IsTestClassAccessible == false`, a private
  or protected nested class, `xUnit1000` on both engines): emit nothing.
  Report the model's diagnostics as usual. No new diagnostic.

##### B5.4 Diagnostics (additive twins; exact)

New `DiagnosticDescriptor` objects only. Existing descriptors, their
messages, and their Verify snapshots stay byte-identical (C-3). Every entry
is `Error`, category `Compono.Usage`. Each is single-sentence, so there's
no trailing period (RS1032). `AnalyzerReleases.Unshipped.md` is keyed by
ID, so it gets no new rows. Update the existing `CMP0047`/`CMP0052`/
`CMP0053`/`CMP0054` notes to mention the new `ComposeData` conditions.

Argument conventions match B4:

- `{memberDisplay}` is B4's `memberDisplayName`: the `MemberType` (or
  declaring type) display plus `.` plus the member name.
- `{method}` is B4's `methodDisplayName`.
- Type and holder texts are `ToDisplayString()`.
- `{attr}` is the existing `ProhibitedCallSiteAttribute` rendering:
  `[Obsolete(error: true)]`, `[Experimental("ID")]`, or
  `[CompilerFeatureRequired("Feature")]`. Generalize the helper from
  `IMethodSymbol` to `ISymbol`. Its rules are unchanged, so
  `CompilerFeatureRequired` with `IsOptional = true` is still ignored.

| ID | Descriptor | Title | Message format (args) |
|---|---|---|---|
| `CMP0054` | `ComposeDataOpenGenericMemberHolder` | ComposeData member is declared on an open generic type | `ComposeData member '{0}' for '{1}' is declared on open generic type '{2}', which generated code cannot name; set MemberType to a non-generic or closed generic type that declares the member` ({0} memberDisplay, {1} method, {2} the member's containing type) |
| `CMP0054` | `ComposeDataOpenGenericSkipHolder` | ComposeData skip condition is declared on an open generic type | `[ComposeData] on '{0}': {1} '{2}' is declared on open generic type '{3}', which generated code cannot name; set SkipType to a non-generic or closed generic type that declares the property` ({0} method, {1} `SkipUnless`/`SkipWhen`, {2} property name, {3} the property's containing type) |
| `CMP0052` | `ComposeDataStaticInterfaceMember` | ComposeData member is a static abstract or static virtual interface member | `ComposeData member '{0}' for '{1}' is a static abstract or static virtual interface member, which generated code cannot call; move the data to a static member of a class or struct` |
| `CMP0052` | `ComposeDataStaticInterfaceSkipProperty` (**added in S5**, ADR-0070 Amendment 4 §7) | ComposeData skip condition property is a static abstract or static virtual interface member | `[ComposeData] on '{0}': {1} '{2}' on '{3}' is a static abstract or static virtual interface property, which generated code cannot read; move the condition to a static property of a class or struct, or to a non-virtual static interface property` ({0} method, {1} `SkipUnless`/`SkipWhen`, {2} property name, {3} the property's containing type) |
| `CMP0047` | `ComposeDataProhibitedMember` | ComposeData member cannot be used at its generated call site | `ComposeData member '{0}' for '{1}' is marked {2}, which makes any use of it a compiler error; the generated registration reads the member directly, so it would fail to compile` |
| `CMP0047` | `ComposeDataProhibitedMemberGetter` | ComposeData member getter cannot be used at its generated call site | `ComposeData member '{0}' for '{1}' has a getter marked {2}, which makes any use of it a compiler error; the generated registration reads the member directly, so it would fail to compile` |
| `CMP0047` | `ComposeDataProhibitedSkipProperty` | ComposeData skip condition property cannot be used at its generated call site | `[ComposeData] on '{0}': {1} '{2}' on '{3}' is marked {4}, which makes any use of it a compiler error; the generated registration reads the property directly, so it would fail to compile` |
| `CMP0047` | `ComposeDataProhibitedSkipPropertyGetter` | ComposeData skip condition getter cannot be used at its generated call site | `[ComposeData] on '{0}': {1} '{2}' on '{3}' has a getter marked {4}, which makes any use of it a compiler error; the generated registration reads the property directly, so it would fail to compile` |
| `CMP0053` | existing `ComposeDataUnsupportedReturnOrRowElementType` (reused unchanged, `return` variant) | — | for a declared member type that implements `IAsyncEnumerable<T>` for two or more distinct `T` (Amendment 4 §2e) |

**Predicates:**

- **Open generic:** a type is open when it is an `INamedTypeSymbol` that
  is `IsUnboundGenericType`, *or* any type argument contains a type
  parameter (`ComposedTypeAnalyzer.ContainsTypeParameter` semantics), *or*
  its `ContainingType` is open, checked recursively. The existing Step 4
  `MemberType` `CMP0054` check is unchanged.
- **Static interface member:** the selected member's `ContainingType` has
  `TypeKind.Interface`, and the member (the property symbol for
  properties) `IsStatic && (IsAbstract || IsVirtual)`.
- **Static interface skip property** (S5, Amendment 4 §7): the same
  predicate (reuse `IsStaticInterfaceMember`), applied to the *resolved*
  active skip property returned by `ResolveSkipProperty`. A plain
  (non-virtual, non-abstract) static interface property doesn't match and
  stays supported through direct `global::IFoo.P` access. A property found
  only on a base interface of `SkipType`, or on an interface implemented by
  a class `SkipType`, isn't resolved at all (`ResolveSkipProperty` walks
  `BaseType` only) and keeps the existing `CMP0057`. Don't change that
  lookup.
- **Async interface set** (ADR-0070 Amendment 4 §2e). Collect the distinct
  constructed `IAsyncEnumerable<T>` from the declared member type's
  `AllInterfaces`, plus the type itself when it *is* `IAsyncEnumerable<>`.
  Let R be the number with a reference-type `T` and V the number with a
  value-type `T`. Like JIT's `TryGetAsyncEnumerableElementType`, any
  non-empty set makes the shape `AsyncEnumerable`, checked first in
  `AnalyzeReturnShape`, so it wins over `Task`/`IEnumerable`. Then:

  | R | V | Result |
  |---|---|---|
  | 1 | 0 | supported; element = that `T` (unchanged B4 behavior) |
  | 0 | 1 | supported; element = that `T`; `AsyncRowElementIsValueType = true` (unchanged) |
  | 1 | ≥ 1 | **supported** (new): JIT always reads the single reference implementation through its `IAsyncEnumerable<object?>` fast path. Element for B4.6/B4.7 row analysis = the reference `T`; `AsyncRowElementIsValueType = false` |
  | ≥ 2 | any | `CMP0053` (`return`): more than one implementation matches the `IAsyncEnumerable<object?>` variance cast |
  | 0 | ≥ 2 | `CMP0053` (`return`): JIT's choice depends on `Type.GetInterfaces()` order |

**Precedence** (extends B4.11). Existing steps and their internal order are
unchanged; the new checks are appended inside the step they belong to:

- **Step 6** (holder accessibility, unchanged), then:
  - **6a** `CMP0054` open-generic member holder
  - **6b** `CMP0052` static interface member
  - **6c** `CMP0047` member, then `CMP0047` getter (properties only)
- **Step 7** `CMP0053`, now starting with the multiple-async-interfaces
  check.
- **Step 9**, per active condition in the existing order (`SkipUnless`
  then `SkipWhen`): `CMP0057` → `CMP0044` (existing), then **9a** `CMP0054`
  open-generic skip holder (the *resolved* property's containing type),
  then **9b** `CMP0052` static interface skip property (**added in S5**,
  mirroring 6a > 6b > 6c), then **9c** `CMP0047` skip property, then
  `CMP0047` skip getter. (S0 shipped 9c as "9b". The relative order of the
  S0 checks doesn't change.)

There is still at most one `ComposeData` diagnostic per method.

**S0 tests** (generator, `AotComposeData*Tests`). Each test asserts the
exact message.

- **Each new condition:**
  - member on `GenericBase<T>` reached through the default lookup
  - member on `GenericBase<T>` with `MemberType = typeof(Holder)` (valid)
  - member on a non-generic `Root` base of `GenericBase<T>` (valid)
  - active skip property on `GenericBase<T>`
  - active skip property with `SkipType = typeof(Holder<>)`
  - static abstract and static virtual interface members through
    `MemberType = typeof(IFoo)`
  - each prohibited attribute on a field, a property, a property getter, a
    method, an active skip property, and a skip getter
  - plain `[Obsolete]` (valid, no diagnostic)
  - the async interface table, one test per row: `string` only;
    `int` only; `int` + `string` (valid, model element `string`,
    `AsyncRowElementIsValueType == false`; a typed
    `TheoryDataRow<…>` reference element still gets the B4.7 checks);
    `string` + `Uri` → `CMP0053`; `int` + `long` → `CMP0053`; also a
    type that implements `IAsyncEnumerable<int>` and `IEnumerable` (still
    `AsyncEnumerable`, as on JIT)
- **One adjacent-precedence test per new boundary:**
  - 6 > 6a > 6b > 6c > 7
  - 8 > 9's `CMP0057` > `CMP0044` > 9a > 9c (S0); S5 adds 9a > 9b > 9c
- **Index renderer:** a unit test for every row of the B5.3 table.
- **Model:** a test for every new field.
- **Regression:** every existing B4 test and Verify snapshot passes
  unchanged. If an existing expected diagnostic changes, stop and report.

##### B5.5 Helper contracts (`src/Shared/Compono.XunitV3/ComposeDataRows.cs`, slice S1)

Same accessibility pattern as the existing members: `internal` on JIT,
`public` + `[EditorBrowsable(EditorBrowsableState.Never)]` on AOT. Move
each text **verbatim** from `ComposeDataAttribute.cs`, including its
interpolation and culture behavior.

```csharp
static CompositionException MemberReadFailure(string member, string methodDisplayName, Exception exception)
{
    var cause = exception is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : exception;
    return new CompositionException(
        $"ComposeData member '{member}' for '{methodDisplayName}' failed while reading its value: {cause.Message}",
        (cause as CompositionException)?.Diagnostic,
        cause);                                     // ADR-0070 Amendment 1 constructor; NO seed line (member level)
}

static CompositionException NegativeAttributeSeed(int seed) =>
    new($"Compono.XunitV3 requires a non-negative seed, but the configured seed was {seed}." + PreRowSeedLine(seed));

static CompositionException NegativeRowSeed(in RowContext context, int rowSeed) =>
    RowFailure(in context, $"Compono.XunitV3 requires a non-negative seed, but the configured seed was {rowSeed}.", RowSeedLine(rowSeed));

#if COMPONO_XUNITV3_AOT
static Type ResolveGenericTestClassType(Type genericTypeDefinition)
{
    for (var type = (TestContext.Current.TestClass as Xunit.v3.ICoreTestClass)?.Class; type is not null; type = type.BaseType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition)
            return type;
    }

    throw new InvalidOperationException(
        $"ComposeData could not resolve the closed generic test class for '{genericTypeDefinition}' from the executing test class.");
}
#endif
```

- **Message text.** The literal "Compono.XunitV3" stays in the
  negative-seed text on AOT too, for byte parity.
- **JIT switch-over** (the only JIT edit in B5):
  - `GetDataCore`'s attribute check calls
    `throw ComposeDataRows.NegativeAttributeSeed(negativeSeed);`
  - `ComposeRow`'s check calls
    `throw ComposeDataRows.NegativeRowSeed(context, row.Seed);`
  - the private `MemberReadFailure` is deleted, and its callers use the
    shared one.

  The JIT suite must pass **unchanged**: its exact-text assertions are the
  byte-parity proof.
- **Public surface.** Update the AOT `PublicApiSurfaceTests` for exactly
  these four members. Package validation / ApiCompat stays additive-only.
- **Other failures** reuse the existing helpers: `NullResult`,
  `UnsupportedReturnType`, `IsSupportedCollectionResult`, `NormalizeRow`,
  `ExtractConfig<TConfig>`, `BindValue<T>`, `TooManyValues`, `RowFailure`,
  `PreRowSeedLine`, `RowSeedLine`, and `CreateDataRow`. Add no other helper.
- **S1 tests** (`ComposeDataRowsTests`, both projects as in B1):
  - exact text of all three messages
  - `TargetInvocationException` unwrap
  - `InnerException` and the `Diagnostic` reference preserved
  - no seed line on `MemberReadFailure`
  - `NegativeAttributeSeed(-1)` ends with `"\n\nSeed: -1"`
  - `NegativeRowSeed` with and without a label
  - AOT only: `ResolveGenericTestClassType`
    - from a `[Fact]` in a closed subclass of a generic base it returns
      the closed base
    - with two closed subclasses (`<int>`, `<string>`) each returns its own
      closed base
    - from a non-generic test class it throws the exact
      `InvalidOperationException` text

##### B5.6 Generated registration: template, emitter, wiring

- **New files:**
  - `src/Compono.Generators/Templates/AotComposeDataRegistration.scriban`
  - `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs`
  - hint name `GeneratedFileNaming.HintNameFor($"{FullyQualifiedTestClassName}.{MethodName}") + ".AotComposeDataRegistration.g.cs"`
- **Wiring:** the existing `aotComposeDataMethodsAll` `RegisterSourceOutput`
  reports diagnostics as today, then:
  - `if (method.Diagnostics.Count > 0 || !method.IsTestClassAccessible) return;`
  - `EmissionIsolation.TryEmit(productionContext, "AOT ComposeData registration", $"{method.FullyQualifiedTestClassName}.{method.MethodName}", () => AotComposeDataRegistrationEmitter.Generate(productionContext, method))`

  An emission failure therefore surfaces as `CMP0050` for that method only
  (ADR-0068).
- **Emission gate.** Every path that can emit a registration gates on the
  single predicate `AotComposeDataRegistrationEmitter.IsWithinEmissionEnvelope`
  - the single authoritative emission-eligibility check. It was introduced in
  slice S2 (`bb67950`) as `IsWithinS2Envelope` and renamed in slice S3
  (`0680dc2`) when S3 widened it. As first introduced (S2) it admitted only the
  non-generic test class; plain `[ComposeData]`; the `Sync` member shape;
  untyped and typed rows; no profile, no `Seed`, no `[Shared]`, no active skip
  predicate; constants-only metadata - the S2 checkpoint Notes below record
  that historical form and name. S3 widened the envelope to admit the
  profile (`<TProfile>`), profile-config (`<TProfile, TConfig>`), attribute
  `Seed` (including a negative seed, whose factory body throws at execution
  time), and `[Shared]` forms, while the envelope still excludes the S4
  (`TaskOf`/`ValueTaskOf`/`AsyncEnumerable`), S5 (active `SkipUnless`/
  `SkipWhen`), and S6 (generic test-class bases) shapes: those still produce
  **no** registration (and no invalid generated source) rather than a
  registration that silently drops the feature's semantics. **S4–S6 must each
  explicitly widen `IsWithinEmissionEnvelope` as they implement their own
  functionality** - a later slice must not emit a shape whose semantics it
  does not yet reproduce.
- **No Scriban partials or includes.** `TemplateHelper` has no loader, and
  adding one is out of scope. "Reusable components" are named, commented
  template sections plus small view-model builder methods in the emitter:
  member read, row reader per shape, seeded-row scaffold, parameter
  binding, and metadata.
- **File rules:**
  - every type `global::`-qualified
  - member and skip-property names in C# positions go through
    `RequiredMemberCollector.EscapeIdentifier`; in string literals they
    stay raw, through `SymbolDisplay.FormatLiteral`
  - no LINQ
  - arrays and lists pre-sized where the count is known
  - lambdas `static` unless they capture `config`
  - `ConfigureAwait(false)` on every `await`
- **Call targets:**
  - the member is read through `MemberContainingTypeFqn`, never the lookup
    type
  - a skip condition is read through its recorded holder
  - a typed row element type is never named (B4 hand-off); binding uses
    parameter types only

**Shape.** Normative: the file-level elements (header, pragmas, `file`
class, `[ModuleInitializer]` registration call and its arguments), the
order of operations, the exception filters, and the values each call
receives. *Suggested:* local names and the decomposition into private
methods. The example is
`ComposeData<TProfile, TConfig>`, a sync member, a non-generic test class,
`Seed = 42`, and a `[Shared]` parameter at ordinal 2.

```csharp
// <auto-generated/>
#nullable enable
// CS0612/CS0618: a warning-level [Obsolete] data member is valid on JIT (Amendment 4 §2c);
// CS1998: sync member shapes produce an async method with no await.
#pragma warning disable CS0612, CS0618, CS1998

[global::System.CodeDom.Compiler.GeneratedCode("Compono.Generators", "<version>")]
file static class AotComposeDataRegistration
{
    [global::System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register() =>
        global::Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(
            "global::Ns.PricingTests",
            "Prices_by_date",
            disableDiscoveryEnumeration: true,
            factory: static _ => CreateRowsAsync());

    private static async global::System.Threading.Tasks.ValueTask<global::System.Collections.Generic.IReadOnlyCollection<global::Xunit.ITheoryDataRow>> CreateRowsAsync()
    {
        var cancellationToken = global::Xunit.TestContext.Current.CancellationToken;
        var testClass = typeof(global::Ns.PricingTests);                       // generic: ResolveGenericTestClassType (B5.3)
        var memberDisplayName = testClass.FullName + ".PricingCases";           // !LookupIsTestClass: typeof(<Lookup>).FullName
        var methodDisplayName = testClass.FullName + ".Prices_by_date";
        var attributeMetadata = new global::Compono.XunitV3.Aot.ComposeDataAttributeMetadata { /* B5.8 */ };

        var rawRows = ReadRows(memberDisplayName, methodDisplayName, cancellationToken);   // async shapes: await ReadRowsAsync(...)

        var result = new global::Xunit.ITheoryDataRow[rawRows.Count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = ComposeRow(rawRows[index], index, testClass, memberDisplayName, methodDisplayName, in attributeMetadata);
        }

        return result;
    }

    // B5.6 member read: one generic helper for every shape; wraps everything except cancellation.
    private static T ReadMember<T>(global::System.Func<T> read, string memberDisplayName, string methodDisplayName)
    {
        try
        {
            return read();
        }
        catch (global::System.Exception exception) when (exception is not global::System.OperationCanceledException)
        {
            throw global::Compono.XunitV3.Aot.ComposeDataRows.MemberReadFailure(memberDisplayName, methodDisplayName, exception);
        }
    }

    // ReadRows / ReadRowsAsync per shape: B5.6 "Row readers". ComposeRow: B5.7.
}
```

When the attribute `Seed` is **negative** (a compile-time constant),
`CreateRowsAsync`'s whole body is
`throw global::Compono.XunitV3.Aot.ComposeDataRows.NegativeAttributeSeed(<seed>);`.
No row reader and no `ComposeRow` is emitted, so there's no unreachable
code and no `CS0162`. The `async` method turns the throw into a faulted
`ValueTask`, the same as JIT's async `GetDataCore`.

**Row readers.** Emit only the one for the model's shape. The behavior of
each reader (order, filters, null and shape checks) is normative and
mirrors B5.9. Its decomposition into `ReadMember`/`AppendSync`/
`ReadValueRowsAsync` is *suggested*. `<read>` is
`static () => global::<MemberContainingType>.<EscapedName>` for a field or
property, plus `()` for a method.

- **`Sync`:**
  - `object? source = ReadMember(<read>, …);`
  - then a shared `AppendSync(object? source, …)` that:
    - throws `NullResult` when `source` is null
    - throws `UnsupportedReturnType(…, source.GetType())` when
      `!IsSupportedCollectionResult(source)`
    - enumerates `(global::System.Collections.IEnumerable)source`, with
      `cancellationToken.ThrowIfCancellationRequested()` before each add
    - wraps the loop in `catch (Exception e) when (e is not CompositionException and not OperationCanceledException)` → `MemberReadFailure`
- **`TaskOf`:**
  - `var pending = ReadMember(<read>, …);`
  - `if (pending is null) throw NullResult(…);`
  - `object? awaited; try { awaited = await pending.ConfigureAwait(false); } catch (Exception e) when (e is not CompositionException and not OperationCanceledException) { throw MemberReadFailure(…); }`
  - then `AppendSync(awaited, …)`
- **`ValueTaskOf`:** the same as `TaskOf`, with **no** null check (a
  `ValueTask<>` is never null; JIT's boxed check never fires).
- **`AsyncEnumerable`, `AsyncRowElementIsValueType == false`** (JIT's fast
  path):
  - `object? source = ReadMember(<read>, …);`
  - `if (source is null) throw NullResult(…);`
  - `try { await foreach (var row in ((global::System.Collections.Generic.IAsyncEnumerable<object?>)source).WithCancellation(cancellationToken).ConfigureAwait(false)) { cancellationToken.ThrowIfCancellationRequested(); rows.Add(row); } }`
  - `catch (Exception e) when (e is not CompositionException and not OperationCanceledException)` → `MemberReadFailure`
- **`AsyncEnumerable`, `AsyncRowElementIsValueType == true`** (JIT's
  reflection path, reproduced statically):
  - `var source = ReadMember(<read>, …);`
  - `if ((object?)source is null) throw NullResult(…);`
  - **Runtime check first, exactly as JIT does:** if
    `(object)source is global::System.Collections.Generic.IAsyncEnumerable<object?>`,
    take the reference reader above. A runtime subclass can add a
    reference implementation to a value-element declared type, and JIT
    decides by the runtime object, not the declared type.
  - otherwise call the file-local
    `ReadValueRowsAsync<TElement>(global::System.Collections.Generic.IAsyncEnumerable<TElement> source, …)`.
    `TElement` is **inferred**, never named; the B5.4 async-interface
    table guarantees the declared type has exactly one implementation.

  Its body:

  ```csharp
  global::System.Collections.Generic.IAsyncEnumerator<TElement> enumerator;
  try { enumerator = source.GetAsyncEnumerator(cancellationToken); }
  catch (global::System.Exception e) when (e is not global::System.OperationCanceledException)
  { throw global::Compono.XunitV3.Aot.ComposeDataRows.MemberReadFailure(memberDisplayName, methodDisplayName, e); }

  var rows = new global::System.Collections.Generic.List<object?>();
  global::System.Exception? failure = null;
  try
  {
      while (await enumerator.MoveNextAsync().ConfigureAwait(false))
      {
          cancellationToken.ThrowIfCancellationRequested();
          rows.Add(enumerator.Current);
      }
  }
  catch (global::System.Exception e) { failure = e; }

  try { await enumerator.DisposeAsync().ConfigureAwait(false); }
  catch (global::System.Exception disposeException) { failure ??= disposeException; }

  if (failure is not null)
  {
      if (failure is global::System.OperationCanceledException or global::Compono.CompositionException)
          global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
      throw global::Compono.XunitV3.Aot.ComposeDataRows.MemberReadFailure(memberDisplayName, methodDisplayName, failure);
  }

  return rows;
  ```

##### B5.7 Per-row scaffold (`ComposeRow`): seeded rows, binding, `[Shared]`

Order and filters are exactly JIT's `ComposeRow`. `attributeSeed` below is
the literal `int` or `null`, `P` is the parameter count, and `RowContext`
and `ComposeDataRows` are `global::Compono.XunitV3.Aot.*`.

1. `var initialContext = new RowContext(index, null, memberDisplayName, methodDisplayName);`
   `var rowData = ComposeDataRows.NormalizeRow(rawRow, in initialContext, attributeSeed);`
   `var context = new RowContext(index, rowData.Source, memberDisplayName, methodDisplayName);`
2. **`TConfig` form:** `var (config, values) = ComposeDataRows.ExtractConfig<global::TConfig>(rowData.Values, in context, attributeSeed);`
   **Other forms:** `var values = rowData.Values;`
3. Build the profile, composer, and row in one block:

   ```csharp
   global::Compono.CompositionRow row;
   try
   {
       var composer = global::Compono.Composer.Create(builder =>
       {
           builder.AddProfile(new global::P(config));   // <TProfile>: builder.AddProfile<global::P>();  no profile: omit
           builder.WithSeed(42);                         // only when Seed is set (non-negative); always AFTER AddProfile (JIT order)
       });
       row = composer.CreateRow(testClass);
   }
   catch (global::System.Exception exception) when (exception is not global::System.OperationCanceledException)
   {
       throw ComposeDataRows.RowFailure(in context, exception.Message, ComposeDataRows.PreRowSeedLine(attributeSeed), exception);
   }
   ```

   - Plain `[ComposeData]` with no `Seed` uses
     `global::Compono.Composer.Create()` inside the same `try`.
   - The lambda is `static` when it captures nothing.
   - `new P(config)` passes the `TConfig`-typed local with **no cast**. A
     cast to the constructor's declared parameter type could be
     `(dynamic)`, which would bind dynamically and isn't AOT-safe. Identity
     conversion already selects the validated constructor, and B4's
     `CMP0048` covers priority siblings.
4. `if (row.Seed < 0) throw ComposeDataRows.NegativeRowSeed(in context, row.Seed);`
5. `if (values.Length > P) throw ComposeDataRows.TooManyValues(in context, values.Length, P, row.Seed);`
6. **Bind every supplied value** before composing anything, in ordinal
   order. For each parameter `i`:
   - declare `global::Ti value_i = default!;`
   - `if (values.Length > i) value_i = ComposeDataRows.BindValue<global::Ti>(values, i, <IsNullable>, "<name>", in context, row.Seed);`
7. **Compose**, in one `try { … } catch (global::Compono.CompositionException exception) { throw ComposeDataRows.RowFailure(in context, exception.Message, ComposeDataRows.RowSeedLine(row.Seed), exception); }`.
   Only `CompositionException` is wrapped, as on JIT.
   - **First,** every `IsShared` parameter in ordinal order:
     - `if (values.Length > i) row.ShareExplicit<global::Ti>(<descriptor i>, value_i);`
     - `else value_i = row.ResolveShared<global::Ti>(<descriptor i>);`
   - **Then** every non-shared parameter in ordinal order:
     `if (values.Length <= i) value_i = row.Resolve<global::Ti>(<descriptor i>);`
   - `<descriptor i>` is
     `new global::Compono.CompositionRequestDescriptor(global::Compono.CompositionRequestKind.TestParameter, i, "<name>", testClass, global::Compono.Nullability.<Nullable|NotNullable>)`.
8. `return ComposeDataRows.CreateDataRow(new object?[] { value_0, …, value_P-1 }, in context, in attributeMetadata, row.Seed);`

Each row gets a fresh profile, composer, and row (ADR-0070 item 6). Nothing
is cached across rows or executions.

##### B5.8 Attribute metadata rendering

`attributeMetadata` is built once per factory execution, as on JIT, with
**every** member assigned explicitly so snapshots are deterministic:

| Member | Rendered from the B4 model |
|---|---|
| `Explicit` | `true`/`false` literal, or `null` |
| `Label`, `Skip`, `TestDisplayName` | `FormatLiteral` string, or `null` |
| `Timeout` | `int` literal, or `null` |
| `Traits` | `null` when absent or empty, else `new string[] { "name", "value", … }` (B4's flat pair order) |
| `DisableParallelization` | the B4 `bool?` value `?? false`: `false` when absent. This matches JIT on xUnit 4.x, which always supplies the attribute's non-null `bool`, so the merged output row is identical (Amendment 4 §5) |
| `SkipUnless`, `SkipWhen` | `null`, or `static () => global::<HolderFqn>.<EscapedProperty>` for a recorded **active** condition only (B4.9) |

The single merge rule stays in `ComposeDataRows.CreateDataRow`. Never call
`DataAttributeRegistration.CreateDataRow`.

##### B5.9 JIT stage table (exception filters and order - must match exactly)

| Stage | Behavior |
|---|---|
| Factory start | negative attribute `Seed` → `NegativeAttributeSeed` (whole body, B5.6); then `{M}`/`{T}` and the metadata once |
| Calling the member (every shape) | `ReadMember`: wraps everything **except** `OperationCanceledException`, **including** `CompositionException` |
| `Task<X>` result is `null` | `NullResult` (D5); never `await` a null task |
| Awaiting `Task<X>`/`ValueTask<X>` | wraps everything except `CompositionException` and `OperationCanceledException` |
| Sync or awaited collection | `null` → D5; `!IsSupportedCollectionResult` → D4 with the runtime type; enumerate through non-generic `IEnumerable`, `ThrowIfCancellationRequested()` before each add; enumeration errors wrapped except `CompositionException`/`OperationCanceledException` |
| Async path selection | by the **runtime** object, as JIT: `source is IAsyncEnumerable<object?>` → reference row; otherwise the value row (reachable only when the declared type's single implementation has a value-type element) |
| Async, reference element | `await foreach` over `IAsyncEnumerable<object?>` with `WithCancellation`; a cancellation check per row; the same filter. A `CompositionException` from `GetAsyncEnumerator` passes through; a `DisposeAsync` failure replaces the original |
| Async, value element | manual loop (B5.6): `GetAsyncEnumerator` wraps everything except `OperationCanceledException`; the original failure wins over a dispose failure; `OperationCanceledException`/`CompositionException` rethrown as-is |
| Reading rows | **all** raw rows are read before any row is composed; no cancellation check while composing |
| Per row | B5.7 steps 1–8 in order |

This deliberately reproduces JIT's two async behaviors, which differ from
each other. Normalizing them would change B3's behavior and is out of
scope (see Notes: the 2026-11-06 B5 specification entry, decision (c), and
the S4 checkpoint entry's known limitations).

##### B5.10 Test harness for real-runner gates (`test/Compono.XunitV3.Aot.Tests`)

- **`Support/RegisteredComposeDataFactory.cs`:**
  - an `internal static` helper with
    `InvokeAsync(string testClassIndex, string methodName)`
  - it gets factories through the public
    `global::Xunit.v3.RegisteredEngineConfig.GetTheoryDataRowFactories(ICodeGenTestMethod)`
  - its private stub class implements `ICodeGenTestMethod`:
    `DeclaredTypeIndex` and `MethodName` return the arguments, and every
    other member throws `NotSupportedException`
  - it asserts exactly **one** registered factory with
    `DisableDiscoveryEnumeration == true`
  - it invokes the factory with `new DisposalTracker()` and returns the
    rows

  It must be called from inside a running test, which supplies
  `TestContext.Current`. The project doesn't reference NSubstitute, so
  don't add it. If the interface can't be stubbed, stop and report.
- **Two kinds of target method:**
  - **Success-path targets** are ordinary `[Theory]` + `[ComposeData…]`
    methods that the real runner executes, with assertions in the theory
    body.
  - **Failure-path and metadata targets** are plain `[ComposeData…]`
    methods **without** `[Theory]`. Their factory is still registered by
    the module initializer, but the runner does not discover the method as
    a theory, so it is never executed as one. A `[Fact]` invokes the actual
    registered generated factory through the harness and asserts exact
    messages, `InnerException`/`Diagnostic`, row counts, labels, traits
    (`Compono.Seed`), and merged metadata.

    **Approved S2 deviation (2026-11-07).** The original plan used
    `[Theory(Explicit = true)]` so the runner would not execute the targets
    by default. That is wrong for the xUnit AOT runner: the xUnit AOT
    runner invokes the row factory even for `[Theory(Explicit = true)]`
    methods during a default test run. For intentionally failing factories,
    this causes the theory to fail before the harness can independently
    validate the expected exception. Claude Opus reproduced this behavior
    on net9.0, net10.0, and net11.0. Plain `[ComposeData]` (no `[Theory]`)
    is the approved pattern. **B6 must reuse this pattern for its
    failure-path and metadata assertions. Risk:** a future diagnostic that
    requires `[Theory]` alongside `[ComposeData]` could invalidate it; if
    that arises, stop and report to the design owner.
- **Generic targets:** the invoking `[Fact]` lives in the **closed
  subclass** (e.g. `IntTests : GenericBase<int>`), so
  `TestContext.Current.TestClass` is the derived class. Also assert that
  the factory is found under the exact `"global::Ns.GenericBase<>"` index.
- **File:** B5's tests go in
  `test/Compono.XunitV3.Aot.Tests/ComposeDataEmissionTests.cs`. B6 later
  adds the full behavioral matrix in `ComposeDataTests.cs` and reuses the
  harness.
- **Generator snapshot tests** (`Compono.Generators.Tests`) assert the
  generated text with `GeneratorTestHelpers.VerifyGeneratedSource` - the
  B5.10 compile-free snapshot helper (S2). It snapshots the driver's
  generated source **without** recompiling it, and still fails when the
  generator reported any diagnostic, so a `CMP0050` emission failure fails
  the test rather than silently snapshotting nothing. They do **not**
  compile the generated code against a stand-in `ComposeDataRows`; a
  stand-in would drift from the real helper. Real generated-code
  compilation is validated separately by the AOT test project build.

##### B5.11 Implementation slices and gates

Each slice is independently reviewable and must pass **every** gate before
the next starts. Record the gate evidence in Notes. Don't commit without
product-owner approval.

**Common gates (G):**

- **G1:** full-solution `dotnet build` with 0 warnings and 0 errors.
- **G2:** full-solution `dotnet test` green. Every pre-existing Verify
  snapshot byte-identical (`git status` shows no modified `*.verified.*`),
  and the AOT `[Compose]` files untouched.
- **G3:** new Verify snapshots for the slice's shapes, with no `CMP0050`.
- **G4:** `test/Compono.XunitV3.Aot.Tests` builds the slice's generated
  code against the real `Compono`, `Compono.XunitV3.Aot`, and xUnit AOT
  assemblies on every TFM it targets.
- **G5:** the slice's tests run under the real xUnit AOT runner
  (`dotnet test test/Compono.XunitV3.Aot.Tests`) and pass, including the
  harness assertions.

| Slice | Scope | Gates |
|---|---|---|
| **S0** | B5.2 + B5.4 (discovery only; no emission) | G1, G2, plus the B5.4 S0 tests |
| **S1** | B5.5 helpers and the JIT switch-over; AOT `PublicApiSurfaceTests` | G1, G2; the JIT suite unchanged and green; AOT S1 tests |
| **S2** | B5.6 wiring and template; non-generic test class; plain `[ComposeData]`; `Sync` shape (field, property, method; inherited member; `MemberType`); untyped and typed (`TheoryData<…>`) rows; B5.7 without a profile; B5.8 with constants only; `Compono.Seed` trait; R0, too-many, null/type-mismatch values (the profile-driven negative row seed is tested in S3) | G1–G5 |
| **S3** | `<TProfile>`, `<TProfile, TConfig>` (incl. the S0 identity shapes executed for real), attribute `Seed` (positive, `0`, negative-throw body), duplicate-seed conflict, every seed-line variant, profile `WithSeed` negative → `NegativeRowSeed`, `[Shared]` (row value and composed; ordering witness) | G1–G5 |
| **S4** | `TaskOf` (incl. null task → D5), `ValueTaskOf`, `AsyncEnumerable` reference and value element paths, the full B5.9 filter matrix (a member that throws `CompositionException` when invoked → wrapped; while awaiting → passes through; at `GetAsyncEnumerator` reference → passes through, value → wrapped; `DisposeAsync` failure precedence on each path), cancellation mid-enumeration → `OperationCanceledException`; async path selection: a declared `int` + `string` type reads the `string` rows; a declared `IAsyncEnumerable<int>` whose runtime object also implements `IAsyncEnumerable<string>` reads the `string` rows (JIT parity) | G1–G5 |
| **S5** | B5.8 full metadata: active `SkipUnless`/`SkipWhen` lambdas (test class, base class, `SkipType` class), grouped-skip precedence, `Explicit`, `Timeout`, traits union, `DisableParallelization` (absent → `false`, `true`, row override), keyword-named member and skip property (`@default`), a plain `[Obsolete]` member under `TreatWarningsAsErrors`. **Added by the S5 review (2026-11-09, Notes; ADR-0070 Amendment 4 §7):** (a) the B5.4 `CMP0052` static-interface skip-property twin at precedence 9b. Generator tests assert the exact message for each of: a `static virtual` interface property (for both `SkipUnless` and `SkipWhen`); a `static abstract` interface property; a `static virtual` property on an interface that also carries an unimplemented `static abstract` member; and a `static virtual` property on a closed generic interface. Adjacent precedence: an open generic interface holder → `CMP0054` (9a > 9b), and a `static virtual` property marked `[Obsolete(error: true)]` → `CMP0052` (9b > 9c). A plain (non-virtual) static interface property is supported: a Verify snapshot plus a real-runner test. A property on a base interface of `SkipType`, and on an interface implemented by a class `SkipType`, → existing `CMP0057` (regression tests). No `CMP0050` and no `CS8926` anywhere. (b) Required test corrections (review F2/F3): a row `DisableParallelization = false` overrides an attribute `DisableParallelization = true` (merged `false`); real-runner tests in `ComposeDataEmissionTests` for a skip property inherited from the test class's base class, for a `SkipType` whose property is inherited from its own base class, and for a plain `[Obsolete]` skip property (compiled under the project's `CS0612`/`CS0618` warnings-as-errors) | G1–G5 |
| **S6** | B5.3 generic bases: `GenericBase<T>` with `MemberType` to a non-generic holder and with a member on a non-generic `Root` base; two closed subclasses (`<int>`, `<string>`) each composing with its own closed root (assert `CreateRow`'s type through a custom provider seeing the closed `request.DeclaringType`, and `{T}` = `typeof(GenericBase<int>).FullName + ".M"` in an R0 failure); `Two<,>` and `OuterPlain.InnerGen<>` index registration; inaccessible nested test class → nothing emitted | G1–G5 |
| **S7** | Completion: the snapshot matrix (each form × each member shape × `[Shared]`/no `[Shared]` × typed/untyped × metadata/no metadata × generic/non-generic); a Native AOT publish of `test/Compono.XunitV3.Aot.Tests` (`-f net10.0`, host RID, `PublishAot=true`) with **0** `IL`/`AOT` warnings, then run the published binary and confirm the `ComposeData` tests executed and passed (count them). If the project can't be AOT-published for a reason unrelated to B5, record the exact error and stop for the design owner; don't mark it passed. Then the B5 acceptance checklist | G1–G5, plus the Native AOT result |

**B5 acceptance checklist**

- [x] ADR-0070 Amendment 4 approved in principle by the product owner (2026-11-06, Notes)
- [x] Product owner confirmed the narrowed Amendment 4 §2e before S0 (2026-11-06, Notes)
- [x] #166 checkpoint re-checked at S2 start and recorded (B5.1)
- [x] S0: `TConfig` identity fix; the B5.4 diagnostics at the B5.4 precedence; model fields; every B4 test and snapshot unchanged
- [x] S1: the four helper members with exact contracts; JIT switched over with its suite unchanged; AOT `PublicApiSurfaceTests` and ApiCompat additive
- [x] S2–S3 delivered per B5.6–B5.9, each slice gate-passed (G1–G5) with evidence in Notes (S2 `bb67950`, S3 `0680dc2`)
- [x] S4 delivered per B5.6–B5.9, gate-passed (G1–G5) and independently approved with evidence in Notes (S4 `3fcc5d4`)
- [x] S5 delivered per B5.6–B5.9, gate-passed (G1–G5) and independently approved with evidence in Notes (S5 `07ff636f`)
- [x] S6 delivered per B5.6–B5.9, gate-passed (G1–G5) and independently approved with evidence in Notes (S6 `20c1eb9`)
- [x] Emission only for zero-diagnostic, accessible-test-class models; `CMP0050` isolation per method (S2 wiring, B5.6)
- [ ] Member called through `MemberContainingTypeFqn`; `{M}`/`{T}` from `testClass`/`typeof(<Lookup>)` `.FullName`; no typed row element type named
- [ ] No reflection other than `ResolveGenericTestClassType`'s type-identity walk; no LINQ; no `DataAttributeRegistration.CreateDataRow`
- [ ] AOT `[Compose]` template, emitter, discovery, and snapshots byte-identical
- [ ] S7 Native AOT publish: 0 IL/AOT warnings and `ComposeData` tests executed
- [x] `AnalyzerReleases.Unshipped.md` notes updated for the new `CMP0047`/`CMP0052`/`CMP0053`/`CMP0054` conditions (the `CMP0052` note also covers the S5 static-interface skip property, ADR-0070 Amendment 4 §7)
- [x] S5: an active `static virtual`/`static abstract` interface skip property reports the `CMP0052` twin (never generated `CS8926`); plain static interface skip properties emit and run; base-interface and implementing-class holders keep `CMP0057` (ADR-0070 Amendment 4 §7)
- [x] If valid skip metadata can't reach full parity, the work **stopped** and went to the product owner. There's no pre-approved fallback (unchanged from round 2)

#### B6 (`compono-lsx.6`, after B5 and B3): AOT real-runner tests, samples, Native AOT

- [ ] `test/Compono.XunitV3.Aot.Tests/ComposeDataTests.cs`: the same behavioral matrix as B3 (plain `Assert`), except assembly fixtures (xunit/xunit#3664, with a comment) and xUnit argument disposal (documented difference, with an assertion that AOT doesn't dispose)
- [ ] Cross-engine message parity: a test in each project asserts the exact R0 text for the same failing scenario
- [ ] Cross-engine metadata parity: the same metadata matrix as B3 (skip conditions with and without `SkipType`, grouped precedence, explicit, timeout, traits, labels) yields the same skipped/run/explicit outcomes and display names on AOT
- [ ] Real-runner skip parity: mirror the JIT baseline B3 established in `test/Compono.XunitV3.Tests/ComposeDataTheoryTests.cs` (`ComposeDataRowSkipConditionTests`, `ComposeDataAttributeSkipTypeTests`, `ComposeDataSkipPrecedenceTests`, `ComposeDataTheoryLevelSkipTests`) — row-level and attribute-level `SkipUnless`/`SkipWhen` with `SkipType` on a different class, grouped-skip precedence (unconditional row `Skip` wins), and theory-level `[Theory(Skip = …)]` conditions combined with unconditional row `Skip` — and assert identical run/skip outcomes and skip reasons on JIT and AOT
- [ ] **Theory-level `SkipUnless`/`SkipWhen` interaction tests** (RESEARCH-0038 §2.6 item 3, §9.2 S2), mirrored in both test projects: a `[Theory(Skip = …, SkipUnless = …)]` and a `[Theory(Skip = …, SkipWhen = …)]` (condition true and false), each combined with (a) no `ComposeData` skip, (b) a row `Skip` with no condition, (c) a row `Skip` with its own condition, (d) an attribute `Skip` with and without a condition. Assert identical run/skip outcomes and skip reasons on JIT and AOT. If the planned approach can't reach parity, **stop and request review**. No speculative workarounds
- [ ] Seed parity: the same seed-line variants as B3, with identical text
- [ ] Name parity: `typeof(…).FullName`-based `{M}`/`{T}` text matches JIT under JIT-run **and** real Native AOT, for a nested test class and a closed-generic holder. Inherited test methods **without** `MemberType` are excluded from `{M}` byte parity (C-1: JIT names the concrete class, AOT the declaring class); add one test per engine that documents that difference
- [ ] ADR-0070 Amendment 2 documenting tests (runtime side; the AOT compile-time side is B4's `CMP0051`/`CMP0044` tests): (a) an inherited test method with a member that a derived class hides, without `MemberType`, asserting each engine's actual resolution (JIT: the derived member; AOT: the declaring-class member); (b) the same inherited test with an explicit `MemberType`, asserting identical rows on both engines; (c) on JIT only, a private nested `MemberType` holder that runs (AOT can't compile it, `CMP0044`). Each test carries a comment naming Amendment 2
- [ ] **Generic test-class bases (ADR-0070 Amendment 4 §1)**, mirrored in both projects: an inherited `[ComposeData]` method on `GenericBase<T>` (with `MemberType` to a non-generic holder) run through two closed subclasses. Assert identical rows, identical `{T}` R0 text (the closed-generic `FullName`), and that a custom `ICompositionValueProvider` sees the same closed `request.DeclaringType` on both engines
- [ ] **ADR-0070 Amendment 4 §2 documenting tests** (runtime side, JIT project only, each with a comment naming Amendment 4; the AOT compile-time side is covered by B5 S0): a member declared on `GenericBase<T>` without `MemberType` runs on JIT; a static virtual interface member through an interface `MemberType` runs on JIT; an `[Obsolete(error: true)]` data member runs on JIT; a member type implementing `IAsyncEnumerable<string>` and `IAsyncEnumerable<Uri>`, and one implementing `IAsyncEnumerable<int>` and `IAsyncEnumerable<long>`, each run on JIT (they're `CMP0053` on AOT); an active `SkipUnless` naming a `static virtual` interface property through `SkipType = typeof(IFoo)` runs on JIT with its condition honored (it's `CMP0052` on AOT, Amendment 4 §7a); plus a cross-engine parity test for the supported `int` + `string` shape (both engines read the `string` rows)
- [ ] `DisableParallelization` parity: absent, `true`, and a row override yield the same merged `ITheoryDataRow.DisableParallelization` on both engines (B5.8)
- [ ] Reuse B5.10's `RegisteredComposeDataFactory` harness for AOT failure-path and metadata assertions. Don't add a second harness
- [ ] Samples: `test/Compono.XunitV3.SampleTests/ComposeDataTests.cs` and `test/Compono.XunitV3.Aot.SampleTests/ComposeDataTests.cs` (documentation-quality: the `TConfig` pricing table, the email table with `[Shared]`, an async source, a fixture-backed row)
- [ ] V3: real Native AOT publish and run of `Compono.XunitV3.Aot.SampleTests`, 0 IL/AOT warnings, all `ComposeData` tests executed (count them in the output)
- [ ] V7: pack-and-consume, a throwaway consumer outside the repo using both packages from a local feed (`AGENTS.md` generator rule)

### Phase 3: docs, skill, validation

#### B7 (`compono-lsx.7`, after B3 and B6): documentation and API reference

- [ ] `docs/packages/compono-xunitv3.md`: a "Composed theory rows (`[ComposeData]`)" section (forms, collection vs. row shapes, row layout, binding and nullable/value-type rules, effective-seed precedence and seed-line meanings, the row-vs-attribute metadata table including `SkipType`/`SkipUnless`/`SkipWhen`, labels, failure granularity, disposal note, mixing with xUnit attributes). Replace the "No stacking…" bullet with a pointer
- [ ] `docs/packages/compono-xunitv3-aot.md`: the same section, the diagnostics table (`CMP0051`–`CMP0057`, plus the ComposeData uses of `CMP0040`/`CMP0042`/`CMP0044`–`CMP0049`), the JIT/AOT differences (disposal, assembly fixtures, inherited test methods resolving the member on the declaring class with `MemberType` as a fixed-holder workaround only, private/protected nested holders rejected with `CMP0044`, the C-4 skip residual), and "Not yet supported" updated: AOT `[Compose]` inline values and `[Shared]` remain a follow-up (record it explicitly). Seed status per #166
- [ ] `docs/packages/compono-xunitv3-aot.md` and `docs/reference/diagnostics.md`: ADR-0070 Amendment 4. That means generic test-class bases being supported, the five AOT-only rejections (§2a–e) with their workarounds (for §2e: the supported mixed shape, and why ambiguous shapes are rejected), the new `CMP0047`/`CMP0052`/`CMP0053`/`CMP0054` `ComposeData` conditions, and the C-1 rationale correction
- [ ] `docs/cookbook/vary-composed-configuration-per-theory-row.md` plus `docs/cookbook/index.md` (alphabetical)
- [ ] `docs/migrating-from-autofixture.md`: `MemberAutoData`/`InlineAutoData` → `ComposeData`
- [ ] `docs/troubleshooting/common-errors.md`: D1–D5/R0 entries. `docs/reference/diagnostics.md`: new CMP entries
- [ ] ADR-0022: append an Amendment recording that Amendment 7's open question is answered by ADR-0070 (no change to `[Compose]`)
- [ ] V4: regenerate `docs/reference/api` (the full API-reference regeneration for the new public types is a B7 obligation; B2 did not regenerate it). Confirm `ComposeDataAttribute*` pages under both packages. Confirm the AOT helper page shows up as infrastructure (EditorBrowsable)
- [ ] `mkdocs build --strict`
- [ ] Full-file grep of touched docs for stale claims ("one Compose-family attribute", "no data rows", "InlineData")

#### B8 (`compono-lsx.8`, after B7): distributed `compono` skill (**mandatory for #163 completion**)

- [ ] `skills/compono/references/xunit-v3.md`: replace or rework "Hard constraint: one Compose-family attribute per method" (exclusivity still applies, and `ComposeData` is the multi-row answer). Add a `ComposeData` section covering the three forms, member shapes, `TConfig`-first row layout, leading-positional binding, `[Shared]`, effective-seed precedence (attribute `Seed` + profile `WithSeed` = conflict), the metadata merge rule (grouped skip), collection vs. row shapes, labels, execution-time lifecycle (fixtures OK, rows built before the first runs, whole-theory failure), disposal JIT/AOT difference, mixing with xUnit attributes, and the AOT section (parity for `ComposeData`, diagnostics, `[Compose]` AOT limits unchanged)
- [ ] `skills/compono/SKILL.md`: routing and decision guidance ("table-driven theory with per-row config/expected values → `ComposeData`"), plus guardrails (don't stack Compose attributes, don't put composed params before row values, don't rely on `Configure` captured state). Keep the frontmatter `description` ≤ 1024 chars
- [ ] Other references: `references/patterns-and-antipatterns.md` (one-method-per-row antipattern → `ComposeData`), `references/diagnostics.md` (new CMP IDs and runtime messages), `references/registrations-profiles-and-scopes.md` (per-row profile with `TConfig`), and the migration guidance wherever `InlineAutoData`/`MemberAutoData` are mapped
- [ ] Every skill example matches the final public API and the docs (compile-check the snippets by copying them into a sample test or a scratch project)
- [ ] `skills/compono/evals/evals.json`: add scenarios (positive: the table-driven theory picks `ComposeData`; AOT project picks the `Compono.XunitV3.Aot` form; correct-behavior: doesn't invent `[InlineData]`+`[Compose]` merging)
- [ ] Validation (V8): skill-creator `quick_validate` on `skills/compono` (frontmatter, description ≤ 1024), all relative links in `skills/compono/**` resolve, and the evals JSON parses

#### B9 (`compono-lsx.9`, after B8): validation gate and closeout

- [ ] V1 full `dotnet build` (0/0) and `dotnet test` green
- [ ] V2 focused suites (JIT, AOT, samples, generators)
- [ ] V3 Native AOT publish and run (re-run on the final tree)
- [ ] V5 xUnit 3.2.2 floor (JIT) and 4.0.2 (JIT plus AOT), in a throwaway consumer outside the repo
- [ ] V6 package validation (all affected packages, against the prior baseline)
- [ ] V7 pack-and-consume
- [ ] V9 per-row cost spot-check: measure `[ComposeData]` (N=50 rows) vs. N `[Compose]` methods. Record it in Notes. Investigate if more than 2× per-row
- [ ] Optional: `scripts/dogfood-validate.sh` (not a gate, per `AGENTS.md`)
- [ ] Record results in Notes, tick tasks, set `Status: Done`

## Critical Files

- New: `src/Shared/Compono.XunitV3/ComposeDataRows.cs`;
  `src/Compono.XunitV3/ComposeDataAttribute{,{TProfile},{TProfile,TConfig}}.cs`;
  `src/Compono.XunitV3.Aot/ComposeDataAttribute{,{TProfile},{TProfile,TConfig}}.cs`;
  `src/Compono.Generators/Templates/AotComposeDataRegistration.scriban`,
  `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs` (B5);
  `test/Compono.XunitV3.Aot.Tests/Support/RegisteredComposeDataFactory.cs`,
  `test/Compono.XunitV3.Aot.Tests/ComposeDataEmissionTests.cs` (B5)
- Modified: `src/Compono.XunitV3/Binding/BindingPlan.cs` (exclusivity);
  both `.csproj`s (link, description);
  `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `AotComposeMethodDiscovery.cs`, `Diagnostics/DiagnosticDescriptors.cs`,
  `AnalyzerReleases.Unshipped.md`, `ComponoIncrementalGenerator.cs`;
  B5: `Discovery/AotComposeDataMethodDiscovery.cs`, `Models/AotComposeDataMethodInfo.cs`,
  `src/Shared/Compono.XunitV3/ComposeDataRows.cs`, `src/Compono.XunitV3/ComposeDataAttribute.cs`
  (helper switch-over only), AOT `PublicApiSurfaceTests.cs`
- Tests: `test/Compono.XunitV3.Tests/*ComposeData*`, `PublicApiSurfaceTests.cs`;
  `test/Compono.XunitV3.Aot.Tests/ComposeDataTests.cs`;
  `test/Compono.Generators.Tests/*ComposeData*`;
  both `*SampleTests/ComposeDataTests.cs`
- Docs: `docs/packages/compono-xunitv3{,-aot}.md`, a new cookbook recipe plus
  index, `docs/migrating-from-autofixture.md`,
  `docs/troubleshooting/common-errors.md`, `docs/reference/diagnostics.md`,
  `docs/adr/0022-…` (Amendment), `docs/reference/api/**` (regenerated)
- Skill: `skills/compono/SKILL.md`, `skills/compono/references/{xunit-v3,patterns-and-antipatterns,diagnostics,registrations-profiles-and-scopes}.md`,
  `skills/compono/evals/evals.json`

## Test Plan

Summarized from the beads. Each item has automated coverage unless marked
manual.

| Area | JIT (`Compono.XunitV3.Tests`) | AOT (`Compono.XunitV3.Aot.Tests` + generator tests) |
|---|---|---|
| Discovery vs. execution | `SupportsDiscoveryEnumeration` false; the member is invoked only at `TestMethodExecution` (probe `TestContext.Current.PipelineStage`) | `disableDiscoveryEnumeration: true` in snapshot; same probe at runtime |
| Multiple rows | N rows, N results, distinct seeds and traits | Same |
| Per-row config isolation | Profile instance and `Configure` counter per row; `DateTimeOffset` config | Same |
| `[Shared]` and row values | Row value shared, composed shared, ordering | Same (new on AOT) |
| Member shapes | field, property, method; `Task`, `ValueTask`, `IAsyncEnumerable`; `MemberType`; inherited | Same plus a diagnostic per invalid shape |
| Fixtures | Class and collection; `RequireFixture` in factory and in member; assembly fixture | Class and collection (assembly blocked by xunit#3664) |
| Parallel | 4+ classes in parallel, distinct fixtures, no leakage | Same |
| Errors | D1–D5/R0 exact text, `InnerException`, `Diagnostic` | Compile-time CMPs, plus runtime R0 for untyped rows (exact text parity) |
| Seeds | Effective-seed precedence; duplicate-seed conflict; every seed-line variant; trait equals the actual seed; reproduction by pasting the reported seed; never a fabricated seed | Same, identical text |
| Collection/row shapes | All verified shapes accepted; `string`/single row/scalar arrays rejected; nullable and value-type boxing; no widening; oblivious nullability | Same, plus static (`TheoryData<…>`) compile-time checks |
| Row/attribute metadata | Grouped skip matrix incl. `SkipUnless`/`SkipWhen`/`SkipType`; row ?? attribute for the rest; traits union; `DisableParallelization` (4.x) | Same outcomes; invalid skip usage → CMP only; theory-level skip edge checked (stop if divergent) |
| Exclusivity and mixing | `[Compose]`+`[ComposeData]` error; `[InlineData]` coexists | `CMP0045`; `[InlineData]` coexists |
| Disposal | Documents that xUnit disposes args; Compono registers nothing | Asserts no disposal by Compono/xUnit core |
| Cancellation | `IAsyncEnumerable` observes the token (cancel mid-enumeration) | Same |
| API compatibility | `PublicApiSurfaceTests`, package validation | Same |
| Native AOT | n/a | Publish plus run, 0 IL warnings (V3) |
| Packaged consumer | V5/V7 (manual, recorded) | V5/V7 (manual, recorded) |
| Regression | Existing `[Compose]` suites unchanged | Existing AOT suites and snapshots unchanged |

## Validation commands

Same mechanics as PLAN-0070. Run from the repo root.

```bash
# V1 full
dotnet build && dotnet test

# V2 focused
dotnet test test/Compono.XunitV3.Tests
dotnet test test/Compono.XunitV3.Aot.Tests
dotnet test test/Compono.Generators.Tests
dotnet test test/Compono.XunitV3.SampleTests
dotnet test test/Compono.XunitV3.Aot.SampleTests

# V3 Native AOT (use your RID)
dotnet publish test/Compono.XunitV3.Aot.SampleTests -c Release -f net10.0 \
  -p:RuntimeIdentifier=osx-arm64 -p:SelfContained=true -p:PublishAot=true -p:UseAppHost=true \
  -o test/Compono.XunitV3.Aot.SampleTests/bin/aot-publish 2>&1 | tee "$SCRATCH/aot-publish.log"
grep -E "warning (IL|AOT)[0-9]+" "$SCRATCH/aot-publish.log" && echo FAIL || echo OK
./test/Compono.XunitV3.Aot.SampleTests/bin/aot-publish/Compono.XunitV3.Aot.SampleTests

# V4 API reference (see PLAN-0070 V4 for the full package loop)
dotnet tool restore && .github/scripts/generate-api-reference.sh
git status --short docs/reference/api

# V5 xUnit floor/current: throwaway consumer outside the repo,
#   xunit.v3.mtp-v2 3.2.2 (JIT) and 4.0.2 (JIT + xunit.v3.aot.mtp-v2 4.0.2);
#   confirm the resolved versions in obj/project.assets.json.

# V6 package validation
dotnet pack src/Compono.XunitV3 -c Release -p:PackageValidationBaselineVersion=<prior>
dotnet pack src/Compono.XunitV3.Aot -c Release -p:PackageValidationBaselineVersion=<prior>

# V7 pack-and-consume (AGENTS.md generator rule): pack Compono + both xUnit packages
#   to a local feed, consume them from a scratch project, and run ComposeData tests (JIT and AOT).

# V8 skill
python3 <skill-creator>/scripts/quick_validate.py skills/compono
python3 -c "import json;json.load(open('skills/compono/evals/evals.json'))"
# relative links: every ](...) target under skills/compono/** exists

# V9 per-row cost spot-check (record numbers in Notes)
```

## Notes

- **2026-11-05 (planning):** written from RESEARCH-0038 and the
  product-owner interview (RESEARCH-0038 §9). The plan number 0071 differs
  from ADR-0070 because PLAN-0070 implements ADR-0069.
- **2026-11-05 (review round 2, RESEARCH-0038 §9.2):** ADR-0070
  **Accepted** and B0 (`compono-lsx.10`) closed. JIT row-level
  `DisableParallelization` goes through cached reflection (3.2.2 kept
  compatible). Targeted theory-level skip-interaction tests are required,
  and lack of parity stops work for review. Strict typing is confirmed. The
  #166 checkpoint is kept. `TheoryData<…>` vs. `TheoryDataRow<…>`
  terminology was corrected.
- **2026-11-05 (review round 1, RESEARCH-0038 §9.1):** `MemberType` kept;
  skip-metadata parity required with no preapproved AOT fallback; seed
  reporting tightened (never fabricated, precedence clarified, duplicate
  seed = core conflict); collection/row shapes clarified and verified
  empirically (xUnit 3.2.2 and 4.0.1); one cohesive feature PR, with beads
  as checkpoints and no partial merges; metadata merge rule defined by
  Compono, since the xUnit engines disagree; #166 checkpoint kept.
- The skill update is a **mandatory** completion requirement (product owner,
  2026-11-05). B8 can't be skipped or deferred.
- The open items from RESEARCH-0038 §10 were all resolved in review round 2
  (RESEARCH-0038 §9.2). None remain open. (Corrected 2026-11-06; this
  bullet previously listed them as open.)
- Beads: epic `compono-lsx` with children `.1`–`.9`, local only (never
  `bd dolt push`, never `git add .beads`). Don't commit, push, or open a PR
  without product-owner approval.
- **2026-11-05 (B1 implementation, `compono-lsx.1`):** B1 landed on branch
  `feat/163-composedata` — the shared `ComposeDataRows` helper (linked into
  both packages), its JIT/AOT unit tests, and one approved additive core API:
  `public CompositionException(string message, CompositionDiagnostic? diagnostic,
  Exception innerException)` (ADR-0070 Amendment 1, the sole core change for
  #163). It exists so the R0 row-failure wrapper can set its own message while
  preserving the causing exception and the original `Diagnostic` reference;
  `docs/reference/api/Compono` was regenerated for it. Independent review of the
  implementation requested changes, which were remediated: interface-typed
  `DisableParallelization` read plus concrete-target write (works for custom
  `ITheoryDataRow` implementations and generic `TheoryDataRow<T…>`), a
  centralized `too many values` failure in the helper, fuller grouped-skip and
  `DisableParallelization` coverage (JIT and AOT `Func<bool>` paths), `BindValue<T>`
  hardened so a non-nullable value type can never silently default on `null`,
  `EditorBrowsable(Never)` applied to the AOT struct members, and `Compono.Seed`
  treated as a reserved, authoritative trait. B1 is **not** yet approved or
  closed — it awaits a further independent review. B2+ not started.
- **2026-11-05 (B1 approved and finalized, `compono-lsx.1`):** A second
  independent review of B1 returned APPROVE. Its three nonblocking follow-ups
  landed: the generator-facing API is minimized (the seven-parameter
  `RowFailure` overload removed in favour of the `RowContext`-based overload,
  and `NormalizeRow` refactored to the three-parameter
  `(object? row, in RowContext context, int? attributeSeed)` form), AOT
  skip-precedence coverage gained a focused test proving a row-level `Skip`
  without a condition overrides an attribute-level `Skip` with a condition
  (clearing the inherited condition), and this plan's status moved to
  In Progress. B1 is complete; B2 remains the next checkpoint and is not
  started.
- **2026-11-05 (B2 implementation, `compono-lsx.2`):** B2 landed on branch
  `feat/163-composedata` — the six public `ComposeData` attribute types, the
  JIT/AOT `PublicApiSurfaceTests` exact-set updates (plus a new AOT
  equivalent), and both package `<Description>`s. JIT:
  `ComposeDataAttribute` (`DataAttribute`, deliberately unsealed, with
  `MemberName`/`MemberType`/`Seed`, a `SupportsDiscoveryEnumeration() => false`
  override, and a `GetData` that throws `NotSupportedException` until B3);
  `ComposeDataAttribute<TProfile>` and `ComposeDataAttribute<TProfile,TConfig>`
  derive from it, carry `[DynamicallyAccessedMembers(PublicConstructors)]` on
  their generic parameters, and `MemberType` carries
  `[DynamicallyAccessedMembers(PublicFields|PublicProperties|PublicMethods)]`.
  AOT: three independently `sealed` markers deriving directly from
  `DataAttribute`, self-declaring the same `MemberName`/`MemberType`/`Seed`
  shape, with no `DynamicallyAccessedMembers` and no `GetData` override
  (matching the existing AOT `Compose` family). Two JIT-only abstractions on
  xUnit's `DataAttribute` (both `GetData` and `SupportsDiscoveryEnumeration`
  are abstract there; neither is required by the AOT package core) were
  confirmed by the build. Validation: full `dotnet build` 0 warnings/0
  errors; full `dotnet test` 4456 passed/0 failed; `dotnet pack` for both
  packages with `-p:PackageValidationBaselineVersion=1.4.0-preview.122` (the
  currently-latest nuget.org version) passed ApiCompat with no breaking
  changes. B2 is **not** yet approved or closed — it awaits independent
  review. B3 not started.
- **2026-11-05 (B2 review and remediation, `compono-lsx.2`):** Independent
  review returned REQUEST CHANGES. **F1 (blocker):** the JIT
  `ComposeDataAttribute<TProfile, TConfig>` XML docs wrongly implied
  `TConfig` was resolved through the profile; corrected to state that element
  0 of *every* row supplies that row's `TConfig` (non-null, assignable),
  elements 1..n are the row's values, and the profile is built once per row
  from that row's config — public API and trimming annotations unchanged.
  **F2:** `docs/plans/README.md` now shows PLAN-0071 `In Progress`.
  **F3:** added a JIT test using real attribute reflection proving
  `SeedAsNullable` distinguishes an unset `Seed` from an explicit `Seed = 0`.
  **F4:** extended both packages' generic-form tests to cover
  `AttributeUsage`, the `MemberType` default, and the `Seed` default.
  **F5** (mixed assertion style) was nonblocking, no action. Carried into
  later beads: explicit `Seed = 0` is a configured seed (B4 reads
  `AttributeData.NamedArguments`); negative seeds keep existing semantics with
  no new AOT diagnostic; the JIT `MemberName` constructor guard is preserved;
  B3 must fail clearly for an unsupported consumer subclass; and full
  `docs/reference/api` regeneration stays a B7 obligation. B2 approved and
  closed. B3 (`compono-lsx.3`) not started.
- **2026-11-05 (B3 implementation, `compono-lsx.3`):** B3 landed on branch
  `feat/163-composedata`. JIT `ComposeDataAttribute.GetData` is fully
  implemented (execution-time only; `SupportsDiscoveryEnumeration() => false`).
  Per call it caches a `LazyInitializer` member accessor (public static field,
  property, or parameterless method on `MemberType ?? ReflectedType ??
  DeclaringType`, `FlattenHierarchy`) and a `BindingPlan`, invokes the member,
  awaits `Task`/`ValueTask`, and enumerates sync or `IAsyncEnumerable` (via the
  `IAsyncEnumerator<>` interface for value-type element rows, so an explicit
  interface implementation on a compiler-generated iterator is handled) with
  `TestContext.Current.CancellationToken` checked between rows. Per row it
  normalizes the row through the B1 shared helper, extracts the row's
  `TConfig` (two-type-parameter form), builds a **fresh** profile and
  `Composer` (never a cached `Lazy<Composer>`), `CreateRow`s, checks the
  negative seed, validates all supplied values before composing, then binds
  `[Shared]` parameters first and the rest, and emits the output row through
  the shared metadata merge with the `Compono.Seed` trait. Design decisions:
  form detection is by `GetType()` (a non-`ComposeDataAttribute`/`<>`/`<,>`
  subclass throws a clear `CompositionException`); the two generic forms
  override internal `ConfigurationType`/`ApplyProfile` (no runtime-generic
  reflection in the base); the per-row composer build catches all
  non-`OperationCanceledException` failures as R0, since
  `CompositionConfigurationException` (duplicate `Seed` + profile `WithSeed`)
  is not a `CompositionException`; `DataAttribute.Traits` is a flat
  `string[]` of alternating key/value strings (not a dictionary). Supporting
  changes: `BindingPlan` exclusivity now counts `ComposeAttribute` **and**
  `ComposeDataAttribute` and names all six forms (keeping the existing
  "Compose" / "Compose<TProfile, TConfig>" substrings the older tests assert);
  `ComposeMethodDiscovery` gained the three `ComposeData` metadata names and
  `ComposeIncrementalGenerator` three new providers folded into
  `composeMethodResultsAll` (so `ComposeData` methods' parameter types get
  plans and row-invoker registrations). Tests: new GetData-level
  `ComposeDataGetDataTests`, real-runner `ComposeDataTheoryTests` (class and
  collection fixtures, fixture read inside the member, mixed
  `[ComposeData]`+`[InlineData]`, member-read-at-execution probe, parallel
  isolation), a generator discovery test `ComposeDataDiscoveryTests`, and the
  removed B2 `GetData_IsNotImplementedYet` placeholder. Validation: full
  `dotnet build` 0 warnings/0 errors; `test/Compono.XunitV3.Tests` 896
  passed/0 failed across net8.0–net11.0; generator discovery test 6
  passed/0 failed. Two limitations recorded: (i) the metadata merge matrix is
  asserted at the GetData level rather than through a real runner, because the
  repo has no in-suite xUnit runner harness; the real-runner theory tests
  cover fixtures, row-source execution timing, mixing, and parallel isolation.
  (ii) The member-read-failure and unsupported-subclass message texts are
  Compono's own (ADR-0070 fixes only the D1–D5/R0 text). **B4 follow-up
  (do not implement now):** the new JIT generator discovery matches
  `[ComposeData]` on *any* method, including the reflection-only
  `SeedSampleMethods` fixtures in `ComposeDataAttributeTests.cs` (static
  parameterless methods with no `[Theory]`/`[Fact]`). They are not run by
  xUnit today, but when B4's AOT discovery/emission lands their parameter
  types must be handled the same way (they currently have none/void, so this
  is expected to be a no-op — verify in B4). (Corrected 2026-11-06: those
  fixtures use the JIT `Compono.XunitV3.ComposeData` attribute, which AOT
  discovery never matches. B4 only needs the regression test in its
  checklist: no `CMP0051`–`CMP0057` and no AOT model for a JIT-namespace
  `[ComposeData("Missing")]`.) B3 is **not** yet approved or
  closed — it awaits independent review. B4 not started.

- **B3 review remediation (2026-11-05).** An independent review of B3 returned
  REQUEST CHANGES; the JIT architecture was approved, and the findings were
  addressed without redesign. **F1 (exception consistency):** synchronous lazy
  enumeration is now wrapped as a `CompositionException` like the async paths
  (excluding `OperationCanceledException`), member resolution catches
  `AmbiguousMatchException` and throws a clear "ambiguous … Set `MemberType` to
  disambiguate" `CompositionException`, and member/test identity, row context,
  `InnerException`, and `Diagnostic` are preserved. **F2 (cancellation):**
  `GetData` delegates to an internal `GetDataCore(testMethod, CancellationToken)`
  so tests can inject a token; an `IAsyncEnumerable` member cancelled mid-stream
  propagates `OperationCanceledException` (observed by the between-row
  `ThrowIfCancellationRequested` check). **F3 (execution stage):** a real-runner
  probe records `TestContext.Current.PipelineStage` inside the member and asserts
  `TestPipelineStage.TestMethodExecution`. **F4 (metadata):** the attribute-level
  `SkipUnless`/`SkipWhen`/`SkipType`/`DisableParallelization` merge is asserted at
  the GetData level, and real-runner theories prove the runner honors row/attribute
  `Skip`, `Explicit`, conditional `SkipUnless`/`SkipWhen` (with a positive
  companion proving the condition is actually evaluated), and row labels/traits
  (via `TestContext.Current.Test!.TestDisplayName`/`.Traits`). **F5 (constructor
  cache):** the two-type-parameter form caches the `TProfile` config constructor
  per attribute instance via `LazyInitializer` (no row-specific state cached).
  **F6 (assertions):** exact D1–D5 text incl. `{M}`, later-row failure naming
  member/index/seed/label with `InnerException`+`Diagnostic` preserved, a
  `Diagnostic`-preservation test, and a real seed reproduction test (run unseeded,
  read the `Compono.Seed` trait, rerun pinned, compare composed values).
  **F7 (gaps):** `DateTimeOffset` `TConfig`, sync `ValueTuple`/`ITuple`,
  `TheoryDataRow<T…>[]`, `List<object?[]>`, assembly-fixture integration, and
  four parallel-isolation classes. **F8 (cleanup):** attribute metadata is built
  once per `GetData` call, value-type async-enumeration reflection is cached on
  the accessor, and disposal failures are surfaced without swallowing the primary
  enumeration failure (cancellation still propagates as cancellation).
  **Cross-engine decisions recorded:** (1) `{M}` uses CLR-style fully qualified
  names incl. `+` for nested types — B4/B5 must reproduce the same formatting from
  Roslyn symbols (superseded 2026-11-06 by B4.3: B5 renders `{M}`/`{T}` at
  runtime with `typeof(…).FullName`, because Roslyn can't reproduce the
  closed-generic `FullName` form); (2) `DisableParallelization` must behave equivalently JIT vs AOT
  (no public-API workaround for xUnit's non-nullable bool); parity verified in B6;
  (3) existing seed/negative-seed semantics preserved; (4) generator changes stay
  limited to B3's JIT row-invoker registration. Validation after remediation:
  `test/Compono.XunitV3.Tests` 1012 passed/0 failed/36 skipped (the skipped set is
  exactly the row/attribute `Skip`+`Explicit` cases, proving the runner honors
  them). Remaining limitation unchanged: the metadata *merge* is asserted at the
  GetData level (no in-suite runner harness), with real-runner coverage proving
  the runner observes skip/explicit/display-name/traits. B3 still awaits
  independent re-review.

- **B3 second-review remediation (2026-11-05).** A second independent review kept
  REQUEST CHANGES but approved F1–F3 and F5–F8; the JIT architecture stayed as-is.
  **Real-runner skip coverage (the review's core ask):** new theories in
  `test/Compono.XunitV3.Tests/ComposeDataTheoryTests.cs` prove, on the real xUnit
  pipeline, that the runner honors (a) row-level `SkipUnless`/`SkipWhen` with
  `SkipType` resolving a `bool` property on a *different* class
  (`ComposeDataSkipConditions`), (b) attribute-level `SkipUnless` via
  `SkipType` on that same external class, (c) grouped-skip precedence — an
  unconditional row `Skip` overrides an attribute conditional skip that would not
  itself have skipped, and (d) theory-level `[Theory(Skip = …, SkipUnless/SkipWhen = …)]`
  interaction with rows carrying an unconditional `Skip`. Bodies `Assert.Fail` when
  executed, and each class carries a positive companion so it cannot pass merely
  because every case was skipped. Observed on all four TFMs: the eight expected
  cases are reported *skipped*, and the companions run — establishing the JIT
  baseline that B6 must mirror on AOT (B6 now has an explicit parity checkbox).
  **Product-owner decision recorded in the B3 checklist:** for B3, GetData-level
  assertions are sufficient for attribute `TestDisplayName`, row/attribute trait
  union, `Timeout`, and `DisableParallelization` — the plan no longer claims
  runner-level verification for those (B6 carries observable JIT/AOT parity checks
  where the execution APIs support them).
  **Additional corrections:** (5) the value-type `IAsyncEnumerable` reflection path
  now wraps `GetAsyncEnumerator` invocation failures as a member-read
  `CompositionException` (previously unwrapped), preserving `InnerException` and the
  member context; (8) that path no longer double-wraps — cancellation propagates as
  cancellation and an already-`CompositionException` failure is rethrown as-is,
  matching the sync/fast-async paths; (6) `ConfigProfileBinder.ResolveProfileConstructor`'s
  `profileType` parameter now carries
  `[DynamicallyAccessedMembers(PublicConstructors)]` so the constructor requirement
  flows through the cache wrapper; (7) the two later-row failure tests whose names
  promised inner-exception preservation but never asserted it were renamed
  (`GetData_FailureInLaterRow_NamesTheRowIndex`,
  `GetData_FailureInLaterRow_NamesMemberIndexAndSeed`) and now assert
  `InnerException` is null — a row-value validation failure is a direct R0 cause,
  not a wrapped exception; (9) value-type enumeration cancellation is now covered
  (`GetDataCore_CancellationDuringAsyncValueTypeEnumeration_PropagatesOperationCanceled`
   over `CancellingTupleRows`). Remaining limitation unchanged: the metadata *merge*
   is asserted at the GetData level (no in-suite runner harness). B3 still awaits
   independent re-review.

- **B3 approved and closed (2026-11-05).** Claude Opus's final independent review
  returned **APPROVE**. B3 (`compono-lsx.3`) is complete and closed. B1, B2, and B3
  are all independently reviewed and approved. B4 and every later bead remain open.

- **B4 specification amendment (2026-11-06, product owner).** Two readiness
  reviews before B4 found six blockers (C1–C6) and four follow-up decisions
  (C-1–C-4). All are resolved, and the B4 section above is the resulting
  contract:
  - **C1** (static assignability): unwrap `Nullable<T>` on both sides; identity/reference/boxing/unboxing only; null values still checked at runtime; the same rule for `TConfig`.
  - **C2** (profile checks): `TConfig` is row-supplied and never constructed, so no `CMP0041`/`CMP0043`. `CMP0046`–`CMP0048` apply to the `TProfile` constructor only. `CMP0044` covers both types. Wording is attribute-aware through twins.
  - **C3** (member resolution): JIT's field → property → method order across the hierarchy, hiding and ambiguity pinned by the probe below, CLR-style `{M}`/`{T}` (rendered at runtime, B4.3), parameterless overloads accepted.
  - **C4** (skip): option A; only active conditions (attribute `Skip` set) are validated and recorded.
  - **C5** (`[Shared]`): `IsShared` in the model; duplicates reuse `CMP0040`.
  - **C6** (plans): B4 owns the three AOT `ComposeData` plan-generation providers.
  - **C-1** (inherited test methods): option A; AOT resolves on the declaring class (xUnit AOT runtime keys data factories by `DeclaredTypeIndex`, `RegisteredEngineConfig.GetTheoryDataRowFactories`; xUnit's own AOT `MemberData` does the same). `MemberType` is a fixed-holder workaround only.
  - **C-2**: `CMP0044` for inaccessible `MemberType`/`SkipType` holders (JIT accepts them; xUnit AOT silently drops them; Compono never silently omits a test). `CMP0052` for generic member methods.
  - **C-3**: twin `DiagnosticDescriptor`s with the same IDs; existing `[Compose]` text and its 18 snapshots stay unchanged.
  - **C-4**: accepted residual. AOT reports invalid active attribute conditions even when every row overrides `Skip` or there are no rows.

  **Reflection probe** (a scratch console app on net10.0 reproducing JIT
  `BuildMemberAccessor` verbatim; the expected outcome for each B4 member-resolution test):

  | # | Shape | JIT outcome | AOT (B4) |
  |---|---|---|---|
  | 01 | `Cases<T>()` only | selected; `Invoke` throws (member-read failure) | `CMP0052` generic |
  | 02 | `Cases<T>()` + `Cases()` | ambiguous | `CMP0051` ambiguous |
  | 03 | `Cases<T>()` + `Cases(int)` | generic selected; `Invoke` throws | `CMP0052` generic |
  | 04 | `Cases()` + `Cases(int)` | parameterless; valid | valid |
  | 05 | `Cases(int x = 0)` only | D3 | `CMP0052` requires arguments |
  | 06 | `Cases(params int[])` only | D3 | `CMP0052` requires arguments |
  | 07 | base field, derived `new` field of another type | derived field | valid (derived) |
  | 08 | base property, derived `new` property of another type | ambiguous | `CMP0051` ambiguous |
  | 09 | base property, derived `new` property of the same type | derived property | valid (derived) |
  | 10 | base field, derived method | **base field** | valid; call target = base |
  | 11 | base static property, derived instance property | **base static** | valid; call target = base |
  | 12 | base method, derived `new` method | derived method | valid (derived) |
  | 13 | public static property, private getter | D2 | `CMP0052` not public static |
  | 14 | field on a closed generic base (`GB<int>`) | resolved | valid; call target `GB<int>` |
  | 15 | public static member on a private nested holder | resolved | `CMP0044` (C-2) |
  | 16 | inherited test, member only on derived, no `MemberType` (declaring-class lookup) | n/a on JIT (JIT looks on derived) | `CMP0051` not found (C-1) |
  | 17 | member on the class itself | resolved | valid |
  | 18 | static property on an interface `MemberType` | resolved | valid |
  | 19 | own protected static | D2 | `CMP0052` |
  | 20 | own internal static | D2 | `CMP0052` |
  | 21 | base static property, derived instance field | base static | valid; call target = base |
  | 22 | base private static | D1 | `CMP0051` not found |
  | 23 | base internal static | D2 | `CMP0052` |
  | 24 | base protected static | D2 | `CMP0052` |
  | 25 | base private instance | D1 | `CMP0051` not found |
  | 26 | base public instance | D2 | `CMP0052` |
  | 27 | base `Cases<T>()`, derived `new Cases()` | derived; valid | valid (derived) |
  | 28 | base static property, derived static field | derived field | valid (derived field) |
  | 29 | base `Cases(int)` only | D3 | `CMP0052` requires arguments |

  Names: `ReflectedType.FullName` is the derived class for an inherited test
  method, while `DeclaringType` is the base. A nested type's `FullName` uses
  `+`. A closed generic's `FullName` is assembly-qualified with the runtime
  core library, which Roslyn can't reproduce, hence B4.3.

  **ADR consistency:** C-1's derived-only-member case and C-2's holder
  accessibility are product-owner-approved AOT-only rejections. ADR-0070
  item 13 says valid usage is never rejected on one engine only, so the
  product owner approved and recorded them as ADR-0070 Amendment 2
  (2026-11-06). They are the only AOT-only exceptions. The C-4 skip residual
  is an invalid-usage timing difference, already covered by item 13. No new public API, no core change, no architectural
  redesign. The diagnostic IDs fall inside the ADR's approved `CMP0051`+
  range.

- **2026-11-06 (B4 implementation, `compono-lsx.4`):** B4 (AOT `ComposeData` discovery + validation) is implemented and all 16 acceptance-checklist items pass. Implementation: `src/Compono.Generators/Models/AotComposeDataMethodInfo.cs` (equatable model, no retained Roslyn symbols/locations); `src/Compono.Generators/Discovery/AotComposeDataMethodDiscovery.cs` (member resolution B4.4, return/element shapes B4.5-B4.6, typed-row assignability B4.7, profile checks B4.8, active-only skip B4.9, duplicate-`[Shared]` B4.10, precedence B4.11, diagnostics CMP0051-CMP0057 + ComposeData twins); `ComposeMethodDiscovery` AOT-ComposeData metadata-name consts; `ComponoIncrementalGenerator` raw plan providers (3) folded into `composeMethodResultsAll` (so parameter plans are emitted even for invalid methods) plus a validated per-method pipeline whose `RegisterSourceOutput` reports diagnostics only (B4 emits no registration); `AotComposeMethodDiscovery` CMP0045 extended to all six Compose-family forms; `DiagnosticDescriptors` CMP0051-CMP0057 + ComposeData twin descriptors (existing `[Compose]` descriptors and snapshots unchanged); `AnalyzerReleases.Unshipped.md` updated.
  Also corrected a B2 public-API omission discovered during B4: added public sealed `Compono.XunitV3.Aot.SharedAttribute` (same contract as the JIT marker), recorded as ADR-0070 Amendment 3, with the AOT public-API surface test, a focused marker contract test, and the package docs updated.
  Tests: new `test/Compono.Generators.Tests/AotComposeData*` files (member-resolution probe rows 01-29, shapes/assignability, profile, diagnostics/CMP0045/skip/CMP0044/precedence, model/metadata, plan pipeline, incremental caching) - 117/117 pass. Full solution `dotnet build`: 0 warnings/0 errors. Full solution `dotnet test`: 5153 total, 0 failed, 68 skipped.
  B4 (`compono-lsx.4`) is complete but not yet closed, pending independent Claude Opus review. B5 (`compono-lsx.5`) is next and owns AOT `[Shared]` execution and the emission of the ComposeData registration.

- **2026-11-06 (B4 independent-review remediation, `compono-lsx.4`):** The independent review returned request-changes; the blocking findings and test-strengthening asks were addressed before the second review.
  B-1: the ComposeData parameter loop now rejects any parameter whose type fails `ComposedTypeAnalyzer.IsRowInvokerShapeEligible` (open generic parameter, ref struct, pointer, function pointer, unsupported array shape) with the CMP0040 signature rule, so an unsupported parameter can no longer produce a zero-diagnostic model.
  B-2: `DiagnosticInfo` value equality/hash now also consider the descriptor's message format, so the ComposeData twin descriptors that share a rule id but differ in wording (e.g. `CMP0051` not-found vs ambiguous, `CMP0052` not-public-static vs requires-arguments vs generic-method) no longer compare equal - the incremental driver now re-reports correctly (unit equality test plus an incremental run test where a not-found member becomes ambiguous).
  B-3: the skip-condition check replaced the "any static public bool property" boolean with a most-derived-by-name resolver plus a public-static-getter/bool validity check, and records the resolved property's containing type. A private getter, a setter-only property, or a derived instance member hiding a base static property now reports `CMP0057`; a valid inherited public static bool property is recorded with its (base) containing type.
  B-4: member-resolution property hiding now matches reflection hide-by-name-and-signature across static properties of identical type regardless of accessibility (a non-public derived static property of identical type hides a public base property -> `CMP0052`; a different type leaves the base selectable).
  B-5: `docs/packages/compono-xunitv3-aot.md` corrected - B4 compile-time discovery/validation is implemented and reports the diagnostics, while B5 execution emission is not, so a `[ComposeData]` theory still does not execute under AOT.
  Inv1: duplicate-`[Shared]` type identity now normalizes CLR-equivalent `dynamic`/`object` and top-level tuple element names before grouping.
  Inv2: a mixed `[Compose]`+`[ComposeData]` stack now reports the six-form `CMP0045` wording from both pipelines while Compose-only stacks keep the existing three-form text and snapshots.
  Inv3: fixed the `CMP0040` double period (the duplicate-`[Shared]` reason no longer ends with a period); the three `CMP0052` messages keep no trailing period because RS1032 (enforced) requires a single-sentence message to have no trailing period - the PLAN's periods on those single-sentence messages are incompatible with RS1032.
  Test strengthening: `ComposeDataAotTestHelper` now fails on unexpected compiler errors, which surfaced that the existing ComposeData test sources were not valid C# (file-scoped test namespace after the block-scoped stand-ins); the six test helpers were made block-scoped with `using Compono;`. New `test/Compono.Generators.Tests/AotComposeDataRemediationTests.cs` covers B-1..B-4 and Inv1..Inv3.
  Post-remediation validation: full solution `dotnet build` 0 warnings/0 errors; full solution `dotnet test` 5197 total, 0 failed, 68 skipped (`Compono.Generators.Tests` 1016 = 508 per TFM, no Verify snapshot changes); `dotnet pack src/Compono.XunitV3.Aot` succeeds with the additive `Compono.XunitV3.Aot.SharedAttribute`. B4 remains open pending the second independent review.

- **2026-11-06 (B4 second-review remediation, `compono-lsx.4`):** The second independent review accepted B-1/B-2/B-3/B-5 and asked for a narrowly scoped remediation; B-4 was over-corrected and has been re-scoped.
  C-1: member-resolution property hiding no longer lets an inherited private static property participate - a private property participates only when declared on the lookup type itself (where it can still cause `CMP0052`); internal/protected/base-public static properties keep the reflection-probe behavior, and differing types still leave the base selectable.
  C-2: duplicate-`[Shared]` detection is now a direct pairwise check using `Compilation.ClassifyConversion(a.Type, b.Type).IsIdentity` (the custom `NormalizeSharedTypeIdentity` grouping was removed). This matches CLR identity: nullable-reference annotations, `dynamic` vs `object`, and tuple element names are erased, while `Box<int?>` stays distinct from `Box<int>`.
  C-3 (recorded here): the plan's `CMP0052` diagnostic texts no longer show a trailing period (RS1032); the B4.10 duplicate-`[Shared]` rule now states the CLR-identity comparison (preserving the nullable-reference vs nullable-value distinction); and an `RS1032` diagnostic-message note was added under the diagnostics table. ADR-0070 was not modified.
  Tests: added the C-1 three-level `G`/`M`/`L` hierarchy tests and the C-2 CLR-identity `Box<...>` theory (plus the corrected, renamed dynamic-parameter `CMP0040` test); strengthened probe rows 02/08/16/22/25 to assert the ambiguous/not-found message variant; added profile-twin wording tests (incl. the `<TProfile>`-only `CMP0044`), a `CMP0056` too-many-values-with-`TConfig` test, adjacent-precedence tests asserting the winning message, and an incremental diagnostic test that edits a separate holder source file while keeping the method location stable. B4 remains open pending the third independent review.
- **2026-11-06 (B4 third-review remediation, `compono-lsx.4`):** D-1 (property hiding must use CLR type identity) fixed: `AotComposeDataMethodDiscovery.ResolveMember` now takes the `Compilation` and compares candidate property signatures with `compilation.ClassifyConversion(existing, candidate).IsIdentity` instead of `SymbolEqualityComparer.Default`. This makes `IEnumerable<dynamic>` hide `IEnumerable<object>` and keeps tuple-element-name differences (`List<(int a, int b)>` vs `List<(int c, int d)>`) as the same CLR signature, so a derived property hides an identical-CLR base property. Preserved behavior: private properties participate in hiding only on the lookup type, inherited private does not participate, internal/protected and public-base are unchanged, differing property types still leave the base selectable, and the field/property/method precedence is untouched. Regression tests added (probe rows 30 and 31) asserting the resolved member's containing type, not merely the absence of diagnostics. Also added the missing precedence tests asserting the winning message: `CMP0040` > profile validation, profile validation > `CMP0054`, `CMP0044` (holder) > `CMP0053`, `CMP0053` > `CMP0055`, `CMP0056` > `CMP0057`; strengthened the `ComposeData` profile-twin wording tests for `CMP0046`/`CMP0047`/`CMP0048`/`CMP0049`; and corrected the diagnostics-table trailing periods for `CMP0055`/`CMP0056`/`CMP0057` (RS1032). ADR-0070 was not modified. B4 received independent approval (all 16 acceptance criteria satisfied); `compono-lsx.4` is closed, with B5 (`compono-lsx.5`) owning the generated `ComposeData` execution.
  Final checkpoint validation: full solution `dotnet build` 0 warnings/0 errors; full solution `dotnet test` 5,255 total tests / 5,187 passed / 68 skipped / 0 failed; `dotnet pack src/Compono.XunitV3.Aot -c Release` succeeds; no Verify snapshot changes.

- **2026-11-06 (B5 specification, `compono-lsx.5`, design owner):** a corrected B5 specification and ADR-0070 Amendment 4 replace the uncommitted draft OpenCode/DeepSeek wrote earlier the same day. That draft was a proposal, not an approved specification. It said "approved by the maintainer" and "approved after the independent Opus architecture review", and neither was true: the review ended at REQUEST CHANGES with open decisions. **Amendment 4 is Proposed, pending product-owner approval. B5 must not start before that approval.**
  - **Evidence:** the two B5 architecture-review rounds (2026-11-06). They used hand-written B5-shaped registrations against `Compono.XunitV3.Aot` at `93c8c40` and xUnit AOT 4.0.1/4.0.2, run under the JIT host and under a real Native AOT publish (0 IL/AOT warnings: `[ComposeData<TProfile, TConfig>]` + `[Shared]` rows passed; attribute `Skip` + `SkipUnless` rows skipped), plus decompiled `xunit.v3.core.aot` 4.0.2. Re-checked during specification: `RegisteredEngineConfig.GetTheoryDataRowFactories(ICodeGenTestMethod)` is public and keys on `DeclaredTypeIndex`; `ICoreTestClass.Class` is public; `test/Compono.XunitV3.Aot.Tests` doesn't reference NSubstitute; #166 is still OPEN with no ADR or plan.
  - **Probe findings the specification relies on:**
    - xUnit's index for inherited methods on generic bases is the open form (`<>`, `<,>`, nested forms; B5.3 table), and `typeof` of that text compiles.
    - `TestContext.Current.TestClass` is the concrete derived class inside the factory under Native AOT, and walking `BaseType` gives the exact closed base.
    - A member on `GenericBase<T>` (default lookup) is `CS0246` from generated code but runs on JIT.
    - B4 compared `TConfig` constructors by symbol equality: `CMP0042` for `(int A, int B)`, `dynamic`, and `List<dynamic>` constructors that JIT accepts.
    - A static virtual interface member gives `CS8926` from generated code but runs on JIT; a static abstract one fails on both engines.
    - `[Obsolete(error: true)]`/`[Experimental]` data members are uncompilable from generated code.
    - JIT's two async enumeration paths differ (B5.9).
    - xUnit 4.x AOT only checks `DisableParallelization == true`.
  - **Corrections to the draft:**
    1. It claimed approvals that didn't exist (removed).
    2. It said `CMP0047` was the *only* new AOT-only exception. Wrong: the open-generic member/skip holder, static virtual interface members, the B4 `CMP0040` rejection of type-parameter parameters on generic bases, and multiple `IAsyncEnumerable<T>` implementations are also AOT-only (Amendment 4 §2a–e).
    3. It gave `MemberReadFailure` an `attributeSeed` parameter and a "pre-row seed line". JIT member-level failures have **no** seed line, and the real contract unwraps one `TargetInvocationException` (B5.5).
    4. It gave `NegativeSeed` a single row-context shape. JIT has two distinct texts: an attribute-level one with a pre-row seed line, and a per-row R0 with a row seed line. Now `NegativeAttributeSeed` and `NegativeRowSeed`.
    5. It said "exactly two helpers" while also requiring a third (`ResolveExecutingDeclaringType`). The surface is now four hidden members, all listed in Amendment 4 §3.
    6. It allowed an open-generic `typeof` fallback. That fallback is rejected: it changes custom providers' `DeclaringType`, diagnostics, and `{T}`.
    7. It said "don't emit for open generic test classes". Wrong: the generic base **is** the registration key, so always register.
    8. It named the value-type async element type. It's now inferred.
    9. It left out keyword escaping, the null-`Task` D5 rule, `CS0162` on a negative attribute seed, plain-`[Obsolete]` `CS0618` under `TreatWarningsAsErrors`, inaccessible test classes, `DisableParallelization ?? false`, a concrete way to run failure-path tests (B5.10 harness), and slice-level real-runner gates.
    10. It cited `ConstructUnboundGenericType()` for the index, which isn't defined for a non-generic type nested in a generic one. It's replaced by a specified renderer.
    11. It said "no new public API". The four helper members are hidden public API on AOT and need the amendment.
  - **Kept from the draft:** support for generic bases with per-execution closed roots; the `TConfig` identity fix; the `CMP0047`/`CMP0052`/`CMP0054` twins (with corrected scope and text); both async paths; no `[Compose]` changes; follow-ups recorded.
  - **Decisions requiring product-owner approval:** Amendment 4 as a whole. In particular:
    - (a) the closed-root type-identity read under ADR-0001 (recommended), versus rejecting generic bases on AOT entirely;
    - (b) the four hidden helper members (recommended), versus duplicating literals in generated code;
    - (c) reproducing both JIT async paths (recommended), versus normalizing JIT, which would change B3 behavior.
  - **Follow-ups (not B5; filing them needs product-owner approval):**
    - AOT `[Compose]` has the same generic-declaring-type rendering defect and the same symbol-equality `TConfig` match (`AotComposeMethodDiscovery`).
    - A `CMP0046`-style check for `RequiresUnreferencedCode`/`RequiresDynamicCode` on the data member.
    - Holder-type or assembly-level `[Obsolete(error: true)]`/`[Experimental]` exposure, which existing AOT `[Compose]` shares.
  - **#166 checkpoint:** B5 owns the seeded-row scaffold (B5.1). Re-check when S2 starts.

- **2026-11-06 (B5 maintainer decisions and §2e investigation, `compono-lsx.5`):** the product owner approved ADR-0070 Amendment 4 in principle:
  1. runtime closed-generic-base resolution (consistent with ADR-0001; no open-generic fallback);
  2. the four hidden `ComposeDataRows` helpers;
  3. preserving both JIT async enumeration paths and their exception semantics;
  4. the `TConfig` CLR-identity fix;
  5. exact diagnostic contracts with real generated-code compilation and execution;
  6. filing separate follow-up issues for the AOT `[Compose]` generic-base/`TConfig` parity defects and for `RequiresUnreferencedCode`/`RequiresDynamicCode` call-site diagnostics.

  §2e (multiple `IAsyncEnumerable<T>`) was conditional pending investigation.
  - **How B3 JIT chooses:**
    - `TryGetAsyncEnumerableElementType` takes the declared type itself if it is `IAsyncEnumerable<>`, otherwise the first match in `Type.GetInterfaces()`.
    - `AppendAsync` then branches on the **runtime** object: `source is IAsyncEnumerable<object?>` → `await foreach` fast path; otherwise the reflection loop over the selected element type.
  - **CoreCLR probe** (net10.0, scratch console app with explicit implementations of each pair):

    | Type | `GetInterfaces()` order | `is IAsyncEnumerable<object?>` | Rows read |
    |---|---|---|---|
    | `IntThenString` / `StringThenInt` | declaration | true | `string` both ways |
    | `IntThenLong` / `LongThenInt` | declaration | false | reflection on the first: `int` vs `long` |
    | `StringThenUri` / `UriThenString` | declaration | true | `string` vs `Uri` (first in the interface map) |
    | `ObjectThenString` / `StringThenObject` | declaration | true | exact `object` implementation both ways |

  - **Conclusion:** one reference element plus value elements is deterministic and is now **supported**: AOT uses the same `IAsyncEnumerable<object?>` cast, with no reflection and no named element type. Two or more reference elements, or two or more value elements with none of reference type, depend on undocumented runtime ordering or variance resolution. So they stay `CMP0053`, justified in Amendment 4 §2e as a correctness requirement. Native AOT runtime behavior for the ambiguous cases wasn't probed, and the rejection doesn't depend on it.
  - **Spec changes:**
    - B5.2: `AsyncRowElementIsValueType` definition.
    - B5.4: the R/V async-interface table, plus S0 tests.
    - B5.6: the value-element reader now performs JIT's runtime `IAsyncEnumerable<object?>` check first. This is a parity gap found during the investigation: JIT decides by the runtime object, so a value-element declared type whose runtime object also implements a reference `IAsyncEnumerable<T>` reads the reference rows.
    - B5.9: a path-selection row.
    - S4: path-selection tests.
    - B6: documenting and parity tests.
  - **Editorial pass:**
    - Marked suggested structure as distinct from normative behavior (B5 status paragraph, row readers).
    - Removed the speculative `ICollection` pre-sizing instruction.
    - Amendment 4 status updated, the ADR-0001 confirmation recorded, and the §6 follow-ups marked approved.
  - **Open:** the product owner's confirmation of the narrowed §2e before S0. The follow-up issues are approved but not yet filed; the design owner was limited to editing ADR-0070 and PLAN-0071.

- **2026-11-06 (B5 design approval gate, `compono-lsx.5`):** the product owner gave final approval of ADR-0070 Amendment 4, including the narrowed §2e:
  - supported: one reference-type `IAsyncEnumerable<T>` plus any number of value-type ones;
  - `CMP0053`: two or more reference-type implementations, or two or more value-type implementations with no reference-type one;
  - preserved: JIT's runtime-object path selection, including the `IAsyncEnumerable<object?>` check before the value-type reader.

  The B5 architecture and this specification are approved for implementation. **The design approval gate has passed and B5 S0 is authorized.** B5 implementation hasn't started.
  - The specification is committed as a documentation-only checkpoint.
  - The three approved follow-up issues (Amendment 4 §6) are still to be filed.

- **2026-11-06 (B5 S0 checkpoint, `compono-lsx.5`):** S0 (B5.2 + B5.4) is committed at `5db86fc` ("feat(xunitv3): add AOT ComposeData S0 discovery support"), whose parent is `e709228`. The design owner verified it independently; the product owner approved it.
  - **Scope:** discovery only — the B5.2 corrections/model additions, the B5.4 diagnostics, and the B5.3 index renderer. No emission. Twelve files.
    - `src/Compono.Generators/Discovery/AotComposeDataMethodDiscovery.cs`: the B5.2 `TConfig` constructor-identity fix (`SymbolEqualityComparer.Default.Equals(…)` replaced by `compilation.ClassifyConversion(…).IsIdentity`); the new B5.4 checks — open-generic member/skip holder, static abstract/virtual interface member, prohibited member/getter/skip property/skip getter, and the multiple-`IAsyncEnumerable<T>` set — at the B5.4 precedence; and population of the new model fields.
    - `src/Compono.Generators/Discovery/AotComposeDataTestClassIndex.cs` (new): the B5.3 index renderer (`Render(INamedTypeSymbol)`), rendering each containing type's own name with open-generic arity markers (`<>`, `<,>`) and `FullyQualifiedFormat` keyword escaping.
    - `src/Compono.Generators/Discovery/ComposedTypeAnalyzer.cs`: `ContainsTypeParameter` made `internal` for reuse by the open-generic check (no behavior change).
    - `src/Compono.Generators/Models/AotComposeDataMethodInfo.cs`: five new fields — `TestClassIndex`, `IsGenericTestClass`, `IsTestClassAccessible`, `LookupIsTestClass`, and `AsyncRowElementIsValueType`.
    - `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs`: seven new `ComposeData` twin descriptors — `ComposeDataOpenGenericMemberHolder` and `ComposeDataOpenGenericSkipHolder` (`CMP0054`), `ComposeDataStaticInterfaceMember` (`CMP0052`), and `ComposeDataProhibitedMember`/`ComposeDataProhibitedMemberGetter`/`ComposeDataProhibitedSkipProperty`/`ComposeDataProhibitedSkipPropertyGetter` (`CMP0047`). The multiple-async-interface condition reuses the existing `ComposeDataUnsupportedReturnOrRowElementType` descriptor (`CMP0053`). Existing descriptors, their messages, and their Verify snapshots are unchanged.
    - `src/Compono.Generators/AnalyzerReleases.Unshipped.md`: the `CMP0047`/`CMP0052`/`CMP0053`/`CMP0054` notes extended for the new `ComposeData` conditions (keyed by ID, so no new rows).
    - Tests: new `test/Compono.Generators.Tests/AotComposeDataAmendment4DiagnosticTests.cs`, `AotComposeDataAmendment4ModelTests.cs`, `AotComposeDataAmendment4TConfigIdentityTests.cs`, `AotComposeDataTestClassIndexTests.cs`, and `CompilerFeatureRequiredReference.cs`, plus additions to `ComposeDataAotTestHelper.cs`.
  - **Approved scope:** B5.2 + B5.4 exactly. No B5 emission (no template, emitter, wiring, or generated registration) and no S1-or-later work.
  - **Review verdict:** independent Claude Opus review returned **APPROVE**.
  - **G1:** full-solution `dotnet build`, 0 warnings, 0 errors.
  - **G2:** full-solution `dotnet test`, 5385 total, 0 failed, 5317 passed, 68 skipped. The 68 skips are pre-existing. The working tree was clean after the run, so no `*.verified.*` changed (the existing generator snapshots are unchanged) and the AOT `[Compose]` files are untouched.
  - **Not in S0's commit (by design):** ADR, plan, bead, and snapshot changes. This entry is S0's documentation closeout.
  - **Open:** S1 (`4b9d12e`) is committed next and recorded below. S2 (`bb67950`) followed it (see the S2 checkpoint below); the #166 re-check (B5.1) happened at S2 start.

- **2026-11-07 (B5 S1 checkpoint, `compono-lsx.5`):** S1 (B5.5) is committed at `4b9d12e` ("feat(xunitv3): add shared ComposeData AOT helpers"), whose parent is the S0 commit `5db86fc`. The design owner verified it independently; the product owner approved it.
  - **Scope:** exactly four files.
    - `src/Shared/Compono.XunitV3/ComposeDataRows.cs`: `MemberReadFailure`, `NegativeAttributeSeed`, `NegativeRowSeed`, and the AOT-only `ResolveGenericTestClassType`. Each matches the B5.5 contract text exactly, is `internal` on JIT, and is `public` + `[EditorBrowsable(Never)]` on AOT. No other helper was added.
    - `src/Compono.XunitV3/ComposeDataAttribute.cs`: the JIT switch-over only. The attribute- and row-level negative-seed checks call the shared helpers, the private `MemberReadFailure` is deleted, and all of its callers use the shared one. The message texts are moved verbatim.
    - `test/Compono.XunitV3.Tests/ComposeDataRowsTests.cs`: additions only, covering all three exact texts, the `TargetInvocationException` unwrap, preserved `InnerException`/`Diagnostic`, no seed line on `MemberReadFailure`, `NegativeAttributeSeed(-1)` ending in `"\n\nSeed: -1"`, and `NegativeRowSeed` with and without a label.
    - `test/Compono.XunitV3.Aot.Tests/ComposeDataRowsTests.cs`: `ResolveGenericTestClassType` from closed `<int>` and `<string>` subclasses, from a multi-level subclass, and from a non-generic class (exact `InvalidOperationException` text). It also checks that the four helpers are public, static, and hidden, and that every declared public member is hidden.
  - **Public surface:** `PublicApiSurfaceTests` locks public *types* only. S1 adds members, not types, so its expected list is correctly unchanged. The member-level lock for the four helpers is in the AOT `ComposeDataRowsTests`.
  - **JIT suite unchanged:** no existing JIT test was edited, and the exact-text assertions that existed before S1 pass. That is the byte-parity proof.
  - **G1:** full-solution `dotnet build`, 0 warnings, 0 errors.
  - **G2:** full-solution `dotnet test`, 5433 total, 0 failed, 5365 passed, 68 skipped. The skips are `Compono.XunitV3.Tests`' 17 per TFM, all predating S1, which added none. `Compono.XunitV3.Aot.Tests` passed 42 on each TFM. The working tree was clean after the run, so no `*.verified.*` changed, and the AOT `[Compose]` files are untouched.
  - **ApiCompat:** `dotnet pack -c Release` of `Compono.XunitV3` and `Compono.XunitV3.Aot` with `-p:Version=1.3.2 -p:PackageValidationBaselineVersion=1.3.2` (the nuget.org baseline) gave no warnings or errors, so the change is additive only.
  - **Not in S1's commit (by design):** ADR, plan, bead, and snapshot changes. This entry is S1's documentation closeout.
  - **Open:** S2 (`bb67950`) followed and is recorded below; the #166 re-check (B5.1) happened at S2 start.

- **2026-11-07 (B5 S2 checkpoint, `compono-lsx.5`):** S2 (B5.6 wiring and template) is committed at `bb67950` ("feat(xunitv3): generate AOT ComposeData registrations"), whose parent is the S0 documentation checkpoint `3d4bd28`. The design owner (Claude Opus) verified it independently and returned **APPROVE**; the product owner approved it.
  - **Scope:** B5.6 only - the generated AOT `ComposeData` registration template, emitter, and generator wiring, plus its tests. 31 files changed (29 added, including 23 new Verify snapshots; 2 modified), covering only the S2 envelope: a non-generic test class; plain `[ComposeData]`; the `Sync` member shape (field, property, method; inherited member; `MemberType`); untyped and typed (`TheoryData<…>`) rows; B5.7 without a profile; B5.8 with constants only; the `Compono.Seed` trait; and R0, too-many, and null/type-mismatch row failures (the profile-driven negative row seed is S3). No generic bases, no `TaskOf`/`ValueTaskOf`/`AsyncEnumerable` shapes, and no profile/seed/`[Shared]`/active-skip features.
    - `src/Compono.Generators/Templates/AotComposeDataRegistration.scriban` (new): the registration template - header/pragmas, the `file` class, the `[ModuleInitializer]` call, the per-shape row readers, the seeded-row scaffold `ComposeRow`, parameter binding, and the metadata block.
    - `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs` (new): the view-model builder and the S2 emission gate `IsWithinS2Envelope` (renamed to `IsWithinEmissionEnvelope` in S3, `0680dc2` - see the S3 checkpoint below).
    - `src/Compono.Generators/ComponoIncrementalGenerator.cs`: the B5.6 wiring - after the existing diagnostics step, skip when `Diagnostics.Count > 0 || !IsTestClassAccessible`, then `EmissionIsolation.TryEmit` (so an emission failure surfaces as `CMP0050` for that method only, ADR-0068).
    - `test/Compono.Generators.Tests/AotComposeDataRegistrationEmissionTests.cs` (new, 14 tests): the S2 envelope - an eligible sync non-generic shape emits exactly one registration; every out-of-envelope shape (profile forms, seed, `[Shared]`, active skip, generic base, async shapes, inaccessible class) emits none and reports no new diagnostic.
    - `test/Compono.Generators.Tests/AotComposeDataRegistrationVerifyTests.cs` (new, 10 tests) plus `GeneratorTestHelpers.cs` (`VerifyGeneratedSource`): 23 new snapshots.
    - `test/Compono.XunitV3.Aot.Tests/Support/RegisteredComposeDataFactory.cs` and `ComposeDataEmissionTests.cs` (new): the B5.10 harness and the real-runner S2 tests.
  - **Claude Opus independent-review correction round (F1/F2/F4/F5).** The review requested changes; the correction round addressed F1, F2, F4, and F5, including making **two eligibility tests non-vacuous** (each now genuinely fails when the S2 envelope predicate is widened or removed, instead of passing without exercising the intended rejection).
  - **G1 (DeepSeek-reported full-suite result):** full-solution `dotnet build`, 0 warnings, 0 errors.
  - **G2 (DeepSeek-reported full-suite result):** full-solution `dotnet test`, 5,544 total, 5,476 passed, 68 skipped, 0 failed. The working tree was clean after the run, so no unexpected `*.verified.*` changed and the AOT `[Compose]` files are untouched.
  - **G3 (Claude Opus independently verified focused tests):** 14 emission tests, 10 snapshot tests, and 23 new snapshots, with no unexpected `CMP0050`.
  - **G4 (Claude Opus independently verified):** the generated code compiles against the real `Compono`, `Compono.XunitV3.Aot`, and xUnit AOT assemblies on net9.0, net10.0, and net11.0.
  - **G5 (Claude Opus independently verified real-runner probes):** the slice's tests run and pass under the real xUnit AOT runner, 63/63 per TFM.
  - **Emission gate:** S2 emits only the `IsWithinS2Envelope` shape; every emit path gates on that single predicate. S3-S6 must each explicitly widen `IsWithinS2Envelope` as they implement their own functionality. (Historical: S3 renamed this predicate to `IsWithinEmissionEnvelope` and widened it for the S3 forms - see the S3 checkpoint below; the pre-widening S2 shape/name is what is recorded here.)
  - **#166 checkpoint (B5.1), re-checked at S2 start:** issue #166 was still OPEN (last updated 2026-11-04), with no accepted ADR and no implementation plan, so B5 keeps ownership of the seeded-row scaffold (B5.7) and no S2 design change was required. Issue #166 was not modified.
  - **Not in S2's commit (by design):** ADR, plan, bead, and snapshot-list changes. This entry is S2's documentation closeout.
  - **Open:** S3-S7 are pending; B5 remains in progress.

- **2026-11-09 (B5 S3 checkpoint, `compono-lsx.5`):** S3 (B5.7-B5.9 profile, seed, and `[Shared]` support) is committed at `0680dc2` ("feat(xunitv3): support AOT ComposeData profiles and seeds"), whose parent is the S2 documentation checkpoint `7c52b0e`. The design owner (Claude Opus) independently re-reviewed after a correction round and returned final **APPROVE**; the maintainer authorized the checkpoint commit.
  - **Scope:** 20 files changed (6 modified, 14 new Verify snapshots; 1,894 insertions, 49 deletions), covering the S3 envelope only.
    - `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs`: the emission gate renamed and widened (see below); view-model additions (`AttributeSeedExpression`, `HasNegativeAttributeSeed`, `HasConfig`, `ConfigTypeName`, `ComposerCreateExpression`, per-parameter `IsShared`).
    - `src/Compono.Generators/Templates/AotComposeDataRegistration.scriban`: per-row profile/config construction, config extraction (`ExtractConfig<TConfig>`), attribute-seed application (`WithSeed` after `AddProfile`), the negative-seed whole-body throw, and shared-first parameter resolution (`ShareExplicit`/`ResolveShared` before ordinary `Resolve`).
    - `src/Compono.Generators/ComponoIncrementalGenerator.cs`: the wiring call updated to the renamed predicate.
    - `test/Compono.Generators.Tests/AotComposeDataRegistrationEmissionTests.cs`: five former S2 no-emit tests converted to positive S3 emission tests; no-emit retained for still-out-of-envelope shapes.
    - `test/Compono.Generators.Tests/AotComposeDataRegistrationVerifyTests.cs`: five new snapshot tests (profile, profile-config+seed, seed-after-profile, negative seed, shared value/branch).
    - `test/Compono.XunitV3.Aot.Tests/ComposeDataEmissionTests.cs`: real-runner S3 success and failure coverage.
  - **S3 features implemented:** `<TProfile>` (fresh profile instance per row, `AddProfile<TProfile>()`) and `<TProfile, TConfig>` (fresh `TProfile(rowConfig)` per row); attribute `Seed` (positive, `0` treated as a real configured seed, and a negative seed whose whole factory body throws `NegativeAttributeSeed`); effective-seed precedence (attribute `Seed` pins every row, else profile `WithSeed`, else a fresh non-negative row seed; attribute `Seed` + profile `WithSeed` surfaces the core duplicate-configuration conflict as a wrapped row failure); `[Shared]` parameters resolved shared-first with row-value → `ShareExplicit`, else `ResolveShared`, and an ordinary parameter of the same type reusing the shared instance; per-row composition independence (fresh `Composer`, per-row shared scope, profile constructed once per row); negative-seed and failure propagation with the approved `Seed:` context lines and preserved `InnerException`/`Diagnostic`.
  - **Initial review findings and resolution (all seven resolved before re-review):** (1) shared-parameter ordering when an ordinary parameter precedes a `[Shared]` parameter of the same type - added a real-runner `Assert.Same` witness and a snapshot proving shared-first emission; (2) the three S0 `TConfig` identity shapes (tuple, dynamic/object, `List<dynamic>`/`List<object>`) - added real generated-registration coverage; (3) attribute `Seed = 0` - added a runtime trait assertion and a `WithSeed(0)` snapshot; (4) seed failure context - added exact-message tests proving `Seed: 42` on both a before-composition failure (config extraction) and an after-row-creation failure (positional mismatch); (5) throwing profile constructors - added ordinary-exception and diagnosed-`CompositionException` cases asserting preserved `InnerException`/`Diagnostic` and the pre-composition seed line; (6) per-row independence - added tests proving `[Shared]` instances are not reused across rows and that the profile constructor runs once per row (distinct instances); (7) low-severity cleanup - the negative-seed test now uses a member that throws if read (proving `NegativeAttributeSeed` wins without evaluating it), and the emitter's trailing newline was restored.
  - **Emission envelope rename:** `IsWithinS2Envelope` → `IsWithinEmissionEnvelope` (single authoritative emission-eligibility predicate). It now admits the S3 profile/config, `Seed`, and `[Shared]` forms while retaining the S4-S6 exclusions. The forward-looking gate text above and the S2 checkpoint Notes were annotated with the rename; S2's pre-widening shape/name is preserved as historical evidence.
  - **G1 (DeepSeek-reported full-suite result):** full-solution `dotnet build`, 0 warnings, 0 errors.
  - **G2 (DeepSeek-reported full-suite result):** full-solution `dotnet test`, 5,621 total, 5,553 passed, 68 skipped, 0 failed. The 68 skips are pre-existing; the tree was clean after the run, so no `*.verified.*` changed and the AOT `[Compose]` files are untouched.
  - **G3 (DeepSeek-reported, Claude independently verified):** generator `AotComposeData` suite 526/526, with no unexpected `CMP0050`.
  - **G4 (Claude independently verified):** the generated registrations compile against the real `Compono`, `Compono.XunitV3.Aot`, and xUnit AOT assemblies on net9.0, net10.0, and net11.0.
  - **G5 (DeepSeek-reported, Claude independently verified):** the real xUnit AOT runner executes the slice's tests - 126/126 `ComposeDataEmission` tests across the three TFMs.
  - **Claude independently verified (full focused suites):** `test/Compono.XunitV3.Aot.Tests` 252/252; generator test project 1,264/1,264; and no existing verified snapshot changed.
   - **Not in S3's commit (by design):** ADR, plan, bead, and snapshot-list changes. This entry is S3's documentation closeout.
   - **Open:** S4-S7 are pending; B5 remains in progress.

- **2026-11-09 (B5 S4 checkpoint, `compono-lsx.5`):** S4 (B5.6-B5.9 asynchronous member sources) is committed at `3fcc5d4` ("feat(xunitv3): support async AOT ComposeData sources"), whose immediate Git parent is the S3 documentation checkpoint `e721e33` ("docs(xunitv3): record ComposeData S3 checkpoint"); the S3 implementation commit is `0680dc2`. The design owner (Claude Opus) independently reviewed after a correction round and returned final **APPROVE**; the maintainer authorized the checkpoint commit. This entry is S4's documentation closeout.
   - **Scope:** 21 files changed (5 modified, 16 new Verify snapshots; 882 insertions, 29 deletions on the five source/test files before snapshot acceptance), covering the S4 envelope only.
     - `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs`: `IsWithinEmissionEnvelope` widened to admit all four return shapes (still excluding a null `ReturnShape`/`MemberKind`, generic test classes, and active `SkipUnless`/`SkipWhen`); view-model additions `IsSync`/`IsTaskOf`/`IsValueTaskOf`/`IsAsyncEnumerable`/`IsAsyncEnumerableValueElement` (the last = `AsyncEnumerable` shape with a value-type row element).
     - `src/Compono.Generators/Templates/AotComposeDataRegistration.scriban`: the row-reader selection (`ReadRows` for `Sync`, `await ReadRowsAsync` otherwise), the `TaskOf`/`ValueTaskOf` awaited reader (with the `TaskOf` null-`Task` check → D5, no null check for `ValueTaskOf`), the `AsyncEnumerable` reference-element reader (`await foreach` over the `IAsyncEnumerable<object?>` covariance fast path), and the value-element reader (`ReadValueRowsAsync<TElement>`, element type inferred) that first performs the runtime `IAsyncEnumerable<object?>` check before the manual enumerator loop.
     - `test/Compono.Generators.Tests/AotComposeDataRegistrationEmissionTests.cs`: three former async no-emit tests converted to positive emission tests, plus new `Task<object>` and value-element emission coverage.
     - `test/Compono.Generators.Tests/AotComposeDataRegistrationVerifyTests.cs`: seven new snapshot tests.
     - `test/Compono.XunitV3.Aot.Tests/ComposeDataEmissionTests.cs`: real-runner S4 success and failure coverage.
   - **S4 features implemented:** `Task<T>` member sources (including a null `Task` → D5, never awaited); `ValueTask<T>` member sources; `IAsyncEnumerable<T>` member sources on both the reference-element and value-element paths; runtime-covariance path selection (a declared value-element source whose runtime object also implements a reference `IAsyncEnumerable<T>` reads the reference rows, matching JIT); full source enumeration/materialization before any per-row composition; preservation of profiles, config extraction, `Seed` (attribute/profile/row), `[Shared]`, row metadata, and per-row isolation; the B5.9 exception/cancellation filter matrix (a member that throws `CompositionException` when invoked → wrapped; thrown while awaiting → passes through unwrapped; thrown at a reference-path `GetAsyncEnumerator` → passes through; at a value-path `GetAsyncEnumerator` → wrapped; `DisposeAsync` failure precedence: reference path replaces the original failure, value path keeps the original); and `OperationCanceledException` propagation on both async enumeration paths. `ConfigureAwait(false)` on every emitted await; no runtime reflection and no LINQ in generated code.
   - **Emission envelope widening:** `IsWithinEmissionEnvelope` (renamed from `IsWithinS2Envelope` in S3) is the single authoritative emission-eligibility predicate. S4 widened it to admit the `TaskOf`/`ValueTaskOf`/`AsyncEnumerable` (reference- and value-element) shapes while retaining the S5 active-skip and S6 generic-test-class exclusions and the zero-diagnostic/accessibility gates and per-method `CMP0050` `EmissionIsolation`.
   - **Review findings and resolution (all addressed before approval):** (1) awaited `CompositionException` for `Task<T>` and `ValueTask<T>` passes through unwrapped - added real-runner tests asserting message, preserved `Diagnostic`, and null `InnerException`; (2) `GetAsyncEnumerator` exceptions - reference path `CompositionException` passes through, value path wraps per B5.9, proven with custom enumerables that throw at `GetAsyncEnumerator`; (3) `DisposeAsync` failure precedence on each path; (4) a static mixed source whose declared type implements both a value- and a reference-`IAsyncEnumerable` proves the reference reader is selected and reads valid rows; (5) a strengthened runtime-covariance test reading rows successfully through the reference interface; (6) restored explicit `Task<object>` emission coverage (kept alongside `Task<IEnumerable<object>>`); (7) corrected two misleading emitter comments (no logic change).
   - **G1 (DeepSeek-reported full-suite result):** full-solution `dotnet build`, 0 warnings, 0 errors.
   - **G2 (DeepSeek-reported full-suite result, Claude independently re-ran):** full-solution `dotnet test`, 5,732 total, 5,664 passed, 68 skipped, 0 failed. The 68 skips are pre-existing; the tree was clean after the run, so no `*.verified.*` changed and the AOT `[Compose]` files are untouched.
   - **G3 (DeepSeek-reported, Claude independently verified):** generator test project 1,282/1,282 across net10.0/net11.0, with no unexpected `CMP0050`; no existing verified snapshot modified.
   - **G4 (DeepSeek-reported, Claude independently verified):** the generated registrations compile against the real `Compono`, `Compono.XunitV3.Aot`, and xUnit AOT assemblies on net9.0, net10.0, and net11.0.
   - **G5 (DeepSeek-reported, Claude independently verified):** the real xUnit AOT runner executes the slice's tests - 345/345 (115 per target framework).
   - **Claude independently ran** the full build, the full suite, the generator tests, and the real AOT runner, and issued **APPROVE**.
   - **Known limitation - reflection wrapping (accepted, low severity, not an S4 blocker):** JIT uses `MethodInfo.Invoke` for member and value-path enumerator calls, so a synchronous exception may arrive wrapped in a `TargetInvocationException`; AOT direct calls expose the original exception to the exception filters. This can produce a different wrapper or `InnerException` identity for synchronous `CompositionException`/`OperationCanceledException` failures. Compiler-generated async methods and iterators generally surface failures through tasks and avoid the discrepancy. S4 intentionally changed neither JIT nor AOT production semantics. Recorded as a known remaining risk; no new implementation task was created and it was not assigned to S5.
   - **Known limitation - cancellation (nonblocking test limitation):** the S4 tests cover `OperationCanceledException` propagation on both async enumeration paths, but they simulate cancellation by throwing `OperationCanceledException` rather than exercising a real cancelled `TestContext` token. The per-row `ThrowIfCancellationRequested()` check is emitted according to the plan, but real-token observation remains untested and is not claimed to have been independently verified; it may be considered during B6 validation.
   - **Not in S4's commit (by design):** ADR, plan, bead, and snapshot-list changes. This entry is S4's documentation closeout. The documentation changes in this entry are not yet committed.
   - **Open:** S5-S7 are pending; B5 remains in progress.
- **2026-11-09 (B5 S5 review and interface skip-holder design decision, `compono-lsx.5`):** S5 is implemented on top of `fc8fe54` and is **uncommitted**. The design owner (Claude Opus) independently reviewed it and returned **REQUEST CHANGES**. The maintainer did not accept deferring finding F1 and decided its design the same day. S0–S4 checkpoint history and status are unchanged.
   - **F1 (blocking): interface skip holders.** An active `SkipUnless`/`SkipWhen` naming a `static virtual` or `static abstract` interface property produced generated code failing with `CS8926` instead of a diagnostic. B4/S0 skip validation (`ResolveSkipProperty`/`IsUsableSkipProperty`) never checked for static interface members, and S5 made the shape emittable.
     - **Investigation probes:** real xUnit 4.0.1 JIT runner, with `[ComposeData]` and native `[Fact]`:
       - `static virtual` runs with its condition honored, including on a closed generic interface and on an interface that also carries an unimplemented `static abstract`
       - `static abstract` fails at run time (`BadImageFormatException`)
       - base-interface and implementing-class holders fail with "Cannot find public static property" (xUnit1054 on `[Fact]`)
       - plain static interface properties run
     - **xUnit's own AOT generator** emits the same `CS8926` for a native `[Fact]` with a `static virtual` interface `SkipType`.
     - **Parity options evaluated:**
       - a constrained generic accessor: `CS8920` for interfaces with unimplemented `static abstract` members, and `TypeLoadException` at Native AOT run time on net9.0/net10.0 with 0 publish warnings
       - `[UnsafeAccessor]`: worked on JIT and Native AOT on net9.0–net11.0, but needs a generic accessor class for generic interfaces and would be a new generated-code pattern
   - **Decision (product owner, 2026-11-09), recorded in ADR-0070 Amendment 4 §7:**
     - no `UnsafeAccessor` support
     - plain static interface skip properties are supported through direct access
     - `static virtual` → `CMP0052` twin, an explicit **AOT-only parity exception** (§7a; the sixth in Amendment 4 §2's list)
     - `static abstract` → the same `CMP0052` twin, **not** a parity exception, because JIT also fails it (§7b)
     - base-interface and implementing-class holders keep the existing `CMP0057`
     - specified in B5.4 (`ComposeDataStaticInterfaceSkipProperty`, predicate, precedence 9b) and the B5.11 S5 row
   - **Required S5 test corrections (F2/F3, specified in the B5.11 S5 row):**
     - `DisableParallelization`: row `false` overrides attribute `true`
     - real-runner coverage for a skip property inherited from the test class's base class
     - real-runner coverage for a `SkipType` that inherits its property from a base class
     - real-runner coverage for a plain `[Obsolete]` skip property under the project's `CS0612`/`CS0618` warnings-as-errors

     Each shape compiled and passed in a design-owner probe, so these are coverage gaps, not defects.
   - **`AnalyzerReleases.Unshipped.md` (implementation phase, not yet changed):** the existing `CMP0052` row's note gains the skip-property condition. The rest of the row stays unchanged. Proposed note: `A [ComposeData] member is not public and static, requires arguments, or is a generic method - ComposeData can only invoke a public static field, property, or parameterless non-generic method; also covers a selected member, or an active SkipUnless/SkipWhen property, that is a static abstract or static virtual interface member, which generated code cannot call (ADR-0070/PLAN-0071 B4/B5)`. No new rows, because the file is keyed by ID.
   - **Known follow-up risk (F4, outside S5, not scheduled):** `ResolveSkipProperty` stops at the most-derived property with the requested name, whatever its accessibility. When a derived holder declares a non-public static property that hides a public static `bool` property of the same name on a base class, AOT reports `CMP0057`. JIT's public-only reflection lookup would skip the hidden member and read the base property. That's a false compile-time rejection of usage JIT accepts, not miscompiled code. It was already present in S0 and is unverified by a runner probe. It doesn't affect interface holders (an interface's `BaseType` is null, and JIT doesn't search base interfaces either). It needs its own decision before any change. S5 must not change `ResolveSkipProperty`'s lookup.
   - **Plan housekeeping:** B5.9's reference to a nonexistent "Remaining risks" section now points to the Notes entries that record those risks: the 2026-11-06 B5 specification entry's decision (c), and the S4 checkpoint's known limitations.
   - **Open:** S5's corrections are pending implementation and re-review; S6–S7 are pending; B5 remains in progress.

- **2026-11-09 (B5 S5 checkpoint, `compono-lsx.5`):** S5 (B5.8 active skip conditions and metadata) is committed at `07ff636f` ("feat(xunitv3): support conditional AOT ComposeData skips"), whose parent is the S5 design-amendment commit `f65fbf8` ("docs(xunitv3): clarify S5 interface skip holder compatibility"). The design owner (Claude Opus) independently re-reviewed after the F1–F3 correction round and returned final **APPROVE**; the product owner authorized the checkpoint commit. The branch `feat/163-composedata` was pushed to `origin` (upstream established; remote HEAD `07ff636f`). This entry is S5's documentation closeout.
  - **Slice state:** S0–S5 completed; S6–S7 pending; B5 remains in progress.
  - **Scope:** 21 files changed (9 modified, 12 new Verify snapshots; 2,062 insertions, 29 deletions across the slice).
    - `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs`: added the `CMP0052` skip-property twin `ComposeDataStaticInterfaceSkipProperty`.
    - `src/Compono.Generators/Discovery/AotComposeDataMethodDiscovery.cs`: inserted the `CMP0052` check at precedence 9b in both the `SkipUnless` and `SkipWhen` step-9 blocks (after the 9a open-generic `CMP0054` check, before the 9c prohibited-property `CMP0047` checks).
    - `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs`: widened `IsWithinEmissionEnvelope` to admit active attribute-level `SkipUnless`/`SkipWhen`; added `RenderSkipCondition`, which emits `static () => global::<HolderFqn>.<EscapedProperty>` for a recorded active condition (the holder is the property's declaring type; the name is escaped).
    - `src/Compono.Generators/AnalyzerReleases.Unshipped.md`: the existing `CMP0052` row's note extended (no new rows).
    - `test/Compono.Generators.Tests/{AotComposeDataAmendment4DiagnosticTests,AotComposeDataRegistrationEmissionTests,AotComposeDataRegistrationVerifyTests}.cs` and `test/Compono.XunitV3.Aot.Tests/{Compono.XunitV3.Aot.Tests.csproj,ComposeDataEmissionTests.cs}`.
  - **S5 functionality implemented:**
    - Attribute-level `SkipUnless`/`SkipWhen` rendered as static, non-capturing predicate delegates (the shared helper still owns the single metadata merge rule; the Scriban template was not changed).
    - Correct conditional-skip timing: predicates are bound but never evaluated during composition; only the xUnit runner evaluates them.
    - Row/attribute skip-group precedence (the row group when the row's `Skip` is set, else the attribute group, else none).
    - `SkipType` and inherited holders (the emitted accessor targets the property's declaring type).
    - Metadata preservation (`Explicit`, `Label`, `TestDisplayName`, `Timeout`, the traits union including `Compono.Seed`, `DisableParallelization`).
    - Plain (non-virtual) static interface skip properties are supported through direct access.
    - `CMP0052` for static `virtual`/`abstract` interface skip properties (instead of generated `CS8926`).
    - No `UnsafeAccessor` introduced.
    - Diagnostic precedence (9a `CMP0054` > 9b `CMP0052` > 9c `CMP0047`) and per-method isolation (`CMP0050`) preserved.
  - **Accepted JIT/AOT exception:** an active `SkipUnless`/`SkipWhen` naming a `static virtual` interface property is supported on JIT (condition honored) but reported as `CMP0052` on AOT - the explicit AOT-only parity exception recorded in ADR-0070 Amendment 4 §7a (the sixth in Amendment 4 §2's list). A `static abstract` interface property yields the same `CMP0052` but is **not** a parity exception, because JIT also fails it at run time (§7b).
  - **Review corrections (all resolved before approval):**
    - **F1:** the `CMP0052` `ComposeDataStaticInterfaceSkipProperty` diagnostic, with the full generator diagnostic acceptance matrix, a plain-static-interface Verify snapshot, and a real-runner test.
    - **F2:** `DisableParallelization` row override - a row `false` overrides an attribute `true` (merged `false`), distinguished from a Boolean OR.
    - **F3:** expanded real-runner coverage - a skip property inherited from the test class's base class, a `SkipType` inheriting its property from a base class, and a plain `[Obsolete]` skip property compiled under the project's `CS0612`/`CS0618` warnings-as-errors.
    - **F4:** the pre-existing name-hiding mismatch in `ResolveSkipProperty`'s lookup remains a documented follow-up risk, deferred outside S5; the lookup algorithm was not changed.
  - **Accepted nonblocking test-coverage observations (Claude Opus's final S5 APPROVE review; low severity; no further S5 changes required):**
    - (1) The "no registration emitted" assertion is only exercised for `static virtual` interface skip properties, not `static abstract`. Nonblocking because generation excludes any method that reports a diagnostic, and the `static abstract` `CMP0052` diagnostic is independently tested.
    - (2) The `CMP0054`-over-`CMP0052` and `CMP0052`-over-`CMP0047` diagnostic precedence tests exercise only `SkipUnless`, not `SkipWhen`. Nonblocking because both paths use equivalent discovery logic, and `SkipWhen` has its own exact-message `CMP0052` diagnostic test.
  - **Validation (G1–G5, independently re-run by Claude Opus for approval):**
    - **G1:** full-solution `dotnet build`, 0 warnings, 0 errors.
    - **G2:** full-solution `dotnet test`, 5,834 total, 5,766 passed, 68 skipped, 0 failed; no pre-existing `*.verified.*` modified; the AOT `[Compose]` files untouched.
    - **G3:** generator test project 1,324/1,324 across net10.0/net11.0, with no unexpected `CMP0050`; no existing verified snapshot modified.
    - **G4:** the generated registrations compile against the real `Compono`, `Compono.XunitV3.Aot`, and xUnit AOT assemblies on net9.0, net10.0, and net11.0.
    - **G5:** the real xUnit AOT runner executes the slice's tests - 405/405 (135 per target framework).
  - **Not in S5's implementation commit (by design):** this PLAN-0071 checkpoint entry (uncommitted, for maintainer review). The S5 implementation commit contains the 9 modified files and 12 new snapshots only.
  - **Open:** S6–S7 are pending; B5 remains in progress. This S5 documentation checkpoint is uncommitted pending maintainer review.

- **2026-11-09 (B5 S6 checkpoint, `compono-lsx.5`):** S6 (B5.3 generic test-class bases) is committed at `20c1eb9331e2c4374f7832e25ae6093949672e8d` ("feat(xunitv3): support generic AOT ComposeData test classes"), whose parent is the S5 documentation checkpoint `b1f4bb3` ("docs(xunitv3): record ComposeData S5 checkpoint"). The implementation was independently reviewed by Claude Opus, which returned final **APPROVE** with no blocking findings; the product owner authorized the S6 implementation checkpoint commit and push. The branch `feat/163-composedata` was pushed to `origin` (fast-forward; remote HEAD `20c1eb9`). This entry is S6's documentation closeout.
  - **Slice state:** S0–S6 completed; S7 pending; B5 remains in progress. S7 remains the final Native AOT publish validation slice.
  - **Scope:** 8 files changed (4 modified, 4 new verified snapshots; 741 insertions, 26 deletions):
    - `src/Compono.Generators/Emitters/AotComposeDataRegistrationEmitter.cs`.
    - `test/Compono.Generators.Tests/AotComposeDataRegistrationEmissionTests.cs`.
    - `test/Compono.Generators.Tests/AotComposeDataRegistrationVerifyTests.cs`.
    - `test/Compono.XunitV3.Aot.Tests/ComposeDataEmissionTests.cs`.
    - Four new `AotComposeDataRegistrationVerifyTests` verified snapshots: `GenericTestClass_InheritedMemberFromNonGenericRoot_RendersResolverRegistration` and `GenericTestClass_MemberType_RendersResolverRegistration`, each with the `AotComposeDataRegistration` and `RowInvokerRegistration` outputs. No pre-existing verified snapshot was modified.
  - **S6 functionality implemented:**
    - Generic test-class emission eligibility.
    - Open-generic registration indices.
    - Reuse of the existing `ResolveGenericTestClassType` closure mechanism.
    - The closed constructed test class flowing into composition and diagnostics.
    - Generic base inheritance.
    - Multiple generic arguments.
    - Nested generic test classes.
    - Generic constraints.
    - `MemberType` and inherited non-generic member holders.
    - The existing unsupported-shape diagnostics.
    - No new public API.
    - No changes to the Scriban template.
  - **Validation (G1–G5):**
    - **G1:** full-solution `dotnet build`, 0 warnings, 0 errors.
    - **G2:** full-solution `dotnet test`, 5,882 total, 5,814 passed, 68 skipped, 0 failed.
    - **G3:** generator tests passed across net10.0/net11.0; no unexpected `CMP0050`.
    - **G4:** generated-code compilation against the real `Compono`, `Compono.XunitV3.Aot`, and xUnit AOT assemblies on net9.0, net10.0, and net11.0.
    - **G5:** the real xUnit AOT runner passed across net9.0/net10.0/net11.0; reported runner count 145 passed per target framework.
    - Claude Opus independently verified G1/G2, the focused generator tests, and runner execution across all three TFMs.
    - **Additional independent probe (distinct from the S7 gate):** Claude Opus published and executed an isolated net10.0 Native AOT probe with 0 IL/AOT warnings and 11/11 passing. This is explicitly **not** the S7 real-suite Native AOT publish gate (a full publish and run of the real test suite/project), which remains pending.
  - **Accepted nonblocking observations (Claude Opus's final S6 APPROVE review; accepted as nonblocking, no fixes implemented):**
    - **(A) Nested inherited theory discovery:** xUnit AOT 4.0.1 does not discover theories inherited from nested base classes, including ordinary non-generic nested base classes. The nested generic theory was therefore **not** executed through normal xUnit discovery. Direct registered-factory tests verify its registration key and resolver. End-to-end theory execution coverage is **not** claimed for this nested shape.
    - **(B) Nested generic coverage gaps:** `Outer<>.InnerBase` and `Outer<>.Inner<>` have index-rendering/probe evidence but no repository-level emission or real-compilation tests for those exact shapes. Claude Opus independently confirmed that both compile and register correctly. `xUnit1032` prevents normal theory execution for these shapes.
    - **(C) Comment accuracy:** an emitter comment groups `CMP0047` with generic-shape diagnostics. `CMP0047` concerns obsolete/experimental usage, not generic-shape eligibility. This is a harmless documentation nit and is deferred.
  - **Not in S6's commit (by design):** ADR, plan, research, and bead changes. This entry is S6's documentation closeout.
  - **Open:** S7 is pending; B5 remains in progress. This S6 documentation checkpoint is uncommitted pending maintainer review.
