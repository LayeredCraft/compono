# [RESEARCH-0001] AutoFixture vs. Compono: `cosmere-tracker` Dogfooding

**Status:** In Progress (Phase 0 baseline, Phase 1 migration, and Phase 2
evidence collection complete, all 73 `cosmere-tracker` tests passing under
Compono (72 migrated plus one new capability test); Phase 3's
classification and Phase 4's final conclusion still to come — see
[the migration guide](../migrating-from-autofixture.md) for Phase
1's real before/after findings in the meantime)

**Feeds:** [PLAN-0007](../plans/0007-milestone-7-dogfooding.md), per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)

This document is the evidence record for Milestone 7's dogfooding pass:
migrating `ncipollina/cosmere-tracker`'s AutoFixture-based test kit
(`test/Cosmere.Tracker.TestKit`) to Compono. It follows
`design-decisions.md`'s `docs/research/` convention — baseline and
post-migration metrics, per-finding evidence, and a closing `## Decisions`
section mapping each finding to the ADR/Amendment/PR it fed into. Sections
below are filled in as each PLAN-0007 phase completes; this is a living
document through Phase 3, not a final write-up.

## Baseline (Phase 0)

Captured against `cosmere-tracker` commit `1bae0b6` ("chore(deps): bump
centrally-managed package versions", 2026-08-03), before any Compono
change. This commit is the previous baseline commit (`2dbd62e`) plus one
intentional, unrelated dependency-version refresh in
`Directory.Packages.props` (AWS SDK, FastEndpoints, Microsoft.Extensions.*,
OpenTelemetry, NSubstitute, DynamoMapper, etc.) that was already present
and intentional at capture time — committed here so the cited commit
exactly matches the working tree the baseline numbers were measured
against, rather than leaving the measurement dependent on an uncommitted
diff.

### Test kit inventory

`test/Cosmere.Tracker.TestKit` (218 lines across 8 `.cs` files, no test
project itself — `IsTestProject=false`, referenced by
`Cosmere.Tracker.Shared.TestKit`, which is in turn referenced by all three
consuming test projects):

| File | Lines | Purpose |
|---|---|---|
| `BaseFixtureFactory.cs` | 32 | Central `IFixture` factory: removes `ThrowingRecursionBehavior`, adds `OmitOnRecursionBehavior`, applies `AutoNSubstituteCustomization { ConfigureMembers = true }` |
| `Attributes/CosmereTrackerAutoDataAttribute.cs` | 24 | `AutoDataAttribute`/`InlineAutoDataAttribute` pair wiring `BaseFixtureFactory` + `CosmereTrackerCustomization` |
| `Attributes/ClientAutoDataAttribute.cs` | 24 | Same pair, additionally freezing `HttpMessageHandler` and adding `HttpClientSpecimenBuilder` |
| `Customizations/CosmereTrackerCustomization.cs` | 18 | Currently an empty extension point (commented-out examples only) |
| `SpecimenBuilders/HttpClientSpecimenBuilder.cs` | 36 | Resolves a frozen `HttpMessageHandler` by type via `ISpecimenContext`, wraps it in a configured `HttpClient` |
| `RequestSpecifications/HttpClientSpecification.cs` | 20 | `IRequestSpecification` matching `HttpClient`-typed requests/parameters |
| `Extensions/HttpMessageHandlerExtensions.cs` | 45 | NSubstitute extension method (`ReturnsResponse`) for stubbing `HttpMessageHandler.SendAsync` via reflection (`BindingFlags.NonPublic`) |
| `Requests/NamedRequest.cs` | 19 | Wrapper record letting specimen builders make name-aware decisions; consumed by `Cosmere.Tracker.Shared.TestKit`'s specimen builders, not by `Cosmere.Tracker.TestKit` itself |

A second, separate test kit, `test/Cosmere.Tracker.Shared.TestKit` (271
lines across 6 files: `SharedCustomization`, four domain-item specimen
builders for `BookItem`/`CharacterItem`/`WorldItem`/edge items, and
`SpecimenBuilderHash`), sits between `Cosmere.Tracker.TestKit` and the
three consuming test projects — none of the consuming test projects
reference `Cosmere.Tracker.TestKit` directly; they reference
`Cosmere.Tracker.Shared.TestKit`, which references `Cosmere.Tracker.TestKit`
transitively. `Cosmere.Tracker.Shared.Tests` additionally has its own
local, further test kit under `test/Cosmere.Tracker.Shared.Tests/TestKit/`
(`PersistenceAutoDataAttribute`, DynamoDB-options/response specimen
builders) for persistence-layer tests. This three-tier fixture stack
(base kit → shared kit → per-suite local kit) is itself a maintainability
data point worth carrying into Phase 2's comparison, not previously called
out in ADR-0029's Context.

### Call-site counts (18 test files, `Api.Tests`/`Shared.Tests`/`Seeder.Tests`)

- `[CosmereTrackerAutoData]`: 1 call site
- `[InlineCosmereTrackerAutoData]`: 7 call sites
- `[ClientAutoData]`/`[InlineClientAutoData]`: 0 call sites in these three
  projects (used only within `Cosmere.Tracker.TestKit`'s own definitions;
  no consuming test currently exercises the `HttpClientSpecimenBuilder`
  path directly through this attribute pair) — **confirmed during Phase 1**:
  zero real call sites anywhere in `cosmere-tracker` outside
  `Cosmere.Tracker.TestKit`'s own definition files. This is itself gap 1's
  Phase 1 finding; `HttpClientSpecimenBuilder`/`HttpClientSpecification`/
  `ClientAutoDataAttribute` were nonetheless migrated (as `ClientTestProfile`/
  `IHttpClientProvider`, by explicit request, since this is a capability
  needed for future tests) rather than dropped, surfacing a further real
  finding: `HttpClient` can't be composed directly as a Compono parameter at
  all (`CMP0001`). See the
  [migration guide](../migrating-from-autofixture.md) for the full
  evidence, including the real (and frequent) `[Frozen]`-for-substitute usage
  found elsewhere that gap 1's rubric evidence actually rests on.

### `dotnet test` baseline run

```
dotnet build Cosmere.Tracker.slnx -c Debug   →  0 errors, 66 warnings (pre-existing NuGet advisories/version-constraint warnings, unrelated to the test kit), ~2.5s (incremental)
dotnet test Cosmere.Tracker.slnx -c Debug --no-build
  Cosmere.Tracker.Api.Tests:    32 passed
  Cosmere.Tracker.Shared.Tests: 38 passed
  Cosmere.Tracker.Seeder.Tests:  2 passed
  total: 72, failed: 0, succeeded: 72, skipped: 0
  duration: 1s 346ms (test execution) / ~2.5s wall (incl. process startup)
```

No Docker/Testcontainers dependency was needed to run this suite (Docker
was unavailable in the environment the baseline was captured in, and the
full suite still passed) — persistence tests
(`BatchRepositoryTests`, `DynamoPartiqlClientTests`, etc.) rely on
specimen-built fakes/local DynamoDB abstractions rather than a live
container for this baseline run.

### Per-file readability notes

- **`BaseFixtureFactory.cs`** — small (32 lines) but dense: three distinct
  framework behaviors (recursion-behavior swap, NSubstitute
  auto-configuration, extensibility hook) are combined in one static
  factory method. A reader has to know what `OmitOnRecursionBehavior` and
  `AutoNSubstituteCustomization { ConfigureMembers = true }` each do
  globally — neither is visible at any individual test's call site.
- **`CosmereTrackerCustomization.cs`** — currently a no-op with
  commented-out examples; the abstraction exists but has never been used
  for its stated purpose (specimen builders/frozen dependencies). A
  candidate for being dropped entirely rather than migrated, per
  ADR-0029's "Migration idiom" (idiomatic Compono over mechanical
  translation) — to be confirmed once Phase 1 checks whether any consumer
  actually needs a project-wide profile hook here.
- **`HttpClientSpecimenBuilder.cs`** / **`HttpClientSpecification.cs`** —
  together implement "detect a request for `HttpClient`, resolve a frozen
  `HttpMessageHandler`, wrap it" — the mechanism is AutoFixture-idiomatic
  (an `ISpecimenBuilder` keyed off `IRequestSpecification`) but requires
  two files and an indirect `ISpecimenContext.Resolve` call to trace; nothing
  in a test signature indicates that a `HttpClient` parameter is specially
  constructed this way.
- **`HttpClientSpecification.cs`** — straightforward once found, but
  finding it requires already knowing `HttpClientSpecimenBuilder` delegates
  matching to a separate `IRequestSpecification` type; two hops to
  understand one behavior.

### Broader maintainability dimensions

- **Framework-specific concepts in play:** `IFixture`, `ICustomization`,
  `ISpecimenBuilder`/`IRequestSpecification`, `AutoDataAttribute`/
  `InlineAutoDataAttribute`, `Freeze<T>()`, `AutoNSubstituteCustomization`,
  `ThrowingRecursionBehavior`/`OmitOnRecursionBehavior` — 8 distinct
  AutoFixture/AutoFixture.AutoNSubstitute concepts a contributor needs to
  recognize before the test kit's behavior is fully legible, on top of
  reflection-based NSubstitute stubbing (`HttpMessageHandlerExtensions`,
  `BindingFlags.NonPublic`) that isn't an AutoFixture concept at all but is
  entangled with this kit.
- **Custom fixture infrastructure present:** yes, a three-tier stack (see
  "Test kit inventory" above) — `Cosmere.Tracker.TestKit` →
  `Cosmere.Tracker.Shared.TestKit` → per-suite local kits — each layer
  adding its own attributes/customizations/specimen builders.
- **Setup visible per test method:** minimal by design — `[CosmereTrackerAutoData]`/
  `[InlineCosmereTrackerAutoData]` hide all fixture configuration behind
  the attribute; a reader sees no indication in a test's own signature that
  NSubstitute members are auto-configured, that recursion is omitted rather
  than failing, or (for `ClientAutoData`, currently unused) that an
  `HttpMessageHandler` is frozen and shared.
  This is the direct AutoFixture-vs.-Compono readability question ADR-0029
  frames for gap 1: explicit `[Shared]` parameters trade this invisibility
  for a longer signature.
- **Concepts a new contributor needs today:** the 8 AutoFixture concepts
  above, plus knowing which of the three fixture-kit tiers to extend for a
  given change (base kit vs. shared kit vs. per-suite local kit), before
  writing a new test that needs custom data shape.

## Post-migration metrics (Phase 2)

Captured against `cosmere-tracker` `main` at commit `4d25e14198d5de3291404c424b6aafa6c2a6299a`
("feat(testkit): migrate from AutoFixture to Compono (#162)", merged
2026-08-03), the squash-merge of PR #162 — the same migration Phase 1's
before/after evidence in [the migration guide](../migrating-from-autofixture.md)
was drawn from, now on `main`.

### Test kit inventory (post-migration)

| Project/folder | Files | Lines | Purpose |
|---|---|---|---|
| `Cosmere.Tracker.TestKit` | 3 | 95 | `IHttpClientProvider`/`HttpClientProvider` (`Http/`), `ClientTestProfile` (`Profiles/`), `HttpMessageHandlerExtensions.ReturnsResponse` (`Extensions/`) |
| `Cosmere.Tracker.Shared.TestKit` | 1 | 132 | `SharedTestKitProfile` — domain items (`BookItem`/`CharacterItem`/`WorldItem` via `UseBogus<T>()`, edge items via `Register<T>`) |
| `Cosmere.Tracker.Api.Tests/TestKit/Profiles/EndpointTestProfile.cs` | 1 | 19 | `EndpointTestProfile` |
| `Cosmere.Tracker.Shared.Tests/TestKit/Profiles/PersistenceTestProfile.cs` | 1 | 38 | `PersistenceTestProfile` |
| **Kit infrastructure subtotal** | **6** | **284** | |
| `Cosmere.Tracker.Api.Tests/TestKit/ClientTestProfileTests.cs` | 1 | 31 | New capability test exercising `ClientTestProfile`/`IHttpClientProvider` — a test, not kit infrastructure |
| **Total (kit infrastructure + the new capability test)** | **7** | **315** | |

The baseline only line-counted its first two tiers (base kit + shared kit)
— `Cosmere.Tracker.TestKit`: 218 lines/8 files, `Cosmere.Tracker.Shared.TestKit`:
271 lines/6 files, 489 lines/14 files total — leaving the third tier
(per-suite local kits) described but uncounted. The same two tiers
post-migration are `Cosmere.Tracker.TestKit` (95/3) +
`Cosmere.Tracker.Shared.TestKit` (132/1) = **227 lines across 4 files, a
54% line reduction and a 71% file reduction** against the baseline's
directly-comparable 489 lines/14 files — this is the apples-to-apples
number, not the 6- or 7-file kit-infrastructure totals above, which also
include the third tier the baseline never quantified.

The third tier (`EndpointTestProfile.cs` + `PersistenceTestProfile.cs`,
57 lines/2 files) has no baseline figure to compare against, but is
included in the 284/6 kit-infrastructure subtotal above for completeness.
Across all three tiers, the baseline's three-tier fixture stack (base kit
→ shared kit → per-suite local kit, each with its own attributes/
customizations/specimen builders) collapses post-migration to plain
`ICompositionProfile` classes composed via `AddProfile<T>()` at every
tier — one concept (a profile) instead of the base kit's four (`IFixture`
factory, `ICustomization`, `ISpecimenBuilder`, `IRequestSpecification`).

### `dotnet test` post-migration run

```
dotnet build Cosmere.Tracker.slnx -c Debug   →  0 errors, 753 warnings (pre-existing style warnings unrelated to the migration), ~[build time not separately isolated]
dotnet test Cosmere.Tracker.slnx -c Debug --no-build
  Cosmere.Tracker.Api.Tests:    33 passed (362ms)
  Cosmere.Tracker.Seeder.Tests:  2 passed (396ms)
  Cosmere.Tracker.Shared.Tests: 38 passed (428ms)
  total: 73, failed: 0, succeeded: 73, skipped: 0
  duration: 1s 292ms (test execution)
```

73 tests (72 migrated + 1 new `ClientTestProfileTests` capability test,
added to actually exercise the `ClientTestProfile`/`IHttpClientProvider`
path this migration introduced) vs. the baseline's 72 — zero regressions.
The one measured post-migration run (1s 292ms) was 54ms faster than the
one measured baseline run (1s 346ms); with only a single sample on each
side and no repeated runs or variance data, this doesn't support a
statistical claim either way — it's reported here as the two observed
numbers, not evidence of a performance difference. The 753 warnings are
pre-existing repo-wide style warnings (see `dotnet build`
output), unrelated to the test kit change; the baseline's 66 warnings were
narrower NuGet-advisory/version-constraint warnings from a different
`dotnet build` invocation, so the two counts aren't directly comparable to
each other — both are noted here for completeness, not as a regression
signal.

### Per-file readability notes (post-migration)

- **`SharedTestKitProfile.cs`** (132 lines, the largest single file) —
  denser than any individual baseline file, but the density is
  concentrated in one place instead of spread across six
  (`SharedCustomization`, the four domain-item specimen builders —
  `BookItemSpecimenBuilder`/`CharacterItemSpecimenBuilder`/
  `WorldItemSpecimenBuilder`/`EdgeItemSpecimenBuilder` — and
  `SpecimenBuilderHash`): one `Configure` method wires all three
  `UseBogus<T>()` calls plus six edge-item `Register<T>` calls, and each
  domain type's faker rules live in one adjacent private method
  (`ConfigureBookItem`/`ConfigureCharacterItem`/`ConfigureWorldItem`)
  directly below `Configure` — a reader sees the whole shape of one type's
  generated data in one place, rather than needing to find a separate
  specimen-builder file per type as the baseline required.
- **`ClientTestProfile.cs`** + **`IHttpClientProvider.cs`** (Compono
  side of the migrated `HttpClientSpecimenBuilder`/`HttpClientSpecification`
  pair) — two files, same as baseline, but the indirection changed
  character: baseline's two hops were "which `IRequestSpecification`
  matches, which `ISpecimenBuilder` handles it"; post-migration's two hops
  are "why can't `HttpClient` be composed directly (`IHttpClientProvider`'s
  XML remarks explain `CMP0001`), what does the profile register instead."
  The latter is a documented compiler limitation with a doc-comment
  pointing at ADR-0002; the former had no equivalent signpost anywhere in
  the baseline code.
- **`EndpointTestProfile.cs`** (19 lines) / **`PersistenceTestProfile.cs`**
  (38 lines, including its documented decision not to port
  `DynamoDbResponseSpecimenBuilder`) — both replace the baseline's
  per-suite local kit tier (`EndpointAutoDataAttribute`,
  `PersistenceAutoDataAttribute` + DynamoDB specimen builders) with a
  profile that composes `SharedTestKitProfile` and adds
  `UseNSubstitute()`/suite-specific registrations. Readable top-to-bottom
  in one file each; the baseline's equivalent required knowing which
  `AutoDataAttribute` subclass to look up per suite, then following it
  into whichever specimen builder it referenced.
- **No equivalent of `BaseFixtureFactory.cs`, `CosmereTrackerCustomization.cs`,
  or `NamedRequest.cs` exists post-migration** — see "Concepts removed
  entirely" below for what happened to each.

### Broader maintainability dimensions (post-migration)

- **Framework-specific concepts in play:** `ICompositionProfile`,
  `CompositionBuilder`/`AddProfile<T>()`, `Register<T>()`,
  `[Compose]`/`[Compose<TProfile>]`, `[Shared]`, `UseNSubstitute()`,
  `UseBogus<T>()` — 7 Compono/Compono.NSubstitute/Compono.Bogus concepts,
  down from the baseline's 8 AutoFixture/AutoFixture.AutoNSubstitute
  concepts, but composed into fewer files per concept (the base kit's four
  layered concepts — fixture factory, customization, specimen builder,
  request specification — collapse into one: a profile).
  `HttpMessageHandlerExtensions.ReturnsResponse`'s reflection-based
  NSubstitute stubbing (`BindingFlags.NonPublic`) is unchanged and carried
  forward as-is; it was never an AutoFixture concept and isn't a Compono
  one either.
- **Custom fixture infrastructure present:** yes, still three tiers
  (`Cosmere.Tracker.TestKit` → `Cosmere.Tracker.Shared.TestKit` →
  per-suite local profile). Each tier's configuration entry point is now
  one profile class implementing `ICompositionProfile`, composed via
  `builder.AddProfile<T>()` rather than inherited/subclassed attribute
  types — but that's the entry point, not the whole tier: the base tier
  (`Cosmere.Tracker.TestKit`) still carries 3 files
  (`ClientTestProfile` plus its supporting `IHttpClientProvider`/
  `HttpClientProvider` and `HttpMessageHandlerExtensions`), matching the
  "Test kit inventory" table above. The tier structure survived the
  migration (it reflects a real reuse need, not AutoFixture idiom), and
  the mechanism connecting the tiers got simpler, but "one class per
  tier" would overstate it — only the shared and per-suite local tiers
  are actually a single file each.
- **Setup visible per test method:** `[Compose]`/`[Compose<TProfile>]`
  parameters are explicit in the test method signature about which
  profile is in play, and `[Shared]` parameters are explicit about reused
  instances — this is the direct trade ADR-0029 anticipated for gap 1:
  baseline's `[ClientAutoData]` hid the frozen-`HttpMessageHandler` pattern
  entirely behind the attribute, where `ClientTestProfile` composed via
  `[Compose<ClientTestProfile>]` makes the shared-handler relationship
  visible in the test signature at the cost of a longer signature.
- **Concepts a new contributor needs today:** the 7 Compono-family
  concepts above, plus (unchanged from baseline) knowing which of the
  three fixture-kit tiers to extend for a given change — the tier-count
  didn't shrink, and (per "Custom fixture infrastructure present" above)
  only the shared and per-suite local tiers are a single, self-contained
  profile class; the base tier's configuration entry point is a profile
  but the tier itself still carries supporting types alongside it.

## Concepts removed entirely (Phase 2)

Per [ADR-0029 Amendment 2](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-2-2026-08-02-removed-concepts-get-their-own-explicit-inventory-not-just-a-count),
an explicit named inventory, not just a count — which of the following
disappeared entirely after migration versus were merely replaced
one-for-one, and what (if anything) replaced each:

- **`IFixture`** — disappeared entirely. No Compono equivalent exists or
  is needed; `CompositionBuilder` is configured directly per profile, with
  no central fixture object threading through the kit.
- **`ICustomization`** — disappeared entirely. Replaced by
  `ICompositionProfile` — but not one-for-one: a profile is a plain class
  with a `Configure(CompositionBuilder)` method, not an extensibility
  interface layered on top of a fixture; there's no `Customize(IFixture)`
  equivalent to implement.
- **`ISpecimenBuilder`** — disappeared entirely. Replaced by
  `builder.Register<T>(Func<ICompositionContext, T>)` for hand-built
  values and `builder.UseBogus<T>(Action<Faker<T>>)` for Bogus-generated
  ones — direct factory functions instead of a builder interface
  participating in AutoFixture's specimen-resolution pipeline.
- **`IRequestSpecification`** — disappeared entirely, with nothing
  replacing it. It existed solely to let `HttpClientSpecimenBuilder`
  detect "this parameter wants an `HttpClient`" during specimen
  resolution; Compono's compile-time `[Compose]` parameter typing makes
  that runtime type-matching step unnecessary. (Its removal is entangled
  with `CMP0001` — see the per-finding dossier below.)
- **`Freeze<T>()`/`[Frozen]`** — disappeared at most of its ~30 call
  sites, replaced one-for-one with `[Shared]` where genuine sharing
  existed. Endpoint tests (`ListWorldsEndpointTests`, etc.) used
  `[Frozen] ICosmereTrackerRepository repo` purely to obtain a substitute
  — no reuse elsewhere in the composition — so post-migration those
  parameters need no annotation at all (Compono composes an interface to
  a substitute automatically once `UseNSubstitute()` is active); this is
  the majority case, a clean elimination rather than a replacement.
  Persistence tests (`GetWorldByIdAsync_UsesPkSkPartiql`, etc.) used
  `[Frozen] IDynamoPartiqlClient partiql` where the same substitute
  instance genuinely needed to be visible for both auto-construction
  (`sut`) and stubbing — there, `[Shared] IDynamoPartiqlClient partiql` is
  the direct equivalent. See the migration guide's "NSubstitute
  `ConfigureMembers`" section for the full before/after of both cases.
- **The custom `AutoDataAttribute`/`InlineAutoDataAttribute` subclasses**
  (`CosmereTrackerAutoDataAttribute`, `ClientAutoDataAttribute`,
  and the previously-unlisted `EndpointAutoDataAttribute`/
  `PersistenceAutoDataAttribute` local-kit equivalents surfaced during
  Phase 1) — all disappeared entirely. Replaced by
  `[Compose]`/`[Compose<TProfile>]`, which are Compono.XunitV3 framework
  attributes, not per-project subclasses — no equivalent custom attribute
  class exists anywhere in the migrated kit.
- **`BaseFixtureFactory` and other fixture-factory infrastructure** —
  disappeared entirely, including its
  `ThrowingRecursionBehavior`→`OmitOnRecursionBehavior` swap and
  `AutoNSubstituteCustomization { ConfigureMembers = true }` call.
  `OmitOnRecursionBehavior`'s *configurability* disappeared — Compono has
  no per-composition switch to opt into silent omission on a construction
  cycle — but recursion itself isn't absent: generated composition plans
  do recursively resolve dependencies, and a genuine construction cycle is
  detected and fails fast with a path-annotated `CompositionException`
  ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)).
  The actual replacement is fail-fast diagnostics for the cycle case, not
  an absence of recursion; see gap 3's dossier entry below for the
  evidence (zero cycles were actually exercised by this migration). No
  `ConfigureMembers`-style global auto-stubbing switch exists either;
  `UseNSubstitute()` registers NSubstitute as the provider for interface
  types, full stop — narrower in scope than `BaseFixtureFactory`'s
  combined behavior, but not because Compono can't recurse.
- **`NamedRequest`** — disappeared entirely, with nothing replacing it. It
  existed to let specimen builders make name-aware decisions during
  AutoFixture's request-resolution pipeline; Compono's `Register<T>`/
  `UseBogus<T>` factories are typed directly per domain type; there's no
  generic "named request" concept for a factory to inspect.
- **`CosmereTrackerCustomization`** — disappeared entirely, confirming
  Phase 0's prediction: it was already a no-op with commented-out examples
  at baseline, and Phase 1 confirmed no consumer needed a project-wide
  extension hook at that layer, so it was dropped rather than migrated
  (per ADR-0029's "migration idiom" — idiomatic Compono over mechanical
  translation).
- **`DynamoDbResponseSpecimenBuilder`** (not in the original starting
  list — surfaced during Phase 1) — disappeared entirely, documented
  explicitly in `PersistenceTestProfile`'s XML remarks: it composed a
  `PartiqlPage` as a test parameter, but no test in the project ever
  requested one that way; every real usage constructed `PartiqlPage`
  directly in the test body. Zero real call sites, alongside gap 1's
  `HttpClientSpecimenBuilder` finding of the same shape.
- **Not removed — carried forward unchanged:**
  `HttpMessageHandlerExtensions.ReturnsResponse` (the reflection-based
  NSubstitute stubbing helper) ported as-is; it was never an AutoFixture
  concept, so migration didn't touch it.
- **Not removed — replaced one-for-one, not eliminated:**
  `HttpClientSpecimenBuilder`/`HttpClientSpecification` →
  `ClientTestProfile`/`IHttpClientProvider`. Kept by explicit request
  despite zero current call sites, because it's a capability needed for
  future tests — this is the one baseline concept pair that survived the
  migration in spirit (frozen-handler-backed `HttpClient` composition)
  while changing mechanism entirely (see the per-finding dossier below).

## Per-finding evidence dossier (Phase 2)

One entry per finding: ADR-0029's three named gaps (1: frozen shared
values, 2: NSubstitute `ConfigureMembers`, 3: recursion behavior), the
mandatory `Compono.Bogus` finding (a required experiment per Amendment 1,
not one of the three named gaps), and every additional finding Phase 1
surfaced beyond that starting list (`CMP0001`; the `[AttributeUsage(AllowMultiple
= false)]` Compose-family stacking constraint; `Compono.Bogus`'s exact
member-name-matching ambiguity; `DynamoDbResponseSpecimenBuilder`'s zero
call sites; the three-tier fixture-stack structural finding; and the
pure-inline-`[Theory]` positive finding). An
earlier draft of this dossier mislabeled the `Compono.Bogus` finding as
"gap 3" and omitted the recursion-behavior gap along with three of these
additional findings entirely — corrected here; see ADR-0029's Context
section for the three gaps' actual definitions. Each entry gives
frequency, a before/after snippet (or a pointer to one already recorded in
[the migration guide](../migrating-from-autofixture.md), per this repo's
link-don't-duplicate documentation principle), a principle-alignment note,
and a classification per ADR-0029's five-way taxonomy (bug / roadmap
candidate / acceptable Compono-native alternative / intentional design
difference / migration-only friction). Full classification rationale is
deferred to Phase 3; the note here is a first-pass lean, not the final
call.

### Gap 1 — frozen `HttpMessageHandler` for `HttpClient` composition

- **Frequency:** two distinct populations, not one. (a) 0 real call sites
  for `[ClientAutoData]`/`[InlineClientAutoData]` specifically —
  `HttpClientSpecimenBuilder`'s hidden-frozen-`HttpMessageHandler` pattern
  ADR-0029 originally framed this gap around — anywhere in `cosmere-tracker`
  outside `Cosmere.Tracker.TestKit`'s own definition files (confirmed
  Phase 1); migrated anyway, by explicit request, since it's needed for
  future HTTP-client tests. (b) `Freeze<T>()`/`[Frozen]` more broadly
  (ADR-0029's actual gap-1 framing — "hidden shared values") has ~30 real
  call sites project-wide. Most of those (endpoint tests) used `[Frozen]`
  purely to obtain a substitute, with no real sharing — Compono needs no
  annotation there at all. A real subset (persistence tests, e.g.
  `GetWorldByIdAsync_UsesPkSkPartiql`) used `[Frozen] IDynamoPartiqlClient
  partiql` for genuine sharing (the same substitute instance visible for
  both auto-construction and stubbing), migrated directly to
  `[Shared] IDynamoPartiqlClient partiql` — this is gap 1's real,
  exercised evidence: explicit `[Shared]` is a direct, low-cost
  replacement for AutoFixture's hidden-frozen-value idiom where genuine
  sharing existed. (The same call sites also inform gap 2's
  `ConfigureMembers` analysis below, for the unstubbed-call behavior
  those tests separately depend on — this entry is about the sharing
  mechanism, gap 2 is about the auto-configuration behavior.)
- **Before/after:** see the migration guide's gap 1 section for the full
  `HttpClientSpecimenBuilder`/`ClientAutoDataAttribute` before-snippet
  (population (a)) and its "NSubstitute `ConfigureMembers`" section for
  the `[Frozen]`→`[Shared]` before/after (population (b)); after for (a)
  is `ClientTestProfile` (`test/Cosmere.Tracker.TestKit/Profiles/ClientTestProfile.cs`)
  + `IHttpClientProvider` (`test/Cosmere.Tracker.TestKit/Http/IHttpClientProvider.cs`).
- **Principle-alignment note:** for (a), the migrated form makes the
  frozen-handler relationship an explicit, composable dependency
  (`IHttpClientProvider` resolved from a shared `HttpMessageHandler`)
  rather than an attribute-hidden specimen-resolution rule — aligns with
  Compono's explicit-composition design principle, at the cost of needing
  an interface indirection `HttpClient` itself can't satisfy (see `CMP0001`
  below).
- **Lean classification:** intentional design difference — the frozen
  handler pattern maps to a real Compono construct with equivalent
  capability, just expressed as an explicit interface dependency rather
  than an implicit specimen-builder rule.

### Gap 2 — `[Frozen]`-for-substitute + auto-configured members (the 2 `NullReferenceException` tests)

- **Frequency:** 2 test failures on first migration attempt
  (`ListWorldsAsync_WhenSortEmpty_DefaultsToName`,
  `ListCharactersAsync_WhenSortEmpty_DefaultsToName`), plus the
  broader-pattern evidence in the migration guide of how often
  `[Frozen]`-for-substitute was used baseline-wide.
- **Before/after:** baseline relied on
  `AutoNSubstituteCustomization { ConfigureMembers = true }` (part of
  `BaseFixtureFactory`) auto-configuring substitute return values for any
  unstubbed call; after migration, bare `Substitute.For<T>()` via
  `UseNSubstitute()` has no auto-configuration equivalent. For the
  observed case specifically — `partiql.ExecuteAsync(...)`, a
  `Task<PartiqlPage>`-returning member — the unstubbed call returned
  NSubstitute's own default for that shape: a non-null completed `Task`
  whose `Result` was `null`, not a null `Task` itself. The repository's
  own code dereferenced that null `Result`, throwing
  `NullReferenceException`. Fixed by adding explicit
  `.ReturnsForAnyArgs(new PartiqlPage([], null))` stubs — see the
  migration guide for the full before/after.
- **Principle-alignment note:** the failure is a real behavioral gap, not
  a migration mistake — Compono.NSubstitute has no `ConfigureMembers`
  equivalent. Making previously-implicit stub behavior explicit surfaced
  two tests that were passing on an implicit default rather than an
  intentional stub, arguably a latent test-quality issue baseline
  papered over.
- **Lean classification:** acceptable Compono-native alternative —
  explicit stubbing is more verbose per call site but removes a global,
  easy-to-forget auto-configuration behavior; the 2-test fix cost was
  small relative to the clarity gained.

### Gap 3 — recursion behavior (`OmitOnRecursionBehavior` vs. fail-fast)

- **Frequency:** zero — no construction-cycle failure was ever triggered
  during this migration. None of `cosmere-tracker`'s composed types
  (`BookItem`/`CharacterItem`/`WorldItem`/edge items,
  `CosmereTrackerRepository`) form a self-referencing graph; edges
  reference other entities by string id, not by object reference.
- **Before/after:** baseline's `BaseFixtureFactory` removed AutoFixture's
  default `ThrowingRecursionBehavior` and added `OmitOnRecursionBehavior`
  (silently omit a value on a construction cycle rather than failing);
  post-migration there is nothing to configure — Compono has no
  per-composition recursion-behavior switch to opt into at all, and a
  genuine construction cycle always fails fast with a path-annotated
  `CompositionException` ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)).
  See the migration guide's "Recursion behaviors" section for the full
  before/after.
- **Principle-alignment note:** this gap was never actually exercised,
  positively or negatively, by `cosmere-tracker`'s codebase — the
  evidence this migration can offer is an absence, not a comparison.
  Compono's fail-fast behavior is a deliberate design choice
  (ADR-0011), and nothing in this migration surfaced a case where
  AutoFixture's silent-omission alternative would have been needed or
  missed.
- **Lean classification:** intentional design difference — zero observed
  frequency means there's no evidence-driven case for changing Compono's
  fail-fast behavior; per ADR-0029's evidence-driven restraint, an
  unexercised gap isn't grounds for a roadmap item.

### `Compono.Bogus` mandatory dogfooding (`UseBogus<T>()`)

- **Frequency:** 3 domain types (`BookItem`, `CharacterItem`,
  `WorldItem`) in `SharedTestKitProfile`, each with 2-3 semantic
  string/date `RuleFor` rules — the entirety of `cosmere-tracker`'s
  Bogus-driven data generation.
- **Before/after:** baseline had no Bogus dependency at all (AutoFixture
  generated semantic-looking strings via its own anonymous-value engine);
  after migration, `builder.UseBogus<T>(ConfigureBookItem)` etc. in
  `SharedTestKitProfile.cs`. Two real bugs surfaced and fixed during this
  adoption (both compono PR #40 review findings, recorded in code
  comments in `SharedTestKitProfile.cs`): (1) an initial hand-rolled
  `Register<T>` + `new Faker<T>().UseSeed(...)` implementation that
  bypassed `UseBogus<T>()`'s own context-seeding entirely, based on an
  incorrect belief that its callback lacked context access; (2)
  `DateTimeOffset.UtcNow`/clock-dependent `PastOffset` defaults that broke
  determinism, fixed via a fixed `ReferenceDate` constant.
- **Principle-alignment note:** per
  [Amendment 1](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-1-2026-08-02-the-componobogus-experiment-is-mandatory-not-its-conclusion),
  this dossier owes a stated recommendation, not just a capability
  report. **Recommendation: continue using `Compono.Bogus`'s
  `UseBogus<T>()` in `cosmere-tracker`.** Once actually called through its
  real API (rather than the initial bypassed hand-roll), it composes
  cleanly with `context.DeriveSeed()`'s determinism contract and reads as
  a natural extension of a profile's `Configure` method — no friction
  remained after the two review-caught bugs were fixed. This is a
  positive finding, not a neutral or partial one.
- **Lean classification:** acceptable Compono-native alternative (verging
  on a genuine improvement — semantic realism plus explicit, per-type
  Faker rules in one place, versus AutoFixture's opaque anonymous-value
  generation).

### Finding 4 — `[AttributeUsage(AllowMultiple = false)]` blocks stacking distinct Compose-family attributes

- **Frequency:** 0 real call sites needing the combination in
  `cosmere-tracker` (`TextNormalizerTests`' inline rows were pure-inline,
  `CursorEncoderTests` was fully composed) — discovered as a constraint,
  not hit as a blocking failure.
- **Before/after:** no direct baseline equivalent — AutoFixture allows
  stacking multiple `[InlineAutoData(...)]` instances on one method,
  mixing several inline rows with composed parameters in each. Compono
  has no equivalent: two *different* Compose-family attribute types
  (e.g. `[Compose]` plus `[Compose<MyProfile>]`) compile without
  complaint, but `Compono.XunitV3`'s `BindingPlan.ValidateSignature`
  (`src/Compono.XunitV3/Binding/BindingPlan.cs`) throws a
  `CompositionException` at data-binding time when more than one
  Compose-family attribute is actually present, regardless of closed
  type; only two instances of the *exact same* closed type are caught
  at compile time via `AllowMultiple = false`. See the migration guide's
  "Real limitation found" note (inline-and-composed section) for the
  full mechanism.
- **Principle-alignment note:** a real, discovered constraint with no
  current pressure to close it — `cosmere-tracker` never needed the
  multi-row-plus-composed-parameter combination for real, so this is
  recorded evidence rather than an unblocking need.
- **Lean classification:** intentional design difference (unexercised
  constraint, pending Phase 3) — ADR-0029 requires a roadmap candidate to
  be backed by real observed frequency and workaround cost, and neither
  exists here: `cosmere-tracker` never hit a real test needing this
  combination, so there's no migrated workaround to point to and no
  measurable cost. Recorded as a discovered constraint for the evidence
  record, not promoted to a roadmap candidate on this evidence alone —
  consistent with gap 3's treatment of its own zero-frequency finding
  above. If a future project's dogfooding surfaces a real call site,
  that's new evidence Phase 3 (or a later milestone) can act on.

### Finding 5 — `Compono.Bogus` exact member-name matching can't disambiguate same-named, different-semantic members

- **Frequency:** 2 domain types (`CharacterItem.Name`, `WorldItem.Name`)
  share the literal member name `"Name"` with different semantics (a
  person's name vs. a place name) — surfaced once, while configuring
  `SharedTestKitProfile`'s Bogus rules, not a recurring pattern across
  many types in this codebase.
- **Before/after:** no baseline equivalent — AutoFixture had no
  member-name-aware semantic generation at all. `Compono.Bogus`'s
  `BogusMemberNameProvider` matches purely on `request.Name`, regardless
  of the requesting type (`src/Compono.Bogus/BogusMemberNameProvider.cs`),
  so a single package-wide `BogusOptions.AddAlias("Name", ...)`/
  `AddConvention("Name", ...)` can't serve both `CharacterItem.Name` and
  `WorldItem.Name` correctly. Worked around by not relying on the
  built-in allowlist for these two types — explicit per-type `RuleFor`
  calls in `ConfigureCharacterItem`/`ConfigureWorldItem` instead. See the
  migration guide's "exact member-name matching" note (Compono.Bogus
  section) for the full evidence.
- **Principle-alignment note:** this is exactly the kind of
  non-person-centric domain `Compono.Bogus`'s built-in allowlist wasn't
  designed around (ADR-0029's own framing) — a genuine finding from
  dogfooding it outside the person/address-heavy domains the allowlist
  favors, though the explicit-`RuleFor` workaround cost nothing extra
  since both types already needed their own `RuleFor` calls regardless.
- **Lean classification:** acceptable Compono-native alternative — the
  explicit per-type `RuleFor` calls this migration already needed absorb
  the workaround at zero marginal cost; type-aware member-name matching
  would be a nice-to-have, not a demonstrated necessity.

### Finding 6 — `DynamoDbResponseSpecimenBuilder`: zero real call sites (dropped, not ported)

- **Frequency:** 0 real call sites anywhere in the project — it composed
  a `PartiqlPage` as a test parameter, but every real usage constructed
  `PartiqlPage` directly in the test body instead.
- **Before/after:** baseline had `DynamoDbResponseSpecimenBuilder`
  registered alongside `PersistenceAutoDataAttribute`'s other
  customizations; post-migration, `PersistenceTestProfile`'s XML remarks
  document the decision not to port it, and no equivalent registration
  exists. See the migration guide's specimen-builders section and
  `PersistenceTestProfile.cs`'s own doc comment for the full evidence.
- **Principle-alignment note:** the same zero-real-call-site pattern as
  gap 1's `HttpClientSpecimenBuilder` — but unlike gap 1, this one was
  dropped outright rather than migrated, since no future need was
  identified (contrast with gap 1's "kept for future HTTP-client tests"
  rationale).
- **Lean classification:** migration-only friction — a pre-existing
  piece of unused AutoFixture infrastructure identified and removed
  during migration; not a Compono capability question at all.

### Finding 7 — `CMP0001`: `HttpClient` can't be composed directly (compile-time constructor-selection limitation)

- **Frequency:** 1 diagnostic, discovered once while migrating gap 1;
  applies to any type with 2+ accessible constructors composed directly
  as a `[Compose]` parameter, not just `HttpClient` (`ConstructorSelector.Select`,
  `src/Compono.Generators/Discovery/ConstructorSelector.cs`, has dedicated
  branches only for zero and exactly one accessible constructor — every
  other count, including exactly two, hits `AmbiguousConstructor`/`CMP0001`).
- **Before/after:** no baseline equivalent — this is a Compono-side
  limitation, not an AutoFixture behavior being replaced. Worked around
  via `IHttpClientProvider` (an interface is always treated as a
  provider-resolved leaf, bypassing constructor-selection entirely) — see
  `IHttpClientProvider.cs`'s XML remarks for the full mechanism and its
  pointer to ADR-0002.
- **Principle-alignment note:** ADR-0002's constructor-selection algorithm
  anticipated a `[CompositionConstructor]` disambiguation attribute for
  exactly this case, but it was never implemented. That attribute alone
  wouldn't actually close this specific gap, though: `HttpClient` is a BCL
  type `cosmere-tracker` doesn't own, so a source attribute on its
  constructor was never going to be applicable regardless of whether the
  attribute ships. The real candidate, per the migration guide's own
  framing, is generic support for disambiguating construction of a
  *registered/external* ambiguous type — a mechanism that works for a type
  the consumer can't annotate, which `[CompositionConstructor]` doesn't
  cover on its own.
- **Lean classification:** roadmap candidate — the interface-wrapper
  workaround is viable and arguably fine practice regardless, but generic
  disambiguation support for registered/external ambiguous types (not
  specifically `[CompositionConstructor]`) closes a real, documented gap
  between ADR-0002's design and what this migration actually needed.

### Finding 8 — Three-tier fixture stack maintainability (structural finding, not a specific API gap)

- **Frequency:** applies repo-wide — every one of the three tiers (base
  kit → shared kit → per-suite local kit) existed at baseline and still
  exists post-migration.
- **Before/after:** see "Test kit inventory (post-migration)" above for
  the file/line comparison; the tier count didn't change, and each tier's
  configuration entry point collapsed from a multi-file attribute/
  customization/specimen-builder group into one `ICompositionProfile`
  class — though the base tier still carries supporting types alongside
  its profile (`IHttpClientProvider`/`HttpClientProvider`,
  `HttpMessageHandlerExtensions`), so "one class per tier" would overstate
  it for that tier specifically.
- **Principle-alignment note:** this finding was raised in Phase 0's
  baseline notes as "worth carrying into Phase 2's comparison" — carrying
  it through, the post-migration numbers confirm the tier structure
  itself is a real reuse need (it survived migration unchanged), while
  the per-tier mechanism got measurably simpler.
- **Lean classification:** intentional design difference — not a gap to
  close, a structural observation that Compono simplifies the
  implementation of a pattern the project will keep regardless of test
  framework.

### Finding 9 — Pure-inline `[Theory]` rows need no Compono attribute at all (positive finding)

- **Frequency:** 7 rows, 1 test class (`TextNormalizerTests`), each a
  `[InlineCosmereTrackerAutoData(...)]` row where every parameter was
  supplied inline and no AutoFixture-composed value was ever used.
- **Before/after:** baseline still had to route every row through the
  `InlineCosmereTrackerAutoDataAttribute` subclass regardless of whether
  any value was actually composed; post-migration, plain xUnit
  `[InlineData]` is correct and sufficient, with no Compono attribute
  needed at all. See the migration guide's "pure-inline `[Theory]`" note
  for the full before/after snippet.
- **Principle-alignment note:** `[Compose]` itself is method-scoped, not
  parameter-scoped (`ComposeAttribute`'s `[AttributeUsage(AttributeTargets.Method)]`
  — it creates the theory row and, per its own binding rules, composes
  every parameter not supplied inline). The actual positive finding is
  narrower than "Compose only applies where composed": it's that a fully
  inline `[Theory]` doesn't need *any* Compose-family attribute at all —
  `[InlineData]` alone is sufficient, since there's no parameter left for
  a method-scoped `[Compose]` to do anything with. That's still simpler
  than AutoFixture's idiom (which always routed through the custom
  `AutoDataAttribute` subclass, composed or not), just for a narrower
  reason than parameter-level selectivity.
- **Lean classification:** intentional design difference (positive) — not
  a gap Compono needs to close; AutoFixture's own idiom carried
  unnecessary indirection for this case that a fully inline Compono test
  doesn't have to begin with.

## Classifications (Phase 3)

_To be filled in per ADR-0029's five-way taxonomy (bug / roadmap candidate
/ acceptable Compono-native alternative / intentional design difference /
migration-only friction)._

## Decisions

_To be filled in — lists exactly which ADR(s)/Amendment(s)/bug-fix PR(s)
each finding fed into, per ADR-0029's "Recorded via existing mechanics."_

## Final architectural conclusion and recommendation (Phase 4)

_To be filled in — answers ADR-0029's "Final architectural conclusion"
questions and, per
[Amendment 3](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-3-2026-08-02-the-final-architectural-conclusion-ends-with-an-explicit-recommendation),
synthesizes them into one explicit, evidence-backed recommendation: a
stated next action for `cosmere-tracker` and Compono, not just a capability
statement._
