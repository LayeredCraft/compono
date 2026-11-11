using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class HostLogCaptureTests
{
    [Fact]
    public void LoggerFactoryCreateLogger_CapturesTheExactCategory()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Arbitrary.Category");

        logger.LogInformation("hello");

        var entry = collector.GetLastCapturedEntry()!.Value;
        entry.Category.Should().Be("Arbitrary.Category");
    }

    [Fact]
    public void ContainerResolvedLoggerOfT_CategoryMatchesLoggerFactory()
    {
        var expected = TestCategoryProbe.For<OrderService>();
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILogger<OrderService>>();

        logger.LogWarning("composed category");

        collector.GetLastCapturedEntry()!.Value.Category.Should().Be(expected);
    }

    [Fact]
    public async Task HostLifetime_StartAndStopEntriesAreCaptured()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        var entries = collector.GetCapturedEntries();
        entries.Should().Contain(entry => entry.Category == "Microsoft.Hosting.Lifetime");
        entries.Should().Contain(entry => entry.Message.Contains("Application started", StringComparison.Ordinal));
        entries.Should().Contain(entry => entry.Message.Contains("Application is shutting down", StringComparison.Ordinal));
    }

    [Fact]
    public void VerifyAtLevelOrAboveError_Never_PassesForAHostThatLoggedNothingFailed()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Clean.Host");

        logger.LogInformation("all good");

        collector.Verify().AtLevelOrAbove(LogLevel.Error).Never();
    }

    [Fact]
    public void VerifyAtLevelOrAboveError_Never_FailsReadablyWhenAHostLoggedError()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Never.Composed.Category");

        logger.LogError("payment declined");

        var act = () => collector.Verify().AtLevelOrAbove(LogLevel.Error).Never();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*level Error or above*received 1*");
    }

    [Fact]
    public void OrdinaryLogCall_CapturesEveryEntryField()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Fields.Category");
        var exception = new InvalidOperationException("boom");
        var eventId = new EventId(17, "OrderFailed");

        logger.Log(LogLevel.Error, eventId, exception, "failed {Code}", 7);

        var entry = collector.GetLastCapturedEntry()!.Value;
        entry.Category.Should().Be("Fields.Category");
        entry.LogLevel.Should().Be(LogLevel.Error);
        entry.EventId.Should().Be(eventId);
        entry.Message.Should().Be("failed 7");
        entry.Exception.Should().BeSameAs(exception);
        entry.State.Should().NotBeNull();
        entry.Properties.Should().NotBeNull();
        entry.Properties!.Should().Contain(pair => pair.Key == "Code" && Equals(pair.Value, 7));
        entry.Properties!.Should().Contain(pair => pair.Key == "{OriginalFormat}" && Equals(pair.Value, "failed {Code}"));
        entry.MessageTemplate.Should().Be("failed {Code}");
        entry.Scopes.Should().BeEmpty();
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void CustomStateAndFormatter_ArePassedThroughUnchanged()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Fields.Category");
        var exception = new InvalidOperationException("boom");

        logger.Log(
            LogLevel.Error,
            new EventId(3),
            "plain state",
            exception,
            static (state, ex) => $"custom:{state}:{ex?.Message}");

        var entry = collector.GetLastCapturedEntry()!.Value;
        entry.Message.Should().Be("custom:plain state:boom");
        entry.State.Should().Be("plain state");
        entry.Exception.Should().BeSameAs(exception);
        entry.Properties.Should().BeNull();
        entry.MessageTemplate.Should().BeNull();
    }

    [Fact]
    public void LoggerMessageSourceGenerated_CapturesEveryEntryField()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Generated.Category");

        GeneratedLogMessages.OrderPlaced(logger, 7);

        var entry = collector.GetLastCapturedEntry()!.Value;
        entry.Category.Should().Be("Generated.Category");
        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Message.Should().Be("Order 7 placed");
        entry.Properties.Should().NotBeNull();
        entry.Properties!.Should().Contain(pair => pair.Key == "OrderId" && Equals(pair.Value, 7));
        entry.MessageTemplate.Should().Be("Order {OrderId} placed");
    }

    [Fact]
    public void ExceptionWithoutMessageArguments_IsCapturedByReference()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Exception.Category");
        var exception = new AggregateException(new InvalidOperationException("boom"));

        logger.LogError(exception, "failure with no message arguments");

        var entry = collector.GetLastCapturedEntry()!.Value;
        entry.Exception.Should().BeSameAs(exception);
        entry.Message.Should().Be("failure with no message arguments");
    }

    [Fact]
    public void DefaultHostMinimumLevel_ExcludesDebugAndTrace()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Filtered.Category");

        logger.LogTrace("trace");
        logger.LogDebug("debug");
        logger.LogInformation("information");

        var entries = collector.GetCapturedEntries();
        entries.Should().ContainSingle();
        entries[0].LogLevel.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void SetMinimumLevelTrace_IncludesDebugAndTrace()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector, builder => builder.Logging.SetMinimumLevel(LogLevel.Trace));
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Filtered.Category");

        logger.LogTrace("trace");
        logger.LogDebug("debug");
        logger.LogInformation("information");

        collector.GetCapturedEntries().Should().HaveCount(3);
    }

    [Fact]
    public void HostCategoryFilter_ExcludesFilteredEntries()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector, builder => builder.Logging.AddFilter("Filtered.Category", LogLevel.Warning));
        var factory = host.Services.GetRequiredService<ILoggerFactory>();

        factory.CreateLogger("Filtered.Category").LogInformation("excluded");
        factory.CreateLogger("Filtered.Category").LogWarning("included");
        factory.CreateLogger("Other.Category").LogInformation("other category is unaffected");

        var entries = collector.GetCapturedEntries();
        entries.Should().Contain(entry => entry.Message == "included");
        entries.Should().Contain(entry => entry.Message == "other category is unaffected");
        entries.Should().NotContain(entry => entry.Message == "excluded");
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void ProviderAliasConfiguration_AppliesProviderSpecificRuleToComponoProvider()
    {
        // ADR-0071 Amendment 1: [ProviderAlias("Compono")] lives in Logging.Abstractions only on
        // net10.0+, so this provider-specific configuration rule is a net10/net11 capability; net8
        // and net9 use the global minimum level/AddFilter rules exercised above.
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Logging:Compono:LogLevel:Default"] = "Warning";
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        using var host = builder.Build();
        var factory = host.Services.GetRequiredService<ILoggerFactory>();

        factory.CreateLogger("Alias.Category").LogInformation("below alias minimum");
        factory.CreateLogger("Alias.Category").LogWarning("meets alias minimum");

        var entries = collector.GetCapturedEntries();
        entries.Should().ContainSingle();
        entries[0].Message.Should().Be("meets alias minimum");
    }
#endif

    [Fact]
    public void LogLevelNone_IsNeverCaptured()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector, builder => builder.Logging.SetMinimumLevel(LogLevel.Trace));
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("None.Category");

        logger.Log(LogLevel.None, new EventId(1), "none", null, static (state, _) => state);

        collector.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void ServicesAddLogging_CanRegisterComponoCapture()
    {
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddLogging(logging => logging.AddComponoCapture(collector));
        using var host = builder.Build();

        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Services.Category").LogInformation("captured");

        collector.GetLastCapturedEntry()!.Value.Category.Should().Be("Services.Category");
    }

    [Fact]
    public void AddComponoCapture_NullArguments_ThrowArgumentNullException()
    {
        var collector = new LogCollector();
        var logging = Host.CreateApplicationBuilder().Logging;

        var nullBuilder = () => ComponoLoggingBuilderExtensions.AddComponoCapture(null!, collector);
        var nullCollector = () => logging.AddComponoCapture(null!);

        nullBuilder.Should().Throw<ArgumentNullException>();
        nullCollector.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddComponoCapture_ReturnsTheSameBuilderForChaining()
    {
        var builder = Host.CreateApplicationBuilder();
        var logging = builder.Logging;

        var returned = logging.AddComponoCapture(new LogCollector());

        returned.Should().BeSameAs(logging);
    }

    [Fact]
    public void EmptyCollector_GetLastIsNullAndVerifyNeverPasses()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);

        _ = host.Services.GetRequiredService<ILoggerFactory>();

        collector.GetCapturedEntries().Should().BeEmpty();
        collector.GetLastCapturedEntry().Should().BeNull();
        collector.Verify().Never();
    }

    [Fact]
    public void ClearCapturedEntries_DiscardsHostCaptures()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Clear.Category");
        logger.LogInformation("one");
        logger.LogInformation("two");

        collector.ClearCapturedEntries();

        collector.GetCapturedEntries().Should().BeEmpty();
        collector.GetLastCapturedEntry().Should().BeNull();
    }

    private static IHost CreateHost(LogCollector collector, Action<HostApplicationBuilder>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        configure?.Invoke(builder);
        return builder.Build();
    }
}

public sealed class HostScopeCaptureTests
{
    [Fact]
    public async Task NestedScopesAcrossAwait_AreCapturedOutermostFirst()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Scopes.Category");

        using (logger.BeginScope("Outer"))
        {
            await Task.Yield();
            using (logger.BeginScope("Inner"))
            {
                await Task.Yield();
                logger.LogInformation("inside");
            }
        }

        var scopes = collector.GetLastCapturedEntry()!.Value.Scopes;
        scopes.Should().HaveCount(2);
        scopes[0].Should().Be("Outer");
        scopes[1].Should().Be("Inner");
    }

    [Fact]
    public void DictionaryScope_IsCapturedAsTheSameInstance()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Scopes.Category");
        var scope = new Dictionary<string, object> { ["OrderId"] = 42 };

        using (logger.BeginScope(scope))
            logger.LogInformation("inside");

        collector.GetLastCapturedEntry()!.Value.Scopes.Should().ContainSingle().Which.Should().BeSameAs(scope);
    }

    [Fact]
    public async Task SiblingTasks_KeepTheirScopesIsolated()
    {
        var collector = new LogCollector();
        using var host = CreateHost(collector);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Scopes.Category");
        using var startGate = new Barrier(2);

        var first = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            using (logger.BeginScope("TaskA"))
                logger.LogInformation("from A");
        }, TestContext.Current.CancellationToken);

        var second = Task.Run(() =>
        {
            startGate.SignalAndWait(TestContext.Current.CancellationToken);
            using (logger.BeginScope("TaskB"))
                logger.LogInformation("from B");
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(first, second);

        var entries = collector.GetCapturedEntries();
        entries.Single(entry => entry.Message == "from A").Scopes.Should().ContainSingle().Which.Should().Be("TaskA");
        entries.Single(entry => entry.Message == "from B").Scopes.Should().ContainSingle().Which.Should().Be("TaskB");
    }

    [Fact]
    public void ActivityTrackingOptions_AddsActivityScope()
    {
        var collector = new LogCollector();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        builder.Services.Configure<LoggerFactoryOptions>(
            options => options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Activity.Category");

        using var activity = new Activity("compono-test").Start();
        logger.LogInformation("with activity");

        var scopes = collector.GetLastCapturedEntry()!.Value.Scopes;
        scopes.Should().Contain(scope => scope.ToString()!.Contains(activity.TraceId.ToHexString(), StringComparison.Ordinal));
    }

    private static IHost CreateHost(LogCollector collector)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddComponoCapture(collector);
        return builder.Build();
    }
}
