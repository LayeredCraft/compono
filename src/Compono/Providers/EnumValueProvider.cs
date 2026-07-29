using Compono;

namespace Compono.Providers;

/// <summary>
/// Stage 7 built-in provider composing a random valid member of any enum type, via
/// <see cref="IRandomSource"/>.
/// </summary>
internal sealed class EnumValueProvider : ICompositionProvider
{
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

        var values = Enum.GetValues(type);

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
