# [PLAN-0004] Milestone 4: xUnit v3 Integration

**Status:** In Progress

**Implements:** [ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md)
(core `CompositionRow`/`CompositionRequestKind.TestParameter`/stage-2 read-gate
change), [ADR-0022](../adr/0022-compono-xunit-package-design.md) (`Compono.Xunit`
package: attribute API, binding algorithm, profile selection, seed policy,
diagnostics, packaging, testing strategy)

## Goal

```csharp
[Theory]
[Compose<ApplicationTestProfile>]
public void Creates_service(
    [Shared] IRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

runs under a real xUnit v3 runner: `service` receives a generated
`OrderService` whose own `IRepository` constructor parameter is the exact
same instance injected as `repository`; `command` is independently
composed; inline values (`[Compose("alice@example.com")]`) take
precedence over composition for the parameters they supply; a composition
failure's message contains a seed that, pasted into
`[Compose(Seed = ...)]`, reproduces the same failure.

## Scope

Per ADR-0021/ADR-0022's Decision Outcomes:

- New core `Compono` surface: `CompositionRow` (`int Seed`, matching
  `WithSeed(int)`/`ComposeAttribute.Seed`), `Composer.CreateRow(Type)`
  (unseeded case via a new non-negative-`int`-range
  `CompositionSeed.GenerateRowSeed()`, distinct from `Create<T>()`'s
  full-`ulong`-range `Generate()`), `CompositionRequestKind.TestParameter`,
  `PathSegment.TestParameter` — the **seventh** `PathSegment` kind (five
  original structured segments + `ManualResolve` = six existing; this is
  the seventh) with a matching seventh `RandomSource` fork tag — and the
  `CompositionContext` stage-2 read-gate change (scope lookup always
  attempted; write side unchanged).
- New `Compono.Xunit` package: `ComposeAttribute`/`ComposeAttribute<TProfile>`
  (`Xunit.v3.DataAttribute`), `SharedAttribute`, the inline/composed
  binding algorithm, `[Shared]` validation and ordering, profile
  selection, seed policy, diagnostics.
- Two new test projects: `test/Compono.Xunit.Tests` (direct `GetData`
  calls, no real runner) and `test/Compono.Xunit.SampleTests` (a real
  xUnit v3 project executed by a real runner).
- Doc updates: `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md`.

Explicitly deferred — see ADR-0022's Deferred Decisions and Non-goals:
class-level profile selection, non-positional/skip-position inline
values, multi-row `[Compose]`, out-of-declaration-order `[Shared]`
dependencies, name/qualifier-based sharing, `CancellationToken`
composition support, seed-in-display-name, combining `[Compose]` with
ordinary xUnit data attributes, generic/`ref`/`out`/`in`/`params` test
methods, and `Compono.NSubstitute`/`Compono.Bogus`-specific ergonomics.

## Phases

Each phase ships as its own PR, per `design-decisions.md`'s phase rule.

### Phase 0: Core engine extension point (ADR-0021)

**Status:** Done

- [x] `CompositionRequestKind.TestParameter` new enum member.
- [x] `PathSegment.TestParameter(int Ordinal, string Name)` new record;
      `CompositionPath.SegmentDisplayString()`/`NodeLabel()` gain a
      matching case (rendered like `ConstructorParameter`: `.{Name}` /
      `{typeName} {Name}`).
- [x] `RandomSource` gains a seventh fork tag, `TestParameterTag = 6`
      (the next unused zero-based tag after the six already in use — `0`–`4`
      for the five original structured segments, `5` for
      `ManualResolveTag`), and a `Fork` switch arm; determinism test
      proving all **seven** segment kinds (not six —
      `ConstructorParameter`/`RequiredMember`/`CollectionElement`/
      `DictionaryKey`/`DictionaryValue`/`ManualResolve`/`TestParameter`)
      fork distinctly at ordinal 0 from the same parent state (extends
      ADR-0012 Amendment 2's existing six-kind coverage).
- [x] `CompositionContext`: new constructor overload accepting `Type
      rootType`, pre-establishing `_path`/`_random` instead of leaving
      them for the first `Resolve` call to claim as root.
- [x] `CompositionContext.ResolveCore`: remove the `if (request.IsShared)`
      gate around the stage-2 scope *read*; the write side
      (`StoreSharedAndReturn`, `ResolveViaGeneratedPlan`'s shared branch)
      is unchanged.
- [x] New internal `CompositionContext` members backing
      `CompositionRow.ResolveShared`/`ShareExplicit`: `ResolveShared` is
      pipeline dispatch + scope-store; `ShareExplicit` skips dispatch
      (the value is already known) but runs the **exact same**
      authoritative validation `ResolveShared`'s successful pipeline
      result gets (`ValidateAuthoritativeValue` — null-for-non-nullable,
      runtime-type-not-assignable) before storing — implement both
      through one shared validate-then-store helper so the two paths
      can't drift apart, rather than duplicating the check.
- [x] Public `CompositionRow : ICompositionContext` sealed class:
      `int Seed` (matching `WithSeed(int)`/`ComposeAttribute.Seed` — not
      the internal `CompositionSeed`'s `ulong`), `ResolveShared<TValue>(descriptor)`,
      `ShareExplicit<TValue>(descriptor, value)`, plus the inherited
      `Resolve<TValue>(descriptor)`/`Resolve<TValue>()`/`ResolveCollectionSize()`
      forwarded to the wrapped context.
- [x] New `internal CompositionSeed.GenerateRowSeed()`, distinct from the
      existing `Generate()`: draws from `int`'s **non-negative** range
      (`Random.Shared.Next(0, int.MaxValue)`) rather than the full 64-bit
      range `Create<T>()`/`CreateMany<T>()` use, so an unseeded row's
      reported `int` seed is always the complete value (never truncated)
      *and* prints identically whether read via `CompositionRow.Seed`
      (`int`) or via `CompositionDiagnostic.Seed` (`ulong`, unaffected by
      this ADR) — see ADR-0021's Seed type consistency note for why
      non-negative specifically.
- [x] `Composer.CreateRow(Type declaringType)`: `_configuration.Seed ??
      CompositionSeed.GenerateRowSeed()`, then constructs the
      pre-rooted `CompositionContext`, then `CompositionRow` with
      `unchecked((int)seed.Value)`.
- [x] `Compono.Tests` coverage for this API in isolation, **before any
      `Compono.Xunit` code exists** — `Composer.CreateRow`/`CompositionRow`
      are a real new public entry point (`testing.md`'s "verifying a new
      public entry point" rule) and must not end up exercised only
      indirectly through xUnit integration tests written later. Cover:
      sibling `Resolve<TValue>(descriptor)` calls forking independently by
      ordinal (the root-collision bug ADR-0021's Context section
      identifies); `ResolveShared` making a value visible to a *later*,
      ordinary (non-shared) same-typed request in the same row;
      `ShareExplicit` storing an already-known value with the same
      validation `ResolveShared` applies to a pipeline-produced one (null-
      for-non-nullable, wrong-runtime-type — see the `ShareExplicit`
      bullet above); `CompositionDiagnostic.RootType` rendering as
      `declaringType` on a row failure; two independent `CreateRow` calls
      never sharing scope/seed; an explicit `WithSeed(int)` value
      round-tripping exactly through `CompositionRow.Seed`; an unseeded
      row's `CompositionRow.Seed` always non-negative and printing
      identically to a failing row's `CompositionDiagnostic.Seed` text.
- [x] `CreateInvocationDiscovery` extended to match
      `CompositionRow.Resolve<T>(descriptor)`/`ResolveShared<T>(descriptor)`
      call sites, alongside its existing `Composer.Create<T>()`/`CreateMany<T>()`
      matching (PR #22 review; ADR-0022 Amendment 2026-07-30, fix #1) —
      without this, a type reached only through a direct `CompositionRow`
      call never got a generated plan. Verified with an isolated
      `Compono.Generators.Tests` snapshot test per call shape (no
      `[Composable]`, no `Create<T>()`/`CreateMany<T>()` anywhere in the
      same source) and a real `dotnet pack` + local-feed +
      throwaway-consumer manual check proving the packaged analyzer
      (never `ProjectReference`) discovers and composes such a type
      correctly.
- [x] The descriptor-less `Resolve<T>()` overload is explicitly
      **excluded** from that discovery match (PR #22 review, second
      round; ADR-0022 Amendment 2026-07-31) — it forwards to the
      manual-resolve seam meant for a factory's own `context.Resolve<T>()`
      calls, which always throws `InvalidOperationException` for a
      `CompositionRow`-holding caller (`InvokeFactory` never hands a
      factory the `CompositionRow` wrapper). Discovering/documenting it as
      an ordinary entry point would have advertised a call shape that
      always throws at runtime - caught only after it had already been
      matched, documented, and (in an earlier manual verification pass)
      hit that exact throw firsthand. `CreateInvocationDiscovery` now
      requires `method.Parameters.Length == 1` to exclude it; the
      isolated generator test for this call shape now asserts *no* plan
      gets generated, not that one does.

### Phase 1: `Compono.Xunit` package skeleton and attributes (ADR-0022)

**Status:** Done

- [x] New `src/Compono.Xunit/Compono.Xunit.csproj` (net10.0;net11.0,
      `PackageReference` to `Compono` + `xunit.v3.extensibility.core`).
- [x] `SharedAttribute` (parameter-targeted marker).
- [x] `ComposeAttribute` (`Xunit.v3.DataAttribute`): constructor
      (`params object?[] inlineValues`), a plain attribute-legal
      `int Seed { get; set; }` backed by a private `int? _seed` field
      with an internal `SeedAsNullable` property the binding algorithm
      actually reads (mirroring `Xunit.v3.DataAttribute`'s own
      `Timeout`/`TimeoutAsNullable` pair — `int?` itself cannot be an
      attribute named-argument target, CS0655), `SupportsDiscoveryEnumeration() => false`,
      `GetData` stub wired to Phase 2's binding algorithm.
- [x] `ComposeAttribute<TProfile> : ComposeAttribute where TProfile :
      ICompositionProfile, new()`.
- [x] `Lazy<Composer>` + immutable binding-plan caching, keyed to the
      attribute instance. The cached binding plan holds only metadata
      derived once from `testMethod`: ordered `ParameterInfo`s, a
      descriptor template per parameter (ordinal, name, declaring type,
      nullability), each parameter's `[Shared]` flag, and the
      signature-validation result (generic method /
      `ref`/`out`/`in`/`params` / duplicate-`[Shared]`-type checks, run
      once). It never holds anything row-scoped: no `CompositionRow`, no
      seed, no scope, no composed value — those are created fresh on
      every `GetData` call by Phase 2's binding algorithm, never cached.
- [x] **Cached per-parameter invoker delegates**, built in the same pass
      as the binding plan above — `CompositionRow.Resolve<T>`/
      `ResolveShared<T>`/`ShareExplicit<T>` are generic, but
      `Compono.Xunit` only knows each parameter's type as a runtime
      `ParameterInfo.ParameterType`. For each parameter, once: close a
      private generic helper (`InvokeResolve<T>`/`InvokeResolveShared<T>`/
      `InvokeShareExplicit<T>`, each declared to *return* `object?`/`void`
      rather than `T`, so the `T → object` boxing a value-typed `T` needs
      happens inside the closed method body itself) via
      `MethodInfo.MakeGenericMethod(parameter.ParameterType)`, then
      `Delegate.CreateDelegate` it against a matching non-generic delegate
      shape (`ResolveInvoker`/`ResolveSharedInvoker`/`ShareExplicitInvoker`).
      Cache the three resulting delegates on that parameter's binding-plan
      entry. **`MakeGenericMethod`/`Invoke` must never run on the per-row
      path** — Phase 2's per-row binding calls only the cached delegates,
      an ordinary (non-reflective) invocation. Cover with a test asserting
      `MakeGenericMethod` construction happens exactly once per parameter
      across many repeated `GetData` calls on one attribute instance, not
      once per row.
- [x] **New generator discovery component for `[Compose]`-attributed test
      methods** (ADR-0022 Amendment 2026-07-30, fix #2; PR #22 review) —
      a separate `Compono.Generators.Discovery` component, not folded
      into `CreateInvocationDiscovery`, using
      `ForAttributeWithMetadataName` (the same mechanism
      `ComposableAttributeDiscovery` uses for `[Composable]`) to find
      methods attributed `[Compose]`/`[Compose<TProfile>]` and generate a
      plan for each eligible parameter type in that method's signature.
      Required because `Compono.Xunit`'s binding (this phase's own
      `MakeGenericMethod`-based invoker caching, above) never emits a
      textual `row.Resolve<T>(...)` call site in the consumer's own
      source for the now-fixed `CreateInvocationDiscovery` extension
      (Phase 0) to find — a type reached only as a `[Compose]` method's
      parameter needs its own discovery path, not an extension of the
      call-site one. "Eligible" mirrors Phase 2's own supported-shape
      table (excludes generic methods, `ref`/`out`/`in`/`params`
      parameters). Every eligible parameter gets a plan unconditionally,
      even one that's always inline-supplied in practice — see the ADR
      amendment for why statically predicting inline-vs-composed per
      call site isn't worth the duplication of Phase 2's own runtime
      inline-binding calculation.

### Phase 2: Binding algorithm, `[Shared]`, diagnostics (ADR-0022)

**Status:** Not Started

Ordered to match the runtime flow (composer/profile/seed are read from
the cache built once in Phase 1; everything after is per-row):

- [ ] Profile application (`[Compose<TProfile>]` → `builder.AddProfile<TProfile>()`;
      `[Compose]` → default `Composer.Create()`) and `Seed` property →
      `builder.WithSeed(...)` — both folded into Phase 1's cached
      `Lazy<Composer>` construction, so this is "read the cached
      `Composer`," not a per-row step.
- [ ] `Composer.CreateRow(declaringType)` for the row — **before**
      checking Phase 1's cached signature-validation result, not after:
      an unseeded row's seed doesn't exist until `CreateRow` runs, and
      every failure this package reports must include the row's real
      seed, so the row has to exist first even for a signature that's
      about to be rejected.
- [ ] **If `SeedAsNullable` has a value and it's negative, throw now**,
      using `row.Seed` (echoing the rejected value back). This is what makes
      every row `Compono.Xunit` creates have a non-negative seed
      unconditionally — `CompositionBuilder.WithSeed(int)` itself has no
      such restriction and happily accepts a negative value when the
      cached `Composer` is built (Phase 1), so this check is the only
      place that actually enforces it, and it must run before any other
      failure category so a rejected negative seed is reported clearly
      rather than surfacing as a confusing mismatch somewhere else.
- [ ] **Decide how a `TProfile.Configure` that itself calls `builder.WithSeed(...)`
      interacts with this check** (PR #23 review): Phase 1's cached
      `Lazy<Composer>` construction applies the profile
      (`builder.AddProfile<TProfile>()`, which runs `Configure` immediately)
      *before* reading `SeedAsNullable`, so a profile-supplied seed reaches
      `CompositionBuilder.WithSeed` — and therefore `Composer.CreateRow`'s
      `_configuration.Seed` — independently of `SeedAsNullable`, which stays
      `null` whenever the attribute itself doesn't set `Seed`. As drafted,
      the negative-seed check above only inspects `SeedAsNullable`, so a
      profile-supplied negative seed is invisible to it; the same gap means
      an unset attribute silently reuses the profile's seed for every row
      instead of generating a fresh one. Resolve this explicitly here
      (e.g. read the composer's actual configured seed for the negative
      check rather than `SeedAsNullable` alone, or reject a profile that
      configures a seed outright) rather than letting Phase 1's existing
      `ApplyProfile`-then-`Seed` ordering silently decide it.
- [ ] If Phase 1's cached signature-validation result is invalid, throw
      here, using `row.Seed` in the appended `Seed:` line — still before
      any parameter is bound or composed, so no random fork is consumed
      and no partially-composed row is ever produced; only *row creation*
      (and the negative-seed check above) now precede this check, not
      composition.
- [ ] Positional inline-value binding (index-based "supplied," explicit
      `null` distinguished from "not supplied" by array length only);
      too-many-inline-values checked against `testMethod.GetParameters().Length`.
      **Every** supplied inline value validated before any parameter is
      bound, shared, or composed — including an inline value targeting a
      `[Shared]` parameter, so this always runs before that parameter's
      `ShareExplicit` invoker is ever called:
      - `inlineValues[i] is null` → valid only if parameter `i`'s cached
        `Nullability` is `Nullable` (nullable reference type *or*
        `Nullable<T>`); otherwise a pre-composition `CompositionException`
        naming the parameter. Nullability-based only — never a
        `GetType()` call on a `null`.
      - `inlineValues[i]` non-`null` → valid only if
        `inlineValues[i]!.GetType()` is assignable to
        `Nullable.GetUnderlyingType(parameterType) ?? parameterType` —
        **not** the raw declared type directly (a non-null `Nullable<T>`
        boxes as a boxed `T`, so `[Compose(42)]` for an `int?` parameter
        would be wrongly rejected against the raw `int?` type); otherwise
        a pre-composition `CompositionException` naming the parameter and
        both types.
      Both categories use the appended `Seed:` line (`row.Seed`).
- [ ] `[Shared]`-first, declaration-order composition (composed via each
      parameter's cached `resolveSharedInvoker`, inline via
      `shareExplicitInvoker`), then remaining parameters via
      `resolveInvoker` — never a direct, runtime-typed
      `row.Resolve<T>(...)`/`ResolveShared<T>(...)`/`ShareExplicit<T>(...)`
      call; always through Phase 1's cached delegates.
- [ ] Construct the final `TheoryDataRow` from the assembled `object?[]`
      **in method declaration order** — binding/composition order (shared
      first, then the rest) and output order are intentionally different;
      the array passed to `TheoryDataRow` must match the method's own
      parameter order. Set `Traits["Compono.Seed"] = [row.Seed.ToString()]`
      unconditionally, on every row regardless of whether it will pass or
      fail — the milestone's "failure output includes a seed" exit
      criterion isn't scoped to composition failures only, and
      `Compono.Xunit` can't know at `GetData` time whether the theory
      body will later throw its own assertion failure, so the trait can't
      be applied only-on-failure.

### Phase 3: Test suites and verification (ADR-0022's Testing Strategy)

**Status:** Not Started

- [ ] `test/Compono.Xunit.Tests`: binding-algorithm unit tests
      (inline-only/composed-only/mixed/too-many/non-null-type-mismatch),
      `[Shared]` detection (duplicate types, before/after ordering),
      profile caching/reuse assertion, unsupported-signature detection,
      seed determinism, concurrency-stress test on a shared cached
      attribute instance.
- [ ] Inline `null` handling, all four combinations: accepted for a
      nullable reference-typed parameter and for a `Nullable<T>`
      parameter; rejected (clear pre-composition exception, no
      `NullReferenceException` from an attempted `GetType()` on `null`)
      for a non-nullable reference-typed parameter and for a non-nullable
      value-typed parameter — each combination covered for both an
      ordinary parameter and an inline-`[Shared]` parameter, asserting
      rejection happens before `ShareExplicit`'s invoker is ever reached.
- [ ] A **non-null** inline value targeting a `Nullable<T>` parameter is
      **accepted** (e.g. `[Compose(42)]` for an `int?` parameter) — the
      regression test for the boxed-`T`-vs-boxed-`Nullable<T>` bug the
      `Nullable.GetUnderlyingType` unwrap fixes; without the unwrap this
      case fails even though nullable parameters are fully supported.
- [ ] Every returned `ITheoryDataRow` carries a `"Compono.Seed"` trait
      matching `row.Seed` exactly, for both a passing-shaped row and a
      deliberately-failing one — proving the trait is unconditional, not
      only attached when composition is about to fail.
- [ ] Cached invoker delegates: `MakeGenericMethod` construction runs
      exactly once per parameter across many repeated `GetData` calls on
      one attribute instance (not once per row); a value-typed and a
      reference-typed parameter both compose correctly through their
      cached delegate (proving the `T → object` boxing inside the closed
      helper works for both).
- [ ] `[Compose(Seed = <negative>)]` rejected with a clear pre-composition
      exception naming the rejected value, distinct from every other
      signature-validation failure; a non-negative configured `Seed`
      round-trips unchanged through `row.Seed`. Combined with Phase 0's
      unseeded-row coverage, this closes the loop end-to-end: assert that
      a *deliberately-failing* composition's exception message contains
      exactly the `int` value from `row.Seed` — not merely `"Seed:"` —
      for both an auto-generated seed and an explicit non-negative one,
      proving the pasteable-seed promise rather than just its presence.
- [ ] An API-surface/approval test (`Compono.Xunit.Tests`, e.g. `Verify`
      over `typeof(ComposeAttribute).Assembly`'s public types) locking
      the public shape of `Compono.Xunit` — `ComposeAttribute`,
      `ComposeAttribute<TProfile>`, `SharedAttribute` and nothing else —
      cheap insurance against accidental API drift, matching this
      milestone's own "keep public APIs minimal" constraint.
- [ ] `test/Compono.Xunit.SampleTests`: real xUnit v3 project with
      representative theories (inline-only, composed-only, mixed,
      `[Shared]`-before-SUT, deliberately-failing composition, `async
      Task` theory). References `Compono.Xunit` via `PackageReference`
      against a **local feed populated by `dotnet pack`**, not a
      `ProjectReference` — the point of this project is to consume
      `Compono.Xunit` exactly the way an external consumer would,
      catching packaging mistakes (missing dependency, wrong
      `PrivateAssets`, the generator analyzer not flowing transitively)
      that a `ProjectReference` build can't surface.
- [ ] **The sample project must include one theory whose parameter type
      is discovered *only* from a `[Compose]`-attributed method** — no
      `[Composable]`, no `Create<T>()`/`CreateMany<T>()`, no direct
      `CompositionRow` call site anywhere else in the sample — proving
      Phase 1's new discovery component (ADR-0022 Amendment 2026-07-30,
      fix #2) actually generates a plan through the real packaged
      pipeline, not just in an isolated `Compono.Generators.Tests`
      snapshot test.
- [ ] A `Compono.Xunit.Tests` test that runs the sample project through a
      real xUnit v3 runner (`dotnet test` or the in-process console
      runner) and asserts on its result — the milestone's required
      "proves behavior through the real xUnit v3 discovery and execution
      pipeline" coverage.

### Phase 4: Docs and cleanup

**Status:** Not Started

- [ ] `docs/mvp.md` Milestone 4 section: link ADR-0021/ADR-0022/PLAN-0004,
      mark exit criteria met.
- [ ] `docs/architecture.md`: correct the stage-2 pipeline table entry
      (read gate removed, write gate unchanged) and the `CompositionScope`/Recursion
      Detection sections' now-outdated "only a request the caller marked
      IsShared reads from scope" framing; add `CompositionRow`/`TestParameter`
      to Composition Requests and Package Boundaries (`Compono.Xunit`).
- [ ] `docs/public-api.md`: replace the `[InlineComposeData(...)]` sketch
      with the unified `[Compose(...)]` shape; resolve the "Questions
      still to resolve" under Shared Values per ADR-0022; fill in the
      xUnit v3 Experience section's settled attribute names.
      `[Compose(Seed = ...)]` example already matches ADR-0022; no change
      needed there.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` index rows (already
      added alongside the ADRs/this plan).

## Critical Files

- `src/Compono/CompositionRequestKind.cs`, `PathSegment.cs`,
  `RandomSource.cs`, `CompositionPath.cs`, `CompositionContext.cs`,
  `Composer.cs` — Phase 0.
- `src/Compono/CompositionRow.cs` (new) — Phase 0.
- `src/Compono.Xunit/` (new project) — `ComposeAttribute.cs`,
  `ComposeAttribute{TProfile}.cs`, `SharedAttribute.cs`, and the binding
  algorithm's implementation file(s) — Phases 1–2.
- `test/Compono.Xunit.Tests/` (new project) — Phase 3.
- `test/Compono.Xunit.SampleTests/` (new project) — Phase 3.
- `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md` — Phase 4.

## Test Plan

Matches `testing.md`'s existing conventions (xUnit v3 on MTP v2,
Arrange-Act-Assert, fixed-seed determinism assertions, one test project
per src project) plus this milestone's own required real-runner proof —
see ADR-0022's Testing Strategy section for the full breakdown, and this
plan's Phase 3 for the concrete task list. Per `testing.md`'s "verifying a
new public entry point" rule, `Composer.CreateRow`/`CompositionRow` need
their own isolated-type coverage in `Compono.Tests` (Phase 0), not only
exercised indirectly through `Compono.Xunit.Tests`.

## Notes

**Phase 0 (Done):**

- `Resolve<TValue>(in CompositionRequestDescriptor)` was refactored to
  share a new private `BuildSegment` helper with the new internal
  `ResolveDescriptorAsShared<TValue>` — not called out explicitly in the
  plan's task wording, but the natural way to avoid duplicating the
  six-kind `PathSegment` switch across the ordinary and shared
  descriptor-based entry points.
- `ShareExplicit<TValue>`'s "runtime type not assignable" validation
  branch (shared with `ResolveShared`'s pipeline-produced-value check) is
  compile-time unreachable through `CompositionRow`'s own strongly-typed
  public API — `value` is statically typed as `TValue`, so the compiler
  already guarantees assignability. The shared validation helper still
  exists (and is still exercised via the pipeline-facing callers,
  `ComposerRegistrationTests` etc.), but `CompositionRowTests` only covers
  the null-value branch for `ShareExplicit` specifically, with a comment
  explaining why the type-mismatch branch isn't independently retested
  there.
- One pre-existing test, `CompositionScopeTests.Resolve_NeverReadsFromScope_EvenWhenTheSameTypeWasAlreadyShared`,
  encoded the exact Milestone 2 behavior ADR-0021 deliberately reverses.
  Renamed to `Resolve_ReadsFromScope_WhenTheSameTypeWasAlreadyShared` and
  its assertions flipped (with an explanatory comment) rather than left
  failing or deleted — this is the one behavior change Phase 0 makes to
  already-shipped Milestone 2/3 code, and its test needed to move with it.
- Full suite green: `Compono.Tests` 388/388 (194 × 2 TFMs), `Compono.Generators.Tests`
  160/160 (154 unaffected + 3 `CreateInvocationDiscovery`-extension
  snapshot tests × 2 TFMs, one of which asserts *no* plan is generated
  for the excluded descriptor-less overload) — Phase 0 originally touched
  no generator code, but later PR #22 review rounds added the
  `CompositionRow` discovery fix below, which does.
- **`CreateInvocationDiscovery` extended for `CompositionRow` call sites,
  then corrected** (PR #22 review, second and third rounds; ADR-0022
  Amendments 2026-07-30 and 2026-07-31) — see the Phase 0 task list above
  for what changed and how it was verified (isolated generator snapshot
  tests plus a real `dotnet pack` + local-feed + throwaway-consumer
  manual check). The second round's fix over-matched (it also discovered
  the descriptor-less `Resolve<T>()` overload, which always throws for a
  `CompositionRow`-holding caller); the third round caught and corrected
  that. This is Phase 0's one piece of generator-touching work; everything
  else in this phase is `Compono` core only.

**Phase 1 (Done):**

- `ComposeAttribute`/`ComposeAttribute<TProfile>`'s `GetData` is a real
  stub, not a placeholder that skips caching: every call builds/reuses
  this attribute instance's cached `Lazy<Composer>` and `BindingPlan`
  (via `LazyInitializer.EnsureInitialized`, since the binding plan needs
  the reflected `testMethod` a plain `Lazy<T>` can't take as a runtime
  argument), then throws `NotImplementedException` - Phase 2 replaces
  that throw with the real inline/composed binding algorithm. This is
  what lets Phase 1's own caching be tested end-to-end through `GetData`
  itself, not just through the internal seams that back it.
- Profile application is a virtual method (`ComposeAttribute.ApplyProfile(CompositionBuilder)`,
  overridden by `ComposeAttribute<TProfile>` to call
  `builder.AddProfile<TProfile>()`) rather than a `Type`-plus-`Activator.CreateInstance`
  indirection - `ComposeAttribute<TProfile>` already has `TProfile` as a
  compile-time generic parameter, so there's no reason to erase it to a
  runtime `Type` and reconstruct an instance through reflection.
- **The `[Compose]`-attributed-method generator discovery component
  (`ComposeMethodDiscovery`, closing the Open Item Phase 0 left tracked)
  excludes a `ref`/`out`/`in`/`params` parameter individually, not the
  whole method** - only a generic method is excluded entirely (its
  parameter types can close over the method's own type parameter, the
  same open-generic shape `ComposedTypeAnalyzer` already rejects for
  every other discovery path). A method's other, ordinary parameters
  still get discovered even if one parameter is unsupported, since
  that's a distinct per-parameter runtime rejection
  `Compono.Xunit`'s own binding algorithm (Phase 2) makes independently.
  Verified with two isolated `Compono.Generators.Tests` snapshot tests -
  a type reached only via a `[Compose]`-attributed method's own
  parameter gets a plan; a `[Compose]`-attributed *generic* method's
  parameter gets none - using a same-metadata-name stand-in
  `Compono.Xunit.ComposeAttribute` declared directly in the test source
  (this generator test project doesn't reference the real
  `Compono.Xunit` assembly, and `ForAttributeWithMetadataName` matches
  by fully qualified name alone). The full packaged-consumer proof
  (`dotnet pack` + local feed + a real `Compono.Xunit.SampleTests`
  theory reached only this way) is Phase 3's own requirement, per the
  Amendment below - PR #23 review asked for this ahead of Phase 3
  specifically for the generic-metadata-name discovery path added in the
  fix below, so it was done as an ad hoc manual check (not the permanent
  `SampleTests` project, which is still Phase 3's job): `dotnet pack`'d
  `Compono` and `Compono.Xunit` into a local feed (after clearing the
  local package cache for both IDs) and referenced `Compono.Xunit` from a
  genuinely separate throwaway console project via `PackageReference`
  alone - no `ProjectReference`, no shared `InternalsVisibleTo`. A
  `Statement` type reachable only through a `[Compose<TestProfile>]`-
  attributed method's own parameter (no `Create<Statement>()` call site,
  no `[Composable]`) got a real generated plan:
  `PlanCache<Statement>.Instance` was non-null at runtime, proving the
  generic-metadata-name discovery path (`ComposeMethodDiscovery
  .GenericAttributeMetadataName`) reaches a real consumer transitively
  through the packaged `Compono.Xunit` → `Compono` dependency, not just
  the generator test project's same-metadata-name stand-in attributes.
- **`[Compose(null)]` fix (PR #23 review)**: a single `null` argument to
  `params object?[] inlineValues` binds in the C# compiler's non-expanded
  form - the whole array arrives `null`, not a one-element array
  containing `null` - so `[Compose(null)]` previously threw from the
  constructor's `ArgumentNullException.ThrowIfNull` instead of supplying
  `null` to the test method's first parameter, contradicting this same
  constructor's own documented "an explicit null entry is a supplied
  value" contract. Fixed by treating a `null` `inlineValues` array as a
  one-element array containing that `null` (`inlineValues ?? [null]`) -
  the only way a `null` array can arise from this constructor's own
  call sites, since a zero-argument `[Compose()]` already produces an
  empty array, never `null`.
- **Enforce a single Compose-family attribute per method (PR #23 review;
  ADR-0022 Amendment)**: `AllowMultiple = false` is checked by the
  compiler per exact attribute type, not across `ComposeAttribute`'s own
  base/derived family, so `[Compose]` plus `[Compose<TProfile>]` (or two
  differently-closed `[Compose<TProfile>]` forms) compiled without error
  even though only one Compose-family attribute per method is the
  documented contract. `BindingPlan.Build`'s `ValidateSignature` now
  rejects this explicitly via
  `testMethod.GetCustomAttributes<ComposeAttribute>().Count() > 1`
  (matches subtypes too), reported through the same `SignatureError`
  Phase 2 throws - the same "computed once, cached, thrown by Phase 2"
  shape as every other signature check here, not a new mechanism.
- Full suite green (as of PR #23's review-response commits): `Compono.Tests`
  388/388 (unchanged - Phase 1 touched no core code), `Compono.Generators.Tests`
  166/166 (160 unaffected + 2 `ComposeMethodDiscovery` snapshot tests + 1
  generic-metadata-name snapshot test, × 2 TFMs), `Compono.Xunit.Tests`
  46/46 (23 × 2 TFMs) covering
  `ComposeAttribute`/`ComposeAttribute<TProfile>` caching (`Composer`
  reuse, profile application, seed round-tripping, binding-plan and
  invoker-delegate identity across repeated `GetData` calls),
  `BindingPlan.Build`'s signature validation (multiple Compose-family
  attributes, generic method,
  `ref`/`out`/`in`, `params`, duplicate `[Shared]` types) and metadata
  capture (`[Shared]` flag, nullability, descriptor ordinal/name/declaring
  type), and the cached invoker delegates' actual runtime correctness
  (`Resolve`/`ResolveShared`/`ShareExplicit` for both a reference- and a
  value-typed parameter) against a real `CompositionRow`.

## Open Items

- Profile-supplied seed vs. `SeedAsNullable`'s negative-seed check (PR #23
  review) - tracked as a Phase 2 checklist item above, not resolved here:
  a `TProfile.Configure` that calls `builder.WithSeed(...)` reaches the
  cached `Composer`'s configuration independently of `ComposeAttribute
  .SeedAsNullable`, which Phase 2's planned negative-seed guard reads
  exclusively. Phase 2's implementation must decide how the two interact
  before that guard can be considered complete.
- **`ComposeMethodDiscovery` reports CMP0003 for an interface/abstract/
  delegate-typed `[Compose]`-attributed parameter unconditionally, even
  when the author intends it to be satisfied entirely by
  `TProfile.Configure`'s own `Register<T>(...)` or an always-supplied
  inline value (PR #23 review) — genuinely blocking: the project fails
  to compile for that otherwise-valid usage.** Deliberately *not* fixed
  as a drive-by change here, because it isn't a gap unique to
  `Compono.Xunit` — `ComposeMethodDiscovery.TransformMethod` reaches
  every eligible parameter through the exact same
  `ComposedTypeAnalyzer.Analyze` root-request path
  `Composer.Create<T>()` and `[Composable]` already use, and that
  path's "an abstract/delegate root always reaches constructor selection
  and gets CMP0003, even when a registration might satisfy it at
  runtime" behavior is deliberate, cross-cutting, and specifically
  regression-tested (`LeafTypeClassifier.IsRuntimeProviderResolved` is
  documented as *narrower than* `IsProviderResolved` for exactly this
  reason; `CompositionPlanVerifyTests.AbstractRootType_
  StillReportsDiagnostic_AfterRootProviderCheck` exists specifically to
  keep a registered-but-abstract `Create<T>()` root reaching CMP0003
  rather than silently compiling into a call that can only fail at
  runtime with a useless message - the PR #11 regression this guards
  against). Loosening that check for `[Compose]`-attributed parameters
  alone - e.g. reusing `LeafTypeClassifier.IsProviderResolved`'s lenient,
  member-style leaf treatment for a parameter root instead of the
  stricter `IsRuntimeProviderResolved` - would resolve this specific
  complaint, but it's a change to a foundational, deliberately-tested
  diagnostic philosophy spanning every discovery path, not a one-line
  fix scoped to this package; it needs its own design dive
  (`design-decisions.md`) weighing the same false-positive-vs-silent-
  failure tradeoff `Create<T>()`'s root already settled, not a decision
  made inline while triaging PR feedback. Tracked here for that dive
  before Phase 2 (which needs to decide inline/registered-parameter
  semantics anyway) or a dedicated follow-up.

The one item Phase 0 left open (no generator discovery path for a
`[Compose]`-attributed method's own parameter) was closed by Phase 1's
`ComposeMethodDiscovery` component above.
