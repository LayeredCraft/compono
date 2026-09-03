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
