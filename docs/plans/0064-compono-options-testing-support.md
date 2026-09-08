# [PLAN-0064] Compono.Options: First-Class .NET Configuration/Options Testing Support

**Status:** Done

**Implements:** [ADR-0061](../adr/0061-compono-options-testing-support.md)

## Goal

A new `Compono.Options` package ships `TestOptionsSource<T>` — a
hand-written, reflection-free, non-generated runtime type per settings
type that directly implements `IOptionsMonitor<T>` and, through
`CompositionBuilder.UseOptions<T>(source)`, coherently wires
`IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` from one
test-configured source of truth — per ADR-0061's Decision Outcome in
full, including its 2026-09-08 revision (public object model,
`Share<T>()`-based `IOptions<T>`/Monitor identity vs. fresh-per-resolution
`IOptionsSnapshot<T>`, source non-disposal, and the concurrent-access
contract).

Done when: every behavior in ADR-0061's Decision Outcome has a passing
deterministic automated test proving it; the real
`alexa-vox-craft`/`MediatRTestProfile.cs` `IOptions<SkillServiceConfiguration>`
scenario is validated against freshly-packed local packages via
`scripts/dogfood-validate.sh` and demonstrably replaces its current
two-registration setup with one coherent call; the Configuration Cookbook
deliverable (ADR-0061's Documentation consequences, independent of this
package) is written; `skills/compono` (`SKILL.md` + a new
`references/options.md`) teaches an agent `Compono.Options`'s package
boundary, identity model, named-option divergence, and Finding B
limitation, validated by a baseline-vs-updated skill-eval comparison; and
package-validation/AOT/trimming checks pass alongside every other
publishable Compono package.

## Scope

Exactly ADR-0061's Decision Outcome (as revised 2026-09-08) — see that ADR
for the full rationale; this plan does not re-derive it. One cohesive
effort, one PR — no phase split; the work is one package plus its
required documentation, skill, and validation surface, not a
multi-milestone effort.

**In scope:**
- `Compono.Options` package: `TestOptionsSource<T>`, an internal
  frozen-view type, `UseOptions<T>` builder extension,
  `UnconfiguredNamedOptionException`.
- `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>` all satisfied
  coherently per the ADR's identity model.
- Named options, deterministic changes, subscription disposal, the
  concurrent-access contract.
- `Compono.Options.Tests` — deterministic contract tests for every ADR
  behavior (§"Test Plan").
- Package guide, skill reference, `evals.json` entries.
- Configuration Cookbook (ADR-0061 Documentation consequences —
  independent of the package's own code, but this plan's definition of
  done per the ADR).
- Dogfooding validation against real `alexa-vox-craft`.

**Explicitly out of scope** (per ADR-0061, unchanged):
- Any solution to ADR-0052 Finding B, or automatic no-registration
  composition of an arbitrary `T`.
- A `Compono.Configuration` package.
- Real `IConfiguration`/change-token/file-watcher simulation,
  `IOptionsFactory<T>` pipeline, DI-scope/container simulation.
- `CallVerifier`-based verification (deferred, not designed against here).
- Source generation, reflection.

## Task 0 — Finalize public API surface (naming; not architecture)

Names below are the finalized surface this plan implements — a naming
decision, not a reopening of ADR-0061's architecture. Checked against
existing conventions (`Register<T>`/`Share<T>()`/`UseNSubstitute()`/
`UseBogus()`/`UseLogging()`/`Compono.Http`'s `TestHttpHandler`/
`UnmatchedHttpRequestException`) before freezing:

- [x] **`TestOptionsSource<T>`** — the one public type per settings type
  (`Compono.Options` namespace). Constructor:
  `TestOptionsSource(T initialValue)` (sets `Options.DefaultName`'s
  initial value; matches `TestHttpHandler`'s "construct directly, no
  factory" shape). Implements `IOptionsMonitor<T>` directly
  (`CurrentValue`, `Get(string? name)`, `OnChange(Action<T, string?>)`).
- [x] **`.Change(T value)`** / **`.Change(string name, T value)`** — the
  one mutation surface. Also how a named value is *first* established
  (no separate "add" API — `Change` both creates and updates a named
  entry, keeping the surface small per ADR-0061's cost-proportionality
  driver). Named after the real contract's own vocabulary
  ("`OnChange`," "options changed") rather than a generic `Set`/`Update`
  that could be confused with `Compono.TestDoubles`'s `.Returns(...)`
  configuration vocabulary.
- [x] **`CompositionBuilder.UseOptions<T>(TestOptionsSource<T> source)`**
  — the one wiring call, matching the established `Use*` builder-extension
  family exactly (`UseNSubstitute`/`UseBogus`/`UseLogging`). Internally:
  registers `IOptions<T>` and `IOptionsMonitor<T>` via `Share<T>()`
  (one instance per graph — `IOptionsMonitor<T>` *is* `source` itself;
  `IOptions<T>` is a frozen view captured once and shared); registers
  `IOptionsSnapshot<T>` as an ordinary, non-shared `Register<T>` factory
  (fresh frozen view per resolution). Reads naturally inline and inside
  an `ICompositionProfile.Configure`, per ADR-0061.
- [x] **`UnconfiguredNamedOptionException`** — dedicated, package-owned
  exception type (matching `Compono.Http`'s `UnmatchedHttpRequestException`
  precedent, not a generic `KeyNotFoundException`/`InvalidOperationException`),
  thrown by `Get(name)`/a frozen view's `Get(name)` when no value has been
  configured for `name`. Message states: the settings type (`T`'s name),
  the requested name, that it was never configured via `.Change(name, ...)`,
  and points at `.Change(name, value)` as the fix — without hardcoding a
  literal code sample that could drift from the real API if it changes
  later.
- [x] Internal frozen-view type — **not public**, no name exposed in any
  public API; implements `IOptions<T>`/`IOptionsSnapshot<T>` by capturing
  `TestOptionsSource<T>`'s state at construction time.

## Task 1 — Package/project scaffolding

Verified against `src/Compono.Http/Compono.Http.csproj` (closest
precedent: hand-written, non-generated, `Microsoft.Extensions.*`-adjacent)
and `src/Compono.Bogus/Compono.Bogus.csproj` (closest precedent for a
`PackageReference` to an external NuGet dependency beyond core `Compono`).

- [x] `src/Compono.Options/Compono.Options.csproj` — `net8.0;net9.0;net10.0;net11.0`,
  `LangVersion=latest`, `ImplicitUsings`/`Nullable` enabled, `Title`/
  `Description` following the established per-package copy style.
  `IsAotCompatible` — **verify empirically** (per `Compono.Http`'s own
  documented finding that this must be set explicitly and confirmed with
  a real analyzer-contract test, not assumed) whether this package needs
  it; likely yes, since it ships no `Requires*`-annotated members and
  should build/consume cleanly under AOT/trim analysis the same way
  `Compono.Logging`/`Compono.DependencyInjection` do.
- [x] `ProjectReference` to `..\Compono\Compono.csproj`
  (`PrivateAssets="none"`, matching every integration package — lets
  Compono's embedded analyzer flow through) + the
  `PinProjectReferenceVersionsExact` target (copy verbatim from
  `Compono.Http.csproj`/`Compono.Bogus.csproj`).
- [x] `PackageReference Include="Microsoft.Extensions.Options"` (no
  inline version — centrally managed).
- [x] `Directory.Packages.props`: add per-TFM `Microsoft.Extensions.Options`
  `PackageVersion` entries, following `Microsoft.Extensions.Logging.Abstractions`'s
  existing four-TFM-conditioned-`ItemGroup` pattern exactly (net8/net9/net10/net11,
  each pinned to that TFM's matching `Microsoft.Extensions.Options`
  release line) — verify current released versions for each TFM at
  implementation time rather than assuming today's numbers stay current.
- [x] `InternalsVisibleTo` → `Compono.Options.Tests`.
- [x] Add both projects to `Compono.slnx`.
- [x] `docs/packages/compono-options.md` package guide stub (content: Task
  9), added to `docs/packages/index.md`'s table and `docs/roadmap/index.md`'s
  shipped-package list, `docs/index.md`'s package table, and root
  `README.md`'s package table — the same discoverability surfaces every
  prior package addition (`Compono.Http`/`Compono.Logging`/`Compono.NUnit`)
  updated. Verify the exact current list of files needing this touch
  against the most recent prior package PR rather than assuming this
  list is exhaustive.
- [x] `docs/roadmap/future-packages.md`: move the `Compono.Options` entry
  from "Roadmap items" to a shipped-package note once implementation
  lands (mirroring how `Compono.TUnit`/`Compono.NUnit`/`Compono.Http`
  graduated), and `docs/roadmap/proposed-adrs.md`: remove the ADR-0061
  entry once this plan reaches `Done` (per that page's own "entries
  removed once implemented" rule).
- [x] CI/release integration: confirm the eight-(soon nine-)publishable-package
  CI matrix (`docs/contributing.md`'s package-validation gate: API-compatibility
  baseline, packed `.nupkg` contents inspection, local-feed consumer smoke
  test) picks up `Compono.Options` automatically vs. needs an explicit
  addition — inspect the actual CI workflow file(s) rather than assuming.

## Task 2 — `TestOptionsSource<T>` implementation

- [x] Named-value storage satisfying ADR-0061's concurrent-access
  contract: internally valid under concurrent reads/changes/subscribe/
  unsubscribe; no corruption across names or within one name. Choose the
  smallest primitive that satisfies this (a single `lock` around the
  store is very likely sufficient and clearer than a lock-free structure
  for this access pattern — confirm against the Test Plan's concurrency
  tests before over-engineering).
- [x] `Get(string? name)`: normalizes `null`/omitted to `Options.DefaultName`
  (`string.Empty`); returns the stored value if configured; throws
  `UnconfiguredNamedOptionException` otherwise. **Case-sensitive** name
  comparison (ordinal), matching the real contract.
- [x] `CurrentValue => Get(Options.DefaultName)`.
- [x] `.Change(value)`/`.Change(name, value)`: establishes the new value
  in the store **before** invoking any subscriber (ADR-0061's ordering
  requirement — a callback reading `CurrentValue`/`Get(name)` mid-callback
  observes the new value), then invokes `OnChange` subscribers
  **synchronously**, for that name only (matching real
  `OptionsMonitor<T>`'s per-name `InvokeChanged`).
- [x] `OnChange(Action<T, string?> listener)`: a plain internal C# event
  (`+=`/`-=`) — **not** a single-field assignment (the exact bug in the
  community's naive fake) — returning a real `IDisposable` whose
  `Dispose()` performs `-=` against the exact delegate instance
  subscribed, and is **idempotent** (disposing twice is a no-op, matching
  ordinary .NET subscription-disposable convention). **No per-subscriber
  exception isolation** — a throwing subscriber blocks subsequent ones,
  matching real `OptionsMonitor<T>` exactly (do not add a `try`/`catch`
  around each invocation).
- [x] **No `IDisposable`/`IAsyncDisposable` on `TestOptionsSource<T>`
  itself** — enforced by the type simply never implementing either
  interface; add a test asserting this (`typeof(TestOptionsSource<>)`
  does not implement `IDisposable`/`IAsyncDisposable`) so a future PR
  can't silently reintroduce it.

## Task 3 — Internal frozen-view type

- [x] Captures `TestOptionsSource<T>`'s current default/named values at
  construction time (a snapshot copy, not a live reference into the
  source's store) — implements `IOptions<T>` (`Value` only) and
  `IOptionsSnapshot<T>` (`Value` + `Get(name)`), reading only its own
  captured snapshot, never touching the source again after construction.
  Also throws `UnconfiguredNamedOptionException` for an unconfigured name
  at the moment of capture (matching the source's own behavior, since a
  name absent at snapshot time is absent in the snapshot).

## Task 4 — `UseOptions<T>` builder extension

- [x] `CompositionBuilder.UseOptions<T>(this CompositionBuilder builder, TestOptionsSource<T> source)`:
  - `builder.Share<IOptionsMonitor<T>>()` is **not** needed —
    `TestOptionsSource<T>` itself *is* `source`, registered directly:
    `builder.Register<IOptionsMonitor<T>>(() => source).Share<IOptionsMonitor<T>>()`
    (confirm exact call shape against `CompositionBuilder`'s real
    `Share<T>()`/`Register<T>()` signatures during implementation — this
    plan states the *intended effect*, not a guessed-at exact call
    chain).
  - `IOptions<T>`: registered + shared, backed by one frozen view
    captured once.
  - `IOptionsSnapshot<T>`: registered **without** `Share<T>()` — a fresh
    frozen view captured from `source`'s *then-current* state on every
    resolution.
  - Returns `CompositionBuilder` for fluent chaining, matching every
    other `Use*` extension's shape.

## Task 5 — Behavioral contract tests (`Compono.Options.Tests`)

Deterministic, handwritten test data (per `testing.md` — this repo
doesn't use AutoFixture-style generated data for its own tests).

**`IOptions<T>`:**
- [x] `Value` reflects the source's value at the moment `IOptions<T>` was
  first resolved.
- [x] Same instance/value returned on every subsequent resolution within
  one graph (shared identity).
- [x] Remains unchanged after a later `.Change(...)` on the source.

**`IOptionsSnapshot<T>`:**
- [x] A freshly-resolved snapshot reflects the source's *current* state.
- [x] Repeated reads (`Value`, `Get(name)`) on the *same* resolved
  snapshot instance stay stable even if the source changes afterward.
- [x] A *second*, later resolution within the same graph, after a source
  change, produces a *new* snapshot reflecting the new state (distinct
  identity from the first).
- [x] Named values work identically to Monitor's (`Get(name)`).

**`IOptionsMonitor<T>`:**
- [x] `CurrentValue`/`Get(Options.DefaultName)` agree.
- [x] `Get(name)` for a configured name returns that name's value.
- [x] Same instance on every resolution within a graph (shared identity).
- [x] `.Change(value)` updates `CurrentValue` and fires subscribers.
- [x] Value is visible via `CurrentValue`/`Get` *from inside* a change
  callback (ordering requirement).
- [x] Callbacks are synchronous (no `Task`/thread hop observed).
- [x] Two+ independent subscribers both fire on one change.
- [x] Disposing one subscription stops only that listener; a second,
  still-subscribed listener keeps firing.
- [x] Disposing a subscription twice does not throw and does not affect
  other subscriptions (idempotent).
- [x] A throwing subscriber prevents a later-registered subscriber in the
  same invocation from firing (matches real behavior — assert this
  explicitly as *intended*, not accidentally-discovered, behavior).

**Named options:**
- [x] Default name (`Options.DefaultName`) and an explicit name are
  independent.
- [x] Case-sensitive: `"Foo"` and `"foo"` are different names.
- [x] Changing one name does not affect another name's value.
- [x] `Get`/`.Value`/`.CurrentValue` for an unconfigured name throws
  `UnconfiguredNamedOptionException` (Monitor, Snapshot, and — where
  applicable at capture time — the frozen view backing `IOptions<T>`).
- [x] Exception message names the settings type and the requested name.

**Coherence (the central contract test):**
- [x] Configure one `TestOptionsSource<T>`; wire via `UseOptions<T>`;
  resolve `IOptions<T>`, `IOptionsMonitor<T>`, and `IOptionsSnapshot<T>`
  all within the same graph; call `.Change(...)` on the source; assert:
  Monitor sees the new value; the already-resolved `IOptions<T>` stays at
  its original value; the already-resolved Snapshot stays at its
  original value; a *newly* resolved Snapshot after the change sees the
  new value. One test, all four assertions — this is ADR-0061's central
  claim, proven directly.

**Registration/composition:**
- [x] Ordinary Compono duplicate-registration semantics hold, corrected
  from this task's original "first-registration-wins" framing (see
  ADR-0061 Amendment 1): an explicit consumer
  `Register<IOptions<T>>(...)`/`Register<IOptionsMonitor<T>>(...)`/
  `Register<IOptionsSnapshot<T>>(...)` collides with `UseOptions<T>`'s own
  internal registration for the same type and throws
  `CompositionConfigurationException` at `Composer.Create`, regardless of
  call order — `CompositionBuilder`'s existing, unchanged, strict
  duplicate-registration rule (ADR-0019), not an override/precedence
  mechanism. No special-cased behavior introduced by this package (test
  against the real rule, not a restated assumption of it) — the wiring
  call is the same ordinary call whether used inline or inside
  `builder.Configure` — no special profile mechanism, no different
  behavior inline vs. inside a profile.
- [x] `Share<T>()` is used correctly for `IOptions<T>`/`IOptionsMonitor<T>`
  and correctly *not* used for `IOptionsSnapshot<T>` — assert both halves
  explicitly (a regression that accidentally shares Snapshot, or stops
  sharing `IOptions<T>`, must fail a test).

**Disposal:**
- [x] `TestOptionsSource<T>` does not implement `IDisposable`/`IAsyncDisposable`
  (type-level assertion, Task 2).
- [x] Multiple independent subscriptions dispose independently.

**Concurrency (focused, not exhaustive stress testing):**
- [x] Concurrent reads (`CurrentValue`/`Get(name)`) from multiple threads
  while a change is in flight never observe a torn/partially-written
  value (assert the read is always *some* valid, previously-`Change`d
  value, never a corrupted intermediate state).
- [x] Concurrent `.Change(...)` calls for *different* names don't corrupt
  each other's storage.
- [x] Concurrent subscribe/unsubscribe calls, including concurrent with
  an in-flight notification, don't throw or corrupt the subscriber list.
- [x] Not tested (explicitly, per ADR-0061): cross-test sharing of one
  source instance — not a supported scenario, no test manufactures one.

**Diagnostics:**
- [x] `UnconfiguredNamedOptionException`'s message is asserted verbatim
  (or via a stable substring match) in at least one test per interface
  surface that can throw it (Monitor, Snapshot). **Implementation note:**
  `IOptions<T>` cannot observably throw this exception in practice — it
  exposes no named-lookup surface (per implementation clarification #1;
  only `Get(name)` on Monitor/Snapshot accepts a name), and its one lookup
  path (the default name) is always established by
  `TestOptionsSource<T>`'s constructor. See Notes.

## Task 6 — Native AOT / trimming validation

- [x] `Compono.Options.AotSmokeTest` (or equivalent), following
  `Compono.Http.AotSmokeTest`'s established shape — confirm `PublishAot`
  succeeds with zero `IL2xxx`/`IL3xxx` warnings from this package's own
  code.
- [x] Confirm no reflection anywhere in `Compono.Options`'s own dispatch
  (a direct code-reading check, the same "confirms this project's own
  dispatch code stays genuinely reflection-free" validation
  `Compono.Http.csproj`'s comments describe doing for itself).

## Task 7 — Package-validation / quality gates

- [x] Nullable annotations clean (no warnings) across the package.
- [x] XML doc comments on every public member (repo-wide hard requirement
  — `documentation.md`).
- [x] Public-API-compatibility baseline file — **not applicable**: verified
  no sibling package (`Compono.Http`, `Compono.NUnit`, etc.) has a
  per-package `PublicAPI.txt`/ApiCompat-baseline file in this repo; API
  compatibility is enforced generically by `package-validation.yaml`'s
  `PackageValidationBaselineVersion`/nuget.org-baseline mechanism (no
  baseline exists yet for a package's first publish), which
  `Compono.Options`'s addition to `PACKAGES` (Task 1) already opts into —
  no package-specific file needed.
- [x] Packed `.nupkg` contents inspected (README embedded, correct
  `TargetFrameworks`, no accidental extra content) per the existing
  package-validation gate.
- [x] Confirm no accidental transitive `Microsoft.Extensions.DependencyInjection`
  dependency is pulled in beyond what `Microsoft.Extensions.Options`
  itself requires.

## Task 8 — Registration-order/profile dogfooding inside this repo

- [x] A sample/usage snippet (in the package guide, Task 9, and/or a
  `Compono.Options.SampleTests`-shaped project if this repo's existing
  per-package sample-project convention applies here — check
  `Compono.Http.SampleTests`/`Compono.Logging`'s equivalent for
  precedent) exercising `UseOptions<T>` both inline and inside an
  `ICompositionProfile`.

## Task 9 — `Compono.Options` package documentation

`docs/packages/compono-options.md`, matching the established package-guide
shape (`compono-http.md`/`compono-logging.md` as the template):

- [x] Installation.
- [x] Basic setup — `IOptions<T>`.
- [x] `IOptionsSnapshot<T>` — the identity model stated in consumer terms
  ("a fresh view each time it's resolved, reflecting the source's current
  state") without requiring the reader to know `Share<T>()` is involved
  internally.
- [x] `IOptionsMonitor<T>` — `CurrentValue`, `Get(name)`, `.Change(...)`,
  `OnChange`/disposal.
- [x] Named options, including the case-sensitivity note.
- [x] Deterministic changes and the ordering guarantee (`.Change`
  establishes the value before subscribers run).
- [x] Inline usage and profile usage, **using the real
  `MediatRTestProfile.cs` before/after** (§"Dogfooding," below) as the
  worked example.
- [x] Explicit "Unconfigured named options" section stating the
  intentional divergence from real `IOptionsFactory<T>`'s silent-default
  behavior, and why (ADR-0045 precedent).
- [x] "What this package doesn't do" — no `IConfiguration`/change-token
  simulation, no `IOptionsFactory<T>` pipeline, no DI-scope simulation,
  no automatic no-registration composition (ADR-0052 Finding B).
- [x] Cross-link to the Configuration Cookbook (Task 10) for
  `IConfiguration` itself.

## Task 10 — Configuration Cookbook (required deliverable, independent of the package's code)

Per ADR-0061's Documentation consequences — this is **not optional
follow-up**; it's part of this plan's own definition of done even though
no `Compono.Configuration` package exists. Location: this repo's existing
Cookbook structure (`docs/cookbook/` — verify exact directory/index
convention against an existing recipe before adding a new one).

- [x] **Basic in-memory `IConfiguration` composition** —
  `ConfigurationBuilder`/`AddInMemoryCollection`/`Build()` +
  `Register<IConfiguration>(...)`.
- [x] **Layered configuration / test-specific overrides** — later
  `AddInMemoryCollection` source wins; a base-values-plus-override-values
  example.
- [x] **Reusable configuration through a profile** — an
  `ICompositionProfile` establishing baseline configuration once,
  reused/varied per test.
- [x] **`GetSection`/common consumption patterns** — brief, not a general
  Microsoft Configuration tutorial.
- [x] **Relationship to `Compono.Options`** — explicit routing: ordinary
  Configuration + `Register<IConfiguration>` for `IConfiguration` itself;
  `Compono.Options` for the Options interfaces; no `Compono.Configuration`
  package exists, stated plainly.
- [x] Cross-link from `docs/packages/compono-options.md` (Task 9) and
  from `docs/roadmap/future-packages.md`'s "Documentation-only ideas"
  entry (mark it fulfilled once this recipe is written).

## Task 11 — Skill and evals (mandatory, not optional cleanup)

- [x] `skills/compono/SKILL.md` — add `Compono.Options` to the
  package-detection table, following the existing per-package row shape.
- [x] `skills/compono/references/options.md` — new reference file
  (verify against the existing per-package reference files' shape/depth,
  e.g. `references/http.md`/`references/logging.md`), covering: when to
  recommend `Compono.Options`; the identity model in agent-actionable
  terms; the `IConfiguration`-vs-`Compono.Options` routing distinction
  (pointing at the Configuration Cookbook for the former); the
  intentional unconfigured-named-option-throws behavior; the ADR-0052
  Finding B limitation (an agent should never suggest "just declare the
  dependency with no registration" for this package).
- [x] `skills/compono/evals/evals.json` — add eval scenarios: recommending
  `Compono.Options` for a Monitor/Snapshot need; correctly routing a
  plain `IConfiguration` need to the Cookbook pattern instead of
  inventing a `Compono.Configuration` package; correctly explaining the
  unconfigured-named-option throw when asked "why did my test throw
  `UnconfiguredNamedOptionException`."
- [x] Run the mandatory baseline-vs-updated skill-eval comparison in
  clean agent contexts, per this repo's established skill-eval workflow.
  Keep generated eval workspaces out of source control.

## Task 12 — Dogfooding validation (`alexa-vox-craft`)

Per ADR-0061's dogfooding validation and this plan's own Scope — **the
real target is composition-ergonomics validation, not Monitor/Snapshot
coverage**, which this plan does not claim and should not manufacture.

- [x] Run `scripts/dogfood-validate.sh` against
  `/Users/ncipollina/source/repos/layered-craft/alexa-vox-craft`,
  packing `Compono`, `Compono.Options`, and whatever else that repo's
  suite already depends on, from this working tree's current state, into
  a local feed — no `ProjectReference` bypass.
- [x] In an isolated branch/worktree of `alexa-vox-craft` (never commit
  against the real repo from this task), replace
  `MediatRTestProfile.cs`'s
  `Register<SkillServiceConfiguration>(...)`/`Register<IOptions<SkillServiceConfiguration>>(...)`
  pair with the `Compono.Options` equivalent and confirm the suite still
  passes.
- [x] Record the before/after in the package guide (Task 9) as the
  worked profile example.
- [x] **If the resulting API is awkward, unnecessarily verbose, or
  doesn't genuinely improve this real scenario, stop and report that
  evidence rather than rationalizing the design** — per this plan's
  explicit instruction; do not silently patch around a bad finding here.
- [x] Do **not** attempt to manufacture `IOptionsMonitor<T>`/
  `IOptionsSnapshot<T>` usage in `alexa-vox-craft` merely to claim
  dogfooding coverage for those interfaces — their correctness is
  validated by Task 5's deterministic contract tests instead, and
  ADR-0061's own honest disclosure of this gap stays accurate.

## Critical Files

- `src/Compono.Options/Compono.Options.csproj` — new package project.
- `src/Compono.Options/TestOptionsSource.cs` — the public source/Monitor
  type.
- `src/Compono.Options/*` — internal frozen-view type, `UseOptions`
  builder extension, `UnconfiguredNamedOptionException`.
- `test/Compono.Options.Tests/**` — contract tests (Task 5).
- `test/Compono.Options.AotSmokeTest/**` — AOT validation (Task 6).
- `Directory.Packages.props` — `Microsoft.Extensions.Options` per-TFM
  versions.
- `Compono.slnx` — new project entries.
- `docs/packages/compono-options.md`, `docs/packages/index.md`,
  `docs/index.md`, `README.md`, `docs/roadmap/index.md` — discoverability
  surfaces.
- `docs/cookbook/**` — the new Configuration recipe (Task 10).
- `docs/roadmap/future-packages.md`, `docs/roadmap/proposed-adrs.md` —
  status updates once shipped.
- `skills/compono/SKILL.md`, `skills/compono/references/options.md`,
  `skills/compono/evals/evals.json`.

## Test Plan

See Task 5 in full — deterministic, handwritten test data throughout
(`testing.md`); every ADR-0061 behavior gets its own named test, not a
single monolithic scenario, except the coherence test (§"Coherence"),
which is deliberately one test proving the central cross-interface claim
in one place. Concurrency tests are focused correctness checks (no torn
state, no corrupted store), not stress/performance benchmarks. AOT/trim
validated by Task 6's smoke test. Real-consumer validation via Task 12's
dogfooding — explicitly scoped to composition-ergonomics, not
Monitor/Snapshot coverage.

## Notes

**Implementation-clarification memo (pre-implementation, incorporated
before any code was written):** three clarifications refined Task 0/2/5
without reopening ADR-0061's architecture: (1) `IOptions<T>` has no
`Get(name)` named-lookup surface — only `Value`; tests and docs were
written against the real interface contract, not an invented one. (2) The
identity/lifetime contract is what's load-bearing, not literally calling
`Share<T>()`; the implementation does call it (`Register<IOptionsMonitor<T>>(() => source).Share<IOptionsMonitor<T>>()`,
same for `IOptions<T>`) because it was in fact the smallest, most direct
way to satisfy the contract — no contortion was needed. (3) `Change(value)`/
`Change(name, value)` as both setup and mutation was pressure-tested during
dogfooding and found natural, not awkward — no evidence emerged to expand
the surface.

**API naming finalized exactly as Task 0 anticipated** — no deviation:
`TestOptionsSource<T>`, `.Change(T)`/`.Change(string, T)`,
`CompositionBuilder.UseOptions<T>(source)`, `UnconfiguredNamedOptionException`.

**Diagnostics coverage gap (expected, not a defect):** `IOptions<T>` can
never observably throw `UnconfiguredNamedOptionException` in practice — its
one lookup path (the default name) is always established by
`TestOptionsSource<T>`'s constructor, and it exposes no named-lookup
surface at all (implementation clarification #1). Task 5's diagnostics
test coverage is therefore Monitor + Snapshot only, not all three
interfaces — this was anticipated, not discovered as a surprise.

**`AttributeAccessedMemberTypes`/AOT annotation needed, not anticipated by
the Plan:** `IOptions<T>`/`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`'s real
Microsoft signatures carry
`[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]`
on their `TOptions` type parameter (for `IOptionsFactory<T>`'s own
reflection-based construction path elsewhere in the framework, unused by
this package). `TestOptionsSource<T>`/`FrozenOptionsView<T>`/`UseOptions<T>`
had to carry the identical attribute on their own `T` to satisfy the
trim/AOT analyzer once `IsAotCompatible=true` was set (IL2091 otherwise) —
a mechanical propagation, not a design decision; no behavior change.

**Namespace collision, mechanical fix:** this package's own namespace is
`Compono.Options`, colliding with `Microsoft.Extensions.Options`'s
`Options` static class (`Options.DefaultName`). Resolved with a `using
MSOptions = Microsoft.Extensions.Options.Options;` alias — purely a naming
mechanic, no public API surface affected.

**Registration-precedence test finding — genuinely contradicted a specific
ADR-0061 claim, corrected via Amendment 1, not just imprecise phrasing:**
ADR-0061's original Decision Outcome claimed an explicit consumer
registration written after `UseOptions<T>` "interacts through Compono's
ordinary, unchanged first-registration-wins rule" and that such a consumer
"still gets ordinary, predictable Compono behavior" — describing a working
override. Real `CompositionBuilder.Register<T>` has no such override
mechanism for an exact-type collision: registering the same type twice
(once by a consumer, once by `UseOptions<T>`'s own internal wiring) is
always a strict build-time `CompositionConfigurationException`
(ADR-0019), regardless of order — there is no partial override of one of
the three Options interfaces while keeping `UseOptions<T>`'s coherence
guarantee for the other two. This is still "no special-cased precedence
invented for this package" (Compono's real rule applies unmodified), but
the ADR's own description of what that rule *does* was factually wrong,
not just loosely worded — recorded as **ADR-0061 Amendment 1
(2026-09-08)**, not silently rewritten in place. The test
(`OrdinaryFirstRegistrationWinsPrecedence_HoldsUnchanged_ForAnExplicitConsumerOverride`,
`CompositionBuilderExtensionsTests.cs`) proves the real behavior (a thrown
`CompositionConfigurationException`) directly.

**`Microsoft.Extensions.Options` net11.0 packaging quirk — pre-existing,
confirmed not package-specific:** the packed `.nuspec`'s net11.0 dependency
group carries no explicit `Microsoft.Extensions.Options` entry (satisfied
by net11.0's own shared framework) — identical to `Compono.Logging`'s
existing, `Accepted` `Microsoft.Extensions.Logging.Abstractions` net11.0
behavior. Confirmed via a real local pack + `inspect-packed-nupkgs.sh`
(extended with a `Compono.Options` case block, Task 7) before concluding
this, not assumed.

**Dogfooding — real evidence, environment obstacle resolved without
weakening the check; also the evidence behind ADR-0061 Amendment 1's
second correction** (coherence is guaranteed among `IOptions<T>`/
`IOptionsSnapshot<T>`/`IOptionsMonitor<T>`, not between the bare settings
type `T` and those three merely because `T` happens to be registered
separately — `UseOptions<T>` never touches a plain `Register<T>()`)**:**
the `alexa-vox-craft` consumer repo has 25 projects;
`SkillServiceConfiguration` is consumed both as `IOptions<T>`
(`SkillMediatorTests`/`DefaultResponseBuilderTests`) and as the bare type
(`ServiceRegistrarTests`) in the same test project, so `UseOptions<T>`
alone couldn't replace the old two-registration shape 1:1 — the plain
`Register<SkillServiceConfiguration>` registration was kept, now sourced
from the same instance passed to `TestOptionsSource<T>`, rather than a
second independently-constructed one (see the package guide's real
before/after). Running the dogfooding validation from a `/tmp`-rooted git
worktree hit a macOS `/tmp`→`/private/tmp` symlink path-duplication
artifact that broke assembly loading for *every* project in the consumer
repo (confirmed byte-identical on completely unmodified `main` code, ruling
out any connection to this change) — resolved by moving the isolated
worktree to a non-`/tmp` sibling directory, not by weakening the
validation. Final result: `scripts/dogfood-validate.sh` — full
`alexa-vox-craft` solution, 2564/2564 tests passed, `Compono.Options`
(and every other requested package) confirmed resolved to the exact
freshly-packed local version. The real repo's working tree was untouched
throughout (worktree + branch deleted after).

**Full local-solution `dotnet test` note (this repo, not the consumer):**
one full `Compono.slnx` run was killed by the OS (SIGKILL, exit 137) after
an anomalous 12-minute hang in `Compono.Logging.Tests` (net9.0) under
heavy parallel resource contention — before the kill, all 3736 tests
including every `Compono.Options.Tests` case had already passed (0
failed). Re-ran `Compono.Logging.Tests` in isolation immediately after:
248/248 passed in ~5s, confirming the hang was a resource-contention flake
unrelated to this change, not a regression.

**One factual correction to ADR-0061 was needed (Amendment 1), no
architectural contradiction.** The registration-precedence finding above
corrected a specific, wrong factual claim in the original Decision
Outcome text (a described override mechanism that doesn't exist) — this
is recorded as a dated Amendment, not a silent rewrite, per this repo's
ADR-immutability convention. It does not touch the core decision (object
model, identity/lifetime contract, `Share<T>()`'s role, disposal, or the
concurrency contract) — those hold exactly as decided. Every other
finding during implementation, testing, and dogfooding was an anticipated
clarification, a mechanical AOT/namespace fix, or an environment artifact
resolved without touching validated behavior.

**Final review pass (2026-09-08), before commit/PR:** an independent
correctness/design-consistency review of the working tree against
ADR-0061 found the implementation, tests, AOT proof, and dogfooding result
were all correct as reported — no code defects, no concurrency defects, no
public-API concerns. It found two documentation defects, both wording
overreach rather than implementation bugs, both now corrected: (1) the
"first-registration-wins"/override framing addressed above was still
present verbatim in ADR-0061's original Decision Outcome prose, this
plan's own Task 5 checklist item, and the Configuration Cookbook's
"Reuse Configuration Through a Profile" recipe (whose own worked example
would have thrown `CompositionConfigurationException` if followed
literally — fixed to pass the override through the profile's constructor
instead of a second `Register<IConfiguration>` call); (2) ADR-0061's
"Selected dogfooding validation target" prose overstated
`Compono.Options` as collapsing the bare-`T` + `IOptions<T>` registrations
into one call — corrected (ADR-0061 Amendment 1, second paragraph) to
state precisely what's guaranteed (coherence among the three Options
interfaces, from one source) versus what's just consumer discipline
(sharing one instance between a separate `Register<T>` and
`UseOptions<T>`). Both corrections are recorded as ADR-0061 Amendment 1,
not silent rewrites of the immutable Accepted text. No code, test, or
public API change resulted — `Compono.Options.Tests` re-run clean
(160/160) after the doc corrections, confirming no runtime behavior was
touched.
