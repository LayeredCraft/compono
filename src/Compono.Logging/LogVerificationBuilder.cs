using System.Text;
using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// A fluent filter chain over a logger's captured entries, ending in a thin, one-line forwarder to
/// core <see cref="CallVerifier"/> - <see cref="Once"/>/<see cref="Never"/>/<see cref="Exactly"/> each
/// build a <see cref="CallVerifier"/> from the filtered match count right here and call the
/// corresponding member. <see cref="CallVerifier"/> itself is never part of this type's public
/// surface, and no new counting/<c>Times</c> abstraction is introduced. See
/// docs/adr/0055-compono-logging-testing-support-package.md §7/§11/§12.
/// </summary>
public sealed class LogVerificationBuilder
{
    private readonly LogEntryCollector _collector;
    private readonly List<(string Description, Func<CapturedLogEntry, bool> Predicate)> _filters = [];

    internal LogVerificationBuilder(LogEntryCollector collector)
    {
        _collector = collector;
    }

    /// <summary>Restricts matches to entries logged at exactly <paramref name="level"/>.</summary>
    public LogVerificationBuilder AtLevel(LogLevel level) =>
        Add($"level {level}", entry => entry.LogLevel == level);

    /// <summary>Restricts matches to entries logged with exactly <paramref name="eventId"/>.</summary>
    public LogVerificationBuilder WithEventId(EventId eventId) =>
        Add($"event id {eventId}", entry => entry.EventId == eventId);

    /// <summary>Restricts matches to entries whose exception is a <typeparamref name="TException"/>.</summary>
    public LogVerificationBuilder WithException<TException>()
        where TException : Exception =>
        Add($"exception of type {typeof(TException)}", entry => entry.Exception is TException);

    /// <summary>Restricts matches to entries whose formatted message contains <paramref name="text"/>
    /// (ordinal comparison).</summary>
    public LogVerificationBuilder WithMessageContaining(string text) =>
        Add($"message containing \"{text}\"", entry => entry.Message.Contains(text, StringComparison.Ordinal));

    /// <summary>Restricts matches to entries satisfying an arbitrary <paramref name="predicate"/> -
    /// the escape hatch for anything the named filters above don't cover.</summary>
    public LogVerificationBuilder Matching(Func<CapturedLogEntry, bool> predicate) =>
        Add("a custom condition", predicate);

    /// <summary>Asserts the accumulated filters matched exactly once.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters did not match exactly once.</exception>
    public void Once() => ToCallVerifier().Once();

    /// <summary>Asserts the accumulated filters never matched.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters matched at least once.</exception>
    public void Never() => ToCallVerifier().Never();

    /// <summary>Asserts the accumulated filters matched exactly <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters did not match exactly
    /// <paramref name="times"/> times.</exception>
    public void Exactly(int times) => ToCallVerifier().Exactly(times);

    private LogVerificationBuilder Add(string description, Func<CapturedLogEntry, bool> predicate)
    {
        _filters.Add((description, predicate));
        return this;
    }

    private CallVerifier ToCallVerifier()
    {
        var entries = _collector.GetEntries();
        var matchCount = 0;
        foreach (var entry in entries)
        {
            var matches = true;
            foreach (var (_, predicate) in _filters)
            {
                if (!predicate(entry))
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
        if (_filters.Count == 0)
            return "any log entry";

        var builder = new StringBuilder("a log entry matching ");
        for (var i = 0; i < _filters.Count; i++)
        {
            if (i > 0)
                builder.Append(" and ");
            builder.Append(_filters[i].Description);
        }

        return builder.ToString();
    }
}
