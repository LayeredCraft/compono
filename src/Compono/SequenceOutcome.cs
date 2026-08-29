namespace Compono;

/// <summary>
/// One outcome in a <see cref="ReturnConfigBuilder{T}.ReturnsSequence"/> sequence - either a
/// configured return value (implicit conversion from <typeparamref name="T"/>) or a configured
/// exception (<see cref="SequenceOutcome.Throw"/>), target-typed so a consumer never spells
/// <c>SequenceOutcome&lt;T&gt;</c> directly (ADR-0054). Mirrors <see cref="Match{T}"/>'s own "implicit
/// conversion from a literal, no public constructor" shape.
/// </summary>
/// <remarks>
/// Only a single implicit conversion exists (from <typeparamref name="T"/>) - a second implicit
/// conversion from <see cref="Exception"/> was rejected because it is silently ambiguous/wrong for
/// <typeparamref name="T"/> values that are themselves <see cref="Exception"/> or a base/derived type
/// of it (e.g. <c>T = object</c> resolves to "throw" with no way left to express "value"; <c>T =
/// InvalidOperationException</c> silently resolves to "value" instead of "throw" - both confirmed by
/// real compiler/runtime evidence, not assumed). <see cref="SequenceOutcome.Throw"/> plus the second
/// implicit conversion from <see cref="SequenceOutcome.ThrownOutcome"/> is unambiguous for every
/// <typeparamref name="T"/>.
/// </remarks>
public readonly struct SequenceOutcome<T>
{
    private readonly bool _isException;
    private readonly T? _value;
    private readonly Exception? _exception;

    private SequenceOutcome(bool isException, T? value, Exception? exception)
    {
        _isException = isException;
        _value = value;
        _exception = exception;
    }

    /// <summary>A sequence entry that returns <paramref name="value"/> when consumed.</summary>
    public static implicit operator SequenceOutcome<T>(T value) => new(false, value, null);

    /// <summary>A sequence entry that throws the exception carried by <paramref name="thrown"/> when consumed.</summary>
    public static implicit operator SequenceOutcome<T>(SequenceOutcome.ThrownOutcome thrown)
    {
        // Guards against `default(ThrownOutcome)` - a public struct's default bypasses
        // SequenceOutcome.Throw's own null-check, so this conversion must re-check.
        if (thrown.Exception is null)
            throw new ArgumentException("A thrown sequence outcome must carry an exception - use SequenceOutcome.Throw(exception), not default(SequenceOutcome.ThrownOutcome).", nameof(thrown));

        return new SequenceOutcome<T>(true, default, thrown.Exception);
    }

    /// <summary>Returns the configured value, or throws the configured exception.</summary>
    internal T Resolve() => _isException ? throw _exception! : _value!;
}

/// <summary>
/// Factory for the exception side of a <see cref="ReturnConfigBuilder{T}.ReturnsSequence"/> entry.
/// </summary>
public static class SequenceOutcome
{
    /// <summary>A sequence entry that throws <paramref name="exception"/> when consumed.</summary>
    public static ThrownOutcome Throw(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ThrownOutcome(exception);
    }

    /// <summary>
    /// Marker carrying the exception for a thrown sequence entry, implicitly convertible to
    /// <see cref="SequenceOutcome{T}"/> for any <c>T</c>. Only ever produced by <see cref="Throw"/> -
    /// its own conversion guards against the struct's <c>default</c> value, which would otherwise
    /// carry a null exception.
    /// </summary>
    public readonly struct ThrownOutcome
    {
        internal readonly Exception? Exception;

        internal ThrownOutcome(Exception exception)
        {
            Exception = exception;
        }
    }
}
