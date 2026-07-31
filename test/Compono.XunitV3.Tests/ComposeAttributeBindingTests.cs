using Compono.XunitV3.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

public sealed class ComposeAttributeBindingTests
{
    [Fact]
    public async Task GetData_ComposesEveryParameter_WhenNoInlineValuesAreSupplied()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();

        data.Should().HaveCount(2);
        data[0].Should().BeOfType<int>();
        data[1].Should().BeOfType<string>();
    }

    [Fact]
    public async Task GetData_BindsInlineValues_InMethodDeclarationOrder()
    {
        var attribute = new ComposeAttribute(42, "hello");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();

        data.Should().Equal(42, "hello");
    }

    [Fact]
    public async Task GetData_MixesInlineAndComposedValues_InlineFirstParameterOnly()
    {
        var attribute = new ComposeAttribute(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var data = rows.Single().GetData();

        data[0].Should().Be(42);
        data[1].Should().BeOfType<string>();
    }

    [Fact]
    public async Task GetData_Throws_ForANegativeSeed()
    {
        var attribute = new ComposeAttribute { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();

        var act = () => attribute.GetData(method, tracker).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*non-negative*");
    }

    [Fact]
    public async Task GetData_SetsTheComponoSeedTrait_MatchingRowSeed()
    {
        var attribute = new ComposeAttribute { Seed = 492173 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var row = rows.Single();

        row.Traits.Should().ContainKey(ComposeAttribute.SeedTraitName)
            .WhoseValue.Should().Contain("492173");
    }

    [Fact]
    public async Task GetData_SetsTheComponoSeedTrait_EvenForAPassingShapedRow()
    {
        // The trait is set unconditionally in GetData, before the theory body ever runs - this
        // asserts it's present regardless of whether the row is shaped to pass or fail, not just
        // reachable from a deliberately-failing case.
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();

        var rows = await attribute.GetData(method, tracker);
        var row = rows.Single();

        row.Traits.Should().ContainKey(ComposeAttribute.SeedTraitName);
    }
}
