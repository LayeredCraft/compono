using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// The nested-graph counterpart to <see cref="EcosystemBenchmarks"/>: how does Compono compare
/// with AutoFixture once there's an actual representative graph to fill (nested composable type,
/// a collection member), rather than <see cref="EcosystemBenchmarks"/>' flat, property-less
/// <see cref="Leaf"/>, which never exercised AutoFixture's real value-generation work.
/// </summary>
[MemoryDiagnoser]
public class ResolutionEcosystemBenchmarks
{
    private readonly Composer _composer = Composer.Create();
    private readonly AutoFixtureComposer _autoFixture = new();

    /// <summary>Composes <see cref="Customer"/> through the real resolution pipeline.</summary>
    [Benchmark(Baseline = true)]
    public Customer Generated() => _composer.Create<Customer>();

    /// <summary>Constructs <see cref="Customer"/> via AutoFixture.</summary>
    [Benchmark]
    public Customer AutoFixture() => _autoFixture.Compose<Customer>();
}
