using System.Globalization;
using System.Text;

namespace Compono.Metrics;

/// <summary>
/// A fluent filter chain over a resolved selection of captured measurements, ending in a thin, one-line
/// forwarder to core <see cref="CallVerifier"/> - <see cref="Once"/>/<see cref="Never"/>/<see cref="Exactly"/>
/// each build a <see cref="CallVerifier"/> from the filtered match count right here and call the corresponding
/// member. <see cref="CallVerifier"/> itself is never part of this type's public surface, and no new
/// counting/<c>Times</c> abstraction is introduced. See
/// docs/adr/0072-compono-metrics-testing-support-package.md §"Selection and matching".
/// </summary>
/// <typeparam name="T">The measurement value type - one of the seven supported types.</typeparam>
public sealed class MeasurementVerificationBuilder<T>
    where T : struct
{
    private readonly IReadOnlyList<CapturedMeasurement<T>> _measurements;
    private readonly string _instrumentName;
    private readonly string? _meterName;
    private readonly IReadOnlyList<string> _existingInstruments;
    private readonly bool _hasCandidates;
    private readonly List<(string Description, Func<CapturedMeasurement<T>, bool> Predicate)> _filters = [];

    internal MeasurementVerificationBuilder(
        SelectionResult<T> selection,
        string instrumentName,
        string? meterName)
    {
        _measurements = selection.Measurements;
        _instrumentName = instrumentName;
        _meterName = meterName;
        _existingInstruments = selection.ExistingInstruments;
        _hasCandidates = selection.HasCandidates;
    }

    /// <summary>Restricts matches to measurements whose value equals <paramref name="value"/> exactly, using
    /// <see cref="EqualityComparer{T}.Default"/>. Floating-point equality is exact - use
    /// <see cref="Matching"/> for a tolerance.</summary>
    public MeasurementVerificationBuilder<T> WithValue(T value) =>
        Add($"value {FormatValue(value)}", measurement => EqualityComparer<T>.Default.Equals(measurement.Value, value));

    /// <summary>Restricts matches to measurements that contain a tag with key <paramref name="key"/> (ordinal)
    /// whose value satisfies <see cref="object.Equals(object, object)"/>. Other tags are ignored, and there is
    /// no numeric coercion (<c>200</c>, an <see cref="int"/>, doesn't match <c>200L</c>). Chain calls for
    /// several tags.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public MeasurementVerificationBuilder<T> WithTag(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Add(
            $"tag {key} = {FormatTagValue(value)}",
            measurement => HasTag(measurement, key, value));
    }

    /// <summary>Restricts matches to measurements that have no tag with key <paramref name="key"/> (ordinal).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public MeasurementVerificationBuilder<T> WithoutTag(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Add($"no tag {key}", measurement => !HasTagKey(measurement, key));
    }

    /// <summary>Restricts matches to measurements satisfying an arbitrary <paramref name="predicate"/> - the
    /// escape hatch for anything the named filters above don't cover (an exact tag-set match, a tolerance, and
    /// so on).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
    public MeasurementVerificationBuilder<T> Matching(Func<CapturedMeasurement<T>, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return Add("a custom condition", predicate);
    }

    /// <summary>Asserts the accumulated filters matched exactly once.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters did not match exactly once.</exception>
    public void Once() => Assert(verifier => verifier.Once());

    /// <summary>Asserts the accumulated filters never matched.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters matched at least once.</exception>
    public void Never() => Assert(verifier => verifier.Never());

    /// <summary>Asserts the accumulated filters matched exactly <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters did not match exactly
    /// <paramref name="times"/> times.</exception>
    public void Exactly(int times) => Assert(verifier => verifier.Exactly(times));

    /// <summary>Asserts the accumulated filters matched at least <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters matched fewer than
    /// <paramref name="times"/> times.</exception>
    public void AtLeast(int times) => Assert(verifier => verifier.AtLeast(times));

    /// <summary>Asserts the accumulated filters matched at most <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters matched more than
    /// <paramref name="times"/> times.</exception>
    public void AtMost(int times) => Assert(verifier => verifier.AtMost(times));

    private MeasurementVerificationBuilder<T> Add(string description, Func<CapturedMeasurement<T>, bool> predicate)
    {
        _filters.Add((description, predicate));
        return this;
    }

    private void Assert(Action<CallVerifier> assertion)
    {
        try
        {
            assertion(ToCallVerifier());
        }
        catch (TestDoubleVerificationException exception) when (!_hasCandidates)
        {
            // Rule 3 (unknown name) is the one case where the empty selection is itself the diagnosis. The
            // count message names the (missing) instrument; add why nothing was found, and what does exist.
            throw new TestDoubleVerificationException(exception.Message + DescribeKnownInstruments());
        }
    }

    private CallVerifier ToCallVerifier()
    {
        var matchCount = 0;
        foreach (var measurement in _measurements)
        {
            var matches = true;
            foreach (var (_, predicate) in _filters)
            {
                if (!predicate(measurement))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                matchCount++;
        }

        return new CallVerifier(matchCount, Describe());
    }

    private string Describe()
    {
        var builder = new StringBuilder("instrument \"");
        builder.Append(_instrumentName);
        builder.Append('"');

        if (_meterName is not null)
        {
            builder.Append(" in meter \"");
            builder.Append(_meterName);
            builder.Append('"');
        }

        builder.Append(" (");
        builder.Append(InstrumentValueTypes.DisplayName(typeof(T)));
        builder.Append(')');

        for (var i = 0; i < _filters.Count; i++)
        {
            builder.Append(i == 0 ? " with " : " and ");
            builder.Append(_filters[i].Description);
        }

        return builder.ToString();
    }

    private string DescribeKnownInstruments()
    {
        var builder = new StringBuilder();
        builder.Append(" No instrument named \"");
        builder.Append(_instrumentName);

        if (_meterName is not null)
        {
            builder.Append("\" has been published by meter \"");
            builder.Append(_meterName);
            builder.Append("\".");
        }
        else
        {
            builder.Append("\" has been published by this factory.");
        }

        if (_existingInstruments.Count > 0)
        {
            builder.Append(" Existing instruments: ");
            for (var i = 0; i < _existingInstruments.Count; i++)
            {
                if (i > 0)
                    builder.Append("; ");

                builder.Append(_existingInstruments[i]);
            }

            builder.Append('.');
        }

        return builder.ToString();
    }

    private static bool HasTag(CapturedMeasurement<T> measurement, string key, object? value)
    {
        foreach (var tag in measurement.Tags)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal) && Equals(tag.Value, value))
                return true;
        }

        return false;
    }

    private static bool HasTagKey(CapturedMeasurement<T> measurement, string key)
    {
        foreach (var tag in measurement.Tags)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string FormatValue(T value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value}");

    // Describes the value the test passed to WithTag - never the captured measurement's tag. A string is
    // shown quoted (the quotes already convey its type); every other value shows the runtime type of the
    // expected value (for example "200 (int)"), so the message never implies a type it didn't check.
    private static string FormatTagValue(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => string.Create(
            CultureInfo.InvariantCulture,
            $"{value} ({InstrumentValueTypes.DisplayName(value.GetType())})"),
    };
}
