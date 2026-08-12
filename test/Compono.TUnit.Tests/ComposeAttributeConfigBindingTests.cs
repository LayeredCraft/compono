using Compono.TUnit.Tests.Fixtures;

namespace Compono.TUnit.Tests;

// Compono.TUnit.AotSmokeTest carries the packaged-consumer, real-runner proof; these tests exercise
// ComposeAttribute{TProfile,TConfig}/ConfigProfileBinder directly via GetDataRowsAsync, the same
// fast, no-real-runner style ComposeAttributeBindingTests.cs uses. Mirrors
// Compono.XunitV3.Tests.ComposeAttributeConfigBindingTests exactly, adapted to TUnit's
// DataGeneratorMetadata-based GetDataRowsAsync entry point.
public sealed class ComposeAttributeConfigBindingTests
{
    [Test]
    public async Task GetDataRowsAsync_MixesInlineValuesWithAProfileAppliedComposer()
    {
        // ComposeAttribute<TProfile> inherits Phase 0's own inline-value constructor unchanged
        // (unlike ComposeAttribute<TProfile, TConfig>, whose constructor arguments bind to TConfig
        // instead) - this proves inline values still take precedence over composition even once a
        // profile is applied to the underlying Composer.
        var attribute = new ComposeAttribute<SampleTestMethods.TestProfile>(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data![0]).IsEqualTo(42);
        await Assert.That(data[1]).IsTypeOf<string>();
    }

    [Test]
    public async Task GetDataRowsAsync_AppendsTheConfiguredSeed_WhenAFixedProfileFailsBeforeARowExists()
    {
        // ComposeAttribute<TProfile>.ApplyProfile runs while the base class's Lazy<Composer> is
        // still being built - before ComposeRow ever calls Composer.CreateRow, so there's no
        // CompositionRow/row.Seed to read from yet when TProfile.Configure itself throws. Every
        // Compono.TUnit-owned pre-composition failure ends with "Seed: {value}" - this proves that
        // convention holds for the fixed-profile form too, not just ComposeAttribute<TProfile,
        // TConfig>'s own identical wrapping.
        var attribute = new ComposeAttribute<SampleTestMethods.ThrowingConfigureTestProfile> { Seed = 492173 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("custom profile configuration failed").And
            .WithMessageContaining("Seed: 492173");
    }

    [Test]
    public async Task GetDataRowsAsync_ConstructsProfileFromConfig_AndComposesEveryTestParameter()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data).IsEquivalentTo(new object?[] { "from-config" });
    }

    [Test]
    public async Task ConfigArguments_AreNeverBoundAsInlineValues()
    {
        // Profile configuration arguments and inline values are two entirely separate binding
        // targets - this attribute's base-class InlineValues must stay empty regardless of how many
        // profile configuration arguments are supplied, proving
        // WithNonNullableReferenceParameter's own parameter is composed via the profile's
        // registration in the test above, never bound directly from "from-config" the way an inline
        // value would be.
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");

        await Assert.That(attribute.InlineValues).IsEmpty();
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenConfigTypeHasNoPublicConstructor()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithNoPublicConstructor>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("exactly one public constructor").And
            .WithMessageContaining("has 0");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenConfigTypeHasMultiplePublicConstructors()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithMultiplePublicConstructors>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("exactly one public constructor").And
            .WithMessageContaining("has 2");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenProfileTypeHasNoConstructorAcceptingTheConfigType()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("must have exactly one public constructor accepting a single").And
            .WithMessageContaining("TestConfig").And
            .WithMessageContaining("has 0");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenTooFewProfileConfigurationArgumentsAreSupplied()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("requires 1 profile configuration argument(s)").And
            .WithMessageContaining("0 were supplied");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenTooManyProfileConfigurationArgumentsAreSupplied()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("one", "two");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("requires 1 profile configuration argument(s)").And
            .WithMessageContaining("2 were supplied");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenConfigTypeIsAbstract()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.AbstractConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        // AbstractConfig has exactly one public constructor - passes the "exactly one constructor"
        // count check on its own, so this proves the explicit IsAbstract guard, not the count check
        // (without it, this would throw MemberAccessException from ConstructorInfo.Invoke instead
        // of the documented CompositionException).
        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("abstract").And
            .WithMessageContaining("cannot be used as profile configuration");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenProfileTypeIsAbstract()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.AbstractProfile, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("abstract").And
            .WithMessageContaining("cannot be used as a profile");
    }

    [Test]
    public async Task GetDataRowsAsync_AppendsTheConfiguredSeed_WhenProfileConstructionFailsBeforeARowExists()
    {
        // Every Compono.TUnit-owned pre-composition failure ends with "Seed: {value}" - this failure
        // category is special because it's thrown from inside the base class's Lazy<Composer>
        // initialization, before ComposeRow ever calls Composer.CreateRow, so there is no
        // CompositionRow/row.Seed to read from yet. Using an explicitly configured Seed proves the
        // reported value is the one this attribute is actually configured with, not an unrelated
        // throwaway number.
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value") { Seed = 492173 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("Seed: 492173");
    }

    [Test]
    public async Task GetDataRowsAsync_AppendsAGeneratedSeed_WhenProfileConstructionFailsWithNoSeedConfigured()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        // No explicit seed configured, so only the convention (a trailing "Seed: <non-negative int>")
        // is checked, not a specific value.
        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("\nSeed: ");
    }

    [Test]
    public async Task GetDataRowsAsync_ReportsTheNegativeSeedDiagnostic_NotTheBinderFailure_WhenBothApply()
    {
        // Seed = -1 combined with an invalid profile/config shape must report the documented
        // negative-seed diagnostic, not the binder failure with "Seed: -1" embedded - the
        // negative-seed check has to run before any config/profile binding is even attempted.
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value") { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("non-negative seed").And
            .WithMessageContaining("-1");
    }

    [Test]
    public async Task GetDataRowsAsync_UnwrapsAndReportsTheOriginalException_WhenTheConfigConstructorThrows()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ThrowingTestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        // Proves ConstructorInfo.Invoke's TargetInvocationException wrapper was unwrapped - the
        // caller sees ThrowingTestConfig's own actionable message (and the seed convention still
        // applies), not a generic reflection failure.
        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("custom validation failed for 'value'").And
            .WithMessageContaining("Seed: ");
    }

    [Test]
    public async Task GetDataRowsAsync_UnwrapsAndReportsTheOriginalException_WhenTheProfileConstructorThrows()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ThrowingTestProfile, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("custom validation failed for 'value'").And
            .WithMessageContaining("Seed: ");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenAProfileConfigurationArgumentHasAnIncompatibleType()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("not assignable to");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_WhenANullProfileConfigurationArgumentTargetsANonNullableParameter()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("is null, but the parameter is not nullable");
    }

    [Test]
    public async Task GetDataRowsAsync_AcceptsANonNullValueTypeArgument_ForANullableValueTypeParameter()
    {
        // 42 boxes as System.Int32, not System.Nullable<System.Int32> (a CLR nullable-boxing rule) -
        // this proves ConfigProfileBinder unwraps Nullable<T> before the assignability check the
        // same way ComposeAttribute's own inline-value binding already does, rather than the check
        // wrongly rejecting a valid int argument for an int? config constructor parameter.
        var attribute = new ComposeAttribute<SampleTestMethods.NullableIntParameterizedTestProfile, SampleTestMethods.NullableIntTestConfig>(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableValueParameter))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data).IsEquivalentTo(new object?[] { 42 });
    }

    [Test]
    public async Task GetDataRowsAsync_AcceptsANullProfileConfigurationArgument_ForANullableParameter()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.NullableParameterizedTestProfile, SampleTestMethods.NullableTestConfig>((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data).IsEquivalentTo(new object?[] { "null" });
    }

    [Test]
    public async Task GetDataRowsAsync_ConstructsTheProfileExactlyOnce_AcrossRepeatedCalls()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        // ApplyProfile (and, inside it, ConfigProfileBinder's reflection) only ever runs the first
        // time the base class's Lazy<Composer> is evaluated - asserting the same Composer instance is
        // reused across repeated calls is what proves the config/profile construction ran exactly
        // once, not once per call.
        var composerBeforeFirstCall = attribute.GetComposer();

        await SingleRow(attribute, method);
        await SingleRow(attribute, method);

        await Assert.That(attribute.GetComposer()).IsSameReferenceAs(composerBeforeFirstCall);
    }

    private static async Task<object?[]?> SingleRow(ComposeAttribute attribute, System.Reflection.MethodInfo method)
    {
        var metadata = DataGeneratorMetadataTestFactory.Create(method);
        var factories = new List<Func<Task<object?[]?>>>();

        await foreach (var factory in attribute.GetDataRowsAsync(metadata))
            factories.Add(factory);

        var single = factories.Single();
        return await single();
    }
}
