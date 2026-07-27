// Portions of this file are derived from roslyn-analyzers
// Source:
// https://github.com/dotnet/roslyn-analyzers/blob/9b58ec3ad33353d1a523cda8c4be38eaefc80ad8/src/Utilities/Compiler/BoundedCacheWithFactory.cs
// Copyright (c) .NET Foundation and Contributors
// Licensed under the MIT License

namespace Compono.Generators.WellKnownTypes;

/// <summary>
/// Provides a bounded cache for analyzers/generators. A good alternative to
/// <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey,TValue}"/> when the cached
/// value has a cyclic reference to the key, which would otherwise prevent early garbage collection.
/// </summary>
internal sealed class BoundedCacheWithFactory<TKey, TValue>
    where TKey : class
{
    // Bounded weak-reference cache. Size 5 is an arbitrarily chosen bound, tunable if needed.
    private readonly List<WeakReference<Entry?>> _weakReferencedEntries =
    [
        new WeakReference<Entry?>(null),
        new WeakReference<Entry?>(null),
        new WeakReference<Entry?>(null),
        new WeakReference<Entry?>(null),
        new WeakReference<Entry?>(null),
    ];

    public TValue GetOrCreateValue(TKey key, Func<TKey, TValue> valueFactory)
    {
        lock (_weakReferencedEntries)
        {
            var indexToSetTarget = -1;

            for (var i = 0; i < _weakReferencedEntries.Count; i++)
            {
                var weakReferencedEntry = _weakReferencedEntries[i];

                if (!weakReferencedEntry.TryGetTarget(out var cachedEntry) || cachedEntry is null)
                {
                    if (indexToSetTarget == -1)
                        indexToSetTarget = i;

                    continue;
                }

                if (Equals(cachedEntry.Key, key))
                {
                    // Move the cache hit to the end so it's least likely to be evicted next.
                    _weakReferencedEntries.RemoveAt(i);
                    _weakReferencedEntries.Add(weakReferencedEntry);
                    return cachedEntry.Value;
                }
            }

            if (indexToSetTarget == -1)
                indexToSetTarget = 0;

            var newEntry = new Entry(key, valueFactory(key));
            _weakReferencedEntries[indexToSetTarget].SetTarget(newEntry);
            return newEntry.Value;
        }
    }

    private sealed class Entry(TKey key, TValue value)
    {
        public TKey Key { get; } = key;

        public TValue Value { get; } = value;
    }
}
