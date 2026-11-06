using System.Collections.Immutable;
using Basic.Reference.Assemblies;
using Compono.Generators;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

internal static class ComposeDataAotTestHelper
{
    internal static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<AotComposeDataMethodInfo> Models) Discover(string source) =>
        DiscoverFullSource(ComposeDataAotStandIns.Source + source);

    internal static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<AotComposeDataMethodInfo> Models) DiscoverFullSource(string fullSource)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var tree = CSharpSyntaxTree.ParseText(
            fullSource,
            parseOptions,
            "Program.cs",
            cancellationToken: cancellationToken);
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable,
            allowUnsafe: true);
        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];
        var compilation = CSharpCompilation.Create(
            "AotComposeDataDiscoveryTestsAssembly",
            [tree],
            references,
            compilationOptions);
        var compilerErrors = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        if (compilerErrors.Count != 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"The test source did not compile: {string.Join(" | ", compilerErrors.Select(diagnostic => diagnostic.ToString()))}");
        }

        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driverOptions = new GeneratorDriverOptions(disabledOutputs: default, trackIncrementalGeneratorSteps: true);
        var driver = CSharpGeneratorDriver.Create([generator], driverOptions: driverOptions)
            .RunGenerators(compilation, cancellationToken);
        var result = driver.GetRunResult();
        var models = result.Results.Single().TrackedSteps.TryGetValue(TrackingNames.AotComposeDataMethodsAll, out var steps)
            ? steps
                .SelectMany(step => step.Outputs)
                .Select(output => (AotComposeDataMethodInfo)output.Value!)
                .ToList()
            : [];
        return (result.Diagnostics, models);
    }

    internal static (GeneratorDriver Driver, CSharpCompilation Compilation) CreateTrackingDriver(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var tree = CSharpSyntaxTree.ParseText(
            ComposeDataAotStandIns.Source + source,
            parseOptions,
            "Program.cs",
            cancellationToken: cancellationToken);
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable,
            allowUnsafe: true);
        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];
        var compilation = CSharpCompilation.Create(
            "AotComposeDataRemediationTestsAssembly",
            [tree],
            references,
            compilationOptions);
        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driverOptions = new GeneratorDriverOptions(disabledOutputs: default, trackIncrementalGeneratorSteps: true);
        var driver = CSharpGeneratorDriver.Create([generator], driverOptions: driverOptions);
        return (driver, compilation);
    }

    internal static AotComposeDataMethodInfo SingleModel(string source)
    {
        var (diagnostics, models) = Discover(source);
        if (diagnostics.Length != 0)
        {
            throw new Xunit.Sdk.XunitException($"Expected no diagnostics, but found: {string.Join(", ", diagnostics.Select(d => d.Id))}");
        }

        if (models.Count != 1)
        {
            throw new Xunit.Sdk.XunitException($"Expected exactly one model, but found {models.Count}.");
        }

        return models[0];
    }

    internal static ImmutableArray<Diagnostic> AssertDiagnostic(string source, string expectedDiagnosticId)
    {
        var (diagnostics, _) = Discover(source);
        if (!diagnostics.Any(d => d.Id == expectedDiagnosticId))
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected diagnostic '{expectedDiagnosticId}', but found: [{string.Join(", ", diagnostics.Select(d => d.Id))}].");
        }

        return diagnostics;
    }
}
