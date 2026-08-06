using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.FeatureOverhead;

/// <summary>
/// How expensive is <c>UseNSubstitute()</c>, on its own? Both arms pin
/// <see cref="ProviderBackedModel.Email"/> identically (a plain member rule - unrelated to what's
/// being measured) and vary only how <see cref="ProviderBackedModel.Clock"/> is resolved: a
/// stage-3 exact registration (the cheapest possible alternative, and the baseline) vs.
/// <c>Compono.NSubstitute</c>'s stage-6 test-double provider.
/// </summary>
[MemoryDiagnoser]
public class NSubstituteOverheadBenchmarks
{
    private readonly Composer _registration = Composer.Create(builder => builder
        .Register<IClock>(_ => new FixedClock())
        .For<ProviderBackedModel>().Member(x => x.Email).Use("fixed@example.com"));

    private readonly Composer _nsubstitute = Composer.Create(builder => builder
        .UseNSubstitute()
        .For<ProviderBackedModel>().Member(x => x.Email).Use("fixed@example.com"));

    /// <summary>Resolves <c>Clock</c> via a stage-3 exact registration - the baseline <see cref="ClockViaNSubstitute"/> is measured against.</summary>
    [Benchmark(Baseline = true)]
    public ProviderBackedModel ClockViaRegistration() => _registration.Create<ProviderBackedModel>();

    /// <summary>Resolves <c>Clock</c> via <c>Compono.NSubstitute</c>'s stage-6 test-double provider.</summary>
    [Benchmark]
    public ProviderBackedModel ClockViaNSubstitute() => _nsubstitute.Create<ProviderBackedModel>();
}
