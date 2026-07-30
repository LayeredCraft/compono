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

    [Fact]
    public void ProfileCycle_Chain_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<Type> { typeof(int), typeof(int) };
        var error = new CompositionConfigurationError.ProfileCycle(original);

        original.Add(typeof(int));

        error.Chain.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ProfileCycle_WithFewerThanTwoChainEntries_Throws(int chainCount)
    {
        var chain = Enumerable.Repeat(typeof(int), chainCount).ToArray();

        var act = () => new CompositionConfigurationError.ProfileCycle(chain);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProfileCycle_WithDifferentFirstAndLastEntries_Throws()
    {
        var act = () => new CompositionConfigurationError.ProfileCycle([typeof(int), typeof(string)]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProfileCycle_WithANullChainEntry_Throws()
    {
        var act = () => new CompositionConfigurationError.ProfileCycle([null!, null!]);

        act.Should().Throw<ArgumentException>();
    }
}
