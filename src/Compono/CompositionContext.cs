using Compono.Providers;

namespace Compono;

/// <summary>
/// Coordinates one root composition operation - the fixed 9-stage resolution pipeline
/// (<c>docs/architecture.md</c>), path tracking, and dispatch into generated plans.
/// </summary>
/// <remarks>
/// One instance per root operation (one <see cref="Composer.Create{T}"/> call, one item of a
/// <see cref="Composer.CreateMany{T}"/> call, or one <see cref="Composer.CreateRow"/> row) - never
/// reused across multiple root operations, per
/// <c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>. Stage 1 (explicit
/// values) has no pipeline mechanism - a test-framework integration's inline theory values are
/// handled entirely above this pipeline, per
/// <c>docs/adr/0022-compono-xunit-package-design.md</c>. Stages 2/3 (shared/scoped values, exact
/// registrations) and the active-construction-frame recursion check are implemented as of Milestone 2
/// Phase 3; stage 2's read side became unconditional (any request, not just one explicitly marked
/// shared, checks scope for a match) per
/// <c>docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md</c>. Stage 8
/// (generated-plan dispatch via <see cref="PlanCache{T}"/>) is unchanged from Milestone 1.
/// </remarks>
internal sealed class CompositionContext : ICompositionContext
{
    private readonly CompositionSeed _seed;
    private readonly CompositionRegistrations _registrations;
    private readonly IServiceProvider? _serviceProvider;
    private readonly CompositionScope _scope = new();
    private readonly List<Type> _activeFrames = [];
    private readonly List<Func<ICompositionContext, object?>> _activeFactories = [];
    private readonly List<(ICompositionValueProvider Provider, Type RequestedType)> _activeProviderRequests = [];
    private readonly List<ManualResolveFrame> _manualResolveFrames = [];
    private readonly CompositionTraceBuffer _trace = new();
    private readonly IReadOnlyList<ICompositionProvider> _configurationRuleProviders;
    private readonly IReadOnlyList<ICompositionProvider> _semanticProviders;
    private readonly IReadOnlyList<ICompositionProvider> _testDoubleProviders;
    private readonly IReadOnlyList<ICompositionProvider> _builtInProviders;
    private readonly CollectionSizePolicy _collectionSizePolicy;
    private readonly IReadOnlySet<Type> _sharedTypes;

    // One shared counter per CompositionContext (one per row) - every TryResolveConfigured call gets
    // the next ordinal, same shape as ManualResolveFrame.NextOrdinal, so sequential sibling calls fork
    // distinct random states instead of colliding on an identical segment identity (PR #105 review).
    private int _nextConfiguredResolutionOrdinal;

    // A cheap, otherwise-empty identity token BuildException stamps onto every CompositionException it
    // creates - InvokeFactory compares a caught exception's own token against this one to tell "my own
    // nested Resolve<T>() call diagnosed this" from "a different CompositionContext (or none at all)
    // diagnosed this," without holding a live reference to this whole context (and everything it
    // reaches - registrations, scope, service provider, trace buffer) from inside the exception itself.
    private readonly object _identity = new();

    private CompositionPath? _path;
    private IRandomSource? _random;
    private Type? _currentDeclaringType;

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
    /// explicit stage-3 registrations and configured <c>IServiceProvider</c>, no configuration rules,
    /// the built-in collection-size policy, and the given explicit root seed - the seam
    /// <c>Compono.Tests</c> uses to exercise stage 3 directly.
    /// </summary>
    internal CompositionContext(CompositionSeed seed, CompositionRegistrations registrations, IServiceProvider? serviceProvider)
        : this(seed, registrations, serviceProvider, configurationRuleProviders: [], semanticProviders: [], testDoubleProviders: [], CollectionSizePolicy.Empty)
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with the real stage-7 built-in providers, the given
    /// explicit stage-3 registrations and configured <c>IServiceProvider</c>, the given compiled
    /// stage-4/5/6 provider lists and collection-size policy, the given explicit root seed, and the
    /// given <see cref="CompositionBuilder.Share{T}"/>-declared shared types - the shape
    /// <see cref="Composer.Create{T}"/>/<see cref="Composer.CreateMany{T}"/> use once a
    /// <see cref="CompositionBuilder"/> has been configured.
    /// </summary>
    internal CompositionContext(
        CompositionSeed seed,
        CompositionRegistrations registrations,
        IServiceProvider? serviceProvider,
        IReadOnlyList<ICompositionProvider> configurationRuleProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        CollectionSizePolicy collectionSizePolicy,
        IReadOnlySet<Type>? sharedTypes = null)
        : this(seed, registrations, serviceProvider, configurationRuleProviders, semanticProviders, testDoubleProviders, builtInProviders: BuiltInProviders.Default, collectionSizePolicy, sharedTypes)
    {
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with its path pre-rooted at <paramref name="rootType"/>
    /// instead of leaving it for the first <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/>
    /// call to claim as root - the shape <see cref="Composer.CreateRow"/> uses so several sibling
    /// top-level requests (e.g. one theory row's own method parameters) each fork independently, as
    /// children of the same pre-established root, rather than each being treated as its own root and
    /// colliding on an identical seed-derived random stream. See
    /// <c>docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md</c>.
    /// </summary>
    internal CompositionContext(
        CompositionSeed seed,
        CompositionRegistrations registrations,
        IServiceProvider? serviceProvider,
        IReadOnlyList<ICompositionProvider> configurationRuleProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        CollectionSizePolicy collectionSizePolicy,
        Type rootType,
        IReadOnlySet<Type>? sharedTypes = null)
        : this(seed, registrations, serviceProvider, configurationRuleProviders, semanticProviders, testDoubleProviders, collectionSizePolicy, sharedTypes)
    {
        _path = CompositionPath.Root(rootType);
        _random = RandomSource.FromSeed(seed);
    }

    /// <summary>
    /// Creates a <see cref="CompositionContext"/> with explicit providers per extensible pipeline
    /// stage and a freshly generated root seed - the seam <c>Compono.Tests</c> uses to inject fake
    /// providers and assert pipeline ordering, since no public configuration surface exists until
    /// Milestone 5/6.
    /// </summary>
    internal CompositionContext(
        IReadOnlyList<ICompositionProvider> configurationRuleProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders)
        : this(CompositionSeed.Generate(), CompositionRegistrations.Empty, serviceProvider: null, configurationRuleProviders, semanticProviders, testDoubleProviders, builtInProviders, CollectionSizePolicy.Empty)
    {
    }

    private CompositionContext(
        CompositionSeed seed,
        CompositionRegistrations registrations,
        IServiceProvider? serviceProvider,
        IReadOnlyList<ICompositionProvider> configurationRuleProviders,
        IReadOnlyList<ICompositionProvider> semanticProviders,
        IReadOnlyList<ICompositionProvider> testDoubleProviders,
        IReadOnlyList<ICompositionProvider> builtInProviders,
        CollectionSizePolicy collectionSizePolicy,
        IReadOnlySet<Type>? sharedTypes = null)
    {
        _seed = seed;
        _registrations = registrations;
        _serviceProvider = serviceProvider;
        _configurationRuleProviders = configurationRuleProviders;
        _semanticProviders = semanticProviders;
        _testDoubleProviders = testDoubleProviders;
        _builtInProviders = builtInProviders;
        _collectionSizePolicy = collectionSizePolicy;
        _sharedTypes = sharedTypes ?? (IReadOnlySet<Type>)new HashSet<Type>();
    }

    /// <summary>
    /// The current node's forked random source - internal test-observability seam for Phase 1's own
    /// determinism tests (via a capturing <see cref="ICompositionPlan{T}"/>). Milestone 2 Phase 2's
    /// built-in providers are the first real consumer of generated values.
    /// </summary>
    internal IRandomSource Random =>
        _random ?? throw new InvalidOperationException("No composition operation is currently in progress.");

    /// <inheritdoc />
    public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor) =>
        ResolveCore<TValue>(descriptor.Nullability, descriptor.DeclaringType, BuildSegment(descriptor), isShared: false);

    /// <summary>
    /// Resolves <typeparamref name="TValue"/> for a <see cref="CompositionRequestKind.TestParameter"/>
    /// descriptor as a shared request (stage 2's write side) - the internal member backing
    /// <see cref="CompositionRow.ResolveShared{TValue}(in CompositionRequestDescriptor)"/>. Composes
    /// through the same pipeline <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> uses,
    /// but additionally stores the successful result into this context's scope so a later request for
    /// the same type - including one made by a nested generated plan - reuses it. See
    /// <c>docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md</c>.
    /// </summary>
    internal TValue ResolveDescriptorAsShared<TValue>(in CompositionRequestDescriptor descriptor) =>
        ResolveCore<TValue>(descriptor.Nullability, descriptor.DeclaringType, BuildSegment(descriptor), isShared: true);

    /// <summary>
    /// Stores <paramref name="value"/> - already known, not composed - as this context's shared value
    /// for <typeparamref name="TValue"/>, after the exact same authoritative validation a successful
    /// <see cref="ResolveDescriptorAsShared{TValue}(in CompositionRequestDescriptor)"/> pipeline result
    /// gets. No pipeline dispatch, no path/random-fork bookkeeping - there is nothing left to compose.
    /// The internal member backing
    /// <see cref="CompositionRow.ShareExplicit{TValue}(in CompositionRequestDescriptor, TValue)"/>.
    /// </summary>
    /// <exception cref="CompositionException">
    /// <paramref name="value"/> is <see langword="null"/> for a non-nullable request, or its runtime
    /// type isn't assignable to <typeparamref name="TValue"/>; or a shared value for
    /// <typeparamref name="TValue"/> has already been established in this row.
    /// </exception>
    internal void ShareExplicitTestParameter<TValue>(in CompositionRequestDescriptor descriptor, TValue value)
    {
        var requestedType = typeof(TValue);

        // Belt-and-suspenders per ADR-0022 - see the matching check in ResolveCore's stage-2 read.
        if (_scope.TryGet(requestedType, out _))
        {
            throw BuildException(
                requestedType,
                $"A shared value for '{CompositionPath.FriendlyTypeName(requestedType)}' has already been " +
                "established in this row - only one shared value per type is allowed.");
        }

        var result = ValidateAuthoritativeValue(value, requestedType, descriptor.Nullability, "explicit value");

        if (result is CompositionResult.Failure failure)
            throw BuildException(requestedType, failure.Message);

        StoreSharedValue(requestedType, isShared: true, result);
    }

    // Shared by Resolve<TValue>(descriptor) and ResolveDescriptorAsShared<TValue> - the only
    // difference between an ordinary and a shared descriptor-based request is the isShared flag each
    // passes to ResolveCore, never how the descriptor's own Kind maps to a PathSegment.
    private static PathSegment BuildSegment(in CompositionRequestDescriptor descriptor) => descriptor.Kind switch
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
        CompositionRequestKind.TestParameter =>
            new PathSegment.TestParameter(descriptor.Ordinal, descriptor.Name),
        _ => throw new ArgumentOutOfRangeException(nameof(descriptor), descriptor.Kind, "Unrecognized composition request kind."),
    };

    /// <inheritdoc />
    public TValue Resolve<TValue>()
    {
        if (_manualResolveFrames.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(ICompositionContext)}.{nameof(Resolve)}<{typeof(TValue).Name}>() with no descriptor can only " +
                "be called from inside a registration or configuration-rule factory, or a public " +
                $"{nameof(ICompositionValueProvider)}.{nameof(ICompositionValueProvider.TryProvide)} invocation.");
        }

        var frame = _manualResolveFrames[^1];
        var segment = new PathSegment.ManualResolve(frame.NextOrdinal++);
        return ResolveCore<TValue>(Nullability.NotNullable, declaringType: null, segment, isShared: false);
    }

    /// <inheritdoc />
    public int DeriveSeed()
    {
        if (_manualResolveFrames.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(ICompositionContext)}.{nameof(DeriveSeed)}() can only be called from inside a " +
                "registration or configuration-rule factory, or a public " +
                $"{nameof(ICompositionValueProvider)}.{nameof(ICompositionValueProvider.TryProvide)} invocation.");
        }

        // _random is always non-null here: a manual-resolve frame only exists while InvokeFactory/
        // InvokeProvider is running, and both are only ever called from inside ResolveCore's try block,
        // which has already set _random for the request currently being resolved.
        var raw = _random!.DeriveSeed();

        // Folds the full 64-bit derived value into an int by XORing its two halves, rather than
        // truncating to the low 32 bits alone - keeps entropy from both halves instead of silently
        // discarding the high bits.
        return unchecked((int)(raw ^ (raw >> 32)));
    }

    /// <inheritdoc />
    public int ResolveCollectionSize() => ResolveCollectionSizeCore();

    /// <summary>
    /// Resolves the root value of this composition operation - the entry point
    /// <see cref="Composer.Create{T}"/> uses, distinct from the descriptor-based
    /// <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> generated code uses. Both funnel into the same pipeline
    /// execution, so the root type is resolved identically to any nested type.
    /// </summary>
    internal TValue ResolveRoot<TValue>() => ResolveCore<TValue>(Nullability.NotNullable, declaringType: null, segment: null, isShared: false);

    /// <summary>
    /// Resolves <typeparamref name="TValue"/> as a shared request (stage 2) - the internal test seam
    /// Phase 3's own scope-reuse tests use before the public <c>[Shared]</c> attribute exists
    /// (Milestone 4). <paramref name="ordinal"/>/<paramref name="name"/> only affect path identity
    /// and diagnostic display, matching an ordinary constructor-parameter request.
    /// </summary>
    internal TValue ResolveSharedForTesting<TValue>(int ordinal, string name) =>
        ResolveCore<TValue>(Nullability.NotNullable, declaringType: null, new PathSegment.ConstructorParameter(ordinal, name), isShared: true);

    /// <summary>
    /// The internal implementation backing <see cref="CompositionRow.TryResolveConfigured"/> - reaches
    /// stage 2 (scope), stage 3a (exact registrations), and stages 4-6 (configuration rules, semantic
    /// providers, test-double providers) only. Never stage 3b (the configured <c>IServiceProvider</c>)
    /// or stages 7-8 (built-in/collection/generated-plan dispatch, which require the requested type
    /// known at compile time via <see cref="PlanCache{T}"/>/<see cref="CollectionPlanCache{T}"/> -
    /// reaching them from a runtime <see cref="Type"/> would need reflection, which
    /// <c>docs/adr/0001-source-generation-first.md</c> rules out by default). See
    /// <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>. A
    /// builder-configured <see cref="CompositionBuilder.Share{T}"/> type participates here exactly
    /// like any other entry point - both establishing and observing the shared scope value,
    /// regardless of resolution order relative to other requests in the same row. See
    /// <c>docs/adr/0056-composition-builder-share-graph-wide-sharing.md</c>.
    /// </summary>
    /// <remarks>
    /// A bare runtime <see cref="Type"/> carries no compile-time nullable-reference-type annotation to
    /// validate against - unlike every other entry point above, whose <c>Nullability</c> comes from a
    /// generic <c>TValue</c>'s or a descriptor's compile-time-known annotation. This method always
    /// validates as <see cref="Nullability.Nullable"/>, matching
    /// <see cref="IServiceProvider"/>.<c>GetService(Type)</c>'s own null-friendly BCL contract, which
    /// this method exists to back - a scope value, registration, or provider that legitimately produces
    /// <see langword="null"/> is accepted here unconditionally, never rejected for "non-nullability"
    /// that has no meaning at this call's own type boundary. A reachable stage that fails does not
    /// fail uniformly: an exact registration or configuration-rule factory's own thrown exception is
    /// wrapped into a diagnosed <see cref="CompositionException"/> (via <see cref="InvokeFactory"/>), but a stage 4-6
    /// <see cref="ICompositionValueProvider"/>'s own thrown exception propagates uncaught, unwrapped,
    /// exactly like every other caller of <see cref="TryProviders"/> - per
    /// <c>docs/adr/0024-public-provider-extensibility-model.md</c>'s Provider Failure Semantics, never
    /// downgraded or reinterpreted for this entry point specifically.
    /// </remarks>
    internal bool TryResolveConfigured(Type requestedType, out object? value)
    {
        var previousRandom = _random;
        var previousDeclaringType = _currentDeclaringType;
        var isRoot = _path is null;
        var segment = new PathSegment.ConfiguredResolution(_nextConfiguredResolutionOrdinal++);
        var checkpoint = _trace.Checkpoint;
        // Whether this call is nested inside another registration/configuration-rule factory or public
        // provider's own invocation (a manual-resolve frame is only ever active during those - see
        // InvokeFactory/InvokeProvider) - if so, an enclosing operation's own exception handling still
        // needs every trace entry recorded here, so the catch clause below must not rewind them away.
        var isNestedInAnotherInvocation = _manualResolveFrames.Count > 0;

        _path = isRoot ? CompositionPath.Root(requestedType) : _path!.Push(requestedType, segment);
        _random = isRoot ? RandomSource.FromSeed(_seed) : previousRandom!.Fork(segment);
        _currentDeclaringType = null;

        try
        {
            // A builder-configured Share<T>() type broadens this entry point's write side exactly
            // like ResolveCore's own effectiveIsShared - this bridge (Compono.DependencyInjection's
            // AsServiceProvider(), via CompositionRow.TryResolveConfigured) is still just another
            // participant in the same row's graph, so a Share<T>()-configured type resolved through
            // it must establish/observe the same scope value as any other request, regardless of
            // resolution order. See docs/adr/0056-composition-builder-share-graph-wide-sharing.md.
            var effectiveIsShared = _sharedTypes.Contains(requestedType);
            var request = new CompositionRequest
            {
                RequestedType = requestedType,
                Nullability = Nullability.Nullable,
                DeclaringType = null,
                Path = _path,
                IsShared = effectiveIsShared,
            };

            // Stage 2: same unconditional scope read every other entry point uses - a value already
            // shared elsewhere in this row (via ordinary [Shared]/ResolveShared/Share<T>() usage) is
            // surfaced here too, per ADR-0021/ADR-0056.
            if (_scope.TryGet(requestedType, out var sharedValue))
            {
                var result = ValidateAuthoritativeValue(sharedValue, request, "shared value");
                _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, OutcomeOf(result));
                value = AuthoritativeValue(result, requestedType);
                _trace.Rewind(checkpoint);
                return true;
            }

            _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, CompositionAttemptOutcome.NotHandled);

            // Stage 3, sub-step (a) only - deliberately skips sub-step (b) (the configured
            // IServiceProvider): see this method's own XML doc and ADR-0047's Recursion section for why.
            if (_registrations.TryGet(requestedType, out var factory))
            {
                _trace.Record(PipelineStage.ExactRegistration, provider: null, CompositionAttemptOutcome.Pending);
                var registeredValue = InvokeFactory(factory, requestedType, PipelineStage.ExactRegistration, provider: null);
                var result = ValidateAuthoritativeValue(registeredValue, request, "registration");
                _trace.Record(PipelineStage.ExactRegistration, provider: null, OutcomeOf(result));
                StoreSharedValue(requestedType, effectiveIsShared, result);
                value = AuthoritativeValue(result, requestedType);
                _trace.Rewind(checkpoint);
                return true;
            }

            _trace.Record(PipelineStage.ExactRegistration, provider: null, CompositionAttemptOutcome.NotHandled);

            // Stages 4-6, unchanged from ResolveCore - the exact same TryProviders dispatch, still
            // records its own Pending/NotHandled/winning-candidate trace entries per candidate.
            if (TryProviders(_configurationRuleProviders, PipelineStage.ConfigurationRule, request, out var configurationRuleValue, out var configurationRuleProvider))
            {
                value = ValidateProviderResultAndReturn(configurationRuleValue, request, PipelineStage.ConfigurationRule, configurationRuleProvider, requestedType);
                _trace.Rewind(checkpoint);
                return true;
            }

            if (TryProviders(_semanticProviders, PipelineStage.SemanticProvider, request, out var semanticValue, out var semanticProvider))
            {
                value = ValidateProviderResultAndReturn(semanticValue, request, PipelineStage.SemanticProvider, semanticProvider, requestedType);
                _trace.Rewind(checkpoint);
                return true;
            }

            if (TryProviders(_testDoubleProviders, PipelineStage.TestDoubleProvider, request, out var testDoubleValue, out var testDoubleProvider))
            {
                value = ValidateProviderResultAndReturn(testDoubleValue, request, PipelineStage.TestDoubleProvider, testDoubleProvider, requestedType);
                _trace.Rewind(checkpoint);
                return true;
            }

            // Nothing in the reachable stages could handle it - a genuine miss, not a failure. Never
            // throws here: this is exactly what lets the caller (Compono.DependencyInjection's adapter)
            // back IServiceProvider.GetService(Type)'s null-on-miss contract.
            value = null;
            _trace.Rewind(checkpoint);
            return false;
        }
        // Every non-exceptional path above already rewinds the trace itself before returning. Without
        // this, an exception thrown here (a stage 3a factory's wrapped CompositionException, or a stage
        // 4-6 provider's own raw, unwrapped exception per this method's XML doc) left every attempt
        // recorded since checkpoint sitting in _trace with nothing to ever rewind them - BuildDiagnostic
        // slices from index 0, so the next unrelated failing call on this same row would have picked up
        // this orphaned batch too (PR #105 review). Only rewind for a genuinely top-level call, though -
        // if this was reached from inside another factory/provider's own invocation
        // (isNestedInAnotherInvocation), that enclosing operation's own exception handling still needs
        // these entries for its own diagnostic; rewinding here would erase them out from under it.
        catch when (!isNestedInAnotherInvocation)
        {
            _trace.Rewind(checkpoint);
            throw;
        }
        finally
        {
            _path = _path.Pop();
            _random = previousRandom;
            _currentDeclaringType = previousDeclaringType;
        }
    }

    // Non-generic sibling of StoreSharedAndReturn<TValue> - validates a winning stage 4-6 candidate's
    // value exactly as that method does, including the same effectiveIsShared write gate (this
    // method's only caller, TryResolveConfigured, carries that in request.IsShared - see ADR-0056),
    // and returns a plain object? instead of casting to a generic TValue.
    private object? ValidateProviderResultAndReturn(object? value, in CompositionRequest request, PipelineStage stage, Type? provider, Type requestedType)
    {
        var result = ValidateAuthoritativeValue(value, request, "provider");
        _trace.Record(stage, provider, OutcomeOf(result));
        StoreSharedValue(requestedType, request.IsShared, result);
        return AuthoritativeValue(result, requestedType);
    }

    // The non-generic sibling of Authoritative<TValue> - same NotHandled/Success/Failure -> value-or-
    // throw conversion, but takes the failed type explicitly instead of reading it off a generic
    // TValue, since TryResolveConfigured has no TValue to read it from.
    private object? AuthoritativeValue(CompositionResult result, Type requestedType) => result switch
    {
        CompositionResult.Success success => success.Value,
        CompositionResult.Failure failure => throw BuildException(requestedType, failure.Message),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "An authoritative composition stage must produce Success or Failure."),
    };

    private TValue ResolveCore<TValue>(Nullability nullability, Type? declaringType, PathSegment? segment, bool isShared)
    {
        var requestedType = typeof(TValue);
        // A builder-configured Share<T>() type makes EVERY request for that type behave as shared for
        // the write side (CompositionRequest.IsShared below), regardless of source - deliberately NOT
        // used for the raw `isShared` duplicate-establishment guard further down (still keyed on the
        // explicit, caller-asserted isShared only), since an ordinary ambient participant hitting an
        // already-populated scope must read-and-return, never throw "already established." See
        // docs/adr/0056-composition-builder-share-graph-wide-sharing.md.
        var effectiveIsShared = isShared || _sharedTypes.Contains(requestedType);
        var previousRandom = _random;
        var previousDeclaringType = _currentDeclaringType;
        var isRoot = _path is null;
        var checkpoint = _trace.Checkpoint;

        // A descriptor-based Resolve<T> called directly on a fresh context (no preceding
        // ResolveRoot<T>() call) - only reachable from a test exercising the descriptor path in
        // isolation, never from generated code - is treated as its own root, exactly like _path's
        // existing null-check above: there is no ancestor random source to fork from either.
        _path = isRoot ? CompositionPath.Root(requestedType) : _path!.Push(requestedType, segment);
        _random = isRoot ? RandomSource.FromSeed(_seed) : previousRandom!.Fork(segment!);
        _currentDeclaringType = declaringType;

        try
        {
            var request = new CompositionRequest
            {
                RequestedType = requestedType,
                Nullability = nullability,
                DeclaringType = declaringType,
                Path = _path,
                IsShared = effectiveIsShared,
            };

            // Stage 1 (explicit values) has no pipeline mechanism - Milestone 4's inline theory values
            // are handled entirely by Compono.XunitV3, one layer above this pipeline (a value already
            // known needs no dispatch, tracing, or randomness at all), per
            // docs/adr/0022-compono-xunit-package-design.md. Every request falls through this stage;
            // nothing to trace.

            // Stage 2: shared/scoped values - every request checks scope for a match, regardless of
            // its own IsShared flag (ADR-0021: this is what lets an ordinary, unmarked nested request -
            // e.g. a SUT's own constructor parameter - transparently reuse a value a [Shared] test
            // parameter already established). Only the *write* side (StoreSharedAndReturn,
            // ResolveViaGeneratedPlan's shared branch, both below) stays restricted to IsShared
            // requests - this read-side change is provably a no-op for every pre-Milestone-4 caller,
            // since nothing ever populated scope on any of those paths. Not an ICompositionProvider,
            // so Provider is null.
            if (_scope.TryGet(requestedType, out var sharedValue))
            {
                // Belt-and-suspenders per ADR-0022: Compono.XunitV3's own signature validation is meant
                // to catch a duplicate [Shared] type before a row is even created, but a caller that
                // reaches this pipeline directly (or bypasses that validation) must still be refused
                // here rather than silently reusing or overwriting the first value.
                if (isShared)
                {
                    _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, CompositionAttemptOutcome.Failure);
                    throw BuildException(
                        requestedType,
                        $"A shared value for '{CompositionPath.FriendlyTypeName(requestedType)}' has already been " +
                        "established in this row - only one shared value per type is allowed.");
                }

                var result = ValidateAuthoritativeValue(sharedValue, request, "shared value");
                _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, OutcomeOf(result));
                var value = Authoritative<TValue>(result);
                _trace.Rewind(checkpoint);
                return value;
            }

            _trace.Record(PipelineStage.SharedOrScopedValue, provider: null, CompositionAttemptOutcome.NotHandled);

            // Stage 3, sub-step (a): exact registrations. A registration factory is invoked through
            // InvokeFactory, which owns the manual-resolve invocation frame and the factory-reentrance
            // guard - never called directly. Recorded Pending *before* InvokeFactory runs - same
            // reasoning as the collection-plan/generated-plan Pending markers below: if the factory
            // calls context.Resolve<T>() and that nested request fails, the nested BuildException
            // snapshots the trace before control returns here, so without this marker the resulting
            // diagnostic would omit that this ExactRegistration dispatch was genuinely in flight when
            // the failure happened.
            if (_registrations.TryGet(requestedType, out var factory))
            {
                _trace.Record(PipelineStage.ExactRegistration, provider: null, CompositionAttemptOutcome.Pending);
                var registeredValue = InvokeFactory(factory, requestedType, PipelineStage.ExactRegistration, provider: null);
                var result = ValidateAuthoritativeValue(registeredValue, request, "registration");
                _trace.Record(PipelineStage.ExactRegistration, provider: null, OutcomeOf(result));
                StoreSharedValue(requestedType, effectiveIsShared, result);
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
                    StoreSharedValue(requestedType, effectiveIsShared, result);
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
            if (TryProviders(_configurationRuleProviders, PipelineStage.ConfigurationRule, request, out var configurationRuleValue, out var configurationRuleProvider))
            {
                var value = StoreSharedAndReturn<TValue>(configurationRuleValue, request, PipelineStage.ConfigurationRule, configurationRuleProvider);
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
                $"No registration, configuration rule, semantic provider, test-double provider, built-in provider, or generated plan could satisfy '{CompositionPath.FriendlyTypeName(requestedType)}'.");
        }
        finally
        {
            _path = _path.Pop();
            _random = previousRandom;
            _currentDeclaringType = previousDeclaringType;
        }
    }

    // ADR-0020: reads the exact same (DeclaringType, member-name) identity a compiled MemberRuleProvider
    // matches on, for the request currently being resolved (the collection member itself - collection
    // dispatch resolves this exact request, not some child of it). A root-level collection request has
    // no DeclaringType at all, so the member-override branch never matches and this falls straight
    // through to the global default/built-in size - the correct outcome for a root. Never advances
    // IRandomSource, never pushes a path segment of its own - a plain, three-level data lookup.
    // _path!.RequestedType is the current node's own requested type (the collection type itself, e.g.
    // List<int>) - passed to TryGetMemberOverride so a differently-typed member sharing the same
    // (declaring type, name) pair as the one an override actually targets can't wrongly inherit it
    // (Codex review, the same collision class MemberRuleProvider's own requested-type check guards
    // against).
    private int ResolveCollectionSizeCore()
    {
        if (_currentDeclaringType is { } declaringType
            && MemberNameOfCurrentRequest() is { } memberName
            && _collectionSizePolicy.TryGetMemberOverride((declaringType, memberName), _path!.RequestedType, out var overrideSize))
        {
            return overrideSize;
        }

        return _collectionSizePolicy.GlobalDefault ?? CollectionDefaults.Size;
    }

    private string? MemberNameOfCurrentRequest() => _path?.Segment switch
    {
        PathSegment.ConstructorParameter p => p.Name,
        PathSegment.RequiredMember m => m.Name,
        _ => null,
    };

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
    // factory call goes through. Owns three mechanisms, all per docs/adr/0019-registrations-and-
    // service-provider-injection.md and docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md:
    // (1) a manual-resolve invocation frame, pushed immediately before the call and popped in finally,
    // giving every descriptor-less Resolve<T>() call made during this one invocation a shared, call-
    // sequence-ordinal-keyed ManualResolve path segment; (2) a factory-reentrance guard, keyed by the
    // exact delegate instance about to be invoked (never by requested type - that would reject the
    // legitimate Node.Child-style cycle-terminating pattern) - a self-referencing factory that calls
    // back into resolving a request routing to itself is caught here, as a diagnosable
    // CompositionException, instead of recursing to a StackOverflowException; (3) converting a
    // factory's own thrown exception into an authoritative Failure - ADR-0010's Failure semantics
    // name this exact case ("an exact registration (stage 3) whose factory throws") explicitly, so an
    // ordinary factory-thrown exception must surface as a structured CompositionException (path, seed,
    // trace, original preserved as InnerException), not escape raw with none of that context.
    // `provider` is recorded on both Failure paths below - null for stage 3 (an exact registration has
    // no ICompositionProvider identity of its own), the concrete TypeRuleProvider/MemberRuleProvider
    // type for a stage-4 rule factory, matching the identity TryProviders' own Pending/NotHandled
    // entries for that same candidate already carry (PR #19 review: a rule factory's Failure entry was
    // otherwise the only trace entry for that attempt not tagged with the provider that produced it).
    internal object? InvokeFactory(Func<ICompositionContext, object?> factory, Type requestedType, PipelineStage stage, Type? provider)
    {
        if (IsFactoryActive(factory))
        {
            _trace.Record(stage, provider, CompositionAttemptOutcome.Failure);
            throw BuildException(requestedType, BuildFactoryReentranceMessage(requestedType));
        }

        _activeFactories.Add(factory);
        _manualResolveFrames.Add(new ManualResolveFrame());
        try
        {
            return factory(this);
        }
        catch (CompositionException ex) when (ReferenceEquals(ex.DiagnosingContextIdentity, _identity))
        {
            // Already a fully-diagnosed CompositionException from a nested Resolve<T>() call made
            // inside this factory, on this exact CompositionContext instance - DiagnosingContextIdentity
            // (set only by this context's own BuildException, to this context's own _identity token) is
            // what distinguishes this case from a factory throwing a CompositionException it built
            // itself (DiagnosingContextIdentity null), or one it captured/rethrew from an entirely
            // different CompositionContext - a separate Composer.Create call the factory happened to
            // invoke (DiagnosingContextIdentity non-null, but a different token - falls to the catch
            // below either way, since only *this* context's own diagnosis is safe to trust as already
            // carrying this operation's ancestor path). That failure's own Diagnostic (built from its
            // own, more specific path/trace) is strictly more useful than anything this outer catch
            // could construct. Re-wrapping it here would discard that detail behind a generic "the
            // factory threw" message, so it's left to propagate exactly as-is.
            throw;
        }
        catch (Exception ex)
        {
            _trace.Record(stage, provider, CompositionAttemptOutcome.Failure);
            throw BuildException(
                requestedType,
                $"The registration or configuration-rule factory for '{CompositionPath.FriendlyTypeName(requestedType)}' threw: {ex.Message}",
                ex);
        }
        finally
        {
            _manualResolveFrames.RemoveAt(_manualResolveFrames.Count - 1);
            _activeFactories.RemoveAt(_activeFactories.Count - 1);
        }
    }

    // A plain indexed loop instead of List<T>.Exists(predicate) - the lambda equivalent captures
    // `factory` into a new closure/delegate allocated on every single registration-factory invocation,
    // on the composition hot path every composed test runs through (ADR-0007's allocation-conscious
    // hot-path goal). Reference identity only - never the delegate's own Equals (target + method),
    // which would treat two unrelated registrations that happen to share a captured-state shape as
    // "the same" factory, exactly the false-positive collision ADR-0019's design rules out.
    private bool IsFactoryActive(Func<ICompositionContext, object?> factory)
    {
        for (var i = 0; i < _activeFactories.Count; i++)
        {
            if (ReferenceEquals(_activeFactories[i], factory))
                return true;
        }

        return false;
    }

    private string BuildFactoryReentranceMessage(Type requestedType) =>
        $"Recursive registration or configuration-rule factory detected while composing " +
        $"'{CompositionPath.FriendlyTypeName(requestedType)}': '{_path!.ToDisplayString()}' would invoke the same " +
        "factory again, which is already in progress. Use a different registration/rule, or terminate the " +
        "recursion inside the factory itself (e.g. by returning a value that doesn't call Resolve<T>() for this type).";

    // Invokes a public ICompositionValueProvider's TryProvide - the single point every stage-5/6
    // public-provider dispatch (PublicProviderAdapter) goes through, mirroring InvokeFactory's manual-
    // resolve-frame push for stage-3/4 factories (per docs/adr/0019-registrations-and-service-provider-injection.md)
    // so a provider can call context.Resolve<T>() (the descriptor-less overload) to compose a nested
    // value, exactly as ICompositionValueProvider's own contract promises. Unlike InvokeFactory, this
    // method never catches or wraps an exception TryProvide throws - ADR-0024's Provider Failure
    // Semantics commit to a public provider's own thrown exception propagating uncaught, exactly like
    // any other ordinary stage-4/7 provider's TryCompose already does; reentrance below produces the
    // engine's own diagnosed CompositionException, which is a distinct concern from wrapping a
    // provider's unrelated thrown exception.
    //
    // Reentrance is keyed on (provider instance, requested type), not provider identity alone -
    // unlike TypeRuleProvider/MemberRuleProvider, where one instance is compiled 1:1 for exactly one
    // type (so "the same factory delegate re-entered" and "the same type re-entered" are equivalent by
    // construction), one ICompositionValueProvider instance can legitimately handle many different
    // types (e.g. "any interface"), including composing a different type as one of its own nested
    // dependencies - keying on the provider alone would wrongly block that legitimate case. Only the
    // exact same provider asked to resolve the exact same type it's already resolving is a real cycle
    // (PR #28 review, Codex: recursing through the public descriptor overload previously ran until
    // StackOverflowException instead of producing this diagnostic).
    internal CompositionProviderResult InvokeProvider(ICompositionValueProvider provider, in CompositionProviderRequest request, PipelineStage stage, Type providerType)
    {
        var requestedType = request.RequestedType;

        if (IsProviderRequestActive(provider, requestedType))
        {
            _trace.Record(stage, providerType, CompositionAttemptOutcome.Failure);
            throw BuildException(requestedType, BuildProviderReentranceMessage(requestedType));
        }

        _activeProviderRequests.Add((provider, requestedType));
        _manualResolveFrames.Add(new ManualResolveFrame());
        try
        {
            return provider.TryProvide(in request, this);
        }
        finally
        {
            _manualResolveFrames.RemoveAt(_manualResolveFrames.Count - 1);
            _activeProviderRequests.RemoveAt(_activeProviderRequests.Count - 1);
        }
    }

    // A plain indexed loop, same reasoning as IsFactoryActive above - avoids a per-call closure/
    // allocation on the composition hot path. Reference identity for the provider, ordinary type
    // equality for the requested type.
    private bool IsProviderRequestActive(ICompositionValueProvider provider, Type requestedType)
    {
        for (var i = 0; i < _activeProviderRequests.Count; i++)
        {
            if (ReferenceEquals(_activeProviderRequests[i].Provider, provider) && _activeProviderRequests[i].RequestedType == requestedType)
                return true;
        }

        return false;
    }

    private string BuildProviderReentranceMessage(Type requestedType) =>
        $"Recursive provider request detected while composing " +
        $"'{CompositionPath.FriendlyTypeName(requestedType)}': '{_path!.ToDisplayString()}' would ask the same " +
        "provider to compose the same type again, which is already in progress. Use a registration or a shared " +
        "value to terminate the recursion, or have the provider avoid resolving its own requested type recursively.";

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
            _trace.Record(stage, candidate.ProviderType, CompositionAttemptOutcome.Pending);
            var result = candidate.TryCompose(request, this);
            _trace.Rewind(checkpoint);

            if (result is CompositionResult.Success success)
            {
                value = success.Value;
                provider = candidate.ProviderType;
                return true;
            }

            _trace.Record(stage, candidate.ProviderType, CompositionAttemptOutcome.NotHandled);
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
        StoreSharedValue(request.RequestedType, isShared: true, result);

        return Authoritative<TValue>(result);
    }

    // The single write side of "shared" storage - every call site that can produce an authoritative
    // shared-request result (stage 2's belt-and-suspenders duplicate check aside, which never reaches
    // here) routes through this one method rather than calling _scope.Set directly, so a future stage
    // can't repeat the exact gap PR #22 review caught: two stage-3 branches that validated a shared
    // result correctly but forgot to store it, because the storage was inlined at each call site
    // instead of centralized.
    private void StoreSharedValue(Type requestedType, bool isShared, CompositionResult result)
    {
        if (isShared && result is CompositionResult.Success success)
            _scope.Set(requestedType, success.Value);
    }

    // Stages 2/3's authoritative validation, per ADR-0011's second amendment: a null value for a
    // non-nullable request, or a value whose runtime type isn't assignable to RequestedType, is a
    // Failure at that stage - never silently passed through as NotHandled.
    private static CompositionResult ValidateAuthoritativeValue(object? value, in CompositionRequest request, string source) =>
        ValidateAuthoritativeValue(value, request.RequestedType, request.Nullability, source);

    // The (Type, Nullability) shape the check above actually needs - factored out so
    // ShareExplicitTestParameter (ADR-0021) can apply the exact same authoritative validation to an
    // already-known value with no CompositionRequest to build (no pipeline dispatch happens for it at
    // all, so there's no in-flight request to source Type/Nullability from).
    private static CompositionResult ValidateAuthoritativeValue(object? value, Type requestedType, Nullability nullability, string source)
    {
        if (value is null)
        {
            return nullability == Nullability.Nullable
                ? new CompositionResult.Success(null)
                : new CompositionResult.Failure(
                    $"The {source} for '{requestedType}' is null, but '{requestedType}' is not nullable.");
        }

        return requestedType.IsInstanceOfType(value)
            ? new CompositionResult.Success(value)
            : new CompositionResult.Failure(
                $"The {source} for '{requestedType}' produced a value of type '{value.GetType()}', " +
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
    // CompositionDiagnostic. _path is always non-null here: either this runs from inside ResolveCore's
    // try block (after _path has been pushed for the current node), or - ShareExplicitTestParameter's
    // validation failure path only - from a CompositionRow whose CreateRow constructor already
    // pre-established _path as its root (ADR-0021), so it's never null there either.
    private CompositionException BuildException(Type failedType, string message) =>
        CompositionException.CreatePipelineDiagnosed(BuildDiagnostic(failedType, message), _identity);

    // The IServiceProvider fallback's throwing-container case is the only stage that needs to preserve
    // an original exception as InnerException (never `throw ex;`) - every other authoritative-stage
    // failure has no prior exception to preserve.
    private CompositionException BuildException(Type failedType, string message, Exception innerException) =>
        CompositionException.CreatePipelineDiagnosed(BuildDiagnostic(failedType, message), innerException, _identity);

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
