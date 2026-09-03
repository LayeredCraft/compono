using NUnit.Framework;

namespace Compono.NUnit.Tests;

// Cheap insurance against accidental public-API drift - locks the exact set of public types
// Compono.NUnit exposes, matching ADR-0059 §4's frozen public API shape and
// Compono.XunitV3.Tests'/Compono.TUnit.Tests'/Compono.MSTest.Tests' identical convention.
[TestFixture]
public sealed class PublicApiSurfaceTests
{
    [Test]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        var publicTypeNames = typeof(ComposeAttribute).Assembly.GetTypes()
            .Where(static type => type.IsPublic || type.IsNestedPublic)
            .Select(static type => type.FullName)
            .ToArray();

        Assert.That(publicTypeNames, Is.EquivalentTo(
            new[]
            {
                "Compono.NUnit.ComposeAttribute",
                "Compono.NUnit.ComposeAttribute`1",
                "Compono.NUnit.ComposeAttribute`2",
                "Compono.NUnit.SharedAttribute",
            }));
    }
}
