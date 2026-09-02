using Compono.MSTest.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

[TestClass]
public sealed class SeedReportingTests
{
    [TestMethod]
    public void GetDisplayName_ContainsTheExactSeedTheRowUsed()
    {
        var attribute = new ComposeAttribute { Seed = 492173 };
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var data = attribute.GetData(method).Single();
        var displayName = attribute.GetDisplayName(method, data);

        Assert.IsNotNull(displayName);
        StringAssert.Contains(displayName, "seed: 492173");
        StringAssert.Contains(displayName, nameof(SampleTestMethods.Simple));
    }

    [TestMethod]
    public void GetDisplayName_ReportsDifferentSeeds_ForTwoIndependentCalls()
    {
        // ADR-0057 §9: each GetData call gets its own fresh row/seed (unless Seed is explicitly
        // pinned) - the display name for each independently-returned row must reflect that specific
        // row's own seed, not a cached "first call" value.
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var firstData = attribute.GetData(method).Single();
        var secondData = attribute.GetData(method).Single();

        var firstName = attribute.GetDisplayName(method, firstData);
        var secondName = attribute.GetDisplayName(method, secondData);

        Assert.AreNotEqual(firstName, secondName);
    }

    [TestMethod]
    public void GetDisplayName_DoesNotThrow_WhenDataIsNotARecognizedRow()
    {
        // Falls back to a fresh/unset seed rather than throwing - a display name is diagnostic,
        // never load-bearing.
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var displayName = attribute.GetDisplayName(method, null);

        Assert.IsNotNull(displayName);
    }
}
