using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.ConsumerScenarios;

/// <summary>
/// What performance should a user expect composing each of ADR-0034's representative models in a
/// realistic application - no comparison baseline, just the absolute cost a consumer actually
/// pays for <c>Create&lt;T&gt;()</c> against each shape. This is the category most likely to
/// surface in public documentation, per ADR-0034.
/// </summary>
[MemoryDiagnoser]
public class RepresentativeModelBenchmarks
{
    private readonly Composer _composer = Composer.Create();
    private readonly Composer _largeCollectionComposer = Composer.Create(builder => builder.WithCollectionSize(100));

    /// <summary>Composes the flat <see cref="SimplePoco"/> model.</summary>
    [Benchmark]
    public SimplePoco SimplePocoScenario() => _composer.Create<SimplePoco>();

    /// <summary>Composes the moderately-nested <see cref="MediumAggregate"/> model.</summary>
    [Benchmark]
    public MediumAggregate MediumAggregateScenario() => _composer.Create<MediumAggregate>();

    /// <summary>Composes the 8-level-deep <see cref="DeepGraph"/> model.</summary>
    [Benchmark]
    public DeepGraph DeepGraphScenario() => _composer.Create<DeepGraph>();

    /// <summary>Composes <see cref="LargeCollection"/> with a 100-element collection size.</summary>
    [Benchmark]
    public LargeCollection LargeCollectionScenario() => _largeCollectionComposer.Create<LargeCollection>();
}
