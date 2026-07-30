namespace Compono.Tests;

public sealed class CompositionConfigurationExceptionTests
{
    [Fact]
    public void Errors_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<CompositionConfigurationError>
        {
            new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct, ConfigurationSource.Direct]),
        };
        var exception = new CompositionConfigurationException(original);

        original.Add(new CompositionConfigurationError.DuplicateConfigurationOption("WithCollectionSize", [ConfigurationSource.Direct, ConfigurationSource.Direct]));

        exception.Errors.Should().ContainSingle();
    }

    [Fact]
    public void Errors_IsNotAConcreteArray_SoACallerCannotCastBackAndMutateIt()
    {
        var exception = new CompositionConfigurationException(
        [
            new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct, ConfigurationSource.Direct]),
        ]);

        (exception.Errors is CompositionConfigurationError[]).Should().BeFalse();
    }

    [Fact]
    public void Message_And_Errors_AreDerivedFromTheSameSnapshot()
    {
        var duplicate = new CompositionConfigurationError.DuplicateConfigurationOption("WithSeed", [ConfigurationSource.Direct, ConfigurationSource.Direct]);
        var exception = new CompositionConfigurationException([duplicate]);

        exception.Message.Should().Contain("WithSeed");
        exception.Errors.Should().ContainSingle().Which.Should().Be(duplicate);
    }

    [Fact]
    public void Message_ForDuplicateRegistration_NamesTheRegisteredType()
    {
        var duplicate = new CompositionConfigurationError.DuplicateRegistration(typeof(int), [ConfigurationSource.Direct, ConfigurationSource.Direct]);
        var exception = new CompositionConfigurationException([duplicate]);

        exception.Message.Should().Contain("Int32");
    }

    [Fact]
    public void Message_ForProfileCycle_NamesTheFullChain()
    {
        var cycle = new CompositionConfigurationError.ProfileCycle([typeof(int), typeof(string), typeof(int)]);
        var exception = new CompositionConfigurationException([cycle]);

        exception.Message.Should().Contain("Int32 -> String -> Int32");
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
