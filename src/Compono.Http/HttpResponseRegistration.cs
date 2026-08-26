namespace Compono.Http;

/// <summary>
/// One <c>OnGet</c>/<c>OnPost</c>/.../<c>When</c> configuration on a <see cref="TestHttpHandler"/> -
/// the handle returned from configuring a response, and the verification identity for it. Owns its
/// matcher, its response-factory behavior, and its own matched-call count - see ADR-0051
/// "Registration handle, not a re-declared matcher" for why verification is a method on this type
/// rather than a second predicate declared inside <c>Verify(...)</c>.
/// </summary>
public sealed class HttpResponseRegistration
{
    private readonly Func<HttpRequestMessage, bool> _matcher;
    private readonly string _description;
    private Func<HttpRequestMessage, HttpResponseMessage>? _responseFactory;
    private int _matchedCallCount;

    internal HttpResponseRegistration(Func<HttpRequestMessage, bool> matcher, string description)
    {
        _matcher = matcher;
        _description = description;
    }

    /// <summary>Whether <paramref name="request"/> matches this registration's configured condition.</summary>
    internal bool Matches(HttpRequestMessage request) => _matcher(request);

    /// <summary>
    /// Sets this registration's response behavior - a factory describing *how to build* a response,
    /// never a stored instance, so every matched invocation gets a fresh <see cref="HttpResponseMessage"/>/
    /// content. See ADR-0051 "Response state: factory, not instance". Called exactly once, by the
    /// <see cref="HttpResponseRegistrationBuilder"/> method (<c>Respond</c>/<c>RespondText</c>/
    /// <c>RespondJson</c>/<c>Throws</c>) that finalizes this registration.
    /// </summary>
    internal void SetResponseFactory(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        _responseFactory = responseFactory;

    /// <summary>
    /// Builds this registration's response for <paramref name="request"/> - a fresh
    /// <see cref="HttpResponseMessage"/>/content per call, or throws the configured exception
    /// (the same instance every time - see ADR-0051's verified <c>Throws</c> semantics).
    /// </summary>
    // Safe: TestHttpHandler.SendAsync only calls this after the registration was returned from a
    // finalizing OnX(...)/When(...).Respond*/Throws call, which is the only way to obtain a
    // reference to this type - SetResponseFactory has always already run by then.
    internal HttpResponseMessage CreateResponse(HttpRequestMessage request) => _responseFactory!(request);

    /// <summary>Records one matched call - dispatch-thread-safe via <see cref="Interlocked.Increment(ref int)"/>.</summary>
    internal void RecordMatch() => Interlocked.Increment(ref _matchedCallCount);

    /// <summary>
    /// Asserts how many times this registration matched a request, reusing the core
    /// <see cref="Compono.CallVerifier"/> type unchanged - <c>Never()</c>/<c>Once()</c>/
    /// <c>Exactly(n)</c>. Kept deliberately separate from <see cref="TestHttpHandler.Requests"/>,
    /// which answers a different question ("what was actually sent") - see ADR-0051 "Kept separate:
    /// global request-log inspection".
    /// </summary>
    public CallVerifier Verify() => new(_matchedCallCount, _description);
}
