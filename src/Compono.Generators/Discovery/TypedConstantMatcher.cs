using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Discovery;

/// <summary>
/// The compile-time counterpart to <c>Compono.XunitV3.Binding.PositionalArgumentBinder.Validate</c> -
/// validates one <see cref="TypedConstant"/> attribute-argument value against a target
/// <see cref="IParameterSymbol"/>, using Roslyn's own conversion classification in place of
/// <see cref="System.Type.IsInstanceOfType(object)"/>, for <c>CMP0043</c>
/// (ADR-0067/PLAN-0067). Deliberately as strict as the runtime check it mirrors: only an identity,
/// boxing, or implicit reference conversion is accepted - never an implicit numeric conversion (e.g. a
/// supplied <see langword="int"/> constant is not valid for a <see langword="double"/> parameter, the
/// same way a boxed <see langword="int"/> is not <see cref="Type.IsInstanceOfType(object)"/> a
/// <see langword="double"/> at runtime).
/// </summary>
internal static class TypedConstantMatcher
{
    public static TypedConstantValidation Validate(TypedConstant constant, IParameterSymbol parameter, Compilation compilation)
    {
        var parameterType = parameter.Type;
        var isNullable = IsNullable(parameterType);
        var underlyingType = UnwrapNullable(parameterType);

        if (constant.IsNull)
        {
            return isNullable
                ? TypedConstantValidation.Valid
                : TypedConstantValidation.NullNotAllowed;
        }

        if (constant.Type is null)
            return TypedConstantValidation.TypeMismatch;

        // PR #140 Codex review round 2 (root-level) + round 4 (nested-array-element evidence): during
        // an incomplete/erroneous compilation (a live IDE analysis pass mid-edit, most commonly),
        // Roslyn can hand back a TypedConstant of Kind = Error whose Type is still the parameter's own
        // declared type - ClassifyConversion would then find a trivial identity conversion and report
        // Valid, and TypedConstantLiteralRenderer.Render has no rendering for Kind = Error at all (it
        // throws). The round-2 fix only checked the outer constant; round 4 found a malformed *array*
        // argument (e.g. new int[] { UndefinedIdentifier }) reports a well-typed outer Array constant
        // whose *element* is Kind = Error - HasError walks the whole constant tree (including nested
        // array elements) so both shapes are caught here, uniformly, before any conversion
        // classification - the same "diagnose rather than let something obscure happen" rule this repo
        // already applies (CMP0040's own rationale).
        if (HasError(constant))
            return TypedConstantValidation.TypeMismatch;

        var conversion = ((CSharpCompilation)compilation).ClassifyConversion(constant.Type, underlyingType);

        return conversion.Exists && (conversion.IsIdentity || conversion.IsBoxing || (conversion.IsImplicit && conversion.IsReference))
            ? TypedConstantValidation.Valid
            : TypedConstantValidation.TypeMismatch;
    }

    private static bool HasError(TypedConstant constant) =>
        constant.Kind == TypedConstantKind.Error
        || (constant.Kind == TypedConstantKind.Array && constant.Values.Any(HasError));

    private static bool IsNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
            return true;

        if (type.IsValueType)
            return false;

        return type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType
            ? namedType.TypeArguments[0]
            : type;
}

/// <summary>The outcome of <see cref="TypedConstantMatcher.Validate"/>.</summary>
internal enum TypedConstantValidation
{
    /// <summary>The value is valid for the parameter.</summary>
    Valid,

    /// <summary>The value is <see langword="null"/>, but the parameter is not nullable-annotated.</summary>
    NullNotAllowed,

    /// <summary>The value's type is not convertible to the parameter's type.</summary>
    TypeMismatch,
}
