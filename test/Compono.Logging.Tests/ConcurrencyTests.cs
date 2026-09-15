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
        const int writeCount = 2_000;
        const int readIterations = 50;

        // RESEARCH-0033: the original version of this test had an unbounded writer
        // (`while (!cts.IsCancellationRequested) logger.LogInformation(...)`, no yield/delay)
        // whose only stop condition was the reader's fixed 500 iterations finishing first and
        // calling cts.Cancel() - under enough CPU/thread-pool contention (routine during a
        // full-solution dotnet test), the reader could be starved long enough for the writer to
        // add an effectively unbounded number of entries before that ever happened, and because
        // LogEntryCollector.GetEntries() does an O(n) copy under lock on every reader call, the
        // reader's own fixed iteration count became progressively more expensive as the list
        // grew - a resource-starvation/performance-runaway race (confirmed via live process
        // diagnostics: RSS reaching 7+ GB, dominant thread time split between blocking GC and a
        // contended lock), not a classic deadlock or livelock (both participants kept doing real
        // work throughout; the work itself ran away). Redesigned so neither participant's
        // termination ever depends on the other, and the upper bound on captured entries -
        // therefore the upper bound on GetCapturedEntries()'s per-call cost - is `writeCount`,
        // fixed at compile time, regardless of how the two tasks happen to be scheduled.
        //
        // Barrier (not a timing-based Task.Delay/sleep) is the start gate: both tasks call
        // SignalAndWait before doing any real work, guaranteeing both are started and have
        // reached this point before either workload begins - substantially improving the
        // opportunity for genuine overlap versus plain Task.Run. It does not guarantee
        // operation-level interleaving after release (the scheduler could still let one loop
        // run ahead of the other); without the gate at all, though, one loop could in principle
        // run to completion before the other is even scheduled, which would still pass but
        // would exercise essentially no concurrent access.
        using var startGate = new Barrier(2);

        var writer = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);

            for (var i = 0; i < writeCount; i++)
                logger.LogInformation("write {Index}", i);
        }, TestContext.Current.CancellationToken);

        var reader = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);

            for (var i = 0; i < readIterations; i++)
            {
                var entries = logger.GetCapturedEntries();
                foreach (var entry in entries)
                    _ = entry.Message;
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(writer, reader);

        // Deterministic - the writer always completes exactly writeCount iterations (its
        // termination never depended on the reader), so the final captured count is exact, not
        // just "at least some entries got through."
        logger.GetCapturedEntries().Should().HaveCount(writeCount);
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
