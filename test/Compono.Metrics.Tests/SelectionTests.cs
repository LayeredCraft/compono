using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class SelectionTests
{
    [Fact]
    public void NullInstrumentName_ThrowsArgumentException()
    {
        using var factory = new CapturingMeterFactory();

        var act = () => factory.GetMeasurements<int>(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmptyInstrumentName_ThrowsArgumentException()
    {
        using var factory = new CapturingMeterFactory();

        var act = () => factory.GetMeasurements<int>(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnsupportedValueType_ThrowsArgumentException_NamingTheSevenTypes()
    {
        using var factory = new CapturingMeterFactory();

        var act = () => factory.GetMeasurements<DateTime>("anything");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*byte, short, int, long, float, double, decimal*");
    }

    [Fact]
    public void Verify_UnsupportedValueType_ThrowsArgumentException_AtBuilderCreation()
    {
        using var factory = new CapturingMeterFactory();

        var act = () => factory.Verify<DateTime>("anything");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnknownInstrumentName_YieldsEmptyResult()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<int>("orders");

        factory.GetMeasurements<int>("no.such.instrument").Should().BeEmpty();
    }

    [Fact]
    public void UnknownInstrumentName_NeverPasses_AndFailingOnce_ListsExistingInstruments()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<int>("orders");

        factory.Verify<int>("typo").Never();

        var act = () => factory.Verify<int>("typo").Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage(
                "Expected exactly 1 call(s) to instrument \"typo\" (int), but received 0. "
                + "No instrument named \"typo\" has been published by this factory. "
                + "Existing instruments: meter \"Shop\": instrument \"orders\" (int).");
    }

    [Fact]
    public void UnknownInstrumentNameInNamedMeter_ReportsTheMeterAndExistingInstruments()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<int>("orders");

        var act = () => factory.Verify<int>("Shop", "typo").Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage(
                "Expected exactly 1 call(s) to instrument \"typo\" in meter \"Shop\" (int), but received 0. "
                + "No instrument named \"typo\" has been published by meter \"Shop\". "
                + "Existing instruments: meter \"Shop\": instrument \"orders\" (int).");
    }

    [Fact]
    public void AmbiguousMeterNames_OnOneArgumentOverload_ThrowsInvalidOperationException()
    {
        using var factory = new CapturingMeterFactory();
        var first = factory.Create(new MeterOptions("A"));
        var second = factory.Create(new MeterOptions("B"));
        first.CreateCounter<int>("shared");
        second.CreateCounter<int>("shared");

        var act = () => factory.GetMeasurements<int>("shared");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*two-argument*");
    }

    [Fact]
    public void TwoArgumentOverload_ResolvesAmbiguityByMeterName()
    {
        using var factory = new CapturingMeterFactory();
        var first = factory.Create(new MeterOptions("A"));
        var second = factory.Create(new MeterOptions("B"));
        first.CreateCounter<int>("shared").Add(1);
        second.CreateCounter<int>("shared").Add(2);

        var measurements = factory.GetMeasurements<int>("A", "shared");
        measurements.Should().ContainSingle();
        measurements[0].Value.Should().Be(1);
    }

    [Fact]
    public void ConflictingValueTypes_ThrowInvalidOperationException_EvenWhenOneMatches()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<int>("x");
        meter.CreateCounter<long>("x");

        var act = () => factory.GetMeasurements<int>("x");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different types*");
    }

    [Fact]
    public void ValueTypeMismatch_ThrowsInvalidOperationException_NamingTheRecordedType()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<int>("x");

        var act = () => factory.GetMeasurements<long>("Shop", "x");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*records int, not long*");
    }

    [Fact]
    public void SameNamedMeters_WithDifferentVersions_MergeWhenTypesAgree()
    {
        using var factory = new CapturingMeterFactory();
        var v1 = factory.Create(new MeterOptions("Shop") { Version = "1.0" });
        var v2 = factory.Create(new MeterOptions("Shop") { Version = "2.0" });
        v1.CreateCounter<int>("x").Add(1);
        v2.CreateCounter<int>("x").Add(2);

        var measurements = factory.GetMeasurements<int>("Shop", "x");
        measurements.Should().HaveCount(2);
        measurements.Select(m => m.Value).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public void TypeConflict_ThrowsAtBuilderCreation_BeforeAnyFilterIsAdded()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        meter.CreateCounter<int>("x");
        meter.CreateCounter<long>("x");

        var act = () => factory.Verify<int>("Shop", "x");

        act.Should().Throw<InvalidOperationException>();
    }
}
