namespace Compono;

/// <summary>
/// Resolves a value needed by a generated <see cref="ICompositionPlan{T}"/> while it composes an instance.
/// </summary>
/// <remarks>
/// This is a deliberately minimal placeholder for Milestone 1
/// (<c>docs/plans/0001-milestone-1-source-generation-foundation.md</c>) — just enough surface for a
/// generated plan to compile against. The real <c>CompositionContext</c> (deterministic seed, scope,
/// the provider resolution pipeline, async resolution) is Milestone 2's "Core Composition Engine" scope
/// per <c>docs/architecture.md</c>, and will very likely replace this interface's shape entirely rather
/// than extend it. That's an accepted, intentional breaking change pre-1.0 — see
/// <c>docs/mvp.md</c>'s "APIs are experimental until the first public preview" status.
/// </remarks>
public interface ICompositionContext
{
    /// <summary>
    /// Resolves a value of type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The requested value's type.</typeparam>
    TValue Resolve<TValue>();
}
