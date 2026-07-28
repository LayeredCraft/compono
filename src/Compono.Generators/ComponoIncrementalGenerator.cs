using Compono.Generators.Diagnostics;
using Compono.Generators.Discovery;
using Compono.Generators.Emitters;
using Compono.Generators.Models;
using Compono.Generators.Types;
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
                // differently for each, so their DiscoveredTypeInfo.Parameters differ. Silently
                // keeping "whichever one was discovered first" (an earlier version of this fix) only
                // stops the duplicate-hint-name crash - it doesn't stop the *other* call site from
                // silently getting the wrong Nullability for its own request, and which one wins
                // becomes dependent on arbitrary discovery order. There's no value that's correct for
                // both requests: Box<string> gets exactly one generated plan, so if two call sites
                // genuinely disagree about it, that's reported (CMP0010) rather than guessed - the
                // same "diagnose, don't guess" rule CMP0001 (ambiguous constructor) already follows.
                // A group can still legitimately contain more than one *structurally identical* entry
                // (the same type discovered via both a call site and [Composable], say) - that's not
                // a conflict, just redundant discovery of the same request, and still collapses to one.
                return callSites.Concat(composables).Concat(assemblyComposables)
                    .GroupBy(static type => (type.Namespace, type.TypeName, type.FullyQualifiedName))
                    .SelectMany(static group =>
                    {
                        var distinct = group.Distinct().ToArray();

                        if (distinct.Length == 1)
                            return distinct;

                        // A failure already carries its own correct diagnostic (CMP0001,
                        // CMP0002, ...) at its own request-site Location - DiagnosticInfo.Equals
                        // includes Location, so the same failing type reached from two different
                        // Create<T>() call sites naturally produces two "distinct" failure entries
                        // even though neither is wrong. Those must always pass through as-is
                        // (reported at both locations, exactly like before conflict-detection
                        // existed) rather than being folded into a synthetic, locationless
                        // CMP0010 that would erase the real diagnostics entirely.
                        var failures = distinct.Where(static t => t.Diagnostics.Count > 0).ToArray();

                        if (failures.Length > 0)
                            return failures;

                        // Every surviving entry succeeded, but disagrees on composition metadata
                        // (e.g. differing Nullability from Box<string> vs Box<string?>) - there's
                        // no value that's correct for every discovery sharing this one emitted
                        // plan, so report it instead of guessing, the same "diagnose an ambiguity,
                        // don't guess" rule CMP0001 already follows.
                        var (@namespace, typeName, fullyQualifiedName) = group.Key;

                        return new DiscoveredTypeInfo[]
                        {
                            new(
                                @namespace,
                                typeName,
                                fullyQualifiedName,
                                EquatableArray<ConstructorParameterInfo>.Empty,
                                EquatableArray<RequiredMemberInfo>.Empty,
                                new[] { new DiagnosticInfo(DiagnosticDescriptors.ConflictingCompositionMetadata, null, fullyQualifiedName) }
                                    .ToEquatableArray()),
                        };
                    });
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
