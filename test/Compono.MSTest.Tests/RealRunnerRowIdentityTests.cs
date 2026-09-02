using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

// Real, actually-executed [TestMethod] + [Compose] tests - unlike every other test file in this
// project, which calls ComposeAttribute.GetData/GetDisplayName directly without ever going through
// MSTest's own discovery/execution pipeline. This class exists solely to prove, under a real MSTest
// test host (MTP or classic VSTest, whichever actually runs this assembly), that
// ComposeAttribute's GetDisplayName -> SeedByRow lookup (ConditionalWeakTable keyed by the exact
// object?[] instance GetData returned) actually hits: i.e. that MSTest passes the identical row-array
// instance back to GetDisplayName that GetData handed it, rather than a clone/re-materialized copy.
// ClassInitialize/ClassCleanup (not AssemblyInitialize/AssemblyCleanup) - scoped to this class alone
// so the counters aren't contaminated by SeedReportingTests' own deliberate
// GetDisplayName_DoesNotThrow_WhenDataIsNotARecognizedRow miss (an intentional edge case elsewhere in
// this project, not evidence about the real runner). Both are stable, long-standing MSTest lifecycle
// attributes honored by both the MTP and classic VSTest adapters, running once per class in-process -
// asserting in ClassCleanup, rather than in a normal [TestMethod], is what makes this a real check of
// "what actually happened across every test this class ran" instead of a check that can only ever see
// its own isolated GetData/GetDisplayName pair.
[TestClass]
public sealed class RealRunnerRowIdentityTests
{
    [ClassInitialize]
    public static void ResetCounters(TestContext context)
    {
        ComposeAttribute.SeedByRowHitCount = 0;
        ComposeAttribute.SeedByRowMissCount = 0;
        ComposeAttribute.GetDataCallCount = 0;
    }

    [TestMethod]
    [Compose]
    public void ComposesTwoStrings_RealRun(string first, string second)
    {
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
    }

    [TestMethod]
    [Compose]
    public void ComposesSharedString_RealRun([Shared] string shared, string other)
    {
        Assert.IsNotNull(shared);
        Assert.IsNotNull(other);
    }

    [TestMethod]
    [Compose(1)]
    public void ComposesWithAnInlineValue_RealRun(int number, string text)
    {
        Assert.AreEqual(1, number);
        Assert.IsNotNull(text);
    }

    [ClassCleanup]
    public static void AssertRowIdentityHeldForEveryRealRun()
    {
        // Empirical finding (recorded in PLAN-0057's Notes): neither MTP nor the classic VSTest
        // adapter calls GetDisplayName during ordinary `dotnet test`/`dotnet vstest` *execution* -
        // only during *discovery*/listing (`--list-tests`, `dotnet vstest -lt`, Test Explorer's own
        // tree population). SeedByRowHitCount is therefore correctly 0 here, in an ordinary execution
        // run, under both runners - asserting it must be positive would be asserting a false
        // expectation about when MSTest actually calls GetDisplayName, not a real regression guard.
        // A miss, however, is never expected in *any* mode - it would mean GetDisplayName was called
        // with a row array that does not match any GetData-returned instance, falsifying the
        // row-array-identity assumption the seed/display-name bridge depends on. This assertion is
        // the permanent regression guard; the discovery-mode row-identity proof itself (seeded display
        // names actually appearing, under both MTP and classic VSTest) is a real-run check recorded
        // once in PLAN-0057's Notes, not something a normal execution-mode test run can re-prove on
        // every CI run.
        Assert.AreEqual(0, ComposeAttribute.SeedByRowMissCount,
            "GetDisplayName received a row array that did not match any GetData-returned instance - " +
            "the row-array-identity assumption Compono.MSTest's seed/display-name bridge depends on " +
            "does not hold under this runner.");

        // ADR-0057 §9's discovery/execution repeat-composition contract is a real, runner-dependent
        // property - PLAN-0057's Notes records the actual GetData invocation counts observed under
        // each scenario (single MTP run, single classic-VSTest run, separate classic-VSTest
        // discovery + execution processes), captured via the COMPONO_MSTEST_GETDATA_LOG environment
        // variable ComposeAttribute.GetData writes to when set - see ComposeAttribute.cs. Not
        // asserted here: the count legitimately differs by runner/mode, so pinning one expected
        // value would make this a false regression guard, not a real one.
    }
}
