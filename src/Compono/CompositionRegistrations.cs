using System.Collections.ObjectModel;
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
    /// <remarks>
    /// Defensively copies <paramref name="factories"/> into a genuinely immutable snapshot - a
    /// <see cref="ReadOnlyDictionary{TKey,TValue}"/> wrapping a freshly-copied <see cref="Dictionary{TKey,TValue}"/>
    /// this type never exposes, mirroring <see cref="ImmutableSnapshot"/>'s guarantee for
    /// <see cref="IReadOnlyList{T}"/> collections elsewhere in this codebase. Required because
    /// <c>CompositionBuilder.Build()</c> passes its own live, mutable accumulator dictionary here - a
    /// consumer that captures the <c>CompositionBuilder</c> out of the configuration callback (e.g.
    /// assigning it to an outer-scope field) could otherwise keep mutating it via
    /// <c>Register&lt;T&gt;</c> after <c>Composer.Create(...)</c> has already returned, silently
    /// violating <c>docs/adr/0017-immutable-composer-configuration-and-builder-model.md</c>'s frozen-
    /// configuration guarantee and racing with concurrent resolution.
    /// </remarks>
    internal CompositionRegistrations(IReadOnlyDictionary<Type, Func<ICompositionContext, object?>> factories)
    {
        _factories = new ReadOnlyDictionary<Type, Func<ICompositionContext, object?>>(
            new Dictionary<Type, Func<ICompositionContext, object?>>(factories));
    }

    /// <summary>Attempts to read the registered factory for <paramref name="type"/>.</summary>
    internal bool TryGet(Type type, [NotNullWhen(true)] out Func<ICompositionContext, object?>? factory) =>
        _factories.TryGetValue(type, out factory);
}
