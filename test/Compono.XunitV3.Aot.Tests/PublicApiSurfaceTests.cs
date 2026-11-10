using System.Reflection;
using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

// AOT counterpart to Compono.XunitV3.Tests.PublicApiSurfaceTests: locks the exact set of public types
// Compono.XunitV3.Aot exposes, so accidental public-API drift is caught the same way on both engines.
// The Name.Contains('<') exclusion mirrors the JIT version: FixtureContextExtensions uses C# 14
// extension-block syntax, which the compiler lowers into its own '<>'-named nested marker type that
// reflection reports as IsNestedPublic=true.
public sealed class PublicApiSurfaceTests
{
    [Fact]
    public void Assembly_ExposesExactlyTheDocumentedPublicTypes()
    {
        var publicTypeNames = typeof(ComposeAttribute).Assembly.GetTypes()
            .Where(static type => (type.IsPublic || type.IsNestedPublic) && !type.Name.Contains('<'))
            .Select(static type => type.FullName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        string[] expected =
        [
            "Compono.XunitV3.Aot.ComposeAttribute",
            "Compono.XunitV3.Aot.ComposeAttribute`1",
            "Compono.XunitV3.Aot.ComposeAttribute`2",
            "Compono.XunitV3.Aot.ComposeDataAttribute",
            "Compono.XunitV3.Aot.ComposeDataAttributeMetadata",
            "Compono.XunitV3.Aot.ComposeDataAttribute`1",
            "Compono.XunitV3.Aot.ComposeDataAttribute`2",
            "Compono.XunitV3.Aot.ComposeDataRows",
            "Compono.XunitV3.Aot.FixtureContextExtensions",
            "Compono.XunitV3.Aot.RowContext",
            "Compono.XunitV3.Aot.RowData",
            "Compono.XunitV3.Aot.SharedAttribute",
        ];

        Assert.Equal(expected, publicTypeNames);
    }
}
