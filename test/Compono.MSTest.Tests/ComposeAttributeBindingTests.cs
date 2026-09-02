using Compono.MSTest.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

[TestClass]
public sealed class ComposeAttributeBindingTests
{
    [TestMethod]
    public void GetData_ComposesEveryParameter_WhenNoInlineValuesAreSupplied()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var rows = attribute.GetData(method).ToArray();
        var data = rows.Single();

        Assert.AreEqual(2, data.Length);
        Assert.IsInstanceOfType<int>(data[0]);
        Assert.IsInstanceOfType<string>(data[1]);
    }

    [TestMethod]
    public void GetData_BindsInlineValues_InMethodDeclarationOrder()
    {
        var attribute = new ComposeAttribute(42, "hello");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = attribute.GetData(method).Single();

        CollectionAssert.AreEqual(new object?[] { 42, "hello" }, data);
    }

    [TestMethod]
    public void GetData_MixesInlineAndComposedValues_InlineFirstParameterOnly()
    {
        var attribute = new ComposeAttribute(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = attribute.GetData(method).Single();

        Assert.AreEqual(42, data[0]);
        Assert.IsInstanceOfType<string>(data[1]);
    }

    [TestMethod]
    public void GetData_Throws_ForANegativeSeed()
    {
        var attribute = new ComposeAttribute { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var exception = Assert.ThrowsExactly<CompositionException>(() => attribute.GetData(method).ToArray());

        StringAssert.Contains(exception.Message, "non-negative");
    }

    [TestMethod]
    public void GetData_ReturnsExactlyOneRow_PerInvocation()
    {
        // ADR-0057 §5/§9: one CompositionRow, and therefore exactly one returned row, per GetData
        // invocation - this attribute owns the entire row for one test method.
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var rows = attribute.GetData(method).ToArray();

        Assert.AreEqual(1, rows.Length);
    }

    [TestMethod]
    public void GetData_ProducesFreshValues_OnEachIndependentCall()
    {
        // No graph state is ever shared across separate GetData calls (ADR-0057 §9) - two
        // independent calls each get their own fresh CompositionRow/composed values, proven here by
        // two composed strings from separate calls not being reference-equal (Composer's default
        // independent-by-default random string generation).
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var first = (string)attribute.GetData(method).Single()[1]!;
        var second = (string)attribute.GetData(method).Single()[1]!;

        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void GetData_SharesTheSameValue_BetweenASharedParameterAndItself_WithinOneCall()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesSharedString))!;

        var data = attribute.GetData(method).Single();

        Assert.AreSame(data[0], data[1]);
    }

    [TestMethod]
    public void GetData_AppliesTheProfile_ForComposeOfTProfile()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.TestProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithProfile))!;

        var data = attribute.GetData(method).Single();

        Assert.AreEqual("from-profile", data[0]);
    }
}
