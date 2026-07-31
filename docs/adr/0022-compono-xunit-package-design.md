# [ADR-0022] Compono.Xunit Package Design

**Status:** Accepted

**Date:** 2026-07-30

**Decision Makers:** solo (inline-value attribute shape and profile scope
confirmed with user)

## Context

`docs/mvp.md`'s Milestone 4 needs a `Compono.Xunit` package so an xUnit v3
theory can declare composed parameters, mix inline and composed values in
one row, share a value into a composed system under test, select a
reusable profile, and report a reproducible seed on failure:

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void Creates_service(
    [Shared] IRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

[ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)
adds the core-side extension point (`CompositionRow`,
`CompositionRequestKind.TestParameter`, the stage-2 read-gate change) this
package needs. This ADR covers everything specific to `Compono.Xunit`
itself: which xUnit v3 extension points it implements, the public
attribute surface, the inline/composed binding algorithm, profile
selection, seed policy, diagnostics, package dependencies, and which test
method shapes are supported.

The xUnit v3 extension point was confirmed directly against the real
`xunit.v3.extensibility.core` 3.2.2 assembly (reflected, not assumed from
v2 familiarity) rather than xUnit v2 documentation, per this milestone's
explicit "do not assume xUnit v2 behavior applies" instruction:

- `Xunit.v3.DataAttribute` is the abstract base every custom data source
  implements: `Attribute`, implementing `Xunit.v3.IDataAttribute`.
- `GetData(MethodInfo testMethod, DisposalTracker disposalTracker)`
  returns `ValueTask<IReadOnlyCollection<ITheoryDataRow>>` — genuinely
  async-capable (a future data source could await I/O), but a `ValueTask`
  completes synchronously with no allocation when there's nothing to
  await, which is exactly `Compono.Xunit`'s case (composition is
  in-memory/CPU-bound, per [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md)).
- `SupportsDiscoveryEnumeration()` is **abstract**, not virtual-with-a-
  default — every `DataAttribute` subclass must decide explicitly whether
  its rows can be enumerated (and, implicitly, serialized/displayed) at
  *discovery* time, before any test runs, or only at *execution* time.
- `ITheoryDataRow.GetData()` returns a plain `object?[]`; the public
  `TheoryDataRow(object?[] data)` constructor is the straightforward way
  to produce one.
- `MethodInfo`/`ParameterInfo` (handed directly to `GetData`, no
  additional reflection cost `Compono.Xunit` introduces beyond what xUnit
  itself already performs to invoke the test) expose everything needed:
  parameter name, type, ordinal, custom attributes (`[Shared]`),
  `IsOptional`/`HasDefaultValue`, and — via `System.Reflection.NullabilityInfoContext`,
  confirmed working directly against a reflected sample method — nullable
  annotations on a reference-typed parameter.

## Decision Drivers

- `docs/mvp.md`'s exit criteria: composed parameters work, inline values
  win, shared values flow into a composed SUT, a failure reports a
  reproducible seed.
- "Avoid relying on undocumented xUnit internals," "keep dependencies
  minimal" — everything above is public, documented extension surface
  (`Xunit.v3` namespace), not an SDK-internal type.
- `design-decisions.md` rule 3 — `Compono.Xunit` may only reach the engine
  through `Compono`'s public surface ([ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)).
- "Prefer existing structured Compono diagnostics rather than inventing a
  parallel exception hierarchy" — a pipeline composition failure should
  surface as the same `CompositionException`/`CompositionDiagnostic`
  `Compono.Tests` already relies on, not a `Compono.Xunit`-specific
  wrapper.
- Smallest discoverable API — confirmed with the user: inline values live
  on `[Compose(...)]`'s own constructor rather than a second attribute
  name, and profile selection is method-level only for this milestone.

## Considered Options

**Discovery-time vs. execution-time composition:**
1. Compose eagerly at discovery time (`SupportsDiscoveryEnumeration() => true`),
   so `dotnet test --list-tests`/IDE test explorers see fully realized
   rows.
2. Defer all composition to execution time (`SupportsDiscoveryEnumeration() => false`);
   discovery sees only that theory data exists, not its content.

**Inline-value attribute shape** (resolved with the user; recorded here
for completeness):
1. Inline values as `[Compose(...)]`'s own constructor arguments
   (chosen).
2. A second, distinct `[InlineComposeData(...)]` attribute.
3. Stack `[Compose]` with ordinary `[InlineData]`/`[MemberData]`.

**Profile selection scope** (resolved with the user; recorded here for
completeness):
1. Method-level `[Compose<TProfile>]` only (chosen).
2. Method-level and class-level, with an explicit precedence rule.

## Decision Outcome

**Discovery-time vs. execution-time: Option 2, defer to execution.**
`ComposeAttribute.SupportsDiscoveryEnumeration()` returns `false`.
Composed values — especially a future `Compono.NSubstitute` substitute, or
any reference type without a meaningful serialized form — are not safely
enumerable or displayable at discovery time, and forcing composition to
run twice (once to enumerate, once for real) would double the random-fork
cost and risk two different values being shown at discovery versus used
at execution unless a seed were pinned solely to prevent that. Discovery
therefore sees only that `[Compose]`/`[Compose<TProfile>]` produces theory
data; `GetData` runs for real exactly once per test execution, which is
also what makes "one composition context per theory row" true by
construction — there is no separate "discovery pass" context to keep
synchronized with the "execution pass" one.

### Attribute surface

```csharp
namespace Compono.Xunit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : Xunit.v3.DataAttribute
{
    private int? _seed;

    public ComposeAttribute(params object?[] inlineValues);

    /// Explicit root seed for this row - same underlying contract as
    /// CompositionBuilder.WithSeed(int), but restricted to non-negative
    /// values (a negative Seed fails with a clear pre-composition
    /// exception) so a seed reported in a failure message is always
    /// pasteable back here unchanged. Unset: a fresh, non-negative seed
    /// is generated on every GetData call. A plain int, not int? -
    /// attribute named arguments cannot target a Nullable<T> property
    /// (CS0655), so this mirrors Xunit.v3.DataAttribute's own
    /// Timeout/TimeoutAsNullable pair exactly (confirmed against the real
    /// xunit.v3.core assembly): a public, attribute-legal int property,
    /// backed by the private _seed field above, which the internal
    /// SeedAsNullable property exposes for the binding algorithm to
    /// actually read.
    public int Seed
    {
        get => _seed ?? default;
        set => _seed = value;
    }

    /// The value actually assigned to Seed, or null if it was never set -
    /// distinguishes "configured to 0" from "never configured," which the
    /// public Seed property alone cannot (its getter falls back to
    /// default(int), i.e. 0, when unset). The binding algorithm reads this,
    /// never Seed directly.
    internal int? SeedAsNullable => _seed;

    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod, DisposalTracker disposalTracker);

    public override bool SupportsDiscoveryEnumeration() => false;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile> : ComposeAttribute
    where TProfile : ICompositionProfile, new()
{
    public ComposeAttribute(params object?[] inlineValues) : base(inlineValues) { }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute;
```

This is the package's **entire** M4 public surface beyond what
[ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)
adds to core `Compono`. `ComposeAttribute` is deliberately unsealed —
`coding-standards.md`'s "classes sealed by default" exception for a type
"genuinely designed for extension" applies directly: `ComposeAttribute<TProfile>`
*is* that extension, mirroring how `CompositionBuilder.AddProfile<TProfile>()`
constrains `TProfile : ICompositionProfile, new()`. Because C# enforces
generic-attribute constraints at the attribute's use site like any other
generic type, `[Compose<NotAProfile>]` for a type that doesn't implement
`ICompositionProfile` or lacks a public parameterless constructor is a
**compile error** — eliminating an entire class of "invalid profile type"
runtime diagnostic the milestone's design questions raised; there is
nothing left to validate at runtime.

```csharp
// Default composition
[Theory, Compose]
public void Creates_service(OrderService service, CreateOrder command) { }

// Profile selection
[Theory, Compose<ApplicationTestProfile>]
public void Creates_service(
    [Shared] IRepository repository, OrderService service, CreateOrder command) { }

// Inline plus composed (positional; leading parameters only)
[Theory, Compose("alice@example.com")]
public void Accepts_email(string email, Customer customer) { }

// Explicit seed reproduction
[Theory, Compose(Seed = 8492173)]
public void Reproduces_failure(Order order) { }
```

### Inline/composed binding algorithm

1. **Read the cached `Composer` and binding metadata** (see Caching
   below) — the reflected `testMethod.GetParameters()`, each parameter's
   `[Shared]` presence and nullability, and the *result* of the
   generic-method/`ref`/`out`/`in`/`params`/duplicate-`[Shared]`-type
   signature checks (computed once, the first time this attribute
   instance's `GetData` ran; see Diagnostics and Exceptions below for
   what each check reports).
2. **Create the row**: `composer.CreateRow(testMethod.DeclaringType)`
   ([ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)).
   This runs *before* the cached signature-validation result is
   consulted, deliberately: a `[Compose(Seed = ...)]`-configured row's
   seed is known before this point, but an *unseeded* row's seed is
   generated by `CreateRow` itself — there is no seed to report at all,
   reproducible or otherwise, until the row exists. Creating the row
   first, and checking cached validation second, is what lets every
   `Compono.Xunit`-authored failure (signature or binding) report the
   row's real seed, not a value invented before one existed.
3. **If `SeedAsNullable` has a value and it's negative, throw now**, using
   `row.Seed` (which just echoes the rejected value back — it was
   supplied explicitly, so there's nothing to discover) — a plain-message
   `CompositionException` stating `Compono.Xunit` accepts only
   non-negative seeds. See Seed Policy and Reporting below for why: this
   is what keeps every row's seed non-negative unconditionally, which is
   what makes the `ulong`/`int` print-identical guarantee hold for every
   failure, not just an auto-generated one.
4. **If the cached signature-validation result is invalid, throw now**,
   using `row.Seed` — a plain-message `CompositionException` naming the
   test class, method, and the specific problem (unsupported shape, or a
   duplicate `[Shared]` type naming both parameter names/ordinals and the
   conflicting type), with the same appended `"\n\nSeed: {value}"`
   convention every other failure category uses (see Seed Policy and
   Reporting below). This still happens **before any parameter is bound
   or composed** — no random fork is consumed and no partially-composed
   row is ever produced for an invalid signature — it is only *row
   creation itself* that now precedes it, not composition.
   [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
   `CompositionRow` still refuses a second same-type share defensively
   (belt-and-suspenders) if this check were ever bypassed, but this check
   is what actually produces the named, actionable message.
5. **Bind inline values, strictly positional, left-to-right from
   parameter 0.** `inlineValues[i]` supplies parameter `i` for every `i <
   inlineValues.Length`, regardless of `[Shared]`; every parameter at
   `i >= inlineValues.Length` is composed. "Supplied" is `i <
   inlineValues.Length`, never null-checked — `inlineValues[i] == null`
   is a **supplied** explicit `null`, distinguished from "not supplied" by
   array length alone, never by nullness. `inlineValues.Length >
   testMethod.GetParameters().Length` is a pre-composition
   `CompositionException` ("too many inline values"). Every supplied
   inline value is validated **before** any parameter is bound, shared,
   or composed (step 6/7/8 below never start until every inline value has
   passed this check) — an inline mismatch always fails as its own
   category, never surfacing indirectly through a later
   `CompositionRow.ShareExplicit`/`Resolve` call:
   - **`inlineValues[i] is null`**: valid only if parameter `i`'s cached
     `Nullability` (from step 1's `NullabilityInfoContext` metadata) is
     `Nullable` — a nullable reference type (`string?`) or a nullable
     value type (`int?`). A `null` supplied for a `NotNullable` parameter
     — a non-nullable reference type *or* a non-nullable value type — is
     a pre-composition `CompositionException` naming the parameter. This
     check is purely nullability-based, never a runtime-type check
     (`null` has no runtime type to inspect at all).
   - **`inlineValues[i]` is non-`null`**: valid only if its runtime type
     (`inlineValues[i]!.GetType()`) is assignable to `Nullable.GetUnderlyingType(parameterType)
     ?? parameterType` — **never the raw declared type directly**. A
     non-null `Nullable<T>` boxes as a boxed `T`, not a boxed
     `Nullable<T>` (a CLR nullable-boxing rule, not a Compono
     convention), so `[Compose(42)]` targeting an `int?` parameter boxes
     to `System.Int32`; checking assignability against the declared
     `int?` directly would reject it (`typeof(int?).IsAssignableFrom(typeof(int))`
     is `false`) despite nullable parameters being fully supported
     (Async and Unsupported Shapes, below). Unwrapping to the underlying
     type first — a no-op for a non-nullable parameter, since
     `Nullable.GetUnderlyingType` returns `null` and the `??` falls back
     to `parameterType` unchanged — is what makes the check correct for
     both. Otherwise (still not assignable after unwrapping) a
     pre-composition `CompositionException` naming the parameter and both
     types.
   Inline values may not target a *later* parameter while an earlier one
   is composed — this is a deliberate limitation (see Non-goals), not a
   distinct failure mode to diagnose.
6. **Compose or share `[Shared]` parameters first, in declaration order
   among themselves**, regardless of where they sit among non-shared
   parameters: an inline-supplied `[Shared]` parameter calls that
   parameter's cached `ShareExplicit` invoker (see Runtime-Typed
   `CompositionRow` Invocation, below — this is never a direct,
   runtime-typed `row.ShareExplicit<T>(...)` call); a composed `[Shared]`
   parameter calls its cached `ResolveShared` invoker. A `[Shared]`
   parameter's own generated dependencies are resolved (and, if it is
   itself composed, its result stored into scope) *before* the next
   `[Shared]` parameter's turn — so a `[Shared]` parameter that itself
   needs a **later**-declared `[Shared]` sibling will not observe it (see
   Non-goals: declare a `[Shared]` dependency before the parameter that
   needs it, same as ordinary top-to-bottom reading order).
7. **Compose every remaining (non-inline, non-shared) parameter**, in
   declaration order, via its cached `Resolve` invoker.
8. **Assemble the final `object?[]` in method declaration order** (not
   binding-processing order) and return it as the row's `TheoryDataRow`,
   with `Traits["Compono.Seed"] = [row.Seed.ToString()]` set unconditionally
   — see Seed Policy and Reporting below for why every row carries this,
   pass or fail.

Optional parameters (a C# default value) are composed exactly like
required ones whenever not inline-supplied — `Compono.Xunit` always
supplies every position explicitly, so the CLR-level default is never
consulted. Static and instance test methods are handled identically;
nothing here depends on `testMethod.IsStatic`.

### Runtime-typed `CompositionRow` invocation

Steps 6 and 7 above call `CompositionRow.Resolve<T>`/`ResolveShared<T>`/
`ShareExplicit<T>` — but `Compono.Xunit` only ever knows a parameter's
type as a runtime `Type` (`ParameterInfo.ParameterType`), never as a
compile-time generic argument. Calling a generic method with a
runtime-only `Type` needs `MethodInfo.MakeGenericMethod`/`Invoke` — but
that reflection cost must not land on the per-row composition path (every
row would pay a `MakeGenericMethod` + boxed-`object[]` `Invoke` per
parameter, on top of the composition it's actually measuring). The fix is
the same shape as `Compono.Generators`' own boxing pattern for a closed
generic type: **close the generic method once, per parameter, while
building the cached binding plan (Phase 1's `Lazy<Composer>`/binding-plan
construction — the same one-time pass that already computes each
parameter's descriptor template and nullability), and cache a strongly
typed delegate — never the raw `MethodInfo` — for the per-row path to
call.**

```csharp
// Non-generic delegate shapes every invoker is adapted to, regardless of
// the closed T - this is what lets a row call an invoker with no
// reflection at all, only an ordinary delegate invocation.
file delegate object? ResolveInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor);
file delegate object? ResolveSharedInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor);
file delegate void ShareExplicitInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor, object? value);

// Private, closed once per parameter type via MakeGenericMethod, then wrapped in the
// delegate shapes above via Delegate.CreateDelegate - never called through reflection
// directly. Each helper's own declared return type is object?, not T, so the T -> object
// boxing conversion a value-typed T needs happens inside the closed method body itself
// (compiled per closed T), which is what makes an exact, non-covariant CreateDelegate
// binding possible - Delegate.CreateDelegate does not perform boxing-conversion return-type
// adaptation on your behalf.
private static object? InvokeResolve<T>(CompositionRow row, in CompositionRequestDescriptor descriptor) =>
    row.Resolve<T>(descriptor);

private static object? InvokeResolveShared<T>(CompositionRow row, in CompositionRequestDescriptor descriptor) =>
    row.ResolveShared<T>(descriptor);

private static void InvokeShareExplicit<T>(CompositionRow row, in CompositionRequestDescriptor descriptor, object? value) =>
    row.ShareExplicit<T>(descriptor, (T)value!); // safe: step 5 already validated null/assignability for this exact T
```

For each parameter, once, while building the cached binding plan:

```csharp
var closedResolve = typeof(BindingPlan)
    .GetMethod(nameof(InvokeResolve), BindingFlags.NonPublic | BindingFlags.Static)!
    .MakeGenericMethod(parameter.ParameterType);
var resolveInvoker = (ResolveInvoker)Delegate.CreateDelegate(typeof(ResolveInvoker), closedResolve);
// ...ResolveSharedInvoker/ShareExplicitInvoker built the same way, once each.
```

The per-parameter binding-plan entry then holds `resolveInvoker`/
`resolveSharedInvoker`/`shareExplicitInvoker` (three delegate fields)
alongside its descriptor template. Steps 6–8's per-row work becomes three
ordinary delegate calls per parameter — `resolveInvoker(row, descriptor)`,
etc. — with **zero** `MakeGenericMethod`/`Invoke` calls anywhere on the
per-row path. This is the exact `ParameterInfo.ParameterType → close a
private generic helper once → cache the delegate → invoke the delegate
per row` shape, and it's what makes the reflection here compatible with
[ADR-0001](0001-source-generation-first.md)'s no-runtime-reflection-on-
the-composition-hot-path rule despite `MakeGenericMethod` being involved
at all: the reflection happens exactly once per parameter, at
binding-plan-cache-construction time (bounded by the test method's own
parameter count, not by how many times the test runs), never once per
row or once per parameter composition.

### Composition order rationale (design question 7)

The milestone's own suggested model — bind inline, compose/share
`[Shared]`, compose the rest, return in declaration order — is confirmed
correct by the binding algorithm above, with one addition the milestone
didn't fully specify: **inline-supplied `[Shared]` parameters share via
`ShareExplicit`, not `ResolveShared`**, since there's nothing left to
compose for them.

### `[Shared]` semantics (design question 6)

- **Type-based only**, per the confirmed default: `CompositionScope`
  ([ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md))
  is unchanged, still keyed by `RequestedType` alone. Name/qualifier-based
  sharing stays deferred past Milestone 4 — no concrete consumer has
  motivated it yet.
- **Duplicate `[Shared]` types fail clearly**, at signature-validation
  time (binding algorithm step 4, checked against the cache built in step
  1, after the row is created in step 2 and the negative-seed check in
  step 3), naming both parameters.
- **`[Shared]` parameters compose before all non-shared parameters**,
  regardless of declaration order — binding algorithm step 6 before step
  7 — so a `[Shared]` value is always in scope before any parameter that
  might structurally depend on it composes.
- **Inline shared values are supported** (`ShareExplicit`).
- **Shared values are visible only within the current row** — each
  `GetData` call builds a brand-new `CompositionRow`/`CompositionContext`/
  `CompositionScope`; nothing survives across rows or across test methods.
- **`[Shared]` is valid on value types** — `CompositionScope` already
  stores `object?` keyed by `Type`; boxing a shared `struct` is no
  different from boxing any other stage's result.
- **A `[Shared]` parameter may depend on an *earlier*-declared `[Shared]`
  sibling**, never a later one (see Non-goals) — declaration order among
  `[Shared]` parameters is the only ordering `Compono.Xunit` establishes
  among them; no dependency graph is built.
- **Conflicts with exact registrations/rules**: none introduced. A
  `[Shared]` parameter still resolves through the full pipeline
  ([ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
  `ResolveShared`), so an exact registration for its type still wins over
  generated composition, exactly as it would for any other request —
  "shared" only changes what happens *after* a value is produced (it gets
  stored for reuse), never which stage produces it.

### Profile selection (design question 8)

Method-level `[Compose<TProfile>]` only, per the confirmed decision.
`TProfile : ICompositionProfile, new()`. `[Compose]` with no type
parameter builds an empty/default `Composer` (`Composer.Create()`). Only
one `[Compose...]` attribute may appear on a method
(`AllowMultiple = false`) — combining several profiles is already fully
supported by [ADR-0018](0018-composition-profiles.md)'s existing
`AddProfile` composition (a profile's own `Configure` can call
`builder.AddProfile<Other>()`), so no attribute-level multi-profile
capability is needed. Profile instances cannot be supplied through the
attribute — only types — since an attribute constructor argument must be
a compile-time constant; this matches `AddProfile<TProfile>()`'s existing
`new()`-only public shape (`AddProfile(ICompositionProfile instance)`
remains programmatic-only, unreachable from an attribute). A profile is
applied exactly once, when the method's cached `Composer` is first built
(see Caching) — never once per row.

### Seed policy and reporting (design question 9)

`ComposeAttribute.SeedAsNullable` (backing the attribute-legal `int Seed`
property — see Attribute Surface above for why it isn't `int?` directly)
feeds `builder.WithSeed(SeedAsNullable.Value)` when it has a value. When
it doesn't, `Composer.CreateRow(...)`'s `_configuration.Seed ??
CompositionSeed.GenerateRowSeed()` fallback ([ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md))
applies unchanged — every `GetData` call without an explicit seed
generates a fresh one, drawn from `int`'s range specifically (unlike an
unseeded `Create<T>()` call's full-`ulong`-range generation), so
`CompositionRow.Seed` is always the complete, reportable, pasteable value
— never a truncated view of a wider one. This is a deliberate non-goal,
not an oversight: `[Compose]` always produces exactly one row in
Milestone 4 (see Non-goals), so there is no "multiple rows from one
attribute" seed-derivation question to answer the way `CreateMany`'s
index-forking does.

Two distinct failure moments, per the milestone's own required
distinction:

- **`ComposeAttribute.Seed` rejects a negative value outright.** Checked
  immediately after `Composer.CreateRow(...)` runs (so the row — and its
  `Seed` — already exists), before any parameter is bound or composed: a
  plain-message `CompositionException` naming the configured value and
  stating that `Compono.Xunit` accepts only non-negative seeds, with the
  same appended `"\n\nSeed: {row.Seed}"` convention every other failure
  uses (`row.Seed` here just echoes the rejected value back, since it was
  supplied explicitly). This is what makes the guarantee below
  unconditional rather than "usually true": every row `Compono.Xunit` ever
  creates — configured or auto-generated — has a non-negative seed, full
  stop. Core `Compono`'s own programmatic `CompositionBuilder.WithSeed(int)`
  ([ADR-0017](0017-immutable-composer-configuration-and-builder-model.md))
  is **unaffected** and continues to accept the full `int` range — this
  restriction is `ComposeAttribute.Seed` specifically, not a core-wide
  change, since only this attribute makes the "paste the reported seed
  back into this same attribute" promise `docs/mvp.md`'s exit criteria
  require.
- **Composition failure before the test runs**: a pipeline stage can't
  satisfy a parameter, a recursion cycle, or one of `Compono.Xunit`'s own
  pre-composition checks (signature validation, or the negative-seed
  rejection above). `GetData` lets the underlying `CompositionException`
  propagate un-wrapped — its `Diagnostic` (when present) already carries
  `RootType` (the test class), the failing parameter's position in the
  path tree, the full dependency path, the provider trace, and the seed,
  rendered exactly as `docs/architecture.md`'s existing Diagnostics
  example shows, ending in `Seed: {value}`. That `value` is guaranteed to
  print identically whether read as the engine's own `ulong` or as
  `CompositionRow.Seed`'s `int` for **every** row, not just an
  auto-generated one — [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)'s
  `GenerateRowSeed()` draws only non-negative `int` values, and the
  negative-seed rejection above closes the one remaining gap (an
  explicitly-configured negative value) — so this holds without
  `Compono.Xunit` rewriting the propagated pipeline message at all. A
  `Compono.Xunit`-authored plain-message exception (no `Diagnostic`)
  manually appends the same `"\n\nSeed: {value}"` convention (using
  `row.Seed`, the `int`), so every failure category — pipeline or
  `Compono.Xunit`-owned — reports a seed the user can paste directly into
  `[Compose(Seed = ...)]` to reproduce it, with no exceptions.
- **Assertion/test failure after successful composition**: xUnit's own
  concern once `GetData` has returned a valid row — `Compono.Xunit`
  never re-enters the picture to report or interpret the assertion
  failure itself. But this is the single most common real-world failure
  shape (composed data happened to trigger a bug the assertion catches),
  and `docs/mvp.md`'s Milestone 4 exit criterion ("failure output
  includes a seed") isn't scoped to composition failures only — a design
  that drops the seed the moment composition succeeds leaves exactly this
  case unreproducible, which an earlier revision of this ADR got wrong.
  **Every row carries its seed as an `ITheoryDataRow` trait**
  (`"Compono.Seed"` → `row.Seed.ToString()`), set unconditionally in
  `GetData`, regardless of whether the test that row feeds will pass or
  fail — `Compono.Xunit` cannot know the outcome at `GetData` time, so
  the trait can't be applied only-on-failure. This is what makes the
  seed discoverable for *any* failure, composition or assertion, without
  reopening "avoid noisy default output" for the passing case: a trait is
  metadata a runner surfaces when inspecting a specific test's details or
  filtering by trait, not something injected into the default pass/fail
  console summary line the way a `TestDisplayName` change would be for
  *every* row, including ones that never fail at all.

**A successful row's default console/IDE output is otherwise unchanged**
— no seed text is added to `TestDisplayName` or printed anywhere for a
passing test; the trait above is the only artifact, and it's inert until
someone actually inspects or filters on it. This is the "avoid noisy
default output" instruction honored for the common (passing) case, while
still satisfying the exit criterion for every failure case, not just
composition ones. A richer, always-visible verbosity opt-in (e.g. the
seed inline in the test name) remains a deferred, non-goal capability.

### Diagnostics and exceptions (design question 10)

| Failure | Mechanism |
|---|---|
| Negative `ComposeAttribute.Seed` | Plain-message `CompositionException`, `Compono.Xunit`, checked immediately after row creation — see Seed Policy and Reporting |
| Unsupported signature (generic method, `ref`/`out`/`in`/`params` parameter) | Plain-message `CompositionException`, `Compono.Xunit`, pre-composition |
| Invalid inline argument count | Same |
| Inline `null` for a non-nullable parameter | Same — checked against cached `Nullability`, never a runtime-type check |
| Inline value type mismatch (non-`null`) | Same |
| Duplicate `[Shared]` types | Same |
| Invalid profile type | **Compile error** (generic constraint) — no runtime diagnostic exists |
| Composition failure for one parameter, recursive graphs, generated-plan absence | Existing `CompositionException`/`CompositionDiagnostic`, unchanged, propagated as-is |
| Failure while producing theory data (an unexpected bug) | Propagates un-wrapped, same as any `DataAttribute.GetData` implementation throwing |

No new exception type is introduced — every case above is either the
engine's own `CompositionException` (used unmodified) or a
`Compono.Xunit`-constructed `CompositionException(string message)` with
the seed line appended by convention, per "prefer existing structured
diagnostics" over inventing a parallel hierarchy.

### Async and unsupported shapes (design question 13)

`Compono.Xunit` **produces an argument array; it never invokes the test
method** — confirmed directly from `Xunit.v3.DataAttribute`'s contract
(`GetData` returns data, not a result). This means:

| Shape | Supported | Notes |
|---|---|---|
| Synchronous methods | Yes | No special handling |
| `Task`/`ValueTask`-returning methods | Yes | No special handling — xUnit's own invoker awaits the result; `Compono.Xunit` never sees the return type |
| Static methods | Yes | No special handling |
| Instance methods | Yes | No special handling |
| Optional parameters | Yes | Composed like any required parameter unless inline-supplied; the CLR default is never consulted |
| Nullable parameters | Yes | `NullabilityInfoContext`-derived `Nullability.Nullable` feeds the descriptor |
| Generic test methods | **No** | `MethodInfo.IsGenericMethodDefinition` check, pre-composition `CompositionException` |
| `ref`/`out`/`in` parameters | **No** | `ParameterInfo.ParameterType.IsByRef`/`IsOut`, pre-composition `CompositionException` |
| `params` parameters | **No** | `ParameterInfo.GetCustomAttribute<ParamArrayAttribute>()`, pre-composition `CompositionException` |
| `CancellationToken` parameters | Not specially handled | Composed like any other type; since no built-in provider exists for it (`docs/mvp.md`'s Milestone 2 built-in type list doesn't include it), an un-inline-supplied, non-`[Shared]`-registered `CancellationToken` parameter fails with an ordinary "no provider/plan could satisfy" diagnostic — a known limitation, not new Milestone-4 behavior; supply it inline or via a registration/`[Shared]` value instead |

### Source generation boundary (design question 11)

**No generator changes.** `MethodInfo`/`ParameterInfo` reflection is
simple, bounded (once per attribute instance's first `GetData` call,
cached thereafter — see Caching), and costs nothing beyond what xUnit
itself already performs to discover and invoke the test in the first
place — there is no "hot path" here in the sense
[ADR-0001](0001-source-generation-first.md)'s no-reflection rule targets
(repeated, per-composed-object construction reflection). This includes
the one genuinely reflection-heavier piece, `MakeGenericMethod` (Runtime-
Typed `CompositionRow` Invocation, above): it runs exactly once per
parameter, at binding-plan-cache-construction time, never on the per-row
path — every row after that calls a cached delegate, not
`MethodInfo.Invoke`. Composed *values* still flow entirely through
generated `ICompositionPlan<T>`s via `PlanCache<T>` — unchanged, and
untouched by this package. A generated-test-method-metadata alternative
was considered and rejected: it would need to duplicate what `MethodInfo`
already gives `Compono.Xunit` for free, for a cost this package doesn't
actually pay.

### Package boundaries and dependencies (design question 12)

```text
Compono.Xunit
├── Compono                          (ProjectReference/PackageReference, ordinary)
└── xunit.v3.extensibility.core      (PackageReference, ordinary - NOT PrivateAssets)
```

- **Target frameworks**: `net10.0;net11.0`, matching `Compono`'s own
  `TargetFrameworks` — no reason to diverge.
- `Compono.Xunit` references `xunit.v3.extensibility.core` (the package
  that owns `Xunit.v3.DataAttribute`/`ITheoryDataRow`) as an **ordinary**
  reference, not `PrivateAssets="all"` — `Compono.Xunit`'s own public
  attribute types derive from/return those types, so a consuming project
  needs them transitively assignable. This is harmless: a consumer of
  `Compono.Xunit` necessarily already references a full xUnit v3 runner
  package (`xunit.v3.mtp-v2` or equivalent) directly, the same way
  `test/Compono.Tests` does today — `Compono.Xunit` does not attempt to
  supply a runner itself.
- **No dependency on `Compono.Generators`.** `Compono.Xunit` performs no
  source generation of its own (per the previous section's decision).
  Generated-plan dispatch for composed values is entirely `Compono`'s
  existing concern.
- **Generator assets already flow transitively**: `Compono.Generators`'
  compiled output is packed into `Compono`'s own nupkg under
  `analyzers/dotnet/cs` ([ADR-0003](0003-generator-package-distribution.md)).
  An ordinary NuGet package reference to `Compono.Xunit` pulls in
  `Compono` transitively, which brings the analyzer along automatically —
  no special-casing needed in `Compono.Xunit`'s own `.csproj`.
- **A consuming test project's references**: `Compono.Xunit` (brings
  `Compono` + the generator analyzer transitively) plus its own direct
  reference to an xUnit v3 runner package (`xunit.v3.mtp-v2`, matching
  `testing.md`'s existing convention) — nothing beyond an ordinary xUnit
  v3 project setup plus one added package.

### Caching (design question 14)

`ComposeAttribute` (and its generic subclass) lazily builds and caches,
in an instance field the first time `GetData` runs on that attribute
instance, exactly two things:

- The `Composer` for this method/profile combination (`Lazy<Composer>`,
  default thread-safety mode — the built-in double-checked
  initialization this repo's "lock-free options first" guidance already
  favors over a hand-rolled lock).
- The parsed, immutable binding plan for this method: which parameters
  are `[Shared]`, each parameter's nullability, the inline-value
  count/type-compatibility check (computed once against `testMethod`,
  which is stable for a given attribute instance), and — per Runtime-
  Typed `CompositionRow` Invocation, above — each parameter's cached
  `resolveInvoker`/`resolveSharedInvoker`/`shareExplicitInvoker`
  delegate, closed over that parameter's `Type` exactly once here via
  `MakeGenericMethod`, never rebuilt or re-reflected on the per-row path.

Both are safe under concurrent `GetData` calls (parallel theory rows, or
a re-run) because `Lazy<Composer>` publication is thread-safe and the
binding plan is immutable once computed. **Never cached**: anything
row-scoped — the `CompositionRow`, its `CompositionContext`/scope/seed,
and any composed value. A fresh `CompositionRow` is created on every
`GetData` call, per [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md).
No disposable test instance is ever retained by either cache. Whether an
attribute instance itself is reused across a test case's discovery and
every subsequent execution (rather than reconstructed per call) is
governed by xUnit's own attribute-caching behavior, not by
`Compono.Xunit` — this package's caching is correct either way, since a
freshly-constructed attribute instance simply repopulates its own
`Lazy<Composer>`/binding-plan fields on first use.

### Testing strategy (design question 15)

- **`test/Compono.Xunit.Tests`** (fast, no real xUnit runner): calls
  `GetData(MethodInfo, DisposalTracker)` directly against attribute
  instances constructed over hand-built sample methods, covering the
  binding algorithm (inline-only, composed-only, mixed, too-many-inline,
  non-null type-mismatch), a non-null inline value **accepted** for a
  `Nullable<T>` parameter specifically (e.g. `[Compose(42)]` for an `int?`
  parameter) — the case a naive declared-type assignability check gets
  wrong via CLR nullable boxing, proving the `Nullable.GetUnderlyingType`
  unwrap actually fixes it — inline `null` handling (a `null` inline
  value accepted for a nullable reference-typed parameter and for a
  nullable value-typed (`Nullable<T>`) parameter; rejected with a clear
  pre-composition exception for a non-nullable reference-typed parameter
  and for a non-nullable value-typed parameter — all four combinations,
  covering both an ordinary and an inline-`[Shared]` target parameter, per
  the requirement that this validation runs before `ShareExplicit` is
  ever invoked), `[Shared]` detection (duplicate types, a `[Shared]`
  parameter declared before/after the parameter that depends on it),
  profile-attribute construction and caching (asserting the same
  `Composer` instance is reused across repeated `GetData` calls on one
  attribute instance), the cached invoker delegates specifically
  (asserting `MakeGenericMethod` runs exactly once per parameter across
  many repeated `GetData` calls on one attribute instance — e.g. by
  counting `Composer.CreateRow`/binding-plan construction invocations, or
  an equivalent seam — never once per row), unsupported-signature
  detection (generic method, `ref`/`out`/`params`), seed determinism
  (same explicit seed twice produces byte-identical row data), a negative
  `Seed` rejected with a distinct, clear exception, and — proving the
  pasteable-seed promise itself, not just its presence — a
  deliberately-failing composition's message containing exactly
  `row.Seed`'s `int` value for both an auto-generated seed and an
  explicit non-negative one, the `"Compono.Seed"` trait present on every
  returned `ITheoryDataRow` with a value matching `row.Seed` exactly —
  for both a passing-shaped and a failing-shaped row, proving it's
  unconditional rather than only attached on the failure path — and a
  concurrency-stress test (`Parallel.For` calling `GetData` many times on
  one shared, cached attribute instance, asserting no exceptions or data
  races from `Lazy<Composer>` publication).
- **`test/Compono.Xunit.SampleTests`**: a genuinely separate, ordinary
  xUnit v3 project (`ProjectReference` to `Compono.Xunit` during
  development; also consumed from a local package feed after `dotnet
  pack`, satisfying "packed-package verification from a separate test
  project") containing representative theories actually run by a real
  xUnit v3 runner — inline-only, composed-only, mixed rows; a `[Shared]`
  parameter declared before the SUT that needs it; class- and
  method-scoped profile selection is out of scope (method-only, per this
  ADR); a deliberately-failing composition (asserted, from
  `Compono.Xunit.Tests`, to produce output containing `"Seed:"`); and an
  ordinary `async Task` theory, proving Async and Unsupported Shapes'
  "no special handling needed" claim against a real runner rather than
  only asserting it by inspection.
- This satisfies the milestone's explicit requirement that "at least one
  test suite must prove behavior through the real xUnit v3 discovery and
  execution pipeline" — `Compono.Xunit.Tests` alone, calling `GetData`
  directly, would not have caught a wrong assumption about
  `SupportsDiscoveryEnumeration()`'s actual effect on discovery/execution
  sequencing the way a real runner does.

### Positive Consequences

- The entire public surface is two attributes and their constructors —
  smaller than `docs/public-api.md`'s original two-attribute
  (`[Compose]` + `[InlineComposeData]`) sketch.
- Every failure category reuses existing `CompositionException`
  machinery; nothing new to teach a consumer about exception handling.
- Verified against the real xUnit v3 assembly rather than assumed from
  v2 — the discovery/execution split, the exact `GetData` signature, and
  `NullabilityInfoContext`'s applicability were all confirmed by
  reflecting the actual 3.2.2 package before this ADR was written.
- Every row's seed is reproducible, not just a composition-failing one —
  the `"Compono.Seed"` trait closes the gap a first pass at this ADR
  left open (an assertion failure after successful composition had no
  way to recover which seed produced the data that triggered it),
  satisfying `docs/mvp.md`'s Milestone 4 exit criterion for every failure
  shape rather than only the composition-failure one.

### Negative Consequences

- Inline values are strictly positional with no way to target a later
  parameter while composing an earlier one — a real limitation compared
  to, say, named-argument binding, accepted as a Milestone 4 non-goal
  (see below) rather than solved now.
- `ComposeAttribute.Seed` accepts a narrower range (non-negative `int`
  only) than `CompositionBuilder.WithSeed(int)`'s full range — a real,
  intentional asymmetry between the programmatic and xUnit-attribute
  seed APIs, not a bug. Accepted because it's what makes "paste the
  reported seed back into `[Compose(Seed = ...)]`" unconditionally true
  rather than true-except-for-negative-values, which the alternative
  (rewriting core's diagnostic rendering, or restricting `WithSeed(int)`
  itself) would have required a wider, out-of-scope change to achieve.
- `[Shared]` parameters that depend on each other only work in
  declaration order — an undocumented-until-you-hit-it footgun if not
  clearly called out (mitigated: this ADR, the package's own XML docs,
  and `public-api.md` all state the rule explicitly).

## Deferred Decisions and Non-goals

- **Class-level `[Compose<TProfile>]`** — confirmed with the user as
  out of scope; method-level only for Milestone 4.
- **Inline values targeting a later parameter while an earlier one is
  composed** — strictly positional, leading parameters only; no "skip a
  position" mechanism.
- **Multiple rows from one `[Compose]` attribute** — always exactly one
  row in Milestone 4; a future `CreateMany`-style multi-row attribute
  would need its own index-forking design, not built here.
- **A `[Shared]` parameter depending on a later-declared `[Shared]`
  sibling** — unsupported; declaration order is the only ordering rule.
- **Name/qualifier-based `[Shared]` matching** — still deferred past
  Milestone 4; type-based only, per [ADR-0011](0011-composition-scope-shared-values-and-recursion-detection.md).
- **`CancellationToken` composition support** — no built-in provider;
  ordinary "unsupported type" failure unless inline/shared/registered.
- **Exposing the seed inline in a successful row's `TestDisplayName` or
  console output** — deliberately not built, to avoid noisy default
  output for the passing case; the `"Compono.Seed"` trait (Seed Policy
  and Reporting) already makes the seed discoverable for every row,
  including a later assertion failure, without needing this. A future,
  always-visible verbosity opt-in beyond the trait is not designed here.
- **Combining `[Compose]` with ordinary `[InlineData]`/`[MemberData]`** —
  not supported; each xUnit data attribute is an independent row source.
- **Stacking multiple `[Compose...]` attributes on one method** —
  `AllowMultiple = false`; not needed, since profile composition already
  covers multi-profile scenarios via `AddProfile`.
- **Generic test methods, `ref`/`out`/`in`/`params` parameters** — fail
  clearly at discovery/execution; no support planned for a future
  milestone unless a concrete need appears.
- **`Compono.NSubstitute`/`Compono.Bogus`-specific test-double or
  semantic-data ergonomics inside `Compono.Xunit`** — out of scope; those
  are Milestone 5/6 packages that plug into the same core pipeline
  `Compono.Xunit`'s composed parameters already flow through, with no
  `Compono.Xunit`-specific integration needed.

## Pros and Cons of the Options

### Discovery enumeration — eager (rejected)

- Good, because discovery-time test explorers would show fully realized
  argument values.
- Bad, because it forces composing every row twice (once to enumerate,
  once for real) unless a seed is pinned, and a composed reference type
  (a substitute, a live object) has no meaningful enumerable/serializable
  form at discovery time regardless.

### Discovery enumeration — deferred to execution (chosen)

- Good, because it composes each row exactly once, matching "one
  composition context per theory row" precisely.
- Good, because it needs no serialization story for composed values at
  all — they never leave the process before the test that consumes them
  runs.
- Bad, because a discovery-time test explorer sees only that theory data
  exists, not its shape — an accepted limitation for a source of
  intentionally-not-pre-known values.

## Amendment (2026-07-30): generator discovery is required after all, for two distinct call shapes

PR #22 review (Codex, on PLAN-0004 Phase 0) caught that design question
11's **"No generator changes"** decision above was wrong on one count:
`CompositionRow.Resolve<T>()`/`Resolve<T>(descriptor)`/`ResolveShared<T>(descriptor)`
dispatch through the exact same `PlanCache<T>` stage-8 mechanism
`Composer.Create<T>()` does, and a type reached *only* through one of
those calls never gets a generated plan unless something else in the
same compilation independently triggers discovery for it (a
`Create<T>()`/`CreateMany<T>()` call site elsewhere, or `[Composable]`).
This was masked in `Compono.Tests`' own row-composition tests, which
hand-assign `PlanCache<T>.Instance` directly rather than going through
the real generator — the exact masking pattern `testing.md`'s "verifying
a new public entry point" rule already names and requires guarding
against (the `CreateMany<T>()` precedent it cites).

Two distinct fixes follow, addressed separately because they are
genuinely different discovery problems:

**1. Direct `CompositionRow` usage — fixed immediately (PLAN-0004 Phase
0, same PR).** `CreateInvocationDiscovery`
([ADR-0004](0004-composition-plan-discovery-and-dispatch.md)'s call-site
mechanism) is extended to also match
`CompositionRow.Resolve<T>()`/`Resolve<T>(descriptor)`/`ResolveShared<T>(descriptor)`
call sites, alongside its existing
`Composer.Create<T>()`/`CreateMany<T>()` matching — the same discovery
path, disambiguated by the resolved method symbol's containing type
(`Compono.Composer` vs. `Compono.CompositionRow`), same as
`Create`/`CreateMany` are already disambiguated from any other type's
same-named method. Verified with an isolated `Compono.Generators.Tests`
snapshot test per call shape (a type reached only through that one call,
no `[Composable]`, no `Create<T>()`/`CreateMany<T>()`) and a real
`dotnet pack` + local-feed + throwaway-consumer manual check (the same
proof shape Milestone 1's plan used) — a packaged `Compono` consumed via
`PackageReference`, never `ProjectReference`, correctly composed a type
reached only via `row.Resolve<T>(descriptor)`.

**2. `[Compose]`-attributed test-method parameters — deferred, tracked
for before Phase 1 begins.** This section's original reasoning assumed
`Compono.Xunit`'s `MethodInfo.MakeGenericMethod`-based binding
(Runtime-Typed `CompositionRow` Invocation, above) would be the *only*
way test-method parameter types are ever reached, and that its
reflection cost was bounded and acceptable — both still hold. What the
original reasoning missed: **there is no textual `row.Resolve<T>(...)`
call site anywhere in a consumer's own source for even the now-fixed
mechanism above to match against** — `Compono.Xunit`'s cached invoker
delegates are built entirely from runtime `ParameterInfo.ParameterType`
reflection, inside `Compono.Xunit`'s own compiled binary, never emitted
as source in the consuming test project. A type reached only as a
`[Compose]`-attributed test method's own parameter therefore still gets
no generated plan under fix #1 alone — a fundamentally different
discovery problem from a missing call-site pattern, since there is no
call site to find.

The resolution: a **separate discovery component**, deliberately not
folded into `CreateInvocationDiscovery` — recognizing methods attributed
with `[Compose]`/`[Compose<TProfile>]` (`ForAttributeWithMetadataName`,
the same mechanism `ComposableAttributeDiscovery` already uses for
`[Composable]`) and generating a plan for each eligible parameter type in
that method's signature. "Eligible" mirrors Phase 2's own supported-shape
table above (excludes generic methods and `ref`/`out`/`in`/`params`
parameters — the same shapes `Compono.Xunit`'s binding algorithm itself
rejects pre-composition). Every eligible parameter gets a plan generated
unconditionally, even one that's always supplied inline at every call
site in practice — statically predicting which parameters will actually
be inline-supplied at a given call site would mean duplicating Phase 2's
own runtime inline-binding calculation inside the generator, for a
benefit (skipping plan generation for a type that's cheap to generate a
plan for anyway) not worth that duplication.

This is scoped as design/planning work to close out **before Phase 1
implementation begins** — not implemented by this amendment.
`docs/plans/0004-milestone-4-xunit-integration.md`'s Phase 1 task list
gets the new discovery-component tasks, and Phase 3's packaged-consumer
verification (`test/Compono.Xunit.SampleTests`) gets an explicit
requirement: prove a parameter type discovered *only* from a
`[Compose]`-attributed method (no `[Composable]`, no
`Create<T>()`/`CreateMany<T>()`, no direct `CompositionRow` call site)
receives a generated plan through the real packaged sample.

## Amendment 2 (2026-07-31): the descriptor-less `Resolve<T>()` overload must never be discovered

A further Codex review round on the same PR caught that Amendment
(2026-07-30)'s fix #1 was itself wrong on one count: it matched **all**
of `CompositionRow.Resolve<T>()`/`Resolve<T>(descriptor)`/`ResolveShared<T>(descriptor)`,
including the descriptor-less `Resolve<T>()` overload. That overload
exists on `CompositionRow` solely to satisfy `ICompositionContext`'s full
interface shape — it forwards to `ICompositionContext.Resolve<TValue>()`'s
manual-resolve seam (`docs/adr/0019-registrations-and-service-provider-injection.md`),
which throws `InvalidOperationException` unless a registration/
configuration-rule factory is actively being invoked. A caller holding a
`CompositionRow` can never satisfy that condition: `InvokeFactory`
(`CompositionContext`'s single factory-invocation point) always hands a
factory the raw internal context, never the `CompositionRow` wrapper —
confirmed both by inspection and by the required manual pack-and-consume
verification itself, which hit this exact throw before being reworked to
use the descriptor overload instead. Discovering (and, worse,
documenting in `docs/public-api.md`) this overload as an ordinary
row-composition entry point advertised a call shape that always throws
at runtime — the opposite of what discovery is supposed to guarantee.

**Fix:** `CreateInvocationDiscovery`'s row-resolve match now additionally
requires `method.Parameters.Length == 1`, excluding the descriptor-less
overload while still matching both overloads that genuinely work
(`Resolve<T>(descriptor)`, `ResolveShared<T>(descriptor)`, both
single-parameter). The isolated `Compono.Generators.Tests` coverage for
this call shape now asserts the opposite of before: no plan is generated
for a type reached only through `row.Resolve<T>()`, proving discovery
correctly excludes it rather than proving it (wrongly) included. No
change to Amendment 2026-07-30's fix #2 (the still-deferred
`[Compose]`-attributed-parameter discovery) — that work is unaffected by
this correction.

## Links

- [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md) —
  `CompositionRow`, `CompositionRequestKind.TestParameter`, the stage-2
  read-gate change this package's `[Shared]` support depends on entirely.
- [ADR-0010](0010-composition-request-pipeline-and-diagnostics-tracing.md) —
  `CompositionException`/`CompositionDiagnostic`, reused unmodified here.
- [ADR-0018](0018-composition-profiles.md) — `ICompositionProfile`,
  `AddProfile`, reused unmodified for `[Compose<TProfile>]`.
- [ADR-0003](0003-generator-package-distribution.md) — why `Compono.Xunit`
  needs no direct dependency on `Compono.Generators`.
