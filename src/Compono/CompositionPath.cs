using System.Text;

namespace Compono;

/// <summary>
/// The chain of request edges from the root of one composition operation to the node currently
/// being resolved.
/// </summary>
/// <remarks>
/// An immutable, persistent linked list - <see cref="Push"/> allocates one new node pointing at the
/// current instance as its parent, mirroring <see cref="CompositionContext.Resolve{TValue}(in CompositionRequestDescriptor)"/>'s own
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

    /// <summary>The type requested at the root of this composition operation.</summary>
    internal Type RootType => Parent?.RootType ?? RequestedType;

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
        PathSegment.ManualResolve r => $".Resolve[{r.Ordinal}]",
        PathSegment.TestParameter t => $".{t.Name}",
        PathSegment.ConfiguredResolution => ".ConfiguredResolve",
        null => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(Segment), Segment, "Unrecognized path segment kind."),
    };

    /// <summary>
    /// Renders this path as an indented tree (root on the first line, each descendant on its own
    /// <c>"└── Type member"</c> line) - <c>docs/architecture.md</c>'s Diagnostics example format,
    /// used by <see cref="CompositionDiagnostic.Path"/>. Distinct from <see cref="ToDisplayString"/>'s
    /// single-line dotted form, which the recursion-cycle message still uses.
    /// </summary>
    internal string ToTreeString()
    {
        var nodes = new List<CompositionPath>();
        for (var node = this; node is not null; node = node.Parent)
            nodes.Add(node);
        nodes.Reverse();

        var builder = new StringBuilder(FriendlyTypeName(nodes[0].RequestedType));
        for (var depth = 1; depth < nodes.Count; depth++)
        {
            builder.Append('\n')
                .Append(' ', (depth - 1) * 4)
                .Append("└── ")
                .Append(nodes[depth].NodeLabel());
        }

        return builder.ToString();
    }

    private string NodeLabel()
    {
        var typeName = FriendlyTypeName(RequestedType);
        return Segment switch
        {
            PathSegment.ConstructorParameter p => $"{typeName} {p.Name}",
            PathSegment.RequiredMember m => $"{typeName} {m.Name}",
            PathSegment.CollectionElement e => $"{typeName}[{e.Index}]",
            PathSegment.DictionaryKey k => $"{typeName} Key[{k.Index}]",
            PathSegment.DictionaryValue v => $"{typeName} Value[{v.Index}]",
            PathSegment.ManualResolve r => $"{typeName} Resolve[{r.Ordinal}]",
            PathSegment.TestParameter t => $"{typeName} {t.Name}",
            PathSegment.ConfiguredResolution => $"{typeName} ConfiguredResolve",
            null => typeName,
            _ => throw new ArgumentOutOfRangeException(nameof(Segment), Segment, "Unrecognized path segment kind."),
        };
    }

    // Type.Name on a closed generic gives the raw CLR form ("List`1"), unreadable in a diagnostic -
    // renders the C#-style form ("List<Node>") instead, recursing for a nested generic type argument.
    // internal (not private): CompositionDiagnostic.ToString()'s heading and CompositionContext's
    // stage-9 failure message both need the same rendering for a generic RequestedType/RootType,
    // not just this type's own tree/display strings.
    internal static string FriendlyTypeName(Type type)
    {
        // An array's element type isn't itself IsGenericType-visible on the array Type - Type.Name
        // for List<Missing>[] is the raw "List`1[]", not just the array suffix, since arrays aren't
        // generic types (array-ness and generic-ness are orthogonal reflection concepts). Recurse
        // into the element type first so a generic (or another array) element still renders
        // correctly, then reapply the array's own rank (PR #13 review).
        if (type.IsArray)
        {
            var elementName = FriendlyTypeName(type.GetElementType()!);
            var rank = type.GetArrayRank();
            return rank == 1 ? $"{elementName}[]" : $"{elementName}[{new string(',', rank - 1)}]";
        }

        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        var arguments = string.Join(", ", type.GetGenericArguments().Select(FriendlyTypeName));
        return $"{name}<{arguments}>";
    }
}
