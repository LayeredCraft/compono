using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// The five closed collection shapes <c>docs/adr/0013-collection-generation-semantics.md</c> and
/// <c>docs/adr/0014-generator-emitted-collection-plans.md</c> give a generated collection plan.
/// </summary>
internal enum CollectionShape
{
    Array,
    List,
    ReadOnlyList,
    HashSet,
    Dictionary,
}

/// <summary>A recognized collection shape's element type (and key type, for <see cref="CollectionShape.Dictionary"/>).</summary>
internal readonly record struct CollectionShapeInfo(CollectionShape Shape, ITypeSymbol ElementType, ITypeSymbol? KeyType);

/// <summary>
/// Recognizes the five ADR-0013 collection shapes by symbol identity - kept distinct from
/// <see cref="WellKnownTypes.WellKnownTypes"/> rather than extending its enum table, since that
/// type's debug self-check assumes a non-generic metadata name (its enum-name-to-metadata-name
/// transform doesn't produce a generic-arity backtick suffix).
/// </summary>
internal sealed class CollectionWellKnownTypes
{
    private static readonly BoundedCacheWithFactory<Compilation, CollectionWellKnownTypes> Cache = new();

    private readonly INamedTypeSymbol? _listOfT;
    private readonly INamedTypeSymbol? _readOnlyListOfT;
    private readonly INamedTypeSymbol? _hashSetOfT;
    private readonly INamedTypeSymbol? _dictionaryOfTKeyTValue;

    private CollectionWellKnownTypes(Compilation compilation)
    {
        _listOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
        _readOnlyListOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
        _hashSetOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1");
        _dictionaryOfTKeyTValue = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
    }

    internal static CollectionWellKnownTypes GetOrCreate(Compilation compilation) =>
        Cache.GetOrCreateValue(compilation, static c => new CollectionWellKnownTypes(c));

    internal bool TryClassify(ITypeSymbol type, out CollectionShapeInfo shape)
    {
        var candidate = FindShape(type);

        // A pointer/function-pointer element type is legal C# for a rank-1 array
        // (`int*[]`/`delegate*<int, int>[]`) even though it's never legal as a generic type
        // argument - unlike List<T>/HashSet<T>/Dictionary<TKey, TValue>, whose element type can
        // never be a pointer in the first place (the C# compiler already rejects `List<int*>` before
        // this code ever runs). `dynamic`, unlike a pointer, genuinely *is* legal as a type argument
        // for all five shapes (`HashSet<dynamic>`, `Dictionary<dynamic, int>`) - but a generated
        // collection plan can't implement `ICompositionPlan<HashSet<dynamic>>` at all (CS1966, "cannot
        // implement a dynamic interface") regardless of anything else the plan's body does, so this
        // has to be rejected here too, for every shape, not fixed by changing what the plan's body
        // emits.
        //
        // Both checks have to look past the immediate element/key type, not just at it: CS1966 fires
        // for `dynamic` anywhere inside the interface's constructed generic arguments, and a pointer
        // array is equally legal nested one level down (`List<int*[]>`) as it is as the element type
        // itself. `List<dynamic[]>` reproduces this directly - its immediate element type is
        // `dynamic[]`, whose own TypeKind is `Array`, not `Dynamic`, so a shallow check here accepted
        // it and only failed once the emitter tried to implement `ICompositionPlan<List<dynamic[]>>`
        // (confirmed directly before fixing: CS1966 on the outer interface itself, not just on a
        // `Resolve<dynamic[]>()` call in the plan body). Either case is left unclassified, falling
        // through to ComposedTypeAnalyzer's ordinary "not a named type"/unsupported-shape diagnostic
        // (CMP0006) instead of reaching a generated collection plan that can't compile.
        if (candidate is not { } shapeInfo ||
            ContainsUnsupportedTypeKind(shapeInfo.ElementType) ||
            (shapeInfo.KeyType is { } keyType && ContainsUnsupportedTypeKind(keyType)))
        {
            shape = default;
            return false;
        }

        shape = shapeInfo;
        return true;
    }

    private static bool ContainsUnsupportedTypeKind(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Dynamic)
            return true;

        if (type is IArrayTypeSymbol array)
            return ContainsUnsupportedTypeKind(array.ElementType);

        if (type is INamedTypeSymbol { IsGenericType: true } named)
            return named.TypeArguments.Any(ContainsUnsupportedTypeKind);

        return false;
    }

    private CollectionShapeInfo? FindShape(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol { Rank: 1 } array)
            return new CollectionShapeInfo(CollectionShape.Array, array.ElementType, KeyType: null);

        if (type is not INamedTypeSymbol { IsGenericType: true } named)
            return null;

        var definition = named.ConstructedFrom;

        if (SymbolEqualityComparer.Default.Equals(definition, _listOfT))
            return new CollectionShapeInfo(CollectionShape.List, named.TypeArguments[0], KeyType: null);

        if (SymbolEqualityComparer.Default.Equals(definition, _readOnlyListOfT))
            return new CollectionShapeInfo(CollectionShape.ReadOnlyList, named.TypeArguments[0], KeyType: null);

        if (SymbolEqualityComparer.Default.Equals(definition, _hashSetOfT))
            return new CollectionShapeInfo(CollectionShape.HashSet, named.TypeArguments[0], KeyType: null);

        if (SymbolEqualityComparer.Default.Equals(definition, _dictionaryOfTKeyTValue))
            return new CollectionShapeInfo(CollectionShape.Dictionary, named.TypeArguments[1], named.TypeArguments[0]);

        return null;
    }
}
