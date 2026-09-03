using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

/// <summary>
/// ADR-0005 requires <c>.WithTrackingName(...)</c> on every named incremental pipeline stage "so
/// incrementality (cache-hit behavior) can be asserted in tests later" - no test ever did, tracked
/// as open work since PLAN-0001 first added <see cref="TrackingNames"/> (2026-09-03, PLAN-0061 Phase
/// 1). This proves the promised invariant for one representative stage, not full incremental-caching
/// coverage of every stage <see cref="TrackingNames"/> names.
/// </summary>
public sealed class IncrementalCachingTests
{
    [Fact]
    public void ComposableTypesStage_ReportsACacheHit_WhenAnUnrelatedSourceEditFollows()
    {
        const string original = """
            namespace TestNamespace;

            [Compono.Composable]
            public sealed class Customer
            {
                public Customer(string firstName)
                {
                    FirstName = firstName;
                }

                public string FirstName { get; }
            }
            """;

        // An edit with no possible effect on TestNamespace.Customer's own [Composable] declaration -
        // a brand-new, entirely separate type appended after it. Roslyn's incremental generator
        // driver diffs the new compilation's syntax trees against the previous run's and reuses
        // per-node results for anything structurally unaffected, regardless of whether the new tree
        // was produced via an incremental text edit or a fresh parse (both go through the same
        // tree-diffing on the driver side) - this is exactly the scenario TrackingNames' own doc
        // comment describes wanting to assert.
        const string edited = original + """

            namespace TestNamespace;

            public sealed class Unrelated;
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var originalTree = CSharpSyntaxTree.ParseText(original, parseOptions, "Program.cs", cancellationToken: TestContext.Current.CancellationToken);

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];
        var originalCompilation = CSharpCompilation.Create("IncrementalCachingTestsAssembly", [originalTree], references, compilationOptions);

        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driverOptions = new GeneratorDriverOptions(disabledOutputs: default, trackIncrementalGeneratorSteps: true);
        var driver = ((GeneratorDriver)CSharpGeneratorDriver.Create([generator], driverOptions: driverOptions))
            .RunGenerators(originalCompilation, TestContext.Current.CancellationToken);

        var editedTree = originalTree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(edited));
        var editedCompilation = originalCompilation.ReplaceSyntaxTree(originalTree, editedTree);

        var secondRunDriver = driver.RunGenerators(editedCompilation, TestContext.Current.CancellationToken);
        var secondResult = secondRunDriver.GetRunResult();

        var steps = secondResult.Results.Single().TrackedSteps[TrackingNames.ComposableTypes];

        steps.Should().NotBeEmpty("the [Composable]-attributed type should still produce a tracked step on the second run");

        // Observed, not assumed: appending an unrelated type changes the syntax tree object, so this
        // stage's transform does re-run (IncrementalStepRunReason.New/.Modified would mean the
        // driver treated it as genuinely new/changed input) - but it recomputes the *same* record
        // value for TestNamespace.Customer, which Roslyn reports as .Unchanged, not .Cached (.Cached
        // is reserved for a node whose transform didn't even need to re-run at all, e.g. an untouched
        // file in a multi-file compilation). Both .Unchanged and .Cached are the two "no downstream
        // recomputation needed" reasons ADR-0005's "cache-hit behavior" comment means - asserting
        // only .Cached here would fail for the wrong reason and not actually prove the invariant.
        steps.SelectMany(step => step.Outputs).Should().OnlyContain(
            output => output.Reason == IncrementalStepRunReason.Cached || output.Reason == IncrementalStepRunReason.Unchanged,
            "an edit with no effect on the [Composable]-attributed type's own syntax should let this stage's result compare equal " +
            "across runs (Cached or Unchanged), not be recomputed as genuinely new or modified - proving real incremental " +
            "cache-hit behavior, not merely that the generator ran twice");
    }
}
