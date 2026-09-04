# Installation

## Which packages to add

The common case is a test project that composes plain object graphs and
composed test method parameters/theories. That's two packages — `Compono`
plus whichever test-framework integration matches your test host:

```bash
dotnet add package Compono
dotnet add package Compono.XunitV3
# or, for TUnit:
dotnet add package Compono.TUnit
# or, for MSTest (MSTest.TestFramework 4.0.0+ - see the Compono.MSTest Package Guide):
dotnet add package Compono.MSTest --prerelease
# or, for NUnit (NUnit 3.14.0+ - see the Compono.NUnit Package Guide):
dotnet add package Compono.NUnit --prerelease
```

This tutorial's assertions (`.Should()`, throughout this site's own
examples) come from [AwesomeAssertions](https://github.com/AwesomeAssertions/AwesomeAssertions),
not Compono itself — add it too if your project doesn't already reference
an assertion library:

```bash
dotnet add package AwesomeAssertions
```

Add the rest of the ecosystem as your tests need it:

```bash
dotnet add package Compono.NSubstitute         # automatic substitute composition
dotnet add package Compono.Bogus               # semantic fake data (names, emails, ...)
dotnet add package Compono.DependencyInjection # row.AsServiceProvider() bridge
dotnet add package Compono.Http                # TestHttpHandler for HttpClient tests
```

Every package targets `net8.0`/`net9.0`/`net10.0`/`net11.0`. Most packages
have a stable release, so a plain `dotnet add package` picks it up with no
extra flag. `Compono.MSTest` and `Compono.NUnit` are still preview-only (no
stable release has shipped for either yet), so they need `--prerelease`
(or an explicit `<PackageReference Version="0.x.y-preview.N" />`) until
their own first stable release ships — a plain `dotnet add package` skips
prerelease versions by default, so it would otherwise resolve nothing for
those two. See [Package Guides](../packages/index.md) for what each
package is for and when to add it.

## No other setup required

`Compono` embeds its source generator as a Roslyn analyzer inside its own
package (`analyzers/dotnet/cs`) — adding the `Compono` `PackageReference` is
the only step needed to enable ordinary composition-plan generation.
There's no separate generator package to add, no `nuget.config` entry
beyond your normal NuGet feed, and no MSBuild property required to opt in
to that.

`Compono.TestDoubles` is the one exception: it needs an explicit
`<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>` MSBuild
property set in your project in addition to the package reference — see
[`Compono.TestDoubles`](../packages/compono-testdoubles.md#compile-time-opt-in).

If your test project doesn't already reference an xUnit v3 (or TUnit, or
MSTest, or NUnit) test host, `Compono.XunitV3` (or `Compono.TUnit`, or
`Compono.MSTest`, or `Compono.NUnit`) doesn't add one for you — each
integrates with an existing test project, it doesn't create one.

## Verify the install

A minimal smoke check once the packages are added:

```csharp
using Compono;

var composer = Composer.Create();
var value = composer.Create<InstallationCheck>();

public sealed class InstallationCheck;
```

Use a plain user-defined type here, not a built-in one like `int` — a
built-in type is satisfied by Compono's own built-in value provider without
ever reaching generated construction, so it would compile and run even if
the source generator/analyzer wasn't actually wired up. A custom type like
`InstallationCheck` only composes successfully through a real
generator-produced construction plan, so this check genuinely exercises the
generator, not just the core package.

If this compiles and runs, the source generator is wired up correctly. Next,
[write your first composed theory](first-test.md).
