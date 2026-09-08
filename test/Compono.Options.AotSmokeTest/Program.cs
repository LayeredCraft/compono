using Compono;
using Compono.Options;
using Microsoft.Extensions.Options;

namespace Compono.Options.AotSmokeTest;

internal sealed record ServiceSettings(string Endpoint, int TimeoutSeconds);

internal static class Program
{
    private static int Main()
    {
        try
        {
            var source = new TestOptionsSource<ServiceSettings>(new ServiceSettings("https://a", 30));

            var composer = Composer.Create(builder => builder.UseOptions(source));
            var options = composer.Create<IOptions<ServiceSettings>>();
            var snapshotBeforeChange = composer.Create<IOptionsSnapshot<ServiceSettings>>();
            var monitor = composer.Create<IOptionsMonitor<ServiceSettings>>();

            if (options.Value != new ServiceSettings("https://a", 30))
                throw new InvalidOperationException($"Unexpected IOptions<T> value: {options.Value}.");

            if (!ReferenceEquals(monitor, source))
                throw new InvalidOperationException("IOptionsMonitor<T> should resolve to the source itself.");

            ServiceSettings? observed = null;
            using var subscription = monitor.OnChange((value, _) => observed = value);

            source.Change(new ServiceSettings("https://b", 60));

            if (observed != new ServiceSettings("https://b", 60))
                throw new InvalidOperationException($"Unexpected OnChange-observed value: {observed}.");

            if (options.Value != new ServiceSettings("https://a", 30))
                throw new InvalidOperationException("IOptions<T> should stay frozen after a later Change().");

            if (snapshotBeforeChange.Value != new ServiceSettings("https://a", 30))
                throw new InvalidOperationException("The already-resolved IOptionsSnapshot<T> should stay frozen after a later Change().");

            var snapshotAfterChange = composer.Create<IOptionsSnapshot<ServiceSettings>>();
            if (snapshotAfterChange.Value != new ServiceSettings("https://b", 60))
                throw new InvalidOperationException("A newly-resolved IOptionsSnapshot<T> should see the source's current state.");

            source.Change("named", new ServiceSettings("https://named", 10));
            if (monitor.Get("named") != new ServiceSettings("https://named", 10))
                throw new InvalidOperationException("Named option value did not round-trip through IOptionsMonitor<T>.Get(name).");

            try
            {
                monitor.Get("never-configured");
                throw new InvalidOperationException("Expected UnconfiguredNamedOptionException for an unconfigured name.");
            }
            catch (UnconfiguredNamedOptionException)
            {
                // expected
            }

            Console.WriteLine(
                "PASS: TestOptionsSource<T>/UseOptions<T> - IOptions<T>/IOptionsSnapshot<T>/IOptionsMonitor<T> " +
                "identity, change notification, named options, and the unconfigured-name diagnostic all " +
                "survived Native AOT through the packaged Compono.Options dependency chain.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }
}
