namespace Compono.TUnit.Binding;

/// <summary>
/// The outcome of <see cref="PositionalArgumentBinder.Validate"/> - a validated value, a
/// <see langword="null"/> value against a non-nullable parameter, or a value whose type isn't
/// assignable to the parameter's type.
/// </summary>
internal enum PositionalArgumentValidation
{
    /// <summary>The value is valid for the parameter.</summary>
    Valid,

    /// <summary>The value is <see langword="null"/>, but the parameter is not nullable-annotated.</summary>
    NullNotAllowed,

    /// <summary>The value's runtime type is not assignable to the parameter's type.</summary>
    TypeMismatch,
}

/// <summary>
/// The single null/<see cref="Nullable{T}"/>-unwrap/assignability check <c>Compono.TUnit</c>'s
/// inline-value binding needs. Duplicated from <c>Compono.XunitV3.Binding.PositionalArgumentBinder</c>
/// rather than shared, per <c>docs/adr/0040-compono-tunit-package-design.md</c>'s binding-logic
/// decision (TUnit and xUnit v3 hand a data source different parameter-metadata shapes, so the
/// correct shared abstraction boundary isn't obvious yet from one example on each side).
/// </summary>
internal static class PositionalArgumentBinder
{
    /// <summary>
    /// Validates <paramref name="value"/> against a parameter of type <paramref name="parameterType"/>
    /// with the given <paramref name="nullability"/>.
    /// </summary>
    public static PositionalArgumentValidation Validate(Type parameterType, Nullability nullability, object? value)
    {
        if (value is null)
        {
            return nullability == Nullability.Nullable
                ? PositionalArgumentValidation.Valid
                : PositionalArgumentValidation.NullNotAllowed;
        }

        // A non-null Nullable<T> boxes as a boxed T, not a boxed Nullable<T> (a CLR nullable-boxing
        // rule) - unwrapping first is a no-op for a non-nullable parameter (Nullable.GetUnderlyingType
        // returns null, so ?? falls back to the declared type unchanged) and is what makes e.g. an
        // int value valid for an int? parameter.
        var underlyingType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

        return underlyingType.IsInstanceOfType(value)
            ? PositionalArgumentValidation.Valid
            : PositionalArgumentValidation.TypeMismatch;
    }
}
