namespace Compono;

/// <summary>
/// Which resolution-pipeline stage (<c>docs/architecture.md</c>) a <see cref="ProviderAttempt"/> was
/// tried at.
/// </summary>
/// <remarks>
/// No case for stage 1 (explicit values, no mechanism until Milestone 3) or stage 9 (diagnostic
/// failure, the terminal absence of any attempt succeeding, not an attempt itself) - only stages
/// that can actually record a <see cref="ProviderAttempt"/> today have a case.
/// </remarks>
public enum PipelineStage
{
    /// <summary>Stage 2: shared or scoped values.</summary>
    SharedOrScopedValue,

    /// <summary>Stage 3: exact registrations.</summary>
    ExactRegistration,

    /// <summary>Stage 4: profile rules.</summary>
    ProfileRule,

    /// <summary>Stage 5: semantic value providers.</summary>
    SemanticProvider,

    /// <summary>Stage 6: test-double providers.</summary>
    TestDoubleProvider,

    /// <summary>Stage 7: built-in value providers, including collection dispatch.</summary>
    BuiltInProvider,

    /// <summary>Stage 8: generated composition plans.</summary>
    GeneratedPlan,
}
