using System.Text;

namespace Compono;

/// <summary>
/// A composition operation's root deterministic state - either generated once per root operation, or
/// an explicit value a test seam supplies (<see cref="Composer.CreateRootForTesting{T}"/>/
/// <see cref="Composer.CreateManyForTesting{T}"/>) before Milestone 3's public <c>WithSeed(...)</c>
/// builder exists.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0009-deterministic-seed-and-forkable-random-source.md</c>, the same seed produces
/// the same output only for a given <c>Compono</c> package version - cross-version stability across an
/// upgrade is not promised.
/// </remarks>
internal readonly record struct CompositionSeed(ulong Value)
{
    // Distinct from RandomSource's five PathSegment-kind tags (0-4) - this tag is only ever used for
    // CreateMany's batch-root/per-item string-key forking, never for path-segment forking.
    private const byte StringKeyTag = 5;

    /// <summary>Generates a new, non-deterministic root seed for an ordinary <see cref="Composer.Create{T}"/> call.</summary>
    internal static CompositionSeed Generate() =>
        new(unchecked((ulong)Random.Shared.NextInt64(long.MinValue, long.MaxValue)));

    /// <summary>
    /// Derives an independent child seed by a stable string key - used for <c>CreateMany</c>'s
    /// two-level <c>"CreateMany"</c> then per-item-index fork
    /// (<c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>), not for
    /// ordinary path-segment forking during resolution (see <see cref="IRandomSource.Fork"/>).
    /// </summary>
    internal CompositionSeed Fork(string key) =>
        new(Fnv1a.Combine(Value, StringKeyTag, Encoding.UTF8.GetBytes(key)));
}
