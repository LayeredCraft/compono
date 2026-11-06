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
- **Failure construction:** wrap the cause into `CompositionException`,
  keeping `InnerException` = the cause and `Diagnostic` copied when the
  cause is a `CompositionException` (reuse
  `CompositionException.WithSeedInMessage` semantics), with the formats
  below.

### Messages (byte-identical on both engines; `{M}` = `Namespace.Type.Member`, `{T}` = test method display name as `BindingPlan.MethodDisplayName`)

- **D1, member not found:** `ComposeData member '{M}' for '{T}' was not found. Expected a public static field, property, or parameterless method.`
- **D2, not public static:** `ComposeData member '{M}' for '{T}' must be public and static.`
- **D3, needs arguments:** `ComposeData member '{M}' for '{T}' requires arguments, which ComposeData does not support.`
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
| `CMP0052` | not public static; requires arguments; generic member method | `ComposeData member '{0}' for '{1}' must be public and static.` / `… requires arguments, which ComposeData does not support.` / `… is a generic method; ComposeData cannot supply type arguments.` |
| `CMP0053` | unsupported return type or row element type | `ComposeData member '{0}' for '{1}' has unsupported {return/row element} type '{2}'. Expected IEnumerable, Task/ValueTask of IEnumerable, or IAsyncEnumerable of ITheoryDataRow, object?[], or ITuple rows.` |
| `CMP0054` | open-generic `MemberType` | `ComposeData MemberType '{2}' for '{1}' is an open generic type; supply a closed type.` |
| `CMP0055` | `TConfig` statically incompatible with the typed row's element 0 | `ComposeData member '{0}' for '{1}': row element 0 type '{2}' can never be the '{3}' profile configuration value.` |
| `CMP0056` | statically known too many values; a typed row value that can never be assigned to its parameter | `ComposeData member '{0}' for '{1}' supplies {n} row value(s), but the method has {p} parameter(s).` / `ComposeData member '{0}' for '{1}': row value {i} type '{2}' can never be assigned to parameter '{name}' of type '{3}'.` |
| `CMP0057` | invalid **active** attribute-level skip condition (B4.9) | `[ComposeData…] on '{1}' sets both SkipUnless and SkipWhen.` / `[ComposeData…] on '{1}': {SkipUnless/SkipWhen} '{prop}' must be a public static bool property on '{holder}' or one of its base types.` |

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

**B4.10 `[Shared]`** (C5): two or more `IsShared` parameters of the same
type (`SymbolEqualityComparer.Default`: `string`/`string?` are the same
type, `int`/`int?` are not) → `CMP0040` (twin) with JIT's reason text.

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

- [ ] Plan pipeline: the three `ComposeMethodDiscovery` AOT `ComposeData` providers exist. A composed custom parameter type of an AOT `[ComposeData]` method gets a generated plan, **including when the method fails validation**. A plan for a type shared with a valid `[Compose]` method is still emitted when the `ComposeData` method is invalid
- [ ] A JIT-namespace `[Compono.XunitV3.ComposeData("Missing")]` method (the `SeedSampleMethods` shape) produces no `CMP0051`–`CMP0057` and no AOT `ComposeData` model
- [ ] Incremental-caching test: an unrelated edit leaves the `ComposeData` per-method steps `Cached`/`Unchanged` (`TrackingNames`)
- [ ] Model tests cover every B4.2 field, including `Seed` unset → `null`, `Seed = 0` → `0`, `Seed = -1` → `-1` with no diagnostic, and every metadata field present vs. absent
- [ ] Member resolution: one generator test per probe row 01–29 (Notes, 2026-11-06), asserting the outcome recorded there. Valid rows also assert `MemberContainingTypeFqn` (rows 10/11/21 → the base class). Inherited test method with a base-declared member: valid. Derived-only member without `MemberType`: `CMP0051`. With `MemberType`: valid
- [ ] Return/element shapes (B4.5/B4.6). Valid: `Task<object>`, `ValueTask<TheoryData<…>>`, `IAsyncEnumerable<object[]>`, `List<string[]>`, a `TheoryData` subclass, `IEnumerable<object>`. `CMP0053`: `string`, a single `TheoryDataRow`, non-generic `Task`, `Task<int>`, a declared `object`, `IEnumerable<int>`, `IEnumerable<string>`. `CMP0054`: `typeof(Holder<>)`
- [ ] Assignability (B4.7). Valid: `int?`→`int`, `int`→`int?`, `string?`→`string`, subclass→base, element `object`, struct `TConfig` with element `TConfig?`. `CMP0056`: `int`→`long`, too many values with and without `TConfig`. `CMP0055`: unrelated `TConfig`
- [ ] Profile (B4.8): `CMP0042`/`CMP0044`/`CMP0046`/`CMP0047`/`CMP0048`/`CMP0049` through the ComposeData twins (wording asserted). **No** diagnostic for `TConfig` = `DateTimeOffset`, an interface, or a two-constructor record
- [ ] `CMP0040` twin: generic test method, ref/out/in, `params`, duplicate `[Shared]` (`string` + `string?` → diagnostic; `int` + `int?` → none)
- [ ] `CMP0045`: every pairing among the six AOT forms (ComposeData twin text whenever a `ComposeData` form is involved); no ComposeData model emitted as valid. `[InlineData]`/`[MemberData]` alongside `[ComposeData]` → no diagnostic
- [ ] Skip (B4.9): valid active conditions on the test class, a base class, and a `SkipType` class → no diagnostic. Inactive conditions (no attribute `Skip`), including a missing property → no diagnostic and nothing recorded. Active: both set, missing, instance, non-public, non-`bool` → `CMP0057`
- [ ] `CMP0044` twin for a private nested `MemberType` holder, a holder reached only through a private nested type, and an active private `SkipType` holder. Internal holders are valid
- [ ] Precedence (B4.11): one test with several simultaneous faults per adjacent pair in the order, asserting only the first is reported
- [ ] **All existing `CMP0040`–`CMP0049` Verify snapshots byte-identical** (twins, C-3). Existing AOT `[Compose]` snapshots unchanged
- [ ] `AnalyzerReleases.Unshipped.md`: `CMP0051`–`CMP0057` added; the `CMP0040`/`CMP0042`/`CMP0044`–`CMP0049` notes say they also apply to `ComposeData`
- [ ] `dotnet build` 0 warnings / 0 errors; `dotnet test` green

#### B5 (`compono-lsx.5`, after B4 and B1): AOT emission

- [ ] **Coordination checkpoint (#166):** before writing the seeded-row scaffold, check #166's status. If #166 has landed (or has an `Accepted` design for the scaffold), reuse it. Otherwise build it here as a reusable emitter component, and note in #166 that it should adopt it. Record the outcome in Notes
- [ ] Reusable emission components (emitter helpers or Scriban partials): member invocation (direct call; `await` for `Task`/`ValueTask`; `await foreach … WithCancellation(TestContext.Current.CancellationToken)` for `IAsyncEnumerable`), row loop with index, config extraction, typed value binding through the helper's `BindValue<T>`, `[Shared]`-first ordering (`ShareExplicit<T>`/`ResolveShared<T>`), composed `Resolve<T>` with literal descriptors, seeded-row scaffold (`WithSeed` when `Seed` is set, negative check, `CreateRow`, failure wrapping per R0, `Compono.Seed` trait), and attribute metadata passed into the shared helper's single merge rule (constants rendered, `SkipUnless`/`SkipWhen` rendered as `() => global::T.Prop`). **Not** through `DataAttributeRegistration.CreateDataRow`, whose field-by-field merge diverges from JIT. If valid skip metadata can't be emitted with full parity, **stop and bring it to the product owner**. There's no preapproved diagnostic fallback
- [ ] New template `AotComposeDataRegistration.scriban` (or an equivalent emitter), `file`-scoped registration class, `global::`-qualified, `disableDiscoveryEnumeration: true`, low-allocation (pre-sized arrays, no LINQ)
- [ ] Per-row isolation in generated code: a new profile, composer, and row per iteration
- [ ] **Not** wired into AOT `[Compose]`. Leave its template unchanged
- [ ] Generator snapshot tests for each form × each member shape × `[Shared]`/no-`[Shared]` × typed/untyped rows. Compile the generated code in-test (no `CS` errors)
- [ ] ADR-0068 isolation: emission failures surface as `CMP0050` for that item only
- [ ] B4 hand-off obligations: emit only for models with zero diagnostics; call the member through `MemberContainingTypeFqn`, never the lookup type; render `{M}`/`{T}` as `typeof(…).FullName` (B4.3); never textually name a typed row element type that B4 hasn't accessibility-checked (bind through parameter types, which `CMP0013`/`CMP0040` cover); render only the active skip conditions B4 recorded; decide and record how `DisableParallelization` is rendered (JIT always supplies the attribute's non-null `bool` on xUnit 4.x) and where the negative-seed message lives (the JIT text is a literal in `ComposeDataAttribute`, not in the shared helper)

#### B6 (`compono-lsx.6`, after B5 and B3): AOT real-runner tests, samples, Native AOT

- [ ] `test/Compono.XunitV3.Aot.Tests/ComposeDataTests.cs`: the same behavioral matrix as B3 (plain `Assert`), except assembly fixtures (xunit/xunit#3664, with a comment) and xUnit argument disposal (documented difference, with an assertion that AOT doesn't dispose)
- [ ] Cross-engine message parity: a test in each project asserts the exact R0 text for the same failing scenario
- [ ] Cross-engine metadata parity: the same metadata matrix as B3 (skip conditions with and without `SkipType`, grouped precedence, explicit, timeout, traits, labels) yields the same skipped/run/explicit outcomes and display names on AOT
- [ ] Real-runner skip parity: mirror the JIT baseline B3 established in `test/Compono.XunitV3.Tests/ComposeDataTheoryTests.cs` (`ComposeDataRowSkipConditionTests`, `ComposeDataAttributeSkipTypeTests`, `ComposeDataSkipPrecedenceTests`, `ComposeDataTheoryLevelSkipTests`) — row-level and attribute-level `SkipUnless`/`SkipWhen` with `SkipType` on a different class, grouped-skip precedence (unconditional row `Skip` wins), and theory-level `[Theory(Skip = …)]` conditions combined with unconditional row `Skip` — and assert identical run/skip outcomes and skip reasons on JIT and AOT
- [ ] **Theory-level `SkipUnless`/`SkipWhen` interaction tests** (RESEARCH-0038 §2.6 item 3, §9.2 S2), mirrored in both test projects: a `[Theory(Skip = …, SkipUnless = …)]` and a `[Theory(Skip = …, SkipWhen = …)]` (condition true and false), each combined with (a) no `ComposeData` skip, (b) a row `Skip` with no condition, (c) a row `Skip` with its own condition, (d) an attribute `Skip` with and without a condition. Assert identical run/skip outcomes and skip reasons on JIT and AOT. If the planned approach can't reach parity, **stop and request review**. No speculative workarounds
- [ ] Seed parity: the same seed-line variants as B3, with identical text
- [ ] Name parity: `typeof(…).FullName`-based `{M}`/`{T}` text matches JIT under JIT-run **and** real Native AOT, for a nested test class and a closed-generic holder. Inherited test methods **without** `MemberType` are excluded from `{M}` byte parity (C-1: JIT names the concrete class, AOT the declaring class); add one test per engine that documents that difference
- [ ] ADR-0070 Amendment 2 documenting tests (runtime side; the AOT compile-time side is B4's `CMP0051`/`CMP0044` tests): (a) an inherited test method with a member that a derived class hides, without `MemberType`, asserting each engine's actual resolution (JIT: the derived member; AOT: the declaring-class member); (b) the same inherited test with an explicit `MemberType`, asserting identical rows on both engines; (c) on JIT only, a private nested `MemberType` holder that runs (AOT can't compile it, `CMP0044`). Each test carries a comment naming Amendment 2
- [ ] Samples: `test/Compono.XunitV3.SampleTests/ComposeDataTests.cs` and `test/Compono.XunitV3.Aot.SampleTests/ComposeDataTests.cs` (documentation-quality: the `TConfig` pricing table, the email table with `[Shared]`, an async source, a fixture-backed row)
- [ ] V3: real Native AOT publish and run of `Compono.XunitV3.Aot.SampleTests`, 0 IL/AOT warnings, all `ComposeData` tests executed (count them in the output)
- [ ] V7: pack-and-consume, a throwaway consumer outside the repo using both packages from a local feed (`AGENTS.md` generator rule)

### Phase 3: docs, skill, validation

#### B7 (`compono-lsx.7`, after B3 and B6): documentation and API reference

- [ ] `docs/packages/compono-xunitv3.md`: a "Composed theory rows (`[ComposeData]`)" section (forms, collection vs. row shapes, row layout, binding and nullable/value-type rules, effective-seed precedence and seed-line meanings, the row-vs-attribute metadata table including `SkipType`/`SkipUnless`/`SkipWhen`, labels, failure granularity, disposal note, mixing with xUnit attributes). Replace the "No stacking…" bullet with a pointer
- [ ] `docs/packages/compono-xunitv3-aot.md`: the same section, the diagnostics table (`CMP0051`–`CMP0057`, plus the ComposeData uses of `CMP0040`/`CMP0042`/`CMP0044`–`CMP0049`), the JIT/AOT differences (disposal, assembly fixtures, inherited test methods resolving the member on the declaring class with `MemberType` as a fixed-holder workaround only, private/protected nested holders rejected with `CMP0044`, the C-4 skip residual), and "Not yet supported" updated: AOT `[Compose]` inline values and `[Shared]` remain a follow-up (record it explicitly). Seed status per #166
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
  `src/Compono.Generators/Templates/AotComposeDataRegistration.scriban` (+ emitter, model)
- Modified: `src/Compono.XunitV3/Binding/BindingPlan.cs` (exclusivity);
  both `.csproj`s (link, description);
  `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `AotComposeMethodDiscovery.cs`, `Diagnostics/DiagnosticDescriptors.cs`,
  `AnalyzerReleases.Unshipped.md`, `ComponoIncrementalGenerator.cs`
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
