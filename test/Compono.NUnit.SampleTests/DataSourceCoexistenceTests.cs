using System.Collections.Concurrent;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Compono.NUnit.SampleTests;

// PLAN-0059 task group 8's own requirement: the [TestCase]/[Values]/[Range]/custom-source
// independent-row coexistence scenario (ADR-0059 §8), run for real through the actual packaged
// Compono.NUnit -> Compono dependency chain - not test/Compono.NUnit.Tests' own ProjectReference-
// based execution. Mirrors that project's RealNUnitExecutionTests.cs assertions exactly.
[TestFixture]
public sealed class DataSourceCoexistenceTests
{
    private static readonly ConcurrentBag<int> TestCaseRowValues = new();
    private static readonly ConcurrentBag<int> ValuesRowValues = new();

    [Compose]
    [TestCase(42)]
    public void ComposedAndTestCase_ProduceIndependentRows(int value) => TestCaseRowValues.Add(value);

    [Compose]
    public void ComposedAndValues_ProduceIndependentRows([Values(7, 8, 9)] int value) => ValuesRowValues.Add(value);

    [OneTimeTearDown]
    public void AssertIndependentRowCoexistence()
    {
        Assert.That(TestCaseRowValues, Has.Count.EqualTo(2),
            "expected exactly 2 independent rows: 1 Compose-owned + 1 NUnit [TestCase]-owned");
        Assert.That(TestCaseRowValues, Does.Contain(42));

        Assert.That(ValuesRowValues, Has.Count.EqualTo(4),
            "expected exactly 4 independent rows: 1 Compose-owned + 3 NUnit [Values]-owned");
        Assert.That(ValuesRowValues, Does.Contain(7).And.Contain(8).And.Contain(9));
    }
}

// Deliberately no [TestFixture] here - discoverability via [Compose] alone (ADR-0059 §7) IS part of
// the proof, exercised for the custom-IParameterDataSource coexistence case specifically.
public class CustomParameterDataSourceCoexistenceTests
{
    private static readonly ConcurrentBag<int> CustomSourceRowValues = new();

    [Compose]
    public void ComposedAndCustomParameterDataSource_ProduceIndependentRows([CustomThreeValues] int value) =>
        CustomSourceRowValues.Add(value);

    [OneTimeTearDown]
    public void AssertIndependentRowCoexistence()
    {
        Assert.That(CustomSourceRowValues, Has.Count.EqualTo(4),
            "expected exactly 4 independent rows: 1 Compose-owned + 3 custom IParameterDataSource-owned");
        Assert.That(CustomSourceRowValues, Does.Contain(100).And.Contain(200).And.Contain(300));
    }
}

public sealed class CustomThreeValuesAttribute : Attribute, IParameterDataSource
{
    public System.Collections.IEnumerable GetData(IParameterInfo parameter) => new object[] { 100, 200, 300 };
}
