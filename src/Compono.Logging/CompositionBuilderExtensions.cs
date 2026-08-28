namespace Compono.Logging;

/// <summary>
/// Registers <see cref="Compono.Logging"/> support into a <see cref="CompositionBuilder"/>.
/// </summary>
public static class CompositionBuilderExtensions
{
    /// <summary>
    /// Registers a stage-6 test-double provider (<see cref="LoggingProvider"/>) so a bare
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/> or any closed
    /// <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}"/> composes as a
    /// <see cref="CapturingLogger"/>/<see cref="CapturingLogger{T}"/>.
    /// </summary>
    /// <remarks>
    /// Register this <b>before</b> <c>UseNSubstitute()</c> if
    /// <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}"/> should resolve through
    /// Compono.Logging rather than an NSubstitute substitute - Compono's stage-6 providers resolve
    /// in registration order (first-registered-wins), an existing, documented, <c>Accepted</c>
    /// pattern (ADR-0024) this package follows rather than replacing. Registration order against
    /// <c>UseGeneratedTestDoubles()</c> is <b>not</b> relevant here: when <c>ComponoGeneratedLogging</c>
    /// is enabled (the package default), <c>ILogger</c>/<c>ILogger{TCategoryName}</c> are
    /// Logging-owned abstractions excluded from <c>Compono.TestDoubles</c> generation entirely, so
    /// <c>UseGeneratedTestDoubles()</c> has no generated double to offer for these types regardless
    /// of order (ADR-0055 Amendment 4). See
    /// docs/adr/0055-compono-logging-testing-support-package.md's "Runtime activation and
    /// precedence" section.
    /// </remarks>
    /// <param name="builder">The builder to register into.</param>
    /// <param name="configure">Optional configuration for the resulting captors' behavior.</param>
    public static CompositionBuilder UseLogging(this CompositionBuilder builder, Action<LoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new LoggingOptions();
        configure?.Invoke(options);

        return builder.AddTestDoubleProvider(new LoggingProvider(options));
    }
}
