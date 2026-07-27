using Compono.Generators.WellKnownTypes;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Decides, per Phase 1 of docs/plans/0001-milestone-1-source-generation-foundation.md, whether a
/// constructor parameter type gets its own recursively generated plan (<see cref="TransitiveClosureWalker"/>)
/// or is left as a bare <c>context.Resolve&lt;TParam&gt;()</c> call for a provider to handle -
/// interfaces, abstract types, delegates, enums, built-in simple types, and a handful of BCL value
/// types that don't have a meaningful composable constructor shape even though they're concrete.
/// </summary>
internal static class LeafTypeClassifier
{
    public static bool IsProviderResolved(ITypeSymbol type, WellKnownTypes.WellKnownTypes wellKnownTypes)
    {
        if (type is not INamedTypeSymbol named)
            return true;

        if (named.IsAbstract || named.TypeKind is TypeKind.Enum or TypeKind.Delegate)
            return true;

        return IsBuiltInSimpleType(named) || IsRecognizedBclValueType(named, wellKnownTypes);
    }

    private static bool IsBuiltInSimpleType(INamedTypeSymbol type) => type.SpecialType is
        SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
        SpecialType.System_Char or SpecialType.System_Decimal or SpecialType.System_Double or
        SpecialType.System_Single or SpecialType.System_Int16 or SpecialType.System_Int32 or
        SpecialType.System_Int64 or SpecialType.System_UInt16 or SpecialType.System_UInt32 or
        SpecialType.System_UInt64 or SpecialType.System_String or SpecialType.System_IntPtr or
        SpecialType.System_UIntPtr;

    private static bool IsRecognizedBclValueType(INamedTypeSymbol type, WellKnownTypes.WellKnownTypes wellKnownTypes) =>
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_DateTime) ||
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_DateTimeOffset) ||
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_Guid) ||
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_TimeSpan);
}
