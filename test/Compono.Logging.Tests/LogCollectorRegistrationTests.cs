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
    public void ClearProvidersThenReRegisteringTheSameCollector_StillCaptures()
    {
        // Regression (PR #177 review): duplicate detection must track the live ILoggerProvider
        // registration, not a separate marker that ClearProviders() leaves behind - otherwise the
        // second AddComponoCapture is silently skipped and Never() passes without capturing anything.
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddComponoCapture(collector);
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cleared.Category").LogError("after re-registration");

        host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Should().ContainSingle();
        collector.GetCapturedEntries().Should().ContainSingle().Which.Message.Should().Be("after re-registration");
        collector.Verify().AtLevelOrAbove(LogLevel.Error).InCategory("Cleared.Category").Once();
        var act = () => collector.Verify().AtLevelOrAbove(LogLevel.Error).Never();
        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public void ClearProvidersThenReRegisteringTwice_StaysIdempotent()
    {
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddComponoCapture(collector);
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        builder.Logging.AddComponoCapture(collector);
        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cleared.Category").LogInformation("once");

        host.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Should().ContainSingle();
        collector.GetCapturedEntries().Should().ContainSingle();
    }

    [Fact]
    public void ClearProvidersThenRegisteringADifferentCollector_OnlyTheNewCollectorCaptures()
    {
        var cleared = new LogCollector();
        var replacement = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddComponoCapture(cleared);
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(replacement);
        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cleared.Category").LogWarning("replacement only");

        cleared.GetCapturedEntries().Should().BeEmpty();
        replacement.GetCapturedEntries().Should().ContainSingle().Which.Message.Should().Be("replacement only");
    }

    [Fact]
    public void SameCollectorOnIndependentServiceCollections_IsNotTreatedAsADuplicate()
    {
        var collector = new LogCollector();
        var first = Host.CreateApplicationBuilder();
        var second = Host.CreateApplicationBuilder();
        first.Logging.ClearProviders();
        second.Logging.ClearProviders();
        first.Logging.AddComponoCapture(collector);
        second.Logging.AddComponoCapture(collector);
        using var firstHost = first.Build();
        using var secondHost = second.Build();

        firstHost.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Should().ContainSingle();
        secondHost.Services.GetServices<ILoggerProvider>().OfType<ComponoLogCollectorProvider>().Should().ContainSingle();
        firstHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("First").LogError("from first");
        secondHost.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Second").LogError("from second");

        collector.Verify().AtLevelOrAbove(LogLevel.Error).Exactly(2);
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
