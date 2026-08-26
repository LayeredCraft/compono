using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Compono.Http.AotSmokeTest;

internal sealed record UserResponse(string Name, int Value);

[JsonSerializable(typeof(UserResponse))]
internal partial class SmokeTestJsonContext : JsonSerializerContext
{
}

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            using var handler = new TestHttpHandler();

            // ADR-0051 Decision Outcome: exact-path OnGet + Match<string>, RespondJson via the
            // guaranteed-AOT-safe JsonTypeInfo<T> overload - deliberately the ONLY RespondJson
            // overload exercised here (Task 5's Proof B does not require the
            // JsonSerializerOptions-based overload to publish warning-free under Native AOT; its
            // separate analyzer-contract proof, in AnalyzerContract/, covers that overload).
            var expected = new UserResponse("Ada", 42);
            // Broad fallback registered FIRST, specific override registered SECOND - last-match-
            // wins dispatch (ADR-0051) walks last-registered-first, so the later, more specific
            // registration below correctly overrides this broader one for /users/42, while every
            // other request still falls through to it.
            handler.When(_ => true).Respond(HttpStatusCode.InternalServerError);
            var registration = handler.OnGet("/users/42")
                .RespondJson(expected, SmokeTestJsonContext.Default.UserResponse);

            using var client = handler.CreateClient(new Uri("https://api.example.com/"));
            var response = await client.GetAsync("/users/42");
            var body = await response.Content.ReadFromJsonAsync(SmokeTestJsonContext.Default.UserResponse);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Expected 200 OK, got {response.StatusCode}.");

            if (body is null || body.Name != "Ada" || body.Value != 42)
                throw new InvalidOperationException($"Expected UserResponse(Ada, 42), got {body}.");

            // Registration verification, reusing core CallVerifier unchanged.
            registration.Verify().Once();

            // The broader fallback still covers everything else.
            var fallback = await client.GetAsync("/anything-else");
            if (fallback.StatusCode != HttpStatusCode.InternalServerError)
                throw new InvalidOperationException($"Expected the explicit fallback to respond 500, got {fallback.StatusCode}.");

            // Strict unmatched-request behavior, on a handler with no fallback configured, and
            // Requests still recording the request that caused it to throw.
            using var strictHandler = new TestHttpHandler();
            using var strictClient = strictHandler.CreateClient(new Uri("https://api.example.com/"));
            var strictThrew = false;
            try
            {
                await strictClient.GetAsync("/unconfigured");
            }
            catch (UnmatchedHttpRequestException)
            {
                strictThrew = true;
            }

            if (!strictThrew)
                throw new InvalidOperationException("Expected an unmatched request to throw UnmatchedHttpRequestException.");

            if (strictHandler.Requests.Count != 1)
                throw new InvalidOperationException("Expected the unmatched request to still appear in Requests.");

            // Throws(exception): the exact same instance is rethrown on every match.
            var exception = new HttpRequestException("simulated failure", null, HttpStatusCode.NotFound);
            strictHandler.OnGet("/error").Throws(exception);
            Exception? caught = null;
            try
            {
                await strictClient.GetAsync("/error");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            if (!ReferenceEquals(caught, exception))
                throw new InvalidOperationException("Expected Throws(exception) to rethrow the exact same instance.");

            Console.WriteLine(
                $"PASS: TestHttpHandler (OnGet + Match<string>, RespondJson via JsonTypeInfo<T>, " +
                $"last-match-wins, strict UnmatchedHttpRequestException with Requests still " +
                $"recording it, registration.Verify(), Throws same-instance rethrow) survived " +
                $"Native AOT through the packaged Compono.Http dependency chain - body={body}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }
}
