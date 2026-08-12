namespace Compono.TUnit.Tests;

// Cheap insurance against accidental public-API drift (PLAN-0040 Phase 2's final API-surface lock) -
// locks the exact set of public types Compono.TUnit exposes, matching Compono.XunitV3's own
// PublicApiSurfaceTests.cs pattern (docs/public-api.md's "keep public APIs minimal" constraint). A
// hand-rolled exact-set assertion, not a Verify snapshot - same reasoning as that file: the expected
// shape is a fixed, short list, so a snapshot would add ceremony without adding coverage.
public sealed class PublicApiSurfaceTests
{
    [Test]
    public async Task Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        // IsPublic alone misses an accidentally-added nested public type - only a top-level type can
        // be IsPublic, a nested one is IsNestedPublic instead (Compono.XunitV3.Tests'
        // PublicApiSurfaceTests.cs, PR #26 review). Checking only IsPublic would let an unapproved
        // `public class Foo { public class Bar { } }` addition slip through this exact-set assertion
        // undetected.
        var publicTypeNames = typeof(ComposeAttribute).Assembly.GetTypes()
            .Where(static type => type.IsPublic || type.IsNestedPublic)
            .Select(static type => type.FullName!);

        await Assert.That(publicTypeNames).IsEquivalentTo(
        [
            "Compono.TUnit.ComposeAttribute",
            "Compono.TUnit.ComposeAttribute`1",
            "Compono.TUnit.ComposeAttribute`2",
            "Compono.TUnit.SharedAttribute",
        ]);
    }
}
