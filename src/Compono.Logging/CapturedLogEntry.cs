using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// One captured log call - raw <see cref="State"/> as an escape hatch, plus derived,
/// reflection-free structured semantics (<see cref="Properties"/>/<see cref="MessageTemplate"/>)
/// next to it, adopting <c>Microsoft.Extensions.Diagnostics.Testing</c>'s <c>FakeLogRecord</c>
/// "raw + derived" shape without depending on that package. See
/// docs/adr/0055-compono-logging-testing-support-package.md §7/§9/§12.
/// </summary>
public readonly record struct CapturedLogEntry
{
    /// <summary>
    /// Populates every field of a captured entry in one call - <see cref="CapturedLogEntry"/> is an
    /// inspection model Compono.Logging itself produces (per ADR-0055 §7/§9/§12's documented
    /// getter-only shape); an external consumer reads it but never fabricates one, so this
    /// constructor is <see langword="internal"/> rather than a public object-initializer surface.
    /// </summary>
    internal CapturedLogEntry(
        LogLevel logLevel,
        EventId eventId,
        Exception? exception,
        string message,
        object? state,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        string? messageTemplate,
        IReadOnlyList<object> scopes,
        DateTimeOffset timestamp)
    {
        LogLevel = logLevel;
        EventId = eventId;
        Exception = exception;
        Message = message;
        State = state;
        Properties = properties;
        MessageTemplate = messageTemplate;
        Scopes = scopes;
        Timestamp = timestamp;
    }

    /// <summary>The level this entry was logged at. Never <see cref="LogLevel.None"/> - a
    /// <see cref="LogLevel.None"/> call is never captured in the first place.</summary>
    public LogLevel LogLevel { get; }

    /// <summary>The <see cref="EventId"/> passed to the logging call.</summary>
    public EventId EventId { get; }

    /// <summary>The exception passed to the logging call, if any.</summary>
    public Exception? Exception { get; }

    /// <summary>
    /// The pre-formatted message, produced via the caller's own <c>formatter(state, exception)</c>
    /// delegate - never re-derived by <see cref="CapturingLogger"/>, so it can't diverge from what a
    /// real logging provider would have produced.
    /// </summary>
    public string Message { get; }

    /// <summary>The raw, boxed <c>TState</c> - always present, the escape hatch for a shape
    /// <see cref="Properties"/>/<see cref="MessageTemplate"/> doesn't cover.</summary>
    public object? State { get; }

    /// <summary>
    /// Non-null only when <see cref="State"/> implements
    /// <see cref="IReadOnlyList{T}"/> of <see cref="KeyValuePair{TKey,TValue}"/> of
    /// <see cref="string"/> and <see cref="object"/> - both the compiler-generated
    /// <c>FormattedLogValues</c> behind an ordinary <c>LogInformation(...)</c> call and the shared
    /// <c>LoggerMessageState</c> behind every <c>[LoggerMessage]</c> source-generated call satisfy
    /// this identically, so one code path covers both. The value slot is exposed as nullable here
    /// even though the BCL's own interface declares it non-nullable <see cref="object"/> - a
    /// structured logging call can legitimately pass a <see langword="null"/> argument, and this
    /// signature is the more truthful contract for that case (ADR-0055's "Properties nullability"
    /// decision).
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>>? Properties { get; }

    /// <summary>
    /// <see cref="Properties"/>'s <c>"{OriginalFormat}"</c> entry, surfaced by name.
    /// <see langword="null"/> if <see cref="Properties"/> is <see langword="null"/> or that key is
    /// absent.
    /// </summary>
    public string? MessageTemplate { get; }

    /// <summary>
    /// Every scope active at the moment this entry was captured, outermost-to-innermost - matches
    /// <see cref="Microsoft.Extensions.Logging.LoggerExternalScopeProvider.ForEachScope{TState}"/>'s
    /// own enumeration order and Microsoft's own <c>FakeLogRecord.Scopes</c> ordering. A snapshot
    /// fixed at capture time - a scope pushed or disposed afterward never retroactively changes an
    /// already-captured entry.
    /// </summary>
    public IReadOnlyList<object> Scopes { get; }

    /// <summary>When this entry was captured.</summary>
    public DateTimeOffset Timestamp { get; }
}
