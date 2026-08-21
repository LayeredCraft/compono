namespace Compono;

/// <summary>
/// The internal <see cref="IServiceProvider"/> implementation backing
/// <see cref="CompositionRowServiceProviderExtensions.AsServiceProvider"/>. Never exposed by name -
/// consumers only ever see the plain <see cref="IServiceProvider"/> interface, per this repo's
/// "prefer implementation types internal when there's no consumer reason to construct them directly"
/// default. See <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>.
/// </summary>
/// <remarks>
/// Owns per-<see cref="Type"/> identity itself - not <see cref="CompositionRow"/>/
/// <see cref="CompositionScope"/>, which introduce no new sharing semantics for
/// <see cref="CompositionRow.TryResolveConfigured"/>. A successful resolution is cached (including a
/// legitimate <see langword="null"/> value - <c>_cache.TryGetValue</c> distinguishes "key present with
/// a null value" from "key absent," so a legitimately-null resolution is cached and not re-attempted).
/// A miss is never cached. This type owns no disposable resource and does not implement
/// <see cref="IDisposable"/>/<see cref="IAsyncDisposable"/> - it also never disposes a value it
/// resolves and caches, matching <see cref="CompositionScope"/>/<see cref="CompositionRow"/>/
/// <see cref="Composer"/>'s own lack of any disposal contract; the caller owns disposal of anything it
/// obtains through this provider, exactly as it would for a value it constructed by hand.
/// </remarks>
internal sealed class ComponoServiceProvider : IServiceProvider
{
    private readonly CompositionRow _row;
    private readonly Dictionary<Type, object?> _cache = [];

    internal ComponoServiceProvider(CompositionRow row)
    {
        _row = row;
    }

    public object? GetService(Type serviceType)
    {
        if (_cache.TryGetValue(serviceType, out var cached))
        {
            return cached;
        }

        if (!_row.TryResolveConfigured(serviceType, out var value))
        {
            return null;
        }

        _cache[serviceType] = value;
        return value;
    }
}
