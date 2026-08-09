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
