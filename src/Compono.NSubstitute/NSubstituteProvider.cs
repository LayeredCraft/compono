using NSubstitute;

namespace Compono;

/// <summary>
/// A stage-6 test-double provider that composes an interface, delegate, or (when
/// <see cref="NSubstituteOptions.SubstituteAbstractClasses"/> allows it) unsealed abstract-class
/// request as a real NSubstitute substitute, via <see cref="Substitute.For(Type[], object[])"/>.
/// Registered via <c>CompositionBuilderExtensions.UseNSubstitute()</c>. See
/// <c>docs/adr/0025-compono-nsubstitute-package-design.md</c> (Amendment 1 for the corrected shape
/// below).
/// </summary>
public sealed class NSubstituteProvider : ICompositionValueProvider
{
    private readonly bool _substituteAbstractClasses;

    /// <summary>
    /// Creates an <see cref="NSubstituteProvider"/>, snapshotting <paramref name="options"/>'s
    /// current values - the provider never retains the caller-owned <see cref="NSubstituteOptions"/>
    /// instance itself, so a caller mutating it after construction can't change this
    /// already-registered provider's behavior (ADR-0024's immutable-provider-registration guarantee).
    /// </summary>
    /// <param name="options">The options to snapshot.</param>
    public NSubstituteProvider(NSubstituteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _substituteAbstractClasses = options.SubstituteAbstractClasses;
    }

    /// <inheritdoc />
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (!IsSubstitutable(request.RequestedType, _substituteAbstractClasses))
            return CompositionProviderResult.NotHandled;

        return CompositionProviderResult.Handled(Substitute.For([request.RequestedType], []));
    }

    // ADR-0025 Amendment 1: IsSubclassOf(typeof(MulticastDelegate)), not
    // typeof(Delegate).IsAssignableFrom(requestedType) - the latter wrongly matches the
    // non-substitutable Delegate/MulticastDelegate framework base types themselves.
    internal static bool IsSubstitutable(Type requestedType, bool substituteAbstractClasses) =>
        requestedType.IsInterface
        || requestedType.IsSubclassOf(typeof(MulticastDelegate))
        || (substituteAbstractClasses && requestedType.IsAbstract && !requestedType.IsSealed && !requestedType.IsInterface);
}
