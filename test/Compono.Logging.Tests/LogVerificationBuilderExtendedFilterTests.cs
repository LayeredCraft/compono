using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class LogVerificationBuilderExtendedFilterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AtLevelOrAbove_Trace_MatchesEveryCapturedLevel()
    {
        var logger = new CapturingLogger<LogVerificationBuilderExtendedFilterTests>();

        logger.LogTrace("trace");
        logger.LogDebug("debug");
        logger.LogInformation("information");
        logger.LogWarning("warning");
        logger.LogError("error");
        logger.LogCritical("critical");

        logger.Verify().AtLevelOrAbove(LogLevel.Trace).Exactly(6);
    }

    [Fact]
    public void AtLevelOrAbove_Warning_ExcludesLessSevereLevels()
    {
        var logger = new CapturingLogger<LogVerificationBuilderExtendedFilterTests>();

        logger.LogInformation("information");
        logger.LogWarning("warning");
        logger.LogError("error");
        logger.LogCritical("critical");

        logger.Verify().AtLevelOrAbove(LogLevel.Warning).Exactly(3);
    }

    [Fact]
    public void AtLevelOrAbove_Error_MatchesErrorAndCriticalOnly()
    {
        var logger = new CapturingLogger<LogVerificationBuilderExtendedFilterTests>();

        logger.LogWarning("warning");
        logger.LogError("error");
        logger.LogCritical("critical");

        logger.Verify().AtLevelOrAbove(LogLevel.Error).Exactly(2);
        logger.Verify().AtLevelOrAbove(LogLevel.Critical).Once();
    }

    [Fact]
    public void AtLevelOrAbove_None_MatchesNothing_EvenIfAnEntryIsPresent()
    {
        var collector = new LogCollector();
        collector.Add(CreateEntry("Category.A", LogLevel.Critical));

        collector.Verify().AtLevelOrAbove(LogLevel.None).Never();
    }

    [Fact]
    public void AtLevelOrAbove_CombinesWithOtherFilters()
    {
        var collector = new LogCollector();
        collector.Add(CreateEntry("Category.A", LogLevel.Error, "first"));
        collector.Add(CreateEntry("Category.B", LogLevel.Error, "second"));
        collector.Add(CreateEntry("Category.A", LogLevel.Warning, "retrying order"));

        collector.Verify()
            .AtLevelOrAbove(LogLevel.Error)
            .InCategory("Category.A")
            .WithMessageContaining("first")
            .Once();
    }

    [Fact]
    public void AtLevelOrAbove_FailureMessageNamesTheLevelAndAbove()
    {
        var logger = new CapturingLogger<LogVerificationBuilderExtendedFilterTests>();
        logger.LogError("boom");

        var act = () => logger.Verify().AtLevelOrAbove(LogLevel.Error).Never();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 0 call(s) to a log entry matching level Error or above, but received 1.");
    }

    [Fact]
    public void InCategory_MatchesExactCategory()
    {
        var collector = new LogCollector();
        collector.Add(CreateEntry("Category.A", LogLevel.Information));
        collector.Add(CreateEntry("Category.B", LogLevel.Information));

        collector.Verify().InCategory("Category.A").Once();
    }

    [Fact]
    public void InCategory_IsCaseSensitive()
    {
        var collector = new LogCollector();
        collector.Add(CreateEntry("Category.A", LogLevel.Information));

        collector.Verify().InCategory("category.a").Never();
    }

    [Fact]
    public void InCategory_DoesNotMatchPrefix()
    {
        var collector = new LogCollector();
        collector.Add(CreateEntry("Category.A.Nested", LogLevel.Information));

        collector.Verify().InCategory("Category.A").Never();
    }

    [Fact]
    public void InCategory_Null_ThrowsArgumentNullException()
    {
        var collector = new LogCollector();

        var act = () => collector.Verify().InCategory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InCategory_FailureMessageNamesTheCategory()
    {
        var collector = new LogCollector();
        collector.Add(CreateEntry("Category.B", LogLevel.Information));

        var act = () => collector.Verify().InCategory("Category.A").Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 1 call(s) to a log entry matching category \"Category.A\", but received 0.");
    }

    private static CapturedLogEntry CreateEntry(string category, LogLevel level, string message = "message") =>
        new(category, level, default, null, message, null, null, null, [], Timestamp);
}
