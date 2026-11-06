using System.Collections.Generic;
using System.Linq;
using Compono.Generators.Models;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataPlanPipelineTests
{
    private static readonly string[] ComposeDataDiagnosticIds =
    [
        "CMP0051", "CMP0052", "CMP0053", "CMP0054", "CMP0055", "CMP0056", "CMP0057",
    ];

    [Fact]
    public void InvalidComposeDataMethod_StillPlansItsParameterType()
    {
        var source = ComposeDataAotStandIns.Source + """

            namespace TestNamespace;

            public sealed class Widget { }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Missing")]
                public void TestMethod(Widget widget) { }
            }
            """;

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions { SourceCode = source },
            TestContext.Current.CancellationToken);

        var run = driver.GetRunResult();

        run.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("CMP0051");

        run.GeneratedTrees
            .Select(tree => tree.GetText().ToString())
            .Should()
            .Contain(
                text => text.Contains("TestNamespace.Widget", System.StringComparison.Ordinal),
                "the plan pipeline must still plan a ComposeData parameter type even when the method fails validation");
    }

    [Fact]
    public void InvalidComposeDataMethod_StillProducesAValidationModel()
    {
        var source = ComposeDataAotStandIns.Source + """

            namespace TestNamespace
            {
                public sealed class Widget { }

                public sealed class SampleTests
                {
                    [Compono.XunitV3.Aot.ComposeData("Missing")]
                    public void TestMethod(Widget widget) { }
                }
            }
            """;

        var (diagnostics, models) = ComposeDataAotTestHelper.DiscoverFullSource(source);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("CMP0051");
        models.Should().ContainSingle();
        models[0].Diagnostics.Select(diagnostic => diagnostic.Descriptor.Id).Should().Contain("CMP0051");
    }

    [Fact]
    public void JitNamespaceComposeData_ProducesNoAotModelAndNoComposeDataDiagnostics()
    {
        var source = ComposeDataAotStandIns.Source + """

            namespace Compono.XunitV3
            {
                public sealed class ComposeDataAttribute : Xunit.v3.DataAttribute
                {
                    public ComposeDataAttribute(string memberName) { }
                }
            }

            namespace TestNamespace
            {
                public sealed class SampleTests
                {
                    [Compono.XunitV3.ComposeData("Missing")]
                    public void TestMethod(string value) { }
                }
            }
            """;

        var (diagnostics, models) = ComposeDataAotTestHelper.DiscoverFullSource(source);

        models.Should().BeEmpty("the AOT ComposeData pipeline only matches the Compono.XunitV3.Aot attribute family");
        diagnostics.Select(diagnostic => diagnostic.Id).Should().NotIntersectWith(ComposeDataDiagnosticIds);
    }

    [Fact]
    public void TypeSharedWithValidComposeMethod_StillGetsAPlanWhenComposeDataMethodIsInvalid()
    {
        var source = ComposeDataAotStandIns.Source + """

            namespace TestNamespace;

            public sealed class Widget { }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.Compose]
                public void Valid(Widget widget) { }

                [Compono.XunitV3.Aot.ComposeData("Missing")]
                public void Invalid(Widget widget) { }
            }
            """;

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions { SourceCode = source },
            TestContext.Current.CancellationToken);

        driver.GetRunResult()
            .GeneratedTrees
            .Select(tree => tree.GetText().ToString())
            .Should()
            .Contain(text => text.Contains("TestNamespace.Widget", System.StringComparison.Ordinal));
    }
}
