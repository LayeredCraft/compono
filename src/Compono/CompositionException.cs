namespace Compono;

/// <summary>
/// Thrown when a composition request reaches the resolution pipeline's terminal stage - no
/// explicit value, shared value, registration, provider, or generated plan could satisfy it.
/// </summary>
/// <remarks>
/// This is the thrown-exception boundary a composed test's <c>try</c>/<c>catch</c> examples catch -
/// <see cref="ICompositionContext.Resolve{TValue}(in CompositionRequestDescriptor)"/> must return a plain <c>TValue</c>, so a
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
    /// - the shape every pipeline-thrown instance uses.
    /// </summary>
    /// <param name="diagnostic">The structured detail behind this failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    public CompositionException(CompositionDiagnostic diagnostic)
        : base(RequireDiagnostic(diagnostic).Message)
    {
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Creates a <see cref="CompositionException"/> from a structured <see cref="CompositionDiagnostic"/>,
    /// preserving <paramref name="innerException"/> - the shape a configured <c>IServiceProvider</c>
    /// throwing during stage 3's fallback sub-step uses, per
    /// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c> ("never <c>throw ex;</c>,
    /// the original exception is always preserved").
    /// </summary>
    /// <param name="diagnostic">The structured detail behind this failure.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="diagnostic"/> or <paramref name="innerException"/> is <see langword="null"/>.
    /// </exception>
    public CompositionException(CompositionDiagnostic diagnostic, Exception innerException)
        : base(RequireDiagnostic(diagnostic).Message, RequireInnerException(innerException))
    {
        Diagnostic = diagnostic;
    }

    /// <summary>
    /// Creates a copy of <paramref name="original"/> whose <see cref="Exception.Message"/> has a
    /// <c>"Seed: &lt;value&gt;"</c> line appended, so a consumer building custom composition-failure
    /// tooling (e.g. a test-framework integration reporting a reproducible seed) can surface it
    /// without needing <see cref="Diagnostic"/> to be present - <see cref="Diagnostic"/> already
    /// renders its own <c>"Seed:"</c> line via <see cref="CompositionDiagnostic.ToString"/> when it's
    /// there, but not every <see cref="CompositionException"/> has one (a plain
    /// <see cref="CompositionException(string)"/>, e.g. a generated collection plan's
    /// unique-value-exhaustion failure, has none).
    /// </summary>
    /// <param name="original">The exception to copy and append the seed to.</param>
    /// <param name="seed">The seed value to append.</param>
    /// <returns>
    /// A new <see cref="CompositionException"/> whose <see cref="Diagnostic"/> is
    /// <paramref name="original"/>'s, unchanged (<see langword="null"/> stays <see langword="null"/>),
    /// and whose <see cref="Exception.InnerException"/> is <paramref name="original"/> itself - so a
    /// provider failure's full chain becomes this new exception, then <paramref name="original"/>,
    /// then whatever <paramref name="original"/>'s own <see cref="Exception.InnerException"/> was (if
    /// any), one level deeper than the original throw.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> is <see langword="null"/>.</exception>
    public static CompositionException WithSeedInMessage(CompositionException original, int seed)
    {
        ArgumentNullException.ThrowIfNull(original);

        var message = $"{original.Message}\n\nSeed: {seed}";
        return new CompositionException(message, original.Diagnostic, original) { DiagnosingContextIdentity = original.DiagnosingContextIdentity };
    }

    // Identifies which CompositionContext instance's own BuildException produced this exception - an
    // opaque identity token (CompositionContext.Identity), not the context itself (PR #17 review):
    // storing the actual CompositionContext would keep its whole object graph alive for as long as a
    // consumer or test runner retains this exception - its configured IServiceProvider, registration
    // factory closures, scope-held shared values, and trace buffer, none of which this comparison
    // needs. A bare bool couldn't distinguish "some context produced this" from "this context's active
    // nested resolution produced it" either (a registration factory can capture and invoke an entirely
    // different Composer and let that composer's own pipeline-diagnosed exception escape, or rethrow
    // one it captured earlier) - InvokeFactory's catch guard (docs/adr/0010) needs identity, just not
    // by holding the whole context to get it.
    internal object? DiagnosingContextIdentity { get; private init; }

    // The only construction path that sets DiagnosingContextIdentity - used exclusively by
    // CompositionContext.BuildException (passing its own Identity token), never by a factory/rule that
    // constructs a CompositionException itself.
    internal static CompositionException CreatePipelineDiagnosed(CompositionDiagnostic diagnostic, object diagnosingContextIdentity) =>
        new(diagnostic) { DiagnosingContextIdentity = diagnosingContextIdentity };

    internal static CompositionException CreatePipelineDiagnosed(CompositionDiagnostic diagnostic, Exception innerException, object diagnosingContextIdentity) =>
        new(diagnostic, innerException) { DiagnosingContextIdentity = diagnosingContextIdentity };

    // The base(...) initializer runs before this constructor's own body, so a guard clause in the
    // body would already be too late - diagnostic.Message is dereferenced in the initializer itself.
    // Routing the null check through this helper is what lets a null argument surface as
    // ArgumentNullException instead of a base-initializer NullReferenceException.
    private static CompositionDiagnostic RequireDiagnostic(CompositionDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return diagnostic;
    }

    private static Exception RequireInnerException(Exception innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
        return innerException;
    }

    private CompositionException(string message, CompositionDiagnostic? diagnostic, Exception innerException)
        : base(message, innerException)
    {
        Diagnostic = diagnostic;
    }
}
