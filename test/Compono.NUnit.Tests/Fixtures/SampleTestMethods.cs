namespace Compono.NUnit.Tests.Fixtures;

// Hand-written test-method shapes for BindingPlan.Build/ComposeAttribute binding tests - never
// actually run as real NUnit tests themselves, just reflected over via typeof(...).GetMethod(...),
// matching Compono.XunitV3.Tests'/Compono.TUnit.Tests'/Compono.MSTest.Tests' identical fixture
// convention.
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

    // The two deliberately-invalid "more than one Compose-family attribute" shapes live in the
    // separate Compono.NUnit.SignatureFixtures assembly, not here - unlike
    // Compono.XunitV3/Compono.TUnit/Compono.MSTest (where [Compose] alone never makes a method
    // independently discoverable - a separate [Fact]/[Test]/[TestMethod] is required),
    // Compono.NUnit's ComposeAttribute derives from NUnit's own TestAttribute, so ANY method
    // carrying it - public or internal, NUnit's own discovery considers both - becomes a real,
    // independently-discovered NUnit test. A deliberately-invalid fixture method living in this
    // project's own assembly would actually run (and fail for real) under this project's real NUnit
    // test host, so it has to live in an assembly the NUnit adapter never scans as a test container
    // instead. See Compono.NUnit.SignatureFixtures.InvalidSignatureFixtures and
    // BindingPlanTests' own two signature-error tests.

    // Real [Compose]-attributed fixture methods, actually reflected AND actually invoked via
    // BuildFrom in ComposeAttributeBindingTests - drives real generator discovery for their own
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
