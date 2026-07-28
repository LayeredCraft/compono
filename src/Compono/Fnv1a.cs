namespace Compono;

/// <summary>
/// FNV-1a hashing, used to derive a child node's fork state from its parent's fork state plus a
/// per-kind tag byte and identifying payload bytes.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>, this
/// combines structured tag+payload data directly - it must never be used to hash a formatted display
/// string, which is what keeps the fork key collision-free by construction instead of by careful
/// string-escaping. The chain starts from the parent's own derived state rather than FNV-1a's usual
/// offset basis, so each fork is a continuation of its ancestor chain rather than an independent hash.
/// </remarks>
internal static class Fnv1a
{
    private const ulong Prime = 1099511628211UL;

    internal static ulong Combine(ulong state, byte tag, ReadOnlySpan<byte> payload)
    {
        var hash = Mix(state, tag);
        foreach (var b in payload)
            hash = Mix(hash, b);

        return hash;
    }

    private static ulong Mix(ulong hash, byte value) => (hash ^ value) * Prime;
}
