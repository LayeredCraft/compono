using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.Scalability;

/// <summary>
/// Shallow vs. deep graph composition cost, per ADR-0034 - generalizes the old suite's one-off
/// <c>DeepGraphBenchmarks</c> (which only ever exercised <see cref="DeepGraph"/> in isolation)
/// into a real depth-only comparison.
/// </summary>
/// <remarks>
/// <see cref="Shallow"/> composes <see cref="DeepLevel8"/> directly - the exact same leaf shape
/// (one <see cref="string"/> member, nothing else) <see cref="Deep"/>'s <see cref="DeepGraph"/>
/// resolves at the bottom of its 8-level chain, just at depth 1 instead of depth 8. An earlier
/// version of this benchmark used <see cref="MediumAggregate"/> as the shallow arm instead, which
/// resolves two objects, seven strings, and a collection - categorically more value-generation
/// work than <see cref="DeepGraph"/>'s single string, so any difference between the two couldn't
/// be attributed to depth alone versus the extra work. Both arms here resolve exactly one leaf
/// string value; depth (1 vs. 8) is the only variable. <see cref="DeepGraph"/>'s 8-level chain is
/// deep enough (~48 trace entries at its deepest point) to exceed
/// <c>CompositionTraceBuffer</c>'s 32-entry initial capacity and trigger a real
/// <c>Array.Resize</c>, unlike the depth-1 case.
/// </remarks>
[MemoryDiagnoser]
public class GraphDepthScalingBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>Composes <see cref="DeepLevel8"/> directly - depth 1, one string leaf, the same leaf shape <see cref="Deep"/> resolves at depth 8.</summary>
    [Benchmark(Baseline = true)]
    public DeepLevel8 Shallow() => _composer.Create<DeepLevel8>();

    /// <summary>Composes the 8-level-deep <see cref="DeepGraph"/> chain - depth 8, the same one-string leaf shape as <see cref="Shallow"/>.</summary>
    [Benchmark]
    public DeepGraph Deep() => _composer.Create<DeepGraph>();
}
