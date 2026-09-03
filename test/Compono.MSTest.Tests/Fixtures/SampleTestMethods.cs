namespace Compono.MSTest.Tests.Fixtures;

// Hand-written test-method shapes for BindingPlan.Build/ComposeAttribute binding tests - never
// actually run as real MSTest data-driven tests themselves, just reflected over via
// typeof(...).GetMethod(...), matching Compono.XunitV3.Tests'/Compono.TUnit.Tests' identical
// fixture convention.
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

    // A by-value (not ref/out/in) ref struct parameter - Span<int> can never legally be a generic
    // type argument to CompositionRow.Resolve<T>()/etc. at all (ADR-0041's dispatch-eligibility
    // guard, runtime side).
    public static void WithRefStructParameter(Span<int> value)
    {
    }

    public static void Generic<T>(T value)
    {
    }

    [Compose]
    [Compose<TestProfile>]
    public static void WithMultipleComposeAttributes(int value)
    {
    }

    [Compose]
    [Compose<ParameterizedTestProfile, TestConfig>("value")]
    public static void WithComposeAndTwoTypeParameterComposeAttributes(int value)
    {
    }

    // Real [Compose]-attributed fixture methods, actually reflected AND actually invoked via
    // GetData in ComposeAttributeBindingTests - drives real generator discovery for their own
    // parameter types (Compono.Generators referenced as an analyzer, see the .csproj).
    [Compose]
    public static void ComposesTwoStrings(string first, string second)
    {
    }

    [Compose]
    public static void ComposesSharedString([Shared] string shared, string other)
    {
    }

    [Compose]
    public static void ComposesOneNullableString(string? value)
    {
    }

    [Compose<TestProfile>]
    public static void ComposesWithProfile(string value)
    {
    }

    [Compose<ParameterizedTestProfile, TestConfig>("from-config")]
    public static void ComposesWithParameterizedProfile(string value)
    {
    }

    public sealed class TestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "from-profile");
    }

    // ComposeAttribute{TProfile}'s own ApplyProfile failure case - a fixed, default-constructed
    // profile whose Configure itself throws, proving that failure is wrapped with the "Seed: {value}"
    // convention the same way ComposeAttribute{TProfile,TConfig}'s identical ApplyProfile failure
    // already is. Mirrors Compono.TUnit.Tests.Fixtures.SampleTestMethods' identical fixture
    // (PLAN-0061 Phase 1).
    public sealed class ThrowingConfigureTestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => throw new CompositionException("custom profile configuration failed");
    }

    public sealed record TestConfig(string Value);

    public sealed class ParameterizedTestProfile : ICompositionProfile
    {
        public ParameterizedTestProfile(TestConfig config) => Config = config;

        public TestConfig Config { get; }

        public void Configure(CompositionBuilder builder) => builder.Register(() => Config.Value);
    }

    // Zero public constructors - ConfigProfileBinder.BindConfig's "exactly one" check, zero case.
    public sealed class ConfigWithNoPublicConstructor
    {
        private ConfigWithNoPublicConstructor()
        {
        }
    }

    // No constructor accepting exactly one TestConfig parameter - ConfigProfileBinder.BuildProfile's
    // "exactly one matching constructor" check, zero-match case.
    public sealed class ProfileWithoutMatchingConstructor : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder)
        {
        }
    }
}

internal interface IUnregisteredDependency;
