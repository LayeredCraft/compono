using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.Scalability;

/// <summary>
/// Shallow vs. deep graph composition cost, per ADR-0034 - generalizes the old suite's one-off
/// <c>DeepGraphBenchmarks</c> (which only ever exercised <see cref="DeepGraph"/> in isolation)
/// into a real comparison against <see cref="MediumAggregate"/>'s shallow, 2-level shape.
/// <see cref="DeepGraph"/>'s 8-level chain is deep enough (~48 trace entries at its deepest point)
/// to exceed <c>CompositionTraceBuffer</c>'s 32-entry initial capacity and trigger a real
/// <c>Array.Resize</c>, unlike <see cref="MediumAggregate"/>.
/// </summary>
[MemoryDiagnoser]
public class GraphDepthScalingBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>Composes the shallow, 2-level <see cref="MediumAggregate"/> graph.</summary>
    [Benchmark(Baseline = true)]
    public MediumAggregate Shallow() => _composer.Create<MediumAggregate>();

    /// <summary>Composes the 8-level-deep <see cref="DeepGraph"/> chain.</summary>
    [Benchmark]
    public DeepGraph Deep() => _composer.Create<DeepGraph>();
}
