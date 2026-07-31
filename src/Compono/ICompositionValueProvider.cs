namespace Compono;

/// <summary>
/// A public extension point for pipeline stage 5 (semantic value providers) or stage 6
/// (test-double providers) - open-ended, pattern-matching composition logic a closed-set
/// <c>.For&lt;T&gt;()</c> rule can't express ("any interface type," "any member named
/// <c>Email</c>"). Registered via <see cref="CompositionBuilder.AddSemanticProvider"/> or
/// <see cref="CompositionBuilder.AddTestDoubleProvider"/> - which method an integration's own
/// <c>UseX()</c> extension calls decides which stage a given instance participates in; the
/// interface itself is not stage-specific. See
/// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
/// </summary>
/// <remarks>
/// An implementation must be safe to invoke repeatedly, including concurrently, once constructed -
/// a <see cref="Composer"/>'s configuration (and every provider registered into it) is immutable
/// and reused across every composition call it ever serves, exactly like every other
/// builder-compiled piece of configuration (a <c>.For&lt;T&gt;()</c> rule, a registration factory).
/// </remarks>
public interface ICompositionValueProvider
{
    /// <summary>
    /// Attempts to produce a value for <paramref name="request"/>. Returns
    /// <see cref="CompositionProviderResult.NotHandled"/> for any request this provider doesn't
    /// apply to, so a later provider or pipeline stage still gets a chance - never throws for an
    /// expected non-match.
    /// </summary>
    /// <param name="request">The request to attempt.</param>
    /// <param name="context">
    /// The active composition context - a provider may call <c>context.Resolve&lt;T&gt;()</c> to
    /// compose part of its value from a nested request, exactly as an internal provider already may
    /// (<c>docs/architecture.md</c>'s Providers section).
    /// </param>
    CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context);
}
