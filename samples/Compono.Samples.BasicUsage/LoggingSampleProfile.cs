using Compono.Logging;

namespace Compono.Samples.BasicUsage;

// A dedicated profile for the logging scenario, kept separate from SampleApplicationProfile - it
// composes ILogger<T> as a CapturingLogger<T> via UseLogging(), a different concern from that
// profile's own registration/member-rule demonstration.
public sealed class LoggingSampleProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.UseLogging();
}
