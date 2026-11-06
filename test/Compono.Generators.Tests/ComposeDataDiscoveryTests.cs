using Microsoft.CodeAnalysis;

namespace Compono.Generators.Tests;

/// <summary>
/// Proves the <c>[ComposeData]</c> discovery path (ADR-0070, issue #163 phase B3) wires all three
/// attribute forms into the same per-parameter plan and <c>RowInvokerRegistry</c> registration that
/// the <c>[Compose]</c> family already gets. The AOT-side registration emission for Composed theory
/// rows is a later change (phase B4) and is intentionally not covered here.
/// </summary>
public sealed class ComposeDataDiscoveryTests
{
    private const string XunitStandIns = """
        namespace Xunit
        {
            public interface ITheoryDataRow { }
            public sealed class TheoryDataRow : ITheoryDataRow
            {
                public TheoryDataRow(object?[] data) { }
            }
        }

        namespace Xunit.Sdk
        {
            public sealed class DisposalTracker { }
        }

        namespace Xunit.v3
        {
            public abstract class DataAttribute : System.Attribute { }
        }

        namespace Compono.XunitV3
        {
            public class ComposeDataAttribute : Xunit.v3.DataAttribute
            {
                public ComposeDataAttribute(string memberName) { }
            }

            public sealed class ComposeDataAttribute<TProfile> : ComposeDataAttribute
            {
                public ComposeDataAttribute(string memberName) : base(memberName) { }
            }

            public sealed class ComposeDataAttribute<TProfile, TConfig> : ComposeDataAttribute
            {
                public ComposeDataAttribute(string memberName) : base(memberName) { }
            }
        }
        """;

    [Fact]
    public void ComposeData_NonGeneric_DiscoversParameterTypeAndEmitsRowInvokerRegistration() =>
        AssertDiscovers("Compono.XunitV3.ComposeData");

    [Fact]
    public void ComposeData_ProfileGeneric_DiscoversParameterTypeAndEmitsRowInvokerRegistration() =>
        AssertDiscovers("Compono.XunitV3.ComposeData<Sample.SomeProfile>");

    [Fact]
    public void ComposeData_ProfileConfigGeneric_DiscoversParameterTypeAndEmitsRowInvokerRegistration() =>
        AssertDiscovers("Compono.XunitV3.ComposeData<Sample.SomeProfile, Sample.SomeConfig>");

    private static void AssertDiscovers(string attributeName)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions { SourceCode = SourceFor(attributeName) },
            TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();

        var generatedText = string.Join("\n", result.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        generatedText.Should().Contain("RowInvokerRegistry.Register");
        generatedText.Should().Contain("Widget");
    }

    private static string SourceFor(string attributeName) => XunitStandIns + $$"""

        namespace Sample;

        public sealed class Widget
        {
            public Widget() { }
        }

        public sealed class SomeProfile { }
        public sealed class SomeConfig { }

        public sealed class Tests
        {
            [global::{{attributeName}}("Rows")]
            public void M(Widget widget) { }
        }
        """;
}
