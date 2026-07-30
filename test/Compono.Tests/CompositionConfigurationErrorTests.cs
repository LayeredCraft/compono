namespace Compono.Tests;

public sealed class CompositionConfigurationErrorTests
{
    [Fact]
    public void DuplicateConfigurationOption_Sources_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<ConfigurationSource> { ConfigurationSource.Direct };
        var error = new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", original);

        original.Add(ConfigurationSource.Direct);

        error.Sources.Should().ContainSingle();
    }
}
