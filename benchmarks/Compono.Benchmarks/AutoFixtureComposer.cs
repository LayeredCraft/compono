using AutoFixture;

namespace Compono.Benchmarks;

/// <summary>
/// Ecosystem-comparison reference point - the established framework developers reach for today
/// to construct test data, benchmarked as a recognizable baseline rather than a target Compono
/// is trying to "beat" (<c>docs/performance.md</c>).
/// </summary>
public sealed class AutoFixtureComposer
{
    private readonly Fixture _fixture = new();

    /// <summary>Constructs an instance of <typeparamref name="T"/> via AutoFixture.</summary>
    /// <typeparam name="T">The type to construct.</typeparam>
    public T Compose<T>() => _fixture.Create<T>();
}
