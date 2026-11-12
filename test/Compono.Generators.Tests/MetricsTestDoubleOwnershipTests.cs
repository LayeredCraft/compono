namespace Compono.Generators.Tests;

/// <summary>
/// ADR-0072 Amendment 5: when <c>ComponoMetricsOwnsMeterFactory</c> is enabled (Compono.Metrics' packed
/// default), <c>System.Diagnostics.Metrics.IMeterFactory</c> is Metrics-owned and excluded from
/// <c>Compono.TestDoubles</c> generation, so <c>UseGeneratedTestDoubles()</c> can never take it ahead of
/// <c>UseMetrics()</c> (the #161 follow-up trap, RESEARCH-0040 finding 1). Mirrors
/// <see cref="LoggingTestDoubleOwnershipTests"/>.
/// </summary>
public sealed class MetricsTestDoubleOwnershipTests
{
    private const string MeterFactoryRegistration =
        "RegisterFactory<global::System.Diagnostics.Metrics.IMeterFactory>";

    private const string SiblingRegistration =
        "RegisterFactory<global::TestNamespace.IJobClock>";

    private const string Source = """
        namespace TestNamespace;

        public interface IJobClock
        {
            System.DateTimeOffset Now();
        }

        public sealed class MeteredJob
        {
            public MeteredJob(System.Diagnostics.Metrics.IMeterFactory meters, IJobClock clock) { }
        }

        public static class EntryPoint
        {
            public static void Run()
            {
                var composer = Compono.Composer.Create();
                var job = composer.Create<TestNamespace.MeteredJob>();
            }
        }
        """;

    private static string Generate(bool testDoublesEnabled, string? metricsOwnsMeterFactory)
    {
        var properties = new Dictionary<string, string>
        {
            ["ComponoGeneratedTestDoubles"] = testDoublesEnabled ? "true" : "false",
        };

        if (metricsOwnsMeterFactory is not null)
            properties["ComponoMetricsOwnsMeterFactory"] = metricsOwnsMeterFactory;

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions
            {
                SourceCode = Source,
                MSBuildProperties = properties,
            },
            TestContext.Current.CancellationToken);

        return string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.GetText().ToString()));
    }

    [Fact]
    public void FlagUnset_TestDoublesEnabled_IMeterFactoryStillGetsADouble_PreExistingBehaviorPreserved()
    {
        var generated = Generate(testDoublesEnabled: true, metricsOwnsMeterFactory: null);

        generated.Should().Contain(MeterFactoryRegistration);
        generated.Should().Contain(SiblingRegistration);
    }

    [Fact]
    public void FlagFalse_TestDoublesEnabled_IMeterFactoryGetsADouble_OptOutHonored()
    {
        var generated = Generate(testDoublesEnabled: true, metricsOwnsMeterFactory: "false");

        generated.Should().Contain(MeterFactoryRegistration);
    }

    [Fact]
    public void FlagTrue_TestDoublesEnabled_MetricsOwnsIMeterFactory_NoGeneratedDouble()
    {
        var generated = Generate(testDoublesEnabled: true, metricsOwnsMeterFactory: "true");

        generated.Should().NotContain(MeterFactoryRegistration);
        generated.Should().NotContain("IMeterFactory_global__");
    }

    [Fact]
    public void FlagTrue_ExclusionIsNarrow_SiblingInterfaceStillGetsADouble()
    {
        var generated = Generate(testDoublesEnabled: true, metricsOwnsMeterFactory: "true");

        generated.Should().Contain(SiblingRegistration);
    }

    [Fact]
    public void FlagTrue_TestDoublesDisabled_NothingGenerated()
    {
        var generated = Generate(testDoublesEnabled: false, metricsOwnsMeterFactory: "true");

        generated.Should().NotContain(MeterFactoryRegistration);
        generated.Should().NotContain(SiblingRegistration);
    }

    [Fact]
    public void FlagUnparseable_TreatedAsDisabled()
    {
        var generated = Generate(testDoublesEnabled: true, metricsOwnsMeterFactory: "yes-please");

        generated.Should().Contain(MeterFactoryRegistration);
    }
}
