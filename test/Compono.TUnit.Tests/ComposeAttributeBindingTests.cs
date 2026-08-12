using Compono.TUnit.Tests.Fixtures;

namespace Compono.TUnit.Tests;

public sealed class ComposeAttributeBindingTests
{
    [Test]
    public async Task GetDataRowsAsync_ComposesEveryParameter_WhenNoInlineValuesAreSupplied()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data!.Length).IsEqualTo(2);
        await Assert.That(data[0]).IsTypeOf<int>();
        await Assert.That(data[1]).IsTypeOf<string>();
    }

    [Test]
    public async Task GetDataRowsAsync_BindsInlineValues_InMethodDeclarationOrder()
    {
        var attribute = new ComposeAttribute(42, "hello");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data![0]).IsEqualTo(42);
        await Assert.That(data[1]).IsEqualTo("hello");
    }

    [Test]
    public async Task GetDataRowsAsync_MixesInlineAndComposedValues_InlineFirstParameterOnly()
    {
        var attribute = new ComposeAttribute(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data![0]).IsEqualTo(42);
        await Assert.That(data[1]).IsTypeOf<string>();
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_ForANegativeSeed()
    {
        var attribute = new ComposeAttribute { Seed = -1 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>();
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
