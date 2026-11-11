using System.Diagnostics.Metrics;

namespace Compono.Metrics;

/// <summary>
/// A hand-written, reflection-free <see cref="IMeterFactory"/> that captures the measurements of every
/// instrument its meters publish into an inspectable, thread-safe, Compono-native model
/// (<see cref="CapturedMeasurement{T}"/>). Directly, publicly constructible - composing through
/// <see cref="CompositionBuilderExtensions.UseMetrics"/> is not required. See
/// docs/adr/0072-compono-metrics-testing-support-package.md.
/// </summary>
/// <remarks>
/// <para>
/// Factory semantics match <c>DefaultMeterFactory</c>: <see cref="Create"/>
/// rejects a <see langword="null"/> <see cref="MeterOptions"/> and a foreign <see cref="MeterOptions.Scope"/>,
/// throws <see cref="ObjectDisposedException"/> after disposal, never mutates the caller's options, and caches
/// meters by name, version, and order-insensitive tag set (duplicate tag keys are significant - see
/// <see cref="Create"/>).
/// </para>
/// <para>
/// Each factory owns one lazily created <see cref="MeterListener"/>, created on the first <see cref="Create"/>
/// and disposed with the factory. It enables only instruments whose <see cref="Meter.Scope"/> is this factory,
/// so two factories never see each other's measurements even for identical meter and instrument names. There
/// is no static or shared capture state.
/// </para>
/// <para>
/// Disposal is the test's responsibility (<see cref="IMeterFactory"/> already extends
/// <see cref="IDisposable"/>); composition never disposes the factory. An undisposed factory that created
/// meters stays referenced by the BCL's static meter list for the process lifetime - the same cost as an
/// undisposed <c>DefaultMeterFactory</c>.
/// </para>
/// </remarks>
public sealed class CapturingMeterFactory : IMeterFactory
{
    // A plain object lock, not System.Threading.Lock - this project multi-targets net8.0, which predates
    // that type. Guards meter caching, the lazy listener, and the disposed flag. Capture appends/reads use
    // CaptureState's own gate (one lock for capture, one for the factory's meter/listener lifecycle).
    private readonly object _factoryGate = new();
    private readonly CaptureState _capture = new();
    private readonly List<MeterEntry> _meters = [];

    private MeterListener? _listener;
    private bool _disposed;

    /// <summary>
    /// Creates a standalone <see cref="CapturingMeterFactory"/>, usable with no Compono composition involved
    /// at all.
    /// </summary>
    public CapturingMeterFactory()
    {
    }

    /// <summary>
    /// Creates a <see cref="Meter"/> bound to this factory. The same name, version, and tag set
    /// (order-insensitive, <see cref="object.Equals(object, object)"/> on values) returns the same
    /// <see cref="Meter"/>; anything else creates a new one. Duplicate tag keys are significant:
    /// <c>[k=1, k=1]</c> is a different set from <c>[k=1, j=2]</c>.
    /// </summary>
    /// <param name="options">The meter options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="options"/>'s <see cref="MeterOptions.Scope"/>
    /// is non-<see langword="null"/> and isn't this factory.</exception>
    /// <exception cref="ObjectDisposedException">This factory has been disposed.</exception>
    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_factoryGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (options.Scope is not null && !ReferenceEquals(options.Scope, this))
            {
                throw new InvalidOperationException(
                    "The MeterOptions.Scope of a meter created by a CapturingMeterFactory must be null or " +
                    "this factory. To record a SUT's measurements, give it this factory as its IMeterFactory " +
                    "and let it create its own meters.");
            }

            var tags = NormalizeTags(options.Tags);

            foreach (var entry in _meters)
            {
                if (entry.Matches(options.Name, options.Version, tags))
                    return entry.Meter;
            }

            EnsureListener();

            var owned = new MeterOptions(options.Name)
            {
                Version = options.Version,
                Scope = this,
                Tags = tags,
            };

            var meter = new Meter(owned);
            _meters.Add(new MeterEntry(options.Name, options.Version, tags, meter));
            return meter;
        }
    }

    /// <summary>
    /// Disposes every meter this factory created (raising
    /// <c>MeasurementsCompleted</c>), then its <see cref="MeterListener"/>. Idempotent and
    /// thread-safe. Captured measurements stay readable afterwards; measurements recorded on the disposed
    /// meters' instruments are no longer captured.
    /// </summary>
    public void Dispose()
    {
        lock (_factoryGate)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        foreach (var entry in _meters)
            entry.Meter.Dispose();

        _listener?.Dispose();
    }

    /// <summary>
    /// Calls this factory's listener's <see cref="MeterListener.RecordObservableInstruments"/>. Callbacks run
    /// synchronously on the calling thread and only for this factory's observable instruments; each
    /// observation appends measurements. A no-op before the first <see cref="Create"/> or after disposal.
    /// </summary>
    internal void RecordObservableInstruments()
    {
        MeterListener? listener;
        lock (_factoryGate)
        {
            if (_disposed)
                return;

            listener = _listener;
        }

        listener?.RecordObservableInstruments();
    }

    /// <summary>Discards every captured measurement.</summary>
    internal void ClearMeasurements() => _capture.Clear();

    /// <summary>Resolves a selection per ADR-0072's seven ordered rules.</summary>
    internal SelectionResult<T> Select<T>(string? meterName, string instrumentName)
        where T : struct =>
        _capture.Select<T>(meterName, instrumentName);

    private void EnsureListener()
    {
        if (_listener is not null)
            return;

        var listener = new MeterListener
        {
            InstrumentPublished = OnInstrumentPublished,
        };

        listener.SetMeasurementEventCallback<byte>(OnMeasurement);
        listener.SetMeasurementEventCallback<short>(OnMeasurement);
        listener.SetMeasurementEventCallback<int>(OnMeasurement);
        listener.SetMeasurementEventCallback<long>(OnMeasurement);
        listener.SetMeasurementEventCallback<float>(OnMeasurement);
        listener.SetMeasurementEventCallback<double>(OnMeasurement);
        listener.SetMeasurementEventCallback<decimal>(OnMeasurement);

        listener.Start();
        _listener = listener;
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        // Scope isolation: enable only this factory's instruments, so two factories never see each other's
        // measurements even for identical names (RESEARCH-0039 fact 3, ADR-0072 "Capture semantics").
        if (!ReferenceEquals(instrument.Meter.Scope, this))
            return;

        var buffer = _capture.TryCreateBuffer(instrument);
        if (buffer is not null)
            listener.EnableMeasurementEvents(instrument, buffer);
    }

    private static void OnMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct
    {
        if (state is TypedCapture<T> capture)
            capture.Record(measurement, tags);
    }

    private static KeyValuePair<string, object?>[] NormalizeTags(IEnumerable<KeyValuePair<string, object?>>? tags)
    {
        if (tags is null)
            return [];

        var list = new List<KeyValuePair<string, object?>>();
        foreach (var tag in tags)
            list.Add(tag);

        return [.. list];
    }

    private static bool TagSetsEqual(
        KeyValuePair<string, object?>[] left,
        KeyValuePair<string, object?>[] right)
    {
        if (left.Length != right.Length)
            return false;

        // The common case (no meter tags) must not allocate.
        if (left.Length == 0)
            return true;

        // Order-insensitive bag equality: each entry on the left consumes exactly one distinct
        // entry on the right. A one-to-one match (rather than "every left entry matches some right
        // entry") is what makes duplicate keys behave correctly - [k=1, k=1] must not equal
        // [k=1, j=2] (both have length 2, and both left entries are individually present on the
        // right). This is intentionally stricter than DefaultMeterFactory, whose duplicate-tag
        // comparison is not a true multiset comparison; here a duplicate meter-tag key is
        // significant.
        var matched = new bool[right.Length];
        foreach (var tag in left)
        {
            var found = false;
            for (var i = 0; i < right.Length; i++)
            {
                if (matched[i])
                    continue;

                if (string.Equals(right[i].Key, tag.Key, StringComparison.Ordinal)
                    && Equals(right[i].Value, tag.Value))
                {
                    matched[i] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private sealed class MeterEntry
    {
        public MeterEntry(string name, string? version, KeyValuePair<string, object?>[] tags, Meter meter)
        {
            Name = name;
            Version = version;
            Tags = tags;
            Meter = meter;
        }

        public string Name { get; }

        public string? Version { get; }

        public KeyValuePair<string, object?>[] Tags { get; }

        public Meter Meter { get; }

        public bool Matches(string name, string? version, KeyValuePair<string, object?>[] tags) =>
            string.Equals(Name, name, StringComparison.Ordinal)
            && string.Equals(Version, version, StringComparison.Ordinal)
            && TagSetsEqual(Tags, tags);
    }
}
