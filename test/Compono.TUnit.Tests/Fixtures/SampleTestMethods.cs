using Compono.TUnit;

namespace Compono.TUnit.Tests.Fixtures;

// Hand-written test-method shapes for BindingPlan.Build tests - never actually run as TUnit tests
// themselves, just reflected over via typeof(...).GetMethod(...) and converted to MethodMetadata by
// MethodMetadataTestFactory. Mirrors Compono.XunitV3.Tests.Fixtures.SampleTestMethods, trimmed to
// the shapes BindingPlan.Build actually validates for Compono.TUnit (no multi-Compose-family-
// attribute check here - BindingPlan.cs documents that as a deliberate v1 scope reduction).
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

    // A ref struct (e.g. Span<int>) can never legally be a generic type argument to
    // CompositionRow.Resolve<T>()/etc. at all (ADR-0041's dispatch-eligibility guard, runtime side).
    public static void WithRefStructParameter(Span<int> value)
    {
    }

    public static void Generic<T>(T value)
    {
    }
}
