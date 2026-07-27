// Vendored from LayeredCraft.SourceGeneratorTools
// Source: https://github.com/LayeredCraft/source-generator-tools/blob/main/src/LayeredCraft.SourceGeneratorTools/Types/EquatableArray/EquatableArray.cs
// which itself credits https://github.com/andrewlock/StronglyTypedId/blob/master/src/StronglyTypedIds/EquatableArray.cs
// Copyright (c) 2025 LayeredCraft
// Licensed under the MIT License
//
// Vendored rather than referenced as a package dependency (see
// docs/adr/0005-generator-implementation-conventions.md) - Compono.Generators keeps its own
// copy instead of taking a dependency on the shared package.

using System.Collections;

namespace Compono.Generators.Types;

/// <summary>
/// An immutable, equatable array - equivalent to <c>T[]</c> but with value equality, so it's safe to
/// use inside an incremental-generator pipeline model that needs correct structural equality for
/// Roslyn's caching (a plain array/<c>List&lt;T&gt;</c> compares by reference).
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new([]);

    private readonly T[]? _array;

    public EquatableArray(T[] array) => _array = array;

    public int Count => _array?.Length ?? 0;

    public T this[int index] => (_array ?? [])[index];

    public bool Equals(EquatableArray<T> array) => AsSpan().SequenceEqual(array.AsSpan());

    public override bool Equals(object? obj) => obj is EquatableArray<T> array && Equals(array);

    public override int GetHashCode()
    {
        if (_array is not { } array)
            return 0;

        var hashCode = default(HashCode);

        foreach (var item in array)
            hashCode.Add(item);

        return hashCode.ToHashCode();
    }

    public ReadOnlySpan<T> AsSpan() => _array.AsSpan();

    public T[]? GetArray() => _array;

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)(_array ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)(_array ?? [])).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
