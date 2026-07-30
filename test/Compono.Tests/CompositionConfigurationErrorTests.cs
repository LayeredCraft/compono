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

    [Fact]
    public void DuplicateConfigurationOption_Sources_IsNotAConcreteArray_SoACallerCannotCastBackAndMutateIt()
    {
        var error = new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct]);

        (error.Sources is ConfigurationSource[]).Should().BeFalse();
    }
}
