# Compono.NUnit

Only relevant if the project references `Compono.NUnit`. Requires
`NUnit >= 3.14.0, < 5.0.0` — one compiled `Compono.NUnit` package
supports the whole range (empirically proven binary-compatible across
3.14.0/4.6.1/5.0.0-beta.1; no `Compono.NUnit3`/`Compono.NUnit4` split
exists or is planned). Depends on `Compono` (the source generator flows
through transitively).

The full attribute family: `[Compose]`, `[Compose<TProfile>]`, and
`[Compose<TProfile, TConfig>]`, method-parameter-only — see
[ADR-0059](../../../docs/adr/0059-compono-nunit-package-design.md) for
the full design.

## `[Compose]` alone — NO `[TestFixture]`, NO `[Test]`

This is the single most important fact about this package, and the
opposite of `Compono.XunitV3`/`Compono.TUnit`/`Compono.MSTest`:

```csharp
public class OrderServiceTests
{
    [Compose]
    public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

    [Compose(42, "widget")]           // inline binds positionally left-to-right
    public void InlineValuesAreUsedDirectly(int quantity, string productName) { }

    [Compose(42)]                     // quantity inline, productName composed
    public void MixesInlineAndComposedValues(int quantity, string productName) { }

    [Compose(Seed = 4219)]
    public void ReproducesTheSameComposedValues(Order order) { }
}
```

**Never suggest adding `[TestFixture]` to the class or `[Test]` to the
method.** `ComposeAttribute` derives from NUnit's own `TestAttribute` and
implements `ITestBuilder` directly — `[Compose]` alone makes the method a
real, independently discovered NUnit test. Adding `[TestFixture]` is
harmless (NUnit ignores it) but never required, and suggesting `[Test]`
alongside `[Compose]` is wrong — NUnit would then try to build two
competing test cases for the same method from two different builders.

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately,
  before any row state is reported. Seed appears in the test's display
  name: `ComposedMethod(Compono, seed: 4219)`.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — `Compono.NUnit.SharedAttribute`, a distinct
  type from `Compono.XunitV3.SharedAttribute`/`Compono.TUnit.SharedAttribute`/
  `Compono.MSTest.SharedAttribute`, with identical binding rules.
- Only one Compose-family attribute per method — stacking two (even of
  different generic arities) is a `CompositionException` at build time.

## `[Compose<TProfile>]` / `[Compose<TProfile, TConfig>]`

Identical semantics to every other framework package — see
`composition-model.md`/`registrations-profiles-and-scopes.md` for the
shared profile model. `[Compose<TProfile, TConfig>]`'s constructor
arguments bind to `TConfig`, never to the test method's own parameters
(every test-method parameter is still composed in full).

## `[Compose]` + NUnit's own data sources: independent, never merged

`[Compose]` owns its own complete row. NUnit's own `[TestCase]`,
`[Values]`, `[Range]`, and any custom `IParameterDataSource` on the same
method each independently produce their **own, separate** test case(s) —
never merged into the Compose row, and `[Compose]` never consumes or
reads them:

```csharp
[Compose]
[TestCase(42)]
public void Mixed(int value) { }
// -> 2 independent test cases: one composed, one literal (42)

[Compose]
public void WithValues([Values(1, 2, 3)] int value) { }
// -> 4 independent test cases: 1 composed + 3 from [Values] (1, 2, 3)
```

Never suggest that `[Compose]` merges with or "wins over" a parameter-level
source, and never suggest building custom merging logic — this
independent-row behavior is NUnit's own `ITestBuilder` model, not a
Compono-imposed restriction.

## Runner support

Both classic VSTest (`NUnit3TestAdapter`) and Microsoft Testing Platform
(`<EnableNUnitRunner>true</EnableNUnitRunner>`) are supported —
`Compono.NUnit` itself is runner-neutral, no adapter/runner package
dependency.

## Composition boundaries (same as every framework package)

- **Synchronous only** — no async composition path; async setup belongs
  in `[OneTimeSetUp]`/`[SetUp]`, with the ready value registered
  synchronously.
- **Non-owning** — `Compono.NUnit` never disposes a composed value.
  `[TearDown]`/`[OneTimeTearDown]`/fixture `IDisposable` remain the
  consumer's own disposal seam.
- **`TestContext.CurrentContext` stays NUnit's** — never suggest composing
  or special-casing it; it's a static/ambient accessor, not a parameter
  Compono injects.
- **May run more than once per eventual test case** — under classic
  VSTest, a separately-invoked discovery session followed by a separate
  execution session each independently call `BuildFrom`. Don't promise
  exactly-once composition.

## Native AOT — two separate claims, don't conflate them

`Compono.NUnit`'s own integration code is reflection-free/trim-safe on
the same terms as the other framework packages (framework-mandated
`MethodInfo`/`IMethodInfo` access only, no dynamic generic activation).
**Never claim NUnit's own runner/adapter chain is Native-AOT-runnable** —
that's a separate, broader claim this package does not make and no
evidence supports.
