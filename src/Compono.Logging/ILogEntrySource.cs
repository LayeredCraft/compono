namespace Compono.Logging;

/// <summary>
/// The entry source <see cref="LogVerificationBuilder"/> filters over - implemented by the composed
/// <see cref="LogEntryCollector"/> and by the public host-wide <see cref="LogCollector"/>, so both
/// verification surfaces share one implementation. Internal - never part of the public API.
/// </summary>
internal interface ILogEntrySource
{
    IReadOnlyList<CapturedLogEntry> GetEntries();
}
