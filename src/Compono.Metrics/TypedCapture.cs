using System.Diagnostics.Metrics;

namespace Compono.Metrics;

/// <summary>
/// The typed capture buffer for one published instrument. Measurements are appended here without boxing,
/// the sequence number is assigned under <see cref="CaptureState.Gate"/> in the same lock acquisition as
/// the append, and a snapshot copies out. Created only by <see cref="CaptureState"/> once, at publish
/// time, from a closed generic instantiation the compiler already sees statically - never by reflection or
/// dynamic activation (ADR-0072 "Native AOT and trimming"). Internal.
/// </summary>
/// <typeparam name="T">One of the seven supported measurement value types.</typeparam>
internal sealed class TypedCapture<T> : IMeasurementBuffer<T>
    where T : struct
{
    private readonly CaptureState _state;
    private readonly List<StoredMeasurement<T>> _measurements = [];

    public TypedCapture(CaptureState state, Instrument instrument)
    {
        _state = state;
        Instrument = instrument;
    }

    /// <inheritdoc />
    public Instrument Instrument { get; }

    /// <inheritdoc />
    public Type ValueType => typeof(T);

    /// <summary>
    /// Appends one measurement. The tags are copied out of the BCL's span <b>before</b> the lock is taken
    /// (ADR-0072 "Concurrency and allocation"); the sequence number is assigned inside the same lock
    /// acquisition that appends (ADR-0072 "Capture order").
    /// </summary>
    public void Record(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagCopy = CopyTags(tags);

        lock (_state.Gate)
        {
            var sequence = _state.NextSequence++;
            _measurements.Add(new StoredMeasurement<T>(
                sequence,
                new CapturedMeasurement<T>(value, tagCopy, Instrument)));
        }
    }

    /// <inheritdoc />
    public void Clear() => _measurements.Clear();

    /// <inheritdoc />
    public void CopyTo(List<StoredMeasurement<T>> destination)
    {
        for (var i = 0; i < _measurements.Count; i++)
            destination.Add(_measurements[i]);
    }

    private static KeyValuePair<string, object?>[] CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
            return [];

        var copy = new KeyValuePair<string, object?>[tags.Length];
        for (var i = 0; i < tags.Length; i++)
            copy[i] = tags[i];

        return copy;
    }
}
