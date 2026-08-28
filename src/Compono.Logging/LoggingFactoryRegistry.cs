using System.Collections.Concurrent;

namespace Compono.Logging;

/// <summary>
/// A <see cref="Type"/>-keyed registry of statically-closed <see cref="CapturingLogger{T}"/>
/// activators, populated by a <c>Compono.Logging.Generators</c>-emitted <c>[ModuleInitializer]</c>
/// per discovered closed <c>ILogger&lt;T&gt;</c> category - never by this type itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately public - this is not an oversight.</b> Generated registration code is compiled
/// directly into the <em>consumer's own assembly</em>, so an <c>internal</c> registry could never be
/// called by it for an arbitrary, unknowable consumer assembly name (<c>InternalsVisibleTo</c> can't
/// solve this generically). This is exact, already-shipped Compono precedent, not a new pattern:
/// <see cref="GeneratedTestDoubleRegistry"/> and <see cref="RowInvokerRegistry"/> are both
/// <see langword="public"/> for this identical cross-assembly reason. See
/// docs/adr/0055-compono-logging-testing-support-package.md's Amendment 2.
/// </para>
/// <para>
/// This is generator infrastructure, not ordinary consumer-facing usage surface - a
/// <c>Compono.Logging</c> consumer composes through <c>UseLogging()</c>, inspects through
/// <see cref="LoggerTestingExtensions"/>, and constructs <see cref="CapturingLogger"/>/
/// <see cref="CapturingLogger{T}"/> directly when bypassing composition; nothing about normal usage
/// calls this type by hand. Left undecorated with no
/// <see cref="System.ComponentModel.EditorBrowsableAttribute"/> - matching
/// <see cref="GeneratedTestDoubleRegistry"/>/<see cref="RowInvokerRegistry"/>/<see cref="PlanCache{T}"/>,
/// none of which carry that attribute either, per this repo's own documented convention
/// (<see cref="RowInvokerRegistry"/>'s remarks).
/// </para>
/// <para>
/// <see cref="Register{TCategory}"/> is idempotent - a second registration for a category type
/// already present (e.g. from another assembly's own generated module initializer) is a no-op,
/// never a throw or an overwrite, matching
/// <see cref="GeneratedTestDoubleRegistry.RegisterFactory{T}"/>'s own established behavior for the
/// same cross-module-initializer-ordering reason.
/// </para>
/// </remarks>
public static class LoggingFactoryRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<LoggingOptions, object>> Factories = new();

    /// <summary>
    /// Idempotently registers <paramref name="factory"/> as the activator for
    /// <c>ILogger&lt;<typeparamref name="TCategory"/>&gt;</c>. <typeparamref name="TCategory"/> is
    /// closed statically wherever this is called from generated code - <c>typeof(ILogger{TCategory})</c>
    /// here is an ordinary generic-token load inside this method's own per-<typeparamref name="TCategory"/>
    /// compiled instantiation, never <see cref="Type.MakeGenericType"/>.
    /// </summary>
    public static void Register<TCategory>(Func<LoggingOptions, object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        Factories.TryAdd(typeof(Microsoft.Extensions.Logging.ILogger<TCategory>), factory);
    }

    /// <summary>
    /// Looks up and invokes the activator registered for <paramref name="requestedType"/>, passing
    /// <paramref name="options"/> through untouched - the caller's live <see cref="LoggingOptions"/>
    /// for the request being resolved, never captured ahead of time by the generated registration
    /// itself. Returns <see langword="false"/> if no activator has been registered - either because
    /// <paramref name="requestedType"/> was never discovered as a composed <c>ILogger&lt;T&gt;</c>
    /// leaf, or because the consuming assembly's module initializers haven't run yet.
    /// </summary>
    public static bool TryCreate(Type requestedType, LoggingOptions options, out object? value)
    {
        ArgumentNullException.ThrowIfNull(requestedType);
        ArgumentNullException.ThrowIfNull(options);

        if (Factories.TryGetValue(requestedType, out var factory))
        {
            value = factory(options);
            return true;
        }

        value = null;
        return false;
    }
}
