using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class LogCollectorDisposalAndIsolationTests
{
    private static readonly LogLevel[] OrdinaryLevels =
    [
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
        LogLevel.Critical,
    ];

    [Fact]
    public void DisposingHost_DisposesProvider_ButCollectorStaysReadable()
    {
        var collector = new LogCollector();
        var host = CreateHost(collector);
        var factoryLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Held.Category");
        var containerLogger = host.Services.GetRequiredService<ILogger<OrderService>>();
        var provider = host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Single();

        factoryLogger.LogInformation("before");
        containerLogger.LogInformation("before container");

        host.Dispose();

        provider.IsDisposed.Should().BeTrue();
        collector.GetCapturedEntries().Should().HaveCount(2);
        collector.GetLastCapturedEntry()!.Value.Message.Should().Be("before container");
        collector.Verify().Exactly(2);
    }

    [Fact]
    public void HeldLoggersAfterDisposal_ReportDisabledAndSilentlyIgnoreEveryLevel()
    {
        var collector = new LogCollector();
        var host = CreateHost(collector);
        var factoryLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Held.Category");
        var containerLogger = host.Services.GetRequiredService<ILogger<OrderService>>();
        factoryLogger.LogInformation("before");
        host.Dispose();

        foreach (var level in OrdinaryLevels)
        {
            factoryLogger.IsEnabled(level).Should().BeFalse();
            containerLogger.IsEnabled(level).Should().BeFalse();
            factoryLogger.Log(level, new EventId(1), "after", null, static (state, _) => state);
            containerLogger.Log(level, new EventId(1), "after", null, static (state, _) => state);
        }

        collector.GetCapturedEntries().Should().ContainSingle();
        collector.GetLastCapturedEntry()!.Value.Message.Should().Be("before");
    }

    [Fact]
    public void HeldLoggersAfterDisposal_BeginScopeReturnsUsableDisposableAndDoesNotThrow()
    {
        var collector = new LogCollector();
        var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILogger<OrderService>>();
        host.Dispose();

        var act = () =>
        {
            using var scope = logger.BeginScope("after disposal");
            scope.Should().NotBeNull();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ProviderCreateLoggerAfterDisposal_ReturnsNonCapturingLogger()
    {
        var collector = new LogCollector();
        var host = CreateHost(collector);
        var provider = host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Single();
        host.Dispose();

        var logger = provider.CreateLogger("After.Disposal");

        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
        logger.LogInformation("ignored");
        using var scope = logger.BeginScope("still usable");
        collector.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void DisposalIsIdempotent_AndNeverClearsOrDetachesTheCollector()
    {
        var collector = new LogCollector();
        var host = CreateHost(collector);
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Idempotent").LogInformation("kept");
        var provider = host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Single();

        host.Dispose();
        host.Dispose();
        provider.Dispose();

        collector.GetCapturedEntries().Should().ContainSingle().Which.Message.Should().Be("kept");

        collector.ClearCapturedEntries();

        collector.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void DisposingOneHost_StopsOnlyThatHostsCapture_ForASharedCollector()
    {
        var collector = new LogCollector();
        var firstHost = CreateHost(collector);
        var secondHost = CreateHost(collector);
        var firstLogger = firstHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("First");
        var secondLogger = secondHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Second");

        firstLogger.LogInformation("first before");
        secondLogger.LogInformation("second before");

        firstHost.Dispose();

        firstLogger.LogInformation("first after");
        secondLogger.LogInformation("second after");

        var entries = collector.GetCapturedEntries();
        entries.Should().HaveCount(3);
        entries.Should().NotContain(entry => entry.Message == "first after");
        entries.Should().Contain(entry => entry.Message == "second after");

        secondHost.Dispose();
    }

    [Fact]
    public async Task ParallelHostsWithSeparateCollectors_ShareNothing()
    {
        const int perTask = 200;
        var first = new LogCollector();
        var second = new LogCollector();
        using var firstHost = CreateHost(first);
        using var secondHost = CreateHost(second);
        var firstLogger = firstHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("First.Category");
        var secondLogger = secondHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Second.Category");
        using var startGate = new Barrier(2);

        var firstWriter = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < perTask; i++)
                firstLogger.LogInformation("first {Index}", i);
        }, TestContext.Current.CancellationToken);

        var secondWriter = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < perTask; i++)
                secondLogger.LogInformation("second {Index}", i);
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(firstWriter, secondWriter);

        first.GetCapturedEntries().Should().HaveCount(perTask).And.OnlyContain(entry => entry.Category == "First.Category");
        second.GetCapturedEntries().Should().HaveCount(perTask).And.OnlyContain(entry => entry.Category == "Second.Category");
    }

    [Fact]
    public async Task ConcurrentMultiCategoryWritesWithReadsAndClears_NeverThrowOrCorrupt()
    {
        // RESEARCH-0033: every participant has a fixed iteration count and never waits for another
        // participant to stop, so the test cannot hang in CI.
        const int categoryCount = 4;
        const int perCategory = 300;
        const int readIterations = 30;
        const int clearIterations = 30;
        string[] categories = ["Multi.Category.0", "Multi.Category.1", "Multi.Category.2", "Multi.Category.3"];
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        using var startGate = new Barrier(categoryCount + 2);

        var writers = categories.Select(category => Task.Run(() =>
        {
            var logger = factory.CreateLogger(category);
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < perCategory; i++)
                logger.LogInformation("write {Index}", i);
        }, TestContext.Current.CancellationToken)).ToArray();

        var reader = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < readIterations; i++)
            {
                _ = collector.GetCapturedEntries();
                _ = collector.GetLastCapturedEntry();
                collector.Verify().AtLeast(0);
            }
        }, TestContext.Current.CancellationToken);

        var clearer = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < clearIterations; i++)
                collector.ClearCapturedEntries();
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll([.. writers, reader, clearer]);

        collector.GetCapturedEntries()
            .Should()
            .HaveCountLessThanOrEqualTo(categoryCount * perCategory)
            .And.OnlyContain(entry => categories.Contains(entry.Category));
    }

    [Fact]
    public async Task ConcurrentWritesAndReads_NeverThrowOrCorrupt()
    {
        // RESEARCH-0033: both participants have fixed iteration counts; neither termination depends
        // on the other, so the captured-entry upper bound stays compile-time constant.
        const int taskCount = 4;
        const int perTask = 500;
        const int readIterations = 50;
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Concurrent.Category");
        using var startGate = new Barrier(taskCount + 1);

        var writers = Enumerable.Range(0, taskCount).Select(writer => Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < perTask; i++)
                logger.LogInformation("writer {Writer} entry {Index}", writer, i);
        }, TestContext.Current.CancellationToken)).ToArray();

        var reader = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < readIterations; i++)
            {
                var entries = collector.GetCapturedEntries();
                foreach (var entry in entries)
                    _ = entry.Message;

                _ = collector.GetLastCapturedEntry();
                collector.Verify().AtLeast(0);
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll([.. writers, reader]);

        collector.GetCapturedEntries().Should().HaveCount(taskCount * perTask);
    }

    [Fact]
    public async Task ConcurrentClearWithWrites_NeverThrowsAndLeavesConsistentSnapshots()
    {
        const int writeCount = 1_000;
        const int clearIterations = 100;
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Clear.Category");
        using var startGate = new Barrier(2);

        var writer = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < writeCount; i++)
                logger.LogInformation("write {Index}", i);
        }, TestContext.Current.CancellationToken);

        var clearer = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            for (var i = 0; i < clearIterations; i++)
            {
                collector.ClearCapturedEntries();
                collector.GetCapturedEntries().Should().OnlyContain(entry => entry.Category == "Clear.Category");
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(writer, clearer);

        collector.GetCapturedEntries().Should().HaveCountLessThanOrEqualTo(writeCount);
    }

    [Fact]
    public void ComposedAndHostCapture_AreSeparateDestinations()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var composer = Composer.Create(builder => builder.UseLogging());
        var composed = composer.Create<OrderService>();

        composed.PlaceOrder(1);

        composed.Logger.GetCapturedEntries().Should().ContainSingle();
        collector.GetCapturedEntries().Should().BeEmpty();

        var hostLogger = host.Services.GetRequiredService<ILogger<OrderService>>();
        hostLogger.LogWarning("host only");

        collector.GetCapturedEntries().Should().ContainSingle();
        composed.Logger.GetCapturedEntries().Should().ContainSingle();
    }

    [Fact]
    public void VerifyOnAHostLogger_ThrowsAndPointsAtLogCollector()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var hostLogger = host.Services.GetRequiredService<ILogger<OrderService>>();

        var act = () => hostLogger.Verify();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*LogCollector*AddComponoCapture*");
    }

    private static IHost CreateHost(LogCollector collector)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        return builder.Build();
    }
}
