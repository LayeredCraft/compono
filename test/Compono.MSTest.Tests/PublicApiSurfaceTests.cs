using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest.Tests;

// Cheap insurance against accidental public-API drift - locks the exact set of public types
// Compono.MSTest exposes, matching ADR-0057 §6's frozen public API shape and
// Compono.XunitV3.Tests'/Compono.TUnit.Tests' identical convention.
[TestClass]
public sealed class PublicApiSurfaceTests
{
    [TestMethod]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        var publicTypeNames = typeof(ComposeAttribute).Assembly.GetTypes()
            .Where(static type => type.IsPublic || type.IsNestedPublic)
            .Select(static type => type.FullName)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "Compono.MSTest.ComposeAttribute",
                "Compono.MSTest.ComposeAttribute`1",
                "Compono.MSTest.ComposeAttribute`2",
                "Compono.MSTest.SharedAttribute",
            },
            publicTypeNames);
    }
}
