using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0048: a non-overloaded, non-generic member with real parameters - the shape ADR-0048 scopes
// argument-aware Configure()/Verify() to. IAccountRepository.Withdraw mixes a literal, Match.Any, and
// Match.Is in one call (the real trivia-manager shape), and is deliberately single-parameter-eligible
// via Rename to prove the one-parameter call-log special case (a plain List<T>, not a one-element
// tuple - "(T)" isn't a tuple type in C#) works too.
public interface IAccountRepository
{
    bool Withdraw(string accountId, decimal amount, bool overdraftAllowed);

    void Rename(string accountId);
}

// Codex review, PR #106, Finding 1: a non-overloaded generic method with no parameter referencing its
// own type parameter is both configuration-surfaced (ADR-0044 Requirement 2) and eligible-for-matching
// (ADR-0048) at once - the explicit interface implementation needs its generic suffix restated
// (void IFoo.Log<T>(...), not void IFoo.Log(...)) even on the new eligible-member codegen path, or the
// generated double doesn't implement the interface at all (CS0535). The Configure()/Verify() extension
// itself stays non-generic either way (ADR-0044 Requirement 2 - the slot's T is concrete regardless of
// what the real call site closes the method's own type parameter to).
public interface IUntypedLogger
{
    void Log<T>(string message);
}

// Codex review, PR #106, Finding 2: a ref-like (Span<T>-shaped) parameter is an ordinary, legal
// by-value method parameter - v1/v2's argument-independent path handles it fine (the value is
// discarded, never stored) - but can never be used as a generic type argument (Match<ReadOnlySpan<char>>?,
// or as an element of the call-log tuple), CS0306. Must be excluded from eligibility, not merely
// diagnosed, falling back to the member's existing zero-argument argument-independent Configure()/
// Verify() shape.
public interface ISpanRepository
{
    bool TryParse(ReadOnlySpan<char> value);
}

// Codex review, PR #106, Findings 4-7: real parameter names chosen specifically to land on every
// synthetic identifier ADR-0048's eligible-member codegen introduces - the receiver ("self"), the
// dispatch-body locals ("__matches", the per-parameter matcher pattern variable "__m_x" for a
// sibling parameter literally named "x"), and the verification-extension locals ("__count", the
// foreach loop variable "call"). Every one of these must be allocated collision-safely against the
// member's own real parameter names, or the generated code simply doesn't compile.
public interface ICollisionProneRepository
{
    bool Check(string self);

    bool Probe(int __matches, int __count, int call);

    void ProbeMatcherLocalCollision(int x, int __m_x);
}

// Codex review, PR #106 (round 2), Finding A: two independently-unremarkable members can derive the
// exact same auxiliary field name without either colliding with anything reserved so far in a single
// linear pass - Foo's parameter "x_calls" derives matcher field "__Foo_m_x_calls"; the sibling member
// "Foo_m_x" derives its own call-log field "__Foo_m_x_calls" from its own FieldName. Both must fall
// back to their existing argument-independent path (neither becomes eligible for matching) rather
// than the generated class failing to compile with a duplicate-member error.
public interface IAuxiliaryNameCollisionRepository
{
    bool Foo(int x_calls);

    bool Foo_m_x(int z);
}

// Codex review, PR #106 (round 2), Finding B: a non-overloaded member literally named "Equals" with
// one real parameter would, if made eligible, emit a Match<T>-typed extension with the same call-site
// arity as the inherited object.Equals(object) instance method - which C# always prefers over an
// extension method regardless of conversion cost, making the generated extension unreachable. Must
// stay on the existing argument-independent path instead.
public interface IEqualsRepository
{
    bool Equals(string other);
}

public sealed class MatchingTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configure_WithMixedLiteralAnyAndIsMatchers_OnlyRespondsWhenAllMatch(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Is<bool>(allowed => allowed))
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
    public void Verify_WithMatchIsFilter_CountsOnlyMatchingCalls(
        [Shared] IAccountRepository repository)
    {
        repository.Configure().Withdraw().Returns(true);

        repository.Withdraw("acct-1", 10m, overdraftAllowed: false);
        repository.Withdraw("acct-2", 10m, overdraftAllowed: false);
        repository.Withdraw("acct-1", 20m, overdraftAllowed: false);

        repository.Verify()
            .Withdraw(Compono.Match.Is<string>(id => id == "acct-1"), Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Exactly(2);
        repository.Verify()
            .Withdraw(Compono.Match.Is<string>(id => id == "acct-2"), Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
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

        repository.Verify().Rename(Compono.Match.Is<string>(id => id == "acct-1")).Once();
        repository.Verify().Rename().Exactly(2);
    }

    // Codex review, PR #106, Finding 3: a second Configure() call through the zero-argument
    // compatibility overload must actually clear a matcher an earlier call configured, not merely
    // leave it in place - otherwise "the second Configure() call overwrites" (PLAN-0048 Task 4) is
    // only true when both calls go through the same overload. Configured with a matcher that would
    // NOT match the real call below - if the matcher weren't cleared, dispatch would fall through to
    // bool's deterministic default (false), which is indistinguishable from a correctly-configured
    // false. Configuring true here, distinct from the default, makes an unfixed regression fail loudly
    // rather than by coincidence.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ZeroArgumentConfigure_ClearsAnEarlierMatcher_RespondingArgumentIndependentlyAfter(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw(Compono.Match.Is<string>(id => id == "acct-1"), Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(false);

        repository.Configure().Withdraw().Returns(true);

        repository.Withdraw("totally-different-account", 1m, overdraftAllowed: true).Should().BeTrue();
    }
}

public sealed class GenericMatchingTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void GenericMethod_WithNoParameterReferencingOwnTypeParameter_IsEligibleAndCompiles(
        [Shared] IUntypedLogger repository)
    {
        repository.Configure().Log(Compono.Match.Is<string>(m => m == "expected")).Throws(new InvalidOperationException());

        var act = () => repository.Log<int>("expected");

        act.Should().Throw<InvalidOperationException>();
        repository.Verify().Log(Compono.Match.Any<string>()).Once();
    }
}

public sealed class RefLikeParameterTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void RefLikeParameter_FallsBackToTheArgumentIndependentPath_AndCompiles(
        [Shared] ISpanRepository repository)
    {
        repository.Configure().TryParse().Returns(true);

        repository.TryParse("hello".AsSpan()).Should().BeTrue();
    }
}

public sealed class AuxiliaryNameCollisionTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void MembersWithCollidingDerivedNames_BothFallBackAndCompile(
        [Shared] IAuxiliaryNameCollisionRepository repository)
    {
        repository.Configure().Foo().Returns(true);
        repository.Configure().Foo_m_x().Returns(false);

        repository.Foo(x_calls: 1).Should().BeTrue();
        repository.Foo_m_x(z: 2).Should().BeFalse();
    }
}

public sealed class ObjectMemberCollisionTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void EqualsWithOneParameter_StaysArgumentIndependent_AndCompiles(
        [Shared] IEqualsRepository repository)
    {
        repository.Configure().Equals().Returns(true);

        repository.Equals("anything").Should().BeTrue();
    }
}

public sealed class CollisionProneNameTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ParameterNamedSelf_CompilesAndDispatchesThroughTheCollisionSafeReceiver(
        [Shared] ICollisionProneRepository repository)
    {
        repository.Configure().Check(Compono.Match.Any<string>()).Returns(true);

        repository.Check("anything").Should().BeTrue();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ParametersNamedAfterGeneratedDispatchLocals_CompileAndDispatchCorrectly(
        [Shared] ICollisionProneRepository repository)
    {
        repository.Configure()
            .Probe(Compono.Match.Any<int>(), Compono.Match.Is<int>(v => v == 2), Compono.Match.Any<int>())
            .Returns(true);

        repository.Probe(1, 2, 3).Should().BeTrue();
        repository.Probe(1, 99, 3).Should().BeFalse();
        repository.Verify().Probe(Compono.Match.Any<int>(), Compono.Match.Is<int>(v => v == 2), Compono.Match.Any<int>()).Once();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ParametersNamedAfterGeneratedMatcherPatternVariable_CompileAndDispatchCorrectly(
        [Shared] ICollisionProneRepository repository)
    {
        repository.ProbeMatcherLocalCollision(1, 2);
        repository.ProbeMatcherLocalCollision(1, 3);

        repository.Verify().ProbeMatcherLocalCollision(Compono.Match.Any<int>(), Compono.Match.Is<int>(v => v == 2)).Once();
    }
}
