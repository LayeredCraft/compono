namespace Compono;

/// <summary>One resolution-pipeline stage tried for one composition request, and what it resulted in.</summary>
/// <param name="Stage">Which pipeline stage this attempt was for.</param>
/// <param name="Outcome">What the stage resulted in.</param>
/// <remarks>
/// Deliberately compact - no strings, no reflection metadata - per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s trace-buffer
/// design: <see cref="CompositionTraceBuffer"/> appends one of these per stage tried and rewinds on
/// success, so this type has to be cheap enough to append without threatening the allocation-free
/// success path. No provider-id field yet, even though ADR-0010's own text describes one - see
/// <c>docs/adr/0015-provider-identity-deferred-in-provider-attempt.md</c> for why: no stage
/// registers more than one competing provider today, so there's nothing yet for an identity field
/// to discriminate.
/// </remarks>
public readonly record struct ProviderAttempt(PipelineStage Stage, CompositionAttemptOutcome Outcome);
