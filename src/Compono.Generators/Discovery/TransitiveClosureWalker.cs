using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Walks a discovered type's constructor parameters recursively, per Phase 1 of
/// docs/plans/0001-milestone-1-source-generation-foundation.md: a parameter type only gets its own
/// generated plan if <see cref="LeafTypeClassifier"/> says it's eligible for generated composition
/// (a concrete, non-abstract, non-delegate/enum/built-in type) - everything else is left as a bare
/// <c>context.Resolve&lt;TParam&gt;()</c> call for a future <c>ICompositionContext</c>/provider to
/// handle. A concrete type that IS eligible but fails constructor selection (ambiguous, no
/// accessible constructor, unsupported parameter kind, unassigned required members) is diagnosed at
/// the original <c>Composer.Create&lt;T&gt;()</c> call site rather than silently left as
/// <c>Resolve&lt;TParam&gt;()</c> - that would hide an invalid generated graph and turn a
/// compile-time composition failure into a runtime one.
/// <para>
/// A member type recognized as one of ADR-0013's five closed collection shapes
/// (<see cref="CollectionWellKnownTypes"/>) is never walked as an ordinary composable type - no
/// constructor selection is attempted against, say, <c>List&lt;Address&gt;</c> itself. Instead it's
/// recorded as a <see cref="DiscoveredCollectionInfo"/> needing its own generated collection plan
/// (ADR-0014), and its element type (and key type, for <c>Dictionary</c>) feeds
/// back into this same eligibility walk - so a composable element type still gets its own plan, a
/// provider-resolved element type stays a bare <c>Resolve&lt;TElement&gt;()</c> call, and a nested
/// collection element type (<c>List&lt;List&lt;Address&gt;&gt;</c>) recurses through this same
/// collection-shape check again.
/// </para>
/// <para>
/// The root type itself goes through this exact same classification before anything else - a
/// provider-resolved root (<c>Composer.Create&lt;int&gt;()</c>, <c>Composer.Create&lt;Guid&gt;()</c>)
/// needs no generated plan at all (stage 7's built-in providers satisfy it directly at runtime,
/// per ADR-0014), and a collection-shaped root
/// (<c>Composer.Create&lt;List&lt;Address&gt;&gt;()</c>) is recorded as a
/// <see cref="DiscoveredCollectionInfo"/> exactly like a collection reached as a nested member,
/// not walked as an ordinary composable type. Only a genuinely composable root reaches constructor
/// selection - PR #11 review caught that an earlier version of this method skipped this check for
/// the root, so a provider-resolved/collection root either failed to compile (ambiguous
/// constructor) or silently generated a dead, always-default-value plan that was never actually
/// reached at runtime.
/// </para>
/// </summary>
internal static class TransitiveClosureWalker
{
    public static TransitiveClosureResult Walk(ITypeSymbol rootType, Compilation compilation, LocationInfo? location)
    {
        var wellKnownTypes = WellKnownTypes.WellKnownTypes.GetOrCreate(compilation);
        var collectionTypes = CollectionWellKnownTypes.GetOrCreate(compilation);

        // IncludeNullability, not Default - Default treats Box<string> and Box<string?> as the
        // same symbol (nullable annotations don't affect its notion of symbol identity), which
        // would silently drop the second variant here before it ever became its own
        // DiscoveredTypeInfo - i.e. before ComponoIncrementalGenerator's cross-discovery conflict
        // check (CMP0010) ever got a chance to see there were two disagreeing entries to compare.
        // With IncludeNullability, both variants get walked and each produces its own entry, so a
        // real conflict between them surfaces through that existing check instead of being erased
        // one layer upstream of it.
        var visitedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.IncludeNullability);
        var visitedCollections = new HashSet<ITypeSymbol>(SymbolEqualityComparer.IncludeNullability);
        var results = new List<DiscoveredTypeInfo>();
        var collections = new List<DiscoveredCollectionInfo>();
        var queue = new Queue<(INamedTypeSymbol Type, string? Path)>();

        EnqueueRoot(rootType, compilation, location, wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);

        while (queue.Count > 0)
        {
            var (type, path) = queue.Dequeue();
            var (info, constructor, requiredMemberTypes) = Analyze(type, compilation, location, path);
            results.Add(info);

            if (constructor is null)
                continue;

            foreach (var parameter in constructor.Parameters)
            {
                EnqueueMember(
                    parameter.Type, parameter.Name, type, path, compilation, location,
                    wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);
            }

            foreach (var (memberType, memberName) in requiredMemberTypes)
            {
                EnqueueMember(
                    memberType, memberName, type, path, compilation, location,
                    wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);
            }
        }

        return new TransitiveClosureResult(results.ToEquatableArray(), collections.ToEquatableArray());
    }

    // Mirrors EnqueueMember's classification (collection shape -> provider-resolved leaf ->
    // genuinely composable type), but for the root, which has no parent/member name to build a
    // diagnostic path from - the root's own type name stands in for both when recursing into a
    // collection root's element/key type(s).
    private static void EnqueueRoot(
        ITypeSymbol rootType,
        Compilation compilation,
        LocationInfo? location,
        WellKnownTypes.WellKnownTypes wellKnownTypes,
        CollectionWellKnownTypes collectionTypes,
        HashSet<INamedTypeSymbol> visitedTypes,
        HashSet<ITypeSymbol> visitedCollections,
        List<DiscoveredCollectionInfo> collections,
        List<DiscoveredTypeInfo> results,
        Queue<(INamedTypeSymbol Type, string? Path)> queue)
    {
        if (collectionTypes.TryClassify(rootType, out var shape))
        {
            if (!visitedCollections.Add(rootType))
                return;

            var collectionInfo = ToDiscoveredCollectionInfo(rootType, shape, compilation, location);
            collections.Add(collectionInfo);

            if (collectionInfo.Diagnostics.Count > 0)
                return;

            EnqueueMember(
                shape.ElementType, "element", rootType, null, compilation, location,
                wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);

            if (shape.KeyType is { } keyType)
            {
                EnqueueMember(
                    keyType, "key", rootType, null, compilation, location,
                    wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);
            }

            return;
        }

        if (LeafTypeClassifier.IsRuntimeProviderResolved(rootType, wellKnownTypes))
            return;

        // Guaranteed INamedTypeSymbol at this point: ComposedTypeAnalyzer only reaches Walk with a
        // non-collection root after its own INamedTypeSymbol check already passed (CMP0006 rejects
        // anything else, e.g. an unsupported array rank, before Walk is ever called) - the pattern
        // match here is a defensive narrowing, not a case this method expects to actually decline.
        if (rootType is not INamedTypeSymbol namedRoot)
            return;

        visitedTypes.Add(namedRoot);
        queue.Enqueue((namedRoot, null));
    }

    private static void EnqueueMember(
        ITypeSymbol memberType,
        string memberName,
        ITypeSymbol parentType,
        string? parentPath,
        Compilation compilation,
        LocationInfo? location,
        WellKnownTypes.WellKnownTypes wellKnownTypes,
        CollectionWellKnownTypes collectionTypes,
        HashSet<INamedTypeSymbol> visitedTypes,
        HashSet<ITypeSymbol> visitedCollections,
        List<DiscoveredCollectionInfo> collections,
        List<DiscoveredTypeInfo> results,
        Queue<(INamedTypeSymbol Type, string? Path)> queue)
    {
        var childPath = parentPath is null ? $"{parentType.Name}.{memberName}" : $"{parentPath}.{memberName}";

        if (collectionTypes.TryClassify(memberType, out var shape))
        {
            if (!visitedCollections.Add(memberType))
                return;

            var collectionInfo = ToDiscoveredCollectionInfo(memberType, shape, compilation, location);
            collections.Add(collectionInfo);

            if (collectionInfo.Diagnostics.Count > 0)
                return;

            EnqueueMember(
                shape.ElementType, "element", parentType, childPath, compilation, location,
                wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);

            if (shape.KeyType is { } keyType)
            {
                EnqueueMember(
                    keyType, "key", parentType, childPath, compilation, location,
                    wellKnownTypes, collectionTypes, visitedTypes, visitedCollections, collections, results, queue);
            }

            return;
        }

        if (memberType is not INamedTypeSymbol namedType)
        {
            // An array/pointer/function-pointer member has no constructor for ConstructorSelector to
            // select and no ordinary-composable-type fallback (unlike a rejected generic collection
            // shape, e.g. HashSet<dynamic>, which is still an INamedTypeSymbol and so still reaches
            // ConstructorSelector's own CMP0001 below via the ordinary path) - without this, the
            // containing type's generated plan still compiles clean, emitting
            // `context.Resolve<TMember>(...)` for a type nothing will ever register a plan for, and
            // only fails at runtime with a generic "no plan registered" message. Same diagnostic
            // (CMP0006) ComposedTypeAnalyzer already reports for the equivalent *root* case - PR #11
            // review caught that only the member case was missing it, confirmed directly before
            // fixing (composer.Create<dynamic[]>() already reported CMP0006 correctly; a dynamic[]
            // constructor parameter did not).
            //
            // `dynamic` itself (not `dynamic[]`) is also not an INamedTypeSymbol, but it's not one of
            // these permanently-unsatisfiable shapes - `Resolve<dynamic>()` is legal and is the
            // established, intentional way to leave a member for a future registration/semantic
            // provider to satisfy at runtime (dynamic erases to object, so virtually anything could
            // claim it). A second PR #11 review caught that reporting CMP0006 unconditionally for
            // every non-INamedTypeSymbol member also caught `dynamic` itself, silently turning a
            // previously-valid provider-resolved graph into a hard generator error - confirmed
            // directly. Only a genuinely unsatisfiable shape (array, pointer, function pointer) gets
            // the diagnostic; `dynamic` (and anything else that isn't one of those three) falls
            // through exactly as it did before this diagnostic existed.
            if (memberType is IArrayTypeSymbol or IPointerTypeSymbol or IFunctionPointerTypeSymbol)
            {
                results.AddRange(ComposedTypeAnalyzer.TypeArgumentFailure(
                    DiagnosticDescriptors.UnsupportedTypeArgumentShape, memberType, location).Types);
            }

            return;
        }

        if (LeafTypeClassifier.IsProviderResolved(namedType, wellKnownTypes))
            return;

        if (!visitedTypes.Add(namedType))
            return;

        queue.Enqueue((namedType, childPath));
    }

    // Every generated collection plan is emitted as a `file`-scoped top-level type
    // (coding-standards.md's "Generated code" section) - it is never nested inside the type that
    // reached this collection, even when the collection was discovered from a call site that could
    // itself see a private/protected element or key type there. IsSymbolAccessibleWithin against the
    // whole compilation assembly is the same check ConstructorSelector/RequiredMemberCollector
    // already use for constructor/required-member accessibility - a private or protected member type
    // is never "accessible from anywhere in the assembly," which is exactly the accessibility domain
    // a top-level generated type actually gets. PR #11 review caught this for collections
    // specifically (Composer.Create<List<PrivateEnum>>() called from inside PrivateEnum's own
    // containing type compiled at the call site but failed in the generated collection plan with
    // CS0122) - confirmed directly before fixing.
    private static DiscoveredCollectionInfo ToDiscoveredCollectionInfo(
        ITypeSymbol collectionType, CollectionShapeInfo shape, Compilation compilation, LocationInfo? location)
    {
        var collectionTypeName = collectionType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var inaccessibleType = !compilation.IsSymbolAccessibleWithin(shape.ElementType, compilation.Assembly)
            ? shape.ElementType
            : shape.KeyType is { } keyType && !compilation.IsSymbolAccessibleWithin(keyType, compilation.Assembly)
                ? keyType
                : null;

        if (inaccessibleType is not null)
        {
            var diagnostic = new DiagnosticInfo(
                DiagnosticDescriptors.InaccessibleCollectionElementType,
                location,
                inaccessibleType.ToDisplayString(),
                collectionType.ToDisplayString());

            return new DiscoveredCollectionInfo(
                shape.Shape,
                collectionTypeName,
                shape.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                shape.ElementType.NullableAnnotation == NullableAnnotation.Annotated,
                shape.KeyType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                shape.KeyType?.NullableAnnotation == NullableAnnotation.Annotated,
                new[] { diagnostic }.ToEquatableArray());
        }

        return new DiscoveredCollectionInfo(
            shape.Shape,
            collectionTypeName,
            shape.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            shape.ElementType.NullableAnnotation == NullableAnnotation.Annotated,
            shape.KeyType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            shape.KeyType?.NullableAnnotation == NullableAnnotation.Annotated,
            EquatableArray<DiagnosticInfo>.Empty);
    }

    private static (DiscoveredTypeInfo Info, IMethodSymbol? Constructor, IReadOnlyList<(ITypeSymbol Type, string Name)> RequiredMemberTypes) Analyze(
        INamedTypeSymbol type, Compilation compilation, LocationInfo? location, string? path)
    {
        // ContainingNamespace.ToDisplayString() returns the literal text "<global namespace>" for
        // a type with no namespace, not an empty string - IsGlobalNamespace is the actual check.
        var @namespace = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

        // FullyQualifiedFormat emits `global::`-prefixed names - required for every type reference
        // emitted into generated code, per the "Generated code" coding-standards section.
        var emittedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var selection = ConstructorSelector.Select(type, compilation, location, path);

        if (!selection.IsSuccess)
        {
            var failure = new DiscoveredTypeInfo(
                @namespace,
                type.Name,
                emittedName,
                EquatableArray<ConstructorParameterInfo>.Empty,
                EquatableArray<RequiredMemberInfo>.Empty,
                new[] { selection.Diagnostic! }.ToEquatableArray());

            return (failure, null, []);
        }

        var requiredMembers = RequiredMemberCollector.Collect(type, selection.Constructor!, compilation, location, path);

        if (!requiredMembers.IsSuccess)
        {
            var failure = new DiscoveredTypeInfo(
                @namespace,
                type.Name,
                emittedName,
                EquatableArray<ConstructorParameterInfo>.Empty,
                EquatableArray<RequiredMemberInfo>.Empty,
                new[] { requiredMembers.Diagnostic! }.ToEquatableArray());

            return (failure, null, []);
        }

        var parameters = selection.Constructor!.Parameters
            .Select(p => new ConstructorParameterInfo(
                p.Name,
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                p.Type.NullableAnnotation == NullableAnnotation.Annotated))
            .ToEquatableArray();

        var success = new DiscoveredTypeInfo(
            @namespace,
            type.Name,
            emittedName,
            parameters,
            requiredMembers.Members,
            EquatableArray<DiagnosticInfo>.Empty);

        return (success, selection.Constructor, requiredMembers.MemberTypes);
    }
}
