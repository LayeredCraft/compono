using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

// Real, actually-executed tests proving ADR-0057 §10's boundary under a real MSTest test host:
// [DataRow]/[DynamicData] and [Compose] are independent, complete-row data sources on the same
// method - they never merge into one row, each producing its own independent test case(s). No
// merging machinery is implemented anywhere in Compono.MSTest; this is a structural property of
// MSTest's own multi-ITestDataSource-attribute model, verified here rather than assumed - both
// [DataRow] and [DynamicData] specifically, not just one of the two ITestDataSource-family
// built-ins ADR-0057 §10 names.
[TestClass]
public sealed class DataRowCoexistenceTests
{
    // A static counter, not a per-case assertion inside the method body - what needs proving is
    // that MSTest runs *two* independent test cases for this one method (one supplied entirely by
    // [DataRow], one entirely by [Compose]), each with every parameter fully supplied by its own
    // data source - not that [Compose] "fills in" what [DataRow] didn't supply.
    private static int _dataRowCaseCount;
    private static int _composeCaseCount;

    // Same shape, for the [DynamicData] + [Compose] scenario - a separate pair of counters so the
    // two scenarios' ClassCleanup checks can't accidentally pass off each other's counts.
    private static int _dynamicDataCaseCount;
    private static int _dynamicDataComposeCaseCount;

    [ClassInitialize]
    public static void ResetCounters(TestContext context)
    {
        _dataRowCaseCount = 0;
        _composeCaseCount = 0;
        _dynamicDataCaseCount = 0;
        _dynamicDataComposeCaseCount = 0;
    }

    [TestMethod]
    [DataRow(42, "from-datarow")]
    [Compose]
    public void DataRowAndComposeProduceIndependentRows(int number, string text)
    {
        // [DataRow(42, "from-datarow")] supplies a complete row - Compono is never consulted for
        // this case. [Compose] supplies a complete, independently-composed row for the other case -
        // [DataRow]'s values never appear in it. Distinguishing which data source produced the
        // current row by value (42/"from-datarow" is [DataRow]'s exact, known row) is the only way
        // to prove this from inside the test body - there is no MSTest API surfacing "which
        // ITestDataSource produced this row" directly.
        if (number == 42 && text == "from-datarow")
        {
            _dataRowCaseCount++;
        }
        else
        {
            _composeCaseCount++;
            // A composed value is never required to differ from 42/"from-datarow" - but Compono's
            // own string/int leaf generation makes a collision astronomically unlikely across a
            // real test run, and this assertion only needs to hold for the one real seed this run
            // actually used, not for all possible seeds.
            Assert.IsNotNull(text);
        }
    }

    // [DynamicData]'s own source method - a distinct ITestDataSource implementation from
    // [DataRow], backed by DynamicDataAttribute rather than a literal attribute-constructor row.
    // Its own known, literal row (99, "from-dynamicdata") plays the identical distinguishing role
    // DataRowAndComposeProduceIndependentRows's (42, "from-datarow") does above.
    private static IEnumerable<object[]> GetDynamicRows()
    {
        yield return [99, "from-dynamicdata"];
    }

    [TestMethod]
    [DynamicData(nameof(GetDynamicRows))]
    [Compose]
    public void DynamicDataAndComposeProduceIndependentRows(int number, string text)
    {
        if (number == 99 && text == "from-dynamicdata")
        {
            _dynamicDataCaseCount++;
        }
        else
        {
            _dynamicDataComposeCaseCount++;
            Assert.IsNotNull(text);
        }
    }

    [ClassCleanup]
    public static void AssertBothRowsRanIndependently()
    {
        Assert.AreEqual(1, _dataRowCaseCount,
            "Expected exactly one test case produced entirely by [DataRow] - if this fails, either " +
            "[DataRow] didn't run at all, or its row was altered by [Compose] merging into it.");
        Assert.AreEqual(1, _composeCaseCount,
            "Expected exactly one test case produced entirely by [Compose] - if this fails, either " +
            "[Compose] didn't run at all, or MSTest only ran one of the two independent data sources " +
            "instead of both.");
        Assert.AreEqual(1, _dynamicDataCaseCount,
            "Expected exactly one test case produced entirely by [DynamicData] - if this fails, " +
            "either [DynamicData] didn't run at all, or its row was altered by [Compose] merging " +
            "into it.");
        Assert.AreEqual(1, _dynamicDataComposeCaseCount,
            "Expected exactly one test case produced entirely by [Compose] alongside [DynamicData] " +
            "- if this fails, either [Compose] didn't run at all, or MSTest only ran one of the two " +
            "independent data sources instead of both.");
    }
}
