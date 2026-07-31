namespace Compono.XunitV3.SampleTests;

public sealed class InlineAndComposedTests
{
    [Theory]
    [Compose(42, "widget")]
    public void InlineValuesAreUsedDirectly(int quantity, string productName)
    {
        quantity.Should().Be(42);
        productName.Should().Be("widget");
    }

    [Theory]
    [Compose]
    public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName)
    {
        ((object)quantity).Should().BeOfType<int>();
        productName.Should().NotBeNull();
    }

    [Theory]
    [Compose(42)]
    public void MixesInlineAndComposedValues(int quantity, string productName)
    {
        quantity.Should().Be(42);
        productName.Should().NotBeNull();
    }

    [Theory]
    [Compose]
    public async Task ComposesForAnAsyncTheory(string value)
    {
        await Task.Yield();

        value.Should().NotBeNull();
    }
}
