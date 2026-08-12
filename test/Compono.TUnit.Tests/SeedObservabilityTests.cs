namespace Compono.TUnit.Tests;

/// <summary>
/// Real, direct verification of ADR-0040's seed-observability requirement - not an assumption that
/// <see cref="ComposeAttribute.OnTestDiscovered"/> was wired correctly, an actual running TUnit test
/// reading back what it reported. A pinned seed makes the expected property value known ahead of
/// time, so this asserts on the real value TUnit's own <c>TestDetails.CustomProperties</c> reports,
/// not merely that some property exists.
/// </summary>
/// <remarks>
/// Uses a built-in-composable parameter type (<see cref="string"/>), not a custom class - Phase 0's
/// unqualified <c>[Compose]</c> has no profile hook to register a rule for a custom type with (that's
/// <c>ComposeAttribute&lt;TProfile&gt;</c>'s Phase 1 <c>ApplyProfile</c> override), and this project
/// doesn't reference <c>Compono.Generators</c> as an analyzer (a plain <c>ProjectReference</c> doesn't
/// propagate the referenced project's own analyzer-only reference - only a packed nupkg's
/// <c>analyzers/dotnet/cs</c> delivery does), so no generated plan is available either. Matches
/// <c>Compono.XunitV3.Tests</c>' own established convention for exactly this constraint.
/// </remarks>
public sealed class SeedObservabilityTests
{
    [Test]
    [Compose(Seed = 42)]
    public async Task PassingRow_ReportsThePinnedSeedAsACustomProperty(string value)
    {
        await Assert.That(value).IsNotNull();

        var properties = TestContext.Current!.Metadata.TestDetails.CustomProperties;

        await Assert.That(properties.ContainsKey(ComposeAttribute.SeedPropertyName)).IsTrue();
        await Assert.That(properties[ComposeAttribute.SeedPropertyName]).Contains("42");
    }

    // Negative-seed case (PLAN-0040 Phase 0's "pass AND fail cases, investigate [Retry]" task) was
    // verified manually rather than left as a permanent [Test] here: a [Compose(Seed = -1)] row
    // throws CompositionException at data-generation time, before TestBuilderContext.Current.StateBag
    // is ever written, so the test is reported Failed by TUnit itself (not something the test body
    // can assert against) - confirmed by a real run:
    //
    //   failed NegativeSeed_ThrowsBeforeReportingAnyCustomProperty (4ms)
    //     CompositionException: Compono.TUnit requires a non-negative seed, but the configured
    //     seed was -1.
    //     Seed: -1
    //
    // matching Compono.XunitV3.ComposeAttribute.GetData's own ordering (the seed-negative check
    // throws before any row state - trait or StateBag - is ever reported). [Retry] would not help:
    // a negative seed is a deterministic configuration error, not a flaky-composition scenario.
    // A permanently-failing [Test] can't live in a suite that CI expects to stay green, so this is
    // recorded as a comment, not an assertion.
}
