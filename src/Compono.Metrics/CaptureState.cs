using System.Diagnostics.Metrics;
using System.Text;

namespace Compono.Metrics;

/// <summary>
/// The capture state behind one <see cref="CapturingMeterFactory"/>: a single lock that guards appends,
/// clears, and reads (ADR-0072 "Concurrency and allocation"), a factory-wide sequence counter, and the
/// list of typed per-instrument buffers this factory has published. One instance per factory - there is no
/// static or shared capture state (ADR-0072 "Capture semantics"). Internal.
/// </summary>
internal sealed class CaptureState
{
    /// <summary>The single factory-wide lock. A plain <see cref="object"/> lock, not
    /// <c>System.Threading.Lock</c> - this package multi-targets net8.0, which predates that type.</summary>
    internal readonly object Gate = new();

    /// <summary>The next factory-wide sequence number. Only ever read or written while holding
    /// <see cref="Gate"/>.</summary>
    internal long NextSequence;

    private readonly List<IMeasurementBuffer> _buffers = [];

    /// <summary>
    /// Creates the typed buffer for <paramref name="instrument"/> and registers it, or returns
    /// <see langword="null"/> when the instrument's value type isn't one of the seven supported types. The
    /// buffer is a closed generic instantiation the compiler sees statically (no reflection, no dynamic
    /// activation); it is passed straight to
    /// <see cref="MeterListener.EnableMeasurementEvents(Instrument, object?)"/> as the instrument's state.
    /// </summary>
    public IMeasurementBuffer? TryCreateBuffer(Instrument instrument)
    {
        if (TryCreate<byte>(instrument, out var buffer))
            return Register(buffer);
        if (TryCreate<short>(instrument, out buffer))
            return Register(buffer);
        if (TryCreate<int>(instrument, out buffer))
            return Register(buffer);
        if (TryCreate<long>(instrument, out buffer))
            return Register(buffer);
        if (TryCreate<float>(instrument, out buffer))
            return Register(buffer);
        if (TryCreate<double>(instrument, out buffer))
            return Register(buffer);
        if (TryCreate<decimal>(instrument, out buffer))
            return Register(buffer);

        return null;
    }

    /// <summary>Discards every captured measurement, across every published instrument.</summary>
    public void Clear()
    {
        lock (Gate)
        {
            foreach (var buffer in _buffers)
                buffer.Clear();
        }
    }

    /// <summary>
    /// Resolves a selection exactly per ADR-0072's seven ordered rules. Rules 1 (unsupported
    /// <typeparamref name="T"/> and null/empty names) are enforced by the caller before this is reached.
    /// </summary>
    /// <param name="meterName">The meter name for the two-argument overload, or <see langword="null"/> for
    /// the one-argument overload (which makes different meter names ambiguous, rule 4).</param>
    /// <param name="instrumentName">The instrument name to match.</param>
    public SelectionResult<T> Select<T>(string? meterName, string instrumentName)
        where T : struct
    {
        lock (Gate)
        {
            var existing = new List<string>(_buffers.Count);
            var candidates = new List<IMeasurementBuffer>();

            foreach (var buffer in _buffers)
            {
                existing.Add(DescribeInstrument(buffer.Instrument, buffer.ValueType));

                if (NameMatches(buffer.Instrument, meterName, instrumentName))
                    candidates.Add(buffer);
            }

            // Rule 3: unknown name - empty result (a failed count lists what does exist).
            if (candidates.Count == 0)
                return SelectionResult<T>.NoMatch(existing);

            // Rule 4: meter ambiguity (one-argument overload only).
            if (meterName is null && CountDistinctMeterNames(candidates) > 1)
                throw new InvalidOperationException(AmbiguityMessage(instrumentName, candidates));

            // Rule 5: type conflict - more than one value type, even if one of them is T.
            if (!AllSameValueType(candidates, out var valueType))
                throw new InvalidOperationException(ConflictMessage(instrumentName, candidates));

            // Rule 6: type mismatch - one type, but not T.
            if (valueType != typeof(T))
                throw new InvalidOperationException(
                    $"instrument \"{instrumentName}\" records {InstrumentValueTypes.DisplayName(valueType!)}, " +
                    $"not {InstrumentValueTypes.DisplayName(typeof(T))}.");

            // Rule 7: merge, in capture order.
            var stored = new List<StoredMeasurement<T>>();
            foreach (var buffer in candidates)
                ((IMeasurementBuffer<T>)buffer).CopyTo(stored);

            stored.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));

            var merged = new List<CapturedMeasurement<T>>(stored.Count);
            foreach (var measurement in stored)
                merged.Add(measurement.Measurement);

            return SelectionResult<T>.Match(merged, existing);
        }
    }

    private IMeasurementBuffer Register(IMeasurementBuffer buffer)
    {
        lock (Gate)
        {
            _buffers.Add(buffer);
        }

        return buffer;
    }

    private bool TryCreate<T>(Instrument instrument, out IMeasurementBuffer buffer)
        where T : struct
    {
        if (IsCapturedKind<T>(instrument))
        {
            buffer = new TypedCapture<T>(this, instrument);
            return true;
        }

        buffer = null!;
        return false;
    }

    private static bool IsCapturedKind<T>(Instrument instrument)
        where T : struct =>
        instrument is Instrument<T> or ObservableInstrument<T>;

    private static bool NameMatches(Instrument instrument, string? meterName, string instrumentName)
    {
        if (!string.Equals(instrument.Name, instrumentName, StringComparison.Ordinal))
            return false;

        return meterName is null
            || string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal);
    }

    private static int CountDistinctMeterNames(List<IMeasurementBuffer> candidates)
    {
        var names = new List<string>();
        foreach (var candidate in candidates)
        {
            if (!names.Contains(candidate.Instrument.Meter.Name))
                names.Add(candidate.Instrument.Meter.Name);
        }

        return names.Count;
    }

    private static bool AllSameValueType(List<IMeasurementBuffer> candidates, out Type? valueType)
    {
        valueType = candidates[0].ValueType;
        for (var i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].ValueType != valueType)
            {
                valueType = null;
                return false;
            }
        }

        return true;
    }

    private static string AmbiguityMessage(string instrumentName, List<IMeasurementBuffer> candidates)
    {
        var builder = new StringBuilder();
        builder.Append($"Instrument \"{instrumentName}\" matches candidates in more than one meter: ");
        AppendCandidateList(builder, candidates);
        builder.Append(". Use the two-argument GetMeasurements/Verify overload with a meter name to select one.");
        return builder.ToString();
    }

    private static string ConflictMessage(string instrumentName, List<IMeasurementBuffer> candidates)
    {
        var builder = new StringBuilder();
        builder.Append($"Instrument \"{instrumentName}\" matches candidates that record more than one value type: ");
        AppendCandidateList(builder, candidates);
        builder.Append(". One selection can't merge measurements of different types - rename an instrument in the " +
            "SUT, or narrow with the two-argument overload when the conflicting instruments live in differently " +
            "named meters.");
        return builder.ToString();
    }

    private static void AppendCandidateList(StringBuilder builder, List<IMeasurementBuffer> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (i > 0)
                builder.Append("; ");

            builder.Append(DescribeInstrument(candidates[i].Instrument, candidates[i].ValueType));
        }
    }

    private static string DescribeInstrument(Instrument instrument, Type valueType)
    {
        var meter = instrument.Meter;
        var version = string.IsNullOrEmpty(meter.Version) ? string.Empty : $" v{meter.Version}";
        return $"meter \"{meter.Name}\"{version}: instrument \"{instrument.Name}\" " +
            $"({InstrumentValueTypes.DisplayName(valueType)})";
    }
}
