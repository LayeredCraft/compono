using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.Scalability;

/// <summary>
/// <c>CreateMany&lt;T&gt;(count)</c>'s scaling behavior across a batch-size matrix, against its
/// own <c>Create&lt;T&gt;()</c> baseline - exists to catch algorithmic (super-linear) regressions
/// in the checkpoint/rewind trace buffer, per-item seed forking, or scope allocation, not just
/// constant-factor ones, per ADR-0034.
/// </summary>
[MemoryDiagnoser]
public class BatchScalingBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>The batch size <see cref="CreateMany"/> is benchmarked at.</summary>
    [Params(1, 10, 100, 1000)]
    public int Count { get; set; }

    /// <summary>Composes one <see cref="MediumAggregate"/> through the real resolution pipeline.</summary>
    [Benchmark(Baseline = true)]
    public MediumAggregate Create() => _composer.Create<MediumAggregate>();

    /// <summary>Composes <see cref="Count"/> independent <see cref="MediumAggregate"/> instances.</summary>
    [Benchmark]
    public IReadOnlyList<MediumAggregate> CreateMany() => _composer.CreateMany<MediumAggregate>(Count);
}
