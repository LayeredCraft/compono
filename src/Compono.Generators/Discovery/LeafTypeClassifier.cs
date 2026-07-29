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

        return IsBuiltInSimpleType(named) || IsRecognizedBclValueType(named, wellKnownTypes) || IsNullableValueType(named, wellKnownTypes);
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
    /// included here only when its underlying type is one <c>NullableValueProvider</c> actually
    /// composes (primitive/enum/recognized BCL value type) - for any other underlying type,
    /// <see cref="IsNullableValueType"/> returns <see langword="false"/> for both this method and
    /// <see cref="IsProviderResolved"/>, so a root/member <c>Nullable&lt;T&gt;</c> over a composable
    /// custom struct falls through to ordinary composable-type handling and gets a real generated
    /// plan instead of silently compiling into a call that can only ever fail at runtime.
    /// </remarks>
    public static bool IsRuntimeProviderResolved(ITypeSymbol type, WellKnownTypes.WellKnownTypes wellKnownTypes) =>
        type is INamedTypeSymbol named &&
        (named.TypeKind == TypeKind.Enum || IsBuiltInSimpleType(named) || IsRecognizedBclValueType(named, wellKnownTypes) || IsNullableValueType(named, wellKnownTypes));

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

    // Only a leaf when the underlying type is one NullableValueProvider (Compono core) actually
    // composes at runtime (a primitive, an enum, or a recognized BCL value type) - PR #11 review
    // caught that treating every Nullable<T> as a leaf regardless of T left a custom struct's
    // Nullable<T> (e.g. Money?) compiling with zero diagnostics and then always throwing at runtime
    // ("no registration, provider, or generated plan could satisfy the request"), since
    // NullableValueProvider declines any T it doesn't recognize and nothing else was ever given a
    // chance to handle it. For a T this doesn't recognize, Nullable<T> now falls through to ordinary
    // composable-type handling instead - Nullable<T> has exactly one accessible constructor
    // (`Nullable(T value)`), so ConstructorSelector picks it like any other single-constructor type,
    // and T is recursed into and given its own real generated plan through the same machinery
    // constructor parameters already use, not a distinct nullable-specific mechanism.
    private static bool IsNullableValueType(INamedTypeSymbol type, WellKnownTypes.WellKnownTypes wellKnownTypes)
    {
        if (type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
            return false;

        return type.TypeArguments[0] is INamedTypeSymbol underlying &&
            (underlying.TypeKind == TypeKind.Enum || IsBuiltInSimpleType(underlying) || IsRecognizedBclValueType(underlying, wellKnownTypes));
    }
}
