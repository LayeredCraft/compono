namespace Compono.NUnit.SignatureFixtures;

/// <summary>
/// Deliberately-invalid <c>[Compose]</c> method shapes, reflected over by
/// <c>Compono.NUnit.Tests.BindingPlanTests</c> via <c>typeof(...).GetMethod(...)</c> - never run for
/// real. See this project's own top-of-file comment for why these live in a separate, non-test
/// assembly rather than alongside <c>Compono.NUnit.Tests</c>' own valid <c>SampleTestMethods</c>.
/// </summary>
public static class InvalidSignatureFixtures
{
    [Compose]
    [Compose<TestProfile>]
    public static void WithMultipleComposeAttributes(int value)
    {
    }

    [Compose]
    [Compose<ParameterizedTestProfile, TestConfig>("value")]
    public static void WithComposeAndTwoTypeParameterComposeAttributes(int value)
    {
    }

    public sealed class TestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "from-profile");
    }

    public sealed record TestConfig(string Value);

    public sealed class ParameterizedTestProfile : ICompositionProfile
    {
        public ParameterizedTestProfile(TestConfig config) => Config = config;

        public TestConfig Config { get; }

        public void Configure(CompositionBuilder builder) => builder.Register(() => Config.Value);
    }
}

/// <summary>
/// A valid (non-invalid-signature) method that lives in a different assembly than
/// <c>Compono.NUnit.Tests</c> - reflected over by <c>ModuleInitializationTests</c> to prove
/// <c>ComposeAttribute.BuildFrom</c> targets the <em>declaring</em> assembly's module (ADR-0059
/// Amendment 1), not the calling test's own module. Deliberately not <c>[Compose]</c>-attributed: it
/// needs no composed row of its own, and adding the ComposeAttribute here would make this
/// never-scanned assembly's method a real NUnit test for any host that did scan it.
/// </summary>
public static class CrossAssemblyComposeFixtures
{
    public static void CrossAssemblyComposed(int number, string text)
    {
    }
}
