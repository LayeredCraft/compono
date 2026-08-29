using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class ConcurrencyTests
{
    [Fact]
    public async Task ManyParallelLogCalls_NoLostEntries()
    {
        var logger = new CapturingLogger<ConcurrencyTests>();
        const int perTask = 200;
        const int taskCount = 20;

        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < perTask; i++)
                    logger.LogInformation("entry {Index}", i);
            }));

        await Task.WhenAll(tasks);

        logger.GetCapturedEntries().Should().HaveCount(taskCount * perTask);
    }

    [Fact]
    public async Task ReadsConcurrentWithWrites_NeverThrowOrCorrupt()
    {
        var logger = new CapturingLogger<ConcurrencyTests>();
        using var cts = new CancellationTokenSource();

        var writer = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
                logger.LogInformation("write");
        }, TestContext.Current.CancellationToken);

        var reader = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                var entries = logger.GetCapturedEntries();
                foreach (var entry in entries)
                    _ = entry.Message;
            }
        }, TestContext.Current.CancellationToken);

        await reader;
        cts.Cancel();
        await writer;
    }

    [Fact]
    public async Task LoggingFactoryRegistry_ConcurrentRegisterAndTryCreate_IsSafe()
    {
        var options = new LoggingOptions();

        var tasks = Enumerable.Range(0, 50).Select(iteration => Task.Run(() =>
        {
            LoggingFactoryRegistry.Register<ConcurrencyRegistryCategory>(static o => new CapturingLogger<ConcurrencyRegistryCategory>(o));
            LoggingFactoryRegistry.TryCreate(typeof(ILogger<ConcurrencyRegistryCategory>), options, out var registered);
            return registered;
        }));

        await Task.WhenAll(tasks);

        LoggingFactoryRegistry.TryCreate(typeof(Microsoft.Extensions.Logging.ILogger<ConcurrencyRegistryCategory>), options, out var value)
            .Should().BeTrue();
        value.Should().BeOfType<CapturingLogger<ConcurrencyRegistryCategory>>();
    }
}

public sealed class ConcurrencyRegistryCategory;
