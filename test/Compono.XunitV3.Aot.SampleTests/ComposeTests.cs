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

// ADR-0067/PLAN-0067 - the same packaged-dependency-chain proof as ComposeTests above, for
// [Compose<TProfile>]/[Compose<TProfile, TConfig>]. This project publishing as Native AOT and running
// as a native binary (PLAN-0067's own Tier 3 task) is the permanent proof that profile construction -
// including [Compose<TProfile, TConfig>]'s generator-emitted direct new TConfig(...)/new TProfile(...)
// construction - survives the trimmer with zero IL2xxx/IL3xxx warnings, through the real packaged
// Compono.XunitV3.Aot -> Compono dependency chain.
public sealed class PackagedProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<OrderService>(() => new OrderService("packaged-profile"));
}

public sealed class GenericComposeTests
{
    [Theory]
    [Compose<PackagedProfile>]
    public void GenericAttributeAppliesProfileThroughThePackagedDependency(OrderService service)
    {
        Assert.Equal("packaged-profile", service.Name);
    }
}

public sealed record PackagedProfileConfig(string Label);

public sealed class PackagedConfiguredProfile : ICompositionProfile
{
    private readonly PackagedProfileConfig _config;

    public PackagedConfiguredProfile(PackagedProfileConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register<OrderService>(() => new OrderService(_config.Label));
}

public sealed class TwoTypeParameterComposeTests
{
    [Theory]
    [Compose<PackagedConfiguredProfile, PackagedProfileConfig>("packaged-configured-profile")]
    public void TwoTypeParameterAttributeConstructsAndAppliesThroughThePackagedDependency(OrderService service)
    {
        Assert.Equal("packaged-configured-profile", service.Name);
    }
}
