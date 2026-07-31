namespace Compono;

/// <summary>
/// The immutable, validated result of a <see cref="CompositionBuilder"/>'s accumulated configuration -
/// produced once, by <see cref="CompositionBuilder.Build"/>, and held by exactly one
/// <see cref="Composer"/> for the rest of its lifetime.
/// </summary>
/// <remarks>
/// See <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>. Every field here is
/// a read-only, already-validated snapshot - later Milestone 3 phases add fields (an exact-registration
/// lookup, compiled configuration rules, the configured <c>IServiceProvider</c>, collection-size
/// policy) as they're implemented; only <see cref="Seed"/> exists so far (Phase 0).
/// </remarks>
internal sealed class CompositionConfiguration
{
    /// <summary>
    /// This composer's explicit root seed, or <see langword="null"/> if none was configured - each
    /// root composition operation then generates its own, per <see cref="CompositionSeed.Generate"/>.
    /// </summary>
    internal required CompositionSeed? Seed { get; init; }

    /// <summary>
    /// This composer's exact-registration lookup - <see cref="CompositionRegistrations.Empty"/> if no
    /// <c>Register&lt;T&gt;</c> call was made.
    /// </summary>
    internal required CompositionRegistrations Registrations { get; init; }

    /// <summary>
    /// This composer's configured <see cref="IServiceProvider"/> fallback, or <see langword="null"/>
    /// if <c>UseServiceProvider</c> was never called - see
    /// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c>.
    /// </summary>
    internal required IServiceProvider? ServiceProvider { get; init; }

    /// <summary>
    /// This composer's compiled pipeline stage-4 configuration-rule providers (type and member value
    /// rules), member rules ordered ahead of type rules - empty if no <c>.For&lt;T&gt;()</c> call was
    /// made. See <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    internal required IReadOnlyList<ICompositionProvider> Rules { get; init; }

    /// <summary>
    /// This composer's compiled pipeline stage-5 semantic value providers, in registration order -
    /// empty if <c>AddSemanticProvider</c> was never called. Each entry wraps one registered
    /// <see cref="ICompositionValueProvider"/> in a <see cref="Providers.PublicProviderAdapter"/>. See
    /// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
    /// </summary>
    internal required IReadOnlyList<ICompositionProvider> SemanticProviders { get; init; }

    /// <summary>
    /// This composer's compiled pipeline stage-6 test-double providers, in registration order - empty
    /// if <c>AddTestDoubleProvider</c> was never called. Each entry wraps one registered
    /// <see cref="ICompositionValueProvider"/> in a <see cref="Providers.PublicProviderAdapter"/>. See
    /// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
    /// </summary>
    internal required IReadOnlyList<ICompositionProvider> TestDoubleProviders { get; init; }

    /// <summary>
    /// This composer's collection-size configuration - <see cref="Compono.CollectionSizePolicy.Empty"/>
    /// if neither <c>WithCollectionSize</c> nor a member-scoped override was ever called.
    /// </summary>
    internal required CollectionSizePolicy CollectionSizePolicy { get; init; }
}
