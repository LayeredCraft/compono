namespace Compono.Providers;

/// <summary>
/// Compiled from a <c>.For&lt;T&gt;().Member(x => x.Y).Use(...)</c> builder call - matches a pipeline
/// stage-4 request only when its <see cref="CompositionRequest.DeclaringType"/> and member name
/// (read off the request's current <see cref="PathSegment.ConstructorParameter"/>/
/// <see cref="PathSegment.RequiredMember"/> path node) equal the captured pair - never inferred from
/// path-parent state. See <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
internal sealed class MemberRuleProvider : ICompositionProvider
{
    private readonly Type _declaringType;
    private readonly string _memberName;
    private readonly Func<ICompositionContext, object?> _factory;

    internal MemberRuleProvider(Type declaringType, string memberName, Func<ICompositionContext, object?> factory)
    {
        _declaringType = declaringType;
        _memberName = memberName;
        _factory = factory;
    }

    /// <inheritdoc />
    public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
    {
        if (request.DeclaringType != _declaringType || MemberNameOf(request) != _memberName)
            return CompositionResult.NotHandled.Instance;

        // Same reentrance-guarded invocation path as TypeRuleProvider - see its remarks.
        var value = ((CompositionContext)context).InvokeFactory(_factory, request.RequestedType, PipelineStage.ConfigurationRule);
        return new CompositionResult.Success(value);
    }

    private static string? MemberNameOf(in CompositionRequest request) => request.Path.Segment switch
    {
        PathSegment.ConstructorParameter p => p.Name,
        PathSegment.RequiredMember m => m.Name,
        _ => null,
    };
}
