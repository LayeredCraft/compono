namespace Compono.Xunit.Tests.Fixtures;

// Hand-written test-method shapes for BindingPlan.Build/ComposeAttribute caching tests - never
// actually run as xUnit theories themselves, just reflected over via typeof(...).GetMethod(...).
internal static class SampleTestMethods
{
    public static void Simple(int number, string text)
    {
    }

    public static void WithShared([Shared] string repository, string other)
    {
    }

    public static void WithDuplicateShared([Shared] string first, [Shared] string second)
    {
    }

    public static void WithNullableParameters(string? nullableReference, string notNullableReference, int? nullableValue, int notNullableValue)
    {
    }

    public static void WithRefParameter(ref int value)
    {
    }

    public static void WithOutParameter(out int value)
    {
        value = 0;
    }

    public static void WithInParameter(in int value)
    {
    }

    public static void WithParamsParameter(params int[] values)
    {
    }

    public static void Generic<T>(T value)
    {
    }

    public sealed class TestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "from-profile");
    }
}
