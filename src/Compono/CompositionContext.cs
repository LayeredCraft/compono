using Compono.Providers;

namespace Compono;

/// <summary>
/// Coordinates one root composition operation - the fixed 9-stage resolution pipeline
/// (<c>docs/architecture.md</c>), path tracking, and dispatch into generated plans.
/// </summary>
/// <remarks>
/// One instance per root operation (one <see cref="Composer.Create{T}"/> call, or one item of a
/// <see cref="Composer.CreateMany{T}"/> call) - never reused across multiple root calls, per
/// <c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>. Stage 1
/// (explicit values) has no mechanism yet - it stays Milestone 3 scope (the public builder). Stages
/// 2/3 (shared/scoped values, exact registrations) and the active-construction-frame recursion check
/// are implemented as of Milestone 2 Phase 3. Stage 8 (generated-plan dispatch via
/// <see cref="PlanCache{T}"/>) is unchanged from Milestone 1.
/// </remarks>
internal sealed class CompositionContext : ICompositionContext
{
    private readonly CompositionSeed _seed;
    private readonly CompositionRegistrations _registrations;
    private readonly CompositionScope _scope = new();
    private readonly List<Type> _activeFrames = [];
    private readonly CompositionTraceBuffer _trace = new();
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
        : this(seed, CompositionRegistrations.Empty)
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with the real stage-7 built-in providers, the given
    /// explicit stage-3 registrations, and the given explicit root seed - the seam
    /// <c>Compono.Tests</c> uses to exercise stage 3 (exact registrations) directly, since no public
    /// <c>builder.Register(...)</c> surface exists until Milestone 3.
    /// </summary>
    internal CompositionContext(CompositionSeed seed, CompositionRegistrations registrations)
        : this(seed, registrations, profileProviders: [], semanticProviders: [], testDoubleProviders: [], builtInProviders: BuiltInProviders.Default)
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
        : this(CompositionSeed.Generate(), CompositionRegistrations.Empty, profileProviders, semanticProviders, testDoubleProviders, builtInProviders)
    {
    }

    private CompositionContext(
        CompositionSeed seed,
        CompositionRegistrations registrations,
        IReadOnlyList<ICompositionProvider> profileProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders)
    {
        _seed = seed;
        _registrations = registrations;
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
            // never reads Name for these three kinds either, per ADR-0014.
            CompositionRequestKind.CollectionElement => new PathSegment.CollectionElement(descriptor.Ordinal),
            CompositionRequestKind.DictionaryKey => new PathSegment.DictionaryKey(descriptor.Ordinal),
            CompositionRequestKind.DictionaryValue => new PathSegment.DictionaryValue(descriptor.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor), descriptor.Kind, "Unrecognized composition request kind."),
        };

        return ResolveCore<TValue>(descriptor.Nullability, segment, isShared: false);
    }

    /// <summary>
    /// Resolves the root value of this composition operation - the entry point
    /// <see cref="Composer.Create{T}"/> uses, distinct from the descriptor-based
    /// <see cref="Resolve{TValue}"/> generated code uses. Both funnel into the same pipeline
    /// execution, so the root type is resolved identically to any nested type.
    /// </summary>
    internal TValue ResolveRoot<TValue>() => ResolveCore<TValue>(Nullability.NotNullable, segment: null, isShared: false);

    /// <summary>
    /// Resolves <typeparamref name="TValue"/> as a shared request (stage 2) - the internal test seam
    /// Phase 3's own scope-reuse tests use before the public <c>[Shared]</c> attribute exists
    /// (Milestone 4). <paramref name="ordinal"/>/<paramref name="name"/> only affect path identity
    /// and diagnostic display, matching an ordinary constructor-parameter request.
    /// </summary>
    internal TValue ResolveSharedForTesting<TValue>(int ordinal, string name) =>
        ResolveCore<TValue>(Nullability.NotNullable, new PathSegment.ConstructorParameter(ordinal, name), isShared: true);

    private TValue ResolveCore<TValue>(Nullability nullability, PathSegment? segment, bool isShared)
    {
        var requestedType = typeof(TValue);
        var previousRandom = _random;
        var isRoot = _path is null;
        var checkpoint = _trace.Checkpoint;

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
                IsShared = isShared,
            };

            // Stage 1 (explicit values) has no mechanism until Milestone 3's public builder - every
            // request falls through it; nothing to trace.

            // Stage 2: shared/scoped values - only a request the caller marked IsShared reads from
            // scope; an ordinary request never does, even if the same type was already shared
            // elsewhere in this operation.
            if (request.IsShared)
            {
                if (_scope.TryGet(requestedType, out var sharedValue))
                {
                    var result = ValidateAuthoritativeValue(sharedValue, request, "shared value");
                    _trace.Record(PipelineStage.SharedOrScopedValue, OutcomeOf(result));
                    var value = Authoritative<TValue>(result);
                    _trace.Rewind(checkpoint);
                    return value;
                }

                _trace.Record(PipelineStage.SharedOrScopedValue, CompositionAttemptOutcome.NotHandled);
            }

            // Stage 3: exact registrations (mechanism only - no public builder.Register(...) yet).
            if (_registrations.TryGet(requestedType, out var registeredValue))
            {
                var result = ValidateAuthoritativeValue(registeredValue, request, "registration");
                _trace.Record(PipelineStage.ExactRegistration, OutcomeOf(result));
                var value = Authoritative<TValue>(result);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.ExactRegistration, CompositionAttemptOutcome.NotHandled);

            // Every Success recording below happens *after* StoreSharedAndReturn/Compose has already
            // returned without throwing - never before. StoreSharedAndReturn can still throw (a
            // shared request whose value fails ADR-0011's authoritative null/type validation), and a
            // collection/generated plan's Compose can throw if one of its own elements fails; a stage
            // whose outward call hasn't actually completed yet has no business being recorded as
            // "Success" in a trace a later BuildException might materialize.
            if (TryProviders(_profileProviders, request, out var profileValue))
            {
                var value = StoreSharedAndReturn<TValue>(profileValue, request);
                _trace.Record(PipelineStage.ProfileRule, CompositionAttemptOutcome.Success);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.ProfileRule, CompositionAttemptOutcome.NotHandled);

            if (TryProviders(_semanticProviders, request, out var semanticValue))
            {
                var value = StoreSharedAndReturn<TValue>(semanticValue, request);
                _trace.Record(PipelineStage.SemanticProvider, CompositionAttemptOutcome.Success);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.SemanticProvider, CompositionAttemptOutcome.NotHandled);

            if (TryProviders(_testDoubleProviders, request, out var testDoubleValue))
            {
                var value = StoreSharedAndReturn<TValue>(testDoubleValue, request);
                _trace.Record(PipelineStage.TestDoubleProvider, CompositionAttemptOutcome.Success);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.TestDoubleProvider, CompositionAttemptOutcome.NotHandled);

            if (TryProviders(_builtInProviders, request, out var builtInValue))
            {
                var value = StoreSharedAndReturn<TValue>(builtInValue, request);
                _trace.Record(PipelineStage.BuiltInProvider, CompositionAttemptOutcome.Success);
                _trace.Rewind(checkpoint);
                return value;
            }

            // Still stage 7 conceptually (docs/architecture.md) - a generated collection plan is
            // "a built-in value provider," just dispatched via a direct closed-generic field read
            // (like stage 8's PlanCache<TValue> below) rather than through ICompositionProvider,
            // which can't itself construct a generic collection without reflection or boxing/erasure.
            // See docs/adr/0014-generator-emitted-collection-plans.md.
            if (CollectionPlanCache<TValue>.Instance is { } collectionPlan)
            {
                var value = StoreSharedAndReturn<TValue>(collectionPlan.Compose(this), request);
                _trace.Record(PipelineStage.BuiltInProvider, CompositionAttemptOutcome.Success);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.BuiltInProvider, CompositionAttemptOutcome.NotHandled);

            var plan = PlanCache<TValue>.Instance;
            if (plan is not null)
            {
                var value = ResolveViaGeneratedPlan(plan, request);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.GeneratedPlan, CompositionAttemptOutcome.NotHandled);

            throw BuildException(
                requestedType,
                $"No registration, profile rule, semantic provider, test-double provider, built-in provider, or generated plan could satisfy '{requestedType.Name}'.");
        }
        finally
        {
            _path = _path.Pop();
            _random = previousRandom;
        }
    }

    // Stage 8: generated-plan dispatch - the only place recursion is checked, per
    // docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md. Stages 1-7 above
    // (explicit/shared/registration/profile/semantic/test-double/built-in) all get a chance to
    // terminate a self-referencing graph before this ever runs.
    private TValue ResolveViaGeneratedPlan<TValue>(ICompositionPlan<TValue> plan, CompositionRequest request)
    {
        var requestedType = request.RequestedType;

        if (_activeFrames.Contains(requestedType))
        {
            _trace.Record(PipelineStage.GeneratedPlan, CompositionAttemptOutcome.Failure);
            return Authoritative<TValue>(new CompositionResult.Failure(BuildCycleMessage(requestedType)));
        }

        _activeFrames.Add(requestedType);
        try
        {
            var value = plan.Compose(this);
            var result = request.IsShared
                ? StoreSharedAndReturn<TValue>(value, request)
                : value;
            _trace.Record(PipelineStage.GeneratedPlan, CompositionAttemptOutcome.Success);
            return result;
        }
        finally
        {
            _activeFrames.RemoveAt(_activeFrames.Count - 1);
        }
    }

    private string BuildCycleMessage(Type requestedType) =>
        $"Recursive composition detected while composing '{requestedType}': '{_path!.ToDisplayString()}' " +
        $"would construct '{requestedType}' again, which is already under construction. Use a registration " +
        "or a shared value to terminate the cycle.";

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

    // A non-shared request is untouched - an ordinary provider's output is only ever validated by
    // its own contract, never by the context. A request the caller marked IsShared is validated the
    // same way ValidateAuthoritativeValue already validates a scope/registration hit, *before* it
    // ever enters _scope - a bad first population must fail right here with a CompositionException,
    // not get cached and surface a confusing InvalidCastException/NullReferenceException later, on
    // whichever subsequent shared request happens to read it back out.
    private TValue StoreSharedAndReturn<TValue>(object? value, CompositionRequest request)
    {
        if (!request.IsShared)
            return CastResult<TValue>(value);

        var result = ValidateAuthoritativeValue(value, request, "shared value");
        if (result is CompositionResult.Success success)
            _scope.Set(request.RequestedType, success.Value);

        return Authoritative<TValue>(result);
    }

    // Stages 2/3's authoritative validation, per ADR-0011's second amendment: a null value for a
    // non-nullable request, or a value whose runtime type isn't assignable to RequestedType, is a
    // Failure at that stage - never silently passed through as NotHandled.
    private static CompositionResult ValidateAuthoritativeValue(object? value, CompositionRequest request, string source)
    {
        if (value is null)
        {
            return request.Nullability == Nullability.Nullable
                ? new CompositionResult.Success(null)
                : new CompositionResult.Failure(
                    $"The {source} for '{request.RequestedType}' is null, but '{request.RequestedType}' is not nullable.");
        }

        return request.RequestedType.IsInstanceOfType(value)
            ? new CompositionResult.Success(value)
            : new CompositionResult.Failure(
                $"The {source} for '{request.RequestedType}' produced a value of type '{value.GetType()}', " +
                "which is not assignable to the requested type.");
    }

    // The outward-facing conversion from a context-owned authoritative stage's CompositionResult to
    // either a plain TValue or a thrown CompositionException - the same NotHandled/Success/Failure ->
    // exception boundary stage 9 already uses, per docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md.
    private TValue Authoritative<TValue>(CompositionResult result) => result switch
    {
        CompositionResult.Success success => CastResult<TValue>(success.Value),
        CompositionResult.Failure failure => throw BuildException(typeof(TValue), failure.Message),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "An authoritative composition stage must produce Success or Failure."),
    };

    private static CompositionAttemptOutcome OutcomeOf(CompositionResult result) => result switch
    {
        CompositionResult.Success => CompositionAttemptOutcome.Success,
        CompositionResult.Failure => CompositionAttemptOutcome.Failure,
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "An authoritative composition stage must produce Success or Failure."),
    };

    // Materializes this operation's whole surviving trace (checkpoint 0 - every already-succeeded
    // sibling has already rewound itself out, per CompositionTraceBuffer's remarks) into a durable
    // CompositionDiagnostic. _path is always non-null here: this is only ever called from inside
    // ResolveCore's try block, after _path has been pushed for the current node.
    private CompositionException BuildException(Type failedType, string message) => new(new CompositionDiagnostic
    {
        RootType = _path!.RootType,
        FailedType = failedType,
        Path = _path.ToTreeString(),
        Trace = _trace.Slice(0),
        Seed = _seed.Value,
        Message = message,
    });

    // A provider-composed value is boxed for a value type TValue (CompositionResult.Success
    // carries object?) - this is the single unbox/cast point back to the generic caller's type.
    private static TValue CastResult<TValue>(object? value) => (TValue)value!;
}
