using System.Collections.Generic;
using System.Linq;
using Basic.Reference.Assemblies;
using Compono.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataIncrementalCachingTests
{
    [Fact]
    public void AotComposeDataMethodsStage_ReportsACacheHit_WhenAnUnrelatedSourceEditFollows()
    {
        const string original = ComposeDataAotStandIns.Source + """

            namespace TestNamespace;

            public sealed class WidgetTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases() => new object[] { "leaf" };

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void Widget_is_composed(string leaf) { }
            }
            """;

        const string edited = original + """

            namespace UnrelatedNamespace;

            public sealed class Unrelated;
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var originalTree = CSharpSyntaxTree.ParseText(original, parseOptions, "Program.cs", cancellationToken: cancellationToken);
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];
        var originalCompilation = CSharpCompilation.Create("AotComposeDataIncrementalCachingTestsAssembly", [originalTree], references, compilationOptions);
        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driverOptions = new GeneratorDriverOptions(disabledOutputs: default, trackIncrementalGeneratorSteps: true);
        var driver = ((GeneratorDriver)CSharpGeneratorDriver.Create([generator], driverOptions: driverOptions)).RunGenerators(originalCompilation, cancellationToken);

        var editedTree = originalTree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(edited));
        var editedCompilation = originalCompilation.ReplaceSyntaxTree(originalTree, editedTree);
        var secondRunDriver = driver.RunGenerators(editedCompilation, cancellationToken);
        var secondResult = secondRunDriver.GetRunResult();
        var steps = secondResult.Results.Single().TrackedSteps[TrackingNames.AotComposeDataMethods];

        steps.Should().NotBeEmpty("the AOT ComposeData per-method stage must have run");
        steps.SelectMany(step => step.Outputs).Should().OnlyContain(
            output => output.Reason == IncrementalStepRunReason.Cached || output.Reason == IncrementalStepRunReason.Unchanged,
            "an unrelated source edit must not invalidate the AOT ComposeData per-method stage");
    }
}
