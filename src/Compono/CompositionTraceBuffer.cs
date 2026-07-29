namespace Compono;

/// <summary>
/// One composition operation's reusable, array-backed trace of resolution-pipeline attempts - the
/// mechanism behind <see cref="CompositionDiagnostic.Trace"/>.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>: one instance
/// per <see cref="CompositionContext"/> (matching its one-root-operation lifetime), never shared
/// across concurrent operations. Each <c>Resolve&lt;T&gt;</c> call records a <see cref="Checkpoint"/>
/// on entry, appends a <see cref="ProviderAttempt"/> per stage tried, then calls <see cref="Rewind"/>
/// back to that checkpoint immediately before returning on success - a nested call's attempts never
/// pollute a later sibling's trace, and nothing survives past a successful root
/// <c>Resolve&lt;T&gt;</c>. A failing request skips its own rewind (the exception propagates past it),
/// so every ancestor's still-open attempts, plus the failing branch's own, remain in the buffer for
/// <see cref="Slice"/> to materialize into the durable <see cref="CompositionDiagnostic"/>.
/// </remarks>
/// <remarks>
/// Growth is <em>not</em> allocation-free (PR #13 review): each active ancestor frame dispatching
/// through stage 8/a collection plan retains ~6 entries (5 declined stages plus a
/// <see cref="CompositionAttemptOutcome.Pending"/> marker) until its own child returns, so a
/// composable-type chain more than ~5 levels deep (already true of
/// <c>docs/architecture.md</c>'s own Diagnostics example - 4 levels) exceeds this buffer's initial
/// capacity and triggers a real <see cref="Array.Resize{T}"/> allocation, same as any growable
/// array-backed collection (<see cref="List{T}"/> included). The initial capacity is sized to cover
/// a typical graph without resizing, not to make resizing impossible - <c>docs/performance.md</c>
/// records a real measurement of a deep graph that does trigger one.
/// </remarks>
internal sealed class CompositionTraceBuffer
{
    // Covers roughly 5 levels of generated-plan/collection-plan nesting (~6 entries retained per
    // active ancestor frame) without a resize - see this type's remarks for the real, measured cost
    // once a graph goes deeper than that.
    private ProviderAttempt[] _attempts = new ProviderAttempt[32];
    private int _count;

    /// <summary>
    /// The buffer's current length - read at a <c>Resolve&lt;T&gt;</c> call's entry to mark where its
    /// own attempts begin, for a later <see cref="Rewind"/>.
    /// </summary>
    internal int Checkpoint => _count;

    /// <summary>
    /// Appends one stage attempt. <paramref name="provider"/> is the concrete
    /// <see cref="ICompositionProvider"/> type that made the attempt, or <see langword="null"/> for
    /// a context-owned stage (per <see cref="ProviderAttempt.Provider"/>'s remarks).
    /// </summary>
    internal void Record(PipelineStage stage, Type? provider, CompositionAttemptOutcome outcome)
    {
        if (_count == _attempts.Length)
            Array.Resize(ref _attempts, _attempts.Length * 2);

        _attempts[_count] = new ProviderAttempt(stage, provider, outcome);
        _count++;
    }

    /// <summary>Discards every attempt recorded since <paramref name="checkpoint"/> - the on-success path.</summary>
    internal void Rewind(int checkpoint) => _count = checkpoint;

    /// <summary>
    /// Copies every attempt recorded since <paramref name="checkpoint"/> into a durable array - done
    /// once per failing request, before the buffer unwinds further.
    /// </summary>
    internal IReadOnlyList<ProviderAttempt> Slice(int checkpoint) => _attempts[checkpoint.._count];
}
