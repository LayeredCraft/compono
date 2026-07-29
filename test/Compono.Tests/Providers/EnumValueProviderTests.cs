namespace Compono.Tests.Providers;

public sealed class EnumValueProviderTests
{
    [Fact]
    public void ComposeEnum_ReturnsDefinedMember()
    {
        var seed = new CompositionSeed(1);

        var result = Composer.CreateRootForTesting<DayOfWeek>(seed);

        Enum.IsDefined(result).Should().BeTrue();
    }

    [Fact]
    public void ComposeEnum_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(2);

        var first = Composer.CreateRootForTesting<DayOfWeek>(seed);
        var second = Composer.CreateRootForTesting<DayOfWeek>(seed);

        first.Should().Be(second);
    }
}
