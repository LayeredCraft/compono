namespace Compono;

/// <summary>
/// Stores at most one shared value per requested type, for the lifetime of one root composition
/// operation.
/// </summary>
/// <remarks>
/// One instance per <see cref="CompositionContext"/> - a <see cref="CompositionContext"/> is itself
/// scoped to one root operation (one <see cref="Composer.Create{T}"/> call, or one
/// <c>CreateMany&lt;T&gt;()</c> item), so this type needs no push/pop lifetime of its own; it simply
/// lives as long as its owning context does. Sharing is type-keyed only in Milestone 2 -
/// name/qualifier-keyed sharing is Milestone 4 scope, per
/// <c>docs/adr/0011-composition-scope-shared-values-and-recursion-detection.md</c>.
/// </remarks>
internal sealed class CompositionScope
{
    private readonly Dictionary<Type, object?> _values = [];

    /// <summary>Attempts to read the value already shared for <paramref name="type"/> in this scope.</summary>
    internal bool TryGet(Type type, out object? value) => _values.TryGetValue(type, out value);

    /// <summary>Stores <paramref name="value"/> as the shared value for <paramref name="type"/> in this scope.</summary>
    internal void Set(Type type, object? value) => _values[type] = value;
}
