using Compono;

namespace Compono.Providers;

/// <summary>
/// Stage 7 built-in provider composing a <c>Nullable&lt;T&gt;</c>'s underlying value type - never
/// <see langword="null"/> itself. Whether/how often a nullable request should compose
/// <see langword="null"/> is a still-open <c>docs/mvp.md</c> "nullability generation defaults" item
/// this phase doesn't resolve (<c>docs/adr/0013-collection-generation-semantics.md</c>).
/// </summary>
internal sealed class NullableValueProvider : ICompositionProvider
{
    public CompositionResult TryCompose(CompositionRequest request, ICompositionContext context)
    {
        var underlyingType = Nullable.GetUnderlyingType(request.RequestedType);

        if (underlyingType is null)
            return CompositionResult.NotHandled.Instance;

        var random = ((CompositionContext)context).Random;

        // A Nullable<T>'s non-null value boxes as a plain boxed T (a CLR nullable-boxing rule), so a
        // boxed underlying value from either provider below is already the correct Success payload -
        // CompositionContext's unboxing cast back to Nullable<T> at the Resolve<TValue>() call site
        // handles the rest.
        if (PrimitiveValueProvider.TryComposeValue(underlyingType, random, out var primitiveValue))
            return new CompositionResult.Success(primitiveValue);

        if (EnumValueProvider.TryComposeValue(underlyingType, random, out var enumValue))
            return new CompositionResult.Success(enumValue);

        return CompositionResult.NotHandled.Instance;
    }
}
