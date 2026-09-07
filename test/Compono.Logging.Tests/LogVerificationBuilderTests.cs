using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class LogVerificationBuilderTests
{
    [Fact]
    public void Verify_AllFiltersCombined_MatchesExactlyOne()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();

        logger.LogWarning(new EventId(7), new InvalidOperationException("boom"), "retrying order {OrderId}", 42);
        logger.LogInformation("unrelated");

        logger.Verify()
            .AtLevel(LogLevel.Warning)
            .WithEventId(new EventId(7))
            .WithException<InvalidOperationException>()
            .WithMessageContaining("retrying")
            .Matching(e => e.Message.Contains("42"))
            .Once();
    }

    [Fact]
    public void Never_PassesWhenNoMatches()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogInformation("something else");

        logger.Verify().AtLevel(LogLevel.Error).Never();
    }

    [Fact]
    public void Never_ThrowsWhenAtLeastOneMatch()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogError("boom");

        var act = () => logger.Verify().AtLevel(LogLevel.Error).Never();

        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public void Exactly_PassesOnExactCount()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogWarning("one");
        logger.LogWarning("two");

        logger.Verify().AtLevel(LogLevel.Warning).Exactly(2);
    }

    [Fact]
    public void Exactly_ThrowsOnWrongCount()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogWarning("one");

        var act = () => logger.Verify().AtLevel(LogLevel.Warning).Exactly(2);

        act.Should().Throw<TestDoubleVerificationException>();
    }

    // PLAN-0063/ADR-0044 Amendment 22: AtLeast/AtMost forwarders - proving filtering happens BEFORE
    // the count terminal, not merely that the methods compile.

    [Fact]
    public void AtLeast_CountsOnlyTheFilteredSubset_NotTheWholeCaptureBuffer()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogWarning("one");
        logger.LogWarning("two");
        logger.LogInformation("unrelated");
        logger.LogInformation("also unrelated");

        // Four entries total, only two at Warning - AtLeast(2) must pass against the filtered count,
        // and AtLeast(3) must fail even though four entries exist overall.
        logger.Verify().AtLevel(LogLevel.Warning).AtLeast(2);

        var act = () => logger.Verify().AtLevel(LogLevel.Warning).AtLeast(3);
        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected at least 3 call(s) to a log entry matching level Warning, but received 2.");
    }

    [Fact]
    public void AtMost_CountsOnlyTheFilteredSubset_NotTheWholeCaptureBuffer()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogWarning("one");
        logger.LogWarning("two");
        logger.LogInformation("unrelated");
        logger.LogInformation("also unrelated");

        // AtMost(2) passes against the filtered Warning-only count even though four entries exist.
        logger.Verify().AtLevel(LogLevel.Warning).AtMost(2);

        var act = () => logger.Verify().AtLevel(LogLevel.Warning).AtMost(1);
        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected at most 1 call(s) to a log entry matching level Warning, but received 2.");
    }

    [Fact]
    public void AtLevel_Alone_NarrowsToThatLevel()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogWarning("warn");
        logger.LogError("error");

        logger.Verify().AtLevel(LogLevel.Error).Once();
    }

    [Fact]
    public void WithEventId_Alone_NarrowsToThatEventId()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.Log(LogLevel.Information, new EventId(1), "a", null, static (s, _) => s);
        logger.Log(LogLevel.Information, new EventId(2), "b", null, static (s, _) => s);

        logger.Verify().WithEventId(new EventId(2)).Once();
    }

    [Fact]
    public void WithException_Alone_NarrowsToThatExceptionType()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogError(new InvalidOperationException(), "a");
        logger.LogError(new ArgumentException(), "b");

        logger.Verify().WithException<ArgumentException>().Once();
    }

    [Fact]
    public void WithMessageContaining_Alone_NarrowsByOrdinalSubstring()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();
        logger.LogInformation("retrying request");
        logger.LogInformation("succeeded");

        logger.Verify().WithMessageContaining("retrying").Once();
    }

    [Fact]
    public void Verify_UsesRealCallVerifier_FailureThrowsTestDoubleVerificationExceptionWithDescription()
    {
        var logger = new CapturingLogger<LogVerificationBuilderTests>();

        var act = () => logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*Warning*retry*");
    }
}
