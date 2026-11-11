using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class InstrumentCaptureTests
{
    [Fact]
    public void Counter_Byte_IsCaptured() => AssertCounter<byte>(1);

    [Fact]
    public void Counter_Short_IsCaptured() => AssertCounter<short>(2);

    [Fact]
    public void Counter_Int_IsCaptured() => AssertCounter<int>(3);

    [Fact]
    public void Counter_Long_IsCaptured() => AssertCounter<long>(4);

    [Fact]
    public void Counter_Float_IsCaptured() => AssertCounter<float>(5.5f);

    [Fact]
    public void Counter_Double_IsCaptured() => AssertCounter<double>(6.5);

    [Fact]
    public void Counter_Decimal_IsCaptured() => AssertCounter<decimal>(7.5m);

    [Fact]
    public void UpDownCounter_IsCaptured()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateUpDownCounter<long>("shop.queue.depth");

        counter.Add(-2);

        var measurements = factory.GetMeasurements<long>("shop.queue.depth");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(-2);
    }

    [Fact]
    public void Histogram_IsCaptured()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var histogram = meter.CreateHistogram<double>("shop.orders.duration");

        histogram.Record(1.5);
        histogram.Record(2.5);

        var measurements = factory.GetMeasurements<double>("shop.orders.duration");
        measurements.Should().HaveCount(2);
        measurements[0].Value.Should().Be(1.5);
        measurements[1].Value.Should().Be(2.5);
    }

#if NET9_0_OR_GREATER
    [Fact]
    public void Gauge_IsCaptured()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var gauge = meter.CreateGauge<double>("shop.queue.gauge");

        gauge.Record(9.5);

        var measurements = factory.GetMeasurements<double>("shop.queue.gauge");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(9.5);
    }
#endif

    [Fact]
    public void Capture_UsesMeterAndInstrumentNames_FromTheComposedFactoryOnly()
    {
        using var factory = new CapturingMeterFactory();
        using var other = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var otherMeter = other.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<int>("orders");
        var otherCounter = otherMeter.CreateCounter<int>("orders");

        counter.Add(1);
        otherCounter.Add(99);

        var measurements = factory.GetMeasurements<int>("orders");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
    }

    private static void AssertCounter<T>(T value)
        where T : struct
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<T>("measured");

        counter.Add(value);

        var measurements = factory.GetMeasurements<T>("measured");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(value);
    }
}
