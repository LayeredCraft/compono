using Compono.Generators.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

public sealed class EmissionIsolationGeneratorDriverTests
{
    [Fact]
    public void RunGenerators_IsolatesFailedItemAndPreservesHealthySourceDeterministically()
    {
        var generator = new EmissionIsolationProbeGenerator();
        var compilation = CSharpCompilation.Create(
            "EmissionIsolationDriverProbe",
            [CSharpSyntaxTree.ParseText(
                "internal sealed class Input {}",
                cancellationToken: TestContext.Current.CancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        var firstResult = driver
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult();
        var secondResult = driver
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult();

        AssertIsolatedResult(firstResult);
        AssertIsolatedResult(secondResult);
        GetDeterministicResult(firstResult).Should().BeEquivalentTo(
            GetDeterministicResult(secondResult),
            options => options.WithStrictOrdering());
    }

    private static void AssertIsolatedResult(GeneratorDriverRunResult result)
    {
        var diagnostic = result.Diagnostics.Should().ContainSingle(item => item.Id == "CMP0050").Which;
        diagnostic.GetMessage().Should().Contain("probe artifact for 'failing'");
        result.Diagnostics.Should().NotContain(item => item.Id == "CS8785");

        var generatedTree = result.GeneratedTrees.Should().ContainSingle().Which;
        generatedTree.FilePath.Should().EndWith("healthy.g.cs");
        generatedTree.GetText().ToString().Should().Be("internal sealed class Generated_healthy {}");
        result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("failing.g.cs", StringComparison.Ordinal));
    }

    private static object GetDeterministicResult(GeneratorDriverRunResult result) => new
    {
        Diagnostics = result.Diagnostics.Select(diagnostic => diagnostic.ToString()).ToArray(),
        GeneratedSources = result.GeneratedTrees
            .Select(tree => new { tree.FilePath, Source = tree.GetText().ToString() })
            .ToArray()
    };

    private sealed class EmissionIsolationProbeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var values = context.CompilationProvider.SelectMany(static (_, _) => new[] { "failing", "healthy" });

            context.RegisterSourceOutput(values, static (productionContext, value) =>
                EmissionIsolation.TryEmit(productionContext, "probe artifact", value, () =>
                {
                    if (value == "failing")
                        throw new InvalidOperationException("probe failure");

                    productionContext.AddSource(
                        $"{value}.g.cs",
                        $"internal sealed class Generated_{value} {{}}");
                }));
        }
    }
}
