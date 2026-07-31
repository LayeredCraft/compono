namespace Compono.NSubstitute.Tests;

// Cheap insurance against accidental public-API drift (PLAN-0005 Phase 2's plan task) - locks the
// exact set of public types Compono.NSubstitute exposes, matching Compono.XunitV3.Tests'
// PublicApiSurfaceTests pattern (PLAN-0004 Phase 3).
public sealed class PublicApiSurfaceTests
{
    [Fact]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        // IsPublic alone misses an accidentally-added nested public type (PR #26 review, fourth
        // round): a nested type is never IsPublic (only a top-level type can be), it's IsNestedPublic
        // instead - reflection's own naming split, not a synonym.
        //
        // The Name.Contains('<') exclusion is new here versus Compono.XunitV3.Tests' version of this
        // test: CompositionBuilderExtensions uses C# 14 extension-block syntax
        // (coding-standards.md), which the compiler lowers into its own nested marker
        // type(s) - one of which reflection reports as IsNestedPublic=true despite carrying no
        // CompilerGeneratedAttribute to filter on, since it's part of the extension block's own
        // metadata encoding rather than an ordinary compiler-synthesized closure. '<'/'>' are
        // otherwise illegal in a C# identifier, so this exclusion can never accidentally hide a real,
        // hand-declared public type.
        var publicTypeNames = typeof(NSubstituteProvider).Assembly.GetTypes()
            .Where(static type => (type.IsPublic || type.IsNestedPublic) && !type.Name.Contains('<'))
            .Select(static type => type.FullName);

        publicTypeNames.Should().BeEquivalentTo(
        [
            "Compono.NSubstituteProvider",
            "Compono.NSubstituteOptions",
            "Compono.CompositionBuilderExtensions",
        ]);
    }
}
