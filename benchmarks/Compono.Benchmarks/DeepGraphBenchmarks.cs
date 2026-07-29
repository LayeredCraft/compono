using BenchmarkDotNet.Attributes;

namespace Compono.Benchmarks;

/// <summary>An 8-level-deep chain of composable types - purely to exercise <see cref="CompositionTraceBuffer"/>'s growth path.</summary>
public sealed record DeepLevel8(string Value);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel7(DeepLevel8 Child);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel6(DeepLevel7 Child);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel5(DeepLevel6 Child);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel4(DeepLevel5 Child);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel3(DeepLevel4 Child);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel2(DeepLevel3 Child);

/// <summary>See <see cref="DeepLevel8"/>'s remarks.</summary>
public sealed record DeepLevel1(DeepLevel2 Child);

/// <summary>
/// Measures a genuinely deep composable-type chain (8 nested generated-plan dispatches), not just
/// <see cref="ResolutionBenchmarks"/>' shallow <see cref="Customer"/>/<see cref="Address"/> graph -
/// a PR #13 review point: each active ancestor frame dispatching through stage 8 retains ~6 trace
/// entries until its own child returns (5 declined stages + a <c>Pending</c> marker), so this
/// 8-level chain (~48 entries at its deepest point) exceeds <see cref="CompositionTraceBuffer"/>'s
/// 32-entry initial capacity and triggers a real <c>Array.Resize</c> - unlike the shallow
/// <c>Customer</c> graph, which never gets deep enough to.
/// </summary>
[MemoryDiagnoser]
public class DeepGraphBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>Composes the 8-level-deep <see cref="DeepLevel1"/> chain.</summary>
    [Benchmark]
    public DeepLevel1 Create() => _composer.Create<DeepLevel1>();
}
