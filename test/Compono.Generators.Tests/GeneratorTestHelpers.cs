using System.Text.RegularExpressions;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

internal sealed class CodeGenerationOptions
{
    internal required string SourceCode { get; init; }

    internal string CodePath { get; init; } = "Program.cs";
}

internal static partial class GeneratorTestHelpers
{
    internal static Task Verify(CodeGenerationOptions options, CancellationToken cancellationToken = default)
    {
        var (driver, originalCompilation) = GenerateFromSource(options, cancellationToken);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty(
            "code should be generated without errors, but found:\n" +
            string.Join("\n---\n", result.Diagnostics.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        // Re-parse generated trees with the same parse options as the original compilation, then
        // add them back and prove the generated code actually compiles - not just that it snapshots.
        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var reparsedTrees = result.GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)parseOptions))
            .ToArray();

        var outputCompilation = originalCompilation.AddSyntaxTrees(reparsedTrees);
        var errors = outputCompilation.GetDiagnostics(cancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty(
            "generated code should compile without errors, but found:\n" +
            string.Join("\n---\n", errors.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        return Verifier.Verify(driver)
            .UseDirectory("Snapshots")
            .DisableDiff()
            .ScrubLinesWithReplace(line =>
                line.Contains("global::System.CodeDom.Compiler.GeneratedCode")
                    ? GeneratedCodeAttributeRegex().Replace(line, "REPLACED")
                    : line);
    }

    internal static Task VerifyFailure(CodeGenerationOptions options, string expectedDiagnosticId, CancellationToken cancellationToken = default)
    {
        var (driver, _) = GenerateFromSource(options, cancellationToken);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().Contain(
            d => d.Id == expectedDiagnosticId,
            $"expected diagnostic {expectedDiagnosticId} to be present, but found:\n" +
            string.Join("\n---\n", result.Diagnostics.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        return Verifier.Verify(driver).UseDirectory("Snapshots").DisableDiff();
    }

    private static (GeneratorDriver driver, Compilation compilation) GenerateFromSource(
        CodeGenerationOptions options, CancellationToken cancellationToken)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var syntaxTree = CSharpSyntaxTree.ParseText(options.SourceCode, parseOptions, options.CodePath, cancellationToken: cancellationToken);

        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable);

        var compilation = CSharpCompilation.Create("TestsAssembly", [syntaxTree], references, compilationOptions);

        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var updatedDriver = driver.RunGenerators(compilation, cancellationToken);

        return (updatedDriver, compilation);
    }

    [GeneratedRegex("""(?<=")\d+\.\d+\.\d+[\w|\+|\.|\-]*(?=")""")]
    private static partial Regex GeneratedCodeAttributeRegex();
}
