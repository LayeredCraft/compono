using System.Buffers.Binary;

namespace Compono;

/// <summary>
/// The only <see cref="IRandomSource"/> implementation.
/// </summary>
/// <remarks>
/// Keeps two independent 64-bit states per node: a fixed "fork state" derived purely from the
/// structural path (used only to derive children via <see cref="Fork"/>, never mutated by
/// <see cref="NextUInt64"/>), and a "value state" seeded from the fork state and advanced on each
/// <see cref="NextUInt64"/> call. Keeping them separate means how many random values a node's own
/// provider draws never perturbs its children's derived state - only structural position does, per
/// <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>.
/// </remarks>
internal sealed class RandomSource : IRandomSource
{
    private const byte ConstructorParameterTag = 0;
    private const byte RequiredMemberTag = 1;
    private const byte CollectionElementTag = 2;
    private const byte DictionaryKeyTag = 3;
    private const byte DictionaryValueTag = 4;
    private const byte ManualResolveTag = 5;
    private const byte TestParameterTag = 6;
    private const byte ConfiguredResolutionTag = 8;

    // Distinct from the eight PathSegment-kind tags above - DeriveSeed's own fixed salt, so a
    // caller-derived seed (ADR-0026) is never accidentally identical to a value this same node might
    // separately fork for an ordinary PathSegment-keyed child. Fixed at 7 - do not renumber, per
    // ADR-0012's deterministic-output compatibility guarantee (renumbering would silently change
    // every derived-seed value for existing consumers on a fixed seed).
    private const byte DeriveSeedTag = 7;

    private readonly ulong _forkState;
    private ulong _valueState;

    private RandomSource(ulong forkState)
    {
        _forkState = forkState;
        _valueState = forkState;
    }

    /// <summary>Creates the root node's random source from the composition operation's seed.</summary>
    internal static RandomSource FromSeed(CompositionSeed seed) => new(seed.Value);

    /// <inheritdoc />
    public IRandomSource Fork(PathSegment segment)
    {
        var (tag, ordinalOrIndex) = segment switch
        {
            PathSegment.ConstructorParameter p => (ConstructorParameterTag, p.Ordinal),
            PathSegment.RequiredMember m => (RequiredMemberTag, m.Ordinal),
            PathSegment.CollectionElement e => (CollectionElementTag, e.Index),
            PathSegment.DictionaryKey k => (DictionaryKeyTag, k.Index),
            PathSegment.DictionaryValue v => (DictionaryValueTag, v.Index),
            PathSegment.ManualResolve r => (ManualResolveTag, r.Ordinal),
            PathSegment.TestParameter t => (TestParameterTag, t.Ordinal),
            PathSegment.ConfiguredResolution c => (ConfiguredResolutionTag, c.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(segment), segment, "Unrecognized path segment kind."),
        };

        // Fixed big-endian encoding regardless of host machine endianness - BitConverter.GetBytes
        // would silently vary the fork key's byte sequence on a big-endian host, which would break
        // the "same seed, same output" guarantee across machines.
        Span<byte> payload = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, ordinalOrIndex);

        return new RandomSource(Fnv1a.Combine(_forkState, tag, payload));
    }

    /// <inheritdoc />
    public ulong NextUInt64() => SplitMix64.Next(ref _valueState);

    /// <inheritdoc />
    public ulong DeriveSeed() => Fnv1a.Combine(_forkState, DeriveSeedTag, ReadOnlySpan<byte>.Empty);
}
