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
    /// Used only for the <em>root</em> of <see cref="TransitiveClosureWalker.Walk"/> -
    /// <c>Composer.Create&lt;T&gt;()</c>'s own requested type, and each of an xUnit theory row's own
    /// composed parameters (<c>CompositionRow</c>).
    /// </summary>
    /// <remarks>
    /// Milestone 1-4 (pre-ADR-0024): this used to be a narrower subset of
    /// <see cref="IsProviderResolved"/> that excluded abstract types and delegates - enums, built-in
    /// simple types, and the recognized BCL value types only. At the time, an abstract/delegate root
    /// had no off-ramp at all (no provider mechanism existed to ever satisfy one at runtime), so
    /// <c>Composer.Create&lt;ISomeInterface&gt;()</c> was routed into constructor selection on
    /// purpose, to surface a real compile-time diagnostic (CMP0003) instead of silently compiling into
    /// a call that could only ever fail at runtime with a generic "nothing could satisfy this"
    /// exception - the exact regression PR #11 review caught when the root skipped classification
    /// entirely.
    /// <para>
    /// ADR-0024's public provider extensibility model (Milestone 5) invalidated that assumption: a
    /// registered <c>ICompositionValueProvider</c> (e.g. <c>Compono.NSubstitute</c>'s
    /// <c>NSubstituteProvider</c>) can now genuinely satisfy an interface/abstract-class/delegate root
    /// at runtime, exactly like it already could for a member (<see cref="IsProviderResolved"/> never
    /// excluded these). Routing such a root into constructor selection today would report a false
    /// CMP0003 for code that compiles and runs correctly - PLAN-0005 Phase 2's own real end-to-end
    /// sample test (a bare <c>[Shared] IOrderRepository</c> xUnit theory parameter, substituted via
    /// <c>UseNSubstitute()</c>) is exactly this case, caught by actually trying to compile it rather
    /// than only unit-testing the classifier in isolation. This method's root-only distinction from
    /// <see cref="IsProviderResolved"/> is kept only for <see cref="Nullable{T}"/> now - see below -
    /// not for abstract types or delegates, which now share the same leaf classification at both root
    /// and member position.
    /// </para>
    /// <see cref="Nullable{T}"/> is included here only when its underlying type is one
    /// <c>NullableValueProvider</c> actually composes (primitive/enum/recognized BCL value type) - for
    /// any other underlying type, <see cref="IsNullableValueType"/> returns <see langword="false"/> for
    /// both this method and <see cref="IsProviderResolved"/>, so a root/member
    /// <c>Nullable&lt;T&gt;</c> over a composable custom struct falls through to ordinary
    /// composable-type handling and gets a real generated plan instead of silently compiling into a
    /// call that can only ever fail at runtime.
    /// </remarks>
    public static bool IsRuntimeProviderResolved(ITypeSymbol type, WellKnownTypes.WellKnownTypes wellKnownTypes) =>
        IsProviderResolved(type, wellKnownTypes);

    /// <summary>
    /// The compile-time-gated third leaf outcome ADR-0043 adds alongside <see cref="IsProviderResolved"/>:
    /// an interface leaf is additionally eligible for a generated test double when
    /// <paramref name="testDoublesEnabled"/> (the <c>ComponoGeneratedTestDoubles</c> MSBuild opt-in,
    /// read once via <c>AnalyzerConfigOptionsProvider</c> and threaded down to this call) is
    /// <see langword="true"/>. This never changes <see cref="IsProviderResolved"/>'s own answer - an
    /// interface leaf is, and remains, provider-resolved either way; this is an orthogonal query the
    /// walker uses only to additionally record the leaf for double emission. When the opt-in is
    /// <see langword="false"/> (the default), this always returns <see langword="false"/> and nothing
    /// about existing generated output changes.
    /// </summary>
    public static bool IsGeneratedTestDoubleEligible(ITypeSymbol type, bool testDoublesEnabled) =>
        testDoublesEnabled && type is INamedTypeSymbol { TypeKind: TypeKind.Interface };

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
