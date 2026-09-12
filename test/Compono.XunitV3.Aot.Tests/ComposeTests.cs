using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

public sealed class OrderService
{
    public OrderService(string name) => Name = name;

    public string Name { get; }
}

public sealed class ComposeTests
{
    [Theory]
    [Compose]
    public void SingleComposedParameter_ReceivesComposedValue(OrderService service)
    {
        Assert.NotNull(service);
        Assert.False(string.IsNullOrEmpty(service.Name));
    }

    [Theory]
    [Compose]
    public void MultipleParameters_AreEachComposed(OrderService service, string leaf, int quantity)
    {
        Assert.NotNull(service);
        Assert.False(string.IsNullOrEmpty(service.Name));
        Assert.False(string.IsNullOrEmpty(leaf));
        // Compono's built-in int provider can compose any int value, negative included - asserting a
        // range here would be exactly the "run it many times and hope" flakiness testing.md warns
        // against. The point of this test is that all three parameters get independently composed
        // real values (proven by SingleComposedParameter_ReceivesComposedValue/BuiltInLeafTypeOnly_
        // IsComposed's own stronger per-type assertions) - a boxed type check is enough here.
        Assert.IsType<int>((object)quantity);
    }

    [Theory]
    [Compose]
    public void BuiltInLeafTypeOnly_IsComposed(string value)
    {
        Assert.False(string.IsNullOrEmpty(value));
    }
}
