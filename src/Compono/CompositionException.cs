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
    /// Creates a <see cref="CompositionException"/>.
    /// </summary>
    /// <param name="message">A message describing what couldn't be composed and why.</param>
    public CompositionException(string message)
        : base(message)
    {
    }
}
