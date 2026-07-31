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
    private readonly PipelineStage _stage;

    // stage is which pipeline stage this instance was registered into (SemanticProvider or
    // TestDoubleProvider, per CompositionBuilder.Build) - carried alongside the wrapped provider so
    // CompositionContext.InvokeProvider's own reentrance-failure trace entry (PR #28 review) is
    // recorded against the correct stage, not guessed or omitted.
    internal PublicProviderAdapter(ICompositionValueProvider inner, PipelineStage stage)
    {
        _inner = inner;
        _stage = stage;
    }

    /// <inheritdoc />
    public Type ProviderType => _inner.GetType();

    /// <inheritdoc />
    public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
    {
        var publicRequest = new CompositionProviderRequest(request.RequestedType, request.DeclaringType, NameOf(request), request.Nullability);

        // Routed through InvokeProvider (not called directly) so TryProvide gets the same manual-
        // resolve frame and reentrance guard TypeRuleProvider/MemberRuleProvider's own factories
        // already get from InvokeFactory - see InvokeProvider's remarks for why a separate method,
        // not a reuse of InvokeFactory itself (PR #28 review, Codex).
        var result = ((CompositionContext)context).InvokeProvider(_inner, in publicRequest, _stage, ProviderType);

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
