namespace Compono;

/// <summary>
/// Resolves a value a generated <see cref="ICompositionPlan{T}"/> needs while it composes an
/// instance.
/// </summary>
/// <remarks>
/// <see cref="Resolve{TValue}"/> is the only public member - everything the implementation owns
/// (seed, scope, path, active construction frames, the provider pipeline) is deliberately not
/// exposed here. Generated code never touches any of that state directly; it only ever calls
/// <see cref="Resolve{TValue}"/> per member. See
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>.
/// </remarks>
public interface ICompositionContext
{
    /// <summary>
    /// Resolves a value of type <typeparamref name="TValue"/> for one constructor parameter or
    /// required member.
    /// </summary>
    /// <typeparam name="TValue">The requested value's type.</typeparam>
    /// <param name="descriptor">The compact, compile-time-constructed request metadata.</param>
    /// <exception cref="CompositionException">
    /// No explicit value, shared value, registration, provider, or generated plan could satisfy the
    /// request.
    /// </exception>
    TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor);
}
