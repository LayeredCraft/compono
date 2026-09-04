# Compono.Http

A reflection-free, handler-based test double for code built on `HttpClient` —
`TestHttpHandler` (an `HttpMessageHandler` subclass) with `OnGet`/`OnPost`/
`OnPut`/`OnPatch`/`OnDelete` + `When(...)` matching, last-match-wins
precedence, strict unmatched-request behavior, and a registration-handle
verification model reusing core `Compono`'s `CallVerifier` unmodified. See
[ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md) for
the full decision record and
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
- **`When(Func<HttpRequestMessage, bool> predicate)`** — the whole-request
  escape hatch, for conditions spanning method, URI, headers, and content
  type together (e.g. `req.Content is FormUrlEncodedContent`). There is
  **no** dedicated header/query/body matcher DSL in v1 — `When(...)` is the
  only mechanism for those dimensions.
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
  `.Exactly(n)`. Answers "how many times did *this configured behavior*
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

## v1 non-goals

Deliberately not in this package — see ADR-0051's Decision Outcome for the
rationale behind each:

- `IHttpClientFactory`/named-client/typed-client integration.
- Dedicated header/query-string/JSON-body matcher types (`When(...)` is the
  only mechanism for these).
- Async request-matching predicates.
- Retry/Polly-aware testing behavior.
- Callback-based, delayed, or sequential/queued responses per registration.
- WireMock-style stateful scenarios.
- Call-order verification (only count-based `Never`/`Once`/`Exactly`).
- A strict/loose unmatched-request mode toggle.
- A raw `HttpResponseMessage`-accepting `Respond(HttpResponseMessage)`
  overload.
- Composition-owned disposal of `TestHttpHandler`.

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
