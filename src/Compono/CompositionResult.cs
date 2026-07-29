namespace Compono;

/// <summary>
/// What an <see cref="ICompositionProvider"/> reports for one <see cref="CompositionRequest"/>.
/// </summary>
/// <remarks>
/// Deliberately only two cases - an ordinary provider can report that it didn't apply or that it
/// succeeded, never that it "failed." <c>Failure</c> is reserved for context-owned authoritative
/// stages (exact registrations, generated-plan dispatch) whose invocation implies exclusive
/// ownership of the request; it isn't exposed through this type at all, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>. A heuristic-match
/// provider that can't produce a value must return <see cref="NotHandled"/> so a later stage still
/// gets a chance.
/// </remarks>
internal abstract record CompositionResult
{
    private CompositionResult()
    {
    }

    /// <summary>The provider does not handle this request.</summary>
    internal sealed record NotHandled : CompositionResult
    {
        /// <summary>The single shared instance - this case carries no data.</summary>
        internal static readonly NotHandled Instance = new();
    }

    /// <summary>The provider composed a value.</summary>
    /// <param name="Value">
    /// The composed value, boxed if its runtime type is a value type -
    /// <see cref="ICompositionContext.Resolve{TValue}"/> unboxes/casts back to the generic caller's
    /// requested type.
    /// </param>
    internal sealed record Success(object? Value) : CompositionResult;

    /// <summary>
    /// A context-owned authoritative stage established exclusive ownership of the request but
    /// couldn't complete it - never constructed by an ordinary <see cref="ICompositionProvider"/>.
    /// </summary>
    /// <param name="Message">
    /// Describes what went wrong (an invalid shared/registered value, or a detected construction
    /// cycle) - <see cref="CompositionContext"/> converts this directly into a thrown
    /// <see cref="CompositionException"/> at the outward-facing <c>Resolve</c>/<c>ResolveRoot</c>
    /// boundary, per
    /// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>.
    /// </param>
    internal sealed record Failure(string Message) : CompositionResult;
}
