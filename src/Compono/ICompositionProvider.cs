namespace Compono;

/// <summary>
/// An extensible pipeline stage's participant - profile rules, semantic providers, test-double
/// providers, or built-in providers (<c>docs/architecture.md</c>'s Resolution Pipeline stages 4-7).
/// </summary>
/// <remarks>
/// Context-owned stages (explicit values, shared/scoped values, exact registrations,
/// generated-plan dispatch, diagnostic failure) are not providers and don't implement this
/// interface - see <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>.
/// </remarks>
internal interface ICompositionProvider
{
    /// <summary>
    /// The concrete provider type diagnostics should report for an attempt this instance makes -
    /// defaults to this instance's own runtime type. <see cref="Providers.PublicProviderAdapter"/>
    /// overrides this to report the wrapped <see cref="ICompositionValueProvider"/>'s own concrete
    /// type instead of the adapter's, so <see cref="ProviderAttempt.Provider"/> keeps naming the
    /// real, meaningful provider for a public-provider attempt exactly as it already does for an
    /// internal one. See <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
    /// </summary>
    Type ProviderType => GetType();

    /// <summary>
    /// Attempts to compose a value for <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request to attempt.</param>
    /// <param name="context">The active composition context, for resolving nested requests.</param>
    CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context);
}
