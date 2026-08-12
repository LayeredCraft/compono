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

    public static void WithNonNullableReferenceParameter(string value)
    {
    }

    public static void WithNonNullableValueParameter(int value)
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

    // TUnit's own analyzer for a data-source attribute without [Test] is expected and suppressed
    // here - these methods are never run as real tests, only reflected over via
    // typeof(...).GetMethod(...) for BindingPlan.Build's multiple-Compose-family-attribute
    // signature check, same as every other method in this fixture class per the type-level comment
    // above. Mirrors Compono.XunitV3.Tests.Fixtures.SampleTestMethods' identical stacked-attribute
    // fixtures.
    [Compose]
    [Compose<TestProfile>]
    public static void WithMultipleComposeAttributes(int value)
    {
    }

    // A zero-parameter method has no ParameterMetadata to read ReflectionInfo.Member from -
    // BindingPlan's own stacked-attribute lookup falls back to a direct Type.GetMethod(name,
    // Type.EmptyTypes) call for exactly this shape; this fixture exercises that fallback path.
    [Compose]
    [Compose<TestProfile>]
    public static void WithMultipleComposeAttributesAndNoParameters()
    {
    }

    // Two zero-parameter overloads sharing a name, distinguished only by generic arity -
    // BindingPlan's own zero-parameter method-resolution fallback matches by parameter *types* only
    // (Type.EmptyTypes), which doesn't distinguish these; without also filtering by generic arity,
    // Type.GetMethod(name, Type.EmptyTypes) throws AmbiguousMatchException for this exact shape,
    // crashing before the generic-method signature check even runs (Codex review).
    public static void AmbiguousZeroParameterMethod()
    {
    }

    public static void AmbiguousZeroParameterMethod<T>()
    {
    }

    // Same reasoning as WithMultipleComposeAttributes above, pairing the two-type-parameter form
    // with the plain one instead of the one-type-parameter form - proves BindingPlan's stacking
    // detection (and its message) covers ComposeAttribute<TProfile, TConfig> too, not just the
    // original two forms.
    [Compose]
    [Compose<ParameterizedTestProfile, TestConfig>("value")]
    public static void WithComposeAndTwoTypeParameterComposeAttributes(int value)
    {
    }

    public sealed class TestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "from-profile");
    }

    // ComposeAttribute{TProfile}'s own ApplyProfile failure case - a fixed, default-constructed
    // profile whose Configure itself throws, proving that failure is wrapped with the "Seed: {value}"
    // convention the same way ComposeAttribute{TProfile,TConfig}'s identical ApplyProfile failure
    // already was (Codex review).
    public sealed class ThrowingConfigureTestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => throw new CompositionException("custom profile configuration failed");
    }

    // ComposeAttribute{TProfile,TConfig} fixtures - a config record with exactly one public
    // constructor (the supported shape), a profile with exactly one public constructor accepting
    // exactly that config type, and one broken variant per ConfigProfileBinder failure mode.
    // Mirrors Compono.XunitV3.Tests.Fixtures.SampleTestMethods' identical set.

    public sealed record TestConfig(string Value);

    public sealed class ParameterizedTestProfile : ICompositionProfile
    {
        public ParameterizedTestProfile(TestConfig config) => Config = config;

        public TestConfig Config { get; }

        public void Configure(CompositionBuilder builder) => builder.Register(() => Config.Value);
    }

    public sealed record NullableTestConfig(string? Value);

    public sealed class NullableParameterizedTestProfile : ICompositionProfile
    {
        public NullableParameterizedTestProfile(NullableTestConfig config) => Config = config;

        public NullableTestConfig Config { get; }

        public void Configure(CompositionBuilder builder) => builder.Register(() => Config.Value ?? "null");
    }

    // A non-null value-typed profile configuration argument for a Nullable<T> constructor parameter
    // - proves ConfigProfileBinder's Nullable<T>-boxing unwrap (a non-null int? boxes as a boxed
    // int, not a boxed int?) the same way ComposeAttribute's own inline-value binding already covers
    // it, retargeted at a config type's constructor instead of a test method's parameters.
    public sealed record NullableIntTestConfig(int? Value);

    public sealed class NullableIntParameterizedTestProfile : ICompositionProfile
    {
        public NullableIntParameterizedTestProfile(NullableIntTestConfig config) => Config = config;

        public NullableIntTestConfig Config { get; }

        public void Configure(CompositionBuilder builder) => builder.Register(() => Config.Value ?? -1);
    }

    // Zero public constructors - ConfigProfileBinder.BindConfig's "exactly one" check, zero case.
    public sealed class ConfigWithNoPublicConstructor
    {
        private ConfigWithNoPublicConstructor()
        {
        }
    }

    // Two public constructors - ConfigProfileBinder.BindConfig's "exactly one" check, ambiguous case.
    public sealed class ConfigWithMultiplePublicConstructors
    {
        public ConfigWithMultiplePublicConstructors(string value) => Value = value;

        public ConfigWithMultiplePublicConstructors(string value, string extra)
        {
            Value = value;
            Extra = extra;
        }

        public string Value { get; }

        public string? Extra { get; }
    }

    // No constructor accepting exactly one TestConfig parameter - ConfigProfileBinder.BuildProfile's
    // "exactly one matching constructor" check, zero-match case.
    public sealed class ProfileWithoutMatchingConstructor : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder)
        {
        }
    }

    // Abstract with an otherwise-qualifying public constructor - ConfigProfileBinder.BindConfig's
    // abstract-type rejection, not the "exactly one constructor" count check (without the explicit
    // IsAbstract check, this shape would pass the count check and then throw
    // MemberAccessException from ConstructorInfo.Invoke instead of the documented CompositionException).
    public abstract class AbstractConfig
    {
        // Public, not protected - an abstract type's constructor accessibility is independent of
        // whether the type itself can be instantiated; C# and the CLR both allow a public constructor
        // on an abstract type (only a derived type can actually call it), which is exactly what makes
        // this shape reach ConstructorInfo.Invoke without the explicit IsAbstract guard.
        public AbstractConfig(string value) => Value = value;

        public string Value { get; }
    }

    // Abstract with an otherwise-qualifying public constructor accepting TestConfig -
    // ConfigProfileBinder.BuildProfile's abstract-type rejection, same reasoning as AbstractConfig
    // above.
    public abstract class AbstractProfile : ICompositionProfile
    {
        public AbstractProfile(TestConfig config) => Config = config;

        public TestConfig Config { get; }

        public void Configure(CompositionBuilder builder)
        {
        }
    }

    // A single public constructor that itself throws - ConfigProfileBinder.Invoke's
    // TargetInvocationException-unwrapping, config-construction case (ConstructorInfo.Invoke wraps
    // a constructor-thrown exception in TargetInvocationException; without unwrapping, ApplyProfile's
    // own catch (CompositionException) never observes this).
    public sealed class ThrowingTestConfig
    {
        public ThrowingTestConfig(string value) => throw new CompositionException($"custom validation failed for '{value}'");
    }

    // Same reasoning as ThrowingTestConfig above, but for the profile-construction call site instead
    // of the config-construction one.
    public sealed class ThrowingTestProfile : ICompositionProfile
    {
        public ThrowingTestProfile(TestConfig config) => throw new CompositionException($"custom validation failed for '{config.Value}'");

        public void Configure(CompositionBuilder builder)
        {
        }
    }
}
