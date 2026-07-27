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
            .WithTrackingName(TrackingNames.CreateInvocations)
            .Where(static types => types is not null)
            // Each call site now yields its whole transitive closure (Phase 1), not just the
            // requested type - flatten before the rest of the pipeline dedupes/emits per type.
            .SelectMany(static (types, _) => types!.Value)
            .WithTrackingName(TrackingNames.CreateInvocationsNotNull)
            .Collect()
            .WithTrackingName(TrackingNames.CreateInvocationsCollected)
            .SelectMany(static (types, _) => types.Distinct())
            .WithTrackingName(TrackingNames.CreateInvocationsDistinct);

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

/// <summary>
/// <c>.WithTrackingName(...)</c> values for <see cref="ComponoIncrementalGenerator"/>'s pipeline
/// stages, per <c>docs/adr/0005-generator-implementation-conventions.md</c> - named up front so
/// incremental-caching tests can locate a stage in <c>GeneratorDriverRunResult.TrackedSteps</c> by
/// name instead of by fragile positional/structural matching.
/// </summary>
internal static class TrackingNames
{
    public const string CreateInvocations = "CreateInvocations";
    public const string CreateInvocationsNotNull = "CreateInvocations.NotNull";
    public const string CreateInvocationsCollected = "CreateInvocations.Collected";
    public const string CreateInvocationsDistinct = "CreateInvocations.Distinct";
}
