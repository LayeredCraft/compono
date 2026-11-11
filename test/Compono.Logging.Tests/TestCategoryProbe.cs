using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Compono.Logging.Tests;

internal static class TestCategoryProbe
{
    public static string For<TCategory>()
    {
        // Ask the real LoggerFactory, so parity is proven against the BCL's own naming rules rather
        // than a hard-coded string or a re-implementation of those rules.
        var probe = new ProbeProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(probe));

        factory.CreateLogger<TCategory>().LogInformation("probe");

        probe.LastCategory.Should().NotBeNull();
        return probe.LastCategory!;
    }

    private sealed class ProbeProvider : ILoggerProvider
    {
        public string? LastCategory { get; private set; }

        public ILogger CreateLogger(string categoryName)
        {
            LastCategory = categoryName;
            return NullLogger.Instance;
        }

        public void Dispose()
        {
        }
    }
}
