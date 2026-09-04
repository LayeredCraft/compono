using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Compono.Http;

/// <summary>
/// Returned by <see cref="TestHttpHandler"/>'s <c>OnGet</c>/<c>OnPost</c>/.../<c>When</c> methods -
/// finishes configuring a matched request's response. Every method here finalizes and returns the
/// underlying <see cref="HttpResponseRegistration"/> handle (never <see langword="void"/>), so the
/// caller can capture it for verification (<c>registration.Verify().Once()</c>).
/// </summary>
public sealed class HttpResponseRegistrationBuilder
{
    private readonly TestHttpHandler _handler;
    private readonly HttpResponseRegistration _registration;
    private bool _finished;

    internal HttpResponseRegistrationBuilder(TestHttpHandler handler, Func<HttpRequestMessage, bool> matcher, string description)
    {
        _handler = handler;
        _registration = new HttpResponseRegistration(matcher, description);
    }

    /// <summary>Responds with <paramref name="statusCode"/> and no content.</summary>
    public HttpResponseRegistration Respond(HttpStatusCode statusCode) =>
        Finish(_ => new HttpResponseMessage(statusCode));

    /// <summary>
    /// Responds with HTTP 200 OK and a fresh <see cref="StringContent"/> per invocation - the
    /// string itself is already immutable, so no serialize-once optimization is needed here
    /// (contrast <see cref="RespondJson{T}(T, JsonSerializerOptions?)"/>, whose body IS
    /// serialized once - see ADR-0051 "Serialize-once-to-bytes model").
    /// </summary>
    public HttpResponseRegistration RespondText(string content, string mediaType = "text/plain", Encoding? encoding = null) =>
        Finish(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, encoding ?? Encoding.UTF8, mediaType),
        });

    /// <summary>
    /// Responds with HTTP 200 OK and <paramref name="value"/> serialized to JSON, using the
    /// ordinary runtime-metadata <see cref="JsonSerializer"/> path. <paramref name="value"/> is
    /// serialized once, here, to an immutable byte buffer; every matched invocation constructs a
    /// fresh <see cref="ByteArrayContent"/> over that same buffer with its own explicit
    /// <c>Content-Type</c> header (ADR-0051 "Serialize-once-to-bytes model").
    /// </summary>
    /// <remarks>
    /// Carries <see cref="RequiresDynamicCodeAttribute"/>/<see cref="RequiresUnreferencedCodeAttribute"/>
    /// because the underlying <see cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)"/>
    /// overload does - <c>Compono.Http</c> itself introduces no reflection, but this overload's
    /// runtime-metadata resolution genuinely isn't Native-AOT-safe unless <paramref name="options"/>
    /// supplies a source-generated resolver. Prefer
    /// <see cref="RespondJson{T}(T, JsonTypeInfo{T})"/> in an AOT/trim-sensitive project - see
    /// ADR-0051 "JSON / AOT" for the verified attribute-propagation rationale.
    /// </remarks>
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use the RespondJson<T>(T, JsonTypeInfo<T>) overload for native AOT applications.")]
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the RespondJson<T>(T, JsonTypeInfo<T>) overload, or make sure all of the required types are preserved.")]
    public HttpResponseRegistration RespondJson<T>(T value, JsonSerializerOptions? options = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
        return RespondJsonBytes(bytes);
    }

    /// <summary>
    /// Responds with HTTP 200 OK and <paramref name="value"/> serialized to JSON via
    /// <paramref name="jsonTypeInfo"/> (e.g. a source-generated <c>JsonSerializerContext</c>'s
    /// metadata) - the guaranteed-AOT-safe path, since it bypasses runtime resolver lookup
    /// entirely. Same serialize-once-to-bytes model as
    /// <see cref="RespondJson{T}(T, JsonSerializerOptions?)"/>.
    /// </summary>
    public HttpResponseRegistration RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, jsonTypeInfo);
        return RespondJsonBytes(bytes);
    }

    /// <summary>
    /// Responds with HTTP 200 OK and <paramref name="content"/> verbatim, as
    /// <paramref name="mediaType"/> - the raw-bytes counterpart to <see cref="RespondText"/> for
    /// binary payloads (e.g. a fetched certificate's DER bytes) that would otherwise need a lossy
    /// or awkward text encoding to round-trip through <see cref="RespondText"/>. Same
    /// serialize-once-to-bytes model as <see cref="RespondJson{T}(T, JsonSerializerOptions?)"/>:
    /// <paramref name="content"/> is captured once; every matched invocation constructs a fresh
    /// <see cref="ByteArrayContent"/> over it (ADR-0051 Amendment 2).
    /// </summary>
    public HttpResponseRegistration RespondBytes(byte[] content, string mediaType = "application/octet-stream")
    {
        ArgumentNullException.ThrowIfNull(content);
        return Finish(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content) { Headers = { ContentType = new MediaTypeHeaderValue(mediaType) } },
        });
    }

    /// <summary>
    /// Configures this registration to throw <paramref name="exception"/> instead of returning a
    /// response - the exact same instance is rethrown on every matched invocation (verified: an
    /// <see cref="Exception"/> carries no disposal semantics comparable to
    /// <see cref="HttpContent"/>'s, so there is no freshness requirement analogous to
    /// <c>Respond*</c>'s - see ADR-0051 "Response state: factory, not instance"). No exception
    /// factory/callback overload - reusing the same instance is the entire v1 behavior.
    /// </summary>
    public HttpResponseRegistration Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Finish(_ => throw exception);
    }

    private HttpResponseRegistration RespondJsonBytes(byte[] bytes) =>
        Finish(_ =>
        {
            var content = new ByteArrayContent(bytes);
            // A fresh MediaTypeHeaderValue per response, not a shared static instance -
            // MediaTypeHeaderValue is mutable, and a caller mutating one response's
            // Content.Headers.ContentType (e.g. its CharSet) must never affect any other response,
            // matched or not, past or future (ADR-0051 "Response state: factory, not instance").
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

    private HttpResponseRegistration Finish(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        // Each OnX(...)/When(...) call produces one builder, meant to be finalized by exactly one
        // Respond*/Throws call. Without this guard, a caller retaining the builder and calling a
        // second Respond*/Throws on it would silently overwrite the first registration's response
        // factory and re-add the same HttpResponseRegistration to the handler's list a second time
        // - the two returned handles would be the same object, and the "first" response would
        // silently start returning the "second" one.
        if (_finished)
        {
            throw new InvalidOperationException(
                "This HttpResponseRegistrationBuilder was already finalized by a prior Respond*/Throws call. Each OnGet/OnPost/etc./When(...) call returns a new builder - call it again to add another registration instead of reusing this one.");
        }

        _finished = true;
        _registration.SetResponseFactory(responseFactory);
        _handler.AddRegistration(_registration);
        return _registration;
    }
}
