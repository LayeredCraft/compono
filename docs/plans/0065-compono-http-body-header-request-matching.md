# [PLAN-0065] Compono.Http: Body and Header Request Matching

**Status:** Done

**Implements:** [ADR-0062](../adr/0062-compono-http-body-header-request-matching.md)

## Goal

`TestHttpHandler` can match a request on its headers and body content
(`WithHeader`, `WithBody`, `WithFormBody`, `WithJsonBody<T>`), chainable
with existing `OnX(path)`/`When(...)` on the same registration, plus a
public `WhenAsync` escape hatch — with every existing `1.0`–`1.2`
`Compono.Http` behavior (precedence, unmatched-request, verification,
response freshness, request logging, `IsAotCompatible=true`) unchanged and
regression-tested, not merely assumed unaffected. Done when the real
`alexa-vox-craft` OAuth test
(`SmapiDeveloperAccessTokenProviderTests.GetAccessTokenAsync_SendsFormUrlEncodedContent`)
is migrated to assert real field values via `WithFormBody`, verified
against a freshly-packed local build through `scripts/dogfood-validate.sh`.

## Scope

**One cohesive Plan, one PR** — internal matcher plumbing, the four new
public matcher methods, `WhenAsync`, documentation, skill/eval updates,
and dogfood validation all land together. No artificial phase-splitting;
the task groups below are execution order *within* this one Plan, not
separate PRs. In scope: everything ADR-0062 decided (D1–D11, D7a).
Explicitly out of scope, unchanged from ADR-0062: streaming/multipart
bodies, a configurable redaction list, `RespondStream`, any response-side
change.

## Tasks

### 1. Internal matcher architecture (`src/Compono.Http/`)

- [x] Add `internal delegate ValueTask<bool> AsyncRequestMatcher(HttpRequestMessage request, CancellationToken cancellationToken);`
  (new file `AsyncRequestMatcher.cs`, or alongside `HttpResponseRegistration.cs`
  — decide during implementation based on file-length convention already
  used in this package).
- [x] `HttpResponseRegistrationBuilder`: replace the single `Func<HttpRequestMessage, bool> matcher`
  constructor parameter with an internal accumulating
  `List<(AsyncRequestMatcher Matcher, string Description)>` — the
  `Description` half is threaded alongside each matcher from the start so
  `Finish()` can build the combined description string (D6) without a
  second pass. `OnX(...)`/`When(...)` seed this list with one entry each
  (wrapping the existing sync predicate/method-and-path check in
  `new ValueTask<bool>(...)`); the new `WithX(...)` methods (task 2)
  append further entries.
- [x] **All `WithX(...)` methods route through one private matcher-
  addition path** (e.g. `AddMatcher(AsyncRequestMatcher matcher, string
  description)`) on the builder — no method appends to the list directly.
  That path **checks `_finished` first and throws the exact same
  exception the existing `Finish()` guard already throws** (reuse, don't
  invent a second lifecycle model — see the "Finalized-builder immutability"
  subsection below for the exact contract).
- [x] `Finish()` (existing one-shot `_finished` guard, unchanged
  semantics, extended scope): **snapshots the accumulated list into an
  immutable representation (an array, or `.ToArray()`'d before compiling)
  before building the final matcher** — the compiled `AsyncRequestMatcher`
  handed to `HttpResponseRegistration` must not close over the builder's
  own mutable `List<T>` field, so no code path (not even a hypothetical
  future one) could let a later builder mutation retroactively change an
  already-finalized registration's behavior. Compiles the snapshot into
  one `AsyncRequestMatcher` that evaluates each entry **in declaration
  order, short-circuiting on the first `false`** (D11), and one
  description string joining each entry's own description with `" and "`
  (mirrors `Compono.Logging`'s `LogVerificationBuilder.Describe()`
  pattern, per D6/D7a), then constructs `HttpResponseRegistration` with
  the compiled matcher + description exactly as today.

#### Finalized-builder immutability — exact contract

Must be impossible for a caller to do:

```csharp
var builder = handler.OnPost("/foo");
var registration = builder.Respond(HttpStatusCode.OK);
builder.WithHeader("X-Test", "abc");  // must throw, must NOT silently change `registration`
```

- [x] `WithHeader`/`WithBody`/`WithFormBody`/`WithJsonBody<T>` (both
  overloads) — every one calls the same private `AddMatcher(...)` path
  (above) as its very first action, before doing any other work.
  `AddMatcher` throws `InvalidOperationException` with the **same message
  text** the existing `Finish()` guard already uses
  (`"This HttpResponseRegistrationBuilder was already finalized by a
  prior Respond*/Throws call...`) when `_finished` is already `true` — one
  exception type, one message convention, one lifecycle flag
  (`_finished`), reused exactly as it already governs `Respond*`/`Throws`
  double-finalization today. No new exception type, no new public
  lifecycle API, no second `_finished`-equivalent flag.
- [x] The already-returned `HttpResponseRegistration` (from the `Respond*`/
  `Throws` call that set `_finished = true`) is entirely unaffected by the
  post-`Finish()` `WithHeader` call above throwing — it was already built
  from the **snapshotted** matcher list (previous bullet), so there is no
  shared mutable state between "the builder, now rejecting further
  `WithX` calls" and "the registration, already matching exactly what it
  matched before the rejected call was attempted."
- [x] `HttpResponseRegistration.Matches(request)`: change from
  `bool Matches(HttpRequestMessage request)` to
  `ValueTask<bool> Matches(HttpRequestMessage request, CancellationToken cancellationToken)`
  — `internal`, so this is not a public signature change (confirmed
  against `PublicApiSurfaceTests`-equivalent coverage, task 8).
- [x] `TestHttpHandler.SendAsync`: change the matching `for` loop to
  `await`-capable — `for (...) { if (await _registrations[i].Matches(request, cancellationToken)) { matched = ...; break; } }`.
  Confirm (do not assume) that this still preserves last-registered-first
  iteration order and stops at the first `true` — this is the one
  execution-shape change ADR-0062 D11 explicitly calls out.
- [x] `RecordRequest(request)` stays unconditional and **before** the
  matching loop starts, unchanged — verify this ordering explicitly with a
  new test (task 5).

### 2. Public matcher APIs on `HttpResponseRegistrationBuilder`

Exact signatures, per ADR-0062's Public API Review — implement verbatim,
no redesign:

```csharp
public HttpResponseRegistrationBuilder WithHeader(string name, string value);
public HttpResponseRegistrationBuilder WithBody(Func<byte[], bool> predicate);
public HttpResponseRegistrationBuilder WithFormBody(Func<ILookup<string, string>, bool> predicate);
public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonTypeInfo<T> jsonTypeInfo);

[RequiresDynamicCode("...")]
[RequiresUnreferencedCode("...")]
public HttpResponseRegistrationBuilder WithJsonBody<T>(Func<T?, bool> predicate, JsonSerializerOptions? options = null);
```

- [x] `WithHeader(name, value)` — per ADR-0062 D5 (corrected): query
  `request.Headers.TryGetValues(name, out var r)` and
  `request.Content?.Headers.TryGetValues(name, out var c)`
  **unconditionally, both**, combine, match if `value` (ordinal) appears
  in the combined set. Name comparison case-insensitive (the BCL
  `HttpHeaders.TryGetValues` overload taking a `string` name is already
  case-insensitive internally — confirm this during implementation rather
  than adding a redundant manual case-fold). Description contribution:
  `header "{name}" = "{value}"`, or `header "{name}" = "<redacted>"` if
  `name` case-insensitively equals `Authorization`/`Proxy-Authorization`/
  `Cookie`/`Set-Cookie` (D6) — the redaction check lives in the
  description-building step only, never in the match predicate itself.
- [x] `WithBody(predicate)` — `ReadAsByteArrayAsync(cancellationToken)`
  when `request.Content is not null`; `Content is null` → `false`, no
  read attempted. No media-type check (D7a).
- [x] `WithFormBody(predicate)` — per ADR-0062 D7a: first check
  `request.Content?.Headers.ContentType?.MediaType` equals
  `"application/x-www-form-urlencoded"` (`OrdinalIgnoreCase`); mismatch or
  absent → `false`, **no body read attempted** (short-circuits before the
  parse, per D11). On a match, `ReadAsStringAsync(cancellationToken)`,
  parse per ADR-0062 D7's corrected algorithm — split on `&`, split each
  piece on the first `=` (no `=` → empty value), decode each raw key/value
  via `Uri.UnescapeDataString(raw.Replace('+', ' '))` (`+`-replace
  **before** unescape, not after), build an `ILookup<string, string>` via
  `.ToLookup(p => p.Key, p => p.Value, StringComparer.Ordinal)` preserving
  wire order and duplicate keys, invoke `predicate`.
- [x] `WithJsonBody<T>` (both overloads) — per ADR-0062 D7a: check
  `Content-Type` is `application/json` (ordinal-insensitive) or ends with
  `+json` (ordinal-insensitive); mismatch/absent → `false`, no
  deserialization attempted. On a match, read
  `ReadAsStringAsync(cancellationToken)`, call
  `JsonSerializer.Deserialize<T>(json, jsonTypeInfo)` /
  `JsonSerializer.Deserialize<T>(json, options)`, invoke `predicate(result)`.
  **Do not catch `JsonException`** — let it propagate per D10. Attribute
  placement on the `JsonSerializerOptions?` overload mirrors
  `RespondJson<T>`'s existing attributes exactly (same message text
  convention, updated to name `WithJsonBody<T>`'s own AOT-safe sibling
  overload instead of `RespondJson<T>`'s).

### 3. `WhenAsync` on `TestHttpHandler`

- [x] `public HttpResponseRegistrationBuilder WhenAsync(Func<HttpRequestMessage, CancellationToken, ValueTask<bool>> predicate)`
  — `ArgumentNullException.ThrowIfNull(predicate)` (matching `When`'s
  existing guard), constructs the builder seeded with `predicate` directly
  as the first `AsyncRequestMatcher` entry, description `"WhenAsync(...) request"`
  (mirroring `When`'s existing `"When(...) request"` literal exactly).

### 4. Compatibility regression coverage (do not assume "existing tests still pass" proves this)

Real semantics that need an explicit new test under the now-async-capable
dispatch path, not merely a rerun of tests written against the old
guaranteed-synchronous scan:

- [x] **Last-match-wins across a mixed sync/async registration set** — a
  test registering an earlier, broader sync-only (`OnGet`/`When`)
  registration, then a later, narrower registration using a new `WithX(...)`
  chained condition, confirms the later (chained) one still wins per
  last-registered-first — proves precedence survives the loop becoming
  `await`-capable, not just that it worked before this feature existed.
- [x] **Unmatched request when every registration's chain fails** — at
  least one registration includes a chained async condition (e.g.
  `WithHeader`) that evaluates `false`; confirms
  `UnmatchedHttpRequestException` still throws with the same
  method+URI-only message (D6), not swallowed or altered by the `await`.
- [x] **`Requests` still records before matching, even when matching
  becomes async** — a request that fails every chained condition (async
  or not) still appears in `Requests` afterward; a test forcing at least
  one registration to have an async condition confirms `RecordRequest`
  still runs unconditionally before the (now-awaited) matching loop.
- [x] **`Verify()` count correctness with a chained-condition
  registration** — a registration with 2+ chained conditions (e.g.
  `OnPost(path).WithHeader(...).WithFormBody(...)`) matched exactly once;
  `Verify().Once()` passes; a second, non-matching request against the
  same registration doesn't increment the count — confirms
  `RecordMatch()`'s timing (only after the full chain passes) is
  unaffected by accumulation.
- [x] **Response factory freshness unaffected** — a chained-condition
  registration matched twice still produces two independently-fresh
  `HttpResponseMessage`/`Content` instances (mirrors the existing
  `RepeatedMatches_GetFreshResponseAndContentEachTime` test, extended to a
  registration using a new chained condition, not just method/path).
- [x] **`ConcurrentSendAsync` with a mix of sync and async-chained
  registrations** — extends the existing
  `ConcurrentSendAsync_RecordsEveryRequestAndCountsEveryMatchCorrectly`
  test (or adds a sibling) to include at least one chained-condition
  registration in the concurrently-dispatched mix, confirming no race in
  the now-awaited matching loop.
- [x] Confirm (read, don't just assume) that none of the above needed
  test already exists under a different name in
  `test/Compono.Http.Tests/TestHttpHandlerTests.cs` before adding a
  duplicate — the file's current 29 tests already cover last-match-wins,
  unmatched-request, `Requests` ordering, `Verify()` counts, response
  freshness, and concurrency **for the pre-existing sync-only surface**;
  every bullet above is specifically "the same guarantee, now proven under
  a registration that actually exercises the new async-capable path,"
  which none of the current 29 do (confirmed by reading the full test
  file — no existing test references any of the new `WithX`/`WhenAsync`
  members, by construction, since they don't exist yet).
- [x] **Post-`Finish()` matcher-mutation rejection** (new — the
  finalized-builder immutability contract, task 1): finalize a builder via
  `Respond(...)`, capture the returned `HttpResponseRegistration`, then
  call `.WithHeader(...)` (or any other `WithX`) on the **same, already-
  finalized builder** — assert it throws `InvalidOperationException` with
  the same message text `Finish()`'s existing double-`Respond*`/`Throws`
  guard already throws; then exercise the captured registration's
  matching behavior again (send a matching request through the handler)
  and assert it still matches/responds exactly as it did before the
  rejected `WithHeader` call was attempted — proving the snapshot-at-
  `Finish()` mechanism actually isolates the finalized registration from
  the rejected mutation attempt, not just that the mutation attempt itself
  throws.

### 5. New-behavior test matrix (`test/Compono.Http.Tests/`)

- [x] **`WithHeader`**: exact match; case-insensitive name, case-sensitive
  value; value present in `Content.Headers` but not `Headers` (and vice
  versa) — proving the corrected no-precedence two-collection check (D5);
  a header present with a *different* value in one collection and the
  matching value in the other (the exact scenario the ADR-0062 review
  caught) — must match; multi-value header, expected value among several;
  missing header → no match, no throw; sensitive-header description
  redaction for each of the four denylisted names, and a non-denylisted
  header's description shows the real value.
- [x] **`WithBody`**: byte-for-byte match/no-match; `Content is null` →
  no match, predicate never invoked (assert via a predicate that throws if
  called, or a captured invocation flag).
- [x] **`WithFormBody`**: the five decoding cases ADR-0062 D7 validated
  (`name=Nick+Cipollina`, `value=a%2Bb`, `tags=a&tags=b`, `empty=`, `flag`)
  each as their own test or a `[Theory]` covering all five; **Content-Type
  requirement** — a body that parses as valid form data but under
  `text/plain` does **not** match; a body under
  `application/x-www-form-urlencoded` (any casing) matches.
- [x] **`WithJsonBody<T>`**: matches on a satisfying predicate via both
  overloads (`JsonTypeInfo<T>` and `JsonSerializerOptions?`); does not
  match a validly-parsed-but-predicate-`false` body; **Content-Type
  requirement** — a JSON-shaped body under `text/plain` does not match; a
  body under `application/vnd.api+json` (a `+json` suffix, not exactly
  `application/json`) does match; **malformed-content exception
  semantics** — a body under `application/json` that isn't valid JSON
  causes `SendAsync` to throw the underlying `JsonException` directly
  (not `UnmatchedHttpRequestException`, not swallowed).
- [x] **`WhenAsync`**: matches/doesn't-match via a real `async`/`await`
  predicate body (not just a synchronously-completing one, to prove the
  dispatch loop genuinely suspends correctly); cancellation — a predicate
  that observes an already-cancelled token throws
  `OperationCanceledException` out of `SendAsync`.
- [x] **Chaining/ordering**: `OnPost(path).WithHeader(h).WithFormBody(f)`
  — a request failing the header check never has its body read (assert via
  a form predicate that throws if invoked, proving short-circuit); the
  same chain with a passing header and failing form body doesn't match;
  both passing matches.
- [x] **Public API surface — confirmed the `PublicApiSurfaceTests` pattern
  doesn't apply here, not added.** Checked: `Compono.Http` has no such test
  today (unlike `Compono.TestDoubles`, whose entire runtime-package public
  surface is a deliberately tiny, fixed 2-type list worth locking exactly).
  `Compono.Http`'s surface already grows incrementally by design (e.g.
  `RespondBytes` was added in a prior release with no such lock) — adding
  one now would be a new, unevidenced convention for this package, not
  filling a gap. Deviation from the plan's own tentative wording, recorded
  here per this file's own "plan being wrong about how" allowance — no ADR
  impact.

### 6. AOT/trimming validation — two independent proofs, kept separate

This package's own established precedent (confirmed by reading it, not
assumed) already splits AOT validation into two deliberately independent
proofs for `RespondJson<T>`'s identical two-overload shape — this task
extends both, in place, without letting either contaminate the other:

**Proof B — the real, clean Native AOT publish-and-run**
(`test/Compono.Http.AotSmokeTest/Program.cs`, driven by
`pack-compono.sh`): this project's own comment is explicit that it
"deliberately" exercises *only* the AOT-safe `RespondJson<T>(value,
JsonTypeInfo<T>)` overload — the `JsonSerializerOptions?`-based one is
intentionally absent, because this is the path that must **publish and
run with zero unexpected IL2xxx/IL3xxx warnings**, and an intentionally-
warning-producing call has no business inside it.

- [x] Extend `Program.cs` to additionally exercise `WithHeader`,
  `WithFormBody`, `WithBody` (if a natural assertion fits alongside the
  existing scenario), and **only** the `JsonTypeInfo<T>` overload of
  `WithJsonBody<T>` — mirroring `RespondJson<T>`'s own precedent exactly:
  the `JsonSerializerOptions?` overload of `WithJsonBody<T>` is
  deliberately **not** called here, for the identical reason.
- [x] Run a real `dotnet publish -p:PublishAot=true` against the updated
  project and confirm it still produces a working native executable with
  **no new, unexpected IL2xxx/IL3xxx warnings** — the smoke path stays
  unambiguously clean.
- [x] Confirm `Compono.Http.csproj`'s `IsAotCompatible=true` property is
  unchanged (no removal, no downgrade) after these additions.

**Proof A — the analyzer-contract pair** (`test/Compono.Http.AotSmokeTest/AnalyzerContract/`,
driven by `verify-analyzer-contract.sh`): this is the actual, already-
established mechanism that originally validated `RespondJson<T>`'s two
overloads' attribute propagation — a build-time (not full-publish)
`IsAotCompatible=true` compile of two small, isolated caller projects,
one expected to warn, one expected not to, asserted via
`-p:WarningsAsErrors="IL2026%3BIL3050"` and a grep over the build log.
This is the mechanism to extend for `WithJsonBody<T>`, not a new one:

- [x] Add a `WithJsonBodyOptionsOverloadCaller` project (sibling of
  `OptionsOverloadCaller`, same shape: `PackageReference` not
  `ProjectReference` — confirmed load-bearing by that project's own
  comment — `TargetFrameworks` override, isolated `RestorePackagesPath`)
  whose `Program.cs` calls only
  `WithJsonBody<T>(predicate, (JsonSerializerOptions?)null)` — expected to
  produce IL2026 **and** IL3050 at that call site.
- [x] Add a `WithJsonBodyTypeInfoOverloadCaller` project (sibling of
  `JsonTypeInfoOverloadCaller`, same shape) whose `Program.cs` calls only
  `WithJsonBody<T>(predicate, jsonTypeInfo)` — expected to build with
  **zero** IL2026/IL3050.
- [x] Extend `verify-analyzer-contract.sh` with the same
  build-expect-warn / build-expect-clean / grep-the-log pattern already
  used for the `RespondJson<T>` pair, for this new pair — proving: (a) the
  `WithJsonBody<T>(..., JsonSerializerOptions?)` overload genuinely
  carries the intended attributes and they propagate to the consumer's
  call site rather than being swallowed inside `Compono.Http`; (b) the
  `JsonTypeInfo<T>` overload is genuinely clean; (c) because this
  proof runs as its own separate, intentionally-warning-tolerant script
  — never as part of the ordinary `dotnet build`/`dotnet test` 0-warnings
  gate, and never inside Proof B's clean smoke path — an expected IL2026/
  IL3050 here is never mistaken for a real `Compono.Http` AOT-path
  failure.
- [x] **Do not suppress any warning anywhere to keep a smoke executable
  green** — Proof A's whole purpose is that the `JsonSerializerOptions?`
  overload *does* warn, honestly, at the consumer's own call site; Proof B
  stays clean by never calling that overload, not by suppressing what it
  would otherwise report.

### 7. Performance/allocation guidance (corrected — no "zero additional allocation" requirement)

Per ADR-0062's own post-review correction: the accepted matcher-
accumulation architecture (task 1) means a plain `OnX(path)`-only
registration no longer hands one delegate straight to
`HttpResponseRegistration`'s constructor — some small, real
registration-construction overhead beyond `1.2`'s exact shape is expected
and accepted. **Do not add pooling, custom collections, caching, or a
single-entry special case to preserve a zero-additional-allocation claim
no evidence demands** — if a straightforward implementation happens to
avoid an allocation for the common case, that's a nice property, not a
requirement to design toward.

The actual, corrected contract:

- [x] **No `Task`/async-state-machine allocation or runtime suspension for
  a synchronously-completing condition** — every existing sync matcher
  (method+path, `When`) wraps in a synchronously-completed
  `ValueTask<bool>` (D1); confirm this holds for the implemented shape
  (a synchronously-completed `ValueTask<bool>` genuinely completes
  without ever suspending the calling `await`, which is `ValueTask<bool>`'s
  own documented behavior, not something this package has to build).
- [x] **No body read or JSON deserialization happens until every
  preceding chained condition has already passed** — already covered as a
  correctness requirement (D11); recorded here too because it's also the
  load-bearing performance property (a failed header check costs nothing
  beyond the header check itself, regardless of how expensive a later
  `WithJsonBody<T>` condition in the same chain would have been).
- [x] **No new per-request allocation solely because an existing
  `OnX`/`When` matcher happens to be synchronous** — beyond whatever the
  final, accepted matcher-dispatch representation (task 1's compiled,
  snapshotted `AsyncRequestMatcher` chain) inherently requires to exist at
  all. Small registration-*construction*-time overhead (building the list,
  snapshotting it at `Finish()`) is acceptable and expected; this bullet
  is about *dispatch*-time cost per incoming request, not construction.
- [x] Spot-check only (no formal benchmark suite added — no evidence this
  package needs one, consistent with ADR-0062's own restraint elsewhere)
  that dispatching a request against a plain `OnX(path)`-only registration
  is not measurably slower than `1.2`'s direct-delegate-invocation shape —
  "not measurably slower," not "zero additional allocation."
- [x] Note in the package doc (task 8) that a chained condition adds one
  more entry to a per-registration list evaluated at dispatch time — a
  cost proportional to the number of chained conditions on the winning
  (or nearest-tried) registration, not a new fixed overhead paid by every
  dispatch regardless of usage.

### 8. Documentation

- [x] `docs/packages/compono-http.md` — add sections for `WithHeader`/
  `WithBody`/`WithFormBody`/`WithJsonBody<T>`/`WhenAsync` following the
  existing "What it gives you" structure; update "v1 non-goals" (remove
  the now-obsolete "Dedicated header/query-string/JSON-body matcher
  types" and "Async request-matching predicates" bullets — these
  described `1.0`–`1.2` scope, not permanent non-goals, per ADR-0062's
  "Relationship to ADR-0051" section); add the `Content-Type` requirement
  for `WithFormBody`/`WithJsonBody<T>` explicitly, since it's the one
  piece of behavior likely to surprise a consumer who expects pure
  content-shape matching.
- [x] XML doc comments on every new public member
  (`references/documentation.md`'s "from the first public member, not a
  backfill" rule) — cross-reference `ADR-0062` in each, matching this
  package's existing convention of citing `ADR-0051` sections by name in
  its own XML docs.
- [x] `docs/adr/README.md` — already updated (this ADR is `Accepted`); no
  further change needed here.

### 8a. `compono` agent skill update — a shipping requirement, not follow-up

- [x] `skills/compono/references/http.md` — add the new matcher vocabulary,
  the `Content-Type` requirement per matcher (D7a), the corrected
  two-collection `WithHeader` semantics, the malformed-JSON-propagates
  rule (D10), and the sensitive-header redaction behavior (D6) — enough
  detail that an agent recommending `Compono.Http` usage reaches for
  `WithFormBody`/`WithJsonBody<T>` over a `When(...)` type-check the same
  way this Plan's own motivating example does.
- [x] `skills/compono/SKILL.md` — check (per the routing table's existing
  pattern for other packages) whether its decision-routing list needs a
  line connecting "the composed type sends an HTTP request with a body
  that needs verification" to the new vocabulary — add one only if the
  existing `Compono.Http` routing entry doesn't already cover this
  (confirm by reading it first, don't assume it's missing).
- [x] Add at least one new eval to `skills/compono/evals/evals.json`,
  following the established structure (`id`, `category`, `prompt`,
  `expected_output`, `files`, `expectations`) — a scenario where a
  consumer needs to verify an outgoing request's form/JSON body content,
  **phrased as a real task, not naming `WithFormBody`/`WithHeader`/
  `WithJsonBody<T>` in the prompt** (the same "don't spoon-feed APIs"
  principle already applied to eval 56 in this repo's history). Negative
  signal to grade against: a response that only checks
  `req.Content is FormUrlEncodedContent`/similar type-only check, or
  drops to `.GetAwaiter().GetResult()` inside `When`, instead of reaching
  for the new first-class body matcher.
- [x] **Baseline-vs-updated skill eval workflow, required because the
  skill changes**: run the new eval (and at least the existing `Compono.Http`-
  related evals, e.g. any header/verification-focused ones already in
  `evals.json`, for regression) against both the pre-change and
  post-change `http.md`/`SKILL.md` content, in clean/isolated contexts —
  same method this repo's own prior skill-maintenance pass used (fresh,
  context-isolated runs, graded against each eval's `expectations` list,
  refine guidance rather than weaken the eval if a real gap surfaces).
  Record baseline vs. updated results in this Plan's Notes section once
  run.

### 9. `alexa-vox-craft` dogfood validation

- [x] Rewrite `SmapiDeveloperAccessTokenProviderTests.GetAccessTokenAsync_SendsFormUrlEncodedContent`
  per ADR-0062's Worked Example 3 — real field assertions
  (`grant_type`/`refresh_token`/`client_id`/`client_secret`) via
  `WithFormBody`, replacing the `req.Content is FormUrlEncodedContent`
  type-check. This is the primary, real dogfood validation target — not a
  manufactured example.
- [x] **Confirmed during this Plan's own read-only inspection (not
  invented here): no other natural `WithHeader`/`WithJsonBody<T>`
  opportunity exists in `alexa-vox-craft` today.** `LocaleHandlerTests`
  inspects the `Accept-Language` header via `innerHandler.Requests`
  (post-hoc request-log inspection, ADR-0051's own "kept separate" design
  — not a matching concern `WithHeader` would replace). Every other
  `OnPost`/`When(req => req.Method == HttpMethod.Post)` site in
  `AlexaSkillInvocationClientTests` matches method/path only and never
  asserts on outgoing JSON body content — introducing a `WithJsonBody<T>`
  assertion there would be adding a *new* assertion the original test
  never made, not migrating an existing weaker one. **Do not manufacture
  such an example** — the OAuth form test is the one real, evidenced
  target.
- [x] Run `scripts/dogfood-validate.sh --consumer-repo
  /Users/ncipollina/source/repos/layered-craft/alexa-vox-craft
  --packages "Compono Compono.Http"` (or the equivalent
  `DOGFOOD_PACKAGES`/`--packages` invocation actually needed — confirm the
  exact package list against `Compono.Http`'s real dependency graph,
  which per ADR-0051/RESEARCH-0024 is `Compono` + `Compono.Http` only, no
  `Compono.NSubstitute`/`Compono.TestDoubles` needed for this specific
  test) against a **freshly packed local build of the working tree**,
  confirming the consumer resolves the exact freshly-packed version (not
  a stale cache hit) for both packages, and that `alexa-vox-craft`'s full
  test suite — not just the rewritten test — passes.
- [x] Record the dogfood run's before/after in this Plan's Notes: before
  (type-check only, passes), after (real field-value assertions, passes,
  proving the new matcher actually observes the real production code's
  real form-encoded values).

### 10. Final full validation

- [x] `dotnet build` — 0 warnings, 0 errors, full solution.
- [x] `dotnet test` — full solution green, including the new
  `Compono.Http.Tests` matrix (task 5) and regression coverage (task 4).
- [x] Package-validation script (`inspect-packed-nupkgs.sh` or this repo's
  current equivalent — confirm current name/location) green for
  `Compono.Http` and every package it's bundled with in CI's package-
  validation matrix.
- [x] Re-confirm `scripts/dogfood-validate.sh` (task 9) against the
  **exact final working tree** being proposed for merge — per this repo's
  own "Consumer/dogfood validation gate" rule
  (`CLAUDE.md`/`AGENTS.md`), a dogfood pass performed before the last
  substantive change doesn't authorize a push; if anything changes after
  the task-9 run, rerun it.

## Critical Files

- `src/Compono.Http/AsyncRequestMatcher.cs` (new) — the internal matcher
  delegate.
- `src/Compono.Http/HttpResponseRegistrationBuilder.cs` — accumulating
  matcher list, `WithHeader`/`WithBody`/`WithFormBody`/`WithJsonBody<T>`
  (×2) added, `Finish()`'s compilation step.
- `src/Compono.Http/HttpResponseRegistration.cs` — `Matches` becomes
  `ValueTask<bool>`-returning, `internal`, cancellation-aware.
- `src/Compono.Http/TestHttpHandler.cs` — `SendAsync`'s matching loop
  becomes `await`-capable; new public `WhenAsync`.
- `test/Compono.Http.Tests/TestHttpHandlerTests.cs` — new-behavior matrix
  (task 5) and compatibility regression tests (task 4).
- `test/Compono.Http.AotSmokeTest/Program.cs` — extended to exercise
  `WithHeader`/`WithFormBody`/`WithBody`/`WithJsonBody<T>(...,
  JsonTypeInfo<T>)` under real `PublishAot=true` (Proof B; the
  `JsonSerializerOptions?` overload deliberately excluded here).
- `test/Compono.Http.AotSmokeTest/AnalyzerContract/` — two new sibling
  caller projects (`WithJsonBodyOptionsOverloadCaller`,
  `WithJsonBodyTypeInfoOverloadCaller`) plus an extension to
  `verify-analyzer-contract.sh` (Proof A).
- `docs/packages/compono-http.md`, `skills/compono/references/http.md`,
  `skills/compono/SKILL.md`, `skills/compono/evals/evals.json`.
- `test/AlexaVoxCraft.Smapi.Tests/Auth/SmapiDeveloperAccessTokenProviderTests.cs`
  (`alexa-vox-craft`, external repo — dogfood target, task 9).

## Test Plan

Per `references/testing.md` (xUnit v3 on Microsoft Testing Platform, AAA
with blank-line separation, handwritten explicit test data):

- **Compatibility regression** (task 4) — proves every pre-existing
  guarantee (precedence, unmatched-request, `Requests` ordering,
  `Verify()` counts, response freshness, concurrency) survives under the
  now-async-capable dispatch path specifically, not merely "old tests
  still pass unchanged."
- **New behavior** (task 5) — full coverage of `WithHeader` (including the
  corrected two-collection semantics and redaction), `WithBody`,
  `WithFormBody` (including the corrected `+`/percent-decoding and the
  `Content-Type` requirement), `WithJsonBody<T>` (both overloads, the
  `Content-Type` requirement, and the malformed-JSON-propagates
  exception rule), `WhenAsync` (including cancellation), and
  chaining/short-circuit order.
- **AOT — two independent proofs** (task 6): Proof B, the extended
  `Compono.Http.AotSmokeTest` smoke test, real `PublishAot=true` publish
  and run, exercising only the AOT-safe surface (including
  `WithJsonBody<T>(..., JsonTypeInfo<T>)`) and staying warning-clean;
  Proof A, the extended `AnalyzerContract` build-time analyzer-contract
  pair, proving `WithJsonBody<T>(..., JsonSerializerOptions?)` genuinely
  warns (IL2026+IL3050) at the consumer's call site while its
  `JsonTypeInfo<T>` sibling stays clean — run separately, never mixed into
  Proof B's clean path or the ordinary `dotnet build`/`dotnet test` gate.
- **Finalized-builder immutability** (task 4) — a post-`Finish()`
  `WithX(...)` call throws the existing `_finished`-guard exception, and
  the already-returned registration's matching behavior is provably
  unaffected by the rejected call.
- **Dogfood** (task 9) — the real `alexa-vox-craft` OAuth test rewritten
  and passing against a freshly-packed local build via
  `scripts/dogfood-validate.sh`.
- **Skill eval** (task 8a) — baseline-vs-updated run for the new eval plus
  existing `Compono.Http`-relevant evals, results recorded in Notes.

## Notes

**Implementation complete (2026-09-11).** All 10 task groups done in one
change, per this Plan's own one-PR scope.

- **Internal matcher/builder:** `AsyncRequestMatcher` (new file), builder
  accumulates `List<(AsyncRequestMatcher, string)>`, `AddMatcher` enforces
  `_finished` via the pre-existing guard/exception, `Finish()` snapshots
  to an array before compiling the AND-chain matcher. `TestHttpHandler.SendAsync`
  is now genuinely `async`, awaiting each registration's matcher in the
  existing last-registered-first loop.
- **Compatibility regressions (task 4):** confirmed the existing 33
  pre-change tests all still pass unmodified, then added 7 new regression
  tests specifically exercising last-match-wins/unmatched-request/
  `Requests`-ordering/`Verify()` counts/response-freshness/concurrency
  *under a registration that actually uses a chained condition* — none of
  the 33 pre-existing tests exercised the new async-capable path, exactly
  as this Plan predicted.
- **New-behavior matrix (task 5):** 39 new tests added (`WithHeader` incl.
  the exact both-collections-different-value scenario ADR-0062's own
  review caught; `WithBody`; `WithFormBody` incl. all 5 corrected decoding
  cases and the Content-Type gate; `WithJsonBody<T>` both overloads incl.
  the Content-Type gate and malformed-JSON-propagates rule; `WhenAsync`
  incl. cancellation; chaining/short-circuit; post-`Finish()` mutation
  rejection). Total `Compono.Http.Tests`: 72 tests × 4 TFMs = 288, all
  green on first full run after implementation.
- **Public-API-surface lock:** deliberately **not** added — see the
  struck-through task 5 bullet above for why (this package's surface
  isn't the fixed, tiny kind `Compono.TestDoubles`' lock test protects).
- **AOT — both proofs green:** Proof B (`Compono.Http.AotSmokeTest`,
  real `dotnet publish -f net10.0 -p:PublishAot=true`) extended to
  exercise `WithHeader`/`WithFormBody`/`WithBody`/`WithJsonBody<T>` via
  `JsonTypeInfo<T>`/`WhenAsync` — published and ran clean, zero
  unexpected warnings. Proof A (`AnalyzerContract/`, two new sibling
  caller projects `WithJsonBodyOptionsOverloadCaller`/
  `WithJsonBodyTypeInfoOverloadCaller`, `verify-analyzer-contract.sh`
  extended) — confirmed `WithJsonBody<T>(..., JsonSerializerOptions?)`
  surfaces IL2026+IL3050 at the consumer call site and the `JsonTypeInfo<T>`
  sibling stays clean, mirroring `RespondJson<T>`'s already-proven
  contract exactly.
- **Skill eval baseline-vs-updated (task 8a):** new eval 57
  (`skills/compono/evals/evals.json`) run against pre- and post-change
  `http.md` in isolated contexts. Baseline fell back to a hand-rolled
  workaround (`handler.Requests` + manual `System.Web.HttpUtility.ParseQueryString`
  parsing) — exactly the negative pattern this eval targets. Updated
  correctly reached for `.WithFormBody(...)`, asserted real field values,
  named no anti-pattern, and even proactively mentioned chaining
  `.WithHeader(...)` for a fuller scenario. No regression checked against
  other evals (this pass touched only `http.md`, `SKILL.md` untouched
  since its existing `Compono.Http` routing entry already covers this).
- **Dogfood (task 9) — real bug found and fixed during validation, not
  just a pass/fail check.** The rewritten `alexa-vox-craft` test initially
  used `handler.OnPost("https://api.amazon.com/auth/o2/token")` (the full
  absolute URI) — `OnPost(string path)` actually matches
  `request.RequestUri.PathAndQuery` only (`/auth/o2/token`), a fact this
  Plan's own author (and every doc example written before real dogfooding)
  had gotten wrong by analogy with `When`'s full-URI comparison in a
  *different* pre-existing test in the same file. The first
  `dogfood-validate.sh` run caught this immediately
  (`UnmatchedHttpRequestException`) — fixed to `OnPost("/auth/o2/token")`,
  and the identical mistake was found and fixed in this same session's own
  ADR-0062/RESEARCH-0030/`http.md`/`compono-http.md` worked examples,
  which had all copied the same wrong path. Second `dogfood-validate.sh`
  run: full `alexa-vox-craft` suite green (2760/2760) against freshly
  packed local `Compono`/`Compono.Http`, version-resolution confirmed
  exact-match, no stale cache hit.
- **No other natural `WithHeader`/`WithJsonBody<T>` dogfood opportunity**
  was found or manufactured — confirmed per task 9's own pre-recorded
  read-only inspection.
- **Docs/skill:** `docs/packages/compono-http.md`, XML docs on every new
  member, `skills/compono/references/http.md` all updated. `docs/adr/0062-...md`,
  `docs/research/0030-...md` also corrected for the same
  `OnPost`-path-vs-full-URI mistake found during dogfooding, for
  consistency across every worked example in the record.
- **Final validation, all green:** full solution `dotnet build` (0
  warnings/0 errors), full solution `dotnet test` (3893/3893),
  `.github/scripts/inspect-packed-nupkgs.sh` against all 12 packed
  packages (all assertions passed), both AOT proofs, both eval runs,
  `scripts/dogfood-validate.sh` (final, post-fix run, green).
