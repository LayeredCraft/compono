using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks.FeatureOverhead;

/// <summary>
/// Isolates the <c>Interlocked.Increment</c> cost <c>ReturnConfig&lt;T&gt;.RecordCall()</c> adds per
/// verified call (ADR-0044's "AOT and performance" section, PLAN-0044 Phase 3) against a plain,
/// non-atomic field increment doing the same job - the only difference between this benchmark's two
/// arms, per ADR-0034's "isolate one feature at a time" principle. Not a benchmark of dispatch
/// through a generated double as a whole (see <see cref="OverloadDispatchOverheadBenchmarks"/> for
/// that) - just the specific atomic-increment primitive verification's counter relies on.
/// </summary>
[MemoryDiagnoser]
public class VerificationOverheadBenchmarks
{
    private int _count;

    /// <summary>A plain, non-atomic increment of the same <c>int</c> field shape.</summary>
    [Benchmark(Baseline = true)]
    public int PlainIncrement() => ++_count;

    /// <summary>The exact primitive <c>RecordCall()</c> uses: <c>Interlocked.Increment(ref field)</c>.</summary>
    [Benchmark]
    public int InterlockedIncrement() => System.Threading.Interlocked.Increment(ref _count);
}
