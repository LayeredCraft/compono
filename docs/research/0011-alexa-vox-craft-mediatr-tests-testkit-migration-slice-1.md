# [RESEARCH-0011] `alexa-vox-craft` `AlexaVoxCraft.TestKit` Migration — Slice 1 (`AlexaVoxCraft.MediatR.Tests`)

**Status:** In Progress — Stage 1 and Stage 2 (composition root: AutoFixture/
AutoNSubstitute → Compono, then `Compono.NSubstitute` → `Compono.TestDoubles`)
both done and recorded below. Re-verified against `Compono`/`Compono.XunitV3`/
`Compono.Http`/`Compono.TestDoubles` packages that additionally ship explicit
constructor selection (ADR-0002 Amendment 3/ADR-0052 Part B) — see "Constructor-
selection dogfood re-verification (2026-08-25)" below. `AlexaVoxCraft.MediatR.Lambda.Tests`
remains the next slice, not yet started.

**Feeds:** [ADR-0052](../adr/0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md)'s
design dive (additional real-migration evidence, both positive and
negative) and [docs/roadmap/post-mvp.md](../roadmap/post-mvp.md)'s
eighth-pass entry.

This is the first vertical slice through `alexa-vox-craft`'s shared
`AlexaVoxCraft.TestKit` — the AutoFixture/AutoNSubstitute composition root
9 of the repo's 12 test projects still depend on, explicitly out of scope
for [RESEARCH-0010](0010-alexa-vox-craft-compono-ecosystem-migration.md)'s
HTTP-touched pass. Scope: `AlexaVoxCraft.MediatR.Tests` only (154 tests, 13
files) — chosen for exercising the widest variety of composition behavior
(ordinary composition, `[Frozen]`/`[Shared]` identity, AutoNSubstitute
interfaces, 9 real specimen-builder families, registration-heavy DI tests,
nested object graphs) without pulling in `AlexaVoxCraft.MediatR.Lambda.Tests`
or either of the two explicitly out-of-scope legacy/frozen projects (per
RESEARCH-0010 §9's existing classification, left undisturbed).

## Result

**154/154 passing** (previously 616/616 across 4 TFMs pre-migration,
confirmed 616/616 post-migration too), under **current, unmodified
Compono** — no core change, no new package, no new public API. Full
solution: 2784/2816 (32 skipped), an exact match to the pre-migration
baseline — no regressions elsewhere.

`AlexaVoxCraft.TestKit`'s `ProjectReference` is dropped from this project
entirely; `MediatRAutoDataAttribute`, `BaseFixtureFactory`,
`TestLoggerCustomization`, and 9 specimen builders no longer apply to this
project (`AlexaVoxCraft.TestKit` itself is untouched — still used by the
other 8 dependent projects, a future slice's job).

## What replaced what

| Old (`AlexaVoxCraft.TestKit`) | New (Compono) |
|---|---|
| `MediatRAutoDataAttribute`/`BaseFixtureFactory` | `[Compose<MediatRTestProfile>]` (`Compono.XunitV3`) |
| `[Frozen]` | `[Shared]` |
| `AutoNSubstituteCustomization` (interfaces) | `Compono.NSubstitute`'s `UseNSubstitute()` |
| `TestLoggerCustomization` | `Register<ILogger<PerformanceLoggingBehavior>>` (one closed type, not open-generic) |
| `RequestHandlerDelegateSpecimenBuilder`/factory half of `SkillRequestFactorySpecimenBuilder` | `Register<RequestHandlerDelegate>`/`Register<SkillRequestFactory>` (bare NSubstitute inside the factory) |
| `ServiceCollectionSpecimenBuilder`/`ServiceProviderSpecimenBuilder` | `Register<IServiceCollection>`/`Register<IServiceProvider>` (real, stateful DI objects) |
| `SkillRequestSpecimenBuilder`'s generic fallback | `Register<SkillRequest>` (`LaunchRequest` default) |
| `OptionsSpecimenBuilder`/`SkillServiceConfigurationSpecimenBuilder`'s generic fallback | `Register<SkillServiceConfiguration>`/`Register<IOptions<SkillServiceConfiguration>>` |
| `SkillRequestSpecimenBuilder`/`OptionsSpecimenBuilder`'s **parameter-name-keyed scenarios** | `TestHelper.cs` — explicit local construction per scenario-sensitive test |
| `JsonElementSpecimenBuilder`/`JsonAttributeBagSpecimenBuilder` | Not ported — unnecessary; both types compose cleanly under plain Compono (no custom builder needed at all) |

## Findings, classified per ADR-0029

### Finding 1 — `Compono.NSubstitute` doesn't replicate `ConfigureMembers=true`

**Classification: Acceptable Compono-native alternative.**

`AutoFixture.AutoNSubstitute`'s `AutoNSubstituteCustomization { ConfigureMembers = true }`
recursively auto-populated every composed substitute's reference-typed
members. `Compono.NSubstitute`'s `UseNSubstitute()` has no equivalent
option — an unconfigured substitute's members return CLR defaults (`null`).
Affected: `IHandlerInput.RequestEnvelope`.

- *Frequency*: real, ~18 test methods across 2 files before the fix.
- *Was it intended to work?* No — no Compono ADR ever promised blanket
  member auto-population; not a bug.
- *Cost*: one `Register<IHandlerInput>(context => ...)` line, same shape
  every other registration in the profile already uses.
- *Principle alignment*: a global, recursive `ConfigureMembers`-equivalent
  would be implicit magic with no evidenced product need — deliberately
  **not** added to `Compono.NSubstitute`. One project-local `Register<T>`
  default is the Compono-native answer.

### Finding 2 — plain settable properties (not constructor/`required` members) are never populated by generated default construction

**Classification: Acceptable Compono-native alternative** (though this one
is closer to "already-documented, project-local surprise" than a gap —
see below).

Per `docs/concepts/composition-model.md`: Compono's generated default
construction covers "the type's own shape (constructor, required
members)" — **not** ordinary public settable properties with no
`required` modifier. `alexa-vox-craft`'s model types (`SkillRequest`,
`SkillResponse`, `ResponseBody`, `SkillServiceConfiguration`, ...) are
all plain DTOs: no constructors, no `required` members, only settable
properties. AutoFixture set every one of these by default; Compono leaves
them at CLR defaults (`null`/`false`/etc.) unless a `Register<T>` (or
`.For<T>().Member().Use(...)`) says otherwise.

This wasn't visible until this slice's tests actually dereferenced the
affected properties (`SkillRequest.Request` — abstract, always null
either way — `SkillResponse.Response`, `SkillServiceConfiguration.SkillId`
et al.) — RESEARCH-0010 §12's "composes cleanly" finding for these same
types was accurate as far as it went (`Composer.Create<T>()` doesn't
throw) but didn't probe every settable property's value, only that
composition succeeded.

- *Frequency*: hit 3 distinct types in this one slice
  (`SkillResponse`, `SkillServiceConfiguration`, and `SkillRequest`'s
  already-known abstract-leaf case); likely to recur in any future slice
  composing another of this repo's plain-DTO model types.
- *Was it intended to work?* This is genuinely documented, existing
  Compono behavior (constructor/required-member-only default
  construction) — not a bug, not a surprise once read, but a real,
  material migration-cost source since `alexa-vox-craft`'s model types
  predate Compono and were never annotated with `required`.
- *Cost*: one `Register<T>` per affected type, each a few lines, giving a
  concrete non-null default — no different in shape from Finding 1's fix.
- *Principle alignment*: fully consistent with explicit-over-implicit —
  Compono's actual behavior (populate only what the type's own shape
  requires) is more predictable than AutoFixture's, once known.

### Finding 3 — parameter-name-keyed scenario dispatch (3 old specimen builders)

**Classification: Acceptable Compono-native alternative. ADR-0036
considered and explicitly not used.**

`SkillRequestSpecimenBuilder`, `OptionsSpecimenBuilder`, and
`SkillServiceConfigurationSpecimenBuilder` all inspected the *composing
parameter's own name* (e.g. `helpIntentRequest`, `whitespaceConfiguration`)
to select real, distinct scenario data — two independent vocabularies (a
`Request`-subtype vocabulary, a `SkillServiceConfiguration`-shape
vocabulary), never co-occurring in the same test.

- *Frequency*: ~16-18 genuinely scenario-sensitive call sites across the
  two vocabularies (well under [ADR-0036](../adr/0036-parameterized-composition-profile-selection.md)'s
  own motivating evidence of 18-45 call sites for a *single* dimension).
- *Was it intended to work?* No — parameter-name-driven dispatch was never
  a Compono convention. `CompositionProviderRequest.Name` technically
  *could* reproduce it (it carries "the test-method-parameter's own name,"
  per its own doc comment, and ADR-0036's Context names this exact
  mechanism as already solving "requested type + resolution-site name" —
  RESEARCH-0002 Finding 2) — deliberately not used here: reproducing
  AutoFixture's specimen-context trick is exactly the kind of implicit
  convention this product avoids.
- *Cost*: low — each scenario-sensitive test now builds its own value via
  a small project-local `TestHelper.cs` (`TestHelper.IntentRequest("...")`,
  `TestHelper.SkillOptions(skillId: "...")`), a few lines, the business
  scenario visible directly in Arrange instead of encoded in an
  identifier.
- *Principle alignment*: parameter-name dispatch is implicit-over-explicit;
  explicit local arrangement is the more Compono-native answer.
- **ADR-0036 investigated and rejected for this finding specifically**:
  would require `MediatRTestProfile` to grow a second constructor
  (`MediatRTestProfile()` delegating to `MediatRTestProfile(MediatRTestConfig)`
  — confirmed via a compile spike that C#'s `new()` constraint requires a
  true zero-arg constructor, so the existing ~140 `[Compose<MediatRTestProfile>]`
  call sites are unaffected either way), plus two independent
  enum-keyed dimensions bundled into one `TConfig` record (ADR-0036
  configures one profile per test *method*, not per parameter). More
  ceremony than local arrange for a call-site count below ADR-0036's own
  bar. ADR-0036 remains the right tool for a *future* finding with real
  single-dimension repetition — not this one.

## ADR-0052 negative evidence

`Register<IServiceProvider>`'s factory calls
`context.Resolve<ILogger<SkillMediator>>()` — `ILogger<SkillMediator>` is
never independently composed as its own theory parameter anywhere in this
project, exactly Finding B's shape (RESEARCH-0010 §11). It resolves
cleanly — no `CompositionException`.

This narrows Finding B: nested `context.Resolve<T>()` inside a
registration factory is **not inherently unsupported** — the failure mode
is specific to a type that needs a **generated composition plan** (a
concrete/record type the `TransitiveClosureWalker` structurally walks). An
interface satisfied by a test-double **provider** never needs a
discovery-root plan at all, so it's unaffected. Recorded here as evidence
for ADR-0052's design dive; does not change ADR-0052's own text (per
explicit product direction — the design dive itself, not this evidence
record, is where that gets weighed against the other open questions).

Neither Finding A nor Finding B was reproduced by this slice otherwise —
no `CMP0001`, no CompositionException from a nested `Resolve<T>()` on a
concrete/record type with no other discovery root.

## Files

- `test/AlexaVoxCraft.MediatR.Tests/TestKit/MediatRTestProfile.cs` (new) —
  the profile; every `Register<T>` above, with inline rationale comments.
- `test/AlexaVoxCraft.MediatR.Tests/TestHelper.cs` (new) — explicit
  scenario construction (Finding 3).
- `test/AlexaVoxCraft.MediatR.Tests/AlexaVoxCraft.MediatR.Tests.csproj` —
  `Compono`/`Compono.XunitV3`/`Compono.NSubstitute` package references
  replacing the `AlexaVoxCraft.TestKit` `ProjectReference`; direct
  `AlexaVoxCraft.MediatR`/`AlexaVoxCraft.Model`/`AwesomeAssertions`/`NSubstitute`
  references (previously only transitive).
- `Directory.Packages.props` — `Compono.TestDoubles`/`Compono.NSubstitute`
  `PackageVersion` entries added (lockstep `0.8.0-preview.83`;
  `Compono.TestDoubles` added during investigation, not used in the final
  shape — see Finding "TestDoubles vs Compono.NSubstitute" below).
- 13 test files under `test/AlexaVoxCraft.MediatR.Tests/` — `[Frozen]` →
  `[Shared]`, `MediatRAutoData` → `Compose<MediatRTestProfile>`, and the
  ~13 scenario-sensitive test bodies described in Finding 3 updated to use
  `TestHelper`.

## Stage 2 — `Compono.NSubstitute` was a temporary investigative baseline, not the target

**Correction to this document's original conclusion.** Stage 1 (above) used
`Compono.NSubstitute` and described that choice as a "provider-choice
decision, not a capability gap" for this project, deferring a
`Compono.TestDoubles` migration as out of scope. That is **not** the
accepted product direction for the `alexa-vox-craft` dogfood effort. The
underlying investigation stands — `Compono.TestDoubles`' generated
doubles genuinely do expose `Configure()`, not NSubstitute's
`.Returns()`/`.Received()`/`Arg.Is`/`Arg.Any`, and every one of the ~150
old NSubstitute call sites really did throw
`NSubstitute.Exceptions.CouldNotSetReturnDueToNoLastCallException` against
a `Compono.TestDoubles`-generated instance — but the conclusion drawn from
that (defer the rewrite, keep `Compono.NSubstitute`) was wrong for this
effort's actual purpose. The point of dogfooding `alexa-vox-craft` before
1.0 is to pressure Compono with real migration surface, not to find the
smallest diff that makes the test suite pass. ~150 real `.Returns()`/
`.Received()`/`Arg.Is`/`Arg.Any` call sites converting to
`Compono.TestDoubles`' actual API **is** the dogfood evidence this effort
exists to produce, not scope to defer.

**Corrected target state for this project**: no `NSubstitute`/
`Compono.NSubstitute` in the resolved dependency graph at all;
`Compono.TestDoubles` is the interface-double implementation throughout.
`Compono.NSubstitute` served only as Stage 1's investigative baseline
(proving the composition-root migration itself worked, isolating that
question from the double-implementation question) — its use here is
preserved in the evidence trail above exactly as it happened, not rewritten,
but it does not represent this project's accepted end state. Stage 2's
conversion, its blockers, and its own ADR-0029-classified findings are
recorded once that work completes (tracked in this same document,
appended below as it proceeds).

## Stage 2 — closed

`AlexaVoxCraft.MediatR.Tests` fully converted to `Compono.TestDoubles`;
`Compono.NSubstitute` removed entirely. The conversion surfaced one genuine
Compono core bug (below, `PLAN-0053`), one genuine Compono capability gap
classified roadmap candidate (below, `ADR-0053`), and several ordinary
project-local migration-completeness bugs (missing `Configure()` calls, a
stale profile comment) - see `PLAN-0053`'s own Notes section for the full
breakdown of which was which. This green migration does not mean every gap
found along the way was silently papered over - both real findings below
were investigated and formally classified, not omitted.

**The gap: `IDefaultRequestHandler.CanHandle`.** Its shape -
`IRequestHandler.CanHandle(...)` (abstract) resolved by
`IDefaultRequestHandler`'s own `new bool CanHandle(...) => true;` redeclaration
- was misclassified as a diamond collision by `TestDoubleAnalyzer`'s original
"same identity reached more than once ⇒ diamond" rule (Amendment 3 Finding 8),
silently discarding the real `=> true` body for a wrong computed default
(`false`). Classified per ADR-0029 as a genuine Compono core defect, not a
project-local workaround target - investigated, designed, and fixed as
`ADR-0044` Amendment 20 / `PLAN-0053`, not papered over with a hand-written
fake or a retained `Compono.NSubstitute` dependency.

**Final state, verified via `scripts/dogfood-validate.sh` against freshly
packed local `Compono`/`Compono.TestDoubles`/`Compono.XunitV3` packages**
(exact versions and counts in `PLAN-0053`'s own Notes section):

- `AlexaVoxCraft.MediatR.Tests`: 154/154 passing, all 4 TFMs.
- Resolved dependency graph (`project.assets.json`, not just the absence of
  a `PackageReference`): zero `NSubstitute`/`Compono.NSubstitute` entries.
- Full `AlexaVoxCraft.slnx` solution, same dogfood gate: 2784/2784 passing,
  0 failed, `dogfood-validate.sh` reported `PASS`.

**The gap: invocation-aware callback responses.** Converting
`Wrappers/RequestHandlerWrapperTests.cs`'s
`Handle_WithPipelineBehaviors_ExecutesBehaviorsInReverseOrder` surfaced a
second, independent finding: the pre-migration NSubstitute test configured
`IPipelineBehavior.Handle(...)` with `.Returns(async call => { ...; await
call.Arg<RequestHandlerDelegate>()(); ...})` - an invocation-aware callback
that invokes a captured delegate argument and records side effects around
it. `Compono.TestDoubles` has no equivalent (`docs/packages/compono-testdoubles.md`'s
"What it deliberately doesn't do" already documents "no
`Returns(Func<...>)` callbacks" as a non-goal). Searched this project's
complete git history: exactly one real site, ever, needed this shape.
Frequency alone would ordinarily classify this "intentional design
difference" under ADR-0029's general discretion, but
[ADR-0042 Amendment 2](../adr/0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-componononsubstitute-substitutability-is-a-goal-not-an-aspiration)
overrides that discretion for this exact category: `Compono.NSubstitute`
(a real NSubstitute substitute, confirmed by reading its provider source)
satisfies this shape natively, so a real, evidenced
`Compono.NSubstitute`-vs-`Compono.TestDoubles` gap is a roadmap candidate
regardless of rarity. **Classification: roadmap candidate**, tracked by
[ADR-0053](../adr/0053-testdoubles-invocation-aware-callback-responses.md)
(`Proposed`; problem and design evidence recorded only, no API decided).
`TestKit/FakeDelegates.cs`'s `FakePipelineBehavior` is the accepted interim
project-local workaround while that roadmap item is unresolved - not
implemented as part of this migration, and not a permanent verdict.

## Constructor-selection dogfood re-verification (2026-08-25)

Re-ran the fresh-package dogfood gate against the exact Compono working
tree carrying explicit constructor selection (ADR-0002 Amendment 3/
ADR-0052 Part B, `builder.For<T>().UseConstructor<...>()`), before
pushing that work:

- `dotnet test Compono.slnx -c Release`: 2532/2532, 0 failed (all
  packages, all 4 TFMs), plus the constructor-selection generator suite
  (`Compono.Generators.Tests`, 226/226) and both AOT proofs
  (`test/Compono.AotSmokeTest` — analyzer-contract, zero trimming/AOT
  warnings; real `dotnet publish -p:PublishAot=true` + run, `PASS`, exit
  0) — the AOT fixture now exercises `UseConstructor<IBar, IBaz>()`
  against a genuinely ambiguous type under Native AOT, not just JIT.
- `scripts/dogfood-validate.sh --consumer-repo alexa-vox-craft --packages
  "Compono Compono.XunitV3 Compono.Http Compono.TestDoubles"` (dropped
  `Compono.NSubstitute` from the requested set — no longer consumed
  anywhere in the solution, consistent with Stage 2's removal above; the
  script's version-verification step confirmed this by failing loudly
  when `Compono.NSubstitute` was included and found unreferenced, rather
  than silently skipping it).
- Resolved version confirmed via the script's own `project.assets.json`
  check: `dogfood-validate.sh: confirmed - every resolved Compono/
  Compono.TestDoubles/Compono.XunitV3 reference resolves to
  0.0.0-local.20260825111436-79105-16358`.
- Full `AlexaVoxCraft.slnx` suite: **2784/2784 passing** (32 skipped,
  pre-existing "Temporarily skipping due to CI issues" markers,
  unrelated), `dogfood-validate.sh: PASS`. Same 2784/2784 count as
  RESEARCH-0010's own acceptance run — this re-run is a clean regression
  confirmation, not a new migration result for that slice.
- **Consumer working tree state at time of this run** (uncommitted,
  preserved exactly, not created or altered by this re-verification):
  `AlexaVoxCraft.MediatR.Tests` (Stage 2, above) and, per
  [RESEARCH-0010](0010-alexa-vox-craft-compono-ecosystem-migration.md),
  `AlexaVoxCraft.InSkillPurchasing.Tests`/`AlexaVoxCraft.Smapi.Tests` (the
  HTTP-touched slice) all sit locally migrated and passing but not yet
  committed/pushed to `alexa-vox-craft`'s `main` — outside this document's
  or PLAN-0053's scope to commit; recorded here only so the dogfood
  re-verification's starting state is accurate.
- **First pass found no natural `UseConstructor<...>()` case in the
  migrated slice** — an initial grep for `UseConstructor` across
  `AlexaVoxCraft.MediatR.Tests`/`AlexaVoxCraft.InSkillPurchasing.Tests`/
  `AlexaVoxCraft.Smapi.Tests` came back empty, and `AlexaInteractionModelClient`/
  `AlexaSkillInvocationClient`/`InSkillPurchasingClient` (RESEARCH-0010's
  Finding A) looked like a dead end: the client's own `HttpClient`
  parameter needs one *specific, already-configured* instance built from
  a particular `TestHttpHandler`, which looked like the `Register<T>`
  shape, not "some accessible constructor Compono should pick." A closer
  investigation (below) found this framing was incomplete, not wrong —
  the ambiguity and the specific-instance concerns are separable, and
  `UseConstructor<...>()` closes the first one for real.

### Real consumer proof: `AlexaInteractionModelClientTests` (2026-08-25)

Investigated whether `AlexaInteractionModelClient` — the exact class whose
`CreateClient(TestHttpHandler)` hand-construction helper and "no
registration-based escape hatch" comment originally motivated this whole
constructor-selection design thread — could now compose directly. Two
independent things were true simultaneously and had to be separated:

1. **`HttpClient` has 3 accessible constructors** → `CMP0001` fires for
   any type reaching it structurally (like `AlexaInteractionModelClient`'s
   own constructor parameter), *regardless* of whether a `Register<HttpClient>`
   already exists — confirmed again here: compile-time discovery can't see
   a runtime registration (this is Finding A/Part A's exact boundary).
   `UseConstructor<HttpMessageHandler, bool>()` closes this at compile
   time by selecting `HttpClient`'s `(HttpMessageHandler, bool)`
   constructor for the generated plan.
2. **The actual runtime value still needs to be the specific,
   already-configured client** `SmapiHttpTestProfile` builds via
   `TestHttpHandler.CreateClient(...)` (shared handler identity, correct
   `BaseAddress`, `disposeHandler:false`). This is exactly what the
   profile's pre-existing `Register<HttpClient>(...)` already supplies —
   and a registration always outranks a generated plan at runtime, so
   `UseConstructor`'s generated construction path is never actually
   invoked here. `UseConstructor` and `Register<HttpClient>` are not
   competing; they solve the compile-time and runtime halves of the same
   problem, exactly as ADR-0052's `Register<T>` vs. `UseConstructor`
   distinction predicts.

Before landing this in the real consumer, spiked both halves in isolation
against the fresh-packed `Compono`/`Compono.Http`/`Compono.XunitV3`
(via `PackageReference`, not `ProjectReference` — a `ProjectReference`
skips the analyzer wiring a real consumer gets through the packaged
`analyzers/dotnet/cs` folder, so it doesn't prove anything a real
consumer would see) to de-risk before touching consumer code:

- `builder.For<HttpClient>().UseConstructor<HttpMessageHandler, bool>()` +
  `builder.Register<HttpMessageHandler>(context => context.Resolve<TestHttpHandler>())`
  correctly resolves `HttpMessageHandler` to the *same* `[Shared]`
  `TestHttpHandler` instance — a request sent through the composed client
  reached `handler.Requests`.
- A narrow, name-scoped `ICompositionValueProvider` (`RequestedType == bool
  && DeclaringType == typeof(HttpClient) && Name == "disposeHandler"` →
  `false`) correctly pinned just that one constructor parameter with no
  global `bool` override — verified `disposeHandler:false` was honored
  (disposing the composed client did not dispose the shared handler).
- `.For<HttpClient>().Member(x => x.BaseAddress).Use(...)` **silently
  never applied** — `BaseAddress` composed as `null`. Root cause,
  confirmed by reading ADR-0020: `.Member(...)` rules only ever fire for
  a constructor parameter or a `required` member — the only kinds the
  composition walker visits. `HttpClient.BaseAddress` is an ordinary
  settable property, neither, so Compono's plan for `HttpClient` never
  asks for it and the rule has nothing to attach to. **Classification
  (ADR-0029): Acceptable Compono-native alternative** — not a bug, and
  not worth expanding the composition walker to visit arbitrary
  non-required settable properties for. `Register<T>`/an explicit
  post-composition assignment remains the right tool for "construct a
  simple value, then imperatively configure more of it." No roadmap ADR
  opened for this; it did not end up mattering for the real migration
  below, since `SmapiHttpTestProfile`'s existing `Register<HttpClient>`
  already sets `BaseAddress` correctly.

Applied to the real consumer: `AlexaVoxCraft.Smapi.Tests/Clients/AlexaInteractionModelClientTests.cs`
now composes `AlexaInteractionModelClient` directly as an ordinary
`[Compose<SmapiHttpTestProfile>]` theory parameter across all 10 test
methods — the `CreateClient(TestHttpHandler)` helper and the stale
"no registration-based escape hatch" comment are both deleted.
`SmapiHttpTestProfile` gained `builder.For<HttpClient>().UseConstructor<HttpMessageHandler, bool>();`
(one line, compile-time only) plus a `Register<ILogger<AlexaInteractionModelClient>>(() =>
NullLogger<AlexaInteractionModelClient>.Instance)` (the same established
`NullLogger<T>` pattern `MediatRTestProfile` already uses for `ILogger<SkillMediator>`
— a genuinely separate, ordinary gap the migration surfaced, not part of
the constructor-selection story). Nothing else in the profile changed;
`Register<HttpClient>` is untouched and still supplies the real runtime
value. Re-ran the fresh-package dogfood gate after this change: **same
2784/2784 passing**, `dogfood-validate.sh: PASS`, resolved to
`0.0.0-local.20260825125740-3256-25825`. This is real, non-synthetic
`AlexaVoxCraft` `UseConstructor` dogfood evidence — it was not
manufactured to claim coverage; it closes the exact case that motivated
this feature's design in the first place.

`AlexaVoxCraft.Smapi.Tests/Clients/AlexaSkillInvocationClientTests.cs`
(the third client in this file family, whose class-level comment already
pointed at `AlexaInteractionModelClientTests.cs`'s reasoning) migrated
identically — same `SmapiHttpTestProfile`, no new profile machinery
needed beyond one more `Register<ILogger<AlexaSkillInvocationClient>>(()
=> NullLogger<AlexaSkillInvocationClient>.Instance)` line alongside the
existing `ILogger<AlexaInteractionModelClient>` registration. All 11 test
methods (including the three region-parameterized tests sharing a
private `AssertRegionCompletesSuccessfully` helper, updated to accept
the composed `client` instead of `handler`-only) now compose
`AlexaSkillInvocationClient` directly; `CreateClient(TestHttpHandler)`
and the stale comment are both deleted. Re-ran the fresh-package dogfood
gate: same **2784/2784 passing**, `PASS`, resolved to
`0.0.0-local.20260825130313-5158-9851`.

`AlexaVoxCraft.InSkillPurchasing.Tests/Clients/InSkillPurchasingClientTests.cs`
carries the identical stale comment/pattern (`InSkillPurchasingClient`,
same `HttpClient`-ambiguity shape, in a different project with its own
composition profile) and is a candidate for the same migration in a
future pass — **not done in this pass**, out of the scope this
investigation was asked to cover.

## Next

- `AlexaVoxCraft.MediatR.Lambda.Tests` (85 tests, 5 files) — the natural
  next *composition-root* slice once Stage 2 closes here; has a
  `TypeRelay(SkillContext → DefaultSkillContext)` abstract-mapping case
  RESEARCH-0010 didn't cover, deliberately excluded from this slice to
  keep the two dimensions of evidence attributable.
- The remaining 7 of 9 `AlexaVoxCraft.TestKit`-dependent projects, in
  future slices — each expected to go straight to `Compono.TestDoubles`,
  not through an `Compono.NSubstitute` baseline stage, now that Stage 2's
  evidence exists.
