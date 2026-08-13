namespace Compono;

/// <summary>
/// Public write surface over a single <see cref="ReturnConfig{T}"/> slot - constructed by
/// generator-emitted configuration extensions (<c>Configure().Member()</c>) in the consumer's own
/// assembly, per ADR-0043. A <see langword="ref struct"/> because it only ever wraps a
/// <see langword="ref"/> to a field already living on the generated double instance; it's never
/// stored, only used inline at the call site.
/// </summary>
/// <remarks>
/// <see cref="Returns"/>/<see cref="Throws"/> are last-configuration-wins: each clears the other's
/// state, so configuring a return after an earlier <see cref="Throws"/> (or vice versa) doesn't
/// leave stale state behind. See ADR-0043 Amendment 7, Finding R.
/// </remarks>
public readonly ref struct ReturnConfigBuilder<T>
{
    private readonly ref ReturnConfig<T> _slot;

    /// <summary>Wraps <paramref name="slot"/>, the generated double's own backing field for this member.</summary>
    public ReturnConfigBuilder(ref ReturnConfig<T> slot) => _slot = ref slot;

    /// <summary>Configures the member to return <paramref name="value"/>, clearing any prior <see cref="Throws"/>.</summary>
    public void Returns(T value)
    {
        _slot.Value = value;
        _slot.HasValue = true;
        _slot.Exception = null;
    }

    /// <summary>Configures the member to throw <paramref name="exception"/>, clearing any prior <see cref="Returns"/>.</summary>
    public void Throws(Exception exception)
    {
        _slot.Exception = exception;
        _slot.HasValue = false;
        _slot.Value = default;
    }
}
