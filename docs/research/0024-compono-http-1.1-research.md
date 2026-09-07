# [RESEARCH-0024] Compono.Http 1.1 Research

**Status:** Done (research only; no ADR yet)

**Feeds:** a future ADR scoping `Compono.Http`'s share of a `1.1.0`
minor release. Builds on `docs/research/0009-compono-http-admission-research.md`
(the original Gate A/B admission research) and `docs/adr/0051-compono-http-handler-based-testing-package.md`
(the accepted design, now with Amendment 1 — path-matcher split — and
Amendment 2 — `RespondBytes`, PR #133). Does not re-litigate admission;
`Compono.Http` exists and ships in 1.0. This document asks only: what, if
anything, in this package deserves a `1.1` addition, and is anything here
usable standalone from core Compono.

**Trigger:** PR #133 (`RespondBytes`) correctly bumped the next preview to
`1.1.0`, but is too small to justify a minor release alone. This research
looks for other genuinely additive candidates in the same package before
committing to what `1.1` actually contains.

---

## 1. Current package responsibility

`Compono.Http` is a **non-generated, runtime-only** package providing a
single public surface: `TestHttpHandler`, a reflection-free
`HttpMessageHandler` subclass for testing code built on
`HttpClient`/`HttpMessageHandler`. It answers one question — "let me
control what an `HttpClient` sees as a response, and verify what it sent"
— and deliberately nothing else. ADR-0051's Decision Drivers explicitly
bound scope: no reflection, no generator involvement, minimal package
graph (no `Microsoft.Extensions.Http`, no `Compono.TestDoubles`, no
`Compono.DependencyInjection`).

## 2. Current architecture

Four public types, all in `src/Compono.Http/`:

- `TestHttpHandler : HttpMessageHandler` (`TestHttpHandler.cs`) — holds a
  `List<HttpResponseRegistration>`, dispatches last-registered-first/
  first-match-wins (`SendAsync`, lines 156–164), records every request in
  a thread-safe log (`Requests`, `RecordRequest`), throws
  `UnmatchedHttpRequestException` when nothing matches (strict-by-default,
  no implicit 404 fallback). `CreateClient(Uri?)` wraps itself in an
  `HttpClient` with `disposeHandler: false` — the handler is caller-owned
  and multiple clients may share one.
- `HttpResponseRegistrationBuilder` (`HttpResponseRegistrationBuilder.cs`)
  — the fluent finisher returned by `OnGet`/`OnPost`/`OnPut`/`OnPatch`/
  `OnDelete`/`When`. Terminal methods: `Respond(HttpStatusCode)`,
  `RespondText(string, mediaType, encoding?)`, `RespondJson<T>(T,
  JsonSerializerOptions?)` (AOT-unsafe, `[RequiresDynamicCode]`/
  `[RequiresUnreferencedCode]`), `RespondJson<T>(T, JsonTypeInfo<T>)`
  (AOT-safe), `RespondBytes(byte[], mediaType)` (Amendment 2, defensively
  clones the input array), `Throws(Exception)` (same instance rethrown
  every match — no factory/callback). A `_finished` guard prevents
  double-finalizing one builder (line 137–141).
- `HttpResponseRegistration` (`HttpResponseRegistration.cs`) — the
  verification handle returned by every terminal method. Holds the
  matcher, a `Func<HttpRequestMessage, HttpResponseMessage>`
  response-factory (never a stored instance — "factory, not instance"),
  and an `Interlocked`-incremented match count. `Verify()` returns a
  **core** `Compono.CallVerifier` unchanged — `Never()`/`Once()`/
  `Exactly(n)` only, no `AtLeast`/`AtMost` (ADR-0044 Requirement 3 binds
  this at the core level, not an `Http`-specific choice).
- `UnmatchedHttpRequestException` — describes method + URI only.

Path matching uses **core `Match<T>`** (`Match<string>`) for the
single-scalar `OnX(path)` overload, but a **plain
`Func<HttpRequestMessage, bool>`** for the whole-request `When(...)`
predicate (ADR-0051 Amendment 1: `Match<T>` exposes no accessor beyond
`Matches()`, so `Compono.Http` can't produce an honest diagnostic string
for an `Is(...)`-based `Match<string>`, only for a literal `string`).

## 3. Current dependency graph

```
Compono.Http.csproj:
  <ProjectReference Include="..\Compono\Compono.csproj" PrivateAssets="none" />
  (no other PackageReference)
```

Confirmed via `src/Compono.Http/Compono.Http.csproj`: the **only**
dependency is core `Compono`, referenced with `PrivateAssets="none"`
(consumers get a transitive `Compono` reference — this is a real,
first-class dependency, not incidental packaging). No
`Microsoft.Extensions.Http`. No generator project reference — this is the
first Compono integration package with **zero** source-generator
involvement (contrast `Compono.TestDoubles`, `Compono.XunitV3`).

**Why it depends on core `Compono` — exactly two touch points, both
compile-time and both reuse-not-recreate:**

1. `Match<string>` (core `src/Compono/Match.cs`) — used only in the
   `OnGet(Match<string> path)`-style overloads. This is genuinely a
   **shared abstraction** reuse, not composition — `Match<T>` has no
   dependency on `CompositionBuilder`/`[Composable]`/anything
   composition-graph-shaped; it is a standalone value type.
2. `CallVerifier` (core `src/Compono/CallVerifier.cs`) — used only in
   `HttpResponseRegistration.Verify()`. Same story: `CallVerifier` is a
   `readonly struct` taking `(int observedCount, string
   memberDescription)` in its constructor — no composition dependency
   whatsoever.

Neither dependency touches `CompositionRow`, `[Shared]`,
`CompositionBuilder`, `ICompositionProvider`, or any other actual
*composition* concept. `TestHttpHandler` is plain-constructed
(`new TestHttpHandler()`); nothing about its lifecycle is
composition-owned (ADR-0051 states this explicitly: "Compono composition
does not own or dispose it"). The dependency on core `Compono` exists
**purely to reuse two small, composition-agnostic value/verification
types** — this is a "shared infrastructure" dependency, not a "runtime
composition integration" or "generator integration" one.

## 4. Existing 1.0 public contract

```csharp
var handler = new TestHttpHandler();

handler.OnGet("/v1/things/42")
    .RespondJson(thing, ThingJsonContext.Default.Thing);

handler.OnPost(Match<string>.Any())
    .Respond(HttpStatusCode.Created);

handler.When(req => req.Method == HttpMethod.Get && req.Headers.Contains("X-Trace"))
    .Throws(new HttpRequestException("simulated transport failure"));

var registration = handler.OnGet("/v1/things/42").RespondBytes(certBytes, "application/x-x509-ca-cert");

using var client = handler.CreateClient(new Uri("https://api.example.test"));
// exercise client...

registration.Verify().Once();
handler.Requests.Should().ContainSingle(r => r.RequestUri!.PathAndQuery == "/v1/things/42");
```

No `IHttpClientFactory` integration, no request-body matching, no header
matching, no stream response, no sequential/conditional responses beyond
last-match-wins precedence, no status-only convenience beyond
`Respond(HttpStatusCode)` (which already covers "status-only" — there is
no separate "empty response" concept needed).

## 5. Consumer ergonomics review

Walking every payload/matching/failure axis the task calls out, against
what's actually implemented (`grep` confirms zero matches for
`RequestContentMatch`, header-matching, or `StreamContent` handling
anywhere in `src/Compono.Http/` or the 420-line
`test/Compono.Http.Tests/TestHttpHandlerTests.cs`):

| Axis | State |
|---|---|
| byte arrays | ✅ `RespondBytes` (1.1-adjacent, just shipped) |
| streams | ❌ no `RespondStream`; see §8.1 |
| `HttpContent` (arbitrary) | ❌ no `Respond(HttpContent)` escape hatch |
| strings | ✅ `RespondText` |
| JSON | ✅ `RespondJson<T>` (both overloads) |
| status-only | ✅ `Respond(HttpStatusCode)` |
| empty responses | ✅ subsumed by `Respond(HttpStatusCode)` |
| response headers (non-content) | ❌ no way to add e.g. `Retry-After`, `ETag` to a response |
| content headers beyond `Content-Type` | ❌ no way to set `Content-Encoding`, `Content-Disposition`, etc. |
| media types/content types | ✅ every `Respond*` takes one |
| reason phrases | ❌ not settable (defaults to the framework's for the status code) |
| custom `HttpResponseMessage` | ❌ no "just give me the message to finish myself" escape hatch |
| method matching | ✅ `OnGet`/`OnPost`/`OnPut`/`OnPatch`/`OnDelete` (no `OnHead`/`OnOptions`) |
| URI/path matching | ✅ exact string or `Match<string>` |
| query-string matching | ⚠️ folded into path (`PathAndQuery`) — no independent query-param matcher |
| header matching | ❌ only via `When(...)`'s whole-request predicate |
| request content/body matching | ❌ only via `When(...)`, and only synchronously (see §8.2 — this is the sharpest real gap) |
| JSON body matching | ❌ none; `When` predicate would need synchronous access to an already-buffered body |
| raw body matching | ❌ same |
| multiple configured responses / sequencing | ❌ last-match-wins is static; no "respond X then Y then Z" |
| conditional responses | ⚠️ possible today via `When(...)` + closured mutable state, but not first-class |
| callbacks | ❌ none |
| request inspection | ✅ `handler.Requests` (post-hoc), no live/streaming inspection |
| non-success status codes | ✅ `Respond(HttpStatusCode.InternalServerError)` etc. — already fully solved, not a gap |
| `HttpRequestException` | ✅ `Throws(new HttpRequestException(...))` — already fully solved |
| timeout/cancellation | ❌ no built-in `OperationCanceledException`/`TaskCanceledException` convenience — achievable via `Throws`, but `Throws` never checks `cancellationToken`, so it can't distinguish a caller-cancelled call |
| malformed payloads | ✅ trivially achievable via `RespondBytes`/`RespondText` with garbage content — not a gap, already general enough |
| transport-level failures | ✅ `Throws(new HttpRequestException(...))`/`Throws(new SocketException(...))` — already solved |

## 6. Observed or likely friction

Two real friction clusters emerge, not a long tail of small requests:

**A. Response-body-shape gaps are nearly closed after `RespondBytes`.**
Streams and raw `HttpContent` are the only remaining payload primitives,
and both have real ownership/lifetime traps (§8.1) that argue for
deliberate exclusion rather than quiet omission.

**B. Request-side matching stops at path/method.** Every other axis
(headers, query params, body) funnels through `When(Func<HttpRequestMessage,
bool>)`, and that predicate is **synchronous**, while reading
`request.Content` (to inspect a JSON body, form data, etc.) is
fundamentally **asynchronous** (`ReadAsStringAsync`/`ReadAsByteArrayAsync`
return `Task<T>`). A consumer wanting to match on request body today must
either: (a) pre-buffer content into a byte array before it's sent
(usually impossible — the body is produced by the system under test at
send time), or (b) call `.GetAwaiter().GetResult()` inside the predicate,
a synchronous-over-asynchronous anti-pattern that risks deadlocks in
`SynchronizationContext`-bound environments (the same class of hazard
ADR-0001 exists to avoid within Compono's own architecture, even though
this instance is at a test-authoring seam rather than the composition
engine itself).

This is the load-bearing finding: **request body matching isn't merely
missing, it's structurally awkward to add without a design decision**,
because `TestHttpHandler.SendAsync` is itself `async` and *could* await a
body-reading predicate — but every existing matcher type
(`Func<HttpRequestMessage, bool>`, core `Match<T>`) is synchronous, and
introducing an async matcher shape is a genuine two-way door (§8.2).

## 7. Candidate improvements

Ranked list evaluated in §8; two rejected outright to keep scope honest
(§15):

1. **Async-aware request body matching / inspection** (`WithJsonBody<T>`,
   `WithContent(Func<HttpRequestMessage, ValueTask<bool>>)`, or similar) —
   addresses friction cluster B, the one with no workaround that isn't an
   anti-pattern.
2. **`RespondStream` for streaming response bodies** — addresses the one
   remaining payload-primitive gap, but only if ownership/lifetime
   semantics can be made unambiguous (§8.1 — this candidate came in
   *rejected* after analysis, see below).
3. **Header-matching convenience (`WithHeader(name, value)` on the builder
   chain)** — a narrower, synchronous-only slice of cluster B that could
   ship independently of the harder async-body problem.

## 8. Detailed analysis of each serious candidate

### 8.1 `RespondStream(...)` — analyzed and NOT recommended for 1.1

The task explicitly asks whether `RespondStream(...)` would be natural.
Tracing the existing `RespondBytes` precedent (ADR-0051 Amendment 2)
against stream semantics surfaces a real conflict:

- **Byte arrays are trivially cloneable and re-fresh-able** —
  `RespondBytes` clones the input once at registration time
  (`(byte[])content.Clone()`), and every matched call constructs "a fresh
  `ByteArrayContent` over that private copy." This is possible *because*
  a `byte[]` can be copied and a fresh `ByteArrayContent` can wrap the
  same underlying bytes indefinitely, arbitrarily many times, with no
  state mutation between reads.
- **A `Stream` cannot be treated the same way.** A `Stream` is
  stateful and single-pass by default (`Position`, `CanSeek`) — reading
  it once (as `HttpContent`'s `StreamContent` does when the response body
  is serialized) advances or exhausts it. A naive `RespondStream(Stream
  stream)` signature would work exactly once, then silently return empty
  content or throw `ObjectDisposedException` on every subsequent matched
  call — a correctness trap the "factory, not instance" architecture
  (ADR-0051) was specifically designed to prevent for every other
  `Respond*` method.
- Fixing this "for real" needs a **factory**: `RespondStream(Func<Stream>
  streamFactory)`, matching `RespondBytes`'s snapshot for freshness. But
  this raises **ownership** ambiguity the task explicitly flags: does
  `TestHttpHandler`/the resulting `StreamContent` dispose the stream the
  factory returns after each response is consumed? `HttpContent.Dispose()`
  disposes its wrapped stream by default — so a factory returning a
  `MemoryStream` per call is fine (cheap, always re-creatable), but a
  factory wrapping a `FileStream` or a caller-owned stream needs an
  explicit non-disposing wrapper, which is exactly the kind of extra API
  surface (`leaveOpen`, a wrapping `NonDisposingStream`) that turns one
  clean method into a small ownership sub-API.
- **Compono's synchronous composition model** doesn't itself conflict
  here (response bodies aren't produced during composition), but stream
  *creation* being potentially I/O-bound (e.g., reading a fixture file
  per response) does mean a synchronous `Func<Stream>` factory is the
  only fit — an `async` factory would need `TestHttpHandler`'s dispatch to
  await it, which is fine (`SendAsync` is already `Task`-returning), but
  then compounds with the async-matcher design question in §8.2 rather
  than being independent of it.

**Verdict:** `RespondStream` is not a clean two-line addition. It either
(a) ships with a silent single-use footgun that contradicts the
established "factory, not instance" invariant, or (b) needs a genuine
mini ownership-and-lifetime design (factory + optional disposal policy) —
a small ADR-worthy decision on its own, not a drive-by 1.1 addition.
Given `RespondBytes` already covers the overwhelmingly common binary-body
case (small fixtures, certs, images), and the marginal case (streaming a
genuinely large or I/O-bound body through a test double) is rare in
practice, this doesn't clear the "real, repeated friction" bar ADR-0039's
Gate B sets. **Recommendation: do not add in 1.1; revisit only if a real
dogfooding signal surfaces (per ADR-0039's evidence standard), and only
alongside an explicit ownership-model ADR.**

### 8.2 Async-aware request body/header matching — top candidate

**Consumer problem:** A consumer testing an HTTP client that POSTs a JSON
body cannot assert-and-branch on that body's content without either (a)
abusing `When`'s side-effect-capture idiom from `alexa-vox-craft` history
(exactly the anti-pattern ADR-0051's own admission research flagged as a
`Compono.Http`-worthy problem, see `docs/research/0009-...`'s
"Request capture without a real API" section) or (b) blocking on
`.Result`/`.GetAwaiter().GetResult()` inside a synchronous predicate.

**Proposed conceptual API** (illustrative, not a locked design):

```csharp
handler.OnPost("/v1/orders")
    .WithJsonBody<CreateOrderRequest>(body => body.CustomerId == expectedId)
    .Respond(HttpStatusCode.Created);
```

or, more conservatively, a single async-capable escape hatch alongside
the existing synchronous `When`:

```csharp
handler.WhenAsync(async req =>
{
    var body = await req.Content!.ReadAsStringAsync();
    return body.Contains(expectedFragment);
});
```

**Why it belongs in `Compono.Http`:** this is the one matching axis where
the *current* API actively pushes consumers toward an anti-pattern
(sync-over-async) rather than merely lacking a convenience. Every other
missing-matcher gap (query params, headers) has a workable, non-hazardous
`When(...)` workaround today; body matching does not.

**Implementation complexity:** moderate. `TestHttpHandler.SendAsync` is
already `async`-compatible (returns `Task<HttpResponseMessage>`), so
awaiting an async predicate during dispatch is mechanically
straightforward. The real design cost is in **not** duplicating
`Match<T>`/`Func<HttpRequestMessage, bool>` into a third parallel
matching vocabulary — likely needs its own `Func<HttpRequestMessage,
ValueTask<bool>>`-shaped overload set (`WhenAsync`, or an
`IAsyncRequestMatcher` distinct from `When`'s sync `Func`), which is new
API surface, not a body-reading convenience layered on the existing one.

**Compatibility risk:** low — purely additive (`WhenAsync` alongside
`When`, or a new `WithJsonBody<T>` builder method); no change to existing
generated/public shapes.

**Testing implications:** needs coverage for ordering semantics when a
registration's matcher must be awaited during dispatch (does an async
matcher change last-match-wins evaluation from "synchronous linear scan"
to "sequential awaited scan"? — yes, mechanically, since `for` + `await`
inside the loop body serializes evaluation; this should be documented,
not just implemented).

**Should it wait for a later release?** No — of the three candidates,
this is the one with a demonstrable anti-pattern-forcing gap today (not
just an inconvenience), which is the strongest form of the "real friction"
bar this research is measuring against. Recommend it as the anchor
feature for `1.1`.

### 8.3 Header-matching convenience — secondary candidate

**Consumer problem:** matching on a request header (e.g. `Authorization`,
a correlation ID, `Accept-Language`) today requires dropping to
`When(req => req.Headers.Contains(...))`, losing the `OnGet(path)`-style
readable diagnostics (`_description` stays `"When(...) request"` rather
than something like `GET /v1/things/42 with header X-Trace`).

**Proposed conceptual API:**

```csharp
handler.OnGet("/v1/things/42")
    .WithHeader("X-Trace", "abc123")
    .RespondJson(thing);
```

This needs `HttpResponseRegistrationBuilder` (or a builder-returned
intermediate) to compose an *additional* matcher condition onto the one
`On(method, path)` already built — a real, if small, shape change: today
`OnX` returns a builder whose matcher is already fixed at construction
(`On(method, path)` closes over `description`/`matcher` immediately).
Adding fluent matcher composition (`.WithHeader(...)`) before a terminal
`Respond*`/`Throws` call means either (a) making the matcher mutable
during the builder phase, or (b) `WithHeader` returning a *new*
intermediate builder type layering an `AND` condition — more design
surface than it first looks.

**Why it belongs in `Compono.Http`:** synchronous, no async complexity
(headers are available on `HttpRequestMessage` without buffering) — a
much smaller, self-contained version of the §8.2 problem.

**Implementation complexity:** low-to-moderate — mechanically simple
matcher composition, but touches `HttpResponseRegistrationBuilder`'s
current "matcher fixed at construction" shape.

**Compatibility risk:** low if purely additive (new fluent method,
existing `OnX`/`When` behavior untouched).

**Should it wait for a later release?** Optional for `1.1` — real but
milder friction than §8.2 (headers are synchronously inspectable today
via `When`, just without nice diagnostics). Worth including only if §8.2
is being done anyway and the two can share design review; not worth a
release on its own.

## 9. Standalone-package feasibility

**Question:** could `Compono.Http`'s HTTP-testing primitives function
without core Compono?

**Answer: yes, almost trivially — and arguably it already does not
meaningfully depend on Compono's *product* (composition), only on two
small reusable value types.** Concretely:

- `TestHttpHandler`/`HttpResponseRegistration`/
  `HttpResponseRegistrationBuilder`/`UnmatchedHttpRequestException` reference
  **zero** composition concepts (`CompositionBuilder`, `[Composable]`,
  `CompositionRow`, `ICompositionProvider` — grepped, no matches in
  `src/Compono.Http/*.cs`).
- The only two core types touched (`Match<string>`, `CallVerifier`) are
  themselves composition-agnostic value/verification primitives — neither
  requires a composition context to construct or use. (`Match<T>` is used
  today purely as an ergonomic string-matching helper; `CallVerifier` is
  a bare `(int, string)` struct.)
- No source generator involvement at all — nothing to decouple there.

This means `Compono.Http`'s dependency on core `Compono` is best
classified, per the task's own taxonomy, as **"shared infrastructure"** —
not compile-time-fundamental, not runtime-composition, not
generator-integration, not registration/configuration, and not "merely
packaging" either (it's a real, exercised dependency, just a narrow one).

## 10. Architectural options for independence

- **Do nothing (status quo).** `Compono.Http` keeps its
  `ProjectReference` to core `Compono` for `Match<T>`/`CallVerifier`
  reuse. Cost: a consumer who wants *only* `TestHttpHandler` and nothing
  else still pulls in the full `Compono` package as a transitive
  dependency (small assembly, no generator, but still a foreign package
  name in their dependency tree).
- **Duplicate `Match<T>`/`CallVerifier` into `Compono.Http`, drop the
  core reference.** Technically trivial (both types are tiny, no
  dependencies of their own) but creates **exactly the "duplicate APIs or
  confusing modes" outcome the task warns against** — two independent
  `Match<T>` types across packages that happen to look identical is worse
  than one shared type, for a savings of one small transitive package
  reference that costs nothing at runtime (no generator, no reflection,
  no AOT/trim impact — verified: neither type triggers any
  `RequiresDynamicCode`/`RequiresUnreferencedCode` behavior).
- **Extract `Match<T>`/`CallVerifier` into a lower-level shared package.**
  Rejected per the task's own standard: this is the "generic shared
  abstraction with little consumer value" anti-pattern — it would exist
  solely to let `Compono.Http` avoid depending on `Compono`, not because
  any real consumer need drives a new package boundary. `Compono` itself
  isn't a heavy dependency (no generator project reference from
  `Compono.Http`, no runtime composition machinery pulled in — the
  `Compono.dll` a `Compono.Http` consumer gets is small and free of
  reflection).

**Conclusion:** independence is *architecturally trivial* here but has
**no real payoff** — the existing dependency costs a consumer nothing
measurable (no generator tax, no AOT/trim risk, no runtime behavior
change) and already satisfies "does this feel like a coherent product"
(consumers install `Compono.Http`, get `TestHttpHandler`, and never
directly touch `Match<T>`/`CallVerifier` as "core Compono" — they're used
transparently as return/parameter types). **Do not pursue package
independence for `Compono.Http`.** This is the cleanest of the three
packages on this axis precisely because the coupling was already minimal
by design (ADR-0051's own "Minimal dependency graph" driver) — there's
no architectural cost to remove, so removing it would be motion without
value.

## 11. Experiments performed

- **Dependency-graph verification**: read
  `src/Compono.Http/Compono.Http.csproj` directly rather than inferring —
  confirmed exactly one `ProjectReference` (core `Compono`), no
  `PackageReference` entries, `IsAotCompatible=true` (the *only* Compono
  package with this set — verified via the csproj's own comment, cross-checked
  against `src/Compono/Compono.csproj` and `src/Compono.TestDoubles/Compono.TestDoubles.csproj`
  lacking the property).
- **Symbol-usage grep**: `grep -rn "Match<\|CallVerifier\|CompositionBuilder\|\[Composable\]\|CompositionRow"
  src/Compono.Http/*.cs` — confirmed exactly two core-type usages
  (`Match<string>`, `CallVerifier`) and zero composition-concept
  references, supporting §9's conclusion without needing a scratch
  compile (the source itself is small enough — 4 files, ~450 lines total
  — to read exhaustively rather than sample).
- **Async escape-hatch check**: read `TestHttpHandlerTests.cs` in full
  (420 lines) to confirm no existing test exercises request-body
  matching — the gap in §6 is a genuine absence, not an
  undocumented-but-present capability.
- Did not stand up a throwaway `RespondStream` prototype — §8.1's
  ownership analysis is derived directly from `RespondBytes`'s existing
  clone-and-refresh contract (ADR-0051 Amendment 2) plus
  `HttpContent`/`StreamContent`'s documented disposal behavior, which was
  sufficient to reach a confident rejection without writing code.

## 12. External research

Not performed for `Compono.Http` specifically — the original admission
research (`docs/research/0009-...`) already surveyed the .NET HTTP-testing
ecosystem (WireMock.Net's numeric-priority model, considered and rejected
per ADR-0051's "Registration precedence" options) as part of Gate A/B.
This document's candidates (async body matching, header matching,
streaming) are internal ergonomic gaps identified directly from the
existing code and tests, not externally inspired — no new competitive
survey was warranted for a package this narrowly scoped.

## 13. Compatibility implications

All three candidates in §7 are additive:

- **§8.2 (async matching):** new methods (`WhenAsync`, or `WithJsonBody<T>`)
  alongside existing `When`/`OnX` — no existing public signature changes.
  `TestHttpHandler.SendAsync`'s dispatch loop gains an `await` in the
  matching loop only when an async matcher is actually registered;
  behavior for existing sync-only-configured handlers is unchanged.
  Source/binary compatible. No `CallVerifier`/generated-shape changes.
- **§8.3 (header matching):** additive `WithHeader(...)` fluent method;
  read §8.3's caveat about `HttpResponseRegistrationBuilder`'s current
  "matcher fixed at construction" shape — implementing this without a
  breaking internal refactor needs care, but nothing in the *public*
  contract needs to change shape.
- **§8.1 (streams) — not recommended,** so no compatibility analysis
  needed beyond what's captured in the rejection rationale.

None of these touch SemVer-significant surfaces (no generated-source
shape, no analyzer/generator behavior, no package dependency changes).

## 14. AOT/trimming/generator implications

`Compono.Http` is the only Compono package with `IsAotCompatible=true`
today (verified in its own csproj), enforced by
`test/Compono.Http.AotSmokeTest/AnalyzerContract/`. Any `1.1` addition
must preserve this:

- §8.2's async matching introduces no JSON/reflection dependency by
  itself (`ReadAsStringAsync`/`ReadAsByteArrayAsync` are reflection-free);
  a `WithJsonBody<T>` convenience would need the same
  `JsonSerializerOptions?`-vs-`JsonTypeInfo<T>` overload split
  `RespondJson<T>` already uses, carrying the same
  `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` pair on the
  reflection-based overload.
- §8.3's header matching introduces no AOT/trim risk at all (no
  serialization involved).
- No generator involvement in any candidate — `Compono.Http` remains
  generator-free.

## 15. Rejected ideas

- **General-purpose HTTP mocking framework features** (numeric response
  priority, regex path matching, `OnHead`/`OnOptions`/arbitrary-verb
  matching, request/response middleware pipelines) — explicitly out of
  scope per ADR-0051's own bounded intent ("intended responsibility ends"
  question from the task): `Compono.Http` is a testing primitive for code
  already built on `HttpClient`, not a WireMock.Net competitor. Adding
  these would be feature creep against the package's stated
  responsibility, not friction-driven.
- **`RespondStream(Stream)`** — rejected in §8.1: real ownership/lifetime
  ambiguity, no demonstrated repeated friction (contrast `RespondBytes`,
  which had none of these problems and a small, obviously-correct
  clone-based fix).
- **Sequential/scripted responses** ("respond 500 once, then 200") — a
  real mocking-framework feature, but no evidence of consumer need was
  found in tests/docs/dogfooding, and it meaningfully expands
  `TestHttpHandler`'s state model (registrations would need an ordered,
  consumable queue rather than a static last-match-wins list). Flagged as
  "unsupported and intentionally out of scope" per this task's own
  guidance to keep the package from becoming a general mocking framework
  — revisit only with real dogfooding evidence per ADR-0039 Gate B.
- **Configurable strict/loose unmatched-request mode** — ADR-0051 already
  considered and rejected this as Option 3 under "Unmatched-request
  behavior"; nothing in this research surfaces new evidence to revisit
  that decision.

## 16. Ranked recommendations

1. **Async-aware request body/header matching (§8.2)** — highest value,
   addresses a real anti-pattern-forcing gap, purely additive, no
   AOT/trim risk. **Recommended for `1.1`.**
2. **Header-matching convenience (§8.3)** — secondary, ships well
   alongside #1 if the fluent-builder shape work is being done anyway;
   optional on its own. **Recommended for `1.1` only if bundled with #1's
   design work; otherwise defer.**
3. **`RespondStream` (§8.1)** — analyzed, not recommended; needs its own
   ownership-model ADR before it's revisited, and no evidence yet
   justifies that investment. **Defer indefinitely pending real
   dogfooding signal.**

## 17. Recommended 1.1 scope

`Compono.Http`'s contribution to `1.1` should be **§8.2 (async request
matching), possibly bundled with §8.3 (header matching)** — not
`RespondBytes` alone (too small, already the trigger for this research),
and not a broader mocking-framework expansion (out of scope per ADR-0051).
This is a single, coherent, evidence-backed addition rather than a
catalog — consistent with the "don't manufacture features to pad the
release" instruction.

## 18. Questions or evidence still unresolved

- **Real dogfooding evidence for async body matching** — this research
  identifies the *anti-pattern-forcing* gap analytically (sync predicate
  vs. async body read), but per ADR-0039's Gate B standard, a genuine
  admission-quality case would benefit from a real consumer repo showing
  the sync-over-async workaround in practice (analogous to the
  `alexa-vox-craft` evidence that justified `Compono.Http` itself). Not
  found in this repo's own `docs/research/0009-...` (that research
  predates any body-matching need surfacing) — worth a light dogfooding
  pass before finalizing an ADR.
- **Exact async-matcher API shape** — `WhenAsync` vs. `WithJsonBody<T>`
  vs. both — needs the same design-dive treatment ADR-0051 gave path
  matching (`Match<T>` vs. HTTP-native vs. split), not assumed here.
- **Whether async matching changes dispatch-order guarantees** in a way
  that needs its own documented contract (§8.2's note that awaiting
  serializes the match loop) — worth confirming isn't a hidden behavior
  change under load/parallel test execution.
