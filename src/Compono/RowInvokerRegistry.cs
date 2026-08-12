using System.Collections.Concurrent;

namespace Compono;

/// <summary>
/// Non-generic delegate shapes every registered invoker is adapted to, regardless of the closed
/// parameter type - what lets <see cref="RowInvokerRegistry"/>'s caller dispatch with no reflection
/// at all, only an ordinary delegate invocation. Moved here from what was previously
/// <c>Compono.XunitV3.Binding.RowInvokers</c>' own local definition, so core and every integration
/// package share one definition instead of each declaring its own. See ADR-0041.
/// </summary>
public delegate object? ResolveInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor);

/// <inheritdoc cref="ResolveInvoker"/>
public delegate object? ResolveSharedInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor);

/// <inheritdoc cref="ResolveInvoker"/>
public delegate void ShareExplicitInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor, object? value);

/// <summary>
/// A non-generic, <see cref="Type"/>-keyed registry of <see cref="CompositionRow.Resolve{TValue}(in CompositionRequestDescriptor)"/>/
/// <see cref="CompositionRow.ResolveShared{TValue}(in CompositionRequestDescriptor)"/>/
/// <see cref="CompositionRow.ShareExplicit{TValue}(in CompositionRequestDescriptor, TValue)"/> dispatch
/// delegates, per ADR-0041 Amendment 2.
/// </summary>
/// <remarks>
/// A test-framework integration's binding algorithm only ever has a runtime <see cref="Type"/> for a
/// test parameter (reflected off the test method's own signature), never a compile-time <c>T</c> -
/// unlike <see cref="PlanCache{T}"/>, whose only reader is itself generic with <c>T</c> bound at a real
/// call site, there is no call site here to bind a closed generic field against. A <see cref="Type"/>
/// object is an ordinary runtime value under Native AOT; only *dynamic instantiation* of a generic
/// method/type from one (<c>MethodInfo.MakeGenericMethod</c>) is unsafe, and this registry never does
/// that - every <c>Resolve&lt;T&gt;()</c>/<c>ResolveShared&lt;T&gt;()</c>/<c>ShareExplicit&lt;T&gt;()</c>
/// call a registered entry actually makes is written directly, with a compile-time-known <c>T</c>, in
/// generator-emitted source.
/// <para>
/// <see cref="Register"/> is populated by a generated module initializer in the consuming assembly
/// (never by <c>Compono</c> itself), the same cross-assembly reason <see cref="PlanCache{T}"/>'s own
/// setter is <see langword="public"/> despite <c>coding-standards.md</c>'s "no static singletons" rule.
/// Two consuming assemblies loaded into the same process that both discover the same parameter type
/// (e.g. both composing <see langword="string"/> as a <c>[Compose]</c> parameter) each run their own
/// generated module initializer against this same registry - backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> with an atomic <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,TValue)"/>,
/// never a throwing or blind-overwrite registration, because a plain, non-concurrent dictionary's
/// internal structure can corrupt under genuinely concurrent writes from two module initializers
/// running on different threads during assembly load - a strictly worse failure mode than "last write
/// wins." This is safe specifically because every registration for a given <see cref="Type"/> is
/// functionally interchangeable regardless of which assembly generated it (the emitted lambda is
/// always the same shape, <c>(row, descriptor) =&gt; row.Resolve&lt;T&gt;(descriptor)</c>, for the same
/// <c>T</c>) - unlike <see cref="PlanCache{T}"/>'s own genuine "which plan is correct" ambiguity, there
/// is no real question to defer here (ADR-0041 Amendment 3).
/// </para>
/// <para>
/// Left undecorated with no <see cref="System.ComponentModel.EditorBrowsableAttribute"/> - a
/// deliberate choice, matching <see cref="PlanCache{T}"/>/<see cref="CollectionPlanCache{T}"/>, its two
/// closest precedents as "generator infrastructure, not consumer-facing" public types that carry no
/// such attribute either, rather than making this one type inconsistent with them.
/// </para>
/// <para>
/// Every entry stored here permanently roots its registered delegates (and the generating assembly) -
/// this registry has no closed-generic-instantiation home-context tie the way <see cref="PlanCache{T}"/>/
/// <see cref="CollectionPlanCache{T}"/> do, so the limitation is broader in scope than
/// <see cref="CollectionPlanCache{T}"/>'s own already-documented one (scoped only to collections whose
/// type arguments are entirely BCL types). Deferred, same disposition as that existing limitation - see
/// <c>docs/architecture/current/generated-plans-and-discovery.md</c>'s collectible-<see cref="System.Runtime.Loader.AssemblyLoadContext"/>-rooting
/// note, extended to name this registry.
/// </para>
/// </remarks>
public static class RowInvokerRegistry
{
    private static readonly ConcurrentDictionary<Type, Entry> Entries = new();

    /// <summary>
    /// Idempotently registers the three dispatch delegates for <paramref name="type"/> - a second
    /// registration for a <paramref name="type"/> already present (e.g. from another assembly's own
    /// generated module initializer) is a no-op, never a throw or an overwrite.
    /// </summary>
    public static void Register(Type type, ResolveInvoker resolve, ResolveSharedInvoker resolveShared, ShareExplicitInvoker shareExplicit)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(resolveShared);
        ArgumentNullException.ThrowIfNull(shareExplicit);

        Entries.GetOrAdd(type, new Entry(resolve, resolveShared, shareExplicit));
    }

    /// <summary>
    /// Looks up the three dispatch delegates registered for <paramref name="type"/>, or
    /// <see langword="false"/> if none has been registered - either because <paramref name="type"/> was
    /// never discovered at a dispatch-eligible <c>[Compose]</c>-family parameter, or because the
    /// consuming assembly's module initializers haven't run yet.
    /// </summary>
    public static bool TryGet(
        Type type, out ResolveInvoker resolve, out ResolveSharedInvoker resolveShared, out ShareExplicitInvoker shareExplicit)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (Entries.TryGetValue(type, out var entry))
        {
            resolve = entry.Resolve;
            resolveShared = entry.ResolveShared;
            shareExplicit = entry.ShareExplicit;
            return true;
        }

        // Safe: every caller checks the `bool` return value before touching an out parameter, the
        // same TryGetValue-style contract ConcurrentDictionary itself uses.
        resolve = null!;
        resolveShared = null!;
        shareExplicit = null!;
        return false;
    }

    private readonly record struct Entry(ResolveInvoker Resolve, ResolveSharedInvoker ResolveShared, ShareExplicitInvoker ShareExplicit);
}
