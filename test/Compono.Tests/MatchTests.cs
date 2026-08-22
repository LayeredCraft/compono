using System.Reflection;

namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="Match{T}"/>'s three matcher kinds (literal equality, <see cref="Match.Any{T}"/>,
/// <see cref="Match.Is{T}"/>) and <see cref="Match{T}.Matches"/>, matching ADR-0048's Decision Outcome.
/// </summary>
public sealed class MatchTests
{
    [Fact]
    public void Literal_Matches_WhenValueEqual()
    {
        Match<string> matcher = "hello";

        matcher.Matches("hello").Should().BeTrue();
    }

    [Fact]
    public void Literal_DoesNotMatch_WhenValueDiffers()
    {
        Match<string> matcher = "hello";

        matcher.Matches("goodbye").Should().BeFalse();
    }

    [Fact]
    public void Literal_UsesDefaultEqualityComparer_NotReferenceEquality()
    {
        Match<string> matcher = new string(['h', 'e', 'l', 'l', 'o']);

        matcher.Matches("hello").Should().BeTrue();
    }

    [Fact]
    public void Literal_StoresNoPredicateDelegate()
    {
        Match<string> matcher = "hello";

        var predicateField = typeof(Match<string>).GetField("_predicate", BindingFlags.NonPublic | BindingFlags.Instance);
        predicateField!.GetValue(matcher).Should().BeNull();
    }

    [Fact]
    public void Any_MatchesAnyValue()
    {
        var matcher = Match.Any<int>();

        matcher.Matches(0).Should().BeTrue();
        matcher.Matches(-1).Should().BeTrue();
        matcher.Matches(int.MaxValue).Should().BeTrue();
    }

    [Fact]
    public void Any_StoresNoPredicateDelegate()
    {
        var matcher = Match.Any<int>();

        var predicateField = typeof(Match<int>).GetField("_predicate", BindingFlags.NonPublic | BindingFlags.Instance);
        predicateField!.GetValue(matcher).Should().BeNull();
    }

    [Fact]
    public void Is_Matches_WhenPredicateReturnsTrue()
    {
        var matcher = Match.Is<int>(value => value > 0);

        matcher.Matches(5).Should().BeTrue();
    }

    [Fact]
    public void Is_DoesNotMatch_WhenPredicateReturnsFalse()
    {
        var matcher = Match.Is<int>(value => value > 0);

        matcher.Matches(-5).Should().BeFalse();
    }

    [Fact]
    public void Is_ReceivesTheRealValue_NotAStaleCapture()
    {
        var seen = new List<int>();
        var matcher = Match.Is<int>(value =>
        {
            seen.Add(value);
            return true;
        });

        matcher.Matches(1);
        matcher.Matches(2);

        seen.Should().Equal(1, 2);
    }

    [Fact]
    public void Is_Throws_WhenPredicateIsNull()
    {
        var act = () => Match.Is<int>(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
