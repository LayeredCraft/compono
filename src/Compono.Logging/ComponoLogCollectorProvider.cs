using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// The internal <see cref="ILoggerProvider"/> behind <see cref="LogCollector"/> - one instance per
/// distinct collector registered with
/// <see cref="ComponoLoggingBuilderExtensions.AddComponoCapture"/>. Implements
/// <see cref="ISupportExternalScope"/> so a logger's scopes come from the host's own scope provider
/// (falling back to its own <see cref="LoggerExternalScopeProvider"/> only if the host never supplies
/// one). Never public - a consumer reaches capture through <see cref="LogCollector"/>. See
/// docs/adr/0071-compono-logging-host-wide-log-capture.md and its Amendment 1.
/// </summary>
// ProviderAliasAttribute only lives in Logging.Abstractions on net10.0+; on net8.0/net9.0 it is in
// the Microsoft.Extensions.Logging implementation package, which ADR-0071 deliberately doesn't
// reference. ADR-0071 Amendment 1 therefore scopes the alias (and the Logging:Compono:* config rule
// it enables) to net10.0/net11.0 so the shipped dependency graph stays Abstractions-only.
#if NET10_0_OR_GREATER
[ProviderAlias("Compono")]
#endif
internal sealed class ComponoLogCollectorProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly LogCollector _collector;
    private readonly LoggerExternalScopeProvider _fallbackScopeProvider = new();
    private IExternalScopeProvider? _scopeProvider;
    private volatile bool _disposed;

    public ComponoLogCollectorProvider(LogCollector collector)
    {
        _collector = collector;
    }

    /// <summary>Observable seam for tests - <see langword="true"/> once <see cref="Dispose"/> ran.</summary>
    internal bool IsDisposed => _disposed;

    public ILogger CreateLogger(string categoryName) => new ComponoLogCollectorLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);

        _scopeProvider = scopeProvider;
    }

    public void Dispose() => _disposed = true;

    internal IDisposable PushScope<TState>(TState state)
        where TState : notnull =>
        (_scopeProvider ?? _fallbackScopeProvider).Push(state);

    internal void Log<TState>(
        string category,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // A racing log against a concurrently-disposed provider may be captured or dropped, but it
        // must never throw or corrupt the collector (ADR-0071 disposal semantics).
        if (_disposed || logLevel == LogLevel.None)
            return;

        var scopes = LogEntryFactory.SnapshotScopes(_scopeProvider ?? _fallbackScopeProvider);
        var entry = LogEntryFactory.Create(category, logLevel, eventId, state, exception, formatter, scopes);
        _collector.Add(entry);
    }
}
