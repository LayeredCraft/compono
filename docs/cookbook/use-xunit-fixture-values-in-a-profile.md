---
title: Use xUnit Fixture Values in a Profile
description: Feed a value xUnit already initialized — a class, collection, or assembly fixture — into a Compono composition via RequireFixture<TFixture>().
packages: [Compono, Compono.XunitV3]
concepts: [profiles, registration]
---

# Use xUnit Fixture Values in a Profile

## Problem

A composed value needs something xUnit owns and initializes for you: a
`WebApplicationFactory<Program>`, a live database connection, a real HTTP
handler — declared as an `IClassFixture<T>`, `ICollectionFixture<T>`, or
`[assembly: AssemblyFixture(typeof(T))]`. Compono composes its own values
from scratch and has no way to see the fixture instance xUnit built, so a
test can't normally route a fixture value into a composed graph.

## Solution

Call `TestContext.Current.RequireFixture<TFixture>()` **inside a
`Register` factory**, and register the value it returns:

```csharp
using Xunit;

public sealed class WebAppFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>();
        await Task.Yield();
    }

    public async ValueTask DisposeAsync() => await Factory.DisposeAsync();
}

public sealed record ApiClient(HttpClient Client) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class ApiTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Register(
        () => new ApiClient(
            TestContext.Current.RequireFixture<WebAppFixture>().Factory.CreateClient()));
}

public sealed class ApiTests : IClassFixture<WebAppFixture>
{
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task Client_reaches_the_fixture(ApiClient client)
    {
        await using (client)
        {
            client.Client.BaseAddress.Should().NotBeNull();
        }
    }
}
```

The same call works for a collection fixture
(`ICollectionFixture<T>` on a `[CollectionDefinition]`, referenced by
`[Collection("...")]`) and an assembly fixture
(`[assembly: AssemblyFixture(typeof(T))]`).

## Discussion

**Read it inside the `Register` factory, not once at profile construction
or `Configure` time.** Compono composes rows at test *execution* time
(`[Compose]` disables discovery-time enumeration), so a factory invoked
while xUnit is running the test sees a live `TestContext.Current` with its
fixtures already initialized — and it runs per row, so it always reads the
fixture belonging to the test that is actually running. `TestContext.Current`
is ambient and available here, but a profile instance can outlive a single
row: under the JIT engine `Configure` runs once per attribute instance and
any value it captured is reused for every row that instance composes (the
AOT engine constructs a profile per row). Capturing the fixture in the
constructor or in `Configure` therefore risks stale or cross-test state;
the registration factory avoids that and behaves identically on both
engines.

**`RequireFixture<TFixture>()` matches the exact type only.** xUnit's own
`GetFixture` returns `null` when the requested exact fixture type isn't
available (a missing fixture, or a base-type/interface request against a
concrete fixture); `RequireFixture<TFixture>()` turns that into a
descriptive `InvalidOperationException` instead of a downstream `null`.
Declare and request the same concrete type on both sides.

**It never waits for fixture initialization.** If the requested fixture
hasn't finished initializing, the call throws immediately rather than
blocking — declare the fixture concretely so xUnit initializes it before
the test runs, instead of relying on a lazy or partially-initialized
value.

**It's the same API in both packages.** `Compono.XunitV3` exposes it in
namespace `Compono.XunitV3`; `Compono.XunitV3.Aot` exposes an identical
method in namespace `Compono.XunitV3.Aot` — only the `using` changes.

**Compono doesn't own or dispose composed values.** The test owns whatever
it composes, so dispose it explicitly. Above, the fixture disposes its own
`WebApplicationFactory`, and the test disposes the `ApiClient` (which
disposes its `HttpClient`). `RequireFixture` never disposes the fixture or
changes its lifetime — that stays xUnit's (per class / per collection /
per assembly).

## Without an ambient xUnit TestContext

`RequireFixture` reads `TestContext.Current`, so it only works while an
xUnit test is running. If you'd rather not couple a profile to xUnit's
ambient context, take the fixture as a constructor argument, pass it in
when you build the composer, and own the disposal of everything you create:

```csharp
public sealed class FixtureBackedApiProfile : ICompositionProfile
{
    private readonly WebAppFixture _fixture;

    public FixtureBackedApiProfile(WebAppFixture fixture) => _fixture = fixture;

    public void Configure(CompositionBuilder builder) => builder.Register(
        () => new ApiClient(_fixture.Factory.CreateClient()));
}

// In a test (any framework, or no framework at all):
await using var fixture = new WebAppFixture();
await fixture.InitializeAsync();

var composer = Composer.Create(b => b.AddProfile(new FixtureBackedApiProfile(fixture)));
await using var client = composer.Create<ApiClient>();

client.Client.BaseAddress.Should().NotBeNull();
```

`await using` disposes the fixture and the client even when an assertion
throws. `FixtureBackedApiProfile` takes the fixture through its
constructor, so there's no `TestContext.Current` involved and any test
framework can drive it. The trade-off: this does **not** use
attribute-driven parameter composition — you call `composer.Create<T>()`
yourself instead of letting `[Compose<TProfile>]` compose the theory
parameters.

## See also

- [Reuse Configuration Through a Profile](reuse-configuration-through-a-profile.md)
- [Composition Profiles](../concepts/profiles.md)
- [`Compono.XunitV3` Package Guide](../packages/compono-xunitv3.md)
