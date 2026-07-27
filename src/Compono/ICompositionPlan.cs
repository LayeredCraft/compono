namespace Compono;

/// <summary>
/// A source-generated construction plan for <typeparamref name="T"/> — invokes <typeparamref name="T"/>'s
/// selected constructor directly, per <c>docs/adr/0002-constructor-selection-algorithm.md</c>, with no
/// runtime reflection.
/// </summary>
/// <typeparam name="T">The type this plan constructs.</typeparam>
public interface ICompositionPlan<out T>
{
    /// <summary>
    /// Constructs an instance of <typeparamref name="T"/>, resolving any constructor arguments through
    /// <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The active composition context.</param>
    T Compose(ICompositionContext context);
}
