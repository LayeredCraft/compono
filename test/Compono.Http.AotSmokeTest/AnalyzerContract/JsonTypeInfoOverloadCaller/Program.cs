using System.Text.Json.Serialization;
using Compono.Http;

namespace JsonTypeInfoOverloadCaller;

internal sealed record UserResponse(string Name, int Value);

[JsonSerializable(typeof(UserResponse))]
internal partial class JsonTypeInfoCallerJsonContext : JsonSerializerContext
{
}

internal static class Program
{
    private static void Main()
    {
        using var handler = new TestHttpHandler();

        // The one call this whole project exists to compile: RespondJson(value, JsonTypeInfo<T>)
        // - expected to produce zero IL2026/IL3050 warnings.
        handler.OnGet("/users/42").RespondJson(new UserResponse("Ada", 42), JsonTypeInfoCallerJsonContext.Default.UserResponse);
    }
}
