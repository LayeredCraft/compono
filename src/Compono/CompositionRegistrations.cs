using System.Diagnostics.CodeAnalysis;

namespace Compono;

/// <summary>
/// An internal, type-keyed store of exact-registration factories - the mechanism pipeline stage 3
/// (<c>docs/architecture.md</c>) queries.
/// </summary>
/// <remarks>
/// Every entry is a type-erased factory (<c>Func&lt;ICompositionContext, object?&gt;</c>) - a plain
/// value registered via <c>CompositionBuilder.Register&lt;T&gt;(Func&lt;T&gt;)</c> is stored as a
/// trivial <c>_ =&gt; value</c> wrapper, so stage 3 has exactly one invocation path (through
/// <see cref="ICompositionContext"/>, per
/// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c>) regardless of which
/// <c>Register&lt;T&gt;</c> overload produced the entry. Immutable once built - <c>CompositionBuilder</c>
/// accumulates registrations separately and hands this type a finished map.
/// </remarks>
internal sealed class CompositionRegistrations
{
    /// <summary>A store with nothing registered.</summary>
    internal static readonly CompositionRegistrations Empty = new(new Dictionary<Type, Func<ICompositionContext, object?>>());

    private readonly IReadOnlyDictionary<Type, Func<ICompositionContext, object?>> _factories;

    /// <summary>Creates a <see cref="CompositionRegistrations"/> store from an explicit type-keyed factory map.</summary>
    internal CompositionRegistrations(IReadOnlyDictionary<Type, Func<ICompositionContext, object?>> factories)
    {
        _factories = factories;
    }

    /// <summary>Attempts to read the registered factory for <paramref name="type"/>.</summary>
    internal bool TryGet(Type type, [NotNullWhen(true)] out Func<ICompositionContext, object?>? factory) =>
        _factories.TryGetValue(type, out factory);
}
