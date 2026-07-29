namespace Compono.Tests;

public sealed class CompositionConfigurationExceptionTests
{
    [Fact]
    public void Errors_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<CompositionConfigurationError>
        {
            new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct]),
        };
        var exception = new CompositionConfigurationException(original);

        original.Add(new CompositionConfigurationError.DuplicateConfigurationOption("WithCollectionSize", [ConfigurationSource.Direct]));

        exception.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Constructor_WithNullErrors_Throws()
    {
        var act = () => new CompositionConfigurationException(errors: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyErrors_Throws()
    {
        var act = () => new CompositionConfigurationException([]);

        act.Should().Throw<ArgumentException>();
    }
}
