namespace Compono;

/// <summary>
/// Public write surface over a single <see cref="ReturnConfig{T}"/> slot - constructed by
/// generator-emitted configuration extensions (<c>Configure().Member()</c>) in the consumer's own
/// assembly, per ADR-0043. A <see langword="ref struct"/> because it only ever wraps a
/// <see langword="ref"/> to a field already living on the generated double instance; it's never
/// stored, only used inline at the call site.
/// </summary>
/// <remarks>
/// <see cref="Returns"/>/<see cref="Throws"/>/<see cref="ReturnsSequence"/> are all
/// last-configuration-wins: each of the three clears the other two's state, so configuring any one
/// of them after an earlier call to a different one of them doesn't leave stale state behind. See
/// ADR-0043 Amendment 7, Finding R (the original two-way rule) and ADR-0054 (the sequence extension).
/// </remarks>
public readonly ref struct ReturnConfigBuilder<T>
{
    private readonly ref ReturnConfig<T> _slot;

    /// <summary>Wraps <paramref name="slot"/>, the generated double's own backing field for this member.</summary>
    public ReturnConfigBuilder(ref ReturnConfig<T> slot) => _slot = ref slot;

    /// <summary>Configures the member to return <paramref name="value"/>, clearing any prior <see cref="Throws"/>/<see cref="ReturnsSequence"/>.</summary>
    public void Returns(T value)
    {
        _slot.Value = value;
        _slot.HasValue = true;
        _slot.Exception = null;
        _slot.Sequence = null;
        _slot.SequenceOrdinal = 0;
    }

    /// <summary>Configures the member to throw <paramref name="exception"/>, clearing any prior <see cref="Returns"/>/<see cref="ReturnsSequence"/>.</summary>
    public void Throws(Exception exception)
    {
        _slot.Exception = exception;
        _slot.HasValue = false;
        _slot.Value = default;
        _slot.Sequence = null;
        _slot.SequenceOrdinal = 0;
    }

    /// <summary>
    /// Configures the member to return (or throw) each <paramref name="outcomes"/> entry in order, one
    /// per invocation, by ordinal - the first call gets <c>outcomes[0]</c>, the second
    /// <c>outcomes[1]</c>, and so on; once exhausted, every further call repeats the final entry
    /// (ADR-0054). Clears any prior <see cref="Returns"/>/<see cref="Throws"/>/<see cref="ReturnsSequence"/>
    /// state and resets the ordinal to 0, the same last-configuration-wins contract <see cref="Returns"/>/
    /// <see cref="Throws"/> already document. An ordinary <typeparamref name="T"/> value implicitly
    /// converts to <see cref="SequenceOutcome{T}"/>, so a pure-value sequence reads as plain values
    /// (<c>.ReturnsSequence(false, false, true)</c>); an exception outcome is spelled explicitly with
    /// <see cref="SequenceOutcome.Throw(Exception)"/> - there is no implicit conversion from
    /// <see cref="Exception"/>, since that would be silently wrong for a <typeparamref name="T"/> that
    /// is itself <see cref="Exception"/> or a base/derived type of it - so a mixed sequence reads
    /// <c>.ReturnsSequence(SequenceOutcome.Throw(ex1), SequenceOutcome.Throw(ex2), value)</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="outcomes"/> is empty.</exception>
    public void ReturnsSequence(params SequenceOutcome<T>[] outcomes)
    {
        if (outcomes.Length == 0)
            throw new ArgumentException("A response sequence needs at least one outcome.", nameof(outcomes));

        // Codex review, PR #115: `outcomes` is not guaranteed to be a fresh array - a caller can pass
        // an existing named array through the `params` parameter and mutate an element afterward,
        // which would silently change an already-configured response and violate
        // ReturnConfig<T>.NextSequenceOutcome()'s lock-free-safety premise that the sequence is
        // immutable once configured. Snapshot it.
        _slot.Sequence = (SequenceOutcome<T>[])outcomes.Clone();
        _slot.SequenceOrdinal = 0;
        _slot.HasValue = false;
        _slot.Value = default;
        _slot.Exception = null;
    }
}
