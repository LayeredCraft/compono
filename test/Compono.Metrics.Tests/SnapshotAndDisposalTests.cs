using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class SnapshotAndDisposalTests
{
    [Fact]
    public void CapturedTags_AreACopy_UnaffectedByLaterMutationOfTheSourceArray()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        var tags = new[] { new KeyValuePair<string, object?>("region", "eu") };

        counter.Add(1, tags);
        tags[0] = new KeyValuePair<string, object?>("region", "us");

        var measurements = factory.GetMeasurements<long>("orders");
        measurements[0].Tags.Should().ContainSingle()
            .Which.Value.Should().Be("eu");
    }

    [Fact]
    public void MeasurementsWithoutTags_GetEmptyNonNullTagList()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1);

        var measurements = factory.GetMeasurements<long>("orders");
        measurements[0].Tags.Should().NotBeNull();
        measurements[0].Tags.Should().BeEmpty();
    }

    [Fact]
    public void GetMeasurements_ReturnsANewListEachCall()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1);

        var first = factory.GetMeasurements<long>("orders");
        var second = factory.GetMeasurements<long>("orders");

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void ClearMeasurements_EmptiesEveryInstrument()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<long>("orders").Add(1);
        meter.CreateHistogram<double>("duration").Record(1.0);

        factory.ClearMeasurements();

        factory.GetMeasurements<long>("orders").Should().BeEmpty();
        factory.GetMeasurements<double>("duration").Should().BeEmpty();
    }

    [Fact]
    public void CaptureOrder_IsPreservedForSequentialRecordings()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        counter.Add(1);
        counter.Add(2);
        counter.Add(3);

        factory.GetMeasurements<long>("orders").Select(m => m.Value)
            .Should().ContainInOrder(1L, 2L, 3L);
    }

    [Fact]
    public void CaptureOrder_ForMergedSelection_OrdersByCaptureSequence_NotByMeterRegistration()
    {
        using var factory = new CapturingMeterFactory();

        // "2.0" is created (and publishes its instrument) before "1.0", so a naive concatenation of the
        // two instruments' buffers would yield [1, 3, 2]. The merged selection must instead sort by the
        // global capture sequence - removing that sort fails this test.
        var v2 = factory.Create(new MeterOptions("Shop") { Version = "2.0" });
        var v1 = factory.Create(new MeterOptions("Shop") { Version = "1.0" });
        var v2Counter = v2.CreateCounter<int>("x");
        var v1Counter = v1.CreateCounter<int>("x");

        v2Counter.Add(1);
        v1Counter.Add(2);
        v2Counter.Add(3);

        factory.GetMeasurements<int>("Shop", "x").Select(m => m.Value)
            .Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Snapshot_StaysReadable_AfterDispose()
    {
        var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop") { Version = "1.0" });
        meter.CreateCounter<long>("orders").Add(5,
            new KeyValuePair<string, object?>("region", "eu"));

        var snapshot = factory.GetMeasurements<long>("orders");
        factory.Dispose();

        snapshot[0].Value.Should().Be(5);
        snapshot[0].Tags.Should().ContainSingle().Which.Value.Should().Be("eu");
        snapshot[0].Instrument.Name.Should().Be("orders");
        snapshot[0].Instrument.Meter.Name.Should().Be("Shop");
        snapshot[0].Instrument.Meter.Version.Should().Be("1.0");
    }

    [Fact]
    public void AfterDispose_RecordingIsNotCaptured_AndReadsStillWork()
    {
        var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        counter.Add(1);
        factory.Dispose();

        counter.Add(2);

        factory.GetMeasurements<long>("orders").Should().ContainSingle()
            .Which.Value.Should().Be(1L);
        var act = () => factory.ClearMeasurements();
        act.Should().NotThrow();
        factory.GetMeasurements<long>("orders").Should().BeEmpty();
    }

    [Fact]
    public void ComposedDisposalPattern_HoldsTheSameFactoryTheSutUses()
    {
        var composer = Composer.Create(builder => builder.UseMetrics().Share<IMeterFactory>());

        var service = composer.Create<OrderService>();
        using var _ = service.Meters;

        service.PlaceOrder("eu", 3);
        service.Meters.Verify<long>("Shop.Orders", "shop.orders.placed").WithValue(1).Once();
    }

    [Fact]
    public void DirectConstructThenRegister_Pattern_IsOwnedByTheTest()
    {
        using var meters = new CapturingMeterFactory();
        var composer = Composer.Create(builder => builder.Register<IMeterFactory>(() => meters));

        var service = composer.Create<OrderService>();
        service.PlaceOrder("eu", 3);

        service.Meters.Should().BeSameAs(meters);
        meters.Verify<double>("Shop.Orders", "shop.orders.duration").Once();
    }
}
