# [RESEARCH-0001] AutoFixture vs. Compono: `cosmere-tracker` Dogfooding

**Status:** Done (all six PLAN-0007 phases complete — Phase 0 baseline,
Phase 1 migration, Phase 5 documentation architecture, Phase 2 evidence
collection, Phase 3 classification, Phase 4 final conclusion — all 73
`cosmere-tracker` tests passing under
Compono (72 migrated plus one new capability test); zero findings
classified bug or roadmap candidate; recommendation: Compono is the
default for all `cosmere-tracker` test code effective immediately — see
"Final architectural conclusion and recommendation" below and
[the migration guide](../migrating-from-autofixture.md) for the full
before/after evidence)

**Feeds:** [PLAN-0007](../plans/0007-milestone-7-dogfooding.md), per
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)

This document is the evidence record for Milestone 7's dogfooding pass:
migrating `ncipollina/cosmere-tracker`'s AutoFixture-based test kit
(`test/Cosmere.Tracker.TestKit`) to Compono. It follows
`design-decisions.md`'s `docs/research/` convention — baseline and
post-migration metrics, per-finding evidence, and a closing `## Decisions`
section mapping each finding to the ADR/Amendment/PR it fed into. Sections
below were filled in as each PLAN-0007 phase completed; with Phase 4 done,
this document is a completed historical record, in the same spirit as an
`Accepted` ADR — settled, not expected to change further, though a future
milestone's own dogfooding pass would be recorded as its own new
`docs/research/NNNN-*.md` document rather than reopening this one.

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
`ICompositionProfile` classes — one concept (a profile) instead of the
base kit's four (`IFixture` factory, `ICustomization`, `ISpecimenBuilder`,
`IRequestSpecification`). Not every tier is activated the same way,
though: `ClientTestProfile` (base tier) and the per-suite local profiles
(`EndpointTestProfile`, `PersistenceTestProfile`) are each selected onto
a test method directly via `[Compose<TProfile>]`; `AddProfile<T>()` is
used differently — only by `EndpointTestProfile` and `PersistenceTestProfile`
internally, each to pull `SharedTestKitProfile` (the shared tier) into
their own `Configure` method. `SharedTestKitProfile` itself is never
directly `[Compose<TProfile>]`-selected by a test in this migration — it's
only ever reached via one of those two `AddProfile<T>()` calls.

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

- **Reusable profiles, providers, and registrations — the total count:**
  4 `ICompositionProfile` classes (`ClientTestProfile`,
  `SharedTestKitProfile`, `EndpointTestProfile`, `PersistenceTestProfile`
  — one per tier plus one per consuming project, matching the "Test kit
  inventory" table above); 9 `Register<T>`/`Register` calls across them
  (2 in `ClientTestProfile` — `HttpMessageHandler`, `IHttpClientProvider`;
  6 in `SharedTestKitProfile` — the edge-item types; 1 in
  `PersistenceTestProfile` — `IOptions<DynamoDbOptions>`); 3
  `UseBogus<T>()` calls (`BookItem`/`CharacterItem`/`WorldItem`, all in
  `SharedTestKitProfile`); 2 `UseNSubstitute()` calls
  (`EndpointTestProfile`, `PersistenceTestProfile`); 2 `AddProfile<T>()`
  calls (both composing `SharedTestKitProfile` into a consuming project's
  own profile). Baseline has no directly equivalent single count to
  compare against — its customization/specimen-builder/attribute trio
  per tier doesn't decompose into the same units — so this is reported as
  a post-migration total for Phase 4's use, not a before/after delta.
- **Framework-specific concepts in play:** `ICompositionProfile`,
  `CompositionBuilder`/`AddProfile<T>()`, `Register<T>()`,
  `ICompositionContext.Resolve<TValue>()`, `[Compose]`/`[Compose<TProfile>]`,
  `[Shared]`, `UseNSubstitute()`, `UseBogus<T>()` — 8
  Compono/Compono.NSubstitute/Compono.Bogus concepts. This list and the
  baseline's 8-concept list above are both illustrative groupings for
  this specific bullet, not a rigorous concept census — neither one
  includes every entry the completed "Concepts removed entirely" section
  below documents (e.g. `ISpecimenContext.Resolve`/`NoSpecimen` on the
  baseline side, surfaced by that section but not itemized here); treat
  a same-count "N down from M" framing as coincidental, not a claimed
  precise reduction — the definitive, complete inventory is that section,
  not this one. What both lists agree on regardless of exact count: the
  base kit's four layered concepts (fixture factory, customization,
  specimen builder, request specification) collapse into one on the
  Compono side (a profile).
  `HttpMessageHandlerExtensions.ReturnsResponse`'s reflection-based
  NSubstitute stubbing (`BindingFlags.NonPublic`) is unchanged and carried
  forward as-is; it was never an AutoFixture concept and isn't a Compono
  one either.
- **Custom fixture infrastructure present:** yes, still three tiers
  (`Cosmere.Tracker.TestKit` → `Cosmere.Tracker.Shared.TestKit` →
  per-suite local profile). Each tier's configuration entry point is now
  one profile class implementing `ICompositionProfile`, rather than
  inherited/subclassed attribute types — activated two different ways,
  not uniformly via `AddProfile<T>()`: the base and per-suite-local
  profiles are each selected directly on a test method via
  `[Compose<TProfile>]`, while only the two per-suite-local profiles
  additionally call `builder.AddProfile<SharedTestKitProfile>()`
  internally to pull the shared tier in (see "Test kit inventory
  (post-migration)" above for the full breakdown). That's the entry
  point, not the whole tier, either way: the base tier
  (`Cosmere.Tracker.TestKit`) still carries 3 files
  (`ClientTestProfile` plus its supporting `IHttpClientProvider`/
  `HttpClientProvider` and `HttpMessageHandlerExtensions`), matching the
  "Test kit inventory" table above. The tier structure survived the
  migration (it reflects a real reuse need, not AutoFixture idiom), and
  the mechanism connecting the tiers got simpler, but "one class per
  tier" would overstate it — only the shared and per-suite local tiers
  are actually a single file each.
- **Setup visible per test method:** two different locations, not one.
  `[Compose]`/`[Compose<TProfile>]` are method-level attributes
  (`ComposeAttribute` targets `AttributeTargets.Method`) — which profile
  is in play is explicit on the method, not in the parameter list.
  `[Shared]` parameters are the ones explicit in the signature itself,
  naming the reused type directly. This is still the direct trade
  ADR-0029 anticipated for gap 1: baseline's `[ClientAutoData]` hid the
  frozen-`HttpMessageHandler` pattern entirely behind the attribute, with
  no visibility anywhere in the test; post-migration,
  `[Compose<ClientTestProfile>]` on the method makes the profile choice
  visible there, and `[Shared] HttpMessageHandler handler` in the
  parameter list makes the shared-handler relationship visible in the
  signature itself, at the cost of a longer signature.
- **Concepts a new contributor needs today:** the 7 Compono-family
  concepts above, plus (unchanged from baseline) knowing which of the
  three fixture-kit tiers to extend for a given change — the tier-count
  didn't shrink, and (per "Custom fixture infrastructure present" above)
  only the shared and per-suite local tiers are a single, self-contained
  profile class; the base tier's configuration entry point is a profile
  but the tier itself still carries supporting types alongside it.

## Concepts removed entirely (Phase 2)

Per [ADR-0029 Amendment 2](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-2-2026-08-02-removed-concepts-get-their-own-explicit-inventory-not-just-a-count),
an explicit named inventory, not just a count — split into two distinct
lists per Amendment 2's own wording, since only the first demonstrates a
real reduction in conceptual complexity; a one-for-one replacement is a
different (also worth recording) kind of finding, not evidence of
elimination. Matches the migration guide's own removed-vs-replaced table
structure.

### Eliminated entirely — no Compono successor

- **`IFixture`** — no Compono equivalent exists or is needed;
  `CompositionBuilder` is configured directly per profile, with no
  central fixture object threading through the kit.
- **`IRequestSpecification`** — nothing replaces it. It existed solely to
  let `HttpClientSpecimenBuilder` detect "this parameter wants an
  `HttpClient`" during specimen resolution; Compono's compile-time
  `[Compose]` parameter typing makes that runtime type-matching step
  unnecessary. (Its removal is entangled with `CMP0001` — see the
  per-finding dossier below.)
- **`BaseFixtureFactory` and other fixture-factory infrastructure** —
  eliminated, including its `ThrowingRecursionBehavior`→
  `OmitOnRecursionBehavior` swap and
  `AutoNSubstituteCustomization { ConfigureMembers = true }` call.
  `OmitOnRecursionBehavior`'s *configurability* has no successor —
  Compono has no per-composition switch to opt into silent omission on a
  construction cycle — but recursion itself isn't absent: generated
  composition plans do recursively resolve dependencies, and a genuine
  construction cycle is detected and fails fast with a path-annotated
  `CompositionException`
  ([ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md)) —
  a real behavior change, not a like-for-like replacement, so it stays in
  this list; see gap 3's dossier entry below for the evidence (zero
  cycles were actually exercised by this migration). `ConfigureMembers`'s
  member-auto-stubbing switch also has no successor at all (contrast with
  `AutoNSubstituteCustomization`'s *substitute-creation* half, which does
  — see the Replaced list below).
- **`NamedRequest`** — nothing replaces it. It existed to let specimen
  builders make name-aware decisions during AutoFixture's
  request-resolution pipeline; Compono's `Register<T>`/`UseBogus<T>`
  factories are typed directly per domain type; there's no generic
  "named request" concept for a factory to inspect.
- **`CosmereTrackerCustomization`** — confirming Phase 0's prediction: it
  was already a no-op with commented-out examples at baseline, and Phase
  1 confirmed no consumer needed a project-wide extension hook at that
  layer, so it was dropped rather than migrated (per ADR-0029's
  "migration idiom" — idiomatic Compono over mechanical translation).
  Nothing replaces it because nothing needed replacing.
- **`DynamoDbResponseSpecimenBuilder`** (not in the original starting
  list — surfaced during Phase 1) — documented explicitly in
  `PersistenceTestProfile`'s XML remarks: it composed a `PartiqlPage` as
  a test parameter, but no test in the project ever requested one that
  way; every real usage constructed `PartiqlPage` directly in the test
  body. Zero real call sites, dropped with nothing replacing it —
  alongside gap 1's `HttpClientSpecimenBuilder` finding of the same
  shape (which *is* replaced — see below).
- **`NoSpecimen`** (not in the original starting list — surfaced by the
  migration guide's specimen-builder examples, e.g.
  `HttpClientSpecimenBuilder.Create`/edge-item specimen builders
  returning `new NoSpecimen()` for a request they don't handle) — nothing
  replaces it. It's AutoFixture's sentinel return value meaning "I don't
  build this, defer to the next specimen builder in the pipeline" —
  Compono has no specimen-builder chain-of-responsibility to decline
  from at all; a `Register<T>` factory unconditionally handles its own
  type, so there's no "not mine, pass it on" case to express.

### Replaced one-for-one — a named Compono successor exists

- **`ICustomization`** → `ICompositionProfile` — not one-for-one in
  shape, though: a profile is a plain class with a
  `Configure(CompositionBuilder)` method, not an extensibility interface
  layered on top of a fixture; there's no `Customize(IFixture)`
  equivalent to implement.
- **`ISpecimenBuilder`** → `builder.Register<T>(Func<ICompositionContext, T>)`
  for hand-built values and `builder.UseBogus<T>(Action<Faker<T>>)` for
  Bogus-generated ones — direct factory functions instead of a builder
  interface participating in AutoFixture's specimen-resolution pipeline.
- **`ISpecimenContext.Resolve`** (the mechanism `HttpClientSpecimenBuilder`
  and the migration guide's other specimen-builder examples used to
  resolve a frozen/nested value by type from inside a builder's `Create`
  method) → `ICompositionContext.Resolve<TValue>()` — the same
  resolve-a-value-from-inside-a-factory shape, used directly in
  `ClientTestProfile`'s `context.Resolve<HttpMessageHandler>()` call. A
  genuine direct successor, not just a conceptual one.
- **`Freeze<T>()`/`[Frozen]`** → `[Shared]`, but only where genuine
  sharing existed — most of its ~30 call sites don't actually belong in
  this list. Endpoint tests (`ListWorldsEndpointTests`, etc.) used
  `[Frozen] ICosmereTrackerRepository repo` purely to obtain a substitute
  — no reuse elsewhere in the composition — so post-migration those
  parameters need no annotation at all (Compono composes an interface to
  a substitute automatically once `UseNSubstitute()` is active); that
  majority case is a clean elimination, not a replacement, and belongs
  conceptually with the list above even though it's the same source
  attribute as the real replacement below. Persistence tests
  (`GetWorldByIdAsync_UsesPkSkPartiql`, etc.) used
  `[Frozen] IDynamoPartiqlClient partiql` where the same substitute
  instance genuinely needed to be visible for both auto-construction
  (`sut`) and stubbing — there, `[Shared] IDynamoPartiqlClient partiql`
  is the direct one-for-one equivalent, which is why this entry is listed
  here. See the migration guide's "NSubstitute `ConfigureMembers`"
  section for the full before/after of both cases.
- **The custom `AutoDataAttribute`/`InlineAutoDataAttribute` subclasses**
  (`CosmereTrackerAutoDataAttribute`, `ClientAutoDataAttribute`, and the
  previously-unlisted `EndpointAutoDataAttribute`/
  `PersistenceAutoDataAttribute` local-kit equivalents surfaced during
  Phase 1) → `[Compose]`/`[Compose<TProfile>]`, which are Compono.XunitV3
  framework attributes, not per-project subclasses — no equivalent
  *custom* attribute class exists anywhere in the migrated kit, but the
  capability they provided (routing a test through the project's fixture
  setup) has a direct, named replacement.
- **`HttpClientSpecimenBuilder`/`HttpClientSpecification`** →
  `ClientTestProfile`/`IHttpClientProvider`. Kept by explicit request
  despite zero current call sites, because it's a capability needed for
  future tests — this is the one baseline concept pair that survived the
  migration in spirit (frozen-handler-backed `HttpClient` composition)
  while changing mechanism entirely (see the per-finding dossier below).
- **`SpecimenBuilderHash`** → `Bogus.Randomizer`/`Faker<T>.UseSeed` —
  Compono.Bogus's own deterministic-seed mechanism
  (`context.DeriveSeed()`) replaces the hand-rolled SHA256 hash-prefix
  helper it used for deterministic values, not a custom hash helper.
- **`DynamoDbOptionsSpecimenBuilder`** → `builder.Register<IOptions<DynamoDbOptions>>(() => ...)`
  in `PersistenceTestProfile`. Unlike `DynamoDbResponseSpecimenBuilder`
  above, this one had a real, load-bearing call site —
  `CosmereTrackerRepository`'s constructor requires
  `IOptions<DynamoDbOptions>` whenever `sut` is composed.
- **`AutoNSubstituteCustomization`'s substitute-creation half** (distinct
  from its member-auto-configuration half, which has no successor — see
  `BaseFixtureFactory` above) → `builder.UseNSubstitute()`: one line per
  profile, registering NSubstitute as the provider for interface types.
  The two halves of `AutoNSubstituteCustomization` had different fates —
  substitute creation itself carried over cleanly; member
  auto-configuration (`ConfigureMembers = true`) did not, per gap 2's
  dossier entry.

### Neither — carried forward unchanged

- **`HttpMessageHandlerExtensions.ReturnsResponse`** (the reflection-based
  NSubstitute stubbing helper) — ported as-is; it was never an
  AutoFixture concept, so migration didn't touch it. Not eliminated (it
  still exists) and not a replacement (nothing about it changed) — a
  third category distinct from both lists above.

## Per-finding evidence dossier (Phase 2)

One entry per finding: ADR-0029's three named gaps (1: frozen shared
values, 2: NSubstitute `ConfigureMembers`, 3: recursion behavior), the
mandatory `Compono.Bogus` finding (a required experiment per Amendment 1,
not one of the three named gaps), and every additional finding Phase 1
surfaced beyond that starting list (`CMP0001`; the Compose-family
binding-validation stacking constraint; `Compono.Bogus`'s exact
member-name-matching ambiguity; `DynamoDbResponseSpecimenBuilder`'s zero
call sites; the three-tier fixture-stack structural finding; and the
pure-inline-`[Theory]` cleanup finding). An
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

- **Frequency:** ~30 real `Freeze<T>()`/`[Frozen]` call sites project-wide
  — ADR-0029's actual gap-1 framing ("hidden shared values") is about
  `Freeze<T>()` broadly, not specifically the `[ClientAutoData]`/
  `HttpClientSpecimenBuilder` pattern the gap was originally illustrated
  with. Most of those ~30 (endpoint tests) used `[Frozen]` purely to
  obtain a substitute, with no real sharing — Compono needs no annotation
  there at all. A real subset (persistence tests, e.g.
  `GetWorldByIdAsync_UsesPkSkPartiql`) used `[Frozen] IDynamoPartiqlClient
  partiql` for genuine sharing (the same substitute instance visible for
  both auto-construction and stubbing), migrated directly to
  `[Shared] IDynamoPartiqlClient partiql` — this is gap 1's real,
  exercised evidence. (The same call sites also inform gap 2's
  `ConfigureMembers` analysis below, for the unstubbed-call behavior
  those tests separately depend on — this entry is about the sharing
  mechanism, gap 2 is about the auto-configuration behavior. The
  `[ClientAutoData]`/`HttpClientSpecimenBuilder`-specific illustration
  ADR-0029 originally used has 0 real call sites of its own outside
  `Cosmere.Tracker.TestKit`'s definition files — migrated anyway by
  explicit request for future HTTP-client tests, and its own workaround
  cost — needing an interface indirection `HttpClient` itself can't
  satisfy — is `CMP0001`'s finding below, not re-classified here to keep
  this entry to one outcome.)
- **Before/after:** see the migration guide's "NSubstitute
  `ConfigureMembers`" section for the `[Frozen]`→`[Shared]` before/after
  this entry's evidence is drawn from, and its gap 1 section for the
  `HttpClientSpecimenBuilder`/`ClientAutoDataAttribute` illustration
  (covered by `CMP0001`'s finding below, not this one).
- **Principle-alignment note:** the exercised evidence — persistence
  tests' genuine sharing — replaced cleanly: `[Frozen] IDynamoPartiqlClient
  partiql` became `[Shared] IDynamoPartiqlClient partiql`, same shape,
  same intent, no extra indirection, no principle conflict, no
  disproportionate complexity. The majority endpoint-test subset needed
  no annotation at all, an even lower cost than a swap.
- **Lean classification:** acceptable Compono-native alternative —
  `[Shared]` is a direct, low-cost, pleasant substitute for
  `Freeze<T>()`/`[Frozen]` everywhere this migration actually exercised
  it, with no downside observed.

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
- **Principle-alignment note:** per ADR-0029's own rubric, workaround cost
  and principle alignment are two separate questions, and this finding
  needs both, not just the second. On cost: the migration guide's own
  verdict on this exact evidence is a **real, material workaround cost**
  — an explicit stub a test previously didn't need to write — not a low
  or zero one; this entry previously understated that as "small," which
  contradicted the migration guide's own recorded verdict on the same
  evidence. On principle alignment: ADR-0029's question 4 asks whether
  *satisfying the gap* — i.e., Compono.NSubstitute restoring
  `ConfigureMembers`-style hidden auto-configuration of substitute return
  values — would conflict with Compono's principles, not whether the
  workaround does. It would: silently auto-configuring a substitute's
  members based on its declared return type is exactly the kind of hidden
  behavior Compono's explicit-over-implicit design bias exists to avoid —
  a test's actual dependency on `ExecuteAsync`'s return shape would go
  back to being invisible in the test body, the same problem this gap's
  own evidence just demonstrated (two tests passing on an implicit
  default they never actually verified). That's a genuine principle
  conflict, not merely a preference.
- **Lean classification:** intentional design difference — per ADR-0029's
  question 3, a real material cost (not a low one) already rules out
  "acceptable alternative," which requires a low-cost, pleasant swap;
  per question 4, satisfying this gap (restoring `ConfigureMembers`-style
  auto-configuration) would conflict with Compono's explicit-over-implicit
  principle, which is exactly this category's own definition — "supporting
  the AutoFixture behavior would conflict with Compono's principles." The
  verdict is "no change": the material cost is the accepted price of a
  deliberate principle, not evidence Compono is missing a capability it
  genuinely needs.

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
- **Before/after:** baseline had no Bogus dependency at all — AutoFixture
  never generated semantic-looking data, only anonymous, nonsemantic
  specimens via its own anonymous-value engine (per ADR-0029's Context);
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

### Finding 4 — Compose-family binding validation blocks stacking distinct Compose-family attributes

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
- **Lean classification:** intentional design difference (unexercised
  constraint, pending Phase 3) — not roadmap candidate. This diagnostic
  fired only while porting `ClientTestProfile`'s capability, which itself
  has zero real pre-migration call sites (see gap 1's `[ClientAutoData]`
  evidence above) — no test in `cosmere-tracker` actually needed to
  compose `HttpClient` before migration; the capability was preserved for
  hypothetical future tests, by explicit request, not because a real test
  hit a wall. ADR-0029 defines observed frequency as pre-migration places
  that actually needed the behavior, and rejects a synthetic exercise as
  roadmap evidence on its own. The interface-wrapper workaround already
  closes this cleanly at the cost this migration actually paid; generic
  disambiguation support for registered/external ambiguous types remains
  a plausible future improvement, but promoting it to a roadmap candidate
  needs a real pre-existing call site this migration doesn't have —
  recorded here for the evidence trail instead, consistent with gap 3's
  and Finding 4's own zero/synthetic-frequency treatment above.

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
- **Lean classification:** acceptable Compono-native alternative — no
  principle conflict and no disproportionate complexity here, the
  opposite: the same required tier structure remains, but its mechanism
  became measurably simpler and more pleasant at low cost, exactly
  ADR-0029's definition of this category rather than "intentional design
  difference" (which requires a principle conflict or a "no change"
  verdict this finding doesn't support).

### Finding 9 — Pure-inline `[Theory]` rows needed no `AutoDataAttribute` wrapper even before migration (project-local cleanup)

- **Frequency:** 7 rows, 1 test class (`TextNormalizerTests`), each a
  `[InlineCosmereTrackerAutoData(...)]` row where every parameter was
  supplied inline and no AutoFixture-composed value was ever used.
- **Before/after:** baseline routed every row through the
  `InlineCosmereTrackerAutoDataAttribute` subclass regardless of whether
  any value was actually composed; post-migration, plain xUnit
  `[InlineData]` is correct and sufficient, with no Compono attribute
  needed at all. See the migration guide's "pure-inline `[Theory]`" note
  for the full before/after snippet. Critically, this wasn't an
  AutoFixture requirement: plain `[InlineData]` was already available and
  would have worked equally well for these seven fully-inline rows while
  AutoFixture was still installed — nothing about AutoFixture forced
  routing through the custom subclass for a row with no composed
  parameter at all.
- **Principle-alignment note:** `[Compose]` itself is method-scoped, not
  parameter-scoped (`ComposeAttribute`'s `[AttributeUsage(AttributeTargets.Method)]`
  — it creates the theory row and, per its own binding rules, composes
  every parameter not supplied inline), so this isn't a case of Compono
  selectively skipping parameters. The real story is narrower and
  project-local: `InlineCosmereTrackerAutoDataAttribute` was
  `cosmere-tracker`'s own wrapper, not something AutoFixture required —
  `TextNormalizerTests` could have used plain `[InlineData]` at any point
  before this migration and lost nothing. Migrating away from the custom
  wrapper simply removed a redundant project-local abstraction; it isn't
  evidence that Compono's model is more capable than AutoFixture's for
  this case, since AutoFixture never stood in the way here either.
- **Lean classification:** migration-only friction — a pre-existing,
  unnecessary project-local wrapper identified and removed during
  migration, not a framework capability difference between AutoFixture
  and Compono.

## Classifications (Phase 3)

Every Phase 2 lean is adopted here as the final verdict — none were
revised during Phase 3. Each lean already reasoned explicitly through
ADR-0029's workaround-cost/principle-alignment rubric (and, for PR #42's
review rounds, was independently challenged and re-verified against that
rubric multiple times before this phase started), so Phase 3's job was to
execute each verdict's recording mechanism, not to re-derive the
classification itself. No finding was classified **bug** — Phase 1's two
`NullReferenceException` failures (gap 2) were real migration-time
failures, but they reflect Compono.NSubstitute's own documented non-goal
working as designed, not a defect against any `Accepted` ADR's claimed
behavior. No finding was classified **roadmap candidate** — every
capability-gap finding either had a low-cost native alternative, was
never materially exercised by a real pre-existing call site, or turned
out to be project-local cleanup rather than a framework capability
question; see `docs/roadmap/post-mvp.md` for why that's a real,
evidence-backed outcome rather than a skipped step.

| Finding | Classification | Recorded via |
|---|---|---|
| Gap 1 — frozen shared values (`Freeze<T>()`/`[Frozen]` → `[Shared]`) | Acceptable Compono-native alternative | This doc + migration guide; no ADR/Amendment |
| Gap 2 — NSubstitute `ConfigureMembers` | Intentional design difference | [ADR-0025 Amendment 2](../adr/0025-compono-nsubstitute-package-design.md#amendment-2-2026-08-04-dogfooding-confirms-the-no-member-auto-configuration-non-goal-at-a-real-material-cost) |
| Gap 3 — recursion behavior | Intentional design difference | [ADR-0011 Amendment 3](../adr/0011-composition-scope-shared-values-and-recursion-detection.md#amendment-3-2026-08-04-dogfooding-confirms-fail-fast-recursion-detection-zero-real-world-exercise-either-way) |
| `Compono.Bogus` mandatory dogfooding | Acceptable Compono-native alternative (verging on improvement) | This doc + [ADR-0029 Amendment 1](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-1-2026-08-02-the-componobogus-experiment-is-mandatory-not-its-conclusion)'s recommendation; no further ADR/Amendment |
| Finding 4 — Compose-family stacking constraint | Intentional design difference (unexercised, no change) | [ADR-0022 Amendment 7](../adr/0022-compono-xunit-package-design.md#amendment-7-2026-08-04-stacking-distinct-compose-family-attributes-stays-unsupported-no-real-call-site-found) |
| Finding 5 — `Compono.Bogus` exact member-name matching | Acceptable Compono-native alternative | This doc + migration guide; no ADR/Amendment |
| Finding 6 — `DynamoDbResponseSpecimenBuilder` zero call sites | Migration-only friction | This doc + migration guide; no ADR/Amendment |
| Finding 7 — `CMP0001` (`HttpClient` construction) | Intentional design difference (unexercised, no change) | [ADR-0002 Amendment 1](../adr/0002-constructor-selection-algorithm.md#amendment-1-2026-08-04-cmp0001-observed-against-a-real-ambiguous-bcl-type-no-change-made) |
| Finding 8 — three-tier fixture stack structural finding | Acceptable Compono-native alternative | This doc; no ADR/Amendment |
| Finding 9 — pure-inline `[Theory]` cleanup | Migration-only friction | This doc + migration guide; no ADR/Amendment |

## Decisions

- **Gap 1** — no ADR/Amendment (acceptable alternative, nothing to
  decide). Documented in this doc's Gap 1 dossier entry and the migration
  guide's "NSubstitute `ConfigureMembers`" section (the `[Frozen]`→
  `[Shared]` before/after this finding's evidence is drawn from).
- **Gap 2** — [ADR-0025 Amendment 2](../adr/0025-compono-nsubstitute-package-design.md#amendment-2-2026-08-04-dogfooding-confirms-the-no-member-auto-configuration-non-goal-at-a-real-material-cost),
  recording the "no change" verdict against ADR-0025's existing
  no-member-auto-configuration non-goal, at a confirmed real cost.
- **Gap 3** — [ADR-0011 Amendment 3](../adr/0011-composition-scope-shared-values-and-recursion-detection.md#amendment-3-2026-08-04-dogfooding-confirms-fail-fast-recursion-detection-zero-real-world-exercise-either-way),
  recording zero real-world exercise of the fail-fast/silent-omission
  question either direction.
- **`Compono.Bogus` mandatory dogfooding** — no new ADR/Amendment;
  [ADR-0029 Amendment 1](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-1-2026-08-02-the-componobogus-experiment-is-mandatory-not-its-conclusion)
  already required this dossier to end in a stated recommendation, and
  this doc's `Compono.Bogus` entry supplies it (continue using
  `UseBogus<T>()`). The two real bugs this dogfooding pass caught and
  fixed (the bypassed-`UseBogus<T>()` hand-roll, the clock-dependent
  `PastOffset` default) were fixed directly in compono PR #40's review
  rounds, in `cosmere-tracker`'s own `SharedTestKitProfile.cs` — a
  migration-time correction, not a Compono-side bug (Compono's own
  `UseBogus<T>()`/`DeriveSeed()` behaved exactly as ADR-0026 documents
  throughout).
- **Finding 4** — [ADR-0022 Amendment 7](../adr/0022-compono-xunit-package-design.md#amendment-7-2026-08-04-stacking-distinct-compose-family-attributes-stays-unsupported-no-real-call-site-found),
  recording the constraint and the "no change" verdict against
  `BindingPlan`'s existing one-Compose-family-attribute rule.
- **Finding 5** — no ADR/Amendment; documented in this doc's Finding 5
  entry and the migration guide's Compono.Bogus section.
- **Finding 6** — no ADR/Amendment; documented in this doc's Finding 6
  entry and the migration guide's specimen-builders section.
- **Finding 7** — [ADR-0002 Amendment 1](../adr/0002-constructor-selection-algorithm.md#amendment-1-2026-08-04-cmp0001-observed-against-a-real-ambiguous-bcl-type-no-change-made),
  recording `CMP0001`'s real trigger, why the originally-anticipated
  `[CompositionConstructor]` attribute wouldn't have closed this specific
  case, and the "no change for now" verdict pending a real (not
  synthetic) pre-existing call site.
- **Finding 8** — no ADR/Amendment; documented in this doc's Finding 8
  entry (the "Broader maintainability dimensions" sections above are the
  underlying evidence).
- **Finding 9** — no ADR/Amendment; documented in this doc's Finding 9
  entry and the migration guide's "pure-inline `[Theory]`" note.

## Final architectural conclusion and recommendation (Phase 4)

Answers to ADR-0029's six "Final architectural conclusion" questions,
each grounded in this document's actual findings, followed by the single
synthesized recommendation Amendment 3 requires.

**Should any manifesto or design-principle language change?** No.
`docs/manifesto.md`'s "Predictability over magic" and "A useful failure is
better than a clever fallback," and `docs/design-principles.md`'s
"Composition over object generation"/"Predictability over magic," each
predicted this migration's actual outcomes rather than being contradicted
by them: gap 2's material-cost-but-principle-aligned verdict is exactly
what "predictability over magic" implies (an explicit stub instead of
hidden auto-configuration), and Finding 9's redundant-wrapper removal is
exactly what "composition over object generation" implies (no
indirection where none is structurally needed). No finding surfaced a
case where the existing language failed to explain or anticipate what
actually happened.

**Did the migration strengthen or weaken confidence in
explicit-over-implicit as Compono's default posture?** Strengthened. The
one finding that tested this posture directly under real cost pressure
(gap 2 — a real, material workaround cost, not a hypothetical one) still
came out in favor of explicit-over-implicit once principle alignment was
actually reasoned through: restoring AutoFixture's hidden
auto-configuration would have reintroduced the exact invisible-dependency
problem the two failing tests demonstrated. `[Shared]` (gap 1's `[Shared] IDynamoPartiqlClient partiql`/
`[Shared] HttpMessageHandler handler` parameters) and `[Compose<TProfile>]`
(gap 1's `[Compose<ClientTestProfile>]`, making the profile choice
explicit on the method where baseline's `[ClientAutoData]` hid it
entirely) both traded a small amount of signature/method-attribute
verbosity for genuine visibility, with no case in this migration where
the explicitness cost outweighed its value. (Finding 9 is a different,
narrower case — a fully inline `[Theory]` needing *no* Compose-family
attribute at all — not an example of trading verbosity for visibility.)

**Did profiles remain the right primary configuration mechanism for a
real project's needs?** Yes, without qualification. All four
`ICompositionProfile` classes this migration produced
(`ClientTestProfile`, `SharedTestKitProfile`, `EndpointTestProfile`,
`PersistenceTestProfile`) map cleanly onto the project's existing
three-tier reuse structure (base kit → shared kit → per-suite local kit,
Finding 8) — the tier structure itself is a real, project-driven need
that survived migration unchanged, while the mechanism connecting the
tiers (profiles composed via `AddProfile<T>()`/selected via
`[Compose<TProfile>]`) got measurably simpler than the baseline's
customization/specimen-builder/attribute trio at every tier. Nothing in
this migration needed a configuration mechanism profiles couldn't
express.

**Was the public provider model (ADR-0024) sufficient for real
application-specific customization, or did it strain anywhere?**
Sufficient, though not stress-tested as a design surface in its own
right: this migration authored zero custom `ICompositionProvider`
implementations. Every real customization went through Compono's
existing built-in extension points —
`Register<T>(Func<ICompositionContext, T>)`, `UseBogus<T>()`,
`UseNSubstitute()` — which fully covered `cosmere-tracker`'s actual
needs (9 `Register` calls, 3 `UseBogus<T>()` calls, 2 `UseNSubstitute()`
calls across the four profiles). That's a genuine, positive data point
(the built-in surface didn't need extending), but it means this
migration is silent on whether the *provider-authoring* experience
itself is well-designed — no finding here tests that question either
way, since nothing forced a project to write its own provider.

**Should any MVP success criterion (`docs/mvp.md`) be revised in light of
real evidence?** No revision needed; every stated Milestone 7 success
measure was assessed against real evidence, not just judgment calls, and
most were met — "failures are reproducible" was not directly exercised
(see below) rather than confirmed met, which doesn't itself call for
revising the criterion, just for recording the assessment honestly:
- *Tests are at least as readable as before* — per-file readability notes
  throughout this document (post-migration section) found every migrated
  file readable top-to-bottom, several strictly more so than their
  baseline equivalent (e.g. `SharedTestKitProfile.cs` concentrating one
  type's whole data shape in one place versus a separate specimen-builder
  file per type at baseline).
- *The composition model remains understandable* — zero findings surfaced
  confusion about what the composition model itself does; every finding
  was about a specific AutoFixture behavior's presence or absence, not
  about profiles/providers/`[Compose]` being hard to reason about.
- *Most setup belongs in profiles rather than custom attributes* — fully
  achieved: zero custom attribute classes exist anywhere in the migrated
  kit (Finding: the custom `AutoDataAttribute` subclasses, "Concepts
  removed entirely"); every reusable behavior lives in one of the four
  profiles.
- *Failures are reproducible* — **not directly exercised, not confirmed
  met.** Gap 3's fail-fast `CompositionException` behavior was never
  triggered by real `cosmere-tracker` code (zero construction cycles),
  so no failing composition was actually reproduced from its seed during
  this migration — observing no regression isn't the same as
  demonstrating reproducibility. `Compono.Bogus`'s `context.DeriveSeed()`
  determinism contract did hold throughout once actually used correctly
  (the two review-caught bugs were migration-time mistakes bypassing that
  contract, not the contract failing), which is real evidence for
  *deterministic data generation* specifically, but that's narrower than
  this success measure as stated. Recorded here as not directly assessed
  by this migration, not as met.
- *Performance does not regress unacceptably* — the one measured
  post-migration run was 54ms faster than the one measured baseline run;
  reported as an observation, not a statistical claim (single sample each
  side), but there is no evidence of regression.
- *Every discovered finding has a recorded classification* — all ten,
  per "Classifications (Phase 3)" above.
- *The research findings are a balanced assessment* — four findings
  classified acceptable Compono-native alternative (including one
  "verging on a genuine improvement," `Compono.Bogus`), four intentional
  design difference, two migration-only friction; the migration guide
  records real workaround costs (gap 2's explicit stub, `CMP0001`'s
  interface wrapper) alongside real wins, not a one-sided account.
- *The migration guide is substantially complete by Phase 4, needing only
  editorial cleanup* — confirmed above; this Phase 4 pass found only
  stale Phase-number cross-references to correct, no content gaps.
- *`docs/roadmap/post-mvp.md` exists and every entry traces to real
  evidence* — it exists, and correctly contains zero entries, per
  ADR-0029's own rule that non-candidate findings don't belong there.
- *Phase 4's final architectural conclusion answers the default-replacement
  question* — this section, directly below.
- *`docs/documentation-architecture.md` exists, covers every section
  named in ADR-0030, and Milestone 8 has a scoped backlog to execute
  against rather than its own design phase* — met: Phase 5 produced
  `docs/documentation-architecture.md` (all 12 ADR-0030 sections),
  ADR-0030 itself, a 58-page documentation skeleton matching that
  architecture, the migration guide's promotion into the primary
  hierarchy, and `docs/plans/0008-milestone-8-public-preview.md` as the
  scoped work-item backlog — see PLAN-0007's Phase 5 for the full record.

**Is Compono now suitable as the default AutoFixture replacement for new
tests in `cosmere-tracker` specifically?** Yes. Every AutoFixture package
reference has already been removed from `cosmere-tracker`'s `test/`
tree — `Directory.Build.props`' global usings and
`Directory.Packages.props`' package references are Compono-only; the only
remaining "AutoFixture" text anywhere in `test/` is documentation
explaining what was migrated away from. This isn't a partial or
incremental migration still in progress — it's complete, for the entire
test suite, not just a representative sample.

### Recommendation

**Compono is the default for all `cosmere-tracker` test code, effective
immediately — there is no remaining AutoFixture code to migrate
incrementally, and no roadmap-candidate finding to wait on.** This follows
directly from the evidence above: the migration is already 100% complete
for `cosmere-tracker` (not partial), zero findings blocked classification
as bug or roadmap candidate, and the one finding that tested
explicit-over-implicit under real cost pressure (gap 2) still confirmed
the posture rather than undermining it. There is nothing left to
sequence — recommending "migrate incrementally" or "wait for a
roadmap item to land" would both be responding to a state of affairs
this migration has already moved past.

For Compono itself, this milestone's evidence supports no MVP scope
change and no urgent roadmap addition. The two findings worth tracking
without a `Proposed` ADR yet — `CMP0001`'s registered/external-type
disambiguation gap
([Finding 7](#finding-7--cmp0001-httpclient-cant-be-composed-directly-compile-time-constructor-selection-limitation),
recorded in
[ADR-0002 Amendment 1](../adr/0002-constructor-selection-algorithm.md#amendment-1-2026-08-04-cmp0001-observed-against-a-real-ambiguous-bcl-type-no-change-made))
and the Compose-family stacking constraint
([Finding 4](#finding-4--compose-family-binding-validation-blocks-stacking-distinct-compose-family-attributes),
recorded in
[ADR-0022 Amendment 7](../adr/0022-compono-xunit-package-design.md#amendment-7-2026-08-04-stacking-distinct-compose-family-attributes-stays-unsupported-no-real-call-site-found))
— are real but unexercised: if a future dogfooding pass (a different real
project, or a future Compono package) produces a genuine pre-existing
call site for either, that's new evidence a future milestone can act on
then — not a reason to design either mechanism speculatively now, per
ADR-0029's evidence-driven restraint. Neither belongs in
`docs/roadmap/post-mvp.md` itself, per ADR-0029's rule that page lists
only roadmap-candidate findings backed by a `Proposed` ADR.
