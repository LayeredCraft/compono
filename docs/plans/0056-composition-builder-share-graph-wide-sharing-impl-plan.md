# [PLAN-0056] `CompositionBuilder.Share<T>()`: Graph-Wide Sharing — Implementation Plan

**Status:** Done

**Implements:** [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md)

## Goal

`CompositionBuilder.Share<T>()` exists, is documented, skill-synchronized,
evaluated, AOT-validated, and dogfooded against **two** real, independent
consumers — `alexa-vox-craft` (the `Compono.Logging`/`ILogger<T>`
motivating case) and `dynamodb-distributed-lock` (an independent
`Meter`/`ILockMetrics` case, a different type and a different
registration shape) — a reader can verify "done" by confirming every
normative contract point in ADR-0056 has a passing, permanent,
reference-identity-asserting test, and that no shipped doc/skill page
still describes `[Shared]` as required for a `Share<T>()`-configured
type.

## Scope

**In scope:** the `CompositionBuilder.Share<T>()` core feature exactly as
ADR-0056 defines it — graph-scoped, lazy, uniform across every resolution
stage, orthogonal to `Register<T>()`, idempotent on repeat calls, zero
change to `[Shared]`'s existing behavior. Full documentation/skill/eval
synchronization (mandatory completion criteria, not follow-up). Native
AOT validation via the existing `test/Compono.AotSmokeTest` project. Real
dogfooding against both `alexa-vox-craft` and `dynamodb-distributed-lock`
via `scripts/dogfood-validate.sh` with freshly packed local packages.

**Explicitly out of scope** (per ADR-0056's own "Rejected alternatives"/
"Noted but explicitly not part of this ADR" sections):
- A `CompositionRow`/`TryGetShared<T>()`-style graph-scoped accessor for
  hand-written `CreateRow` usage. No dogfooding evidence requires it; not
  designed further here.
- Disposal/lifetime-ownership semantics for a shared value. Remains the
  separate, not-yet-started pre-1.0 disposal investigation's
  responsibility.
- Any handle type, ambient/service-locator retrieval, lifetime enum,
  sharing-policy abstraction, or generator-facing API — ADR-0056
  evaluated and rejected all of these explicitly.

The spike prototype (`docs/research/0014-...md` §11) is evidence that this
architecture works, not a merge candidate — every production change below
is justified against ADR-0056's contract, not inherited merely because
the spike happened to use that shape.

## Tasks

### 1. Core engine: `Share<T>()` and the graph-wide write-gate

- [x] `src/Compono/CompositionBuilder.cs` — add a `_sharedTypes`
  accumulator (a `HashSet<Type>`, matching this file's existing
  `_semanticProviders`/`_testDoubleProviders` plain-list-accumulator
  style rather than the `ConfigurationOptionSlot<T>` pattern reserved for
  scalar, conflict-detecting options — `Share<T>()` has no duplicate-call
  conflict to detect, ADR-0056's "Duplicate `Share<T>()`" section) and a
  public `Share<T>()` method (`_sharedTypes.Add(typeof(T)); return
  this;`). Wire into `Build()`'s `CompositionConfiguration` construction.
- [x] `src/Compono/CompositionConfiguration.cs` — add `internal required
  IReadOnlySet<Type> SharedTypes { get; init; }`, matching this file's
  existing `required`-property, frozen-snapshot convention.
- [x] `src/Compono/CompositionContext.cs` — add a `_sharedTypes` field
  (`IReadOnlySet<Type>`), threaded through the constructor overloads
  `Composer.Create<T>()`/`CreateMany<T>()`/`CreateRow` actually use
  (default to an empty set for every other, test-seam-only constructor,
  so no existing `Compono.Tests` internal-seam test is affected). In
  `ResolveCore`, compute `effectiveIsShared = isShared ||
  _sharedTypes.Contains(requestedType)` once, and use it for the
  request's write-establishing behavior.
- [x] **The pipeline audit, explicitly required by ADR-0056's
  "Implementation constraint discovered by the spike" section — not
  optional, and not satisfied by touching `CompositionRequest.IsShared`
  alone.** Confirm, by reading each site directly (not by assuming), that
  every stage capable of storing a produced value into `CompositionScope`
  honors `effectiveIsShared`, specifically:
  - Generated-plan dispatch's own store call.
  - Every `ICompositionValueProvider` stage (configuration rules,
    semantic providers, test-double providers, built-in providers,
    collection-plan dispatch) — these already route through a shared
    `StoreSharedAndReturn<TValue>(value, request, ...)` helper reading
    `request.IsShared`, so confirm this remains correct once
    `request.IsShared` itself carries the broadened value.
  - **Exact registrations** and **the configured `IServiceProvider`
    fallback** — the two sites the spike proved read a stale, separately-
    captured raw flag instead of the per-request value. Confirm both now
    read the same `effectiveIsShared` the request itself carries, not a
    second, independently-captured local.
  - Prefer structure over commentary: every write site should read the
    same `effectiveIsShared`/`request.IsShared` value through one
    consistent path, so there is nothing left to explain in a comment. The
    ADR (`docs/adr/0056-...md`'s "Implementation constraint discovered by
    the spike" section) and the permanent tests in task 2 are this
    change's durable audit trail — do not leave a process/historical
    comment ("audited on <date>", "confirmed this site") at each site.
    Reserve an inline comment only for a genuinely non-obvious invariant
    the code's own naming/structure can't make clear on its own.
- [x] `src/Compono/Composer.cs` — thread `_configuration.SharedTypes`
  into the three `CompositionContext` construction sites (`Create<T>()`,
  the per-item context inside `CreateMany`'s internal loop, `CreateRow`).

### 2. Permanent test coverage

Two homes, deliberately split for the same reason the spike used two
projects: `Compono.Tests` cannot safely gain an active
`Compono.Generators` analyzer reference without breaking ~350 existing,
unrelated internal-seam fixtures that were never meant to be composed
through a real generated plan (confirmed directly during the spike,
RESEARCH-0014 §11a) — fixing that repo-wide gap is out of scope for this
plan. `Compono.Tests`' own established pattern for exercising nested
resolution without real generation — a hand-written `Register<T>(ctx =>
new Wrapper(ctx.Resolve<Leaf>()))` factory — uses the *identical*
`context.Resolve<T>(descriptor)` code path a generated plan's own nested
request uses, so it's strong (not merely convenient) coverage for
everything except "is this specifically proven against real
generator-emitted code."

- [x] **`test/Compono.Tests/CompositionShareTests.cs`** (new) — the
  exhaustive mechanics/boundary suite, using the existing
  Register-factory-driven nested-composition pattern
  (`CompositionManualResolveTests.cs`'s own style) so no analyzer changes
  are needed here. Every case asserts `ReferenceEquals` where identity is
  the contract (never value equality). Add a paired control case (no
  `Share<T>()` configured) only where it materially proves the observed
  identity is actually caused by `Share<T>()` rather than some other
  latent composition behavior — not mechanically for every case:
  - [x] Sharing within one `Create<T>()` graph; no sharing across two
    independent `Create<T>()` calls against the same `Composer`.
  - [x] Sharing within each `CreateMany<T>()` item; no sharing across
    items (the spike's own boundary check, promoted to permanent) —
    **include a control case**: the cross-item independence half of this
    test only means something once the within-item sharing half is
    confirmed to be real sharing, not an accident of how the fixtures are
    constructed.
  - [x] Sharing across an entire `CompositionRow` (via
    `Composer.CreateRow`/`Resolve`/`ResolveShared`, not `[Compose]`
    attribute binding, which lives in task 2's second file below) —
    **include a control case**, since this is a graph-boundary claim
    (§7's least-precedented case for a hand-written, non-attribute path).
  - [x] `Register<T>(...).Share<T>()` and `Share<T>().Register<T>(...)`
    resolve through the registration and cache identically in both
    orders (the spike's own bug-catching test, kept and hardened —
    assert both the registered marker value *and* reference identity).
    No control case needed: the registered marker value itself is
    self-proving evidence (a generated-plan default value could never
    produce it), and comparing the two orderings against each other is
    the actual assertion, not a share/no-share comparison.
  - [x] A registered value that flows through the configured
    `IServiceProvider` fallback (not an exact `Register<T>()`) also
    participates — the other site the spike found reading a stale flag;
    this path had no direct spike coverage and must not be assumed fixed
    by analogy alone. No control case needed, same reasoning as the
    `Register<T>()` ordering case above.
  - [x] Duplicate `Share<T>()` calls for the same type (directly twice,
    and once via each of two profiles) are idempotent — no exception, no
    behavior change from a single call. No control case: idempotency is
    self-proving (a single call's own passing behavior is the baseline
    this compares against).
  - [x] Nested/transitive dependencies at more than one level of depth
    participate (not just immediate constructor parameters).
  - [x] Existing `[Shared]`-driven `CompositionRow` behavior (declaration
    order among `[Shared]` parameters, duplicate-type rejection, row-local
    visibility) is unchanged — re-run/extend a representative slice of
    `CompositionRowTests.cs`'s own existing assertions with `Share<T>()`
    never configured, confirming zero regression. (This *is* the control
    evidence for `[Shared]` non-regression — no separate paired case
    needed.)
- [x] **`test/Compono.XunitV3.Tests/CompositionBuilderShareTests.cs`**
  (new, replacing the spike's `SPIKE_ShareSemanticsTests.cs`) — the real,
  compiled generated-plan and `[Compose<TProfile>]` proof, in the one
  project already confirmed (spike) to build clean with the
  `Compono.Generators` analyzer wired in. Convert the spike's fixture
  shapes into permanently-named types and tests:
  - [x] A real generated plan's own nested `context.Resolve<T>(descriptor)`
    call participates in `Share<T>()` (promoted from spike 1) — **include
    a control case**: this is the one place proving the sharing is
    specifically attributable to `Share<T>()` and not some other
    identity-preserving behavior already latent in generated-plan
    dispatch.
  - [x] Two ordinary, unattributed production-shaped constructors
    (`ServiceA`/`ServiceB`-equivalent) reached only as nested dependencies
    — no `[Shared]`, no test parameter of the shared type, no Compono
    annotation anywhere — receive identical shared identity (promoted
    from spike 3b) — **include a control case**: the strongest, least-
    precedented claim in this whole plan, worth the extra proof that two
    ordinary constructors are independent by default and shared only
    because `Share<T>()` was configured.
  - [x] An ordinary, undecorated `[Compose<TProfile>]` theory parameter
    participates automatically, in **both** declaration orders relative
    to its structural dependent, asserting `ReferenceEquals` (promoted
    from spike 3a) — zero `[Shared]` attributes anywhere in either test
    method. No control case needed: comparing the two declaration orders
    against each other is the actual assertion (order-independence), and
    a `Share<T>()`-absent variant would just be two ordinary, unrelated
    theory parameters with nothing to compare.
  - [x] `test/Compono.XunitV3.Tests/Compono.XunitV3.Tests.csproj` keeps
    the `Analyzer`-only `ProjectReference` to `Compono.Generators.csproj`
    the spike added (already confirmed to introduce zero regressions to
    the other 70 existing tests in this project) — this becomes a real,
    permanent project dependency, not a temporary spike artifact.
- [x] Delete `test/Compono.XunitV3.Tests/SPIKE_ShareSemanticsTests.cs`
  once its coverage is fully represented in the two permanent files
  above — no spike-named file should remain once this plan reaches
  `Status: Done`.

### 3. Native AOT/trimming validation

- [x] Extend the existing **`test/Compono.AotSmokeTest`** project (core
  Compono's own established AOT smoke test — `Widget`/`AmbiguousFoo`-style
  fixtures already prove ordinary generated-plan and explicit-constructor-
  selection composition survive Native AOT) with a `Share<T>()` scenario:
  a builder-configured shared type reached by two sibling composed
  dependents, asserting reference identity in the published, natively
  compiled binary's own output — not just under ordinary `dotnet test`
  JIT execution.
- [x] Run this project's existing `pack-compono.sh` +
  `dotnet publish -f net10.0 -p:PublishAot=true` + execute-the-published-
  binary sequence (this project's own established pattern, per
  `Compono.Logging.AotSmokeTest`'s identical convention from PLAN-0055
  task 15) and confirm zero `IL2026`/`IL3050`/trim-analyzer warnings
  attributable to `Share<T>()`'s own code path.

### 4. Documentation (mandatory completion criteria, per ADR-0056)

- [x] `docs/architecture/current/provider-pipeline.md` and/or
  `docs/architecture/current/generated-plans-and-discovery.md` — a
  `Share<T>()` section describing the graph-wide contract as *current*
  state (only once the code above ships — per this repo's own
  "don't describe unimplemented behavior as current" rule).
- [x] `docs/concepts/shared-values.md` — new `Share<T>()` entry alongside
  the existing `Register<T>()`/`.For<T>()` surface. (Correction, round-1
  Codex review: `docs/public-api.md` is an intentional ADR-0030 Amendment
  2 tombstone, not the live public-surface doc — its own text redirects
  to `docs/concepts/` as the canonical home. Adding a duplicate entry to
  the tombstone would violate `documentation.md`'s "avoid duplicating
  content" principle; this plan's original bullet just cited a stale
  pre-tombstone path.)
- [x] `docs/packages/compono-logging.md` — revise the existing
  `PerformanceLoggingBehavior`-style example to show:

  ```csharp
  builder
      .UseLogging(...)
      .Share<ILogger<PerformanceLoggingBehavior>>();
  ```

  paired with an **ordinary, undecorated**
  `ILogger<PerformanceLoggingBehavior> logger` theory parameter — **no
  `[Shared]` on it**. This is a normative requirement from ADR-0056 and
  RESEARCH-0014 §10, not a nice-to-have; the plan is not `Done` while this
  page still shows or implies `[Shared]` is needed once `Share<T>()` is
  configured.
- [x] Regenerate `docs/reference/api` for `Compono` (the new public
  `Share<T>()` XML doc), per this repo's standing "regenerate API
  reference whenever public XML docs change, before push" convention.

### 5. Skill/reference synchronization (mandatory, not optional cleanup)

- [x] `skills/compono/SKILL.md` — the mechanism-choice guidance that
  already lists `[Shared]` gains a `Share<T>()` row, with explicit
  guidance on when to reach for which (profile-level/reusable sharing
  intent → `Share<T>()`; one-off, single-test sharing not worth
  centralizing → `[Shared]`) — matching ADR-0056's own "Relationship with
  `[Shared]`" section exactly, not a paraphrase that could drift from it.
- [x] `skills/compono/references/registrations-profiles-and-scopes.md` —
  primary home for `Share<T>()` usage guidance (already documents
  `Register<T>()`/`.For<T>()`/profiles). Decide during this task, not
  before, whether the combined `[Shared]` + `Share<T>()` material
  justifies splitting into a dedicated sharing reference file (ADR-0056
  leaves this open) — if split, update `SKILL.md`'s own reference table
  to point at the new file.
- [x] **Explicit profile blast-radius warning**, per ADR-0056's own
  documentation requirement: adding `Share<T>()` to a profile several
  tests already reuse changes sharing semantics for *every* graph
  composed with that profile, silently, for any test structurally
  reaching the type more than once — materially larger blast radius than
  adding `[Shared]` to one test method. Must be stated plainly wherever
  `Share<T>()` usage guidance lives, not left implicit.
- [x] `skills/compono/references/logging.md` — same correction as the
  `docs/packages/compono-logging.md` example above (`Share<T>()` +
  ordinary undecorated parameter, no `[Shared]`), kept consistent between
  the two.
- [x] **Verification pass, explicitly required by this plan's own Goal
  statement**: grep the full `skills/` and `docs/` trees for any
  remaining example or prose implying `[Shared]` is required to retrieve
  a `Share<T>()`-configured value, or implying `Share<T>()` shares across
  separate `Create<T>()` calls/is composer-wide — fix every hit found.
  This plan is not `Done` while a shipped page still contradicts
  ADR-0056's contract.

### 6. Evals

- [x] `skills/compono-evals/evals.json` — add at least one new eval
  distinguishing correct `Share<T>()` usage (graph-scoped, lazy,
  ordinary-parameter participation, `[Shared]` not required) from the
  specific incorrect assumption a model could plausibly make without
  skill guidance: that `Share<T>()` is composer-wide, or that it solves
  identity across separate `Create<T>()` calls (which it explicitly does
  not, ADR-0056's own rejected-alternatives section).
- [x] Run the eval through the repo's normal before/after with-skill
  grading workflow (`skills/compono-evals` benchmark methodology, per its
  own established convention) and record the result — not merely add the
  eval file entry and assume it passes.

### 7. Dogfooding (real consumer evidence, not ad hoc validation — two independent consumers)

Two real consumers, each proving a genuinely different slice of ADR-0056's
contract: `alexa-vox-craft` is the `Compono.Logging`/`ILogger<T>` case
that directly motivated the ADR (a provider-produced value shared via a
profile). `dynamodb-distributed-lock` is an **independent** case found
during this plan's own drafting — `Meter` (a plain BCL type, not a
Compono.Logging concept) shared via an ordinary `Register<T>()` +
nested-`Resolve<T>()` registration shape, consumed by two structurally
unrelated composed types (`ILockMetrics`'s own nested dependency, and
`TestMetricAggregator<T>`'s direct constructor parameter). The two
together validate that `Share<T>()`'s contract holds across different
types, different resolution sources, and different consumer codebases —
not just the one case that happened to motivate the ADR. Both dogfood
passes are part of this same cohesive plan; neither is a separate phase.

**7a. `alexa-vox-craft`** (the `Compono.Logging` motivating case):

- [x] Use `scripts/dogfood-validate.sh` with freshly packed local
  `Compono` (this plan's own change lives in core, no other package
  needs repacking unless `Compono.Logging`'s own `.nuspec`-declared
  `Compono` dependency range needs bumping to include the new local
  version) against `alexa-vox-craft` — the repository's normal
  dogfooding process, not an ad hoc consumer check.
- [x] In `alexa-vox-craft`'s `MediatRTestProfile`, add
  `.Share<ILogger<PerformanceLoggingBehavior>>()` alongside the existing
  `.UseLogging(...)` call, and change `PerformanceLoggingBehaviorTests.cs`'s
  `[Shared] ILogger<PerformanceLoggingBehavior> logger` parameters (all
  eight methods) to an ordinary, undecorated `ILogger<
  PerformanceLoggingBehavior> logger` — the exact before/after this
  plan's own motivating evidence (RESEARCH-0014 §6e, Candidate B)
  described, now exercised for real against a real consumer's full test
  suite rather than only a synthetic spike fixture.
- [x] Confirm the full `alexa-vox-craft` solution still builds and passes
  (matching PLAN-0055 task 18's own real-consumer bar) with this change.

**7b. `dynamodb-distributed-lock`** (independent `Meter` evidence — repo:
`LayeredCraft/dynamodb-distributed-lock`, not the unrelated `ncipollina/`
fork; confirmed during plan drafting by inspecting the real, current
branch `feat/compono-0.9.0-preview.88`, clean working tree):

- [x] **Inspect before changing anything** (read-only pass, already
  substantially done during plan drafting — re-confirm against whatever
  state the repo is in when this task actually runs, since it may have
  moved): `test/DynamoDb.DistributedLock.Tests/TestKit/Profiles/
  DynamoDbDistributedLockCompositionDefaults.cs` (`builder.Register<Meter>
  (...)`, `builder.Register<ILockMetrics>(context => new
  LockMetrics(context.Resolve<Meter>()))`, and a required
  `builder.For<Meter>().UseConstructor<string>()` disambiguation — `Meter`
  has 4 accessible constructors), `DynamoDbDistributedLockTests.cs`, and
  `Retry/ExponentialBackoffRetryPolicyTests.cs`.
- [x] **Confirm each `[Shared] Meter meter` usage genuinely matches
  ADR-0056** before touching it, per this task's own instruction not to
  mechanically remove every occurrence just because the type is `Meter`.
  Already confirmed during plan drafting, re-verify at implementation
  time: all 13 occurrences (11 in `DynamoDbDistributedLockTests.cs`, 2 in
  `ExponentialBackoffRetryPolicyTests.cs`) declare `meter` purely to
  establish shared identity between the `Meter` `ILockMetrics` resolves
  internally (consumed by `sut`) and the `Meter`
  `TestMetricAggregator<T>` resolves directly (consumed by the test's own
  assertions) — the `meter` parameter itself is never referenced in any
  test body (confirmed by direct inspection, not assumed). This is the
  same "otherwise-unused `[Shared]` parameter, purely a workaround"
  pattern ADR-0056 targets, independently arrived at in a different
  codebase. **Do not remove a `[Shared] Meter` occurrence found to have
  a genuinely test-local reason at implementation time** — none was found
  during drafting, but re-confirm rather than trusting this note alone
  once real changes are being made. Leave every other `[Shared]`
  parameter in these files untouched (`dynamo`/`IAmazonDynamoDB`'s own
  `[Shared]` usage retrieves a generated test double for `.Configure()`/
  verification — a different, valid, unrelated pattern, not part of this
  migration).
- [x] Where confirmed matching: add `builder.Share<Meter>();` to
  `DynamoDbDistributedLockCompositionDefaults.Configure` (alongside the
  existing `Register<Meter>(...)` call — the exact `Register<T>()` +
  `Share<T>()` combination this plan's own task 2 tests prove is
  order-independent), and change every confirmed `[Shared] Meter meter`
  parameter to an ordinary, undecorated `Meter meter`.
- [x] Verify the exact `Meter` instance observed by `metricAggregator`
  remains reference-identical to the one `sut`'s own `ILockMetrics`
  publishes to, and that every existing behavioral assertion
  (`metricAggregator.Collect(...)`) continues to pass unchanged — the
  migration must be observably invisible except for the removed
  attribute.
- [x] Use `scripts/dogfood-validate.sh` with freshly packed local
  `Compono` against `dynamodb-distributed-lock` — the same established
  process as 7a, not an ad hoc `ProjectReference` or a stale local
  package.
- [x] Confirm the full `dynamodb-distributed-lock` solution still builds
  and passes, matching the same real-consumer bar as 7a.

**Both consumers:**

- [x] Record the before/after evidence (exact diff shape, test/build
  result) for both `alexa-vox-craft` and `dynamodb-distributed-lock` in
  this plan's own Notes section once dogfooding runs for real.
- [x] Per the standing instruction for this whole workstream: do not
  commit or push in `alexa-vox-craft`, `dynamodb-distributed-lock`, or
  `compono` without being explicitly asked — this task produces evidence
  for the report, not a merged consumer change in either repo.

## Critical Files

New:
- `test/Compono.Tests/CompositionShareTests.cs`
- `test/Compono.XunitV3.Tests/CompositionBuilderShareTests.cs`

Modified:
- `src/Compono/CompositionBuilder.cs` — `Share<T>()`, `_sharedTypes`.
- `src/Compono/CompositionConfiguration.cs` — `SharedTypes` property.
- `src/Compono/CompositionContext.cs` — `_sharedTypes` field,
  `effectiveIsShared`, the audited write-gate at every stage.
- `src/Compono/Composer.cs` — `SharedTypes` threaded through three call
  sites.
- `test/Compono.XunitV3.Tests/Compono.XunitV3.Tests.csproj` — permanent
  `Analyzer`-only `ProjectReference` to `Compono.Generators.csproj`.
- `test/Compono.AotSmokeTest/Program.cs` (or a new fixture file in that
  project) — `Share<T>()` AOT scenario.
- `docs/architecture/current/provider-pipeline.md` and/or
  `generated-plans-and-discovery.md`, `docs/public-api.md`,
  `docs/packages/compono-logging.md`, `docs/reference/api/Compono/*`.
- `skills/compono/SKILL.md`,
  `skills/compono/references/registrations-profiles-and-scopes.md` (or a
  new dedicated sharing reference), `skills/compono/references/logging.md`.
- `skills/compono-evals/evals.json`.

Deleted:
- `test/Compono.XunitV3.Tests/SPIKE_ShareSemanticsTests.cs` (superseded by
  the two permanent test files above).

External (uncommitted evidence only, per task 7):
- `alexa-vox-craft`'s `MediatRTestProfile.cs`/`PerformanceLoggingBehaviorTests.cs`.
- `dynamodb-distributed-lock`'s `TestKit/Profiles/
  DynamoDbDistributedLockCompositionDefaults.cs`,
  `DynamoDbDistributedLockTests.cs`, `Retry/ExponentialBackoffRetryPolicyTests.cs`.

## Test Plan

Every normative contract point in ADR-0056 has direct, permanent,
`ReferenceEquals`-asserting coverage (task 2), split between
`Compono.Tests` (exhaustive mechanics/boundaries, Register-factory-driven
nested composition) and `Compono.XunitV3.Tests` (real generated-plan and
`[Compose<TProfile>]` proof). Every resolution stage capable of writing
into `CompositionScope` is individually covered, not assumed correct by
analogy to a sibling stage — this is the one lesson the spike's own
found-and-fixed bug requires taking literally, not just noting. A
dedicated Native AOT smoke pass (task 3) proves the feature survives
publish-time trimming/AOT analysis, not just ordinary JIT test execution.
An eval (task 6) proves a model given only the skill can distinguish
correct usage from the composer-wide-singleton misconception this
research spent real effort ruling out. Real dogfood evidence against
**two independent consumers** (task 7) proves the actual boilerplate
reduction generalizes beyond the one case that motivated the ADR — not
just a synthetic fixture's, and not just the specific `ILogger<T>`/
`Compono.Logging` shape: `alexa-vox-craft` proves the motivating
provider-produced-value case, `dynamodb-distributed-lock` independently
proves the same contract for a plain BCL type shared through an ordinary
`Register<T>()` + nested-`Resolve<T>()` registration, consumed by two
structurally unrelated composed types.

## Notes

**Dogfood evidence — `alexa-vox-craft` (task 7a).** `MediatRTestProfile.Configure`
gained `.Share<ILogger<PerformanceLoggingBehavior>>()` alongside the
existing `.UseLogging(...)` call; all eight `[Shared]
ILogger<PerformanceLoggingBehavior> logger` theory parameters in
`PerformanceLoggingBehaviorTests.cs` became ordinary, undecorated
`ILogger<PerformanceLoggingBehavior> logger` parameters — the exact
before/after this plan's own motivating evidence described. Ran via
`scripts/dogfood-validate.sh --consumer-repo .../alexa-vox-craft
--packages "Compono Compono.XunitV3 Compono.TestDoubles Compono.Logging"`
against a freshly packed local build: **PASS**, full solution
`total: 2816, failed: 0, succeeded: 2784, skipped: 32` (the 32 skips are
pre-existing, unrelated to this change — `AlexaVoxCraft.Model.Apl.Legacy.Tests`'
own "Temporarily skipping due to CI issues" markers).
`AlexaVoxCraft.MediatR.Tests` itself: `[+154/x0/?0]` across all four TFMs.
No commit made in `alexa-vox-craft` — evidence only, per this workstream's
standing instruction.

**Dogfood evidence — `dynamodb-distributed-lock` (task 7b).** Re-confirmed
against `main` (`a5ec6ee`, freshly pulled — the `feat/compono-0.9.0-preview.88`
branch inspected during drafting had since merged into `main` via PR #76)
that all 13 `[Shared] Meter meter` occurrences (11 in
`DynamoDbDistributedLockTests.cs`, 2 in
`Retry/ExponentialBackoffRetryPolicyTests.cs`) were still genuine matches
with `meter` never referenced in any test body — none needed to stay
`[Shared]`. Added `builder.Share<Meter>();` to
`DynamoDbDistributedLockCompositionDefaults.Configure` alongside the
existing `Register<Meter>(...)` call.

Beyond the plan's own anticipated `[Shared]` → ordinary-parameter edit: since
`Share<T>()` makes *every* request for the type participate (not just a
declared retrieval parameter), and neither `ILockMetrics` (resolves
`Meter` internally via `context.Resolve<Meter>()`) nor
`TestMetricAggregator<T>` (a direct constructor dependency) needed the
`meter` parameter to observe the shared instance, the parameter itself
was surplus — removed entirely from all 13 call sites rather than merely
un-annotated. (A first pass that only removed `[Shared]` and kept the
now-plain `meter` parameter surfaced 24 new `xUnit1026` "theory parameter
not used" warnings — expected, since `[Shared]`-decorated parameters are
apparently exempt from that analyzer rule but ordinary ones aren't.
Removing the parameter outright, once confirmed safe, eliminated the
warnings too.) Ran via `scripts/dogfood-validate.sh --consumer-repo
.../dynamodb-distributed-lock --packages "Compono Compono.XunitV3
Compono.TestDoubles"` against a freshly packed local build: **PASS**,
`total: 180, failed: 0, succeeded: 180, skipped: 0`, zero `xUnit1026`
warnings, across all four TFMs. `metricAggregator.Collect(...)`
assertions continued to pass unchanged, confirming the exact `Meter`
instance observed by `TestMetricAggregator<T>` remains reference-identical
to the one `ILockMetrics` publishes to. No commit made in
`dynamodb-distributed-lock` — evidence only.

**Eval evidence (task 6).** New eval id 34 added to
`skills/compono-evals/evals.json`, run with-skill vs. without-skill; see
`skills/compono-evals/benchmarks/2026-08-28/README.md` for the full
grading breakdown. Result: with-skill 5/5, without-skill 2/5 (2 of its
passes vacuous — the baseline never learns `Share<T>()` exists at all and
solves the prompt with a hand-rolled `Register<T>(() => sameInstance)`
workaround instead).
