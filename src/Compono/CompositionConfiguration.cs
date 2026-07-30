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
}
