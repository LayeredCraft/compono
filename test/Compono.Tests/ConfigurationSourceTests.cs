namespace Compono.Tests;

public sealed class ConfigurationSourceTests
{
    [Fact]
    public void ProfileChain_Profiles_IsUnaffectedByMutatingTheOriginalListAfterConstruction()
    {
        var original = new List<Type> { typeof(ConfigurationSourceTests) };
        var source = new ConfigurationSource.ProfileChain(original);

        original.Add(typeof(string));

        source.Profiles.Should().ContainSingle();
    }

    [Fact]
    public void ProfileChain_Profiles_IsNotAConcreteArray_SoACallerCannotCastBackAndMutateIt()
    {
        var source = new ConfigurationSource.ProfileChain([typeof(ConfigurationSourceTests)]);

        (source.Profiles is Type[]).Should().BeFalse();
    }
}
