namespace Compono.XunitV3.SampleTests;

// Documentation-quality examples for [ComposeData] (ADR-0070), reached through the packaged
// Compono.XunitV3 -> Compono dependency chain exactly as an external consumer would. Each example is
// deliberately the smallest shape that shows the feature; the same three examples appear in
// Compono.XunitV3.Aot.SampleTests (with plain Assert, since that project opts out of global usings).

// Example 1 - per-row configuration: the two-type-parameter form reads a distinct TConfig value from
// element 0 of every row. The rest of the row is the method's own values, bound left-to-right, and any
// remaining parameters are composed from the row's config.
public sealed record PricingConfig(decimal DiscountRate);

public sealed class PriceCalculator
{
    private readonly decimal _discountRate;

    public PriceCalculator(PricingConfig config) => _discountRate = config.DiscountRate;

    public decimal ApplyDiscount(decimal price) => price * (1 - _discountRate);
}

public sealed class PricingProfile : ICompositionProfile
{
    private readonly PricingConfig _config;

    public PricingProfile(PricingConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new PriceCalculator(_config));
}

public sealed class ComposeDataPricingTests
{
    // Row layout for ComposeData<TProfile, TConfig>: [config, ...row values]. "Price" and "Expected"
    // fill the first two parameters from the row; PriceCalculator is composed from that row's config.
    public static readonly object?[][] PricingRows =
    [
        [new PricingConfig(0.10m), 100m, 90m],
        [new PricingConfig(0.25m), 80m, 60m],
    ];

    [Theory]
    [ComposeData<PricingProfile, PricingConfig>(nameof(PricingRows))]
    public void EachRowSuppliesItsOwnConfiguration(decimal price, decimal expected, PriceCalculator calculator) =>
        calculator.ApplyDiscount(price).Should().Be(expected);
}

// Example 2 - a shared dependency: a [Shared] parameter is resolved once per row and reused by every
// other composed parameter that depends on it, so collaborators within a row observe the same
// instance. Here the row supplies the shared value (element 1), and the composed CatalogService picks
// up that exact instance rather than a second copy.
public sealed class CatalogRepository
{
    public CatalogRepository(string name) => Name = name;

    public string Name { get; }
}

public sealed class CatalogService
{
    public CatalogService(CatalogRepository repository) => Repository = repository;

    public CatalogRepository Repository { get; }
}

public sealed class ComposeDataSharedTests
{
    public static readonly object?[][] SharedRows =
    [
        ["alpha", new CatalogRepository("alpha")],
        ["beta", new CatalogRepository("beta")],
    ];

    [Theory]
    [ComposeData(nameof(SharedRows))]
    public void ASharedRowValueIsReusedByTheComposedService(
        string label, [Shared] CatalogRepository repository, CatalogService service)
    {
        repository.Name.Should().Be(label);
        service.Repository.Should().BeSameAs(repository);
    }
}

// Example 3 - a fixture-backed row source: rows are read at execution time (after xUnit has initialized
// the class fixture), so the row member can read the fixture directly, and a profile composes from the
// same fixture. Both the row value and the composed value end up equal to the fixture's own.
public sealed class CatalogDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await Task.Yield();
        ConnectionString = "Server=localhost;Database=composedata-samples";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed record CatalogConnection(string ConnectionString);

public sealed class CatalogConnectionProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() =>
            new CatalogConnection(TestContext.Current.RequireFixture<CatalogDatabaseFixture>().ConnectionString));
}

public sealed class ComposeDataFixtureBackedTests : IClassFixture<CatalogDatabaseFixture>
{
    private readonly CatalogDatabaseFixture _fixture;

    public ComposeDataFixtureBackedTests(CatalogDatabaseFixture fixture) => _fixture = fixture;

    public static object?[][] FixtureBackedRows() =>
        [[TestContext.Current.RequireFixture<CatalogDatabaseFixture>().ConnectionString]];

    [Theory]
    [ComposeData<CatalogConnectionProfile>(nameof(FixtureBackedRows))]
    public void TheFixtureIsReadableFromBothTheRowMemberAndTheComposition(
        string rowConnectionString, CatalogConnection connection)
    {
        rowConnectionString.Should().Be(_fixture.ConnectionString);
        connection.ConnectionString.Should().Be(_fixture.ConnectionString);
        rowConnectionString.Should().Be(connection.ConnectionString);
    }
}
