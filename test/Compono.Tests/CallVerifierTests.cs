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

    // PLAN-0063/ADR-0044 Amendment 22: AtLeast/AtMost boundary tests (below/equal/above), negative-
    // count behavior matching Exactly's existing (non-)validation, and AtLeast(0)/AtMost(0) edge cases.

    [Fact]
    public void AtLeast_WhenObservedCountIsBelowThreshold_ThrowsWithMessage()
    {
        var verifier = new CallVerifier(2, "IFoo.Bar");

        var act = () => verifier.AtLeast(3);

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected at least 3 call(s) to IFoo.Bar, but received 2.");
    }

    [Fact]
    public void AtLeast_WhenObservedCountEqualsThreshold_DoesNotThrow()
    {
        var verifier = new CallVerifier(3, "IFoo.Bar");

        var act = () => verifier.AtLeast(3);

        act.Should().NotThrow();
    }

    [Fact]
    public void AtLeast_WhenObservedCountIsAboveThreshold_DoesNotThrow()
    {
        var verifier = new CallVerifier(4, "IFoo.Bar");

        var act = () => verifier.AtLeast(3);

        act.Should().NotThrow();
    }

    [Fact]
    public void AtLeast_Zero_AlwaysPasses()
    {
        var verifier = new CallVerifier(0, "IFoo.Bar");

        var act = () => verifier.AtLeast(0);

        act.Should().NotThrow();
    }

    [Fact]
    public void AtLeast_NegativeThreshold_BehavesLikeExactlyAlwaysVacuouslyTrue()
    {
        var verifier = new CallVerifier(0, "IFoo.Bar");

        var act = () => verifier.AtLeast(-1);

        act.Should().NotThrow("observedCount can never be negative, so AtLeast(-1) can never fail, " +
            "matching Exactly's existing no-argument-validation behavior (ADR-0044 Amendment 22)");
    }

    [Fact]
    public void AtMost_WhenObservedCountIsAboveThreshold_ThrowsWithMessage()
    {
        var verifier = new CallVerifier(4, "IFoo.Bar");

        var act = () => verifier.AtMost(3);

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected at most 3 call(s) to IFoo.Bar, but received 4.");
    }

    [Fact]
    public void AtMost_WhenObservedCountEqualsThreshold_DoesNotThrow()
    {
        var verifier = new CallVerifier(3, "IFoo.Bar");

        var act = () => verifier.AtMost(3);

        act.Should().NotThrow();
    }

    [Fact]
    public void AtMost_WhenObservedCountIsBelowThreshold_DoesNotThrow()
    {
        var verifier = new CallVerifier(2, "IFoo.Bar");

        var act = () => verifier.AtMost(3);

        act.Should().NotThrow();
    }

    [Fact]
    public void AtMost_Zero_EquivalentToNever_WhenNeverCalled()
    {
        var verifier = new CallVerifier(0, "IFoo.Bar");

        var act = () => verifier.AtMost(0);

        act.Should().NotThrow();
    }

    [Fact]
    public void AtMost_Zero_EquivalentToNever_WhenCalled()
    {
        var verifier = new CallVerifier(1, "IFoo.Bar");

        var act = () => verifier.AtMost(0);

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("Expected at most 0 call(s) to IFoo.Bar, but received 1.");
    }

    [Fact]
    public void AtMost_NegativeThreshold_ThrowsBecauseObservedCountCanNeverBeNegative()
    {
        var verifier = new CallVerifier(0, "IFoo.Bar");

        var act = () => verifier.AtMost(-1);

        act.Should().Throw<TestDoubleVerificationException>(
            "0 > -1, matching Exactly's existing no-argument-validation behavior (ADR-0044 Amendment 22)");
    }

    // PLAN-0063/ADR-0060: ReturnConfig<T>.ClearObservedCalls() - the mirror of ClearConfiguredResponse(),
    // clearing only CallCount, never Value/Exception/Sequence/SequenceOrdinal.

    [Fact]
    public void ClearObservedCalls_ResetsCallCountToZero()
    {
        var slot = new ReturnConfig<string>();
        slot.RecordCall();
        slot.RecordCall();

        slot.ClearObservedCalls();

        slot.ConfiguredCallCount.Should().Be(0);
    }

    [Fact]
    public void ClearObservedCalls_DoesNotAffectConfiguredValue()
    {
        var slot = new ReturnConfig<string>();
        new ReturnConfigBuilder<string>(ref slot).Returns("configured");
        slot.RecordCall();

        slot.ClearObservedCalls();

        slot.HasConfiguredValue.Should().BeTrue();
        slot.ConfiguredValue.Should().Be("configured");
    }

    [Fact]
    public void ClearObservedCalls_DoesNotAffectConfiguredException()
    {
        var slot = new ReturnConfig<string>();
        var exception = new InvalidOperationException("boom");
        new ReturnConfigBuilder<string>(ref slot).Throws(exception);
        slot.RecordCall();

        slot.ClearObservedCalls();

        slot.HasConfiguredException.Should().BeTrue();
        slot.ConfiguredException.Should().BeSameAs(exception);
    }

    [Fact]
    public void ClearObservedCalls_DoesNotRewindSequenceOrdinal()
    {
        var slot = new ReturnConfig<string>();
        new ReturnConfigBuilder<string>(ref slot).ReturnsSequence("A", "B", "C");
        slot.NextSequenceOutcome().Should().Be("A");
        slot.NextSequenceOutcome().Should().Be("B");

        slot.ClearObservedCalls();

        slot.NextSequenceOutcome().Should().Be("C", "SequenceOrdinal is runtime progress through configured " +
            "behavior, not observation history - ClearCalls() must never rewind it (ADR-0060)");
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
