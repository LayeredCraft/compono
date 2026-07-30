using System.Collections.ObjectModel;

namespace Compono;

/// <summary>
/// Copies a caller-supplied sequence into a genuinely immutable snapshot - not just an
/// <see cref="IReadOnlyList{T}"/>-typed view over a plain array or list a caller could cast back to
/// and mutate.
/// </summary>
/// <remarks>
/// A collection expression assigned to an <see cref="IReadOnlyList{T}"/>-typed property compiles to a
/// plain array under the interface - read-only *as viewed through that interface*, but a caller who
/// casts back to the concrete array type can still mutate it. <see cref="ReadOnlyCollection{T}"/>
/// wraps a private array reference this type never exposes, so there is no concrete type a caller
/// could cast back to and mutate through. Used everywhere this codebase's public configuration-error
/// types need to defend against a caller mutating a list after handing it to a constructor.
/// </remarks>
internal static class ImmutableSnapshot
{
    /// <summary>Copies <paramref name="source"/> into a genuinely immutable snapshot.</summary>
    internal static IReadOnlyList<T> Of<T>(IReadOnlyList<T> source) => new ReadOnlyCollection<T>([.. source]);
}
