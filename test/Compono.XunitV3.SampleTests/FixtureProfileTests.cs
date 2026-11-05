namespace Compono.XunitV3.SampleTests;

// Documentation-quality example: read a running xUnit fixture's runtime value from inside a profile's
// registration factory via TestContext.Current.RequireFixture<TFixture>() (ADR-0069). Reached through
// the packaged Compono.XunitV3 -> Compono dependency chain, exactly as an external consumer would.

// A DB-style class fixture: xUnit creates it once per test class and runs InitializeAsync before any
// test method executes, so its initialized value is already available when the profile runs.
public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        ConnectionString = "Server=localhost;Database=fixture-samples";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed record DatabaseSettings(string ConnectionString);

// The fixture only exists while a test class is executing, so read it inside a Register factory - not
// in the profile's constructor and not at Configure's top level. RequireFixture never waits: xUnit has
// already initialized the fixture by the time the test runs.
public sealed class DatabaseSettingsProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() =>
            new DatabaseSettings(TestContext.Current.RequireFixture<DatabaseFixture>().ConnectionString));
}

public sealed class FixtureClassProfileTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public FixtureClassProfileTests(DatabaseFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<DatabaseSettingsProfile>]
    public void ComposeReadsTheClassFixturesRuntimeValue(DatabaseSettings settings)
    {
        settings.ConnectionString.Should().Be(_fixture.ConnectionString);
        settings.ConnectionString.Should().Be("Server=localhost;Database=fixture-samples");
    }
}

// Collection fixture example: a single fixture instance is shared across every test class in the
// collection. It is declared on the collection definition and reached from a profile the same way.
public sealed class FixtureCollectionFixture
{
    public string Token => "collection-fixture-token";
}

public sealed record CollectionToken(string Value);

[CollectionDefinition("FixtureSamples")]
public sealed class FixtureSamplesCollection : ICollectionFixture<FixtureCollectionFixture>;

public sealed class CollectionTokenProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() =>
            new CollectionToken(TestContext.Current.RequireFixture<FixtureCollectionFixture>().Token));
}

[Collection("FixtureSamples")]
public sealed class FixtureCollectionProfileTests
{
    private readonly FixtureCollectionFixture _fixture;

    public FixtureCollectionProfileTests(FixtureCollectionFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<CollectionTokenProfile>]
    public void ComposeReadsTheCollectionFixturesValue(CollectionToken token)
    {
        token.Value.Should().Be(_fixture.Token);
        token.Value.Should().Be("collection-fixture-token");
    }
}
