namespace Compono.Tests.Providers;

public sealed class NullableValueProviderTests
{
    [Fact]
    public void ComposeNullableInt_ComposesTheUnderlyingType_NeverNull()
    {
        var seed = new CompositionSeed(1);

        var result = Composer.CreateRootForTesting<int?>(seed);

        result.Should().NotBeNull();
    }

    [Fact]
    public void ComposeNullableEnum_ComposesTheUnderlyingType_NeverNull()
    {
        // Also the regression guard for the PR #11 review's EnumValueProvider AOT-safety fix: boxing
        // via Enum.ToObject(type, underlyingValue) (not a boxed underlying-type value directly) is
        // required for this to work at all - a boxed int unboxes fine to a non-nullable enum type,
        // but throws InvalidCastException unboxing to Nullable<TEnum> specifically (confirmed while
        // fixing this).
        var seed = new CompositionSeed(2);

        var result = Composer.CreateRootForTesting<DayOfWeek?>(seed);

        result.Should().NotBeNull();
        Enum.IsDefined(result!.Value).Should().BeTrue();
    }

    [Fact]
    public void ComposeNullableInt_IsDeterministic_ForSameSeed()
    {
        var seed = new CompositionSeed(3);

        var first = Composer.CreateRootForTesting<int?>(seed);
        var second = Composer.CreateRootForTesting<int?>(seed);

        first.Should().Be(second);
    }
}
