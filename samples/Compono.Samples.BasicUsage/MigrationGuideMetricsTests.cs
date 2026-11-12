using System.Diagnostics.Metrics;
using Compono.Metrics;
using Compono.XunitV3;

// A dedicated namespace so the guide's own type names (MetricsProfile, OrderService) compile verbatim
// without colliding with this sample's Domain.cs.
namespace Compono.Samples.BasicUsage.Migration;

// The compiled, running source of the composed-factory example in
// docs/migrating-from-microsoft-extensions-diagnostics-testing.md ("Composed factory
// (MetricCollector<T> -> UseMetrics())"). Keep the guide snippet and this test in step.
public sealed class MetricsProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseMetrics();
}

public sealed class OrderService
{
    private readonly Counter<long> _placed;

    public OrderService(IMeterFactory meters) =>
        _placed = meters.Create(new MeterOptions("Shop.Orders")).CreateCounter<long>("shop.orders.placed");

    public void PlaceOrder(string region, int quantity)
    {
        _ = quantity;
        _placed.Add(1, new KeyValuePair<string, object?>("region", region));
    }
}

public sealed class MigrationGuideMetricsTests
{
    [Theory, Compose<MetricsProfile>]
    public void PlacingAnOrderCountsIt([Shared] IMeterFactory meters, OrderService sut)
    {
        sut.PlaceOrder("eu", 2);

        meters.Verify<long>("Shop.Orders", "shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();
        var placed = meters.GetMeasurements<long>("Shop.Orders", "shop.orders.placed");   // GetMeasurementSnapshot()
        Assert.Single(placed);
    }
}
