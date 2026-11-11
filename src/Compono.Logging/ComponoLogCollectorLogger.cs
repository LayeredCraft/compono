using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// The <see cref="ILogger"/> handed out by <see cref="ComponoLogCollectorProvider"/> - one per
/// <c>CreateLogger(categoryName)</c> call, forwarding into the provider's single shared
/// <see cref="LogCollector"/> tagged with the exact category string it was created with. Internal - a
/// consumer never holds this type directly. See
/// docs/adr/0071-compono-logging-host-wide-log-capture.md.
/// </summary>
internal sealed class ComponoLogCollectorLogger : ILogger
{
    private readonly ComponoLogCollectorProvider _provider;
    private readonly string _category;

    public ComponoLogCollectorLogger(ComponoLogCollectorProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        _provider.PushScope(state);

    public bool IsEnabled(LogLevel logLevel) =>
        !_provider.IsDisposed && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _provider.Log(_category, logLevel, eventId, state, exception, formatter);
}
