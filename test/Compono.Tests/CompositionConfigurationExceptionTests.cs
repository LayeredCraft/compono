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
    public void Errors_IsNotAConcreteArray_SoACallerCannotCastBackAndMutateIt()
    {
        var exception = new CompositionConfigurationException(
        [
            new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct]),
        ]);

        (exception.Errors is CompositionConfigurationError[]).Should().BeFalse();
    }

    [Fact]
    public void Message_And_Errors_AreDerivedFromTheSameSnapshot()
    {
        var duplicate = new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct]);
        var exception = new CompositionConfigurationException([duplicate]);

        exception.Message.Should().Contain("WithSeed");
        exception.Errors.Should().ContainSingle().Which.Should().Be(duplicate);
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
