namespace Compono.Metrics;

/// <summary>
/// A captured measurement paired with the factory-wide sequence number assigned to it when it was
/// appended. Internal - the sequence number is what selection rule 7 and every snapshot use to order a
/// merged selection; it is never part of the public surface (ADR-0072 "Capture order"). The name
/// deliberately avoids "entry" to leave message wording free.
/// </summary>
/// <typeparam name="T">The measurement's value type.</typeparam>
internal readonly struct StoredMeasurement<T>
    where T : struct
{
    public StoredMeasurement(long sequence, CapturedMeasurement<T> measurement)
    {
        Sequence = sequence;
        Measurement = measurement;
    }

    /// <summary>The factory-wide sequence number, assigned inside the same lock acquisition that appended
    /// this measurement.</summary>
    public long Sequence { get; }

    /// <summary>The captured measurement itself.</summary>
    public CapturedMeasurement<T> Measurement { get; }
}
