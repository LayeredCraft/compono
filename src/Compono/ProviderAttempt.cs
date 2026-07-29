namespace Compono;

/// <summary>One resolution-pipeline stage tried for one composition request, and what it resulted in.</summary>
/// <param name="Stage">Which pipeline stage this attempt was for.</param>
/// <param name="Outcome">What the stage resulted in.</param>
/// <remarks>
/// Deliberately compact - no strings, no reflection metadata - per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s trace-buffer
/// design: <see cref="CompositionTraceBuffer"/> appends one of these per stage tried and rewinds on
/// success, so this type has to be cheap enough to append without threatening the allocation-free
/// success path.
/// </remarks>
public readonly record struct ProviderAttempt(PipelineStage Stage, CompositionAttemptOutcome Outcome);
