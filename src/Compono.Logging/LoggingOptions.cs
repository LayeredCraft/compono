using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// Configuration for <see cref="CompositionBuilderExtensions.UseLogging"/> and for directly
/// constructing a <see cref="CapturingLogger"/>/<see cref="CapturingLogger{T}"/>. Fixed at
/// construction time - there is no runtime-mutable equivalent, unlike
/// <c>LayeredCraft.StructuredLogging</c>'s <c>TestLogger.MinimumLogLevel</c> setter. See
/// docs/adr/0055-compono-logging-testing-support-package.md.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// The minimum <see cref="LogLevel"/> a captor records. Real filtering, not merely an
    /// <see cref="ILogger.IsEnabled"/> opinion layered on top of an otherwise-complete capture
    /// stream: an entry below this level is never captured. <see cref="LogLevel.None"/> disables
    /// all logging entirely, and is itself never an enabled/capturable level regardless of this
    /// value - see the "MinimumLevel semantics" section of ADR-0055's amendment for the exact rule.
    /// Defaults to <see cref="LogLevel.Trace"/> (every ordinary level captured).
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
}
