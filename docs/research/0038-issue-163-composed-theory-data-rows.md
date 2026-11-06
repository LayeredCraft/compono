# RESEARCH-0038: Composed Theory Data Rows for xUnit v3 (Issue #163)

**Status:** Concluded (2026-11-05). Product-owner interview (§9) and
review rounds 1 (§9.1) and 2 (§9.2) are complete. Recorded as
[ADR-0070](../adr/0070-xunitv3-composedata-composed-theory-rows.md)
(**Accepted** 2026-11-05).
Implementation is planned in
[PLAN-0071](../plans/0071-xunitv3-composedata-composed-theory-rows-impl-plan.md)
(Not Started). Review round 1 added §2.6 (metadata), §2.7 (row shapes),
and §5.1 (effective seed).

**Issue:** [#163](https://github.com/LayeredCraft/compono/issues/163)
(child of [#161](https://github.com/LayeredCraft/compono/issues/161), item 3).
**Builds on:** [RESEARCH-0036](0036-issue-161-test-composition-gaps-intake.md)
(intake), [RESEARCH-0037](0037-issue-162-runtime-fixture-values-in-profiles.md)
§4–§6 and §12 (fixture lifecycle; the delayed-enumeration constraint),
[ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md) ("Constraint
carried forward").
**Governing process:**
[`docs/architecture/capability-admission.md`](../architecture/capability-admission.md).
Gate B (evidence) is met by #161's migration. Gate A is assessed in §8.

**Release constraint (product owner, final):** all of #161's children
(#162–#166) ship together in one Compono release. No interim release.

## 1. Summary of findings

- **xUnit never merges rows across data attributes.** On every supported
  engine (JIT 3.2.2 and 4.0.1, AOT 4.0.1), each data attribute (or each
  AOT-registered row factory) produces complete, independent rows. So the
  #163 candidate "`[InlineData]`/`[MemberData]` supplies the leading values
  and `[Compose]` fills the rest" can't be built on top of xUnit. It would
  require Compono to own a theory discoverer. Rejected (§2.1).
- **One non-discoverable source collapses the whole theory into one
  delay-enumerated test case**, on both engines. Every row of every data
  attribute on that method is then built inside one `CreateTests()` call,
  at execution time, before the first row runs (§2.2).
- **Consequence for failure reporting:** a composition failure in any row
  aborts that `CreateTests()` call, so the whole theory test case fails and
  no row runs. A per-row failure would need a fake row or a custom invoker.
  The product owner rejected both (§9, Q12).
- **Execution-time composition is already where #162 needs it.** Composing
  inside the delayed `GetData`/row factory keeps fixtures available
  (RESEARCH-0037 §12). Nothing in the chosen design pre-enumerates.
- **JIT xUnit disposes theory arguments; AOT xUnit core does not.**
  `XunitDelayEnumeratedTheoryTestCase.CreateTests` calls
  `DisposalTracker.AddRange(data)` for every row on 3.2.2 and 4.0.1, so any
  `IDisposable`/`IAsyncDisposable` argument, composed or literal, is disposed
  by xUnit after the test case. `TheoryTestCaseFactory.GenerateDelayEnumerated`
  (AOT) adds nothing. This is pre-existing behavior for `[Compose]` today
  and isn't Compono's (§2.4).
- **Row metadata is reusable, but the engines don't merge it the same
  way** (§2.6). JIT groups the skip properties
  (`Skip`/`SkipType`/`SkipUnless`/`SkipWhen`) as one unit, while AOT merges
  them field by field. Row-level `SkipUnless`/`SkipWhen` are member-name
  strings on JIT and `Func<bool>` delegates on AOT. xUnit's own JIT
  `ConvertDataRow` silently drops row-level `SkipType`/`SkipUnless`/`SkipWhen`.
  `DisableParallelization` exists only on xUnit 4.x. So Compono must own one
  merge rule and implement it on each engine, not inherit either engine's
  behavior. A `Label` replaces the argument-based display name entirely
  (`XunitTestMethod.GetDisplayName`), which gives deterministic row names
  even though composed arguments are random (§2.3).
- **Seed precedence has no override.** An attribute `Seed` combined with a
  profile `WithSeed` is a `DuplicateConfigurationOption` conflict in core
  (`CompositionBuilder.Build`), not "one wins". The effective seed is
  exactly one of: attribute `Seed`, profile `WithSeed`, or a fresh per-row
  seed (§5.1).
- **Row shapes verified empirically** on xUnit 3.2.2 and 4.0.1 (§2.7).
- **AOT member data is direct-call, not reflection.** xUnit's own AOT
  `MemberDataAttributeGenerator` accepts public static fields, properties,
  and methods, including `Task`/`ValueTask` and `IAsyncEnumerable` results,
  and emits a direct call. AOT runtime exposes the public
  `Xunit.v3.DataAttributeRegistration.CreateDataRow(...)` for `object?[]`,
  `ITuple`, and `ITheoryDataRow` rows. It merges row over attribute field by
  field. Compono's generator can use the same direct-call approach (§2.5),
  but not that merge, because it differs from JIT (§2.6).
- **AOT parity needs one additive core API.** `Composer.CreateRow`,
  `CompositionRow.Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>`, and
  `CompositionException.WithSeedInMessage` are all public, so generated code
  can bind row values, `[Shared]` parameters, and seeds directly (§3). Building
  the R0 row-failure wrapper additionally needs a public
  `CompositionException(string message, CompositionDiagnostic? diagnostic,
  Exception innerException)` constructor, so the cause is preserved as
  `InnerException` and the exact `Diagnostic` reference is carried through
  (ADR-0070 Amendment 1 - the sole approved core change for #163).
- **AOT [Compose] today** has no inline values, no `[Shared]`, and no seed
  reporting (no `Seed:` line, no `Compono.Seed` trait, no `Seed` property).
  The seed gap is #166.

## 2. xUnit v3 theory lifecycle (decompiled, verified)

Sources: `xunit.v3.extensibility.core` 3.2.2 and 4.0.1 (`xunit.v3.core.dll`,
netstandard2.0), `xunit.v3.extensibility.core.aot` 4.0.1
(`xunit.v3.core.aot.dll`, net9.0), `xunit.v3.common` 3.2.2 / 4.0.1, and the
AOT source generator `xunit.v3.core.aot.generators.dll`
(`xunit.v3.core.aot.mtp-v2` 4.0.1). Decompiled with `ilspycmd` into the
session scratchpad. Nothing was added to the repo.

### 2.1 Discovery: data attributes are independent

`TheoryDiscoverer.Discover` (JIT):

1. If the theory is statically skipped, or pre-enumeration is off, or
   `ITheoryAttribute.DisableDiscoveryEnumeration` is set, it returns
   `CreateTestCasesForTheory(...)`: one `XunitDelayEnumeratedTheoryTestCase`.
2. Otherwise it loops over `testMethod.DataAttributes`. **The first
   attribute whose `SupportsDiscoveryEnumeration()` is `false` makes it
   return `CreateTestCasesForTheory(...)` for the entire method.** The same
   happens for non-serializable data, data registering disposables at
   discovery, or any discovery exception.
3. Otherwise it makes one `XunitTestCase` per row, per attribute, with no
   merging between attributes.

AOT (`TheoryTestCaseFactory.GenerateTestCases`): if **any** registered
factory for the method has `DisableDiscoveryEnumeration = true` (or
pre-enumeration is off), it calls `GenerateDelayEnumerated` with all
factories. Otherwise it pre-enumerates. Factories are registered per
`(testClassIndex, methodName)` in a list, one per data attribute.

So `[InlineData(10m)]` plus `[Compose]` on one method yields two
independent rows: `InlineData`'s row (with only one argument, which fails
argument-count validation if there are more parameters) and `Compose`'s
fully composed row. There's no hook to fill one row from two sources.

### 2.2 Execution: delay-enumerated test case

JIT `XunitDelayEnumeratedTheoryTestCase.CreateTests()` (3.2.2 and 4.0.1):

```text
foreach dataAttribute in TestMethod.DataAttributes:
    foreach row in await dataAttribute.GetData(method, DisposalTracker):
        DisposalTracker.AddRange(row.GetData())          // §2.4
        resolve generic types, ResolveMethodArguments, TypeHelper.ConvertArguments
        displayName = method.GetDisplayName(row.TestDisplayName ?? attr.TestDisplayName ?? caseName,
                                            row.Label, args, genericTypes)
        traits = GetTraits(method, row)
        timeout = row.Timeout ?? attr.Timeout ?? case.Timeout
        skip = row.Skip* ?? attr.Skip* ?? case.Skip*
        new XunitTest(..., testIndex++, ..., row.Explicit, row.Label,
                      row.DisableParallelization ?? attr.DisableParallelization)
```

AOT `GenerateDelayEnumerated`: one `CodeGenTestCase` with one test factory.
That factory awaits each registered row factory in order and calls the
generated `MethodInvokerFactory(row)` per row. Each test gets
`UniqueIDGenerator.ForTest(caseId, index)`, so test identity is stable by
row order, not by argument values.

Implications:

- **Timing.** All rows of the theory (from all its data attributes) are
  produced before the first row runs. That happens at
  `TestPipelineStage.TestMethodExecution`, after class, collection, and
  assembly fixtures are initialized (RESEARCH-0037 §4.1, §5).
- **Granularity.** If `GetData` or the factory throws, `CreateTests` throws
  and the test case fails as a unit. No row runs.
- **Identity.** Test Explorer and CI show one test case with N test results.
  The display name is `Method(arg: value, ...)` unless `Label` is set, in
  which case it's `Method [label]` (`XunitTestMethod.GetDisplayName`,
  identical on AOT through `GetTestDisplayName`). Composed argument values
  are random, so argument-based names vary run to run. Labels don't.
- **Parallelism.** Rows inside one test case run sequentially. Separate
  methods and classes run in parallel per the collection rules. One
  theory's rows never race each other, but different theories do.

### 2.3 Row metadata members (all supported versions)

`DataAttribute` (3.2.2): `Label`, `Skip`, `SkipType`, `SkipUnless`,
`SkipWhen`, `TestDisplayName`, `Traits`, plus `Timeout`/`Explicit` through
the base. 4.0.1 adds `DisableParallelization`. `ITheoryDataRow` mirrors the
row-level counterparts (`Label`, `Skip`, `SkipUnless`, `SkipWhen`, `Timeout`,
`Explicit`, `Traits`, `TestDisplayName`, `DisableParallelization`).
`TheoryData<T1..Tn>` enumerates `TheoryDataRow<T1..Tn>`, which is an
`ITheoryDataRow`.

### 2.4 Disposal

- **JIT:** `DisposalTracker.AddRange(data)` per row. `DisposalTracker.Add`
  keeps only `IDisposable`/`IAsyncDisposable` instances and disposes them
  (async preferred) when the test case is disposed. This applies to
  **every** argument: composed values, literal row values, and a
  fixture-owned instance if a test passes one as a parameter.
- **AOT:** core doesn't add row data. xUnit's own generated `MemberData`
  factories receive the tracker. Compono's generated factory doesn't
  register anything (current `[Compose]` AOT template).
- **Compono's position (unchanged):** ADR-0022 Amendment 4. Compono never
  registers composed values for disposal, because it can't tell owned
  instances from shared ones.

### 2.5 AOT member-data mechanics

`MemberDataAttributeGenerator` (xUnit AOT generator, 4.0.1):

- It resolves the member on `MemberType ?? testClass`. The member must be
  **public static** (field, property, or method). An open-generic
  declaring type is skipped.
- `GetTheoryDataInfo` classifies the return type: synchronous enumerable,
  `Task<>`/`ValueTask<>`, or `IAsyncEnumerable<>`.
- It emits `async disposalTracker => { var attr = <registration>; var
  dataRows = [await] Type.Member[(args)]; [await] foreach (...) result.Add(attr.CreateDataRow(dataRow)); }`.
- `DataAttributeRegistration.CreateDataRow` is public on the AOT runtime.
  It has overloads for `object?[]`, `ITuple`, and `ITheoryDataRow`
  (`[OverloadResolutionPriority]`). Row values take precedence over
  attribute values, and traits are merged.
  `SkipUnless`/`SkipWhen` are `Func<bool>` on the registration, rendered
  by the generator as direct member-access lambdas.

JIT `MemberDataAttributeBase` does the equivalent with reflection.

### 2.6 Row-level vs. attribute-level metadata (verified, review round 1)

**Where each property lives:**

| Property | `DataAttribute` (attribute level) | `ITheoryDataRow` (row level) |
|---|---|---|
| `Label`, `Skip`, `TestDisplayName` | `string?`, all versions | `string?`, all versions |
| `Explicit` | `bool` + protected `ExplicitAsNullable` | `bool?` |
| `Timeout` | `int` + protected `TimeoutAsNullable` | `int?` |
| `Traits` | `string[]?` (name/value pairs) | `Dictionary<string, HashSet<string>>?` |
| `SkipType` | `Type?`, all versions | `Type?`, all versions (unused by the AOT runtime) |
| `SkipUnless`, `SkipWhen` | `string?` member name, all versions (AOT included) | **JIT: `string?` member name. AOT: `Func<bool>?`** |
| `DisableParallelization` | `bool`, **4.x only** | `bool?`, **4.x only** |

**How each engine combines them for a delay-enumerated row:**

- **JIT data attribute → row** (`DataAttribute.ConvertDataRow`, used by
  xUnit's own `MemberData`): row value ?? attribute value for
  `Explicit`, `Label`, `Skip`, `TestDisplayName`, `Timeout`,
  `DisableParallelization` (4.x), with merged traits. **It drops row-level
  `SkipType`/`SkipUnless`/`SkipWhen`** (3.2.2 and 4.0.1).
- **JIT row → test** (`XunitDelayEnumeratedTheoryTestCase.CreateTests`):
  `TestDisplayName`, `Timeout`, and `DisableParallelization` fall back
  row → attribute → theory. **Skip is grouped**: if `row.Skip != null`, the
  row's `(Skip, SkipType, SkipUnless, SkipWhen)` is used, else if
  `attr.Skip != null` the attribute's group, else the theory's group.
  `Explicit` and `Label` come from the row only, and traits come from the
  method plus the row.
- **JIT evaluation** (`XunitTestRunnerBaseContext.SafeGetRuntimeSkipReason`):
  `SkipUnless` and `SkipWhen` are mutually exclusive and require `Skip`.
  The member is a public static `bool` property found on
  `SkipType ?? test class` with `FlattenHierarchy`, read through reflection
  at run time. Violations are a `TestPipelineException` at run time.
- **AOT attribute → row** (xUnit generator plus `DataAttributeRegistration.CreateDataRow`):
  the generator validates at compile time that `SkipUnless`/`SkipWhen` are
  not both set and that the member is a public static `bool` property on
  `SkipType ?? test class` (walking base types), then renders
  `SkipUnless = () => global::T.Prop`. The runtime merge is **field by
  field**: `row.X ?? attr.X` for every property, including
  `SkipUnless`/`SkipWhen`.
- **AOT row → test** (`TheoryTestCaseFactory.CreateDelayEnumeratedTest`):
  field by field again, `row.X ?? theory.X`.

**Consequences for `ComposeData`:**

1. Neither engine's built-in merge can be reused as-is if parity is
   required. Compono defines one rule and both engines implement it in
   the shared helper. The rule is xUnit JIT's `CreateTests` semantics,
   which is the more conservative one: grouped skip, with each other
   property falling back row → attribute. The output row Compono hands
   xUnit is already fully merged, so xUnit's own downstream fallback can
   only ever reach the theory level.
2. **`SkipType` applicability.** At attribute level it applies on both
   engines (JIT resolves at run time, AOT at compile time). At row level it
   applies on JIT (it qualifies the row's `string` member name). On AOT a
   row's `SkipUnless`/`SkipWhen` are delegates, so a row-level `SkipType`
   has nothing to qualify and xUnit's AOT runtime ignores it. That's
   inherent to xUnit's AOT row API, not a Compono choice. Both engines
   produce the same behavior for the same conditions.
3. **Residual theory-level edge.** If the merged `ComposeData` group sets
   `Skip` with no condition, and the `[Theory]` itself sets
   `SkipUnless`/`SkipWhen`, the AOT runtime's field-by-field
   `row ?? theory` merge applies the theory's condition, while JIT applies
   none. Compono can't express "explicitly no condition" to the AOT runtime
   without changing semantics. This must be checked empirically in B6. If
   it's real, it's an obstacle to bring back to the product owner, not
   something to paper over.
4. **`DisableParallelization` on JIT.** `Compono.XunitV3` compiles against
   its xUnit floor (3.2.2), where the property doesn't exist. Copying a
   4.x source row's value onto the output row needs either a one-time
   cached `PropertyInfo` lookup (JIT package only), or a documented gap.
   The attribute-level value is still honored by xUnit 4.x's `CreateTests`
   with no Compono involvement. See §10.

### 2.7 Collection vs. row shapes (empirically verified, review round 1)

A scratch console project outside the repo, referencing
`xunit.v3.extensibility.core` 3.2.2 and then 4.0.1 (net10.0), enumerated each
shape and classified every element. Output was identical on both versions:

| Member result (collection) | Element (row) seen | Row values (`GetData`/indexer) |
|---|---|---|
| `TheoryData<int?, string?, DateTimeOffset>` | `ITheoryDataRow` (`TheoryDataRow<…>`) | `1:Int32, a:String, …:DateTimeOffset`; `null, null, default` |
| `TheoryDataRow<int, long?>[]` (with `Label`/`Skip`) | `ITheoryDataRow`, metadata intact | `1:Int32, null`; `2:Int32, 3:Int64` |
| `List<object?[]>` | `object?[]` | `1:Int32, null`; a nested `Int32[]` stays one value |
| `(int, string?)[]` | `ITuple` | `1:Int32, null` |
| 9-element `ValueTuple` | `ITuple` | 9 flattened values (`Rest` expanded) |
| `object[]` of `object[]` | `object?[]` | valid rows |
| `object[]` of scalars | `Int32`, `String` | not rows, so a row-shape error |

Other checks: `string` is `IEnumerable` (so it must be rejected as a
collection), a `TheoryDataRow` is neither `IEnumerable` nor `ITuple` (so a
single row returned as the member is a collection-shape error), a boxed
`int?` with a value is a boxed `Int32`, and a boxed `int` is not `long` (no
widening).

Rules adopted from this:

- **Terminology.** `TheoryData<T…>` is a **collection**
  (`IReadOnlyCollection<TheoryDataRow<T…>>`, verified on 3.2.2). It is
  **not** itself an `ITheoryDataRow`. Its **elements** are
  `TheoryDataRow<T…>`, and those are the `ITheoryDataRow` rows.

- **Collection** = the member's (awaited) result: `IEnumerable` other than
  `string` (this includes `TheoryData<…>`, arrays, and lists), or
  `IAsyncEnumerable<T>`. Each element is one row.
- **Row** = `ITheoryDataRow` (checked first, matching xUnit's
  `[OverloadResolutionPriority(2)]`), then `object?[]`, then `ITuple`.
  Anything else, including `null`, is a row error.
- **Values** are boxed: `Nullable<T>` arrives as `T` or `null`. Validation
  is ADR-0022's: `null` needs a nullable parameter (a nullable value type,
  or a reference type annotated `?`; a reference parameter in an
  oblivious nullable context counts as **not** nullable, matching
  `BindingPlan.GetNullability`). Non-null values must satisfy
  `Nullable.GetUnderlyingType(p) ?? p` `.IsInstanceOfType`. **No numeric
  widening or `TypeHelper` conversion**, unlike xUnit's own `MemberData`.
  It's the same strictness as `[Compose(42)]` today.

## 3. Current Compono architecture (relevant parts)

| Concern | `Compono.XunitV3` (JIT) | `Compono.XunitV3.Aot` |
|---|---|---|
| Entry point | `ComposeAttribute.GetData` (runtime `DataAttribute`) | Marker attributes. The generator emits `RegisterTheoryDataRowFactory(..., disableDiscoveryEnumeration: true, ...)` (`AotTheoryDataRowRegistration.scriban`) |
| Composer lifetime | `Lazy<Composer>` per attribute instance. `Configure` runs once per attribute instance | `Composer.Create(...)` per factory call, which is per row today |
| Signature checks | `BindingPlan.Build/ValidateSignature` (cached once per attribute) | Compile time: `CMP0040`, and `CMP0041`–`CMP0049` for profiles |
| Compose-family exclusivity | `BindingPlan`: `GetCustomAttributes<ComposeAttribute>().Count() > 1` → `CompositionException` | `CMP0045` |
| Parameter dispatch | `RowInvokers.Build(Type)` → `RowInvokerRegistry` (generator-registered per parameter type, ADR-0041). No reflection | Literal `row.Resolve<T>(descriptor)` in generated code |
| Generator discovery | `ComposeMethodDiscovery` matches `Compono.XunitV3.ComposeAttribute`, `` `1 ``, `` `2 `` and registers parameter types for plans and row invokers | `AotComposeMethodDiscovery` matches the AOT marker names |
| Inline values | Leading positional, validated by `PositionalArgumentBinder` (nullable-unwrapped assignability) | Not supported |
| `[Shared]` | Shared parameters first (inline → `ShareExplicit`, else `ResolveShared`), then the rest | Not supported |
| Seed | `row.Seed` non-negative check, `"\n\nSeed: n"` on every failure, `Compono.Seed` trait, `Seed` property | None (#166) |
| `TConfig` profile | `ConfigProfileBinder` reflection, cached per attribute | Direct `new TConfig(literals)` / `new TProfile(config)` |

Extension points that already exist and are reused:

- **Execution-time row composition:** `Composer.CreateRow(Type)` gives one
  `CompositionRow` per row (fresh seed, row-scoped `[Shared]` scope). It's
  the same seam for every framework (ADR-0021).
- **Typed dispatch:** generator-registered row invokers (JIT) and literal
  generic calls (AOT).
- **Positional binding and validation:** `PositionalArgumentBinder`,
  `BindingPlan` (JIT).
- **Profile construction from a config instance:**
  `ConfigProfileBinder.BuildProfile<TProfile, TConfig>(config)` (JIT), and
  generated `new TProfile(config)` with the `CMP0042`/`CMP0044`/
  `CMP0046`–`CMP0048` constructor checks (AOT).
- **Linked shared source between the two xUnit packages** (ADR-0069
  precedent), and **public generator-facing hooks** marked
  `[EditorBrowsable(Never)]` (ADR-0058).

No new core seam is needed: per-row composition through
`CreateRow` + `Resolve`/`ResolveShared`/`ShareExplicit` is already the
parameter-resolution mechanism. The feature changes *where row values come
from*, not *how parameters are composed*.

## 4. Candidate shapes compared

| | Shape | Non-constant values | Per-row config | xUnit fit | AOT feasibility | Verdict |
|---|---|---|---|---|---|---|
| a | Stack several `[Compose…]` (`AllowMultiple = true`) | No (attribute constants) | Constants only | Medium (like `InlineAutoData`) | Easy | **Rejected.** Doesn't solve `DateTimeOffset`. Reverses ADR-0022. The `TConfig` form has no slot for expected values |
| b | `[Compose]` + xUnit `[InlineData]`/`[MemberData]` | Via `MemberData` | Not expressible | High on paper | Not possible without replacing xUnit's discoverer/factory merge | **Rejected.** xUnit never merges rows across attributes (§2.1) |
| c | `MemberName` property on existing `[Compose…]` | Yes | Yes | Medium | Feasible | **Rejected.** Ambiguous with `params` inline and config arguments, and changes single-row attribute semantics |
| d | **New `ComposeData` family reading a public static member** (MemberData-style) | Yes | Yes (`TConfig` per row) | High (mirrors `MemberData` and AutoFixture `MemberAutoData`) | Direct call, like xUnit AOT `MemberData` | **Chosen** |
| e | Explicit "compose here" marker in row slots | Yes | n/a | Low (untyped sentinels) | Feasible | **Rejected** (§9, Q3). Leading-positional matches ADR-0022 |
| f | New Compono row type `ComposeRow<TConfig>` | Yes | Yes | Lower (new vocabulary) | Feasible with linked source | **Rejected** (§9, Q2). xUnit `TheoryData`/`TheoryDataRow` already carry values and metadata |

## 5. Lifecycle and correctness analysis (chosen shape)

- **Exact moment of composition.** Inside the delayed `GetData`
  (JIT) or generated factory (AOT), during `CreateTests()` at
  `TestMethodExecution`, after fixtures exist. The member is read at the
  same time. **No discovery-time enumeration**, so ADR-0069's carried-forward
  constraint holds.
- **No captured per-test state in `Configure`.** Every row builds a fresh
  profile instance (for `TConfig` forms, from that row's config) and a
  fresh `Composer`, on both engines. JIT's per-attribute `Lazy<Composer>`
  is deliberately **not** used by `ComposeData`. A profile's `Configure`
  therefore never sees another row's state, and fixture reads stay correct.
- **Multiple rows sharing one profile type.** Each row has its own profile
  instance, composer, `CompositionRow`, seed, and `[Shared]` scope. Rows
  share nothing that Compono creates. Values the *member* returns (for
  example a `static readonly TheoryData`) are consumer-owned. Guidance:
  return fresh, immutable rows.
- **Parallel execution.** No static mutable state is added. The JIT
  attribute caches only immutable, method-derived metadata (binding plan,
  member accessor, profile constructor), using the existing
  `LazyInitializer` pattern. AOT generated code is stateless.
- **Fixture-dependent values.** Two supported paths, both execution-time:
  a profile registration factory calling `RequireFixture<T>()`
  (ADR-0069), or the member itself reading `TestContext.Current.RequireFixture<T>()`.
  The second works because the member is invoked during `CreateTests`.
  It's documented but secondary.
- **Disposal and ownership.** Compono registers nothing (ADR-0022
  Amendment 4). The JIT/AOT xUnit difference (§2.4) is documented, with a
  warning not to pass fixture-owned disposables as theory parameters.
- **Exceptions.** A failure while reading the member or composing any row
  throws a `CompositionException` naming the member, zero-based row index,
  label (if any), the row's actual seed, and the cause. The original is
  kept as `InnerException` and `Diagnostic` is carried through. The message
  is byte-identical on both engines (shared source).
- **Cancellation.** `IAsyncEnumerable` sources are enumerated with
  `TestContext.Current.CancellationToken`. Between rows, the token is
  checked. Parameterless `Task`/`ValueTask` members get no token (no member
  arguments, by decision).
- **Generics.** Open-generic test methods stay rejected (existing rule).
  The member's declaring type must be closed. Row element types are checked
  against parameter types at compile time on AOT when statically known
  (a `TheoryData<T…>` collection, or a collection whose element type is
  `TheoryDataRow<T…>`), otherwise at runtime on both
  engines with the ADR-0022 rule.
- **Nullable arguments.** Same rule as `PositionalArgumentBinder`: `null`
  only for nullable parameters, and non-null values are checked against
  `Nullable.GetUnderlyingType(p) ?? p`.
- **Determinism.** The effective seed rules are in §5.1. Row order follows
  the member's enumeration order. Test identity is the row index (§2.2).
  A `Label` gives a stable display name.

### 5.1 Effective seed and seed reporting (review round 1)

**Precedence (verified in core).** `CompositionBuilder.WithSeed` stores
into a `ConfigurationOptionSlot` that records every call.
`CompositionBuilder.Build()` (run by `Composer.Create`) reports more than
one call as `DuplicateConfigurationOption`. `Composer.CreateRow` uses
`configuration.Seed ?? CompositionSeed.GenerateRowSeed()`. So for one row
the effective seed is exactly one of:

1. the attribute's `Seed` (applied with `WithSeed` after `AddProfile`), or
2. the profile's own `WithSeed` from `Configure` (ADR-0022 Amendment 3), or
3. a fresh non-negative seed that `CreateRow` generates for that row.

Attribute `Seed` **plus** profile `WithSeed` doesn't override anything.
It's a configuration error that fails composition of every row, with
core's conflict message naming both sources. That's existing behavior for
`[Compose]`, and `ComposeData` keeps it. To reproduce a failure from a
profile that pins its own seed, the seed is already fixed. To vary it,
change the profile.

**When the effective seed becomes known for a row:** for (1), before
anything runs. For (2) and (3), only once `Composer.Create` has finished
and `CreateRow` has returned (`row.Seed`).

**Reporting rule (no fabricated seeds).** A reported seed must be the one
the failing composition attempt used, or would deterministically use.
Compono never generates a seed only to print it. This deliberately differs
from `[Compose]`'s `ApplyProfile` path, which prints
`Random.Shared.Next(...)` for profile-construction failures.

| Failure point | Seed line |
|---|---|
| Member resolution, invocation, or enumeration (no row attempt) | none: not seed-dependent |
| Row shape, `TConfig` extraction, profile construction, `Configure`, `Composer.Create` (including a seed conflict), before `CreateRow` | `Seed: {n}` if attribute `Seed` is set (it's necessarily the effective seed, because a second source would itself be the reported conflict). Otherwise `Seed: not assigned (the failure occurred before the row's composition started)` |
| Negative effective seed | the negative-seed message, reporting the offending value (from the attribute, or `row.Seed` for a profile seed) |
| Row-value validation, `[Shared]`, composition (after `CreateRow`) | `Seed: {row.Seed}`, the actual seed |
| Success | no message; the `Compono.Seed` trait equals `row.Seed` |

Per-row ordering that maximizes seed availability: normalize the row,
extract `TConfig`, build the profile and composer, `CreateRow`, then the
negative-seed check, then value validation, then `[Shared]`, then the rest.
Validation that doesn't need the profile runs after `CreateRow`, so it
reports a real seed, which matches ADR-0022's order for `[Compose]`.

## 6. Native AOT findings

- **Feasible without reflection, dynamic code, or unbounded generics:**
  the member is a direct generated call, row element conversion uses `is`
  pattern checks against compile-time parameter types, and composition
  uses literal `Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>`. The
  `TConfig` profile is a direct `new TProfile(config)`.
- **Row metadata:** generated code passes attribute-level metadata
  (rendered constants, plus `SkipUnless`/`SkipWhen` as
  `() => global::T.Prop` lambdas resolved on `SkipType ?? test class` at
  compile time, the same technique as xUnit's own AOT generator) into the
  shared helper. The helper applies Compono's single merge rule (§2.6),
  which is grouped skip. xUnit's field-by-field `DataAttributeRegistration`
  merge is **not** used, because it would diverge from JIT. Invalid skip
  usage (both conditions set, or the member isn't a public static `bool`
  property) is a compile-time diagnostic, matching the JIT runtime
  `TestPipelineException`. Valid usage is never rejected.
- **Genuine asymmetries that remain (documented, not bugs):**
  1. xUnit disposal of row arguments (JIT yes, AOT no). It's xUnit
     behavior.
  2. Assembly fixtures under AOT remain blocked by xunit/xunit#3664 (not
     Compono).
  3. Shape errors that the generator can see are compile-time `CMPxxxx` on
     AOT and runtime `CompositionException` on JIT. The same conditions are
     rejected.
  4. Existing AOT `[Compose]` still lacks inline values, `[Shared]`, and
     (until #166) seed reporting. `ComposeData` has them. This is a
     temporary intra-AOT asymmetry the product owner accepted (§9, Q9).
- **Not attempted:** AOT-publishing `Compono.XunitV3` (the JIT package). It
  isn't an AOT package. Its member lookup uses reflection exactly as
  xUnit's own JIT `MemberData` does, and it's annotated with
  `DynamicallyAccessedMembers` where a `Type` flows (issue #119 lesson).

## 7. #166 coordination (seed parity)

`ComposeData` on AOT must report seeds (failure `Seed:` line, `Compono.Seed`
trait, `Seed` property). #166 needs the same for AOT `[Compose]`. To avoid
two implementations:

- The seed-and-failure logic lives in one place: a shared
  runtime helper (linked source, §ADR-0070) plus one reusable generator
  emission component ("seeded row scaffold": `WithSeed`, negative-seed
  check, `CreateRow`, failure wrapping, trait).
- PLAN-0071 builds that component for `ComposeData` without wiring it into
  `[Compose]`. #166's design should adopt it. If #166 is designed first,
  PLAN-0071's bead B5 must instead reuse #166's component. That's a
  checkpoint in the plan, not two parallel implementations.

## 8. Capability admission

- **Gate A.1 (meaningful abstraction):** yes. It removes one method per
  row and string stand-ins, and it keeps composition per row with seeds.
- **Gate A.2 (ecosystem fit):** yes. It reuses xUnit `TheoryData`/
  `TheoryDataRow`, `MemberData` member rules, and row metadata. The name
  mirrors `MemberData`/`MemberAutoData`.
- **Gate A.3 (no reflection by default, AOT):** the AOT path is fully
  generated. The JIT path uses reflection only where the JIT package
  already does and where xUnit itself does (member lookup, `TConfig`
  profile constructor), cached once per attribute.
- **Gate B:** #161 migration evidence.
- **Cross-framework:** TUnit, NUnit, and MSTest are deferred. Each has its
  own row model and there's no evidence yet (§9, Q8).

## 9. Product-owner interview (2026-11-05)

| # | Question | Decision |
|---|---|---|
| Q1 | Attribute shape | New member-sourced `ComposeData` family |
| Q2 | Row representation | xUnit's types: `TheoryData<…>` collections of `TheoryDataRow<…>` rows, plus any collection of `ITheoryDataRow`, `object?[]`, or `ITuple` rows, per xUnit convention. For `ComposeData<TProfile, TConfig>`, element 0 is the row's `TConfig` |
| Q3 | Binding | Leading positional (ADR-0022). No markers |
| Q4 | AOT scope | Full JIT/AOT parity for `ComposeData`, including `[Shared]` and row values. Reuse existing infrastructure, keep per-invocation isolation, validate with real Native AOT. No unrelated AOT enhancements. Flag substantial architectural change for review (none found, §6) |
| Q5 | Member shapes | Sync and async (`Task`/`ValueTask`/`IAsyncEnumerable`), parameterless, on both engines. Member arguments deferred |
| Q6 | Seed | Fresh per row. Existing `Seed`/`WithSeed` semantics preserved. Every row reports its actual seed. Coordinate AOT seed with #166. Per-row overrides deferred |
| Q7 | Naming and forms | `ComposeData`, `ComposeData<TProfile>`, `ComposeData<TProfile, TConfig>` in both xUnit packages. The non-generic form has a clear default (default composition) |
| Q8 | Cross-framework | xUnit only. Others deferred until there's evidence |
| Q9 | Intra-AOT asymmetry | Accept temporarily. Build reusable generator components. Document limits and record the follow-up |
| Q10 | Disposal | Keep ADR-0022 Amendment 4: no Compono ownership. Document the JIT/AOT xUnit difference and warn about fixture-owned disposables |
| Q11 | Combining | `AllowMultiple = false`. `ComposeData` joins the Compose-family exclusivity rule. Standard xUnit data attributes stay allowed, with independent rows. Combine data sets in the member. Test mixed attributes, exclusivity, and isolation |
| Q12 | Failures | Fail the theory with a `CompositionException` carrying the member, zero-based row index, optional label, actual seed, and cause. `InnerException` preserved. Consistent across JIT and AOT. No placeholder rows |
| — | Skill (added requirement) | `skills/compono` updates are mandatory for #163 completion: `SKILL.md`, `references/xunit-v3.md`, other relevant references, examples consistent with the final API, validated using the repo's process |

### 9.1 Review round 1 (2026-11-05)

| # | Decision |
|---|---|
| R1 | Keep `MemberType` in the public API |
| R2 | JIT/AOT parity is required for `SkipUnless`, `SkipWhen`, and applicable `SkipType` behavior. No preapproved diagnostic-only AOT fallback. A significant obstacle stops implementation and comes back for review (see §2.6 item 3) |
| R3 | A reported seed must be the composition attempt's actual seed wherever possible. Never generate one just for a message. Precedence clarified (§5.1) |
| R4 | Collection vs. row shapes clarified and verified (§2.7) |
| R5 | One cohesive feature PR (JIT, AOT, tests, docs, skill). Beads stay as checkpoints. No partial-feature merges |
| R6 | Inherited `DataAttribute` metadata verified and documented, row level vs. attribute level (§2.6) |
| R7 | Keep the #166 checkpoint: whichever issue lands first owns the shared seed infrastructure, and the other reuses it |

### 9.2 Review round 2 (2026-11-05): all §10 items resolved; ADR-0070 Accepted

| # | Decision |
|---|---|
| S1 | Row-level `DisableParallelization` on xUnit 4.x is supported in `Compono.XunitV3` through a narrowly scoped, cached reflection lookup. xUnit 3.2.2 compatibility is preserved |
| S2 | Targeted JIT and AOT tests are required for the theory-level `SkipUnless`/`SkipWhen` interaction. If the planned approach can't reach real parity, stop and request review. No speculative workarounds |
| S3 | Strict row-value typing per ADR-0022 is confirmed. No implicit numeric conversions |
| S4 | #166 coordination stays an implementation checkpoint (B5) |
| S5 | Terminology: `TheoryData<T…>` is a collection of `TheoryDataRow<T…>` elements, not itself an `ITheoryDataRow`. Checked across this document, ADR-0070, and PLAN-0071 |

## 10. Open questions for product-owner review (resolved, see §9.2)

1. **Row-level `DisableParallelization` on `Compono.XunitV3` (JIT).** It's
   4.x-only, and the package compiles against xUnit 3.2.2. Recommended:
   copy it with one cached `PropertyInfo` lookup (JIT package only, null on
   3.2.2, no AOT impact), which keeps metadata parity. The alternative is a
   documented JIT gap for row-level values only (attribute level works
   either way).
2. **Theory-level skip edge on AOT (§2.6 item 3).** Planned as an
   empirical check in B6. If it diverges, implementation stops for review.
   No mitigation is preapproved.
3. **Strict value typing.** `ComposeData` keeps ADR-0022's strictness (no
   `int` → `long` widening), unlike xUnit's `MemberData`. Confirm, or ask
   for xUnit-style conversion (which would need its own AOT-safe design).
4. **#166 ordering** (§7): whichever lands first owns the seeded-row
   component.

## Links

- Issues: [#161](https://github.com/LayeredCraft/compono/issues/161),
  [#163](https://github.com/LayeredCraft/compono/issues/163),
  [#162](https://github.com/LayeredCraft/compono/issues/162),
  [#166](https://github.com/LayeredCraft/compono/issues/166)
- [ADR-0022](../adr/0022-compono-xunit-package-design.md) (Amendments 3, 4, 7),
  [ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md),
  [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md),
  [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md),
  [ADR-0058](../adr/0058-public-generator-facing-runtime-infrastructure.md),
  [ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md),
  [ADR-0067](../adr/0067-compono-xunitv3-aot-profile-support.md),
  [ADR-0069](../adr/0069-xunitv3-require-fixture-accessor.md)
- Upstream: [xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664)
