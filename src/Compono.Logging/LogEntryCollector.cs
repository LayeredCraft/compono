using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// Owns the actual capture state behind one <see cref="CapturingLogger"/>/<see cref="CapturingLogger{T}"/>
/// instance - a lock-guarded entry list, one <see cref="LoggerExternalScopeProvider"/>, and the
/// effective <see cref="LoggingOptions.MinimumLevel"/> fixed at construction. Internal - never
/// referenced directly by a consumer; reached only through the two public logger types and
/// <see cref="LoggerTestingExtensions"/>. See docs/adr/0055-compono-logging-testing-support-package.md.
/// </summary>
internal sealed class LogEntryCollector
{
    // A plain object lock, not System.Threading.Lock - this project multi-targets net8.0, which
    // predates that type (introduced in .NET 9).
    private readonly object _lock = new();
    private readonly List<CapturedLogEntry> _entries = [];
    private readonly LoggerExternalScopeProvider _scopeProvider = new();
    private readonly LogLevel _minimumLevel;

    public LogEntryCollector(LoggingOptions? options)
    {
        _minimumLevel = (options ?? new LoggingOptions()).MinimumLevel;
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

        var message = formatter(state, exception);
        var (properties, messageTemplate) = ExtractStructuredState(state);
        var scopes = SnapshotScopes();

        var entry = new CapturedLogEntry(
            logLevel,
            eventId,
            exception,
            message,
            state,
            properties,
            messageTemplate,
            scopes,
            DateTimeOffset.UtcNow);

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

    // Reflection-free: a single pattern match against the interface both the compiler-generated
    // FormattedLogValues (an ordinary LogInformation(...) call) and the shared LoggerMessageState
    // (every [LoggerMessage] source-generated call site) satisfy identically - confirmed empirically
    // during RESEARCH-0013's pre-ADR validation spike, no special-casing needed for either style.
    private static (IReadOnlyList<KeyValuePair<string, object?>>? Properties, string? MessageTemplate) ExtractStructuredState<TState>(TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object>> pairs)
            return (null, null);

        var properties = new KeyValuePair<string, object?>[pairs.Count];
        string? messageTemplate = null;
        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            properties[i] = new KeyValuePair<string, object?>(pair.Key, pair.Value);
            if (pair.Key == "{OriginalFormat}")
                messageTemplate = pair.Value as string;
        }

        return (properties, messageTemplate);
    }

    private object[] SnapshotScopes()
    {
        var scopes = new List<object>();
        // ForEachScope's callback parameter is annotated `object?` even though a real scope value can
        // never actually be null (BeginScope<TState> requires `TState : notnull`) - the null-forgiving
        // operator here reflects that guarantee, not a genuine possible-null case.
        _scopeProvider.ForEachScope(static (scope, state) => state.Add(scope!), scopes);
        return [.. scopes];
    }
}
