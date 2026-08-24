using System.Text.Json;
using Compono.Http;

namespace OptionsOverloadCaller;

internal sealed record UserResponse(string Name, int Value);

internal static class Program
{
    private static void Main()
    {
        using var handler = new TestHttpHandler();

        // The one call this whole project exists to compile: RespondJson(value,
        // JsonSerializerOptions?) - expected to warn IL2026 + IL3050 right here, at this call
        // site, not silently inside Compono.Http (ADR-0051's verified attribute-propagation
        // contract).
        handler.OnGet("/users/42").RespondJson(new UserResponse("Ada", 42), (JsonSerializerOptions?)null);
    }
}
