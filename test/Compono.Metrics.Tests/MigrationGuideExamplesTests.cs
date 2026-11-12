using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Compono.Metrics.Tests;

/// <summary>
/// The compiled, running source of the metrics examples in
/// docs/migrating-from-microsoft-extensions-diagnostics-testing.md (RESEARCH-0040 finding 7), plus the
/// ClearMeasurements() multi-pass pattern from docs/packages/compono-metrics.md ("Asserting on one
/// pass", finding 4). Keep the guide snippets in step with these tests.
/// </summary>
public sealed class MigrationGuideExamplesTests
{
    public sealed class OutboxProcessor
    {
        private readonly Counter<long> _published;
        private int _pending = 3;

        public OutboxProcessor(IMeterFactory meters) =>
            _published = meters.Create(new MeterOptions("Outbox")).CreateCounter<long>("outbox.published");

        public void RunOnce()
        {
            if (_pending == 0)
                return;

            _pending--;
            _published.Add(1, new KeyValuePair<string, object?>("queue", "orders"));
        }
    }

    [Fact]
    public void ComposedFactory_ReplacesMetricCollector()
    {
        // Before: using var meterFactory = <hand-written test IMeterFactory>;
        //         using var collector = new MetricCollector<long>(meterFactory, "Shop.Orders", "shop.orders.placed");
        var composer = Composer.Create(builder => builder.UseMetrics().Share<IMeterFactory>());
        var coordinator = composer.Create<OrderCoordinator>();
        using var meters = coordinator.Meters;

        coordinator.Service.PlaceOrder("eu", 2);

        // Before: Assert.Equal(1, collector.LastMeasurement!.Value);
        //         Assert.Equal("eu", collector.LastMeasurement.Tags["region"]);
        meters.Verify<long>("Shop.Orders", "shop.orders.placed").WithTag("region", "eu").WithValue(1).Once();

        // Before: collector.GetMeasurementSnapshot()
        var placed = meters.GetMeasurements<long>("Shop.Orders", "shop.orders.placed");
        placed.Should().ContainSingle().Which.Value.Should().Be(1);
    }

    [Fact]
    public async Task HostRecipe_LastIMeterFactoryRegistrationWins()
    {
        using var meters = new CapturingMeterFactory();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IMeterFactory>(meters);   // after the host default: last wins
        builder.Services.AddSingleton<OutboxProcessor>();
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        host.Services.GetRequiredService<OutboxProcessor>().RunOnce();
        await host.StopAsync(TestContext.Current.CancellationToken);

        meters.Verify<long>("outbox.published").WithTag("queue", "orders").Once();
    }

    [Fact]
    public void ClearMeasurements_AssertsOnlyThePassUnderTest()
    {
        // Before: collector.Clear(); before the step under test.
        using var meters = new CapturingMeterFactory();
        var sut = new OutboxProcessor(meters);

        sut.RunOnce();
        sut.RunOnce();
        var earlierPasses = meters.GetMeasurements<long>("outbox.published");

        meters.ClearMeasurements();
        sut.RunOnce();

        meters.Verify<long>("outbox.published").WithValue(1).Once();
        earlierPasses.Should().HaveCount(2, "a snapshot taken before ClearMeasurements() is unaffected by it");
    }
}
