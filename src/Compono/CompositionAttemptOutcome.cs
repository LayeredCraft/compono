namespace Compono;

/// <summary>What a <see cref="ProviderAttempt"/> resulted in.</summary>
public enum CompositionAttemptOutcome
{
    /// <summary>Nothing at this stage applied to the request.</summary>
    NotHandled,

    /// <summary>This stage composed the requested value.</summary>
    Success,

    /// <summary>
    /// This stage established authoritative ownership of the request but couldn't complete it (an
    /// invalid shared/registered value, or a detected construction cycle).
    /// </summary>
    Failure,

    /// <summary>
    /// This stage took ownership of the request and began composing it, but hasn't concluded -
    /// recorded for a generated-plan or collection-plan dispatch immediately before invoking
    /// <c>Compose</c>, so an ancestor still in flight when a descendant fails isn't silently absent
    /// from the materialized trace. Never survives a successful resolution - the eventual
    /// <see cref="Success"/> entry recorded alongside it is what gets rewound away
    /// (<see cref="CompositionTraceBuffer"/>'s remarks); only on failure does this entry (with no
    /// following <see cref="Success"/>/<see cref="Failure"/> at the same position) stay in the trace.
    /// </summary>
    Pending,
}
