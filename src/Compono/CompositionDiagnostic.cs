namespace Compono;

/// <summary>
/// The structured detail behind a thrown <see cref="CompositionException"/> - what couldn't be
/// composed, where in the graph, what was tried, and with which seed. Exposed via
/// <c>exception.Diagnostic</c> - see
/// <see href="https://layeredcraft.github.io/compono/troubleshooting/common-errors/#runtime-composition-failures">Troubleshooting: Runtime composition failures</see>.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> renders the tree-shaped format documented in
/// <see href="https://layeredcraft.github.io/compono/troubleshooting/common-errors/#runtime-composition-failures">Troubleshooting: Runtime composition failures</see> -
/// the shape a consumer gets from <c>Console.WriteLine(exception.Diagnostic)</c>.
/// </remarks>
public sealed class CompositionDiagnostic
{
    /// <summary>The type requested at the root of this composition operation.</summary>
    public required Type RootType { get; init; }

    /// <summary>The type that could not be composed.</summary>
    public required Type FailedType { get; init; }

    /// <summary>The request path from the root to <see cref="FailedType"/>, rendered as a tree.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// The stage/outcome attempts tried for the failing request and its ancestors - never a sibling
    /// request that already succeeded elsewhere in this operation
    /// (<c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s
    /// checkpoint/rewind trace buffer).
    /// </summary>
    public required IReadOnlyList<ProviderAttempt> Trace { get; init; }

    /// <summary>This operation's root deterministic seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>A human-readable, remediation-oriented explanation of what went wrong.</summary>
    public required string Message { get; init; }

    /// <summary>Renders this diagnostic in Troubleshooting's tree-shaped Diagnostics example format.</summary>
    public override string ToString() =>
        // CompositionPath.FriendlyTypeName, not RootType.Name directly - a closed generic root
        // (Create<List<Missing>>()) would otherwise render the raw CLR form ("List`1") here even
        // though Path below already renders the same type correctly (PR #13 review).
        $"Unable to compose {CompositionPath.FriendlyTypeName(RootType)}.\n\n{Path}\n\n{Message}\n\nSeed: {Seed}";
}
