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

// Codex review, PR #108 (round 7): a matching-eligible candidate no longer emits a plain `__{Name}`
// field either (ADR-0050 replaced it with `__{Name}_Entry`/`__{Name}_entries`) - but the base-slot
// reservation pre-pass didn't know that yet, so a matching-eligible sibling literally named
// "Foo_calls" reserved the phantom "__Foo_calls" base slot, which then falsely collided with Foo's
// own real, actually-emitted "__Foo_calls" call-log field name, disabling matching for Foo even
// though nothing in the final generated layout actually collides.
public interface IBaseSlotCollisionRepository
{
    bool Foo(int x);

    bool Foo_calls(int z);
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

// Codex review, PR #106 (round 4): an eligible generic member's own type parameter is in scope in
// its dispatch body exactly like a real value parameter is - naming it "__matches" (or any of the
// other synthetic dispatch-local names, e.g. a "__m_x"-shaped name for a sibling parameter "x")
// collides with the generated local the same way a real value parameter's name would, but the
// reservation set only ever considered value-parameter names, not type-parameter names. CS0412.
public interface ITypeParameterCollisionRepository
{
    void Log<__matches>(string message);
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

public sealed class BaseSlotCollisionTests
{
    // Regression (Codex review, PR #108 round 7): both Foo and Foo_calls must be full matching-
    // eligible members (accepting a Match<int> and supporting multi-entry Configure()), not silently
    // downgraded to the argument-independent fallback by a phantom reservation collision.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void MembersWithNoRealCollision_BothStayMatchingEligible(
        [Shared] IBaseSlotCollisionRepository repository)
    {
        repository.Configure().Foo(Compono.Match.Is<int>(x => x == 1)).Returns(true);
        repository.Configure().Foo(Compono.Match.Is<int>(x => x == 2)).Returns(false);
        repository.Configure().Foo_calls(Compono.Match.Any<int>()).Returns(true);

        repository.Foo(1).Should().BeTrue();
        repository.Foo(2).Should().BeFalse();
        repository.Foo_calls(99).Should().BeTrue();
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

public sealed class TypeParameterCollisionTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void TypeParameterNamedAfterAGeneratedDispatchLocal_CompilesAndDispatchesCorrectly(
        [Shared] ITypeParameterCollisionRepository repository)
    {
        repository.Configure().Log(Compono.Match.Is<string>(m => m == "expected")).Throws(new InvalidOperationException());

        var act = () => repository.Log<int>("expected");

        act.Should().Throw<InvalidOperationException>();
        repository.Verify().Log(Compono.Match.Any<string>()).Once();
    }
}

// ADR-0050: multiple Configure() calls append entries instead of overwriting the member's one slot.
// Dispatch scans the ordered entry list in reverse registration order - the most recently registered
// matching entry wins; no match falls through to ADR-0045's existing default/throw behavior unchanged.
public sealed class MultiEntryTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void TwoDisjointLiteralEntries_EachRespondsWithItsOwnConfiguredValue(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(true);
        repository.Configure()
            .Withdraw("acct-2", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(false);

        repository.Withdraw("acct-1", 10m, overdraftAllowed: false).Should().BeTrue();
        repository.Withdraw("acct-2", 10m, overdraftAllowed: false).Should().BeFalse();
    }

    // A call matching neither registered entry falls through to the existing configuration-required/
    // deterministic-default behavior, unchanged by how many entries exist.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void CallMatchingNeitherEntry_FallsThroughToTheExistingDefaultBehavior(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(true);
        repository.Configure()
            .Withdraw("acct-2", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(false);

        repository.Withdraw("acct-3", 10m, overdraftAllowed: false).Should().BeFalse();
    }

    // Proves reverse-scan-last-*matching*-wins, not just "the last entry always wins regardless of
    // match": the broad Match.Any() entry is registered first, a narrower literal-argument entry is
    // registered second - the narrower entry wins for its own argument, but the broad entry still
    // answers everything else, since it's still the first match found scanning backward past the
    // non-matching narrow entry.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void SpecificLiteralEntryRegisteredAfterAMatchAnyEntry_WinsForItsOwnArgument_DefaultEntryHandlesTheRest(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw(Compono.Match.Any<string>(), Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(false);
        repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(true);

        repository.Withdraw("acct-1", 10m, overdraftAllowed: false).Should().BeTrue();
        repository.Withdraw("acct-9", 10m, overdraftAllowed: false).Should().BeFalse();
    }

    // The zero-argument Configure()/Verify() compatibility overloads: a single call configures an
    // always-matching entry; a repeated call appends another, most-recently-registered entry that wins
    // by recency (reproducing v1/v2's "second Configure() call overwrites" observable behavior without
    // mutating the first entry) - and Verify()'s call count reads the shared call log, unaffected by
    // how many response entries exist.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ZeroArgumentConfigure_RepeatedCall_MostRecentlyRegisteredEntryWinsByRecency(
        [Shared] IAccountRepository repository)
    {
        repository.Configure().Withdraw().Returns(false);
        repository.Configure().Withdraw().Returns(true);

        repository.Withdraw("any-account", 1m, overdraftAllowed: true).Should().BeTrue();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void ZeroArgumentVerify_CountsAllCallsRegardlessOfHowManyResponseEntriesExist(
        [Shared] IAccountRepository repository)
    {
        repository.Configure().Withdraw().Returns(false);
        repository.Configure().Withdraw().Returns(true);

        repository.Withdraw("acct-1", 1m, overdraftAllowed: true);
        repository.Withdraw("acct-2", 1m, overdraftAllowed: true);

        repository.Verify().Withdraw().Exactly(2);
    }

    // Regression (Codex review, PR #108 round 6): a newer matching entry that has been Configure()d
    // but never had .Returns()/.Throws() called on it - i.e. it matched, but has neither a
    // configured exception nor a configured value - must not shadow an older, fully-configured
    // matching entry. The reverse scan must keep going past it rather than stopping and falling
    // through to the default/required-configuration rule.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void NewerMatchingEntryWithNoConfiguredResponse_DoesNotShadowAnOlderConfiguredEntry(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>())
            .Returns(true);
        // Retains the builder without ever calling .Returns()/.Throws() on it - this entry matches
        // "acct-1" but has no configured response of its own.
        _ = repository.Configure()
            .Withdraw("acct-1", Compono.Match.Any<decimal>(), Compono.Match.Any<bool>());

        repository.Withdraw("acct-1", 10m, overdraftAllowed: false).Should().BeTrue();
    }

    // Regression (Codex review, PR #108 round 7): a void member's ReturnConfig<Unit> DOES have a
    // genuine "configured" state distinct from "incomplete" - .Returns(default) sets
    // HasConfiguredValue even though there's nothing to return. A newer matching entry configured
    // this way must win over (i.e. stop the scan before reaching) an older matching entry configured
    // to throw - the void dispatch loop must treat "HasConfiguredValue" as a stop condition exactly
    // like the value-returning branch does, not as equivalent to an unconfigured/incomplete entry.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void NewerConfiguredVoidEntry_WinsOverAnOlderThrowingEntry(
        [Shared] IAccountRepository repository)
    {
        repository.Configure()
            .Rename(Compono.Match.Any<string>())
            .Throws(new InvalidOperationException("should not be reached - the newer entry should win"));
        repository.Configure()
            .Rename(Compono.Match.Any<string>())
            .Returns(default);

        var act = () => repository.Rename("acct-1");

        act.Should().NotThrow();
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
