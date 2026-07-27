namespace Compono;

/// <summary>
/// Holds the generated <see cref="ICompositionPlan{T}"/> for <typeparamref name="T"/>, per
/// <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c>'s dispatch mechanism.
/// </summary>
/// <remarks>
/// A closed generic static field is one field per closed generic type in the CLR, so
/// <see cref="Instance"/> is a direct field read on <see cref="Composer"/>'s hot path — not a
/// <c>typeof(T)</c>-keyed dictionary lookup, and not runtime reflection.
/// <para>
/// <see cref="Instance"/> is populated exactly once, by a generated module initializer in the
/// consuming assembly (never by <c>Compono</c> itself) — this is why the setter has to be
/// <see langword="public"/> despite the "no static singletons" rule in
/// <c>coding-standards.md</c>: cross-assembly generated code has no other way to reach it. This is a
/// deliberate, ADR-recorded exception to that rule, not an oversight.
/// </para>
/// </remarks>
/// <typeparam name="T">The type the cached plan constructs.</typeparam>
public static class PlanCache<T>
{
    /// <summary>
    /// The generated plan for <typeparamref name="T"/>, or <see langword="null"/> if none has been
    /// registered — either because <typeparamref name="T"/> was never discovered at a
    /// <c>Composer.Create&lt;T&gt;()</c> call site, or because the consuming assembly's module
    /// initializers haven't run yet.
    /// </summary>
    public static ICompositionPlan<T>? Instance { get; set; }
}
