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
    /// <para>
    /// Register this <b>before</b> <c>UseNSubstitute()</c> or any other catch-all test-double provider if
    /// <see cref="System.Diagnostics.Metrics.IMeterFactory"/> should resolve through Compono.Metrics rather
    /// than a substitute - Compono's stage-6 providers resolve in registration order
    /// (first-registered-wins), an existing, documented, <c>Accepted</c> pattern (ADR-0024) this package
    /// follows rather than replacing. An exact <c>Register&lt;IMeterFactory&gt;(...)</c> always wins over
    /// <c>UseMetrics()</c> regardless of order.
    /// </para>
    /// <para>
    /// Order against <c>UseGeneratedTestDoubles()</c> doesn't matter while
    /// <c>ComponoMetricsOwnsMeterFactory</c> is enabled (the default whenever Compono.Metrics is
    /// referenced): <see cref="System.Diagnostics.Metrics.IMeterFactory"/> is then Metrics-owned and
    /// <c>Compono.TestDoubles</c> never generates a double for it. The same default means a composer that
    /// calls <c>UseGeneratedTestDoubles()</c> without <c>UseMetrics()</c> gets no
    /// <see cref="System.Diagnostics.Metrics.IMeterFactory"/> double at all. See
    /// docs/adr/0072-compono-metrics-testing-support-package.md Amendment 5.
    /// </para>
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
