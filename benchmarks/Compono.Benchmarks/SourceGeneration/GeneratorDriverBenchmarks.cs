using System.Text;
using BenchmarkDotNet.Attributes;
using Basic.Reference.Assemblies;
using Compono.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Benchmarks.SourceGeneration;

/// <summary>
/// Clean vs. incremental generation cost, across a composable-type-count matrix, per ADR-0034 - a
/// separate concern from every other category's runtime performance, measured in-process via
/// Roslyn's <see cref="CSharpGeneratorDriver"/> directly (the same way
/// <c>Compono.Generators.Tests</c> drives it), not a separate timing harness. Primarily serves
/// maintainers.
/// </summary>
[MemoryDiagnoser]
public class GeneratorDriverBenchmarks
{
    // Assigned in GlobalSetup, which BenchmarkDotNet guarantees runs (once per Params value)
    // before any [Benchmark] method executes.
    private Compilation _baseCompilation = null!;
    private Compilation _touchedCompilation = null!;
    private GeneratorDriver _warmDriver = null!;

    /// <summary>How many <c>Composer.Create&lt;T&gt;()</c> call sites (and matching composable types) the compilation contains for this run.</summary>
    [Params(1, 10, 50)]
    public int TypeCount { get; set; }

    /// <summary>
    /// Builds this run's base and touched compilations, and runs the generator once against the
    /// base compilation to fully warm <see cref="_warmDriver"/>'s incremental cache - kept out of
    /// both timed benchmark methods.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _baseCompilation = BuildCompilation(TypeCount, touched: false);

        // Derived from _baseCompilation via ReplaceSyntaxTree, not built as an independent
        // CSharpCompilation.Create(...) call - Roslyn's incremental generator cache is keyed off
        // compilation/tree identity, so two separately-constructed compilations look entirely
        // unrelated to the driver (forcing a full recompute, not a real incremental diff) even if
        // their source text is nearly identical.
        var touchedTree = BuildSyntaxTree(TypeCount, touched: true);
        _touchedCompilation = _baseCompilation.ReplaceSyntaxTree(_baseCompilation.SyntaxTrees.Single(), touchedTree);

        var driver = CSharpGeneratorDriver.Create(new ComponoIncrementalGenerator().AsSourceGenerator());
        _warmDriver = driver.RunGenerators(_baseCompilation);
    }

    /// <summary>A brand-new driver against the base compilation - no incremental cache to reuse, models a first/cold build.</summary>
    [Benchmark(Baseline = true)]
    public GeneratorDriver CleanGeneration()
    {
        var driver = CSharpGeneratorDriver.Create(new ComponoIncrementalGenerator().AsSourceGenerator());
        return driver.RunGenerators(_baseCompilation);
    }

    /// <summary>The already-warmed driver against a trivially touched compilation (an unrelated trailing comment, no call site changed) - models an incremental rebuild after a small, unrelated source edit.</summary>
    [Benchmark]
    public GeneratorDriver IncrementalGeneration() => _warmDriver.RunGenerators(_touchedCompilation);

    private static Compilation BuildCompilation(int typeCount, bool touched)
    {
        var syntaxTree = BuildSyntaxTree(typeCount, touched);

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

        return CSharpCompilation.Create("SourceGenerationBenchmarkAssembly", [syntaxTree], references, compilationOptions);
    }

    private static SyntaxTree BuildSyntaxTree(int typeCount, bool touched)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        return CSharpSyntaxTree.ParseText(BuildSource(typeCount, touched), parseOptions, "GeneratedTypes.cs");
    }

    private static string BuildSource(int typeCount, bool touched)
    {
        var source = new StringBuilder();
        source.AppendLine("namespace Compono.Benchmarks.SourceGeneration.Generated;");
        source.AppendLine();

        for (var i = 0; i < typeCount; i++)
            source.AppendLine($"public sealed record GeneratedType{i}(string Value);");

        source.AppendLine();
        source.AppendLine("public static class EntryPoint");
        source.AppendLine("{");
        source.AppendLine("    public static void ComposeAll(global::Compono.Composer composer)");
        source.AppendLine("    {");
        for (var i = 0; i < typeCount; i++)
            source.AppendLine($"        composer.Create<GeneratedType{i}>();");
        source.AppendLine("    }");
        source.AppendLine("}");

        if (touched)
            source.AppendLine("// incremental-touch marker - no call site changed");

        return source.ToString();
    }
}
