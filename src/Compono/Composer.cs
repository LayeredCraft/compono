using System.Globalization;

namespace Compono;

/// <summary>
/// The public entry point for composing test data, per <c>docs/public-api.md</c>.
/// </summary>
/// <remarks>
/// <c>docs/architecture.md</c>'s builder configuration (<c>Composer.Create(builder => ...)</c>),
/// profiles, and registrations are Milestone 3 scope. <see cref="Create{T}"/> resolves through the
/// real <see cref="CompositionContext"/>/resolution pipeline (Milestone 2) rather than dispatching
/// into a generated <see cref="ICompositionPlan{T}"/> directly.
/// </remarks>
public sealed class Composer
{
    private Composer()
    {
    }

    /// <summary>
    /// Creates a new <see cref="Composer"/>.
    /// </summary>
    public static Composer Create() => new();

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
        var context = new CompositionContext();
        return context.ResolveRoot<T>();
    }

    /// <summary>
    /// Composes an instance of <typeparamref name="T"/> from an explicit root seed - the internal
    /// test seam Milestone 2 Phase 1's own determinism tests (and Phase 4's <c>CreateMany</c>
    /// stability/end-to-end tests) use to exercise the real <see cref="Composer"/>/
    /// <see cref="CompositionContext"/> flow before Milestone 3's public <c>WithSeed(...)</c> builder
    /// exists.
    /// </summary>
    internal static T CreateRootForTesting<T>(CompositionSeed seed)
    {
        var context = new CompositionContext(seed);
        return context.ResolveRoot<T>();
    }

    /// <summary>
    /// Composes <paramref name="count"/> independent instances of <typeparamref name="T"/> from an
    /// explicit batch root seed - the internal test seam mirroring <c>CreateMany&lt;T&gt;()</c>'s
    /// (Phase 4) seed-derivation contract: each item's root seed forks from the batch root via
    /// <c>"CreateMany"</c> then the item's index
    /// (<c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>), never
    /// from <paramref name="count"/> itself.
    /// </summary>
    internal static IReadOnlyList<T> CreateManyForTesting<T>(int count, CompositionSeed seed)
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
