# Compono.Http

Only relevant if the project references `Compono.Http`. One public type,
`TestHttpHandler` (an `HttpMessageHandler` subclass), plus its
`HttpResponseRegistration`/`HttpResponseRegistrationBuilder` return types
and `UnmatchedHttpRequestException`.

```csharp
[Theory, Compose]
public async Task GetAsync_ReturnsCustomer([Shared] TestHttpHandler handler)
{
    var registration = handler.OnGet("/v1/customers/42")
        .RespondJson(new CustomerDto("42", "Ada Lovelace"));

    using var client = handler.CreateClient(new Uri("https://api.example.com/"));
    var customer = await client.GetFromJsonAsync<CustomerDto>("/v1/customers/42");

    registration.Verify().Once();
}
```

## When to use it

A test deliberately needs to exercise the real HTTP client pipeline —
`real HttpClient -> TestHttpHandler -> configured HTTP response` — not an
application-level abstraction. Concrete signs: the type under test is
built directly on `HttpClient`, the test cares about URI/method/header/
request construction, request serialization, or `DelegatingHandler`
behavior, or the codebase has a hand-written/reflection-based
`HttpMessageHandler` fake that `Compono.Http` should replace.

## When NOT to use it

The production seam is already an ordinary application interface
(`ICustomerApi`, `IWeatherService`, ...) and the test doesn't care that
it's HTTP-backed specifically — stay with `Compono.TestDoubles`/
`Compono.NSubstitute` there. Never special-case `HttpClient`/
`HttpMessageHandler` through `Compono.TestDoubles`/`Compono.NSubstitute`
instead of `Compono.Http` — that boundary is architectural (ADR-0051), not
a v1-only limitation that might later be lifted.

## Core usage vocabulary

- `handler.OnGet(path)` / `OnPost(path)` / `OnPut(path)` / `OnPatch(path)` /
  `OnDelete(path)` — two overloads. `OnGet(string path)` is the normal,
  common-case one: an exact equality match against the request URI's
  path+query, e.g. `handler.OnGet("/v1/customers/42")` — use this for the
  ordinary case, it's what preserves the literal path in a `Verify()`
  failure message. `OnGet(Match<string> path)` is for
  `handler.OnGet(Match.Any<string>())` (any path) or
  `handler.OnGet(Match.Is<string>(p => p.StartsWith("/v1/")))` (a
  predicate) — its `Verify()` description is deliberately generic
  ("matching a custom path condition"), since `Match<T>` exposes no way to
  tell `Any()` from `Is(...)` from the outside (ADR-0051 Amendment 1);
  never claim a `Match<string>`-based registration's failure message names
  the actual path or predicate.
- `handler.When(req => ...)` — synchronous whole-request predicate (method,
  URI, headers together). **Never suggest a blocking
  `.GetAwaiter().GetResult()`/`.Result` call inside a `When` predicate to
  read the request body** — that's exactly the anti-pattern
  `WithFormBody`/`WithJsonBody<T>`/`WhenAsync` (below) exist to replace.
  For a real `alexa-vox-craft` example of the type-only-check trap this
  causes, see "Body/header matching," below.
- `handler.WhenAsync(async (req, ct) => ...)` — the async peer of `When`,
  for any body-reading/awaited condition none of the named matchers below
  covers.
- Every match finalizes with `.Respond(HttpStatusCode)`,
  `.RespondText(content, mediaType, encoding)`,
  `.RespondJson(value, options?)`, `.RespondJson(value, jsonTypeInfo)`,
  `.RespondBytes(content, mediaType)` (raw binary payloads — e.g. fetched
  certificate bytes — that `RespondText` can't carry without a lossy text
  encoding; `content` is copied at registration time, so mutating the
  caller's array afterward never changes the registered response), or
  `.Throws(exception)` — each returns the `HttpResponseRegistration`
  handle, capture it for verification:
  ```csharp
  var registration = handler.OnPost("/v1/orders").RespondJson(order);
  ...
  registration.Verify().Once();   // .Never() / .Exactly(n) / .AtLeast(n) / .AtMost(n) also available
  ```

## Body/header matching — reach for a named matcher, not a type-only check

**When a test needs to verify what a request actually carried — a header
value, a form field, a JSON property — chain `.WithHeader(...)`/
`.WithFormBody(...)`/`.WithJsonBody<T>(...)`/`.WithBody(...)` onto the
same registration, don't drop to `req.Content is FormUrlEncodedContent`
(a type-only check) or a hand-rolled `When` predicate that can't safely
read the body.** This is a real, shipped trap this package's own history
hit: a real `alexa-vox-craft` OAuth test could only assert
`req.Content is FormUrlEncodedContent` — not the actual `grant_type`/
`client_id`/`client_secret` field values — because `When`'s predicate is
synchronous and reading a body is async. The fix (ADR-0062):

```csharp
handler.OnPost("/auth/o2/token")
    .WithFormBody(form =>
        form["grant_type"].SingleOrDefault() == "refresh_token" &&
        form["client_id"].SingleOrDefault() == expectedClientId)
    .RespondJson(tokenResponse);
```

Never recommend a type-only `is FormUrlEncodedContent`/`is StringContent`
check, or a `When` predicate that blocks on `.Result`/`.GetAwaiter().GetResult()`
to read a body, when `WithFormBody`/`WithJsonBody<T>`/`WithBody` already
cover the case — those are exactly the pattern this feature replaced.

- **`.WithHeader(name, value)`** — checks both `HttpRequestMessage.Headers`
  and `HttpRequestMessage.Content?.Headers`, no precedence between them
  (matches if the value is in either). Case-insensitive name, ordinal
  value, any-of-multiple-values, missing header → no match (never throws).
  The value for `Authorization`/`Proxy-Authorization`/`Cookie`/`Set-Cookie`
  is redacted in `Verify()`'s diagnostic text only — **matching itself
  always uses the real value**; never claim the redaction affects matching
  behavior.
- **`.WithFormBody(Func<ILookup<string,string>, bool>)`** — **requires**
  `Content-Type: application/x-www-form-urlencoded` (case-insensitive); a
  wrong/missing media type is "no match," and the body is never read in
  that case. The lookup, not a dictionary, because a form body can
  legitimately repeat a key — `form["tags"]` returns every value for that
  key, in order, or an empty sequence for an absent one.
- **`.WithJsonBody<T>(predicate, jsonTypeInfo)` /
  `.WithJsonBody<T>(predicate, options?)`** — **requires** a JSON media
  type (`application/json` or a `+json`-suffixed type) before
  deserializing; a malformed body under a JSON `Content-Type` throws the
  real `JsonException` — **never** claim this is "no match," it's a loud
  failure by design (ADR-0062 D10). AOT posture mirrors `RespondJson<T>`
  exactly: `jsonTypeInfo` is the AOT-safe overload, `options?` carries the
  same `RequiresDynamicCode`/`RequiresUnreferencedCode` attributes.
- **`.WithBody(Func<byte[], bool>)`** — the generic, **media-type-agnostic**
  escape hatch the other two are built on; no `Content-Type` requirement,
  deliberately.
- All chained conditions on one registration are ANDed in declaration
  order, short-circuiting on the first failure — recommend ordering a
  cheap header check before an expensive body-reading one when both are
  needed.

## Matching semantics

Registrations are evaluated **last-registered-first, first match wins** —
register a broad fallback first, a specific override after it:

```csharp
handler.When(_ => true).Respond(HttpStatusCode.NotFound);   // fallback, registered first
handler.OnGet("/v1/customers/42").RespondJson(customer);    // override, registered second, wins
```

Getting registration order backwards silently breaks this — the fallback
would otherwise win for every request, including the one meant to hit the
specific override.

## Unmatched behavior

Strict by default — a request matching no registration throws
`UnmatchedHttpRequestException` (naming the method and URI), never a
fabricated response. There is no loose-mode switch. Want a fallback?
Configure one explicitly: `handler.When(_ => true).Respond(...)`. The
unmatched request still appears in `handler.Requests`.

## Verification vs. request inspection

Two different questions, two different APIs — never conflate them:

- `registration.Verify().Once()` / `.Never()` / `.Exactly(n)` /
  `.AtLeast(n)` / `.AtMost(n)` — "how many times did *this configured
  behavior* match." Reuses core `Compono`'s `CallVerifier` unchanged.
- `handler.Requests` (`IReadOnlyList<HttpRequestMessage>`) — "what did the
  system under test actually send," every request in arrival order,
  matched or not, snapshotted fresh on every access.

Never suggest reconstructing a verification predicate or an expression-
based `Verify(...)` API — that shape was considered and rejected in
ADR-0051.

## Lifetime

`TestHttpHandler` is caller-owned. `[Shared]` gives identity/reuse across
composed parameters in one test — **not** disposal; Compono does not
currently dispose `[Shared]`-composed `IDisposable` values, and this
package doesn't add that. `handler.CreateClient(...)` always builds the
`HttpClient` with `disposeHandler: false`, so disposing a client never
disposes the handler — dispose both yourself:

```csharp
using var client = handler.CreateClient(baseAddress);
```

Never teach or imply automatic composition-scope disposal for
`TestHttpHandler` — if a future core ADR changes that, this file updates
then, not preemptively.

## `IHttpClientFactory` boundary

`Compono.Http` is not an `IHttpClientFactory` mocking package and ships no
`Microsoft.Extensions.Http` helper. For that seam, use a tiny
project-local fake — it's a single-method interface, no special machinery
needed — or `Compono.TestDoubles`/`Compono.NSubstitute` if the project
already uses one of those for other doubles:

```csharp
private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
```

Never suggest a `Microsoft.Extensions.Http` helper `Compono.Http` doesn't
ship.

## JSON / AOT

`RespondJson(value, options)` is the ergonomic runtime-metadata path — it
carries the normal `RequiresDynamicCode`/`RequiresUnreferencedCode`
trimming/AOT warnings at the consumer's own call site, propagated
honestly rather than suppressed. `RespondJson(value, jsonTypeInfo)` (a
source-generated `JsonSerializerContext`'s metadata) is the AOT-safe path
— prefer it in an AOT/trim-sensitive project. Never claim all
`RespondJson` usage is automatically AOT-safe — only the `JsonTypeInfo<T>`
overload is.
