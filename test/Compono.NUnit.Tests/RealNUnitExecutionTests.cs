using System.Collections.Concurrent;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Compono.NUnit.Tests;

/// <summary>
/// Real, end-to-end NUnit discovery/execution coverage - unlike every other test class in this
/// project (which calls <c>ComposeAttribute.BuildFrom</c> directly), the classes below are
/// deliberately real, undecorated (no <c>[TestFixture]</c>) NUnit test classes, actually discovered
/// and run by this project's own real NUnit test host (see this project's <c>.csproj</c> - it runs
/// under both classic VSTest and MTP, matching ADR-0059 §11). Proves, by real execution rather than
/// assertion, the two most consumer-visible behavioral claims ADR-0059 makes: <c>[Compose]</c> alone
/// (no <c>[TestFixture]</c>) makes a class's methods real NUnit tests (§7), and <c>[Compose]</c>
/// coexists with NUnit's own <c>[TestCase]</c>/<c>[Values]</c>/<c>[Range]</c>/a custom
/// <see cref="global::NUnit.Framework.Interfaces.IParameterDataSource"/> as independent, non-merging rows
/// (§8).
/// </summary>
// Deliberately no [TestFixture] here - this class's own discoverability (via its [Compose]-only
// method below) IS the proof ADR-0059 §7 requires.
public class NoTestFixtureRequiredTests
{
    // No [TestFixture] on the containing class, and this class itself carries no other
    // test-identifying attribute - if this method is not discovered and does not pass, ADR-0059 §7's
    // central claim is false. A composed int/string pair is all that's asserted: the goal is
    // discovery/execution, not composition correctness (already covered exhaustively by
    // ComposeAttributeBindingTests).
    [Compose]
    public void ComposedMethod_IsDiscoveredAndRuns_WithNoTestFixtureAttribute(int number, string text)
    {
        Assert.That(text, Is.Not.Null);
    }
}

/// <summary>
/// Regression-locks ADR-0059 §8's independent-row coexistence contract: <c>[Compose]</c> always
/// contributes exactly one additional row on top of whatever NUnit's own parameter-level data
/// source(s) independently produce, never merged, never dropped. Each test method below records
/// every row's actual parameter value into a method-specific bag; <see cref="AssertIndependentRowCoexistence"/>
/// (an <c>[OneTimeTearDown]</c>, guaranteed by NUnit to run once after every row in this fixture has
/// executed) asserts the exact expected row count and membership per method - not merely that "a"
/// test passed, which the pre-Pi-review version of this file only proved. Set-membership assertions
/// (<c>Does.Contain</c>) rather than exact per-value counts deliberately tolerate the astronomically
/// unlikely case of Compose's own random <c>int</c> colliding with one of NUnit's literal values -
/// the row-count assertion alone still catches a real regression (a missing or an extra row) in that
/// case.
/// </summary>
[TestFixture]
public sealed class DataSourceCoexistenceTests
{
    private static readonly ConcurrentBag<int> TestCaseRowValues = new();
    private static readonly ConcurrentBag<int> ValuesRowValues = new();
    private static readonly ConcurrentBag<int> RangeRowValues = new();
    private static readonly ConcurrentBag<int> CustomSourceRowValues = new();

    // [Compose] and NUnit's own [TestCase] each independently and completely own their own row -
    // ADR-0059 §8, ADR-0059 §18 Option 1 (independent, non-merging rows, chosen). Expected: 1
    // Compose-owned row (any int) + 1 literal [TestCase(42)] row = 2 total.
    [Compose]
    [TestCase(42)]
    public void ComposedAndTestCase_ProduceIndependentRows(int value) => TestCaseRowValues.Add(value);

    // [Compose] alone drives its own row; NUnit's own [Values] independently drives its own
    // additional row(s) on top - never merged into the Compose row, and not "unused" (a corrected
    // finding from an earlier draft's inaccurate assumption; see ADR-0059 §8). Expected: 1
    // Compose-owned row + 3 [Values(7,8,9)]-owned rows = 4 total.
    [Compose]
    public void ComposedAndValues_ProduceIndependentRows([Values(7, 8, 9)] int value) => ValuesRowValues.Add(value);

    // Same contract for [Range], NUnit's other built-in IParameterDataSource shape - not spiked
    // separately from [Values] pre-acceptance, added here per explicit post-acceptance review
    // feedback. Expected: 1 Compose-owned row + 3 [Range(1,3)]-owned rows (1, 2, 3) = 4 total.
    [Compose]
    public void ComposedAndRange_ProduceIndependentRows([Range(1, 3)] int value) => RangeRowValues.Add(value);

    // Same independent-row model for a custom IParameterDataSource, not just NUnit's own built-in
    // [Values]/[Range] - ADR-0059 §8's own "at least one representative custom source" evidence
    // requirement. Expected: 1 Compose-owned row + 3 custom-source-owned rows (100, 200, 300) = 4
    // total.
    [Compose]
    public void ComposedAndCustomParameterDataSource_ProduceIndependentRows([CustomThreeValues] int value) =>
        CustomSourceRowValues.Add(value);

    [OneTimeTearDown]
    public void AssertIndependentRowCoexistence()
    {
        Assert.That(TestCaseRowValues, Has.Count.EqualTo(2),
            "expected exactly 2 independent rows for ComposedAndTestCase_ProduceIndependentRows: 1 Compose-owned + 1 NUnit [TestCase]-owned");
        Assert.That(TestCaseRowValues, Does.Contain(42),
            "the literal [TestCase(42)] row must have executed independently of the Compose row");

        Assert.That(ValuesRowValues, Has.Count.EqualTo(4),
            "expected exactly 4 independent rows for ComposedAndValues_ProduceIndependentRows: 1 Compose-owned + 3 NUnit [Values]-owned");
        Assert.That(ValuesRowValues, Does.Contain(7).And.Contain(8).And.Contain(9),
            "all three [Values(7,8,9)] rows must have executed independently of the Compose row");

        Assert.That(RangeRowValues, Has.Count.EqualTo(4),
            "expected exactly 4 independent rows for ComposedAndRange_ProduceIndependentRows: 1 Compose-owned + 3 NUnit [Range]-owned");
        Assert.That(RangeRowValues, Does.Contain(1).And.Contain(2).And.Contain(3),
            "all three [Range(1,3)] rows must have executed independently of the Compose row");

        Assert.That(CustomSourceRowValues, Has.Count.EqualTo(4),
            "expected exactly 4 independent rows for ComposedAndCustomParameterDataSource_ProduceIndependentRows: 1 Compose-owned + 3 custom IParameterDataSource-owned");
        Assert.That(CustomSourceRowValues, Does.Contain(100).And.Contain(200).And.Contain(300),
            "all three custom IParameterDataSource rows must have executed independently of the Compose row");
    }
}

/// <summary>
/// A minimal custom <see cref="global::NUnit.Framework.Interfaces.IParameterDataSource"/> - proves
/// <c>[Compose]</c>'s independent-row coexistence isn't specific to NUnit's own built-in
/// <c>[Values]</c>/<c>[Range]</c> attributes (ADR-0059 §8).
/// </summary>
public sealed class CustomThreeValuesAttribute : Attribute, IParameterDataSource
{
    public System.Collections.IEnumerable GetData(IParameterInfo parameter) => new object[] { 100, 200, 300 };
}
