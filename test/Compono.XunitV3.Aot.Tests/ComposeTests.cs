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

// ADR-0067/PLAN-0067 - Compono.XunitV3.Aot profile-based composition. Each profile here mirrors what a
// real Compono.XunitV3 profile would look like (see docs/adr/0022-compono-xunit-package-design.md/
// docs/adr/0036-parameterized-composition-profile-selection.md's own examples) - this project can't
// reference Compono.XunitV3 itself (xunit.v3.aot.mtp-v2 and xunit.v3.mtp-v2 conflict, per ADR-0066),
// so "JIT parity" is proven at the level of: the profile's own Configure() logic (an ordinary
// Register<T>() call, unrelated to xUnit/AOT at all) is honored identically to how Compono.XunitV3
// honors it - the only thing that differs between the two packages is how the profile gets selected
// and constructed, not what composition does once it's applied.
public sealed class PersistentWidgetProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register<OrderService>(() => new OrderService("profile-registered"));
}

public sealed class ProfileComposeTests
{
    [Theory]
    [Compose<PersistentWidgetProfile>]
    public void GenericAttribute_AppliesProfileRegistration(OrderService service)
    {
        Assert.Equal("profile-registered", service.Name);
    }
}

public sealed record WidgetConfig(string Name, int Quantity);

public sealed class ConfiguredWidgetProfile : ICompositionProfile
{
    private readonly WidgetConfig _config;

    public ConfiguredWidgetProfile(WidgetConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) => builder
        .Register<OrderService>(() => new OrderService(_config.Name))
        .Register<int>(() => _config.Quantity);
}

public sealed class TwoTypeParameterComposeTests
{
    [Theory]
    [Compose<ConfiguredWidgetProfile, WidgetConfig>("configured-widget", 7)]
    public void TwoTypeParameterAttribute_ConstructsConfigAndAppliesProfile(OrderService service, int quantity)
    {
        Assert.Equal("configured-widget", service.Name);
        Assert.Equal(7, quantity);
    }
}

// PR #140 Codex review round 5 - real, runtime proof (not just a generator snapshot) that the
// generated `new OverloadConfig((object)"value")` call actually invokes the *public* constructor
// Compono.Generators selected and validated, not the more-specific *internal* one ordinary C# overload
// resolution would otherwise prefer (the generated registration lives in this same assembly, so the
// internal constructor is genuinely accessible from it). Each constructor sets a different observable
// value specifically so a real xUnit run - not just "it compiled" - proves which one actually ran.
public sealed class OverloadConfig
{
    public OverloadConfig(object value) => Source = "public(object)";

    internal OverloadConfig(string value) => Source = "internal(string)";

    public string Source { get; }
}

public sealed class OverloadProfile : ICompositionProfile
{
    private readonly OverloadConfig _config;

    public OverloadProfile(OverloadConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) =>
        builder.Register<string>(() => _config.Source);
}

public sealed class OverloadSelectionTests
{
    [Theory]
    [Compose<OverloadProfile, OverloadConfig>("value")]
    public void TConfigWithMoreSpecificInternalOverload_UsesSelectedPublicConstructor(string source)
    {
        Assert.Equal("public(object)", source);
    }
}
