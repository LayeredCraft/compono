namespace Compono;

/// <summary>
/// Coordinates one root composition operation - the fixed 9-stage resolution pipeline
/// (<c>docs/architecture.md</c>), path tracking, and dispatch into generated plans.
/// </summary>
/// <remarks>
/// One instance per root operation (one <see cref="Composer.Create{T}"/> call, or one item of a
/// future <c>CreateMany&lt;T&gt;()</c> call) - never reused across multiple root calls, per
/// <c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>. Stages 1-3
/// (explicit values, shared/scoped values, exact registrations) and the active-construction-frame
/// recursion check are Milestone 2 Phase 3 scope and are not implemented yet - every request falls
/// through stages 1-3 to the extensible provider stages below. Stage 8 (generated-plan dispatch via
/// <see cref="PlanCache{T}"/>) is unchanged from Milestone 1.
/// </remarks>
internal sealed class CompositionContext : ICompositionContext
{
    private readonly IReadOnlyList<ICompositionProvider> _profileProviders;
    private readonly IReadOnlyList<ICompositionProvider> _semanticProviders;
    private readonly IReadOnlyList<ICompositionProvider> _testDoubleProviders;
    private readonly IReadOnlyList<ICompositionProvider> _builtInProviders;

    private CompositionPath? _path;

    /// <summary>Creates a <see cref="CompositionContext"/> with no providers registered in any stage.</summary>
    internal CompositionContext()
        : this(profileProviders: [], semanticProviders: [], testDoubleProviders: [], builtInProviders: [])
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with explicit providers per extensible pipeline
    /// stage - the seam <c>Compono.Tests</c> uses to inject fake providers and assert pipeline
    /// ordering, since no public configuration surface exists until Milestone 3/5/6.
    /// </summary>
    internal CompositionContext(
        IReadOnlyList<ICompositionProvider> profileProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders)
    {
        _profileProviders = profileProviders;
        _semanticProviders = semanticProviders;
        _testDoubleProviders = testDoubleProviders;
        _builtInProviders = builtInProviders;
    }

    /// <inheritdoc />
    public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor)
    {
        PathSegment segment = descriptor.Kind switch
        {
            CompositionRequestKind.ConstructorParameter =>
                new PathSegment.ConstructorParameter(descriptor.Ordinal, descriptor.Name),
            CompositionRequestKind.RequiredMember =>
                new PathSegment.RequiredMember(descriptor.Ordinal, descriptor.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor), descriptor.Kind, "Unrecognized composition request kind."),
        };

        return ResolveCore<TValue>(descriptor.Nullability, segment);
    }

    /// <summary>
    /// Resolves the root value of this composition operation - the entry point
    /// <see cref="Composer.Create{T}"/> uses, distinct from the descriptor-based
    /// <see cref="Resolve{TValue}"/> generated code uses. Both funnel into the same pipeline
    /// execution, so the root type is resolved identically to any nested type.
    /// </summary>
    internal TValue ResolveRoot<TValue>() => ResolveCore<TValue>(Nullability.NotNullable, segment: null);

    private TValue ResolveCore<TValue>(Nullability nullability, PathSegment? segment)
    {
        var requestedType = typeof(TValue);
        _path = _path is null ? CompositionPath.Root(requestedType) : _path.Push(requestedType, segment);

        try
        {
            var request = new CompositionRequest
            {
                RequestedType = requestedType,
                Nullability = nullability,
                Path = _path,
                IsShared = false,
            };

            // Stages 1-3 (explicit values, shared/scoped values, exact registrations) are
            // context-owned deterministic checks - Milestone 2 Phase 3 scope, not implemented yet.
            // Every request currently falls through to the extensible provider stages below.

            if (TryProviders(_profileProviders, request, out var profileValue))
                return CastResult<TValue>(profileValue);

            if (TryProviders(_semanticProviders, request, out var semanticValue))
                return CastResult<TValue>(semanticValue);

            if (TryProviders(_testDoubleProviders, request, out var testDoubleValue))
                return CastResult<TValue>(testDoubleValue);

            if (TryProviders(_builtInProviders, request, out var builtInValue))
                return CastResult<TValue>(builtInValue);

            var plan = PlanCache<TValue>.Instance;
            if (plan is not null)
                return plan.Compose(this);

            throw new CompositionException(
                $"Unable to compose '{requestedType}'. No registration, provider, or generated plan could satisfy the request.");
        }
        finally
        {
            _path = _path.Pop();
        }
    }

    private bool TryProviders(IReadOnlyList<ICompositionProvider> providers, CompositionRequest request, out object? value)
    {
        foreach (var provider in providers)
        {
            if (provider.TryCompose(request, this) is CompositionResult.Success success)
            {
                value = success.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    // A provider-composed value is boxed for a value type TValue (CompositionResult.Success
    // carries object?) - this is the single unbox/cast point back to the generic caller's type.
    private static TValue CastResult<TValue>(object? value) => (TValue)value!;
}
