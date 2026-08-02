namespace Compono.Bogus.Tests;

// Cheap insurance against accidental public-API drift, matching Compono.NSubstitute.Tests'
// PublicApiSurfaceTests pattern (PLAN-0006 Phase 3) - locks the exact set of public types
// Compono.Bogus exposes, now including ADR-0028's BogusConvention/BogusOptions.AddAlias/AddConvention.
public sealed class PublicApiSurfaceTests
{
    [Fact]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        // IsPublic alone misses a nested public type (never IsPublic, only IsNestedPublic - reflection's
        // own naming split). The Name.Contains('<') exclusion filters out the compiler's own
        // extension-block marker type(s) (C# 14 extension syntax, coding-standards.md) - '<'/'>' are
        // otherwise illegal in a hand-declared C# identifier, so this can never hide a real public type.
        var publicTypeNames = typeof(BogusMemberNameProvider).Assembly.GetTypes()
            .Where(static type => (type.IsPublic || type.IsNestedPublic) && !type.Name.Contains('<'))
            .Select(static type => type.FullName);

        publicTypeNames.Should().BeEquivalentTo(
        [
            "Compono.BogusMemberNameProvider",
            "Compono.BogusOptions",
            "Compono.BogusConvention",
            "Compono.CompositionBuilderExtensions",
            "Compono.MemberRuleExtensions",
        ]);
    }
}
