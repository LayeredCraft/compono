# [RESEARCH-0010] `alexa-vox-craft` Compono Ecosystem Migration (PLAN-0051 Task 10)

**Status:** Done (HTTP-touched slice migrated and validated; broader
repo-wide migration explicitly out of scope — see "Scope boundary" below)

**Feeds:** [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md) /
[PLAN-0051](../plans/0051-compono-http-handler-based-testing-package-impl-plan.md)
Task 10's dogfood-acceptance evidence, and the future pre-1.0
constructor-selection design deep dive this document's Finding A feeds.

This is the evidence record for PLAN-0051 Task 10 — the broader Compono-
ecosystem migration in `ncipollina/alexa-vox-craft` (not just the narrow
`Compono.Http` swap), following `docs/research/0008-...md`'s
established real-migration format. Two sub-agent inventory passes fed
this document (the HTTP-specific inventory in
`docs/research/0009-...md` §1, and a broader AutoFixture/NSubstitute
inventory run at the start of this pass) — this document is the
migration's actual execution record and acceptance evidence, not a
re-run of that inventory.

## Scope boundary (per explicit product direction)

This pass migrated exactly the **HTTP-testing-touched slice**:
`AlexaVoxCraft.Smapi.Tests` and `AlexaVoxCraft.InSkillPurchasing.Tests`
(priority 1/2 per PLAN-0051 Task 10d's priority order — the tests
`Compono.Http` itself touches, plus their shared HTTP composition root).
The broader repo (`AlexaVoxCraft.MediatR.Tests`, `AlexaVoxCraft.MediatR.Lambda.Tests`,
`AlexaVoxCraft.Model.Tests`, and the rest of `AlexaVoxCraft.TestKit`'s
non-HTTP specimen builders/`AutoDataAttribute` family) was **not**
migrated — per PLAN-0051 Task 10d, nothing in that broader surface shares
a composition root with the HTTP-touched projects, so migrating it doesn't
naturally cascade from this work and would be a separate, unrelated,
much larger undertaking. This boundary is recorded explicitly, not a
silent omission.

Two core-Compono findings surfaced during this pass (Findings A and B
below) are **deliberately not fixed here** — captured as evidence for a
separate, future pre-1.0 design deep dive, per explicit product
direction. `ADR-0051` and `Compono.Http` remain unchanged by either
finding.

## 1. Before/after test-composition architecture

**Before**: `AlexaVoxCraft.Smapi.Tests` and `AlexaVoxCraft.InSkillPurchasing.Tests`
both composed entirely through `AlexaVoxCraft.TestKit`'s
`BaseFixtureFactory` (AutoFixture + `AutoNSubstituteCustomization { ConfigureMembers
= true }`), reached via a project-local `AlexaVoxCraft.Http.TestKit`
project that added `HttpClientSpecimenBuilder`/`HttpClientSpecification`
(freezing a mocked `HttpMessageHandler` and wrapping it in an `HttpClient`)
plus per-project `AutoDataAttribute` subclasses
(`SmapiClientAutoDataAttribute`, `SkillInvocationClientAutoDataAttribute`,
`IspClientAutoDataAttribute`) layering in domain-specific specimen
builders. Response configuration went through a hand-rolled,
reflection-based `HttpMessageHandlerExtensions.ReturnsResponse` (see
`docs/research/0009-...md` §1 for the full original inventory).

**After**: `AlexaVoxCraft.Smapi.Tests` composes entirely through
`Compono`/`Compono.XunitV3`, via one shared profile
(`SmapiHttpTestProfile`, `test/AlexaVoxCraft.Smapi.Tests/TestKit/SmapiHttpTestProfile.cs`)
applied with `[Compose<SmapiHttpTestProfile>]`. `AlexaVoxCraft.InSkillPurchasing.Tests`
is **mixed**, deliberately: its HTTP-touched tests compose via plain
`[Compose]` (no profile needed — `InSkillPurchasingClient` needs nothing
beyond what auto-composes), while its two untouched non-HTTP files still
use `AlexaVoxCraft.TestKit`'s AutoFixture-based `InlineAlexaVoxCraftAutoDataAttribute`,
per the explicit scope boundary above. The `AlexaVoxCraft.Http.TestKit`
project is deleted entirely (all 4 of its files existed only to support
the old approach) — this repo now has **zero** `Compono` footprint
before this pass, and a real, dual-package-set footprint after it.

## 2. AutoFixture/AutoNSubstitute/NSubstitute usage counts, before and after

Counts below are **resolved-dependency-graph confirmed** (`project.assets.json`'s
`targets` section — the actual consumed packages per TFM — not a source
grep, per the plan's own "verify against resolved assets, not source
grep" requirement) and **source-level call-site confirmed** together.

| Project | Before (resolved deps) | After (resolved deps) | Before (call sites) | After (call sites) |
|---|---|---|---|---|
| `AlexaVoxCraft.Smapi.Tests` | `AutoFixture`, `AutoFixture.AutoNSubstitute`, `AutoFixture.Xunit3`, `NSubstitute` | **none** — `Compono`, `Compono.XunitV3`, `Compono.Http` only | 26 `[Frozen] HttpMessageHandler`/`AutoData`-family call sites across 3 files (10+9+7 methods, `docs/research/0009-...md` §1) | **0** — 30 `[Compose<SmapiHttpTestProfile>]`/`[Shared] TestHttpHandler` call sites across the same 3 files (10+11+9 methods; the higher method count is `ComposeAttribute`'s `AllowMultiple=false` forcing 2 previously-`[InlineData]`-stacked methods apart into 3 methods each — see Finding-adjacent note below, not a behavior change) |
| `AlexaVoxCraft.InSkillPurchasing.Tests` | `AutoFixture`, `AutoFixture.AutoNSubstitute`, `AutoFixture.Xunit3`, `NSubstitute` | **both** — `AutoFixture`/`AutoFixture.AutoNSubstitute`/`AutoFixture.Xunit3`/`NSubstitute` (2 untouched files) **and** `Compono`/`Compono.XunitV3`/`Compono.Http` (HTTP-touched files) | 11 `[Frozen]`/`AutoData` call sites (`InSkillPurchasingClientTests.cs`) + 2 `Substitute.For<HttpMessageHandler>()` (`LocaleHandlerTests.cs`) + 6 `InlineAlexaVoxCraftAutoData` (both untouched files combined) | 0 in the 2 migrated files (11 `[Compose]`/`[Shared] TestHttpHandler` call sites); the 6 `InlineAlexaVoxCraftAutoData` call sites in the 2 untouched files are **unchanged**, in scope-boundary bucket 5/7 (see §14) |

`AlexaVoxCraft.Http.TestKit` (the shared reflection-based HTTP TestKit
project both projects depended on): **deleted in full** — 4 files, ~150
lines, 0 remaining references anywhere in the solution (confirmed:
`AlexaVoxCraft.slnx` no longer lists it, and `dotnet build AlexaVoxCraft.slnx`
succeeds with no missing-reference errors).

## 3. Compono usage introduced

| Mechanism | `Smapi.Tests` call sites | `InSkillPurchasing.Tests` call sites |
|---|---|---|
| `[Compose]`/`[Compose<TProfile>]` | 31 (30 test methods + 1 profile file self-reference in a doc comment) | 11 |
| `[Shared] TestHttpHandler` | 30 | 11 |
| `TestHttpHandler` (all mentions: `OnGet`/`OnPost`/`OnPut`/`Requests`/etc.) | 38 across 4 files | 14 across 2 files |
| Shared composition profiles | 1 (`SmapiHttpTestProfile`) | 0 (nothing beyond plain `[Compose]` needed) |

`Compono.NSubstitute` and `Compono.TestDoubles`: **evaluated, not
adopted** — neither migrated file ended up needing a substitutable
interface dependency once `HttpClient` construction was handled
directly (Finding A) and `IHttpClientFactory` was satisfied by a 3-line
project-local fake (ADR-0051 §8.2's proven pattern, confirmed working
here for real). Both package references were added speculatively at the
start of this pass and **removed** once the actual migrated code proved
not to need them — recorded here so "we didn't end up needing X" is
traceable evidence, not silently dropped.

## 4. `AlexaVoxCraft.Http.TestKit` removal — confirmed

- [x] `test/AlexaVoxCraft.Http.TestKit/` directory deleted in full (4 files:
      `HttpMessageHandlerExtensions.cs`, `HttpClientSpecimenBuilder.cs`,
      `HttpClientSpecification.cs`, `ClientAutoDataAttribute.cs`).
- [x] Removed from `AlexaVoxCraft.slnx`.
- [x] Removed as a `ProjectReference` from both consuming projects'
      `.csproj` files.
- [x] `dotnet build AlexaVoxCraft.slnx` succeeds with zero missing-reference
      errors — confirms nothing else in the solution depended on it.
- [x] The reflection-based `SendAsyncMethod.Invoke(handler, ...)` call
      (the whole reason this project existed) no longer appears anywhere
      in the solution.

## 5. Complete `alexa-vox-craft` test suite — before/after, exact match

Both runs below are the **complete solution suite**
(`dotnet test AlexaVoxCraft.slnx -c Debug -f net10.0`), run against the
literal same working tree state (before: `git stash` of this pass's
changes, restoring the pristine pre-migration tree; after: `git stash pop`
restoring this pass's changes) — not two different partial runs compared
by inference.

| | Total | Succeeded | Skipped | Failed |
|---|---|---|---|---|
| **Before** (pristine, pre-migration) | 704 | 696 | 8 | 0 |
| **After** (this pass's working tree) | 704 | 696 | 8 | 0 |

**Identical.** The 8 skipped tests are pre-existing
`[Fact(Skip = "Temporarily skipping due to CI issues")]` markers in
`AlexaVoxCraft.Model.Apl.Legacy.Tests` (unrelated to this migration,
present before and after unchanged). Zero tests were silently dropped,
zero new failures, zero new skips.

The full solution suite was additionally run through the real
multi-TFM matrix (`net8.0`/`net9.0`/`net10.0`/`net11.0`) as part of the
formal dogfood-validate.sh gate (§7) — 2816 total executions
(704 × 4 TFMs), 2784 succeeded, 32 skipped (8 × 4), 0 failed.

## 6. Assertion-intent preservation

Every migrated test's assertion intent was checked file-by-file against
its original, not assumed from "compiles and passes":

- **Response-shape assertions** (`.Should().BeEquivalentTo(responseModel)`,
  `.Should().BeNull()`, `.Should().NotBeNull()`) — unchanged, 1:1.
- **Request-matching assertions** (path/method/content-type predicates) —
  unchanged in what they check; syntax changed from a hand-written
  `Func<HttpRequestMessage, bool>` passed to `ReturnsResponse` to the same
  predicate shape passed to `TestHttpHandler.When(...)`, or a
  `Match<string>`-based `OnGet(path)`/`OnPost(path)` for the exact-path
  cases (the dominant real shape, per `docs/research/0009-...md` §1).
- **Call-count assertions**: every `handler.ReceivedCalls().Should().HaveCount(n)`
  became `registration.Verify().Exactly(n)`; every meaningful
  `handler.Received()` became `registration.Verify().Once()`.
- **A real latent bug fixed, not just carried forward**: the original
  `AlexaInteractionModelClientTests.cs`'s `UpdateAsync_*` tests each ended
  with a bare `handler.Received();` statement — in NSubstitute, `Received()`
  with no further chained call on the returned object asserts **nothing**;
  it was a silent no-op that always "passed" regardless of whether the
  request actually happened. The migrated versions
  (`registration.Verify().Once()`) are real assertions that would now
  actually fail if the request stopped happening. This is a genuine
  correctness improvement the migration produced as a side effect, not
  something this pass set out to fix — flagged here because "assertion
  intent preserved" undersells it; in this specific case intent was
  *restored*.
- **`LocaleHandlerTests.cs`'s request-capture test**: the original used a
  matcher-predicate side effect (`predicate: req => { capturedRequest =
  req; return true; }`) to smuggle the sent request out for inspection
  after the fact — exactly the pattern ADR-0051 was written to replace.
  The migrated version reads `innerHandler.Requests` directly, a real,
  first-class capture API. Same assertion (`AcceptLanguage` header
  contains the locale), cleaner mechanism.

## 7. Local-package validation — exact versions consumed

Per PLAN-0051 Task 11's generalized `scripts/dogfood-validate.sh`:

```
scripts/dogfood-validate.sh \
  --consumer-repo /Users/ncipollina/source/repos/layered-craft/alexa-vox-craft \
  --consumer-solution AlexaVoxCraft.slnx \
  --packages "Compono Compono.XunitV3 Compono.Http" \
  --configuration Release
```

- Packed version (shared across all three packages, one run): `0.0.0-local.20260824151319-11320-19792`.
- Resolved-version check: **passed** for all three packages — no "STALE
  VERSION" output, confirming every one resolved to that exact freshly-
  packed version, not a cached/previously-published one.
- Full `alexa-vox-craft` suite under this exact package set: 2816 total,
  2784 succeeded, 32 skipped, **0 failed**.
- Consumer git working tree: confirmed byte-identical before and after
  the run (the script's own safety net).
- Script's own exit: `PASS - consumer test suite succeeded against local
  Compono 0.0.0-local.20260824151319-11320-19792`.

## 8. Resolved dependency-graph evidence

Confirmed directly against `project.assets.json`'s `targets` section
(the actual per-TFM resolved package set — not source grep, not the CPM
`PackageVersion` declaration list, which lists every centrally-declared
version regardless of whether a given project actually consumes it):

**`AlexaVoxCraft.Smapi.Tests`** (all 4 TFMs): `Compono/1.0.0`,
`Compono.XunitV3/1.0.0`, `Compono.Http/1.0.0`. **Zero** `AutoFixture*`/
`NSubstitute` entries.

**`AlexaVoxCraft.InSkillPurchasing.Tests`** (all 4 TFMs): **both** stacks
resolve simultaneously — `Compono/1.0.0`, `Compono.XunitV3/1.0.0`,
`Compono.Http/1.0.0` **and** `AutoFixture/4.18.1`,
`AutoFixture.AutoNSubstitute/4.18.1`, `AutoFixture.Xunit3/4.19.0`,
`NSubstitute/6.2.0` — the latter kept alive specifically by the two
untouched non-HTTP files' `AlexaVoxCraft.TestKit` dependency (§9).

## 9. What remains non-Compono, and why (no "not migrated yet" without classification)

| Remaining item | Where | Classification | Why it remains |
|---|---|---|---|
| `AutoFixture`/`AutoFixture.AutoNSubstitute`/`AutoFixture.Xunit3`/`NSubstitute` | `AlexaVoxCraft.InSkillPurchasing.Tests` (via `AlexaVoxCraft.TestKit` reference) | **Bucket 5/7** — project-local, intentional, out of scope | `AlexaRequestAccessTokenProviderTests.cs` and `LocaleHandlerTests.cs`'s third test use `InlineAlexaVoxCraftAutoDataAttribute` for non-HTTP concerns (a nullable/empty/whitespace string parameter) — neither test touches HTTP composition, and migrating `AlexaVoxCraft.TestKit` itself (the shared base every other test project in the repo also depends on) is explicitly out of this pass's scope per Task 10d's priority order. |
| `AlexaVoxCraft.TestKit` itself (AutoFixture-based `BaseFixtureFactory`, 10 `AutoDataAttribute` subclasses, 25 specimen builders, 23 request specifications, 1 customization) | Whole repo | **Bucket 5/7** — explicitly out of scope | Shared by 9 of 12 test projects repo-wide; migrating it is a large, separate undertaking with no natural cascade from the `Compono.Http` work (per Task 10d's own scope-boundary rule — "if an unrelated area has unique, risky test infrastructure with no relationship to the migration, leave it alone and record the boundary"). |
| `AlexaVoxCraft.MediatR.Tests`/`AlexaVoxCraft.MediatR.Lambda.Tests`/`AlexaVoxCraft.Model.Tests`/etc. | Whole repo | **Bucket 5/7** — out of scope | None of these share the HTTP composition root; 152 `[Frozen]` occurrences repo-wide (per the original broader inventory) are concentrated here, not in the HTTP-touched projects. A future, separately-scoped migration pass, not this one. |
| Two legacy hand-rolled fakes (`ActionHandler`, `ActionMessageHandler`) | `AlexaVoxCraft.Model.Apl.Legacy.Tests`, `AlexaVoxCraft.Model.Legacy.Tests` | **Bucket 2** — frozen legacy code | Explicitly stretch/non-blocking per PLAN-0051; not attempted this pass, per explicit product direction not to expand into unrelated stretch work. |
| `BearerTokenHandlerTests.cs` (still absent) | `AlexaVoxCraft.Smapi.Tests`/`AlexaVoxCraft.Http` | N/A — a gap in coverage, not a migration item | Explicitly stretch/non-blocking per PLAN-0051; not attempted this pass. |

No item above is recorded as "not migrated yet" without a reason —
every remaining non-Compono dependency has an explicit classification
and rationale.

## 10. Finding A — ambiguous-constructor selection has no `Register<T>` escape hatch when reached through another composed type's constructor

**Exact reproduction**: `AlexaInteractionModelClient`'s constructor is
`AlexaInteractionModelClient(HttpClient client, ILogger<AlexaInteractionModelClient> logger)`.
Composing it as a theory parameter —

```csharp
[Theory, Compose<SmapiHttpTestProfile>]
public async Task GetAsync_RequestIsValid_ReturnsModel(
    [Shared] TestHttpHandler handler,
    AlexaInteractionModelClient client,   // <- composed directly
    string skillId, string stage, string locale,
    InteractionModelDefinition responseModel)
```

— with `SmapiHttpTestProfile` registering `Register<HttpClient>(context =>
context.Resolve<TestHttpHandler>().CreateClient(baseAddress))` in scope —
produced a **compile-time** error, not a runtime one:

```
error CMP0001: 'System.Net.Http.HttpClient' (reached via AlexaInteractionModelClient.client)
has 3 accessible constructors and no way to disambiguate them
```

**Root cause, traced in `Compono.Generators` source** (not guessed):
`Compono.Generators.Discovery.TransitiveClosureWalker.EnqueueMember`
calls `LeafTypeClassifier.IsProviderResolved(memberType, ...)` for every
constructor-parameter member it walks; only if that returns `true` does
the walker skip trying to select a constructor for the member. `IsProviderResolved`
(`src/Compono.Generators/Discovery/LeafTypeClassifier.cs`) returns `true`
only for: `null`/non-`INamedTypeSymbol`, abstract types, enums,
delegates, built-in simple types (`bool`/`int`/`string`/etc.), a fixed
list of recognized BCL value types (`DateTime`, `DateTimeOffset`, `Guid`,
...), and nullable-of-those. **A concrete, non-abstract, non-value-type
class like `HttpClient` is never provider-resolved**, regardless of any
`Register<T>` in scope — the compile-time walk that decides whether to
attempt constructor selection never consults the registration table at
all; `Register<T>` only affects the separate, later, **runtime**
resolution path. The walker structurally descends into `HttpClient`'s
constructor parameters and hits its 3 accessible constructors with no
compile-time way to know which one (or whether a registration exists) is
intended.

**This matches, and is not a contradiction of, already-documented
Compono architecture** — `skills/compono/SKILL.md`'s "When not to use
Compono" section already names exactly this class of type ("The type has
an ambiguous-constructor BCL shape (e.g. `HttpClient`, `Exception`) —
these hit `CMP0001` with no registration-based escape hatch") and
ADR-0002's Amendment 1 already recorded the `HttpClient` case in the
abstract. What this finding adds is **the first real evidence of it
occurring specifically as a *nested* constructor-parameter dependency of
another composed type** (not `HttpClient` composed directly at the root),
confirmed by tracing the actual generator source rather than inferring
from documentation, and a concrete before/after in a real dogfood
consumer.

**Current `alexa-vox-craft` workaround** (applied, working, evidenced by
the passing test suite): don't compose the client type at all — request
only `[Shared] TestHttpHandler handler` plus the client's other
constructor-independent parameters, and hand-construct the client via a
small per-test-class helper:

```csharp
private static AlexaInteractionModelClient CreateClient(TestHttpHandler handler) =>
    new(handler.CreateClient(BaseAddress), NullLogger<AlexaInteractionModelClient>.Instance);
```

**Classification**: a **core-Compono capability gap**, not a
`Compono.Http` problem — `Compono.Http`'s `TestHttpHandler`/`CreateClient()`
work exactly as designed; the gap is that Compono has no compile-time
mechanism for a consumer to say "this ambiguous-constructor type should
use *this* constructor, composed like so" when that type is reached
structurally rather than composed at the root. **Not fixed in
PLAN-0051** — deliberately out of scope, captured here as the primary
evidence for a separate, future pre-1.0 design deep dive. That future
design's already-stated constraints (from product review, not re-derived
here):

- no Compono-specific constructor-selection attributes on production
  types (a test-composition library must not require production code to
  carry test-only annotations);
- no "greediest constructor"/longest-constructor/first-resolvable-
  constructor guessing;
- no reflection;
- selection should live in Compono composition/profile/test
  configuration, not on the type being composed;
- prefer a source-generated, strongly-typed mechanism over a runtime one;
- the consumer should ideally specify *which* constructor is intended
  while Compono continues to compose that constructor's own parameters
  normally (not hand-construct the whole graph, just disambiguate the
  entry point);
- investigate whether a dedicated constructor-selection API earns its
  keep beyond what `Register<T>(factory)` already offers, rather than
  shipping a second spelling of the same capability.

This document does not attempt that design — evidence only.

## 11. Finding B — a type reachable only via nested `context.Resolve<T>()` inside a registration factory may have no generated plan

**Exact registration/factory** (`SmapiHttpTestProfile.Configure`, before
the fix):

```csharp
.Register<IOptions<SmapiDeveloperAccessTokenOptions>>(context =>
    Options.Create(context.Resolve<SmapiDeveloperAccessTokenOptions>()));
```

**Exact failing call**: `context.Resolve<SmapiDeveloperAccessTokenOptions>()`
— `SmapiDeveloperAccessTokenOptions` is a plain `sealed record` with an
implicit public parameterless constructor and settable `[Required]`
string properties (`ClientId`, `ClientSecret`, `RefreshToken`); nothing
about its own shape is unusual or CMP0001-prone.

**Diagnostic/runtime behavior**: this is **not** a compile-time
diagnostic — the project built successfully. It failed at **test-run
time**, once, for every theory row that reached this registration:

```
Compono.CompositionException: No registration, configuration rule, semantic provider,
test-double provider, built-in provider, or generated plan could satisfy
'SmapiDeveloperAccessTokenOptions'.

Seed: 235636474
```

with a stack trace showing the failure originating inside
`SmapiHttpTestProfile`'s own registration factory, at the
`context.Resolve<SmapiDeveloperAccessTokenOptions>()` call.

**Why no generated plan existed**: `SmapiDeveloperAccessTokenOptions` is
never requested as a `Composer.Create<T>()` root, a composed theory
parameter, or a member of any *other* independently-discovered type
anywhere in the project — the **only** place it's ever mentioned is
inside this one registration factory's own body. Compono's compile-time
discovery (the same `TransitiveClosureWalker` from Finding A) walks from
known roots (`Create<T>()` calls, `[Compose]`-attributed theory
parameters); an arbitrary `context.Resolve<T>()` call written inside an
ordinary C# lambda is not syntactically distinguishable, from the
generator's perspective, from any other method call — it is not itself
treated as a discovery root. So no plan was ever generated for this
record type, and the runtime resolution path (which normally falls back
to a generated plan when no registration matches) had nothing to fall
back to.

**Working project-local workaround** (applied, working): construct the
record directly from provider-resolved primitives instead of asking
Compono to compose the record type itself —

```csharp
.Register<IOptions<SmapiDeveloperAccessTokenOptions>>(context =>
    Options.Create(new SmapiDeveloperAccessTokenOptions
    {
        ClientId = context.Resolve<string>(),
        ClientSecret = context.Resolve<string>(),
        RefreshToken = context.Resolve<string>(),
    }));
```

`context.Resolve<string>()` works unconditionally because `string` is a
built-in simple type (`LeafTypeClassifier.IsBuiltInSimpleType`),
resolved by a built-in provider with no discovery/generated-plan
dependency at all — the same reason `docs/concepts/registrations-and-rules.md`'s
own worked example (`Register<IClock>(context => new
FakeClock(context.Resolve<DateTimeOffset>()))`) never hits this problem:
`DateTimeOffset` is a recognized BCL value type, also provider-resolved
unconditionally. That existing doc example happens to avoid this exact
edge by construction, not by design intent — it simply never nests a
*user-defined* composable type inside a registration factory.

**Classification — explicitly uncertain, preserved as such per product
direction**: this **may or may not** be the same architectural problem
as Finding A. Both ultimately trace back to "Compono's compile-time
discovery only knows about types reached through its own recognized
structural paths (constructor parameters of a discovered type, or a
`[Compose]`-attributed root) — not arbitrary code inside a registration
factory or an ambiguous-constructor concrete class's own constructor
parameters." But they are functionally distinct failure shapes (Finding
A is a **compile-time** `CMP0001`; Finding B is a **runtime**
`CompositionException`, with no compile-time signal at all that anything
is wrong until a test actually runs and hits that code path) and could
plausibly be fixed by either the same mechanism or two different ones
(Finding A's future fix might be "let a consumer specify which
constructor a type uses"; Finding B's might be "let a registration
factory's own nested `Resolve<T>()` calls participate in discovery," a
materially different capability). This document does **not** assume
they're the same problem — that determination belongs to the future
design pass Finding A is already scoped for, with this finding captured
alongside it for that pass to evaluate.

**Not fixed in PLAN-0051** — captured as evidence only, per explicit
product direction.

## 12. Other blocked-migration findings

**None beyond Findings A and B.** Every other domain type composed by
the migrated tests — `InteractionModelDefinition`, `SkillRequest`,
`SkillInvocationResponse<SkillResponse>`, `Product`, `ProductResponse`,
`PurchasingEnabled`, `TransactionResponse` — composed cleanly via plain
Compono auto-composition with **zero** custom registration/specimen-
builder logic, confirmed by real compilation and passing tests (not
assumed from the original AutoFixture specimen builders' apparent
simplicity — each was actually attempted and built clean). This is
itself notable evidence: the pre-migration specimen builders for these
types (`InteractionModelDefinitionSpecimenBuilder`,
`SkillRequestSpecimenBuilder`, `SkillInvocationResponseSpecimenBuilder`,
`InSkillProductSpecimenBuilder`, `TransactionSpecimenBuilder`) existed
purely to hand-build "realistic-looking" nested object graphs with
specific string/enum values — none of them existed to work around a
Compono-relevant composability limitation (no ambiguous constructors, no
BCL types without accessible constructors), so all five were deleted
outright with no replacement needed (bucket 1, per `docs/research/0009-...md`
§1's classification scheme).

## 13. `Compono.Bogus` — evaluated, not adopted

Per PLAN-0051 Task 10e's policy, `Compono.Bogus` adoption was evaluated
against this pass's actual scope, not forced in to demonstrate coverage.
**Result: not adopted, for this slice.** The one real repeated semantic-
data pattern identified in the original broader inventory (the
`amzn1.ask.*`/`amzn1.adg.*`-prefixed Amazon-style ID convention, 29
occurrences across 3+ files) lives entirely in shared
`AlexaVoxCraft.TestKit`/project-local specimen builders **outside** the
HTTP-touched projects this pass migrated (`SkillServiceConfigurationSpecimenBuilder.cs`,
`SkillRequestSpecimenBuilder.cs` under the *repo-wide* `AlexaVoxCraft.TestKit`,
not the now-deleted HTTP-specific one, plus `InSkillProductSpecimenBuilder.cs`
which was deleted as bucket-1 in this pass without needing Bogus). None
of the domain values the migrated tests actually compose (skill IDs,
stages, locales, response models) have a real semantic-realism
requirement the tests care about — every assertion is either an
equality/equivalence check against the same composed value the test
itself configured, or a structural check (status code, header presence),
never a check that a value *looks like* a real Amazon identifier.
**"`Compono.Bogus` provides no meaningful value in this migrated slice"
is the honest result**, recorded as such rather than forcing an
adoption to avoid a null finding.

## 14. Classification summary (10a's 7-bucket scheme, applied)

| Mechanism | Bucket | Outcome |
|---|---|---|
| `HttpMessageHandlerExtensions.ReturnsResponse` (reflection-based) | 3/7 | Replaced by `Compono.Http`'s `TestHttpHandler` (the original HTTP-admission decision, ADR-0051) |
| `HttpClientSpecimenBuilder`/`HttpClientSpecification`/`ClientAutoDataAttribute` family | 3/7 | Replaced by `[Shared] TestHttpHandler` + `[Compose]`/`[Compose<TProfile>]` |
| `InteractionModelDefinitionSpecimenBuilder`, `SkillRequestSpecimenBuilder`, `SkillInvocationResponseSpecimenBuilder`, `InSkillProductSpecimenBuilder`, `TransactionSpecimenBuilder` (+ `RequestSpecifications` siblings) | 1 | Deleted outright — plain Compono auto-composition suffices, confirmed by real build/test |
| `AlexaInteractionModelClient`/`AlexaSkillInvocationClient`/`InSkillPurchasingClient` composition | **6 (core gap, Finding A)** | Not composed via Compono at all — hand-constructed via a `CreateClient(handler)` helper |
| `SmapiDeveloperAccessTokenOptions` composition inside a registration factory | **6 (core gap, Finding B)** | Built from resolved primitives instead of `context.Resolve<TheRecordType>()` |
| `IHttpClientFactory` for `SmapiDeveloperAccessTokenProvider` | 7 (proven, not a gap) | 3-line project-local `FakeHttpClientFactory`, registered via `Register<IHttpClientFactory>` — the exact pattern ADR-0051 §8.2 proposed, now proven working end to end |
| `LocaleHandlerTests.cs`'s predicate-side-effect capture | 3 | Replaced by `TestHttpHandler.Requests` |
| `AlexaVoxCraft.TestKit`'s `BaseFixtureFactory`/`AutoDataAttribute` family (repo-wide, non-HTTP) | 5/7 | Out of scope, explicit boundary (§9) |
| Two legacy hand-rolled fake handlers | 2 | Frozen legacy code, stretch item not attempted |
| `amzn1.*`-prefixed semantic ID pattern | 4 (candidate), not adopted | `Compono.Bogus` evaluated, no real need in the migrated slice (§13) |

## 15. Final acceptance statement

`Compono.Http`'s ADR-0051 acceptance criteria (the original 41-call-site
`ReturnsResponse` reflection workaround) are met, plus the broader
ecosystem-migration goal this plan's review feedback added: the
HTTP-touched slice of `alexa-vox-craft` now composes entirely through
`Compono`, with real, evidenced boundaries around what wasn't migrated
and why, two real core-Compono findings captured (not fixed) for future
design work, and a fully green consumer suite validated twice — once
directly (704/696/8/0, exact match before and after) and once through
the formal fresh-package dogfood gate (2816/2784/32/0). Nothing in this
document expands `Compono.Http`'s own scope beyond ADR-0051; `Compono.Http`
itself is unchanged by any finding here.
