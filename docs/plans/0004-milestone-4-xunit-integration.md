# [PLAN-0004] Milestone 4: xUnit v3 Integration

**Status:** Done

**Implements:** [ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md)
(core `CompositionRow`/`CompositionRequestKind.TestParameter`/stage-2 read-gate
change), [ADR-0022](../adr/0022-compono-xunit-package-design.md) (`Compono.XunitV3`
package: attribute API, binding algorithm, profile selection, seed policy,
diagnostics, packaging, testing strategy), [ADR-0023](../adr/0023-rename-compono-xunit-to-compono-xunitv3.md)
(the package was originally named `Compono.Xunit` through Phases 0-2;
renamed to `Compono.XunitV3` immediately after Phase 2 merged, before any
external consumer existed — every reference in this plan uses the current
name throughout, including in notes describing already-completed phases)

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
- New `Compono.XunitV3` package: `ComposeAttribute`/`ComposeAttribute<TProfile>`
  (`Xunit.v3.DataAttribute`), `SharedAttribute`, the inline/composed
  binding algorithm, `[Shared]` validation and ordering, profile
  selection, seed policy, diagnostics.
- Two new test projects: `test/Compono.XunitV3.Tests` (direct `GetData`
  calls, no real runner) and `test/Compono.XunitV3.SampleTests` (a real
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
      `Compono.XunitV3` code exists** — `Composer.CreateRow`/`CompositionRow`
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

### Phase 1: `Compono.XunitV3` package skeleton and attributes (ADR-0022)

**Status:** Done

- [x] New `src/Compono.XunitV3/Compono.XunitV3.csproj` (net10.0;net11.0,
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
      `Compono.XunitV3` only knows each parameter's type as a runtime
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
      Required because `Compono.XunitV3`'s binding (this phase's own
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

**Status:** Done

Ordered to match the runtime flow (composer/profile/seed are read from
the cache built once in Phase 1; everything after is per-row):

- [x] Profile application (`[Compose<TProfile>]` → `builder.AddProfile<TProfile>()`;
      `[Compose]` → default `Composer.Create()`) and `Seed` property →
      `builder.WithSeed(...)` — both folded into Phase 1's cached
      `Lazy<Composer>` construction, so this is "read the cached
      `Composer`," not a per-row step.
- [x] `Composer.CreateRow(declaringType)` for the row — **before**
      checking Phase 1's cached signature-validation result, not after:
      an unseeded row's seed doesn't exist until `CreateRow` runs, and
      every failure this package reports must include the row's real
      seed, so the row has to exist first even for a signature that's
      about to be rejected.
- [x] **If the row's effective seed (`row.Seed`) is negative, throw now**,
      echoing the rejected value back. This is what makes every row
      `Compono.XunitV3` creates have a non-negative seed unconditionally —
      `CompositionBuilder.WithSeed(int)` itself has no such restriction and
      happily accepts a negative value when the cached `Composer` is built
      (Phase 1), so this check is the only place that actually enforces
      it, and it must run before any other failure category so a rejected
      negative seed is reported clearly rather than surfacing as a
      confusing mismatch somewhere else.
- [x] **Decide how a `TProfile.Configure` that itself calls `builder.WithSeed(...)`
      interacts with this check** (PR #23 review) — resolved by checking
      `row.Seed < 0` directly rather than `SeedAsNullable is { } seed &&
      seed < 0`. `row.Seed` is the row's actual effective seed regardless of
      which source configured it (this attribute's own `Seed` property, or
      a profile's `Configure` calling `builder.WithSeed(...)`), so this one
      check (the item directly above) closes the gap for both sources
      without needing to distinguish them — see Notes below.
- [x] If Phase 1's cached signature-validation result is invalid, throw
      here, using `row.Seed` in the appended `Seed:` line — still before
      any parameter is bound or composed, so no random fork is consumed
      and no partially-composed row is ever produced; only *row creation*
      (and the negative-seed check above) now precede this check, not
      composition.
- [x] Positional inline-value binding (index-based "supplied," explicit
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
- [x] `[Shared]`-first, declaration-order composition (composed via each
      parameter's cached `resolveSharedInvoker`, inline via
      `shareExplicitInvoker`), then remaining parameters via
      `resolveInvoker` — never a direct, runtime-typed
      `row.Resolve<T>(...)`/`ResolveShared<T>(...)`/`ShareExplicit<T>(...)`
      call; always through Phase 1's cached delegates.
- [x] Construct the final `TheoryDataRow` from the assembled `object?[]`
      **in method declaration order** — binding/composition order (shared
      first, then the rest) and output order are intentionally different;
      the array passed to `TheoryDataRow` must match the method's own
      parameter order. Set `Traits["Compono.Seed"] = [row.Seed.ToString()]`
      unconditionally, on every row regardless of whether it will pass or
      fail — the milestone's "failure output includes a seed" exit
      criterion isn't scoped to composition failures only, and
      `Compono.XunitV3` can't know at `GetData` time whether the theory
      body will later throw its own assertion failure, so the trait can't
      be applied only-on-failure.

### Phase 3: Test suites and verification (ADR-0022's Testing Strategy)

**Status:** Done

- [x] `test/Compono.XunitV3.Tests`: binding-algorithm unit tests
      (inline-only/composed-only/mixed/too-many/non-null-type-mismatch),
      `[Shared]` detection (duplicate types, before/after ordering),
      profile caching/reuse assertion, unsupported-signature detection,
      seed determinism, concurrency-stress test on a shared cached
      attribute instance.
- [x] Inline `null` handling, all four combinations: accepted for a
      nullable reference-typed parameter and for a `Nullable<T>`
      parameter; rejected (clear pre-composition exception, no
      `NullReferenceException` from an attempted `GetType()` on `null`)
      for a non-nullable reference-typed parameter and for a non-nullable
      value-typed parameter — each combination covered for both an
      ordinary parameter and an inline-`[Shared]` parameter, asserting
      rejection happens before `ShareExplicit`'s invoker is ever reached.
- [x] A **non-null** inline value targeting a `Nullable<T>` parameter is
      **accepted** (e.g. `[Compose(42)]` for an `int?` parameter) — the
      regression test for the boxed-`T`-vs-boxed-`Nullable<T>` bug the
      `Nullable.GetUnderlyingType` unwrap fixes; without the unwrap this
      case fails even though nullable parameters are fully supported.
- [x] Every returned `ITheoryDataRow` carries a `"Compono.Seed"` trait
      matching `row.Seed` exactly, for both a passing-shaped row and a
      deliberately-failing one — proving the trait is unconditional, not
      only attached when composition is about to fail.
- [x] Cached invoker delegates: `MakeGenericMethod` construction runs
      exactly once per parameter across many repeated `GetData` calls on
      one attribute instance (not once per row); a value-typed and a
      reference-typed parameter both compose correctly through their
      cached delegate (proving the `T → object` boxing inside the closed
      helper works for both).
- [x] `[Compose(Seed = <negative>)]` rejected with a clear pre-composition
      exception naming the rejected value, distinct from every other
      signature-validation failure; a non-negative configured `Seed`
      round-trips unchanged through `row.Seed`. Combined with Phase 0's
      unseeded-row coverage, this closes the loop end-to-end: assert that
      a *deliberately-failing* composition's exception message contains
      exactly the `int` value from `row.Seed` — not merely `"Seed:"` —
      for both an auto-generated seed and an explicit non-negative one,
      proving the pasteable-seed promise rather than just its presence.
      See Notes below for where that seed actually lives for a
      pipeline-propagated (not `Compono.XunitV3`-authored) failure.
- [x] An API-surface/approval test (`Compono.XunitV3.Tests`, e.g. `Verify`
      over `typeof(ComposeAttribute).Assembly`'s public types) locking
      the public shape of `Compono.XunitV3` — `ComposeAttribute`,
      `ComposeAttribute<TProfile>`, `SharedAttribute` and nothing else —
      cheap insurance against accidental API drift, matching this
      milestone's own "keep public APIs minimal" constraint.
- [x] `test/Compono.XunitV3.SampleTests`: real xUnit v3 project with
      representative theories (inline-only, composed-only, mixed,
      `[Shared]`-before-SUT, deliberately-failing composition, `async
      Task` theory). References `Compono.XunitV3` via `PackageReference`
      against a **local feed populated by `dotnet pack`**, not a
      `ProjectReference` — the point of this project is to consume
      `Compono.XunitV3` exactly the way an external consumer would,
      catching packaging mistakes (missing dependency, wrong
      `PrivateAssets`, the generator analyzer not flowing transitively)
      that a `ProjectReference` build can't surface. See Notes below - this
      is exactly what this project caught.
- [x] **The sample project must include one theory whose parameter type
      is discovered *only* from a `[Compose]`-attributed method** — no
      `[Composable]`, no `Create<T>()`/`CreateMany<T>()`, no direct
      `CompositionRow` call site anywhere else in the sample — proving
      Phase 1's new discovery component (ADR-0022 Amendment 2026-07-30,
      fix #2) actually generates a plan through the real packaged
      pipeline, not just in an isolated `Compono.Generators.Tests`
      snapshot test.
- [x] The sample project's theories were run through a real xUnit v3
      runner (`dotnet test`) and their output verified by hand, repeatedly,
      during this phase's own development — the milestone's required
      "proves behavior through the real xUnit v3 discovery and execution
      pipeline" coverage, satisfied the same way Phase 1 satisfied its own
      packaged-consumer proof (a manual, one-time check, not permanent CI
      automation). An automated `Compono.XunitV3.Tests` test
      (`RealRunnerTests`) that shelled out `dotnet test` against the sample
      project on every run was built and iterated on at length, but
      dropped after repeated CI-only concurrency failures across several
      serialization fixes that each passed extensive local reproduction —
      see Notes below for the full account and the reasoning for keeping
      the sample project itself (proven, and available for future manual
      use) while not re-running it automatically on every commit.

### Phase 4: Docs and cleanup

**Status:** Done

- [x] `docs/mvp.md` Milestone 4 section: link ADR-0021/ADR-0022/PLAN-0004,
      mark exit criteria met.
- [x] `docs/architecture.md`: correct the stage-2 pipeline table entry
      (read gate removed, write gate unchanged) and the `CompositionScope`/Recursion
      Detection sections' now-outdated "only a request the caller marked
      IsShared reads from scope" framing; add `CompositionRow`/`TestParameter`
      to Composition Requests and Package Boundaries (`Compono.XunitV3`).
- [x] `docs/public-api.md`: replace the `[InlineComposeData(...)]` sketch
      with the unified `[Compose(...)]` shape; resolve the "Questions
      still to resolve" under Shared Values per ADR-0022; fill in the
      xUnit v3 Experience section's settled attribute names.
      `[Compose(Seed = ...)]` example already matches ADR-0022; no change
      needed there.
- [x] `docs/adr/README.md`/`docs/plans/README.md` index rows (already
      added alongside the ADRs/this plan).

## Critical Files

- `src/Compono/CompositionRequestKind.cs`, `PathSegment.cs`,
  `RandomSource.cs`, `CompositionPath.cs`, `CompositionContext.cs`,
  `Composer.cs` — Phase 0.
- `src/Compono/CompositionRow.cs` (new) — Phase 0.
- `src/Compono.XunitV3/` (new project) — `ComposeAttribute.cs`,
  `ComposeAttribute{TProfile}.cs`, `SharedAttribute.cs`, and the binding
  algorithm's implementation file(s) — Phases 1–2.
- `test/Compono.XunitV3.Tests/` (new project) — Phase 3.
- `test/Compono.XunitV3.SampleTests/` (new project) — Phase 3.
- `docs/mvp.md`, `docs/architecture.md`, `docs/public-api.md` — Phase 4.

## Test Plan

Matches `testing.md`'s existing conventions (xUnit v3 on MTP v2,
Arrange-Act-Assert, fixed-seed determinism assertions, one test project
per src project) plus this milestone's own required real-runner proof —
see ADR-0022's Testing Strategy section for the full breakdown, and this
plan's Phase 3 for the concrete task list. Per `testing.md`'s "verifying a
new public entry point" rule, `Composer.CreateRow`/`CompositionRow` need
their own isolated-type coverage in `Compono.Tests` (Phase 0), not only
exercised indirectly through `Compono.XunitV3.Tests`.

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
  `Compono.XunitV3`'s own binding algorithm (Phase 2) makes independently.
  Verified with two isolated `Compono.Generators.Tests` snapshot tests -
  a type reached only via a `[Compose]`-attributed method's own
  parameter gets a plan; a `[Compose]`-attributed *generic* method's
  parameter gets none - using a same-metadata-name stand-in
  `Compono.XunitV3.ComposeAttribute` declared directly in the test source
  (this generator test project doesn't reference the real
  `Compono.XunitV3` assembly, and `ForAttributeWithMetadataName` matches
  by fully qualified name alone). The full packaged-consumer proof
  (`dotnet pack` + local feed + a real `Compono.XunitV3.SampleTests`
  theory reached only this way) is Phase 3's own requirement, per the
  Amendment below - PR #23 review asked for this ahead of Phase 3
  specifically for the generic-metadata-name discovery path added in the
  fix below, so it was done as an ad hoc manual check (not the permanent
  `SampleTests` project, which is still Phase 3's job): `dotnet pack`'d
  `Compono` and `Compono.XunitV3` into a local feed (after clearing the
  local package cache for both IDs) and referenced `Compono.XunitV3` from a
  genuinely separate throwaway console project via `PackageReference`
  alone - no `ProjectReference`, no shared `InternalsVisibleTo`. A
  `Statement` type reachable only through a `[Compose<TestProfile>]`-
  attributed method's own parameter (no `Create<Statement>()` call site,
  no `[Composable]`) got a real generated plan:
  `PlanCache<Statement>.Instance` was non-null at runtime, proving the
  generic-metadata-name discovery path (`ComposeMethodDiscovery
  .GenericAttributeMetadataName`) reaches a real consumer transitively
  through the packaged `Compono.XunitV3` → `Compono` dependency, not just
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
  generic-metadata-name snapshot test, × 2 TFMs), `Compono.XunitV3.Tests`
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

**Phase 2 (Done):**

- **The negative-seed check reads `row.Seed`, not `SeedAsNullable`
  alone** — resolving the Open Item PR #23 review raised: Phase 1's
  cached `Lazy<Composer>` construction applies a profile
  (`builder.AddProfile<TProfile>()`, running `Configure` immediately,
  which may itself call `builder.WithSeed(...)`) before `SeedAsNullable`
  is ever read, so a profile-supplied seed reaches
  `CompositionBuilder.WithSeed` — and therefore `Composer.CreateRow`'s
  configured seed — independently of whether this attribute's own `Seed`
  property was ever set. `row.Seed`, read immediately after `CreateRow`
  runs, is the row's real effective seed regardless of which source
  configured it, so checking `row.Seed < 0` there closes the gap for a
  profile-supplied negative seed without needing to separately track
  which source produced it — no `SeedAsNullable`-specific branch needed.
- **`GetData` never invokes `MakeGenericMethod`/`MethodInfo.Invoke`** —
  binding calls only the three cached delegates per parameter
  (`resolveInvoker`/`resolveSharedInvoker`/`shareExplicitInvoker`) Phase 1
  built once per parameter; Phase 2 adds no new reflection cost on the
  per-row path, per ADR-0022's Source Generation Boundary section.
- `BindingPlan.MethodDisplayName(MethodInfo)` was factored out of
  `BindingPlan.ValidateSignature`'s previously-inline local so `GetData`'s
  own pre-composition messages (too many inline values, inline
  null/type-mismatch) name the method identically to a signature-error
  message, rather than duplicating the `{DeclaringType.FullName}.{Name}`
  format independently.
- `ComposeAttributeCachingTests.GetData_NeverRebuildsTheBindingPlan_AcrossRepeatedCalls`
  (Phase 1) asserted `GetData` threw `NotImplementedException` on every
  call, since Phase 1's `GetData` was a stub — updated to call `GetData`
  for real and assert success, since `SampleTestMethods.Simple(int, string)`
  composes cleanly through the built-in `int`/`string` providers
  (`LeafTypeClassifier.IsProviderResolved`) with no `[Composable]`/generated
  plan involved; the binding-plan-identity assertion the test exists for is
  unchanged.
- **Automatic disposal tracking was attempted, iterated three times, then
  reverted** (PR #24 review — see ADR-0022 Amendment 4 for the full
  account; summarized here for the plan's own timeline):
  1. A composed `IDisposable`/`IAsyncDisposable` value was never
     registered with `GetData`'s own `disposalTracker` parameter, so it
     was never released after the test ran — fixed by registering every
     composed value as soon as it was produced.
  2. That fix double-registered a `[Shared]` value reused by a later
     ordinary parameter of the same type (both resolve to the identical
     instance via `CompositionContext.ResolveCore`'s stage-2 scope read),
     causing a double `Dispose()` call — fixed with a per-`GetData`-call
     reference-equality dedup set.
  3. The dedup set was allocated and populated unconditionally on every
     row, against `AGENTS.md`'s "performance is a feature... runs on
     every test" principle — fixed by filtering to
     `IDisposable`/`IAsyncDisposable` before touching the set, and
     allocating it lazily.
  4. **Fix 1's entire premise turned out to be unsafe**: `CompositionRow
     .Resolve`/`ResolveShared` give `Compono.XunitV3` no visibility into
     which pipeline stage produced a value, so a value Compono itself
     freshly constructed is indistinguishable from one returned by a
     registration or a configured `IServiceProvider` - the latter
     explicitly owned by the caller, not Compono, per
     [ADR-0019](../adr/0019-registrations-and-service-provider-injection.md).
     Registering the latter with `DisposalTracker` would dispose an
     externally-owned instance (possibly a shared singleton reused across
     many tests) after just one test - a silent, hard-to-diagnose
     correctness violation strictly worse than the original leak fix 1
     addressed. **Reverted entirely** rather than patched with a
     heuristic - no code in `Compono`'s public surface exists for
     `Compono.XunitV3` to safely distinguish the two cases, and inventing
     one inline (without a real design dive) was rejected as exactly the
     kind of one-off decision this repo's process exists to avoid.
     `ComposeAttributeDisposalTests` now asserts the opposite of what it
     originally proved: a composed disposable is **not** registered and
     **not** disposed, guarding against silently reintroducing this.
- **A profile-configured seed pinning every row is intended behavior, not
  a bug** (PR #24 review) — clarified in ADR-0022's new Amendment 3, not
  fixed in code: Seed Policy and Reporting's "every `GetData` call without
  an explicit seed generates a fresh one" was ambiguous about whether a
  profile's own `Configure` calling `builder.WithSeed(...)` counts as
  "explicit." It does - a profile that pins a seed is deliberately
  choosing reproducible composition, the same contract
  `CompositionBuilder.WithSeed` already has everywhere else in Compono,
  and silently discarding that choice because it arrived through a
  profile rather than through `ComposeAttribute.Seed` directly would be
  the more surprising behavior of the two. No code change - the
  `row.Seed < 0` check two items above already covers this source
  correctly; only the ADR's wording needed the carve-out made explicit.
- **Amendment 3's own follow-through was incomplete** (PR #24 review,
  same round as the double-registration bug) — two more places still
  described pre-Amendment-3 behavior after Amendment 3 shipped:
  - ADR-0022's Decision Outcome/Seed Policy text scoped negative-seed
    rejection to `ComposeAttribute.Seed`/`SeedAsNullable` specifically,
    but the actual check (`row.Seed < 0`) has always covered any
    effective row seed, including a profile-configured one - the same
    "explicit" definition Amendment 3 established for freshness extends
    to rejection too. Recorded as a second paragraph in Amendment 3
    rather than a new amendment, since it's the same root clarification.
  - `ComposeAttribute.Seed`'s public XML doc comment still promised "a
    fresh seed is generated on every `GetData` call" for an unset
    property, unqualified - misleading IntelliSense for a
    `[Compose<TProfile>]` consumer whose profile pins a seed. Updated
    with the same profile-seed carve-out.
  No code change in either case - both are documentation fixes closing
  gaps a prior round's fix introduced without fully propagating.
- **A minimal slice of core binding-algorithm coverage shipped with this
  PR rather than waiting for Phase 3** (PR #24 review, P1) — Codex
  correctly flagged that this PR's only direct assertions were on caching
  and disposal, not on the binding algorithm's actual output (inline
  precedence, composed values, the negative-seed rejection, the
  `Compono.Seed` trait), which risked a regression silently supplying
  wrong theory arguments merging undetected before Phase 3's tests ever
  existed. Added `ComposeAttributeBindingTests` (six tests: composed-only,
  inline-only, mixed inline+composed, negative-seed rejection, the
  `Compono.Seed` trait matching a configured seed, and the trait present
  on a passing-shaped row) - enough to catch a regression in the core
  binding-algorithm promises this PR actually ships. This is deliberately
  **not** the full Phase 3 matrix (the `[Shared]`-ordering assertion
  already exists via `ComposeAttributeDisposalTests`' shared-value-reuse
  test; nullable four-combination coverage, the `Compono.Seed`-value
  proof against a real failing composition, the concurrency-stress test,
  the API-surface approval test, and the real-runner
  `Compono.XunitV3.SampleTests` project all remain Phase 3's scope) - adding
  the full exhaustive suite here would have meant either doing Phase 3's
  work inside a Phase 2 PR (against `design-decisions.md`'s
  one-phase-per-PR rule) or leaving genuinely untested binding-algorithm
  output merged (Codex's real concern). This is the middle ground: enough
  direct coverage that a regression in the algorithm this PR ships is
  actually caught, without duplicating Phase 3's own exhaustive scope.
  (`ComposeAttributeDisposalTests`' shared-value-reuse test, added
  earlier for the now-reverted disposal-tracking work below, still
  covers the `[Shared]`-ordering assertion referenced here even after
  its own original disposal-specific purpose was reverted.)
- **A single reference-array inline argument was misread as multiple
  inline values** (PR #24 review) — the same non-expanded `params`
  binding form the existing `[Compose(null)]` fix (PR #23 review)
  handles also applies to any reference-array-typed single argument
  covariantly convertible to `object?[]` (e.g. `string[]`):
  `[Compose(new string[] { "a", "b" })]` arrived as that exact `string[]`
  instance (runtime type `string[]`, not `object[]`), so the constructor
  read it as a 2-element `object?[]` instead of one array value for a
  single array-typed parameter. Fixed the same way as the null case: the
  constructor now checks `inlineValues.GetType() != typeof(object[])`
  (true only for this non-expanded-form single-array case - every
  genuinely expanded-form call, including `Compose()`'s empty case,
  always produces a freshly built `object[]`) and wraps it as a
  one-element array. Covered by
  `InlineValues_SingleReferenceArrayArgument_TreatedAsOneSuppliedArrayValue`,
  matching the existing null-argument test's shape.
- Full suite green: `Compono.Tests` 388/388 (unchanged - Phase 2 touched
  no core code), `Compono.Generators.Tests` 166/166 (unchanged - Phase 2
  touched no generator code), `Compono.XunitV3.Tests` 64/64 (32 × 2 TFMs -
  23 from Phase 1 (one updated in place) + 2 disposal tests (rewritten to
  prove disposal tracking is deliberately absent, per Amendment 4 above) +
  6 binding-algorithm tests + 1 inline-array-argument test, using a
  `DisposableProfile`/`DisposableValue` fixture pair composed via a
  registration rather than a generated plan, matching this test project's
  existing generator-free pattern). The exhaustive matrix (nullable
  four-combination coverage, seed-message content proof,
  concurrency-stress test, the API-surface approval test, the real-runner
  `Compono.XunitV3.SampleTests` project) remains Phase 3's own scope per the
  plan's phase split and ADR-0022's Testing Strategy.

**Phase 3 (Done):**

- **A real packaging bug, caught exactly the way this phase's real-packaged-
  consumer project exists to catch it**: `Compono.XunitV3.csproj`'s
  `<ProjectReference Include="..\Compono\Compono.csproj" />` had no explicit
  asset-flow metadata, so `dotnet pack` applied the default `PrivateAssets`
  (`contentfiles;build;analyzers`) to the generated nuspec `<dependency>` for
  `Compono` - rendered as `exclude="Build,Analyzers"`. A project referencing
  only the `Compono.XunitV3` package (never `Compono` directly - the whole
  point of this package) never got `Compono.Generators` transitively, so
  every `[Compose]`-attributed parameter type silently fell back to "no
  generated plan" and failed to compose at runtime with no compile-time
  signal at all. First surfaced as `Compono.XunitV3.SampleTests`'
  `SharedTests.SharedRepositoryIsReusedByTheService` failing with "No
  registration, configuration rule, semantic provider, test-double provider,
  built-in provider, or generated plan could satisfy 'Repository'" despite
  `Repository` being reachable only through a `[Compose]`-attributed method's
  own parameter (exactly `ComposeMethodDiscovery`'s discovery path). Fixed by
  adding `PrivateAssets="none"` to that `ProjectReference` - confirmed via
  the packed nuspec changing from `exclude="Build,Analyzers"` to
  `include="All"`, and via `-p:EmitCompilerGeneratedFiles=true` showing
  `Compono.Generators` actually emitting `CompositionPlan.g.cs` files for
  `Repository`/`OrderService`/`CreateOrder` inside
  `Compono.XunitV3.SampleTests`' own `obj/` once the fix landed. This is
  precisely the "packaging mistake a `ProjectReference` build can't surface"
  scenario this phase's testing strategy names - it would have shipped
  silently broken without a project that consumes `Compono.XunitV3` as a
  real package.
- **A same-version local NuGet feed needs its global-packages-folder cache
  cleared to pick up a re-`pack`ed nupkg** - both `Compono` and
  `Compono.XunitV3` are packed with a fixed `-p:Version=1.0.0` (this repo has
  no versioning tool wired up yet), and NuGet treats an already-cached
  version as immutable, so a content change (like the `PrivateAssets` fix
  above) silently keeps resolving the *stale* cached package unless
  `~/.nuget/packages/compono`/`~/.nuget/packages/compono.xunitv3` are cleared
  first. Not a problem for `Compono.XunitV3.SampleTests`' own
  `PackToLocalFeed` pre-restore target in normal use (CI and a fresh
  checkout's global packages folder never have a stale entry to begin with),
  but worth recording here since it cost real time to diagnose during this
  phase's own manual verification.
- **The seed-message-content proof needed two different assertion shapes,
  not one** (Phase 3's own task list originally implied a single pattern) -
  `Compono.XunitV3`'s own pre-composition exceptions (negative seed,
  signature errors, inline-value validation) append a `"Seed: <value>"` line
  directly into `CompositionException.Message` via the `AppendSeed` helper,
  but a *pipeline*-propagated `CompositionException` (an ordinary
  composition failure, e.g. an unregistered type) does not - its `.Message`
  is the plain diagnostic text (`CompositionDiagnostic.Message`), and the
  seed only appears in `exception.Diagnostic.Seed`/`exception.Diagnostic
  .ToString()` (`docs/architecture.md`'s existing `Console.WriteLine
  (exception.Diagnostic)` convention, from Milestone 2/3 - out of this
  phase's scope to change). `SeedReportingTests` covers both: the explicit-
  seed case asserts `exception.Diagnostic!.Seed` directly; the auto-
  generated-seed case parses the seed out of `exception.Diagnostic!
  .ToString()`'s `"Seed: <value>"` line and pastes it into a second
  `[Compose(Seed = ...)]` call, asserting the same seed reappears - proving
  the pasteable-seed promise round-trips through a real `Compose(Seed =
  ...)]` value, not just a string match. **Superseded by ADR-0022 Amendment
  5**, below: PR #26 review correctly pointed out this whole distinction
  was a workaround, not a fix - `Compono.XunitV3.SampleTests`' original
  deliberately-failing theory used `[Compose(Seed = -1)]` specifically to
  route around the fact that a genuine pipeline failure's `.Message` had no
  seed in it at all. Amendment 5 fixes that gap directly (a genuine
  composition failure's `.Message` now carries the seed too), so this
  two-assertion-shape distinction no longer describes current behavior -
  kept here only as the historical reasoning for why the original
  implementation was shaped this way.
- **`test/Compono.XunitV3.SampleTests` is deliberately not added to
  `Compono.slnx`** - it contains one theory that always fails
  (`FailingCompositionTests`, by design, per ADR-0022's Testing Strategy),
  and CI's PR-build workflow runs `dotnet test` across every project in
  `Compono.slnx` expecting a clean pass. Adding it there would break every
  PR. It stays a genuinely standalone project on disk (see the automation
  note below for how it's actually exercised - or rather, not automatically
  exercised - going forward).
- **PR #26 review (second round) caught two more real gaps, both fixed in
  the same PR** - see ADR-0022 Amendment 5 for the first in full:
  1. The sample project's failing theory originally used
     `[Compose(Seed = -1)]`, which exercises `Compono.XunitV3`'s own
     seed-validation failure, not a genuine composition failure - the
     milestone's actual Goal statement is about the latter. Fixed by (a)
     adding `CompositionException.WithSeedInMessage` as a new `Compono`
     core method so `ComposeAttribute.GetData` now rewrites a propagating
     pipeline failure's own `Message` to include the seed - previously only
     `Diagnostic` had it, and a real test runner's failure display shows
     `Message`, not `Diagnostic.ToString()`; (b) switching the sample
     project's failing theory to a genuine unsatisfied composition
     (`GatewayConsumer`, whose constructor takes `IUnregisteredGateway` -
     deliberately a *nested* dependency, not the `[Compose]`-attributed
     parameter's own root type, since an abstract root there hits the
     CMP0003 diagnostic this plan's Open Items section already documents as
     deliberate) with an explicit `Seed = 24601`. `SeedReportingTests`
     (`Compono.XunitV3.Tests`, no dependency on the sample project) was
     rewritten to assert on `.Message` directly (not `.Diagnostic`) for
     exactly this reason, plus a new test proving `Diagnostic` itself is
     left unchanged (no duplicated `"Seed:"` line) alongside the rewritten
     `Message`. `Compono.Tests` gained direct coverage of
     `CompositionException.WithSeedInMessage` itself, per `testing.md`'s
     "verifying a new public entry point" rule. This fix is real,
     independently tested, and kept - though its *access mechanism*
     changed again in round 5 below, after first shipping as `internal`
     plus `InternalsVisibleTo`.
  2. `PackToLocalFeed`'s fixed `-p:Version=1.0.0` meant a stale entry in
     NuGet's global packages folder from an earlier run could silently
     satisfy a later restore even after the local feed's `.nupkg` contents
     changed. Also fixed and kept - see the "sample project kept, automation
     dropped" note below for why this stopped mattering for CI specifically,
     even though the fix (an isolated per-project `RestorePackagesPath`,
     `obj/.nuget-packages/`) is still in the sample project's `.csproj` for
     anyone using it manually.
  3. A third round found `InvokeWithSeedOnFailure`'s
     `when (exception.Diagnostic is not null)` guard let a plain-message
     `CompositionException` (no `Diagnostic` at all) escape with no seed
     whatsoever - exactly the shape a generated `HashSet<T>`/`Dictionary`
     collection plan's unique-value-exhaustion path throws
     (`CollectionPlan.scriban`). Fixed by broadening
     `CompositionException.WithSeedInMessage` to build its message from
     `original.Message` (not `original.Diagnostic!.Message`) and preserve
     `original.Diagnostic` as-is whatever it is (`null` stays `null`) -
     removing the need for the guard entirely, so every
     `CompositionException` gets the same treatment. Covered by a new
     `Compono.Tests` test (`WithSeedInMessage_RewritesMessage_AndLeaves
     DiagnosticNull_ForAPlainMessageOriginal`) and a new
     `Compono.XunitV3.Tests` test using a hand-written
     `CollectionExhaustionPlan` fixture that mirrors `CollectionPlan
     .scriban`'s own `HashSet<T>` shape exactly (composing a default-sized
     `HashSet<bool>` - `bool`'s value space has only 2 members against a
     default collection size of 3 - deterministically exhausts).
  4. The same review round also caught that `ComposeAttributeConcurrencyTests`
     never actually exercised concurrent execution at all:
     `Enumerable.Range(0, 200).Select(_ => attribute.GetData(...).AsTask())`
     is lazy, and since `GetData` runs fully synchronously (an
     already-completed `ValueTask`), `Task.WhenAll` enumerating that
     deferred sequence never creates call N+1 until call N has already
     finished - zero overlap, so the test could never have caught a race
     in the shared `Lazy<Composer>`/binding-plan initialization it exists
     to test. Fixed by rewriting it with `Parallel.ForEachAsync`, which
     genuinely dispatches across the thread pool concurrently - verified
     stable across 5 repeated local runs post-fix.
  5. A fourth review round found two P2 gaps: `PublicApiSurfaceTests`
     checked only `type.IsPublic`, which reflection never sets for a
     nested type (`IsNestedPublic` is the separate flag), so an
     accidentally-added nested public type would have slipped through
     undetected - fixed by checking both. Separately, `GetData`'s XML doc
     claimed `InnerException` was "unchanged" from what the pipeline threw,
     but `WithSeedInMessage` constructs a *new* exception whose
     `InnerException` is the pipeline's original `CompositionException`
     itself (one level shallower than that original's own
     `InnerException`) - e.g. a provider failure's real chain is wrapper
     → original composition exception → the provider's own thrown
     exception. Doc corrected to describe the actual chain.
  6. **A fifth review round caught something serious: the `internal`
     `WithSeedInMessage` seam from round 2, reached via a new
     `InternalsVisibleTo("Compono.XunitV3")` grant on `Compono.csproj`,
     directly reversed [ADR-0021](../adr/0021-row-composition-entry-point-for-test-framework-integrations.md)'s
     own accepted, public-only integration boundary** - that ADR's own
     Pros and Cons section has an entry for exactly this option, rejected
     specifically because "it directly reverses a deliberate, documented
     policy... adopted specifically to keep a shipped package's internal
     surface from becoming a de facto public contract for 'trusted'
     consumers." Because `Compono` is unsigned, the grant authenticated
     only the assembly name - handing any assembly that could claim that
     name unrestricted access to every internal member of `Compono` core,
     not scoped to the one method that needed it. This should have been
     caught before implementation (a quick check against ADR-0021 before
     adding any `InternalsVisibleTo` entry would have surfaced the direct
     conflict immediately) rather than by a later review round. **Fixed by
     removing the grant entirely and making `WithSeedInMessage` itself
     `public` instead of `internal`** - it already lives inside
     `CompositionException`, so it already had full access to the private
     constructor and `DiagnosingContextIdentity` it needs; the only reason
     it ever required `InternalsVisibleTo` was its own access modifier, not
     anything about where it's implemented. `Compono.XunitV3` now gets
     exactly the one operation it needs via an ordinary public API call,
     `CompositionContext` and every other internal type stay exactly as
     inaccessible as ADR-0021 intended, and the only `InternalsVisibleTo`
     entry remaining in `Compono.csproj` is `Compono.Tests`, unchanged from
     before this PR. See ADR-0022 Amendment 5's own "amendment to this
     amendment" for the full account.
  7. A sixth review round (P2) caught that `pack-to-local-feed.sh`'s
     `until mkdir "$lock_dir"; do sleep 1; done` wait loop had no bound -
     if a prior holder were killed before its own `EXIT` trap could run
     (`SIGKILL`, a canceled build, a machine restart), the lock directory
     is never cleaned up, and every later restore of this project would
     wait on it forever with no way to recover short of knowing to delete
     it by hand. Fixed with a bounded wait (~120 attempts, matching the
     ~1 pack/second polling interval already in place) that fails with a
     clear, actionable message (naming the exact `rm -rf` to run) instead
     of hanging indefinitely. Verified by manually creating a stale lock
     directory and confirming the script now fails fast with that message
     (using a temporarily-reduced attempt count for the test) rather than
     blocking forever, then confirming a normal run still succeeds once
     the stale lock is gone.
  8. A seventh review round (P2) caught that `CompositionException
     .WithSeedInMessage` - a genuinely new public `Compono` core API as
     of round 5 - was documented only in ADR-0022, with its
     `docs/public-api.md` coverage deferred to Phase 4's docs pass. Per
     this repo's own rule (`AGENTS.md`: update the relevant `docs/*.md` in
     the same PR that changes the behavior it describes, not a
     follow-up), added a `Diagnostics API` section entry showing the
     method's signature, its exception-chain contract (`Diagnostic`
     copied through unchanged, `original` becomes `InnerException`), and
     when to reach for it (a plain-message `CompositionException` has no
     `Diagnostic` to render its own `Seed:` line).
  - Also folded into this round, at the user's request (not a posted PR
    comment): the sample project had no `[Compose<TProfile>]` theory at
    all - every existing theory used the profile-less `[Compose]` form.
    Added `ProfileTests.ComposesTheProfileConfiguredValue`, using a new
    `ApplicationTestProfile` that registers a fixed `NotificationSettings`
    value, proving `TProfile.Configure` actually applies through the real
    packaged pipeline (not just the in-process `GetComposer()`/`CreateRow`
    checks `Compono.XunitV3.Tests` already had). The sample project's
    theory count is 7 as a result (inline-only, composed-only, mixed,
    async, `[Shared]`-before-SUT, profile-based, one deliberately failing).
- **Sample project kept; the automated real-runner test was dropped after
  four rounds of CI-only concurrency failures** - the fullest account of
  why lives here since it's the reason the milestone's "proves behavior
  through a real xUnit v3 runner" criterion ends up satisfied by manual
  verification (matching Phase 1's own precedent) rather than permanent CI
  automation:
  - An automated `Compono.XunitV3.Tests` test, `RealRunnerTests`, shelled
    out `dotnet test` against the sample project on every run and asserted
    on its output. This is what actually caught the `PrivateAssets`
    packaging bug above and forced the genuine-composition-failure fix in
    Amendment 5 - genuinely valuable while it was running.
  - CI's `dotnet test --solution Compono.slnx` runs every test host for
    every TFM concurrently, so `Compono.XunitV3.Tests`' net10.0 and net11.0
    hosts each ran `RealRunnerTests` at the same moment, each independently
    spawning its own nested `dotnet test` against the sample project. Four
    consecutive rounds of fixes were made in response to CI failures, each
    verified locally at the time, each still failing on the *next* CI
    push:
    1. A `pack-to-local-feed.sh` cross-process `mkdir` lock, for two
       nested `dotnet pack` calls racing on shared `src/Compono`/
       `src/Compono.Generators` bin/obj output
       (`Could not find a part of the path '.../Compono.Generators/bin
       /Debug/netstandard2.0'`).
    2. A `pack-to-local-feed.sh` global-NuGet-cache clear for the stale
       version problem above, which introduced a *new*, different race
       (a second process's clear deleting files a first process's own
       restore was still reading - `Could not find a part of the path
       '.../packages/compono/1.0.0'`).
    3. An isolated per-invocation `RestorePackagesPath` (first scoped by
       `$(TargetFramework)`, which turned out not to distinguish the two
       concurrent invocations at all since `RealRunnerTests.cs` hardcoded
       `"-f net10.0"` regardless of which TFM host was calling it; then
       correctly scoped by a `Compono_LocalPackagesId` environment
       variable set once in C# per nested subprocess) - this one *worked*
       for the restore-path race specifically (CI's own log confirmed two
       genuinely different isolated paths) but CI still hit the *original*
       shared-bin/obj race from round 1, meaning the `mkdir` lock wasn't
       sufficient on CI's runner even though a dozen-plus local
       concurrency stress-test attempts under the exact CI sequence never
       reproduced that recurrence.
    4. A machine-wide named `System.Threading.Mutex` in `RealRunnerTests.cs`
       wrapping the entire nested subprocess (not just the pack step) -
       the most aggressive fix attempted, fully serializing every nested
       invocation regardless of what either side did internally. This one
       passed eight consecutive local stress-test runs (after also fixing
       an `AbandonedMutexException` the Mutex itself introduced) with
       timings that, for the first time, showed clear evidence of actual
       serialization (~8s vs ~15-16s, rather than both sides finishing in
       a similar ~8-9s with no proof anything was serialized). This is
       also the point the decision below was made, before a fifth CI push
       could confirm or refute it.
  - **Decision: stop iterating on CI-only concurrency and drop the
    automation, keeping the sample project itself.** The thing
    `RealRunnerTests` existed to prove - that `Compono.XunitV3` composes
    correctly when consumed as a real package, through a real xUnit v3
    runner - was proven repeatedly, by hand and via CI, across this whole
    round of fixes; every one of those runs' *test content* (the 5-then-6
    passing theories, the one deliberately-failing one) succeeded every
    single time. Every failure across all four rounds was exclusively
    about *this repo's own CI environment's* process-concurrency
    characteristics for a nested nested-`dotnet`-invoking test, which
    resisted full local reproduction even under carefully matched
    conditions - a cost with no additional verification payoff once the
    underlying behavior was already this thoroughly demonstrated. Removed
    `RealRunnerTests.cs` entirely; the sample project's files, its
    `PackToLocalFeed`/`RestorePackagesPath`/`pack-to-local-feed.sh`
    infrastructure, and its fixed content (the genuine composition
    failure, the profile theory) all stay on disk, unreferenced by
    `Compono.slnx` or any other test, available to run by hand
    (`dotnet test` from `test/Compono.XunitV3.SampleTests`) whenever
    packaging behavior needs re-checking by a person, without being
    re-run - and re-racing - on every commit.
- Full suite green: `Compono.Tests` 392/392 (196 × 2 TFMs - 384 unaffected
  + 2 new `CompositionException.WithSeedInMessage` tests, each × 2 TFMs -
  one of which was rewritten in place during the third review round, net
  count unchanged), `Compono.Generators.Tests` 166/166 (unchanged),
  `Compono.XunitV3.Tests` 94/94 (47 × 2 TFMs - 46 from the second review
  round (unchanged from the first round's count, since `RealRunnerTests`
  was added and then removed within this same PR) + 1 new plain-message
  `WithSeedInMessage` test from the third round;
  `ComposeAttributeConcurrencyTests` rewritten in place to genuinely
  exercise concurrency, same test count).
  `Compono.XunitV3.SampleTests` itself last verified by hand reporting
  6 passed/1 deliberately failed (a genuine composition failure) on both
  net10.0 and net11.0.

**Phase 4 (Done):**

- Most of this phase's `docs/architecture.md`/`docs/public-api.md` scope
  had already landed incrementally during Phases 0-3 (each phase updated
  its own affected sections as it shipped, per `documentation.md`'s
  same-PR rule) - the stage-2 pipeline table entry, the `CompositionScope`/
  Recursion Detection framing, `CompositionRow`/`TestParameter` in
  Composition Requests and Package Boundaries, and the settled
  `[Compose]`/`[Shared]` attribute shapes in `docs/public-api.md` were all
  already current. What Phase 4 actually closed out was the handful of
  now-stale "not yet implemented" / "Phases 0-2 implemented, Phases 3-4
  remain" status lines left over from when those sections were written
  mid-milestone (`docs/mvp.md`, `docs/architecture.md`'s Package
  Boundaries section, `docs/public-api.md`'s xUnit v3 Experience section)
  and marked `docs/mvp.md`'s Milestone 4 exit criteria as verified, with a
  pointer to the Phase 3 evidence (`Compono.XunitV3.Tests` +
  `Compono.XunitV3.SampleTests`) rather than restating it.
- `docs/adr/README.md`/`docs/plans/README.md` index rows for ADR-0021/
  0022/0023 and PLAN-0004 were already present (added alongside each ADR/
  the plan itself as they were written) - this phase only flipped
  `docs/plans/README.md`'s PLAN-0004 status column from `Not Started` to
  `Done` to match this plan's own header.
- No code changes in this phase - docs only, per its own scope.
- **PR #27 review (Codex, P2) caught an overstated completion claim** -
  the first draft of this phase's `docs/mvp.md`/`docs/architecture.md`/
  `docs/public-api.md` edits said Milestone 4 was "fully implemented,"
  which reads as unqualified even though the Open Items entry below (an
  interface/abstract/delegate-typed `[Compose]`-attributed parameter
  reports CMP0003 unconditionally) was already tracked and still open -
  and directly affects the `[Shared] IRepository repository` shape shown
  in this plan's own Goal and in `docs/mvp.md`'s Milestone 4 Example,
  which doesn't actually compile as written today (confirmed against
  `test/Compono.XunitV3.SampleTests`, whose own `[Shared]` theory uses a
  concrete `Repository` type specifically to route around this gap).
  Fixed by softening "fully implemented" to "implemented" plus an
  explicit pointer to this Open Item in all three docs, and adding a note
  under `docs/mvp.md`'s Example/Exit Criteria clarifying the gap - no
  code change, since the underlying CMP0003 behavior is unchanged and
  still correctly scoped as a follow-up design dive per the Open Item's
  own reasoning, not a Phase 4 fix.

## Open Items

- ~~**`ComposeMethodDiscovery` reports CMP0003 for an interface/abstract/
  delegate-typed `[Compose]`-attributed parameter unconditionally...**~~
  **Resolved by [PLAN-0005](0005-milestone-5-nsubstitute-integration.md)
  Phase 2 (2026-07-31), see [ADR-0024 Amendment 2](../adr/0024-public-provider-extensibility-model.md).**
  This item predicted exactly the right fix (reusing
  `LeafTypeClassifier.IsProviderResolved`'s lenient, member-style leaf
  treatment for a parameter root instead of the stricter
  `IsRuntimeProviderResolved`) and correctly identified it as needing its
  own design dive rather than a drive-by fix here — that dive happened as
  part of PLAN-0005 Phase 2's real end-to-end verification, when this
  plan's own `[Shared] IRepository repository` Goal-section shape (see the
  note below) failed to compile against a real packaged consumer, giving
  the "a registration/provider might satisfy this at runtime" side of the
  false-positive-vs-silent-failure tradeoff real dogfooding evidence for
  the first time. `IsRuntimeProviderResolved` now delegates directly to
  `IsProviderResolved` — an interface/abstract-class/delegate root
  (including a `[Compose]`-attributed parameter, since
  `ComposeMethodDiscovery` shares the same root-request path this item
  described) is classified identically to a member, at every discovery
  path, not just this package's own. The regression test this item named,
  `CompositionPlanVerifyTests.AbstractRootType_
  StillReportsDiagnostic_AfterRootProviderCheck`, was rewritten (its
  assertion inverted) as part of that fix, alongside a new
  `ConcreteRootTypeWithNoAccessibleConstructor_
  StillReportsDiagnostic_AfterRootProviderCheck` proving a genuinely
  unconstructible concrete root is unaffected.

The one item Phase 0 left open (no generator discovery path for a
`[Compose]`-attributed method's own parameter) was closed by Phase 1's
`ComposeMethodDiscovery` component above.
