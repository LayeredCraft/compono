namespace Compono;

/// <summary>
/// An internal, type-keyed store of exact registered values - the mechanism pipeline stage 3
/// (<c>docs/architecture.md</c>) queries.
/// </summary>
/// <remarks>
/// Mechanism only, per
/// <c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>'s scope note - no
/// public <c>builder.Register(...)</c> surface exists until Milestone 3. This type exists purely so
/// <c>Compono.Tests</c> can exercise stage 3 directly via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class CompositionRegistrations
{
    /// <summary>A store with nothing registered.</summary>
    internal static readonly CompositionRegistrations Empty = new(new Dictionary<Type, object?>());

    private readonly IReadOnlyDictionary<Type, object?> _values;

    /// <summary>Creates a <see cref="CompositionRegistrations"/> store from an explicit type-keyed map.</summary>
    internal CompositionRegistrations(IReadOnlyDictionary<Type, object?> values)
    {
        _values = values;
    }

    /// <summary>Attempts to read the registered value for <paramref name="type"/>.</summary>
    internal bool TryGet(Type type, out object? value) => _values.TryGetValue(type, out value);
}
