using System.Diagnostics.Metrics;

namespace Compono.Metrics;

/// <summary>
/// Non-generic view of one published instrument's typed capture buffer - what a
/// <see cref="CaptureState"/> needs in order to enumerate published instruments, answer selection rules
/// 2-5 from an instrument's metadata alone, and clear every buffer. Internal.
/// </summary>
internal interface IMeasurementBuffer
{
    /// <summary>The instrument whose measurements this buffer owns.</summary>
    Instrument Instrument { get; }

    /// <summary>The instrument's value type - which of the seven supported types this buffer stores.</summary>
    Type ValueType { get; }

    /// <summary>Discards every measurement in this buffer. The caller must already hold
    /// <see cref="CaptureState.Gate"/>.</summary>
    void Clear();
}

/// <summary>
/// Typed view of a capture buffer, used by selection rule 7 to merge one value type's measurements across
/// several same-named instruments. Internal. <typeparamref name="T"/> always equals
/// <see cref="IMeasurementBuffer.ValueType"/>.
/// </summary>
/// <typeparam name="T">One of the seven supported measurement value types.</typeparam>
internal interface IMeasurementBuffer<T> : IMeasurementBuffer
    where T : struct
{
    /// <summary>Appends this buffer's measurements (already in factory-wide sequence order) to
    /// <paramref name="destination"/>. The caller must already hold <see cref="CaptureState.Gate"/>.</summary>
    void CopyTo(List<StoredMeasurement<T>> destination);
}
