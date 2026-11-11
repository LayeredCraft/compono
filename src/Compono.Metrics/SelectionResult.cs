namespace Compono.Metrics;

/// <summary>
/// The outcome of resolving a selection (ADR-0072 "Selection and matching"). Carries the merged
/// measurements for the requested <typeparamref name="T"/>, whether any instrument matched the name at
/// all (rule 3, which decides whether a failed count lists existing instruments), and a description of
/// every instrument the factory has published for that diagnostic. Internal.
/// </summary>
/// <typeparam name="T">The requested measurement value type.</typeparam>
internal readonly struct SelectionResult<T>
    where T : struct
{
    private SelectionResult(
        IReadOnlyList<CapturedMeasurement<T>> measurements,
        bool hasCandidates,
        IReadOnlyList<string> existingInstruments)
    {
        Measurements = measurements;
        HasCandidates = hasCandidates;
        ExistingInstruments = existingInstruments;
    }

    /// <summary>The merged, capture-order measurements for <typeparamref name="T"/>.</summary>
    public IReadOnlyList<CapturedMeasurement<T>> Measurements { get; }

    /// <summary><see langword="false"/> only when no instrument matched the requested name (rule 3).</summary>
    public bool HasCandidates { get; }

    /// <summary>One description line per published instrument (meter name, version, instrument name, value
    /// type), for the "existing instruments" diagnostic on a failed count against an unknown name.</summary>
    public IReadOnlyList<string> ExistingInstruments { get; }

    /// <summary>Rule 3: no instrument matched the requested name - an empty result.</summary>
    public static SelectionResult<T> NoMatch(IReadOnlyList<string> existingInstruments) =>
        new([], hasCandidates: false, existingInstruments);

    /// <summary>Rules 4-7 resolved: candidate instruments exist and their measurements are merged for
    /// <typeparamref name="T"/>.</summary>
    public static SelectionResult<T> Match(
        IReadOnlyList<CapturedMeasurement<T>> measurements,
        IReadOnlyList<string> existingInstruments) =>
        new(measurements, hasCandidates: true, existingInstruments);
}
