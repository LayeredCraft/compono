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
/// <see cref="Slice"/> to materialize into the durable <see cref="CompositionDiagnostic"/> - the only
/// point this type allocates.
/// </remarks>
internal sealed class CompositionTraceBuffer
{
    private ProviderAttempt[] _attempts = new ProviderAttempt[16];
    private int _count;

    /// <summary>
    /// The buffer's current length - read at a <c>Resolve&lt;T&gt;</c> call's entry to mark where its
    /// own attempts begin, for a later <see cref="Rewind"/>.
    /// </summary>
    internal int Checkpoint => _count;

    /// <summary>Appends one stage attempt.</summary>
    internal void Record(PipelineStage stage, CompositionAttemptOutcome outcome)
    {
        if (_count == _attempts.Length)
            Array.Resize(ref _attempts, _attempts.Length * 2);

        _attempts[_count] = new ProviderAttempt(stage, outcome);
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
