namespace Compono;

/// <summary>
/// The structural identity of one node in a <see cref="CompositionPath"/> - what a request was
/// <em>for</em> (a named constructor parameter, a collection index, a dictionary key/value role),
/// distinct from what <em>type</em> was requested there.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>, only
/// each variant's <c>Ordinal</c>/<c>Index</c> is stable identity fed into random-fork hashing -
/// <see cref="ConstructorParameter.Name"/>/<see cref="RequiredMember.Name"/> exist for diagnostic
/// display only.
/// </remarks>
internal abstract record PathSegment
{
    private PathSegment()
    {
    }

    /// <summary>A selected constructor's parameter, identified by its position in that constructor.</summary>
    internal sealed record ConstructorParameter(int Ordinal, string Name) : PathSegment;

    /// <summary>A required init-only member, identified by its generator-assigned declaration-order index.</summary>
    internal sealed record RequiredMember(int Ordinal, string Name) : PathSegment;

    /// <summary>An element of a sequence-shaped collection (array, <c>List&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c>).</summary>
    internal sealed record CollectionElement(int Index) : PathSegment;

    /// <summary>A dictionary entry's key, at the given entry position.</summary>
    internal sealed record DictionaryKey(int Index) : PathSegment;

    /// <summary>A dictionary entry's value, at the given entry position.</summary>
    internal sealed record DictionaryValue(int Index) : PathSegment;
}
