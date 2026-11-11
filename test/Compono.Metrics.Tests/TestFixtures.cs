using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

/// <summary>Composition profile that enables <c>UseMetrics()</c>, mirroring a real consumer profile.</summary>
public sealed class MetricsProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseMetrics();
}

/// <summary>A small SUT that takes <see cref="IMeterFactory"/> and records two instruments.</summary>
public sealed class OrderService
{
    private readonly IMeterFactory _meters;
    private readonly Counter<long> _placed;
    private readonly Histogram<double> _duration;

    public OrderService(IMeterFactory meters)
    {
        _meters = meters;
        var meter = meters.Create(new MeterOptions("Shop.Orders"));
        _placed = meter.CreateCounter<long>("shop.orders.placed");
        _duration = meter.CreateHistogram<double>("shop.orders.duration");
    }

    public IMeterFactory Meters => _meters;

    public void PlaceOrder(string region, int quantity)
    {
        _placed.Add(1, new KeyValuePair<string, object?>("region", region));
        _duration.Record(quantity * 1.5, new KeyValuePair<string, object?>("region", region));
    }
}

/// <summary>A SUT with no <see cref="IMeterFactory"/> dependency, for the "doesn't break other types" case.</summary>
public sealed class PlainRecord
{
    public int Value { get; set; } = 1;
}

/// <summary>Two meter-factory consumers in one graph, to prove graph-wide sharing of <see cref="IMeterFactory"/>.</summary>
public sealed class OrderCoordinator
{
    public OrderCoordinator(IMeterFactory meters, OrderService service)
    {
        Meters = meters;
        Service = service;
    }

    public IMeterFactory Meters { get; }

    public OrderService Service { get; }
}
