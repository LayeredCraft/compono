namespace Compono;

/// <summary>
/// A small, explicitly Compono-owned pseudo-random number generator - not <see cref="Random"/> - so
/// the byte-for-byte output sequence is something Compono controls and can keep stable across .NET
/// runtime versions, per
/// <c>docs/adr/0009-deterministic-seed-and-forkable-random-source.md</c>.
/// </summary>
/// <remarks>
/// SplitMix64 is used both to derive a node's initial value-stream state from its fork state, and as
/// the generator advancing that stream on each <see cref="Next"/> call - a deliberate simplification
/// of ADR-0009's "SplitMix64 for derivation, feeding a xoshiro-family generator for value
/// generation" phrasing: SplitMix64 alone is a well-established, sufficiently-distributed PRNG for
/// this purpose, and Milestone 2 Phase 2's built-in providers are the first real consumer of
/// generated values - swapping in a xoshiro-family generator later, if a stronger statistical
/// distribution is ever needed, doesn't change <see cref="IRandomSource"/>'s internal contract.
/// </remarks>
internal static class SplitMix64
{
    private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>Advances <paramref name="state"/> and returns the next pseudo-random value.</summary>
    internal static ulong Next(ref ulong state)
    {
        state += GoldenGamma;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
