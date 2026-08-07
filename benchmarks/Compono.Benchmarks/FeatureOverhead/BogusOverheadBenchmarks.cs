using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.FeatureOverhead;

/// <summary>
/// How expensive is <c>UseBogus()</c>, on its own? Both arms pin <see cref="ProviderBackedModel.Clock"/>
/// identically (a plain registration - unrelated to what's being measured) and vary only how
/// <see cref="ProviderBackedModel.Email"/> is resolved: a stage-4 member rule (the baseline) vs.
/// <c>Compono.Bogus</c>'s stage-5 convention provider.
/// </summary>
[MemoryDiagnoser]
public class BogusOverheadBenchmarks
{
    private readonly Composer _memberRule = Composer.Create(builder => builder
        .Register<IClock>(_ => new FixedClock())
        .For<ProviderBackedModel>().Member(x => x.Email).Use("fixed@example.com"));

    private readonly Composer _bogus = Composer.Create(builder => builder
        .Register<IClock>(_ => new FixedClock())
        .UseBogus());

    /// <summary>Resolves <c>Email</c> via a stage-4 member rule - the baseline <see cref="EmailViaBogus"/> is measured against.</summary>
    [Benchmark(Baseline = true)]
    public ProviderBackedModel EmailViaMemberRule() => _memberRule.Create<ProviderBackedModel>();

    /// <summary>Resolves <c>Email</c> via <c>Compono.Bogus</c>'s stage-5 convention provider.</summary>
    [Benchmark]
    public ProviderBackedModel EmailViaBogus() => _bogus.Create<ProviderBackedModel>();
}
