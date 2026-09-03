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
