namespace Compono.Providers;

/// <summary>
/// Compiled from a <c>.For&lt;T&gt;().Member(x => x.Y).Use(...)</c> builder call - matches a pipeline
/// stage-4 request only when its <see cref="CompositionRequest.DeclaringType"/>, requested type, and
/// member name (read off the request's current <see cref="PathSegment.ConstructorParameter"/>/
/// <see cref="PathSegment.RequiredMember"/> path node) all equal the captured triple - never inferred
/// from path-parent state. See <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
internal sealed class MemberRuleProvider : ICompositionProvider
{
    private readonly Type _declaringType;
    private readonly string _memberName;
    private readonly Type _memberType;
    private readonly Func<ICompositionContext, object?> _factory;

    internal MemberRuleProvider(Type declaringType, string memberName, Type memberType, Func<ICompositionContext, object?> factory)
    {
        _declaringType = declaringType;
        _memberName = memberName;
        _memberType = memberType;
        _factory = factory;
    }

    /// <inheritdoc />
    public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
    {
        // The requested-type check (exact match, mirroring TypeRuleProvider's own exact-type-only
        // matching, per ADR-0020) is what catches a hand-written class where a property and a
        // constructor parameter legally share the same case-sensitive name but not a type
        // (e.g. `object Value` property + `C(string Value)` constructor parameter) - without it, this
        // rule would wrongly claim the parameter's request too (same DeclaringType/name), handing back
        // a value typed for the property that then fails a raw, undiagnosed InvalidCastException deep
        // inside CastResult<TValue> instead of cleanly declining here (Codex review).
        if (request.DeclaringType != _declaringType || request.RequestedType != _memberType || MemberNameOf(request) != _memberName)
            return CompositionResult.NotHandled.Instance;

        // Same reentrance-guarded invocation path as TypeRuleProvider - see its remarks, including why
        // GetType() (not null) is passed as the provider identity (PR #19 review).
        var value = ((CompositionContext)context).InvokeFactory(_factory, request.RequestedType, PipelineStage.ConfigurationRule, GetType());
        return new CompositionResult.Success(value);
    }

    private static string? MemberNameOf(in CompositionRequest request) => request.Path.Segment switch
    {
        PathSegment.ConstructorParameter p => p.Name,
        PathSegment.RequiredMember m => m.Name,
        _ => null,
    };
}
