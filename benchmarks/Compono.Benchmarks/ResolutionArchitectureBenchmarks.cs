using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>
/// The nested-graph counterpart to <see cref="ArchitectureBenchmarks"/>: what does the real
/// resolution pipeline (provider dispatch, deterministic random forking, collection generation,
/// the diagnostics trace buffer) cost against <see cref="Direct"/>'s theoretical floor and
/// <see cref="Reflection"/>'s hand-rolled alternative, for a representative graph rather than a
/// flat, property-less type.
/// </summary>
/// <remarks>
/// Unlike <see cref="ArchitectureBenchmarks"/>' <c>Leaf</c> (where <see cref="Direct"/>,
/// <see cref="Generated"/>, and <see cref="Reflection"/> all agree on "what work gets done" -
/// nothing, there are no fields to fill), <see cref="Reflection"/> here is *not* an
/// apples-to-apples value-generation comparison: <see cref="ReflectionComposer.ComposeRecursive{T}"/>
/// stamps fixed placeholder values (a literal <c>"value"</c> string, etc.), while
/// <see cref="Generated"/> does Compono's real deterministic random-value generation per field. So
/// <see cref="Reflection"/> being faster than <see cref="Generated"/> here isn't "reflection beats
/// source generation" - it's "doing less work is faster than doing real randomized generation
/// work," the mirror image of <see cref="ResolutionEcosystemBenchmarks"/>' AutoFixture caveat.
/// This benchmark isolates dispatch-mechanism cost for the theoretical floor and generated
/// comparisons; it does not isolate dispatch cost alone for the reflection comparison, since
/// building a reflection-based composer that also does Compono's real random-value generation
/// would mean reimplementing the engine reflectively, disproportionate to what this benchmark
/// needs to establish.
/// </remarks>
[MemoryDiagnoser]
public class ResolutionArchitectureBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>Constructs <see cref="Customer"/> directly - the theoretical floor.</summary>
    [Benchmark(Baseline = true)]
    public Customer Direct() => new("first", "last", new Address("street", "city"), ["tag1", "tag2", "tag3"]);

    /// <summary>Composes <see cref="Customer"/> through the real resolution pipeline.</summary>
    [Benchmark]
    public Customer Generated() => _composer.Create<Customer>();

    /// <summary>
    /// Constructs <see cref="Customer"/> via a recursive reflection-based composer that fills every
    /// field with a fixed placeholder value - see this class' remarks for why that makes this a
    /// dispatch-cost-only baseline, not a value-generation comparison.
    /// </summary>
    [Benchmark]
    public Customer Reflection() => ReflectionComposer.ComposeRecursive<Customer>();
}
