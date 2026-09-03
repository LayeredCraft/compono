using Compono.NUnit.Tests.Fixtures;
using NUnit.Framework;

namespace Compono.NUnit.Tests;

[TestFixture]
public sealed class ComposeAttributeConfigBindingTests
{
    [Test]
    public void BuildFrom_BuildsTProfileFromTConfig_ViaTheAttributesOwnConstructorArguments()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var data = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments;

        Assert.That(data[0], Is.EqualTo("from-config"));
    }

    [Test]
    public void BuildFrom_Throws_WhenTConfigHasNoPublicConstructor()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithNoPublicConstructor>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var exception = Assert.Throws<CompositionException>(() =>
            attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray());

        Assert.That(exception!.Message, Does.Contain("public constructor"));
    }

    [Test]
    public void BuildFrom_Throws_ForANegativeSeed_BeforeAttemptingConfigBinding()
    {
        // The negative-seed check runs before ApplyProfile does any config/profile binding work -
        // proven here by a deliberately-broken TConfig/TProfile pairing still reporting the
        // documented negative-seed diagnostic, not a binder failure.
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithNoPublicConstructor> { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var exception = Assert.Throws<CompositionException>(() =>
            attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray());

        Assert.That(exception!.Message, Does.Contain("non-negative"));
    }

    [Test]
    public void BuildFrom_AppendsTheConfiguredSeed_WhenAFixedProfileFailsBeforeARowExists_ForComposeAttributeTProfile()
    {
        // ComposeAttribute{TProfile}.ApplyProfile had no negative-seed guard or exception wrapping
        // at all until PLAN-0061 Phase 1 - a copy-paste gap from before this convention was
        // introduced for ComposeAttribute{TProfile, TConfig}. Mirrors that class's own precedence
        // test above, and Compono.TUnit.Tests' identical, already-correct coverage for this
        // one-generic-argument form.
        var attribute = new ComposeAttribute<SampleTestMethods.ThrowingConfigureTestProfile> { Seed = 492173 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var exception = Assert.Throws<CompositionException>(() =>
            attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray());

        Assert.That(exception!.Message, Does.Contain("custom profile configuration failed"));
        Assert.That(exception.Message, Does.Contain("Seed: 492173"));
    }

    [Test]
    public void BuildFrom_ReportsTheNegativeSeedDiagnostic_NotTheProfileFailure_ForComposeAttributeTProfile_WhenBothApply()
    {
        // Seed = -1 combined with a throwing TProfile.Configure must report the documented
        // negative-seed diagnostic, not the profile failure with "Seed: -1" embedded - before
        // PLAN-0061 Phase 1, this attribute had no guard at all, so CompositionBuilder.WithSeed's own
        // unchecked(int->ulong) cast would have silently accepted the negative seed with no exception
        // at all, rather than merely reporting the wrong one.
        var attribute = new ComposeAttribute<SampleTestMethods.ThrowingConfigureTestProfile> { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var exception = Assert.Throws<CompositionException>(() =>
            attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray());

        Assert.That(exception!.Message, Does.Contain("non-negative"));
    }

    [Test]
    public void BuildFrom_ComposesEveryTestMethodParameter_RegardlessOfConfigArguments()
    {
        // Every test-method parameter is composed in full under [Compose<TProfile, TConfig>] -
        // configArguments never bind to test-method parameters (a distinct binding target).
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var data = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments;

        Assert.That(data.Length, Is.EqualTo(1));
        Assert.That(data[0], Is.EqualTo("from-config"));
    }
}
