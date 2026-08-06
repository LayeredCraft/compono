using BenchmarkDotNet.Attributes;
using Compono.Benchmarks.Models;

namespace Compono.Benchmarks.ConsumerScenarios;

/// <summary>
/// What performance should a user expect from a shared value reused across sibling row
/// parameters - the mechanism <c>Compono.XunitV3</c>'s <c>[Shared]</c> attribute builds on, per
/// ADR-0021. Composed via <c>Composer.CreateRow</c>/<c>ResolveShared</c> directly (the same public
/// core API a test-framework integration uses) rather than depending on
/// <c>Compono.XunitV3</c> itself.
/// </summary>
[MemoryDiagnoser]
public class SharedValueBenchmarks
{
    private readonly Composer _composer = Composer.Create();

    /// <summary>
    /// Composes one <see cref="SharedContext"/> row value plus two sibling consumers, both of
    /// which nest it as an ordinary constructor parameter - the row's shared instance is
    /// transparently reused by both, never composed independently.
    /// </summary>
    [Benchmark]
    public (ConsumerOne One, ConsumerTwo Two) SharedContextAcrossRow()
    {
        var row = _composer.CreateRow(typeof(SharedValueBenchmarks));

        row.ResolveShared<SharedContext>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 0, "context", typeof(SharedValueBenchmarks), Nullability.NotNullable));

        var one = row.Resolve<ConsumerOne>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 1, "one", typeof(SharedValueBenchmarks), Nullability.NotNullable));
        var two = row.Resolve<ConsumerTwo>(new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, 2, "two", typeof(SharedValueBenchmarks), Nullability.NotNullable));

        return (one, two);
    }
}
