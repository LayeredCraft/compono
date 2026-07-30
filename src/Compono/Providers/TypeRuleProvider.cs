namespace Compono.Providers;

/// <summary>
/// Compiled from a <c>.For&lt;T&gt;().Use(...)</c> builder call with no <c>.Member(...)</c> in
/// between - matches any pipeline stage-4 request for exactly the captured type, regardless of which
/// member/position requested it. See <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
internal sealed class TypeRuleProvider : ICompositionProvider
{
    private readonly Type _type;
    private readonly Func<ICompositionContext, object?> _factory;

    internal TypeRuleProvider(Type type, Func<ICompositionContext, object?> factory)
    {
        _type = type;
        _factory = factory;
    }

    /// <inheritdoc />
    public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
    {
        if (request.RequestedType != _type)
            return CompositionResult.NotHandled.Instance;

        // Invoked through CompositionContext.InvokeFactory (not called directly) - the same
        // manual-resolve invocation frame and factory-reentrance guard stage 3's exact registrations
        // already share, per docs/adr/0019-registrations-and-service-provider-injection.md. context is
        // always the concrete CompositionContext that dispatched this provider via TryProviders.
        // Passing GetType() (not null) is what lets a Failure trace entry for this factory - a thrown
        // exception, or the reentrance guard - be attributed to TypeRuleProvider specifically, matching
        // the provider identity TryProviders' own Pending/NotHandled entries for this same candidate
        // already carry (PR #19 review).
        var value = ((CompositionContext)context).InvokeFactory(_factory, request.RequestedType, PipelineStage.ConfigurationRule, GetType());
        return new CompositionResult.Success(value);
    }
}
