namespace Compono;

/// <summary>
/// An argument matcher for a generator-emitted test double's argument-aware <c>Configure()</c>/
/// <c>Verify()</c> surface - a literal value (equality match), <see cref="Match.Any{T}"/> (matches
/// anything), or <see cref="Match.Is{T}"/> (matches by predicate). See ADR-0048.
/// </summary>
/// <remarks>
/// Exposes exactly one public, generated-code-facing operation - <see cref="Matches"/>. No public
/// delegate/predicate accessor: generated dispatch/verification code lives in the *consumer*
/// assembly, not <c>Compono</c>, so an <see langword="internal"/> accessor is unreachable from it -
/// the same cross-assembly-accessibility class of defect ADR-0044 Amendment 3 already fixed for
/// <see cref="ReturnConfig{T}"/>. Keeping the internal representation private also means it stays
/// free to change without becoming a breaking change to generated output's dependency on this type.
/// Named <c>Match</c>, not <c>Arg</c>, specifically because <c>Compono.Arg</c> collides with
/// <c>NSubstitute.Arg</c> for any consumer whose own namespace nests under <c>Compono</c> (this
/// repo's own samples convention) or who combines <c>Compono</c> with <c>Compono.NSubstitute</c>
/// directly - confirmed with a real failing build during PLAN-0048's implementation, not a
/// theoretical concern. See ADR-0048's Decision Outcome.
/// </remarks>
public readonly struct Match<T>
{
    private enum Kind : byte
    {
        Equality,
        Any,
        Predicate,
    }

    private readonly Kind _kind;
    private readonly T? _value;
    private readonly Func<T, bool>? _predicate;

    private Match(Kind kind, T? value, Func<T, bool>? predicate)
    {
        _kind = kind;
        _value = value;
        _predicate = predicate;
    }

    /// <summary>
    /// A literal argument matches by equality (<see cref="EqualityComparer{T}.Default"/>) - the same
    /// implicit meaning NSubstitute itself gives a literal argument, and the common case in real
    /// migrated call sites, so it allocates no closure (unlike <see cref="Match.Is{T}"/>).
    /// </summary>
    public static implicit operator Match<T>(T value) => new(Kind.Equality, value, null);

    internal static Match<T> Any() => new(Kind.Any, default, null);

    internal static Match<T> Is(Func<T, bool> predicate) => new(Kind.Predicate, default, predicate);

    /// <summary>
    /// Whether <paramref name="value"/> satisfies this matcher - the one operation generated
    /// dispatch/verification code calls.
    /// </summary>
    public bool Matches(T value) => _kind switch
    {
        Kind.Any => true,
        Kind.Equality => EqualityComparer<T>.Default.Equals(_value!, value),
        Kind.Predicate => _predicate!(value),
        _ => false,
    };
}

/// <summary>Factory methods for <see cref="Match{T}"/> - <c>Match.Any&lt;T&gt;()</c>/<c>Match.Is&lt;T&gt;(predicate)</c>.</summary>
public static class Match
{
    /// <summary>A <see cref="Match{T}"/> that matches any value of <typeparamref name="T"/>.</summary>
    public static Match<T> Any<T>() => Match<T>.Any();

    /// <summary>A <see cref="Match{T}"/> that matches a value satisfying <paramref name="predicate"/>.</summary>
    public static Match<T> Is<T>(Func<T, bool> predicate) => Match<T>.Is(predicate);
}
