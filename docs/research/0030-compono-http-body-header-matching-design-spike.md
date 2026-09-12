# [RESEARCH-0030] `Compono.Http` Body/Header Matching — Design Spike

**Status:** Done (design spike only; no ADR/Plan created). Admission is
**settled** for both capabilities per
[RESEARCH-0029](0029-post-1.2-capability-admission-research.md)'s explicit
product-owner decision — this document does not re-litigate whether these
should exist, only how. Feeds a future `Proposed` ADR directly; no code
changed in `src/`/`test/`.

**Scope:** the two admitted `Compono.Http` capabilities — request body
matching and request header matching — evaluated together per
RESEARCH-0029 §5a/§8's own conclusion that they share one architectural
blocker and should go through one design pass, even if they ship as
differently-shaped public surface.

## 1. Executive summary

**Recommendation, stated once, not as a menu:** one internal,
uniformly-async-capable matcher abstraction (`AsyncRequestMatcher`) that
every existing and new matcher — method/path, `When`, the new
`WithHeader`/`WithJsonBody<T>`/`WithFormBody`/`WithBody` — compiles down
to; `HttpResponseRegistrationBuilder` accumulates these as an ANDed,
short-circuiting chain instead of fixing one matcher at construction;
`TestHttpHandler.SendAsync`'s dispatch loop awaits that chain
sequentially, last-registered-first, exactly preserving today's
precedence contract; a public `WhenAsync` ships alongside the new named
helpers as the async peer to today's `When`. Verified, not assumed: a
real experiment (§7) confirms .NET's built-in `HttpContent` types already
buffer on first `ReadAsStringAsync`/`ReadAsByteArrayAsync` call, so no
buffering wrapper is needed in Compono.Http itself. **ADR-ready**, with
two specific points flagged for explicit product-owner confirmation
before drafting (§20/§24) — not because the design is unresolved, but
because they're consequential enough to want an explicit yes rather than
an assumed one.

## 2. What's already settled (not reopened here)

Per RESEARCH-0029: both capabilities are admitted by explicit
product-owner decision, independent of dogfooding evidence. This document
does not run Gate A/Gate B again, does not ask "should this exist," and
does not demand additional dogfooding to justify admission — the real
`alexa-vox-craft`/`cosmere-tracker` evidence gathered below (§9) is used
to *shape* the design, not to *justify* it.

## 3. Documents and code read

- [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md) —
  the accepted v1 design, in full, including its "Request matching,"
  "Precedence," "Response state: factory, not instance," "JSON / AOT,"
  and "Lifecycle, disposal, concurrency" sections.
- [RESEARCH-0009](0009-compono-http-admission-research.md) (original
  admission), [RESEARCH-0024](0024-compono-http-1.1-research.md) (1.1
  scoping — §8.2/§8.3's async-matching/header-matching analysis, §18's
  open questions), [RESEARCH-0029](0029-post-1.2-capability-admission-research.md)
  (admission decisions, §5a's design-readiness assessment — this spike
  executes exactly what §5a called for).
- `src/Compono.Http/TestHttpHandler.cs`,
  `HttpResponseRegistrationBuilder.cs`, `HttpResponseRegistration.cs` —
  read in full, current state (post-`RespondBytes`, PR #133; post-`AtLeast`/
  `AtMost`, PR #134 — neither touched this package).
- `docs/packages/compono-http.md`, `test/Compono.Http.Tests/TestHttpHandlerTests.cs`
  (420 lines, read for existing test conventions/shape).
- Read-only inspection of
  `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft` and
  `/Users/ncipollina/source/repos/ncipollina/cosmere-tracker` (§9) — no
  modifications made to either.

## 4. Current architecture, restated precisely (the constraint this design must resolve)

- `TestHttpHandler.OnGet(path)`/`.OnPost(path)`/etc. and `.When(predicate)`
  each call a private factory that builds one final
  `Func<HttpRequestMessage, bool>` and immediately constructs
  `HttpResponseRegistrationBuilder(handler, matcher, description)`
  (`TestHttpHandler.cs:205-228`). The builder's constructor
  (`HttpResponseRegistrationBuilder.cs:22-26`) immediately constructs the
  backing `HttpResponseRegistration(matcher, description)` — **the matcher
  is fixed the instant the builder exists**, before any `Respond*`/`Throws`
  call. There is no accumulator today.
- `HttpResponseRegistration.Matches(request)` (`HttpResponseRegistration.cs:24`)
  is a direct, synchronous delegate invocation.
- `TestHttpHandler.SendAsync` (`TestHttpHandler.cs:145-180`) is already
  `async Task<HttpResponseMessage>`-returning, but its matching loop
  (`for (var i = _registrations.Count - 1; i >= 0; i--) { if
  (_registrations[i].Matches(request)) ... }`) never awaits anything —
  purely synchronous linear scan, last-registered-first.
- `HttpResponseRegistrationBuilder.Finish(responseFactory)`
  (`HttpResponseRegistrationBuilder.cs:129-147`) has a one-shot `_finished`
  guard preventing a builder from being finalized twice by a second
  `Respond*`/`Throws` call — this guard is orthogonal to matcher
  composition and is unaffected by anything proposed below.

This is exactly RESEARCH-0029 §5a's finding, re-confirmed directly against
current source rather than assumed still true.

## 5. Design question 1 — matcher representation

**Recommendation: one internal delegate, `AsyncRequestMatcher`, that
every matcher — old and new — compiles to.**

```csharp
internal delegate ValueTask<bool> AsyncRequestMatcher(HttpRequestMessage request, CancellationToken cancellationToken);
```

- Every existing sync matcher (`OnX(path)`'s method+path check, `When`'s
  predicate) wraps trivially:
  `new ValueTask<bool>(syncPredicate(request))` — zero allocation for the
  synchronously-completed case, the exact pattern `Compono.XunitV3`'s own
  `ComposeAttribute.GetData` already uses today (confirmed,
  RESEARCH-0016 §2) for an already-computed `ValueTask` result.
- New matchers (`WithHeader`, `WithJsonBody<T>`, `WithFormBody`, `WithBody`)
  implement this directly, `await`ing content reads where needed.
- **Rejected: two parallel matcher lists (one sync, one async), dispatched
  separately.** This was RESEARCH-0024 §8.2's own tentative framing
  ("two parallel matching vocabularies") — rejected here on inspection:
  it would double the precedence logic (which list wins when both a sync
  and an async registration could match?) for no benefit, since wrapping
  every sync matcher in an already-completed `ValueTask<bool>` costs
  nothing measurable and keeps exactly one precedence rule.
- **Why a `CancellationToken` parameter, not just `HttpRequestMessage`:**
  see §11 — a body-reading matcher can be a real `await` point, and
  `SendAsync` already receives a token from its caller; threading it
  through means a matcher reading a large/streamed body can observe
  cancellation instead of silently ignoring it.

## 6. Design question 2 — builder composition model

**Recommendation: `HttpResponseRegistrationBuilder` accumulates an ANDed
list of `AsyncRequestMatcher` conditions, seeded by `OnX(path)`/`When(...)`,
extended by new fluent methods, compiled into one combined matcher only at
`Finish()`.**

Concretely: the builder's constructor stores its first matcher in a
private `List<AsyncRequestMatcher>` instead of handing a single delegate
straight to `HttpResponseRegistration`'s constructor. New fluent methods
(`WithHeader`, `WithJsonBody<T>`, `WithFormBody`, `WithBody`) each append
one more entry to that list and return `this`, mirroring exactly the
pattern `Compono.Logging`'s `LogVerificationBuilder.Add(description,
predicate)` already uses for chained filters (a proven, working shape
elsewhere in this codebase, not invented for this spike). `Finish()`
(unchanged one-shot guard) compiles the list into a single AND-chain
`AsyncRequestMatcher` and passes *that* to `HttpResponseRegistration`'s
constructor — so `HttpResponseRegistration` itself keeps holding exactly
one matcher, its existing shape, and the "matcher fixed once `Finish()`
runs" invariant is preserved, just moved from "at builder construction" to
"at builder finalization." This is a **private implementation change
inside `HttpResponseRegistrationBuilder`**, not a public shape change —
see §16.

**Rejected: a new intermediate builder type per chained condition**
(`.WithHeader(...)` returning a *different* type than `.OnGet(...)` did).
RESEARCH-0024 §8.3 raised this as a possibility; rejected here because it
would fragment the fluent surface (a consumer chaining `.WithHeader(...).
WithJsonBody<T>(...).Respond(...)` would be juggling three different
static types for no reason) where a single mutable accumulator on the one
existing builder type does the same job with a smaller public surface.

## 7. Design question 3 — repeated-read/buffering behavior (verified experimentally, not assumed)

**Real experiment performed** (throwaway console project, outside
`src`/`test`, deleted after use — no trace in `git status`): constructed
`FormUrlEncodedContent`, `StringContent`, and `StreamContent` instances,
called `ReadAsStringAsync()` twice on each, and separately simulated a
matcher reading a request's `Content` *before* handing that same
`HttpRequestMessage` to a real `HttpClient`/`HttpMessageHandler.SendAsync`
pipeline (a second, independent read of the same content).

**Result: every case round-tripped identically on the second read** —
`FormUrlEncodedContent`/`StringContent`/`StreamContent` all returned the
exact same string both times, and the downstream handler's own
`ReadAsStringAsync()` (after a matcher had already read the same content)
saw byte-for-byte the same value. This is not a coincidence specific to
this spike — `HttpContent.ReadAsStringAsync()`/`ReadAsByteArrayAsync()`
call `LoadIntoBufferAsync()` internally before extracting their result,
per .NET's own documented `HttpContent` contract, which buffers the
serialized content into an internal, re-readable `MemoryStream` on first
access for exactly this reason.

**Conclusion: Compono.Http needs no defensive buffering wrapper of its
own.** A matcher reading `request.Content` during matching, followed by
the real `HttpClient` pipeline (or a second chained matcher, §10) reading
the same content again, is safe by construction for every built-in
`HttpContent` type — `FormUrlEncodedContent`, `StringContent`,
`ByteArrayContent`, `JsonContent`, and `StreamContent` all inherit this
buffering behavior from the base class. **One honest caveat, not a design
requirement:** a hand-written custom `HttpContent` subclass that overrides
`SerializeToStreamAsync` to write from a genuinely single-use,
non-seekable source (e.g., piping a live, unbuffered network stream with
no internal caching) could still misbehave on a second read — but that
subclass would already misbehave identically without Compono.Http
involved at all (the real `HttpClient` pipeline reads content exactly the
same way); this is not a gap Compono.Http introduces or should try to
paper over.

## 8. Design question 4 — public `WhenAsync`: yes

**Recommendation: ship a public `WhenAsync(Func<HttpRequestMessage,
CancellationToken, ValueTask<bool>> predicate)` alongside `When`, on the
same footing.**

ADR-0051 already established that a whole-request predicate escape hatch
is legitimate, intentional product surface (`When`'s own existence, "the
whole-request escape hatch for conditions spanning
header/query-string/body/multi-dimension"), not something to avoid. Once
body/header matching exist as named helpers built on an internal async
primitive, withholding a public async equivalent of `When` would leave a
real gap: a consumer whose condition doesn't fit `WithHeader`/`WithJsonBody<T>`/
`WithFormBody`/`WithBody` (e.g., "the body is valid JSON with property X
provided the client didn't also send header Y" — one an-hoc combination)
has no public way to write it without reaching for the same internal
mechanism the named helpers already use. `WhenAsync` **is** that internal
mechanism, exposed — not a second, parallel one.

**Rejected: internal-only, no public `WhenAsync`.** This would force
every genuinely custom async condition through a support ticket/API
request instead of the same escape hatch pattern `When` already provides
for the sync case — inconsistent with ADR-0051's own established
philosophy for this exact package.

## 9. Dogfooding evidence used to shape (not justify) the design

**`alexa-vox-craft`** (`main`, current) — the primary worked example, used
verbatim below (§17):
`test/AlexaVoxCraft.Smapi.Tests/Auth/SmapiDeveloperAccessTokenProviderTests.cs`'s
`GetAccessTokenAsync_SendsFormUrlEncodedContent` test, and the real
production code it exercises
(`AlexaVoxCraft.Smapi.Auth.SmapiDeveloperAccessTokenProvider.GetAccessTokenAsync`),
which POSTs a `FormUrlEncodedContent` body with four real fields
(`grant_type`, `refresh_token`, `client_id`, `client_secret`) to
`https://api.amazon.com/auth/o2/token`. This is real evidence the body-
matching design must handle form-urlencoded content specifically, not
just JSON — confirms form-urlencoded matching belongs in v1 (§13), not as
a later addition.

**`cosmere-tracker`** (`main`, `4d25e14`) — no `Compono.Http` usage exists
(the repo pins `Compono` `0.1.0-alpha.33`, which predates the package
entirely — per RESEARCH-0029 §4). Its hand-rolled
`HttpMessageHandlerExtensions.ReturnsResponse(...)` helper
(`test/Cosmere.Tracker.TestKit/Extensions/HttpMessageHandlerExtensions.cs`)
takes an optional `Func<HttpRequestMessage, bool>` predicate — the same
synchronous-only shape `When` has today — and no test in the repo uses
that predicate to inspect a request body. This adds no new body/header-
matching evidence; recorded here to confirm it was checked, not silently
skipped, per the task's explicit dogfooding requirement.

## 10. Design question 5 — ordering and short-circuit behavior within one registration's chain

**Recommendation: evaluate a registration's chained conditions in
declaration order, short-circuiting on the first failing condition,
exactly like ordinary `&&`.** Method+path (or `When`'s seed predicate) is
always first (it's how the chain starts); each subsequent `.WithX(...)`
call appends after it. If an earlier condition fails, later conditions —
including any that would read the request body — are never evaluated.
This is not just an optimization detail: it means a registration chained
as `OnPost(path).WithHeader("X-Trace", "abc").WithJsonBody<T>(...)` never
reads the request body at all for a request that already failed the
header check, which is the right default (cheaper, and avoids the AOT-
relevant `JsonSerializer` path running unless actually needed).
**Cross-registration precedence is unchanged** — still last-registered-
first, first-fully-matching-registration-wins (ADR-0051's "Precedence"),
now with "fully matching" meaning "every chained condition on that
registration passed," evaluated as a sequential-awaited scan instead of
an instantaneous synchronous one whenever any registration in the list
uses an async condition. This must be documented explicitly (mirroring
RESEARCH-0024 §13's own flag) — it is a real, if usually invisible,
execution-shape change from today's guaranteed-synchronous scan.

## 11. Design question 6 — cancellation semantics

**Recommendation: `WhenAsync` and every new named matcher accept and
honor the same `CancellationToken` `SendAsync` already receives.**
`TestHttpHandler.SendAsync(request, cancellationToken)` already has a
token in scope; threading it into `AsyncRequestMatcher`'s signature (§5)
and every `ReadAsStringAsync(cancellationToken)`/`ReadAsByteArrayAsync(cancellationToken)`
call inside the built-in body matchers means a test that cancels mid-flight
gets `OperationCanceledException` from the matcher's own body read,
exactly the same as it would from any other awaited call in that test —
no special handling needed, no swallowing. **Unchanged:** the existing
synchronous `When(Func<HttpRequestMessage, bool>)` keeps its current
signature exactly, with no token parameter — it's a pure, fast,
synchronous check by design, and adding a token there would be a breaking
signature change for zero benefit (nothing in a synchronous predicate can
meaningfully observe cancellation mid-check anyway).

## 12. Design question 7 — exception semantics inside a matcher

**Recommendation: an exception thrown by any matcher — sync or async,
existing or new — propagates directly out of `SendAsync`, unmodified, the
same as today.** A `WithJsonBody<T>` matcher whose `JsonTypeInfo<T>`
deserialization fails against a malformed body should **not** be treated
as "didn't match, try the next registration" — that would silently hide a
real signal (the request body wasn't valid JSON, which is almost always a
genuine test/SUT bug, not routine "just try a different registration"
territory) behind a confusing `UnmatchedHttpRequestException` for the
wrong reason. This matches today's implicit behavior exactly (a `When`
predicate that throws already propagates identically — `TestHttpHandler.cs`
has no matcher-exception-catching logic anywhere today) — no new
exception type, no new catch block, this is a "keep doing what already
happens" decision, stated explicitly so a future implementer doesn't
invent swallowing behavior that isn't there today.

## 13. Design question 8 — v1 scope per body shape

| Shape | v1? | Why |
|---|---|---|
| JSON (`WithJsonBody<T>`) | **Yes** | Mirrors `RespondJson<T>`'s existing, accepted AOT split (§14) — the response side already has this, the request side is the missing symmetric half. |
| Form-urlencoded (`WithFormBody`) | **Yes** | The one real, dated, on-point dogfooding site (§9) — the motivating evidence for this whole capability. |
| Raw bytes (`WithBody(Func<byte[], bool>)`) | **Yes, as the generic foundation, not a named comparison helper** | `WithJsonBody<T>`/`WithFormBody` are themselves built by reading raw bytes/string first — exposing the raw-predicate primitive publicly costs nothing additional and is the honest "anything else" escape hatch, mirroring `RespondBytes`'s existing symmetry on the response side. No dedicated "compare to an expected `byte[]`" sugar is added — a predicate already expresses that trivially (`bytes => bytes.SequenceEqual(expected)`), and a named helper for it would be exactly the kind of un-evidenced convenience `docs/architecture/capability-admission.md`'s Step 2/3 exists to catch. |
| Streaming/multipart bodies | **No** | Zero evidence in either dogfood repo (§9); `RespondStream` was already rejected on the response side (RESEARCH-0024 §8.1) for real ownership/lifetime reasons that don't even apply here (request content is caller-owned, not Compono-constructed) — but no evidence justifies the added surface either way. |

## 14. Design question 9 — header API

**Recommendation: `WithHeader(string name, string value)` — exact-value,
case-insensitive-name matching, checking both `HttpRequestMessage.Headers`
and `HttpRequestMessage.Content?.Headers`.**

- **Case-insensitive names, case-sensitive values**: matches
  `System.Net.Http.Headers.HttpHeaders`' own real contract (header *names*
  are case-insensitive per HTTP; values are compared verbatim, same as
  every other exact-match matcher this package already has).
- **Checks both collections without the caller needing to know which
  one**: `Content-Type`/`Content-Length`/etc. live on
  `HttpContent.Headers`, everything else on `HttpRequestMessage.Headers` —
  a consumer asking "does this request have header X with value Y" rarely
  wants to first learn .NET's own two-collection split. `WithHeader`
  checks `request.Headers.TryGetValues(name, out var values)` first, then
  falls back to `request.Content?.Headers.TryGetValues(name, out values)`
  if the request-level collection didn't have it.
- **Multiple values per header name**: `TryGetValues` already returns
  `IEnumerable<string>` for a genuinely multi-valued header — match if
  **any** returned value equals the expected value (contains-among-values
  semantics, not "the header has exactly one value and it equals this").
- **A missing header is "doesn't match," not an error** — consistent with
  every other matcher in this package (a `When` predicate that checks a
  missing header today just evaluates `false`; `WithHeader` does the
  same, explicitly, rather than throwing).
- **No regex/contains-on-value/case-insensitive-value variant in v1** — no
  evidence for any of these; a consumer needing one still has `WhenAsync`/
  `When` as the escape hatch, same as any unnamed condition today.
- **Authorization/sensitive headers are a diagnostics concern, not a
  matching-semantics one** — see §15.

## 15. Diagnostics: sensitive-header redaction

**Recommendation: a small, fixed denylist of header names
(`Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`,
case-insensitive) whose **value** is redacted in registration
*description* text — never in matching behavior.**

- `WithHeader("Authorization", "Bearer secret-token")`'s contribution to
  a registration's description renders as
  `header "Authorization" = "<redacted>"`, following the exact same
  `Add(description, predicate)` pattern `Compono.Logging`'s
  `LogVerificationBuilder` already uses (§6) — the *predicate itself*
  still compares the real value normally; only the human-readable
  description string a failed `Verify()`/`UnmatchedHttpRequestException`
  might print is affected.
- **This is deliberately narrow — a small, fixed, non-configurable list,
  not a general secret-detection system.** No pattern matching, no
  entropy heuristics, no user-extensible denylist in v1 — exactly the
  same restraint this package's other design decisions already apply
  (ADR-0051's own "no strict/loose mode switch," "no
  `IHttpClientFactory` integration" — unevidenced generality is
  consistently rejected here).
- **`UnmatchedHttpRequestException`'s own message is deliberately left
  unchanged** (method + URI only, per ADR-0051's existing design) — not
  extended to print the *actual* unmatched request's headers/body. Doing
  so would risk printing a real secret from a genuinely-sent request (not
  a configured expectation) into test-failure output with no redaction
  list to catch it, since the redaction list above only applies to
  *registration* descriptions, not to arbitrary incoming requests. This
  is a deliberate scope boundary, not an oversight — flagged explicitly
  so a future implementer doesn't casually "improve" the exception
  message into a real disclosure risk.

## 16. AOT/trimming implications

- `WithJsonBody<T>(Func<T, bool> predicate, JsonTypeInfo<T> jsonTypeInfo)` —
  **AOT-safe**, no attribute needed, mirroring `RespondJson<T>(value,
  JsonTypeInfo<T>)`'s existing unannotated shape exactly (ADR-0051's "JSON
  / AOT" section, re-verified current in `docs/packages/compono-http.md`).
- `WithJsonBody<T>(Func<T, bool> predicate, JsonSerializerOptions?
  options = null)` — carries `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`,
  identical rationale and identical attribute placement to
  `RespondJson<T>(value, JsonSerializerOptions?)`'s already-accepted
  precedent (verified via that same build-spike methodology ADR-0051's
  own JSON/AOT section already used — not re-run here since the
  underlying `JsonSerializer.Deserialize<T>(string, JsonSerializerOptions?)`
  call carries the identical framework attributes as `Serialize<T>` does,
  confirmed against the framework's own API reference).
- `WithFormBody`/`WithBody`/`WithHeader` — **no JSON, no reflection, no
  new AOT/trim risk at all** — string/byte parsing and header-collection
  lookups only.
- `Compono.Http` remains generator-free and keeps `IsAotCompatible=true`
  unconditionally.

## 17. Before/after — the real `alexa-vox-craft` OAuth test

**Before** (current, shipped — real code, confirmed against
`test/AlexaVoxCraft.Smapi.Tests/Auth/SmapiDeveloperAccessTokenProviderTests.cs`):

```csharp
[Theory, Compose<SmapiHttpTestProfile>]
public async Task GetAccessTokenAsync_SendsFormUrlEncodedContent(
    [Shared] HttpTestHarness handler,
    SmapiDeveloperAccessTokenProvider provider,
    string accessToken)
{
    var lwaResponse = new { access_token = accessToken, expires_in = 3600 };

    var registration = handler.When(req => req.Content is FormUrlEncodedContent)
        .RespondJson(lwaResponse);

    var token = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

    token.Should().Be(accessToken);
    registration.Verify().Once();
}
```

This only proves the SUT sent *some* `FormUrlEncodedContent` — it cannot
verify which `grant_type`/`client_id`/`client_secret`/`refresh_token`
values were actually sent, even though the production code
(`SmapiDeveloperAccessTokenProvider.GetAccessTokenAsync`) constructs all
four deliberately.

**After** (illustrative — this design, not yet built):

```csharp
[Theory, Compose<SmapiHttpTestProfile>]
public async Task GetAccessTokenAsync_SendsFormUrlEncodedContent(
    [Shared] HttpTestHarness handler,
    SmapiDeveloperAccessTokenProvider provider,
    string accessToken,
    string refreshToken,
    string clientId,
    string clientSecret)
{
    var lwaResponse = new { access_token = accessToken, expires_in = 3600 };

    var registration = handler.OnPost("/auth/o2/token")
        .WithFormBody(form =>
            form["grant_type"] == "refresh_token" &&
            form["refresh_token"] == refreshToken &&
            form["client_id"] == clientId &&
            form["client_secret"] == clientSecret)
        .RespondJson(lwaResponse);

    var token = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

    token.Should().Be(accessToken);
    registration.Verify().Once();
}
```

Now a genuinely wrong field (a typo'd parameter name, a swapped
`client_id`/`client_secret`) fails this test directly, at the point the
wrong request is sent — not silently passing because "some form content"
was good enough.

## 18. Compatibility / breaking-change risk

**Zero.** Every new member (`WhenAsync`, `WithHeader`, `WithJsonBody<T>`
×2, `WithFormBody`, `WithBody`) is additive. `When`'s existing public
signature and behavior are untouched — a project using only today's API
sees no change at all. `HttpResponseRegistrationBuilder`'s internal
storage change (§6, one fixed matcher → an accumulated list, compiled at
`Finish()`) is a private implementation detail with no public shape
change — every existing `OnGet(path).RespondJson(...)`-style call site
keeps compiling and behaving identically, since a single-entry
accumulator compiles to exactly the same effective matcher a fixed one
did. `IsAotCompatible=true` is preserved unconditionally (§16). No
generator involvement, no new package dependency, no SemVer-significant
surface beyond the expected additive minor-version bump.

## 19. Rejected alternatives

- **A full WireMock-style priority/regex/stateful-scenario DSL** —
  already rejected once at admission (ADR-0051's "Considered Options";
  reaffirmed in RESEARCH-0024 §15) — out of `Compono.Http`'s stated scope
  as a testing primitive, not a WireMock competitor; nothing here
  reopens that.
- **Wrapping body/header matchers in core `Match<T>`** — ADR-0051 already
  rejected wrapping the whole-request predicate in `Match<T>` because its
  `Equality`/`Any` states are meaningless for a request object with no
  sensible default equality; the identical reasoning applies to a request
  body or a header-value-set, which have the same "no sensible default
  equality, 'any' is free via omitting the condition" shape. A plain
  predicate stays the right vocabulary for both, consistent with `When`'s
  own existing design.
- **Compono.Http managing its own content-buffering layer** — rejected
  per §7's verified experiment: the built-in `HttpContent` types already
  buffer correctly; adding a Compono-owned buffering wrapper would be
  unevidenced complexity solving a problem that doesn't exist.
  Reflected on the response side as this document's search discipline
  applied — no separate buffering-related package/dependency is
  considered here since none is warranted.
- **Internal-only `WhenAsync` with no public escape hatch** — rejected
  per §8: inconsistent with `When`'s own established role as public,
  intentional escape-hatch surface.
- **A configurable/user-extensible sensitive-header denylist in v1** —
  rejected per §15: unevidenced generality; a small fixed list matches
  this package's consistent restraint elsewhere.

## 20. Unresolved issues needing an explicit product-owner decision before the ADR

1. **Whether body and header matching are spelled as one chainable fluent
   surface on a single `OnX(...)`/`When(...)` builder** (this document's
   recommendation, §6) **or as fully independent, separately-invoked
   mechanisms.** This is the single most consequential public-API-shape
   decision in this whole design — it determines whether
   `handler.OnPost(path).WithHeader(...).WithJsonBody<T>(...)` is legal,
   idiomatic Compono.Http, or whether header/body conditions must each be
   expressed through their own separate, non-composable entry point. This
   spike recommends the unified chain and believes the reasoning above is
   sound, but it's consequential enough to want an explicit confirmation
   rather than an assumed yes before an ADR locks it in.
2. **The exact sensitive-header denylist and whether it should ever be
   extensible.** This spike recommends a small, fixed, non-configurable
   list (`Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`)
   for v1 (§15) — lower-stakes than #1, but still a product-facing
   decision about what "small and predictable" means in practice, worth
   a quick explicit sign-off rather than silent assumption.

No other open question in this document rises to this bar — every other
decision above (matcher representation, builder model, cancellation,
exception semantics, ordering, AOT posture, v1 body-shape scope) is
resolved with a specific, reasoned recommendation, not left open.

## 21. ADR-readiness verdict

**Ready.** This spike resolves every architectural fork RESEARCH-0029 §5a
identified as blocking an ADR (matcher-composition mechanism, sync/async
dispatch model, `WhenAsync` public-or-not) with a specific, justified
recommendation, verifies the one empirical unknown (repeated-read/
buffering behavior) with a real experiment rather than an assumption, and
grounds the v1 scope decision in the real dogfooding evidence RESEARCH-0029
gathered. A `Proposed` ADR can be drafted directly from this document's
recommendations — contingent on the two explicit confirmations in §20,
which are product decisions this spike deliberately did not make
unilaterally.

## Evidence index

- `src/Compono.Http/TestHttpHandler.cs`,
  `HttpResponseRegistrationBuilder.cs`, `HttpResponseRegistration.cs` —
  read in full for §4-§6, §10-§12.
- [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md),
  `docs/packages/compono-http.md` — read in full for §8, §14-§16, §19.
- [RESEARCH-0024](0024-compono-http-1.1-research.md) — §8.1-§8.3, §13,
  §15 re-read directly for §5, §10, §13, §19.
- [RESEARCH-0029](0029-post-1.2-capability-admission-research.md) — §4,
  §5a, §8 re-read directly; this spike is its direct continuation.
- Read-only inspection of
  `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft`
  (`SmapiDeveloperAccessTokenProviderTests.cs`,
  `SmapiDeveloperAccessTokenProvider.cs`) and
  `/Users/ncipollina/source/repos/ncipollina/cosmere-tracker`
  (`HttpMessageHandlerExtensions.cs`) — §9, §17; no modifications made.
- A throwaway console experiment (`FormUrlEncodedContent`/`StringContent`/
  `StreamContent` repeated-read behavior, plus a real
  `HttpMessageHandler.SendAsync` round trip after a simulated matcher
  pre-read) — run outside `src`/`test`, deleted after use, no trace left
  in `git status` — primary evidence for §7.

## Links

- Feeds a `Proposed` ADR for `Compono.Http` body/header matching directly,
  per `tasks/design.md` — contingent on §20's two confirmations.
- Supersedes nothing; extends ADR-0051 additively, per §18.
