# [ADR-0051] `Compono.Http`: Handler-Based HTTP Client Testing Package

**Status:** Accepted

**Date:** 2026-08-24

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

Compono's product direction is that Compono should provide a good way to
test code depending on `HttpClient`/`HttpMessageHandler`/`IHttpClientFactory`,
unless research shows the capability makes essentially no sense,
duplicates an existing Compono primitive, conflicts with Compono's
source-generation/AOT architecture, or adds disproportionate complexity
for negligible benefit. This ADR is the outcome of a deep design dive run
against that working assumption, using
`ncipollina/alexa-vox-craft` as the real dogfood evidence source, per
`docs/adr/0039-...`'s Gate A/Gate B package-admission process.

The full research trail — repo inventory, .NET HTTP seam analysis,
ecosystem survey, Compono-primitive composition analysis, and three
rounds of design refinement (Match<T> reuse, verification/response/JSON/
concurrency/request-log semantics, unmatched-request behavior) — lives in
`docs/research/0009-compono-http-admission-research.md`. This ADR
summarizes the decision content from that research; it does not
re-derive the evidence.

**The core finding**: `alexa-vox-craft` already has a ~150-line
hand-rolled "mini `Compono.Http`"
(`test/AlexaVoxCraft.Http.TestKit/Extensions/HttpMessageHandlerExtensions.cs`)
used across **41 real call sites**, that reaches the protected
`HttpMessageHandler.SendAsync` via **reflection** wired through
NSubstitute, deliberately allocates fresh `HttpResponseMessage`/`JsonContent`
per call to avoid a disposed-content bug, and has no reusable matcher DSL,
no first-class request-capture API (a matcher predicate's side effect is
abused to smuggle out captured requests), and two further
independently-written duplicate fake-handler classes elsewhere in the
same repo solving the same problem a third way. No existing Compono
package reaches this: `Compono.TestDoubles` is structurally interface-only
(`HttpMessageHandler`/`HttpClient` are concrete, non-interface types) and
`Compono.DependencyInjection`'s bridge is pull-only, reaching only row
scope, exact `Register<T>` registrations, and stage 4-6 value providers —
not factory-registered types.

## Decision Drivers

- No reflection by default (ADR-0001) — the correct implementation must
  be a plain `HttpMessageHandler` subclass, not a Moq-`.Protected()`-style
  reflection workaround.
- Core `Compono` must never know about integration packages; `Compono.Http`
  must not teach `Compono.TestDoubles` anything about `HttpMessageHandler`.
- Composition-over-inheritance / DI-only — the design must compose
  through `CompositionRow`/`[Shared]`, not a bespoke mechanism.
- Minimal package graph — consumers using only direct `HttpClient`
  construction shouldn't be forced to pull in `Microsoft.Extensions.Http`,
  `Compono.TestDoubles`, or `Compono.DependencyInjection`.
- AOT/trimming compatibility, honestly represented — no reflection
  internally, and any `System.Text.Json`-related AOT constraint that
  *does* exist must be surfaced to consumers via the standard
  `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` analyzer
  machinery, not just documentation.
- Real, repeated `alexa-vox-craft` friction (41 call sites, a reflection
  workaround, a request-capture hack, three independently-duplicated fake
  handlers) as the evidence bar for admission, per ADR-0039 Gate B.
- Naming precedent (`Compono.<Ecosystem>`, e.g. `.Bogus`, `.TestDoubles`,
  `.DependencyInjection`) and the explicit product-direction bias toward
  `Compono.Http` over `Compono.HttpClient`.

## Considered Options

**Package admission:**
1. Admit `Compono.Http` — a runtime (non-generated) `HttpMessageHandler`-based
   testing package, depending only on core `Compono`.
2. Reject — existing `Compono.TestDoubles`/`Compono.DependencyInjection`
   plus ordinary .NET HTTP APIs are sufficient.
3. Admit, but scoped narrowly as `Compono.HttpClient` rather than
   `Compono.Http`.

**Request/whole-request matching primitive** (the seam most likely to
tempt over-engineering):
1. Reuse core `Match<T>` uniformly for every matchable parameter,
   including the whole-request condition (`Match<HttpRequestMessage>`).
2. HTTP-native throughout: plain `string`/`Func<HttpRequestMessage, bool>`
   parameters, no `Match<T>` reuse at all.
3. Split: `Match<string>` for the one genuinely single-scalar parameter
   (`OnX(path)`), plain `Func<HttpRequestMessage, bool>` for the
   whole-request condition (`When(...)`).

**Registration precedence:**
1. Last-registered-first, first match wins ("last-match-wins") — the
   same model `Compono.TestDoubles` established in ADR-0050.
2. First-match-wins (registration order).
3. Explicit numeric priority (WireMock.Net-style).
4. "Most-specific-wins" automatic precedence.

**Unmatched-request behavior:**
1. Strict — throw a dedicated exception; no implicit fallback response.
2. Loose — return a default response (e.g. 404) for anything unconfigured.
3. Configurable strict/loose mode.

**Verification identity:**
1. `handler.Verify(h => h.OnGet(path)).Once()` — an expression re-declaring
   the matcher inside `Verify`.
2. A registration handle returned by `OnX`/`When`, verified directly:
   `registration.Verify().Once()`.

## Decision Outcome

**Chosen, per axis**: package admission — **Option 1** (admit
`Compono.Http`); request matching primitive — **Option 3** (split:
`Match<string>` for the single-scalar path parameter,
`Func<HttpRequestMessage, bool>` for the whole-request predicate);
registration precedence — **Option 1** (last-match-wins, ADR-0050
consistency); unmatched-request behavior — **Option 1** (strict); and
verification identity — **Option 2** (registration handle).

### Package identity and dependency graph

```
Compono.Http
    -> Compono   (reuses Match<T>, CallVerifier — no generator dependency)
```

- **Name: `Compono.Http`**, not `Compono.HttpClient`. HTTP testing is not
  tied to one library version the way `Compono.XunitV3` is tied to one
  test-framework version (ADR-0023's rationale); the v1 boundary already
  extends past `HttpClient` alone (handler-level interception, JSON
  helpers, request capture, verification), so the broader,
  ecosystem-scoped name is correct on the same logic that named
  `Compono.Bogus`/`Compono.TestDoubles`, not merely on product-direction
  preference.
- **No hard dependency on `Compono.TestDoubles`** — nothing in v1 needs
  source-generated interface doubling; `HttpMessageHandler` is not an
  interface and is never taught to that generator.
- **No hard dependency on `Compono.DependencyInjection`** — its current
  pull-only bridge doesn't reach factory-registered types anyway, and
  isn't needed for `[Shared]` composition.
- **No hard dependency on `Microsoft.Extensions.Http`** — proven
  unnecessary even for the real `IHttpClientFactory` call site in
  `alexa-vox-craft` (see "IHttpClientFactory" below).
- **No NSubstitute/Moq dependency** — a fake handler needs no mocking
  framework.

### Core abstraction

A concrete `HttpMessageHandler` subclass (working name `TestHttpHandler`)
— the sanctioned .NET substitution seam (public, unsealed, designed for
exactly this via `protected internal` `SendAsync` override; `HttpClient`
itself is explicitly documented as not meant to be subclassed for this
purpose, and no BCL `IHttpClient` exists for interface-doubling to even
apply). `[Shared]`-composable via ordinary `Register<T>`/profile wiring
— no new core Compono mechanism required; `CompositionRow`'s existing
`ResolveShared<T>` already gives one handler instance both to the test
(for setup/verification) and to the SUT's `HttpClient` construction.

### Request matching

- `OnGet(Match<string> path)`, `OnPost(Match<string> path)`, etc. — HTTP
  method fixed by helper name (100% of real evidence is exact-method,
  exact-path matching); `path` is typed `Match<string>`, reusing core
  `Match<T>` deliberately because a single matchable scalar genuinely
  exists here and all three `Match<T>` states (`Equality`/`Any`/`Is`) are
  meaningful for it, with zero cost to the dominant exact-match call site
  (`Match<T>`'s implicit conversion from a literal keeps `OnGet("/users/42")`
  unchanged).
- `When(Func<HttpRequestMessage, bool> predicate)` — the whole-request
  escape hatch, covering header/query-string/body/multi-dimension
  conditions. Deliberately **not** `Match<HttpRequestMessage>` —
  `Match<T>`'s `Equality`/`Any` states are meaningless for a whole
  request object (there's no sensible default equality, and "any" is
  already `When(_ => true)` for free), so wrapping the predicate would add
  a type name and a `Match.Is<T>(...)` call with zero behavioral gain.
- Matching stays synchronous (`Func<HttpRequestMessage, bool>`, not
  `Task<bool>`) — no `alexa-vox-craft` matcher reads request content
  asynchronously as part of matching.
- No dedicated header/query-string/JSON-body matcher types — the
  predicate escape hatch covers the one real occurrence
  (`FormUrlEncodedContent` type-checking); a full matcher DSL is
  unevidenced.

### Precedence

Ordered, append-only registration list; dispatch walks
**last-registered-first**, first matching entry wins
("last-match-wins") — the same model `Compono.TestDoubles` established
in ADR-0050, reused for product consistency (a Compono user who knows
"later registration wins" from `Compono.TestDoubles` doesn't learn a
second rule for HTTP) since no external evidence (the ecosystem survey
found no single dominant precedence model across MockHttp/WireMock/etc.)
overrides it. "Most-specific-wins" is explicitly rejected as unevidenced
magic.

### Unmatched requests: strict by default

No matching registration → `SendAsync` throws a dedicated
`UnmatchedHttpRequestException` (method, URI, and "nothing matched"
in the message) — never a fabricated response (no implicit 404, no
`default(HttpResponseMessage)`). This mirrors `Compono.TestDoubles`'
own `TestDoubleNotConfiguredException` rule (ADR-0045) and exists for
the same reason ADR-0050's last-match-wins ordering exists: a real
false-positive-test failure mode is exactly what an implicit fallback
would risk. A consumer wanting a fallback configures one explicitly
(`handler.When(_ => true).Respond(...)`), which composes naturally with
last-match-wins. No strict/loose mode switch — unevidenced complexity.
The request is still recorded into `Requests` before matching is
attempted, so an unmatched request remains visible for diagnosis even
though it threw.

### Response state: factory, not instance

A registration's response state is `Func<HttpRequestMessage,
HttpResponseMessage>` — describing *how to build* a response, never a
stored instance. `Respond(statusCode)`/`RespondText(...)`/`RespondJson(...)`
assign a lambda materializing a fresh `HttpResponseMessage` (and fresh
content) on every invocation; `Throws(exception)` assigns a lambda that
throws instead. This is a hard invariant, not an implementation detail —
`alexa-vox-craft`'s own TestKit already had to work around exactly the
bug this invariant prevents (disposed-content reuse across repeated
matches).

`Throws(exception)` **rethrows the same configured `Exception` instance
on every match** — not a per-invocation exception factory. This was
verified, not assumed: a standalone spike (`Func<HttpRequestMessage,
HttpResponseMessage>` that always `throw`s one shared `Exception`
instance, invoked three times) confirms `ReferenceEquals` holds across
every throw, the message and exception type are unaffected, and nothing
crashes or corrupts state — each throw simply refreshes the exception's
own `StackTrace` to point at that throw site, which is ordinary,
expected .NET behavior for rethrowing an instance, not a defect. An
`Exception` carries no disposal semantics comparable to `HttpContent`'s
(the problem `Respond*` guards against), so there is no analogous
freshness requirement here. **No exception factory/callback API, no
cloning machinery, and no exception-type-specific recreation are added
in v1** — reusing the same instance is the whole v1 behavior, and no
concrete correctness issue was found that would justify anything more.
JSON bodies are serialized once, at registration time, to an
immutable `byte[]`; each invocation constructs a fresh `ByteArrayContent`
over that buffer with headers set explicitly per instance — correct and
cheaper than re-serializing per call.

### JSON / AOT

```csharp
RespondJson<T>(T value, JsonSerializerOptions? options = null)
RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)
```

`Compono.Http`'s own dispatch architecture introduces no reflection
anywhere. The `JsonSerializerOptions`-based overload, however, calls
`System.Text.Json`'s `Serialize<TValue>(TValue, JsonSerializerOptions?)`,
which the framework itself marks `[RequiresDynamicCode]` and
`[RequiresUnreferencedCode]` — confirmed against the framework's own API
reference and verified with a build spike (`dotnet build` with
`IsAotCompatible=true`, no full native `PublishAot` compile needed to
observe the analyzer diagnostics) showing that an *unannotated* wrapper
around that call produces **no warning at its own external call sites**,
while an *annotated* wrapper correctly moves the IL2026/IL3050 warnings
to the caller. **`Compono.Http`'s `RespondJson<T>(value, options)`
therefore carries both attributes itself**, so AOT/trim-checked consumers
see the real warning at their own call site rather than relying on
documentation. `RespondJson<T>(value, JsonTypeInfo<T>)` carries neither
attribute, matching the framework's own unannotated `JsonTypeInfo<T>`
overload — the genuinely AOT-safe path, strictly additive, not required
for the API to work. No internal `[UnconditionalSuppressMessage]` is used
anywhere to manufacture a false-clean appearance.

### Verification

`OnX(...)`/`When(...)` return a registration handle
(`HttpResponseRegistration`) rather than `void` — a response registration
already owns its matcher, response behavior, and matched-call count, so
it's a correct, sufficient, non-redeclared verification identity on its
own:

```csharp
var registration = handler.OnGet(expectedUri).RespondJson(response);
// ... act ...
registration.Verify().Once();
```

An earlier `handler.Verify(h => h.OnGet(expectedUri)).Once()` shape (and
an expression-tree-parsing alternative) was rejected — both mix
configuration and observation or add expression-walking complexity for
no evidenced benefit. `registration.Verify().Once()` (not a bare
`registration.Once()`) matches `Compono.TestDoubles`' own established
`double.Verify().Member().Once()` vocabulary exactly, reusing the
unmodified `CallVerifier` type directly — no new verification concept.
Global request inspection (`handler.Requests`) stays a deliberately
separate surface from registration verification — one answers "how many
times was this configured behavior exercised," the other "what did the
SUT actually send, matched or not."

### Request log

`IReadOnlyList<HttpRequestMessage> Requests` — raw `HttpRequestMessage`
references (not a captured-request snapshot type): `HttpRequestMessage.Dispose()`
only disposes `Content`, so `Method`/`RequestUri`/`Headers` remain safe to
read regardless of downstream disposal — which fully covers the real
acceptance case (`LocaleHandlerTests`' `Accept-Language` header
inspection), and matches what `alexa-vox-craft`'s own current
capture-via-predicate-side-effect hack already relies on working. Each
access to `Requests` returns a fresh point-in-time snapshot (never a live
view over a mutable backing collection), since concurrent `SendAsync` is
supported. Recording happens **before** matching is attempted,
unconditionally — so an unmatched request that threw is still visible
afterward.

### Lifecycle, disposal, concurrency

**`TestHttpHandler` is caller-owned — Compono composition never owns or
disposes it.** `CompositionRow`/`CompositionScope`/`CompositionContext`/
`Composer` do not track or dispose resolved `IDisposable` values anywhere
in Compono's existing lifetime model (confirmed: no `IDisposable`/`Dispose`
handling exists in `src/Compono/CompositionRow.cs`,
`CompositionScope.cs`, or `CompositionContext.cs`) — `[Shared]` gives
identity/reuse across one row's graph, it does not create a disposal
scope. Composing `[Shared] TestHttpHandler handler` means Compono
resolves and shares one instance within that row; it does **not** mean
Compono disposes that instance when the row or test ends. The
consumer/test fixture is responsible for disposing the handler itself
(e.g. an explicit `using`/`IDisposable` fixture, or whatever disposal
convention the test framework integration already uses for other
non-Compono-owned resources) — this ADR does not introduce a new general
Compono disposable-scope/lifetime mechanism to change that; automatic
disposal of composed values, if ever wanted, is a separate core-capability
decision outside this ADR's scope.

`HttpClient`s are built via `handler.CreateClient(...)` (not
`ToHttpClient` — the name change reflects that multiple clients may be
created from and share one handler), always with `disposeHandler: false`.
Ownership is therefore layered and entirely caller-side: the caller owns
and disposes each `HttpClient` it creates; the caller (not Compono) also
owns and disposes the handler itself. Disposing an `HttpClient` never
disposes the handler (`disposeHandler: false`); conversely, disposing the
handler invalidates subsequent sends through any `HttpClient` still
wrapping it (`SendAsync` on a disposed handler throws
`ObjectDisposedException`, standard .NET disposal convention) — a
consequence of ordinary handler disposal semantics, not a
Compono-specific behavior. Concurrency
contract is deliberately asymmetric: **configure before execution;
concurrent `SendAsync` is supported; configuration concurrent with sends
is unsupported/not guaranteed** — this lets the registration list stay a
plain `List<T>` (concurrent reads of an unmutated list are safe) while
still requiring real synchronization for what genuinely is written
concurrently (`Interlocked` match counts, a lock/`ConcurrentQueue`-backed
request log).

### `IHttpClientFactory`

No native `Compono.Http` support in v1. `IHttpClientFactory`
(`Microsoft.Extensions.Http`, not a BCL type) is an ordinary
single-method public interface (`HttpClient CreateClient(string name)`),
so it needs no handler-style substitution machinery at all — it's served
either by a 3-line project-local fake (`IHttpClientFactory` implemented
directly, returning a `Compono.Http`-backed `HttpClient`) or, if a test
fixture already has `Compono.TestDoubles` for other doubles, by
`Compono.TestDoubles` itself (since it's a plain interface). Worked
through concretely against the one real `alexa-vox-craft` call site
(`SmapiDeveloperAccessTokenProviderTests`, untyped/unnamed
`factory.CreateClient()`) — the 3-line fake is the recommended dogfood
migration path, since `alexa-vox-craft` has no existing `Compono`
footprint and adding `Compono.TestDoubles` solely for one single-method
interface costs more than writing it by hand. Named/typed-client DI
registration remains reachable today via plain
`Microsoft.Extensions.Http.ConfigurePrimaryHttpMessageHandler` without
any `Compono.Http` involvement.

### Positive Consequences

- Removes a real, repeated (41-call-site) reflection-based workaround
  from a dogfood consumer, replacing it with a zero-reflection,
  AOT/trim-safe implementation.
- Fixes two concrete correctness/ergonomics gaps evidenced in
  `alexa-vox-craft`: the disposed-content bug class (via the
  fresh-response-factory invariant) and the request-capture-via-predicate-
  side-effect hack (via `handler.Requests`).
- Extends zero new core Compono mechanisms — `[Shared]`/`CompositionRow`,
  `Match<T>`, and `CallVerifier` are reused exactly as they exist today.
- Keeps the dependency graph minimal: a consumer wanting only direct
  `HttpClient` testing pulls in nothing beyond `Compono` itself.
- Establishes, with empirical (not just documented) confirmation, a
  pattern for how a Compono package should represent a genuine
  `System.Text.Json` AOT constraint honestly via analyzer attributes
  rather than suppressing or glossing over it — a precedent other future
  packages doing runtime JSON work can follow.

### Negative Consequences

- `Compono.Http` is Compono's first non-generated, hand-written-runtime
  integration package (no source generator involved) — this is a
  deliberate, evidence-based choice (§5/§12 of the research), not an
  oversight, but it does mean this package's implementation and
  maintenance burden looks different from `Compono.TestDoubles`'
  generator-emitted code, and that difference should be called out
  clearly in its own package documentation so it isn't mistaken for an
  inconsistency.
- Strict unmatched-request behavior (throwing rather than a fabricated
  default) is a deliberate ergonomics trade-off — a test that forgets to
  configure a registration for a request the SUT legitimately needs to
  make will fail loudly rather than silently degrading, which is the
  intended behavior but is a small first-use surprise relative to
  libraries that default to a permissive fallback.
- No native `IHttpClientFactory`/named-client support in v1 means
  consumers with heavier `IHttpClientFactory` usage than
  `alexa-vox-craft`'s (e.g. genuine multi-named-client fixtures) get no
  first-class help from `Compono.Http` itself yet — mitigated by the
  3-line fake being genuinely small, but a real gap if a future
  dogfood target needs more.

## Pros and Cons of the Options

### Package admission

**Option 1 (admit `Compono.Http`)**
- Good, because real, repeated, worse-than-any-surveyed-alternative
  friction already exists in the dogfood target.
- Good, because the correct implementation needs zero reflection and is
  confirmed AOT-safe — no architectural conflict.
- Bad, because it's a new package to design, ship, and maintain with no
  generator to lean on.

**Option 2 (reject)**
- Good, because it avoids adding package surface.
- Bad, because it isn't actually true — no existing Compono primitive
  reaches `HttpMessageHandler`, and the current alternative
  (`alexa-vox-craft`'s hand-rolled kit) is objectively worse than every
  surveyed OSS library, which fails the higher evidentiary bar this
  product-direction-mandated capability requires for rejection.

**Option 3 (`Compono.HttpClient`, narrower name)**
- Good, because it would be more conservative if the design were
  genuinely permanently scoped to `HttpClient` alone.
- Bad, because the v1 boundary already includes handler-level
  interception, JSON helpers, capture, and verification — concerns that
  outlive "just `HttpClient`" — so the narrower name would misstate scope
  the way ADR-0023 specifically warns against.

### Request matching primitive

**Option 1 (uniform `Match<T>`, including whole-request)**
- Good, because it would be maximally consistent with `Match<T>`'s use
  elsewhere.
- Bad, because `Match<HttpRequestMessage>` would only ever be constructed
  via `Match.Is<T>(predicate)` in practice — the exact same
  `Func<HttpRequestMessage, bool>` a plain parameter already accepts,
  plus indirection with zero behavioral gain; two of three `Match<T>`
  states are dead weight for this parameter.

**Option 2 (HTTP-native throughout, no `Match<T>` reuse)**
- Good, because it avoids any risk of forcing a mismatched primitive onto
  the whole-request case.
- Bad, because it discards a genuine, concrete win at the `OnX(path)`
  parameter, where a single matchable scalar exists and all three
  `Match<T>` states are meaningful — `Compono.Http` would end up writing
  and maintaining its own duplicate string-matcher type for no reason.

**Option 3 (split — chosen)**
- Good, because it applies `Match<T>` exactly where the brief's own test
  ("does it materially simplify the API, avoid duplicated matcher
  infrastructure, or share one clean parameter shape") is satisfied, and
  nowhere else.
- Bad, because it means the API has two different-looking matching
  mechanisms (`Match<string>` vs. `Func<HttpRequestMessage, bool>`)
  rather than one uniform shape — judged an acceptable, well-documented
  asymmetry given the alternative is worse on both ends it would try to
  unify.

### Registration precedence

**Option 1 (last-match-wins — chosen)**
- Good, because it reuses an established, dogfood-validated Compono
  pattern (ADR-0050), giving one precedence rule across the whole
  product.
- Good, because it directly enables "broad default, then specific
  override" composition.
- Bad, because "last wins" can read as slightly less intuitive than
  "first wins" to a reader unfamiliar with the ADR-0050 precedent, though
  this is mitigated by consistency across the product.

**Option 2 (first-match-wins)**
- Good, because it matches naive intuition ("the first thing I wrote that
  matches is what happens") and MockHttp's backend-definition behavior.
- Bad, because it breaks product consistency with `Compono.TestDoubles`,
  and it makes "register a specific override after a broad default" less
  natural (the override would need to come *before* the default, which
  reads backwards).

**Option 3 (explicit numeric priority)**
- Good, because it's maximally expressive (WireMock.Net's model).
- Bad, because it adds a whole new concept (a priority number) with zero
  `alexa-vox-craft` evidence motivating it, and no other precedent
  anywhere in Compono uses numeric priority for anything.

**Option 4 (most-specific-wins)**
- Good, because it can feel "smart" for simple cases.
- Bad, because it's exactly the kind of matching magic the brief warns
  against without an unusually strong reason, and none was found; it
  also has no clear, unambiguous definition once predicates are involved
  (a `Func<HttpRequestMessage, bool>` has no inherent "specificity").

### Unmatched-request behavior

**Option 1 (strict — chosen)**
- Good, because it makes unexpected outbound calls visible immediately,
  matching `Compono.TestDoubles`' own `TestDoubleNotConfiguredException`
  precedent and preventing the exact false-positive-test failure mode
  that motivated ADR-0050's ordering rule.
- Bad, because a first-time user who forgets a registration for a
  request their SUT legitimately makes gets a hard failure instead of a
  softer degrade — judged the correct trade-off for a testing library.

**Option 2 (loose/fabricated default)**
- Good, because it never fails unexpectedly.
- Bad, because it's exactly the false-positive-test risk this product
  has already been burned by once (the motivation behind ADR-0050); a
  wrong-request bug in the SUT could pass silently.

**Option 3 (configurable mode)**
- Good, because it gives maximum flexibility.
- Bad, because there's zero dogfood evidence either mode needs to be
  switchable — unevidenced complexity, rejected on the same grounds as
  every other unevidenced knob in this design.

### Verification identity

**Option 1 (`Verify(expression)`, re-declared matcher)**
- Good, because it superficially reads close to how one might describe
  the intent in prose ("verify that a GET to this path happened").
- Bad, because it mixes configuration and observation (`OnGet` is a
  configuration operation) and either silently re-registers a redundant
  entry or requires a parallel non-mutating lookup path duplicating the
  matcher logic.

**Option 2 (registration handle — chosen)**
- Good, because the registration already owns matcher + response +
  matched-call count, making it a correct, sufficient, non-redeclared
  verification identity, and it matches `Compono.TestDoubles`' own
  `.Verify().Member().Once()` vocabulary via `registration.Verify().Once()`.
- Bad, because it requires the consumer to capture and hold a local
  variable (`var registration = ...`) rather than write a single
  self-contained expression — judged a minor, acceptable ergonomic cost
  for the correctness/simplicity gained.

## Links

- `docs/research/0009-compono-http-admission-research.md` — the full
  evidence trail this ADR summarizes.
- `docs/adr/0001-source-generation-first.md` — no-reflection-by-default
  constraint.
- `docs/adr/0023-...` — `Compono.XunitV3` naming precedent informing
  `Compono.Http` vs. `Compono.HttpClient`.
- `docs/adr/0039-...` — Gate A/Gate B package-admission process this ADR
  satisfies.
- `docs/adr/0045-...`, `docs/adr/0047-...`, `docs/adr/0048-...`,
  `docs/adr/0050-...` — `TestDoubleNotConfiguredException`,
  `Compono.DependencyInjection`'s pull-only bridge, `Match<T>`/`CallVerifier`,
  and last-match-wins precedent, each reused or deliberately not extended
  by this ADR.
- `alexa-vox-craft` repo (`test/AlexaVoxCraft.Http.TestKit`,
  `SmapiDeveloperAccessTokenProviderTests.cs`, `LocaleHandlerTests.cs`) —
  the dogfood evidence source.
