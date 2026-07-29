namespace Compono.Tests;

public sealed class CompositionExceptionTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDiagnosticIsNull()
    {
        // Regression for a real gap (PR #13 review): the base(diagnostic.Message) initializer
        // dereferences diagnostic before this constructor's own body ever runs, so an unguarded
        // null argument surfaced as NullReferenceException instead of the expected
        // ArgumentNullException every other guarded constructor in this codebase throws.
        var act = () => new CompositionException(diagnostic: null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
