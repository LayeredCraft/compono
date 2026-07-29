namespace Compono;

/// <summary>
/// The structured detail behind a thrown <see cref="CompositionException"/> - what couldn't be
/// composed, where in the graph, what was tried, and with which seed. <c>docs/public-api.md</c>'s
/// Diagnostics API (<c>exception.Diagnostic</c>).
/// </summary>
/// <remarks>
/// <see cref="ToString"/> renders <c>docs/architecture.md</c>'s Diagnostics example format - the
/// shape a consumer gets from <c>Console.WriteLine(exception.Diagnostic)</c>.
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

    /// <summary>Renders this diagnostic in <c>docs/architecture.md</c>'s Diagnostics example format.</summary>
    public override string ToString() =>
        $"Unable to compose {RootType.Name}.\n\n{Path}\n\n{Message}\n\nSeed: {Seed}";
}
