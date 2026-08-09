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
        var callSiteResults = context.SyntaxProvider
            .CreateSyntaxProvider(CreateInvocationDiscovery.IsCandidate, CreateInvocationDiscovery.Transform)
            .WithTrackingName(TrackingNames.CreateInvocations)
            .Where(static result => result is not null)
            .Select(static (result, _) => result!)
            .WithTrackingName(TrackingNames.CreateInvocationsNotNull);

        // [Composable] on a type declaration (Phase 2) - the AttributeUsage on ComposableAttribute
        // restricts placement to classes/structs, so TypeDeclarationSyntax is the only shape that
        // can reach the transform.
        var composableResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComposableAttributeDiscovery.AttributeMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                ComposableAttributeDiscovery.TransformTypeLevel)
            .WithTrackingName(TrackingNames.ComposableTypes);

        // [assembly: Composable(typeof(...))] (Phase 2) - assembly-level attributes aren't
        // reachable through ForAttributeWithMetadataName (it only matches attributes on
        // declarations), so this form gets its own syntax provider over `[assembly: ...]` lists.
        var assemblyComposableResults = context.SyntaxProvider
            .CreateSyntaxProvider(ComposableAttributeDiscovery.IsAssemblyCandidate, ComposableAttributeDiscovery.TransformAssemblyLevel)
            .WithTrackingName(TrackingNames.AssemblyComposables)
            .Where(static result => result is not null)
            .Select(static (result, _) => result!)
            .WithTrackingName(TrackingNames.AssemblyComposablesNotNull);

        // [Compose] on a test method (Compono.XunitV3, Milestone 4 Phase 1) - a type reached only as
        // one of these methods' own parameters has no textual call site for CreateInvocationDiscovery
        // to match, so it needs this dedicated discovery path. Matches on the non-generic
        // ComposeAttribute metadata name only - ForAttributeWithMetadataName matches an attribute
        // usage's own attribute-class metadata name, not a base type's, so [Compose<TProfile>] (whose
        // attribute class metadata name is the distinct, arity-suffixed "ComposeAttribute`1") needs
        // its own separately-registered provider below, not this one (PR #23 review).
        var composeMethodResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComposeMethodDiscovery.AttributeMetadataName,
                static (node, _) => node is MethodDeclarationSyntax,
                ComposeMethodDiscovery.TransformMethod)
            .WithTrackingName(TrackingNames.ComposeMethods);

        // [Compose<TProfile>] specifically - ForAttributeWithMetadataName matches an attribute
        // usage against its own attribute class's exact metadata name, not a base type's, so the
        // non-generic registration above never sees [Compose<TProfile>] (whose attribute class
        // metadata name is the arity-suffixed "ComposeAttribute`1", not "ComposeAttribute"). Same
        // transform, since ComposeMethodDiscovery.TransformMethod only cares about the attributed
        // method's own parameters, not which ComposeAttribute form triggered it.
        var composeGenericMethodResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComposeMethodDiscovery.GenericAttributeMetadataName,
                static (node, _) => node is MethodDeclarationSyntax,
                ComposeMethodDiscovery.TransformMethod)
            .WithTrackingName(TrackingNames.ComposeGenericMethods);

        // [Compose<TProfile, TConfig>] specifically (ADR-0036) - same reasoning as the arity-1
        // registration immediately above: ForAttributeWithMetadataName matches only the exact,
        // arity-suffixed attribute class metadata name ("ComposeAttribute`2"), invisible to either
        // of the other two registrations. Same transform - TransformMethod only cares about the
        // attributed method's own parameters, not which ComposeAttribute arity triggered it.
        var composeTwoTypeParameterMethodResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComposeMethodDiscovery.TwoTypeParameterAttributeMetadataName,
                static (node, _) => node is MethodDeclarationSyntax,
                ComposeMethodDiscovery.TransformMethod)
            .WithTrackingName(TrackingNames.ComposeTwoTypeParameterMethods);

        // All three ComposeMethodDiscovery registrations above (non-generic, arity-1, arity-2) feed
        // the exact same discovery logic - merge them into one provider here so every consumer below
        // treats "a [Compose]/[Compose<TProfile>]/[Compose<TProfile, TConfig>]-attributed method" as
        // a single source, same as CreateInvocations/Composable/AssemblyComposable already do for
        // their own multi-syntax-form splits.
        var composeMethodResultsAll = composeMethodResults.Collect()
            .Combine(composeGenericMethodResults.Collect())
            .Combine(composeTwoTypeParameterMethodResults.Collect())
            .SelectMany(static (results, _) => results.Left.Left.Concat(results.Left.Right).Concat(results.Right))
            .WithTrackingName(TrackingNames.ComposeMethodsAll);

        // Each discovery result carries its own transitive closure (Types) alongside every closed
        // collection shape reached within it (Collections, ADR-0014) - flatten both
        // before the rest of the pipeline dedupes/emits per type/collection.
        var callSiteTypes = callSiteResults.SelectMany(static (result, _) => result.Types)
            .WithTrackingName(TrackingNames.CreateInvocationsTypes);
        var composableTypes = composableResults.SelectMany(static (result, _) => result.Types)
            .WithTrackingName(TrackingNames.ComposableTypesFlattened);
        var assemblyComposableTypes = assemblyComposableResults.SelectMany(static (result, _) => result.Types)
            .WithTrackingName(TrackingNames.AssemblyComposablesTypes);
        var composeMethodTypes = composeMethodResultsAll.SelectMany(static (result, _) => result.Types)
            .WithTrackingName(TrackingNames.ComposeMethodsTypes);

        var discoveredCollections = callSiteResults.SelectMany(static (result, _) => result.Collections)
            .Collect()
            .Combine(composableResults.SelectMany(static (result, _) => result.Collections).Collect())
            .Combine(assemblyComposableResults.SelectMany(static (result, _) => result.Collections).Collect())
            .Combine(composeMethodResultsAll.SelectMany(static (result, _) => result.Collections).Collect())
            .WithTrackingName(TrackingNames.DiscoveredCollectionsCollected)
            .SelectMany(static (collections, _) =>
            {
                var (((callSites, composables), assemblyComposables), composeMethods) = collections;

                // The same closed collection type can legitimately be reached from more than one
                // discovery path (or more than one member site) - collapse to one emitted plan per
                // distinct closed collection type. Two discoveries of the *same* closed type that
                // disagree on element/key nullability (e.g. a List<string> member and a List<string?>
                // member both reaching List<string>) have no value that's correct for both - reported
                // as CMP0011 instead of silently picking whichever discovery happened to come first,
                // mirroring DiscoveredTypeInfo's CMP0010 conflict check for ordinary composable types.
                return callSites.Concat(composables).Concat(assemblyComposables).Concat(composeMethods)
                    .GroupBy(static collection => collection.FullyQualifiedCollectionTypeName)
                    .SelectMany(static group =>
                    {
                        var distinct = group.Distinct().ToArray();

                        if (distinct.Length == 1)
                            return distinct;

                        // Mirrors the ordinary-type merge below: an entry that already carries its own
                        // diagnostic (e.g. CMP0012, an inaccessible element/key type) has a real,
                        // actionable failure at its own request-site Location - DiagnosticInfo.Equals
                        // includes Location, so the same failing collection reached from two different
                        // call sites naturally produces two "distinct" failure entries even though
                        // neither is wrong. PR #11 review caught that this branch previously folded
                        // those straight into a synthetic, locationless CMP0011 "conflicting
                        // nullability" diagnostic instead - erasing the real, more specific failures
                        // entirely. Preserve and report them as-is instead of treating this as a
                        // metadata conflict.
                        var failures = distinct.Where(static c => c.Diagnostics.Count > 0).ToArray();

                        if (failures.Length > 0)
                            return failures;

                        return new DiscoveredCollectionInfo[]
                        {
                            new(
                                distinct[0].Shape,
                                group.Key,
                                distinct[0].ElementFullyQualifiedTypeName,
                                distinct[0].ElementIsNullable,
                                distinct[0].KeyFullyQualifiedTypeName,
                                distinct[0].KeyIsNullable,
                                new[] { new DiagnosticInfo(DiagnosticDescriptors.ConflictingCollectionMetadata, null, group.Key) }
                                    .ToEquatableArray()),
                        };
                    });
            })
            .WithTrackingName(TrackingNames.DiscoveredCollectionsDistinct);

        context.RegisterSourceOutput(discoveredCollections, static (productionContext, collection) =>
        {
            foreach (var diagnostic in collection.Diagnostics)
                diagnostic.Report(productionContext);

            if (collection.Diagnostics.Count > 0)
                return;

            CollectionPlanEmitter.Generate(productionContext, collection);
        });

        // All discovery paths produce equivalent plan-generation requests - merge before deduping
        // so a type discovered via both a call site and [Composable] still gets exactly one plan.
        var discoveredTypes = callSiteTypes.Collect()
            .Combine(composableTypes.Collect())
            .Combine(assemblyComposableTypes.Collect())
            .Combine(composeMethodTypes.Collect())
            .WithTrackingName(TrackingNames.DiscoveredCollected)
            .SelectMany(static (types, _) =>
            {
                var (((callSites, composables), assemblyComposables), composeMethods) = types;

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
                return callSites.Concat(composables).Concat(assemblyComposables).Concat(composeMethods)
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
    public const string CreateInvocationsTypes = "CreateInvocations.Types";
    public const string ComposableTypes = "ComposableTypes";
    public const string ComposableTypesFlattened = "ComposableTypes.Flattened";
    public const string AssemblyComposables = "AssemblyComposables";
    public const string AssemblyComposablesNotNull = "AssemblyComposables.NotNull";
    public const string AssemblyComposablesTypes = "AssemblyComposables.Types";
    public const string ComposeMethods = "ComposeMethods";
    public const string ComposeGenericMethods = "ComposeMethods.Generic";
    public const string ComposeTwoTypeParameterMethods = "ComposeMethods.TwoTypeParameter";
    public const string ComposeMethodsAll = "ComposeMethods.All";
    public const string ComposeMethodsTypes = "ComposeMethods.Types";
    public const string DiscoveredCollected = "Discovered.Collected";
    public const string DiscoveredDistinct = "Discovered.Distinct";
    public const string DiscoveredCollectionsCollected = "DiscoveredCollections.Collected";
    public const string DiscoveredCollectionsDistinct = "DiscoveredCollections.Distinct";
}
