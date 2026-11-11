using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// Host integration entry point - registers a test-owned <see cref="LogCollector"/> as a logging
/// provider so every host logger's output lands in it. Named to avoid any ambiguity with
/// <c>Microsoft.Extensions.Logging.LoggingBuilderExtensions</c>. See
/// docs/adr/0071-compono-logging-host-wide-log-capture.md.
/// </summary>
public static class ComponoLoggingBuilderExtensions
{
    /// <summary>
    /// Registers <paramref name="collector"/> as a Compono logging provider on
    /// <paramref name="builder"/>. Every host logger's entries are then appended to the collector
    /// (subject to the host's own filter rules) and inspectable via
    /// <see cref="LogCollector.GetCapturedEntries"/>/<see cref="LogCollector.Verify"/>. This never
    /// changes filter rules. Registering the same collector twice is idempotent (one provider, no
    /// duplicate entries); registering distinct collectors adds one provider per collector, and each
    /// receives every entry. Returns <paramref name="builder"/> for chaining.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or
    /// <paramref name="collector"/> is <see langword="null"/>.</exception>
    public static ILoggingBuilder AddComponoCapture(this ILoggingBuilder builder, LogCollector collector)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(collector);

        if (IsAlreadyRegistered(builder.Services, collector))
            return builder;

        var registration = new ComponoCaptureRegistration(collector);
        builder.Services.Add(ServiceDescriptor.Singleton<ILoggerProvider>(registration.CreateProvider));
        return builder;
    }

    // Inspects the live ILoggerProvider descriptors themselves (not a separate marker service), so
    // ClearProviders()/RemoveAll<ILoggerProvider>() also clears the duplicate-detection state.
    private static bool IsAlreadyRegistered(IServiceCollection services, LogCollector collector)
    {
        foreach (var descriptor in services)
        {
            // IsKeyedService is checked first: ImplementationFactory throws on a keyed descriptor.
            if (descriptor.ServiceType == typeof(ILoggerProvider)
                && !descriptor.IsKeyedService
                && descriptor.ImplementationFactory?.Target is ComponoCaptureRegistration registration
                && ReferenceEquals(registration.Collector, collector))
            {
                return true;
            }
        }

        return false;
    }
}
