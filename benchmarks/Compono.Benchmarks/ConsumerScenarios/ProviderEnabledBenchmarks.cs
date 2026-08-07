using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.ConsumerScenarios;

/// <summary>
/// What performance should a user expect from a profile with a package provider active - a
/// <c>Compono.Bogus</c>-enabled profile composing <see cref="MediumAggregate"/> (whose
/// <c>FirstName</c>/<c>LastName</c> members match Bogus's built-in convention allowlist), and a
/// <c>Compono.NSubstitute</c>-enabled profile composing <see cref="ProviderBackedModel"/> (whose
/// <see cref="IClock"/> member NSubstitute's stage-6 provider satisfies) - realistic usage, not
/// the isolated marginal cost <c>FeatureOverhead/ProviderOverheadBenchmarks</c> measures.
/// </summary>
[MemoryDiagnoser]
public class ProviderEnabledBenchmarks
{
    private readonly Composer _bogusComposer = Composer.Create(builder => builder.UseBogus());
    private readonly Composer _nsubstituteComposer = Composer.Create(builder => builder.UseNSubstitute());

    /// <summary>Composes <see cref="MediumAggregate"/> with <c>UseBogus()</c> active.</summary>
    [Benchmark]
    public MediumAggregate BogusEnabledScenario() => _bogusComposer.Create<MediumAggregate>();

    /// <summary>Composes <see cref="ProviderBackedModel"/> with <c>UseNSubstitute()</c> active.</summary>
    [Benchmark]
    public ProviderBackedModel NSubstituteEnabledScenario() => _nsubstituteComposer.Create<ProviderBackedModel>();
}
