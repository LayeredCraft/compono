# Installation

## Which packages to add

The common case is a test project that composes plain object graphs and
composed xUnit v3 theories. That's two packages:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.XunitV3 --prerelease
```

Add the rest of the ecosystem as your tests need it:

```bash
dotnet add package Compono.NSubstitute --prerelease   # automatic substitute composition
dotnet add package Compono.Bogus --prerelease         # semantic fake data (names, emails, ...)
```

Every package targets `net10.0`/`net11.0`. Until the first stable `1.0`
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

If your test project doesn't already reference an xUnit v3 test host,
`Compono.XunitV3` doesn't add one for you — it integrates with an existing
xUnit v3 project (`xunit.v3` + the Microsoft Testing Platform runner), it
doesn't create one.

## Verify the install

A minimal smoke check once the packages are added:

```csharp
using Compono;

var composer = Composer.Create();
var value = composer.Create<int>();
```

If this compiles and runs, the source generator is wired up correctly. Next,
[write your first composed theory](first-test.md).
