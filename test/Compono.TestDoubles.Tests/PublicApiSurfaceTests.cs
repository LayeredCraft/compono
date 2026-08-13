namespace Compono.TestDoubles.Tests;

// Cheap insurance against accidental public-API drift, matching Compono.NSubstitute.Tests'
// PublicApiSurfaceTests pattern - locks the exact set of public types Compono.TestDoubles exposes
// (deliberately small, per ADR-0043 Amendment 2: just the provider type and UseGeneratedTestDoubles()).
public sealed class PublicApiSurfaceTests
{
    [Fact]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        // IsPublic alone misses an accidentally-added nested public type; the Name.Contains('<')
        // exclusion filters out the compiler-synthesized marker type(s) C# 14's extension-block syntax
        // lowers to - see Compono.NSubstitute.Tests.PublicApiSurfaceTests' identical comment.
        var publicTypeNames = typeof(GeneratedTestDoubleProvider).Assembly.GetTypes()
            .Where(static type => (type.IsPublic || type.IsNestedPublic) && !type.Name.Contains('<'))
            .Select(static type => type.FullName);

        publicTypeNames.Should().BeEquivalentTo(
        [
            "Compono.GeneratedTestDoubleProvider",
            "Compono.CompositionBuilderExtensions",
        ]);
    }
}
