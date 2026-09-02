using System.Reflection;
using Compono.XunitV3;
using Xunit.Sdk;

namespace Compono.XunitV3.AotSmokeTest;

// A real, custom composed type - needs both a generated PlanCache<Widget> entry (ordinary
// plan-generation discovery) and a generated RowInvokerRegistry registration (ADR-0041).
internal sealed class Widget
{
    public Widget(string name) => Name = name;

    public string Name { get; }
}

// Issue #119's Native AOT gate: ConfigProfileBinder's ConstructorInfo.Invoke-based TConfig/TProfile
// construction needs the same real publish-and-run proof RowInvokerRegistry dispatch already gets
// below - "likely AOT-safe because it's a non-generic, already-known Type" isn't good enough on its
// own (Compono.TUnit's and Compono.MSTest's own AOT smoke tests each found a real trim gap here that
// needed DynamicallyAccessedMembers annotations - ADR-0041 Amendment 1, ADR-0057).
internal sealed record ProfileConfig(int Seed);

internal sealed class ConfiguredProfile : ICompositionProfile
{
    private readonly ProfileConfig _config;

    public ConfiguredProfile(ProfileConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) => builder.WithSeed(_config.Seed);
}

internal static class SmokeTestMethods
{
    // The real target of this whole harness: a real Compono.XunitV3.ComposeAttribute-attributed
    // method parameter list containing both a custom composed type (Widget, needs a real
    // PlanCache<Widget> entry) and a provider-resolved leaf type (string, needs no PlanCache entry
    // at all per ADR-0041 Amendment 2) - both need a real RowInvokerRegistry registration to
    // dispatch through CompositionRow.Resolve<T>() under Native AOT with no MakeGenericMethod
    // anywhere. Unlike test/Compono.AotSmokeTest (which stands in for Compono.XunitV3.ComposeAttribute
    // via a hand-written ComposeAttributeStandIn and dispatches through RowInvokerRegistry manually,
    // proving only the shared mechanism in isolation), this harness drives the *real*
    // Compono.XunitV3.ComposeAttribute.GetData(MethodInfo, DisposalTracker) - proving BindingPlan.Build
    // and Compono.XunitV3.Binding.RowInvokers.Build themselves survive Native AOT, not just the
    // registry they call into.
    [Compose]
    public static void Handle(Widget widget, string leaf)
    {
    }

    // Exercises ComposeAttribute<TProfile, TConfig>.ApplyProfile -> ConfigProfileBinder.BindConfig/
    // BuildProfile, both ConstructorInfo.Invoke-based - the issue #119 AOT gate this harness exists to
    // prove.
    [Compose<ConfiguredProfile, ProfileConfig>(12345)]
    public static void HandleWithConfiguredProfile(Widget widget, string leaf)
    {
    }
}

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            await RunRow(
                typeof(SmokeTestMethods).GetMethod(nameof(SmokeTestMethods.Handle))!,
                new ComposeAttribute(),
                "Compono.XunitV3.ComposeAttribute");

            await RunRow(
                typeof(SmokeTestMethods).GetMethod(nameof(SmokeTestMethods.HandleWithConfiguredProfile))!,
                new ComposeAttribute<ConfiguredProfile, ProfileConfig>(12345),
                "Compono.XunitV3.ComposeAttribute<TProfile, TConfig> (ConfigProfileBinder)");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    private static async Task RunRow(MethodInfo method, ComposeAttribute attribute, string label)
    {
        var rows = await attribute.GetData(method, new DisposalTracker());

        if (rows.Count != 1)
            throw new InvalidOperationException($"Expected exactly one data row, got {rows.Count}.");

        var data = rows.Single().GetData();

        if (data is not [Widget { Name.Length: > 0 } widget, string { Length: > 0 } leaf])
            throw new InvalidOperationException($"Unexpected composed row: {(data is null ? "null" : string.Join(", ", data))}");

        Console.WriteLine($"PASS: {label} dispatch survived Native AOT - Widget.Name='{widget.Name}', leaf='{leaf}'.");
    }
}
