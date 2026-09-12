using System.Text.Json;
using Compono.Http;

namespace WithJsonBodyOptionsOverloadCaller;

internal sealed record OrderDto(int CustomerId, string Sku);

internal static class Program
{
    private static void Main()
    {
        using var handler = new TestHttpHandler();

        // The one call this whole project exists to compile: WithJsonBody<T>(predicate,
        // JsonSerializerOptions?) - expected to warn IL2026 + IL3050 right here, at this call site,
        // not silently inside Compono.Http (ADR-0062's verified attribute-propagation contract).
        handler.OnPost("/orders").WithJsonBody<OrderDto>(o => o?.CustomerId == 42, (JsonSerializerOptions?)null);
    }
}
