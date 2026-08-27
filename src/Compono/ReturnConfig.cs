namespace Compono;

/// <summary>
/// Per-member configured-return state for a generator-emitted test double, one instance per
/// double member. Backing fields are <see langword="internal"/> - only <see cref="ReturnConfigBuilder{T}"/>,
/// same assembly, ever writes them - but the read side is <see langword="public"/> because the
/// generated dispatch code reading a slot's configured state lives in a different (consumer)
/// assembly. See ADR-0043 Amendment 3, Finding A.
/// </summary>
public struct ReturnConfig<T>
{
    internal bool HasValue;
    internal T? Value;
    internal Exception? Exception;
    internal int CallCount;

    // ADR-0054: sequential/call-count-based responses. Null except after
    // ReturnConfigBuilder<T>.ReturnsSequence - the same slot Returns/Throws already use, extended
    // with a third, mutually-exclusive state rather than a separate parallel type (see that ADR's
    // "one remaining open design question" section, resolved by this shape composing cleanly with
    // the existing Value/Exception fields and this struct's own already-Interlocked-based
    // RecordCall() precedent). Immutable once set (ReturnsSequence always replaces the whole array,
    // never mutates an element) - safe to read from multiple threads with no lock, only the ordinal
    // claim below needs synchronization.
    internal SequenceOutcome<T>[]? Sequence;
    internal int SequenceOrdinal;

    /// <summary>Whether <see cref="ConfiguredValue"/> was set via <see cref="ReturnConfigBuilder{T}.Returns"/>.</summary>
    public readonly bool HasConfiguredValue => HasValue;

    /// <summary>Whether <see cref="ConfiguredException"/> was set via <see cref="ReturnConfigBuilder{T}.Throws"/>.</summary>
    public readonly bool HasConfiguredException => Exception is not null;

    /// <summary>Whether a response sequence was set via <see cref="ReturnConfigBuilder{T}.ReturnsSequence"/>.</summary>
    public readonly bool HasConfiguredSequence => Sequence is not null;

    /// <summary>
    /// The value configured via <see cref="ReturnConfigBuilder{T}.Returns"/>. Only meaningful when
    /// <see cref="HasConfiguredValue"/> is <see langword="true"/>.
    /// </summary>
    // Safe: generated dispatch code only reads this when HasConfiguredValue is true, the same
    // TryGetValue-style contract every other guarded accessor in this codebase follows.
    public readonly T ConfiguredValue => Value!;

    /// <summary>
    /// The exception configured via <see cref="ReturnConfigBuilder{T}.Throws"/>. Only meaningful
    /// when <see cref="HasConfiguredException"/> is <see langword="true"/>.
    /// </summary>
    // Safe: generated dispatch code only reads this when HasConfiguredException is true.
    public readonly Exception ConfiguredException => Exception!;

    /// <summary>The number of times this member's dispatch body has actually run, read by <see cref="CallVerifier"/>.</summary>
    public readonly int ConfiguredCallCount => CallCount;

    /// <summary>
    /// Records one call to this member. Generated dispatch code always calls this rather than
    /// incrementing <see cref="CallCount"/> directly - that field is <see langword="internal"/> and
    /// unwritable from the consumer assembly the generated code actually lives in. See ADR-0044
    /// Amendment 2, Finding 1.
    /// </summary>
    public void RecordCall() => System.Threading.Interlocked.Increment(ref CallCount);

    /// <summary>
    /// Consumes and returns (or throws) the next outcome in the configured sequence, by invocation
    /// ordinal - the first call gets index 0, the second index 1, and so on. Only meaningful when
    /// <see cref="HasConfiguredSequence"/> is <see langword="true"/>. Once the sequence is exhausted,
    /// every further call repeats the final configured outcome (ADR-0054's chosen exhaustion
    /// semantics, matching NSubstitute's own established <c>Returns(a, b, c)</c> behavior).
    /// </summary>
    /// <remarks>
    /// Thread-safe with no lock: <see cref="Sequence"/> is never mutated after
    /// <see cref="ReturnConfigBuilder{T}.ReturnsSequence"/> sets it (a reconfiguration replaces the
    /// whole array reference, never edits an element in place), so the only shared mutable state is
    /// the ordinal itself - claimed via <see cref="System.Threading.Interlocked.Increment(ref int)"/>,
    /// the same primitive <see cref="RecordCall"/> already uses, so two concurrent callers always
    /// claim two distinct, strictly-increasing ordinals and never observe or corrupt each other's
    /// index.
    /// </remarks>
    public T NextSequenceOutcome()
    {
        var outcomes = Sequence!;
        var ordinal = System.Threading.Interlocked.Increment(ref SequenceOrdinal) - 1;
        var index = ordinal >= outcomes.Length ? outcomes.Length - 1 : ordinal;
        return outcomes[index].Resolve();
    }
}
