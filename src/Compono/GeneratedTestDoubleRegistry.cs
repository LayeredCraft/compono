using System.Collections.Concurrent;

namespace Compono;

/// <summary>
/// A <see cref="Type"/>-keyed registry of factories for generator-emitted test doubles, populated by
/// a generated <c>[ModuleInitializer]</c> per discovered interface (never by <c>Compono</c> itself),
/// the same cross-assembly-population shape as <see cref="RowInvokerRegistry"/>. Read by
/// <c>Compono.TestDoubles</c>'s <c>GeneratedTestDoubleProvider</c> - core <c>Compono</c> has no
/// reference the other way. See ADR-0043 Amendment 2.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="RowInvokerRegistry"/>, whose duplicate registrations for the same
/// <see cref="Type"/> are genuinely interchangeable, two different assemblies generating a double
/// for the same shared interface produce two distinct, non-interchangeable concrete types.
/// <see cref="RegisterFactory{T}"/> is still first-registration-wins (via
/// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,TValue)"/>, deterministic, never a
/// throw or blind overwrite) - a documented v1 limitation for the multi-assembly-same-interface
/// scenario, not a bug: the generated <c>Configure()</c> bridge's cast-failure message names this
/// exact scenario so a consumer who hits it understands why. See ADR-0043 Amendment 3, Finding C.
/// </para>
/// <para>
/// Every entry stored here permanently roots its registered factory delegate (and the generating
/// assembly) for the process's lifetime - the same collectible-<see cref="System.Runtime.Loader.AssemblyLoadContext"/>-rooting
/// consequence already documented for <see cref="RowInvokerRegistry"/>. See ADR-0043 Amendment 5,
/// Finding M, and <c>docs/architecture/current/generated-plans-and-discovery.md</c>'s "Open
/// questions" section (Phase 3 doc task).
/// </para>
/// </remarks>
public static class GeneratedTestDoubleRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<object>> Factories = new();

    /// <summary>
    /// Idempotently registers <paramref name="factory"/> for <typeparamref name="T"/> - a second
    /// registration for a <typeparamref name="T"/> already present (e.g. from another assembly's
    /// own generated module initializer) is a no-op, never a throw or an overwrite.
    /// </summary>
    public static void RegisterFactory<T>(Func<T> factory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        Factories.GetOrAdd(typeof(T), (Func<object>)(() => factory()));
    }

    /// <summary>
    /// Looks up and invokes the factory registered for <paramref name="requestedType"/>, or
    /// <see langword="false"/> if none has been registered - either because
    /// <paramref name="requestedType"/> was never discovered as a generated-test-double leaf, or
    /// because the consuming assembly's module initializers haven't run yet.
    /// </summary>
    public static bool TryCreate(Type requestedType, out object? value)
    {
        ArgumentNullException.ThrowIfNull(requestedType);

        if (Factories.TryGetValue(requestedType, out var factory))
        {
            value = factory();
            return true;
        }

        value = null;
        return false;
    }
}
