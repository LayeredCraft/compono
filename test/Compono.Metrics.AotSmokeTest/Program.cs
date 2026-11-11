using System.Diagnostics.Metrics;
using Compono;
using Compono.Metrics;

namespace Compono.Metrics.AotSmokeTest;

internal sealed class OrderService
{
    private readonly IMeterFactory _meters;
    private readonly Counter<long> _placed;
    private readonly Histogram<double> _duration;

    public OrderService(IMeterFactory meters)
    {
        _meters = meters;
        var meter = meters.Create("Shop.Orders");
        _placed = meter.CreateCounter<long>("shop.orders.placed");
        _duration = meter.CreateHistogram<double>("shop.orders.duration");
    }

    public IMeterFactory Meters => _meters;

    public void PlaceOrder(string region, int quantity)
    {
        var tag = new KeyValuePair<string, object?>("region", region);
        _placed.Add(1, tag);
        _duration.Record(quantity * 1.5, tag);
    }
}

internal static class Program
{
    private static int Main()
    {
        try
        {
            // The generated composition path (ADR-0072 relies on the same generator-discovered
            // activation other packages use), not direct construction alone - OrderService's own
            // IMeterFactory constructor dependency is discovered by the shared Compono.Generators
            // walk and satisfied by the stage-6 MetricsProvider that UseMetrics() registers.
            var composer = Composer.Create(builder => builder.UseMetrics());
            var service = composer.Create<OrderService>();
            var meters = service.Meters;

            if (meters is not CapturingMeterFactory)
                throw new InvalidOperationException(
                    $"Expected UseMetrics() to compose a CapturingMeterFactory, got '{meters.GetType()}'.");

            using var _ = meters;

            service.PlaceOrder("eu", 3);

            // Counter: exact value plus tag matching.
            meters.Verify<long>("shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();

            // Histogram: one recording, value math stays in LINQ.
            var durations = meters.GetMeasurements<double>("Shop.Orders", "shop.orders.duration");
            if (durations.Count != 1 || durations[0].Value != 4.5)
                throw new InvalidOperationException(
                    $"Expected one histogram recording of 4.5, got {durations.Count}.");

            VerifyEveryInstrumentKindAndObservables(meters);
            VerifyTagSnapshot(meters);
            VerifyTypeConflictDiagnostic(meters);
            VerifyDirectConstructionParity();

            Console.WriteLine(
                "PASS: UseMetrics()-composed IMeterFactory (generator-discovered activation), counter/" +
                "histogram/up-down-counter/gauge capture, explicit observable polling, tag matching and " +
                "snapshot copies, the type-conflict diagnostic, Verify(), and direct CapturingMeterFactory " +
                "construction all survived Native AOT through the packaged Compono.Metrics dependency chain.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    private static void VerifyEveryInstrumentKindAndObservables(IMeterFactory meters)
    {
        // A fresh factory so this factory's published-instrument set is exactly what this method makes.
        using var local = new CapturingMeterFactory();
        var meter = local.Create("Kinds");

        var counter = meter.CreateCounter<int>("kind.counter");
        counter.Add(5);

        var upDown = meter.CreateUpDownCounter<int>("kind.updown");
        upDown.Add(-2);

        var histogram = meter.CreateHistogram<double>("kind.histogram");
        histogram.Record(1.25);

        var gauge = meter.CreateGauge<double>("kind.gauge");
        gauge.Record(21.5);

        meter.CreateObservableGauge<int>("kind.observable", () => 7);

        local.Verify<int>("kind.counter").WithValue(5).Once();
        local.Verify<int>("kind.updown").WithValue(-2).Once();
        local.Verify<double>("kind.histogram").WithValue(1.25).Once();
        local.Verify<double>("kind.gauge").WithValue(21.5).Once();

        // Observable is captured only after an explicit poll; reads never poll.
        local.Verify<int>("kind.observable").Never();
        local.RecordObservableInstruments();
        local.Verify<int>("kind.observable").WithValue(7).Once();
    }

    private static void VerifyTagSnapshot(IMeterFactory meters)
    {
        using var local = new CapturingMeterFactory();
        var meter = local.Create("Tags");
        var counter = meter.CreateCounter<long>("tags.counter");

        var source = new KeyValuePair<string, object?>[]
        {
            new("region", "eu"),
            new("tier", "gold"),
        };
        counter.Add(1, source);

        // Mutate the caller's array after recording: the captured snapshot must be an independent copy.
        source[0] = new KeyValuePair<string, object?>("region", "us");

        var measurement = local.GetMeasurements<long>("tags.counter")[0];
        local.Verify<long>("tags.counter").WithTag("region", "eu").WithTag("tier", "gold").Once();
    }

    private static void VerifyTypeConflictDiagnostic(IMeterFactory meters)
    {
        using var local = new CapturingMeterFactory();
        var meter = local.Create("Conflict");
        meter.CreateCounter<int>("conflict.counter");
        meter.CreateCounter<long>("conflict.counter");

        try
        {
            local.GetMeasurements<long>("conflict.counter");
            throw new InvalidOperationException("Expected a type-conflict InvalidOperationException.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("different types"))
        {
            // Expected: one selection can't merge measurements of different value types.
        }
    }

    private static void VerifyDirectConstructionParity()
    {
        // Direct construction, no composition - CapturingMeterFactory is publicly constructible.
        using var direct = new CapturingMeterFactory();
        var meter = direct.Create("Direct");
        var counter = meter.CreateCounter<int>("direct.counter");
        counter.Add(1);
        direct.Verify<int>("direct.counter").WithValue(1).Once();

        // Reads stay valid after disposal.
        var snapshot = direct.GetMeasurements<int>("direct.counter");
        direct.Dispose();
        if (snapshot.Count != 1 || snapshot[0].Value != 1 || snapshot[0].Instrument.Name != "direct.counter")
            throw new InvalidOperationException("Expected a snapshot to stay readable after disposal.");
    }
}
