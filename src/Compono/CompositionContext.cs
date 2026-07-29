using Compono.Providers;

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
    private readonly CompositionSeed _seed;
    private readonly IReadOnlyList<ICompositionProvider> _profileProviders;
    private readonly IReadOnlyList<ICompositionProvider> _semanticProviders;
    private readonly IReadOnlyList<ICompositionProvider> _testDoubleProviders;
    private readonly IReadOnlyList<ICompositionProvider> _builtInProviders;

    private CompositionPath? _path;
    private IRandomSource? _random;

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with no providers registered in any stage and a
    /// freshly generated root seed.
    /// </summary>
    internal CompositionContext()
        : this(CompositionSeed.Generate())
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with the real stage-7 built-in providers, no
    /// providers registered in any other stage, and the given explicit root seed - the seam
    /// <see cref="Composer.CreateRootForTesting{T}"/> uses.
    /// </summary>
    internal CompositionContext(CompositionSeed seed)
        : this(seed, profileProviders: [], semanticProviders: [], testDoubleProviders: [], builtInProviders: BuiltInProviders.Default)
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with explicit providers per extensible pipeline
    /// stage and a freshly generated root seed - the seam <c>Compono.Tests</c> uses to inject fake
    /// providers and assert pipeline ordering, since no public configuration surface exists until
    /// Milestone 3/5/6.
    /// </summary>
    internal CompositionContext(
        IReadOnlyList<ICompositionProvider> profileProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders)
        : this(CompositionSeed.Generate(), profileProviders, semanticProviders, testDoubleProviders, builtInProviders)
    {
    }

    private CompositionContext(
        CompositionSeed seed,
        IReadOnlyList<ICompositionProvider> profileProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders)
    {
        _seed = seed;
        _profileProviders = profileProviders;
        _semanticProviders = semanticProviders;
        _testDoubleProviders = testDoubleProviders;
        _builtInProviders = builtInProviders;
    }

    /// <summary>
    /// The current node's forked random source - internal test-observability seam for Phase 1's own
    /// determinism tests (via a capturing <see cref="ICompositionPlan{T}"/>). Milestone 2 Phase 2's
    /// built-in providers are the first real consumer of generated values.
    /// </summary>
    internal IRandomSource Random =>
        _random ?? throw new InvalidOperationException("No composition operation is currently in progress.");

    /// <inheritdoc />
    public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor)
    {
        PathSegment segment = descriptor.Kind switch
        {
            CompositionRequestKind.ConstructorParameter =>
                new PathSegment.ConstructorParameter(descriptor.Ordinal, descriptor.Name),
            CompositionRequestKind.RequiredMember =>
                new PathSegment.RequiredMember(descriptor.Ordinal, descriptor.Name),
            // CollectionElement/DictionaryKey/DictionaryValue carry no Name - a generated collection
            // plan's Ordinal is the segment's Index; CompositionPath's display-string derivation
            // never reads Name for these three kinds either, per ADR-0010's third amendment.
            CompositionRequestKind.CollectionElement => new PathSegment.CollectionElement(descriptor.Ordinal),
            CompositionRequestKind.DictionaryKey => new PathSegment.DictionaryKey(descriptor.Ordinal),
            CompositionRequestKind.DictionaryValue => new PathSegment.DictionaryValue(descriptor.Ordinal),
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
        var previousRandom = _random;
        var isRoot = _path is null;

        // A descriptor-based Resolve<T> called directly on a fresh context (no preceding
        // ResolveRoot<T>() call) - only reachable from a test exercising the descriptor path in
        // isolation, never from generated code - is treated as its own root, exactly like _path's
        // existing null-check above: there is no ancestor random source to fork from either.
        _path = isRoot ? CompositionPath.Root(requestedType) : _path!.Push(requestedType, segment);
        _random = isRoot ? RandomSource.FromSeed(_seed) : previousRandom!.Fork(segment!);

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

            // Still stage 7 conceptually (docs/architecture.md) - a generated collection plan is
            // "a built-in value provider," just dispatched via a direct closed-generic field read
            // (like stage 8's PlanCache<TValue> below) rather than through ICompositionProvider,
            // which can't itself construct a generic collection without reflection or boxing/erasure.
            // See docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md's third
            // amendment.
            if (CollectionPlanCache<TValue>.Instance is { } collectionPlan)
                return collectionPlan.Compose(this);

            var plan = PlanCache<TValue>.Instance;
            if (plan is not null)
                return plan.Compose(this);

            throw new CompositionException(
                $"Unable to compose '{requestedType}'. No registration, provider, or generated plan could satisfy the request.");
        }
        finally
        {
            _path = _path.Pop();
            _random = previousRandom;
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
