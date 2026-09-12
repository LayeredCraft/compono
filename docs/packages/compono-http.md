# Compono.Http

A reflection-free, handler-based test double for code built on `HttpClient` —
`TestHttpHandler` (an `HttpMessageHandler` subclass) with `OnGet`/`OnPost`/
`OnPut`/`OnPatch`/`OnDelete` + `When`/`WhenAsync` matching,
`WithHeader`/`WithBody`/`WithFormBody`/`WithJsonBody<T>` chainable body/
header conditions, last-match-wins precedence, strict unmatched-request
behavior, and a registration-handle verification model reusing core
`Compono`'s `CallVerifier` unmodified. See
[ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md) for
the original decision record,
[ADR-0062](../adr/0062-compono-http-body-header-request-matching.md) for
the body/header matching addition, and
[RESEARCH-0009](../research/0009-compono-http-admission-research.md) for
the admission investigation this package's shape came from.

## When to install

Your code under test consumes a real `HttpClient` (or a `DelegatingHandler`
sitting in front of one), and the test wants to exercise that real client
pipeline against a configured HTTP response — not substitute an
application-level interface:

```bash
dotnet add package Compono
dotnet add package Compono.Http
```

If the seam under test is already an ordinary application interface
(`ICustomerApi`, `IWeatherService`, ...) and the test doesn't care about
HTTP behavior specifically, that stays a
[`Compono.TestDoubles`](compono-testdoubles.md)/
[`Compono.NSubstitute`](compono-nsubstitute.md) case — don't reach for
`Compono.Http` there. This package depends only on `Compono`; it does not
add or require `Microsoft.Extensions.Http`.

## What it gives you

```csharp
using Compono.Http;

var handler = new TestHttpHandler();
var registration = handler.OnGet("/v1/customers/42")
    .RespondJson(new CustomerDto("42", "Ada Lovelace"));

using var client = handler.CreateClient(new Uri("https://api.example.com/"));
var customer = await client.GetFromJsonAsync<CustomerDto>("/v1/customers/42");

registration.Verify().Once();
```

- **`OnGet`/`OnPost`/`OnPut`/`OnPatch`/`OnDelete`** — fixes the HTTP method,
  two overloads:
  - **`OnX(string path)`** — the normal, common-case entry point: an exact
    equality match against the request URI's path+query. Preserves `path`
    verbatim in `registration.Verify()`'s diagnostics (e.g. a failure reads
    `GET /v1/customers/42`, not a generic placeholder) — `handler.OnGet("/v1/customers/42")`
    resolves here automatically, no code change needed from earlier
    `Match<string>`-only usage.
  - **`OnX(Match<string> path)`** — `Match.Any<string>()`/
    `Match.Is<string>(predicate)`, reusing core `Compono`'s `Match<T>`
    unchanged. `Match<T>` deliberately exposes no way to tell an `Any()`
    match from an `Is(predicate)` match apart from the outside, so a
    `Verify()` failure on one of these describes itself honestly and
    generically (`"GET request matching a custom path condition"`) rather
    than guessing which kind it is. See
    [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)'s
    Amendment 1 for why.
- **`When(Func<HttpRequestMessage, bool> predicate)`** — the synchronous
  whole-request escape hatch, for conditions spanning method, URI, and
  headers together. For anything that needs to read the request body, or
  await something to decide, use `WhenAsync` or one of the named body/
  header matchers below instead — `When`'s own predicate is deliberately
  synchronous and unchanged.
- **`WhenAsync(Func<HttpRequestMessage, CancellationToken, ValueTask<bool>> predicate)`** —
  the async peer of `When`. Also the primitive `WithHeader`/`WithBody`/
  `WithFormBody`/`WithJsonBody<T>` (below) compile to internally, exposed
  publicly so a condition none of them covers still has a first-class
  escape hatch (ADR-0062).
- **`.WithHeader(string name, string value)`, `.WithBody(Func<byte[], bool> predicate)`,
  `.WithFormBody(Func<ILookup<string,string>, bool> predicate)`,
  `.WithJsonBody<T>(Func<T?, bool> predicate, JsonTypeInfo<T> jsonTypeInfo)`/
  `.WithJsonBody<T>(Func<T?, bool> predicate, JsonSerializerOptions? options = null)`** —
  chainable request-matching conditions on `OnX(...)`/`When`/`WhenAsync`,
  ANDed together in declaration order with short-circuiting (a failing
  earlier condition means a later, more expensive one never runs):

  ```csharp
  handler.OnPost("/auth/o2/token")
      .WithFormBody(form =>
          form["grant_type"].SingleOrDefault() == "refresh_token" &&
          form["client_id"].SingleOrDefault() == expectedClientId)
      .RespondJson(tokenResponse);
  ```

  - `WithHeader` checks both `HttpRequestMessage.Headers` and
    `HttpRequestMessage.Content?.Headers`, with no precedence between
    them — matches if the value appears in either. Header-name comparison
    is case-insensitive; value comparison is ordinal. A header with
    multiple values matches if any one equals the expected value. A
    missing header evaluates to no match, never an exception. The value
    for `Authorization`/`Proxy-Authorization`/`Cookie`/`Set-Cookie`
    (case-insensitive) is **redacted in `Verify()`'s diagnostic
    description only** — matching always compares the real value.
  - `WithFormBody` **requires** `Content-Type: application/x-www-form-urlencoded`
    (case-insensitive) — a missing or different media type evaluates to
    no match *without reading the body at all*. The parsed fields are an
    `ILookup<string,string>`, not a dictionary, because a real form body
    can legitimately repeat a key (a checkbox group) — the lookup's
    indexer returns every value for a key, in wire order, or an empty
    sequence (never a throw) for an absent key.
  - `WithJsonBody<T>` **requires** a JSON media type — exactly
    `application/json`, or any type whose subtype ends in `+json`
    (case-insensitive, e.g. `application/vnd.api+json`) — checked before
    deserializing. A body that fails to deserialize as `T` (malformed
    JSON) propagates the underlying `JsonException` directly — it is
    **not** treated as "no match." See JSON / AOT below for the two
    overloads' AOT posture.
  - `WithBody` is the generic, **media-type-agnostic** foundation the
    other two are themselves built on — no `Content-Type` requirement, by
    design, since it's the escape hatch for a condition that isn't one of
    the two named representations above.
- **Precedence: last-registered-first, first match wins.** A later, more
  specific registration overrides an earlier, broader one — register a
  catch-all first, then override it:
  ```csharp
  handler.When(_ => true).Respond(HttpStatusCode.NotFound);      // fallback
  handler.OnGet("/v1/customers/42").RespondJson(customer);       // override
  ```
- **Strict unmatched-request behavior.** A request matching no registration
  throws `UnmatchedHttpRequestException` (naming the method and URI) rather
  than returning a fabricated response. Want a fallback? Configure one
  explicitly with `handler.When(_ => true)`. The unmatched request still
  appears in `handler.Requests`.
- **Response APIs**, every one returning the finalized `HttpResponseRegistration`
  handle (never `void`):
  - `Respond(HttpStatusCode)` — no content.
  - `RespondText(string content, string mediaType = "text/plain", Encoding? encoding = null)`.
  - `RespondJson<T>(T value, JsonSerializerOptions? options = null)` — the
    ergonomic runtime-metadata path (see JSON/AOT below).
  - `RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)` — the
    source-generated, AOT-safe path.
  - `RespondBytes(byte[] content, string mediaType = "application/octet-stream")` —
    raw binary payloads (e.g. fetched certificate bytes) that `RespondText`
    can't carry without a lossy or awkward text encoding. `content` is
    defensively copied at registration time, so mutating the caller's array
    afterward never changes the registered response.
  - `Throws(Exception exception)` — rethrows the **same instance** on every
    matched invocation; there's no exception-factory overload.
  - Every `Respond*` call builds a **fresh** response/content per matched
    invocation — reading or disposing one match's content never affects the
    next.
- **`registration.Verify()`** — returns a `CallVerifier` (the exact type
  core `Compono` already uses elsewhere): `.Never()`, `.Once()`,
  `.Exactly(n)`, `.AtLeast(n)`, `.AtMost(n)`
  ([ADR-0044 Amendment 22](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-22-2026-09-06-callverifieratleastintatmostint-added-requirement-3s-minimality-preserved-not-reversed)).
  Answers "how many times did *this configured behavior*
  match" — kept deliberately separate from `handler.Requests`, which
  answers "what did the system under test actually send."
- **`handler.Requests: IReadOnlyList<HttpRequestMessage>`** — every request
  that reached the handler, matched or not, in arrival order, recorded
  before matching is attempted. A fresh point-in-time snapshot on every
  access, never a live view over the mutable backing log.
- **`handler.CreateClient(Uri? baseAddress = null)`** — always constructs
  `new HttpClient(handler, disposeHandler: false)`. May be called more than
  once to produce several independent clients sharing this handler and its
  request log.

## Lifetime — caller-owned, always

`TestHttpHandler` is a plain caller-owned object. Compono composition
(`[Shared]`/`CompositionRow`) never owns or disposes it — there is no
composition-scope auto-disposal for `[Shared]`-composed `IDisposable`
values today, and this package doesn't add one. `CreateClient` always uses
`disposeHandler: false`, so disposing a client never disposes the handler,
and disposing the handler is the caller's own responsibility, same as any
other `IDisposable` you construct by hand:

```csharp
[Theory, Compose]
public async Task GetAsync_ReturnsCustomer([Shared] TestHttpHandler handler, ...)
{
    handler.OnGet("/v1/customers/42").RespondJson(customer);
    using var client = handler.CreateClient(baseAddress);
    // ...
}
```

`[Shared]` here gives you identity/reuse across composed parameters in the
same test — the same `TestHttpHandler` instance handed to every parameter
requesting it — not disposal.

## `IHttpClientFactory`

`Compono.Http` is **not** an `IHttpClientFactory` mocking package and ships
no `Microsoft.Extensions.Http` helper. If your code under test resolves its
client through `IHttpClientFactory`, satisfy that interface with a small
project-local fake (it's a single-method interface — no special machinery
needed) or via `Compono.TestDoubles`/`Compono.NSubstitute` if your project
already uses one of those for other doubles:

```csharp
private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
```

## JSON / AOT

`RespondJson<T>(T value, JsonSerializerOptions? options = null)` uses
`System.Text.Json`'s ordinary runtime-metadata resolution and carries
`[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` — the same attributes
`JsonSerializer.Serialize<T>` itself carries, propagated honestly, not
suppressed. Under a Native-AOT/trim-sensitive project, this overload
produces the framework's real IL2026/IL3050 warnings at your own call
site.

`RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)` — pass a
source-generated `JsonSerializerContext`'s metadata — carries **neither**
attribute; it bypasses runtime resolver lookup entirely and is the
AOT-safe path:

```csharp
handler.OnGet("/v1/customers/42").RespondJson(customer, AppJsonContext.Default.CustomerDto);
```

Prefer the `JsonTypeInfo<T>` overload in any project that publishes Native
AOT or enables trim analysis. Not all `RespondJson` usage is automatically
AOT-safe — only this overload is.

`WithJsonBody<T>` mirrors this exactly:
`WithJsonBody<T>(predicate, JsonTypeInfo<T>)` is the AOT-safe path (no
attribute); `WithJsonBody<T>(predicate, JsonSerializerOptions? = null)`
carries the same `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`
attributes for the identical reason.

## v1 non-goals

Deliberately not in this package — see ADR-0051's and ADR-0062's Decision
Outcomes for the rationale behind each:

- `IHttpClientFactory`/named-client/typed-client integration.
- Streaming/multipart request or response bodies.
- Retry/Polly-aware testing behavior.
- Callback-based, delayed, or sequential/queued responses per registration.
- WireMock-style stateful scenarios, numeric priority, or regex path
  matching.
- Call-order verification (only count-based `Never`/`Once`/`Exactly`/
  `AtLeast`/`AtMost`).
- A strict/loose unmatched-request mode toggle.
- A raw `HttpResponseMessage`-accepting `Respond(HttpResponseMessage)`
  overload.
- Composition-owned disposal of `TestHttpHandler`.
- A configurable/extensible sensitive-header redaction list — the
  `Authorization`/`Proxy-Authorization`/`Cookie`/`Set-Cookie` denylist is
  fixed.

## Next

- [ASP.NET API sample](../samples/aspnet-api.md) (`ShippingClientTests`) —
  a real outbound-HTTP-calling service tested against a composed
  `TestHttpHandler`, in a realistic multi-layer application.
- [`Compono.TestDoubles`](compono-testdoubles.md)/[`Compono.NSubstitute`](compono-nsubstitute.md)
  — for `IHttpClientFactory` or any other ordinary interface dependency.
- [`Compono.XunitV3`](compono-xunitv3.md) — `[Compose]`/`[Shared]` used
  throughout the examples above.
- [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md) —
  the full decision record.
