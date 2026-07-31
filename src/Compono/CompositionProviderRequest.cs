namespace Compono;

/// <summary>
/// A composition request, as seen by a public <see cref="ICompositionValueProvider"/> - decoupled
/// from the engine's own internal <see cref="CompositionRequest"/> (no path, no shared-scope flag,
/// no pipeline plumbing a provider author has no legitimate use for). See
/// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
/// </summary>
public readonly struct CompositionProviderRequest
{
    /// <summary>Creates a <see cref="CompositionProviderRequest"/>.</summary>
    /// <param name="requestedType">The requested CLR type.</param>
    /// <param name="declaringType">
    /// The type whose constructor/required member declares this request, or <see langword="null"/>
    /// for a request with no member identity of its own.
    /// </param>
    /// <param name="name">
    /// The declaring constructor parameter/required member/test-method-parameter's own name, or
    /// <see langword="null"/> for a request with no name of its own.
    /// </param>
    /// <param name="nullability">Whether the requesting parameter or member is nullable-annotated.</param>
    public CompositionProviderRequest(Type requestedType, Type? declaringType, string? name, Nullability nullability)
    {
        ArgumentNullException.ThrowIfNull(requestedType);
        RequestedType = requestedType;
        DeclaringType = declaringType;
        Name = name;
        Nullability = nullability;
    }

    /// <summary>The requested CLR type.</summary>
    public Type RequestedType { get; }

    /// <summary>
    /// The type whose constructor/required member declares this request, or <see langword="null"/>
    /// for a request with no member identity (a collection element, a manual resolve, or the
    /// composition root itself) - the same field/same semantics
    /// <see cref="CompositionRequestDescriptor.DeclaringType"/> already carries, per
    /// <c>docs/adr/0020-composition-configuration-rules.md</c>.
    /// </summary>
    public Type? DeclaringType { get; }

    /// <summary>
    /// The declaring constructor parameter/required member/test-method-parameter's own name, for
    /// diagnostic display and name-based provider matching (e.g. a future <c>Compono.Bogus</c>
    /// member-name convention) - <see langword="null"/> for a request with no name of its own (a
    /// collection element, dictionary key/value, or manual resolve).
    /// </summary>
    public string? Name { get; }

    /// <summary>Whether the requesting parameter or member is nullable-annotated.</summary>
    public Nullability Nullability { get; }
}
