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
    /// <summary>The level this entry was logged at. Never <see cref="LogLevel.None"/> - a
    /// <see cref="LogLevel.None"/> call is never captured in the first place.</summary>
    public required LogLevel LogLevel { get; init; }

    /// <summary>The <see cref="EventId"/> passed to the logging call.</summary>
    public required EventId EventId { get; init; }

    /// <summary>The exception passed to the logging call, if any.</summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// The pre-formatted message, produced via the caller's own <c>formatter(state, exception)</c>
    /// delegate - never re-derived by <see cref="CapturingLogger"/>, so it can't diverge from what a
    /// real logging provider would have produced.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>The raw, boxed <c>TState</c> - always present, the escape hatch for a shape
    /// <see cref="Properties"/>/<see cref="MessageTemplate"/> doesn't cover.</summary>
    public object? State { get; init; }

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
    public IReadOnlyList<KeyValuePair<string, object?>>? Properties { get; init; }

    /// <summary>
    /// <see cref="Properties"/>'s <c>"{OriginalFormat}"</c> entry, surfaced by name.
    /// <see langword="null"/> if <see cref="Properties"/> is <see langword="null"/> or that key is
    /// absent.
    /// </summary>
    public string? MessageTemplate { get; init; }

    /// <summary>
    /// Every scope active at the moment this entry was captured, outermost-to-innermost - matches
    /// <see cref="Microsoft.Extensions.Logging.LoggerExternalScopeProvider.ForEachScope{TState}"/>'s
    /// own enumeration order and Microsoft's own <c>FakeLogRecord.Scopes</c> ordering. A snapshot
    /// fixed at capture time - a scope pushed or disposed afterward never retroactively changes an
    /// already-captured entry.
    /// </summary>
    public required IReadOnlyList<object> Scopes { get; init; }

    /// <summary>When this entry was captured.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
