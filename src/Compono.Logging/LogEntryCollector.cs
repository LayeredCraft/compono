using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// Owns the actual capture state behind one <see cref="CapturingLogger"/>/<see cref="CapturingLogger{T}"/>
/// instance - a lock-guarded entry list and one <see cref="LoggerExternalScopeProvider"/>, with the
/// effective <see cref="LoggingOptions.MinimumLevel"/> fixed at construction and the category computed
/// once. Internal - never referenced directly by a consumer; reached only through the two public logger
/// types and <see cref="LoggerTestingExtensions"/>. See
/// docs/adr/0055-compono-logging-testing-support-package.md and
/// docs/adr/0071-compono-logging-host-wide-log-capture.md.
/// </summary>
internal sealed class LogEntryCollector : ILogEntrySource
{
    // A plain object lock, not System.Threading.Lock - this project multi-targets net8.0, which
    // predates that type (introduced in .NET 9).
    private readonly object _lock = new();
    private readonly List<CapturedLogEntry> _entries = [];
    private readonly LoggerExternalScopeProvider _scopeProvider = new();
    private readonly LogLevel _minimumLevel;
    private readonly string _category;

    public LogEntryCollector(LoggingOptions? options, string category)
    {
        _minimumLevel = (options ?? new LoggingOptions()).MinimumLevel;
        _category = category;
    }

    /// <summary>
    /// <see langword="true"/> iff <paramref name="level"/> would actually be captured -
    /// <see cref="LogLevel.None"/> is never enabled regardless of <see cref="_minimumLevel"/>;
    /// <see cref="_minimumLevel"/> itself being <see cref="LogLevel.None"/> disables every ordinary
    /// level. See ADR-0055's "MinimumLevel semantics" amendment for the exact rule and its
    /// validation expectations.
    /// </summary>
    public bool IsEnabled(LogLevel level) =>
        level != LogLevel.None && _minimumLevel != LogLevel.None && level >= _minimumLevel;

    public void Record<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Defense-in-depth, not a reliance on caller discipline: most ILogger extension methods
        // (LogInformation, etc.) already check IsEnabled before calling Log<TState>, but Log<TState>
        // can be, and in generated/manual code sometimes is, called directly - a disabled entry must
        // never be built or appended regardless of who calls this.
        if (!IsEnabled(logLevel))
            return;

        var scopes = LogEntryFactory.SnapshotScopes(_scopeProvider);
        var entry = LogEntryFactory.Create(_category, logLevel, eventId, state, exception, formatter, scopes);

        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    public IDisposable PushScope<TState>(TState state)
        where TState : notnull =>
        _scopeProvider.Push(state);

    public IReadOnlyList<CapturedLogEntry> GetEntries()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }

    public CapturedLogEntry? GetLast()
    {
        lock (_lock)
        {
            return _entries.Count == 0 ? null : _entries[^1];
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
