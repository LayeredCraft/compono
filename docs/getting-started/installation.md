# Installation

## Which packages to add

The common case is a test project that composes plain object graphs and
composed test method parameters/theories. That's two packages — `Compono`
plus whichever test-framework integration matches your test host:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.XunitV3 --prerelease
# or, for TUnit:
dotnet add package Compono.TUnit --prerelease
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
dotnet add package Compono.NSubstitute --prerelease   # automatic substitute composition
dotnet add package Compono.Bogus --prerelease         # semantic fake data (names, emails, ...)
```

Every package targets `net8.0`/`net9.0`/`net10.0`/`net11.0`. Until the first stable `1.0`
release, every published version is a `0.x.y-preview.N` prerelease — the
`--prerelease` flag (or an explicit `<PackageReference Version="0.x.y-preview.N" />`)
is required for `dotnet add package`/NuGet restore to pick it up at all,
since a plain `dotnet add package` skips prerelease versions by default. See
[Package Guides](../packages/index.md) for what each package is for and
when to add it.

## No other setup required

`Compono` embeds its source generator as a Roslyn analyzer inside its own
package (`analyzers/dotnet/cs`) — adding the `Compono` `PackageReference` is
the only step needed to enable source generation. There's no separate
generator package to add, no `nuget.config` entry beyond your normal NuGet
feed, and no MSBuild property required to opt in.

If your test project doesn't already reference an xUnit v3 (or TUnit) test
host, `Compono.XunitV3` (or `Compono.TUnit`) doesn't add one for you — each
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
