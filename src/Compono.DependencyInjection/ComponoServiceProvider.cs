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

    // PR #105 review, round three: a fixed TryEnter timeout (rounds one and two of this fix) cannot
    // distinguish an actual multi-lock cycle from ordinary nested contention - a legitimate cross-row
    // call (Row A's factory calling into Row B) blocked behind an unrelated, independently slow caller
    // already inside Row B would time out just as wrongly as a real deadlock would. The only sound fix
    // is detecting an actual wait-for cycle before blocking at all, not guessing from a fixed deadline.
    //
    // GraphLock guards two small maps, shared across every ComponoServiceProvider instance (this is
    // deliberately static, not per-instance - a cycle can span any number of rows, so the bookkeeping
    // has to see all of them): which thread currently owns each adapter's lock, and which adapter (if
    // any) each thread is currently blocked trying to acquire. Before blocking on an adapter another
    // thread owns, this walks the wait-for chain starting at that owner: if it leads back to the
    // CURRENT thread, granting this wait would close a cycle - refuse immediately with a diagnosed
    // exception instead of blocking (a real cycle can never resolve itself by waiting longer). If it
    // doesn't, block with a PLAIN, unbounded Monitor.Enter - safe by construction, since the cycle
    // check already proved this wait cannot be part of a cycle, so no arbitrary timeout is needed to
    // "protect" it, and none is imposed. This is what lets a legitimately slow nested cross-row call
    // (finding from this same review round) succeed no matter how long it takes, while a genuine cycle
    // is refused instantly rather than only after some fixed wait.
    private static readonly object GraphLock = new();
    private static readonly Dictionary<ComponoServiceProvider, Thread> Owners = [];
    private static readonly Dictionary<Thread, ComponoServiceProvider> WaitingFor = [];

    // Monitor.Enter/Exit on the SAME thread is reentrant - a registration factory that calls back into
    // its OWN row's adapter for a different type re-enters the same _lock without blocking. Without
    // this, the inner call's `finally` would remove the Owners entry entirely while the OUTER call
    // still holds the lock, leaving a window where this adapter's lock is genuinely held but Owners
    // says nobody owns it - a different thread checking during that window would neither detect a
    // would-be cycle nor register its own wait, silently defeating the cycle check for exactly the case
    // it exists to catch. Tracked per-adapter, incremented/decremented in lockstep with every
    // Monitor.Enter/Exit pair; Owners[this] is only cleared when depth reaches zero, i.e. when the
    // outermost call actually releases the underlying Monitor.
    private static readonly Dictionary<ComponoServiceProvider, int> OwnerDepth = [];

    internal ComponoServiceProvider(CompositionRow row)
    {
        _row = row;
    }

    public object? GetService(Type serviceType)
    {
        var thisThread = Thread.CurrentThread;

        lock (GraphLock)
        {
            if (Owners.TryGetValue(this, out var ownerThread) && ownerThread != thisThread)
            {
                // Walk the chain of threads this wait would transitively depend on: does it lead back
                // to this thread? The self-check runs BEFORE the visited-set dedup guard on every
                // iteration - reversing that order would let a genuine cycle back to this thread get
                // silently swallowed by the dedup check instead of ever being reported (this thread
                // would already be "seen" from nowhere, since it's never pre-seeded). The visited set
                // exists only to bound the walk defensively if bookkeeping were ever inconsistent for
                // some OTHER thread; it never suppresses the one check that matters.
                var probe = ownerThread;
                var visited = new HashSet<Thread>();
                while (true)
                {
                    if (probe == thisThread)
                    {
                        throw new CompositionException(
                            $"Resolving '{serviceType}' through this row's AsServiceProvider() adapter " +
                            "would deadlock: this thread is already, transitively, waiting on itself " +
                            "through one or more other rows' adapters. This is the cross-row cycle ADR-" +
                            "0047's Recursion section describes (row A's factory calling into row B, " +
                            "whose own resolution calls back into row A), hit concurrently rather than " +
                            "sequentially - refused immediately rather than blocking forever.");
                    }

                    if (!visited.Add(probe))
                    {
                        break;
                    }

                    if (!WaitingFor.TryGetValue(probe, out var nextTarget) ||
                        !Owners.TryGetValue(nextTarget, out var nextOwner))
                    {
                        break;
                    }

                    probe = nextOwner;
                }

                WaitingFor[thisThread] = this;
            }
        }

        Monitor.Enter(_lock);

        lock (GraphLock)
        {
            WaitingFor.Remove(thisThread);
            Owners[this] = thisThread;
            OwnerDepth[this] = OwnerDepth.GetValueOrDefault(this) + 1;
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
            lock (GraphLock)
            {
                var depth = OwnerDepth[this] - 1;
                if (depth <= 0)
                {
                    OwnerDepth.Remove(this);
                    Owners.Remove(this);
                }
                else
                {
                    OwnerDepth[this] = depth;
                }
            }

            Monitor.Exit(_lock);
        }
    }
}
