using Compono.Providers;

namespace Compono;

/// <summary>
/// Mutable configuration accumulator for a <see cref="Composer"/>, live only for the duration of the
/// <see cref="Composer.Create(Action{CompositionBuilder})"/> callback that receives it.
/// </summary>
/// <remarks>
/// Every accumulated setting is validated and frozen into an immutable <see cref="CompositionConfiguration"/>
/// once the callback returns - nothing about a later <see cref="Composer.Create{T}"/>/
/// <see cref="Composer.CreateMany{T}"/> call can observe a mutation made after that point, since this
/// instance is never reachable again. See
/// <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>.
/// </remarks>
public sealed class CompositionBuilder
{
    private readonly ConfigurationOptionSlot<CompositionSeed> _seed = new("WithSeed");
    private readonly ConfigurationOptionSlot<IServiceProvider> _serviceProvider = new("UseServiceProvider");
    private readonly ConfigurationOptionSlot<int> _globalCollectionSize = new("WithCollectionSize");
    private readonly Dictionary<Type, List<ConfigurationSource>> _registrationSources = new();
    private readonly Dictionary<Type, Func<ICompositionContext, object?>> _registrationFactories = new();
    private readonly Dictionary<Type, List<ConfigurationSource>> _typeRuleSources = new();
    private readonly Dictionary<Type, Func<ICompositionContext, object?>> _typeRuleFactories = new();
    private readonly Dictionary<(Type DeclaringType, string MemberName), List<ConfigurationSource>> _memberRuleSources = new();
    private readonly Dictionary<(Type DeclaringType, string MemberName), Func<ICompositionContext, object?>> _memberRuleFactories = new();
    private readonly Dictionary<(Type DeclaringType, string MemberName), List<ConfigurationSource>> _memberCollectionSizeSources = new();
    private readonly Dictionary<(Type DeclaringType, string MemberName), int> _memberCollectionSizes = new();
    private readonly Stack<Type> _applyingProfiles = new();

    internal CompositionBuilder()
    {
    }

    /// <summary>
    /// Sets this composer's explicit root seed - the same seed produces the same composed output
    /// (for a given <c>Compono</c> package version) across every <see cref="Composer.Create{T}"/>/
    /// <see cref="Composer.CreateMany{T}"/> call this composer ever serves. Without this call, each
    /// root composition operation generates its own seed.
    /// </summary>
    /// <remarks>
    /// Calling this more than once (directly, or once directly and once from a profile, or from two
    /// different profiles) is a configuration conflict, not last-write-wins - surfaced as a
    /// <see cref="CompositionConfigurationException"/> once <see cref="Composer.Create(Action{CompositionBuilder})"/>'s
    /// validation runs, not immediately. See
    /// <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s Amendment for why:
    /// two different seeds configured for the same composer has no coherent "effective" value the way
    /// a typical options-builder's last-write-wins convention would assume.
    /// </remarks>
    /// <param name="seed">The explicit root seed.</param>
    public CompositionBuilder WithSeed(int seed)
    {
        _seed.Set(new CompositionSeed(unchecked((ulong)seed)), CurrentSource());
        return this;
    }

    /// <summary>
    /// Registers an exact-type factory for <typeparamref name="T"/> - pipeline stage 3
    /// (<c>docs/architecture.md</c>) resolves <typeparamref name="T"/> by invoking
    /// <paramref name="factory"/>, called through <see cref="ICompositionContext"/> exactly like
    /// generated code's own resolution, so <paramref name="factory"/> can call
    /// <see cref="ICompositionContext.Resolve{TValue}()"/> to compose nested dependencies.
    /// </summary>
    /// <remarks>
    /// Registering the same <typeparamref name="T"/> more than once (directly, or once directly and
    /// once from a profile, or from two different profiles) is a build-time conflict, not last-write-
    /// wins - see <c>docs/adr/0019-registrations-and-service-provider-injection.md</c>'s deliberately
    /// strict throw-on-duplicate decision.
    /// </remarks>
    /// <typeparam name="T">The exact type to register a factory for.</typeparam>
    /// <param name="factory">Produces the registered value, given the resolving context.</param>
    public CompositionBuilder Register<T>(Func<ICompositionContext, T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        AddRegistration(typeof(T), context => factory(context));
        return this;
    }

    /// <summary>
    /// Registers an exact-type factory for <typeparamref name="T"/> with no dependency on the
    /// resolving context - the convenience overload for <see cref="Register{T}(Func{ICompositionContext, T})"/>'s
    /// common no-dependency case (e.g. <c>Register&lt;IClock&gt;(() =&gt; new FakeClock())</c>).
    /// </summary>
    /// <typeparam name="T">The exact type to register a factory for.</typeparam>
    /// <param name="factory">Produces the registered value.</param>
    public CompositionBuilder Register<T>(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        AddRegistration(typeof(T), _ => factory());
        return this;
    }

    /// <summary>
    /// Configures a native <see cref="IServiceProvider"/> as stage 3's fallback for a type with no
    /// exact <see cref="Register{T}(Func{ICompositionContext, T})"/> entry - tried only after every
    /// exact registration misses, before falling through to stage 4. See
    /// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c> for the full ordering and
    /// null/exception/wrong-type semantics.
    /// </summary>
    /// <remarks>
    /// Compono never creates, resolves, or disposes a scope from <paramref name="provider"/> - it
    /// calls <c>GetService(Type)</c> directly and nothing else. Calling this more than once is a
    /// build-time conflict, following the same scalar-fail-fast rule as <see cref="WithSeed"/>.
    /// </remarks>
    /// <param name="provider">The externally-owned container to fall back to.</param>
    public CompositionBuilder UseServiceProvider(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _serviceProvider.Set(provider, CurrentSource());
        return this;
    }

    /// <summary>
    /// Applies <typeparamref name="TProfile"/>'s <see cref="ICompositionProfile.Configure"/> to this
    /// builder immediately, synchronously - constructed via an ordinary, reflection-free <c>new()</c>.
    /// </summary>
    /// <remarks>
    /// A profile applied while another profile of the exact same declared type is already applying
    /// (directly or nested several levels deep) is a cycle - see
    /// <c>docs/adr/0018-composition-profiles.md</c>.
    /// </remarks>
    /// <typeparam name="TProfile">The profile type to construct and apply.</typeparam>
    /// <exception cref="CompositionConfigurationException">
    /// Applying <typeparamref name="TProfile"/> would create a cycle - thrown immediately, containing
    /// exactly one <see cref="CompositionConfigurationError.ProfileCycle"/> error naming the full chain.
    /// </exception>
    public CompositionBuilder AddProfile<TProfile>()
        where TProfile : ICompositionProfile, new()
    {
        ApplyProfile(new TProfile());
        return this;
    }

    /// <summary>
    /// Applies <paramref name="profile"/>'s <see cref="ICompositionProfile.Configure"/> to this builder
    /// immediately, synchronously - the instance-based counterpart to
    /// <see cref="AddProfile{TProfile}()"/>, for a profile that needs constructor arguments or is
    /// already an instance.
    /// </summary>
    /// <param name="profile">The profile instance to apply.</param>
    /// <exception cref="CompositionConfigurationException">
    /// Applying <paramref name="profile"/> would create a cycle (by its declared CLR type) - thrown
    /// immediately, containing exactly one <see cref="CompositionConfigurationError.ProfileCycle"/>
    /// error naming the full chain.
    /// </exception>
    public CompositionBuilder AddProfile(ICompositionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ApplyProfile(profile);
        return this;
    }

    /// <summary>
    /// Starts a type or member configuration rule for <typeparamref name="T"/> - calling <c>.Use(...)</c>
    /// directly registers a type rule (matches any stage-4 request for exactly <typeparamref name="T"/>,
    /// regardless of which member/position requested it); calling <c>.Member(x => x.Y)</c> first
    /// registers a member rule instead (matches only requests for that exact declaring type/member
    /// pair). See <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    /// <typeparam name="T">The type to configure a rule for.</typeparam>
    public CompositionTypeRuleBuilder<T> For<T>() => new(this);

    /// <summary>
    /// Sets this composer's global default collection size - the size a generated collection plan
    /// builds when no member-scoped <c>.For&lt;T&gt;().Member(x => x.Y).WithCollectionSize(...)</c>
    /// override applies. Falls back to the built-in size of <c>3</c> if never called. See
    /// <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    /// <remarks>
    /// Calling this more than once is a build-time conflict, following the same scalar-fail-fast rule
    /// as <see cref="WithSeed"/>.
    /// </remarks>
    /// <param name="size">The default collection size.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public CompositionBuilder WithCollectionSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        _globalCollectionSize.Set(size, CurrentSource());
        return this;
    }

    // Validates every accumulated setting and freezes it into an immutable CompositionConfiguration -
    // called exactly once, by Composer.Create, immediately after the configuration callback returns.
    // Collects every conflict in one pass rather than throwing at the first one found, so a single
    // Composer.Create(...) call reports everything wrong with it at once.
    internal CompositionConfiguration Build()
    {
        var errors = new List<CompositionConfigurationError>();

        if (_seed.TryGetConflict(out var seedConflict))
            errors.Add(seedConflict);

        if (_serviceProvider.TryGetConflict(out var serviceProviderConflict))
            errors.Add(serviceProviderConflict);

        if (_globalCollectionSize.TryGetConflict(out var collectionSizeConflict))
            errors.Add(collectionSizeConflict);

        foreach (var (type, sources) in _registrationSources)
        {
            if (sources.Count > 1)
                errors.Add(new CompositionConfigurationError.DuplicateRegistration(type, sources));
        }

        foreach (var (type, sources) in _typeRuleSources)
        {
            if (sources.Count > 1)
                errors.Add(new CompositionConfigurationError.DuplicateRule(type, memberName: null, sources));
        }

        foreach (var (key, sources) in _memberRuleSources)
        {
            if (sources.Count > 1)
                errors.Add(new CompositionConfigurationError.DuplicateRule(key.DeclaringType, key.MemberName, sources));
        }

        foreach (var (key, sources) in _memberCollectionSizeSources)
        {
            if (sources.Count > 1)
                errors.Add(new CompositionConfigurationError.DuplicateCollectionSizeOverride(key.DeclaringType, key.MemberName, sources));
        }

        if (errors.Count > 0)
            throw new CompositionConfigurationException(errors);

        // Member rules ahead of type rules, unconditionally - dispatch order is a property of each
        // rule's own specificity, never of which builder call happened to run first (ADR-0020's
        // "specificity-based, not call-order-based" precedence).
        IReadOnlyList<ICompositionProvider> rules =
        [
            .. _memberRuleFactories.Select(entry => (ICompositionProvider)new MemberRuleProvider(entry.Key.DeclaringType, entry.Key.MemberName, entry.Value)),
            .. _typeRuleFactories.Select(entry => (ICompositionProvider)new TypeRuleProvider(entry.Key, entry.Value)),
        ];

        return new CompositionConfiguration
        {
            Seed = _seed.HasValue ? _seed.Value : null,
            Registrations = _registrationFactories.Count == 0
                ? CompositionRegistrations.Empty
                : new CompositionRegistrations(_registrationFactories),
            ServiceProvider = _serviceProvider.HasValue ? _serviceProvider.Value : null,
            Rules = rules,
            CollectionSizePolicy = _memberCollectionSizes.Count == 0 && !_globalCollectionSize.HasValue
                ? CollectionSizePolicy.Empty
                : new CollectionSizePolicy(_globalCollectionSize.HasValue ? _globalCollectionSize.Value : null, _memberCollectionSizes),
        };
    }

    // Shared by both Register<T> overloads - the first call for a given type wins as the factory
    // actually used (only meaningful when Build() finds no conflict; a conflicting type never reaches
    // CompositionConfiguration at all).
    private void AddRegistration(Type type, Func<ICompositionContext, object?> factory)
    {
        if (!_registrationSources.TryGetValue(type, out var sources))
        {
            sources = [];
            _registrationSources[type] = sources;
            _registrationFactories[type] = factory;
        }

        sources.Add(CurrentSource());
    }

    // Called by CompositionTypeRuleBuilder<T>.Use(...) with no preceding .Member(...) call - the first
    // call for a given type wins as the factory actually used, same "first wins, conflict wins later"
    // shape as AddRegistration.
    internal void AddTypeRule(Type type, Func<ICompositionContext, object?> factory)
    {
        if (!_typeRuleSources.TryGetValue(type, out var sources))
        {
            sources = [];
            _typeRuleSources[type] = sources;
            _typeRuleFactories[type] = factory;
        }

        sources.Add(CurrentSource());
    }

    // Called by CompositionMemberRuleBuilder<TParent, TMember>.Use(...), after .Member(...) already
    // parsed the target (declaring type, member name) pair immediately.
    internal void AddMemberRule((Type DeclaringType, string MemberName) key, Func<ICompositionContext, object?> factory)
    {
        if (!_memberRuleSources.TryGetValue(key, out var sources))
        {
            sources = [];
            _memberRuleSources[key] = sources;
            _memberRuleFactories[key] = factory;
        }

        sources.Add(CurrentSource());
    }

    // Called by CompositionMemberRuleBuilder<TParent, TMember>.WithCollectionSize(...) - a member-scoped
    // collection-size override, keyed identically to a member value rule (same (declaring type, member
    // name) identity), but never compiled into a stage-4 provider - it's read directly by
    // ResolveCollectionSizeCore instead, per ADR-0020.
    internal void AddMemberCollectionSize((Type DeclaringType, string MemberName) key, int size)
    {
        if (!_memberCollectionSizeSources.TryGetValue(key, out var sources))
        {
            sources = [];
            _memberCollectionSizeSources[key] = sources;
            _memberCollectionSizes[key] = size;
        }

        sources.Add(CurrentSource());
    }

    // Applies one profile's Configure to this builder. Cycle identity is the profile's declared CLR
    // type (profile.GetType()) - deliberately conservative: two different instances of the same profile
    // type nested inside each other are still treated as a cycle, mirroring the engine's own
    // type-keyed active-construction-frame stack (ADR-0011). A cycle throws immediately, from here, not
    // from Build() - a distinct failure path from Build()'s aggregated conflict scan.
    private void ApplyProfile(ICompositionProfile profile)
    {
        var profileType = profile.GetType();

        if (_applyingProfiles.Contains(profileType))
        {
            // _applyingProfiles enumerates top-of-stack first; Reverse() restores application
            // (outermost-first) order. Slicing from profileType's first occurrence - rather than
            // from the bottom of the whole stack - matters when a non-cyclic outer profile wraps the
            // cycle (RootProfile -> ProfileA -> ProfileB -> ProfileA): without the slice, RootProfile
            // would land in Chain even though it's not part of the cycle, breaking ProfileCycle.Chain's
            // documented "repeated type at both ends" contract.
            var outermostFirst = _applyingProfiles.Reverse().ToList();
            var cycleStart = outermostFirst.IndexOf(profileType);
            List<Type> chain = [.. outermostFirst.Skip(cycleStart), profileType];
            throw new CompositionConfigurationException([new CompositionConfigurationError.ProfileCycle(chain)]);
        }

        _applyingProfiles.Push(profileType);
        try
        {
            profile.Configure(this);
        }
        finally
        {
            _applyingProfiles.Pop();
        }
    }

    // "Direct" outside any profile; otherwise the currently-applying profile chain, outermost first -
    // used to tag every registration/scalar-option entry accumulated while a profile's Configure is
    // running, per ADR-0018's provenance decision.
    private ConfigurationSource CurrentSource() =>
        _applyingProfiles.Count == 0
            ? ConfigurationSource.Direct
            : new ConfigurationSource.ProfileChain([.. _applyingProfiles.Reverse()]);
}
