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
