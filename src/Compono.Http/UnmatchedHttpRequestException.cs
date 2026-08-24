namespace Compono.Http;

/// <summary>
/// Thrown by <see cref="TestHttpHandler"/> when a request reaches <c>SendAsync</c> and no
/// configured <see cref="HttpResponseRegistration"/> matches it. <see cref="TestHttpHandler"/> is
/// strict by default - see ADR-0051 "Unmatched requests: strict by default" - so this never
/// silently falls back to a fabricated response; a consumer that wants a fallback configures one
/// explicitly via <c>handler.When(_ => true)</c>.
/// </summary>
public sealed class UnmatchedHttpRequestException : Exception
{
    /// <summary>Creates an exception describing the unmatched request's method and URI.</summary>
    public UnmatchedHttpRequestException(HttpMethod method, Uri? requestUri)
        : base(BuildMessage(method, requestUri))
    {
    }

    private static string BuildMessage(HttpMethod method, Uri? requestUri) =>
        $"No configured registration matched {method.Method} {requestUri?.ToString() ?? "(no request URI)"}.";
}
