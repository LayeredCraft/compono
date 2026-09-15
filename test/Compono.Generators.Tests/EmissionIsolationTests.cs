using Compono.Generators.Diagnostics;
using Compono.Generators.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Tests;

public sealed class EmissionIsolationTests
{
    [Fact]
    public void TryEmit_ExecutesDelegate_WhenEmissionSucceeds()
    {
        var didEmit = false;

        var result = Run(context =>
            EmissionIsolation.TryEmit(context, "probe artifact", "ProbeItem", () => didEmit = true));

        didEmit.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void TryEmit_ReportsCmp0050_WhenEmissionThrows()
    {
        var result = Run(context =>
            EmissionIsolation.TryEmit(
                context,
                "probe artifact",
                "ProbeItem",
                () => throw new InvalidOperationException("boom")));

        var diagnostic = result.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("CMP0050");
        diagnostic.Descriptor.Should().BeSameAs(DiagnosticDescriptors.GeneratedSourceEmissionFailed);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.Should().Be(Location.None);
        diagnostic.GetMessage().Should().Be(
            "Compono could not emit generated probe artifact for 'ProbeItem' due to an unexpected " +
            "internal error (InvalidOperationException: boom). Generated output for this item is unavailable.");
    }

    [Fact]
    public void TryEmit_PropagatesOperationCanceledException_WithoutReportingCmp0050()
    {
        var cancellation = new OperationCanceledException("cancelled");
        OperationCanceledException? observedCancellation = null;

        var result = Run(context =>
        {
            try
            {
                EmissionIsolation.TryEmit(context, "probe artifact", "ProbeItem", () => throw cancellation);
            }
            catch (OperationCanceledException ex)
            {
                observedCancellation = ex;
            }
        });

        observedCancellation.Should().BeSameAs(cancellation);
        result.Diagnostics.Should().BeEmpty();
    }

    private static GeneratorDriverRunResult Run(Action<SourceProductionContext> action)
    {
        var generator = new SingleActionProbeGenerator(action);
        var compilation = CSharpCompilation.Create(
            "EmissionIsolationProbe",
            [CSharpSyntaxTree.ParseText(
                "internal sealed class Input {}",
                cancellationToken: TestContext.Current.CancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        return driver
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult();
    }

    private sealed class SingleActionProbeGenerator : IIncrementalGenerator
    {
        private readonly Action<SourceProductionContext> _action;

        public SingleActionProbeGenerator(Action<SourceProductionContext> action)
        {
            _action = action;
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, (productionContext, _) => _action(productionContext));
        }
    }
}
