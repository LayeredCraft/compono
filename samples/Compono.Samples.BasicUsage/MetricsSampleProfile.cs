using Compono.Metrics;

namespace Compono.Samples.BasicUsage;

// A dedicated profile for the metrics scenario, kept separate from the others - it composes
// IMeterFactory as a CapturingMeterFactory via UseMetrics(), a different concern from the logging
// profile's ILogger<T> capture or SampleApplicationProfile's own registration/member-rule demo.
public sealed class MetricsSampleProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseMetrics();
}
