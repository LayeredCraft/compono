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

    /// <summary>Whether <see cref="ConfiguredValue"/> was set via <see cref="ReturnConfigBuilder{T}.Returns"/>.</summary>
    public readonly bool HasConfiguredValue => HasValue;

    /// <summary>Whether <see cref="ConfiguredException"/> was set via <see cref="ReturnConfigBuilder{T}.Throws"/>.</summary>
    public readonly bool HasConfiguredException => Exception is not null;

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
}
