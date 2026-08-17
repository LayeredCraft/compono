using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.FeatureOverhead;

/// <summary>
/// PLAN-0044 Phase 3: the concrete number behind <c>Compono.TestDoubles</c>' own stated rationale -
/// an AOT-safe alternative to <c>Compono.NSubstitute</c>'s runtime-proxy dependency for the common
/// case (<c>docs/packages/compono-testdoubles.md</c>). Both arms resolve the identical
/// <see cref="IClock"/> interface leaf; the only difference is which provider satisfies it - a
/// source-generated double (no proxy generation, no reflection) vs. NSubstitute's runtime proxy.
/// Not a general "which mock framework wins" exercise (ADR-0034 explicitly disallows that) - scoped
/// to this one provider-mechanism cost, same as <see cref="NSubstituteOverheadBenchmarks"/>'s own
/// baseline-vs-alternative shape.
/// </summary>
[MemoryDiagnoser]
public class GeneratedTestDoubleVsNSubstituteBenchmarks
{
    private readonly Composer _generatedTestDouble = Composer.Create(builder => builder.UseGeneratedTestDoubles());
    private readonly Composer _nsubstitute = Composer.Create(builder => builder.UseNSubstitute());

    /// <summary>Resolves <see cref="IClock"/> via <c>Compono.TestDoubles</c>' source-generated double - the baseline <see cref="ClockViaNSubstitute"/> is measured against.</summary>
    [Benchmark(Baseline = true)]
    public IClock ClockViaGeneratedTestDouble() => _generatedTestDouble.Create<IClock>();

    /// <summary>Resolves <see cref="IClock"/> via <c>Compono.NSubstitute</c>'s runtime-proxy provider.</summary>
    [Benchmark]
    public IClock ClockViaNSubstitute() => _nsubstitute.Create<IClock>();
}
