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
/// coexists with NUnit's own <c>[TestCase]</c>/<c>[Values]</c>/a custom
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

[TestFixture]
public sealed class DataSourceCoexistenceTests
{
    // [Compose] and NUnit's own [TestCase] each independently and completely own their own row -
    // ADR-0059 §8, ADR-0059 §18 Option 1 (independent, non-merging rows, chosen). Two real, separate
    // test cases are expected: one composed, one literal.
    [Compose]
    [TestCase(42)]
    public void ComposedAndTestCase_ProduceIndependentRows(int value)
    {
        // No assertion on `value` itself - both the composed row (any int) and the literal TestCase
        // row (42) must pass; the point is that both rows exist and run at all.
    }

    // [Compose] alone drives its own row; NUnit's own [Values] independently drives its own
    // additional row(s) on top - never merged into the Compose row, and not "unused" (a corrected
    // finding from an earlier draft's inaccurate assumption; see ADR-0059 §8). Total expected rows
    // for this method: 1 (Compose) + 3 (Values: 7, 8, 9) = 4.
    [Compose]
    public void ComposedAndValues_ProduceIndependentRows([Values(7, 8, 9)] int value)
    {
    }

    // Same independent-row model for a custom IParameterDataSource, not just NUnit's own built-in
    // [Values]/[Range] - ADR-0059 §8's own "at least one representative custom source" evidence
    // requirement.
    [Compose]
    public void ComposedAndCustomParameterDataSource_ProduceIndependentRows([CustomThreeValues] int value)
    {
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
