namespace Compono;

/// <summary>
/// The internal, pipeline-facing request a <see cref="CompositionContext"/> expands a
/// <see cref="CompositionRequestDescriptor"/> into - never constructed by generated code directly.
/// </summary>
/// <remarks>
/// Trimmed to the fields with a real Milestone 2 consumer, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c> - no custom
/// attributes, generic context, requested lifetime, or semantic hints until a later milestone needs
/// them.
/// </remarks>
internal sealed record CompositionRequest
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
