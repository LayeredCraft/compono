namespace Compono.Logging;

/// <summary>
/// Implemented by <see cref="CapturingLogger"/> and <see cref="CapturingLogger{T}"/> so
/// <see cref="LoggerTestingExtensions"/> can reach the shared <see cref="LogEntryCollector"/> behind
/// an arbitrary <see cref="Microsoft.Extensions.Logging.ILogger"/> parameter without a public
/// downcast to either concrete type. Internal - this is purely an extension-method dispatch detail,
/// never part of the public API surface. See docs/adr/0055-compono-logging-testing-support-package.md's
/// "Failure semantics for a non-Compono.Logging ILogger" section.
/// </summary>
internal interface ICapturingLoggerFacade
{
    LogEntryCollector Collector { get; }
}
