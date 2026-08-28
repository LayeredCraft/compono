using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// The generic counterpart to <see cref="CapturingLogger"/> - implemented once, works for every
/// closed <see cref="ILogger{TCategoryName}"/>, no per-<typeparamref name="T"/> generation needed
/// for its own behavior. Composes an internal <see cref="LogEntryCollector"/> directly rather than
/// containing or delegating to a <see cref="CapturingLogger"/> instance (composition over
/// inheritance, deliberately deviating from <c>LayeredCraft.StructuredLogging</c>'s
/// <c>TestLogger&lt;T&gt; : TestLogger</c> shape). Directly, publicly constructible - composing
/// through <see cref="CompositionBuilderExtensions.UseLogging"/> is not required, and
/// <see cref="Compono.Logging.LoggingFactoryRegistry"/>'s generated activators call this exact same
/// public constructor rather than a separate internal-only path. See
/// docs/adr/0055-compono-logging-testing-support-package.md.
/// </summary>
/// <typeparam name="T">The logger's category type.</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>, ICapturingLoggerFacade
{
    private readonly LogEntryCollector _collector;

    LogEntryCollector ICapturingLoggerFacade.Collector => _collector;

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
