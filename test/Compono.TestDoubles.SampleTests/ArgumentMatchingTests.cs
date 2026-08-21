using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0048: a non-overloaded, non-generic member with real parameters - the shape ADR-0048 scopes
// argument-aware Configure()/Verify() to. IAccountRepository.Withdraw mixes a literal, Arg.Any, and
// Arg.Is in one call (the real trivia-manager shape), and is deliberately single-parameter-eligible
// via Rename to prove the one-parameter call-log special case (a plain List<T>, not a one-element
// tuple - "(T)" isn't a tuple type in C#) works too.
public interface IAccountRepository
{
    bool Withdraw(string accountId, decimal amount, bool overdraftAllowed);

    void Rename(string accountId);
}

public sealed class ArgumentMatchingTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configure_WithMixedLiteralAnyAndIsMatchers_OnlyRespondsWhenAllMatch(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw("acct-1", Compono.Arg.Any<decimal>(), Compono.Arg.Is<bool>(allowed => allowed))
            .Returns(true);

        var matchingCall = repository.Withdraw("acct-1", 50m, overdraftAllowed: true);
        var wrongAccount = repository.Withdraw("acct-2", 50m, overdraftAllowed: true);
        var wrongOverdraftFlag = repository.Withdraw("acct-1", 50m, overdraftAllowed: false);

        matchingCall.Should().BeTrue();
        wrongAccount.Should().BeFalse();
        wrongOverdraftFlag.Should().BeFalse();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configure_WithNoMatcherConfigured_RespondsArgumentIndependently(
        [Shared] IAccountRepository repository)
    {
        repository.Configure().Withdraw().Returns(true);

        repository.Withdraw("any-account", 999m, overdraftAllowed: false).Should().BeTrue();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Verify_WithArgIsFilter_CountsOnlyMatchingCalls(
        [Shared] IAccountRepository repository)
    {
        repository.Configure().Withdraw().Returns(true);

        repository.Withdraw("acct-1", 10m, overdraftAllowed: false);
        repository.Withdraw("acct-2", 10m, overdraftAllowed: false);
        repository.Withdraw("acct-1", 20m, overdraftAllowed: false);

        repository.Verify()
            .Withdraw(Compono.Arg.Is<string>(id => id == "acct-1"), Compono.Arg.Any<decimal>(), Compono.Arg.Any<bool>())
            .Exactly(2);
        repository.Verify()
            .Withdraw(Compono.Arg.Is<string>(id => id == "acct-2"), Compono.Arg.Any<decimal>(), Compono.Arg.Any<bool>())
            .Once();
        repository.Verify().Withdraw().Exactly(3);
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void SingleParameterMember_MatchesAndRecordsThroughAPlainCallLog_NotATuple(
        [Shared] IAccountRepository repository)
    {
        repository.Rename("acct-1");
        repository.Rename("acct-2");

        repository.Verify().Rename(Compono.Arg.Is<string>(id => id == "acct-1")).Once();
        repository.Verify().Rename().Exactly(2);
    }
}
