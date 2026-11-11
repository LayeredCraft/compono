using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// The single, shared construction path for every <see cref="CapturedLogEntry"/> - both the composed
/// <see cref="LogEntryCollector"/> path and the host <see cref="ComponoLogCollectorProvider"/> path
/// call this, so structured-state extraction, message formatting, scope snapshots, and timestamps can
/// never drift between the two (ADR-0071). Internal and reflection-free.
/// </summary>
internal static class LogEntryFactory
{
    public static CapturedLogEntry Create<TState>(
        string category,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter,
        IReadOnlyList<object> scopes)
    {
        var message = formatter(state, exception);
        var (properties, messageTemplate) = ExtractStructuredState(state);

        return new CapturedLogEntry(
            category,
            logLevel,
            eventId,
            exception,
            message,
            state,
            properties,
            messageTemplate,
            scopes,
            DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<object> SnapshotScopes(IExternalScopeProvider scopeProvider)
    {
        var scopes = new List<object>();
        // ForEachScope's callback parameter is annotated `object?` even though a real scope value can
        // never actually be null (BeginScope<TState> requires `TState : notnull`) - the
        // null-forgiving operator here reflects that guarantee, not a genuine possible-null case.
        scopeProvider.ForEachScope(static (scope, state) => state.Add(scope!), scopes);
        return [.. scopes];
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
}
