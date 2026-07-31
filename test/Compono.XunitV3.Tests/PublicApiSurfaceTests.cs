namespace Compono.XunitV3.Tests;

// Cheap insurance against accidental public-API drift (Phase 3's plan task) - locks the exact set of
// public types Compono.XunitV3 exposes, matching this milestone's "keep public APIs minimal"
// constraint (docs/public-api.md). A hand-rolled exact-set assertion, not a Verify snapshot: the
// expected shape is a fixed, short list, so a snapshot file would add ceremony without adding
// coverage testing.md doesn't already ask for.
public sealed class PublicApiSurfaceTests
{
    [Fact]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        var publicTypeNames = typeof(ComposeAttribute).Assembly.GetTypes()
            .Where(static type => type.IsPublic)
            .Select(static type => type.FullName);

        publicTypeNames.Should().BeEquivalentTo(
        [
            "Compono.XunitV3.ComposeAttribute",
            "Compono.XunitV3.ComposeAttribute`1",
            "Compono.XunitV3.SharedAttribute",
        ]);
    }
}
