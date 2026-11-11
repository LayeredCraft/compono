namespace Compono.Metrics;

/// <summary>
/// Registers <see cref="Compono.Metrics"/> support into a <see cref="CompositionBuilder"/>.
/// </summary>
public static class CompositionBuilderExtensions
{
    /// <summary>
    /// Registers a stage-6 test-double provider (<see cref="MetricsProvider"/>) so a bare
    /// <see cref="System.Diagnostics.Metrics.IMeterFactory"/> composes as a new
    /// <see cref="CapturingMeterFactory"/> per request.
    /// </summary>
    /// <remarks>
    /// Register this <b>before</b> <c>UseNSubstitute()</c> if
    /// <see cref="System.Diagnostics.Metrics.IMeterFactory"/> should resolve through Compono.Metrics rather
    /// than an NSubstitute substitute - Compono's stage-6 providers resolve in registration order
    /// (first-registered-wins), an existing, documented, <c>Accepted</c> pattern (ADR-0024) this package
    /// follows rather than replacing. An exact <c>Register&lt;IMeterFactory&gt;(...)</c> always wins over
    /// <c>UseMetrics()</c> regardless of order.
    /// </remarks>
    /// <param name="builder">The builder to register into.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static CompositionBuilder UseMetrics(this CompositionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddTestDoubleProvider(new MetricsProvider());
    }
}
