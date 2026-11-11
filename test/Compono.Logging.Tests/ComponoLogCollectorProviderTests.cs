using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class ComponoLogCollectorProviderTests
{
    [Fact]
    public void LoggerWithoutHostScopeProvider_FallsBackToItsOwnScopeProvider()
    {
        var collector = new LogCollector();
        using var provider = new ComponoLogCollectorProvider(collector);
        var logger = provider.CreateLogger("Fallback.Category");

        using (logger.BeginScope("outer"))
        using (logger.BeginScope("inner"))
            logger.LogInformation("captured");

        var entry = collector.GetCapturedEntries().Should().ContainSingle().Subject;
        entry.Scopes.Should().Equal("outer", "inner");
    }

    [Fact]
    public void SetScopeProvider_ReplacesTheFallbackScopeSource()
    {
        var collector = new LogCollector();
        using var provider = new ComponoLogCollectorProvider(collector);
        var external = new LoggerExternalScopeProvider();
        provider.SetScopeProvider(external);
        var logger = provider.CreateLogger("External.Category");

        using (logger.BeginScope("external scope"))
            logger.LogInformation("captured");

        var entry = collector.GetCapturedEntries().Should().ContainSingle().Subject;
        entry.Scopes.Should().Equal("external scope");
    }
}
