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
    private readonly IServiceProvider? _serviceProvider;
    private readonly CompositionScope _scope = new();
    private readonly List<Type> _activeFrames = [];
    private readonly List<Func<ICompositionContext, object?>> _activeFactories = [];
    private readonly List<ManualResolveFrame> _manualResolveFrames = [];
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
        : this(seed, registrations, serviceProvider: null)
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with the real stage-7 built-in providers, the given
    /// explicit stage-3 registrations and configured <c>IServiceProvider</c>, and the given explicit
    /// root seed - the shape <see cref="Composer.Create{T}"/>/<see cref="Composer.CreateMany{T}"/> use
    /// once a <see cref="CompositionBuilder"/> has been configured.
    /// </summary>
    internal CompositionContext(CompositionSeed seed, CompositionRegistrations registrations, IServiceProvider? serviceProvider)
        : this(seed, registrations, serviceProvider, profileProviders: [], semanticProviders: [], testDoubleProviders: [], builtInProviders: BuiltInProviders.Default)
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
        : this(CompositionSeed.Generate(), CompositionRegistrations.Empty, serviceProvider: null, profileProviders, semanticProviders, testDoubleProviders, builtInProviders)
    {
    }

    private CompositionContext(
        CompositionSeed seed,
        CompositionRegistrations registrations,
        IServiceProvider? serviceProvider,
        IReadOnlyList<ICompositionProvider> profileProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders)
    {
        _seed = seed;
        _registrations = registrations;
        _serviceProvider = serviceProvider;
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

    /// <inheritdoc />
    public TValue Resolve<TValue>()
    {
        if (_manualResolveFrames.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(ICompositionContext)}.{nameof(Resolve)}<{typeof(TValue).Name}>() with no descriptor can only " +
                "be called from inside a registration or configuration-rule factory.");
        }

        var frame = _manualResolveFrames[^1];
        var segment = new PathSegment.ManualResolve(frame.NextOrdinal++);
        return ResolveCore<TValue>(Nullability.NotNullable, segment, isShared: false);
    }

    /// <summary>
    /// Resolves the root value of this composition operation - the entry point
    /// <see cref="Composer.Create{T}"/> uses, distinct from the descriptor-based
    /// <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> generated code uses. Both funnel into the same pipeline
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
            // elsewhere in this operation. Not an ICompositionProvider, so Provider is null.
            if (request.IsShared)
            {
                if (_scope.TryGet(requestedType, out var sharedValue))
                {
                    var result = ValidateAuthoritativeValue(sharedValue, request, "shared value");
                    _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, OutcomeOf(result));
                    var value = Authoritative<TValue>(result);
                    _trace.Rewind(checkpoint);
                    return value;
                }

                _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, CompositionAttemptOutcome.NotHandled);
            }

            // Stage 3, sub-step (a): exact registrations. A registration factory is invoked through
            // InvokeFactory, which owns the manual-resolve invocation frame and the factory-reentrance
            // guard - never called directly.
            if (_registrations.TryGet(requestedType, out var factory))
            {
                var registeredValue = InvokeFactory(factory, requestedType);
                var result = ValidateAuthoritativeValue(registeredValue, request, "registration");
                _trace.Record(PipelineStage.ExactRegistration, provider: null, OutcomeOf(result));
                var value = Authoritative<TValue>(result);
                _trace.Rewind(checkpoint);
                return value;
            }

            // Stage 3, sub-step (b): the configured IServiceProvider fallback, tried only on an exact-
            // registration miss - never the other way around. Per
            // docs/adr/0019-registrations-and-service-provider-injection.md: null means "unresolved"
            // and falls through to stage 4 (not authoritative, unlike an ordinary registration's null),
            // a thrown exception is authoritative and never downgraded to NotHandled, and a non-null
            // wrongly-typed result is a structured CompositionException rather than an unchecked cast.
            if (_serviceProvider is not null)
            {
                object? serviceValue;
                try
                {
                    serviceValue = _serviceProvider.GetService(requestedType);
                }
                catch (Exception ex)
                {
                    _trace.Record(PipelineStage.ExactRegistration, provider: null, CompositionAttemptOutcome.Failure);
                    throw BuildException(
                        requestedType,
                        $"The configured IServiceProvider threw while resolving '{CompositionPath.FriendlyTypeName(requestedType)}': {ex.Message}",
                        ex);
                }

                if (serviceValue is not null)
                {
                    var result = ValidateAuthoritativeValue(serviceValue, request, "configured IServiceProvider");
                    _trace.Record(PipelineStage.ExactRegistration, provider: null, OutcomeOf(result));
                    var value = Authoritative<TValue>(result);
                    _trace.Rewind(checkpoint);
                    return value;
                }
            }

            _trace.Record(PipelineStage.ExactRegistration, provider: null, CompositionAttemptOutcome.NotHandled);

            // TryProviders records one NotHandled entry per provider it actually tries, tagged with
            // that provider's own concrete type (PR #13 review: stage 7 alone has three real
            // providers today, not a hypothetical future case - a single aggregate entry can't tell
            // them apart). The eventual winning provider's own outcome (Success, or Failure if a
            // shared request's value fails validation) is recorded by StoreSharedAndReturn itself,
            // tagged with that same provider type - never by this method returning first and the
            // caller recording second, which is exactly the ordering bug (PR #13 review, prior
            // rounds) that let a validation failure throw before anything got recorded at all.
            if (TryProviders(_profileProviders, PipelineStage.ProfileRule, request, out var profileValue, out var profileProvider))
            {
                var value = StoreSharedAndReturn<TValue>(profileValue, request, PipelineStage.ProfileRule, profileProvider);
                _trace.Rewind(checkpoint);
                return value;
            }

            if (TryProviders(_semanticProviders, PipelineStage.SemanticProvider, request, out var semanticValue, out var semanticProvider))
            {
                var value = StoreSharedAndReturn<TValue>(semanticValue, request, PipelineStage.SemanticProvider, semanticProvider);
                _trace.Rewind(checkpoint);
                return value;
            }

            if (TryProviders(_testDoubleProviders, PipelineStage.TestDoubleProvider, request, out var testDoubleValue, out var testDoubleProvider))
            {
                var value = StoreSharedAndReturn<TValue>(testDoubleValue, request, PipelineStage.TestDoubleProvider, testDoubleProvider);
                _trace.Rewind(checkpoint);
                return value;
            }

            if (TryProviders(_builtInProviders, PipelineStage.BuiltInProvider, request, out var builtInValue, out var builtInProvider))
            {
                var value = StoreSharedAndReturn<TValue>(builtInValue, request, PipelineStage.BuiltInProvider, builtInProvider);
                _trace.Rewind(checkpoint);
                return value;
            }

            // Still stage 7 conceptually (docs/architecture.md) - a generated collection plan is
            // "a built-in value provider," just dispatched via a direct closed-generic field read
            // (like stage 8's PlanCache<TValue> below) rather than through ICompositionProvider,
            // which can't itself construct a generic collection without reflection or boxing/erasure
            // (so Provider is null here too - CollectionPlanCache<T> holds an ICompositionPlan<T>,
            // not an ICompositionProvider instance). See docs/adr/0014-generator-emitted-collection-plans.md.
            if (CollectionPlanCache<TValue>.Instance is { } collectionPlan)
            {
                // Recorded *before* Compose runs: if an element inside this collection fails, this
                // entry - not a Success or a NotHandled - is what should survive in a failing
                // descendant's materialized trace, showing this ancestor genuinely entered
                // collection-plan dispatch rather than looking like it was never tried (PR #13 review).
                _trace.Record(PipelineStage.BuiltInProvider, provider: null, CompositionAttemptOutcome.Pending);
                var value = StoreSharedAndReturn<TValue>(collectionPlan.Compose(this), request, PipelineStage.BuiltInProvider, provider: null);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.BuiltInProvider, provider: null, CompositionAttemptOutcome.NotHandled);

            var plan = PlanCache<TValue>.Instance;
            if (plan is not null)
            {
                var value = ResolveViaGeneratedPlan(plan, request);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.GeneratedPlan, provider: null, CompositionAttemptOutcome.NotHandled);

            throw BuildException(
                requestedType,
                $"No registration, profile rule, semantic provider, test-double provider, built-in provider, or generated plan could satisfy '{CompositionPath.FriendlyTypeName(requestedType)}'.");
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
    // terminate a self-referencing graph before this ever runs. Not an ICompositionProvider (it's
    // ICompositionPlan<T> dispatch via PlanCache<T>), so Provider is null throughout.
    private TValue ResolveViaGeneratedPlan<TValue>(ICompositionPlan<TValue> plan, in CompositionRequest request)
    {
        var requestedType = request.RequestedType;

        if (_activeFrames.Contains(requestedType))
        {
            _trace.Record(PipelineStage.GeneratedPlan, provider: null, CompositionAttemptOutcome.Failure);
            return Authoritative<TValue>(new CompositionResult.Failure(BuildCycleMessage(requestedType)));
        }

        _activeFrames.Add(requestedType);
        try
        {
            // Same reasoning as the collection-dispatch Pending recording above: this ancestor's
            // generated-plan dispatch genuinely started before any descendant could fail, so it
            // shouldn't be silently absent from a failing descendant's materialized trace.
            _trace.Record(PipelineStage.GeneratedPlan, provider: null, CompositionAttemptOutcome.Pending);
            var value = plan.Compose(this);

            if (!request.IsShared)
            {
                _trace.Record(PipelineStage.GeneratedPlan, provider: null, CompositionAttemptOutcome.Success);
                return value;
            }

            // StoreSharedAndReturn records this stage's real outcome (Success or Failure) itself -
            // not a second, separate recording here - so a shared value that fails validation still
            // gets an entry even though Authoritative throws before returning.
            return StoreSharedAndReturn<TValue>(value, request, PipelineStage.GeneratedPlan, provider: null);
        }
        finally
        {
            _activeFrames.RemoveAt(_activeFrames.Count - 1);
        }
    }

    // Invokes a registration or configuration-rule factory - the single point every stage-3/stage-4
    // factory call goes through. Owns two mechanisms, both per docs/adr/0019-registrations-and-
    // service-provider-injection.md: (1) a manual-resolve invocation frame, pushed immediately before
    // the call and popped in finally, giving every descriptor-less Resolve<T>() call made during this
    // one invocation a shared, call-sequence-ordinal-keyed ManualResolve path segment; (2) a factory-
    // reentrance guard, keyed by the exact delegate instance about to be invoked (never by requested
    // type - that would reject the legitimate Node.Child-style cycle-terminating pattern) - a self-
    // referencing factory that calls back into resolving a request routing to itself is caught here,
    // as a diagnosable CompositionException, instead of recursing to a StackOverflowException.
    private object? InvokeFactory(Func<ICompositionContext, object?> factory, Type requestedType)
    {
        if (_activeFactories.Exists(active => ReferenceEquals(active, factory)))
        {
            _trace.Record(PipelineStage.ExactRegistration, provider: null, CompositionAttemptOutcome.Failure);
            throw BuildException(requestedType, BuildFactoryReentranceMessage(requestedType));
        }

        _activeFactories.Add(factory);
        _manualResolveFrames.Add(new ManualResolveFrame());
        try
        {
            return factory(this);
        }
        finally
        {
            _manualResolveFrames.RemoveAt(_manualResolveFrames.Count - 1);
            _activeFactories.RemoveAt(_activeFactories.Count - 1);
        }
    }

    private string BuildFactoryReentranceMessage(Type requestedType) =>
        $"Recursive registration or configuration-rule factory detected while composing " +
        $"'{CompositionPath.FriendlyTypeName(requestedType)}': '{_path!.ToDisplayString()}' would invoke the same " +
        "factory again, which is already in progress. Use a different registration/rule, or terminate the " +
        "recursion inside the factory itself (e.g. by returning a value that doesn't call Resolve<T>() for this type).";

    // One mutable counter per active manual-resolve invocation frame - shared and incremented by
    // every descriptor-less Resolve<T>() call made during that one factory invocation, per ADR-0019.
    private sealed class ManualResolveFrame
    {
        internal int NextOrdinal;
    }

    private string BuildCycleMessage(Type requestedType) =>
        $"Recursive composition detected while composing '{requestedType}': '{_path!.ToDisplayString()}' " +
        $"would construct '{requestedType}' again, which is already under construction. Use a registration " +
        "or a shared value to terminate the cycle.";

    // Records one NotHandled entry per provider actually tried, tagged with that provider's own
    // concrete type (PR #13 review) - not one aggregate entry for the whole stage, and not the
    // provider instance itself (ProviderAttempt stays a value-type struct; the Type is enough to
    // identify which provider without holding a live reference to it). An ordinary
    // ICompositionProvider can only ever decline (NotHandled) or hand back a value (Success, still
    // unvalidated at this point), so every iteration here that doesn't return true is a real,
    // completed NotHandled outcome, safe to record immediately. The winning provider's own outcome
    // is deliberately left unrecorded here - it's still unvalidated (StoreSharedAndReturn hasn't run
    // yet) - but its identity is handed back via `provider` so the caller can tag
    // that eventual recording correctly.
    //
    // ICompositionProvider.TryCompose is handed the live context, so a provider is free to call
    // context.Resolve<T>() itself to compose part of its value (no built-in provider does today, but
    // nothing about the contract forbids it, and a Milestone 3/5/6 profile/semantic/test-double
    // provider is exactly the kind of extension point expected to) - if that nested resolution
    // fails, BuildException materializes the trace while this candidate's own TryCompose call is
    // still on the stack. Same ancestor-visibility gap the Pending marker already closes for stage
    // 8/collection-plan dispatch (PR #13 review): record Pending immediately before the call, so a
    // nested failure still shows this provider's own in-flight attempt instead of omitting it
    // entirely, and rewind that marker away once TryCompose actually returns (success or decline) -
    // it's superseded at that point by either the winning-provider return below or the definitive
    // NotHandled recorded right after.
    private bool TryProviders(
        IReadOnlyList<ICompositionProvider> providers, PipelineStage stage, in CompositionRequest request, out object? value, out Type? provider)
    {
        foreach (var candidate in providers)
        {
            var checkpoint = _trace.Checkpoint;
            _trace.Record(stage, candidate.GetType(), CompositionAttemptOutcome.Pending);
            var result = candidate.TryCompose(request, this);
            _trace.Rewind(checkpoint);

            if (result is CompositionResult.Success success)
            {
                value = success.Value;
                provider = candidate.GetType();
                return true;
            }

            _trace.Record(stage, candidate.GetType(), CompositionAttemptOutcome.NotHandled);
        }

        value = null;
        provider = null;
        return false;
    }

    // Records this stage's real outcome itself (Success, or Failure if a shared request's value
    // fails ADR-0011's authoritative validation) - the single point of truth for what actually
    // happened at `stage`/`provider`, so a validation failure that
    // throws via Authoritative still leaves a real trace entry instead of silently having none (PR
    // #13 review: the previous shape recorded Success only after this method returned, which a
    // thrown exception never reaches). A non-shared request's output is only ever validated by its
    // own provider's contract, never by the context, so it's always Success here.
    private TValue StoreSharedAndReturn<TValue>(object? value, in CompositionRequest request, PipelineStage stage, Type? provider)
    {
        if (!request.IsShared)
        {
            _trace.Record(stage, provider, CompositionAttemptOutcome.Success);
            return CastResult<TValue>(value);
        }

        var result = ValidateAuthoritativeValue(value, request, "shared value");
        _trace.Record(stage, provider, OutcomeOf(result));
        if (result is CompositionResult.Success success)
            _scope.Set(request.RequestedType, success.Value);

        return Authoritative<TValue>(result);
    }

    // Stages 2/3's authoritative validation, per ADR-0011's second amendment: a null value for a
    // non-nullable request, or a value whose runtime type isn't assignable to RequestedType, is a
    // Failure at that stage - never silently passed through as NotHandled.
    private static CompositionResult ValidateAuthoritativeValue(object? value, in CompositionRequest request, string source)
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
    private CompositionException BuildException(Type failedType, string message) => new(BuildDiagnostic(failedType, message));

    // The IServiceProvider fallback's throwing-container case is the only stage that needs to preserve
    // an original exception as InnerException (never `throw ex;`) - every other authoritative-stage
    // failure has no prior exception to preserve.
    private CompositionException BuildException(Type failedType, string message, Exception innerException) =>
        new(BuildDiagnostic(failedType, message), innerException);

    private CompositionDiagnostic BuildDiagnostic(Type failedType, string message) => new()
    {
        RootType = _path!.RootType,
        FailedType = failedType,
        Path = _path.ToTreeString(),
        Trace = _trace.Slice(0),
        Seed = _seed.Value,
        Message = message,
    };

    // A provider-composed value is boxed for a value type TValue (CompositionResult.Success
    // carries object?) - this is the single unbox/cast point back to the generic caller's type.
    private static TValue CastResult<TValue>(object? value) => (TValue)value!;
}
