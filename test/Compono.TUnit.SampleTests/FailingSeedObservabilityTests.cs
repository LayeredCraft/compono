using TUnit.Core.Interfaces;

namespace Compono.TUnit.SampleTests;

// Real, running-TUnit proof that Compono.TUnit's seed-reporting guarantee holds for a row that
// fails during the test body (a genuine test-execution failure), not just a composition failure -
// PR #73 review: Compono.TUnit.Tests' own negative-seed case throws before the StateBag write even
// happens, so it never exercised this path. [Explicit] keeps this deliberately-failing test out of a
// normal `dotnet test` run (same purpose as Compono.XunitV3.SampleTests' Failing*-named/filtered
// tests, via TUnit's own opt-in mechanism instead of a wildcarded CLI filter) while keeping it live,
// compiled, and runnable on demand.
public sealed class FailingSeedObservabilityTests
{
    [Test]
    [Explicit]
    [Compose(Seed = 24601)]
    public async Task RowFailsAfterAValidSeedWasRecorded(string value)
    {
        // Deliberately false - composition succeeds (a valid, non-negative seed, and `value` is a
        // real composed non-null string), but this assertion still fails. SeedIsStillReported below
        // proves Compono.Seed survives that test-body failure.
        await Assert.That(value).IsNull();
    }

    [After(HookType.Test)]
    public void SeedIsStillReported()
    {
        var context = TestContext.Current!;
        var result = ((ITestExecution)context).Result;

        if (result?.State != TestState.Failed)
        {
            throw new InvalidOperationException(
                $"Expected {nameof(RowFailsAfterAValidSeedWasRecorded)} to fail, but its result state " +
                $"was '{result?.State}' - this hook only proves anything if the test actually failed.");
        }

        var properties = context.Metadata.TestDetails.CustomProperties;
        if (!properties.TryGetValue("Compono.Seed", out var values)
            || !values.Contains("24601"))
        {
            throw new InvalidOperationException(
                "Compono.Seed was not reported as '24601' for a row that failed during the test body, " +
                "not composition.");
        }
    }
}
