using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// The owner of one <see cref="ComponoLoggingBuilderExtensions.AddComponoCapture"/> registration -
/// its <see cref="CreateProvider"/> method is the <see cref="ILoggerProvider"/> descriptor's
/// implementation factory, so the descriptor itself identifies which <see cref="LogCollector"/> it
/// captures into. That is how a repeated registration of the same collector is detected and made
/// idempotent: detection inspects the live provider descriptors, so a <c>ClearProviders()</c>
/// (which removes them) also removes the duplicate-detection state, and a later re-registration is
/// honored. A naive <c>TryAddEnumerable</c> (dedup by implementation type) would instead drop a
/// second, distinct collector. Internal - purely a registration detail. See
/// docs/adr/0071-compono-logging-host-wide-log-capture.md.
/// </summary>
internal sealed class ComponoCaptureRegistration
{
    public ComponoCaptureRegistration(LogCollector collector)
    {
        Collector = collector;
    }

    public LogCollector Collector { get; }

    // An instance method (not a lambda) so the registered delegate's Target is this registration -
    // ComponoLoggingBuilderExtensions reads it back to detect a duplicate. The container still
    // creates, owns, and disposes the provider (ADR-0071 "Ownership, lifetime, disposal").
    public ILoggerProvider CreateProvider(IServiceProvider services) => new ComponoLogCollectorProvider(Collector);
}
