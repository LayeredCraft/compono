using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Compono.Http;

/// <summary>
/// Returned by <see cref="TestHttpHandler"/>'s <c>OnGet</c>/<c>OnPost</c>/.../<c>When</c>/<c>WhenAsync</c>
/// methods - accumulates further matching conditions (<see cref="WithHeader"/>/<see cref="WithBody"/>/
/// <see cref="WithFormBody"/>/<see cref="WithJsonBody{T}(Func{T,bool},JsonTypeInfo{T})"/>) as an ANDed,
/// declaration-order, short-circuiting chain (ADR-0062 D2/D11), then finishes configuring a matched
/// request's response. Every <c>Respond*</c>/<c>Throws</c> method finalizes and returns the underlying
/// <see cref="HttpResponseRegistration"/> handle (never <see langword="void"/>), so the caller can
/// capture it for verification (<c>registration.Verify().Once()</c>). Once finalized, this builder
/// rejects any further <c>WithX</c>/<c>Respond*</c>/<c>Throws</c> call - the finalized
/// <see cref="HttpResponseRegistration"/> is built from an immutable snapshot of the matcher chain
/// (ADR-0062 D2), so it can never observe a later mutation attempt even hypothetically.
/// </summary>
public sealed class HttpResponseRegistrationBuilder
{
    private static readonly string[] SensitiveHeaderNames =
        ["Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie"];

    private readonly TestHttpHandler _handler;
    private readonly List<(AsyncRequestMatcher Matcher, string Description)> _matchers = [];
    private bool _finished;

    internal HttpResponseRegistrationBuilder(TestHttpHandler handler, AsyncRequestMatcher matcher, string description)
    {
        _handler = handler;
        _matchers.Add((matcher, description));
    }

    /// <summary>
    /// Adds a condition requiring header <paramref name="name"/> to carry <paramref name="value"/> -
    /// checked against both <see cref="HttpRequestMessage.Headers"/> and
    /// <see cref="HttpRequestMessage.Content"/>'s own <see cref="HttpContent.Headers"/>, with no
    /// precedence between the two collections (ADR-0062 D5): matches if <paramref name="value"/>
    /// appears in either. Header-name comparison is case-insensitive (matching
    /// <see cref="System.Net.Http.Headers.HttpHeaders"/>'s own contract); value comparison is
    /// ordinal. A header with multiple values matches if any one of them equals
    /// <paramref name="value"/>. A missing header evaluates to no match, never an exception. The
    /// value for <c>Authorization</c>/<c>Proxy-Authorization</c>/<c>Cookie</c>/<c>Set-Cookie</c>
    /// (case-insensitive) is redacted in this registration's diagnostic description only - matching
    /// always compares the real value (ADR-0062 D6).
    /// </summary>
    public HttpResponseRegistrationBuilder WithHeader(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        var description = IsSensitiveHeaderName(name)
            ? $"header \"{name}\" = \"<redacted>\""
            : $"header \"{name}\" = \"{value}\"";

        return AddMatcher((request, _) => new ValueTask<bool>(HeaderMatches(request, name, value)), description);
    }

    /// <summary>
    /// Adds a condition evaluating <paramref name="predicate"/> against the request body's raw
    /// bytes - the generic, media-type-agnostic foundation
    /// <see cref="WithFormBody"/>/<see cref="WithJsonBody{T}(Func{T,bool},JsonTypeInfo{T})"/> are
    /// themselves built on (ADR-0062 D7/D7a). A request with no content evaluates to no match;
    /// <paramref name="predicate"/> is never invoked in that case.
    /// </summary>
    public HttpResponseRegistrationBuilder WithBody(Func<byte[], bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return AddMatcher(
            async (request, cancellationToken) =>
            {
                if (request.Content is null)
                {
                    return false;
                }

                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                return predicate(bytes);
            },
            "body matching a custom condition");
    }

    /// <summary>
    /// Adds a condition requiring <c>Content-Type: application/x-www-form-urlencoded</c>
    /// (case-insensitive) and evaluating <paramref name="predicate"/> against the parsed form
    /// fields (ADR-0062 D7/D7a). A missing or different <c>Content-Type</c> evaluates to no match
    /// without reading the body at all. Percent-escapes are decoded, and a raw <c>+</c> is decoded
    /// to a space, per <c>application/x-www-form-urlencoded</c> semantics (ADR-0062 D7's corrected
    /// mechanism). <paramref name="predicate"/> receives an <see cref="ILookup{TKey,TElement}"/>,
    /// not a dictionary, because a real form body can legitimately repeat a key (e.g. a checkbox
    /// group) - the lookup's indexer returns every value for a key, in wire order, or an empty
    /// sequence (never a throw) for an absent key.
    /// </summary>
    public HttpResponseRegistrationBuilder WithFormBody(Func<ILookup<string, string>, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return AddMatcher(
            async (request, cancellationToken) =>
            {
                if (!HasMediaType(request, "application/x-www-form-urlencoded"))
                {
                    return false;
                }

                var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return predicate(ParseFormBody(body));
            },
            "form body matching a custom condition");
    }

    /// <summary>
    /// Adds a condition requiring a JSON <c>Content-Type</c> (<c>application/json</c>, or any
    /// media type whose subtype ends in <c>+json</c>, case-insensitive) and evaluating
    /// <paramref name="predicate"/> against the body deserialized via <paramref name="jsonTypeInfo"/>
    /// (e.g. a source-generated <c>JsonSerializerContext</c>'s metadata) - the guaranteed-AOT-safe
    /// overload, since it bypasses runtime resolver lookup entirely (ADR-0062 D7a/D8). A missing or
    /// non-JSON <c>Content-Type</c> evaluates to no match without reading or deserializing the
    /// body. A body that cannot be deserialized as <typeparamref name="T"/> propagates the
    /// underlying <see cref="JsonException"/> directly - it is not treated as "no match"
    /// (ADR-0062 D10).
    /// </summary>
    public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonTypeInfo<T> jsonTypeInfo)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        return AddMatcher(
            async (request, cancellationToken) =>
            {
                if (!HasJsonMediaType(request))
                {
                    return false;
                }

                var json = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var value = JsonSerializer.Deserialize(json, jsonTypeInfo);
                return predicate(value);
            },
            "JSON body matching a custom condition");
    }

    /// <summary>
    /// Adds a condition requiring a JSON <c>Content-Type</c> and evaluating
    /// <paramref name="predicate"/> against the body deserialized via
    /// <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>'s ordinary
    /// runtime-metadata path (ADR-0062 D7a/D8).
    /// </summary>
    /// <remarks>
    /// Carries <see cref="RequiresDynamicCodeAttribute"/>/<see cref="RequiresUnreferencedCodeAttribute"/>
    /// because the underlying deserialize overload does - <c>Compono.Http</c> itself introduces no
    /// reflection, but this overload's runtime-metadata resolution genuinely isn't Native-AOT-safe
    /// unless <paramref name="options"/> supplies a source-generated resolver. Prefer
    /// <see cref="WithJsonBody{T}(Func{T,bool},JsonTypeInfo{T})"/> in an AOT/trim-sensitive
    /// project.
    /// </remarks>
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use the WithJsonBody<T>(Func<T?, bool>, JsonTypeInfo<T>) overload for native AOT applications.")]
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the WithJsonBody<T>(Func<T?, bool>, JsonTypeInfo<T>) overload, or make sure all of the required types are preserved.")]
    public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return AddMatcher(
            async (request, cancellationToken) =>
            {
                if (!HasJsonMediaType(request))
                {
                    return false;
                }

                var json = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var value = JsonSerializer.Deserialize<T>(json, options);
                return predicate(value);
            },
            "JSON body matching a custom condition");
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
    /// <paramref name="content"/> is defensively copied once at registration time - not retained
    /// by reference - so a caller mutating or reusing its own array afterward can never change an
    /// already-registered response; every matched invocation constructs a fresh
    /// <see cref="ByteArrayContent"/> over that private copy (ADR-0051 Amendment 2).
    /// </summary>
    public HttpResponseRegistration RespondBytes(byte[] content, string mediaType = "application/octet-stream")
    {
        ArgumentNullException.ThrowIfNull(content);
        var snapshot = (byte[])content.Clone();
        return Finish(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(snapshot) { Headers = { ContentType = new MediaTypeHeaderValue(mediaType) } },
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

    private HttpResponseRegistrationBuilder AddMatcher(AsyncRequestMatcher matcher, string description)
    {
        EnsureNotFinished();
        _matchers.Add((matcher, description));
        return this;
    }

    private void EnsureNotFinished()
    {
        // Each OnX(...)/When(...)/WhenAsync(...) call produces one builder, meant to be extended by
        // zero or more WithX(...) calls and finalized by exactly one Respond*/Throws call. Without
        // this guard, a caller retaining the builder past Respond*/Throws and calling WithX/a second
        // Respond*/Throws on it could silently look like it changed an already-returned
        // HttpResponseRegistration - it can't (Finish() below snapshots the matcher chain before
        // building that registration), but allowing the call to silently no-op would still be a
        // confusing, easy-to-misread API. One flag, one exception, shared by every WithX method and
        // by Finish() itself - not two separate lifecycle rules.
        if (_finished)
        {
            throw new InvalidOperationException(
                "This HttpResponseRegistrationBuilder was already finalized by a prior Respond*/Throws call. Each OnGet/OnPost/etc./When(...) call returns a new builder - call it again to add another registration instead of reusing this one.");
        }
    }

    private HttpResponseRegistration Finish(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        EnsureNotFinished();
        _finished = true;

        // Snapshot into an array before compiling - the compiled matcher below closes over this
        // array, never over the builder's own _matchers list, so no later mutation of this builder
        // (rejected by EnsureNotFinished() above regardless) could ever retroactively change an
        // already-finalized HttpResponseRegistration's behavior (ADR-0062 D2).
        var snapshot = _matchers.ToArray();
        var description = DescribeChain(snapshot);

        AsyncRequestMatcher combinedMatcher = async (request, cancellationToken) =>
        {
            // Declaration-order, short-circuiting AND (ADR-0062 D11) - a failing earlier condition
            // (e.g. a header check) means a later, more expensive one (e.g. a body read) never runs.
            foreach (var (matcher, _) in snapshot)
            {
                if (!await matcher(request, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        };

        var registration = new HttpResponseRegistration(combinedMatcher, description);
        registration.SetResponseFactory(responseFactory);
        _handler.AddRegistration(registration);
        return registration;
    }

    private static string DescribeChain((AsyncRequestMatcher Matcher, string Description)[] matchers)
    {
        if (matchers.Length == 1)
        {
            return matchers[0].Description;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < matchers.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(" and ");
            }

            builder.Append(matchers[i].Description);
        }

        return builder.ToString();
    }

    private static bool IsSensitiveHeaderName(string name)
    {
        foreach (var sensitive in SensitiveHeaderNames)
        {
            if (string.Equals(name, sensitive, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HeaderMatches(HttpRequestMessage request, string name, string value)
    {
        if (request.Headers.TryGetValues(name, out var requestValues) && ContainsValue(requestValues, value))
        {
            return true;
        }

        return request.Content?.Headers.TryGetValues(name, out var contentValues) == true &&
               ContainsValue(contentValues!, value);
    }

    private static bool ContainsValue(IEnumerable<string> values, string expected)
    {
        foreach (var value in values)
        {
            if (string.Equals(value, expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMediaType(HttpRequestMessage request, string mediaType) =>
        request.Content?.Headers.ContentType?.MediaType is { } actual &&
        string.Equals(actual, mediaType, StringComparison.OrdinalIgnoreCase);

    private static bool HasJsonMediaType(HttpRequestMessage request)
    {
        var mediaType = request.Content?.Headers.ContentType?.MediaType;
        if (mediaType is null)
        {
            return false;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    // application/x-www-form-urlencoded decoding (ADR-0062 D7, corrected): split on '&', split each
    // piece on the FIRST '=' (no '=' -> empty value), then decode each raw key/value independently by
    // replacing a raw '+' with a space BEFORE percent-decoding via Uri.UnescapeDataString - doing this
    // in the opposite order would incorrectly turn an intentionally-percent-encoded "%2B" into a space
    // instead of a literal '+'. ILookup<string,string>, not a dictionary, because a real form body can
    // legitimately repeat a key.
    private static ILookup<string, string> ParseFormBody(string body)
    {
        var pairs = new List<KeyValuePair<string, string>>();

        if (!string.IsNullOrEmpty(body))
        {
            foreach (var piece in body.Split('&'))
            {
                if (piece.Length == 0)
                {
                    continue;
                }

                var separatorIndex = piece.IndexOf('=');
                string rawKey;
                string rawValue;
                if (separatorIndex < 0)
                {
                    rawKey = piece;
                    rawValue = string.Empty;
                }
                else
                {
                    rawKey = piece[..separatorIndex];
                    rawValue = piece[(separatorIndex + 1)..];
                }

                pairs.Add(new KeyValuePair<string, string>(DecodeFormComponent(rawKey), DecodeFormComponent(rawValue)));
            }
        }

        return pairs.ToLookup(p => p.Key, p => p.Value, StringComparer.Ordinal);
    }

    private static string DecodeFormComponent(string raw) => Uri.UnescapeDataString(raw.Replace('+', ' '));

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
}
