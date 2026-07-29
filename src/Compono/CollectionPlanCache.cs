namespace Compono;

/// <summary>
/// Holds the generated collection <see cref="ICompositionPlan{T}"/> for the closed collection type
/// <typeparamref name="T"/> (e.g. <c>List&lt;Address&gt;</c>), per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s third amendment.
/// </summary>
/// <remarks>
/// Mirrors <see cref="PlanCache{T}"/> exactly - a closed generic static field is a direct field read
/// on <see cref="CompositionContext"/>'s hot path, not a <c>typeof(T)</c>-keyed dictionary lookup and
/// not runtime reflection. Kept as a distinct cache from <see cref="PlanCache{T}"/> rather than reused
/// for both: dispatch through this cache happens at stage 7 (built-in value providers), after
/// registrations/profile/semantic/test-double providers have already had first refusal, whereas
/// <see cref="PlanCache{T}"/> dispatch is stage 8.
/// <para>
/// <see cref="Instance"/> is populated exactly once, by a generated module initializer in the
/// consuming assembly (never by <c>Compono</c> itself) - the same cross-assembly reason
/// <see cref="PlanCache{T}"/>'s setter is <see langword="public"/> despite <c>coding-standards.md</c>'s
/// "no static singletons" rule.
/// </para>
/// </remarks>
/// <typeparam name="T">The closed collection type the cached plan constructs.</typeparam>
public static class CollectionPlanCache<T>
{
    /// <summary>
    /// The generated collection plan for <typeparamref name="T"/>, or <see langword="null"/> if none
    /// has been registered - either because <typeparamref name="T"/> was never discovered in the
    /// transitive composition graph reachable from a <c>Composer.Create&lt;T&gt;()</c> call site, or
    /// because the consuming assembly's module initializers haven't run yet.
    /// </summary>
    public static ICompositionPlan<T>? Instance { get; set; }
}
