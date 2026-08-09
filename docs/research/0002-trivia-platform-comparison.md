# [RESEARCH-0002] AutoFixture vs. Compono: `trivia-platform` Pre-Migration Capability Survey

**Status:** Done (survey complete; no migration performed — see "Scope" below)

**Feeds:** [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)

This document is the evidence record for a pre-migration capability-gap
survey of `ncipollina/trivia-platform`'s AutoFixture-based test kit,
following `design-decisions.md`'s `docs/research/` convention and reusing
[ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
rubric/five-way classification framework. Unlike
[RESEARCH-0001](0001-autofixture-comparison.md) (a full Milestone-7
dogfooding migration of `cosmere-tracker`), this is a lighter-weight
**survey, not a migration** — no `Compono` package reference exists
anywhere in `trivia-platform` (confirmed via `grep -rl "Compono"
--include="*.csproj" .` returning nothing at the time of this survey), and
no code in either repo changed as part of it. The rubric's four questions
are therefore answered qualitatively, from Compono's documented model and
`trivia-platform`'s existing AutoFixture call sites, rather than from an
actual before/after migration diff.

## Scope

`trivia-platform` is a much larger and more elaborate AutoFixture test kit
than `cosmere-tracker`'s — seven layered `TestKit` projects (one base kit,
one per platform integration — Alexa, APL, DynamoDB — and one per gameplay
module — Announcements, Commerce, Gameplay, Leaderboard — plus an infra
test kit), roughly sixteen custom `AutoDataAttribute` subclasses, and
several actively-used (not zero-call-site, unlike `cosmere-tracker`'s
`HttpClientSpecimenBuilder`) request-specification/specimen-builder pairs.
The survey read every file under each kit's
`Attributes/`/`Customizations/`/`SpecimenBuilders/`/
`RequestSpecifications/`/`Extensions/` folder, then sampled 2-3 real test
files per consuming module to confirm frequency claims against actual call
sites.

## Inventory

| Mechanism | Module(s) | What it does |
|---|---|---|
| `BaseFixtureFactory` | core | `Fixture()` + `OmitOnRecursionBehavior` + `AutoNSubstituteCustomization{ConfigureMembers=true}` |
| `DisneyTriviaAutoDataAttribute`/`Inline...` | core | Thin `AutoDataAttribute` wrapper around `BaseFixtureFactory` |
| `LazySpecimenBuilder` | core/DynamoDb | Open-generic `Lazy<T>` match, reflectively builds `Lazy<T>` deferring to `context.Resolve(typeof(T))`; 1 real call site (`Lazy<IAmazonDynamoDB>`) |
| `HandlerInputSpecification`/`ResponseBuilderSpecification`/`AttributesManagerSpecification` + matching `SpecimenBuilder`s | Platform.Alexa | Exact-`Type` dispatch on `IHandlerInput`/`IResponseBuilder`/`IAttributesManager`, heavily used (30–45+ call sites via `HandlerAutoData`/`PresenterAutoData`/`InterceptorAutoData`) |
| `DocumentBuilderSpecification`/`DocumentBuilderSpecimenBuilder` | Platform.Apl | Exact `typeof(IDocumentBuilder)` |
| `IntentNameSpecimenBuilder`, `JsonAttributeBagSpecimenBuilder`, `JsonElementSpecimenBuilder` | Platform.Alexa | Inline exact-`Type`-equality dispatch |
| `ResponseModelSpecification`/`ResponseModelSpecimenBuilder` | Commerce | Set-membership over 3 known types |
| `SlotSpecification`/`SlotSpecimenBuilder` | Gameplay | Matches on member **name + type** (`PropertyInfo{Name:"Slots"}`, type `Dictionary<string,Slot>`), regardless of declaring type |
| `ProductSpecimenBuilder`, `UpsellPayloadSpecimenBuilder`, `LeaderboardEntrySpecimenBuilder` | Commerce/Leaderboard | Parameter-name-polymorphic dispatch (`pi.Name.Contains(...)`/`switch` on `pi.Name`) — several distinct values for the same declared type, chosen by parameter name |
| `GameplayIntentSpecimenBuilder`, `DynamoDbOptionsSpecimenBuilder`, `AnnouncementsOptionsSpecimenBuilder`, `RequestSpecimenBuilder` | various | Exact-type, constructor-configured |
| `GamePlayStateCustomization`, `SkillLocalizerCustomization`, `SkillThemeCustomization`, `TimeProviderCustomization`, `ConversationalContextCustomization` | various | Exact-type: build/inject one substitute or fixed value |
| `SlotCustomization`, `UserEventArgumentsCustomization`, `UserEventAnswerArgumentsCustomization`, `SkillRequestCustomization`, `InfraStackCustomization` | various | Member-level `.With(...)` override chains |
| `HandlerFixtureExtensions`, `CommerceFixtureExtensions`, `GameplayFixtureExtensions`, `LeaderboardFixtureExtensions`, `DynamoDbFixtureExtensions` | all | `IFixture` extension methods, called only from `AutoDataAttribute` constructors — declarative setup-time wiring, never mid-test |
| ~16 `*AutoDataAttribute` subclasses | all modules | Each wires the above together; **most take runtime constructor literals** that parameterize the underlying customization/specimen-builder logic per call site (see Finding 1) |

## Findings

Reusing ADR-0029's five-way classification (Bug / Roadmap candidate /
Acceptable Compono-native alternative / Intentional design difference /
Migration-only friction). "Bug" is not a reachable classification here —
no Compono code path was ever exercised against this repo, so there is
nothing to be a defect in.

### Finding 1 — Parameterized custom AutoData attributes (roadmap candidate)

The one genuinely new finding this survey surfaces, not exercised by
`cosmere-tracker`'s dogfooding pass.

- **Frequency:** pervasive. `PersistenceAutoData(repositoryName)` — ~45
  call sites, each a different repository name;
  `AnnouncementsAutoData(validConfig, gameOverEnabled, audienceEnabled,
  audienceItemEnabled, startOffsetDays, endOffsetDays, messageLocale,
  defaultLocale)` — 8 constructor parameters, 18 call sites, each a
  different boolean/locale combination; `HandlerAutoData`/
  `InterceptorAutoData`/`PresenterAutoData(requestType, aplSupported,
  locale, ...)` — hundreds of call sites; `InfraStackAutoData(region,
  account)`. Nearly every one of the ~16 custom attribute subclasses takes
  constructor arguments that change what the underlying customization or
  specimen builder actually produces, not just which type gets composed.
- **Compono's documented answer today:** none, and the gap is narrower
  than it first looks. Two adjacent capabilities are already solved and
  are *not* this finding: requested-type-plus-resolution-site-name
  matching (`CompositionProviderRequest.Name`, an `ICompositionValueProvider` —
  see Finding 2 below) and fixed member-specific overrides
  (`.For<T>().Member(...)`). What's actually missing is a way for a
  compile-time-constant value known at a specific test's call site to
  reach a composition decision made deeper in that test's own graph —
  `[Compose<TProfile>]` selects a fixed, compile-time profile *type* with
  no documented way to carry a call-site value into it, and
  `[Compose(42, "widget")]`'s inline-value binding binds *test method
  parameters* positionally, not a value used inside nested configuration
  logic.
- **Workaround cost:** real and structural, not cosmetic. Compono's
  current model offers two workarounds — a dedicated `ICompositionProfile`
  subclass per distinct configuration variant, or hand-building
  `Composer.Create(builder => ...)` inline in the test body — and both
  remain technically possible even at `AnnouncementsAutoData`'s 8-flag
  argument space. The actual cost is that neither preserves the concise,
  declarative attribute-based idiom (`[Compose<TProfile>]` on the method,
  real composed values in the signature) without substantial duplication:
  a profile-per-variant approach means a new subclass for every argument
  combination a test actually needs, and the inline-`Composer.Create`
  fallback reintroduces per-test setup code the profile idiom exists to
  eliminate. Neither is a hard wall; both are a real, recurring tax on
  every call site this pattern touches.
- **Principle-alignment note:** doesn't obviously require reflection or
  hidden state — attribute constructor arguments are already compile-time
  constants, so a shape that threads a call-site-known constant into
  nested composition configuration wouldn't conflict with
  [ADR-0001](../adr/0001-source-generation-first.md)'s no-reflection-by-default
  posture. This is new territory for Compono (nothing today lets a
  call-site value reach configuration logic that runs deeper than a
  top-level `[Compose(...)]`-bound parameter), not a rejection of an
  existing principle.
- **Classification: roadmap candidate.** High frequency, real material
  cost, no identified principle conflict — recorded as
  [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md),
  now `Accepted` following a deep-design pass (a typed `TConfig` object
  paired with the profile, implemented entirely in `Compono.XunitV3`; see
  [PLAN-0036](../plans/0036-call-site-values-influencing-nested-composition.md)).

### Finding 2 — Parameter/member-name-polymorphic specimen builders (acceptable alternative, documentation gap)

`ProductSpecimenBuilder`, `UpsellPayloadSpecimenBuilder`,
`LeaderboardEntrySpecimenBuilder` (parameter-name `switch`/`Contains`
dispatch) and `SlotSpecification`/`SlotSpecimenBuilder` (member-name+type
match, declaring-type-agnostic).

- **Frequency:** 4 mechanisms, moderate-to-heavy real call sites (e.g.
  `NewGameUpsellHandlerTests`, `UpsellYesHandlerTests`,
  `LockedPackUpsellHandlerTests` for `UpsellPayload`; `SlotSpecimenBuilder`
  reachable through `GameplayHandlerAutoData`'s 127 call sites).
- **Compono's answer:** `CompositionProviderRequest.Name` exposes the
  declaring constructor parameter/required member/test-method-parameter's
  own name for exactly this purpose — a custom `ICompositionValueProvider`
  checking `request.RequestedType == typeof(UpsellPayload) &&
  request.Name == "newGamePayload"` is the documented use case in
  `docs/concepts/providers.md` for shape-based (not fixed-type) matching.
- **Workaround cost:** real but moderate — one hand-written provider per
  polymorphic-by-name family, replacing an `ISpecimenBuilder` (plus,
  sometimes, a separate `IRequestSpecification`) with a single
  `ICompositionValueProvider` of similar shape.
- **Classification: acceptable Compono-native alternative.** No ADR/
  Amendment needed — there's no decision to make, just a pattern worth
  adding to the migration guide, since it currently doesn't call out
  `Name`-based provider matching as a first-class pattern and this is the
  first real evidence it's needed.

### Finding 3 — `Lazy<T>` support (acceptable alternative)

`LazySpecimenBuilder`, 1 real call site (`Lazy<IAmazonDynamoDB>`). No
built-in Compono `Lazy<T>` support exists, but
`Register<Lazy<IAmazonDynamoDB>>(context => new(() =>
context.Resolve<IAmazonDynamoDB>()))` is trivial — registration bypasses
constructor selection entirely, so `Lazy<T>`'s multiple constructors never
risk `CMP0001`. **Classification: acceptable Compono-native alternative** —
single closed type, zero generalized `Lazy<T>` need observed.

### Finding 4 — SDK-interface specimen builders at scale (acceptable alternative)

`HandlerInputSpecimenBuilder`/`ResponseBuilderSpecimenBuilder`/
`AttributesManagerSpecimenBuilder`/`DocumentBuilderSpecimenBuilder` — all
exact-type dispatch on interfaces (`IHandlerInput`, `IResponseBuilder`,
`IAttributesManager`, `IDocumentBuilder`), heavily used. Interfaces are
always provider-resolved in Compono, never hit `CMP0001`; concrete SDK
construction happens inside an ordinary `Register<T>` factory body, not
through compile-time constructor selection, so multi-constructor SDK types
carry no ambiguity risk there either. **Classification: acceptable
Compono-native alternative** — a stronger positive data point than
`cosmere-tracker`'s zero-call-site `HttpClientSpecimenBuilder`: this
pattern is real, heavily-exercised infrastructure that maps cleanly to
`Register<T>` at scale.

### Finding 5 — NSubstitute `ConfigureMembers` recurrence (no new evidence)

`BaseFixtureFactory` applies the same `AutoNSubstituteCustomization
{ConfigureMembers=true}` `cosmere-tracker`'s did. No new evidence toward a
different verdict than
[ADR-0025](../adr/0025-compono-nsubstitute-package-design.md)'s existing
Amendment — same expected migration cost (explicit stubs where a test
silently relied on an auto-configured return value), at larger scale.
**Classification: intentional design difference**, recurring — not a new
finding, no new Amendment written from this survey alone.

### Finding 6 — `OmitOnRecursionBehavior` recurrence (no new evidence)

Same swap present in `BaseFixtureFactory`; no genuinely self-referencing
object graph identified (Gameplay's `GameState`↔`Question` linkage is
one-directional via `context.Create<Question>()`, not a cycle).
**Classification: intentional design difference**, still unexercised,
consistent with [ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md).

### Finding 7 — Compose-family attribute stacking: still unexercised

Every sampled test method uses exactly one `*AutoData` attribute; the
"stacking many customizations" pattern happens *inside* one attribute's
constructor logic (e.g. `GameplayHandlerAutoDataAttribute` wires
Alexa-handler-input + `SlotCustomization` + localizer +
`GamePlayStateCustomization` + `ConversationalContextCustomization`),
mapping cleanly to one Compono profile with many `Register`/`.For()`/
`UseNSubstitute()` calls in one `Configure` method — not to stacking two
*different* Compose-family attributes on one test method.
**Classification: intentional design difference, still zero real call
sites** — consistent with `cosmere-tracker`'s own Finding 4; no new
evidence either direction.

### Finding 8 — Exact-type/member-level customizations (the bulk of the kit)

`GamePlayStateCustomization`, `SkillLocalizerCustomization`,
`SkillThemeCustomization`, `TimeProviderCustomization`,
`ConversationalContextCustomization`, `SlotCustomization`,
`UserEventArgumentsCustomization`, `UserEventAnswerArgumentsCustomization`,
`SkillRequestCustomization`, `InfraStackCustomization`, and
`ResponseModelSpecimenBuilder`'s 3-type set all map directly to
`Register<T>` (exact-type) or `.For<T>().Member(...)` (member override),
including ones with non-trivial construction logic inside the factory.
**Classification: acceptable Compono-native alternative**, already fully
covered by the existing migration guide — no new pattern.

### Finding 9 — ADR-0017 immutable-builder concern: cleared

Explicitly checked: grepped for `.Customize(`/`.Register(`/`.Inject(`
inside `[Fact]`/`[Theory]` bodies across every sampled module. Zero hits —
every customization happens declaratively at `AutoDataAttribute`-
construction time (the direct analog of a profile's `Configure` running
once before composition), never mid-test. `trivia-platform`'s kit is
disciplined the same way `cosmere-tracker`'s was; nothing here conflicts
with [ADR-0017](../adr/0017-immutable-composer-configuration-and-builder-model.md)'s
frozen-configuration decision. **No classification needed** — confirmed
non-conflict, not a finding.

### Finding 10 — Duplicated bootstrap logic (project-local cleanup)

`AnnouncementsAutoDataAttribute` and `InfraStackAutoDataAttribute` bypass
`DisneyTriviaAutoDataAttribute`/`BaseFixtureFactory`, duplicating the
`Fixture()` + behavior + `AutoNSubstituteCustomization` bootstrap inline
rather than reusing it; localizer-substitute setup is independently
copy-pasted across Commerce's and Leaderboard's fixture extensions rather
than shared. **Classification: migration-only friction** — a real
migration would naturally collapse these into one shared profile plus
`AddProfile<T>()` composition, the same simplification `cosmere-tracker`'s
three-tier-stack finding already demonstrated. Not a Compono capability
question.

### Finding 11 — Testcontainers-backed real client injection (non-finding)

`DynamoDbFixtureExtensions.AddDynamoDbPersistence` wires a real
`IAmazonDynamoDB` from a running test container, not an AutoFixture-specific
concept at all — trivially `Register<IAmazonDynamoDB>(_ =>
DynamoContainerFixture.CurrentClient)`, orthogonal to the framework choice.

## Decisions

- **Finding 1** → [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)
  (`Accepted`, implementation tracked in
  [PLAN-0036](../plans/0036-call-site-values-influencing-nested-composition.md)) —
  the only finding from this survey promoted to a new ADR.
- **Findings 2-4, 8** → no ADR/Amendment; each is an acceptable
  Compono-native alternative already covered by existing documentation
  (Finding 2 additionally flags a migration-guide documentation gap —
  `Name`-based provider matching isn't currently called out as a pattern).
- **Findings 5-7** → no new Amendment; each recurs an already-`Accepted`
  verdict ([ADR-0025](../adr/0025-compono-nsubstitute-package-design.md),
  [ADR-0011](../adr/0011-composition-scope-shared-values-and-recursion-detection.md),
  [ADR-0022](../adr/0022-compono-xunit-package-design.md)) with no new
  evidence in either direction.
- **Finding 9** → confirms no conflict with
  [ADR-0017](../adr/0017-immutable-composer-configuration-and-builder-model.md);
  no action.
- **Findings 10-11** → project-local, not recorded against any Compono
  ADR.

## Links

- [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the rubric/classification framework this survey reuses
- [RESEARCH-0001](0001-autofixture-comparison.md) — the prior, full
  dogfooding migration (`cosmere-tracker`) this survey's findings are
  compared against
- [migrating-from-autofixture.md](../migrating-from-autofixture.md) — the
  migration guide; Finding 2 flags a gap in its provider-matching coverage
- `ncipollina/trivia-platform` — the repo surveyed; not part of this
  monorepo, no code there was changed by this survey
