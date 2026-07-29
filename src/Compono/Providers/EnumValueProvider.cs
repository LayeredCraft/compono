using System.Collections.Concurrent;
using Compono;

namespace Compono.Providers;

/// <summary>
/// Stage 7 built-in provider composing a random valid member of any enum type, via
/// <see cref="IRandomSource"/>.
/// </summary>
internal sealed class EnumValueProvider : ICompositionProvider
{
    // Enum.GetValues(Type) allocates a fresh array on every call - caching per enum type keeps the
    // resolution hot path from re-allocating and re-copying the same metadata-derived array for every
    // resolved value of a given enum type. Lock-free (ConcurrentDictionary), per coding-standards.md's
    // "shared mutable state" guidance - the cache is populated at most once per distinct enum type.
    private static readonly ConcurrentDictionary<Type, Array> ValuesByType = new();

    public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context)
    {
        var random = ((CompositionContext)context).Random;

        return TryComposeValue(request.RequestedType, random, out var value)
            ? new CompositionResult.Success(value)
            : CompositionResult.NotHandled.Instance;
    }

    // Shared with NullableValueProvider - see PrimitiveValueProvider.TryComposeValue's remarks.
    internal static bool TryComposeValue(Type type, IRandomSource random, out object? value)
    {
        if (!type.IsEnum)
        {
            value = null;
            return false;
        }

        var values = ValuesByType.GetOrAdd(type, static t => Enum.GetValues(t));

        if (values.Length == 0)
        {
            value = null;
            return false;
        }

        var index = (int)(random.NextUInt64() % (ulong)values.Length);
        value = values.GetValue(index);
        return true;
    }
}
