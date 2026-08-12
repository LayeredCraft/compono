# Compono.TUnit

TUnit integration — a `[Compose]` data source attribute that composes test
method parameters directly, instead of hand-building `[Arguments]` rows or a
custom data source generator.

## When to install

You write TUnit tests and want method parameters composed automatically:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.TUnit --prerelease
```

`Compono.TUnit` doesn't add a TUnit test host for you — it integrates with
an existing one. If your project targets xUnit v3, NUnit, or MSTest, see
[`Compono.XunitV3`](compono-xunitv3.md) instead (or, for NUnit/MSTest, core
`Compono` still works standalone via `Composer.Create()` and the resulting
composer's own `Create<T>()`).

## What it gives you (today)

PLAN-0040 Phase 0/1 have shipped — see
[ADR-0040](../adr/0040-compono-tunit-package-design.md) for the full design
and [PLAN-0040](../plans/0040-compono-tunit-package-design.md) for phase
status.

- **`[Compose]`** — every method parameter is composed:

  ```csharp
  [Test]
  [Compose]
  public async Task Saves_order(OrderService service)
  {
      await Assert.That(service).IsNotNull();
  }
  ```

- **Inline + composed mixing** — `[Compose(42, "widget")]` binds inline
  values left-to-right; anything left over is composed.
- **`[Shared]`** — reuse one composed instance across every parameter (or
  nested dependency) in the same row that requests the same type. See
  [Shared Values](../concepts/shared-values.md).
- **`Compose(Seed = ...)`** — reproduce a specific composed row exactly;
  a passing row also reports the seed back as a `Compono.Seed` custom
  property (`TestContext.Current.Metadata.TestDetails.CustomProperties`),
  and a *composition* failure's message includes the seed that produced it.

- **`[Compose<TProfile>]`** — applies a fixed, default-constructed
  profile to the row's `Composer`, matching
  [`Compono.XunitV3`](compono-xunitv3.md)'s own `ComposeAttribute<TProfile>`
  exactly:

  ```csharp
  [Test]
  [Compose<NSubstituteTestProfile>]
  public async Task Saves_order([Shared] IOrderRepository repository, CreateOrderHandler handler, PlaceOrder command)
  {
      await handler.Handle(command);
      await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
  }
  ```

- **`[Compose<TProfile, TConfig>]`** — profile selection and profile
  configuration arguments, matching
  [`Compono.XunitV3`](compono-xunitv3.md#profile-configuration-arguments)'s
  own shape exactly, including its once-per-attribute-instance reflection
  bound (`ConfigProfileBinder`, mirrored into `Compono.TUnit.Binding`).

## Hard constraint: one Compose-family attribute per method

`[Compose]`, `[Compose<TProfile>]`, and `[Compose<TProfile, TConfig>]` are
all `ComposeAttribute` subclasses. `[AttributeUsage(AllowMultiple = false)]`
is enforced per exact attribute type by the compiler, not across the
family — stacking two *different* Compose-family attributes on one method
compiles, but `BindingPlan.ValidateSignature` rejects it at
data-generation time with a clear `CompositionException`.

## Native AOT

`Compono.TUnit`'s dispatch path is Native AOT-safe end to end — no
`MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` anywhere, per
[ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)'s shared
`RowInvokerRegistry` mechanism. Proven by a real `dotnet publish -c Release
-p:PublishAot=true` build and run against the packaged `Compono`/
`Compono.TUnit` dependency chain (`test/Compono.TUnit.AotSmokeTest`), driving
the real `ComposeAttribute.GetDataRowsAsync` through both a custom composed
type and a provider-resolved leaf type.

`[Compose<TProfile, TConfig>]`'s `ConfigProfileBinder` needed its own
separate AOT gate (ADR-0041 Amendment 1) — `ConstructorInfo.Invoke`-based
construction on a closed generic type argument is **not** safe by default
under trimming; the trimmer strips a type's public constructors unless
something tells it they're reachable. `ConfigProfileBinder` and
`ComposeAttribute<TProfile, TConfig>` carry
`[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]`
annotations end to end to fix this, verified by the same AOT smoke test
exercising `[Compose<TProfile, TConfig>]` alongside the plain form.

## Disposal

TUnit disposes a `[Compose]`-composed **root** method argument itself,
automatically, once the test completes — `Compono.TUnit` needs no
`IDisposable`/`ITestEndEventReceiver` cleanup of its own for that case. A
non-`[Shared]` dependency **nested** inside a composed argument (e.g. a
constructor parameter one level down) is disposed by no one: TUnit's own
nested-object disposal tracking is scoped to `IAsyncInitializer`-registered
properties, not a general graph walk. Don't compose a cross-test-shared
disposable as a `[Compose]`/`[Shared]` parameter either — TUnit's reference
counting for shared values has no provenance awareness of where a value
came from. See ADR-0040's "Diagnostics, disposal, and seed observability"
section for the full reasoning.

## What it deliberately doesn't do

- **No fixture object** — configuration lives in a profile, applied per
  test method, not a shared mutable object.

## Next

- [Shared Values](../concepts/shared-values.md)
- [ADR-0040](../adr/0040-compono-tunit-package-design.md)
