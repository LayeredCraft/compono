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

    [Test]
    public async Task InlineValues_SingleNullArgument_TreatedAsOneSuppliedNullValue()
    {
        // [Compose(null)] binds in the C# compiler's non-expanded params form - the constructor
        // receives a null array, not a one-element array containing null - so the constructor must
        // recover the author's actual intent (a single supplied null value) instead of throwing on a
        // null inlineValues array. The null-forgiving operator here simulates exactly that
        // compiler-produced null array from ordinary C# code, which can't itself pass a null array
        // literal to a `params object?[]` parameter without it. Mirrors
        // Compono.XunitV3.Tests.ComposeAttributeCachingTests' identical regression test.
        var attribute = new ComposeAttribute((object?[])null!);

        await Assert.That(attribute.InlineValues).IsEquivalentTo(new object?[] { null });
    }

    [Test]
    public async Task InlineValues_SingleReferenceArrayArgument_TreatedAsOneSuppliedArrayValue()
    {
        // [Compose(new string[] { "a", "b" })] also binds in the C# compiler's non-expanded params
        // form - string[] is covariantly convertible to object?[], so the constructor receives that
        // exact string[] instance (runtime type string[], not object[]) rather than a 2-element
        // object?[]. Must be recovered the same way the single-null case is: as one supplied array
        // value, not two separate inline values. Mirrors Compono.XunitV3.Tests'
        // ComposeAttributeCachingTests' identical regression test.
        var tags = new[] { "a", "b" };
        var attribute = new ComposeAttribute(tags);

        await Assert.That(attribute.InlineValues).IsEquivalentTo(new object?[] { tags });
    }

    [Test]
    public async Task GetDataRowsAsync_AcceptsANullInlineValue_ForANullableReferenceParameter()
    {
        var attribute = new ComposeAttribute(new object?[] { null, "text", 1, 2 });
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data![0]).IsNull();
    }

    [Test]
    public async Task GetDataRowsAsync_AcceptsANullInlineValue_ForANullableValueParameter()
    {
        var attribute = new ComposeAttribute(new object?[] { "text", "other", null, 2 });
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        var data = await SingleRow(attribute, method);

        await Assert.That(data![2]).IsNull();
    }

    [Test]
    public async Task GetDataRowsAsync_RejectsANullInlineValue_ForANonNullableReferenceParameter()
    {
        var attribute = new ComposeAttribute(new object?[] { "text", null, 1, 2 });
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("is null, but the parameter is not nullable");
    }

    [Test]
    public async Task GetDataRowsAsync_RejectsANullInlineValue_ForANonNullableValueParameter()
    {
        var attribute = new ComposeAttribute(new object?[] { "text", "other", 1, null });
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableParameters))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("is null, but the parameter is not nullable");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_ForATypeMismatchedInlineValue()
    {
        var attribute = new ComposeAttribute("not-an-int");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("which is not assignable to");
    }

    [Test]
    public async Task GetDataRowsAsync_Throws_ForTooManyInlineValues()
    {
        var attribute = new ComposeAttribute(1, "text", "extra");
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        await Assert.That(() => SingleRow(attribute, method)).Throws<CompositionException>()
            .WithMessageContaining("Too many inline values supplied");
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
