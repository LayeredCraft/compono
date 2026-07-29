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
}
