using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="TypedConstant"/> - an attribute constructor argument's compile-time-constant
/// value, as Roslyn sees it - back into valid C# source text, for
/// <c>Compono.XunitV3.Aot.ComposeAttribute&lt;TProfile, TConfig&gt;</c>'s generated
/// <c>new TConfig(...)</c> construction (ADR-0067/PLAN-0067). The one genuinely new piece of generator
/// machinery this feature needs - no existing emitter in this codebase renders argument *values*, only
/// *types* (ADR-0067's own "Negative Consequences" section).
/// </summary>
/// <remarks>
/// Bounded by construction: C# restricts an attribute constructor parameter's type to
/// <see langword="bool"/>/<see langword="byte"/>/<see langword="char"/>/<see langword="double"/>/
/// <see langword="float"/>/<see langword="int"/>/<see langword="long"/>/<see langword="sbyte"/>/
/// <see langword="short"/>/<see langword="string"/>/<see langword="uint"/>/<see langword="ulong"/>/
/// <see langword="ushort"/>, an enum type, <see cref="System.Type"/>, <see langword="object"/> (boxing
/// one of the above), or a single-dimensional array of any of those - there is no broader value space
/// to handle. Every non-numeric case renders directly; every numeric primitive case renders as an
/// explicit cast (<c>(byte)200</c>) against its invariant-culture digit string rather than guessing a
/// C# numeric-literal suffix - always round-trips correctly regardless of which specific numeric type
/// was used, and <c>Discovery.TypedConstantMatcher.Validate</c> has already proven, at the call site
/// this renderer is used from, that the constant's own type converts to the target parameter type.
/// </remarks>
internal static class TypedConstantLiteralRenderer
{
    public static string Render(TypedConstant constant, ITypeSymbol targetType)
    {
        if (constant.IsNull)
            return "null";

        return constant.Kind switch
        {
            TypedConstantKind.Type => RenderTypeofValue(constant),
            TypedConstantKind.Array => RenderArray(constant, targetType),
            TypedConstantKind.Enum => RenderCast(constant.Type!, constant.Value!),
            TypedConstantKind.Primitive => RenderPrimitive(constant),
            _ => throw new NotSupportedException($"Unsupported TypedConstantKind '{constant.Kind}' for profile configuration argument rendering."),
        };
    }

    private static string RenderTypeofValue(TypedConstant constant)
    {
        var typeValue = (ITypeSymbol)constant.Value!;
        return $"typeof({typeValue.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
    }

    private static string RenderArray(TypedConstant constant, ITypeSymbol targetType)
    {
        var elementType = targetType is IArrayTypeSymbol arrayType
            ? arrayType.ElementType
            : ((IArrayTypeSymbol)constant.Type!).ElementType;

        var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var elements = constant.Values.Select(value => Render(value, elementType));

        return $"new {elementTypeName}[] {{ {string.Join(", ", elements)} }}";
    }

    private static string RenderPrimitive(TypedConstant constant)
    {
        var value = constant.Value!;

        return value switch
        {
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            bool b => b ? "true" : "false",
            double or float => RenderFloatingPoint(constant.Type!, value),
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                RenderCast(constant.Type!, value),
            _ => throw new NotSupportedException($"Unsupported primitive value type '{value.GetType()}' for profile configuration argument rendering."),
        };
    }

    // double.NaN/double.PositiveInfinity/double.NegativeInfinity (and their float equivalents) are
    // real, legal compile-time-constant attribute arguments (declared `const`, confirmed by a direct
    // compile probe during PR #140 review) - RenderCast's Convert.ToString(...) renders these as the
    // bare identifiers "NaN"/"Infinity"/"-Infinity", which are not valid C# literal/cast syntax
    // ((double)NaN doesn't compile), so they need their own qualified-constant rendering instead.
    // Negative zero is a related, separate correctness gap the same underlying digit-string approach
    // has: RenderCast's Convert.ToString(-0.0) renders "-0", and casting the *int* literal -0 (unary
    // minus of int 0, itself just 0) to double produces positive zero, silently losing the sign bit -
    // rendered here as an explicit unary negation of a real zero double/float instead, which IEEE 754
    // negation correctly turns into negative zero.
    private static string RenderFloatingPoint(ITypeSymbol constantType, object value)
    {
        var typeName = constantType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // double.IsNegative/float.IsNegative aren't available on netstandard2.0 (Compono.Generators'
        // own TFM) - the reciprocal trick (1/-0.0 = -Infinity, 1/0.0 = +Infinity) detects the sign of
        // zero using only APIs netstandard2.0 has always had.
        var (isNaN, isPositiveInfinity, isNegativeInfinity, isNegativeZero) = value switch
        {
            double d => (double.IsNaN(d), double.IsPositiveInfinity(d), double.IsNegativeInfinity(d), d == 0d && double.IsNegativeInfinity(1d / d)),
            float f => (float.IsNaN(f), float.IsPositiveInfinity(f), float.IsNegativeInfinity(f), f == 0f && float.IsNegativeInfinity(1f / f)),
            _ => throw new InvalidOperationException($"Unreachable: RenderFloatingPoint called with non-floating-point value type '{value.GetType()}'."),
        };

        if (isNaN)
            return $"{typeName}.NaN";

        if (isPositiveInfinity)
            return $"{typeName}.PositiveInfinity";

        if (isNegativeInfinity)
            return $"{typeName}.NegativeInfinity";

        if (isNegativeZero)
            return $"-({typeName})0";

        return RenderCast(constantType, value);
    }

    // An explicit cast against the constant's own real type - always round-trips, regardless of
    // suffix rules, and regardless of whether the target parameter's type is the constant's own type
    // exactly or something it merely converts to (e.g. an int constant supplied to a long parameter).
    private static string RenderCast(ITypeSymbol constantType, object value)
    {
        var typeName = constantType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var literal = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";

        return $"({typeName}){literal}";
    }
}
