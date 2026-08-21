namespace Compono;

/// <summary>
/// One composition scope for several sibling top-level parameter requests - e.g. one xUnit theory
/// row's own method parameters - sharing one seed, one shared-value scope, and one pre-rooted path.
/// </summary>
/// <remarks>
/// Obtained via <see cref="Composer.CreateRow"/>, never constructed directly. Implements
/// <see cref="ICompositionContext"/> by forwarding to the internal <see cref="CompositionContext"/> it
/// wraps, so a value that itself needs further nested composition (a generated plan's own
/// <c>context.Resolve&lt;T&gt;(descriptor)</c> calls) is unaffected - generated code never sees this
/// type, only the interface. <see cref="ResolveShared{TValue}"/>/<see cref="ShareExplicit{TValue}"/>
/// are new members with no equivalent on <see cref="ICompositionContext"/> - they exist only here, for
/// a test-framework integration that holds this concrete type directly. See
/// <c>docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md</c>.
/// </remarks>
public sealed class CompositionRow : ICompositionContext
{
    private readonly CompositionContext _context;

    internal CompositionRow(CompositionContext context, int seed)
    {
        _context = context;
        Seed = seed;
    }

    /// <summary>
    /// This row's root deterministic seed - matches <see cref="CompositionBuilder.WithSeed"/>'s
    /// <see langword="int"/> contract exactly, so a value read here is always pasteable directly into
    /// a seed-configuration API that reports it (e.g. a test-framework integration's own attribute).
    /// </summary>
    public int Seed { get; }

    /// <inheritdoc />
    public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor) => _context.Resolve<TValue>(descriptor);

    /// <inheritdoc />
    public TValue Resolve<TValue>() => _context.Resolve<TValue>();

    /// <inheritdoc />
    public int DeriveSeed() => _context.DeriveSeed();

    /// <inheritdoc />
    public int ResolveCollectionSize() => _context.ResolveCollectionSize();

    /// <summary>
    /// Composes <typeparamref name="TValue"/> through the same pipeline
    /// <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> uses, and additionally stores
    /// the successful result into this row's shared scope - a later request for the same type in this
    /// row, including one made by a nested generated plan, reuses it instead of composing its own
    /// independent value.
    /// </summary>
    /// <typeparam name="TValue">The requested value's type.</typeparam>
    /// <param name="descriptor">The compact, compile-time-constructed request metadata.</param>
    /// <exception cref="CompositionException">
    /// No explicit value, shared value, registration, provider, or generated plan could satisfy the
    /// request; or the pipeline-produced value is <see langword="null"/> for a non-nullable request,
    /// or its runtime type isn't assignable to <typeparamref name="TValue"/>; or a shared value for
    /// <typeparamref name="TValue"/> has already been established in this row.
    /// </exception>
    public TValue ResolveShared<TValue>(in CompositionRequestDescriptor descriptor) =>
        _context.ResolveDescriptorAsShared<TValue>(descriptor);

    /// <summary>
    /// Stores <paramref name="value"/> - already known, not composed - as this row's shared value for
    /// <typeparamref name="TValue"/>, after the same authoritative validation a successful
    /// <see cref="ResolveShared{TValue}(in CompositionRequestDescriptor)"/> pipeline result gets. No
    /// pipeline dispatch, no random fork consumed - there is nothing left to compose.
    /// </summary>
    /// <typeparam name="TValue">The shared value's type.</typeparam>
    /// <param name="descriptor">The compact, compile-time-constructed request metadata.</param>
    /// <param name="value">The already-known value to share.</param>
    /// <exception cref="CompositionException">
    /// <paramref name="value"/> is <see langword="null"/> for a non-nullable request, or its runtime
    /// type isn't assignable to <typeparamref name="TValue"/>; or a shared value for
    /// <typeparamref name="TValue"/> has already been established in this row.
    /// </exception>
    public void ShareExplicit<TValue>(in CompositionRequestDescriptor descriptor, TValue value) =>
        _context.ShareExplicitTestParameter(descriptor, value);

    /// <summary>
    /// Attempts to resolve <paramref name="type"/> using only Compono's configured/provider-backed
    /// resolution stages: this row's existing scope values, exact registrations, configuration rules,
    /// and registered <see cref="ICompositionValueProvider"/> instances (including Compono.TestDoubles
    /// and Compono.NSubstitute). This is NOT equivalent to <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> -
    /// it does not consult a configured <c>IServiceProvider</c> (<c>UseServiceProvider</c>), and it
    /// cannot perform ordinary generated-plan composition of arbitrary concrete types, because that
    /// dispatch requires the target type to be known at compile time. See
    /// <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>.
    /// </summary>
    /// <param name="type">The runtime type to resolve.</param>
    /// <param name="value">
    /// The resolved value if a configured/provider stage satisfied <paramref name="type"/>; otherwise
    /// <see langword="null"/>. A legitimate handled result can itself be <see langword="null"/> - check
    /// this method's return value, not whether <paramref name="value"/> is <see langword="null"/>, to
    /// tell "handled" from "not handled" apart.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a configured/provider stage satisfied <paramref name="type"/>;
    /// <see langword="false"/> if no such stage could handle it.
    /// </returns>
    /// <exception cref="CompositionException">
    /// An exact registration factory or configuration-rule (<c>.For&lt;T&gt;().Use(...)</c>) factory
    /// threw, or the scope/registration/provider result's runtime type wasn't assignable to the
    /// requested type. A <see langword="null"/> result is never a failure
    /// here - unlike <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/>, this method always
    /// validates as nullable (see the parameter docs above), so a legitimate <see langword="null"/>
    /// always comes back as <see langword="true"/>, never this exception. This method distinguishes
    /// "nothing could handle this" (a <see langword="false"/> return) from "something tried and failed"
    /// (a thrown exception) - it never collapses the latter into the former.
    /// </exception>
    /// <exception cref="Exception">
    /// A stage 4-6 <see cref="ICompositionValueProvider"/> threw - its own original exception type
    /// propagates uncaught, exactly like <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/>'s
    /// existing provider-failure contract (a public provider's thrown exception is never wrapped in a
    /// <see cref="CompositionException"/>, per <c>docs/adr/0024-public-provider-extensibility-model.md</c>'s
    /// Provider Failure Semantics) - only an exact registration or configuration-rule factory's failure
    /// gets wrapped.
    /// </exception>
    public bool TryResolveConfigured(Type type, out object? value)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _context.TryResolveConfigured(type, out value);
    }
}
