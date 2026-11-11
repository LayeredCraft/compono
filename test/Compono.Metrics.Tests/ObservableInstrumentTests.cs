using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class ObservableInstrumentTests
{
    [Fact]
    public void RecordObservableInstruments_PollsObservableCounter()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateObservableCounter<int>("shop.queue.depth", () => 7);

        factory.GetMeasurements<int>("shop.queue.depth").Should().BeEmpty();

        factory.RecordObservableInstruments();

        var measurements = factory.GetMeasurements<int>("shop.queue.depth");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(7);
    }

    [Fact]
    public void RecordObservableInstruments_PollsObservableGauge()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateObservableGauge<double>("shop.queue.gauge", () => 3.25);

        factory.RecordObservableInstruments();

        var measurements = factory.GetMeasurements<double>("shop.queue.gauge");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(3.25);
    }

    [Fact]
    public void RecordObservableInstruments_PollsObservableUpDownCounter()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateObservableUpDownCounter<long>("shop.queue.pending", () => 11L);

        factory.RecordObservableInstruments();

        var measurements = factory.GetMeasurements<long>("shop.queue.pending");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(11);
    }

    [Fact]
    public void ReadsDoNotPoll_AndHaveNoSideEffects()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateObservableCounter<int>("shop.queue.depth", () => 7);

        factory.GetMeasurements<int>("shop.queue.depth").Should().BeEmpty();
        factory.GetMeasurements<int>("shop.queue.depth").Should().BeEmpty();
    }

    [Fact]
    public void RecordObservableInstruments_TwiceRecordsTwice()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateObservableCounter<int>("shop.queue.depth", () => 7);

        factory.RecordObservableInstruments();
        factory.RecordObservableInstruments();

        factory.GetMeasurements<int>("shop.queue.depth").Should().HaveCount(2);
    }

    [Fact]
    public void RecordObservableInstruments_BeforeFirstCreate_IsNoOp()
    {
        using var factory = new CapturingMeterFactory();

        var act = () => factory.RecordObservableInstruments();

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordObservableInstruments_AfterDispose_IsNoOp()
    {
        var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateObservableCounter<int>("shop.queue.depth", () => 7);
        factory.Dispose();

        var act = () => factory.RecordObservableInstruments();

        act.Should().NotThrow();
        factory.GetMeasurements<int>("shop.queue.depth").Should().BeEmpty();
    }

    [Fact]
    public void PollingOneFactory_DoesNotPollAnother()
    {
        using var first = new CapturingMeterFactory();
        using var second = new CapturingMeterFactory();
        var firstMeter = first.Create(new MeterOptions("Shop"));
        var secondMeter = second.Create(new MeterOptions("Shop"));
        firstMeter.CreateObservableCounter<int>("depth", () => 1);
        secondMeter.CreateObservableCounter<int>("depth", () => 2);

        first.RecordObservableInstruments();

        var firstMeasurements = first.GetMeasurements<int>("depth");
        firstMeasurements.Should().ContainSingle();
        firstMeasurements[0].Value.Should().Be(1);
        second.GetMeasurements<int>("depth").Should().BeEmpty();
    }

    [Fact]
    public void RecordObservableInstruments_ThrowingCallback_PropagatesAggregateException_AndRecordsOthers()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        Func<int> boom = () => throw new InvalidOperationException("boom");
        meter.CreateObservableGauge("boom", boom);
        meter.CreateObservableCounter("depth", () => 5);

        var act = () => factory.RecordObservableInstruments();

        act.Should().Throw<AggregateException>()
            .WithInnerException<InvalidOperationException>();
        factory.GetMeasurements<int>("depth").Should().ContainSingle()
            .Which.Value.Should().Be(5);
    }
}
