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
/// <see cref="Direct"/> stays the theoretical floor (no fields to fill, same as
/// <see cref="ArchitectureBenchmarks"/>' <c>Leaf</c>), but <see cref="Reflection"/> here does
/// comparable real work to <see cref="Generated"/>: <see cref="ReflectionComposer.ComposeRecursive{T}"/>
/// fills every field with a genuinely random value (an 8-character alphanumeric string, a
/// 3-element collection - Compono's own defaults), not a fixed placeholder. An earlier version of
/// this baseline used fixed placeholders, which made <see cref="Reflection"/> faster than
/// <see cref="Generated"/> for doing categorically less work rather than because reflective
/// dispatch actually beats source-generated dispatch - a misleading comparison caught in PR #13
/// review, fixed by rewriting <see cref="ReflectionComposer.ComposeRecursive{T}"/> to do real
/// value generation. The one remaining, deliberate asymmetry: <see cref="Reflection"/>'s randomness
/// is ordinary <see cref="Random.Shared"/>, not Compono's deterministic, seed-forked
/// <see cref="IRandomSource"/> - reproducibility is a Compono product feature
/// (<c>README.md</c>'s "Deterministic by design"), not a cost every random-value generator has to
/// pay, so <see cref="Generated"/>'s cost here includes work <see cref="Reflection"/>'s doesn't.
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
    /// field with a genuinely random value - see this class' remarks for the one remaining,
    /// deliberate asymmetry (ordinary randomness, not Compono's deterministic seed-forking).
    /// </summary>
    [Benchmark]
    public Customer Reflection() => ReflectionComposer.ComposeRecursive<Customer>();
}
