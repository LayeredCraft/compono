# [ADR-0062] `Compono.Http`: Body and Header Request Matching

**Status:** Accepted

**Date:** 2026-09-11

**Decision Makers:** Nick Cipollina (product owner), Claude (design)

## Context

`Compono.Http`'s `TestHttpHandler` (`ADR-0051`, shipped in `1.0`) matches
requests on HTTP method and path only (`OnGet`/`OnPost`/etc., plus
`Match<string>` path variants) or through `When(Func<HttpRequestMessage,
bool>)`, a fully synchronous whole-request escape hatch. Reading a
request's body — the single most common thing a real HTTP-client test
needs to assert beyond method/path — requires `await
request.Content.ReadAsStringAsync()`, which `When`'s synchronous predicate
cannot express without either a deadlock-risking
`.GetAwaiter().GetResult()` or settling for a weaker, non-content check.

This is not a hypothetical gap. A real, shipped `alexa-vox-craft` test
(`SmapiDeveloperAccessTokenProviderTests.GetAccessTokenAsync_SendsFormUrlEncodedContent`)
can only assert `req.Content is FormUrlEncodedContent` — a type check —
because it cannot assert on the actual `grant_type`/`refresh_token`/
`client_id`/`client_secret` field values the real production code
(`SmapiDeveloperAccessTokenProvider.GetAccessTokenAsync`) sends. Header
matching faces the identical "only reachable via `When`, no first-class
vocabulary" gap.

**Admission is already settled**, by explicit product-owner decision, not
re-litigated here:
[RESEARCH-0029](../research/0029-post-1.2-capability-admission-research.md)
admits both capabilities independent of dogfooding evidence.
[RESEARCH-0030](../research/0030-compono-http-body-header-matching-design-spike.md)
is this ADR's design spike, resolving every architectural fork with a
specific recommendation and an experimentally-verified content-buffering
finding. This ADR also resolves RESEARCH-0030 §20's two open questions,
per explicit product-owner direction (2026-09-11):

1. Body and header matching ship as **one coherent, chainable fluent
   request-matching surface** on the existing
   `HttpResponseRegistrationBuilder` — not separate, non-composable entry
   points.
2. Sensitive-header redaction in diagnostic/description output uses a
   **small, fixed denylist** (`Authorization`, `Proxy-Authorization`,
   `Cookie`, `Set-Cookie`) — no configurability, no general secret
   detection, in v1.

## Decision Drivers

- Real, shipped consumer test (`alexa-vox-craft`) currently asserts a
  weaker condition than its own intent demands, because no first-class
  body-matching vocabulary exists.
- Preserve `TestHttpHandler`'s existing, accepted (`ADR-0051`) contract
  exactly for every consumer not using the new capability — no breaking
  change to `When`, `OnX(...)`, precedence, or unmatched-request behavior.
- Native AOT compatibility (`IsAotCompatible=true`) must be preserved
  unconditionally, matching this package's existing, unique-in-the-repo
  posture.
- No reflection-based `JsonSerializer.Deserialize<T>` path without an
  explicit, opt-in, honestly-annotated escape hatch — mirror
  `RespondJson<T>`'s existing `JsonTypeInfo<T>`/`JsonSerializerOptions?`
  split exactly, don't invent a third shape.
- Small, predictable, non-configurable diagnostics — this package
  consistently rejects unevidenced generality (`ADR-0051`'s "no
  strict/loose mode," "no `IHttpClientFactory` integration").
- A representation for form-urlencoded content must not silently lose
  legitimate data (duplicate keys are real, valid HTML form shapes).

## Considered Options

1. **One coherent, chainable async-capable matcher model on the existing
   builder** (this ADR's choice) — `HttpResponseRegistrationBuilder`
   accumulates an ANDed list of matchers; every matcher, old and new,
   compiles to one internal async delegate type; a public `WhenAsync`
   exposes the same primitive the named helpers use internally.
2. **Separate, non-composable header/body matching entry points** (e.g.
   `handler.OnGet(path)` for path, a wholly independent
   `handler.MatchingHeader(name, value)` registration list evaluated
   orthogonally) — rejected; see Rejected Alternatives.
3. **Two parallel matcher pipelines** (sync list dispatched synchronously,
   async list dispatched separately, first-matching-list-wins or similar)
   — rejected; see Rejected Alternatives.
4. **Internal-only async matching, no public `WhenAsync`** — rejected; see
   Rejected Alternatives.

## Decision Outcome

**Chosen option: Option 1.** One internal async matcher abstraction, one
accumulating builder, one small public vocabulary
(`WithHeader`/`WithBody`/`WithFormBody`/`WithJsonBody<T>`/`WhenAsync`)
layered onto the existing `HttpResponseRegistrationBuilder` — because it
is the only option that gives a consumer one coherent, composable request-
matching model (`OnPost(path).WithHeader(...).WithFormBody(...)`) while
preserving every existing public contract byte-for-byte.

**Wording correction (post-review):** an earlier draft of this sentence
claimed the new architecture costs "nothing (no allocation...)" for a
consumer who never touches the new surface. Stated that broadly, it
overclaims — the accepted matcher-accumulation architecture (D2) means
every registration, including a plain `OnX(path)`-only one, now builds
through a small per-registration list rather than handing one delegate
straight to `HttpResponseRegistration`'s constructor, which is some real,
if small, registration-construction overhead beyond `1.0`–`1.2`'s exact
shape. The accurate claim, scoped correctly: **sync-only matching needs no
`Task`/async state-machine allocation or runtime suspension machinery** —
D1's `ValueTask<bool>`-wrapping keeps a synchronously-completing condition
synchronously completing, with no `Task` object, no `await`-driven state
machine, and no measurable behavioral difference from direct delegate
invocation for the common case. This is not a claim that the new builder
allocates exactly zero additional objects compared to `1.2`'s
single-fixed-delegate shape — small, per-registration construction
overhead from matcher accumulation (D2) is expected and acceptable; the
implementation Plan (PLAN-0065) records the precise allocation contract
this ADR intends, and does not treat "zero additional allocation" as a
requirement to design around.

### D1 — Internal matcher representation

One internal delegate type, used by every matcher, old and new:

```csharp
namespace Compono.Http;

internal delegate ValueTask<bool> AsyncRequestMatcher(HttpRequestMessage request, CancellationToken cancellationToken);
```

Every existing sync condition (method+path, `When`'s predicate) wraps as
`new ValueTask<bool>(syncPredicate(request))` — a synchronously-completed
`ValueTask<bool>`, zero heap allocation, the same pattern
`Compono.XunitV3`'s `ComposeAttribute.GetData` already uses today for an
already-computed result. New conditions (`WithHeader`, `WithBody`,
`WithFormBody`, `WithJsonBody<T>`) implement this delegate directly,
`await`ing content reads where needed. This is `internal` — never part of
`Compono.Http`'s public surface; a consumer never constructs or names this
type directly, only the public methods that produce it.

### D2 — Builder accumulation model

`HttpResponseRegistrationBuilder` holds a private `List<AsyncRequestMatcher>`,
seeded by whichever `OnX(...)`/`When(...)`/`WhenAsync(...)` call produced
the builder. Each new `WithHeader`/`WithBody`/`WithFormBody`/
`WithJsonBody<T>` call appends one more entry and returns `this` — the
same "fluent chain appends to a list, compiled at the end" shape
`Compono.Logging`'s `LogVerificationBuilder.Add(description, predicate)`
already uses elsewhere in this codebase, not invented for this ADR.
`Finish()` (the existing one-shot `_finished`-guarded terminal step,
unchanged) compiles the accumulated list into a single AND-chain
`AsyncRequestMatcher` and constructs `HttpResponseRegistration` with it —
`HttpResponseRegistration` itself keeps holding exactly one matcher, its
existing shape, unchanged in its own file. **This is a private
implementation change inside `HttpResponseRegistrationBuilder` only** —
see D3/the Public API Review for why zero public signatures move.

### D3 — `When` compatibility

`When(Func<HttpRequestMessage, bool> predicate)`'s public signature,
return type, and behavior are **completely unchanged**. Internally, it now
seeds the builder's matcher list with one wrapped entry instead of handing
a bare delegate straight to `HttpResponseRegistration`'s constructor — a
call site that never chains a `WithX(...)` method after `When(...)`
produces the exact same effective matcher, compiled from a one-element
list instead of held directly. No consumer-observable difference exists
for any code written against `1.0`/`1.1`/`1.2`.

### D4 — Public `WhenAsync`

```csharp
public HttpResponseRegistrationBuilder WhenAsync(Func<HttpRequestMessage, CancellationToken, ValueTask<bool>> predicate)
```

Ships as a public peer to `When`, on `TestHttpHandler` (same declaring
type, same access level, same role). `ADR-0051` already established that a
whole-request predicate escape hatch is intentional, permanent product
surface (`When`'s own existence) — once `WithHeader`/`WithJsonBody<T>`/
etc. exist as named helpers built on an internal async primitive,
withholding a public async equivalent of `When` would leave a genuinely
custom async condition (one that doesn't fit any named helper) with no
public way to express itself. `WhenAsync` *is* the internal primitive,
exposed — not a second, parallel mechanism. The `CancellationToken`
parameter is deliberate (D9) — the internal `AsyncRequestMatcher` shape
threads it through uniformly, and a consumer's own async predicate can
observe cancellation the same way any other awaited call in the test
would.

**Return type:** `HttpResponseRegistrationBuilder`, exactly matching
`When`'s existing return type — `WhenAsync(...)` seeds a builder exactly
like `When(...)` does; nothing about its return shape differs.

### D5 — Header matching semantics

```csharp
public HttpResponseRegistrationBuilder WithHeader(string name, string value)
```

- **Header-name comparison is case-insensitive**; **value comparison is
  ordinal (case-sensitive)** — matches `System.Net.Http.Headers.HttpHeaders`'
  own real contract (names are case-insensitive per HTTP; every other
  exact-match condition in this package already compares values ordinally).
- **Checks both header collections with no precedence between them —
  revised, corrected during ADR review.** An earlier draft of this
  section tried `HttpRequestMessage.Headers` first and only fell back to
  `HttpContent.Headers` when the request-level collection had *no entry
  at all* for that name. That's wrong: `HttpHeaders.TryGetValues` (the
  real, verified BCL contract — it returns `false` for an absent header,
  it never throws) can find the name present with a *different* value in
  one collection while the *matching* value sits in the other — a
  fallback that only triggers on "name absent" would wrongly return
  `false` for a request where, say, `request.Headers` happens to carry an
  unrelated `X-Custom: something-else` and `request.Content.Headers`
  carries the actual expected value under the same name. **Corrected
  contract: gather every value for `name` from *both* collections
  unconditionally (`request.Headers.TryGetValues(name, out r)` and
  `request.Content?.Headers.TryGetValues(name, out c)`, each independently
  optional/absent, never short-circuited by the other), and match if
  `value` appears anywhere in the combined set.** A consumer asking "does
  this request carry header X with value Y" should not need to know in
  advance that `Content-Type`/`Content-Length`-family headers live on
  `HttpContent.Headers` while everything else lives on
  `HttpRequestMessage.Headers` — that split is a `System.Net.Http`
  implementation detail this method deliberately hides, and hiding it
  correctly means checking both collections as one combined set, not as a
  first-then-fallback pair. **No separate `WithContentHeader` API is
  added** — this correction is a same-signature behavior fix, not a new
  capability; nothing about the motivating evidence (`Content-Type`
  verification, D7/Issue 3 below) needs a header name to be
  collection-specific.
- **Multiple values per header name**: `TryGetValues` already returns
  `IEnumerable<string>` for a genuinely multi-valued header (or a single-
  element sequence for an ordinary one) — `WithHeader` matches if **any**
  returned value equals `value` (contains-among-values semantics, not
  "must be the header's sole value").
- **A missing header is "doesn't match," never an exception** — consistent
  with every existing matcher in this package (a `When` predicate that
  inspects an absent header today evaluates `false`, not throws;
  `WithHeader` does the same, explicitly).
- **No regex/value-contains/case-insensitive-value variant in v1** — no
  evidence for any of these; `WhenAsync`/`When` remain the escape hatch for
  a condition this named helper doesn't cover, exactly as they already are
  for every other unnamed condition today.

### D6 — Sensitive-header redaction (diagnostics only, never matching)

A small, fixed, non-configurable denylist — `Authorization`,
`Proxy-Authorization`, `Cookie`, `Set-Cookie` (case-insensitive
name comparison, matching D5's own name-comparison rule) — whose
**value** is redacted only in a registration's human-readable
*description* string (the text `Verify()`'s failure message or a future
diagnostic surface would print). **Matching itself always compares the
real value** — redaction is purely a presentation concern, applied at the
point a description string is built, never inside the matcher predicate
itself. A `WithHeader("Authorization", "Bearer secret-token")`
contribution to a registration's description renders as `header
"Authorization" = "<redacted>"`; `WithHeader("X-Trace-Id", "abc123")`
renders its real value unchanged. This is deliberately narrow — a fixed
list, not a pattern-matching or entropy-based secret detector, matching
this package's established restraint elsewhere (`ADR-0051`'s consistent
rejection of unevidenced configurability).

**`UnmatchedHttpRequestException`'s message is unchanged** (method + URI
only) — not extended to print the *actual* unmatched request's headers or
body. The redaction list above only covers *registration* descriptions
(values the test itself configured as an expectation); a real, incoming
request's headers were never vetted against that list, so printing them
verbatim on a failure could leak a genuine secret the test never intended
to expose. Left alone deliberately, not by omission.

### D7 — Body matching: raw bytes, form-urlencoded, JSON

```csharp
public HttpResponseRegistrationBuilder WithBody(Func<byte[], bool> predicate)
```

The generic foundation — reads `request.Content` via
`ReadAsByteArrayAsync(cancellationToken)` (D9) and invokes `predicate`.
**A request with no content (`Content is null`) never invokes `predicate`
— the condition evaluates to `false`** (same "absent thing → doesn't
match, no exception" rule as D5's missing header). No dedicated
"compare to an expected `byte[]`" convenience is added — a predicate
already expresses that trivially (`bytes => bytes.SequenceEqual(expected)`),
and a named comparison helper would be exactly the unevidenced convenience
this package's admission process consistently rejects.

```csharp
public HttpResponseRegistrationBuilder WithFormBody(Func<ILookup<string, string>, bool> predicate)
```

**Representation, reconsidered from RESEARCH-0030's spike draft:** the
spike tentatively proposed `IReadOnlyDictionary<string, string>`. That
representation is **lossy for legitimate form-urlencoded requests** — a
real HTML form (checkbox groups, multi-select fields) can and does send
the same key more than once (`tags=a&tags=b`), and .NET's own
`application/x-www-form-urlencoded` model does not forbid it. A
`Dictionary<string, string>` can only retain one value per key (silently
dropping every earlier occurrence, or throwing `ArgumentException` on
construction depending on how it's built) — a consumer asserting on a
multi-valued field would either get a confusing exception or silently
wrong data with no signal anything was lost. **`ILookup<string, string>`**
(a real, existing BCL type from `System.Linq`, built via `.ToLookup()`
over the parsed key/value pairs in wire order) is the correct
representation instead: its indexer (`form["tags"]`) returns
`IEnumerable<string>` — every value for that key, in the order they
appeared, or an **empty sequence** (never a throw) for a key that wasn't
present at all. A single-valued field reads naturally as
`form["grant_type"].SingleOrDefault() == "refresh_token"` or
`.Contains("refresh_token")`; a multi-valued field reads as
`form["tags"].Contains("a") && form["tags"].Contains("b")`. This is not a
new dependency — `ILookup<TKey, TElement>`/`Enumerable.ToLookup` are core
`System.Linq`, already an implicit part of any C# project's BCL surface,
reflection-free, and AOT-safe.

**Parsing — corrected during ADR review.** An earlier draft of this
section claimed `Uri.UnescapeDataString` alone handles both percent-
decoding and the `+`-means-space rule `application/x-www-form-urlencoded`
requires. That's inaccurate: `Uri.UnescapeDataString` only percent-decodes
(`%XX` sequences) — it has no concept of `+` at all and leaves a literal
`+` character untouched. The correct, still BCL-only, still
reflection-free mechanism, verified by a focused experiment (below): split
the body on `&`, split each resulting piece on the *first* `=` into a raw
key and raw value (a piece with no `=` is a valueless key with an empty
value), then **replace every raw `+` character with a literal space
*before* calling `Uri.UnescapeDataString`** on each of the key and the
value independently:

```csharp
static string DecodeFormComponent(string raw) => Uri.UnescapeDataString(raw.Replace('+', ' '));
```

**Ordering is what makes this correct**: `string.Replace('+', ' ')` only
touches literal `+` bytes already present in the raw, still-percent-
encoded text — a percent-encoded literal plus sign (`%2B`) is three
different characters (`%`, `2`, `B`) at that point, not a `+`, so the
`Replace` call cannot and does not touch it; `Uri.UnescapeDataString`
then correctly turns `%2B` into a literal `+` in the final decoded value.
Doing this in the opposite order (`Uri.UnescapeDataString` first, then
`Replace('+', ' ')`) would be wrong — it would also convert an
intentionally-encoded `%2B`'s decoded `+` into a space, indistinguishable
from a real space.

**Verified with a focused, temporary experiment** (a throwaway console
project, outside `src`/`test`, deleted after use, no trace in `git
status`), against exactly the cases this ADR review asked to validate:

| Input | Parsed as |
|---|---|
| `name=Nick+Cipollina` | `name` = `"Nick Cipollina"` |
| `value=a%2Bb` | `value` = `"a+b"` |
| `tags=a&tags=b` | `tags` = `"a"`, `tags` = `"b"` (both retained, in order) |
| `empty=` | `empty` = `""` |
| `flag` (no `=`) | `flag` = `""` |

All five decoded exactly as expected — the `+`/`%2B` distinction is
preserved correctly, duplicate keys are both retained in wire order, and
an empty value and a valueless key both decode to an empty string rather
than throwing or being dropped.

**Representation is unaffected by this correction** — `ILookup<string,
string>` (D7, unchanged) still correctly holds the result: each decoded
`(key, value)` pair is added to the lookup in wire order via
`.ToLookup()`, so `tags=a&tags=b` still produces `form["tags"]` yielding
`"a"` then `"b"`, in that order. Malformed percent-escapes are still
passed through unchanged by `Uri.UnescapeDataString` rather than throwing
(D10's "no real analog to malformed" conclusion for form bodies is
unaffected by this correction — it was never about the `+`/percent-decode
mechanism, only about `Uri.UnescapeDataString`'s own non-throwing
behavior, which is unchanged).

```csharp
public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonTypeInfo<T> jsonTypeInfo)

[RequiresDynamicCode("...")]
[RequiresUnreferencedCode("...")]
public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonSerializerOptions? options = null)
```

Mirrors `RespondJson<T>`'s existing, already-accepted AOT split exactly
(D8). `T?` (not bare `T`) in the predicate's parameter is deliberate — a
request body can legitimately deserialize to `null` (a literal JSON
`null` body, or an empty body under certain `JsonSerializerOptions`), and
the predicate should see that honestly rather than the API silently
special-casing it.

**Streaming/multipart bodies are explicitly out of scope for this ADR** —
zero evidence in either dogfood repo (`alexa-vox-craft`, `cosmere-tracker`;
RESEARCH-0030 §9), and `RespondStream` was already rejected on the
response side (RESEARCH-0024 §8.1) for real ownership/lifetime reasons.
Nothing here forecloses adding it later with its own evidence and its own
design pass.

### D7a — Content-Type/media-type contract per body matcher (new, ADR review)

**The real motivating gap this decision closes:** the old `alexa-vox-craft`
test asserted `req.Content is FormUrlEncodedContent` — which verifies
*representation* (this is form-urlencoded content), not field values. A
naive `.WithFormBody(...)` that only parses bytes as form data, with no
media-type check, would accept a `text/plain` body that merely *happens*
to look like `grant_type=refresh_token&...` — a real regression from what
the old test verified, not just a smaller one. Decided independently per
body matcher, per explicit product-owner instruction not to assume they
share one answer:

**`WithFormBody` — requires `Content-Type: application/x-www-form-urlencoded`.**
Adopted as the product owner's stated default, and independently justified
here, not merely deferred to: "form body" names an HTTP *representation*,
the same way `OnGet`/`OnPost` name a *method* — a body matcher should
verify the representation it's named after, not merely succeed at parsing
bytes that happen to fit. Concretely: `WithFormBody` checks
`request.Content?.Headers.ContentType?.MediaType` against
`"application/x-www-form-urlencoded"` (`StringComparison.OrdinalIgnoreCase`
— media types are case-insensitive per RFC 9110 §8.3.1), **before**
parsing the body at all (cheaper, and consistent with D11's declaration-
order short-circuiting — a wrong media type never triggers a body read).
A missing `Content-Type`, or any other media type, means the condition
evaluates to `false` — never an exception, consistent with every other
"absent thing → no match" rule in this ADR (D5's missing header, D7's
absent content). **This closes the motivating gap directly**: the rewritten
`alexa-vox-craft` test (Worked Example 3) now verifies both the
representation (via `WithFormBody`'s own implicit `Content-Type` check)
and the field values (via the parsed `ILookup`), in one call, with no
separate `.WithHeader("Content-Type", ...)` needed — genuinely simpler
than the old test, not merely different.

**`WithJsonBody<T>` — requires a JSON media type, evaluated independently
and landing on the same answer as `WithFormBody`, for the same reason, not
by default inheritance.** A JSON media type is `application/json` exactly,
or any type whose subtype ends in the registered `+json` structured
syntax suffix (RFC 6839) — real, existing media types like
`application/vnd.api+json` (JSON:API) or `application/hal+json` (HAL) are
legitimate JSON bodies a consumer might reasonably want to match. The
check stays small and bounded, not a general media-type-matching
subsystem: `mediaType is not null && (mediaType.Equals("application/json",
StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+json",
StringComparison.OrdinalIgnoreCase))`. **Rejected alternative, considered
seriously: ignore `Content-Type` and match on deserializability alone.**
This would mean a `text/plain` body that happens to deserialize
successfully into `T` silently satisfies `WithJsonBody<T>` — the identical
representation-vs-parseability gap `WithFormBody`'s own decision exists to
close, for no offsetting benefit (a consumer who genuinely wants "match
if this parses as JSON regardless of what `Content-Type` claims" still has
`WithBody`/`WhenAsync` available, same as any other unnamed condition).
Checked, like `WithFormBody`, **before** deserialization is attempted —
a wrong media type never triggers `JsonSerializer.Deserialize<T>`, so
D10's malformed-JSON-propagates-loudly rule only ever applies to a body
that already passed the media-type gate.

**`WithBody` — deliberately media-type-agnostic, confirmed explicitly.**
`WithBody(Func<byte[], bool>)` is this package's raw, unopinionated
foundation — it never inspects `Content-Type` and matches purely on the
byte content a consumer's own predicate decides to accept or reject. This
is intentional, not an oversight: `WithBody` is the escape hatch for
exactly the case where a consumer's condition doesn't correspond to a
named representation (`WithFormBody`/`WithJsonBody<T>`) at all — binding
it to a media-type check would remove the one thing that makes it a
genuine fallback rather than a third named-representation helper wearing
a generic name.

### D8 — JSON/AOT overload model

Identical shape and identical reasoning to `RespondJson<T>`'s existing,
accepted split (`ADR-0051` "JSON / AOT," re-verified current against
`docs/packages/compono-http.md`):

- `WithJsonBody<T>(Func<T?, bool>, JsonTypeInfo<T>)` — **AOT-safe, no
  attribute**. Bypasses runtime resolver lookup entirely; the recommended
  path for any AOT/trim-sensitive project.
- `WithJsonBody<T>(Func<T?, bool>, JsonSerializerOptions? = null)` —
  carries `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]`, the same
  attributes `JsonSerializer.Deserialize<T>(string, JsonSerializerOptions?)`
  itself carries, propagated honestly rather than suppressed — identical
  rationale and identical attribute placement to `RespondJson<T>`'s own
  `JsonSerializerOptions?` overload. Under a Native-AOT/trim-checked
  project, this overload produces the framework's real IL2026/IL3050
  warnings at the *consumer's* call site, not silently.

No third JSON shape (no source-generator-context-only overload, no
`JsonDocument`-based structural matcher) is added — two overloads, exactly
mirroring the response side, is the whole v1 surface.

### D9 — Content-read/buffering contract (conservative, re-verified)

**What Compono.Http itself guarantees, stated narrowly:**

- Every built-in matcher (`WithBody`, `WithFormBody`, `WithJsonBody<T>`)
  reads `request.Content` using an **ordinary, public** `HttpContent` read
  API — `WithBody` calls `ReadAsByteArrayAsync(CancellationToken)`;
  `WithFormBody` and both `WithJsonBody<T>` overloads call
  `ReadAsStringAsync(CancellationToken)`, since both operate on text
  (percent-encoded form pairs, JSON) rather than raw bytes. No private
  reflection, no `ReadAsStream()`/`ReadAsStreamAsync()` direct-stream
  access, no custom buffering wrapper of Compono's own, in any case.
- **Compono.Http performs no buffering, consumption, disposal, or
  replacement of the caller-supplied `HttpContent` itself.** The
  `HttpRequestMessage`/`HttpContent` instance handed to `SendAsync` is the
  same instance every matcher (and, ultimately, the response-building
  step) observes — Compono never swaps it for a copy, never disposes it,
  never mutates its headers.
- **What Compono.Http does *not* promise:** that every conceivable
  `HttpContent` subclass is safely re-readable an arbitrary number of
  times. That property belongs to `System.Net.Http.HttpContent`'s own base
  contract, not to anything Compono adds — a custom `HttpContent`
  implementation remains solely responsible for its own repeatability
  contract, exactly as it already would be for a real `HttpClient`
  pipeline with no Compono involved at all.

**Verified experimentally, not assumed** (two throwaway console
experiments, outside `src`/`test`, deleted after use, no trace in `git
status`):

1. (RESEARCH-0030 §7) `FormUrlEncodedContent`, `StringContent`, and a
   `MemoryStream`-backed `StreamContent` each returned byte-for-byte
   identical results across two consecutive `ReadAsStringAsync()` calls,
   and a simulated "matcher reads content, then a real
   `HttpMessageHandler.SendAsync` pipeline reads the same
   `HttpRequestMessage`'s content again" round trip produced the same
   value both times.
2. **(New, this ADR)** — RESEARCH-0030's stream case used a seekable
   `MemoryStream` as its backing source, which doesn't rule out a
   genuinely non-seekable, forward-only stream behaving differently. A
   targeted follow-up experiment wrapped a `MemoryStream` in a custom
   `Stream` subclass reporting `CanSeek => false` and throwing on
   `Seek`/`Length`/`Position`, passed it to `StreamContent`, and called
   `ReadAsStringAsync()` twice followed by `ReadAsByteArrayAsync()` once
   more. **All three reads returned identical, correct content.** This
   confirms `HttpContent`'s internal `LoadIntoBufferAsync()` reads its
   underlying source stream **exactly once**, caches the result in its
   own internal buffer, and serves every subsequent
   `ReadAsStringAsync`/`ReadAsByteArrayAsync` call from that cache — the
   source stream's own seekability is irrelevant to whether *repeated
   high-level reads* work.

**Conclusion:** for every content type Compono.Http's own built-in
matchers are designed to handle (`FormUrlEncodedContent`, `StringContent`,
`ByteArrayContent`, `JsonContent`, `StreamContent` over any source
stream), reading the body once for matching and letting the rest of the
dispatch pipeline read it again afterward is safe by construction — **not
because Compono.Http adds any safety mechanism, but because it relies
entirely on `HttpContent`'s own, already-existing base-class behavior and
never works around or bypasses it.**

### D10 — Malformed-content vs. doesn't-satisfy-predicate: exception semantics

Two genuinely different failure modes, made explicit rather than
conflated:

1. **Content that parses/deserializes successfully but the predicate
   returns `false`** — this is an ordinary non-match. The registration
   simply doesn't match this request; `TestHttpHandler` moves on to the
   next-highest-precedence registration exactly as it would for a failed
   method/path/header check.
2. **Content that cannot be interpreted as the type the consumer
   explicitly asked for** (`WithJsonBody<T>` against a body that isn't
   valid JSON, or that's valid JSON but doesn't deserialize to `T`) —
   **this propagates the underlying exception (`JsonException`) directly
   out of `SendAsync`, unchanged, never caught and reinterpreted as "no
   match."** Silently treating malformed JSON as a non-match would hide a
   real signal — the request body wasn't what the code under test was
   supposed to send — behind a confusing, unrelated
   `UnmatchedHttpRequestException` naming the wrong reason.

**`WithFormBody` has no real analog to "malformed."**
`application/x-www-form-urlencoded` parsing (splitting on `&`/`=`,
percent-decoding via `Uri.UnescapeDataString`) accepts nearly any byte
sequence as *some* sequence of key/value pairs — `Uri.UnescapeDataString`
passes an invalid percent-escape through literally rather than throwing,
and a body with no `=` characters at all simply parses as one key with an
empty value. There is effectively no form-urlencoded input this parser
throws on, so D10's malformed/non-match distinction is a **JSON-specific**
concern in practice, not a general body-matching one — stated here
explicitly so a future implementer doesn't invent unneeded
try/catch-and-treat-as-non-match logic around `WithFormBody`.

**`WithBody`'s raw-bytes predicate has no interpretation step at all** —
the bytes are handed to the predicate exactly as received; there is
nothing to fail to parse.

This is a "keep doing what already happens, stated precisely" decision for
the general case (no matcher-exception-catching logic exists anywhere in
`TestHttpHandler` today, and none is added) — the only genuinely new
behavior is that `WithJsonBody<T>` is now the first matcher whose own
internal logic (`JsonSerializer.Deserialize<T>`) can itself throw for a
reason unrelated to the predicate.

### D11 — Ordering and short-circuit behavior

**Within one registration's chained conditions**: evaluated in
declaration order, short-circuiting on the first failing condition,
exactly like ordinary `&&`. `OnPost(path).WithHeader(h, v).WithFormBody(f)`
never reads the request body for a request that already failed the header
check — cheaper, and avoids invoking `WithJsonBody<T>`'s
`JsonSerializer`-based path unless actually needed.

**Across registrations**: **unchanged** — still last-registered-first,
first-fully-matching-registration-wins (`ADR-0051` "Precedence"), where
"fully matching" now means "every chained condition on that registration's
matcher passed." The scan becomes a sequentially-awaited one (rather than
an instantaneously-synchronous one) whenever any registration in the list
uses an async condition — this is a real, documented execution-shape
change from `1.0`'s guaranteed-synchronous scan, though not an
observable *outcome* change for any existing test (the same registration
wins in the same order; it just may now be reached via `await` instead of
a direct call).

## Public API Review

Full concrete signatures for every affected public member, reviewed as a
consumer would read them (overload resolution, nullability, naming,
AOT annotations, discoverability):

```csharp
namespace Compono.Http;

public sealed class TestHttpHandler : HttpMessageHandler
{
    // Unchanged from 1.0/1.1/1.2 — included here only to confirm no
    // signature moved:
    public HttpResponseRegistrationBuilder OnGet(string path);
    public HttpResponseRegistrationBuilder OnGet(Match<string> path);
    public HttpResponseRegistrationBuilder OnPost(string path);
    public HttpResponseRegistrationBuilder OnPost(Match<string> path);
    public HttpResponseRegistrationBuilder OnPut(string path);
    public HttpResponseRegistrationBuilder OnPut(Match<string> path);
    public HttpResponseRegistrationBuilder OnPatch(string path);
    public HttpResponseRegistrationBuilder OnPatch(Match<string> path);
    public HttpResponseRegistrationBuilder OnDelete(string path);
    public HttpResponseRegistrationBuilder OnDelete(Match<string> path);
    public HttpResponseRegistrationBuilder When(Func<HttpRequestMessage, bool> predicate);
    public HttpClient CreateClient(Uri? baseAddress = null);
    public IReadOnlyList<HttpRequestMessage> Requests { get; }

    // New — one addition, same declaring type as When:
    public HttpResponseRegistrationBuilder WhenAsync(Func<HttpRequestMessage, CancellationToken, ValueTask<bool>> predicate);
}

public sealed class HttpResponseRegistrationBuilder
{
    // Unchanged terminal methods — every one still finalizes the builder
    // via the existing one-shot Finish() guard, unaffected by matcher
    // accumulation:
    public HttpResponseRegistration Respond(HttpStatusCode statusCode);
    public HttpResponseRegistration RespondText(string content, string mediaType = "text/plain", Encoding? encoding = null);
    public HttpResponseRegistration RespondJson<T>(T value, JsonSerializerOptions? options = null);
    public HttpResponseRegistration RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo);
    public HttpResponseRegistration RespondBytes(byte[] content, string mediaType = "application/octet-stream");
    public HttpResponseRegistration Throws(Exception exception);

    // New — fluent, chainable, each returns this same builder type:
    public HttpResponseRegistrationBuilder WithHeader(string name, string value);
    public HttpResponseRegistrationBuilder WithBody(Func<byte[], bool> predicate);
    public HttpResponseRegistrationBuilder WithFormBody(Func<ILookup<string, string>, bool> predicate);
    public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonTypeInfo<T> jsonTypeInfo);

    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use the WithJsonBody<T>(Func<T?, bool>, JsonTypeInfo<T>) overload for native AOT applications.")]
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the WithJsonBody<T>(Func<T?, bool>, JsonTypeInfo<T>) overload, or make sure all of the required types are preserved.")]
    public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonSerializerOptions? options = null);
}
```

**Overload-resolution check:** `WithJsonBody<T>`'s two overloads differ on
their second parameter's type (`JsonTypeInfo<T>` vs. `JsonSerializerOptions?`)
— the same shape `RespondJson<T>` already uses without ambiguity; calling
`WithJsonBody<Order>(o => o?.Id == 42, AppJsonContext.Default.Order)`
resolves unambiguously to the `JsonTypeInfo<T>` overload, and
`WithJsonBody<Order>(o => o?.Id == 42)` (relying on the `options = null`
default) resolves unambiguously to the other — identical to
`RespondJson<T>`'s already-shipped, already-proven-unambiguous resolution.

**Nullability check:** `WithJsonBody<T>`'s predicate parameter is `T?`
(D7) — a consumer's lambda must handle a possible `null` deserialized
value, which the compiler enforces under nullable-reference-types-enabled
projects (the predicate parameter type is exactly `T?`, not `T`).
`WithHeader`'s `string name`/`string value` are non-nullable, matching
every other exact-match parameter in this package (`OnGet(string path)`,
etc.) — passing `null` throws `ArgumentNullException` immediately, the
same guard shape `On(method, path)`/`When(predicate)` already use.

**Naming check against the existing vocabulary:** `WithX` matches no
existing prefix in `Compono.Http` directly, but mirrors `Compono.Logging`'s
`WithEventId`/`WithException<T>`/`WithMessageContaining` (`Compono.Logging`'s
`LogVerificationBuilder`) closely enough that a consumer who has used
either package recognizes the pattern immediately — "declarative filter
condition, chainable, returns the same builder type." `WhenAsync` follows
the already-established `XAsync` suffix convention .NET itself uses
everywhere (`ReadAsStringAsync`, `SendAsync`) and reads naturally next to
`When`.

**Discoverability check:** every new member lives on one of the two
existing public types this package already has (`TestHttpHandler`,
`HttpResponseRegistrationBuilder`) — no new public type is introduced. A
consumer typing `handler.OnPost(path).With` in an IDE sees every new
matcher condition via ordinary IntelliSense, discoverable the same way
`RespondJson`/`RespondText`/`RespondBytes` already are today.

**AOT-annotation check:** exactly one of the six new/changed public
members carries AOT attributes (`WithJsonBody<T>(Func<T?,bool>,
JsonSerializerOptions?)`), matching `RespondJson<T>`'s existing one-of-two
pattern precisely — no new attribute shape invented.

## Worked Examples

**1. Existing GET usage — completely unchanged:**

```csharp
handler.OnGet("/v1/customers/42").RespondJson(customer, AppJsonContext.Default.Customer);
```

**2. Header matching:**

```csharp
handler.OnGet("/v1/orders")
    .WithHeader("Authorization", $"Bearer {expectedToken}")
    .RespondJson(orders, AppJsonContext.Default.OrderListDto);

// A failure here (Verify().Once() throwing) describes the registration as:
// "GET /v1/orders with header \"Authorization\" = \"<redacted>\""
```

**3. The real `alexa-vox-craft` OAuth form request, rewritten** (before:
`req.Content is FormUrlEncodedContent` type-check only; after: real field
assertions — and, per D7a, `WithFormBody` itself now requires
`Content-Type: application/x-www-form-urlencoded`, so this single call
verifies both the representation the old test checked *and* the field
values it couldn't, with no separate header assertion needed):

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
            form["grant_type"].SingleOrDefault() == "refresh_token" &&
            form["refresh_token"].SingleOrDefault() == refreshToken &&
            form["client_id"].SingleOrDefault() == clientId &&
            form["client_secret"].SingleOrDefault() == clientSecret)
        .RespondJson(lwaResponse);

    var token = await provider.GetAccessTokenAsync(TestContext.Current.CancellationToken);

    token.Should().Be(accessToken);
    registration.Verify().Once();
}
```

**4. AOT-safe JSON POST body matching** (per D7a, `WithJsonBody<T>`
requires `Content-Type: application/json` or a `+json`-suffixed media
type — an ordinary `JsonContent`/`RespondJson`-style client request
already sends `application/json`, so this is transparent for the
overwhelming majority of real JSON clients):

```csharp
handler.OnPost("/v1/orders")
    .WithJsonBody<CreateOrderRequest>(
        body => body is { CustomerId: 42, Items.Count: > 0 },
        AppJsonContext.Default.CreateOrderRequest)
    .Respond(HttpStatusCode.Created);
```

**5. A custom condition via `WhenAsync`** (something no named helper
covers — here, "the body is valid JSON AND a specific header is absent"):

```csharp
handler.WhenAsync(async (req, ct) =>
{
    if (req.Headers.Contains("X-Idempotency-Key"))
        return false;

    var body = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
    return body is not null && JsonDocument.Parse(body).RootElement.TryGetProperty("orderId", out _);
}).Respond(HttpStatusCode.BadRequest);
```

## Relationship to ADR-0051

**This ADR extends `ADR-0051` — it does not contradict or reopen any of
its decisions.** Checked explicitly, section by section:

- **"Request matching"** — `ADR-0051` scoped v1 to method+path (`OnX`) and
  a synchronous whole-request `When`, explicitly naming "no dedicated
  header/query-string/JSON-body matcher types" and "matching stays
  synchronous... no `alexa-vox-craft` matcher reads request content
  asynchronously as part of matching" as v1 facts, **not** as permanent
  prohibitions — both statements were true descriptions of v1 scope, which
  this ADR is the evidenced, deliberate expansion of, not a reversal of.
  Nothing in `ADR-0051`'s own text frames these as never-to-be-revisited.
- **"Precedence"** — unchanged (D11); still last-registered-first,
  first-match-wins.
- **"Unmatched requests: strict by default"** — unchanged;
  `UnmatchedHttpRequestException`'s message is deliberately left exactly
  as `ADR-0051` specified (D6).
- **"Response state: factory, not instance"** — untouched; this ADR is
  entirely about *request* matching, no response-side behavior changes.
- **"JSON / AOT"** — extended, not changed: `WithJsonBody<T>`'s two-overload
  split is the request-side mirror of `RespondJson<T>`'s existing,
  unmodified response-side split (D8).
- **"Verification"** — untouched; `HttpResponseRegistration.Verify()`
  still returns the same, unmodified `CallVerifier`.
- **"Request log"** — untouched; `Requests` is unaffected by anything
  here.
- **"Lifecycle, disposal, concurrency"** — untouched; this ADR adds no new
  disposal or ownership concern (D9 is explicit that Compono.Http still
  never owns, buffers, or disposes caller-supplied content).

**No point of actual conflict was found.** The one place a future reader
might wonder about tension — `ADR-0051`'s "matching stays synchronous"
sentence — is resolved above: that sentence described v1's *scope*, cited
against the *evidence available at the time* ("no `alexa-vox-craft`
matcher reads request content asynchronously as part of matching" was
true in `1.0`; it is no longer true, per RESEARCH-0029/0030's later
evidence), not a standing architectural constraint this ADR would need to
override.

## Rejected Alternatives

- **`WithHeader` falling back to `HttpContent.Headers` only when
  `HttpRequestMessage.Headers` has no entry for the name at all** — this
  ADR's own first draft, rejected during review (D5): a header name
  present with a *non-matching* value in one collection and the expected
  value in the other would wrongly evaluate to `false`, since the
  fallback only triggers on absence, not on a failed match. Corrected to
  check both collections unconditionally and match on the combined set.
- **`Uri.UnescapeDataString` alone for form-body decoding, with no
  explicit `+`-to-space step** — this ADR's own first draft, rejected
  during review (D7): `Uri.UnescapeDataString` only percent-decodes; it
  has no `+`-means-space behavior at all, so a real, common form value
  like `Nick+Cipollina` would have decoded incorrectly (literal `+`
  retained instead of a space). Corrected to replace raw `+` characters
  with spaces *before* percent-decoding each component.
- **Ignoring `Content-Type` for `WithJsonBody<T>` and matching on
  deserializability alone** — considered seriously (D7a), rejected: it
  would let a non-JSON-labeled body (e.g. `text/plain`) that merely
  happens to deserialize successfully silently satisfy the matcher,
  reopening the same representation-vs-parseability gap `WithFormBody`'s
  own `Content-Type` requirement exists to close, for no offsetting
  benefit — `WithBody`/`WhenAsync` remain available for a consumer who
  genuinely wants content-type-agnostic parseability matching.
- **Separate, non-composable header/body matching entry points** (Option
  2) — rejected per explicit product-owner direction (Context): body and
  header matching must belong to one composable registration model. Also
  independently weaker on the merits: a consumer needing both a header
  and a body condition on the same request (the realistic common case —
  an authenticated JSON POST) would have no way to express "both of these
  together, on this one registration" without a fluent chain; two
  independent registration lists evaluated orthogonally would need a
  cross-list correlation mechanism this package has no reason to invent.
- **Two parallel matcher pipelines** (sync list dispatched synchronously,
  async list dispatched separately) (Option 3) — rejected in
  RESEARCH-0030 §5 and reaffirmed here: doubles precedence logic (which
  list wins when both a sync and an async registration could match a
  request?) for zero measurable benefit, since wrapping every sync
  matcher in an already-completed `ValueTask<bool>` costs nothing.
- **Internal-only async matching, no public `WhenAsync`** (Option 4) —
  rejected per D4: inconsistent with `When`'s own established role as
  permanent, intentional public escape-hatch surface; would strand any
  condition the named helpers don't cover.
- **Compono-owned content-buffering layer** — rejected per D9's verified
  experiments: the built-in `HttpContent` types (including a genuinely
  non-seekable source stream) already buffer correctly on first read;
  adding a Compono-owned buffering wrapper would be unevidenced complexity
  solving a problem that doesn't exist for any content shape this
  package's matchers are designed to handle.
- **A full WireMock-style matching/mocking DSL** (numeric priority,
  regex path matching, stateful scenarios, request/response middleware) —
  already rejected once at `ADR-0051`'s own admission and reaffirmed in
  RESEARCH-0024 §15; nothing here reopens `Compono.Http`'s scope as a
  testing primitive, not a general HTTP-mocking framework.
- **A configurable/extensible sensitive-header redaction framework** —
  rejected per explicit product-owner direction (Context) and this
  package's consistent restraint elsewhere: a small, fixed, non-
  configurable list is deliberately narrower than a general secret-
  detection system, and no evidence justifies more.
- **`IReadOnlyDictionary<string, string>` for form-body representation**
  (RESEARCH-0030's own tentative shape) — rejected per D7/product-owner
  instruction: lossy for legitimate duplicate-key form data;
  `ILookup<string, string>` replaces it as the accurate, still-BCL-only,
  still-AOT-safe representation.

## Positive Consequences

- Closes a real, shipped consumer gap (`alexa-vox-craft`'s OAuth test) with
  a concrete before/after rewrite that asserts what the test actually
  intends.
- Fully additive — every existing `1.0`/`1.1`/`1.2` call site compiles and
  behaves identically; `IsAotCompatible=true` preserved unconditionally.
- One coherent fluent model, not a growing set of unrelated escape
  hatches — `WithHeader`/`WithBody`/`WithFormBody`/`WithJsonBody<T>` all
  compose on the same builder, on the same internal primitive `WhenAsync`
  also exposes.
- Small, predictable diagnostics posture (D6) consistent with this
  package's established restraint.

## Negative Consequences

- `TestHttpHandler.SendAsync`'s dispatch loop becomes a sequentially-
  awaited scan whenever any registration uses an async condition, rather
  than a guaranteed-synchronous one — a real, if usually invisible,
  execution-shape change from `1.0`'s contract (mitigation: documented
  explicitly, D11; no observable *outcome* change for any existing test).
- `HttpResponseRegistrationBuilder`'s internal matcher storage changes
  from a single fixed delegate to an accumulated, compiled-at-`Finish()`
  list — real internal complexity added to a previously simple type
  (mitigation: fully private, zero public surface change, D2/D3).
- `WithJsonBody<T>` introduces the first case where a `Compono.Http`
  matcher can itself throw for a reason independent of the predicate
  (malformed JSON) — a new failure mode a consumer must understand
  (mitigation: documented explicitly and deliberately, D10, matching how
  `RespondJson<T>`'s own serialization already can throw on the response
  side).

## Links

- [RESEARCH-0029](../research/0029-post-1.2-capability-admission-research.md) —
  admission decision for both capabilities.
- [RESEARCH-0030](../research/0030-compono-http-body-header-matching-design-spike.md) —
  the design spike this ADR formalizes; every design question here
  traces back to a specific RESEARCH-0030 section, with one revision
  (form-body representation, D7) and one additional experiment (D9)
  made explicit above.
- [ADR-0051](0051-compono-http-handler-based-testing-package.md) — the
  `Compono.Http` v1 design this ADR extends; see "Relationship to
  ADR-0051," above.
- [RESEARCH-0024](../research/0024-compono-http-1.1-research.md) — earlier
  1.1-scoping research whose §8.1 `RespondStream` rejection and §15
  rejected-ideas list this ADR's own Rejected Alternatives section builds
  on directly.
- `test/AlexaVoxCraft.Smapi.Tests/Auth/SmapiDeveloperAccessTokenProviderTests.cs`,
  `SmapiDeveloperAccessTokenProvider.cs` (`alexa-vox-craft`) — the real
  before/after example (Worked Example 3).
