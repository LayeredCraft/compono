namespace Compono.Tests.Providers;

public sealed class PrimitiveValueProviderTests
{
    [Fact]
    public void ComposeString_ReturnsNonEmptyString()
    {
        var seed = new CompositionSeed(1);

        var result = Composer.CreateRootForTesting<string>(seed);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ComposeBool_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(2);

        var first = Composer.CreateRootForTesting<bool>(seed);
        var second = Composer.CreateRootForTesting<bool>(seed);

        first.Should().Be(second);
    }

    [Fact]
    public void ComposeInt_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(3);

        var first = Composer.CreateRootForTesting<int>(seed);
        var second = Composer.CreateRootForTesting<int>(seed);

        first.Should().Be(second);
    }

    [Fact]
    public void ComposeGuid_ReturnsNonEmptyGuid()
    {
        var seed = new CompositionSeed(4);

        var result = Composer.CreateRootForTesting<Guid>(seed);

        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ComposeDecimal_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(5);

        var first = Composer.CreateRootForTesting<decimal>(seed);
        var second = Composer.CreateRootForTesting<decimal>(seed);

        first.Should().Be(second);
    }

    [Fact]
    public void ComposeTimeSpan_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(6);

        var first = Composer.CreateRootForTesting<TimeSpan>(seed);
        var second = Composer.CreateRootForTesting<TimeSpan>(seed);

        first.Should().Be(second);
    }

    [Fact]
    public void ComposeDateOnly_IsWithinValidRange()
    {
        var seed = new CompositionSeed(7);

        var result = Composer.CreateRootForTesting<DateOnly>(seed);

        result.Should().BeOnOrAfter(DateOnly.MinValue).And.BeOnOrBefore(DateOnly.MaxValue);
    }

    [Fact]
    public void ComposeTimeOnly_IsWithinValidRange()
    {
        var seed = new CompositionSeed(8);

        var result = Composer.CreateRootForTesting<TimeOnly>(seed);

        result.Should().BeOnOrAfter(TimeOnly.MinValue).And.BeOnOrBefore(TimeOnly.MaxValue);
    }

    [Fact]
    public void ComposeChar_IsPrintableAscii()
    {
        var seed = new CompositionSeed(10);

        var result = Composer.CreateRootForTesting<char>(seed);

        ((int)result).Should().BeInRange(32, 126);
    }

    [Fact]
    public void ComposeNInt_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(11);

        var first = Composer.CreateRootForTesting<nint>(seed);
        var second = Composer.CreateRootForTesting<nint>(seed);

        first.Should().Be(second);
    }

    [Fact]
    public void ComposeNUInt_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(12);

        var first = Composer.CreateRootForTesting<nuint>(seed);
        var second = Composer.CreateRootForTesting<nuint>(seed);

        first.Should().Be(second);
    }

    [Fact]
    public void ComposeUnregisteredType_Throws()
    {
        var seed = new CompositionSeed(9);

        var act = () => Composer.CreateRootForTesting<UnhandledPrimitiveLike>(seed);

        act.Should().Throw<CompositionException>();
    }

    private readonly struct UnhandledPrimitiveLike
    {
    }
}
