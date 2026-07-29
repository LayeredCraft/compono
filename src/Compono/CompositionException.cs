namespace Compono;

/// <summary>
/// Thrown when a composition request reaches the resolution pipeline's terminal stage - no
/// explicit value, shared value, registration, provider, or generated plan could satisfy it.
/// </summary>
/// <remarks>
/// This is the thrown-exception boundary <c>docs/public-api.md</c>'s examples catch -
/// <see cref="ICompositionContext.Resolve{TValue}"/> must return a plain <c>TValue</c>, so a
/// terminal non-success pipeline outcome has no return-value channel to report through and
/// converts to this exception instead, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>. The pipeline's own
/// internal stages still communicate via <see cref="CompositionResult"/>, not exceptions - only
/// this outward-facing boundary throws.
/// </remarks>
public sealed class CompositionException : Exception
{
    /// <summary>
    /// The structured detail behind this failure, or <see langword="null"/> if this instance was
    /// constructed from a plain message.
    /// </summary>
    public CompositionDiagnostic? Diagnostic { get; }

    /// <summary>
    /// Creates a <see cref="CompositionException"/> with no structured <see cref="Diagnostic"/>.
    /// </summary>
    /// <param name="message">A message describing what couldn't be composed and why.</param>
    public CompositionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionException"/> from a structured <see cref="CompositionDiagnostic"/>
    /// - the shape every pipeline-thrown instance uses, per <c>docs/public-api.md</c>'s Diagnostics
    /// API.
    /// </summary>
    /// <param name="diagnostic">The structured detail behind this failure.</param>
    public CompositionException(CompositionDiagnostic diagnostic)
        : base(diagnostic.Message)
    {
        Diagnostic = diagnostic;
    }
}
