namespace Compono;

/// <summary>
/// The chain of request edges from the root of one composition operation to the node currently
/// being resolved.
/// </summary>
/// <remarks>
/// An immutable, persistent linked list - <see cref="Push"/> allocates one new node pointing at the
/// current instance as its parent, mirroring <see cref="CompositionContext.Resolve{TValue}"/>'s own
/// recursive call structure, so push/pop composes with the call stack instead of needing separate
/// bookkeeping. Kept distinct from the active-construction-frame stack
/// (<c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>) - this records
/// every request edge for diagnostics and random forking, not just types under active construction.
/// Random-fork key derivation (<c>IRandomSource</c>) is Milestone 2 Phase 1 scope; this type only
/// carries the structural chain.
/// </remarks>
internal sealed class CompositionPath
{
    private CompositionPath(CompositionPath? parent, Type requestedType, PathSegment? segment)
    {
        Parent = parent;
        RequestedType = requestedType;
        Segment = segment;
    }

    /// <summary>The parent node, or <see langword="null"/> for the root.</summary>
    internal CompositionPath? Parent { get; }

    /// <summary>The type requested at this node.</summary>
    internal Type RequestedType { get; }

    /// <summary>
    /// How this node was reached from its parent, or <see langword="null"/> for the root (a
    /// <c>Create&lt;T&gt;()</c>/<c>CreateMany&lt;T&gt;()</c> call has no parent request).
    /// </summary>
    internal PathSegment? Segment { get; }

    /// <summary>Creates the root node of a new composition operation's path.</summary>
    internal static CompositionPath Root(Type rootType) => new(parent: null, rootType, segment: null);

    /// <summary>Appends a child node for a nested request.</summary>
    internal CompositionPath Push(Type requestedType, PathSegment? segment) => new(this, requestedType, segment);

    /// <summary>
    /// Returns to the parent node, or <see langword="null"/> if this was the root - the composition
    /// operation is idle between root calls.
    /// </summary>
    internal CompositionPath? Pop() => Parent;

    /// <summary>
    /// Renders this path as a dotted, human-readable string (e.g. <c>"Customer.homeAddress.street"</c>)
    /// for diagnostic display only - built from each segment's <c>Name</c>, never consumed by random
    /// forking (<see cref="IRandomSource"/> hashes <see cref="PathSegment"/> tag+<c>Ordinal</c>/
    /// <c>Index</c> data directly, per
    /// <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>).
    /// </summary>
    internal string ToDisplayString() => Parent is null
        ? RequestedType.Name
        : Parent.ToDisplayString() + SegmentDisplayString();

    private string SegmentDisplayString() => Segment switch
    {
        PathSegment.ConstructorParameter p => $".{p.Name}",
        PathSegment.RequiredMember m => $".{m.Name}",
        PathSegment.CollectionElement e => $"[{e.Index}]",
        PathSegment.DictionaryKey k => $".Key[{k.Index}]",
        PathSegment.DictionaryValue v => $".Value[{v.Index}]",
        null => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(Segment), Segment, "Unrecognized path segment kind."),
    };
}
