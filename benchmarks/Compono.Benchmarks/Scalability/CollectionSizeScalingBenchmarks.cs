using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.Scalability;

/// <summary>
/// Composition cost as a single collection member's size grows, per ADR-0034 - catches
/// algorithmic regressions in stage 7's collection dispatch (<c>CollectionPlanCache&lt;T&gt;</c>)
/// that a fixed, small collection size would never surface.
/// </summary>
[MemoryDiagnoser]
public class CollectionSizeScalingBenchmarks
{
    // Assigned in GlobalSetup, which BenchmarkDotNet guarantees runs (once per Params value)
    // before any [Benchmark] method executes.
    private Composer _composer = null!;

    /// <summary><see cref="LargeCollection.Items"/>' element count for this run.</summary>
    [Params(3, 10, 50, 200)]
    public int CollectionSize { get; set; }

    /// <summary>Builds a composer configured for this run's <see cref="CollectionSize"/> - kept out of the timed benchmark method.</summary>
    [GlobalSetup]
    public void Setup() => _composer = Composer.Create(builder => builder.WithCollectionSize(CollectionSize));

    /// <summary>Composes <see cref="LargeCollection"/> at this run's <see cref="CollectionSize"/>.</summary>
    [Benchmark]
    public LargeCollection Create() => _composer.Create<LargeCollection>();
}
