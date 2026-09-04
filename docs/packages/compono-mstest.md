# Compono.MSTest

MSTest integration — an `ITestDataSource`-implementing `[Compose]` attribute
that composes test method parameters directly, instead of hand-building
`[DataRow]`/`[DynamicData]` rows.

## When to install

You write MSTest tests and want method parameters composed automatically:

```bash
dotnet add package Compono
dotnet add package Compono.MSTest
```

`Compono.MSTest` doesn't add an MSTest test host for you — it integrates
with an existing one. `[TestMethod]` alone is the intended syntax;
`[DataTestMethod]` is unnecessary — it "provides no additional value over
`TestMethodAttribute`" (`microsoft/testfx` issue #4166) and is actively
flagged by analyzer `MSTEST0044` for removal.

```csharp
[TestClass]
public class OrderServiceTests
{
    [TestMethod]
    [Compose]
    public void Creates_service(
        SomeService sut,
        [Shared] SomeDependency dependency)
    {
    }
}
```

## What it gives you

The full attribute family — see [ADR-0057](../adr/0057-compono-mstest-package-design.md)
for the full design and [PLAN-0057](../plans/0057-compono-mstest-package-design-impl-plan.md)
for implementation status.

- **`[Compose]`** — every method parameter is composed.
- **Inline + composed mixing** — `[Compose(42, "widget")]` binds inline
  values left-to-right; anything left over is composed.
- **`[Shared]`** — reuse one composed instance across every parameter (or
  nested dependency) in the same row that requests the same type. See
  [Shared Values](../concepts/shared-values.md).
- **`Seed`** — `[Compose(Seed = ...)]` reproduces a specific composed row
  exactly. A row's seed is always surfaced via `GetDisplayName`'s output
  (below), and a composition failure's message includes the seed that
  produced it.
- **`[Compose<TProfile>]`** — applies a fixed, default-constructed profile
  to the row's `Composer`, matching `Compono.XunitV3`/`Compono.TUnit`'s own
  `ComposeAttribute<TProfile>` exactly:

  ```csharp
  [TestMethod]
  [Compose<NSubstituteTestProfile>]
  public async Task Saves_order([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
  {
      await handler.Handle(command);
      await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
  }
  ```

- **`[Compose<TProfile, TConfig>]`** — profile selection and profile
  configuration arguments, matching `Compono.XunitV3`/`Compono.TUnit`'s own
  shape exactly, including the once-per-attribute-instance reflection bound
  (`ConfigProfileBinder`, mirrored into `Compono.MSTest.Binding`).

## Hard constraint: one Compose-family attribute per method

`[Compose]`, `[Compose<TProfile>]`, and `[Compose<TProfile, TConfig>]` are
all `ComposeAttribute` subclasses. `[AttributeUsage(AllowMultiple = false)]`
is enforced per exact attribute type by the compiler, not across the
family — stacking two *different* Compose-family attributes on one method
compiles, but `BindingPlan.ValidateSignature` rejects it with a clear
`CompositionException` the first time MSTest asks for that method's data.

## `[DataRow]`/`[DynamicData]` coexistence

`[DataRow]`, `[DynamicData]`, and `[Compose]` are **independent, complete-row
data sources** on the same method — they never merge into one row. A method
carrying both `[DataRow(1, "a")]` and `[Compose]` produces two independent
test cases, each with every parameter supplied entirely by its own data
source. `[Compose]` never "fills in" values `[DataRow]` didn't supply —
this mirrors `Compono.XunitV3`/`Compono.TUnit`'s own scope: inline values
belong to `[Compose(...)]`'s own constructor, never a separate attribute.

## Discovery/execution repeat-composition behavior

**MSTest may invoke a `[Compose]` attribute's `GetData` more than once
across separately-invoked discovery and execution sessions.** Consequently,
Compono composition — including any side-effecting `Register<T>()` factory
or `ICompositionValueProvider` it invokes — may also execute more than once
for what you perceive as one eventual test case.

This is **not** "every run composes twice." A single `dotnet test`/
`dotnet vstest` invocation (MTP or the classic VSTest adapter — both were
verified) produces exactly one `GetData` call per method. It's specifically
a *separate discovery process followed by a separate execution process* —
the realistic Visual Studio Test Explorer workflow (discover once when the
tree populates, execute separately and possibly repeatedly afterward) —
that produces two calls, one per process. Compono establishes no
exactly-once contract for `Register<T>()` factories that would excuse
this — if your factory has an observable side effect (I/O, a counter,
external state mutation), it may genuinely run more than once under this
workflow.

**Reproducibility across those separate calls depends on whether `Seed` is
pinned — an unqualified claim of "logically reproducible values" here would
overstate it:**

- **Unpinned `[Compose]`** — each independent `GetData` call generates its
  own fresh, non-negative random seed (`CompositionBuilder.WithSeed` is
  never called). Two separate calls therefore generally produce **different**
  composed values, not the same ones — a discovery-time row and a later,
  separately-invoked execution-time row are not guaranteed to describe the
  same generated graph, and a seed shown for a discovery row should not be
  presented as sufficient to reproduce a later, independently-generated
  execution row.
- **`[Compose(Seed = N)]`** — every independent `GetData` call uses the
  identical, explicitly configured seed, so every call *does* produce the
  same deterministic composed values (same seed → same generated values),
  even though each call still builds its own distinct `CompositionRow`
  instance. This is the mechanism to reach for when exact reproduction
  across separate discovery/execution sessions actually matters.
- **A composition *failure*** carries the seed that specific failing
  execution actually used, via `CompositionException`'s seed-enriched
  message (`CompositionException.WithSeedInMessage`) — that's the
  execution-time reproduction value to paste back into `Seed`, independent
  of whatever a discovery-time row's own display name showed.

`[Shared]`/`Share<T>()` are never split across calls: each `GetData`
invocation gets its own fresh `CompositionRow`, so sharing stays correct
*within* one call regardless of how many times `GetData` itself runs.

## Seed and display name

`ITestDataSource.GetDisplayName(MethodInfo, object?[]?)` is the primary and
only supported seed-reporting path — no `TestContext.Properties`/
`TestProperty` usage. Every row's display name has the form:

```
{methodName} (Compono, seed: {seed})
```

without dumping composed object values into the name.

**Where this actually shows up, verified directly**: `GetDisplayName` is
called by MSTest during *discovery*/listing (`--list-tests`, `dotnet vstest
-lt`, Visual Studio Test Explorer's own tree population) under both MTP and
the classic VSTest adapter — confirmed the seed appears there, e.g.
`ComposesTwoStrings_RealRun (Compono, seed: 1913922119)`. It is **not**
called during an ordinary `dotnet test`/`dotnet vstest` execution run under
either runner — confirmed empirically, not assumed. In other words: the
seed-bearing display name is what you see when you *browse* tests (Test
Explorer's tree, `--list-tests` output), not necessarily in the console
summary line of a plain `dotnet test` run. A composition *failure*'s own
exception message always carries the seed too (via
`CompositionException.WithSeedInMessage`), independent of `GetDisplayName`
— that's the path visible in an ordinary failing execution run.

A seed shown in a discovery-time display name only reproduces a later
execution row if `Seed` was explicitly pinned (`[Compose(Seed = N)]`) —
see "Discovery/execution repeat-composition behavior" above for why an
unpinned `[Compose]`'s discovery-time seed and its execution-time seed are
generally different values.

## Synchronous-only composition

`ITestDataSource.GetData` returns `IEnumerable<object?[]>` — no `Task`/
`ValueTask` anywhere in its signature, a **harder** constraint than
`Compono.XunitV3`'s own `ValueTask`-returning `GetData`. `[Compose]`-supplied
MSTest parameters are synchronously composed, full stop. Async resource
initialization belongs in MSTest's own lifecycle (`[ClassInitialize]`/
`[AssemblyInitialize]`), with the already-initialized resource registered
into Compono synchronously.

## Non-ownership / disposal

`Compono.MSTest` does not dispose composed argument objects — a composed
value's provenance (freshly constructed vs. a shared/cached instance from a
registration or configured `IServiceProvider`) is indistinguishable from
Compono's own vantage point, so no disposal-tracking mechanism is
introduced that could risk disposing an externally-owned instance. Use
MSTest's own post-test lifecycle (`[TestCleanup]`, `IDisposable`/
`IAsyncDisposable` on the test class itself) for your own disposal needs.

## `TestContext`

`Compono.MSTest` does **not** auto-inject `TestContext` (or any other
MSTest framework value) as a composed parameter. MSTest already provides
`TestContext` idiomatically — constructor injection (MSTest 3.6+) or the
classic `public TestContext TestContext { get; set; }` property — with
ownership unambiguously MSTest's.

## MTP and VSTest

`Compono.MSTest` supports MSTest under **both** the modern
`Microsoft.Testing.Platform` (MTP, the `dotnet new mstest` default) and the
classic VSTest adapter — `ITestDataSource` is a first-party MSTest
extension point exercised identically by both, and `Compono.MSTest`
introduces no runner-selection logic of its own. Runner choice stays
entirely your project's own configuration (`<UseVSTest>`).

## MSTest version floor

**`MSTest.TestFramework` `4.0.0` or later.** `MSTest.TestFramework`'s `3.x`
line ships its framework types under a different, binary-incompatible
assembly identity (`Microsoft.VisualStudio.TestPlatform.TestFramework.dll`)
than `4.x`'s (`MSTest.TestFramework.dll`), with no compatibility bridge
between them — a `Compono.MSTest` built against one cannot serve a
consumer on the other. If you're still on MSTest `3.x`, upgrade to `4.x` to
use `Compono.MSTest`; see [ADR-0057](../adr/0057-compono-mstest-package-design.md)'s
Amendment 1 for the full evidence behind this decision.

## Native AOT

`Compono.MSTest`'s dispatch path is Native AOT-safe end to end — no
`MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` anywhere, per
[ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)'s shared
`RowInvokerRegistry` mechanism. Proven by a real `dotnet publish -c Release
-p:PublishAot=true` build and run against the packaged `Compono`/
`Compono.MSTest` dependency chain (`test/Compono.MSTest.AotSmokeTest`),
driving the real `ComposeAttribute.GetData` through both a custom composed
type and a provider-resolved leaf type.

`[Compose<TProfile, TConfig>]`'s `ConfigProfileBinder` needed the same AOT
gate `Compono.TUnit` already required — `ConstructorInfo.Invoke`-based
construction on a closed generic type argument is **not** safe by default
under trimming; the trimmer strips a type's public constructors unless
something tells it they're reachable. `ConfigProfileBinder` and
`ComposeAttribute<TProfile, TConfig>` carry
`[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
annotations end to end to fix this, verified by the same AOT smoke test
exercising `[Compose<TProfile, TConfig>]` alongside the plain form —
zero trim warnings attributable to `Compono.MSTest`'s own code.

## Migrating from `[DynamicData]`

A `[DynamicData]`-backed test typically looks like:

```csharp
[TestMethod]
[DynamicData(nameof(GetOrders), DynamicDataSourceType.Method)]
public void Processes_order(Order order)
{
    // ...
}

private static IEnumerable<object[]> GetOrders()
{
    yield return new object[] { new Order("widget", 3) };
    yield return new object[] { new Order("gadget", 1) };
}
```

Replace the hand-written data method with `[Compose]`, composing the
parameter instead of hand-authoring rows:

```csharp
[TestMethod]
[Compose]
public void Processes_order(Order order)
{
    // order is composed - a real Order with realistic constructor arguments
}
```

If a test genuinely needs specific, fixed values (not realistic composed
data), supply them inline instead: `[Compose(new Order("widget", 3))]`, or
keep `[DataRow]`/`[DynamicData]` as its own independent data source on the
method — the two never merge, so pick one per parameter set your test
actually needs. No real internal MSTest consumer existed to validate this
migration path against during the package's own initial development; the
external MSTest packaged-consumer validation fixture (see
[PLAN-0057](../plans/0057-compono-mstest-package-design-impl-plan.md) task
group 15) is where this actually got exercised for real before shipping.

## What it deliberately doesn't do

- **No fixture object** — configuration lives in a profile, applied per
  test method, not a shared mutable object.
- **No `TestContext` auto-injection** — see above.
- **No `[DataRow]`/`[Compose]` per-parameter merging** — see above.

## Next

- [Shared Values](../concepts/shared-values.md)
- [ADR-0057](../adr/0057-compono-mstest-package-design.md)
