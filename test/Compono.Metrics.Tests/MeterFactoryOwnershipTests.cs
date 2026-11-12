using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

/// <summary>
/// ADR-0072 Amendment 5: with <c>ComponoMetricsOwnsMeterFactory</c> enabled (Compono.Metrics' packed
/// default, set explicitly in this project), <see cref="IMeterFactory"/> is Metrics-owned and
/// <c>Compono.TestDoubles</c> never generates a double for it. This project enables
/// <c>ComponoGeneratedTestDoubles</c>, so <see cref="IClockSource"/> (a sibling interface reached from
/// the same root) does get a real generated double - proving the exclusion is narrow. Reproduces the
/// #161 follow-up trap: a shared base profile registering <c>UseGeneratedTestDoubles()</c> before
/// <c>UseMetrics()</c>.
/// </summary>
public sealed class MeterFactoryOwnershipTests
{
    public interface IClockSource
    {
        DateTimeOffset Now();
    }

    public sealed class MeteredJob(IMeterFactory meters, IClockSource clock)
    {
        public IMeterFactory Meters { get; } = meters;

        public IClockSource Clock { get; } = clock;

        public void Run() =>
            Meters.Create(new MeterOptions("Jobs")).CreateCounter<long>("jobs.runs").Add(1);
    }

    [Fact]
    public void UseGeneratedTestDoublesBeforeUseMetrics_IMeterFactory_ResolvesToCapturingMeterFactory()
    {
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles().UseMetrics());

        var job = composer.Create<MeteredJob>();
        using var _ = job.Meters;
        job.Run();

        job.Meters.Should().BeOfType<CapturingMeterFactory>();
        job.Meters.Verify<long>("jobs.runs").Once();
    }

    [Fact]
    public void UseMetricsBeforeUseGeneratedTestDoubles_IMeterFactory_ResolvesToCapturingMeterFactory()
    {
        var composer = Composer.Create(builder => builder.UseMetrics().UseGeneratedTestDoubles());

        var job = composer.Create<MeteredJob>();
        using var _ = job.Meters;

        job.Meters.Should().BeOfType<CapturingMeterFactory>();
    }

    [Fact]
    public void ExclusionIsNarrow_SiblingInterface_StillGetsAGeneratedDouble()
    {
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles().UseMetrics());

        var job = composer.Create<MeteredJob>();
        using var meters = job.Meters;

        job.Clock.Should().NotBeNull();
        job.Clock.Should().NotBeAssignableTo<IMeterFactory>();
        GeneratedTestDoubleRegistry.TryCreate(typeof(IClockSource), out var clockDouble).Should().BeTrue();
        clockDouble.Should().BeAssignableTo<IClockSource>();
    }

    [Fact]
    public void NoGeneratedDouble_IsRegisteredForIMeterFactory()
    {
        GeneratedTestDoubleRegistry.TryCreate(typeof(IMeterFactory), out var value).Should().BeFalse();

        value.Should().BeNull();
    }

    [Fact]
    public void ReferencedWithoutUseMetrics_UseGeneratedTestDoublesAlone_FailsToComposeIMeterFactory()
    {
        // The accepted tradeoff (ADR-0072 Amendment 5): referencing Compono.Metrics makes IMeterFactory
        // Metrics-owned, so forgetting UseMetrics() fails loudly instead of yielding a silently
        // uncapturing generated double.
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

        var act = () => composer.Create<MeteredJob>();

        act.Should().Throw<CompositionException>()
            .WithMessage("*IMeterFactory*");
    }

    [Fact]
    public void ReferencedWithoutUseMetrics_FallsThroughToALaterCatchAllProvider()
    {
        var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles().UseNSubstitute());

        var job = composer.Create<MeteredJob>();

        (job.Meters is CapturingMeterFactory).Should().BeFalse();
        job.Meters.Should().NotBeNull();
    }
}
