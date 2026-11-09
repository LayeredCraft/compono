# [ADR-0070] xUnit v3 Composed Theory Rows (`[ComposeData]`)

**Status:** Accepted

**Date:** 2026-11-05 (revised the same day after product-owner review round 1,
RESEARCH-0038 §9.1; accepted 2026-11-05 after round 2, §9.2)

**Decision Makers:** product owner (interview decisions, 2026-11-05), assisted
by Claude Opus 5.5

## Context

Issue [#163](https://github.com/LayeredCraft/compono/issues/163) (child of
[#161](https://github.com/LayeredCraft/compono/issues/161), item 3): a
table-driven xUnit theory needs each row to vary **composed configuration**
(for example a clock instant or an option value) together with **expected
values**, while Compono still composes the SUT and its dependencies per row.
Today that takes one method per row plus a shared assertion helper, and
values that aren't attribute constants (`DateTimeOffset`) need a string
stand-in.

[ADR-0022](0022-compono-xunit-package-design.md) allows one Compose-family
attribute per method and rejected mixing `[Compose]` with
`[InlineData]`/`[MemberData]`. Amendment 7 kept that restriction only because
no real call site existed. #161 is that call site.

[RESEARCH-0038](../research/0038-issue-163-composed-theory-data-rows.md)
verified against decompiled xUnit 3.2.2, 4.0.1, and AOT 4.0.1:

- xUnit never merges rows across data attributes, so
  `[InlineData]` + `[Compose]` can't fill different parameters of one row on
  either engine.
- One non-discoverable source makes the whole theory a single delay-enumerated
  test case. All rows are produced in one `CreateTests()` call at execution
  time, before the first row runs.
- xUnit AOT `MemberData` is a generated direct call to a public static
  member, including async shapes. The AOT runtime exposes
  `DataAttributeRegistration.CreateDataRow(...)`, but its field-by-field
  metadata merge differs from JIT's grouped skip merge (RESEARCH-0038 §2.6),
  so `ComposeData` doesn't use it.
- Every core API that per-row binding needs (`Composer.CreateRow`,
  `CompositionRow.Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>`,
  `CompositionException.WithSeedInMessage`) is already public, so
  generated AOT code can reach full parity without a core change.

Carried-forward constraints (ADR-0069, RESEARCH-0037 §12): composition stays
execution-time (delayed enumeration), and per-row values must not depend on
JIT's once-per-attribute `Configure` caching.

## Decision Drivers

- One method expresses N cases. Each row varies profile configuration,
  non-composed values, and non-constant values, and is reported and
  reproducible on its own.
- Idiomatic xUnit: reuse `TheoryData`/`TheoryDataRow` and `MemberData`
  conventions instead of a Compono row vocabulary (capability admission
  Gate A.2).
- Full JIT/AOT parity for the new feature, including `[Shared]` and row
  values. No reflection, dynamic code, or runtime type discovery on AOT
  ([ADR-0001](0001-source-generation-first.md)).
- Isolation: no state shared between rows or invocations, and no mutable
  statics.
- Additive public API only. Existing `[Compose]` source and behavior
  unchanged.
- No core `Compono` change, and core never learns about xUnit.
- A compile-time diagnostic beats generated code that might not compile.

## Considered Options

1. Stack several Compose-family attributes (`AllowMultiple = true`), one row
   each.
2. Combine `[Compose]` with xUnit's `[InlineData]`/`[MemberData]`, with xUnit
   supplying leading values and Compono composing the rest.
3. Add a `MemberName` named property to the existing `[Compose…]`
   attributes.
4. A new `ComposeData` attribute family that reads rows from a public static
   member, MemberData-style, using xUnit's own row types.
5. Option 4 with a Compono-owned row type (`ComposeRow<TConfig>`) and/or
   explicit "compose this slot" markers.

## Decision Outcome

Chosen option: **4, a new `ComposeData` attribute family reading rows from a
public static member, with xUnit row types and leading-positional binding.**

### Public API

Both packages, same shape. `Compono.XunitV3` types are runtime
`DataAttribute`s. `Compono.XunitV3.Aot` types are sealed, independent markers
(the ADR-0067 Amendment 1 pattern) read by `Compono.Generators`.

```csharp
namespace Compono.XunitV3;   // Compono.XunitV3.Aot: identical shapes in namespace Compono.XunitV3.Aot

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeDataAttribute : DataAttribute                 // AOT: sealed, derives DataAttribute
{
    public ComposeDataAttribute(string memberName);
    public string MemberName { get; }
    public Type? MemberType { get; set; }   // xUnit MemberData convention; default = the test class
    public int Seed { get; set; }           // pins every row; same contract as ComposeAttribute.Seed
}

public sealed class ComposeDataAttribute<TProfile> : ComposeDataAttribute
    where TProfile : ICompositionProfile, new();                  // AOT: independent sealed marker

public sealed class ComposeDataAttribute<TProfile, TConfig> : ComposeDataAttribute
    where TProfile : ICompositionProfile;                          // AOT: independent sealed marker
```

The `Compono.XunitV3` generic forms carry
`[DynamicallyAccessedMembers(PublicConstructors)]` on `TProfile`/`TConfig`
like `ComposeAttribute<TProfile, TConfig>`. `MemberType` carries
`[DynamicallyAccessedMembers(PublicFields | PublicProperties | PublicMethods)]`.
Inherited `DataAttribute` metadata is honored on both engines with one
Compono-defined merge rule. See "Row and attribute metadata" below.

A shared runtime helper, one linked source file under `src/Shared/`
(the ADR-0069 mechanism, extended by this ADR to a second file), owns row
normalization, positional value validation, row-metadata copying plus the
`Compono.Seed` trait, and failure-message construction. It's `internal` in
`Compono.XunitV3`. In `Compono.XunitV3.Aot` it's `public` with
`[EditorBrowsable(EditorBrowsableState.Never)]` on its generator-facing
members, because generated code in the consumer assembly calls it
([ADR-0058](0058-public-generator-facing-runtime-infrastructure.md)). It
never composes. Composition stays in typed calls: row invokers on JIT and
literal generic calls on AOT.

### Usage

```csharp
public sealed record ClockConfig(DateTimeOffset Now);

public sealed class PricingProfile(ClockConfig config) : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<TimeProvider>(() => new FakeTimeProvider(config.Now));
}

public static TheoryData<ClockConfig, decimal> PricingCases => new()
{
    { new ClockConfig(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), 10.00m },
    { new ClockConfig(DateTimeOffset.UnixEpoch), 12.50m },
};

[Theory]
[ComposeData<PricingProfile, ClockConfig>(nameof(PricingCases))]
public void Prices_by_date(decimal expected, PriceCalculator sut)   // expected from the row; sut composed per row
    => Assert.Equal(expected, sut.Price());

// No profile config: default or profile composition, with literal leading values
public static IEnumerable<TheoryDataRow<string, bool>> EmailCases =>
[
    new("alice@example.com", true) { Label = "valid" },
    new("not-an-email", false)     { Label = "invalid" },
];

[Theory, ComposeData<AppProfile>(nameof(EmailCases))]
public void Validates(string email, bool expected, [Shared] IClock clock, EmailValidator sut) { }
```

### Behavior contract (identical on both engines)

1. **Execution-time only.** `SupportsDiscoveryEnumeration() => false` on
   JIT. `disableDiscoveryEnumeration: true` on AOT. The member is read, and
   every row composed, inside xUnit's delayed `CreateTests()`, after
   fixtures exist. Nothing is cached from discovery.
2. **Member resolution.** A public static field, property, or parameterless
   method named `MemberName` on `MemberType ?? the test class`, including
   inherited static members. Return shapes: `IEnumerable<TRow>`,
   `Task<IEnumerable-of-TRow>`, `ValueTask<…>`, or `IAsyncEnumerable<TRow>`.
   Member arguments aren't supported (deferred).
3. **Collection and row shapes** (verified on xUnit 3.2.2 and 4.0.1,
   RESEARCH-0038 §2.7). The member's awaited result is the **collection**:
   any `IEnumerable` except `string` (for example a `TheoryData<…>`, which
   is a collection of `TheoryDataRow<…>` rows and not itself a row,
   `TheoryDataRow<…>[]`, `List<object?[]>`, `object[]`), or
   `IAsyncEnumerable<T>`. Each element is one **row**, classified in xUnit's
   order: `ITheoryDataRow` (including `TheoryDataRow<…>`), then `object?[]`,
   then `ITuple` (`ValueTuple`/`Tuple`, with 8+ elements flattened).
   Returning a single row as the member (a `TheoryDataRow`, or an
   `object[]` of scalars) isn't a collection of rows and fails. A `null`
   result or `null` row is an error.
4. **Row layout.** For `ComposeData<TProfile, TConfig>`, element 0 is that
   row's `TConfig` (non-null, assignable to `TConfig`), and elements 1..n are
   row values. For the other two forms, every element is a row value.
5. **Leading-positional binding** (ADR-0022): row value *i* binds to test
   parameter *i*. Every later parameter is composed. Too many values is an
   error. Values are validated before anything is composed, with the
   `PositionalArgumentBinder` rule. Values arrive boxed (`Nullable<T>` as
   `T` or `null`). `null` is allowed only for a nullable value type or a
   `?`-annotated reference type. A reference parameter in an oblivious
   nullable context counts as non-nullable on both engines. Non-null values
   must be instances of `Nullable.GetUnderlyingType(p) ?? p`. There's no
   numeric widening and no xUnit `TypeHelper` conversion (same strictness
   as `[Compose(42)]`, confirmed by the product owner, RESEARCH-0038 §9.2).
6. **Per-row isolation.** For each row: build a fresh profile instance
   (`new TProfile()` or `new TProfile(rowConfig)`) and a fresh `Composer`
   (`AddProfile` + `WithSeed(Seed)` when set), then `CreateRow(testClass)`.
   `[Shared]` parameters go first (a row value goes through `ShareExplicit`,
   otherwise `ResolveShared`), then the remaining parameters, matching
   ADR-0022 steps 6–8. Nothing created for a row is reused by another row or
   another test-case execution.
7. **Seeds.** See "Effective seed" below. Every successfully built row
   carries `Traits["Compono.Seed"] = row.Seed`. Per-row seed overrides are
   deferred.
8. **Row metadata.** See "Row and attribute metadata" below. A `Label`
   gives a deterministic display name.
9. **Failures.** Any failure while reading the member or building row *i*
   throws a `CompositionException`. A row failure names the member
   (`Type.Member`), the zero-based row index, and the label (if any), and
   reports a seed only per "Effective seed" below. The cause is kept as
   `InnerException`, and `Diagnostic` is carried through.
   Because xUnit builds all rows before running any, the theory test case
   fails as a unit. No placeholder rows, no custom invokers. Exact message
   formats are specified in PLAN-0071 and live in the shared helper.
10. **Cancellation.** `IAsyncEnumerable` sources are enumerated with
    `TestContext.Current.CancellationToken`, which is also checked between
    rows.
11. **Exclusivity.** `AllowMultiple = false`. `ComposeData` counts as
    Compose-family: at most one of `[Compose…]`/`[ComposeData…]` per method
    (JIT `BindingPlan` check, AOT `CMP0045` extended). xUnit's own
    `[InlineData]`/`[MemberData]`/`[ClassData]` remain allowed on the same
    method. Their rows are independent and never composed.
12. **Disposal.** Compono registers nothing for disposal (ADR-0022
    Amendment 4). xUnit JIT disposes `IDisposable` theory arguments and
    xUnit AOT core doesn't. This is documented, with a warning not to pass
    fixture-owned disposables as theory parameters.
13. **Unsupported shapes** fail with a compile-time `CMPxxxx` on AOT
    (starting at `CMP0051`) and a `CompositionException` on JIT for the same
    conditions: member not found, not public static, unsupported return or
    row shape, member arguments required, open-generic declaring type,
    `TConfig` mismatch, statically known value/parameter count or type
    mismatch (AOT), **invalid** skip-condition usage (`SkipUnless` and
    `SkipWhen` both set, or a member that isn't a public static `bool`
    property on `SkipType ?? test class`: a run-time `TestPipelineException`
    from xUnit on JIT, a compile-time diagnostic on AOT), and the existing
    signature rules (`CMP0040`). On AOT the `TProfile` constructor checks
    reuse `CMP0042`/`CMP0044`/`CMP0046`–`CMP0049`. **Valid usage is never
    rejected on one engine only.** There's no diagnostic-only AOT fallback
    for a supported feature.

### Effective seed

Exactly one source determines a row's seed (verified in core:
`CompositionBuilder.Build` reports a second `WithSeed` as
`DuplicateConfigurationOption`, and `Composer.CreateRow` uses
`configuration.Seed ?? GenerateRowSeed()`):

1. **Attribute `Seed`**: pins every row. It's validated non-negative before
   anything runs.
2. **Profile `WithSeed`** in `Configure`: pins every row (ADR-0022
   Amendment 3). A negative value is rejected through `row.Seed`.
3. **Otherwise**, `CreateRow` generates a fresh non-negative seed per row.

Attribute `Seed` **and** profile `WithSeed` together is **not** an
override. It's core's existing duplicate-configuration error, which fails
the row and names both sources. `ComposeData` keeps it unchanged.

**Reporting.** A reported seed is always the seed the failing attempt
used, or deterministically would have used. Compono never generates a
seed only for a message (unlike `[Compose]`'s `ApplyProfile` path today,
which is out of scope here):

- Member-level failures (resolution, invocation, enumeration): no seed line,
  because they don't depend on a seed.
- Row failures before `CreateRow` (row shape, `TConfig`, profile
  construction, `Configure`, `Composer.Create`): `Seed: {n}` when attribute
  `Seed` is set (necessarily the effective one), otherwise
  `Seed: not assigned (the failure occurred before the row's composition started)`.
- Row failures from `CreateRow` on (negative-seed check, value validation,
  `[Shared]`, composition): `Seed: {row.Seed}`.

Per-row order: normalize the row, extract `TConfig`, build the profile and
`Composer`, `CreateRow`, negative-seed check, validate values, `[Shared]`,
then the remaining parameters. So everything that doesn't need the profile
runs after the seed exists.

### Row and attribute metadata

xUnit's two engines merge metadata differently (RESEARCH-0038 §2.6). JIT
groups the skip properties and drops row-level conditions in
`ConvertDataRow`. AOT merges field by field. Row-level conditions are
member-name strings on JIT and `Func<bool>` on AOT. `ComposeData` defines
**one** rule, implemented in the shared helper for both engines, and hands
xUnit an already-merged row:

| Property | Rule |
|---|---|
| `Skip`, `SkipType`, `SkipUnless`, `SkipWhen` | **Grouped.** If the row's `Skip` is set, use the row's group. Otherwise, if the attribute's `Skip` is set, use the attribute's group. Otherwise none (the theory's own skip applies through xUnit) |
| `Label`, `TestDisplayName`, `Timeout`, `Explicit` | Row value, else attribute value |
| `Traits` | Union of row traits, attribute traits (name/value pairs), and `Compono.Seed` |
| `DisableParallelization` (xUnit 4.x only) | Row value, else attribute value |

Where it's resolved:

- **Attribute-level `SkipUnless`/`SkipWhen`** name a public static `bool`
  property on `SkipType ?? test class` (inherited members included). JIT
  passes the names to xUnit, which reads them at run time. AOT renders
  `() => global::T.Prop` at compile time, as xUnit's own AOT generator does.
- **Row-level conditions** keep each engine's own row type: a string name
  plus an optional `SkipType` on JIT, a delegate on AOT. On AOT a row-level
  `SkipType` therefore has nothing to qualify, and xUnit's AOT runtime
  ignores it. The behavior for the same conditions is identical.
- **Row-level `DisableParallelization` on `Compono.XunitV3`**, which compiles
  against xUnit 3.2.2 where the property doesn't exist, is copied through a
  narrowly scoped, one-time cached property lookup (`Compono.XunitV3`
  only). It's absent and inert on 3.2.2, which stays fully supported
  (product-owner decision, RESEARCH-0038 §9.2).
- **Residual edge:** a merged group with `Skip` but no condition, together
  with a `[Theory]`-level condition, may differ on the AOT runtime because
  of its field-by-field `row ?? theory` merge. Targeted JIT and AOT tests
  for every theory-level `SkipUnless`/`SkipWhen` interaction are required.
  If the planned approach can't reach real parity, implementation stops for
  product-owner review. No speculative workarounds.

### Generator scope

- **JIT discovery:** `ComposeMethodDiscovery` also matches
  `Compono.XunitV3.ComposeDataAttribute` (plus the `` `1 `` and `` `2 ``
  forms), so parameter types get composition plans and row-invoker
  registrations (ADR-0041), exactly as for `[Compose]`.
- **AOT emission:** a new registration template for `ComposeData`, built on
  reusable emission components (member invocation, row loop, typed value
  binding, `[Shared]` ordering, seeded-row scaffold). The components are
  shaped so AOT `[Compose]` could later adopt inline values, `[Shared]`, and
  seeds, but **this ADR doesn't wire them into `[Compose]`**.
- **#166 coordination:** the seeded-row scaffold (seed configuration,
  negative-seed check, failure wrapping, trait) is the single implementation
  of AOT seed reporting. Whichever of #163 or #166 lands first owns it, and
  the other reuses it.

### Positive Consequences

- One method, N rows, with per-row non-constant config and expected values.
  Each row has its own seed and trait, and a label gives it a stable name.
- Idiomatic: `TheoryData`/`TheoryDataRow` plus `MemberData` rules. No new
  row vocabulary.
- Full JIT/AOT parity for `ComposeData`, including `[Shared]` and row values.
  No core change, no new dependency, and no reflection on AOT.
- Fixture access keeps working, both from profile factories and from the
  member, because everything happens at execution time.
- Existing `[Compose]` is untouched, so the change is additive.

### Negative Consequences

- **Six new public attribute types** (three per package), plus a public,
  `EditorBrowsable(Never)` generator-facing helper in `Compono.XunitV3.Aot`.
- **Whole-theory failure granularity.** One bad row fails the test case and
  no rows run. That's inherent to xUnit's delayed enumeration and is
  mitigated by precise row-identifying messages.
- **Composer per row.** `Composer.Create` runs once per row on both engines
  (AOT `[Compose]` already does this). That's accepted for isolation and
  measured in PLAN-0071.
- **JIT reflection grows slightly:** member lookup and invocation, cached
  once per attribute, in line with xUnit's own JIT `MemberData`, plus one
  cached `DisableParallelization` property lookup (approved, narrowly
  scoped).
- **Compono owns a metadata merge rule** instead of deferring to either
  xUnit engine, because the engines disagree. It must be kept in step if
  xUnit adds row metadata.
- **Temporary intra-AOT asymmetry.** AOT `ComposeData` supports row values,
  `[Shared]`, and seeds, while AOT `[Compose]` still doesn't (inline values
  and `[Shared]` deferred, seeds tracked by #166). Documented, with the
  follow-up recorded.
- **Second linked shared source file.** CI path filters already cover
  `src/Shared/**` (PLAN-0070).
- **No TUnit/NUnit/MSTest equivalent.** Deferred until there's evidence,
  since each framework has its own row model.

## Pros and Cons of the Options

### Option 1: Stack Compose-family attributes

- Good, because it's the smallest conceptual change (like AutoFixture's
  `InlineAutoData`).
- Bad, because values stay attribute constants (`DateTimeOffset` unsolved),
  the `TConfig` form has no slot for expected values, and it reverses
  ADR-0022.

### Option 2: `[Compose]` plus xUnit data attributes

- Good, because it would be the most xUnit-native-looking.
- Bad, because xUnit never merges rows across attributes (verified on all
  engines). Making it work means replacing xUnit's discoverer and AOT
  factory merging, which is out of proportion and fragile across xUnit
  versions.

### Option 3: `MemberName` on existing `[Compose…]`

- Good, because it adds no new types.
- Bad, because it's ambiguous with the `params` inline and config arguments
  and changes the meaning of attributes consumers already use.

### Option 4: `ComposeData` with xUnit row types (chosen)

- Good, because it covers non-constant values, per-row config, async
  sources, labels, and skips, uses the familiar `MemberData` model, and is
  feasible as direct generated calls on AOT.
- Bad, because it adds new public types and has whole-theory failure
  granularity.

### Option 5: Compono row type and/or explicit markers

- Good, because a dedicated config slot is explicit, and markers allow
  composing middle parameters.
- Bad, because it creates new vocabulary that duplicates xUnit's row
  metadata, markers are untyped sentinels, and it conflicts with ADR-0022's
  leading-positional rule.

## Links

- Issues [#163](https://github.com/LayeredCraft/compono/issues/163),
  [#161](https://github.com/LayeredCraft/compono/issues/161),
  [#162](https://github.com/LayeredCraft/compono/issues/162),
  [#166](https://github.com/LayeredCraft/compono/issues/166)
- [RESEARCH-0038](../research/0038-issue-163-composed-theory-data-rows.md),
  [RESEARCH-0037](../research/0037-issue-162-runtime-fixture-values-in-profiles.md),
  [RESEARCH-0036](../research/0036-issue-161-test-composition-gaps-intake.md)
- [ADR-0022](0022-compono-xunit-package-design.md) (Amendments 3, 4, 7),
  [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md),
  [ADR-0036](0036-parameterized-composition-profile-selection.md),
  [ADR-0041](0041-aot-safe-row-binding-dispatch.md),
  [ADR-0058](0058-public-generator-facing-runtime-infrastructure.md),
  [ADR-0066](0066-compono-xunitv3-aot-package-architecture.md),
  [ADR-0067](0067-compono-xunitv3-aot-profile-support.md),
  [ADR-0069](0069-xunitv3-require-fixture-accessor.md)
- [PLAN-0071](../plans/0071-xunitv3-composedata-composed-theory-rows-impl-plan.md)
- [`docs/architecture/capability-admission.md`](../architecture/capability-admission.md)

## Amendment 1 (2026-11-05): One additive core API for the R0 row-failure wrapper

**Amends:** the Context claim that "Every core API that per-row binding needs
(`Composer.CreateRow`, `CompositionRow.Resolve<T>`/`ResolveShared<T>`/
`ShareExplicit<T>`, `CompositionException.WithSeedInMessage`) is already
public, so generated AOT code can reach full parity without a core change",
and the matching "No core `Compono` change" Decision Driver - for the single
case below.

The Decision's failure contract (item 9) requires every row failure to be
thrown as a `CompositionException` that names the member, row index, and
label (the "R0" wrapper in PLAN-0071), **keeps the underlying cause as
`InnerException`**, and **carries the `Diagnostic` through**. The accepted
decision asserted the existing public surface was sufficient by way of
`CompositionException.WithSeedInMessage`. During B1 that proved incomplete:
the only public constructors are `(string)`, `(CompositionDiagnostic)`, and
`(CompositionDiagnostic, Exception)`, each of which either fixes `Message` to
the diagnostic's message or drops the inner exception / diagnostic.
`WithSeedInMessage` can only *append a `Seed:` line to an existing message*,
so it cannot add the R0 prefix while simultaneously preserving both the cause
as `InnerException` and the exact `Diagnostic` reference. The
`(string, CompositionDiagnostic?, Exception)` constructor that
`WithSeedInMessage` itself relies on was `private`, and core exposes no
`InternalsVisibleTo` to the xUnit packages, so neither the shared helper nor
generated AOT code could reach it.

**The additive constructor was selected** over the alternatives (synthesizing
a new `CompositionDiagnostic` from the cause's fields and constructing through
`(CompositionDiagnostic, Exception)`, or a Compono-specific `WithRowContext`
factory) because it is the smallest change that satisfies the contract
exactly: it reuses the precise construction the existing `WithSeedInMessage`
path already depends on, so JIT and AOT share one identical code path, and no
new vocabulary is introduced. It mirrors the established
`(CompositionDiagnostic, Exception)` constructor exactly apart from accepting
a caller-supplied message.

Preserving `InnerException` and the exact `Diagnostic` reference matters
because a row failure frequently wraps a *pipeline* `CompositionException`
whose `Diagnostic` is the consumer-facing structured detail (root/failed
type, path, trace, seed). Dropping or copying it would break the
troubleshooting experience ADR-0010 established and would drop the original
exception chain.

The change is purely additive - a new public
`CompositionException(string message, CompositionDiagnostic? diagnostic,
Exception innerException)` constructor; no existing member changes and
`WithSeedInMessage` behavior is unchanged. **This is the sole core `Compono`
change approved for #163.** Any further core change is not covered by this
amendment and must stop and return for review.

**Recorded:** PLAN-0071 B1; covered by
`test/Compono.Tests/CompositionExceptionTests.cs`.

## Amendment 2 (2026-11-06): Two narrow AOT-only member-resolution limitations

**Amends:** Behavior contract item 2 ("on `MemberType ?? the test class`")
and the sentence in item 13 "**Valid usage is never rejected on one engine
only.**", for exactly the two cases below. Approved by the product owner
during the PLAN-0071 B4 specification review (decisions C-1 and C-2,
recorded in PLAN-0071 Notes, "B4 specification amendment").

### 1. Inherited theory methods (C-1)

When `MemberType` is not set, the engines resolve the member against
different classes:

- **JIT** resolves against the concrete reflected test class
  (`MemberType ?? ReflectedType ?? DeclaringType`), so an inherited test
  method sees the members of each derived class.
- **AOT** resolves against the test method's **declaring** class. This
  follows xUnit's AOT architecture: theory-data factories are registered
  per declaring type and looked up by the test method's declared type
  index (`RegisteredEngineConfig.GetTheoryDataRowFactories`). The factory
  receives no derived-class identity. xUnit's own AOT `MemberData` behaves
  the same way.

Consequences:

- A member that exists only on a derived test class is rejected on AOT
  with `CMP0051` (not found).
- Member hiding in a derived class (a derived `new` member with the same
  name) can resolve to a different member on each engine.
- An explicit `MemberType` names one **fixed** holder class for every
  derived class. It's a workaround only when a fixed holder fits. It does
  **not** reproduce JIT's per-derived-class selection.

### 2. Inaccessible member-holder types (C-2)

Generated AOT code calls the member and the skip-condition property
directly, so it can't reach a `private` or `protected` nested `MemberType`
or `SkipType` holder, or one reached only through such a type.

- JIT may accept these holders, because reflection ignores accessibility.
- AOT reports `CMP0044`, with wording that names the holder role (the
  `MemberType` holder or the `SkipType` holder). A `SkipType` holder is
  checked only when its condition is active (PLAN-0071 B4.9).
- AOT must never silently omit an affected test. (xUnit's own AOT
  `MemberData` generator silently drops such tests. Compono doesn't follow
  that.)

### 3. Parity scope

These two are the **only** newly approved AOT-only compatibility
exceptions. The general requirement in item 13 stays in force for every
other case: supported, valid usage must behave the same on JIT and AOT and
must never be rejected on one engine only. Any further engine difference
needs its own amendment.

Unchanged by this amendment:

- The previously accepted runtime-versus-compile-time validation
  differences: a JIT `CompositionException` or xUnit
  `TestPipelineException` versus an AOT `CMPxxxx` for the same invalid
  usage (item 13). This includes the accepted static-skip residual, where
  AOT reports an invalid active attribute-level skip condition even when no
  row ends up evaluating it (PLAN-0071 B4.9, C-4).
- The documented disposal and assembly-fixture differences.
- Amendment 1.

There is no new public API and no core `Compono` change. Both limitations
use existing or approved diagnostic IDs (`CMP0044`, and `CMP0051` from the
approved `CMP0051`+ range).

**Recorded:** PLAN-0071 B4.4 and B4.9 (rules), B6 (documenting tests), and
B7 (`docs/packages/compono-xunitv3-aot.md` JIT/AOT differences).

## Amendment 3 (2026-11-06): One additive AOT marker for `ComposeData` parameter sharing

**Amends:** the "Public API" section's "Both packages, same shape" statement
and the `Compono.XunitV3.Aot` marker list, for exactly one missing type.
Approved by the product owner during PLAN-0071 B4 implementation.

### The omission

The Decision's item 6 (per-row isolation and `[Shared]` ordering) and the
stated goal of full JIT/AOT parity for the feature "including `[Shared]` and
row values" require AOT `ComposeData` test methods to accept a `[Shared]`
parameter marker. The JIT `ComposeDataAttribute`
(`Compono.XunitV3.ComposeDataAttribute`) already honors it via
`Compono.XunitV3.SharedAttribute`.

B2 added the six `ComposeData` attribute markers to `Compono.XunitV3.Aot`
but **omitted the `[Shared]` parameter marker**. `Compono.XunitV3.Aot` cannot
reference the JIT `Compono.XunitV3.SharedAttribute`: that package depends on
the reflection-mode xUnit extensibility package, which is exactly what xUnit's
native AOT package family rejects (ADR-0066). An AOT consumer therefore had no
way to write `[Shared]` on an AOT `ComposeData` parameter. This is a B2
public-API omission discovered during B4.

### The additive marker

One new public type in the AOT package, mirroring the JIT marker's exact
public contract (a pure marker: sealed, `System.Attribute`, parameter-target
only, parameterless, no additional public members):

```csharp
namespace Compono.XunitV3.Aot;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute;
```

It carries no runtime logic, exactly like the other AOT markers - it is read
by `Compono.Generators` by its own fully qualified metadata name
(`Compono.XunitV3.Aot.SharedAttribute`), the same fully-qualified-metadata-name
matching the AOT `Compose`/`ComposeData` markers already use.

### Ownership

- **B4** owns discovery and validation: it detects `[Shared]` on AOT
  `ComposeData` parameters by matching the marker's fully qualified metadata
  name and records `IsShared` on the parameter model. Duplicate `[Shared]`
  parameter types reuse `CMP0040`, as specified in B4.10.
- **B5** owns the generated sharing behavior (`ShareExplicit<T>` /
  `ResolveShared<T>` ordering and emission). This amendment adds the marker
  only; it does not generate or execute anything.

### Scope

No core `Compono` change and no change to existing core sharing semantics
(ADR-0011 / ADR-0022). This is the sole public-API addition approved for the
B4 slice; any further public API needs its own amendment.

**Recorded:** PLAN-0071 B2 (omission corrected), B4.10 (discovery), B5
(generated behavior), and `docs/packages/compono-xunitv3-aot.md`.

## Amendment 4 (2026-11-06): B5 AOT emission - generic test-class bases, AOT-only call-site limits, and hidden helper members

**Status:** **Approved** by the product owner on 2026-11-06.

- Proposed by the B5 design owner on 2026-11-06.
- Approved in principle the same day: §1 (including the ADR-0001
  confirmation), §3, the `TConfig` identity fix (§5), both JIT async paths,
  exact diagnostic contracts, and real generated-code compilation and
  execution.
- Final approval on 2026-11-06 covers the whole amendment, including the
  narrowed §2e async-interface classification:
  - one reference-type `IAsyncEnumerable<T>` plus any number of value-type
    ones is supported;
  - two or more reference-type implementations, or two or more value-type
    ones with no reference-type implementation, are rejected with
    `CMP0053`;
  - JIT's runtime-object path selection is preserved, including the
    `IAsyncEnumerable<object?>` check before the value-type reader.

**Amends:**

- Item 13's sentence "**Valid usage is never rejected on one engine only.**"
  and Amendment 2 §3, which named Amendment 2's two cases as the only
  AOT-only exceptions. This amendment adds exactly the cases in §2.
- Amendment 3's Scope sentence "any further public API needs its own
  amendment", for exactly the hidden members in §3.
- Amendment 2 §1's stated rationale (§4). The C-1 decision itself is
  unchanged.

**Evidence:** the B5 architecture-review probes (2026-11-06, PLAN-0071
Notes). They hand-wrote B5-shaped registrations against
`Compono.XunitV3.Aot` at `93c8c40`, with xUnit AOT 4.0.1 and 4.0.2, ran
them under the JIT host and under a real Native AOT publish (0 IL/AOT
warnings), and checked the result against decompiled `xunit.v3.core.aot`
4.0.2.

### 1. Generic test-class bases are supported on AOT

A concrete test class can inherit a `[ComposeData]` test method that is
declared on a generic base class (`IntTests : GenericBase<int>`). JIT
supports this. On AOT:

- **Registration key.** xUnit AOT keys theory-data factories by the
  declaring type's *open-generic* index: `global::Ns.GenericBase<>`,
  `global::Ns.Two<,>`, `global::Ns.OuterPlain.InnerGen<>`, or
  `global::Ns.Outer<>.InnerBase`. All four were verified on 4.0.2. The
  generated registration uses exactly that string, and `typeof(...)` of the
  same text.
- **Composition root.** Each time the factory runs, generated code
  resolves the **closed** constructed base. It reads
  `TestContext.Current.TestClass` (`ICoreTestClass.Class`) and walks
  `BaseType` until it finds the type whose generic type definition is the
  open base. That closed type is used everywhere JIT uses
  `testMethod.DeclaringType`:
  - `Composer.CreateRow`
  - every `CompositionRequestDescriptor.DeclaringType`
  - the `{T}` text
  - the `{M}` text, when `MemberType` isn't set

  This gives exact JIT parity. It reads only type identity metadata
  (`BaseType`, `IsGenericType`, `GetGenericTypeDefinition`). It uses no
  member reflection, no `MakeGenericType`, and no dynamic code, and the
  probe showed Native AOT with 0 IL/AOT warnings. The product owner
  confirmed (2026-11-06) that this is consistent with
  [ADR-0001](0001-source-generation-first.md), because it doesn't
  dynamically discover or invoke members.
- **Rejected: the open-generic type as composition root.** Using
  `typeof(GenericBase<>)` as the root changes what valid usage observes
  compared with JIT:
  - the `request.DeclaringType` that custom `ICompositionValueProvider`s
    receive (`PublicProviderAdapter`)
  - the diagnostic root type and path text
  - the `{T}` failure text

  There is no fallback. If the closed base can't be resolved, the factory
  throws. Under xUnit's delayed enumeration that can't happen.
- A method declared directly on an open generic test class (`xUnit1063`)
  or on a class nested in a generic type (`xUnit1032`) is invalid on both
  engines. Compono adds no rule for these. The factory is still
  registered, because the generator can't know whether a concrete subclass
  exists, possibly in another assembly. xUnit runs it only for concrete
  subclasses.

### 2. AOT-only rejections

Generated AOT code is ordinary C# in the consumer's assembly, and JIT
reaches the same members through reflection. In each case below JIT
accepts the usage, but AOT can't express it as a compilable direct call.
Each gets a compile-time diagnostic, never a silently omitted test
(Amendment 2 §2's rule):

a. **Member or active skip-condition holder that contains a type
   parameter.** Usually this is the default lookup on a generic base, with
   the member or `SkipUnless`/`SkipWhen` property declared on
   `GenericBase<T>`. C# can't name `GenericBase<T>.Cases` from outside the
   class. Reported as `CMP0054` twins. Workaround: set `MemberType`/
   `SkipType` to a non-generic or closed type that declares the member.
   xUnit's own AOT `MemberData` rejects the same shape (`xUnit1068`).
b. **Test parameter whose type contains the generic base's type
   parameter** (`T value`, `List<T> items`). Generated code can't name the
   type. This is B4's existing `CMP0040` rule, recorded here because it is
   AOT-only for inherited generic-base tests.
c. **Selected member, its property getter, an active skip property, or
   that property's getter marked `[Obsolete(error: true)]`,
   `[Experimental]`, or a non-optional `[CompilerFeatureRequired]`.** Using
   any of these is a compiler error in generated code. Reported as
   `CMP0047` twins, following the PR #140 constructor precedent. Plain
   `[Obsolete]` (a warning) is **not** rejected; the generated file
   suppresses `CS0612`/`CS0618`.
d. **Static virtual interface member with a default body**, selected
   through an interface `MemberType`. The direct call is `CS8926`, but JIT
   runs it. Reported as a `CMP0052` twin. A static **abstract** interface
   member gets the same diagnostic, but it isn't an exception: JIT fails it
   at run time too.
e. **A member whose declared type implements `IAsyncEnumerable<T>` more
   than once, where JIT's choice isn't deterministic.** Reported as
   `CMP0053` (unsupported return type). JIT (B3) treats any
   `IAsyncEnumerable<>` implementation as an async source. It then picks
   the reading path by the **runtime** object: if
   `source is IAsyncEnumerable<object?>` succeeds, it reads through that
   variance cast; otherwise it reflects over the element type it took from
   the first matching interface `Type.GetInterfaces()` returned.

   A CoreCLR probe (2026-11-06, PLAN-0071 Notes) gives three cases:

   - **One reference-type element plus any number of value-type elements**
     (`int` + `string`): deterministic. Variance never applies to value
     types, so the cast has exactly one match and JIT always reads the
     reference rows, whatever the declaration order. **Supported on AOT**
     through the same cast. No exception.
   - **Two or more reference-type elements** (`string` + `Uri`): the cast
     matches several implementations. CoreCLR took an exact
     `IAsyncEnumerable<object>` if present, else the first in its
     interface map, so swapping the declaration order changed the rows
     read. No documented rule says which one is chosen, and the AOT
     runtime that executes the generated cast isn't shown to choose the
     same.
   - **Two or more value-type elements and no reference one**
     (`int` + `long`): JIT uses the first interface `GetInterfaces()`
     returns. .NET documents that method's order as unspecified. CoreCLR
     happens to follow declaration order (`IntThenLong` read `int` rows,
     `LongThenInt` read `long` rows), but Roslyn can't reproduce the
     runtime interface-map order reliably (inherited interfaces,
     metadata-only types).

   In the last two cases, any compile-time choice would encode
   undocumented runtime behavior and could silently read different rows
   than JIT. Rejecting them is a correctness requirement, not an
   implementation convenience.

As approved on 2026-11-06, these five, plus Amendment 2's two, were the
complete set of AOT-only exceptions for `ComposeData`. §7 (2026-11-09) adds
a sixth exception, §7a: an active skip-condition property that is a
`static virtual` interface property. The complete set is now these five,
§7a, and Amendment 2's two. Item 13 stays in force for everything else.
Exact descriptors, messages, and precedence are in PLAN-0071 B5.4.

### 3. Hidden generator-facing helper members

`ComposeDataRows` gains members. They are `public` with
`[EditorBrowsable(EditorBrowsableState.Never)]` on AOT and `internal` on
JIT, per [ADR-0058](0058-public-generator-facing-runtime-infrastructure.md).

- `MemberReadFailure`, `NegativeAttributeSeed`, and `NegativeRowSeed`.
  Their texts are currently private literals in the JIT
  `ComposeDataAttribute`. The JIT attribute switches to these members, so
  both engines build each message from one source and stay byte-identical.
- `ResolveGenericTestClassType` (AOT build only): the §1 closed-root
  lookup.

Contracts are in PLAN-0071 B5.5. There are no new public types and no core
`Compono` change.

### 4. C-1 rationale correction

Amendment 2 §1 says the AOT factory "receives no derived-class identity".
That's inaccurate: at execution time the factory can read the derived test
class through `TestContext.Current.TestClass` (§1 uses this). C-1 still
holds for a different reason. The member *call* is bound at compile time,
and it can't vary with derived classes the generator may not see. No
behavior changes.

### 5. Not exceptions (recorded for completeness)

- **`TConfig` constructor matching.** B4 compares the `TProfile(TConfig)`
  parameter by symbol equality, which wrongly rejects `(int A, int B)`,
  `dynamic`, and `List<dynamic>` constructors that JIT accepts. B5 fixes
  it to CLR identity (`ClassifyConversion(...).IsIdentity`, the B4.10/D-1
  rule). That restores item 13 parity; it isn't an exception.
- **`DisableParallelization`** is set through the statically typed AOT
  property and rendered as `value ?? false`, which produces the same
  output row as JIT on xUnit 4.x. No difference.
- **Keyword member and property names** (`@default`) are escaped in
  generated code. No diagnostic.
- **Private or protected nested test classes** get `xUnit1000` on both
  engines, so no registration is emitted.
- **Unchanged:**
  - Amendments 1–3
  - the compile-time-versus-run-time validation difference and the C-4
    skip residual
  - the disposal and assembly-fixture differences
  - the AOT `[Compose]` template and snapshots

### 6. Follow-up (not B5 scope)

The existing AOT `[Compose]` path has both defects §1 and §5 fix for
`ComposeData`:

- it renders a generic declaring type as `<T>`, which gives `CS0246` and
  doesn't match xUnit's `<>` key
- it matches the `TConfig` constructor by symbol equality
  (`AotComposeMethodDiscovery`)

Each should be its own issue. A third follow-up issue covers call-site
diagnostics for `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` on the
data member. The product owner approved filing all three on 2026-11-06.
B5 must not change `[Compose]`.

**Recorded:** PLAN-0071 B5 (B5.2–B5.11), B6 (cross-engine and
Amendment-4 documenting tests), and B7 (`docs/packages/compono-xunitv3-aot.md`
JIT/AOT differences, diagnostics docs).

### 7. Interface skip-condition properties (2026-11-09, B5 S5 review)

**Status:** **Approved** by the product owner on 2026-11-09.

**Amends:** §2's complete exception list (one addition, §7a) and §2d's
scope, which named only the data member.

**Evidence:** the S5 independent review (finding F1) and its follow-up
investigation (PLAN-0071 Notes, 2026-11-09). These ran real xUnit 4.0.1
JIT runner probes, generator probes against `Compono.XunitV3.Aot`, and
Native AOT publishes on net9.0, net10.0, and net11.0.

**Defect.** Before S5 no active skip condition was emitted. S5 renders each
one as `static () => global::Holder.Prop`. B4's skip-property validation
never checked for static interface members, so an active `SkipUnless`/
`SkipWhen` that names a `static virtual` or `static abstract` interface
property produced generated code failing with `CS8926` instead of a Compono
diagnostic. xUnit's own AOT generator has the same gap: it emits `CS8926`
for `[Fact(SkipType = typeof(IFoo), SkipUnless = …)]` with the same
property.

Observed JIT behavior, with `SkipType` set to the interface:

| Interface skip property | JIT (`[ComposeData]` and native `[Fact]`) | AOT |
|---|---|---|
| Plain (non-virtual) `static bool P` | runs, condition honored | **supported**: direct `global::IFoo.P` access, legal C# |
| `static virtual bool P` with a body (including when the interface also carries an unimplemented `static abstract` member, or is a closed generic interface) | runs, condition honored | **`CMP0052`** (§7a, AOT-only exception) |
| `static abstract bool P { get; }` | fails at run time (`BadImageFormatException` reading the property) | **`CMP0052`** (§7b, not an exception) |
| Declared on a base interface of `SkipType` | fails at run time ("Cannot find public static property"; xUnit1054 on a native `[Fact]`) | existing `CMP0057`, unchanged |
| Declared on an interface that `SkipType`, a class, implements, with no matching property of its own | fails the same way | existing `CMP0057`, unchanged |

**a. `static virtual` interface skip properties are an AOT-only exception.**
JIT reads the default body through reflection, so the test runs. C# can't
name the property outside a type-parameter context (`CS8926`), so generated
code can't read it directly. It's reported as a skip-property twin of
`CMP0052`. This adds a sixth case to §2's list. Workaround: move the
condition to a static property of a class or struct, or to a plain
(non-virtual) static interface property.

**b. `static abstract` interface skip properties are diagnosed, but that is
not a parity exception.** JIT fails the same usage at run time, so this is
item 13's existing rule for invalid skip-condition usage (a run-time
failure on JIT, a compile-time diagnostic on AOT), with a clearer
compile-time message than `CS8926`. It uses the same `CMP0052` twin as §7a,
just as `CMP0052`'s data-member descriptor already covers both forms (§2d).

**Rejected: full `static virtual` parity.** The investigation found two
ways to read the default body without reflection:

- A constrained generic accessor (`static bool Read<T>() where T : IFoo
  => T.P`, called with `T = IFoo`). It is `CS8920` whenever the interface
  carries any unimplemented `static abstract` member, inherited or its own.
  It also compiles and runs on JIT but **throws `TypeLoadException` at run
  time under Native AOT on net9.0 and net10.0**, with 0 publish warnings
  (it works on net11.0). It's unusable.
- `[UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_P")]`
  against the interface. It ran correctly on JIT and Native AOT for all
  three TFMs, but a closed generic interface needs a separate generic
  accessor class whose type parameters mirror the holder's. It would be
  the first `UnsafeAccessor` in Compono's generated code. It also relies on
  interface-targeted static accessors that work today but aren't explicitly
  documented. It needs model, emitter, and template changes, plus Native AOT
  coverage for every holder form.

The product owner rejected both. The complexity and maintenance cost are
disproportionate for an uncommon shape that xUnit's own AOT generator
doesn't support either, and that has a direct workaround.

**Unchanged:** plain static interface skip properties stay supported, and
the base-interface and implementing-class shapes keep their existing
`CMP0057`. §2d's data-member rule and every other section are unchanged.

**Recorded:** PLAN-0071 B5.4 (descriptor, message, predicate, precedence),
B5.11 S5 (acceptance), the B5 acceptance checklist, B6 (the JIT-side
documenting test), and Notes (2026-11-09).

## Amendment 5 (2026-11-09): AOT skip-group normalization against theory-level conditions

**Status:** **Approved** by the product owner on 2026-11-09.

**Amends:**

- "Row and attribute metadata": the "Where it's resolved" rules gain an AOT
  normalization step. The rule table, including grouped skip precedence, is
  unchanged.
- Amendment 4 §3's hidden-member list (and Amendment 3's Scope sentence
  "any further public API needs its own amendment"), for exactly the one
  member in §4 below.

**Resolves:** RESEARCH-0038 §2.6 item 3 (the "residual theory-level edge")
and the R2/S2 requirement to check it empirically in B6. The divergence was
real and wider than predicted. It's fixed, so there is **no** new parity
exception.

**Evidence:** the B6 readiness review and the skip-parity investigation
(PLAN-0071 Notes, 2026-11-09). They ran generated JIT (`Compono.XunitV3`)
and AOT (`Compono.XunitV3.Aot`) probe suites on xUnit 4.0.1, net10.0, in
isolated copies of `04e1a11`, comparing outcome **and** skip reason against
JIT, on the managed xUnit AOT runner and a real Native AOT publish.

### 1. Defect and root cause

xUnit AOT merges each row a **registered theory-data factory** returns with
the theory's own `[Theory]`/`[Fact]` metadata **field by field**:
`row.SkipUnless ?? theory.SkipUnless` and `row.SkipWhen ?? theory.SkipWhen`,
independently. It then rejects any test where both end up set
(`Both 'SkipUnless' and 'SkipWhen' are set on test method '…'; they are
mutually exclusive`). JIT replaces the whole skip group instead.

`ComposeDataRows.CreateDataRow` produces a correctly grouped row (B5.8), but
leaves the condition field the group doesn't use as `null`. Under a
theory-level `SkipUnless`/`SkipWhen`, xUnit AOT fills that field from the
theory:

- **Leak.** A group with `Skip` and no condition inherits the theory's
  condition. JIT skips the row; AOT runs it whenever the theory condition
  doesn't trigger.
- **Mutual exclusion.** A group whose condition is the opposite kind to the
  theory's ends up with both fields set, and xUnit **fails** the test, on
  valid usage that JIT runs or skips correctly.

xUnit's own AOT `MemberData` path doesn't show the leak, so the defect is
specific to the registered-factory path `ComposeData` uses. It can't be
fixed at run time: the factory receives only a `DisposalTracker`,
`TestContext.Current.TestCase` is `null` when the factory runs (probed), and
setting both fields is rejected.

### 2. Findings (the parity matrix)

Theory variants: none; `Skip` + `SkipUnless` true or false; `Skip` +
`SkipWhen` true or false; unconditional `Skip`. `ComposeData` variants: no
skip; row `Skip` with no condition, `SkipUnless` true/false, `SkipWhen`
true/false; attribute `Skip` with no condition and each condition kind and
value; and three row-versus-attribute precedence combinations. That's 70
conditional cases plus 14 under an unconditional theory skip.

| Build | Cases matching JIT (outcome and reason) | AOT failures |
|---|---|---|
| HEAD `04e1a11` | 44/70 conditional, 14/14 unconditional | 20 mutual-exclusion failures; 6 leaks (row or attribute skip ran) |
| Rejected: always-false `SkipUnless` on every unconditional group | 44/70 | 26: fixes theory-`SkipUnless` leaks but creates new mutual-exclusion failures under theory-`SkipWhen` |
| **Approved: §3 normalization** | **84/84**, managed runner and Native AOT (0 IL/AOT warnings) | **0** |

### 3. Decision: normalize the AOT skip group into the theory's condition field

The effective skip group must occupy **the same condition field the theory
uses**. Then `row ?? theory` takes Compono's value for that field, and the
other field is `null` on both sides.

- **Discovery (compile time).** The generator reads the test method's xUnit
  `[Fact]`/`[Theory]` attribute (`Xunit.FactAttribute`/`Xunit.TheoryAttribute`
  in `xunit.v3.core.aot`) from compile-time attribute data and records the
  theory's skip-condition kind:
  - **None**: neither `SkipUnless` nor `SkipWhen` is set, **or both are**;
  - **Unless**: only `SkipUnless` is set;
  - **When**: only `SkipWhen` is set.

  No runtime reflection. Only named arguments visible in compile-time
  attribute data count, the same information xUnit's own AOT generator
  reads.
- **Both conditions on the theory.** That's invalid xUnit usage. Recording
  None leaves xUnit's own validation exactly as it is. Verified in a
  scratch probe (xUnit 4.0.1, net10.0):
  - By default, xUnit's analyzer rejects it at compile time on both engines
    (`error xUnit1055: Conditional skipping cannot set both SkipUnless and
    SkipWhen`), with or without Compono, and with the B6.0 change applied.
  - With `xUnit1055` suppressed, xUnit's AOT generator emits **no test** for
    the method (`[Fact]`, `[InlineData]`, and `[ComposeData]` alike: 0
    discovered) both before and after the change. JIT runs it and reports
    the mutual-exclusion failure for a theory whose rows don't replace the
    skip group.

  Compono adds no rule of its own here.
- **Supported boundary.** On AOT, `FactAttribute` and `TheoryAttribute` are
  sealed (`CS0509`, probed), so a derived test attribute that sets its
  conditions in a constructor can't be written. Only the named arguments of
  `[Fact]`/`[Theory]` themselves are read. A test attribute that isn't one of
  those two types (for example, one deriving from xUnit's
  `Xunit.Internal.FactAttributeBase`) is treated as None. That's outside
  xUnit's supported surface, and B6.0 adds nothing for it: no runtime
  reflection and no wider scope without evidence. B7 documents the boundary.
- **Emission.** The generated `ComposeDataAttributeMetadata` initializer
  sets `TheorySkipCondition` **only when the kind isn't None**. Generated
  code for a theory without a condition is byte-identical to before, so
  every existing snapshot is preserved.
- **Runtime (AOT build only), in `CreateDataRow` after B5.8's grouped
  row-versus-attribute precedence is resolved.** When the effective group
  has `Skip` set and the theory kind isn't None:

  | Theory kind | Row's `SkipUnless` | Row's `SkipWhen` |
  |---|---|---|
  | Unless | the group's `SkipUnless`; else `() => !when()` from the group's `SkipWhen`; else a cached static `() => false` | `null` |
  | When | `null` | the group's `SkipWhen`; else `() => !unless()` from the group's `SkipUnless`; else a cached static `() => true` |

  The conversions preserve the effective predicate exactly: a test is
  skipped when `SkipUnless` returns `false` or `SkipWhen` returns `true`, so
  `SkipUnless = () => !when()` skips exactly when `when()` is `true`. An
  unconditional group becomes an always-skip predicate in the theory's
  field. The skip reason (`Skip`) is unchanged. A negating closure is
  allocated only when converting an opposite-kind conditional predicate;
  unconditional groups use the two cached static delegates.
- **No-op cases.** When the theory kind is None, or the effective group has
  no `Skip`, the row is exactly what B5.8 produces today. In the second
  case the theory's own skip correctly applies, as on JIT.

### 4. Hidden helper member

`ComposeDataAttributeMetadata` gains one AOT-only `init` property,
`TheorySkipCondition`, `public` with
`[EditorBrowsable(EditorBrowsableState.Never)]` per
[ADR-0058](0058-public-generator-facing-runtime-infrastructure.md), like the
struct's other AOT members.

- **Representation:** an `int` code. **0 = None, 1 = Unless, 2 = When.** No
  public enum and no other new public type; no core `Compono` change.
- **Runtime:** compares the code only against named internal constants,
  never bare literals.
- **Generator:** may use an internal enum or named constants. Generated code
  emits the integer value.
- **Invalid code.** When normalization applies (the effective group has
  `Skip` set and the code isn't 0) and the code isn't 1 or 2, the runtime
  throws a deterministic `InvalidOperationException` naming the code and the
  valid values. It never treats an unknown code as None, Unless, or When.
  Only a generator/runtime mismatch or hand-written metadata can produce
  one; generated code never does. When normalization doesn't apply (no
  effective `Skip`), the code isn't consulted. The exact message is in
  PLAN-0071 B6.0.

### 5. Constraints

- The JIT path (`Compono.XunitV3`) is unchanged.
- B5.8's metadata rules, including grouped skip precedence and every other
  property's `row ?? attribute` rule, are unchanged; this step runs after
  them.
- No reflection, no dynamic invocation, no new runtime dependency, and no
  generated-code change beyond the conditional `TheorySkipCondition` line.
- Valid usage still has no AOT-only rejection for this shape. Amendment 4
  §2's exception list is unchanged.

### 6. Unchanged

B5's acceptance record stands as a record of what B5 delivered and
verified. This defect sits in a combination B5's matrix didn't cover, and
B6 owns that matrix (RESEARCH-0038 §2.6 item 3). The correction lands as
PLAN-0071's B6.0 slice.

**Recorded:** PLAN-0071 B6 (B6.0 slice and the B6 decisions) and Notes
(2026-11-09).
