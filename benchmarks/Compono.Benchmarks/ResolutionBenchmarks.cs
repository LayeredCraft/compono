using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// End-to-end resolution-pipeline throughput/allocations for a representative graph
/// (<see cref="Customer"/>/<see cref="Address"/>: nested composable type, every Phase 2 built-in
/// kind exercised via <c>string</c>, and a <c>List&lt;string&gt;</c> collection member) - Compono's
/// actual per-call cost (provider dispatch, random forking, collection generation, the Phase 4
/// diagnostics trace buffer), not just <see cref="ArchitectureBenchmarks"/>'s construction-dispatch
/// cost. <see cref="Count"/> also covers <c>CreateMany&lt;T&gt;(count)</c>'s scaling behavior across
/// a few batch sizes, per
/// <c>docs/plans/0002-milestone-2-core-composition-engine.md</c>'s Phase 4 benchmark task.
/// </summary>
[MemoryDiagnoser]
public class ResolutionBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>The batch size <see cref="CreateMany"/> is benchmarked at.</summary>
    [Params(1, 10, 100)]
    public int Count { get; set; }

    /// <summary>Composes one <see cref="Customer"/> through the real resolution pipeline.</summary>
    [Benchmark(Baseline = true)]
    public Customer Create() => _composer.Create<Customer>();

    /// <summary>Composes <see cref="Count"/> independent <see cref="Customer"/> instances.</summary>
    [Benchmark]
    public IReadOnlyList<Customer> CreateMany() => _composer.CreateMany<Customer>(Count);
}
