using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0063/ADR-0060: a mutable reference-type argument, used to prove ReceivedCalls()'s explicit
// no-deep-copy, reference-retention capture semantics - the retained record observes a mutation made
// to the argument object after the call returns.
public sealed class MutableRecord
{
    public int Value { get; set; }
}

public interface IArchiver
{
    void Archive(MutableRecord record);
}

// PLAN-0063/ADR-0060: a single-parameter, ADR-0048-eligible, non-void member used for the
// ClearCalls()-does-not-rewind-ReturnsSequence proof (ADR-0060's own worked example).
public interface ILedger
{
    string NextValue(string key);
}

public sealed class ReceivedCallsTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void SingleCall_ReturnsOneRecordWithTheRealArgumentValues(
        [Shared] IAccountRepository repository)
    {
        repository.Withdraw("acct-1", 50m, overdraftAllowed: true);

        var calls = repository.ReceivedCalls().Withdraw();

        calls.Should().HaveCount(1);
        calls[0].accountId.Should().Be("acct-1");
        calls[0].amount.Should().Be(50m);
        calls[0].overdraftAllowed.Should().BeTrue();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void MultipleCalls_PreservesAppendOrder(
        [Shared] IAccountRepository repository)
    {
        repository.Withdraw("acct-1", 10m, overdraftAllowed: false);
        repository.Withdraw("acct-2", 20m, overdraftAllowed: true);
        repository.Withdraw("acct-3", 30m, overdraftAllowed: false);

        var calls = repository.ReceivedCalls().Withdraw();

        calls.Should().HaveCount(3);
        calls[0].accountId.Should().Be("acct-1");
        calls[1].accountId.Should().Be("acct-2");
        calls[2].accountId.Should().Be("acct-3");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void SingleParameterMember_ReceivedCallsUsesTheBareParameterShape_NotATuple(
        [Shared] IAccountRepository repository)
    {
        repository.Rename("acct-1");

        var calls = repository.ReceivedCalls().Rename();

        calls.Should().ContainSingle().Which.accountId.Should().Be("acct-1");
    }

    // ADR-0060 snapshot semantics: a later invocation must never retroactively grow an
    // already-returned ReceivedCalls() snapshot.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void SnapshotIsolation_LaterInvocationDoesNotAffectAnEarlierSnapshot(
        [Shared] IAccountRepository repository)
    {
        repository.Withdraw("acct-1", 10m, overdraftAllowed: false);

        var firstSnapshot = repository.ReceivedCalls().Withdraw();
        repository.Withdraw("acct-2", 20m, overdraftAllowed: true);
        var secondSnapshot = repository.ReceivedCalls().Withdraw();

        firstSnapshot.Should().HaveCount(1, "the earlier snapshot must not observe a call made after it was taken");
        secondSnapshot.Should().HaveCount(2);
    }

    // ADR-0060 capture semantics: reference types are retained by reference, not deep-copied - a
    // mutation made to the argument object after the call returns IS observed by a later
    // ReceivedCalls() inspection. This is a documented, deliberate footgun, not a bug.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ReferenceTypeArgument_IsRetainedByReference_LaterMutationIsObserved(
        [Shared] IArchiver archiver)
    {
        var record = new MutableRecord { Value = 1 };
        archiver.Archive(record);

        record.Value = 2;

        var calls = archiver.ReceivedCalls().Archive();
        calls.Should().ContainSingle().Which.record.Value.Should().Be(2,
            "ReceivedCalls() stores the same reference the caller passed, per ADR-0060 - no deep copy");
    }

    // ADR-0060 capture semantics: value-type arguments are ordinary C# value copies - mutating the
    // caller's own local after the call has no effect on the already-captured value.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ValueTypeArgument_IsCopiedAtCallTime_LaterLocalMutationIsNotObserved(
        [Shared] IAccountRepository repository)
    {
        var amount = 10m;
        repository.Withdraw("acct-1", amount, overdraftAllowed: false);
        amount = 999m;

        var calls = repository.ReceivedCalls().Withdraw();

        calls.Should().ContainSingle().Which.amount.Should().Be(10m);
    }
}

public sealed class ClearCallsTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ClearCalls_ResetsCallCountAndReceivedCalls(
        [Shared] IAccountRepository repository)
    {
        repository.Withdraw("acct-1", 10m, overdraftAllowed: false);
        repository.Withdraw("acct-2", 20m, overdraftAllowed: true);

        repository.ClearCalls();

        repository.Verify().Withdraw().Never();
        repository.ReceivedCalls().Withdraw().Should().BeEmpty();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ClearCalls_PreservesConfiguredReturnValue(
        [Shared] IAccountRepository repository)
    {
        repository.Configure().Withdraw().Returns(true);
        repository.Withdraw("acct-1", 10m, overdraftAllowed: false);

        repository.ClearCalls();

        repository.Withdraw("acct-2", 20m, overdraftAllowed: false).Should().BeTrue(
            "ClearCalls() must preserve configured behavior - only observation history is reset");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ClearCalls_PreservesMultiEntryConfiguration(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(true);
        repository.Configure()
            .Withdraw("acct-2", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(false);
        repository.Withdraw("acct-1", 1m, overdraftAllowed: false);

        repository.ClearCalls();

        repository.Withdraw("acct-1", 1m, overdraftAllowed: false).Should().BeTrue();
        repository.Withdraw("acct-2", 1m, overdraftAllowed: false).Should().BeFalse();
        repository.Verify()
            .Withdraw(Compono.Match.Is<string>(id => id == "acct-1"), Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Once();
    }

    // ADR-0060's own worked example: ReturnsSequence(A, B, C), two calls consume A then B,
    // ClearCalls() runs, the next call must return C - not rewind to A.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ClearCalls_DoesNotRewindAConfiguredSequence(
        [Shared] ILedger ledger)
    {
        ledger.Configure().NextValue().ReturnsSequence("A", "B", "C");

        ledger.NextValue("key").Should().Be("A");
        ledger.NextValue("key").Should().Be("B");

        ledger.ClearCalls();

        ledger.NextValue("key").Should().Be("C",
            "SequenceOrdinal is configured-behavior progress, not observation history - ClearCalls() must never rewind it");
        // ClearCalls() reset the call count even though the sequence itself did not rewind - only
        // the third (post-clear) call is observed.
        ledger.Verify().NextValue().Once();
    }

    // Deterministic concurrency proof: many concurrent invocations racing one ClearCalls() call must
    // never corrupt the call log or throw - each call either lands fully before or fully after the
    // clear, per ADR-0060's synchronization contract.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task ClearCalls_RacingConcurrentInvocations_NeverThrowsOrCorruptsState(
        [Shared] IAccountRepository repository)
    {
        const int iterations = 200;
        using var barrier = new Barrier(2);

        var cancellationToken = TestContext.Current.CancellationToken;

        var callerTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
                repository.Withdraw("acct-1", 1m, overdraftAllowed: false);
        }, cancellationToken);

        var clearerTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
                repository.ClearCalls();
        }, cancellationToken);

        var act = async () => await Task.WhenAll(callerTask, clearerTask);

        await act.Should().NotThrowAsync();

        // Whatever call count survives the race is a legal outcome (0..iterations) - the invariant
        // under test is "no exception, no torn state", not a specific final count.
        var finalCalls = repository.ReceivedCalls().Withdraw();
        finalCalls.Count.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(iterations);
    }
}
