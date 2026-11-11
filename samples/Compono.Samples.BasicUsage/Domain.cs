using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Compono.Samples.BasicUsage;

// Plain application types - nothing here is Compono-specific. Compono composes ordinary
// constructors and record parameters; it doesn't require attributes, base classes, or interfaces
// on the types it builds. Mirrors docs/getting-started/first-test.md's own example so a reader who
// just finished Getting Started recognizes this sample immediately.
public sealed class Repository;

public sealed class OrderService
{
    public OrderService(Repository repository)
    {
        Repository = repository;
    }

    public Repository Repository { get; }
}

public sealed record CreateOrder(string CustomerName, int Quantity);

public sealed record Customer
{
    public required string Name { get; init; }

    public required string Email { get; init; }
}

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/>-dependent type - the natural fit for
/// <c>Compono.Logging</c>'s sample scenario, composed via <c>UseLogging()</c> so the entry it logs
/// can be asserted with <c>Verify()</c> instead of substituted away.
/// </summary>
public sealed class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Creates a new <see cref="NotificationService"/> logging through <paramref name="logger"/>.
    /// </summary>
    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs an informational entry describing <paramref name="order"/>'s customer and quantity.
    /// </summary>
    public void Notify(CreateOrder order) =>
        _logger.LogInformation(
            "Notified {CustomerName} about their order for {Quantity} units", order.CustomerName, order.Quantity);
}

/// <summary>
/// An <see cref="IMeterFactory"/>-dependent type - the natural fit for <c>Compono.Metrics</c>'s
/// sample scenario, composed via <c>UseMetrics()</c> so the measurements it records can be asserted
/// with <c>Verify&lt;T&gt;()</c> instead of substituted away.
/// </summary>
public sealed class CheckoutService
{
    private readonly Counter<long> _orders;
    private readonly Histogram<double> _duration;

    /// <summary>
    /// Creates the meters and instruments <see cref="CheckoutService"/> records through.
    /// </summary>
    public CheckoutService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Shop.Checkout");
        _orders = meter.CreateCounter<long>("shop.orders.placed");
        _duration = meter.CreateHistogram<double>("shop.orders.duration");
    }

    /// <summary>
    /// Counts the placed order and records how long it took, both tagged with the region.
    /// </summary>
    public void Place(CreateOrder order, string region, double durationMs)
    {
        var tag = new KeyValuePair<string, object?>("region", region);
        _orders.Add(1, tag);
        _duration.Record(durationMs, tag);
    }
}
