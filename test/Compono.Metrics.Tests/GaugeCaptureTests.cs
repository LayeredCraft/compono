using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

/// <summary>
/// Regression coverage for the <see cref="Gauge{T}"/> capture classification. <see cref="Gauge{T}"/>
/// derives from <see cref="Instrument{T}"/>, so it is captured on every target framework whose
/// runtime provides it - including a net8.0 consumer running against a newer
/// System.Diagnostics.DiagnosticSource (the net8.0 test build references 9.x for exactly this
/// reason). This file is deliberately not guarded by <c>#if NET9_0_OR_GREATER</c>: the whole point
/// is that capture is decided by the instrument's base type, not by the compile-time target.
/// </summary>
public sealed class GaugeCaptureTests
{
    [Fact]
    public void GaugeDouble_IsCaptured_OnEveryTargetFramework()
    {
        using var meters = new CapturingMeterFactory();
        using var meter = meters.Create(new MeterOptions("Shop.Queue"));
        var gauge = meter.CreateGauge<double>("shop.queue.length");

        gauge.Record(3.5);

        var captured = meters.GetMeasurements<double>("shop.queue.length");

        captured.Should().ContainSingle();
        captured[0].Value.Should().Be(3.5);
        captured[0].Instrument.Should().BeSameAs(gauge);
    }

    [Fact]
    public void GaugeDouble_IsVerifiable_OnEveryTargetFramework()
    {
        using var meters = new CapturingMeterFactory();
        using var meter = meters.Create(new MeterOptions("Shop.Queue"));
        var gauge = meter.CreateGauge<double>("shop.queue.length");

        gauge.Record(7.25, new KeyValuePair<string, object?>("region", "eu"));

        meters.Verify<double>("shop.queue.length").WithValue(7.25).WithTag("region", "eu").Once();
    }

    [Fact]
    public void GaugeDouble_ComposesAndCaptures_OnEveryTargetFramework()
    {
        var composer = Composer.Create(builder => builder.UseMetrics());
        var meters = (CapturingMeterFactory)composer.Create<IMeterFactory>();
        using var _ = meters;

        using var meter = meters.Create(new MeterOptions("Shop.Queue"));
        var gauge = meter.CreateGauge<double>("shop.queue.length");
        gauge.Record(11.0);

        meters.GetMeasurements<double>("shop.queue.length").Should().ContainSingle()
            .Which.Instrument.Should().BeSameAs(gauge);
    }
}
