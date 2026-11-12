using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

/// <summary>
/// ADR-0055 Amendment 7: <see cref="LogVerificationBuilder.InCategoryPrefix"/> and
/// <see cref="LogVerificationBuilder.NotInCategoryPrefix"/> - ordinal, case-sensitive, literal prefixes
/// (no implied namespace separator).
/// </summary>
public sealed class CategoryPrefixFilterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static LogCollector CollectorWith(params (string Category, LogLevel Level)[] entries)
    {
        var collector = new LogCollector();
        foreach (var (category, level) in entries)
            collector.Add(new CapturedLogEntry(category, level, default, null, "message", null, null, null, [], Timestamp));

        return collector;
    }

    [Fact]
    public void InCategoryPrefix_MatchesTheCategoryItselfAndEverythingBeneathIt()
    {
        var collector = CollectorWith(
            ("MyApp.Jobs", LogLevel.Information),
            ("MyApp.Jobs.Scheduler", LogLevel.Information),
            ("MyApp.Api", LogLevel.Information));

        collector.Verify().InCategoryPrefix("MyApp.Jobs").Exactly(2);
    }

    [Fact]
    public void InCategoryPrefix_IsLiteral_SoAPrefixWithoutTrailingDotMatchesSimilarlyNamedCategories()
    {
        var collector = CollectorWith(
            ("MyApp.Jobs.Scheduler", LogLevel.Information),
            ("MyApp.JobsLegacy.Runner", LogLevel.Information));

        collector.Verify().InCategoryPrefix("MyApp.Jobs").Exactly(2);
    }

    [Fact]
    public void InCategoryPrefix_TrailingDot_ExcludesSimilarlyNamedCategoriesAndTheBareNamespace()
    {
        var collector = CollectorWith(
            ("MyApp.Jobs", LogLevel.Information),
            ("MyApp.Jobs.Scheduler", LogLevel.Information),
            ("MyApp.JobsLegacy.Runner", LogLevel.Information));

        collector.Verify().InCategoryPrefix("MyApp.Jobs.").Once();
    }

    [Fact]
    public void InCategoryPrefix_IsCaseSensitive()
    {
        var collector = CollectorWith(("MyApp.Jobs.Scheduler", LogLevel.Information));

        collector.Verify().InCategoryPrefix("myapp.jobs").Never();
    }

    [Fact]
    public void InCategoryPrefix_Empty_MatchesEveryEntry()
    {
        var collector = CollectorWith(("A", LogLevel.Information), ("B", LogLevel.Information));

        collector.Verify().InCategoryPrefix(string.Empty).Exactly(2);
    }

    [Fact]
    public void NotInCategoryPrefix_HostGuard_NoErrorsOutsideOwnNamespace()
    {
        var collector = CollectorWith(
            ("MyApp.Jobs.Scheduler", LogLevel.Error),
            ("Microsoft.Hosting.Lifetime", LogLevel.Information),
            ("Quartz.Core.QuartzScheduler", LogLevel.Error));

        collector.Verify().AtLevelOrAbove(LogLevel.Error).NotInCategoryPrefix("MyApp.Jobs.").Once();
        collector.Verify().AtLevelOrAbove(LogLevel.Error).InCategoryPrefix("MyApp.Jobs.").Once();
    }

    [Fact]
    public void NotInCategoryPrefix_TrailingDot_TreatsSimilarlyNamedCategoryAsOutside()
    {
        var collector = CollectorWith(
            ("MyApp.Jobs.Scheduler", LogLevel.Error),
            ("MyApp.JobsLegacy.Runner", LogLevel.Error));

        collector.Verify().NotInCategoryPrefix("MyApp.Jobs.").Once();
        collector.Verify().NotInCategoryPrefix("MyApp.Jobs").Never();
    }

    [Fact]
    public void NotInCategoryPrefix_IsCaseSensitive()
    {
        var collector = CollectorWith(("MyApp.Jobs.Scheduler", LogLevel.Error));

        collector.Verify().NotInCategoryPrefix("myapp.jobs.").Once();
    }

    [Fact]
    public void NotInCategoryPrefix_Empty_MatchesNoEntry()
    {
        var collector = CollectorWith(("A", LogLevel.Error));

        collector.Verify().NotInCategoryPrefix(string.Empty).Never();
    }

    [Fact]
    public void PrefixFilters_Null_ThrowArgumentNullException()
    {
        var collector = new LogCollector();

        var include = () => collector.Verify().InCategoryPrefix(null!);
        var exclude = () => collector.Verify().NotInCategoryPrefix(null!);

        include.Should().Throw<ArgumentNullException>().WithParameterName("prefix");
        exclude.Should().Throw<ArgumentNullException>().WithParameterName("prefix");
    }

    [Fact]
    public void InCategory_IsUnchanged_StillExactMatchOnly()
    {
        var collector = CollectorWith(("MyApp.Jobs.Scheduler", LogLevel.Information));

        collector.Verify().InCategory("MyApp.Jobs").Never();
    }

    [Fact]
    public void FailureMessage_DescribesBothPrefixFilters()
    {
        var collector = CollectorWith(("Quartz.Core", LogLevel.Error));

        var act = () => collector.Verify()
            .InCategoryPrefix("Quartz.")
            .NotInCategoryPrefix("Quartz.Core")
            .Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 1 call(s) to a log entry matching category starting with \"Quartz.\" and category not starting with \"Quartz.Core\", but received 0.");
    }

    [Fact]
    public void PrefixFilters_WorkOnAComposedCapturingLogger()
    {
        var logger = new CapturingLogger<CategoryPrefixFilterTests>();

        logger.LogWarning("warning");

        logger.Verify().InCategoryPrefix("Compono.Logging.").Once();
        logger.Verify().NotInCategoryPrefix("Compono.Logging.").Never();
    }
}
