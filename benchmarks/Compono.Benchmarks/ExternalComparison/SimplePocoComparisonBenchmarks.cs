using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Baselines;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.ExternalComparison;

/// <summary>
/// What should a developer expect when migrating from AutoFixture, for the flat
/// <see cref="SimplePoco"/> model - per ADR-0034, AutoFixture is one comparison point, not the
/// suite's center. Equivalent object graph, equivalent work - published honestly either way.
/// </summary>
[MemoryDiagnoser]
public class SimplePocoComparisonBenchmarks
{
    private readonly Composer _composer = Composer.Create();
    private readonly AutoFixtureComposer _autoFixture = new();

    /// <summary>Composes <see cref="SimplePoco"/> through the real resolution pipeline.</summary>
    [Benchmark(Baseline = true)]
    public SimplePoco Generated() => _composer.Create<SimplePoco>();

    /// <summary>Constructs <see cref="SimplePoco"/> via AutoFixture.</summary>
    [Benchmark]
    public SimplePoco AutoFixture() => _autoFixture.Compose<SimplePoco>();
}
