using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// Does generated construction (<see cref="Composer.Create{T}"/>) outperform a comparable
/// reflection-based implementation - the architectural question behind Milestone 1
/// (<c>docs/mvp.md</c>'s explicit benchmark ask). <see cref="Direct"/> is the theoretical floor
/// bare <c>new Leaf()</c> gives, so <see cref="Generated"/>'s overhead against it is visible on
/// its own terms, not just relative to reflection.
/// </summary>
[MemoryDiagnoser]
public class ArchitectureBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>Constructs <see cref="Leaf"/> directly - the theoretical floor.</summary>
    [Benchmark(Baseline = true)]
    public Leaf Direct() => new();

    /// <summary>Constructs <see cref="Leaf"/> via its generated <see cref="ICompositionPlan{T}"/>.</summary>
    [Benchmark]
    public Leaf Generated() => _composer.Create<Leaf>();

    /// <summary>Constructs <see cref="Leaf"/> via reflection.</summary>
    [Benchmark]
    public Leaf Reflection() => ReflectionComposer.Compose<Leaf>();
}
