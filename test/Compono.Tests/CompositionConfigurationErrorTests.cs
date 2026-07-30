namespace Compono.Tests;

public sealed class CompositionConfigurationErrorTests
{
    [Fact]
    public void DuplicateConfigurationOption_Sources_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<ConfigurationSource> { ConfigurationSource.Direct, ConfigurationSource.Direct };
        var error = new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", original);

        original.Add(ConfigurationSource.Direct);

        error.Sources.Should().HaveCount(2);
    }

    [Fact]
    public void DuplicateConfigurationOption_Sources_IsNotAConcreteArray_SoACallerCannotCastBackAndMutateIt()
    {
        var error = new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct, ConfigurationSource.Direct]);

        (error.Sources is ConfigurationSource[]).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DuplicateConfigurationOption_WithFewerThanTwoSources_Throws(int sourceCount)
    {
        var sources = Enumerable.Repeat(ConfigurationSource.Direct, sourceCount).ToArray();

        var act = () => new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", sources);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DuplicateRegistration_Sources_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<ConfigurationSource> { ConfigurationSource.Direct, ConfigurationSource.Direct };
        var error = new CompositionConfigurationError.DuplicateRegistration(typeof(int), original);

        original.Add(ConfigurationSource.Direct);

        error.Sources.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DuplicateRegistration_WithFewerThanTwoSources_Throws(int sourceCount)
    {
        var sources = Enumerable.Repeat(ConfigurationSource.Direct, sourceCount).ToArray();

        var act = () => new CompositionConfigurationError.DuplicateRegistration(typeof(int), sources);

        act.Should().Throw<ArgumentException>();
    }
}
