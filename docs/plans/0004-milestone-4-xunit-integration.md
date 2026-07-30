# [PLAN-0004] Milestone 4: xUnit v3 Integration

**Status:** Not Started

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

**Status:** Not Started

- [ ] `CompositionRequestKind.TestParameter` new enum member.
- [ ] `PathSegment.TestParameter(int Ordinal, string Name)` new record;
      `CompositionPath.SegmentDisplayString()`/`NodeLabel()` gain a
      matching case (rendered like `ConstructorParameter`: `.{Name}` /
      `{typeName} {Name}`).
- [ ] `RandomSource` gains a seventh fork tag, `TestParameterTag = 6`
      (the next unused zero-based tag after the six already in use — `0`–`4`
      for the five original structured segments, `5` for
      `ManualResolveTag`), and a `Fork` switch arm; determinism test
      proving all **seven** segment kinds (not six —
      `ConstructorParameter`/`RequiredMember`/`CollectionElement`/
      `DictionaryKey`/`DictionaryValue`/`ManualResolve`/`TestParameter`)
      fork distinctly at ordinal 0 from the same parent state (extends
      ADR-0012 Amendment 2's existing six-kind coverage).
- [ ] `CompositionContext`: new constructor overload accepting `Type
      rootType`, pre-establishing `_path`/`_random` instead of leaving
      them for the first `Resolve` call to claim as root.
- [ ] `CompositionContext.ResolveCore`: remove the `if (request.IsShared)`
      gate around the stage-2 scope *read*; the write side
      (`StoreSharedAndReturn`, `ResolveViaGeneratedPlan`'s shared branch)
      is unchanged.
- [ ] New internal `CompositionContext` members backing
      `CompositionRow.ResolveShared`/`ShareExplicit`: `ResolveShared` is
      pipeline dispatch + scope-store; `ShareExplicit` skips dispatch
      (the value is already known) but runs the **exact same**
      authoritative validation `ResolveShared`'s successful pipeline
      result gets (`ValidateAuthoritativeValue` — null-for-non-nullable,
      runtime-type-not-assignable) before storing — implement both
      through one shared validate-then-store helper so the two paths
      can't drift apart, rather than duplicating the check.
- [ ] Public `CompositionRow : ICompositionContext` sealed class:
      `int Seed` (matching `WithSeed(int)`/`ComposeAttribute.Seed` — not
      the internal `CompositionSeed`'s `ulong`), `ResolveShared<TValue>(descriptor)`,
      `ShareExplicit<TValue>(descriptor, value)`, plus the inherited
      `Resolve<TValue>(descriptor)`/`Resolve<TValue>()`/`ResolveCollectionSize()`
      forwarded to the wrapped context.
- [ ] New `internal CompositionSeed.GenerateRowSeed()`, distinct from the
      existing `Generate()`: draws from `int`'s **non-negative** range
      (`Random.Shared.Next(0, int.MaxValue)`) rather than the full 64-bit
      range `Create<T>()`/`CreateMany<T>()` use, so an unseeded row's
      reported `int` seed is always the complete value (never truncated)
      *and* prints identically whether read via `CompositionRow.Seed`
      (`int`) or via `CompositionDiagnostic.Seed` (`ulong`, unaffected by
      this ADR) — see ADR-0021's Seed type consistency note for why
      non-negative specifically.
- [ ] `Composer.CreateRow(Type declaringType)`: `_configuration.Seed ??
      CompositionSeed.GenerateRowSeed()`, then constructs the
      pre-rooted `CompositionContext`, then `CompositionRow` with
      `unchecked((int)seed.Value)`.
- [ ] `Compono.Tests` coverage for this API in isolation, **before any
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

### Phase 1: `Compono.Xunit` package skeleton and attributes (ADR-0022)

**Status:** Not Started

- [ ] New `src/Compono.Xunit/Compono.Xunit.csproj` (net10.0;net11.0,
      `PackageReference` to `Compono` + `xunit.v3.extensibility.core`).
- [ ] `SharedAttribute` (parameter-targeted marker).
- [ ] `ComposeAttribute` (`Xunit.v3.DataAttribute`): constructor
      (`params object?[] inlineValues`), `Seed` property,
      `SupportsDiscoveryEnumeration() => false`, `GetData` stub wired to
      Phase 2's binding algorithm.
- [ ] `ComposeAttribute<TProfile> : ComposeAttribute where TProfile :
      ICompositionProfile, new()`.
- [ ] `Lazy<Composer>` + immutable binding-plan caching, keyed to the
      attribute instance. The cached binding plan holds only metadata
      derived once from `testMethod`: ordered `ParameterInfo`s, a
      descriptor template per parameter (ordinal, name, declaring type,
      nullability), each parameter's `[Shared]` flag, and the
      signature-validation result (generic method /
      `ref`/`out`/`in`/`params` / duplicate-`[Shared]`-type checks, run
      once). It never holds anything row-scoped: no `CompositionRow`, no
      seed, no scope, no composed value — those are created fresh on
      every `GetData` call by Phase 2's binding algorithm, never cached.
- [ ] **Cached per-parameter invoker delegates**, built in the same pass
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
- [ ] **If `Seed` was configured and is negative, throw now**, using
      `row.Seed` (echoing the rejected value back). This is what makes
      every row `Compono.Xunit` creates have a non-negative seed
      unconditionally — `CompositionBuilder.WithSeed(int)` itself has no
      such restriction and happily accepts a negative value when the
      cached `Composer` is built (Phase 1), so this check is the only
      place that actually enforces it, and it must run before any other
      failure category so a rejected negative seed is reported clearly
      rather than surfacing as a confusing mismatch somewhere else.
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
        `inlineValues[i]!.GetType()` is assignable to parameter `i`'s
        declared type; otherwise a pre-composition `CompositionException`
        naming the parameter and both types.
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
      parameter order.

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

_(Filled in as Phase 0 begins — implementation may surface plumbing
details this plan's ADRs deliberately left at decision-level.)_
