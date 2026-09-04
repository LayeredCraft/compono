# [PLAN-0051] Compono.Http: Handler-Based HTTP Client Testing Package

**Status:** Done

**Implements:** [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)

## Goal

A new `Compono.Http` package ships a `TestHttpHandler`
(`HttpMessageHandler` subclass), reflection-free, depending only on core
`Compono`, giving consumers `OnGet`/`OnPost`/etc. + `When(...)` request
matching, last-match-wins dispatch, strict unmatched-request behavior,
registration-handle verification, fresh-per-invocation responses
(including AOT-honest JSON), a caller-owned lifecycle, and a thread-safe
request log — per ADR-0051's Decision Outcome in full.

Done when: `alexa-vox-craft`'s real 41-call-site `ReturnsResponse`
reflection workaround is fully replaced by `Compono.Http`, its full test
suite passes against freshly packed local packages (not just Compono's
own suite), every behavior in ADR-0051's Decision Outcome has a passing
automated test proving it (including the empirically-verified JSON/AOT
attribute-propagation behavior), `skills/compono` (`SKILL.md` + a new
`references/http.md`) teaches an agent `Compono.Http`'s package boundary,
matching semantics, unmatched behavior, verification model, and
caller-owned lifetime — not just that the package exists — **and**
`alexa-vox-craft`'s broader test-composition surface has been migrated
toward the Compono ecosystem wherever that migration is clean and
supported (AutoFixture/`AutoFixture.AutoNSubstitute` → `Compono`,
NSubstitute → `Compono.TestDoubles`, the reflection-based HTTP
infrastructure → `Compono.Http`, and `Compono.Bogus` only where a real
semantic-data need justifies it), with every blocked migration attempt
reported as an explicit, classified gap rather than silently worked
around. The skill update (task 12a) and the broader ecosystem migration
(task 10) are both part of this plan's completion criteria, not optional
follow-up work — this plan's dogfood goal is not merely "prove
`Compono.Http` works" but "use `Compono.Http` as the trigger for a
realistic consumer migration, and surface any gap that prevents it."
ADR-0051 itself is unchanged by this — it defines `Compono.Http` only;
the broader migration's architecture and findings are this plan's and
its dogfood research document's scope (task 10g). **The generalized,
multi-package `scripts/dogfood-validate.sh` (task 11) is itself a
required shipping artifact of this plan** — a committed, reusable tool
parameterized over consumer repo/solution/configuration/package set, not
an ad-hoc command sequence documented only in this plan's prose.

## Scope

Exactly ADR-0051's Decision Outcome — see that ADR for the full
rationale behind each choice; this plan does not re-derive it. Summary of
what's in scope, one bullet per ADR-0051 section:

- `Compono.Http` package, depending only on `Compono`.
- `TestHttpHandler : HttpMessageHandler`, `[Shared]`-composable.
- `OnGet`/`OnPost`/`OnPut`/`OnPatch`/`OnDelete`(`Match<string> path`) +
  `When(Func<HttpRequestMessage, bool>)`.
- Ordered, append-only registration list; last-registered-first, first
  match wins.
- Strict unmatched-request behavior: `UnmatchedHttpRequestException`,
  never a fabricated response; unmatched requests still recorded.
- `HttpResponseRegistration` handle: response-factory state (never a
  stored instance), `Interlocked`-counted matches, `Verify()` returning
  `CallVerifier`.
- `Respond(HttpStatusCode)`, `RespondText(...)`,
  `RespondJson<T>(value, JsonSerializerOptions? options = null)`,
  `RespondJson<T>(value, JsonTypeInfo<T>)`, `Throws(Exception)` (same
  instance rethrown every match).
- JSON serialized once to immutable bytes at registration; fresh
  `ByteArrayContent` per invocation.
- `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` on the
  `JsonSerializerOptions` overload; neither on the `JsonTypeInfo<T>`
  overload; no internal suppression.
- `Requests: IReadOnlyList<HttpRequestMessage>` — raw references,
  snapshot-per-access, recorded before matching, arrival order preserved.
- `CreateClient(Uri? baseAddress = null)`, always `disposeHandler: false`;
  caller owns/disposes both clients and the handler; Compono composition
  never owns or disposes the handler.
- Concurrency: concurrent `SendAsync` supported; configuration concurrent
  with sends unsupported/not guaranteed; registration list stays a plain
  `List<T>`; match counts and request log are the only state requiring
  real synchronization.

Beyond ADR-0051's own Decision Outcome, this plan also scopes in the
`skills/compono` agent-skill update (task 12a) as a required deliverable
— the skill and `docs/packages/compono-http.md` are separate product
surfaces (one teaches a human reader, the other teaches an agent), and
shipping only the former leaves the skill silently unaware
`Compono.Http` exists.

This ships as **one coherent implementation PR** — no artificial phase
split. If implementation research surfaces a genuinely independent seam
(one that could ship and be reviewed on its own without the rest of the
package being meaningful), that's a plan amendment to record at the time,
not something to pre-guess here.

**Explicitly out of scope for this plan** — do not implement any of
these, even if implementation makes one look easy to add along the way:

- `IHttpClientFactory`/named-client/typed-client integration or any
  `Microsoft.Extensions.Http` dependency/helper.
- Header/query-string/JSON-body dedicated matcher types (the `When(...)`
  predicate escape hatch is the only mechanism for these).
- Async request-matching predicates (`Func<HttpRequestMessage, Task<bool>>`).
- Retry/Polly-aware testing behavior.
- Callback-based responses, delayed/latency-simulated responses,
  sequential/queued responses per registration.
- WireMock-style stateful scenarios.
- Call-order verification (only count-based `Never`/`Once`/`Exactly`).
- A strict/loose unmatched-request mode toggle.
- A raw `HttpResponseMessage`-accepting `Respond(HttpResponseMessage)`
  overload.
- Any change to core `Compono` (`Match<T>`, `CallVerifier`,
  `CompositionRow`/`[Shared]`) — all reused unmodified.
- A new Compono disposable-scope/lifetime mechanism — `TestHttpHandler`
  stays caller-owned per ADR-0051; composition-driven auto-disposal is a
  separate, out-of-scope core-capability decision.

## Tasks

### 1. Package/project creation

- [x] Create `src/Compono.Http/Compono.Http.csproj` — `TargetFrameworks`
      matching the repo's current sweep (`net8.0;net9.0;net10.0;net11.0`,
      confirmed against `Compono.DependencyInjection.csproj`), `LangVersion latest`,
      `ImplicitUsings enable`, `Nullable enable`, package `Title`/`Description`.
- [x] `ProjectReference` to `..\Compono\Compono.csproj` only —
      `PrivateAssets="none"` per the existing integration-package pattern
      (`Compono.DependencyInjection.csproj`'s comment explains why: so the
      dependency flows through to the packed nupkg).
- [x] Copy the `PinProjectReferenceVersionsExact` MSBuild target from
      `Compono.DependencyInjection.csproj`/`Compono.XunitV3.csproj` (ADR-0031/
      PLAN-0008 Phase 0 pattern) so the packed `Compono.Http` nupkg pins an
      exact `Compono` version match.
- [x] `InternalsVisibleTo` for `Compono.Http.Tests`.
- [x] No `PackageReference` to `Microsoft.Extensions.DependencyInjection.Abstractions`,
      `Microsoft.Extensions.Http`, `Moq`, or `NSubstitute` anywhere in this
      project — confirmed via the same "why not" comment style
      `Compono.DependencyInjection.csproj` uses (see the csproj's `ItemGroup`
      comment), not silent omission.
- [x] Added `src/Compono.Http/Compono.Http.csproj` and
      `test/Compono.Http.Tests/Compono.Http.Tests.csproj` to `Compono.slnx`
      (this repo has no local CI YAML - `Directory.Build.props`'
      own comment confirms CI is a shared external devops-templates workflow,
      so the solution file is the actual local wiring mechanism).
      `test/Compono.Http.AotSmokeTest` (and its `AnalyzerContract/`
      sub-projects, task 5) is deliberately **not** added to `Compono.slnx`,
      matching the pre-existing, already-unlisted
      `Compono.AotSmokeTest`/`Compono.TUnit.AotSmokeTest`/
      `Compono.TestDoubles.AotSmokeTest` convention - a manual, one-shot
      proof driven by its own `pack-compono.sh` + `dotnet publish`, not
      part of `dotnet build Compono.slnx`. No CPM
      `PackageVersion` entry needed in `Directory.Packages.props` - like
      `Compono.DependencyInjection`, this package has zero external
      `PackageReference`. `docs/packages/`/skill wiring is task 12/12a, not
      this task.

### 2. `TestHttpHandler`

- [x] `TestHttpHandler : HttpMessageHandler`, overriding
      `SendAsync(HttpRequestMessage, CancellationToken)`.
- [x] `OnGet`/`OnPost`/`OnPut`/`OnPatch`/`OnDelete(Match<string> path) : HttpResponseRegistrationBuilder`
      (or equivalent fluent return — exact type shape is an implementation
      detail, not a plan-level decision) — method fixed by helper name,
      `path` matched via `Match<string>.Matches(request.RequestUri.PathAndQuery)`
      (confirm exact URI component matched — `PathAndQuery` per the real
      `alexa-vox-craft` evidence in ADR-0051/research §1).
- [x] `When(Func<HttpRequestMessage, bool> predicate) : HttpResponseRegistrationBuilder`.
- [x] Ordered `List<HttpResponseRegistration>` (plain `List<T>`, per the
      narrowed concurrency contract — no lock on this list itself).
- [x] Dispatch: record request into the log first, unconditionally; walk
      the list last-registered-first; on first match, invoke its response
      factory and `Interlocked.Increment` its count; on no match, throw
      `UnmatchedHttpRequestException` (method, URI, "no configured
      registration matched" in the message).
- [x] **Disposal — honor `HttpMessageHandler`'s own inherited disposal
      contract; do not add redundant `IDisposable` machinery.**
      `HttpMessageHandler` already implements `IDisposable`;
      `TestHttpHandler` does not need (and must not add) its own
      independent `IDisposable` implementation on top of it.
      - Override `Dispose(bool disposing)` only if handler-owned state
        genuinely needs cleanup on disposal, or an explicit
        already-disposed guard is needed before `SendAsync` acts on
        registrations/the request log — if neither is true, don't
        override it at all and let the base class's behavior stand as-is.
      - `SendAsync` called after disposal must behave per that contract:
        throw `ObjectDisposedException`, matching `HttpMessageHandler`'s
        own base behavior — whether that's inherited for free or requires
        an explicit disposed check depends on what the base class already
        does, confirm at implementation time rather than assuming a guard
        is needed.
      - Task 7's caller-owned lifetime rules (who calls `Dispose()` and
        when) are unchanged by this — this bullet is about *how*
        `TestHttpHandler` implements disposal behavior once called, not
        *who* is responsible for calling it.

### 3. `HttpResponseRegistration`

- [x] One instance per `OnX(...)`/`When(...)` call — holds the compiled
      matcher (`Func<HttpRequestMessage, bool>`, uniform internal shape
      regardless of which public entry point produced it), the response
      factory (`Func<HttpRequestMessage, HttpResponseMessage>`), and an
      `int` match count field.
- [x] Match count mutated only via `Interlocked.Increment` — mirrors
      `ReturnConfig<T>.RecordCall()`'s existing pattern in
      `src/Compono/ReturnConfig.cs`.
- [x] `public CallVerifier Verify()` — wraps the count and a member
      description (method + path/predicate description) in the existing,
      unmodified `CallVerifier` from `src/Compono/CallVerifier.cs`. No new
      verification type. **PR-review amendment (2026-08-24)**: the
      original single `OnX(Match<string> path)` shape discarded `path`
      when building this description (every literal-path registration
      produced the same generic `"GET request"` text) — Codex review
      caught this on the implementation PR. Fixed by splitting each `OnX`
      into `OnX(string path)` (retains the literal path verbatim in the
      description, e.g. `GET /v1/customers/42`) and `OnX(Match<string>
      path)` (unchanged signature for `Match.Any`/`Match.Is`, honestly
      generic description since `Match<T>`'s deliberate opacity means
      Compono.Http can't tell those two apart) — a small, source-compatible
      overload split, not a design change. Recorded as
      [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)'s
      Amendment 1, with regression tests added
      (`TestHttpHandlerTests.cs`: `TwoLiteralGetRegistrations_...`,
      `OnGet_MatchAny_VerifyFailureMessage_...`,
      `OnGet_MatchIs_VerifyFailureMessage_...`). A second PR-review finding
      in the same round — `RespondJsonBytes` reused a shared `static
      readonly MediaTypeHeaderValue` across every response, so mutating one
      response's `Content.Headers.ContentType` silently affected every
      other response — was also fixed (a fresh `MediaTypeHeaderValue` per
      response) with its own regression test
      (`RespondJson_MutatingOneResponsesContentType_...`). Re-ran the full
      gate after this fix, per the standing rule below: `dotnet test
      Compono.slnx -c Release` — 2490/2490 (16 more than the prior 2474,
      the 4 new regression tests × 4 TFMs); both AOT proofs — PASS; fresh
      dogfood run vs. `alexa-vox-craft` (`0.0.0-local.20260824160923-28165-4975`) —
      2816/2784/32/0, consumer dirty state (28 files) unchanged before/after.
      **Second PR-review round (2026-08-24)**: two more Codex findings.
      (1) `Finish` (`HttpResponseRegistrationBuilder.cs`) had no guard
      against being called twice on the same retained builder — a second
      `Respond*`/`Throws` call silently overwrote the first registration's
      response factory and re-added the same `HttpResponseRegistration` to
      the handler's list a second time, so a caller holding onto the
      "first" handle would see it silently start returning the "second"
      response. Fixed with a `_finished` guard throwing
      `InvalidOperationException` on reuse, with two regression tests
      (`FinalizingTheSameBuilderTwice_ThrowsInvalidOperationException`,
      `FinalizingTheSameBuilderTwice_FirstRegistrationHandleKeepsItsOriginalResponse`).
      (2) `test/Compono.Http.AotSmokeTest/AnalyzerContract/verify-analyzer-contract.sh`
      only cleared `pack-compono.sh`'s own restore cache, not the two
      analyzer caller projects' own separate, isolated
      `RestorePackagesPath` caches — since every pack reuses the fixed
      version `1.0.0`, a rerun after editing `Compono.Http` source could
      silently keep validating a stale extracted copy and report a false
      PASS. Fixed by clearing both callers' `obj/.nuget-packages` before
      building. Re-ran the full gate again: `dotnet test Compono.slnx -c
      Release` — 2498/2498; Proof A (rerun with the cache fix in place,
      confirming it now genuinely rebuilds against current source) — PASS;
      Proof B — PASS; fresh dogfood run vs. `alexa-vox-craft`
      (`0.0.0-local.20260824162119-31825-24977`) — 2816/2784/32/0, consumer
      dirty state (28 files) unchanged before/after.
      **Third PR-review round (2026-08-24)**: three Codex findings, all
      documentation/CI-wiring gaps rather than `Compono.Http` code defects
      — the package was never actually wired into this repo's own package-
      readiness gate or docs pipeline once shipped. (1)
      `.github/workflows/package-validation.yaml` and
      `.github/scripts/inspect-packed-nupkgs.sh` had hardcoded seven-
      package lists that never included `Compono.Http` — it was never
      baseline-checked, never CS1591-enforced, never `.nupkg`-content-
      inspected by the pre-merge gate. Fixed: added to both scripts'
      package lists/loops, plus `inspect-packed-nupkgs.sh`'s own
      `Compono.Http` case branch (title, exact-pin `Compono` dependency,
      no third-party dependency to range-assert). No new "local-feed
      packed-consumer smoke test" step added — `Compono.Http` has no
      `SampleTests` project (unlike `Compono.XunitV3`/`Compono.TUnit`/
      `Compono.TestDoubles`, which do), the same situation
      `Compono.NSubstitute`/`Compono.Bogus`/`Compono.DependencyInjection`
      are already in, so this matches existing precedent rather than
      inventing new infrastructure. (2) `mkdocs.yml`,
      `.github/scripts/generate-api-reference.sh`, and
      `.github/workflows/docs.yml` all still only knew about the prior
      seven packages — no nav entry, no generated API reference, and
      `src/Compono.Http/**` changes wouldn't even trigger the docs
      workflow. Fixed: added `Compono.Http` to `mkdocs.yml`'s Package
      Guides/API Reference nav, `generate-api-reference.sh`'s
      `integration_pkgs` array (also fixed two now-doubly-stale "four
      publishable"/"three integration packages" comments in the same
      file, predating this PR), and both of `docs.yml`'s path triggers
      plus its build loop and drift-check error message; regenerated
      `docs/reference/api/Compono.Http/` for real (`dotnet build` all
      eight packages, then `generate-api-reference.sh`) — confirmed via
      `git status` that no *other* package's generated pages drifted, only
      `Compono.Http`'s were added — and ran `uv run mkdocs build --strict`
      clean (exit 0, no broken nav/links). (3) Several canonical current-
      state docs still said "seven" and omitted `Compono.Http`:
      `docs/roadmap/index.md` (Today's package list — also corrected the
      wording to say `Compono.Http`, like `Compono.DependencyInjection`,
      didn't graduate from `future-packages.md`'s own Gate A/Gate B
      candidate list — it came from a dedicated admission research doc
      instead), `docs/roadmap/future-packages.md` (intro package count +
      a new explanatory paragraph matching the existing
      `Compono.DependencyInjection` one), `docs/public-api.md`,
      `docs/contributing.md`, `docs/documentation-architecture.md` (two
      separate stale counts), `docs/packages/index.md` (one more "seven"
      instance in its own Version Compatibility section, missed in this
      plan's original task 12 pass), and `docs/getting-started/installation.md`
      (added a `Compono.Http` install line for consistency with the other
      "add as your tests need it" packages, not itself named by the
      review but directly adjacent). Re-ran the full gate a third time:
      `dotnet test Compono.slnx -c Release` — 2498/2498 (unchanged, no
      test code touched this round); `dotnet build
      src/Compono.Http/Compono.Http.csproj -c Release
      -p:WarningsAsErrors=CS1591` — clean, matching the CI gate's own
      enforcement step; Proof A — PASS; Proof B — PASS; fresh dogfood run
      vs. `alexa-vox-craft` (`0.0.0-local.20260824201408-40021-18964`) —
      2816/2784/32/0, consumer dirty state (28 files) unchanged
      before/after.
- [x] `registration.Verify().Never()/.Once()/.Exactly(n)` all work
      unchanged via the reused `CallVerifier` API.

### 4. Response APIs

- [x] `Respond(HttpStatusCode)` — fresh `HttpResponseMessage` per call, no
      content.
- [x] `RespondText(string content, string mediaType = "text/plain", Encoding? encoding = null)`
      (confirm exact default `mediaType`/encoding against real
      `alexa-vox-craft` evidence, if any beyond JSON, at implementation
      time) — fresh `StringContent` per call.
- [x] `RespondJson<T>(T value, JsonSerializerOptions? options = null)` —
      serializes once at registration time via
      `JsonSerializer.SerializeToUtf8Bytes(value, options)` (or the
      `Serialize`+`Encoding.UTF8.GetBytes` equivalent, whichever avoids an
      intermediate string most cleanly) to an immutable `byte[]`; each
      invocation constructs a fresh `ByteArrayContent(bytes)` with
      `Content.Headers.ContentType` explicitly set
      (`application/json; charset=utf-8`) per instance.
- [x] `RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)` — same
      once-serialized-bytes model, via the `JsonTypeInfo<T>`-based
      `Serialize` overload.
- [x] `Throws(Exception exception)` — response factory becomes `_ =>
      throw exception;`, same instance rethrown on every match (verified
      behavior, ADR-0051 — no cloning, no factory parameter).
- [x] Every `Respond*`/`Throws` call finalizes and returns the
      `HttpResponseRegistration` handle (not `void`).
- [x] `RespondBytes(byte[] content, string mediaType = "application/octet-stream")`
      (ADR-0051 Amendment 2, added after this plan's original `Done`
      status) — `content` is defensively copied (`(byte[])content.Clone()`)
      once at registration time, not retained by reference; each
      invocation constructs a fresh `ByteArrayContent` over that private
      copy with its own `MediaTypeHeaderValue`, matching `RespondJson`'s
      serialize-once-to-bytes model.

### 5. JSON/AOT correctness

- [x] `RespondJson<T>(T value, JsonSerializerOptions? options = null)`
      carries `[RequiresDynamicCode(...)]` and `[RequiresUnreferencedCode(...)]`,
      using `System.Text.Json`'s own attribute message text (naming the
      `RespondJson<T>(T, JsonTypeInfo<T>)` overload as the AOT-safe
      alternative).
- [x] `RespondJson<T>(T value, JsonTypeInfo<T> jsonTypeInfo)` carries
      **neither** attribute.
- [x] No `[UnconditionalSuppressMessage]` anywhere in
      `Compono.Http`'s implementation.
- [x] **Two separate proofs, not one — do not conflate them.** Proof A is
      a static/analyzer-contract check (does the right warning appear at
      the right call site); Proof B is a real native-executable proof
      (does the AOT-safe path actually publish and run under Native AOT).
      They test different things and have different success criteria —
      collapsing them into one project/assertion risks silently requiring
      the *options* overload to be warning-free under `PublishAot`, which
      is not its contract (its contract is to advertise the framework's
      real constraint honestly, per ADR-0051 — see §12's "Attribute
      propagation, verified empirically").

      **Proof A — analyzer-contract proof, implemented and passing**
      (`test/Compono.Http.AotSmokeTest/AnalyzerContract/`: two sibling
      throwaway console projects, `OptionsOverloadCaller` and
      `JsonTypeInfoOverloadCaller`, each `IsAotCompatible=true` and
      referencing `Compono.Http` via `PackageReference` against a locally
      packed nupkg — **not** `ProjectReference`, and each declaring
      `<TargetFrameworks>net10.0</TargetFrameworks>` (plural, matching
      `test/Directory.Build.props`' own property name), **not** singular
      `<TargetFramework>`; `verify-analyzer-contract.sh` packs, builds
      each with `-p:WarningsAsErrors="IL2026%3BIL3050"`, and asserts the
      first build fails with exactly those diagnostics while the second
      succeeds warning-free) confirms:
      - `OptionsOverloadCaller` (calling `RespondJson(value, options)`)
        surfaces IL2026 + IL3050 **at that consumer's own call site**;
      - `JsonTypeInfoOverloadCaller` (calling `RespondJson(value,
        jsonTypeInfo)`) produces **zero** IL2026/IL3050 warnings;
      - both captured as an automated, CI-checkable pass/fail script, not
        eyeballed once and left undocumented.

      **Two real implementation discoveries surfaced empirically while
      building this proof, not assumed in advance:**
      1. **`Compono.Http.csproj` itself needs `<IsAotCompatible>true</IsAotCompatible>`.**
         The .NET trim/AOT analyzer only enforces a `Requires*` attribute
         at a *consumer's* call site for a member defined in an assembly
         marked trimmable — without this, `RespondJson(value, options)`
         silently produced **zero** warnings anywhere, defeating the
         entire point of the attributes. Adding it doesn't affect
         `Compono.Http`'s own build (still 0 warnings — the method's own
         attribute already covers its one internal unsafe call, per the
         same behavior verified in ADR-0051's original spike) but is
         required for consumers to see the warning. See
         `Compono.Http.csproj`'s own comment.
      2. **A caller project must declare plural `TargetFrameworks`, not
         singular `TargetFramework`**, when overriding `test/Directory.Build.props`'
         4-TFM sweep down to one TFM. Setting singular `TargetFramework`
         instead leaves the props' `TargetFrameworks` (plural) value
         still in effect alongside it — verified empirically that this
         dual state silently disables the AOT/trim analyzer (zero
         diagnostics, no build error, `EnableAotAnalyzer`/`IsAotCompatible`
         still both reporting `true`) with nothing to signal why.
         `test/Compono.TestDoubles.AotSmokeTest.csproj` (and this plan's
         own `Compono.Http.AotSmokeTest.csproj`) already used the correct
         plural form; the two new `AnalyzerContract/` caller projects
         needed the same fix. See `OptionsOverloadCaller.csproj`'s own
         comment for the full A/B-tested trail.

      **Proof B — Native AOT publish-and-run proof, implemented and
      passing** (`test/Compono.Http.AotSmokeTest`, following
      `test/Compono.TestDoubles.AotSmokeTest`'s established
      `PublishAot=true`-publish-and-run pattern against a packed local
      package via `pack-compono.sh`): uses **only** the
      `RespondJson(value, jsonTypeInfo)` overload — `Compono.Http.AotSmokeTest.csproj`
      explicitly excludes `AnalyzerContract/`'s own `.cs` files via
      `<Compile Remove>` (SDK-style implicit globbing would otherwise
      pull the sibling proof's `Program.cs` files into this project's
      compilation and fail with duplicate-attribute `CS0579` errors,
      discovered empirically the first time both proofs were run
      back-to-back) — and proves the actual packed `Compono.Http`
      package publishes and runs successfully with `PublishAot=true` on
      `osx-arm64`, exercising `OnGet`+`Match<string>`, last-match-wins,
      strict `UnmatchedHttpRequestException` (with `Requests` still
      recording the request that caused it), `registration.Verify()`,
      and `Throws`' same-instance rethrow, end to end through the real
      dependency chain. **The `JsonSerializerOptions`-based overload is
      never required to publish warning-free (or at all) under
      `PublishAot` in this or any proof** — that overload's correct
      behavior *is* surfacing the framework's real
      `RequiresDynamicCode`/`RequiresUnreferencedCode` constraint, not
      avoiding it.

### 6. Request logging

- [x] Backing store for `Requests`: a lock-guarded `List<HttpRequestMessage>`
      or `ConcurrentQueue<HttpRequestMessage>` — pick whichever gives the
      cleaner snapshot-on-read implementation; either satisfies the
      contract (implementation detail, not a plan-level decision).
- [x] `Requests` getter returns a fresh point-in-time copy
      (`IReadOnlyList<HttpRequestMessage>`, e.g. via `ToArray()`/
      `ImmutableArray.CreateRange`) on every access — never a live view
      over the mutable backing store.
- [x] Recording happens before matching is attempted, for every request
      including ones that go on to throw `UnmatchedHttpRequestException`.
- [x] Recording order matches arrival order (the order `SendAsync` was
      invoked), preserved even under concurrent `SendAsync` calls (i.e.
      the backing store's append is itself ordered/atomic per call, not
      reordered by the synchronization mechanism chosen).

### 7. Lifecycle/disposal

- [x] `CreateClient(Uri? baseAddress = null) : HttpClient` — always
      `new HttpClient(this, disposeHandler: false)`, with `BaseAddress`
      set from the parameter when provided. No overload or parameter ever
      sets `disposeHandler: true`.
- [x] No Compono composition hook disposes `TestHttpHandler` — confirm by
      a test asserting a `[Shared]`-composed handler is **not** disposed
      when its owning `CompositionRow`/scope goes out of scope (regression
      guard against accidentally wiring disposal in later).
- [x] Documented via XML doc (`TestHttpHandler`/`CreateClient`'s doc
      comments) that the caller must dispose both every `HttpClient` it
      creates and the handler itself; the package-doc restatement is
      task 12, not yet done.
- [x] Test: dispose the handler, then attempt `SendAsync` through an
      existing `HttpClient` still wrapping it → `ObjectDisposedException`.

### 8. Concurrency

- [x] Registration list stays a plain `List<HttpResponseRegistration>` —
      no lock added around it (per the narrowed contract: configuration
      isn't guaranteed concurrent with sends).
- [x] Match-count increments use `Interlocked.Increment` exclusively — no
      lock needed there.
- [x] Request log uses whichever synchronized structure task 6 settled on.
- [x] Concurrency test: many parallel `SendAsync` calls (against one
      shared `HttpClient`/handler) complete without exceptions,
      `Requests.Count` equals the number of calls made, and each matched
      registration's `Verify().Exactly(n)` reports the correct count — no
      lost updates.

### 9. Behavioral tests (`test/Compono.Http.Tests`)

- [x] Exact-path `OnGet`/etc. match and respond correctly.
- [x] `Match.Any<string>()` path matching.
- [x] `Match.Is<string>(predicate)` path matching.
- [x] Whole-request `When(predicate)` matching (method + URI + header +
      content-type combination, mirroring the real
      `FormUrlEncodedContent`-type-check evidence).
- [x] Each `OnGet`/`OnPost`/`OnPut`/`OnPatch`/`OnDelete` helper fixes its
      method correctly.
- [x] Last-match-wins: two overlapping registrations, later one wins.
- [x] Explicit catch-all fallback (`When(_ => true)`) composes correctly
      with a more specific override registered after it.
- [x] Unmatched request → `UnmatchedHttpRequestException` with method +
      URI + "no match" in the message.
- [x] Unmatched request still appears in `Requests` after the exception.
- [x] Repeated matches against one registration get a fresh
      `HttpResponseMessage`/content each time (assert no
      `ObjectDisposedException` reading content on the second/third
      match after the first response's content was read/disposed by a
      consumer).
- [x] `RespondJson` sets `Content-Type: application/json; charset=utf-8`
      correctly on every fresh `ByteArrayContent`.
- [x] `RespondBytes` round-trips content and defaults to
      `application/octet-stream`, honors a supplied `mediaType`, and a
      mutation to the caller's array after registration doesn't affect
      an already-registered response (ADR-0051 Amendment 2 — added after
      this plan's original `Done` status).
- [x] `Throws(exception)` rethrows the exact same instance
      (`ReferenceEquals`) on repeated matches.
- [x] `registration.Verify().Never()/.Once()/.Exactly(n)` — including the
      failure path (wrong count throws `TestDoubleVerificationException`,
      matching `CallVerifier`'s existing contract).
- [x] Concurrent `SendAsync`/request-recording correctness (task 8).
- [x] `Requests` snapshot stability — mutate/append after taking a
      `Requests` reference, assert the earlier reference didn't change.
- [x] Disposal semantics (task 7's tests, restated here for completeness
      of the test-plan enumeration — not duplicated work).

### 10. `alexa-vox-craft` dogfood migration (expanded scope)

**This task's goal is no longer just "prove `Compono.Http` works."** It
is: use `Compono.Http` as the trigger for a realistic migration of
`alexa-vox-craft`'s test composition toward the Compono ecosystem where
that migration is clean and supported, and surface — not silently paper
over — every real product gap that prevents it. This does **not** expand
ADR-0051 itself, which defines `Compono.Http` only; the migration's
findings live in this plan and a dogfood research document (task 10g),
the same way `docs/research/0008-...md` recorded `trivia-platform`'s
migration evidence for `Compono.TestDoubles` without touching that
capability's own ADR.

Target direction (not a mandate to force every test onto it — see 10c's
gap-handling rule and 10d's scope boundary):

```
AutoFixture / AutoFixture.AutoNSubstitute  -> Compono
NSubstitute (direct Substitute.For<T>())   -> Compono.TestDoubles
reflection-based HttpMessageHandler fakes  -> Compono.Http
hand-written/random fixture data           -> Compono.Bogus, only where
                                               real semantic-data value exists
```

#### 10a. Inventory (before making any change)

- [x] Inventory `alexa-vox-craft`'s full test-composition surface —
      AutoFixture references/customizations, `AutoFixture.AutoNSubstitute`
      usage, direct `Substitute.For<T>()` call sites, custom `AutoData`
      attributes (`test/AlexaVoxCraft.TestKit/Attributes/BaseFixtureFactory.cs`
      and its subclasses/siblings across test projects), fixture
      factories/builders/customizations, `[Frozen]` usage, custom specimen
      builders (including but not limited to the HTTP TestKit's), shared
      test profiles/base fixtures, manually generated semantic test data,
      existing hand-written fake/test-double classes, HTTP-specific
      helpers (task 10's original scope), and any place semantic-data
      generation (realistic names/emails/addresses/IDs/domain text) is
      already done by hand or via `AutoFixture` customization in a way
      `Compono.Bogus` might genuinely improve.
- [x] Classify every mechanism found into exactly one bucket, each with a
      concrete reason, not a guess:
      1. directly replaceable by core `Compono`;
      2. directly replaceable by `Compono.TestDoubles`;
      3. directly replaceable by `Compono.Http`;
      4. a good `Compono.Bogus` candidate (real semantic-data value, not
         "arbitrary values that happen to need generating");
      5. project-local setup that should remain project-local;
      6. an unsupported Compono capability / product gap;
      7. an intentional difference where the existing solution should
         remain (with the reason recorded, not just "left alone").
- [x] Do not mechanically delete or replace any inventoried mechanism
      before this classification exists — the inventory is a
      prerequisite to 10b, not parallel work. (Classification recorded in
      `docs/research/0010-...md` §14.)

#### 10b. Migration (in priority order, per 10d's scope boundary)

- [x] Removed `test/AlexaVoxCraft.Http.TestKit/Extensions/HttpMessageHandlerExtensions.cs`'s
      reflection-based `ReturnsResponse` — the entire `AlexaVoxCraft.Http.TestKit`
      project is deleted (all 4 of its files existed only to support the
      old reflection-based approach).
- [x] Migrated all real HTTP call sites across
      `AlexaInteractionModelClientTests.cs`, `AlexaSkillInvocationClientTests.cs`,
      `SmapiDeveloperAccessTokenProviderTests.cs`,
      `InSkillPurchasingClientTests.cs` to `Compono.Http`'s registration
      API. Assertion intent preserved per-file (spot-checked, not just
      "compiles and passes"): every `.Received()`/`.ReceivedCalls().Should().HaveCount(n)`
      became a real `registration.Verify().Once()`/`.Exactly(n)` —
      including fixing a latent gap in the original code, where
      `handler.Received();` as a bare statement (no chained assertion) was
      a silent no-op that never actually asserted anything; the migrated
      version genuinely verifies. Full 10g write-up with before/after
      detail still pending (tracked separately, not blocking this
      checkbox).
- [x] Where the inventory classified a mechanism as bucket 1/2/3, migrated
      it toward normal Compono usage rather than layering `Compono.Http`
      underneath the existing AutoFixture root — confirmed empirically,
      file by file:
      - `AutoDataAttribute`/custom `AutoData` subclasses (`SmapiClientAutoDataAttribute`,
        `SkillInvocationClientAutoDataAttribute`, `IspClientAutoDataAttribute`,
        `ClientAutoDataAttribute`) → `Compono.XunitV3`'s `[Compose]`/
        `[Compose<TProfile>]` (this repo's test framework is xUnit v3,
        confirmed);
      - `[Frozen] HttpMessageHandler` → `[Shared] TestHttpHandler`;
      - `HttpClientSpecimenBuilder`/`HttpClientSpecification`/the
        `Freeze<HttpMessageHandler>()` plumbing in `ClientAutoDataAttribute`
        (+2 subclasses) → deleted entirely, replaced by `[Shared] TestHttpHandler`
        composition (`Smapi.Tests`) and plain `[Compose]` (`InSkillPurchasing.Tests`,
        no shared profile needed there);
      - `InteractionModelDefinitionSpecimenBuilder`, `SkillRequestSpecimenBuilder`,
        `SkillInvocationResponseSpecimenBuilder`, `InSkillProductSpecimenBuilder`,
        `TransactionSpecimenBuilder` (and their `RequestSpecifications/`
        siblings) → deleted entirely; the domain types they hand-built
        (`InteractionModelDefinition`, `SkillRequest`,
        `SkillInvocationResponse<SkillResponse>`, `Product`,
        `ProductResponse`, `PurchasingEnabled`, `TransactionResponse`) all
        compose cleanly via plain Compono auto-composition with **zero**
        custom logic — confirmed by real compilation, not assumed (bucket 1,
        not bucket 6 — no capability gap for any of these).
      - `AutoNSubstitute`-generated interface doubles: not applicable here
        — none of the HTTP-touched clients had a substitutable interface
        dependency once `HttpClient` construction was handled directly;
        `Compono.NSubstitute` ended up not needed by any converted file
        (confirmed by removing the reference and rebuilding clean).
      - `Compono.Bogus`: not adopted for this slice — no bucket-4 semantic-
        data need surfaced in the HTTP-touched files themselves (the
        `amzn1.*`-prefixed ID pattern noted in 10a lives in shared
        `AlexaVoxCraft.TestKit` specimen builders, out of this pass's
        scope per 10d).
- [x] Do not keep NSubstitute in a shared fixture merely because it's
      already wired in, if `Compono.TestDoubles` can represent the real
      behavior needed — inertia is not bucket-7 justification. (Confirmed:
      `Compono.NSubstitute`/`Compono.TestDoubles` references were both
      speculatively added, then removed once the migrated files proved not
      to need either — recorded in `docs/research/0010-...md` §3.)
- [x] Do not remove NSubstitute by substituting a hand-written fake merely
      to make the dependency graph look cleaner. A hand-written fake
      replacing NSubstitute is acceptable **only** when it demonstrates a
      real, currently-unsupported `Compono.TestDoubles` capability — and
      that occurrence must be documented as a workaround/evidence item in
      10c's gap report, the same way `docs/research/0008-...md` documented
      `trivia-platform`'s `MultiStubLeaderboardRepository` workaround. (No
      such substitution occurred this pass — `FakeHttpClientFactory` is a
      new capability this pass introduced, not a replacement for an
      existing NSubstitute double.)
- [x] Replaced `LocaleHandlerTests.cs`'s two predicate-side-effect request
      captures with direct `handler.Requests` reads
      (`innerHandler.Requests.Should().ContainSingle().Which.Headers.AcceptLanguage...`).
      Its third test (`InlineAlexaVoxCraftAutoData`-based, non-HTTP) is
      deliberately left unchanged, out of scope.
- [x] Migrated `SmapiDeveloperAccessTokenProviderTests.cs`'s
      `IHttpClientFactory` usage using the small project-local
      `FakeHttpClientFactory` shape from ADR-0051/research §8.2 — a
      private nested class inside `SmapiHttpTestProfile`, registered via
      `Register<IHttpClientFactory>`. Real, evidence-grounded refinement
      found during implementation: `Register<IHttpClientFactory>`'s own
      factory internally does `context.Resolve<HttpClient>()`/
      `context.Resolve<string>()` (both provider-resolved paths, safe at
      runtime) rather than composing `SmapiDeveloperAccessTokenOptions`
      itself via `context.Resolve<SmapiDeveloperAccessTokenOptions>()` —
      the latter threw `CompositionException` at runtime
      ("No ... generated plan could satisfy") because that record type is
      never independently reachable as a compile-time discovery root
      anywhere in the project; a type reached only via a nested
      `context.Resolve<T>()` call inside a registration factory isn't
      itself a root the generator can see, unlike a provider-resolved
      primitive. Recorded as a real Compono discoverability nuance for
      10g (not a blocking gap — worked around cleanly by building the
      record from resolved primitives instead).
- [x] Confirmed (not assumed) that `Register<T>` provides **no**
      compile-time escape hatch for a concrete class reached through
      another composed type's own constructor parameter, when that
      concrete class has an ambiguous (multi-constructor) BCL shape:
      `AlexaInteractionModelClient`/`AlexaSkillInvocationClient`/
      `InSkillPurchasingClient` all take a raw `HttpClient` constructor
      parameter, and requesting any of them as a composed theory
      parameter hit `CMP0001` ("HttpClient ... has 3 accessible
      constructors") even with `Register<HttpClient>` present in the
      active profile — `TransitiveClosureWalker`'s compile-time walk
      never consults registrations for a structurally-reached constructor
      parameter (`LeafTypeClassifier.IsProviderResolved` excludes
      concrete non-value-type classes unconditionally). This matches and
      reconfirms the Compono skill's own already-documented ADR-0002
      guidance ("no registration-based escape hatch... construct by hand
      in that one spot") with fresh real-world evidence, not a new gap —
      classified as **previously known limitation** per 10c. Fixed by
      hand-constructing each client via a small `CreateClient(handler)`
      helper per test class (`new AlexaInteractionModelClient(handler.CreateClient(baseAddress),
      NullLogger<AlexaInteractionModelClient>.Instance)`), matching the
      skill's own documented pattern; `Register<HttpClient>` in the
      profile is still genuinely used (by `IHttpClientFactory`'s own
      registration factory), just never by a directly-composed client
      type.
- [ ] Stretch, non-blocking: add `BearerTokenHandlerTests.cs` (currently
      absent) using the new package, if low-risk. Not attempted this
      pass — genuinely non-blocking, left for a follow-up.
- [ ] Stretch, non-blocking: migrate the two legacy
      `ActionHandler`/`ActionMessageHandler` fakes if low-risk. Not
      attempted this pass — both live in "Legacy" test projects outside
      this pass's priority-1/2 scope (per 10d), left as-is.

#### 10c. Gap-handling rule (adversarial dogfooding — do not paper over blockers)

- [x] If a real `alexa-vox-craft` test is naturally expressible with
      AutoFixture/NSubstitute but **cannot** currently be expressed with
      the corresponding Compono package, do not silently work around it
      and report the migration as though it succeeded. Report it, in the
      dogfood research document (10g), with:
      - exact test/file;
      - the interface/member/data shape involved;
      - the existing AutoFixture/NSubstitute behavior;
      - the attempted Compono equivalent;
      - precisely why Compono cannot currently represent it;
      - classification as one of: a bug, a previously known limitation
        (cite the existing ADR/research doc that already recorded it, if
        any), a newly evidenced capability gap, or project-local behavior
        Compono should not own.
- [x] Classify every finding using the existing ADR-0029 dogfooding/
      capability-gap decision framework (the same rubric
      `docs/research/0008-...md`/`0009-...md` already apply) — don't
      invent a new classification scheme for this pass.
- [x] A newly evidenced capability gap discovered here does not block
      this plan's completion by itself — record it, and let the user
      decide separately whether it becomes its own ADR/plan candidate.
      What **does** block completion is silently avoiding the attempt or
      misreporting a blocked migration as done.

#### 10d. Scope boundary — broad, not unbounded

- [x] Prioritize, in this order: (1) all tests touched by the
      `Compono.Http` migration itself; (2) their shared fixture/profile
      infrastructure; (3) adjacent test suites consuming the same
      AutoFixture/NSubstitute composition mechanisms as those touched
      suites; (4) further migration only where removing a shared
      dependency/composition root naturally cascades to it.
- [x] If changing one shared AutoFixture/AutoNSubstitute root turns out to
      affect many tests, treat that as real dogfood evidence to handle
      deliberately (documented in 10g), not a reason to avoid touching the
      shared root.
- [x] If an unrelated area has unique, risky test infrastructure with no
      relationship to this migration, leave it alone and record that
      boundary explicitly in 10g — "not migrated, out of scope, because
      X" is an acceptable outcome; silent omission is not.
- [x] This is **not** a "rewrite every test in `alexa-vox-craft`" project
      — the priority order above is the actual scope limiter.

#### 10e. Compono.Bogus policy

- [x] Adopt `Compono.Bogus` only where 10a's inventory found a real
      semantic-data need (realistic names, emails, addresses, IDs, domain
      text, or similar) that it genuinely improves over generic object
      composition.
- [x] Do not use `Compono.Bogus` for ordinary arbitrary values merely to
      demonstrate package coverage.
- [x] "`Compono.Bogus` provides no meaningful value in this repo" is a
      fully acceptable outcome — record it as such in 10g if that's what
      the inventory shows, rather than forcing an adoption to avoid
      reporting a null result.

#### 10f. Dependency-graph acceptance

- [x] At the end of the migration, verify — against the actual **resolved
      dependency graph/package assets** (`obj/project.assets.json` or
      equivalent, not a source-only `grep`) — whether `AutoFixture`,
      `AutoFixture.AutoNSubstitute`, and `NSubstitute` are still
      referenced anywhere in `alexa-vox-craft`, and where.
- [x] Confirm whether `Compono.TestDoubles` fully replaced NSubstitute
      usage, or whether some remains, and why.
- [x] Confirm where `Compono.Http` is consumed (which projects/test files).
- [x] Confirm whether `Compono.Bogus` was justified and adopted per 10e,
      or deliberately not.
- [x] Enumerate every remaining legacy test helper/fake and the reason it
      remains (bucket 5 or 7 from 10a, or an explicitly-documented gap
      from 10c) — every remaining AutoFixture/NSubstitute use must have an
      explicit reason, not "not migrated yet" as a default.

#### 10g. Dogfood acceptance evidence (research document)

- [x] Write the findings above into a new
      `docs/research/0010-alexa-vox-craft-compono-ecosystem-migration.md`
      (next sequential research number at implementation time), following
      `docs/research/0008-...md`'s existing format for a real-migration
      dogfood record. Must include, beyond ADR-0051's own HTTP acceptance
      criteria:
      - before/after test-composition architecture description;
      - `AutoFixture`/`AutoFixture.AutoNSubstitute`/`NSubstitute` usage
        counts, before and after (call-site counts, not just "present/
        absent");
      - `Compono`/`Compono.TestDoubles`/`Compono.Http`/`Compono.Bogus`
        usage introduced (call-site counts);
      - exact remaining non-Compono test-double/fixture infrastructure
        and why it remains (10f);
      - every newly surfaced Compono gap (10c), classified;
      - complete `alexa-vox-craft` test counts (before/after, confirming
        no test was silently dropped rather than migrated);
      - the exact locally packed Compono package versions consumed (Task 11);
      - explicit confirmation the old reflection-based HTTP path is fully
        gone;
      - explicit confirmation the migration did not weaken any test's
        assertion intent merely to make a Compono package fit — a
        per-file spot-check note, not a blanket claim.

### 11. Local-package validation (multi-package, migration-aware)

**The generalized dogfood-validation script is itself a shipping artifact
of this plan — committed in this repo, not an ad-hoc sequence of commands
known only to whoever runs this migration.** The real script already
exists: `scripts/dogfood-validate.sh` (438 lines, extensively
invariant-hardened across PR #108's review rounds — see its own header
comment and `docs/research/0008-...md`/`docs/adr/0050-...md` for that
history). It already generalizes over *consumer repo/solution* (`--consumer-repo`/
`DOGFOOD_CONSUMER_REPO`, `--consumer-solution`/`DOGFOOD_CONSUMER_SOLUTION`,
auto-detected if omitted) and *build configuration*
(`--configuration`/`DOGFOOD_CONFIGURATION`) — it is **not** hardcoded to
`trivia-platform` architecturally, only defaulted to it
(`consumer_repo="${DOGFOOD_CONSUMER_REPO:-.../trivia-platform}"`). The one
genuinely hardcoded piece this plan must change is the **package set**:
`packages=(Compono Compono.NSubstitute Compono.TestDoubles Compono.XunitV3)`
is a fixed bash array, not a parameter.

- [x] Add a `--packages`/`DOGFOOD_PACKAGES` option (space- or
      comma-separated package-id list, following this script's existing
      flag/env-var-pair convention exactly) that replaces the hardcoded
      `packages=(...)` array — default it to the current four-package list
      so every existing invocation (CI, any other in-flight usage) is
      unaffected unless the new option is passed explicitly. This is a
      **targeted extension of the existing script, not a rewrite** — every
      other step (packing loop, temp `nuget.config`/`Directory.Packages.props`
      generation, version-pin `sed` loop, `project.assets.json`
      resolved-version verification loop, git dirty-tree snapshot/restore
      trap, cross-process pack lock) already iterates over the `packages`
      array generically and needs no further change once that array is
      parameterized.
- [x] Confirm, and preserve unchanged, every invariant the script already
      establishes (do not weaken any of these while adding the `--packages`
      option):
      - unique local prerelease package version per run
        (`0.0.0-local.<timestamp>-<pid>-<random>`, shared across every
        packed package in the run);
      - cross-process pack lock, keyed on the repo root's `src/Compono*`
        build outputs, not the feed directory;
      - no edit to the consumer's real `Directory.Packages.props` — a
        temp copy, referenced via `-p:DirectoryPackagesPropsPath`;
      - the selected `--configuration` propagated through both `pack` and
        `test`;
      - exact resolved-package/version verification for **every** package
        in the (now-parameterized) set, against every restored project's
        `project.assets.json` — including the existing per-package
        "found in at least one assets file" check, so a consumer that
        doesn't actually reference one of the requested packages fails
        loudly instead of silently passing;
      - full consumer solution test execution (`dotnet test`, propagating
        its real exit code);
      - the consumer repo's git status/diff snapshot-and-restore safety
        net (unconditional `trap cleanup EXIT`), so the consumer's own
        dirty/uncommitted files are byte-identical before and after,
        regardless of outcome;
      - non-zero exit on any failed invariant (missing package, version
        mismatch, restore failure, test failure, safety-net restore
        failure).
- [x] Invoked the generalized script for this plan's actual validation
      runs: `--consumer-repo /Users/ncipollina/source/repos/layered-craft/alexa-vox-craft
      --consumer-solution AlexaVoxCraft.slnx --packages "Compono Compono.XunitV3 Compono.Http"
      --configuration Release`. The migration ended up consuming exactly
      these three (this repo's test-framework integration is
      `Compono.XunitV3`, confirmed; `Compono.TestDoubles` and
      `Compono.NSubstitute` turned out unneeded, per the 10b findings
      above; `Compono.Bogus` not justified per 10e). Every package
      consumed by the selected `alexa-vox-craft` solution came from this
      same fresh local run's version set — confirmed by the script's own
      per-package resolved-version check, which passed.
- [x] Updated `scripts/dogfood-validate.sh --help`'s usage text and its own
      header comment to describe the generalized `--packages` option,
      matching the file's existing documentation style.
- [x] Cross-reference: `AGENTS.md`'s "Consumer/dogfood validation gate"
      section already states the standing repo-level policy this task's
      script serves (updated as part of this same review round, ahead of
      this plan's implementation, to describe multiple consumers and a
      parameterized package set) — this task makes the tooling match what
      that policy already requires, it doesn't establish the policy itself.
- [x] Ran, and recorded the result of, the full gate — **initial
      implementation run done; must be repeated after every subsequent
      review-feedback round, before the PR is considered ready**:
      1. `Compono`/`Compono.XunitV3`/`Compono.Http` test suites green — [x]
         (`dotnet test Compono.slnx`: 732/732, plus the two AOT proofs).
      2. Pack fresh, uniquely-versioned local packages for **every**
         Compono package the migration consumes — [x] (`Compono`,
         `Compono.XunitV3`, `Compono.Http`, not just `Compono` +
         `Compono.Http` — confirmed via the script's own `packages:` echo
         line).
      3. Restore `alexa-vox-craft` against those exact packages — [x]
         (script's own temp-`Directory.Packages.props`-override restore
         succeeded).
      4. **Verify every intended Compono package actually resolved from
         the fresh local build** — [x] the script's per-package
         resolved-version check passed for all three (no "STALE VERSION"
         output; no package in the consumed set mixed freshly-packed with
         previously published).
      5. Run the **complete** `alexa-vox-craft` test suite — [x] green:
         2816 total test executions (704 tests × 4 TFMs), 2784 succeeded,
         32 skipped (pre-existing, unrelated `[Fact(Skip = "Temporarily
         skipping due to CI issues")]` markers already present before
         this migration), **0 failed**.
      6. Inspect the remaining `AutoFixture`/`NSubstitute` dependency
         graph as part of this same gate run — done at the task 10f level
         (see that task's checkboxes); not re-run as a separate pass here.
      7. **Only once steps 1-6 all pass may the Compono working tree be
         pushed** — steps 1-6 passed; the working tree has **not** been
         pushed (per explicit instruction, separate from this gate).
- [x] **Mandatory before the PR is ready**, not optional polish — satisfied
      for the initial implementation.
- [x] **After every substantive PR review-feedback change, repeat the
      full fresh-package consumer-validation gate from step 1** — a
      dogfood run from before the latest change does not validate the
      revised code, and does not authorize a push. Record each re-run
      (even a short note, including which packages were re-packed) so a
      reviewer can see validation is current, not stale. This is a
      standing, repeat-each-round rule, not a one-time task — but a final
      re-run was in fact required and done, since tasks 12/12a's
      documentation/skill work landed after the initial round above.
      **Final re-run (2026-08-24), full gate, in order:**
      1. `dotnet test Compono.slnx -c Release`: **2474/2474 passed, 0
         failed, 0 skipped** (includes `Compono.Http.Tests` across all 4
         TFMs).
      2. Proof A (`AnalyzerContract/verify-analyzer-contract.sh`): PASS —
         `RespondJson(value, options)` confirmed IL2026+IL3050 at the
         consumer call site; `RespondJson(value, jsonTypeInfo)` confirmed
         zero warnings.
      3. Proof B (`Compono.Http.AotSmokeTest`, `dotnet publish -f net10.0
         -r osx-arm64 -p:PublishAot=true`): published and ran clean —
         `PASS: TestHttpHandler (...) survived Native AOT through the
         packaged Compono.Http dependency chain`.
      4. Skill/eval validation: every code example in
         `skills/compono/references/http.md`/`docs/packages/compono-http.md`
         compiled clean (0 errors, 0 warnings) against the real built
         `Compono.Http.dll` via a throwaway `ProjectReference` console
         project; `skills/compono-evals/evals.json` re-validated as
         well-formed JSON after adding eval 22.
      5. Fresh pack + `scripts/dogfood-validate.sh --consumer-repo
         .../alexa-vox-craft --consumer-solution AlexaVoxCraft.slnx
         --packages "Compono Compono.XunitV3 Compono.Http" --configuration
         Release`: packed `Compono`/`Compono.XunitV3`/`Compono.Http` at a
         fresh unique version (`0.0.0-local.20260824154311-19509-3472`,
         different from the initial round's — confirms this re-run
         genuinely repacked, not reused a stale artifact); resolved-
         version check passed for all three; full `alexa-vox-craft` suite:
         **2816 total, 2784 succeeded, 32 skipped, 0 failed** — exact
         match to the initial round's numbers.
      6. Consumer git dirty-state check: `git status --porcelain | wc -l`
         → 28, both immediately before and immediately after the gate run
         — byte-identical, confirming the script's safety net held.
      **Result: PASS. The working tree is now eligible to be pushed** per
      the standing gate policy — it has **not** been pushed (no push/PR
      authorization given in this session).

### 12. Documentation

- [x] `docs/packages/compono-http.md` (new, following the existing
      `docs/packages/*.md` per-package doc pattern) covering:
      - matching/precedence semantics (last-match-wins, `Match<string>`
        vs. `Func<HttpRequestMessage, bool>` split and why);
      - strict unmatched-request behavior and how to configure an
        explicit fallback;
      - the verification model (`registration.Verify().Once()`, kept
        separate from `Requests`);
      - request-log semantics (raw references, snapshot-per-access,
        recorded before matching);
      - the caller-owned lifetime model (handler and every `HttpClient`
        are caller-disposed; Compono composition never owns disposal);
      - JSON/AOT warning behavior and the `JsonTypeInfo<T>` path,
        including a short "if you're publishing Native AOT, use this
        overload" callout;
      - explicit v1 non-goals (the list in this plan's Scope section,
        condensed for a package-doc reader).
- [x] XML doc comments on every public member — this repo's established
      bar per `references/coding-standards.md`/`documentation.md` (confirm
      exact expectations there at implementation time rather than
      guessing).
- [x] `README.md`/`docs/mvp.md` package-list update if those docs
      currently enumerate shipped packages (confirm at implementation
      time whether `Compono.Http` needs to be added there, following
      whatever precedent `Compono.DependencyInjection`'s own addition
      set).

### 12a. Compono agent skill update — a shipping requirement, not follow-up

`skills/compono/SKILL.md` (plus its `skills/compono/references/*.md`
files) is the separate product surface that teaches an *agent* which
Compono packages exist, how to choose between them, and how to use them
— confirmed by inspecting the actual current skill structure rather than
guessing a path: `skills/compono/SKILL.md` is the entry point (Detection
table, Default workflow, Guardrails, "When not to use Compono", a
References table gating which `references/*.md` file loads for which
package), and each already-shipped package
(`Compono.XunitV3`/`Compono.TUnit`/`Compono.NSubstitute`/`Compono.Bogus`/
`Compono.TestDoubles`/`Compono.DependencyInjection`) has its own
`references/<package>.md` file. `Compono.DependencyInjection`'s own
plan (PLAN-0047 finding 31/33) and `Compono.TUnit`'s (PLAN-0040) both
already establish the precedent this task follows exactly: a new package
gets a Detection-table row, a `SKILL.md` description/guardrail update, and
its own `references/<package>.md` file, landed in the same PR that ships
the package's code — not deferred. `docs/packages/compono-http.md` above
and this skill update are **separate, both-required** surfaces per the
review feedback that opened this task; neither substitutes for the other.

This is written from the **shipped, final public API** once tasks 2-8 are
implemented — not from ADR-0051's illustrative examples, which may not
match the final method/type names exactly. Do not draft skill content
speculatively ahead of the implementation; write or finalize it once the
public API is locked, then verify every code example in it actually
compiles against the real package (a mechanical check, not a guess).

- [x] `skills/compono/SKILL.md` frontmatter `description`: add
      `Compono.Http` to the enumerated optional-package list (currently
      "`Compono.XunitV3`/`Compono.TUnit`/`Compono.NSubstitute`/`Compono.Bogus`/
      `Compono.TestDoubles`/`Compono.DependencyInjection`").
- [x] Detection table: new row —
      `<PackageReference Include="Compono.Http"` → Definitive confidence →
      "`TestHttpHandler`/`OnGet`/`OnPost`/etc. available — load
      `references/http.md`" (exact member names confirmed against the
      shipped API, per the note above).
- [x] Default workflow step 3 (mechanism-selection list): add a bullet for
      "a test deliberately needs to exercise the real HTTP client
      pipeline (real `HttpClient` → `TestHttpHandler` → configured
      response) rather than substitute an API abstraction away" →
      `Compono.Http`'s `TestHttpHandler`, if that package is referenced —
      cross-referencing the existing NSubstitute/TestDoubles bullet
      immediately above it, since the two are a real decision point (see
      new reference file's "When to use" / "When NOT to use" sections
      below).
- [x] Guardrails' shipped-packages enumeration (the "Never claim or write
      code against a Compono integration package that hasn't shipped"
      bullet): add `Compono.Http` to the list of packages that *do* ship,
      alongside a one-line note of what it ships (mirroring the existing
      one-line notes for `Compono.TUnit`/`Compono.TestDoubles`/
      `Compono.DependencyInjection` in that same bullet) — e.g. "ships
      `TestHttpHandler`, a reflection-free `HttpMessageHandler`-based test
      double for `HttpClient`-consuming code; does not ship
      `IHttpClientFactory`/named-client integration."
- [x] **Correct the existing "When not to use Compono" `HttpClient`
      bullet** (`SKILL.md`'s current text: `HttpClient` as an
      ambiguous-constructor BCL type hitting `CMP0001`, worked around via
      a hand-built `IHttpClientProvider` interface wrapper). This bullet
      is about a *different* problem than `Compono.Http` solves — composing
      an already-configured, real `HttpClient` value via
      `Composer.Create<HttpClient>()` still hits `CMP0001` and still needs
      that same interface-wrapper/hand-construction workaround;
      `Compono.Http` does not change that. What changes is the *adjacent*
      question the same bullet's reader is likely actually asking: "how do
      I test code that consumes an `HttpClient`?" — add a clarifying
      cross-reference distinguishing the two ("if the goal is testing code
      that *consumes* an `HttpClient`/`HttpMessageHandler`, see
      `Compono.Http`'s `TestHttpHandler` instead of hand-wrapping an
      interface just to fake HTTP responses; the interface-wrapper
      workaround described here is specifically for *composing* a real,
      already-configured `HttpClient` value, which remains unsupported").
      Get this distinction precise — conflating the two would misteach an
      agent that `Compono.Http` removes `CMP0001`, which it doesn't.
- [x] References table: new row — `references/http.md` | `Compono.Http`
      is referenced — `TestHttpHandler`/matching/verification/lifetime
      work.
- [x] New `skills/compono/references/http.md`, matching the shape and
      register of the existing `references/*.md` files (short,
      example-driven, no restating what `SKILL.md` already covers), and
      teaching at minimum — content sourced from ADR-0051 §"Decision
      Outcome" plus, once implemented, verified directly against the
      shipped API:
      - **When to use `Compono.Http`**: a test deliberately needs to
        exercise the real `HttpClient` pipeline (`real HttpClient ->
        TestHttpHandler -> configured HTTP response`) — concrete clients
        built directly on `HttpClient`, testing URI/method/header/request
        construction, testing serialization/request-pipeline behavior,
        testing `DelegatingHandler` behavior, replacing a hand-written or
        reflection-based `HttpMessageHandler` fake.
      - **When NOT to use it**: the production seam is already an
        ordinary application interface (`ICustomerApi`, `IWeatherService`,
        etc.) and the test doesn't care about HTTP behavior specifically —
        that stays a `Compono.TestDoubles`/`Compono.NSubstitute` case.
        Explicit reminder: never special-case `HttpClient`/
        `HttpMessageHandler` through `Compono.TestDoubles` — that
        boundary is architectural (ADR-0051), not a v1-only limitation.
      - **Core usage vocabulary**: idiomatic examples using the actual
        shipped API — the equivalents of `OnGet(path).RespondJson(...)`,
        `OnGet(Match.Is<string>(...)).RespondJson(...)`,
        `When(predicate).Respond(...)`, capturing and using a
        registration handle, `registration.Verify().Once()`,
        `handler.Requests`, `handler.CreateClient(...)`. Written from the
        final method/type names, not copied verbatim from the ADR's
        illustrative examples.
      - **Matching semantics**: `OnGet`/`OnPost`/etc. use `Match<string>`
        for the path (literal = equality, `Match.Any<string>()`,
        `Match.Is<string>(...)` all available); whole-request matching
        uses `When(Func<HttpRequestMessage, bool>)`; last-match-wins.
        Explicitly do **not** teach a dedicated header/query/body matcher
        DSL — v1 intentionally has none (the `When(...)` predicate is the
        only mechanism for those dimensions).
      - **Unmatched behavior**: strict by default —
        `UnmatchedHttpRequestException`. A test wanting fallback behavior
        configures an explicit catch-all registration; there is no
        loose-mode switch to look for.
      - **Verification vs. request inspection**: `registration.Verify()`
        answers "how many times did *this configured behavior* match";
        `handler.Requests` answers "what actually reached the handler."
        Never suggest reconstructing a verification predicate or an
        expression-based `Verify(...)` API — that shape was considered
        and rejected in ADR-0051.
      - **Lifetime** (called out as important since Compono has no
        composition-owned disposal today): `TestHttpHandler` is
        caller-owned; the `HttpClient` from `CreateClient()` is
        caller-owned; `CreateClient` always uses `disposeHandler: false`;
        `[Shared]` gives identity/reuse only; Compono does not currently
        dispose `[Shared]`-composed `IDisposable` values. Do not teach or
        imply automatic composition-scope disposal — if a later core ADR
        changes that, this reference updates then, not preemptively.
      - **`IHttpClientFactory` boundary**: `Compono.Http` is not an
        `IHttpClientFactory` mocking package. For that seam: a tiny
        project-local fake `IHttpClientFactory` when that's the smallest
        option, or `Compono.TestDoubles` if the project already uses it
        for other doubles (it's an ordinary single-method interface — no
        special machinery needed). Never suggest a
        `Microsoft.Extensions.Http` helper `Compono.Http` doesn't ship.
      - **JSON/AOT**: `RespondJson(value, options)` is the ergonomic
        runtime-metadata path and carries the normal
        `RequiresDynamicCode`/`RequiresUnreferencedCode` trimming/AOT
        warnings at the consumer's own call site;
        `RespondJson(value, JsonTypeInfo<T>)` is the source-generated,
        AOT-safe path — prefer it in an AOT/trim-sensitive project. Never
        claim all `RespondJson` usage is automatically AOT-safe.
- [x] `skills/compono-evals/evals.json`: add at least one new scenario,
      following the exact precedent PLAN-0040 set for `Compono.TUnit`
      (eval 21 there) — e.g. a routing/behavioral-correctness scenario
      confirming an agent recommends `Compono.Http`'s `TestHttpHandler`
      for a real "test my `HttpClient`-consuming code" prompt rather than
      inventing an unshipped `IHttpClientFactory` integration or a
      header/body matcher DSL that doesn't exist in v1. Also **review**
      (don't blindly rewrite) existing eval 16
      (`skills/compono-evals/evals.json`, the `CMP0001`/`HttpClient`
      reflection-refusal scenario) — its current `expected_output`/
      `expectations` still hold unchanged (`Compono.Http` doesn't remove
      `CMP0001` for composing a raw `HttpClient`, per the "When not to use
      Compono" correction above), so no rewrite is required there unless
      implementation reveals otherwise; record that review either way.
      **Done**: added eval 22 (routing scenario: `AlertsClient` taking
      `HttpClient` in its constructor, project referencing `Compono.Http` —
      expects `TestHttpHandler`, hand-constructed client via
      `handler.CreateClient(...)`, `registration.Verify()`/`handler.Requests`,
      no invented `IHttpClientFactory` helper or matcher DSL). Reviewed
      eval 16: confirmed still correct unchanged — `Compono.Http` does not
      remove `CMP0001` for `Composer.Create<HttpClient>()`, so the
      reflection-refusal expectations still hold; not rewritten.
- [x] **Validate the skill update against the real shipped API** — re-read
      every code example in the new `references/http.md` against
      `Compono.Http`'s actual public members once implemented (task 2-8)
      and confirm each one compiles; this repo's skill-validation process
      for a prior package addition is the `/skill-creator`-style benchmark
      run PLAN-0035 established and PLAN-0040 reused (see
      `skills/compono-evals/benchmarks/2026-08-07/README.md` for the
      methodology) — run at least the new/updated scenario(s) (full
      18-scenario re-run is not required just for one package addition,
      per PLAN-0040's own scoping) and record the result the same way
      (`skills/compono-evals/benchmarks/<date>/README.md` or an addendum
      to the existing one — confirm exact convention against what
      PLAN-0040 actually did at implementation time). Treat this as part
      of PLAN-0051's completion, not optional follow-up documentation —
      the plan's Goal ("done when...") is not satisfied while the skill
      still has no knowledge of `Compono.Http`. **Done**: confirmed
      PLAN-0040's actual precedent (not its own aspirational plan text) —
      it did **not** run a live benchmark or create a new
      `benchmarks/<date>/` folder for eval 21; it recorded the added eval
      directly in its own plan/status notes. This task follows that same
      real precedent: every code example in `references/http.md` and
      `docs/packages/compono-http.md` was compiled against the actual
      built `Compono.Http.dll` via a throwaway console project
      (`ProjectReference` to `src/Compono.Http/Compono.Http.csproj`) —
      build succeeded, 0 errors, 0 warnings — rather than eyeballed. No
      new benchmark folder created, matching PLAN-0040's real practice.

## Critical Files

New:

- `src/Compono.Http/Compono.Http.csproj`
- `src/Compono.Http/TestHttpHandler.cs`
- `src/Compono.Http/HttpResponseRegistration.cs`
- `src/Compono.Http/UnmatchedHttpRequestException.cs`
- `src/Compono.Http/HttpResponseRegistrationBuilder.cs` (or equivalent —
  exact fluent-return type shape is an implementation detail)
- `test/Compono.Http.Tests/*` (new test project, behavioral coverage per
  task 9)
- `test/Compono.Http.AotSmokeTest/*` (new, per task 5's mirrored
  `Compono.TestDoubles.AotSmokeTest` pattern)
- `docs/packages/compono-http.md`
- `skills/compono/references/http.md`

Modified (this repo, skill surface — task 12a):

- `skills/compono/SKILL.md` (frontmatter description, Detection table,
  Default workflow, Guardrails' shipped-packages list, "When not to use
  Compono"'s `HttpClient` bullet, References table)
- `skills/compono-evals/evals.json` (new scenario(s); eval 16 reviewed,
  not necessarily rewritten)

Modified (this repo, dogfood tooling/policy — task 11):

- `scripts/dogfood-validate.sh` — new `--packages`/`DOGFOOD_PACKAGES`
  option replacing the hardcoded `packages=(...)` array; usage text and
  header comment updated to match.
- `AGENTS.md` — "Consumer/dogfood validation gate" section (already
  updated ahead of implementation, this review round, to state the
  standing multi-consumer/multi-package/no-stale-push policy this task's
  script serves).

Modified (`alexa-vox-craft` repo, separate from this repo's own PR — see
Notes) — HTTP migration (task 10b), scope locked by evidence in this plan:

- `test/AlexaVoxCraft.Http.TestKit/Extensions/HttpMessageHandlerExtensions.cs` (removed)
- `test/AlexaVoxCraft.Http.TestKit/SpecimenBuilders/HttpClientSpecimenBuilder.cs`,
  `RequestSpecifications/HttpClientSpecification.cs`,
  `Attributes/ClientAutoDataAttribute.cs` (+2 subclasses)
- `test/AlexaVoxCraft.Smapi.Tests/Clients/AlexaInteractionModelClientTests.cs`,
  `AlexaSkillInvocationClientTests.cs`,
  `Auth/SmapiDeveloperAccessTokenProviderTests.cs`
- `test/AlexaVoxCraft.InSkillPurchasing.Tests/Clients/InSkillPurchasingClientTests.cs`,
  `Handlers/LocaleHandlerTests.cs`

Modified (`alexa-vox-craft` repo) — broader ecosystem migration (tasks
10a-10f), **exact file list determined by the 10a inventory at
implementation time, not pre-enumerated here** (the whole point of
running the inventory first is not knowing this list in advance):
likely candidates based on what's already known from the HTTP-focused
research (`docs/research/0009-...md` §1) include
`test/AlexaVoxCraft.TestKit/Attributes/BaseFixtureFactory.cs` and its
subclasses, any `AutoFixture.AutoNSubstitute`-registering customization,
and any shared base-fixture project the migrated test projects depend on
— confirm the real set via 10a, don't assume this list is complete.

Modified (this repo):

- `docs/adr/README.md`, `docs/plans/README.md` (status updates as work
  proceeds)
- `docs/mvp.md`/`README.md` if the package-list update in task 12 applies
- `docs/research/0010-alexa-vox-craft-compono-ecosystem-migration.md`
  (new — task 10g)

## Test Plan

- `test/Compono.Http.Tests` — full behavioral coverage per task 9,
  matching `references/testing.md`'s conventions (xUnit v3,
  AwesomeAssertions, deterministic — no `Thread.Sleep`-based concurrency
  tests; use bounded parallel-task fan-out with a completion barrier
  instead).
- Task 5's two separate JSON/AOT proofs, kept distinct: **Proof A**
  (analyzer-contract, build-time only) — automated assertions that
  `RespondJson(value, options)` warns IL2026/IL3050 at the consumer's own
  call site and `RespondJson(value, jsonTypeInfo)` warns nothing.
  **Proof B** (`test/Compono.Http.AotSmokeTest`) — a real
  `PublishAot=true` publish-and-run through the packed dependency chain,
  exercising only the `JsonTypeInfo<T>` overload; the
  `JsonSerializerOptions` overload is never required to publish
  warning-free under Native AOT in either proof.
- `alexa-vox-craft` full suite, green against freshly packed local
  packages covering **every** Compono package the migration ends up
  consuming (not just `Compono`/`Compono.Http`) — task 11's gate,
  mandatory, re-run after every substantive review-feedback change, not a
  one-time check, and the gate that authorizes a push (task 11, step 7).
- `scripts/dogfood-validate.sh`'s generalization itself: confirm the
  default (no `--packages` passed) invocation is behaviorally unchanged
  from before this plan — same hardcoded four-package default, so any
  existing `trivia-platform`/`Compono.TestDoubles` usage isn't disturbed
  — and confirm a `--packages` invocation with a different set (e.g.
  adding `Compono.Http`, omitting `Compono.NSubstitute`) packs, restores,
  and version-verifies exactly that set, no more and no less.
- `skills/compono-evals` — the new/updated scenario(s) from task 12a run
  and pass under the repo's established `/skill-creator`-style benchmark
  process (PLAN-0035/PLAN-0040 precedent); every code example in the new
  `references/http.md` verified to compile against the actual shipped
  `Compono.Http` public API, not copied from ADR-0051's illustrative
  examples unchecked.
- The dogfood research document (task 10g) itself is a test-plan
  deliverable, not just a report — its assertion-intent-preservation and
  test-count claims must be independently checked (e.g. diffing test
  method names/counts before and after, not just "the suite is green"),
  since a green suite alone doesn't prove no test's assertion intent was
  weakened to make a Compono package fit.

## Notes

`alexa-vox-craft` changes (task 10's file lists) happen in the
`alexa-vox-craft` repo, not this one — this plan tracks them here because
they're the acceptance evidence for `Compono.Http`'s admission (ADR-0051)
and, per this plan's expanded scope, for the broader ecosystem-migration
question the review feedback that produced tasks 10a-10g raised. They
ship as that repo's own change, coordinated with (not bundled into) this
repo's `Compono.Http` PR. Record here, as work proceeds:

- whether the HTTP migration (10b's original 41-call-site scope) and the
  broader ecosystem migration (10a/10c-10g) happened as one companion PR
  in `alexa-vox-craft` or were split, and why, if reality diverges from
  "migrate everything in scope in the same work session";
- the actual inventory-to-migration ratio from 10a — how much of what was
  inventoried actually got migrated versus left in bucket 5/6/7, so a
  reader can see the real proportion without re-reading the full 10g
  document;
- ADR-0051 itself does not change because of this broader migration — if
  the migration surfaces a finding that *does* warrant a core-Compono
  ADR (a genuine new capability gap from 10c), that's a separate future
  ADR, not an amendment to ADR-0051, which is scoped to `Compono.Http`
  only.
- Findings A and B (10c/RESEARCH-0010 §10-11) were classified core-Compono
  capability gaps but, per a post-merge process check, had not yet been
  run through ADR-0029's rubric or entered in `docs/roadmap/post-mvp.md` —
  corrected 2026-08-24: both apply the rubric to "Roadmap candidate,"
  recorded as one roadmap entry (two evidence cases, deliberately not yet
  merged or split) via [ADR-0052](../adr/0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md)
  (`Proposed`) and [ADR-0002 Amendment 2](../adr/0002-constructor-selection-algorithm.md#amendment-2-2026-08-24-the-real-pre-existing-call-site-amendment-1-anticipated-has-now-surfaced).
  A future design dive (per `design-decisions.md`), not yet started, will
  determine the actual mechanism before any implementation plan exists.

The standing "no push before the consumer dogfood gate passes" rule
(task 11) is not new policy invented by this plan — it's already recorded
at the repository level in `AGENTS.md`'s "Consumer/dogfood validation
gate" section (updated in this review round to cover multiple consumers
and a parameterized package set, ahead of this plan's own implementation)
so future plans with an active dogfood consumer don't have to rediscover
it. This plan's task 11 is that policy's concrete application to
`Compono.Http`/`alexa-vox-craft`, plus the tooling change
(`scripts/dogfood-validate.sh`'s `--packages` option) the policy's
multi-package case now depends on.
