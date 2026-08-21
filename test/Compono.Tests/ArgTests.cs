namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="Arg{T}"/>'s three matcher kinds (literal equality, <see cref="Arg.Any{T}"/>,
/// <see cref="Arg.Is{T}"/>) and <see cref="Arg{T}.Matches"/>, matching ADR-0048's Decision Outcome.
/// </summary>
public sealed class ArgTests
{
    [Fact]
    public void Literal_Matches_WhenValueEqual()
    {
        Arg<string> matcher = "hello";

        matcher.Matches("hello").Should().BeTrue();
    }

    [Fact]
    public void Literal_DoesNotMatch_WhenValueDiffers()
    {
        Arg<string> matcher = "hello";

        matcher.Matches("goodbye").Should().BeFalse();
    }

    [Fact]
    public void Literal_UsesDefaultEqualityComparer_NotReferenceEquality()
    {
        Arg<string> matcher = new string(['h', 'e', 'l', 'l', 'o']);

        matcher.Matches("hello").Should().BeTrue();
    }

    [Fact]
    public void Any_MatchesAnyValue()
    {
        var matcher = Arg.Any<int>();

        matcher.Matches(0).Should().BeTrue();
        matcher.Matches(-1).Should().BeTrue();
        matcher.Matches(int.MaxValue).Should().BeTrue();
    }

    [Fact]
    public void Is_Matches_WhenPredicateReturnsTrue()
    {
        var matcher = Arg.Is<int>(value => value > 0);

        matcher.Matches(5).Should().BeTrue();
    }

    [Fact]
    public void Is_DoesNotMatch_WhenPredicateReturnsFalse()
    {
        var matcher = Arg.Is<int>(value => value > 0);

        matcher.Matches(-5).Should().BeFalse();
    }

    [Fact]
    public void Is_ReceivesTheRealValue_NotAStaleCapture()
    {
        var seen = new List<int>();
        var matcher = Arg.Is<int>(value =>
        {
            seen.Add(value);
            return true;
        });

        matcher.Matches(1);
        matcher.Matches(2);

        seen.Should().Equal(1, 2);
    }
}
