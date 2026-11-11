using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class LogCollectorRegistrationTests
{
    [Fact]
    public void SameCollectorRegisteredTwice_ProducesOneProviderAndOneCopyOfEachEntry()
    {
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        builder.Logging.AddComponoCapture(collector);
        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cardinality.Category").LogInformation("once");

        host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Should().ContainSingle();
        collector.GetCapturedEntries().Should().ContainSingle();
    }

    [Fact]
    public void DifferentCollectorsOnOneBuilder_EachReceivesEveryEntry()
    {
        var first = new LogCollector();
        var second = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(first);
        builder.Logging.AddComponoCapture(second);
        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cardinality.Category").LogInformation("everyone");

        host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Should().HaveCount(2);
        first.GetCapturedEntries().Should().ContainSingle().Which.Message.Should().Be("everyone");
        second.GetCapturedEntries().Should().ContainSingle().Which.Message.Should().Be("everyone");
    }

    [Fact]
    public void SameCollectorAcrossTwoHosts_ReceivesEntriesFromBoth()
    {
        var collector = new LogCollector();
        using var firstHost = CreateHost(collector);
        using var secondHost = CreateHost(collector);

        firstHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("First").LogInformation("from first");
        secondHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Second").LogInformation("from second");

        collector.GetCapturedEntries().Should().HaveCount(2);
        collector.GetCapturedEntries().Should().Contain(entry => entry.Message == "from first");
        collector.GetCapturedEntries().Should().Contain(entry => entry.Message == "from second");
    }

    private static IHost CreateHost(LogCollector collector)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        return builder.Build();
    }
}
