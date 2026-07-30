using System.Collections.ObjectModel;

namespace Compono;

/// <summary>
/// This composer's collection-size configuration - a global default plus a member-scoped override
/// table, queried directly by stage 7's collection dispatch rather than compiled into a stage-4
/// provider. See <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
internal sealed class CollectionSizePolicy
{
    /// <summary>No global default and no member-scoped overrides configured.</summary>
    internal static readonly CollectionSizePolicy Empty = new(null, new Dictionary<(Type, string), int>());

    private readonly IReadOnlyDictionary<(Type DeclaringType, string MemberName), int> _memberOverrides;

    /// <summary>Creates a <see cref="CollectionSizePolicy"/>.</summary>
    /// <remarks>
    /// Defensively copies <paramref name="memberOverrides"/> into a genuinely immutable snapshot -
    /// same rationale as <see cref="CompositionRegistrations"/>'s constructor.
    /// </remarks>
    internal CollectionSizePolicy(int? globalDefault, IReadOnlyDictionary<(Type, string), int> memberOverrides)
    {
        GlobalDefault = globalDefault;
        _memberOverrides = new ReadOnlyDictionary<(Type, string), int>(
            new Dictionary<(Type, string), int>(memberOverrides));
    }

    /// <summary>The global default set via <c>WithCollectionSize(int)</c>, or <see langword="null"/> if never set.</summary>
    internal int? GlobalDefault { get; }

    /// <summary>Attempts to read a member-scoped override for <paramref name="key"/>.</summary>
    internal bool TryGetMemberOverride((Type DeclaringType, string MemberName) key, out int size) =>
        _memberOverrides.TryGetValue(key, out size);
}
