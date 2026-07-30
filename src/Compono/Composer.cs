using System.Globalization;

namespace Compono;

/// <summary>
/// The public entry point for composing test data, per <c>docs/public-api.md</c>.
/// </summary>
/// <remarks>
/// <c>docs/architecture.md</c>'s builder configuration (<c>Composer.Create(builder => ...)</c>) is
/// Milestone 3 scope; profiles, registrations, and configuration rules build on it as later Milestone 3
/// phases land. <see cref="Create{T}"/> resolves through the real <see cref="CompositionContext"/>/
/// resolution pipeline (Milestone 2) rather than dispatching into a generated
/// <see cref="ICompositionPlan{T}"/> directly.
/// </remarks>
public sealed class Composer
{
    private readonly CompositionConfiguration _configuration;

    private Composer(CompositionConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Creates a new <see cref="Composer"/> with no explicit configuration - equivalent to
    /// <see cref="Create(Action{CompositionBuilder})"/> with an empty callback.
    /// </summary>
    public static Composer Create() => Create(static _ => { });

    /// <summary>
    /// Creates a new <see cref="Composer"/> from an explicit configuration callback.
    /// </summary>
    /// <param name="configure">
    /// Configures the new composer's <see cref="CompositionBuilder"/>. Runs synchronously, exactly
    /// once, before this method returns.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="CompositionConfigurationException">
    /// The accumulated configuration has one or more conflicts (e.g. <c>WithSeed</c> called more than
    /// once).
    /// </exception>
    public static Composer Create(Action<CompositionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CompositionBuilder();
        configure(builder);
        return new Composer(builder.Build());
    }

    /// <summary>
    /// Composes an instance of <typeparamref name="T"/> - a new root composition operation, with
    /// its own scope and path, resolved through the same pipeline as any nested request.
    /// </summary>
    /// <typeparam name="T">The type to compose.</typeparam>
    /// <exception cref="CompositionException">
    /// No explicit value, shared value, registration, provider, or generated plan could satisfy
    /// <typeparamref name="T"/>.
    /// </exception>
    public T Create<T>()
    {
        var context = new CompositionContext(_configuration.Seed ?? CompositionSeed.Generate());
        return context.ResolveRoot<T>();
    }

    /// <summary>
    /// Composes <paramref name="count"/> independent instances of <typeparamref name="T"/> - each its
    /// own root composition operation (its own scope, path, and active-construction-frame stack), per
    /// the Execution Flow section of <c>docs/plans/0002-milestone-2-core-composition-engine.md</c>.
    /// Item <c>i</c>'s root seed forks from this composer's batch seed (this composer's configured
    /// <c>WithSeed</c> value, or a freshly generated one if none was configured) via
    /// <c>"CreateMany"</c> then <c>i</c>
    /// (<c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>) - no
    /// value is shared across items.
    /// </summary>
    /// <typeparam name="T">The type to compose.</typeparam>
    /// <param name="count">How many instances to compose.</param>
    /// <returns>
    /// A fully, eagerly materialized list of exactly <paramref name="count"/> instances - empty but
    /// never <see langword="null"/> when <paramref name="count"/> is <c>0</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="CompositionException">
    /// No explicit value, shared value, registration, provider, or generated plan could satisfy
    /// <typeparamref name="T"/> for one of the requested instances.
    /// </exception>
    public IReadOnlyList<T> CreateMany<T>(int count) => ComposeMany<T>(count, _configuration.Seed ?? CompositionSeed.Generate());

    /// <summary>
    /// Composes an instance of <typeparamref name="T"/> from an explicit root seed - the internal
    /// test seam Milestone 2 Phase 1's own determinism tests (and Phase 4's <c>CreateMany</c>
    /// stability/end-to-end tests) use to exercise the real <see cref="Composer"/>/
    /// <see cref="CompositionContext"/> flow directly, bypassing configuration entirely.
    /// </summary>
    internal static T CreateRootForTesting<T>(CompositionSeed seed)
    {
        var context = new CompositionContext(seed);
        return context.ResolveRoot<T>();
    }

    /// <summary>
    /// Composes <paramref name="count"/> independent instances of <typeparamref name="T"/> from an
    /// explicit batch root seed - the internal test seam mirroring <see cref="CreateMany{T}"/>'s
    /// seed-derivation contract, bypassing configuration entirely.
    /// </summary>
    internal static IReadOnlyList<T> CreateManyForTesting<T>(int count, CompositionSeed seed) =>
        ComposeMany<T>(count, seed);

    // Shared by the public CreateMany<T>(count) (this composer's configured/generated batch seed) and
    // CreateManyForTesting<T>(count, seed) (an explicit one) - both fork the same
    // "CreateMany" -> per-item-index chain from whatever batch seed they're given.
    private static IReadOnlyList<T> ComposeMany<T>(int count, CompositionSeed seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
            return [];

        var batchSeed = seed.Fork("CreateMany");
        var results = new List<T>(count);

        for (var i = 0; i < count; i++)
        {
            var itemSeed = batchSeed.Fork(i.ToString(CultureInfo.InvariantCulture));
            results.Add(CreateRootForTesting<T>(itemSeed));
        }

        return results;
    }
}
