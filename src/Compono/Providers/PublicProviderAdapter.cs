namespace Compono.Providers;

/// <summary>
/// Wraps a public <see cref="ICompositionValueProvider"/> as an internal <see cref="ICompositionProvider"/>
/// so it can be dropped into pipeline stage 5/6's existing ordered-provider dispatch - the same
/// "compile public builder data into an internal provider" shape
/// <c>docs/adr/0020-composition-configuration-rules.md</c>'s <see cref="TypeRuleProvider"/>/
/// <see cref="MemberRuleProvider"/> already use for <c>.For&lt;T&gt;()</c> rules. Constructed once per
/// registered provider, at <see cref="CompositionBuilder.Build"/> time - never per request. See
/// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
/// </summary>
internal sealed class PublicProviderAdapter : ICompositionProvider
{
    private readonly ICompositionValueProvider _inner;

    internal PublicProviderAdapter(ICompositionValueProvider inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public Type ProviderType => _inner.GetType();

    /// <inheritdoc />
    public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
    {
        var publicRequest = new CompositionProviderRequest(request.RequestedType, request.DeclaringType, NameOf(request), request.Nullability);
        var result = _inner.TryProvide(in publicRequest, context);

        return result.IsHandled
            ? new CompositionResult.Success(result.Value)
            : CompositionResult.NotHandled.Instance;
    }

    // Only ConstructorParameter/RequiredMember/TestParameter segments carry a name at all - every
    // other segment kind (collection element, dictionary key/value, manual resolve) has none, mirroring
    // CompositionRequestDescriptor.DeclaringType's own "null means no member identity" contract.
    private static string? NameOf(in CompositionRequest request) => request.Path.Segment switch
    {
        PathSegment.ConstructorParameter p => p.Name,
        PathSegment.RequiredMember m => m.Name,
        PathSegment.TestParameter t => t.Name,
        _ => null,
    };
}
