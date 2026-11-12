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
    private readonly ILogEntrySource _entrySource;
    private readonly List<(string Description, Func<CapturedLogEntry, bool> Predicate)> _filters = [];

    internal LogVerificationBuilder(ILogEntrySource entrySource)
    {
        _entrySource = entrySource;
    }

    /// <summary>Restricts matches to entries logged at exactly <paramref name="level"/>.</summary>
    public LogVerificationBuilder AtLevel(LogLevel level) =>
        Add($"level {level}", entry => entry.LogLevel == level);

    /// <summary>
    /// Restricts matches to entries logged at <paramref name="level"/> or any more severe level
    /// (e.g. <c>AtLevelOrAbove(LogLevel.Error)</c> matches Error and Critical). A captured entry is
    /// never <see cref="LogLevel.None"/>, and neither is <see cref="LogLevel.None"/> ever treated as
    /// "above" anything, so <c>AtLevelOrAbove(LogLevel.None)</c> matches nothing.
    /// </summary>
    public LogVerificationBuilder AtLevelOrAbove(LogLevel level) =>
        Add($"level {level} or above", entry => entry.LogLevel != LogLevel.None && entry.LogLevel >= level);

    /// <summary>
    /// Restricts matches to entries whose <see cref="CapturedLogEntry.Category"/> is exactly
    /// <paramref name="category"/> (ordinal, case-sensitive - use <see cref="InCategoryPrefix"/> for a
    /// prefix match, or <see cref="Matching"/> for anything else).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="category"/> is <see langword="null"/>.</exception>
    public LogVerificationBuilder InCategory(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return Add($"category \"{category}\"", entry => string.Equals(entry.Category, category, StringComparison.Ordinal));
    }

    /// <summary>
    /// Restricts matches to entries whose <see cref="CapturedLogEntry.Category"/> starts with
    /// <paramref name="prefix"/> (ordinal, case-sensitive). The prefix is literal - no namespace
    /// separator is implied - so <c>InCategoryPrefix("MyApp.Jobs")</c> also matches
    /// <c>MyApp.JobsLegacy</c>; pass a trailing dot (<c>"MyApp.Jobs."</c>) to match only categories
    /// beneath that namespace. An empty prefix matches every entry.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    public LogVerificationBuilder InCategoryPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return Add($"category starting with \"{prefix}\"", entry => entry.Category.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Restricts matches to entries whose <see cref="CapturedLogEntry.Category"/> does <b>not</b> start
    /// with <paramref name="prefix"/> (ordinal, case-sensitive) - for host-wide guards such as "nothing
    /// at <see cref="LogLevel.Error"/> outside <c>MyApp.Jobs.</c>". The prefix is literal, exactly as for
    /// <see cref="InCategoryPrefix"/>; an empty prefix matches no entry.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    public LogVerificationBuilder NotInCategoryPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return Add($"category not starting with \"{prefix}\"", entry => !entry.Category.StartsWith(prefix, StringComparison.Ordinal));
    }

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

    /// <summary>Asserts the accumulated filters matched at least <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters matched fewer than
    /// <paramref name="times"/> times.</exception>
    public void AtLeast(int times) => ToCallVerifier().AtLeast(times);

    /// <summary>Asserts the accumulated filters matched at most <paramref name="times"/> times.</summary>
    /// <exception cref="TestDoubleVerificationException">The filters matched more than
    /// <paramref name="times"/> times.</exception>
    public void AtMost(int times) => ToCallVerifier().AtMost(times);

    private LogVerificationBuilder Add(string description, Func<CapturedLogEntry, bool> predicate)
    {
        _filters.Add((description, predicate));
        return this;
    }

    private CallVerifier ToCallVerifier()
    {
        var entries = _entrySource.GetEntries();
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
