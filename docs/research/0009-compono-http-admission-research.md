# [RESEARCH-0009] `Compono.Http` Package Admission Research

**Status:** Done (research complete; no ADR written yet — this document is
the pre-ADR evidence base, per `design-decisions.md`'s rule that a design
dive's brainstorm/research phase precedes drafting)

**Feeds:** [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)
(`Status: Proposed`), proposing `Compono.Http` per `docs/adr/0039-...`
Gate A/Gate B admission process. This document supplies the Gate A
architectural-legitimacy evidence and a first pass at Gate B (real
dogfood friction in `alexa-vox-craft`).

**Dogfood target:** `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft`

**Product direction (given, not re-litigated here):** the working
assumption is that Compono *should* ship an HTTP-testing capability unless
research shows one of: no real value beyond existing primitives,
duplication of an existing primitive, an AOT/reflection conflict, or
disproportionate complexity for negligible benefit (outcomes B/C/D below).
A real, coherent HTTP-testing boundary counts as sufficient product
justification even though `alexa-vox-craft` could keep using hand-written
infrastructure indefinitely.

---

## 1. `alexa-vox-craft` HTTP testing inventory

`alexa-vox-craft` has **zero existing Compono footprint** (`rg -n
"Compono" --type cs -l` across the whole repo: no matches) — it is 100%
NSubstitute + AutoFixture.AutoNSubstitute + a hand-built xUnit v3
`AutoDataAttribute`/`BaseFixtureFactory` composition root
(`test/AlexaVoxCraft.TestKit/Attributes/BaseFixtureFactory.cs`). A
`Compono.Http` package would be introduced into a codebase with no prior
Compono adoption to build on — it has to earn its place on its own
technical merits, not ride in on an existing Compono investment.

### Production HTTP infrastructure (`src/`)

| # | What | Where |
|---|---|---|
| 1 | `BearerTokenHandler : DelegatingHandler` — stamps `Authorization: Bearer` | `src/AlexaVoxCraft.Http/BearerTokenHandler.cs` |
| 2 | `LocaleHandler : DelegatingHandler` — stamps `Accept-Language` | `src/AlexaVoxCraft.InSkillPurchasing/LocaleHandler.cs` |
| 3 | `BaseClient` abstract base — manual `JsonSerializer.Serialize`/`Deserialize`, `EnsureSuccessStatusCode`, catches `HttpRequestException`, swallows 404→null | `src/AlexaVoxCraft.Http/Clients/BaseClient.cs`, subclassed 3x |
| 4 | `IHttpClientBuilder` extensions (`AddAuthorizationForwarding()`, `AddLocale()`) — `TryAddTransient` + `AddHttpMessageHandler<T>()` | `HttpClientBuilderExtensions.cs` (2 projects) |
| 5 | `AddHttpClient<TInterface, TImpl>()` typed-client registration, 3 call sites, each with a `configureHttpClientBuilder` escape hatch | `Smapi/ServiceCollectionExtensions.cs`, `InSkillPurchasing/ServiceCollectionExtensions.cs` |
| 6 | `IHttpClientFactory.CreateClient()` used untyped/unnamed for a token endpoint, with hand-rolled expiry caching | `Smapi/Auth/SmapiDeveloperAccessTokenProvider.cs` |

Retry/Polly: **not present anywhere** — 4 XML-doc comments *mention*
"resiliency policies" as a hypothetical future extension point, but no
retry/circuit-breaker code exists. This rules out "policy/retry testing"
as evidenced v1 scope.

### The central artifact: `test/AlexaVoxCraft.Http.TestKit`

A repo-owned, hand-built **~150-line "mini Compono.Http"**, shared across
`Smapi.Tests` and `InSkillPurchasing.Tests`:

- `Extensions/HttpMessageHandlerExtensions.cs` — `ReturnsResponse(statusCode, body, predicate)`. Uses **reflection** (`BindingFlags.NonPublic`) to reach the protected `SendAsync` on an NSubstitute-mocked `HttpMessageHandler`, wires an NSubstitute `Arg.Is` predicate, and deliberately allocates a **fresh** `HttpResponseMessage`/`JsonContent` per call — the code comment says this is "to avoid disposed content issues," a known NSubstitute/HttpClient gotcha.
- `RequestSpecifications/HttpClientSpecification.cs` + `SpecimenBuilders/HttpClientSpecimenBuilder.cs` — AutoFixture wiring that freezes a mocked `HttpMessageHandler` and hands out `new HttpClient(handler) { BaseAddress = ... }`.
- `Attributes/ClientAutoDataAttribute.cs` (+ 2 per-project subclasses) — `AutoDataAttribute` subclass that does `fixture.Freeze<HttpMessageHandler>()` plus registers the specimen builder.

**Usage: 41 `ReturnsResponse(...)` call sites, 36 `[Frozen]
HttpMessageHandler` parameter injections**, across
`AlexaInteractionModelClientTests.cs`, `AlexaSkillInvocationClientTests.cs`,
`SmapiDeveloperAccessTokenProviderTests.cs`,
`InSkillPurchasingClientTests.cs`, `LocaleHandlerTests.cs`. Representative
call site:

```csharp
[Theory, SmapiClientAutoData]
public async Task GetAsync_WithValidUri_CallsCorrectEndpoint(
    [Frozen] HttpMessageHandler handler, AlexaInteractionModelClient client, ...)
{
    var expectedUri = $"/v1/skills/{skillId}/stages/{stage}/interactionModel/locales/{locale}";
    handler.ReturnsResponse(HttpStatusCode.OK, responseModel,
        req => req.RequestUri?.PathAndQuery == expectedUri);
    ...
}
```

Matching is **always** a hand-written predicate lambda over
`req.RequestUri.PathAndQuery`/`.AbsolutePath`/`.ToString()`,
`req.Method == HttpMethod.X`, or `req.Content is FormUrlEncodedContent` —
no reusable matcher DSL exists; every test writes its own. Verification is
raw NSubstitute `handler.Received()` / `.ReceivedCalls().Should().HaveCount(n)`.

### Request capture without a real API

`InSkillPurchasing.Tests/Handlers/LocaleHandlerTests.cs` substitutes the
*inner* handler of a `DelegatingHandler` under test and "captures" the
request purely as a **side effect of the matcher predicate**:

```csharp
HttpRequestMessage? capturedRequest = null;
innerHandler.ReturnsResponse(HttpStatusCode.OK, predicate: req => { capturedRequest = req; return true; });
```

This idiom repeats twice in that file — there is no first-class "capture
what was sent" API, so a matcher closure is abused to smuggle the request
out.

### Duplicated legacy fake handlers

Two independently-written `HttpMessageHandler` subclasses solve the same
problem a third, different way, in code that predates the TestKit and does
**not** use it:

- `test/AlexaVoxCraft.Model.Apl.Legacy.Tests/ActionHandler.cs` (75 lines, 5 constructor overloads) — 4 call sites.
- `test/AlexaVoxCraft.Model.Legacy.Tests/Responses/ProgressiveResponseTests.cs:125` (`ActionMessageHandler`) — 2 call sites.

Verification in both is inline `Assert.Equal` on method/URI/body inside the
handler callback — no matcher/predicate abstraction at all, not even
NSubstitute.

### Coverage gap as friction evidence

`BearerTokenHandler` (the sibling of the tested `LocaleHandler`) has **no
dedicated test anywhere in the repo** — testing a `DelegatingHandler` in
isolation (wire a fake `InnerHandler`, assert header mutation) apparently
has enough ceremony that it was skipped for one of the two handlers that
need it.

### Classification summary

| Pattern | Repeats | Bucket |
|---|---|---|
| Typed-client DI (`AddHttpClient<T,T>`, builder extensions) | 3 registrations | **6** — DependencyInjection concern, not HTTP-testing |
| `BaseClient` JSON send/receive base | 3 subclasses | **2** — project-local (Alexa/SMAPI JSON conventions), not a Compono concern |
| Shared TestKit (`ReturnsResponse`, specimen builder, `AutoData` subclass) | 41 call sites / 36 params | **3 / 7** — general primitive missing; strongest candidate capability |
| Ad-hoc method/path/query/header/body predicates | ~30+ distinct lambdas | **3** — no matcher DSL, but evidence favors a *thin* predicate escape hatch over a full DSL (see §9) |
| Request-capture-via-predicate-side-effect | 2 | **3** — no first-class capture API |
| Legacy duplicate fake handlers | 6 call sites, 2 files | **2** (frozen legacy code) with **3** as supporting evidence — even inside one repo, "fake handler that returns X and lets me assert on the request" gets reinvented three separate ways |
| `DelegatingHandler` isolation testing | 1 of 2 handlers tested | **3** — gap suggests missing helper suppresses otherwise-warranted tests |
| Retry/Polly | 0 real usage | n/a — not in evidenced scope |
| Compono anywhere in the repo | 0 | n/a |

---

## 2. Current .NET HTTP seam and lifetime analysis

Corrects one assumption in the original research brief: **there is no
public `SendAsync` overload on `HttpMessageHandler` in any current .NET
version.** `HttpMessageHandler.SendAsync` is, and always has been,
`protected internal abstract`; .NET 5 added a *synchronous* `Send`
counterpart, but it is `protected internal virtual`, not public. The
actual public entry point sits one level up, on `HttpMessageInvoker`
(`HttpClient`'s base class): `public Task<HttpResponseMessage>
SendAsync(...)`. This is exactly why Moq's workaround
(`.Protected().Setup<...>("SendAsync", ...)`, a string-keyed reflection
call) exists — there is no public seam at the handler level, only at the
invoker/client level.

Key facts, each independently confirmed against Microsoft Learn docs and
`dotnet/runtime` source:

- **`HttpClient` is not sealed**, derives from `HttpMessageInvoker`, and has a `HttpClient(HttpMessageHandler handler, bool disposeHandler)` constructor. The single-arg `HttpClient(handler)` overload defaults `disposeHandler: true`. Docs explicitly instruct against subclassing `HttpClient` to inject behavior — "use a constructor overload that accepts `HttpMessageHandler`" instead.
- **Disposal**: an `HttpClient` disposes its handler only if `disposeHandler` was `true` at construction. `IHttpClientFactory`-issued clients pass `disposeHandler: false` internally so disposing a factory client doesn't tear down the pooled handler; the factory disposes the handler itself once its `HandlerLifetime` (default 2 minutes) expires and no live client references it.
- **`IHttpClientFactory` test seam**: `services.AddHttpClient(...).ConfigurePrimaryHttpMessageHandler(() => fakeHandler)` is the documented, first-class extension point for named/typed clients — no reflection, no handler-cache introspection required. `AddHttpMessageHandler` layers an additional `DelegatingHandler` (e.g. a request recorder) into the pipeline the same way.
- **Sanctioned substitution seam is the handler, not the client**: both `HttpMessageHandler` and `DelegatingHandler` are public, unsealed, designed-for-extension types (`DelegatingHandler`'s own doc remarks call it "a class used to plug a handler into a handler chain"). Subclassing either and overriding the `protected internal` `SendAsync` needs no special assembly permission — `protected` grants override rights to derived types in any assembly. This is the same seam every serious approach (including Moq's reflection workaround) ultimately reduces to; a hand-written subclass reaches it with zero reflection.
- **Why `HttpClient` can't be interface-doubled**: no BCL `IHttpClient` exists; much of `HttpClient`'s real behavior (`BaseAddress` resolution, `DefaultRequestHeaders`, and critically the `System.Net.Http.Json` convenience methods like `GetFromJsonAsync`) is implemented as **extension methods**, which cannot be intercepted through an interface at all. An interface double over `HttpClient` would either lose that behavior or have to reimplement it — neither is sound. The correct target for a fake is the handler; production code keeps using the real, concrete `HttpClient` wired to a fake handler, so `BaseAddress`/header/JSON-extension behavior all still runs for real.
- **Concurrency contract is explicit and sourced**: `HttpMessageHandler`'s own doc remarks state that a derived class overriding `SendAsync` "must make sure that `SendAsync` can get called concurrently by different threads" — this is not a style preference, it's the documented contract `HttpClient` relies on. A fake handler that records requests or does response lookups under concurrent `SendAsync` calls (e.g. parallel xUnit v3 test execution sharing one `HttpClient`) **must** synchronize that state (lock, or `ConcurrentQueue`/`ConcurrentDictionary`) — an unsynchronized `List<T>.Add` is a real data race, not an edge case.
- **AOT/trimming**: a hand-rolled fake `HttpMessageHandler` + explicit `new HttpClient(fakeHandler, disposeHandler: false)` construction uses only ordinary managed-code constructs (subclassing, abstract-method override, collections) — no reflection, `Activator`, `MakeGenericType`, or dynamic proxying. This path is unambiguously AOT/trim-safe. The DI-registered path through `Microsoft.Extensions.Http`/`IServiceCollection`/Options pattern carries more (unconfirmed, but plausible) trim-annotation risk inherited from the wider DI/Options subsystem — not something specific to HTTP handling, but a reason to keep the core package's AOT story independent of that path (see §13).

---

## 3. Survey of existing HTTP mocking/testing approaches

| Library | Core abstraction | Matching precedence | HttpClient story | `IHttpClientFactory` support | JSON dep | Internals |
|---|---|---|---|---|---|---|
| **RichardSzalay.MockHttp** | `MockHttpMessageHandler` (handler subclass) | `Expect()` = ordered queue, consumed on match; then `When()` backend defs = first-match-wins; then `Fallback` | Handler you wrap (`.ToHttpClient()` convenience) | None built-in ([issue #89](https://github.com/richardszalay/mockhttp/issues/89), the library's most reported gap) | None bundled | Plain subclass, no reflection |
| **TUnit.Mocks.Http** | `MockHttpHandler`/`MockHttpClient` (source-generated) | Not fully public yet (very new package, mid-2026) | Hands you a real `HttpClient` directly | Declares `Microsoft.Extensions.Http` dep; details unconfirmed | `RespondWithJson()` built in | Source-generated, explicitly no runtime proxy — designed for AOT/trim, same philosophy as Compono |
| **WireMock.Net** | `WireMockServer` (real embedded HTTP server, binds a real port) | Explicit numeric **priority** field (routing-table style, not match-order) | `server.CreateClient()` | None native; requires pairing with **Moq** to fake the factory | Newtonsoft.Json + Handlebars templating | Real listener process — heaviest footprint, real I/O in a unit test |
| **Hand-rolled Moq `.Protected()`** | `Mock<HttpMessageHandler>` | Whatever Moq's setup matching does | You wire `new HttpClient(handler)` yourself | None | None | **String-keyed reflection** over the protected `SendAsync` — the canonical anti-pattern for a no-reflection library; confirmed by real GitHub friction ([moq4#1315](https://github.com/moq/moq4/issues/1315)) |
| **Hand-rolled NSubstitute** | Can't mock protected members at all ([nsubstitute#800](https://github.com/nsubstitute/NSubstitute/issues/800)) — forces a hand-written subclass, which is exactly what `alexa-vox-craft`'s TestKit works around via reflection instead | — | — | — | — | — |
| **skwasjer/MockHttp** | `MockHttpHandler` | Fluent `.When()`, Moq-styled `.Verifiable()`/`.Verify()` | `new HttpClient(mockHttp)` | Unconfirmed | Opt-in via separate `.Json` package (core has none) | Unconfirmed |

Takeaways relevant to design, not to be copied mechanically:

- Every serious library converges on **"substitute the handler, not the
  client"** — independent confirmation of §2's conclusion.
- `IHttpClientFactory` integration is the single most consistently
  reported gap across the ecosystem (MockHttp's most-starred issue;
  WireMock.Net needs a second mocking library just to fake the factory).
  This is real evidence that a v1 without native factory support is not
  an unusual or incomplete-feeling omission — it's the ecosystem norm, not
  a corner Compono would be uniquely cutting.
- Precedence models vary (queue-then-first-match, pure first-match,
  explicit numeric priority) — there is no single "obviously correct"
  answer from the ecosystem to defer to; nothing here overrides Compono's
  own established precedent (§4).
- TUnit.Mocks.Http is the only surveyed library sharing Compono's
  source-generation-over-reflection philosophy, but it's too new/thin on
  public documentation to treat as a design template.
- `alexa-vox-craft`'s own hand-rolled `ReturnsResponse` sits at the worst
  point in this space: it pays MockHttp's problem (reflection into a
  protected member) *and* gets none of MockHttp's ergonomics (no matcher
  DSL, no capture API, no `IHttpClientFactory` story) — it's strictly
  worse than adopting an existing OSS library, let alone a Compono-native
  one.

---

## 4. How existing Compono primitives compose with the HTTP domain

*(Full detail from the primitives-inventory sub-agent; summarized here.)*

- **`CompositionRow`/`[Shared]`** is exactly the mechanism needed to give a
  test both "the thing it configures/verifies" and "the thing injected
  into the SUT" as one stable identity: `ResolveShared<T>` composes once
  per row and reuses the same instance wherever else in that row's graph
  the same type is requested. An HTTP test abstraction declared `[Shared]`
  gets this for free, unmodified — no new core mechanism required. Caveat:
  sharing is **type-keyed only**, no name/qualifier-based sharing exists,
  so two distinct named-client identities can't be `[Shared]`d apart by
  name within one row today, only by distinct types.
- **`Compono.DependencyInjection`** is not a general MS.DI bridge — its
  entire public surface is `CompositionRow.AsServiceProvider() :
  IServiceProvider`, pull-only, reaching only row scope + exact
  `Register<T>` registrations + stage 4-6 `ICompositionValueProvider`s. It
  explicitly cannot push a Compono-composed value forward into an
  `IServiceCollection`, and richer DI integration was evaluated and
  rejected in ADR-0039/`future-packages.md` for not clearing Gate A yet.
  **Conclusion: DI-registered `IHttpClientFactory`/named-client bridging is
  not something Compono.DependencyInjection already solves — if wanted, it
  would need its own justification, separately from `Compono.Http`'s core
  value.**
- **`Compono.TestDoubles`' ADR-0050 matching model** (ordered
  append-only entries, dispatch walks **last-registered-first**, first
  matching entry wins — i.e. **last-match-wins**) is conceptually reusable
  but **mechanically is not**: it's generator-emitted per-interface-member
  code, not a runtime type. What *is* directly reusable, unchanged, no
  generator involved: `Match<T>`, `ReturnConfig<T>`/`ReturnConfigBuilder<T>`,
  `CallVerifier` — all public, generic, runtime types in `src/Compono`.
  `Compono.Http` can build its own hand-written ordered-list/last-match-wins
  dispatch on top of these building blocks, but has to write that dispatch
  loop itself — there's no off-the-shelf `OrderedMatchList<T>` to import.
- **No special-casing precedent or temptation**: nothing in
  `Compono.TestDoubles` references any concrete BCL type; its entire
  design is interface-leaf-specific by construction (explicit-interface-
  implementation trick, substitutability predicate that explicitly
  excludes sealed/concrete classes). This confirms the product instinct in
  the brief — `HttpMessageHandler` should never be taught to TestDoubles.
- **Package-naming precedent** (`Compono.XunitV3`, `.NSubstitute`, `.Bogus`,
  `.DependencyInjection`, `.TestDoubles`, `.TUnit`) is `Compono.<Ecosystem>`,
  version-qualified only when the integration is genuinely version-specific
  (ADR-0023's rationale for `XunitV3`). HTTP testing is not tied to one
  library version the way a test-framework integration is — **this
  precedent favors `Compono.Http` over `Compono.HttpClient`**, matching
  the product direction's naming bias, unless the design were deliberately
  and permanently scoped to `HttpClient` alone (it isn't — see §7).
- `docs/mvp.md` and `docs/roadmap/future-packages.md` **do not mention
  HTTP anywhere** — this is a genuinely new candidate, not a previously
  rejected or deferred one, and it currently sits at neither Gate A nor
  Gate B of ADR-0039's process.

---

## 5. Is a new core capability required?

**No.** Every extension point a `Compono.Http` v1 needs already exists:

- Identity/sharing across test setup and SUT injection: `[Shared]` +
  `CompositionRow` (unmodified).
- Registration into a fixture: ordinary `Register<T>`/profile extension
  methods (`UseHttp(...)`), same pattern as every other integration.
- Matching/verification building blocks: `Match<T>`, `CallVerifier`
  (unmodified, reused directly, no generator dependency).
- Extension surface for future DI/factory integration, if ever wanted:
  ordinary `IHttpClientBuilder`/`IServiceCollection` extension methods —
  standard `Microsoft.Extensions.Http` seam, not a Compono core concept at
  all.

The one piece of infrastructure `Compono.Http` needs that doesn't exist
anywhere in Compono today — an ordered, last-match-wins request/response
registration list — is **package-local, hand-written runtime code**, not
a missing core primitive. It's the same category of thing
`Compono.TestDoubles`' generator emits per interface member, reimplemented
by hand for one concrete domain (HTTP), which is exactly what a "genuine,
non-generated integration package" is supposed to look like per the
design principles.

---

## 6. Recommended `Compono.Http` package boundary

- **Owns**: a fake `HttpMessageHandler` (the sanctioned .NET seam),
  request matching against it, response configuration, a thread-safe
  request log, count-based verification, and convenience `HttpClient`
  construction over that handler.
- **Does not own**: DI/`IServiceCollection` registration of typed/named
  clients (that's ordinary `Microsoft.Extensions.Http`, already solved by
  `.ConfigurePrimaryHttpMessageHandler` — no Compono concept needed to use
  it), interface-level API double-ing (that's `Compono.TestDoubles`'s
  domain and doesn't apply here since `HttpClient` isn't an interface),
  and general request-list/DI-container bridging (that's
  `Compono.DependencyInjection`'s domain, and its current pull-only shape
  doesn't need to change for `Compono.Http` to work).
- **Boundary test that holds up**: a custom `IApiClient` interface
  wrapping HTTP calls is *already* perfectly served by `Compono.TestDoubles`
  today (interface, source-generated double, `Match<T>`/`CallVerifier`) —
  `Compono.Http` is for tests that deliberately want to exercise the real
  `HttpClient`/serialization/request pipeline instead of substituting the
  whole API abstraction away. `alexa-vox-craft`'s evidence is squarely in
  that second category: `BaseClient`, `AlexaInteractionModelClient`, etc.
  are concrete classes built directly on `HttpClient`, not behind a
  substitutable interface — that's precisely why the repo had to hand-roll
  handler-level fakes instead of just using NSubstitute normally.

---

## 7. Smallest v1 feature set

Driven strictly by `alexa-vox-craft` evidence (§1), not by "what other
libraries have":

- A concrete `HttpMessageHandler` subclass (name TBD in the ADR, working
  name `TestHttpHandler`) that:
  - accepts ordered response registrations built two ways: HTTP-specific
    helpers (`OnGet(path)`, `OnPost(path)`, etc. — method is fixed by the
    helper name, exact-match on the real evidence in §1) and a general
    `When(Func<HttpRequestMessage, bool> predicate)` escape hatch for
    anything spanning multiple request dimensions at once (method + URI +
    headers + content type) — see §9 for why this is a `Func`, not a
    `Match<HttpRequestMessage>`;
  - `path` in the `OnX(path)` helpers is typed `Match<string>`, not
    `string` — see §9 for why this one parameter is the one place v1
    reuses core `Match<T>` deliberately;
  - header/query-string/body matching (not evidenced as a *repeated*
    pattern, but present at least once via `FormUrlEncodedContent`
    type-checking) is reachable through `When(...)` without a dedicated
    matcher for each — matches §9's decision not to build a full matcher
    DSL for dimensions with only one or two real occurrences;
  - responds with status code + optional JSON body (covers the dominant
    real case — `JsonContent.Create(body)` in every TestKit call) and a
    plain string/no-body variant;
  - supports `Throws(exception)` as a response (real evidence:
    `BaseClient` explicitly catches `HttpRequestException` and swallows
    404 — a consumer needs to simulate a thrown `HttpRequestException`
    to exercise that path, not just a 404 status code);
  - records every request received in a thread-safe log (§2's concurrency
    contract), fixing the request-capture-via-predicate-side-effect hack
    (§1) with a real `IReadOnlyList<HttpRequestMessage> Requests` property;
  - is `IDisposable`, but disposing it does **not** assume it owns any
    `HttpClient` built over it (see §11).
- **Each `OnX(...)`/`When(...)` call returns a registration handle**
  (working name `HttpResponseRegistration`), not `void` — this is a
  deliberate design decision, not just a return-type detail; see §10 for
  why (it's what makes verification an observation of a specific
  configured behavior instead of a second predicate declaration).
- **Every response is a per-invocation factory, never a stored
  instance** — `Respond*`/`Throws` configure *how to build* a response,
  not *which instance* to hand back. This is a hard invariant, not an
  implementation detail — see §11 for why and what it rules out.
- A convenience `CreateClient(baseAddress)` (renamed from an earlier
  `ToHttpClient` working name — `CreateClient` reads correctly when
  called more than once against the same handler, which `ToHttpClient`
  doesn't) producing `new HttpClient(this, disposeHandler: false)` —
  matches `alexa-vox-craft`'s existing `HttpClientSpecimenBuilder` shape
  (`new HttpClient(handler) { BaseAddress = ... }`) almost exactly.
- Count-based verification, reusing `CallVerifier` directly (no new
  verification concept), called on the registration handle itself:
  `registration.Verify().Once()` — see §10.
- `[Shared]`-composable via ordinary `Register<T>`/profile wiring — no
  bespoke composition mechanism.

Deliberately excluded from v1 (see §14).

---

## 8. Proposed consumer API (illustrative, not final — ADR territory)

Modeled directly on real call sites, not invented shapes. Revised from the
previous draft per a second design pass: registrations are handles (§10),
responses are factories not instances (§11), `CreateClient` replaces
`ToHttpClient` (§11), and the `IHttpClientFactory` story is proven
concretely (§8.2), not asserted.

### 8.1 Core surface

```csharp
// --- exact GET, the dominant real case (41/41 alexa-vox-craft call sites
// are exact-path checks) ---
handler.OnGet(expectedUri)
       .RespondJson(responseModel);

// --- predicate-path GET, using Match.Is<string> — not evidenced today,
// but the one place v1 deliberately reuses core Match<T> (§9) ---
handler.OnGet(Match.Is<string>(p => p.StartsWith("/users/")))
       .RespondJson(user);

// --- any-path GET, same Match<T> reuse ---
handler.OnGet(Match.Any<string>())
       .RespondJson(defaultUser);

// --- whole-request predicate escape hatch (covers FormUrlEncodedContent-
// style checks, and anything spanning method + URI + headers + content
// type at once) — a plain Func, not Match<HttpRequestMessage> (§9) ---
handler.When(req => req.Method == HttpMethod.Post && req.Content is FormUrlEncodedContent)
       .Respond(HttpStatusCode.OK);

// --- status-only response (no body) ---
handler.OnDelete(expectedUri)
       .Respond(HttpStatusCode.NoContent);

// --- text response ---
handler.OnGet("/health")
       .RespondText("OK", mediaType: "text/plain");

// --- JSON response, default (ordinary System.Text.Json reflection-based
// resolver — AOT-safety follows whatever JsonSerializerOptions/JsonTypeInfo
// the caller supplies, see §12) ---
handler.OnGet(expectedUri)
       .RespondJson(responseModel);

// --- JSON response, explicit options (e.g. a source-generated resolver
// the caller already owns) ---
handler.OnGet(expectedUri)
       .RespondJson(responseModel, MyJsonContext.Default.Options);

// --- JSON response, guaranteed-AOT-safe overload using JsonTypeInfo<T>
// directly — no JsonSerializerOptions indirection at all ---
handler.OnGet(expectedUri)
       .RespondJson(responseModel, MyJsonContext.Default.ResponseModel);

// --- thrown exception (BaseClient's HttpRequestException/404-swallow path) ---
handler.OnGet(expectedUri)
       .Throws(new HttpRequestException("simulated failure", null, HttpStatusCode.NotFound));

// --- registration verification: the registration handle IS the
// verification identity — no re-declared matcher (§10) ---
var registration = handler.OnGet(expectedUri).RespondJson(responseModel);
// ... act ...
registration.Verify().Once();

// --- global request inspection/capture, replacing the predicate-side-
// effect hack — kept deliberately separate from registration verification
// (§10), raw HttpRequestMessage references, snapshot-per-access (§11.3) ---
handler.Requests.Should().ContainSingle(r =>
    r.Headers.AcceptLanguage.ToString() == "en-US");

// --- unmatched request: strict by default (§9) — no registration
// configured for this request means SendAsync throws, not a fabricated
// response. The request still shows up in handler.Requests afterward. ---
await Assert.ThrowsAsync<UnmatchedHttpRequestException>(
    () => client.GetAsync("/unconfigured/path"));

// --- creating and disposing HttpClient: handler owns matching/log,
// HttpClient ownership is the caller's (disposeHandler: false), multiple
// clients may share one handler (§11.1) ---
using var client = handler.CreateClient(baseAddress: new Uri("https://api.amazonalexa.com/"));
// or, equivalently, without the convenience overload:
using var client2 = handler.CreateClient();
client2.BaseAddress = new Uri("https://api.amazonalexa.com/");
```

`[Shared] HttpMessageHandler handler, MyClient client` composed in one row
— the same handler instance both the test configures and the SUT's
`HttpClient` was built over — replaces `[Frozen] HttpMessageHandler` +
`ClientAutoDataAttribute` entirely.

### 8.2 The real `IHttpClientFactory` dogfood composition

`SmapiDeveloperAccessTokenProviderTests` is the one real `alexa-vox-craft`
call site depending on `IHttpClientFactory` (production code calls
`factory.CreateClient()`, untyped/unnamed — no name argument, no typed
client, §1). This was worked through concretely rather than deferred on
assumption, because the brief specifically asked not to declare native
factory support unnecessary without proving the alternative.

**Terminology correction from the first draft**: `IHttpClientFactory` is
**not** a BCL type — it's part of the `Microsoft.Extensions.Http`
ecosystem package (`Microsoft.Extensions.Http.dll`), not
`System.Net.Http`/the BCL proper. The architectural conclusion is
unaffected by this correction: it's still **an ordinary public
interface** — `HttpClient CreateClient(string name)`, one method, no
`HttpMessageHandler`-style protected-member problem at all. That means it
needs **no `Compono.Http`-specific support whatsoever** — it's either:

**Option 1 — a 3-line project-local fake** (no new package dependency):

```csharp
private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
```

```csharp
var handler = new TestHttpHandler();
handler.OnPost("/auth/O2/token").RespondJson(tokenResponse);
using var httpClient = handler.CreateClient(baseAddress: lwaTokenEndpoint);
var factory = new FakeHttpClientFactory(httpClient);
var provider = new SmapiDeveloperAccessTokenProvider(factory, ...);
```

**Option 2 — `Compono.TestDoubles`, if the test already has it for other
doubles in the same fixture** (since `IHttpClientFactory` is a plain
interface, it's already a perfect `Compono.TestDoubles` target — no
special-casing needed, confirming §4/§6's boundary claim concretely
rather than just architecturally):

```csharp
factory.Configure().CreateClient(Match.Any<string>()).Returns(httpClient);
```

**Cost called out explicitly, per the brief:** `alexa-vox-craft` has zero
`Compono` footprint today (§1). Option 1 costs nothing beyond
`Compono.Http` itself — no second package. Option 2 requires adding
`Compono.TestDoubles` as well, which is a real, non-trivial cost for a
single-method interface fake if `Compono.TestDoubles` isn't already
in the picture for other reasons. **Recommendation: the dogfood migration
of `SmapiDeveloperAccessTokenProviderTests` uses Option 1** — it's smaller
than the cost of adopting a second package for one interface, and it's
exactly the kind of "tiny project-local helper that should stay
project-local" bucket-2 pattern from §1's own classification scheme, not
something `Compono.Http` should try to absorb. This directly grounds
§14's "no native `IHttpClientFactory` support in v1" deferral in a proven
consumer path rather than an assumption — the real call site is served
today, at zero cost to `Compono.Http`'s dependency graph, by a boundary
`Compono.Http` and `Compono.TestDoubles` already agree on for unrelated
reasons.

### 8.3 Internal model (informing, not fixing, the ADR's implementation section)

- **`TestHttpHandler : HttpMessageHandler`** — holds a `List<HttpResponseRegistration>`
  (append-only during configuration; see §11.3 for why a plain `List<T>`
  is safe here) and a thread-safe request log.
- **`HttpResponseRegistration`** — one instance per `OnX(...)`/`When(...)`
  call. Holds: the matcher (a `Func<HttpRequestMessage, bool>`, whether
  produced by an `OnX(Match<string>)` helper or a raw `When(...)` call —
  both compile down to the same matcher shape internally, keeping
  dispatch uniform even though the public API has two entry points), a
  single `Func<HttpRequestMessage, HttpResponseMessage>` **response
  factory** (§11.1 — `Respond*` helpers assign a lambda that builds a
  fresh `HttpResponseMessage`/content per call; `Throws` assigns a lambda
  that throws instead of returning), and an `int _matchedCallCount` field
  mutated only via `Interlocked.Increment` (mirrors `ReturnConfig<T>.RecordCall()`'s
  own pattern). Exposes `public CallVerifier Verify()` wrapping that
  count — no new verification type.
- **Dispatch** (`SendAsync`): record the request into the log first,
  unconditionally (§11.3's locked ordering); then walk the registration
  list last-registered-first (§9), and either call the first matching
  registration's response factory and `Interlocked.Increment` its count,
  or — no registration matches — throw `UnmatchedHttpRequestException`
  (§9), carrying the method/URI/"nothing matched" diagnostic. No lock is
  needed around the registration list itself under the narrowed
  concurrency contract (§11.4).
- **Request log**: a lock-guarded (or `ConcurrentQueue<HttpRequestMessage>`-backed)
  append-only collection of raw `HttpRequestMessage` references (§11.3),
  exposed as `IReadOnlyList<HttpRequestMessage> Requests` returning a
  fresh point-in-time snapshot per access (§11.3) — never a live view.
- **`UnmatchedHttpRequestException`**: a plain `Compono.Http`-owned
  exception type, no new core primitive — reports HTTP method, request
  URI, and that no configured registration matched (§9).

---

## 9. Matching semantics

**Recommendation: last-match-wins over an ordered, append-only
registration list — the same model ADR-0050 already established for
`Compono.TestDoubles`.**

Rationale: this is a genuine architectural choice, not an accident of copy-
paste — the ecosystem survey (§3) shows there's no single dominant answer
(MockHttp uses queue-then-first-match, WireMock uses explicit priority),
so nothing externally overrides Compono's own precedent. Product
consistency (a Compono user who already knows "later registration wins"
from `Compono.TestDoubles` doesn't have to learn a second rule for HTTP)
outweighs picking a different model for its own sake, and it directly
enables the exact "broad default, then override for one specific request"
pattern named in the brief:

```csharp
handler.When(_ => true).Respond(HttpStatusCode.InternalServerError); // fallback
handler.OnGet("/users/42").RespondJson(user);                        // specific override
```

Matching dimensions in v1, per evidence in §7: HTTP method, path/URI
(exact-string match against `PathAndQuery`, mirroring what every real test
already checks), and a general predicate escape hatch. **No dedicated
header matcher, query-string matcher, or JSON-body matcher in v1** — none
of these appeared as a *repeated* pattern in `alexa-vox-craft` (the one
`FormUrlEncodedContent` type-check is a single occurrence, fully served by
the predicate escape hatch). Building a full matcher DSL for dimensions
with one real occurrence each would be exactly the "large fluent DSL"
the brief says to avoid in favor of a simple predicate.

Explicitly rejected: "most-specific-wins" — no evidence anywhere (in
Compono's own precedent or the surveyed ecosystem) that this is worth its
complexity, and the brief calls it out as magic to avoid without an
unusually strong reason. None was found.

### Should this reuse core `Match<T>`, or stay HTTP-native?

Two shapes were compared directly, rather than defaulting to `Match<T>`
for ecosystem consistency:

```csharp
// (a) Match<T>-based
handler.OnGet(Match<string> path)          // where "path" is exact/Any/predicate
handler.When(Match<HttpRequestMessage> request)

// (b) HTTP-native
handler.OnGet(string path)
handler.When(Func<HttpRequestMessage, bool> predicate)
```

**`Match<T>`'s actual shape** (`src/Compono/Match.cs`) is a
three-state matcher over **one scalar value**: `Equality` (implicit from a
literal `T`, no closure allocated), `Any<T>()`, or `Is<T>(predicate)`. It
was designed for generated per-*parameter* dispatch, where a method has
several independent arguments and each gets its own `Match<TParam>`
(ADR-0048). That shape is a good fit wherever **one** matchable scalar
naturally exists on its own — and a poor fit wherever the thing being
matched is really several dimensions of one larger object evaluated
together.

**Where it doesn't earn its place: the whole-request condition.**
`Match<HttpRequestMessage>` would only ever be constructed via
`Match.Is<HttpRequestMessage>(predicate)` in practice — there's no
sensible default `EqualityComparer<HttpRequestMessage>` to make the
implicit-literal `Equality` case meaningful, and `Any<HttpRequestMessage>()`
is already spelled `When(_ => true)` for free. Wrapping the predicate in
`Match<HttpRequestMessage>` would mean every real call still writes
`Match.Is<HttpRequestMessage>(req => ...)`, i.e. the exact same
`Func<HttpRequestMessage, bool>` a plain `When(Func<...>)` parameter
already accepts, plus one extra type name and one extra call
(`Match.Is<T>(...)`) with zero behavioral gain. This is precisely the
"replacement for HTTP-domain design" case the brief warns against — two
of `Match<T>`'s three states (`Equality`, `Any`) are dead weight for this
parameter, so it doesn't produce a smaller or clearer API; it produces a
same-size API with an extra indirection. **Decision: `When` takes a plain
`Func<HttpRequestMessage, bool>`, not `Match<HttpRequestMessage>`.**

**Where it does earn its place: the `OnGet`/`OnPost`/etc. path
parameter.** Here a single scalar (the path string) genuinely is what's
being matched, and all three `Match<T>` states are meaningful for it:
`Equality` covers the dominant real case (100% of `alexa-vox-craft`'s
matchers are exact-path checks) with zero extra ceremony — `implicit
operator Match<T>(T value)` means `handler.OnGet("/users/42")` compiles
unchanged whether the parameter is `string` or `Match<string>`; `Is`
covers a prefix/pattern path check (not evidenced today, but a real,
plausible future need) without inventing a second overload or a
bespoke `PathMatcher` type; `Any<string>()` gives "any path for this
method" for free. Reusing `Match<string>` here is a genuine, concrete win
against the brief's own test: it avoids `Compono.Http` writing and
maintaining its own duplicate string-matcher type, it keeps `OnGet`'s
public signature future-proof for predicate-based path matching without a
breaking change or a second method name, and it costs nothing at the
dominant call site (the implicit conversion means existing-shaped calls
are unaffected). **Decision: `OnGet`/`OnPost`/etc. take `Match<string>
path`, reusing core `Match<T>` deliberately, not for consistency's own
sake.**

**Net effect on the dependency question (§13):** `Compono.Http` still
takes its dependency on core `Compono` for `Match<T>`/`CallVerifier`
regardless (`CallVerifier` reuse for §10 already required it) — this
finding doesn't change the dependency graph, only which of `Match<T>`'s
call sites are genuine versus decorative. If a future implementation
pass finds `Match<string>` for `path` isn't pulling its weight in
practice (e.g. `Is`/`Any` never get used against path in real dogfood
code), that's a signal to fall back to a plain `string` there too — this
isn't a one-way door, just the better-evidenced default.

### Unmatched-request behavior

Not locked in the first draft. **Decision: strict by default — no
matching registration means `SendAsync` throws a dedicated
`Compono.Http`-specific exception (working name
`UnmatchedHttpRequestException`), never a silently fabricated response.**

Rationale: a testing transport's job is to make an unexpected outbound
call *visible*, not to paper over it with a plausible-looking default. A
handler that returned, say, `default(HttpResponseMessage)` or an
implicit 404 for anything unconfigured would let a test whose SUT sends
the *wrong* request (wrong path, wrong method, a bug) still receive *some*
response and potentially pass — exactly the false-positive-test failure
mode that motivated the last-match-wins ordering itself (§9's ADR-0050
precedent was adopted specifically because a real dogfooding bug of this
shape was found in `Compono.TestDoubles`). An unmatched request should
fail loudly, the same way an unconfigured `Compono.TestDoubles` member
throws `TestDoubleNotConfiguredException` rather than returning
`default(T)` (§4's ADR-0045 rule) — this is the same design instinct
applied to the HTTP domain, not a new one invented for it.

A consumer that genuinely wants a fallback already has one, composing
naturally with last-match-wins (§9):

```csharp
handler.When(_ => true).Respond(HttpStatusCode.NotFound); // explicit fallback
```

No strict/loose mode switch is added — there's no dogfood evidence either
mode needs to be optional, and a mode flag would be exactly the kind of
unevidenced complexity the brief says to avoid.

**Exception diagnostics**: the exception message includes, at minimum,
the HTTP method, the request URI, and an explicit statement that no
configured registration matched — enough to immediately identify *which*
outbound call was unexpected without attaching a debugger. No stack
trace of registration internals is needed beyond that; nothing in the
evidence asks for more.

**`Requests` still records unmatched requests.** `handler.Requests`
represents *every request that reached the handler*, matched or not —
recording happens before matching is attempted (see §11.3's revised
ordering), so a request that triggers `UnmatchedHttpRequestException` is
still visible in the log for post-failure diagnosis. This is the same
principle as keeping request-log inspection independent from
registration-count verification (§10): the log answers "what did the SUT
actually send," which must include the request that just caused the test
to fail.

### Async matching stays out of v1

Every real `alexa-vox-craft` matcher
evaluated in this research (§1) reads only synchronous properties of the
request (`Method`, `RequestUri`, `Content`'s runtime *type* via `is
FormUrlEncodedContent`) — none reads request *content* asynchronously
(`ReadAsStringAsync()`/`ReadFromJsonAsync()`) as part of matching; body
comparison only ever happens in production `BaseClient` code or in
legacy tests' post-hoc `Assert` callbacks, never inside a matcher used to
select a response. `When(Func<HttpRequestMessage, bool>)` (synchronous)
is therefore sufficient for all evidenced cases. An async predicate
overload (`Func<HttpRequestMessage, Task<bool>>`) is deferred, not ruled
out — add it only if a future dogfood pass produces a real case needing
awaited content inspection during matching.

---

## 10. Verification model

### Registration handle, not a re-declared matcher

The first draft of this research proposed `handler.Verify(h =>
h.OnGet(expectedUri)).Once()`. On review, that shape is wrong: `OnGet` is
a *configuration* operation (it appends a registration to the handler's
list), and replaying it inside `Verify(...)` either (a) actually
re-registers a second, redundant entry as a side effect of "just asking a
question," or (b) requires a parallel non-mutating lookup path that
duplicates the matcher logic a second way — either way, it mixes
configuration and observation in one call, which is exactly the smell the
review flagged. An expression-tree-based `Verify(Expression<Func<...>>)`
API (parse the lambda to find which `OnGet` call it "means" without
executing it) was considered and rejected outright — it would require
expression-tree walking to recover the original registration, which is
unnecessary complexity for no evidenced benefit and sits close to the
"expression compilation" the AOT constraint (§12) already rules out.

**Decision: `OnX(...)`/`When(...)` return a registration handle
(`HttpResponseRegistration`), and verification is a method on that
handle** — a response registration already owns its matcher, its response
behavior, and (per §8.3) its own matched-call count, which makes it a
correct and sufficient verification identity on its own, with nothing to
re-declare:

```csharp
var registration = handler.OnGet(expectedUri).RespondJson(response);
// ... act ...
registration.Verify().Once();
```

### `registration.Verify().Once()` vs `registration.Once()`

Compared directly, per the brief. `Compono.TestDoubles`' own established
vocabulary is always `double.Verify().Member(args).Once()` — verification
is a distinct hop (`.Verify()`) before the terminal assertion, never a
bare `.Once()` straight off the thing being observed (confirmed against
real call sites in `test/Compono.TestDoubles.SampleTests/VerificationTests.cs`
and `ClosedInstantiationTests.cs`). `registration.Once()` would break that
established pattern for no evidenced gain, and it's also more ambiguous
at the call site — a bare `.Once()` on a registration object could
plausibly be misread as a *configuration* directive ("respond once, then
stop") rather than an assertion. `registration.Verify().Once()` costs
one extra hop and buys unambiguous intent plus consistency with the
vocabulary Compono users already know from `Compono.TestDoubles`.
**Decision: `registration.Verify().Once()`.**

### Reuse `CallVerifier` directly

No new verification type. `HttpResponseRegistration.Verify()` wraps its
own `_matchedCallCount` in the existing, unmodified `CallVerifier`
(`Never`/`Once`/`Exactly` semantics carry over as-is) — the same pattern
`ReturnConfig<T>.RecordCall()` + `CallVerifier` already establishes in
`Compono.TestDoubles`, reused here without any generator involvement
(§4). No call-order verification, no argument-matcher-aware verification
beyond what the registration's own matcher already encodes — matches
`Compono.TestDoubles`' own deliberately minimal verification model, and
nothing in the `alexa-vox-craft` evidence asks for more (every real
verification today is `Received()`/`ReceivedCalls().Should().HaveCount(n)`,
i.e. count-only).

### Kept separate: global request-log inspection

`handler.Requests` (§8.1, §11.2) stays a distinct, independent surface
from registration-level verification — one answers "how many times did
*this configured behavior* get exercised," the other answers "what did
the SUT actually send, in full, regardless of which registration matched
it (or none did)." Collapsing these into one API was considered and
rejected: `LocaleHandlerTests`' real acceptance case (inspect the
outgoing `Accept-Language` header) doesn't need a registration at all —
it just needs the raw sent request, which argues for keeping request-log
access unconditional and not gated behind having configured a matching
registration first.

---

## 11. Lifecycle, disposal, concurrency

### 11.1 Handler / `HttpClient` ownership

- **Handler is `IDisposable`, and is caller-owned — Compono composition
  never owns or disposes it.** Corrected from an earlier draft's "typically
  `[Shared]`-composition-row-owned" phrasing, which misstated Compono's
  actual lifetime model: `CompositionRow`/`CompositionScope`/
  `CompositionContext`/`Composer` do not track or dispose resolved
  `IDisposable` values anywhere in Compono today (confirmed: no
  `IDisposable`/`Dispose` handling exists in `src/Compono/CompositionRow.cs`,
  `CompositionScope.cs`, or `CompositionContext.cs`) — `[Shared]` provides
  identity/reuse across one row's graph, it does **not** create a
  disposal scope. Composing `[Shared] TestHttpHandler handler` means
  Compono resolves and shares one instance within that row; it does not
  mean Compono disposes that instance when the row or test ends. The
  consumer/test fixture is responsible for disposing the handler itself.
  This ADR-territory decision deliberately does not introduce a new
  general Compono disposable-scope/lifetime mechanism to change that —
  automatic disposal of composed values, if ever wanted, is a separate
  core-capability decision outside this package's scope. Disposing the
  handler stops it accepting further configuration/requests but doesn't
  need to do anything beyond releasing its own state (no unmanaged
  resources).
- **`HttpClient`s built over the handler** are constructed with
  `disposeHandler: false` (§2) — the handler's lifetime is independent of
  any `HttpClient` wrapping it, so multiple `HttpClient`s (simulating
  multiple named/typed clients that should share one fake backend and one
  request log) can safely share one handler instance, and disposing one
  `HttpClient` never tears down the shared handler out from under
  another. This is locked into explicit API semantics: `CreateClient(...)`
  (renamed from the earlier working name `ToHttpClient` — `CreateClient`
  reads correctly when called more than once against the same handler to
  produce several independent clients, matching what "multiple clients may
  share one handler" actually looks like at a call site; `ToHttpClient`
  reads like a single, implied one-shot conversion, which is the wrong
  mental model here) always constructs with `disposeHandler: false` —
  never configurable to `true`, so there's no hidden mode where calling
  `CreateClient` a second time silently invalidates the first client's
  handler. Ownership is layered and entirely caller-side: the caller owns
  and disposes each `HttpClient` it creates, *and* the caller owns and
  disposes the handler itself (not Compono, per the correction above).
  Disposing an `HttpClient` never disposes the handler; disposing the
  handler invalidates subsequent sends through any `HttpClient` still
  wrapping it (`SendAsync` on a disposed handler throws
  `ObjectDisposedException`, §11.4) — ordinary handler disposal semantics,
  not a Compono-specific behavior. Nothing about this is hidden behind a
  convenience default, per the brief's requirement.
- `BaseAddress`, per `alexa-vox-craft`'s own real setup (object-initializer
  style, not a `HttpClient` constructor parameter — §1), is offered as an
  optional `CreateClient(Uri? baseAddress = null)` parameter purely as
  sugar for `var c = handler.CreateClient(); c.BaseAddress = baseAddress;`
  — this doesn't hide any lifetime behavior (`BaseAddress` is an ordinary
  mutable property, not a disposal concern), it just matches the existing
  call-site shape.

### 11.2 Response state: factory, not instance — a hard invariant

`alexa-vox-craft`'s own TestKit already had to work around exactly this
problem: its `ReturnsResponse` extension explicitly allocates a **fresh**
`HttpResponseMessage`/`JsonContent` on every matched call, with a code
comment citing "disposed content issues" (§1) — because `HttpContent` is
`IDisposable`, and the underlying transport/consumer code may dispose a
response's content after reading it once. A registration that stored one
`HttpResponseMessage` instance and handed the same reference back on every
match would reproduce that exact bug the moment a test's matcher matches
more than once (e.g. any test asserting `registration.Verify().Exactly(2)`
would be handing back an already-disposed response on the second call).

**Decision, locked as a hard invariant, not an implementation detail: a
registration's response state is `Func<HttpRequestMessage,
HttpResponseMessage>` — a factory describing *how to build* a response,
never a stored instance.** `Respond(statusCode)`, `RespondText(...)`,
`RespondJson(...)` all assign a lambda that materializes a brand-new
`HttpResponseMessage` (and brand-new content, see §12 for the JSON case)
on every invocation; `Throws(exception)` assigns a lambda that throws
instead of returning. This unifies the model to one delegate type instead
of a discriminated "response-or-exception" slot, and it's what makes
`Throws` composition-free — no separate dual-slot state to keep in sync,
unlike `ReturnConfig<T>`'s `Returns`/`Throws` mutual-exclusivity (which
exists there because a *value* can be reconfigured; an HTTP response
*registration* is fixed once created — reconfiguring one isn't an
evidenced need).

**`Throws(exception)` semantics, verified rather than assumed**: it
rethrows the **same configured `Exception` instance on every match** — no
per-invocation exception factory. A standalone spike (a
`Func<HttpRequestMessage, HttpResponseMessage>` that always `throw`s one
shared `Exception` instance, invoked three times in a loop) confirms
`ReferenceEquals` holds true across every throw, the exception's type and
`Message` are unaffected, and nothing crashes or corrupts state — each
throw only refreshes the exception's own `StackTrace` to the new throw
site, which is ordinary, expected .NET rethrow behavior, not a defect.
Unlike `HttpContent`, `Exception` carries no disposal semantics, so there
is no freshness requirement analogous to the one motivating `Respond*`'s
factory model above. **v1 does not add an exception factory/callback API,
cloning machinery, or exception-type-specific recreation** — reusing the
same instance is the entire v1 behavior, and the spike found no concrete
correctness issue that would justify anything more.

**If a raw `HttpResponseMessage` instance is ever exposed as an input at
all** (e.g. a hypothetical `Respond(HttpResponseMessage response)`
overload for full manual control), it must be documented and enforced as
single-use — the API should make "you own this instance, and it will be
consumed at most once, then this registration stops being callable a
second way" explicit at the call site, never silently mean "reuse this
same instance forever." Given no `alexa-vox-craft` evidence needs
hand-constructed `HttpResponseMessage` control beyond what
`Respond`/`RespondText`/`RespondJson`/`Throws` already cover, this
overload is not part of v1 — noted here only so the invariant above isn't
accidentally violated if it's added later.

### 11.3 Request log: raw `HttpRequestMessage` references

Compared three shapes: (A) raw `HttpRequestMessage` references, (B) a
small immutable captured-request snapshot type, (C) some other minimal
model. **Decision: (A), raw references** — for the following reasons,
checked directly against the real acceptance case named in the brief
(`LocaleHandlerTests`, inspecting the outgoing `Accept-Language` header
after `SendAsync` completes):

- `HttpRequestMessage.Dispose()` only disposes `Content` — `Method`,
  `RequestUri`, and `Headers` are ordinary properties, not
  `IDisposable`-backed, and remain fully readable after the message is
  disposed. The real acceptance case is a **header** read, which means
  it's unconditionally safe with raw references regardless of whatever
  disposes the request afterward.
- `alexa-vox-craft`'s *own current* capture idiom (§1's
  predicate-side-effect hack: `req => { capturedRequest = req; return
  true; }`) is already exactly this — a raw `HttpRequestMessage`
  reference stashed in a closure and inspected after the fact. It
  demonstrably already works reliably for this codebase's real tests
  today; adopting raw references isn't a new risk, it's formalizing what
  already works.
- The narrower risk — reading `Content` (body) after `SendAsync` returns,
  if the caller wrapped its own request in a `using` block that disposes
  it once the call completes — is real but (a) not the evidenced
  acceptance case (that's a header, not a body, read), and (b) an
  inherited property of the seam itself, not something a captured-request
  snapshot type would fix any more cleanly (a snapshot taken *before* the
  request is sent can't reflect content that's still being streamed, and
  `alexa-vox-craft`'s bodies are all small, pre-buffered types —
  `JsonContent`/`StringContent`/`FormUrlEncodedContent` — that support
  multiple reads while not yet disposed, so the risk only bites in the
  narrow disposed-before-inspection case, not as a general rule).
- Building a snapshot type (B) with no evidenced need would be exactly
  the kind of "giant snapshot type without evidence" the brief warns
  against — deferred unless a future dogfood pass surfaces a real case
  where raw references aren't reliable enough.

**`Requests` snapshot semantics.** The element type is settled (raw
`HttpRequestMessage`), but the *collection* type needs its own contract
under concurrent `SendAsync`. **Decision: `Requests` returns
`IReadOnlyList<HttpRequestMessage>`, where each property access takes a
stable point-in-time snapshot of everything recorded up to that access —
never a live read-only view over a mutable backing list.** A thin
`IReadOnlyList<T>` wrapper around a mutable `List<T>` (or exposing a
`ConcurrentQueue<T>` cast to `IReadOnlyList<T>`, which isn't even valid —
`ConcurrentQueue<T>` doesn't implement `IReadOnlyList<T>`) would let the
count or contents change out from under a caller mid-enumeration if a
concurrent `SendAsync` call is still appending — a real hazard given §11.4
already commits to concurrent `SendAsync` being supported. The
implementation backs `Requests` with a lock-guarded list; the `Requests`
getter takes the lock, copies to an array (or `ImmutableArray`), and
returns that — a small, bounded cost (a copy per access) in exchange for
never handing the caller a collection that can mutate underneath it.
`ConcurrentQueue<HttpRequestMessage>.ToArray()` (which snapshots
atomically) is an equally valid backing choice if it turns out to have
better throughput than a plain lock; either satisfies the same contract
and the decision is an implementation detail, not part of the public
contract.

**Recording order, locked**: per the brief's stated preference,

```
SendAsync receives request
    -> record request (into Requests)
    -> locate matching registration
    -> if matched: increment its matched-call count, invoke its response factory
    -> if unmatched: throw UnmatchedHttpRequestException (§9)
```

Recording happens **before** matching is attempted, unconditionally — this
is what guarantees an unexpected/unmatched request still appears in
`Requests` for diagnosis (§9's unmatched-request behavior explicitly
depends on this ordering), and it means `Requests` never has a gap
corresponding to a request that caused an exception. No alternative
ordering was found to have any advantage — recording *after* successful
matching would silently drop exactly the requests a test most needs to
see (the ones that didn't match anything).

### 11.4 Concurrency: narrowed contract

Per the brief's preference, the contract is deliberately asymmetric
rather than "everything concurrent with everything":

- **Configure before execution; concurrent `SendAsync` is supported;
  configuration concurrent with sends is unsupported/not guaranteed.**
  This matches how every real `alexa-vox-craft` test already uses the
  handler (all `OnX`/`When` calls happen during test arrange, before the
  SUT runs) and is a materially simpler contract to implement and reason
  about than full bidirectional thread-safety.
- Under that contract, the registration list itself can be a **plain
  `List<HttpResponseRegistration>`** — concurrent *reads* of an unmutated
  `List<T>` are safe in .NET, and since configuration isn't guaranteed
  concurrent with sends, the list is never being appended to while
  `SendAsync` is walking it. No lock is added here "to be safe" beyond
  what the contract actually promises — per the brief's "don't add
  complexity solely to promise it."
- What **does** need synchronization, because concurrent `SendAsync` calls
  genuinely do write to it: each registration's `_matchedCallCount`
  (`Interlocked.Increment` — lock-free) and the request log (a lock or a
  `ConcurrentQueue<HttpRequestMessage>` — genuinely concurrent appends
  from parallel `SendAsync` calls sharing one `HttpClient`, e.g. under
  xUnit v3 parallel test execution).
- **Behavior after disposal**: `SendAsync` on a disposed handler throws
  `ObjectDisposedException`, matching standard .NET disposal conventions
  (and `HttpMessageHandler`'s own base behavior) rather than a bespoke
  error.

---

## 12. AOT/trimming/System.Text.Json implications

**Correction from the first draft**: the handler/matching/dispatch
architecture itself (handler subclass, explicit `HttpClient` construction,
ordered list + lock/concurrent-collection) is fully AOT/trim-safe per §2's
conclusion — no reflection, no `Activator`, no expression compilation, no
proxying. That claim was, and stays, correct. But it does **not**
automatically extend to `RespondJson<T>(value)` as a bare, one-argument
call — `JsonSerializer.Serialize<T>(value)` with the *default* serializer
options uses `System.Text.Json`'s reflection-based contract resolver,
which is not Native-AOT-safe on its own. The first draft's phrasing
implied `RespondJson<T>` was inherently AOT-safe by virtue of the rest of
the package being reflection-free; that's not accurate, and the doc
shouldn't state it that way going forward.

### v1 JSON surface

```csharp
RespondJson<T>(T value, JsonSerializerOptions? options = null)
RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)
```

- **`RespondJson<T>(value, options: null)`** (the ergonomic default) —
  falls back to `JsonSerializer.Serialize<T>(value)`'s default resolver.
  Matches `alexa-vox-craft`'s own current practice exactly: production
  code's `AlexaJsonOptions.DefaultOptions` is an ordinary runtime
  `JsonSerializerOptions` instance, not a source-generated context — no
  `alexa-vox-craft` evidence requires AOT-hardened JSON in test response
  bodies today. This overload is **not** claimed to be AOT-safe; it's the
  ergonomic path for the common (non-AOT-publishing test project) case.
- **`RespondJson<T>(value, options)`** — accepts caller-supplied
  `JsonSerializerOptions`, e.g. the consumer's own `AlexaJsonOptions.DefaultOptions`
  or a source-generated resolver they already own
  (`options.TypeInfoResolver` pointing at a `JsonSerializerContext`).
  AOT-safety here is **inherited entirely from what the caller passes in**
  — `Compono.Http` introduces no reflection of its own regardless of which
  `options` value is supplied, but whether the *resulting* serialization
  call is AOT-safe depends on the resolver behind those options, not on
  anything `Compono.Http` does.
  `Compono.Http` itself introduces no reflection at any point.
- **`RespondJson<T>(value, JsonTypeInfo<T> jsonTypeInfo)`** — the
  guaranteed-AOT-safe overload: `JsonSerializer.Serialize<T>(value,
  jsonTypeInfo)` takes a `JsonTypeInfo<T>` directly (e.g.
  `MyJsonContext.Default.ResponseModel` from a `[JsonSerializable(typeof(ResponseModel))]`-
  annotated source-generated context), bypassing any resolver lookup
  entirely. Cheap to support (one more overload over the same underlying
  `JsonSerializer.Serialize` family) and strictly additive — not required
  for the API to work, available for consumers who need the guarantee.
- Documentation must state plainly: **`Compono.Http` itself introduces no
  reflection anywhere in its own code; whether a specific `RespondJson`
  call is AOT-safe follows the `JsonSerializerOptions`/`JsonTypeInfo<T>`
  path the caller selects.** No overload is described as "inherently
  AOT-safe" independent of that choice, except the `JsonTypeInfo<T>`
  overload, which genuinely is by construction.

### Attribute propagation, verified empirically

The brief asked not to rely on documentation alone — AOT/trimming
consumers should get the actual compiler warning at *their* call site.
This was checked directly, both against the framework source and with a
small standalone build spike (not touching `Compono.Http`'s own source;
scratch project only, no code committed).

**Confirmed via Microsoft Learn's generated API reference** (`JsonSerializer.Serialize`,
`net-10.0`): the generic `Serialize<TValue>(TValue value,
JsonSerializerOptions? options = null)` overload — the one a
`RespondJson<T>(value, options)` implementation would call — carries both:

```csharp
[RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation. Use System.Text.Json source generation for native AOT applications.")]
[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.")]
```

while `Serialize<TValue>(TValue value, JsonTypeInfo<TValue> jsonTypeInfo)`
carries **neither** attribute.

**Confirmed via a build spike** (`dotnet build` with
`<IsAotCompatible>true</IsAotCompatible>` on a scratch `net10.0` console
project — this enables the same IL2026/IL3050 trim/AOT analyzers a real
AOT-publishing consumer project would have on, without needing a full
native `PublishAot` compile, which needs the ILCompiler toolchain and
is unnecessary just to observe analyzer diagnostics):

- A wrapper method shaped like `RespondJson<T>(value, options)`, calling
  `JsonSerializer.Serialize(value, options)` internally, **without** the
  `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` attributes on
  itself, produced IL2026 + IL3050 warnings **at its own internal call
  site** (i.e. inside `Compono.Http`'s own build) — but a caller of that
  *unannotated* wrapper from elsewhere got **no warning at all**. This is
  the failure mode the brief was worried about: if `Compono.Http` ships
  this overload unannotated, an AOT-publishing consumer calling
  `handler.OnGet(...).RespondJson(value)` would receive **zero** warning
  at their call site, even though the call is genuinely not AOT-safe —
  exactly "relying on documentation instead of tooling."
- The same wrapper, **with** the two attributes added to its own
  signature, produced **no warning internally** (the annotation satisfies
  the analyzer for the call inside `Compono.Http`'s own body), and the
  warnings correctly **moved to the external call site** — a caller
  invoking the annotated wrapper from an AOT-compatible project got the
  IL2026/IL3050 warnings at *their* call, pointing at *their* code.
- A third wrapper calling the `JsonTypeInfo<T>` overload produced **zero**
  warnings anywhere, confirming that path is genuinely clean, not just
  clean "by omission."

**Decision, locked**: `RespondJson<T>(T value, JsonSerializerOptions?
options = null)` on `Compono.Http`'s public API carries
`[RequiresDynamicCode(...)]` and `[RequiresUnreferencedCode(...)]`,
propagating `System.Text.Json`'s own warning text (or a
`Compono.Http`-authored equivalent naming the `JsonTypeInfo<T>` overload
as the AOT-safe alternative) so that AOT/trim-checked consumers see the
warning where it actually matters — at their own call site — not just in
a doc comment. `RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)`
carries neither attribute, matching the framework's own unannotated
`JsonTypeInfo<T>` overload exactly. No internal suppression
(`[UnconditionalSuppressMessage]`) is used anywhere in `Compono.Http`'s
own implementation to make it *look* AOT-clean while actually calling the
unsafe path underneath — the product story is honesty about which path
was taken, not a manufactured absence of warnings.

### Serialize-once-to-bytes model

Evaluated (not assumed) against the fresh-response-per-invocation
invariant (§11.2): **serialize once, at registration time, to an
immutable `byte[]`; construct a fresh `ByteArrayContent` from that same
buffer on every matched invocation.** This is safe and correct because:
`byte[]` itself is immutable data, freely shareable across calls with no
disposal concerns; `ByteArrayContent`'s *headers* (`Content-Type`,
`charset`) belong to the content instance, not the buffer, so they're set
explicitly on each fresh `ByteArrayContent` (`Headers.ContentType = new
MediaTypeHeaderValue("application/json") { CharSet = "utf-8" }`) —
nothing about headers is lost or shared incorrectly by reusing the byte
buffer. This also avoids re-running `JsonSerializer.Serialize` on every
matched call for a registration that gets hit many times (a real
possibility once `registration.Verify().Exactly(n)` for `n > 1` is a
supported pattern, §10) — a one-time cost instead of a per-call one, at no
correctness cost. `RespondText`'s string content doesn't need this
optimization (the string itself is already immutable) — a fresh
`StringContent(text, encoding, mediaType)` per call is fine there without
pre-encoding to bytes.

### Dependency isolation

Keep the core package's AOT story independent of
`Microsoft.Extensions.Http`/DI/Options (§2's flagged, only partially
confirmed trim risk there) by not taking a hard dependency on it (§13).

---

## 13. Minimal dependency graph

```
Compono.Http
    -> Compono   (reuses Match<T>, CallVerifier — no generator dependency)
```

- **No hard dependency on `Compono.TestDoubles`** — nothing in v1 needs
  source-generated interface doubling; `HttpMessageHandler` isn't an
  interface and shouldn't be taught to that generator (§4).
- **No hard dependency on `Compono.DependencyInjection`** — its current
  pull-only `AsServiceProvider()` bridge doesn't reach factory-registered
  types anyway (§4), and v1's `ConfigurePrimaryHttpMessageHandler` story
  (if built at all) is a plain `Microsoft.Extensions.Http` extension
  method needing only `IHttpClientBuilder`, not Compono's own DI bridge.
- **No hard dependency on `Microsoft.Extensions.Http`** in the core
  package — a consumer using `Compono.Http` with only direct `new
  HttpClient(handler)` construction (the dominant `alexa-vox-craft`
  pattern today) shouldn't have to pull in the DI/Options stack at all.
  If named/typed-client integration is added, it should be a separate,
  clearly optional extension surface (either a conditionally-compiled
  path or, if it grows enough shape, a later `Compono.Http.DependencyInjection`
  extension package — decide in the ADR once/if that feature is actually
  scoped, not preemptively now).
- **No NSubstitute/Moq dependency** — the whole point is that a fake
  handler needs no mocking framework; it's a plain subclass.

---

## 14. Explicit non-goals / deferred capabilities

- Full HTTP matcher DSL (dedicated header/query-string/JSON-body matcher
  types) — no repeated evidence for it; the predicate escape hatch covers
  the one observed case. Revisit only if a future dogfood pass produces
  repeated evidence.
- Native `IHttpClientFactory`/named-client/typed-client DI registration
  helpers — genuinely useful (`alexa-vox-craft` has 3 typed-client
  registrations) but this is a `Microsoft.Extensions.Http` concern
  reachable today via plain `ConfigurePrimaryHttpMessageHandler`, not
  something `Compono.Http` needs to own to deliver its core value. This
  deferral is no longer just an assumption — §8.2 works the real
  `SmapiDeveloperAccessTokenProviderTests` `IHttpClientFactory` call site
  through concretely and shows it's served today at zero cost to
  `Compono.Http`'s dependency graph (a 3-line project-local fake
  `IHttpClientFactory`, since it's an ordinary single-method interface).
  Revisit native support as an optional extension only if a future
  dogfood pass surfaces *named*/*typed*-client factory usage the 3-line
  fake or `Compono.TestDoubles` genuinely can't serve well.
- Retry/Polly-aware testing — zero evidence of retry policies existing
  anywhere in `alexa-vox-craft`.
- "Most-specific-wins" matching — explicitly rejected, no evidence, adds
  magic the brief warns against.
- Delay/latency simulation, callback-based responses, WireMock-style
  stateful scenarios, GraphQL/gRPC matching — zero evidence, out of scope
  for a client-testing-focused package.
- `JsonSerializerContext`-mandatory JSON path — optional future addition,
  not required for v1's API to work.
- Async request matching (`Func<HttpRequestMessage, Task<bool>>`) — no
  evidenced need for awaited content inspection during matching (§9);
  every real matcher only reads synchronous request properties.
- A strict/loose unmatched-request mode toggle — unmatched requests
  always throw `UnmatchedHttpRequestException` (§9); no configuration
  flag to make that behavior optional. No dogfood evidence either mode
  needs to be switchable, and a fallback is already reachable explicitly
  via `handler.When(_ => true).Respond(...)`.
- A raw `HttpResponseMessage`-accepting `Respond(HttpResponseMessage)`
  overload — no evidenced need beyond `Respond`/`RespondText`/`RespondJson`/
  `Throws`; if ever added, it must be documented single-use per the
  fresh-response invariant (§11.2), not silently reused across matches.
- Migrating the two frozen "Legacy" test projects' hand-rolled
  `ActionHandler`/`ActionMessageHandler` — real duplication evidence
  (§1), but those projects are explicitly legacy/frozen; migrating them
  is a stretch goal for the dogfood pass, not a blocking acceptance
  criterion (see §15).

---

## 15. Dogfood acceptance criteria

Primary (blocking):

1. `test/AlexaVoxCraft.Http.TestKit/Extensions/HttpMessageHandlerExtensions.cs`'s
   reflection-based `ReturnsResponse` is removed and replaced by
   `Compono.Http`'s registration API; the 41 real call sites across
   `AlexaInteractionModelClientTests.cs`, `AlexaSkillInvocationClientTests.cs`,
   `SmapiDeveloperAccessTokenProviderTests.cs`, `InSkillPurchasingClientTests.cs`
   are migrated and pass unchanged in assertion intent.
2. `HttpClientSpecimenBuilder.cs`/`HttpClientSpecification.cs`/the
   `Freeze<HttpMessageHandler>()` plumbing in `ClientAutoDataAttribute`
   (and its 2 per-project subclasses) is replaced by `[Shared]`
   composition of the new handler type.
3. `LocaleHandlerTests.cs`'s two predicate-side-effect request captures
   are replaced by real `handler.Requests` reads.
4. `SmapiDeveloperAccessTokenProviderTests.cs`'s untyped
   `IHttpClientFactory.CreateClient()` usage is migrated using the 3-line
   project-local `FakeHttpClientFactory` shape proven in §8.2 (not a new
   `Compono.Http` capability) — confirms v1 doesn't require factory
   integration to serve this real call site.
5. Full `alexa-vox-craft` suite passes against packed local
   `Compono`/`Compono.Http` packages (see §16).

Stretch (non-blocking, report but don't gate on):

6. `BearerTokenHandlerTests.cs` gets written for the first time, using the
   new package — direct evidence the missing-helper friction (§1) was the
   actual cause of the coverage gap.
7. The two legacy `ActionHandler`/`ActionMessageHandler` fakes are
   migrated if it's low-risk; otherwise leave them and note why.

---

## 16. Reusing the local-package consumer-validation workflow

The `trivia-platform` pack-and-validate workflow (pack fresh uniquely-
versioned local packages → target repo restores against exact packages →
full target suite → repeat after substantive PR feedback) should be
**generalized, not reimplemented**, for `alexa-vox-craft`: parameterize
the existing script by target-repo path and package list rather than
writing an `alexa-vox-craft`-specific script. This is the same script,
different target repo and a `Compono.Http` entry added to its package
list — no new mechanism needed. (Locating and confirming the exact script
path/interface is implementation-task work, not part of this research
document.)

---

## 17. Final admission recommendation: **A**

`Compono.Http` clearly earns a package, with the v1 boundary above.

Reasoning against the outcome criteria:

- **Not C** (existing Compono + .NET APIs already solve it): they don't.
  `Compono.TestDoubles` structurally cannot reach `HttpMessageHandler`
  (not an interface — §4), `Compono.DependencyInjection`'s bridge is
  pull-only and doesn't reach factory-registered types (§4), and the
  *actual* current solution in `alexa-vox-craft` is a ~150-line hand-rolled
  kit that resorts to reflection into a protected member specifically
  because no clean primitive exists (§1). That kit is real, repeated (41
  call sites), and objectively worse than even the weakest surveyed OSS
  alternative (§3) — this clears the higher bar the brief sets for
  rejecting HTTP support given it's an explicit product requirement.
- **Not D** (would require disproportionate reflection/proxying/complexity):
  the opposite is true — the correct implementation (subclass
  `HttpMessageHandler`, ordered last-match-wins list reusing `Match<T>`/
  `CallVerifier`, explicit `HttpClient` construction) needs zero
  reflection and is confirmed AOT/trim-safe (§2, §12). This is materially
  *less* runtime machinery than what `alexa-vox-craft` already hand-rolls
  today.
- **Not B** (right architecture is materially different from the working
  hypothesis): the working hypothesis (`Compono.Http`, depends only on
  core `Compono`, handler-level fake, `[Shared]`-composable, ADR-0050-style
  matching) survived every research leg without needing correction beyond
  scope-narrowing (no native `IHttpClientFactory` support in v1, no
  matcher DSL beyond method+path+predicate). That's v1 sizing, not an
  architectural redirection.
- **Naming**: `Compono.Http`, per §4/§6/§7 — the ecosystem-scoped naming
  precedent applies cleanly (this isn't tied to one library version the
  way `XunitV3` is), and the v1 boundary genuinely isn't `HttpClient`-only
  forever — `HttpMessageHandler`-level interception, JSON helpers,
  request capture, and verification are all HTTP-domain concerns a single
  coherent package can own, matching the product direction's explicit
  preference.

**Update:** [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)
now records this decision (`Status: Proposed`, pending review) — this
research document remains the evidence trail the ADR summarizes. Per the
task boundary given: no code has been written, nothing has been
committed. Once the ADR is confirmed `Accepted`, the next step is a plan
scoping the v1 implementation and the dogfood migration in §15.
