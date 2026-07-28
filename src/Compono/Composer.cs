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
}
