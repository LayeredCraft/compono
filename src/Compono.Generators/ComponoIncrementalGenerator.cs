using Compono.Generators.Discovery;
using Compono.Generators.Emitters;
using Microsoft.CodeAnalysis;

namespace Compono.Generators;

/// <summary>
/// Discovers <c>Composer.Create&lt;T&gt;()</c> call sites and emits a generated
/// <c>ICompositionPlan&lt;T&gt;</c> for each discovered type, per
/// <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c> and
/// <c>docs/plans/0001-milestone-1-source-generation-foundation.md</c>.
/// </summary>
[Generator]
internal sealed class ComponoIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var discoveredTypes = context.SyntaxProvider
            .CreateSyntaxProvider(CreateInvocationDiscovery.IsCandidate, CreateInvocationDiscovery.Transform)
            .Where(static type => type is not null)
            .Select(static (type, _) => type!)
            .Collect()
            .SelectMany(static (types, _) => types.Distinct());

        context.RegisterSourceOutput(discoveredTypes, static (productionContext, type) =>
        {
            foreach (var diagnostic in type.Diagnostics)
                diagnostic.Report(productionContext);

            if (type.Diagnostics.Count > 0)
                return;

            CompositionPlanEmitter.Generate(productionContext, type);
        });
    }
}
