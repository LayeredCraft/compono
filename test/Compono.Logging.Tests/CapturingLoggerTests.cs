using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class CapturingLoggerTests
{
    [Fact]
    public void DirectConstruction_NonGeneric_WorksStandalone()
    {
        var logger = new CapturingLogger();

        logger.LogWarning("hello");

        logger.GetCapturedEntries().Should().HaveCount(1);
    }

    [Fact]
    public void DirectConstruction_Generic_WorksStandalone()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogWarning("hello");

        logger.GetCapturedEntries().Should().HaveCount(1);
    }

    [Fact]
    public void DirectConstruction_DefaultOptions_MinimumLevelIsTrace()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.IsEnabled(LogLevel.Trace).Should().BeTrue();
        logger.IsEnabled(LogLevel.Critical).Should().BeTrue();
    }

    [Theory]
    [InlineData(LogLevel.Trace, true)]
    [InlineData(LogLevel.Debug, true)]
    [InlineData(LogLevel.Information, true)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public void MinimumLevel_Trace_EnablesEveryOrdinaryLevel(LogLevel level, bool expected)
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Trace });

        logger.IsEnabled(level).Should().Be(expected);
    }

    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public void MinimumLevel_Warning_ExcludesLowerLevels(LogLevel level, bool expected)
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Warning });

        logger.IsEnabled(level).Should().Be(expected);

        logger.Log(level, new EventId(1), "state", null, static (s, _) => s);
        logger.GetCapturedEntries().Should().HaveCount(expected ? 1 : 0);
    }

    [Fact]
    public void MinimumLevel_None_DisablesEveryOrdinaryLevel()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.None });

        foreach (var level in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            logger.IsEnabled(level).Should().BeFalse();

        logger.LogCritical("should not capture");
        logger.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void LogLevelNone_IsNeverEnabled_RegardlessOfMinimumLevel()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Trace });

        logger.IsEnabled(LogLevel.None).Should().BeFalse();
    }

    [Fact]
    public void DirectLogCall_WithLogLevelNone_CapturesNothing()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Trace });

        logger.Log(LogLevel.None, new EventId(1), "state", null, static (s, _) => s);

        logger.GetCapturedEntries().Should().BeEmpty();
    }

    [Fact]
    public void DisabledEntry_DoesNotAffectLastEntryOrVerify()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Warning });

        logger.LogInformation("ignored");

        logger.GetLastCapturedEntry().Should().BeNull();
        logger.Verify().Never();
    }

    [Fact]
    public void LogInformation_CapturesFormattedMessageStateAndProperties()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogInformation("User {UserId} did {Action}", 42, "checkout");

        var entry = logger.GetLastCapturedEntry()!.Value;

        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Message.Should().Be("User 42 did checkout");
        entry.State.Should().NotBeNull();
        entry.Properties.Should().NotBeNull();
        entry.Properties!.Should().ContainSingle(p => p.Key == "UserId" && Equals(p.Value, 42));
        entry.Properties!.Should().ContainSingle(p => p.Key == "Action" && Equals(p.Value, "checkout"));
        entry.MessageTemplate.Should().Be("User {UserId} did {Action}");
    }

    [Fact]
    public void LoggerMessageSourceGenerated_CapturesSameStructuredShapeAsOrdinaryCall()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        GeneratedLogMessages.OrderPlaced(logger, 7);

        var entry = logger.GetLastCapturedEntry()!.Value;

        entry.LogLevel.Should().Be(LogLevel.Information);
        entry.Message.Should().Be("Order 7 placed");
        entry.Properties.Should().NotBeNull();
        entry.Properties!.Should().ContainSingle(p => p.Key == "OrderId" && Equals(p.Value, 7));
        entry.MessageTemplate.Should().Be("Order {OrderId} placed");
    }

    [Fact]
    public void StructuredValue_Null_IsPreservedAsNull()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogInformation("User {UserId}", (int?)null);

        var entry = logger.GetLastCapturedEntry()!.Value;

        entry.Properties.Should().NotBeNull();
        entry.Properties!.Single(p => p.Key == "UserId").Value.Should().BeNull();
    }

    [Fact]
    public void NonStructuredState_HasNoProperties()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();
        var (state, formatter) = ((object)"plain state", (Func<object, Exception?, string>)((s, _) => (string)s));

        logger.Log(LogLevel.Information, new EventId(0), state, null, formatter);

        var entry = logger.GetLastCapturedEntry()!.Value;
        entry.Properties.Should().BeNull();
        entry.MessageTemplate.Should().BeNull();
        entry.State.Should().Be("plain state");
    }

    [Fact]
    public void NoActiveScopes_ScopesIsEmpty()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogInformation("no scope");

        logger.GetLastCapturedEntry()!.Value.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void SingleScope_IsCaptured()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        using (logger.BeginScope("Processing {OrderId}", 1))
            logger.LogInformation("inside scope");

        logger.GetLastCapturedEntry()!.Value.Scopes.Should().HaveCount(1);
    }

    [Fact]
    public void NestedScopes_AreCapturedOutermostFirst()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        using (logger.BeginScope("Outer"))
        using (logger.BeginScope("Middle"))
        using (logger.BeginScope("Inner"))
            logger.LogInformation("deep");

        var scopes = logger.GetLastCapturedEntry()!.Value.Scopes;
        scopes.Should().HaveCount(3);
        scopes[0].Should().Be("Outer");
        scopes[1].Should().Be("Middle");
        scopes[2].Should().Be("Inner");
    }

    [Fact]
    public void DisposingInnerScope_RemovesOnlyThatScope()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        using (logger.BeginScope("Outer"))
        {
            var inner = logger.BeginScope("Inner");
            inner!.Dispose();
            logger.LogInformation("after inner disposed");
        }

        var scopes = logger.GetLastCapturedEntry()!.Value.Scopes;
        scopes.Should().ContainSingle().Which.Should().Be("Outer");
    }

    [Fact]
    public void DisposingOuterScope_ClearsBoth()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        var outer = logger.BeginScope("Outer");
        var inner = logger.BeginScope("Inner");
        inner!.Dispose();
        outer!.Dispose();
        logger.LogInformation("no scopes left");

        logger.GetLastCapturedEntry()!.Value.Scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task Scope_FlowsAcrossAwait()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        using (logger.BeginScope("AsyncScope"))
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
            logger.LogInformation("after await");
        }

        logger.GetLastCapturedEntry()!.Value.Scopes.Should().ContainSingle().Which.Should().Be("AsyncScope");
    }

    [Fact]
    public async Task Scope_SnapshotIsFixedAtCaptureTime()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        using (logger.BeginScope("First"))
        {
            logger.LogInformation("first entry");
        }

        using (logger.BeginScope("Second"))
        {
            logger.LogInformation("second entry");
        }

        await Task.CompletedTask;

        var entries = logger.GetCapturedEntries();
        entries[0].Scopes.Should().ContainSingle().Which.Should().Be("First");
        entries[1].Scopes.Should().ContainSingle().Which.Should().Be("Second");
    }

    [Fact]
    public void ClearCapturedEntries_RemovesEverything()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();
        logger.LogInformation("one");
        logger.LogInformation("two");

        logger.ClearCapturedEntries();

        logger.GetCapturedEntries().Should().BeEmpty();
        logger.GetLastCapturedEntry().Should().BeNull();
    }

    [Fact]
    public void DirectlyConstructed_AndProviderProduced_BehaveIdentically()
    {
        var direct = new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Warning });
        var viaProvider = (ILogger<CapturingLoggerTests>)new CapturingLogger<CapturingLoggerTests>(new LoggingOptions { MinimumLevel = LogLevel.Warning });

        direct.LogInformation("filtered");
        viaProvider.LogInformation("filtered");
        direct.LogWarning("kept");
        viaProvider.LogWarning("kept");

        direct.GetCapturedEntries().Should().HaveCount(1);
        viaProvider.GetCapturedEntries().Should().HaveCount(1);
        direct.GetLastCapturedEntry()!.Value.Message.Should().Be(viaProvider.GetLastCapturedEntry()!.Value.Message);
    }
}

internal static partial class GeneratedLogMessages
{
    [LoggerMessage(LogLevel.Information, "Order {OrderId} placed")]
    public static partial void OrderPlaced(ILogger logger, int orderId);
}
