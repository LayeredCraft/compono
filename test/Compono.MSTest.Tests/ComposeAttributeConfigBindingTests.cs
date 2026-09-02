using Compono.MSTest.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

[TestClass]
public sealed class ComposeAttributeConfigBindingTests
{
    [TestMethod]
    public void GetData_BuildsTProfileFromTConfig_ViaTheAttributesOwnConstructorArguments()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var data = attribute.GetData(method).Single();

        Assert.AreEqual("from-config", data[0]);
    }

    [TestMethod]
    public void GetData_Throws_WhenTConfigHasNoPublicConstructor()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithNoPublicConstructor>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var exception = Assert.ThrowsExactly<CompositionException>(() => attribute.GetData(method).ToArray());

        StringAssert.Contains(exception.Message, "public constructor");
    }

    [TestMethod]
    public void GetData_Throws_ForANegativeSeed_BeforeAttemptingConfigBinding()
    {
        // The negative-seed check runs before ApplyProfile does any config/profile binding work -
        // proven here by a deliberately-broken TConfig/TProfile pairing still reporting the
        // documented negative-seed diagnostic, not a binder failure.
        var attribute = new ComposeAttribute<SampleTestMethods.ProfileWithoutMatchingConstructor, SampleTestMethods.ConfigWithNoPublicConstructor> { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var exception = Assert.ThrowsExactly<CompositionException>(() => attribute.GetData(method).ToArray());

        StringAssert.Contains(exception.Message, "non-negative");
    }

    [TestMethod]
    public void GetData_ComposesEveryTestMethodParameter_RegardlessOfConfigArguments()
    {
        // Every test-method parameter is composed in full under [Compose<TProfile, TConfig>] -
        // configArguments never bind to test-method parameters (a distinct binding target).
        var attribute = new ComposeAttribute<SampleTestMethods.ParameterizedTestProfile, SampleTestMethods.TestConfig>("from-config");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithParameterizedProfile))!;

        var data = attribute.GetData(method).Single();

        Assert.AreEqual(1, data.Length);
        Assert.AreEqual("from-config", data[0]);
    }
}
