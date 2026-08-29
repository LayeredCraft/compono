using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// A hand-written, reflection-free <see cref="ILogger"/> that captures every logged entry into an
/// inspectable, thread-safe, Compono-native model (<see cref="CapturedLogEntry"/>) - real scope
/// tracking via <see cref="LoggerExternalScopeProvider"/>, structured-property extraction, and
/// genuine <see cref="LoggingOptions.MinimumLevel"/> filtering. Directly, publicly constructible -
/// composing through <see cref="CompositionBuilderExtensions.UseLogging"/> is not required. See
/// docs/adr/0055-compono-logging-testing-support-package.md.
/// </summary>
public sealed class CapturingLogger : ILogger, ICapturingLoggerFacade
{
    private readonly LogEntryCollector _collector;

    LogEntryCollector ICapturingLoggerFacade.Collector => _collector;

    /// <summary>
    /// Creates a standalone <see cref="CapturingLogger"/>, usable with no Compono composition
    /// involved at all - the same "no factory needed for the common case" ergonomics
    /// <c>Microsoft.Extensions.Diagnostics.Testing</c>'s <c>FakeLogger&lt;T&gt;</c> already
    /// established as prior art (RESEARCH-0013 §3).
    /// </summary>
    /// <param name="options">Configuration, or <see langword="null"/> for the default
    /// (<see cref="LogLevel.Trace"/> minimum level).</param>
    public CapturingLogger(LoggingOptions? options = null)
    {
        _collector = new LogEntryCollector(options);
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        _collector.PushScope(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _collector.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _collector.Record(logLevel, eventId, state, exception, formatter);
}
