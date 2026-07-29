using System.Runtime.CompilerServices;
using Compono;

namespace Compono.Providers;

/// <summary>
/// Stage 7 built-in provider composing a random valid member of any enum type, via
/// <see cref="IRandomSource"/>.
/// </summary>
/// <remarks>
/// Reflection-free/AOT-safe by construction: <see cref="Enum.GetValues(Type)"/> (the non-generic,
/// <see langword="Type"/>-based overload) is annotated <c>[RequiresDynamicCode]</c> - the runtime may
/// need to construct an array of the dynamically-supplied enum type, which breaks under Native AOT.
/// <see cref="Enum.GetValuesAsUnderlyingType(Type)"/> and <see cref="Enum.ToObject(Type, object)"/>
/// carry no such annotation (the array/boxed value they produce is of the enum's already-known
/// primitive underlying type, never the enum type itself, so no dynamic array-of-enum-type
/// construction is ever needed) - this provider uses only those two instead. PR #11 review caught the
/// original <c>Enum.GetValues(Type)</c> use as a real violation of ADR-0001's no-reflection-by-default
/// rule, the same rule ADR-0010's third amendment already retracted a reflection-based collection
/// bridge over.
/// </remarks>
internal sealed class EnumValueProvider : ICompositionProvider
{
    // Enum.GetValuesAsUnderlyingType(Type) allocates a fresh array on every call - caching per enum
    // type keeps the resolution hot path from re-allocating and re-copying the same metadata-derived
    // array for every resolved value of a given enum type. ConditionalWeakTable, not
    // ConcurrentDictionary<Type, Array> - a long-lived host that loads/unloads consumer assemblies via
    // a collectible AssemblyLoadContext would otherwise have this cache strongly root every enum Type
    // it ever composed for the process lifetime, preventing those assemblies from ever unloading. A
    // conditional weak table's keys don't root anything - once nothing else references a given enum
    // Type (e.g. its collectible assembly unloads), this cache entry collects with it.
    private static readonly ConditionalWeakTable<Type, Array> UnderlyingValuesByType = new();

    public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context)
    {
        var random = ((CompositionContext)context).Random;

        return TryComposeValue(request.RequestedType, random, out var value)
            ? new CompositionResult.Success(value)
            : CompositionResult.NotHandled.Instance;
    }

    // Shared with NullableValueProvider - see PrimitiveValueProvider.TryComposeValue's remarks. The
    // boxed result here must be Enum.ToObject's output specifically (boxed as the actual enum type),
    // not a boxed underlying-type value directly - a boxed int, say, unboxes correctly to a
    // non-nullable enum type (the CLR's enum-unboxing rule accepts a same-size underlying-type box)
    // but does NOT unbox correctly to a Nullable<TEnum> target (confirmed: throws InvalidCastException
    // - nullable unboxing requires the box's exact runtime type to match), which NullableValueProvider
    // needs to work for Resolve<TEnum?>().
    internal static bool TryComposeValue(Type type, IRandomSource random, out object? value)
    {
        if (!type.IsEnum)
        {
            value = null;
            return false;
        }

        var underlyingValues = UnderlyingValuesByType.GetValue(type, static t => Enum.GetValuesAsUnderlyingType(t));

        if (underlyingValues.Length == 0)
        {
            value = null;
            return false;
        }

        var index = (int)(random.NextUInt64() % (ulong)underlyingValues.Length);
        value = Enum.ToObject(type, underlyingValues.GetValue(index)!);
        return true;
    }
}
