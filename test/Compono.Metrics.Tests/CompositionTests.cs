using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class CompositionTests
{
    [Fact]
    public void UseMetrics_ComposesIMeterFactory_AsCapturingMeterFactory()
    {
        var composer = Composer.Create(builder => builder.UseMetrics());

        var service = composer.Create<OrderService>();

        service.Meters.Should().BeOfType<CapturingMeterFactory>();
    }

    [Fact]
    public void UseMetrics_DoesNotBreakComposition_ForTypesWithNoMeterDependency()
    {
        var composer = Composer.Create(builder => builder.UseMetrics());

        var value = composer.Create<PlainRecord>();

        value.Should().NotBeNull();
    }

    [Fact]
    public void EachComposedRequest_GetsItsOwnFactory_AbsentShared()
    {
        var composer = Composer.Create(builder => builder.UseMetrics());

        var first = composer.Create<OrderService>();
        var second = composer.Create<OrderService>();

        second.Meters.Should().NotBeSameAs(first.Meters);
    }

    [Fact]
    public void NestedConsumers_GetIndependentFactories_WithinOneRoot_AbsentShared()
    {
        var composer = Composer.Create(builder => builder.UseMetrics());

        var coordinator = composer.Create<OrderCoordinator>();

        coordinator.Meters.Should().NotBeSameAs(coordinator.Service.Meters);
    }

    [Fact]
    public void Share_MakesMeterFactoryGraphWide_WithinOneRoot()
    {
        var composer = Composer.Create(builder => builder.UseMetrics().Share<IMeterFactory>());

        var coordinator = composer.Create<OrderCoordinator>();

        coordinator.Meters.Should().BeSameAs(coordinator.Service.Meters);
    }

    [Fact]
    public void ExactRegistration_ForIMeterFactory_TakesPrecedenceOverUseMetrics()
    {
        using var registered = new CapturingMeterFactory();
        var composer = Composer.Create(builder =>
        {
            builder.Register<IMeterFactory>(() => registered);
            builder.UseMetrics();
        });

        var service = composer.Create<OrderService>();

        service.Meters.Should().BeSameAs(registered);
    }

    [Fact]
    public void UseMetricsBeforeUseNSubstitute_IMeterFactory_ResolvesToCapturingMeterFactory()
    {
        var composer = Composer.Create(builder => builder.UseMetrics().UseNSubstitute());

        var service = composer.Create<OrderService>();

        service.Meters.Should().BeOfType<CapturingMeterFactory>();
    }

    [Fact]
    public void UseNSubstituteBeforeUseMetrics_IMeterFactory_ResolvesToSubstitute()
    {
        // First-registered-wins (ADR-0024). Reversing order is the documented consequence, not a
        // diagnostic - the misuse surface (MeterFactoryTestingExtensions) is what surfaces the cause.
        var composer = Composer.Create(builder => builder.UseNSubstitute().UseMetrics());

        var meters = composer.Create<IMeterFactory>();

        (meters is CapturingMeterFactory).Should().BeFalse();
    }
}
