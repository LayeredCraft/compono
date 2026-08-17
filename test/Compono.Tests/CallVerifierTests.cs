namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="CallVerifier"/>'s pass/fail behavior and <see cref="ReturnConfig{T}.RecordCall"/>'s
/// counter, matching ADR-0044 Requirement 3's deliberately minimal <c>Never</c>/<c>Once</c>/<c>Exactly</c>
/// surface.
/// </summary>
public sealed class CallVerifierTests
{
    [Fact]
    public void Never_WhenNeverCalled_DoesNotThrow()
    {
        var verifier = new CallVerifier(0, "IFoo.Bar");

        var act = verifier.Never;

        act.Should().NotThrow();
    }

    [Fact]
    public void Never_WhenCalled_ThrowsWithMessage()
    {
        var verifier = new CallVerifier(1, "IFoo.Bar");

        var act = verifier.Never;

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 0 call(s) to IFoo.Bar, but received 1.");
    }

    [Fact]
    public void Once_WhenCalledOnce_DoesNotThrow()
    {
        var verifier = new CallVerifier(1, "IFoo.Bar");

        var act = verifier.Once;

        act.Should().NotThrow();
    }

    [Fact]
    public void Once_WhenCalledTwice_ThrowsWithMessage()
    {
        var verifier = new CallVerifier(2, "IFoo.Bar");

        var act = verifier.Once;

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 1 call(s) to IFoo.Bar, but received 2.");
    }

    [Fact]
    public void Once_WhenNeverCalled_ThrowsWithMessage()
    {
        var verifier = new CallVerifier(0, "IFoo.Bar");

        var act = verifier.Once;

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 1 call(s) to IFoo.Bar, but received 0.");
    }

    [Fact]
    public void Exactly_WhenCountMatches_DoesNotThrow()
    {
        var verifier = new CallVerifier(3, "IFoo.Bar");

        var act = () => verifier.Exactly(3);

        act.Should().NotThrow();
    }

    [Fact]
    public void Exactly_WhenCountDiffers_ThrowsWithMessage()
    {
        var verifier = new CallVerifier(3, "IFoo.Bar");

        var act = () => verifier.Exactly(5);

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected exactly 5 call(s) to IFoo.Bar, but received 3.");
    }

    [Fact]
    public void RecordCall_IncrementsConfiguredCallCount()
    {
        var slot = new ReturnConfig<string>();

        slot.RecordCall();
        slot.RecordCall();
        slot.RecordCall();

        slot.ConfiguredCallCount.Should().Be(3);
    }

    [Fact]
    public void RecordCall_UnderConcurrentContention_CountsEveryCall()
    {
        var slot = new ReturnConfig<string>();
        const int callsPerThread = 1_000;
        const int threadCount = 8;

        Parallel.For(0, threadCount, _ =>
        {
            for (var i = 0; i < callsPerThread; i++)
                slot.RecordCall();
        });

        slot.ConfiguredCallCount.Should().Be(threadCount * callsPerThread);
    }
}
