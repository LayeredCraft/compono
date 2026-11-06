using System.Linq;
using Basic.Reference.Assemblies;
using Compono.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataIncrementalDiagnosticTests
{
    private const string TestsSource = """
        namespace TestNamespace;

        public sealed class SampleTests
        {
            [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
            public void TestMethod() { }
        }
        """;

    private const string HolderOriginal = """
        namespace TestNamespace;

        public sealed class Holder
        {
        }
        """;

    private const string HolderEdited = """
        namespace TestNamespace;

        public sealed class Holder
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases() => [];
            public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => [];
        }
        """;

    [Fact]
    public void EditingHolderInSeparateFile_UpdatesDiagnosticAndKeepsMethodLocation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);

        List<MetadataReference> references = [
#if NET11_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        var standInTree = CSharpSyntaxTree.ParseText(ComposeDataAotStandIns.Source, parseOptions, "StandIns.cs", cancellationToken: cancellationToken);
        var testsTree = CSharpSyntaxTree.ParseText(TestsSource, parseOptions, "Tests.cs", cancellationToken: cancellationToken);
        var holderTree = CSharpSyntaxTree.ParseText(HolderOriginal, parseOptions, "Holder.cs", cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create("AotComposeDataIncrementalDiagnosticTestsAssembly", [standInTree, testsTree, holderTree], references, compilationOptions);

        var driverOptions = new GeneratorDriverOptions(disabledOutputs: default, trackIncrementalGeneratorSteps: true);
        var driver = ((GeneratorDriver)CSharpGeneratorDriver.Create([new ComponoIncrementalGenerator().AsSourceGenerator()], driverOptions: driverOptions))
            .RunGenerators(compilation, cancellationToken);

        var firstDiagnostic = driver.GetRunResult().Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0051");
        firstDiagnostic.GetMessage().Should().Contain("was not found");

        var editedHolderTree = holderTree.WithChangedText(SourceText.From(HolderEdited));
        var editedCompilation = compilation.ReplaceSyntaxTree(holderTree, editedHolderTree);
        var secondDriver = driver.RunGenerators(editedCompilation, cancellationToken);
        var secondResult = secondDriver.GetRunResult();
        var secondDiagnostic = secondResult.Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0051");

        secondDiagnostic.GetMessage().Should().Contain("is ambiguous");
        secondDiagnostic.GetMessage().Should().NotContain("was not found");

        secondDiagnostic.Location.GetLineSpan().Path.Should().EndWith("Tests.cs");
        secondDiagnostic.Location.GetLineSpan().StartLinePosition.Should().Be(firstDiagnostic.Location.GetLineSpan().StartLinePosition);

        var freshDriver = ((GeneratorDriver)CSharpGeneratorDriver.Create([new ComponoIncrementalGenerator().AsSourceGenerator()], driverOptions: driverOptions))
            .RunGenerators(editedCompilation, cancellationToken);
        var freshDiagnostic = freshDriver.GetRunResult().Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0051");
        secondDiagnostic.GetMessage().Should().Be(freshDiagnostic.GetMessage());

        secondResult.Results.Single().TrackedSteps
            .Where(pair => pair.Key is TrackingNames.AotComposeDataMethods or TrackingNames.AotComposeDataMethodsAll)
            .SelectMany(pair => pair.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .Should().Contain(reason => reason != IncrementalStepRunReason.Cached && reason != IncrementalStepRunReason.Unchanged);
    }
}
