using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// How does Compono compare with AutoFixture, the established framework developers reach for
/// today to construct test data - a separate question from <see cref="ArchitectureBenchmarks"/>,
/// which validates the architecture on its own terms. AutoFixture does substantially more
/// runtime work and has different goals (randomized value generation, kept here unexercised
/// since <see cref="Leaf"/> has no properties to fill), so this is a recognizable reference
/// point, not the success criterion for Milestone 1 (<c>docs/performance.md</c>).
/// </summary>
[MemoryDiagnoser]
public class EcosystemBenchmarks
{
    private readonly Composer _composer = Composer.Create();
    private readonly AutoFixtureComposer _autoFixture = new();

    /// <summary>Constructs <see cref="Leaf"/> via its generated <see cref="ICompositionPlan{T}"/>.</summary>
    [Benchmark(Baseline = true)]
    public Leaf Generated() => _composer.Create<Leaf>();

    /// <summary>Constructs <see cref="Leaf"/> via AutoFixture.</summary>
    [Benchmark]
    public Leaf AutoFixture() => _autoFixture.Compose<Leaf>();
}
