// Portions of this file are derived from aspnetcore
// Source:
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Mvc/Mvc.Testing/src/DeferredHostBuilder.cs
// Copyright (c) .NET Foundation and Contributors
// Licensed under the MIT License

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.WellKnownTypes;

/// <summary>
/// A per-<see cref="Compilation"/>-scoped cache of well-known type symbols, so the generator checks
/// symbol identity (<see cref="SymbolEqualityComparer"/>) instead of scattering
/// <c>GetTypeByMetadataName</c>/string-based type-name comparisons through the codebase. Adopted from
/// <c>dynamo-mapper</c> per <c>docs/adr/0005-generator-implementation-conventions.md</c>.
/// </summary>
internal sealed class WellKnownTypes
{
    private static readonly BoundedCacheWithFactory<Compilation, WellKnownTypes> LazyWellKnownTypesCache = new();

    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol?[] _lazyWellKnownTypes;

    static WellKnownTypes() => AssertEnumAndTableInSync();

    private WellKnownTypes(Compilation compilation)
    {
        _compilation = compilation;
        _lazyWellKnownTypes = new INamedTypeSymbol?[WellKnownTypeData.WellKnownTypeNames.Length];
    }

    public static WellKnownTypes GetOrCreate(Compilation compilation) =>
        LazyWellKnownTypesCache.GetOrCreateValue(compilation, static c => new WellKnownTypes(c));

    public INamedTypeSymbol? Get(WellKnownTypeData.WellKnownType type)
    {
        var index = (int)type;
        var symbol = _lazyWellKnownTypes[index];

        return symbol ?? GetAndCache(index);
    }

    public bool IsType(ITypeSymbol type, WellKnownTypeData.WellKnownType wellKnownType) =>
        SymbolEqualityComparer.Default.Equals(type, Get(wellKnownType));

    [Conditional("DEBUG")]
    private static void AssertEnumAndTableInSync()
    {
        for (var i = 0; i < WellKnownTypeData.WellKnownTypeNames.Length; i++)
        {
            var name = WellKnownTypeData.WellKnownTypeNames[i];
            var typeId = (WellKnownTypeData.WellKnownType)i;
            var typeIdName = typeId.ToString().Replace("__", "+").Replace('_', '.');

            Debug.Assert(name == typeIdName, $"Enum name ({typeIdName}) and type name ({name}) must match at {i}");
        }
    }

    private INamedTypeSymbol? GetAndCache(int index)
    {
        var result = GetTypeByMetadataNameInTargetAssembly(WellKnownTypeData.WellKnownTypeNames[index]);
        if (result is null)
            return null;

        Interlocked.CompareExchange(ref _lazyWellKnownTypes[index], result, null);
        return _lazyWellKnownTypes[index];
    }

    // Filter for types within well-known assemblies only, in case a consumer happens to reuse
    // the same namespace + type name.
    private INamedTypeSymbol? GetTypeByMetadataNameInTargetAssembly(string metadataName)
    {
        var types = _compilation.GetTypesByMetadataName(metadataName);

        if (types.Length == 0)
            return null;

        if (types.Length == 1)
            return types[0];

        return types.FirstOrDefault(IsFromCompono);
    }

    private static bool IsFromCompono(INamedTypeSymbol type) =>
        type.ContainingAssembly.Identity.Name.Equals("Compono", StringComparison.Ordinal);
}
