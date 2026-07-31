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

    public static void WithDisposableParameter(DisposableValue disposable)
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

    // xUnit1008 (data attribute without [Theory]) is expected and suppressed here - this method is
    // never run as a real theory, only reflected over via typeof(...).GetMethod(...) for
    // BindingPlan.Build's multiple-Compose-family-attribute signature check, same as every other
    // method in this fixture class per the type-level comment above.
#pragma warning disable xUnit1008
    [Compose]
    [Compose<TestProfile>]
    public static void WithMultipleComposeAttributes(int value)
    {
    }
#pragma warning restore xUnit1008

    public sealed class TestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "from-profile");
    }

    // Composed via a registration, not a generated plan - this test project doesn't reference
    // Compono.Generators as an analyzer (testing.md), so a registration is the only way to get a
    // real (non-fake) composed value for a custom type here.
    public sealed class DisposableProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => new DisposableValue());
    }
}

public sealed class DisposableValue : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}
