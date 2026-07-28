using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// Generated construction (<see cref="Composer.Create{T}"/>) against a reflection baseline
/// (<see cref="ReflectionComposer.Compose{T}"/>) - <c>docs/mvp.md</c>'s explicit Milestone 1
/// benchmark ask.
/// </summary>
[MemoryDiagnoser]
public class CompositionBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>Constructs <see cref="Leaf"/> via reflection.</summary>
    [Benchmark(Baseline = true)]
    public Leaf Reflection() => ReflectionComposer.Compose<Leaf>();

    /// <summary>Constructs <see cref="Leaf"/> via its generated <see cref="ICompositionPlan{T}"/>.</summary>
    [Benchmark]
    public Leaf Generated() => _composer.Create<Leaf>();
}
