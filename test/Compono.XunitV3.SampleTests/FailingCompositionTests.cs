namespace Compono.XunitV3.SampleTests;

// Deliberately fails, on every run - Compono.XunitV3.Tests' RealRunnerTests shells out `dotnet test`
// against this project and asserts the captured output contains "Seed:", proving the milestone's
// "a composition failure's message contains a seed" promise reaches a real xUnit v3 runner's actual
// output, not just an in-process GetData call.
public sealed class FailingCompositionTests
{
    [Theory]
    [Compose(Seed = -1)]
    public void DeliberatelyFailingComposition_NegativeSeedIsRejected(int value)
    {
        value.Should().Be(0, "GetData throws before this body ever runs - this line never executes");
    }
}
