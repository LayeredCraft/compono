using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// Direct inspection and fluent verification over any <see cref="ILogger"/> - no assertion
/// framework required. Every method here requires the <see cref="ILogger"/> it's called on to
/// actually be a <see cref="Compono.Logging"/> capturing logger (produced by
/// <see cref="CompositionBuilderExtensions.UseLogging"/>, or directly via
/// <see cref="CapturingLogger"/>/<see cref="CapturingLogger{T}"/>'s public constructors) - calling
/// any of these against <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/>, an
/// NSubstitute substitute, a <c>Compono.TestDoubles</c>-generated double, or any other
/// non-Compono.Logging <see cref="ILogger"/> throws immediately, diagnostically, rather than
/// returning an empty/default result. See docs/adr/0055-compono-logging-testing-support-package.md's
/// "Failure semantics for a non-Compono.Logging ILogger" section.
/// </summary>
public static class LoggerTestingExtensions
{
    /// <summary>Every entry captured by <paramref name="logger"/> so far, oldest first.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="logger"/> is not a
    /// Compono.Logging capturing logger.</exception>
    public static IReadOnlyList<CapturedLogEntry> GetCapturedEntries(this ILogger logger) =>
        RequireFacade(logger).Collector.GetEntries();

    /// <summary>The most recently captured entry, or <see langword="null"/> if nothing has been
    /// captured yet.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="logger"/> is not a
    /// Compono.Logging capturing logger.</exception>
    public static CapturedLogEntry? GetLastCapturedEntry(this ILogger logger) =>
        RequireFacade(logger).Collector.GetLast();

    /// <summary>Discards every entry captured by <paramref name="logger"/> so far.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="logger"/> is not a
    /// Compono.Logging capturing logger.</exception>
    public static void ClearCapturedEntries(this ILogger logger) =>
        RequireFacade(logger).Collector.Clear();

    /// <summary>
    /// The entry point for fluent verification, e.g.
    /// <c>logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once()</c> -
    /// matching the same single-verb vocabulary <c>Compono.TestDoubles</c>/<c>Compono.Http</c>
    /// already established (<c>repository.Verify().Save().Once()</c>,
    /// <c>registration.Verify().Once()</c>), not a two-verb <c>VerifyLog()...Verify()</c> shape.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="logger"/> is not a
    /// Compono.Logging capturing logger.</exception>
    public static LogVerificationBuilder Verify(this ILogger logger) =>
        new(RequireFacade(logger).Collector);

    private static ICapturingLoggerFacade RequireFacade(ILogger logger)
    {
        if (logger is ICapturingLoggerFacade facade)
            return facade;

        throw new InvalidOperationException(
            $"'{logger.GetType()}' is not a Compono.Logging capturing logger. This member is only " +
            "usable against an ILogger produced by Compono.Logging (UseLogging(), or a directly " +
            "constructed CapturingLogger/CapturingLogger<T>). If this ILogger was composed, confirm " +
            "UseLogging() is registered before UseNSubstitute()/UseGeneratedTestDoubles() - Compono's " +
            "stage-6 test-double providers resolve in registration order, and whichever provider is " +
            "registered first wins for a given request.");
    }
}
