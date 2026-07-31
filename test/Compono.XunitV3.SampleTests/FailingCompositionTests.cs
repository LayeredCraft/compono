namespace Compono.XunitV3.SampleTests;

// Deliberately fails, on every run, via a genuine (pipeline-propagated) composition failure - not one
// of Compono.XunitV3's own pre-composition validation failures (a negative seed, a signature error, an
// inline-value mismatch), which don't exercise real composition at all (PR #26 review). Uses an
// explicit seed so Compono.XunitV3.Tests' RealRunnerTests can assert on a deterministic value: it
// shells out `dotnet test` against this project and asserts the captured output contains this exact
// seed, proving the milestone's "a composition failure's message contains a seed that reproduces the
// same failure" promise reaches a real xUnit v3 runner's actual output, not just an in-process GetData
// call.
public sealed class FailingCompositionTests
{
    public const int Seed = 24601;

    [Theory]
    [Compose(Seed = Seed)]
    public void DeliberatelyFailingComposition_NoProviderCanSatisfyTheNestedInterfaceDependency(GatewayConsumer consumer)
    {
        consumer.Should().BeNull("GetData throws before this body ever runs - this line never executes");
    }
}
