using System.Text.Json.Serialization;
using Compono.Http;

namespace WithJsonBodyTypeInfoOverloadCaller;

internal sealed record OrderDto(int CustomerId, string Sku);

[JsonSerializable(typeof(OrderDto))]
internal partial class WithJsonBodyTypeInfoCallerJsonContext : JsonSerializerContext
{
}

internal static class Program
{
    private static void Main()
    {
        using var handler = new TestHttpHandler();

        // The one call this whole project exists to compile: WithJsonBody<T>(predicate,
        // JsonTypeInfo<T>) - expected to produce zero IL2026/IL3050 warnings.
        handler.OnPost("/orders").WithJsonBody<OrderDto>(o => o?.CustomerId == 42, WithJsonBodyTypeInfoCallerJsonContext.Default.OrderDto);
    }
}
