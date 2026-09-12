using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.SampleTests;

public sealed class OrderService
{
    public OrderService(string name) => Name = name;

    public string Name { get; }
}

public sealed class ComposeTests
{
    // PLAN-0066's own Tier 2/Tier 3 scenario: composing through the actual packaged
    // Compono.XunitV3.Aot -> Compono dependency chain (never a ProjectReference), proving the real,
    // shipped generator output - not a hand-built stand-in (RESEARCH-0032 §9) and not the
    // ProjectReference-based inner loop (Compono.XunitV3.Aot.Tests).
    [Theory]
    [Compose]
    public void ComposedTypeIsGeneratedThroughThePackagedDependency(OrderService service)
    {
        Assert.NotNull(service);
        Assert.False(string.IsNullOrEmpty(service.Name));
    }
}
