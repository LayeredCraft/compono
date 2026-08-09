namespace Compono.XunitV3.Tests.Fixtures;

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

    public static void WithNullableReferenceParameter(string? value)
    {
    }

    public static void WithNonNullableReferenceParameter(string value)
    {
    }

    public static void WithNullableValueParameter(int? value)
    {
    }

    public static void WithNonNullableValueParameter(int value)
    {
    }

    // Deliberately single-parameter - a second same-typed parameter would read this shared value
    // back from scope too (Phase 0's stage-2 read gate applies to every same-typed request in the
    // row, not just ones marked [Shared]) and re-validate it against that parameter's own
    // nullability, which isn't what these fixtures exist to test.
    public static void WithSharedNullableReferenceParameter([Shared] string? value)
    {
    }

    public static void WithSharedNonNullableReferenceParameter([Shared] string value)
    {
    }

    public static void WithSharedNullableValueParameter([Shared] int? value)
    {
    }

    public static void WithSharedNonNullableValueParameter([Shared] int value)
    {
    }

    // No provider and no generated plan can ever satisfy this interface (this test project doesn't
    // reference Compono.Generators as an analyzer, and nothing registers it) - composing it always
    // fails, deterministically, which is exactly what the seed-message-content proof needs.
    public static void WithUnregisteredInterfaceParameter(IUnregisteredDependency value)
    {
    }

    // Reaches CollectionExhaustionPlan (registered via CollectionPlanCache<HashSet<bool>>.Instance,
    // since this test project doesn't reference Compono.Generators as an analyzer) - a plain-message
    // CompositionException, no Diagnostic at all, exactly matching what the real generated
    // HashSet<T>/Dictionary collection plan throws on unique-value exhaustion
    // (CollectionPlan.scriban). Proves GetData appends the seed to this shape too, not only a
    // pipeline-diagnosed one (PR #26 review, third round).
    public static void WithExhaustedHashSetParameter(HashSet<bool> values)
    {
    }

    public static void WithDisposableParameter(DisposableValue disposable)
    {
    }

    public static void WithSharedDisposableFollowedByOrdinaryOfTheSameType([Shared] DisposableValue shared, DisposableValue ordinary)
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

    // Same reasoning as WithMultipleComposeAttributes above, pairing the two-type-parameter form
    // with the plain one instead of the one-type-parameter form - proves BindingPlan's stacking
    // detection (and its message) covers ComposeAttribute<TProfile, TConfig> too, not just the
    // original two forms (PR #65 review).
    [Compose]
    [Compose<ParameterizedTestProfile, TestConfig>("value")]
    public static void WithComposeAndTwoTypeParameterComposeAttributes(int value)
    {
    }
#pragma warning restore xUnit1008

    public sealed class TestProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register(() => "from-profile");
    }

    // ComposeAttribute{TProfile,TConfig} fixtures - a config record with exactly one public
    // constructor (the supported shape), a profile with exactly one public constructor accepting
    // exactly that config type, and one broken variant per ConfigProfileBinder failure mode.

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
    // it, retargeted at a config type's constructor instead of a test method's parameters (PR #65
    // review: this exact case had no regression coverage).
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
    // abstract-type rejection, not the "exactly one constructor" count check (PR #65 review: without
    // the explicit IsAbstract check, this shape would pass the count check and then throw
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
    // TargetInvocationException-unwrapping, config-construction case (PR #65 review:
    // ConstructorInfo.Invoke wraps a constructor-thrown exception in TargetInvocationException;
    // without unwrapping, ApplyProfile's own catch (CompositionException) never observes this).
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

    // Mirrors CollectionPlan.scriban's own HashSet<T> shape exactly (same UniqueValueResolver call,
    // same plain-message CompositionException on exhaustion) rather than just throwing directly,
    // since this test project doesn't reference Compono.Generators as an analyzer and can't get the
    // real generated plan (testing.md's hand-fake convention). Registered by the test itself (not a
    // static constructor here - typeof(...)/GetMethod(...) don't trigger a type's static constructor,
    // only an actual member access does), matching Compono.Tests' CollectionPlanCacheDispatchTests
    // register/try-finally-unregister pattern.
    public sealed class CollectionExhaustionPlan : ICompositionPlan<HashSet<bool>>
    {
        public HashSet<bool> Compose(ICompositionContext context)
        {
            var size = context.ResolveCollectionSize();
            var result = new HashSet<bool>(size);

            for (var i = 0; i < size; i++)
            {
                if (!UniqueValueResolver.TryResolve<bool>(context, CompositionRequestKind.CollectionElement, i, Nullability.NotNullable, result, out _))
                    throw new CompositionException($"Could not generate {size} unique values of type 'bool' for 'HashSet<bool>' after {UniqueValueResolver.MaxAttempts} attempts per element - the element type's value space is likely too small for the requested collection size.");
            }

            return result;
        }
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
    public bool Disposed => DisposeCount > 0;

    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;
}

internal interface IUnregisteredDependency;
