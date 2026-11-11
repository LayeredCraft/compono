namespace Compono.Logging;

/// <summary>
/// A test-owned sink for host-wide logging output. Register one with
/// <see cref="ComponoLoggingBuilderExtensions.AddComponoCapture"/> on a host's
/// <see cref="Microsoft.Extensions.Logging.ILoggingBuilder"/>, then inspect it with
/// <see cref="GetCapturedEntries"/>/<see cref="GetLastCapturedEntry"/> or verify it with
/// <see cref="Verify"/> - the same verification surface <see cref="LoggerTestingExtensions"/> offers
/// for a composed logger. The collector is thread-safe, is never disposed or reset by the host, and
/// stays readable after the host is disposed. See
/// docs/adr/0071-compono-logging-host-wide-log-capture.md.
/// </summary>
public sealed class LogCollector : ILogEntrySource
{
    // A plain object lock, not System.Threading.Lock - this project multi-targets net8.0, which
    // predates that type (introduced in .NET 9).
    private readonly object _lock = new();
    private readonly List<CapturedLogEntry> _entries = [];

    /// <summary>Creates an empty, test-owned collector.</summary>
    public LogCollector()
    {
    }

    /// <summary>Every entry captured so far, oldest first - a snapshot, safe to read while logging
    /// continues on other threads.</summary>
    public IReadOnlyList<CapturedLogEntry> GetCapturedEntries()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }

    /// <summary>The most recently captured entry, or <see langword="null"/> if nothing has been
    /// captured yet.</summary>
    public CapturedLogEntry? GetLastCapturedEntry()
    {
        lock (_lock)
        {
            return _entries.Count == 0 ? null : _entries[^1];
        }
    }

    /// <summary>Discards every entry captured so far. The collector stays fully usable afterward -
    /// clearing never detaches it from a host that is still capturing into it.</summary>
    public void ClearCapturedEntries()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    /// <summary>
    /// The entry point for fluent verification over this collector's entries, e.g.
    /// <c>logs.Verify().AtLevelOrAbove(LogLevel.Error).Never()</c>.
    /// </summary>
    public LogVerificationBuilder Verify() => new(this);

    IReadOnlyList<CapturedLogEntry> ILogEntrySource.GetEntries() => GetCapturedEntries();

    internal void Add(CapturedLogEntry entry)
    {
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }
}
