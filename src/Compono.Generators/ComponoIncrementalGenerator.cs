using Compono.Generators.Discovery;
using Compono.Generators.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Compono.Generators;

/// <summary>
/// Discovers types needing a generated <c>ICompositionPlan&lt;T&gt;</c> — via
/// <c>Composer.Create&lt;T&gt;()</c> call sites and <c>[Composable]</c> opt-in requests — and emits
/// a plan for each, per <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c> and
/// <c>docs/plans/0001-milestone-1-source-generation-foundation.md</c>.
/// </summary>
[Generator]
internal sealed class ComponoIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var callSiteTypes = context.SyntaxProvider
            .CreateSyntaxProvider(CreateInvocationDiscovery.IsCandidate, CreateInvocationDiscovery.Transform)
            .WithTrackingName(TrackingNames.CreateInvocations)
            .Where(static types => types is not null)
            // Each call site yields its whole transitive closure (Phase 1), not just the requested
            // type - flatten before the rest of the pipeline dedupes/emits per type.
            .SelectMany(static (types, _) => types!.Value)
            .WithTrackingName(TrackingNames.CreateInvocationsNotNull);

        // [Composable] on a type declaration (Phase 2) - the AttributeUsage on ComposableAttribute
        // restricts placement to classes/structs, so TypeDeclarationSyntax is the only shape that
        // can reach the transform.
        var composableTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComposableAttributeDiscovery.AttributeMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                ComposableAttributeDiscovery.TransformTypeLevel)
            .WithTrackingName(TrackingNames.ComposableTypes)
            .SelectMany(static (types, _) => types)
            .WithTrackingName(TrackingNames.ComposableTypesFlattened);

        // [assembly: Composable(typeof(...))] (Phase 2) - assembly-level attributes aren't
        // reachable through ForAttributeWithMetadataName (it only matches attributes on
        // declarations), so this form gets its own syntax provider over `[assembly: ...]` lists.
        var assemblyComposableTypes = context.SyntaxProvider
            .CreateSyntaxProvider(ComposableAttributeDiscovery.IsAssemblyCandidate, ComposableAttributeDiscovery.TransformAssemblyLevel)
            .WithTrackingName(TrackingNames.AssemblyComposables)
            .Where(static types => types is not null)
            .SelectMany(static (types, _) => types!.Value)
            .WithTrackingName(TrackingNames.AssemblyComposablesNotNull);

        // All discovery paths produce equivalent plan-generation requests - merge before deduping
        // so a type discovered via both a call site and [Composable] still gets exactly one plan.
        var discoveredTypes = callSiteTypes.Collect()
            .Combine(composableTypes.Collect())
            .Combine(assemblyComposableTypes.Collect())
            .WithTrackingName(TrackingNames.DiscoveredCollected)
            .SelectMany(static (types, _) =>
            {
                var ((callSites, composables), assemblyComposables) = types;

                // Two discoveries can share the same emission identity (Namespace/TypeName/
                // FullyQualifiedName - what AddSource's hint name and PlanCache<T> slot actually
                // key on) while still being structurally unequal records - e.g. composing both
                // Box<string> and Box<string?> at two different call sites: FullyQualifiedFormat
                // erases the top-level nullable annotation (identical hint name either way), but
                // Roslyn substitutes Box<T>'s constructor parameter's own NullableAnnotation
                // differently for each, so their DiscoveredTypeInfo.Parameters differ. A plain
                // `.Distinct()` (full structural equality) would keep both, and AddSource throws on
                // the resulting duplicate hint name - a whole-generator-run crash, not a diagnostic.
                // Grouping by emission identity and keeping the first-discovered entry guarantees
                // exactly one AddSource call per hint name regardless of what a later duplicate
                // disagreed about.
                return callSites.Concat(composables).Concat(assemblyComposables)
                    .GroupBy(static type => (type.Namespace, type.TypeName, type.FullyQualifiedName))
                    .Select(static group => group.First());
            })
            .WithTrackingName(TrackingNames.DiscoveredDistinct);

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
    public const string ComposableTypes = "ComposableTypes";
    public const string ComposableTypesFlattened = "ComposableTypes.Flattened";
    public const string AssemblyComposables = "AssemblyComposables";
    public const string AssemblyComposablesNotNull = "AssemblyComposables.NotNull";
    public const string DiscoveredCollected = "Discovered.Collected";
    public const string DiscoveredDistinct = "Discovered.Distinct";
}
