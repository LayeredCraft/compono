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

        return IsBuiltInSimpleType(named) || IsRecognizedBclValueType(named, wellKnownTypes) || IsNullableValueType(named);
    }

    /// <summary>
    /// The narrower subset of <see cref="IsProviderResolved"/> that a stage-7 built-in
    /// <c>ICompositionProvider</c> actually claims at runtime - enums, built-in simple types, and the
    /// recognized BCL value types, but <b>not</b> abstract types or delegates (which
    /// <see cref="IsProviderResolved"/> also treats as "leave as a bare <c>Resolve&lt;T&gt;()</c> call,"
    /// but nothing ever satisfies at runtime either).
    /// </summary>
    /// <remarks>
    /// Used only for the <em>root</em> of <see cref="TransitiveClosureWalker.Walk"/> - a member of an
    /// abstract/delegate type is legitimately left unresolved for now (a future provider/registration
    /// might claim it), but the root of <c>Composer.Create&lt;T&gt;()</c> has no such off-ramp: an
    /// abstract/delegate root must still reach constructor selection so it gets a real compile-time
    /// diagnostic (CMP0003) instead of silently compiling into a call that can only ever fail at
    /// runtime with a generic "nothing could satisfy this" exception - the exact regression PR #11
    /// review caught when the root skipped classification entirely. <see cref="Nullable{T}"/> is
    /// included here (unlike abstract/delegate) even though a <em>custom</em> struct's
    /// <c>Nullable&lt;T&gt;</c> isn't runtime-satisfiable either - <c>NullableValueProvider</c> always
    /// attempts and cleanly declines any unsupported <c>Nullable&lt;T&gt;</c> at runtime (reaching
    /// stage 9's diagnostic, which correctly names the request), so there's no compile-time
    /// distinction worth drawing between "a supported Nullable&lt;T&gt;" and "an unsupported one" here.
    /// </remarks>
    public static bool IsRuntimeProviderResolved(ITypeSymbol type, WellKnownTypes.WellKnownTypes wellKnownTypes) =>
        type is INamedTypeSymbol named &&
        (named.TypeKind == TypeKind.Enum || IsBuiltInSimpleType(named) || IsRecognizedBclValueType(named, wellKnownTypes) || IsNullableValueType(named));

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
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_TimeSpan) ||
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_DateOnly) ||
        wellKnownTypes.IsType(type, WellKnownTypeData.WellKnownType.System_TimeOnly);

    // Nullable<T> is always left as a leaf regardless of T - NullableValueProvider (Compono core)
    // already attempts any Nullable<T> at runtime and cleanly declines (reaching stage 9's
    // diagnostic, naming the actual request) when T isn't one of the primitive/enum types it composes
    // - a custom struct's Nullable<T> is exactly the same "no provider/plan could satisfy this" shape
    // as any other unhandled type, not a distinct compile-time-diagnosable case.
    private static bool IsNullableValueType(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
}
