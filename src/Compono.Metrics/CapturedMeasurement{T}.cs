using System.Diagnostics.Metrics;

namespace Compono.Metrics;

/// <summary>
/// A single captured measurement: the recorded value, its tags, and the <see cref="Instrument"/> that
/// recorded it. A plain <see langword="readonly"/> <see langword="struct"/> - deliberately not a
/// <see langword="record"/> <see langword="struct"/>, so there is no synthesized value equality that
/// would suggest <c>==</c> compares <see cref="Tags"/> by value; use <see cref="Value"/> directly or
/// the <see cref="MeterFactoryTestingExtensions.Verify{T}(IMeterFactory, string)"/> filter chain.
/// See docs/adr/0072-compono-metrics-testing-support-package.md.
/// </summary>
/// <typeparam name="T">The measurement's value type - one of the seven BCL measurement value types
/// (<see cref="byte"/>, <see cref="short"/>, <see cref="int"/>, <see cref="long"/>, <see cref="float"/>,
/// <see cref="double"/>, <see cref="decimal"/>).</typeparam>
public readonly struct CapturedMeasurement<T>
    where T : struct
{
    internal CapturedMeasurement(T value, IReadOnlyList<KeyValuePair<string, object?>> tags, Instrument instrument)
    {
        Value = value;
        Tags = tags;
        Instrument = instrument;
    }

    /// <summary>The recorded value, exactly as the instrument received it (no normalization, no
    /// aggregation).</summary>
    public T Value { get; }

    /// <summary>
    /// This measurement's tags, copied out of the BCL's callback span in callback order. A per-measurement
    /// copy that never aliases the BCL's span or any other measurement's tags; empty (never
    /// <see langword="null"/>) when the measurement had no tags. Meter-level and instrument-level tags are
    /// not merged in - they're reachable through <see cref="Instrument"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Tags { get; }

    /// <summary>
    /// The actual <see cref="Instrument"/> instance that recorded this measurement (the one the SUT
    /// created). Through <see cref="System.Diagnostics.Metrics.Instrument.Meter"/> it references the
    /// actual <see cref="Meter"/>, and through <see cref="Meter.Scope"/> the factory. Descriptive members
    /// (for example <see cref="System.Diagnostics.Metrics.Instrument.Name"/>,
    /// <see cref="System.Diagnostics.Metrics.Instrument.Enabled"/>, and the meter's name/version/tags)
    /// stay readable after the factory is disposed.
    /// </summary>
    public Instrument Instrument { get; }
}
