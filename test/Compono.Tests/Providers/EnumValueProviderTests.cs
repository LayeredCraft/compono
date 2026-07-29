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

    [Fact]
    public void ComposeDifferentEnumTypes_BothReturnDefinedMembers()
    {
        // Regression coverage for the PR #11 review finding: EnumValueProvider caches
        // Enum.GetValues(type) per enum type (ConcurrentDictionary<Type, Array>) rather than
        // re-allocating on every resolution - composing two distinct enum types in the same process
        // must not have the second type's cache entry collide with or overwrite the first's.
        var dayOfWeekSeed = new CompositionSeed(3);
        var consoleColorSeed = new CompositionSeed(4);

        var dayOfWeek = Composer.CreateRootForTesting<DayOfWeek>(dayOfWeekSeed);
        var consoleColor = Composer.CreateRootForTesting<ConsoleColor>(consoleColorSeed);

        Enum.IsDefined(dayOfWeek).Should().BeTrue();
        Enum.IsDefined(consoleColor).Should().BeTrue();
    }
}
