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
        // IsPublic alone misses an accidentally-added nested public type (PR #26 review, fourth
        // round): a nested type is never IsPublic (only a top-level type can be), it's IsNestedPublic
        // instead - reflection's own naming split, not a synonym. Checking only IsPublic would let an
        // unapproved `public class Foo { public class Bar { } }` addition slip through this exact-set
        // assertion undetected.
        //
        // The Name.Contains('<') exclusion mirrors Compono.NSubstitute.Tests' version of this test:
        // FixtureContextExtensions uses C# 14 extension-block syntax (coding-standards.md), which the
        // compiler lowers into its own nested marker type that reflection reports as
        // IsNestedPublic=true despite carrying no CompilerGeneratedAttribute. '<'/'>' are otherwise
        // illegal in a C# identifier, so this exclusion can never hide a real, hand-declared type.
        var publicTypeNames = typeof(ComposeAttribute).Assembly.GetTypes()
            .Where(static type => (type.IsPublic || type.IsNestedPublic) && !type.Name.Contains('<'))
            .Select(static type => type.FullName);

        publicTypeNames.Should().BeEquivalentTo(
        [
            "Compono.XunitV3.ComposeAttribute",
            "Compono.XunitV3.ComposeAttribute`1",
            "Compono.XunitV3.ComposeAttribute`2",
            "Compono.XunitV3.ComposeDataAttribute",
            "Compono.XunitV3.ComposeDataAttribute`1",
            "Compono.XunitV3.ComposeDataAttribute`2",
            "Compono.XunitV3.FixtureContextExtensions",
            "Compono.XunitV3.SharedAttribute",
        ]);
    }
}
