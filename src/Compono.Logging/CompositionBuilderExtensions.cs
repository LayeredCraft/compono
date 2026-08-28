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
    /// Register this <b>before</b> <c>UseNSubstitute()</c>/<c>UseGeneratedTestDoubles()</c> if
    /// <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}"/> should resolve through
    /// Compono.Logging rather than a generic substitute/generated double - Compono's stage-6
    /// providers resolve in registration order (first-registered-wins), an existing, documented,
    /// <c>Accepted</c> pattern (ADR-0024/ADR-0043) this package follows rather than replacing. See
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
