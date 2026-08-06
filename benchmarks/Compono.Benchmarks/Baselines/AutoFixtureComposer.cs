using AutoFixture;

namespace Compono.Benchmarks.Baselines;

/// <summary>
/// External-comparison reference point, per ADR-0034: AutoFixture is one comparison point
/// answering "what should a developer expect when migrating," not the suite's center. Stock
/// <c>Fixture</c>, no customization - an honest out-of-the-box comparison, not tuned to
/// artificially favor either library.
/// </summary>
public sealed class AutoFixtureComposer
{
    private readonly Fixture _fixture = new();

    /// <summary>Constructs an instance of <typeparamref name="T"/> via AutoFixture.</summary>
    /// <typeparam name="T">The type to construct.</typeparam>
    public T Compose<T>() => _fixture.Create<T>();
}
