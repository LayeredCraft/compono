using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Baselines;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.ExternalComparison;

/// <summary>
/// What should a developer expect when migrating from AutoFixture, for the moderately-nested
/// <see cref="MediumAggregate"/> model - the nested-graph counterpart to
/// <see cref="SimplePocoComparisonBenchmarks"/>, giving AutoFixture real randomized-value-
/// generation work to do (unlike the flat <see cref="Models.SimplePoco"/> model).
/// </summary>
[MemoryDiagnoser]
public class MediumAggregateComparisonBenchmarks
{
    private readonly Composer _composer = Composer.Create();
    private readonly AutoFixtureComposer _autoFixture = new();

    /// <summary>Composes <see cref="MediumAggregate"/> through the real resolution pipeline.</summary>
    [Benchmark(Baseline = true)]
    public MediumAggregate Generated() => _composer.Create<MediumAggregate>();

    /// <summary>Constructs <see cref="MediumAggregate"/> via AutoFixture.</summary>
    [Benchmark]
    public MediumAggregate AutoFixture() => _autoFixture.Compose<MediumAggregate>();
}
