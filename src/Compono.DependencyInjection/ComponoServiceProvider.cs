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

    // Plain object, not System.Threading.Lock (coding-standards.md's usual preference) - this package
    // multi-targets net8.0, where System.Threading.Lock doesn't exist yet (.NET 9+ only). Serializes
    // the whole cache-check/resolve/cache-write section below: CompositionContext's own _path/_random/
    // trace state (reached via _row.TryResolveConfigured) is unsynchronized mutable state, not just
    // this adapter's Dictionary - two concurrent GetService calls racing into it could corrupt that
    // shared bookkeeping or hand two same-type callers different instances despite the documented
    // stable-identity guarantee, not just throw the ordinary "Dictionary isn't thread-safe" exception.
    private readonly object _lock = new();

    // Bounded, not Monitor.Enter's indefinite wait - PR #105 review: two cross-wired rows (Row A's
    // registration factory calls into Row B's adapter, Row B's calls back into Row A's) resolved
    // concurrently on two threads each acquire their OWN row's lock first, then block waiting for the
    // OTHER row's lock the other thread already holds - a classic AB-BA deadlock neither row's
    // reentrance guard can see, since each thread never gets far enough to reach it. Verified directly:
    // reverting this to a plain `lock (_lock)` makes a two-row concurrent-cycle repro hang every run
    // (5/5) until forcibly killed; TryEnter with this bound turns that same repro into a diagnosed,
    // catchable TimeoutException instead (5/5). The timeout is generous specifically so it never fires
    // for legitimate contention (a slow custom provider, GC pause) - it exists to convert an
    // unrecoverable process hang into a debuggable failure, not to police normal latency.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    internal ComponoServiceProvider(CompositionRow row)
    {
        _row = row;
    }

    public object? GetService(Type serviceType)
    {
        if (!Monitor.TryEnter(_lock, LockTimeout))
        {
            throw new TimeoutException(
                $"Timed out after {LockTimeout} waiting to resolve '{serviceType}' through this row's " +
                "AsServiceProvider() adapter. This usually means two cross-wired rows (see ADR-0047's " +
                "Recursion section) are being resolved concurrently on different threads, each holding " +
                "its own row's lock while waiting for the other's - a deadlock, not ordinary contention.");
        }

        try
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
        finally
        {
            Monitor.Exit(_lock);
        }
    }
}
