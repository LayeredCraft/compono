using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class MisuseTests
{
    private static IMeterFactory ForeignFactory() => Substitute.For<IMeterFactory>();

    [Fact]
    public void GetMeasurements_ThrowsOnForeignFactory_NamingUseMetrics()
    {
        var act = () => ForeignFactory().GetMeasurements<int>("orders");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseMetrics()*");
    }

    [Fact]
    public void Verify_ThrowsOnForeignFactory()
    {
        var act = () => ForeignFactory().Verify<int>("orders");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseMetrics()*");
    }

    [Fact]
    public void RecordObservableInstruments_ThrowsOnForeignFactory()
    {
        var act = () => ForeignFactory().RecordObservableInstruments();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ClearMeasurements_ThrowsOnForeignFactory()
    {
        var act = () => ForeignFactory().ClearMeasurements();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Extensions_ThrowArgumentNull_WhenFactoryIsNull()
    {
        IMeterFactory? factory = null;

        var act = () => factory!.ClearMeasurements();

        act.Should().Throw<ArgumentNullException>();
    }
}
