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
    /// or its runtime type isn't assignable to <typeparamref name="TValue"/>.
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
    /// type isn't assignable to <typeparamref name="TValue"/>.
    /// </exception>
    public void ShareExplicit<TValue>(in CompositionRequestDescriptor descriptor, TValue value) =>
        _context.ShareExplicitTestParameter(descriptor, value);
}
