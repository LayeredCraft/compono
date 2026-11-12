using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

// Logs synchronously in StartAsync so the entry exists once host.StartAsync returns. A
// BackgroundService would race here: since .NET 10 its ExecuteAsync runs on a background task, so
// StopAsync can cancel it before it logs (observed as an intermittent net10.0 failure of this test).
public sealed class CheckoutWorker(ILogger<CheckoutWorker> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checkout started for {OrderId}", 42);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// The compiled, running source of the logging examples in
/// docs/migrating-from-microsoft-extensions-diagnostics-testing.md (RESEARCH-0040 finding 7). Keep the
/// two in step: a change to one of these tests means the matching guide snippet changes too.
/// </summary>
public sealed class MigrationGuideExamplesTests
{

    [Fact]
    public async Task HostWideCapture_ReplacesAddFakeLoggingAndFakeLogCollector()
    {
        // Before: builder.Logging.AddFakeLogging(); var logs = host.Services.GetFakeLogCollector();
        var logs = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(logs);
        builder.Services.AddHostedService<CheckoutWorker>();
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        // Before: Assert.DoesNotContain(logs.GetSnapshot(), r => r.Level >= LogLevel.Error);
        logs.Verify().AtLevelOrAbove(LogLevel.Error).Never();

        // Before: logs.GetSnapshot().Where(r => r.Category == "Compono.Logging.Tests.CheckoutWorker")
        logs.Verify()
            .InCategory("Compono.Logging.Tests.CheckoutWorker")
            .WithMessageContaining("Checkout started for 42")
            .Once();

        // Before: logs.GetSnapshot().Where(r => r.Level >= LogLevel.Error && !r.Category.StartsWith("Compono.Logging.Tests."))
        logs.Verify().AtLevelOrAbove(LogLevel.Error).NotInCategoryPrefix("Compono.Logging.Tests.").Never();

        // Before: logs.LatestRecord / logs.GetSnapshot() / logs.Count
        var entries = logs.GetCapturedEntries();
        entries.Should().Contain(entry => entry.Message == "Checkout started for 42");

        // Before: logs.Clear();
        logs.ClearCapturedEntries();
        logs.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void ComposedLogger_ReplacesFakeLoggerOfT()
    {
        // Before: var logger = new FakeLogger<CheckoutWorker>(); ... logger.Collector.GetSnapshot()
        var composer = Composer.Create(builder => builder.UseLogging());
        var logger = composer.Create<ILogger<CheckoutWorker>>();

        logger.LogWarning("Payment retry {Attempt}", 2);

        logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("Payment retry 2").Once();
        logger.GetLastCapturedEntry()!.Value.Category.Should().Be("Compono.Logging.Tests.CheckoutWorker");
    }
}
