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
    internal static readonly CollectionSizePolicy Empty = new(null, new Dictionary<(Type, string), (Type, int)>());

    private readonly IReadOnlyDictionary<(Type DeclaringType, string MemberName), (Type MemberType, int Size)> _memberOverrides;

    /// <summary>Creates a <see cref="CollectionSizePolicy"/>.</summary>
    /// <remarks>
    /// Defensively copies <paramref name="memberOverrides"/> into a genuinely immutable snapshot -
    /// same rationale as <see cref="CompositionRegistrations"/>'s constructor.
    /// </remarks>
    internal CollectionSizePolicy(int? globalDefault, IReadOnlyDictionary<(Type, string), (Type MemberType, int Size)> memberOverrides)
    {
        GlobalDefault = globalDefault;
        _memberOverrides = new ReadOnlyDictionary<(Type, string), (Type MemberType, int Size)>(
            new Dictionary<(Type, string), (Type MemberType, int Size)>(memberOverrides));
    }

    /// <summary>The global default set via <c>WithCollectionSize(int)</c>, or <see langword="null"/> if never set.</summary>
    internal int? GlobalDefault { get; }

    /// <summary>
    /// Attempts to read a member-scoped override for <paramref name="key"/> - only a match if
    /// <paramref name="requestedType"/> also equals the member's own type captured when
    /// <c>.Member&lt;TMember&gt;(...).WithCollectionSize(...)</c> was called. Without this check, a
    /// hand-written class with a differently-typed member sharing the same (declaring type, name) pair
    /// as the one the override actually targets (e.g. an unrelated <c>object Value</c> property next to
    /// a <c>List&lt;int&gt; Value</c> constructor parameter) would silently apply this override to that
    /// other member's collection too (Codex review) - the same collision class
    /// <see cref="Providers.MemberRuleProvider"/>'s own requested-type check already guards against.
    /// </summary>
    internal bool TryGetMemberOverride((Type DeclaringType, string MemberName) key, Type requestedType, out int size)
    {
        if (_memberOverrides.TryGetValue(key, out var entry) && entry.MemberType == requestedType)
        {
            size = entry.Size;
            return true;
        }

        size = default;
        return false;
    }
}
