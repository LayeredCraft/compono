using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.FeatureOverhead;

/// <summary>
/// Isolates the marginal cost of <c>[Shared]</c>'s underlying mechanism
/// (<c>CreateRow</c>/<c>ResolveShared</c>) against composing the same two sibling values with no
/// sharing at all - both arms go through <c>CreateRow</c>, so the only difference is whether
/// sharing is actually invoked, per ADR-0034's "isolate one feature at a time" principle.
/// </summary>
[MemoryDiagnoser]
public class SharingOverheadBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>
    /// Composes two sibling row values, each independently composing its own
    /// <see cref="SharedContext"/> - no sharing established.
    /// </summary>
    [Benchmark(Baseline = true)]
    public (ConsumerOne One, ConsumerTwo Two) WithoutSharing()
    {
        var row = _composer.CreateRow(typeof(SharingOverheadBenchmarks));

        var one = row.Resolve<ConsumerOne>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 0, "one", typeof(SharingOverheadBenchmarks), Nullability.NotNullable));
        var two = row.Resolve<ConsumerTwo>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 1, "two", typeof(SharingOverheadBenchmarks), Nullability.NotNullable));

        return (one, two);
    }

    /// <summary>
    /// Composes one shared <see cref="SharedContext"/> row value, then two sibling consumers that
    /// both reuse it - the same shape as <see cref="WithoutSharing"/>, with sharing added.
    /// </summary>
    [Benchmark]
    public (ConsumerOne One, ConsumerTwo Two) WithSharing()
    {
        var row = _composer.CreateRow(typeof(SharingOverheadBenchmarks));

        row.ResolveShared<SharedContext>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 0, "context", typeof(SharingOverheadBenchmarks), Nullability.NotNullable));

        var one = row.Resolve<ConsumerOne>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 1, "one", typeof(SharingOverheadBenchmarks), Nullability.NotNullable));
        var two = row.Resolve<ConsumerTwo>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 2, "two", typeof(SharingOverheadBenchmarks), Nullability.NotNullable));

        return (one, two);
    }
}
