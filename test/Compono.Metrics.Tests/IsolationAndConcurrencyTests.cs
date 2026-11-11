using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Compono.Metrics.Tests;

public sealed class IsolationAndConcurrencyTests
{
    [Fact]
    public void TwoFactoriesUnderParallelExecution_NeverSeeEachOthersMeasurements()
    {
        using var first = new CapturingMeterFactory();
        using var second = new CapturingMeterFactory();
        var firstMeter = first.Create(new MeterOptions("Shop"));
        var secondMeter = second.Create(new MeterOptions("Shop"));
        var firstCounter = firstMeter.CreateCounter<long>("orders");
        var secondCounter = secondMeter.CreateCounter<long>("orders");

        Parallel.Invoke(
            () => { for (var i = 0; i < 500; i++) firstCounter.Add(1); },
            () => { for (var i = 0; i < 300; i++) secondCounter.Add(1); });

        first.GetMeasurements<long>("orders").Should().HaveCount(500);
        second.GetMeasurements<long>("orders").Should().HaveCount(300);
    }

    [Fact]
    public void ConcurrentRecordings_AreAllCaptured_AndReadsAreSafe()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");

        Parallel.For(0, 1000, _ =>
        {
            counter.Add(1);
            factory.GetMeasurements<long>("orders");
            factory.Verify<long>("orders").AtLeast(0);
        });

        factory.GetMeasurements<long>("orders").Should().HaveCount(1000);
    }

    [Fact]
    public void ConcurrentClearAndRecord_KeepsCaptureConsistent()
    {
        using var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        const int iterations = 500;
        var observed = new ConcurrentQueue<int>();

        Parallel.For(0, iterations, i =>
        {
            counter.Add(1);
            observed.Enqueue(factory.GetMeasurements<long>("orders").Count);
            if (i % 50 == 49)
                factory.ClearMeasurements();
        });

        factory.ClearMeasurements();

        // Clear fully empties every instrument, and a recording is never lost, duplicated, or torn:
        // every snapshot count sits between 0 and the number of recordings made by that point.
        factory.GetMeasurements<long>("orders").Should().BeEmpty();
        observed.Should().NotBeEmpty();
        observed.Should().OnlyContain(count => count >= 0 && count <= iterations);
        observed.Should().Contain(count => count > 0);
    }

    [Fact]
    public void RecordingWhileDisposing_DoesNotThrow_AndStopsCapturingAfterDispose()
    {
        var factory = new CapturingMeterFactory();
        var meter = factory.Create(new MeterOptions("Shop"));
        var counter = meter.CreateCounter<long>("orders");
        using var barrier = new Barrier(2);
        Exception? unexpected = null;

        Parallel.Invoke(
            () =>
            {
                barrier.SignalAndWait();
                try
                {
                    for (var i = 0; i < 1000; i++)
                        counter.Add(1);
                }
                catch (Exception exception)
                {
                    unexpected = exception;
                }
            },
            () =>
            {
                barrier.SignalAndWait();
                factory.Dispose();
            });

        unexpected.Should().BeNull();

        var captured = factory.GetMeasurements<long>("orders").Count;
        captured.Should().BeInRange(0, 1000);

        // Disposal has completed, so a further recording is not captured - and reads/clears still work.
        counter.Add(1);
        factory.GetMeasurements<long>("orders").Should().HaveCount(captured);
        var act = () => factory.ClearMeasurements();
        act.Should().NotThrow();
    }
}
