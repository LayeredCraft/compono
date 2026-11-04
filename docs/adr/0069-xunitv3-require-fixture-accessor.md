# [ADR-0069] xUnit v3 Fixture Accessor for Composition Profiles (`RequireFixture<TFixture>()`)

**Status:** Accepted

**Date:** 2026-11-03

**Decision Makers:** product owner, assisted by Claude Opus 5.5

## Context

Issue [#162](https://github.com/LayeredCraft/compono/issues/162) (child of
[#161](https://github.com/LayeredCraft/compono/issues/161)): a SUT composed
through `[Compose<TProfile>]` depends on runtime values owned by an xUnit
fixture, such as a container connection string or a broker endpoint. The
consumer wants to keep attribute-driven composition instead of building a
composer by hand in every test.

[RESEARCH-0037](../research/0037-issue-162-runtime-fixture-values-in-profiles.md)
established the following on both `Compono.XunitV3` and
`Compono.XunitV3.Aot`, including a real Native AOT publish:

- A profile's registration factory can already read class, collection,
  and assembly fixtures through xUnit's own `ITestContext.GetFixture(Type)`.
- Compono composes rows during test-method execution
  (`SupportsDiscoveryEnumeration() => false` /
  `disableDiscoveryEnumeration: true`). By then xUnit has eagerly created
  and initialized every declared fixture, so the returned `ValueTask` is
  already complete.

The path consumers actually write first is still incorrect:

- **Sync-over-async.** `GetFixture<T>().GetAwaiter().GetResult()` is the
  pattern `references/coding-standards.md` forbids. Under
  `parallelAlgorithm=aggressive`, xUnit installs `MaxConcurrencySyncContext`,
  so blocking would deadlock in the rare case where the `ValueTask` isn't
  complete (open-generic or skipped-level fixtures, RESEARCH-0037 §4.4).
- **Version floor.** `GetFixture<T>()` doesn't exist on xUnit 3.2.2,
  `Compono.XunitV3`'s supported floor.
- **Silent `null`.** A missing fixture and an exact-type mismatch both
  return `null`, which turns into an unhelpful downstream failure.

Round 2 of RESEARCH-0037 (§10–§11) compared documentation-only guidance
with a Compono-owned accessor. The product owner chose the accessor.

## Decision Drivers

- Make the discoverable path also the correct one: never blocking, fail
  fast, a precise diagnostic (capability-admission Gate A.1).
- Native xUnit ecosystem fit (Gate A.2).
- No new dependencies, no reflection, Native AOT and trim safety
  ([ADR-0001](0001-source-generation-first.md)).
- Identical API and behavior in `Compono.XunitV3` and `Compono.XunitV3.Aot`
  ([ADR-0066](0066-compono-xunitv3-aot-package-architecture.md)).
- Support the full xUnit range of both packages: `[3.2.2, 5.0.0)` for JIT
  and `[4.0.1, 5.0.0)` for AOT.
- Leave Compono's composition model, ownership model, and core package
  untouched.

## Considered Options

1. Documentation only: a consumer-owned helper in a cookbook recipe.
2. A Compono-owned extension method on xUnit's `ITestContext`, in both
   xUnit packages.
3. A static Compono accessor over `TestContext.Current`
   (e.g. `XunitFixture.Require<T>()`).
4. A `CompositionBuilder` extension (e.g. `RegisterFromFixture<TFixture, T>(…)`).

(RESEARCH-0037 §7 had already rejected wider alternatives: profile
constructor injection, a framework-neutral runtime-value source in core,
static or `AsyncLocal` holders, and compile-time fixture diagnostics.)

## Decision Outcome

Chosen option: **2, an extension method on `ITestContext` in both
`Compono.XunitV3` and `Compono.XunitV3.Aot`**. It makes the obvious call
correct and keeps the xUnit dependency visible at the call site. It's
fully testable against a fake `ITestContext`, and it needs nothing beyond
public xUnit API.

### Public API

```csharp
namespace Compono.XunitV3;          // Compono.XunitV3.Aot declares the identical type in namespace Compono.XunitV3.Aot

public static class TestContextExtensions
{
    public static TFixture RequireFixture<TFixture>(this ITestContext context)
        where TFixture : class;
}
```

Usage, in a profile's registration factory, never in the profile
constructor or eagerly in `Configure`:

```csharp
builder.Register(() => new DbSettings(TestContext.Current.RequireFixture<DbFixture>().ConnectionString));
```

### Behavior contract

Evaluated in this order:

1. **`context` is null:** `ArgumentNullException.ThrowIfNull(context)`.
2. **No running test class** (`context.TestClass` is null, e.g. called
   during discovery, outside a test, or from a context with no class):
   throw `InvalidOperationException` naming the requested fixture type and
   `context.PipelineStage`.
3. **Lookup:** call `context.GetFixture(typeof(TFixture))`, the
   non-generic overload, which exists on xUnit 3.2.2 and 4.x in both JIT
   and AOT. No reflection, no runtime discovery.
4. **Incomplete initialization** (`!pending.IsCompleted`): throw
   `InvalidOperationException` immediately. **Never wait** on an
   incomplete `ValueTask`.
5. **Fixture creation failure** (completed but faulted or canceled):
   rethrow the original exception unchanged via
   `pending.GetAwaiter().GetResult()`. That's safe because the
   `ValueTask` has already completed, so nothing blocks.
6. **Missing fixture** (result is `null`): throw `InvalidOperationException`
   naming the test class (`context.TestClass.TestClassName`) and the
   exact fixture type. The message says matching is exact-type only and
   lists the supported declarations: `IClassFixture<T>` on the test class,
   `ICollectionFixture<T>` on the test's collection definition, and
   `[assembly: AssemblyFixture(typeof(T))]`.
7. **Unexpected type** (result isn't a `TFixture`, which is only possible
   with a non-xUnit `ITestContext` implementation): throw
   `InvalidOperationException` naming the expected and actual types.
8. Otherwise, return the fixture.

Exception messages are specified verbatim in
[PLAN-0070](../plans/0070-xunitv3-require-fixture-accessor-impl-plan.md)
so both packages stay byte-identical.

### Single shared source, compiled per package

`Compono.XunitV3` and `Compono.XunitV3.Aot` compile against different
xUnit assemblies (`xunit.v3.core` vs. `xunit.v3.core.aot`) that each
define their own `Xunit.ITestContext` (RESEARCH-0032 §1–§2). A shared
compiled assembly is therefore impossible, and core `Compono` must never
reference xUnit. One source file lives under `src/Shared/` and both
package projects link it (`<Compile Include="…" Link="…" />`). Each
project compiles it against its own xUnit reference, and a
package-defined compilation symbol selects the namespace. This is the
repo's first linked shared source. It is adopted for this case: two
packages whose public surfaces must be identical while compiling against
non-interchangeable dependency assemblies. It isn't a general license.
New linked shared source needs its own decision.

### Positive Consequences

- The discoverable call is correct: it never blocks, fails fast, and
  gives an actionable diagnostic.
- Parity by construction: one implementation, two compilations.
- No new dependencies, no reflection, no change to core `Compono`, to
  composition, or to ownership. Compono never creates, initializes, or
  disposes fixtures.
- The edge-case branches (incomplete, faulted, wrong type, no class) can
  be unit-tested with a fake `ITestContext`.

### Negative Consequences

- **Permanent public API on two packages**, coupled to xUnit's
  exact-type fixture semantics, which Compono doesn't own.
- **Name shadowing.** A future xUnit instance member named
  `RequireFixture` on `ITestContext` would silently take precedence over
  the extension. Mitigation: tests pin the documented behavior, so a
  shadowing change would surface.
- **Encourages xUnit-coupled profiles.** Mitigation: the docs also
  present the framework-neutral alternative, passing the fixture into a
  profile instance (`Composer.Create(b => b.AddProfile(new P(fixture)))`).
- **New repo pattern (linked source).** CI path filters must treat
  `src/Shared/` changes as affecting both packages.
- **No other frameworks.** TUnit, NUnit, and MSTest get no equivalent and
  no parity commitment, because their fixture models differ and there's
  no evidence of need.
- **AOT assembly fixtures stay unusable** until xUnit fixes its AOT
  generator ([xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664)).
  Compono can't work around that.

### Constraint carried forward

Fixture access, and therefore this API, requires composed rows to be
built at execution time (delayed enumeration). Any future design that
pre-enumerates or caches composed rows at discovery time, notably
[#163](https://github.com/LayeredCraft/compono/issues/163), must preserve
delayed enumeration or explicitly redesign fixture access. That design
also must not rely on JIT's once-per-attribute `Configure` caching for
per-row values. See RESEARCH-0037 §12.

## Pros and Cons of the Options

### Option 1: Documentation only

- Good, because it adds zero public API and zero maintenance.
- Bad, because every consumer re-derives the never-block, floor-safe,
  diagnostic details, and the discoverable call stays the incorrect one.

### Option 2: `ITestContext` extension in both packages (chosen)

- Good, because it's idiomatic xUnit, sitting next to `GetFixture`, and
  it keeps the ambient dependency explicit.
- Good, because it's unit-testable without a runner and has no
  reflection or dependencies.
- Bad, because it's public API on two packages, and it introduces linked
  shared source.

### Option 3: Static accessor over `TestContext.Current`

- Good, because it's the shortest call site.
- Bad, because it hides the ambient dependency inside a Compono static
  and can't be tested against a fake context.

### Option 4: `CompositionBuilder` extension

- Good, because it reads as composition configuration.
- Bad, because it couples the composition API to xUnit and covers only
  `Register`, while an accessor works inside any factory.

## Links

- Issues [#162](https://github.com/LayeredCraft/compono/issues/162),
  [#161](https://github.com/LayeredCraft/compono/issues/161),
  [#163](https://github.com/LayeredCraft/compono/issues/163)
- [RESEARCH-0037](../research/0037-issue-162-runtime-fixture-values-in-profiles.md),
  [RESEARCH-0036](../research/0036-issue-161-test-composition-gaps-intake.md),
  [RESEARCH-0016](../research/0016-async-composition-viability-research.md),
  [RESEARCH-0032](../research/0032-compono-xunitv3-native-aot-package-architecture.md)
- [ADR-0022](0022-compono-xunit-package-design.md),
  [ADR-0066](0066-compono-xunitv3-aot-package-architecture.md),
  [ADR-0067](0067-compono-xunitv3-aot-profile-support.md)
- [PLAN-0070](../plans/0070-xunitv3-require-fixture-accessor-impl-plan.md)
- Upstream: [xunit/xunit#3664](https://github.com/xunit/xunit/issues/3664)
  (AOT assembly-fixture generator emits code that doesn't compile)
- [`docs/architecture/capability-admission.md`](../architecture/capability-admission.md)

## Amendment (2026-11-04): the accessor class is `FixtureContextExtensions`, not `TestContextExtensions`

**Independent review finding.** The public extension class shipped as
`TestContextExtensions`, which collides (by name) with xUnit's own public
`Xunit.TestContextExtensions` — a type that exists in every supported xUnit
version and also hangs `GetFixture` helpers off `ITestContext`. Two
same-named extension classes both extending `ITestContext` are an
unnecessary source of ambiguity at the call site and in `using`-directive
resolution.

**Correction.** The class is renamed to `FixtureContextExtensions` in both
packages (`Compono.XunitV3.FixtureContextExtensions`,
`Compono.XunitV3.Aot.FixtureContextExtensions`). The extension method and
its signature are unchanged:

```csharp
namespace Compono.XunitV3;          // Compono.XunitV3.Aot declares the identical type in namespace Compono.XunitV3.Aot

public static class FixtureContextExtensions
{
    extension(ITestContext context)
    {
        public TFixture RequireFixture<TFixture>() where TFixture : class
        {
            // ...implementation...
        }
    }
}
```

Nothing else changes: same C# 14 extension-block syntax, same call site
(`TestContext.Current.RequireFixture<TFixture>()`), same behavior contract,
same exception messages, same single linked shared source. The shared
source file is renamed to `FixtureContextExtensions.cs` to match the type
name (one public type per file, filename == type name). This Amendment
records the rename; it does not reverse any part of the Decision Outcome.

## Amendment 2 (2026-11-04): the public generic `GetFixture<TFixture>()` lookup does exist on the xUnit 3.2.2 floor

**Independent review finding.** The Context section above and the original
behavior contract justified the non-generic lookup partly with "version
floor: `GetFixture<T>()` doesn't exist on xUnit 3.2.2." That claim is
**incorrect** and is corrected here.

**What is actually true.** xUnit 3.2.2 exposes two things:

- `ITestContext.GetFixture(Type)` — the **non-generic instance method**,
  present in 3.2.2 and 4.x.
- `Xunit.TestContextExtensions.GetFixture<TFixture>(this ITestContext)` — a
  public, **generic extension method** that wraps the non-generic call. It
  is present in 3.2.2 (verified: `xunit.v3.extensibility.core` 3.2.2,
  namespace `Xunit`). xUnit 4.x additionally promotes a generic
  **instance** member `ITestContext.GetFixture<TFixture>()` onto the
  interface itself.

So a generic lookup is reachable on the 3.2.2 floor via xUnit's own
extension method. The original "doesn't exist" wording was wrong.

**Why the implementation still uses `GetFixture(Type)`.** This is a
deliberate choice for semantics, not a floor workaround:

- **Never-wait contract.** xUnit's generic extension is `async` and awaits
  the `ValueTask` internally, so it cannot express this accessor's
  "inspect `IsCompleted`, throw immediately, never block" requirement. The
  non-generic overload returns the `ValueTask` directly, which the
  completion check needs.
- **Consistent semantics across supported versions.** The non-generic
  instance method has identical behavior on 3.2.2 and 4.x; routing through
  the generic path would resolve to an extension on 3.2.2 and an instance
  member on 4.x.
- **Clear failure behavior.** Using the returned `ValueTask` directly keeps
  the faulted/canceled/missing branches explicit and local.

The code comment in the shared source and the unit-test comment were
updated to match. No behavior changes.

## Amendment 3 (2026-11-04): fixture ownership, exact-type wording, and a framework-neutral alternative

**Independent review findings on the prose**, recorded here so the ADR's
negative-consequences guidance stays accurate:

- **Ownership.** Examples must dispose what they create. A
  `WebApplicationFactory` is `IDisposable`/`IAsyncDisposable`; an
  `HttpClient` returned by `CreateClient()` must be disposed too. Compono
  neither owns nor disposes anything it composes (Positive Consequences
  already say this) — the docs/examples were corrected so they don't imply
  otherwise and so the sample fixtures dispose their own resources.
- **Exact-type lookup wording.** xUnit's `GetFixture` returns `null` when
  the requested fixture type isn't available (missing type or exact-type
  mismatch); Compono's `RequireFixture` instead throws a descriptive
  `InvalidOperationException`. The docs now state that contrast explicitly
  rather than only describing Compono's behavior.
- **Framework-neutral alternative.** `RequireFixture` reads the ambient
  xUnit `TestContext.Current`, so it only works under a running xUnit test.
  The docs now also present the alternative for callers who don't want that
  coupling: build the composer by hand and pass the fixture into a profile
  instance —
  `Composer.Create(b => b.AddProfile(new P(fixture)))` — which works
  without an ambient xUnit `TestContext` (the caller supplies the fixture),
  but does not use attribute-driven parameter composition the way
  `[Compose<TProfile>]` does.
- **Upstream assembly-fixture fix.** Wording changed from "permanently
  unsupported" to "reported fixed upstream in xUnit 4.0.3-pre.19; not yet
  validated by Compono."
