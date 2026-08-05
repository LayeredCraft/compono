---
title: Freeze a Shared HttpMessageHandler
description: Compose one HttpMessageHandler substitute and reuse it everywhere a composed row needs it.
packages: [Compono, Compono.XunitV3, Compono.NSubstitute]
concepts: [shared-values]
---

# Freeze a Shared HttpMessageHandler

## Problem

A system under test wraps `HttpMessageHandler` inside its own `HttpClient`
internally — you want one composed, canned-response handler, and to be
certain the system under test received that exact instance, not a
look-alike, without reaching for a mocking library at all.

## Solution

```csharp
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public StubHttpMessageHandler(HttpStatusCode statusCode)
    {
        Response = new HttpResponseMessage(statusCode);
    }

    public HttpResponseMessage Response { get; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(Response);
}

public sealed class OrderApiClient
{
    private readonly HttpClient _client;

    public OrderApiClient(StubHttpMessageHandler handler)
    {
        _client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
    }

    public Task<HttpResponseMessage> GetOrdersAsync() => _client.GetAsync("/orders");
}
```

```csharp
[Theory]
[Compose]
public async Task GetOrdersAsync_ReturnsTheHandlersResponse([Shared] StubHttpMessageHandler handler, OrderApiClient client)
{
    // handler is the exact StubHttpMessageHandler instance OrderApiClient's own constructor received
    var response = await client.GetOrdersAsync();

    response.Should().BeSameAs(handler.Response);
}
```

`[Shared]` marks `handler` so `OrderApiClient`'s own constructor dependency
reuses this exact instance instead of composing a second, unrelated one —
sharing is type-keyed and exact, so `OrderApiClient`'s constructor
parameter is declared as `StubHttpMessageHandler` (not the base
`HttpMessageHandler`) to match (see
[Shared Values](../concepts/shared-values.md#scope-and-limits)).

## Discussion

`StubHttpMessageHandler` composes like any other concrete type — its
`HttpStatusCode` constructor parameter comes from Compono's built-in enum
provider, no `Compono.NSubstitute` needed. `HttpClient` itself is
never composed directly: it has more than one public constructor, which is
an ambiguous construction path Compono reports at compile time
(`CMP0001` — see [Common Errors](../troubleshooting/common-errors.md))
rather than guessing which one to use. Wrapping it inside a type with a
single constructor, like `OrderApiClient` above, is the fix — the same
pattern applies to any BCL type with more than one accessible constructor.

Want the substitute-based version instead (asserting a specific request was
sent, not just a canned response)? Compose the abstract `HttpMessageHandler`
itself via `Compono.NSubstitute` (`UseNSubstitute()`), then configure its
`protected SendAsync` through NSubstitute's `Protected()` helper.

## See also

- [Shared Values](../concepts/shared-values.md) — the full mechanics of
  `[Shared]` and why composed parameters are independent by default.
- [`Compono.NSubstitute` Package Guide](../packages/compono-nsubstitute.md)
  — abstract-class substitution and `Received`/`Returns` usage.
