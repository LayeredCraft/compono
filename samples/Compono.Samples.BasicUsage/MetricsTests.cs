using System.Diagnostics.Metrics;
using Compono.Metrics;
using Compono.XunitV3;

namespace Compono.Samples.BasicUsage;

// Compono.Metrics: UseMetrics() composes IMeterFactory as a CapturingMeterFactory - no substitute
// needed to assert the measurements CheckoutService actually recorded. [Shared] reuses the exact
// same composed factory the service's own constructor received, so the test asserts against the
// measurements the service produced. The test owns disposal via "using var _ = meters;".
public sealed class MetricsTests
{
    [Theory]
    [Compose<MetricsSampleProfile>]
    public void Place_RecordsAnOrderCountAndDurationForTheRegion(
        [Shared] IMeterFactory meters, CheckoutService service, CreateOrder order)
    {
        using var _ = meters;

        service.Place(order, "eu", durationMs: 12.5);

        meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();
        meters.Verify<double>("shop.orders.duration").WithTag("region", "eu").Once();

        var placed = meters.GetMeasurements<long>("shop.orders.placed");
        Assert.Equal(1, placed.Sum(measurement => measurement.Value));
    }
}
