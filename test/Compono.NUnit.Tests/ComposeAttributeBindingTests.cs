using Compono.NUnit.Tests.Fixtures;
using NUnit.Framework;

namespace Compono.NUnit.Tests;

[TestFixture]
public sealed class ComposeAttributeBindingTests
{
    [Test]
    public void BuildFrom_ComposesEveryParameter_WhenNoInlineValuesAreSupplied()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var rows = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray();
        var data = rows.Single().Arguments;

        Assert.That(data.Length, Is.EqualTo(2));
        Assert.That(data[0], Is.InstanceOf<int>());
        Assert.That(data[1], Is.InstanceOf<string>());
    }

    [Test]
    public void BuildFrom_BindsInlineValues_InMethodDeclarationOrder()
    {
        var attribute = new ComposeAttribute(42, "hello");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments;

        Assert.That(data, Is.EqualTo(new object?[] { 42, "hello" }));
    }

    [Test]
    public void BuildFrom_MixesInlineAndComposedValues_InlineFirstParameterOnly()
    {
        var attribute = new ComposeAttribute(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments;

        Assert.That(data[0], Is.EqualTo(42));
        Assert.That(data[1], Is.InstanceOf<string>());
    }

    [Test]
    public void BuildFrom_Throws_ForANegativeSeed()
    {
        var attribute = new ComposeAttribute { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var exception = Assert.Throws<CompositionException>(() =>
            attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray());

        Assert.That(exception!.Message, Does.Contain("non-negative"));
    }

    [Test]
    public void BuildFrom_ReturnsExactlyOneRow_PerInvocation()
    {
        // ADR-0059 §5/§12: one CompositionRow, and therefore exactly one returned TestMethod, per
        // BuildFrom invocation - this attribute owns the entire row for one test method.
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var rows = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).ToArray();

        Assert.That(rows.Length, Is.EqualTo(1));
    }

    [Test]
    public void BuildFrom_ProducesFreshValues_OnEachIndependentCall()
    {
        // No graph state is ever shared across separate BuildFrom calls (ADR-0059 §12) - two
        // independent calls each get their own fresh CompositionRow/composed values, proven here by
        // two composed strings from separate calls not being equal (Composer's default
        // independent-by-default random string generation).
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var first = (string)attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments[1]!;
        var second = (string)attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments[1]!;

        Assert.That(first, Is.Not.EqualTo(second));
    }

    [Test]
    public void BuildFrom_SharesTheSameValue_BetweenASharedParameterAndItself_WithinOneCall()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesSharedString))!;

        var data = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments;

        Assert.That(data[0], Is.SameAs(data[1]));
    }

    [Test]
    public void BuildFrom_AppliesTheProfile_ForComposeOfTProfile()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.TestProfile>();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.ComposesWithProfile))!;

        var data = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single().Arguments;

        Assert.That(data[0], Is.EqualTo("from-profile"));
    }

    [Test]
    public void BuildFrom_SetsASeedBearingDisplayName()
    {
        var attribute = new ComposeAttribute { Seed = 12345 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var testMethod = attribute.BuildFrom(MethodInfoWrapper.Wrap(method), null).Single();

        Assert.That(testMethod.Name, Does.Contain("12345"));
    }
}
