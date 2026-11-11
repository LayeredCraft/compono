using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Compono.Metrics.Tests;

public sealed class HostRecipeTests
{
    [Fact]
    public async Task HostRecipe_RegisteredFactoryIsTheOneTheSutUses()
    {
        using var meters = new CapturingMeterFactory();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IMeterFactory>(meters);
        builder.Services.AddSingleton<OrderService>();
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var service = host.Services.GetRequiredService<OrderService>();
        service.Meters.Should().BeSameAs(meters);
        service.PlaceOrder("eu", 2);
        await host.StopAsync(TestContext.Current.CancellationToken);

        meters.Verify<long>("Shop.Orders", "shop.orders.placed").WithTag("region", "eu").Once();
    }

    [Fact]
    public async Task HostRecipe_TestOwnsDisposal_AfterHostStops()
    {
        var meters = new CapturingMeterFactory();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IMeterFactory>(meters);
        builder.Services.AddSingleton<OrderService>();
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        host.Services.GetRequiredService<OrderService>().PlaceOrder("us", 1);
        await host.StopAsync(TestContext.Current.CancellationToken);

        // The container doesn't dispose an instance registration, so the test's own using owns it.
        meters.Dispose();

        meters.GetMeasurements<double>("Shop.Orders", "shop.orders.duration").Should().ContainSingle();
    }

    [Fact]
    public async Task HostRecipe_BuilderAlreadyRegistersADefaultMeterFactory()
    {
        var builder = Host.CreateApplicationBuilder();
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var factory = host.Services.GetRequiredService<IMeterFactory>();
        await host.StopAsync(TestContext.Current.CancellationToken);

        factory.Should().NotBeNull();
        (factory is CapturingMeterFactory).Should().BeFalse(
            "the default host registers its own IMeterFactory before the test registers anything");
    }

    [Fact]
    public async Task HostRecipe_LaterRegistrationWinsForSingleServiceResolution()
    {
        using var first = new CapturingMeterFactory();
        using var second = new CapturingMeterFactory();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IMeterFactory>(first);
        builder.Services.AddSingleton<IMeterFactory>(second);
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var resolved = host.Services.GetRequiredService<IMeterFactory>();
        await host.StopAsync(TestContext.Current.CancellationToken);

        resolved.Should().BeSameAs(second);
    }

    [Fact]
    public async Task HostRecipe_TryAddSingletonDoesNotReplaceAnExistingRegistration()
    {
        using var meters = new CapturingMeterFactory();
        var builder = Host.CreateApplicationBuilder();

        // The host already registered a default IMeterFactory, so TryAddSingleton keeps that one.
        builder.Services.TryAddSingleton<IMeterFactory>(meters);
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var resolved = host.Services.GetRequiredService<IMeterFactory>();
        await host.StopAsync(TestContext.Current.CancellationToken);

        resolved.Should().NotBeSameAs(meters);
    }
}
