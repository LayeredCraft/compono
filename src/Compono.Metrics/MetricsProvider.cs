using System.Diagnostics.Metrics;

namespace Compono.Metrics;

/// <summary>
/// A stage-6 test-double provider that composes an <see cref="IMeterFactory"/> request as a new
/// <see cref="CapturingMeterFactory"/>. Registered via <see cref="CompositionBuilderExtensions.UseMetrics"/>.
/// It handles exactly <see cref="IMeterFactory"/> and returns
/// <see cref="CompositionProviderResult.NotHandled"/> for everything else (ADR-0072 "Precedence and misuse").
/// </summary>
internal sealed class MetricsProvider : ICompositionValueProvider
{
    /// <inheritdoc />
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (request.RequestedType == typeof(IMeterFactory))
            return CompositionProviderResult.Handled(new CapturingMeterFactory());

        return CompositionProviderResult.NotHandled;
    }
}
