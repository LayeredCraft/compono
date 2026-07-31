namespace Compono;

/// <summary>
/// Resolves a value a generated <see cref="ICompositionPlan{T}"/> needs while it composes an
/// instance.
/// </summary>
/// <remarks>
/// <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> and <see cref="Resolve{TValue}()"/> are the only public members - everything the implementation owns
/// (seed, scope, path, active construction frames, the provider pipeline) is deliberately not
/// exposed here. Generated code never touches any of that state directly; it only ever calls
/// <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/> per member. See
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

    /// <summary>
    /// Resolves a value of type <typeparamref name="TValue"/> from inside a registration or
    /// configuration-rule factory, or a public <see cref="ICompositionValueProvider.TryProvide"/>
    /// invocation - the hand-written counterpart to
    /// <see cref="Resolve{TValue}(in CompositionRequestDescriptor)"/>, which only generated code
    /// calls. Only valid while one of those three is actively being invoked.
    /// </summary>
    /// <typeparam name="TValue">The requested value's type.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// No registration/configuration-rule factory or public provider invocation is currently in
    /// progress.
    /// </exception>
    /// <exception cref="CompositionException">
    /// No explicit value, shared value, registration, provider, or generated plan could satisfy the
    /// request.
    /// </exception>
    TValue Resolve<TValue>();

    /// <summary>
    /// Resolves the collection size a generated collection plan should build - the size a
    /// member-scoped <c>WithCollectionSize</c> override configures for the collection member
    /// currently being resolved, falling back to the global default, then the built-in size of
    /// <c>3</c>. Parameterless: the context already knows the current request's declaring type/member
    /// name (the same identity <c>.For&lt;T&gt;().Member(...)</c> rule matching uses), since a
    /// collection plan's <see cref="ICompositionPlan{T}.Compose"/> has no descriptor to pass. See
    /// <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    int ResolveCollectionSize();
}
