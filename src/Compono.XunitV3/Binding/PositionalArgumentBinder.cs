namespace Compono.XunitV3.Binding;

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
/// The single null/<see cref="Nullable{T}"/>-unwrap/assignability check every positional-argument
/// binding target in <c>Compono.XunitV3</c> needs - <see cref="ComposeAttribute"/>'s inline-value
/// binding (test-method parameters, ADR-0022) and <c>ConfigProfileBinder</c>'s profile-configuration-
/// argument binding (a <c>TConfig</c> type's constructor parameters, ADR-0036) both call this,
/// rather than each independently reimplementing the check - a correction to this logic now reaches
/// every binding target that uses it, not just whichever one it happened to be written against
/// (PR #65 review). Message text stays owned by each call site, not centralized here: the two
/// targets describe the value being validated differently ("Inline value for parameter... on..."
/// vs. "Profile configuration argument for parameter... of..."), and centralizing the message text
/// as well would either lose that distinction or force an awkward shared template.
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
