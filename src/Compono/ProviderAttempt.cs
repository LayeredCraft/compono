namespace Compono;

/// <summary>One resolution-pipeline stage tried for one composition request, and what it resulted in.</summary>
/// <param name="Stage">Which pipeline stage this attempt was for.</param>
/// <param name="Provider">
/// The concrete <see cref="ICompositionProvider"/> type that made this attempt, or
/// <see langword="null"/> for a context-owned stage (shared/scoped values, exact registrations,
/// collection-plan/generated-plan dispatch) - those aren't <see cref="ICompositionProvider"/>
/// instances at all, per <c>docs/architecture.md</c>'s Resolution Pipeline table.
/// </param>
/// <param name="Outcome">What the stage resulted in.</param>
/// <remarks>
/// Deliberately compact - per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s trace-buffer
/// design: <see cref="CompositionTraceBuffer"/> appends one of these per stage tried and rewinds on
/// success, so this type has to be cheap enough to append without threatening the allocation-free
/// success path. <see cref="Provider"/> is a plain <see cref="Type"/> reference, not a runtime
/// reflection *operation* - identical in kind to <c>PlanCache&lt;T&gt;</c>'s own closed-generic-`Type`
/// identity and the active-construction-frame stack's `Type`-keyed lookup, both already established
/// elsewhere in this engine. See
/// <c>docs/adr/0016-provider-identity-restored-in-provider-attempt.md</c> for why this field exists
/// at all - <see cref="Compono.Providers.BuiltInProviders.Default"/> already registers three
/// providers in stage 7 today, not a hypothetical future case, so the trace needs a way to tell
/// them apart (supersedes
/// <c>docs/adr/0015-provider-identity-deferred-in-provider-attempt.md</c>'s deferral, which rested
/// on a premise that was already false when written).
/// </remarks>
public readonly record struct ProviderAttempt(PipelineStage Stage, Type? Provider, CompositionAttemptOutcome Outcome);
