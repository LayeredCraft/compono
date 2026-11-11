namespace Compono.Logging;

/// <summary>
/// DI registration marker for <see cref="ComponoLoggingBuilderExtensions.AddComponoCapture"/> - its
/// presence in the service collection is how a repeated registration of the same
/// <see cref="LogCollector"/> is detected and made idempotent, without the distinct-collector-dropping
/// behavior a naive <c>TryAddEnumerable</c> (dedup by implementation type) would cause. Internal -
/// purely a registration detail. See docs/adr/0071-compono-logging-host-wide-log-capture.md.
/// </summary>
internal sealed class ComponoCaptureRegistration
{
    public ComponoCaptureRegistration(LogCollector collector)
    {
        Collector = collector;
    }

    public LogCollector Collector { get; }
}
