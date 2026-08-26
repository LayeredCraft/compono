# Package Guides

Compono ships as eight independently-installable NuGet packages. Pick which
ones you need before reading any single guide in depth — most projects only
need the first two.

| Package | What it adds | Install if... |
|---|---|---|
| [`Compono`](compono.md) | The core composition engine: `Composer`, the resolution pipeline, and the source generator (embedded, no separate install). | Always — every other package depends on it. |
| [`Compono.XunitV3`](compono-xunitv3.md) | `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` theory data attributes and `[Shared]` parameter sharing for xUnit v3. | You write xUnit v3 tests and want composed theory parameters instead of hand-built test data. |
| [`Compono.NSubstitute`](compono-nsubstitute.md) | Automatic substitute composition for interface, delegate, and (optionally) abstract-class parameters. | Your composed types depend on interfaces you'd otherwise stub by hand with NSubstitute. |
| [`Compono.Bogus`](compono-bogus.md) | Realistic fake data — member-name-convention matching plus explicit `Faker<T>` sugar. | You want `FullName`/`Email`/`StreetAddress`-shaped fields to look like real data instead of anonymous strings. |
| [`Compono.TUnit`](compono-tunit.md) | `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]` data source attributes and `[Shared]` parameter sharing for TUnit. | You write TUnit tests and want composed method parameters instead of hand-built data sources. |
| [`Compono.TestDoubles`](compono-testdoubles.md) | A source-generated, AOT-safe double for an otherwise-unresolvable interface leaf, with per-member `Configure().Member().Returns(...)`/`.Throws(...)`. | You want a generated interface double without `Compono.NSubstitute`'s runtime-proxy dependency, or need the composed path to survive `PublishAot`. |
| [`Compono.DependencyInjection`](compono-dependencyinjection.md) | `row.AsServiceProvider()` — a configured-resolution `IServiceProvider` bridge over a `CompositionRow`. | You need Compono's registered/provider-backed values reachable through a plain `IServiceProvider`, e.g. as a fallback provider for another ecosystem's own DI container. |
| [`Compono.Http`](compono-http.md) | `TestHttpHandler` — a reflection-free `HttpMessageHandler` test double: `OnGet`/`OnPost`/etc. + `When(...)` matching, strict unmatched-request behavior, registration-handle verification. | Your test needs to exercise the real `HttpClient` pipeline against a configured HTTP response, instead of substituting an application-level interface. |

Every package targets `net8.0`/`net9.0`/`net10.0`/`net11.0` and, until the
first stable `1.0` release, publishes as a `0.x.y-preview.N` prerelease —
see [Installation](../getting-started/installation.md) for the exact
`dotnet add package` commands and why `--prerelease` is required. See
[ADR-0038](../adr/0038-net8-net9-explicit-multi-target.md) for why `net8.0`/
`net9.0` were added alongside the existing `net10.0`/`net11.0` window.

`Compono.Generators` (the source generator itself) is not a separate
package you install — it's embedded inside `Compono.nupkg`'s
`analyzers/dotnet/cs` and activates automatically once you reference
`Compono`. See [`Compono`](compono.md) for what that means in practice.

## The common case

For most test projects, that's `Compono` plus whichever test-framework
integration matches your test host — `Compono.XunitV3` for xUnit v3, or
`Compono.TUnit` for TUnit:

```bash
dotnet add package Compono --prerelease
dotnet add package Compono.XunitV3 --prerelease
# or, for TUnit:
dotnet add package Compono.TUnit --prerelease
```

Add `Compono.NSubstitute` and/or `Compono.Bogus` independently, as your
tests need them — neither depends on the other, and both depend only on
`Compono`, never on `Compono.XunitV3` (composing plain object graphs with
NSubstitute-provided substitutes or Bogus-generated data works the same
whether or not you're also using xUnit v3 integration).

## Version compatibility

All eight packages ship in lockstep during the `0.x` line — each integration
package's dependency on `Compono` is exact-pinned at pack time, so mixing
versions across packages (e.g. `Compono.XunitV3 0.3.0` with `Compono
0.5.0`) is not supported and will fail to restore. Always update all
installed Compono packages together — see
[ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)
for the full `0.x` versioning policy.
