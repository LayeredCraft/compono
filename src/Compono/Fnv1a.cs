using System.Buffers.Binary;

namespace Compono;

/// <summary>
/// FNV-1a hashing, used to derive a child node's fork state from its parent's fork state plus a
/// per-kind tag byte and identifying payload bytes.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>, this
/// combines structured tag+payload data directly - it must never be used to hash a formatted display
/// string, which is what keeps the fork key collision-free by construction instead of by careful
/// string-escaping. The chain always starts from FNV-1a's real offset basis, folding the parent's
/// state in as ordinary input bytes rather than using it as the initial accumulator directly - using
/// the parent's state as the accumulator has a degenerate fixed point (state 0, tag 0, all-zero
/// payload bytes all mix to 0, so it stays 0 forever), which would silently break distinct structural
/// positions forking independently. Starting from the fixed, non-zero offset basis every time and
/// mixing the state in as data avoids that fixed point.
/// </remarks>
internal static class Fnv1a
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    internal static ulong Combine(ulong state, byte tag, ReadOnlySpan<byte> payload)
    {
        Span<byte> stateBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(stateBytes, state);

        var hash = OffsetBasis;
        foreach (var b in stateBytes)
            hash = Mix(hash, b);

        hash = Mix(hash, tag);
        foreach (var b in payload)
            hash = Mix(hash, b);

        return hash;
    }

    private static ulong Mix(ulong hash, byte value) => (hash ^ value) * Prime;
}
