using Compono.XunitV3.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

// Compono.XunitV3.SampleTests carries the packaged-consumer, real-runner proof (per ADR-0022's own
// packaged-consumer precedent); these tests exercise ComposeAttribute{TProfile,TConfig}/
// ConfigProfileBinder directly via GetData, the same fast, no-real-runner style every other file in
// this project uses.
public sealed class ComposeAttributeConfigBindingTests
{
    [Fact]
    public async Task GetData_ConstructsProfileFromConfig_AndComposesEveryTestParameter()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();

        data.Should().Equal("from-config");
    }

    [Fact]
    public void ConfigArguments_AreNeverBoundAsInlineValues()
    {
        // Profile configuration arguments and inline values are two entirely separate binding
        // targets (ADR-0036's terminology split) - this attribute's base-class InlineValues must
        // stay empty regardless of how many profile configuration arguments are supplied, proving
        // WithNonNullableReferenceParameter's own parameter is composed via the profile's
        // registration in the test above, never bound directly from "from-config" the way an inline
        // value would be.
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");

        attribute.InlineValues.Should().BeEmpty();
    }

    [Fact]
    public async Task GetData_Throws_WhenConfigTypeHasNoPublicConstructor()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithNoPublicConstructor>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*exactly one public constructor*has 0*");
    }

    [Fact]
    public async Task GetData_Throws_WhenConfigTypeHasMultiplePublicConstructors()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithMultiplePublicConstructors>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*exactly one public constructor*has 2*");
    }

    [Fact]
    public async Task GetData_Throws_WhenProfileTypeHasNoConstructorAcceptingTheConfigType()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*must have exactly one public constructor accepting a single*TestConfig*parameter*has 0*");
    }

    [Fact]
    public async Task GetData_Throws_WhenTooFewProfileConfigurationArgumentsAreSupplied()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*requires 1 profile configuration argument(s)*0 were supplied*");
    }

    [Fact]
    public async Task GetData_Throws_WhenTooManyProfileConfigurationArgumentsAreSupplied()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("one", "two");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*requires 1 profile configuration argument(s)*2 were supplied*");
    }

    [Fact]
    public async Task GetData_Throws_WhenConfigTypeIsAbstract()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.AbstractConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        // AbstractConfig has exactly one public constructor - passes the "exactly one constructor"
        // count check on its own, so this proves the explicit IsAbstract guard, not the count check
        // (PR #65 review: without it, this would throw MemberAccessException from
        // ConstructorInfo.Invoke instead of the documented CompositionException).
        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*abstract*cannot be used as profile configuration*");
    }

    [Fact]
    public async Task GetData_Throws_WhenProfileTypeIsAbstract()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.AbstractProfile, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*abstract*cannot be used as a profile*");
    }

    [Fact]
    public async Task GetData_AppendsTheConfiguredSeed_WhenProfileConstructionFailsBeforeARowExists()
    {
        // Every Compono.XunitV3-owned pre-composition failure ends with "Seed: {value}" (ADR-0022) -
        // this failure category is special because it's thrown from inside the base class's
        // Lazy<Composer> initialization, before GetData ever calls Composer.CreateRow, so there is no
        // CompositionRow/row.Seed to read from yet (PR #65 review: this was previously missing
        // entirely for config/profile binder failures). Using an explicitly configured Seed proves the
        // reported value is the one this attribute is actually configured with, not an unrelated
        // throwaway number.
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value") { Seed = 492173 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*Seed: 492173*");
    }

    [Fact]
    public async Task GetData_AppendsAGeneratedSeed_WhenProfileConstructionFailsWithNoSeedConfigured()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        // No explicit seed configured, so only the convention (a trailing "Seed: <non-negative int>")
        // is checked, not a specific value.
        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*\nSeed: *");
    }

    [Fact]
    public async Task GetData_ReportsTheNegativeSeedDiagnostic_NotTheBinderFailure_WhenBothApply()
    {
        // Seed = -1 combined with an invalid profile/config shape must report the documented
        // negative-seed diagnostic, not the binder failure with "Seed: -1" embedded (PR #65 review) -
        // the negative-seed check has to run before any config/profile binding is even attempted.
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.TestConfig>("value") { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*non-negative seed*-1*");
    }

    [Fact]
    public async Task GetData_UnwrapsAndReportsTheOriginalException_WhenTheConfigConstructorThrows()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ThrowingTestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        // Proves ConstructorInfo.Invoke's TargetInvocationException wrapper was unwrapped - the
        // caller sees ThrowingTestConfig's own actionable message (and the seed convention still
        // applies), not a generic reflection failure.
        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*custom validation failed for 'value'*Seed: *");
    }

    [Fact]
    public async Task GetData_UnwrapsAndReportsTheOriginalException_WhenTheProfileConstructorThrows()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ThrowingTestProfile, SampleTestMethods.TestConfig>("value");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*custom validation failed for 'value'*Seed: *");
    }

    [Fact]
    public async Task GetData_Throws_WhenAProfileConfigurationArgumentHasAnIncompatibleType()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*not assignable to*");
    }

    [Fact]
    public async Task GetData_Throws_WhenANullProfileConfigurationArgumentTargetsANonNullableParameter()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*is null, but the parameter is not nullable*");
    }

    [Fact]
    public async Task GetData_AcceptsANonNullValueTypeArgument_ForANullableValueTypeParameter()
    {
        // 42 boxes as System.Int32, not System.Nullable<System.Int32> (a CLR nullable-boxing rule) -
        // this proves ConfigProfileBinder unwraps Nullable<T> before the assignability check the
        // same way ComposeAttribute's own inline-value binding already does, rather than the check
        // wrongly rejecting a valid int argument for an int? config constructor parameter.
        var attribute = new ComposeAttribute<SampleTestMethods.NullableIntParameterizedTestProfile, SampleTestMethods.NullableIntTestConfig>(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableValueParameter))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();

        data.Should().Equal(42);
    }

    [Fact]
    public async Task GetData_AcceptsANullProfileConfigurationArgument_ForANullableParameter()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.NullableParameterizedTestProfile, SampleTestMethods.NullableTestConfig>((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();

        data.Should().Equal("null");
    }

    [Fact]
    public async Task GetData_ConstructsTheProfileExactlyOnce_AcrossRepeatedGetDataCalls()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;
        var tracker = new DisposalTracker();

        // ApplyProfile (and, inside it, ConfigProfileBinder's reflection) only ever runs the first
        // time the base class's Lazy<Composer> is evaluated - asserting the same Composer instance is
        // reused across repeated GetData calls is what proves the config/profile construction ran
        // exactly once, not once per call, mirroring ComposeAttributeCachingTests' existing style for
        // ComposeAttribute<TProfile>.
        var composerBeforeFirstCall = attribute.GetComposer();

        await attribute.GetData(method, tracker);
        await attribute.GetData(method, tracker);

        attribute.GetComposer().Should().BeSameAs(composerBeforeFirstCall);
    }
}
