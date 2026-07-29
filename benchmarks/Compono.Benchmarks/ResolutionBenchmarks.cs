using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// <c>CreateMany&lt;T&gt;(count)</c>'s scaling behavior across a few batch sizes, against its own
/// <c>Create&lt;T&gt;()</c> baseline (<see cref="Count"/>) - no external comparison here, since the
/// question this benchmark answers ("does allocation grow linearly or super-linearly with the
/// batch size?", per <c>docs/plans/0002-milestone-2-core-composition-engine.md</c>'s Phase 4
/// benchmark task) has no equivalent in `new()`/reflection/AutoFixture. For the representative-graph
/// comparison against those baselines, see <see cref="ResolutionArchitectureBenchmarks"/> and
/// <see cref="ResolutionEcosystemBenchmarks"/> - both use the same <see cref="Customer"/>/
/// <see cref="Address"/> graph (nested composable type, every Phase 2 built-in kind via
/// <c>string</c>, a <c>List&lt;string&gt;</c> collection member) this class' own <see cref="Create"/>
/// composes.
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
