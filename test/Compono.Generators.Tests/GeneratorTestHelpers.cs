using System.Text.RegularExpressions;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

internal sealed class CodeGenerationOptions
{
    internal required string SourceCode { get; init; }

    internal string CodePath { get; init; } = "Program.cs";

    /// <summary>
    /// Extra metadata references beyond the BCL and <c>Compono</c> itself - used to test behavior
    /// against a type that lives in a separate referenced assembly (e.g. accessibility checks that
    /// only matter across an assembly boundary).
    /// </summary>
    internal IReadOnlyList<MetadataReference> ExtraReferences { get; init; } = [];
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
        // The tree's FilePath must be preserved: file-scoped types (which generated plans are) get
        // their identity from the file path, and re-parsing without one gives every tree path "",
        // making same-named file-scoped types from different generated files spuriously collide in
        // this harness even though they're legal in a real build.
        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var reparsedTrees = result.GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)parseOptions, tree.FilePath))
            .ToArray();

        var outputCompilation = originalCompilation.AddSyntaxTrees(reparsedTrees);
        var errors = outputCompilation.GetDiagnostics(cancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty(
            "generated code should compile without errors, but found:\n" +
            string.Join("\n---\n", errors.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        return VerifyDriver(driver);
    }

    // A failing type can still share its generator run with a sibling that succeeded (Phase 1's
    // transitive closure walk - e.g. Customer succeeds even though its nested Address parameter
    // fails constructor selection), so VerifyFailure can emit real generated `.cs` output too, not
    // just a diagnostics-only snapshot - it needs the same GeneratedCodeAttribute version scrub as
    // Verify, or the snapshot embeds whatever commit SHA happened to be checked out locally and
    // fails on every other machine/commit.
    internal static Task VerifyFailure(CodeGenerationOptions options, string expectedDiagnosticId, CancellationToken cancellationToken = default)
    {
        var (driver, _) = GenerateFromSource(options, cancellationToken);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().Contain(
            d => d.Id == expectedDiagnosticId,
            $"expected diagnostic {expectedDiagnosticId} to be present, but found:\n" +
            string.Join("\n---\n", result.Diagnostics.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        return VerifyDriver(driver);
    }

    // GeneratedCodeAttribute's version argument embeds the generator assembly's own build commit
    // SHA (CompositionPlanEmitter.GeneratorVersion) - it changes every time the generator is
    // rebuilt at a different commit, so it has to be scrubbed to a fixed placeholder before
    // snapshotting, or the snapshot only ever matches the exact commit it was accepted against.
    private static SettingsTask VerifyDriver(GeneratorDriver driver) =>
        Verifier.Verify(driver)
            .UseDirectory("Snapshots")
            .DisableDiff()
            .ScrubLinesWithReplace(line =>
                line.Contains("global::System.CodeDom.Compiler.GeneratedCode")
                    ? GeneratedCodeAttributeRegex().Replace(line, "REPLACED")
                    : line);

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
            .. options.ExtraReferences,
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

    /// <summary>
    /// Compiles <paramref name="sourceCode"/> into a standalone in-memory assembly (no
    /// InternalsVisibleTo grants), for tests that need a type living in a genuinely separate
    /// referenced assembly - e.g. proving an <c>internal</c> constructor there is correctly treated
    /// as inaccessible to generated code in a different consuming assembly.
    /// </summary>
    internal static MetadataReference CompileLibrary(string sourceCode, string assemblyName)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Net100.References.All,
#endif
        ];

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        emitResult.Success.Should().BeTrue(
            "the library helper source should compile cleanly, but found:\n" +
            string.Join("\n---\n", emitResult.Diagnostics.Select(d => $"  - {d.Id}: {d.GetMessage()}")));

        stream.Position = 0;
        return MetadataReference.CreateFromStream(stream);
    }

    [GeneratedRegex("""(?<=")\d+\.\d+\.\d+[\w|\+|\.|\-]*(?=")""")]
    private static partial Regex GeneratedCodeAttributeRegex();
}
