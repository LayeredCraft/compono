namespace Compono.XunitV3.SampleTests;

// Deliberately fails, on every run, via a genuine (pipeline-propagated) composition failure - not one
// of Compono.XunitV3's own pre-composition validation failures (a negative seed, a signature error, an
// inline-value mismatch), which don't exercise real composition at all (PR #26 review). Uses an
// explicit seed so its output can be checked for a deterministic value, proving the milestone's "a
// composition failure's message contains a seed that reproduces the same failure" promise reaches a
// real xUnit v3 runner's actual output, not just an in-process GetData call. This project's CI
// "Local-feed packed-consumer smoke test" step (.github/workflows/package-validation.yaml) filters
// out every class whose name starts with "Failing" for exactly this reason - running this project
// with a bare `dotnet test` (no `--filter-not-class "Compono.XunitV3.SampleTests.Failing*"`) will
// report this class (and FailingConfigProfileTests) as failing; that is expected, not a broken
// project. See this project's own README.md for the full CI invocation.
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
