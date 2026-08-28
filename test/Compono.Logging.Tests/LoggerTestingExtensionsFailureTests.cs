using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Compono.Logging.Tests;

/// <summary>
/// Two distinct failure conditions, deliberately covered side by side so the difference between
/// them stays obvious: a non-Compono.Logging <see cref="ILogger"/> (this file) vs. a recognized but
/// un-activated closed <c>ILogger&lt;T&gt;</c> request (<see cref="LoggingProviderCompositionTests"/>'s
/// missing-generated-activation test). Both throw <see cref="InvalidOperationException"/>, but for
/// different reasons and with different messages.
/// </summary>
public sealed class LoggerTestingExtensionsFailureTests
{
    [Fact]
    public void GetCapturedEntries_OnNonCompanoLogger_Throws()
    {
        ILogger logger = NullLogger<LoggerTestingExtensionsFailureTests>.Instance;

        var act = () => logger.GetCapturedEntries();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a Compono.Logging capturing logger*");
    }

    [Fact]
    public void GetLastCapturedEntry_OnNonCompanoLogger_Throws()
    {
        ILogger logger = NullLogger.Instance;

        var act = () => logger.GetLastCapturedEntry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a Compono.Logging capturing logger*");
    }

    [Fact]
    public void ClearCapturedEntries_OnNonCompanoLogger_Throws()
    {
        ILogger logger = NullLogger.Instance;

        var act = () => logger.ClearCapturedEntries();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a Compono.Logging capturing logger*");
    }

    [Fact]
    public void Verify_OnNonCompanoLogger_Throws()
    {
        ILogger logger = NullLogger.Instance;

        var act = () => logger.Verify();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a Compono.Logging capturing logger*UseLogging*");
    }
}
