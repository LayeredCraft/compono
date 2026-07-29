namespace Compono;

/// <summary>
/// The internal, pipeline-facing request a <see cref="CompositionContext"/> expands a
/// <see cref="CompositionRequestDescriptor"/> into - never constructed by generated code directly.
/// </summary>
/// <remarks>
/// Trimmed to the fields with a real Milestone 2 consumer, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c> - no custom
/// attributes, generic context, requested lifetime, or semantic hints until a later milestone needs
/// them. A <c>readonly record struct</c>, not a class - never stored beyond the synchronous
/// <c>ResolveCore</c> call that builds it (never captured, never crosses an async boundary), so
/// there's no reason to pay a heap allocation for it. Confirmed by measurement: converting from a
/// class to this struct shape removed 40 B/call and cut construction+read time from ~14.9 ns to
/// ~5.5 ns, real - see <c>docs/performance.md</c>.
/// </remarks>
internal readonly record struct CompositionRequest
{
    /// <summary>The requested CLR type.</summary>
    public required Type RequestedType { get; init; }

    /// <summary>Whether the requesting parameter or member is nullable-annotated.</summary>
    public required Nullability Nullability { get; init; }

    /// <summary>The path from the root of this composition operation to this request.</summary>
    public required CompositionPath Path { get; init; }

    /// <summary>
    /// Whether this request participates in scope reuse (stage 2, <c>docs/architecture.md</c>'s
    /// resolution pipeline) - see
    /// <c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>.
    /// </summary>
    public required bool IsShared { get; init; }
}
