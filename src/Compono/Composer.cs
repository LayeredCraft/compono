namespace Compono;

/// <summary>
/// The public entry point for composing test data, per <c>docs/public-api.md</c>.
/// </summary>
/// <remarks>
/// This is a Milestone 1 placeholder — <c>docs/architecture.md</c>'s builder configuration
/// (<c>Composer.Create(builder => ...)</c>), profiles, registrations, and the full provider
/// resolution pipeline are Milestone 2/3 scope. Only <see cref="Create{T}"/>'s dispatch into a
/// generated <see cref="ICompositionPlan{T}"/> exists so far.
/// </remarks>
public sealed class Composer
{
    private readonly ICompositionContext _context;

    private Composer(ICompositionContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new <see cref="Composer"/>.
    /// </summary>
    public static Composer Create() => new(new PlaceholderCompositionContext());

    /// <summary>
    /// Composes an instance of <typeparamref name="T"/> via its generated <see cref="ICompositionPlan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type to compose.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// No generated plan is registered for <typeparamref name="T"/> — either the Compono source
    /// generator didn't run against this call site, or <typeparamref name="T"/>'s construction is
    /// unsupported (see the generator's compile-time diagnostics for why).
    /// </exception>
    public T Create<T>()
    {
        var plan = PlanCache<T>.Instance;

        if (plan is null)
            throw new InvalidOperationException(
                $"No generated composition plan is registered for '{typeof(T)}'. " +
                "This means the Compono source generator either didn't run against this call site, " +
                "or reported a diagnostic for this type instead of generating a plan - check the build output.");

        return plan.Compose(_context);
    }

    private sealed class PlaceholderCompositionContext : ICompositionContext
    {
        public TValue Resolve<TValue>() =>
            throw new NotSupportedException(
                "Value resolution isn't implemented yet - it's Milestone 2's Core Composition Engine scope. " +
                "Milestone 1 only supports types whose constructors take no arguments, or whose argument " +
                "types are themselves composed the same way.");
    }
}
