# Compono.NUnit

NUnit integration — a `TestAttribute`/`ITestBuilder`-implementing `[Compose]`
attribute that composes test method parameters directly, without needing
`[TestFixture]` on the containing class or a separate `[Test]` attribute.

## When to install

You write NUnit tests and want method parameters composed automatically:

```bash
dotnet add package Compono
dotnet add package Compono.NUnit --prerelease
```

`Compono.NUnit` doesn't add an NUnit test host for you — it integrates with
an existing one (both classic VSTest and Microsoft Testing Platform are
supported — see "Runner support" below). `[Compose]` alone makes a method a
real, independently discovered NUnit test — **no `[TestFixture]` on the
containing class, and no separate `[Test]` attribute, are required or
expected**:

```csharp
public class OrderServiceTests
{
    [Compose]
    public void Creates_service(
        SomeService sut,
        [Shared] SomeDependency dependency)
    {
    }
}
```

This is the single most important thing to know about `Compono.NUnit`,
because it's the opposite of `Compono.XunitV3`/`Compono.TUnit`/
`Compono.MSTest` — those need a separate `[Fact]`/`[Test]`/`[TestMethod]`
alongside `[Compose]`; `Compono.NUnit` never does, and adding `[TestFixture]`
"just in case" is harmless but unnecessary. See
[ADR-0059](../adr/0059-compono-nunit-package-design.md) for the full design
and [PLAN-0059](../plans/0059-compono-nunit-package-design-impl-plan.md) for
implementation status.

## What it gives you

- **`[Compose]`** — every method parameter is composed; the method itself
  needs no other test-identifying attribute.
- **Inline + composed mixing** — `[Compose(42, "widget")]` binds inline
  values left-to-right; anything left over is composed.
- **`[Shared]`** — reuse one composed instance across every parameter (or
  nested dependency) in the same row that requests the same type. See
  [Shared Values](../concepts/shared-values.md).
- **`Seed`** — `[Compose(Seed = ...)]` reproduces a specific composed row
  exactly. A row's seed is always surfaced in the test's display name
  (`ComposedMethod(Compono, seed: 12345)`), and a composition failure's
  message includes the seed that produced it. An unpinned seed is fresh on
  every `BuildFrom` invocation — under classic VSTest, a separately-invoked
  discovery session followed by a separate execution session can therefore
  see two different unpinned seeds for the same eventual test case; pin
  `Seed` for a reliably reproducible row.
- **`[Compose<TProfile>]`** — applies a fixed, default-constructed profile
  to the row's `Composer`, matching `Compono.XunitV3`/`Compono.TUnit`/
  `Compono.MSTest`'s own `ComposeAttribute<TProfile>` exactly.
- **`[Compose<TProfile, TConfig>]`** — constructs `TConfig` from this
  attribute's own constructor arguments, then constructs and applies
  `TProfile` from that `TConfig` — a distinct binding target from ordinary
  inline values; every test-method parameter is still composed in full.

## `[Compose]` and NUnit's own data sources coexist, never merge

One `[Compose]`-family attribute owns its entire row. NUnit's own
`[TestCase]`, `[Values]`, `[Range]`, and any custom
`IParameterDataSource`-implementing attribute keep producing their own,
completely independent test case(s) on the same method — never merged with
`[Compose]`'s row, and never suppressed by it either:

```csharp
[Compose]
[TestCase(42)]
public void Mixed(int value)
{
}
// -> two independent test cases: one composed, one literal (42)

[Compose]
public void WithValues([Values(1, 2, 3)] int value)
{
}
// -> four independent test cases: one composed (Compose ignores [Values]
//    entirely) plus three from [Values] itself (1, 2, 3)
```

## Runner support

Both classic VSTest (`NUnit3TestAdapter` + `Microsoft.NET.Test.Sdk`) and
Microsoft Testing Platform (`<EnableNUnitRunner>true</EnableNUnitRunner>`)
are supported — `Compono.NUnit` itself is runner-neutral and takes no
dependency on either; which one runs your tests is entirely your test
project's own choice.

## Supported NUnit versions

`NUnit >= 3.14.0, < 5.0.0` — one compiled `Compono.NUnit` package supports
the full range; there is no `Compono.NUnit3`/`Compono.NUnit4` split.
NUnit 5 is prerelease as of this writing and is tracked as
forward-compatibility surveillance, not a current support target; the range
will widen once NUnit 5.0.0 ships stable. See
[ADR-0059](../adr/0059-compono-nunit-package-design.md) §3/§5 for the
evidence.

## Composition boundaries

- **Synchronous only** — composition happens inside `BuildFrom`, which
  NUnit calls synchronously; there is no async composition path. Async
  resource setup belongs in NUnit's own `[OneTimeSetUp]`/`[SetUp]`
  lifecycle, with the already-initialized resource registered into Compono
  synchronously.
- **Non-owning** — `Compono.NUnit` never disposes a composed value. NUnit's
  own `[TearDown]`/`[OneTimeTearDown]`/`IDisposable` fixture lifecycle
  remains your own disposal seam.
- **`TestContext` stays NUnit's** — `Compono.NUnit` never composes or
  special-cases `TestContext.CurrentContext`; it's NUnit's own
  static/ambient accessor, not a parameter Compono ever injects.
- **Composition may run more than once for one eventual test case** — under
  classic VSTest, a separately-invoked discovery session followed by a
  separate execution session each independently call `BuildFrom`. A
  registration factory or `ICompositionValueProvider` may therefore run
  more than once for what appears to be a single test. `[Shared]`/
  `Share<T>()` remain correct *within* each independently-built row.

## Native AOT / trimming

`Compono.NUnit`'s own integration code is reflection-free/trim-safe on the
same terms as `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest` —
framework-mandated `MethodInfo`/`IMethodInfo` access only, no
`MakeGenericType`/`Activator.CreateInstance`/dynamic generic activation.
Whether NUnit's own runner/adapter chain is itself Native-AOT-runnable is a
separate, narrower claim this package does not make either way — see
[ADR-0059](../adr/0059-compono-nunit-package-design.md) §17.
