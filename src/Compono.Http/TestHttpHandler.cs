namespace Compono.Http;

/// <summary>
/// A reflection-free <see cref="HttpMessageHandler"/> fake for testing code built on
/// <see cref="HttpClient"/> - the sanctioned .NET substitution seam (ADR-0051 "Core abstraction").
/// Configure responses with <c>OnGet</c>/<c>OnPost</c>/<c>OnPut</c>/<c>OnPatch</c>/<c>OnDelete</c>
/// (exact-method, <see cref="Match{T}"/>-based path matching) or <c>When</c> (a whole-request
/// predicate), then build one or more <see cref="HttpClient"/>s over this handler via
/// <see cref="CreateClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Matching</b>: registrations are evaluated last-registered-first, first match wins (ADR-0051
/// "Precedence") - a later registration overrides an earlier, broader one. A request matching no
/// registration throws <see cref="UnmatchedHttpRequestException"/> rather than a fabricated
/// response (ADR-0051 "Unmatched requests: strict by default"); configure an explicit fallback with
/// <c>handler.When(_ =&gt; true)</c> if one is wanted.
/// </para>
/// <para>
/// <b>Lifetime</b>: this handler is caller-owned. Compono composition (<c>[Shared]</c>/
/// <c>CompositionRow</c>) does not own or dispose it - see ADR-0051 "Lifecycle, disposal,
/// concurrency". Every <see cref="HttpClient"/> from <see cref="CreateClient"/> is constructed with
/// <c>disposeHandler: false</c>, so disposing a client never disposes this handler, and multiple
/// clients may safely share one handler. Disposing this handler invalidates subsequent sends
/// through any client still wrapping it.
/// </para>
/// <para>
/// <b>Concurrency</b>: configure registrations before the system under test runs - configuration
/// concurrent with <see cref="SendAsync"/> calls is unsupported/not guaranteed. Concurrent
/// <see cref="SendAsync"/> calls themselves are fully supported.
/// </para>
/// </remarks>
public sealed class TestHttpHandler : HttpMessageHandler
{
    private readonly List<HttpResponseRegistration> _registrations = [];
    // Plain object, not System.Threading.Lock - this project multi-targets net8.0, which predates
    // that type (introduced in .NET 9).
    private readonly object _requestsLock = new();
    private readonly List<HttpRequestMessage> _requests = [];
    private volatile bool _disposed;

    /// <summary>
    /// Every request that has reached this handler's <see cref="SendAsync"/>, in arrival order,
    /// matched or not - a fresh point-in-time snapshot on every access, never a live view over the
    /// mutable backing log (ADR-0051 "Request log"). Kept deliberately separate from
    /// <see cref="HttpResponseRegistration.Verify"/> - this answers "what did the system under test
    /// actually send," not "how many times did one configured behavior match."
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_requestsLock)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <summary>
    /// Matches an HTTP GET whose request URI's path/query equals <paramref name="path"/> exactly -
    /// the normal, common-case entry point. Preserves <paramref name="path"/> verbatim in
    /// <see cref="HttpResponseRegistration.Verify"/>'s diagnostics (e.g. <c>GET /v1/customers/42</c>),
    /// unlike the <see cref="Match{T}"/> overload below, which can't (ADR-0051 Amendment 1).
    /// </summary>
    public HttpResponseRegistrationBuilder OnGet(string path) => On(HttpMethod.Get, path);

    /// <summary>Matches an HTTP GET whose request URI's path/query satisfies <paramref name="path"/> (<see cref="Match.Any{T}"/>/<see cref="Match.Is{T}"/>).</summary>
    public HttpResponseRegistrationBuilder OnGet(Match<string> path) => On(HttpMethod.Get, path);

    /// <summary>
    /// Matches an HTTP POST whose request URI's path/query equals <paramref name="path"/> exactly -
    /// see <see cref="OnGet(string)"/>'s remarks (same rationale, every <c>OnX</c> method).
    /// </summary>
    public HttpResponseRegistrationBuilder OnPost(string path) => On(HttpMethod.Post, path);

    /// <summary>Matches an HTTP POST whose request URI's path/query satisfies <paramref name="path"/> (<see cref="Match.Any{T}"/>/<see cref="Match.Is{T}"/>).</summary>
    public HttpResponseRegistrationBuilder OnPost(Match<string> path) => On(HttpMethod.Post, path);

    /// <summary>
    /// Matches an HTTP PUT whose request URI's path/query equals <paramref name="path"/> exactly -
    /// see <see cref="OnGet(string)"/>'s remarks (same rationale, every <c>OnX</c> method).
    /// </summary>
    public HttpResponseRegistrationBuilder OnPut(string path) => On(HttpMethod.Put, path);

    /// <summary>Matches an HTTP PUT whose request URI's path/query satisfies <paramref name="path"/> (<see cref="Match.Any{T}"/>/<see cref="Match.Is{T}"/>).</summary>
    public HttpResponseRegistrationBuilder OnPut(Match<string> path) => On(HttpMethod.Put, path);

    /// <summary>
    /// Matches an HTTP PATCH whose request URI's path/query equals <paramref name="path"/> exactly -
    /// see <see cref="OnGet(string)"/>'s remarks (same rationale, every <c>OnX</c> method).
    /// </summary>
    public HttpResponseRegistrationBuilder OnPatch(string path) => On(HttpMethod.Patch, path);

    /// <summary>Matches an HTTP PATCH whose request URI's path/query satisfies <paramref name="path"/> (<see cref="Match.Any{T}"/>/<see cref="Match.Is{T}"/>).</summary>
    public HttpResponseRegistrationBuilder OnPatch(Match<string> path) => On(HttpMethod.Patch, path);

    /// <summary>
    /// Matches an HTTP DELETE whose request URI's path/query equals <paramref name="path"/> exactly -
    /// see <see cref="OnGet(string)"/>'s remarks (same rationale, every <c>OnX</c> method).
    /// </summary>
    public HttpResponseRegistrationBuilder OnDelete(string path) => On(HttpMethod.Delete, path);

    /// <summary>Matches an HTTP DELETE whose request URI's path/query satisfies <paramref name="path"/> (<see cref="Match.Any{T}"/>/<see cref="Match.Is{T}"/>).</summary>
    public HttpResponseRegistrationBuilder OnDelete(Match<string> path) => On(HttpMethod.Delete, path);

    /// <summary>
    /// Matches any request satisfying <paramref name="predicate"/> - the whole-request escape
    /// hatch for conditions spanning method/URI/headers/content type at once. A plain
    /// <see cref="Func{T, TResult}"/>, not <see cref="Match{T}"/> - see ADR-0051 "Should this reuse
    /// core Match&lt;T&gt;, or stay HTTP-native?" for why.
    /// </summary>
    public HttpResponseRegistrationBuilder When(Func<HttpRequestMessage, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new HttpResponseRegistrationBuilder(this, predicate, "When(...) request");
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> over this handler with <c>disposeHandler: false</c> -
    /// the caller owns and disposes the returned client; this handler's own lifetime is
    /// independent (ADR-0051 "Lifecycle, disposal, concurrency"). May be called more than once to
    /// produce several independent clients sharing this handler and its request log.
    /// </summary>
    public HttpClient CreateClient(Uri? baseAddress = null)
    {
        var client = new HttpClient(this, disposeHandler: false);
        if (baseAddress is not null)
        {
            client.BaseAddress = baseAddress;
        }

        return client;
    }

    internal void AddRegistration(HttpResponseRegistration registration)
    {
        // Configuration is not guaranteed concurrent with SendAsync (ADR-0051 "Concurrency:
        // narrowed contract") - a plain List<T> append here is safe under that contract; concurrent
        // *reads* of an unmutated list during dispatch don't race with this.
        _registrations.Add(registration);
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_disposed)
        {
            return Task.FromException<HttpResponseMessage>(new ObjectDisposedException(nameof(TestHttpHandler)));
        }

        RecordRequest(request);

        HttpResponseRegistration? matched = null;
        for (var i = _registrations.Count - 1; i >= 0; i--)
        {
            if (_registrations[i].Matches(request))
            {
                matched = _registrations[i];
                break;
            }
        }

        if (matched is null)
        {
            return Task.FromException<HttpResponseMessage>(new UnmatchedHttpRequestException(request.Method, request.RequestUri));
        }

        matched.RecordMatch();
        try
        {
            return Task.FromResult(matched.CreateResponse(request));
        }
        catch (Exception ex)
        {
            return Task.FromException<HttpResponseMessage>(ex);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
        }

        base.Dispose(disposing);
    }

    private void RecordRequest(HttpRequestMessage request)
    {
        lock (_requestsLock)
        {
            _requests.Add(request);
        }
    }

    // Exact-path overload - path is a plain string, so it can be embedded verbatim in the
    // registration's description (ADR-0051 Amendment 1). Behaviorally identical to the
    // Match<string> overload's equality case (both end up comparing via string equality), kept as
    // a separate code path only so the literal is retained for diagnostics, not duplicated logic.
    private HttpResponseRegistrationBuilder On(HttpMethod method, string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var description = $"{method.Method} {path}";
        return new HttpResponseRegistrationBuilder(
            this,
            request => request.Method == method && string.Equals(request.RequestUri?.PathAndQuery ?? string.Empty, path, StringComparison.Ordinal),
            description);
    }

    // Match<string> overload - Match<T> deliberately exposes no accessor beyond Matches() (see its
    // own XML doc), so Compono.Http genuinely cannot tell an Any() match from an Is(predicate) match
    // from the value alone, and touching core Compono to expose that is out of scope here (ADR-0051
    // Amendment 1). The description below is deliberately generic rather than guessing which one it
    // is - an honest "can't say more than this" beats a fabricated "matching any path" that might be
    // wrong for an Is(...) registration.
    private HttpResponseRegistrationBuilder On(HttpMethod method, Match<string> path)
    {
        var description = $"{method.Method} request matching a custom path condition";
        return new HttpResponseRegistrationBuilder(
            this,
            request => request.Method == method && path.Matches(request.RequestUri?.PathAndQuery ?? string.Empty),
            description);
    }
}
