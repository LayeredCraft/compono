namespace Compono;

/// <summary>
/// The compact, compile-time-constructible value a generated <see cref="ICompositionPlan{T}"/>
/// passes to <see cref="ICompositionContext.Resolve{TValue}(in CompositionRequestDescriptor)"/> for one constructor parameter or
/// required member.
/// </summary>
/// <remarks>
/// A plain <see langword="struct"/>, not a <c>record struct</c> - equality, <c>Deconstruct</c>, and
/// record-style formatting aren't part of this type's contract, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s second amendment.
/// <see cref="Ordinal"/>, not <see cref="Name"/>, is the identity <see cref="CompositionContext"/>
/// uses for path/random-fork keys - <see cref="Name"/> exists for diagnostic display only, per
/// <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>'s second
/// amendment.
/// </remarks>
public readonly struct CompositionRequestDescriptor
{
    /// <summary>
    /// Creates a <see cref="CompositionRequestDescriptor"/>.
    /// </summary>
    /// <param name="kind">Whether this is a constructor parameter or a required member.</param>
    /// <param name="ordinal">
    /// The stable identity this request forks random state and builds path identity from - a
    /// constructor parameter's position in the selected constructor, or a required member's
    /// generator-assigned declaration-order index. Never <see cref="Name"/>.
    /// </param>
    /// <param name="name">The parameter or member name, for diagnostic display only.</param>
    /// <param name="declaringType">
    /// The type whose constructor/required member declares this parameter/member - <see langword="null"/>
    /// for a request with no member identity of its own (a collection element, dictionary key/value, or
    /// manual resolve). See <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </param>
    /// <param name="nullability">Whether the requesting parameter or member is nullable-annotated.</param>
    public CompositionRequestDescriptor(CompositionRequestKind kind, int ordinal, string name, Type? declaringType, Nullability nullability)
    {
        Kind = kind;
        Ordinal = ordinal;
        Name = name;
        DeclaringType = declaringType;
        Nullability = nullability;
    }

    /// <summary>Whether this is a constructor parameter or a required member.</summary>
    public CompositionRequestKind Kind { get; }

    /// <summary>
    /// The stable identity this request forks random state and builds path identity from - never
    /// <see cref="Name"/>.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>The parameter or member name, for diagnostic display only.</summary>
    public string Name { get; }

    /// <summary>
    /// The type whose constructor/required member declares this parameter/member - <see langword="null"/>
    /// for a request with no member identity of its own (a collection element, dictionary key/value, or
    /// manual resolve). Never fed into random-fork hashing - used only for configuration-rule matching
    /// (stage 4) and collection-size override lookup (stage 7). See
    /// <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    public Type? DeclaringType { get; }

    /// <summary>Whether the requesting parameter or member is nullable-annotated.</summary>
    public Nullability Nullability { get; }
}
