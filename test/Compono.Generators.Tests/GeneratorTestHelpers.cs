using System.Reflection;
using System.Text.RegularExpressions;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

    /// <summary>
    /// Simulates a consumer's own MSBuild property settings (e.g. <c>ComponoGeneratedTestDoubles</c>,
    /// ADR-0043) - each entry is exposed to <see cref="Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider"/>
    /// under the real <c>build_property.&lt;Name&gt;</c> key a packaged <c>CompilerVisibleProperty</c>
    /// declaration produces. Empty by default, matching an ordinary consumer who never opted into
    /// anything.
    /// </summary>
    internal IReadOnlyDictionary<string, string> MSBuildProperties { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// A minimal <see cref="AnalyzerConfigOptionsProvider"/> exposing a fixed set of
/// <c>build_property.*</c> global options - what a real MSBuild-driven build exposes to the
/// generator once a <c>CompilerVisibleProperty</c> declares the property (ADR-0043 Amendment 4,
/// Finding F), without needing a real MSBuild invocation in this test harness.
/// </summary>
internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> msBuildProperties)
    {
        GlobalOptions = new TestAnalyzerConfigOptions(msBuildProperties);
    }

    public override AnalyzerConfigOptions GlobalOptions { get; }

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> msBuildProperties)
        {
            _values = msBuildProperties.ToDictionary(kvp => $"build_property.{kvp.Key}", kvp => kvp.Value);
        }

        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }
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

    // ADR-0044: a diamond-colliding identity or an overload-set-internal unsupported ref/out/in
    // shape reports a non-blocking, Info-severity diagnostic *and* still emits a working double -
    // unlike VerifyFailure (which asserts the leaf produces zero members), this asserts the
    // expected diagnostic is present *and* the generated code still compiles, matching Verify's own
    // "should compile without errors" check.
    internal static Task VerifyWithInfoDiagnostic(
        CodeGenerationOptions options, string expectedDiagnosticId, CancellationToken cancellationToken = default)
    {
        var (driver, originalCompilation) = GenerateFromSource(options, cancellationToken);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().Contain(
            d => d.Id == expectedDiagnosticId,
            $"expected diagnostic {expectedDiagnosticId} to be present, but found:\n" +
            string.Join("\n---\n", result.Diagnostics.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

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

    // ADR-0049 / PR #107 Codex review round 11: Verify/VerifyWithInfoDiagnostic's own "generated code
    // should compile" check only filters DiagnosticSeverity.Error - it never fails a test over a
    // WARNING, so a real CS8603/CS8616/CS8619 nullable-mismatch warning escaping the closed-
    // instantiation nullable-suppression pragma would silently pass every existing test even though a
    // consumer building with warnings-as-errors would see their build fail on an advertised-supported
    // shape. This helper exists specifically to close that gap - it asserts zero diagnostics with any
    // of the given IDs appear anywhere in the recompiled output, at any severity, not just Error.
    internal static Task VerifyWithNoWarnings(
        CodeGenerationOptions options, string[] forbiddenDiagnosticIds, CancellationToken cancellationToken = default)
    {
        var (driver, originalCompilation) = GenerateFromSource(options, cancellationToken);
        var result = driver.GetRunResult();

        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var reparsedTrees = result.GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)parseOptions, tree.FilePath))
            .ToArray();

        var outputCompilation = originalCompilation.AddSyntaxTrees(reparsedTrees);
        var forbidden = outputCompilation.GetDiagnostics(cancellationToken)
            .Where(d => forbiddenDiagnosticIds.Contains(d.Id))
            .ToList();

        forbidden.Should().BeEmpty(
            "generated code should compile without any of the forbidden diagnostics, but found:\n" +
            string.Join("\n---\n", forbidden.Select(e => $"  - {e.Id} ({e.Severity}): {e.GetMessage()} at {e.Location}")));

        var errors = outputCompilation.GetDiagnostics(cancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty(
            "generated code should compile without errors, but found:\n" +
            string.Join("\n---\n", errors.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        return VerifyDriver(driver);
    }

    /// <summary>
    /// Compiles <paramref name="options"/>' source plus the real generated output into an in-memory
    /// assembly, loads it, and invokes <paramref name="methodName"/> on the static type named
    /// <paramref name="typeName"/> - a real end-to-end execution of a dispatched generated plan
    /// (module initializer registration, template output, and the runtime pipeline all together),
    /// not just a compile-correctness check. PR #11 review caught that no test exercised this: every
    /// existing test either snapshots/compiles generated source without running it (<see cref="Verify"/>),
    /// or exercises the runtime pipeline against a hand-written test double
    /// (<c>UniqueValueResolverTests</c>, <c>CollectionPlanCacheDispatchTests</c> in
    /// <c>Compono.Tests</c>), never a real generated collection plan actually dispatched through
    /// <c>CollectionPlanCache&lt;T&gt;</c>.
    /// </summary>
    /// <returns>
    /// <paramref name="methodName"/>'s return value - the caller casts to whatever a `public static`
    /// entry-point method in the compiled source returns. A thrown exception (e.g. the
    /// <see cref="CompositionException"/> a retry-exhaustion failure produces) surfaces wrapped in a
    /// <see cref="TargetInvocationException"/>, per ordinary reflection-invoke semantics - unwrap via
    /// <c>.InnerException</c>.
    /// </returns>
    internal static object? CompileAndExecute(
        CodeGenerationOptions options, string typeName, string methodName, CancellationToken cancellationToken = default)
    {
        var (driver, originalCompilation) = GenerateFromSource(options, cancellationToken);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty(
            "code should be generated without errors, but found:\n" +
            string.Join("\n---\n", result.Diagnostics.Select(e => $"  - {e.Id}: {e.GetMessage()} at {e.Location}")));

        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var reparsedTrees = result.GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), (CSharpParseOptions)parseOptions, tree.FilePath))
            .ToArray();

        var outputCompilation = originalCompilation.AddSyntaxTrees(reparsedTrees);

        using var peStream = new MemoryStream();
        var emitResult = outputCompilation.Emit(peStream, cancellationToken: cancellationToken);

        emitResult.Success.Should().BeTrue(
            "generated code should compile and emit without errors, but found:\n" +
            string.Join("\n---\n", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"  - {d.Id}: {d.GetMessage()}")));

        peStream.Position = 0;
        var assembly = Assembly.Load(peStream.ToArray());

        var type = assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Type '{typeName}' not found in the compiled assembly.");
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Public static method '{methodName}' not found on '{typeName}'.");

        return method.Invoke(null, null);
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
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(options.MSBuildProperties);
        var driver = ((GeneratorDriver)CSharpGeneratorDriver.Create(generator)).WithUpdatedAnalyzerConfigOptions(optionsProvider);
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
