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

    // Bounded only for a NESTED call (see t_heldAdapterLockDepth below), not for an ordinary top-level
    // one - PR #105 review, round two: an earlier fix bounded every GetService call, including a
    // top-level one contending only with another top-level call for the SAME row - a single lock can
    // never deadlock by itself (whoever holds it eventually releases it), so that turned legitimately
    // slow user code (a slow custom provider, a debugger pause, loaded CI) into a spurious
    // TimeoutException. Deadlock is only possible when a thread already holding ONE row's lock tries to
    // acquire ANOTHER's while blocked inside a factory/provider callback - exactly what
    // t_heldAdapterLockDepth detects. The timeout is generous specifically so it never fires for
    // legitimate NESTED contention either - it exists to convert an unrecoverable process hang into a
    // debuggable failure, not to police normal latency.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    // Per-thread, not per-instance - the deadlock risk is "this thread already holds SOME row's adapter
    // lock and is now trying to acquire a DIFFERENT one," regardless of which rows are involved, so
    // tracking has to span every ComponoServiceProvider instance a thread touches, not just this one.
    [ThreadStatic]
    private static int t_heldAdapterLockDepth;

    internal ComponoServiceProvider(CompositionRow row)
    {
        _row = row;
    }

    public object? GetService(Type serviceType)
    {
        var isNestedCall = t_heldAdapterLockDepth > 0;
        if (isNestedCall)
        {
            if (!Monitor.TryEnter(_lock, LockTimeout))
            {
                throw new TimeoutException(
                    $"Timed out after {LockTimeout} waiting to resolve '{serviceType}' through this " +
                    "row's AsServiceProvider() adapter, from inside another row's own resolution on this " +
                    "thread. This usually means two cross-wired rows (see ADR-0047's Recursion section) " +
                    "are being resolved concurrently on different threads, each holding its own row's " +
                    "lock while waiting for the other's - a deadlock, not ordinary contention.");
            }
        }
        else
        {
            Monitor.Enter(_lock);
        }

        t_heldAdapterLockDepth++;
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
            t_heldAdapterLockDepth--;
            Monitor.Exit(_lock);
        }
    }
}
