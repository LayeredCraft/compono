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

    // PR #105 review, round four: rounds one through three of this fix (a fixed timeout, then a
    // nested-only timeout, then real wait-for-cycle detection) all still had a per-adapter Monitor
    // (`_lock`) as the actual serialization mechanism, with a SEPARATE step publishing ownership into
    // the cycle-detection graph afterward. That left a genuine race: a thread could win `_lock` but be
    // descheduled before publishing `Owners[this]`, during which a second thread's cycle check would
    // see this adapter as unowned, skip registering its own wait, and block directly on `_lock` -
    // invisible to the graph. If the roles then reversed, both threads could deadlock for real, with
    // the detector never seeing it. Narrowing that window further wouldn't close it - any two separate
    // steps (acquire, then publish) leave A window, however small.
    //
    // The actual fix removes the separate per-adapter lock object entirely. Ownership itself now IS the
    // synchronization primitive: "does a thread hold this adapter" is defined purely by presence in
    // `Owners`, mutated only while holding `GraphLock`, and waiting is done via `Monitor.Wait(GraphLock)`
    // / `Monitor.PulseAll(GraphLock)` rather than a second lock object. Because `Monitor.Wait` atomically
    // releases and re-acquires the SAME lock it's called under, there is no instant where a thread holds
    // "ownership" without every other thread's view of `Owners` already reflecting it - acquisition and
    // publication happen in the same critical section, always. `Monitor.PulseAll` (not the earlier
    // targeted-wakeup scheme) wakes every waiter on release; each re-checks its own condition and goes
    // back to sleep if its own adapter is still held by someone else. This is the standard
    // correct-by-construction pattern for a lock a deadlock detector protects (a global monitor guarding
    // both the resource-ownership state AND the actual blocking), not an optimization over the
    // two-lock scheme - the earlier design could not be patched to close this window without becoming
    // exactly this.
    private static readonly object GraphLock = new();
    private static readonly Dictionary<ComponoServiceProvider, Thread> Owners = [];
    private static readonly Dictionary<ComponoServiceProvider, int> OwnerDepth = [];
    private static readonly Dictionary<Thread, ComponoServiceProvider> WaitingFor = [];

    internal ComponoServiceProvider(CompositionRow row)
    {
        _row = row;
    }

    public object? GetService(Type serviceType)
    {
        var thisThread = Thread.CurrentThread;

        lock (GraphLock)
        {
            while (true)
            {
                if (!Owners.TryGetValue(this, out var ownerThread))
                {
                    // Free - acquire and publish ownership in this same critical section. No other
                    // thread can ever observe this adapter as "unowned" once this returns, because both
                    // happen atomically under GraphLock.
                    Owners[this] = thisThread;
                    OwnerDepth[this] = 1;
                    WaitingFor.Remove(thisThread);
                    break;
                }

                if (ownerThread == thisThread)
                {
                    // Reentrant same-row call (this thread already owns this adapter, e.g. a factory
                    // calling back into its own row for a different type) - just bump the depth.
                    OwnerDepth[this]++;
                    break;
                }

                // Someone else owns it. The graph is fully consistent here - nothing else can mutate
                // Owners/WaitingFor while this thread holds GraphLock - so this walk can't miss an edge
                // the two-lock design's race allowed. Does the chain of "who does the owner's owner
                // wait for, transitively" lead back to this thread? The self-check runs BEFORE the
                // visited-set dedup guard on every iteration - reversing that order would let a genuine
                // cycle back to this thread get silently swallowed by the dedup check instead of ever
                // being reported.
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

                // No cycle - wait for a release. Monitor.Wait atomically releases GraphLock and blocks,
                // then re-acquires it before returning, so there is no gap between "about to wait" and
                // "actually waiting" a release could slip through unnoticed (the classic lost-wakeup
                // problem this pattern exists to avoid).
                WaitingFor[thisThread] = this;
                Monitor.Wait(GraphLock);
                WaitingFor.Remove(thisThread);
                // Loop back around: PulseAll wakes every waiter, not just ones waiting on THIS adapter,
                // so re-check whether it's actually free now before assuming so.
            }
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
                    Monitor.PulseAll(GraphLock);
                }
                else
                {
                    OwnerDepth[this] = depth;
                }
            }
        }
    }
}
