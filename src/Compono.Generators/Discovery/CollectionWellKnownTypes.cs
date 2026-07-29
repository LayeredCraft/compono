using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// The five closed collection shapes <c>docs/adr/0013-collection-generation-semantics.md</c> and
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s third amendment
/// give a generated collection plan.
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
        if (type is IArrayTypeSymbol { Rank: 1 } array)
        {
            shape = new CollectionShapeInfo(CollectionShape.Array, array.ElementType, KeyType: null);
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            var definition = named.ConstructedFrom;

            if (SymbolEqualityComparer.Default.Equals(definition, _listOfT))
            {
                shape = new CollectionShapeInfo(CollectionShape.List, named.TypeArguments[0], KeyType: null);
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(definition, _readOnlyListOfT))
            {
                shape = new CollectionShapeInfo(CollectionShape.ReadOnlyList, named.TypeArguments[0], KeyType: null);
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(definition, _hashSetOfT))
            {
                shape = new CollectionShapeInfo(CollectionShape.HashSet, named.TypeArguments[0], KeyType: null);
                return true;
            }

            if (SymbolEqualityComparer.Default.Equals(definition, _dictionaryOfTKeyTValue))
            {
                shape = new CollectionShapeInfo(CollectionShape.Dictionary, named.TypeArguments[1], named.TypeArguments[0]);
                return true;
            }
        }

        shape = default;
        return false;
    }
}
