# Compono.XunitV3.Aot

xUnit v3 **Native AOT** integration — the `[Compose]` theory data attribute
for consumers publishing their test project with `PublishAot=true`, using
xUnit v3's own official Native AOT package family
(`xunit.v3.aot.mtp-v2` and friends). See
[ADR-0066](../adr/0066-compono-xunitv3-aot-package-architecture.md) for the
full architecture decision and
[RESEARCH-0031](../research/0031-native-aot-framework-native-testing-admission-research.md)/
[RESEARCH-0032](../research/0032-compono-xunitv3-native-aot-package-architecture.md)
for the evidence behind it.

## `Compono.XunitV3` vs. `Compono.XunitV3.Aot` — which do I install?

These are **two separate, mutually exclusive packages**, not one package
with a mode switch — never reference both in the same project.

| | `Compono.XunitV3` | `Compono.XunitV3.Aot` |
|---|---|---|
| Use when | Your test project runs under ordinary reflection-mode xUnit v3 (the default for almost every consumer today) | Your test project publishes with `PublishAot=true` using xUnit v3's own AOT package family |
| xUnit package it pairs with | `xunit.v3.mtp-v2` | `xunit.v3.aot.mtp-v2` |
| Minimum xUnit v3 version | Whatever `Compono.XunitV3` already supports | **4.0.0+** — the version that introduced xUnit's Native AOT support at all |
| Minimum .NET | Compono's ordinary floor | **.NET 9+** — xUnit's own stated Native AOT requirement |
| `[Compose]` surface | Full: inline values, `[Shared]`, `[Compose<TProfile>]`, `[Compose<TProfile, TConfig>]` | **Phase 1: plain `[Compose]` only** — no inline values, `[Shared]`, or profile variants yet |

**Why two packages, not one:** xUnit v3 ships its reflection-mode and
Native AOT surfaces as genuinely separate, non-interchangeable assemblies
— referencing both `xunit.v3.mtp-v2` and `xunit.v3.aot.mtp-v2` (or
`Compono.XunitV3` and `Compono.XunitV3.Aot`) in the same project fails to
compile (`CS0433`, a duplicate `TheoryAttribute`/`DataAttribute`
identity). This is a hard technical constraint, not a design choice — see
ADR-0066 for the full account of why a single package supporting both
modes isn't achievable through any supported NuGet/MSBuild mechanism.

You can, however, have **both packages installed across different test
projects in the same solution** — one project running JIT with
`Compono.XunitV3`, another publishing AOT with `Compono.XunitV3.Aot` —
that's a normal, supported setup.

## When to install

You publish your xUnit v3 test project as Native AOT (`PublishAot=true`)
and want theory parameters composed automatically, the same way
`Compono.XunitV3` does for ordinary JIT tests:

```bash
dotnet add package Compono
dotnet add package Compono.XunitV3.Aot
dotnet add package xunit.v3.aot.mtp-v2
```

## What it gives you (Phase 1)

- **`[Compose]`** — every theory parameter is composed. Usage is
  textually identical to `Compono.XunitV3`'s own `[Compose]`:

  ```csharp
  using Compono.XunitV3.Aot;
  using Xunit;

  public sealed class WidgetTests
  {
      [Theory]
      [Compose]
      public void Widget_is_composed_and_test_passes(Widget widget, string leaf)
      {
          Assert.False(string.IsNullOrEmpty(widget.Name));
          Assert.False(string.IsNullOrEmpty(leaf));
      }
  }
  ```

  Only the `using` (`Compono.XunitV3.Aot` instead of `Compono.XunitV3`)
  and the referenced packages change when moving a test project from JIT
  to Native AOT — the `[Theory]`/`[Compose]` source itself doesn't.

## Not yet supported (Phase 1 limitations)

`Compono.XunitV3.Aot.ComposeAttribute` is a parameterless-only, sealed,
non-generic attribute — the following `Compono.XunitV3` capabilities are
**not available** in this package yet, and there is no syntax to attempt
them (a real compile error results, not silent incorrect behavior):

- Inline values (`[Compose(42, "widget")]`)
- `[Shared]` parameter reuse
- `[Compose<TProfile>]` / `[Compose<TProfile, TConfig>]` profile variants

These are real, additional generator work deferred to a later phase — see
[PLAN-0066](../plans/0066-compono-xunitv3-aot-package-architecture-impl-plan.md).

## Unsupported method/parameter shapes (`CMP0040`)

Because `ComposeAttribute` is a marker only (see below) with no runtime
fallback, an unsupported `[Compose]`-attributed method shape is rejected
at compile time with **`CMP0040`** instead:

- A generic test method — rejected before any registration is built, so
  without this diagnostic the method would simply never be discovered by
  xUnit's Native AOT pipeline, with no diagnostic anywhere.
- A `ref`/`out`/`in` or `params` parameter — same silent-non-discovery
  failure mode as above.
- A parameter type that can never be a legal `CompositionRow.Resolve<T>()`
  type argument — an open generic parameter, a `ref struct`, a pointer, a
  function pointer, or an unsupported array shape. Without this
  diagnostic, code generation would proceed and emit a registration
  containing an illegal `CompositionRow.Resolve<T>()` call, so the
  consumer would see a generated-code compiler error instead of an
  actionable diagnostic pointing at their own test method.

See [CMP0040](../reference/diagnostics.md#cmp0040-unsupported-componoxunitv3aot-attributed-method-signature)
for the full diagnostic reference entry.

## How it actually works under the hood

Unlike `Compono.XunitV3.ComposeAttribute`, this package's `ComposeAttribute`
carries **no runtime composition logic** — it's a thin marker attribute,
matching the shape xUnit's own official Native AOT extensibility samples
(`AotCsvDataSource`, `AotRetryFact`, `AotTraitExtensibility`) use.
`Compono.Generators` discovers every `[Compose]`-attributed method at
compile time and emits a
`Xunit.v3.RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)`
registration — xUnit v3's own official Native AOT theory-data mechanism —
whose factory closure calls real, compile-time-closed-generic
`CompositionRow.Resolve<T>()` calls, exactly the same core `Compono`
composition machinery `Compono.XunitV3` uses. `DataAttribute.GetData` is
never invoked under this path at all; xUnit's AOT pipeline calls the
registered factory directly.

## Native AOT publish and run

```bash
dotnet publish YourTestProject.csproj -c Release -f net10.0 \
  -p:RuntimeIdentifier=<rid> -p:SelfContained=true -p:PublishAot=true -p:UseAppHost=true
./bin/Release/net10.0/<rid>/publish/YourTestProject
```

Pass `RuntimeIdentifier`/`SelfContained` as `-p:` MSBuild properties, not
the `-r <rid>`/`--self-contained` CLI shorthand flags — the shorthand form
can fail this exact publish shape with `MSB3030: Could not copy ... apphost
... because it was not found` in some project configurations. The
resulting native binary is a real, self-hosting xUnit v3 test executable —
run it directly; there is no `dotnet test` step for a published Native AOT
binary.

## Compatibility

- **xUnit v3 4.0.0+** required — earlier versions have no Native AOT
  support to integrate with at all.
- **.NET 9.0+** required — matches xUnit's own stated floor.
- **`Compono.XunitV3` is completely unaffected** by this package's
  existence — same package ID, same dependency on the reflection-mode
  `xunit.v3.extensibility.core`, same behavior. Installing
  `Compono.XunitV3.Aot` in a new project never changes anything about an
  existing `Compono.XunitV3` project elsewhere in your solution.
