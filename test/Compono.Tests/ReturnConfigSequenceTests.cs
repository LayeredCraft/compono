namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="ReturnConfig{T}.NextSequenceOutcome"/> and
/// <see cref="ReturnConfigBuilder{T}.ReturnsSequence"/> - ADR-0054's sequential/call-count-based
/// response capability. Covers the acceptance scenarios that ADR named explicitly: value chains,
/// mixed exception/value chains, exhaustion, reconfiguration, call recording staying independent of
/// response consumption, and concurrent consumption not corrupting ordinal state.
/// </summary>
public sealed class ReturnConfigSequenceTests
{
    [Fact]
    public void ReturnsSequence_ValueThenValueThenValue_ConsumedInOrder()
    {
        var slot = new ReturnConfig<bool>();
        new ReturnConfigBuilder<bool>(ref slot).ReturnsSequence(false, false, true);

        slot.HasConfiguredSequence.Should().BeTrue();
        slot.NextSequenceOutcome().Should().BeFalse();
        slot.NextSequenceOutcome().Should().BeFalse();
        slot.NextSequenceOutcome().Should().BeTrue();
    }

    [Fact]
    public void ReturnsSequence_ExceptionThenValue_ThrowsThenReturns()
    {
        var slot = new ReturnConfig<string>();
        var exception = new InvalidOperationException("first call fails");
        new ReturnConfigBuilder<string>(ref slot).ReturnsSequence(SequenceOutcome.Throw(exception), "second call succeeds");

        var act = () => slot.NextSequenceOutcome();
        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(exception);

        slot.NextSequenceOutcome().Should().Be("second call succeeds");
    }

    [Fact]
    public void ReturnsSequence_ExceptionThenExceptionThenValue_MatchesRealRetryShape()
    {
        var slot = new ReturnConfig<string>();
        var first = new InvalidOperationException("attempt 1");
        var second = new InvalidOperationException("attempt 2");
        new ReturnConfigBuilder<string>(ref slot).ReturnsSequence(SequenceOutcome.Throw(first), SequenceOutcome.Throw(second), "attempt 3 succeeds");

        Invoking(() => slot.NextSequenceOutcome()).Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(first);
        Invoking(() => slot.NextSequenceOutcome()).Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(second);
        slot.NextSequenceOutcome().Should().Be("attempt 3 succeeds");

        static Action Invoking(Action action) => action;
    }

    [Fact]
    public void ReturnsSequence_AfterExhaustion_RepeatsFinalOutcome()
    {
        var slot = new ReturnConfig<int>();
        new ReturnConfigBuilder<int>(ref slot).ReturnsSequence(1, 2, 3);

        slot.NextSequenceOutcome().Should().Be(1);
        slot.NextSequenceOutcome().Should().Be(2);
        slot.NextSequenceOutcome().Should().Be(3);

        // Exhausted - every further call repeats the final configured outcome (ADR-0054).
        slot.NextSequenceOutcome().Should().Be(3);
        slot.NextSequenceOutcome().Should().Be(3);
    }

    [Fact]
    public void ReturnsSequence_CalledAgain_ReplacesSequenceAndResetsOrdinal()
    {
        var slot = new ReturnConfig<int>();
        var builder = new ReturnConfigBuilder<int>(ref slot);
        builder.ReturnsSequence(1, 2, 3);
        slot.NextSequenceOutcome().Should().Be(1);
        slot.NextSequenceOutcome().Should().Be(2);

        builder.ReturnsSequence(100, 200);

        // Ordinal reset to 0 against the NEW sequence, not continued against the old one.
        slot.NextSequenceOutcome().Should().Be(100);
        slot.NextSequenceOutcome().Should().Be(200);
        slot.NextSequenceOutcome().Should().Be(200);
    }

    [Fact]
    public void Returns_AfterReturnsSequence_ClearsSequenceState()
    {
        var slot = new ReturnConfig<int>();
        var builder = new ReturnConfigBuilder<int>(ref slot);
        builder.ReturnsSequence(1, 2, 3);

        builder.Returns(42);

        slot.HasConfiguredSequence.Should().BeFalse();
        slot.HasConfiguredValue.Should().BeTrue();
        slot.ConfiguredValue.Should().Be(42);
    }

    [Fact]
    public void Throws_AfterReturnsSequence_ClearsSequenceState()
    {
        var slot = new ReturnConfig<int>();
        var builder = new ReturnConfigBuilder<int>(ref slot);
        builder.ReturnsSequence(1, 2, 3);

        builder.Throws(new InvalidOperationException("boom"));

        slot.HasConfiguredSequence.Should().BeFalse();
        slot.HasConfiguredException.Should().BeTrue();
    }

    [Fact]
    public void ReturnsSequence_AfterReturns_ClearsPriorSingleValue()
    {
        var slot = new ReturnConfig<int>();
        var builder = new ReturnConfigBuilder<int>(ref slot);
        builder.Returns(42);

        builder.ReturnsSequence(1, 2, 3);

        slot.HasConfiguredValue.Should().BeFalse();
        slot.HasConfiguredSequence.Should().BeTrue();
        slot.NextSequenceOutcome().Should().Be(1);
    }

    [Fact]
    public void ReturnsSequence_EmptyArray_ThrowsArgumentException()
    {
        var slot = new ReturnConfig<int>();
        var builder = new ReturnConfigBuilder<int>(ref slot);
        ArgumentException? thrown = null;

        try { builder.ReturnsSequence(); }
        catch (ArgumentException ex) { thrown = ex; }

        thrown.Should().NotBeNull();
    }

    [Fact]
    public void RecordCall_IsIndependentOfSequenceConsumption_EvenWhenSequenceThrows()
    {
        var slot = new ReturnConfig<int>();
        new ReturnConfigBuilder<int>(ref slot).ReturnsSequence(
            SequenceOutcome.Throw(new InvalidOperationException("fails")),
            SequenceOutcome.Throw(new InvalidOperationException("fails")),
            42);

        for (var i = 0; i < 3; i++)
        {
            slot.RecordCall();
            try { slot.NextSequenceOutcome(); }
            catch (InvalidOperationException) { /* expected for the first two calls */ }
        }

        slot.ConfiguredCallCount.Should().Be(3);
    }

    [Fact]
    public void NextSequenceOutcome_ConcurrentConsumption_EveryOrdinalClaimedExactlyOnce()
    {
        const int sequenceLength = 500;
        var outcomes = Enumerable.Range(0, sequenceLength)
            .Select(i => (SequenceOutcome<int>)i)
            .ToArray();
        var slot = new ReturnConfig<int>();
        new ReturnConfigBuilder<int>(ref slot).ReturnsSequence(outcomes);

        var results = new int[sequenceLength];
        Parallel.For(0, sequenceLength, _ =>
        {
            var value = slot.NextSequenceOutcome();
            // Each concurrently-consumed outcome is recorded at its OWN value's index - a corrupted
            // ordinal (two threads claiming the same index, or an index skipped/repeated
            // unexpectedly) would show up as a lost or duplicated write here.
            Interlocked.Increment(ref results[value]);
        });

        results.Should().OnlyContain(count => count == 1);
    }

    [Fact]
    public void ReturnsSequence_TExactlyException_ValueConversionAndThrowBothResolveUnambiguously()
    {
        var slot = new ReturnConfig<Exception>();
        var valueOutcome = new ArgumentNullException("configured as a VALUE, not thrown");
        var thrownException = new InvalidOperationException("configured via SequenceOutcome.Throw");
        new ReturnConfigBuilder<Exception>(ref slot).ReturnsSequence(valueOutcome, SequenceOutcome.Throw(thrownException));

        // T-conversion: returned as an ordinary value, never thrown.
        slot.NextSequenceOutcome().Should().BeSameAs(valueOutcome);

        // SequenceOutcome.Throw: thrown, unambiguously distinct from the value case above.
        var act = () => slot.NextSequenceOutcome();
        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(thrownException);
    }

    [Fact]
    public void ReturnsSequence_TIsInvalidOperationException_ValueConversionResolvesAsValueNotThrow()
    {
        var slot = new ReturnConfig<InvalidOperationException>();
        var valueOutcome = new InvalidOperationException("configured as a value");
        new ReturnConfigBuilder<InvalidOperationException>(ref slot).ReturnsSequence(valueOutcome);

        slot.NextSequenceOutcome().Should().BeSameAs(valueOutcome);
    }

    [Fact]
    public void ReturnsSequence_TIsObject_ThrowStillResolvesAsThrowAndValueConversionStillWorks()
    {
        var slot = new ReturnConfig<object>();
        var thrown = new InvalidOperationException("thrown for T=object");
        new ReturnConfigBuilder<object>(ref slot).ReturnsSequence(SequenceOutcome.Throw(thrown), "a plain object value");

        var act = () => slot.NextSequenceOutcome();
        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(thrown);

        slot.NextSequenceOutcome().Should().Be("a plain object value");
    }

    [Fact]
    public void ReturnsSequence_TIsNullableException_NullValueViaTConversionResolvesAsNullNotThrow()
    {
        var slot = new ReturnConfig<Exception?>();
        new ReturnConfigBuilder<Exception?>(ref slot).ReturnsSequence((Exception?)null);

        slot.NextSequenceOutcome().Should().BeNull();
    }

    [Fact]
    public void ReturnsSequence_ReferenceTypeNullViaTConversion_ResolvesAsNull()
    {
        var slot = new ReturnConfig<string?>();
        new ReturnConfigBuilder<string?>(ref slot).ReturnsSequence((string?)null, "second");

        slot.NextSequenceOutcome().Should().BeNull();
        slot.NextSequenceOutcome().Should().Be("second");
    }

    [Fact]
    public void ThrownOutcome_DefaultValue_ConversionToSequenceOutcomeThrowsArgumentException()
    {
        var defaultThrown = default(SequenceOutcome.ThrownOutcome);

        var act = () =>
        {
            SequenceOutcome<int> outcome = defaultThrown;
            return outcome;
        };

        act.Should().Throw<ArgumentException>();
    }
}
