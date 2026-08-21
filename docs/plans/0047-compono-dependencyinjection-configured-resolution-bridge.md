# [PLAN-0047] Compono.DependencyInjection: Configured-Resolution IServiceProvider Bridge

**Status:** Done

**Implements:** ADR-0047

## Goal

`CompositionRow.TryResolveConfigured(Type, out object?)` ships in core
`Compono`, and a new `Compono.DependencyInjection` package ships
`row.AsServiceProvider()` on top of it — a plain `IServiceProvider` with
adapter-owned, per-`Type` stable identity, reaching only scope/exact
registrations/configuration-rules/providers (never `UseServiceProvider`,
never generated-plan composition). Done means: both are packed, tested,
documented in the same package-guide/index/README pattern every other
Compono package already uses, and verified via a real local-feed restore —
without touching `Compono.BUnit`, because it doesn't exist.

## Scope

In scope, per ADR-0047's Decision Outcome:

- `CompositionRow.TryResolveConfigured(Type, out object?)` in core `Compono`.
- New package `Compono.DependencyInjection`: `CompositionRowServiceProviderExtensions.AsServiceProvider()`
  + internal `ComponoServiceProvider` adapter.
- Package guide, index entry, README entry — the same doc footprint every
  other Compono package gets.

Explicitly deferred, per ADR-0047's Negative Consequences and Considered
Options (do **not** build these against this plan):

- `Compono.BUnit` — rejected outright by ADR-0047, not a deferred item.
- `UseServiceProvider` forwarding through `TryResolveConfigured`.
- Any runtime-`Type` path into generated-plan composition (stages 7-8).
- `services.AddCompono()`, `Composer`/`IComposer` registration into DI,
  `AddServices(IServiceCollection)`, `IServiceScope`/`IServiceScopeFactory`
  integration, automatic `IServiceCollection` population.
- Cross-context recursion detection/tracking (documented as a known,
  narrow hazard in ADR-0047's Recursion section; XML-doc warning only).
- A confirming dogfood pass migrating `trivia-manager`'s `FreezeAndRegister`
  pattern to this package — that's RESEARCH-0007's explicitly deferred
  follow-up, run against a separate repository after this ships, not a task
  in this plan.
- Any dependency on `bUnit` itself, anywhere in this repo. `bUnit`'s
  `AddFallbackServiceProvider`/`BunitServiceProvider` is a bUnit-owned
  capability, not a standard `Microsoft.Extensions.DependencyInjection`
  one (`IServiceCollection`/`ServiceProvider` have no built-in
  fallback-provider chaining) — `Compono.DependencyInjection`'s job ends at
  being a correct `IServiceProvider`; proving bUnit's own fallback ordering
  is bUnit's test suite's job, and proving the two compose correctly end to
  end is the deferred `trivia-manager` dogfood above, not this plan.

This is one cohesive change — one implementation phase, one PR. The task
sections below (core primitive, package, tests, docs, packaging
verification) are logical groupings for readability, not sequential
phases or separate PR boundaries.

## Tasks

### Core primitive (`Compono`)

- [x] Add `CompositionRow.TryResolveConfigured(Type type, out object? value)`
      to `src/Compono/CompositionRow.cs`, per ADR-0047's exact contract:
      reaches stage 2 (scope, unconditional read, `isShared: false` write —
      no new sharing semantics), stage 3a (exact registrations), stages 4-6
      (configuration rules, `ICompositionValueProvider` implementations).
      Excludes stage 3b (`UseServiceProvider`) and stages 7-8
      (generated-plan/collection-plan). Returns `false` (never throws) when
      no reachable stage handles `type`. Still throws `CompositionException`
      via the existing `BuildException` path when a reachable stage is
      applicable but fails.
  - **Implementation finding, see Notes:** a bare runtime `Type` carries no
    compile-time nullable-reference annotation, unlike `Resolve<TValue>()`.
    `TryResolveConfigured`'s internal `CompositionContext` implementation
    always validates as `Nullability.Nullable` — every reachable stage's
    `null` result is accepted, none rejected as "non-nullable" (there's no
    per-call way to know a bare `Type` was meant non-nullable). This is
    narrower/simpler than originally scoped here, not a contract violation.
- [x] Add the XML doc from ADR-0047's Core Primitive section verbatim (or
      near-verbatim) — the "NOT equivalent to `Resolve<TValue>()`" framing is
      load-bearing, not optional polish.
  - Also required, discovered during implementation: a new eighth
    `PathSegment.ConfiguredResolution` kind (`src/Compono/PathSegment.cs`),
    threaded through `RandomSource.Fork`'s tag switch (a **new, unused**
    tag value - `DeriveSeedTag`'s existing value was left untouched,
    per ADR-0012's deterministic-output compatibility guarantee) and
    `CompositionPath`'s two display-string switches - so a
    `TryResolveConfigured` call has its own diagnosable path identity,
    the same as every other entry point. All 242 pre-existing
    `Compono.Tests` still pass unchanged, confirming no existing
    seed-derived value shifted.

### `Compono.DependencyInjection` package

- [x] Scaffold `src/Compono.DependencyInjection/Compono.DependencyInjection.csproj`,
      matching `src/Compono.TestDoubles/Compono.TestDoubles.csproj`'s shape:
      `net8.0;net9.0;net10.0;net11.0`, `ProjectReference` to `Compono` with
      the same `PinProjectReferenceVersionsExact` target, `Title`/
      `Description` metadata, `InternalsVisibleTo` to
      `Compono.DependencyInjection.Tests` only. **No `ProjectReference`/
      `PackageReference` to `Compono.TestDoubles` or `Compono.NSubstitute`
      — the package itself must stay provider-agnostic; those two are test
      project dependencies only (see Tests below).**
  - [x] ~~Add `Microsoft.Extensions.DependencyInjection.Abstractions`
        package reference~~ **Superseded — see ADR-0047 Amendment 1.**
        Originally added, then removed entirely once PR review surfaced
        that the package doesn't reference anything from that namespace at
        all (`row.AsServiceProvider()` returns plain `System.IServiceProvider`,
        BCL). The final, shipped `Compono.DependencyInjection.csproj` has
        no `PackageReference` at all beyond the `Compono` `ProjectReference` —
        this task is recorded as done in its amended, dependency-free
        form, not the originally-scoped one.
- [x] Add `CompositionRowServiceProviderExtensions.AsServiceProvider(this CompositionRow row)`
      in namespace `Compono` (matching every other integration package's
      "extension method lives in `Compono`'s own namespace" convention),
      with the XML doc from ADR-0047 (the cross-row-recursion warning is
      load-bearing, not optional).
- [x] Add internal `ComponoServiceProvider(CompositionRow row) : IServiceProvider`
      exactly per ADR-0047's sketch: `Dictionary<Type, object?>` cache,
      `GetService` checks cache first, calls `TryResolveConfigured` on miss,
      caches on success (including a legitimate `null`), does not cache a
      `false`/miss result, does not implement `IDisposable`/
      `IAsyncDisposable`, does not dispose cached values.

### Tests

Core primitive — `test/Compono.Tests/CompositionRowTryResolveConfiguredTests.cs`
(new file):

- [x] Resolves a value from an exact registration (stage 3a).
- [x] Resolves a value from a stage 4-6 provider (a minimal fake
      `ICompositionValueProvider` is enough — this file doesn't need any
      integration-package dependency).
- [x] Reads an existing shared scope value (stage 2) when one was already
      established via ordinary `[Shared]`/`ResolveShared` usage elsewhere
      in the same row.
- [x] Returns `false`, does not throw, for a type with no reachable
      registration/provider/scope value.
- [x] Does **not** consult a configured `UseServiceProvider` — a type only
      satisfiable via `UseServiceProvider` returns `false`, not the
      service-provider's value.
- [x] Does **not** reach ordinary generated-plan composition — a concrete
      type with no registration/provider, but composable via the
      generated plan through `Resolve<T>()`, returns `false` through
      `TryResolveConfigured`. Test proves both halves in one case: the
      same type is unresolvable via `TryResolveConfigured` yet genuinely
      composes via `row.Resolve<T>(descriptor)` on the same row, ruling
      out "this type just isn't composable at all" as the explanation.
- [x] Two calls for the same type without an intervening `ResolveShared`
      are independent (no new caching in `CompositionScope` itself —
      confirms this primitive didn't accidentally change `Resolve<T>()`'s
      existing unshared-by-default behavior). Proven via a provider that
      hands back a fresh instance per call; two `TryResolveConfigured`
      calls return non-reference-equal results.
- [x] A registration/provider that produces `null` returns `(true, null)`,
      not `false` and not a thrown validation failure.
- [x] **Superseded, not applicable** (see the Core primitive section's
      finding above): `TryResolveConfigured` always validates as
      `Nullability.Nullable` for every reachable stage - a bare runtime
      `Type` has no per-call non-nullable/nullable distinction to enforce,
      unlike `Resolve<TValue>()`'s compile-time-known `TValue`. There is no
      "non-nullable `Type` still throws on null" case to test, because
      `TryResolveConfigured` never treats any `Type` as non-nullable.
- [x] A reachable-but-failing stage (a throwing registration factory or
      provider) still throws, not `false` — but not uniformly the same
      exception type. Covered both shapes: a throwing registration factory
      throws a wrapped, diagnosed `CompositionException`; a throwing
      stage-4-6 provider propagates its own original exception type
      uncaught and unwrapped, per ADR-0024's Provider Failure Semantics
      (see ADR-0047 Amendment 2).

`Compono.DependencyInjection` — `test/Compono.DependencyInjection.Tests/`
(new project):

- [x] `GetService` returns a value composed via `Compono.TestDoubles` — the
      primary demonstrated provider per ADR-0047, and this repo's primary
      product direction. `Compono.TestDoubles` is a test-project-only
      dependency here. (Uses `GeneratedTestDoubleRegistry.RegisterFactory<T>`
      + `UseGeneratedTestDoubles()` directly, same hand-registered-factory
      convention `Compono.TestDoubles.Tests` itself already uses in place of
      a real generator run.)
- [x] `GetService` for an unsatisfiable type returns `null`, does not throw.
- [x] Two `GetService` calls for the same `Type` return the identical
      instance (reference equality) — the adapter's own caching, not
      `CompositionScope`'s.
- [x] **Misses are not cached.** Implemented as scoped: a provider that
      starts declining, then is switched to handling before a second
      `GetService` call for the same type - first call returns `null`,
      second returns a real (non-`null`) value, and the provider's own
      call counter proves it was invoked again on the second call rather
      than short-circuited by a cached miss.
- [x] A legitimately-`null` resolution returns `null` on the first
      `GetService` call, and does not re-invoke the provider on a second
      call for the same type - verified via a call-counting fake provider
      (`CallCount` stays `1` across two `GetService` calls).
- [x] A type only satisfiable via a `UseServiceProvider`-configured
      external provider is **not** resolved by the adapter (`GetService`
      returns `null`) — confirms the stage-3b exclusion.
- [x] A type only satisfiable via ordinary generated-plan composition
      (no registration/provider) is **not** resolved by the adapter.
- [x] Confirm the adapter does not implement `IDisposable`/
      `IAsyncDisposable` (a compile-time/type check, not a runtime test).
- [x] **Decided: skip.** A real `Compono.NSubstitute`-backed test would only
      re-demonstrate the same provider-neutral dispatch path the fake-
      provider tests (and `test/Compono.Tests`' own stage 4-6 coverage)
      already prove - no concrete regression scenario specific to
      NSubstitute's own proxy identity was found. `Compono.DependencyInjection.Tests.csproj`
      references only `Compono.TestDoubles` (test-project-only); the
      shipped `Compono.DependencyInjection` package references neither
      provider package.

### Documentation and packaging

- [x] New `docs/packages/compono-dependencyinjection.md`, matching the
      existing package-guide shape (`compono-testdoubles.md` is the closest
      analog — a single, focused, non-framework-specific package). Cover:
      what `AsServiceProvider()` does, its exact reachable-stage contract
      (stage 2/3a/4-6 only — the same honesty requirement ADR-0047 holds
      the API itself to), the adapter's caching/null/disposal contract, a
      worked bUnit example (`Ctx.Services.AddFallbackServiceProvider(...)`,
      both the configured and lazy-fallback shapes from ADR-0047, clearly
      labeled as an illustrative consumer example, not something this
      package depends on or tests), and an explicit "this is not
      bUnit-specific" callout with an ASP.NET Core/generic-host-shaped
      example alongside it.
- [x] Add a row to `docs/packages/index.md`'s package table.
- [x] Add `Compono.DependencyInjection` to `README.md`'s package table
      (same badge-link shape as the existing rows).
- [x] Cross-link ADR-0047 and RESEARCH-0007 from the new package guide.
- [x] Update `docs/roadmap/post-mvp.md`: remove the `Compono.DependencyInjection`
      bullet from "Current state" (following the exact pattern passes 2-6
      already established — "this finding is no longer listed here").
      **Reversed from an earlier deferral** — PR review (Codex, #105)
      correctly caught that deferring this edit until after merge was
      already inconsistent with `docs/roadmap/future-packages.md`'s own
      edits *in this same PR*, which already described the package as
      shipped. Fixed by doing this edit now instead, treating this PR's
      merge as the shipping event, consistently across both docs.

### Packaging verification

- [x] **Added, not originally scoped: wire the new package into the CI
      package-validation gate.** PR review (Codex, #105, P1) correctly
      caught that `.github/workflows/package-validation.yaml` (baseline
      lookup, pack, CS1591 enforcement loops) and
      `.github/scripts/inspect-packed-nupkgs.sh` (file-listing/manifest/
      dependency assertions) never enumerated `Compono.DependencyInjection`
      at all — the pre-merge package-readiness gate silently never
      inspected it. Added to all three `package-validation.yaml` loops and
      `inspect-packed-nupkgs.sh`'s package loop + a new
      `Compono.DependencyInjection)` case block (title assertion,
      exact-pin `Compono` dependency assertion; no third-party dependency
      assertion needed, see the dependency-removal note below). Verified
      by running the full script against a real 7-package local pack,
      matching CI's own job exactly - all assertions pass. Deliberately
      did **not** add a new `Compono.DependencyInjection.SampleTests`
      local-feed packed-consumer smoke-test project (the shape the other
      five packages have) - out of scope for this fix, a candidate
      follow-up if wanted.

Matches this repo's established convention (PLAN-0004 Phase 3 / PLAN-0005
Phase 2 / PLAN-0040 Phase 0's pattern) — a real `dotnet pack` → local NuGet
feed → real restore proof, done as part of this same PR, not gated on an
actual nuget.org publish (which happens later, outside this plan, the same
way it has for every prior package):

- [x] `dotnet pack` both `Compono` (with the new primitive) and
      `Compono.DependencyInjection` locally (`-p:Version=99.0.0`, `-c
      Release`, to an isolated scratch feed directory).
- [x] Push both to a local NuGet feed and restore them into a minimal
      scratch consumer project, confirming `row.AsServiceProvider()` is
      usable from a real packaged reference — not just a `ProjectReference`
      in this repo's own test projects. A real `dotnet run` against the
      packaged consumer printed `PACKED-CONSUMER-OK` for both a real
      registration resolving through the packed `AsServiceProvider()` and
      an unregistered type returning `null`.
- [x] Confirm the exact-pin `ProjectReference` → packed-dependency version
      match works (`PinProjectReferenceVersionsExact`), same verification
      prior integration packages ran at their own implementation PR - the
      packed `Compono.DependencyInjection.99.0.0.nupkg` restored against
      exactly `Compono 99.0.0`, confirmed by the successful restore/run
      above (a version mismatch here fails restore outright).

## Critical Files

- `src/Compono/CompositionRow.cs` — new public method.
- `test/Compono.Tests/CompositionRowTryResolveConfiguredTests.cs` — new.
- `src/Compono.DependencyInjection/Compono.DependencyInjection.csproj` — new.
- `src/Compono.DependencyInjection/CompositionRowServiceProviderExtensions.cs` — new.
- `src/Compono.DependencyInjection/ComponoServiceProvider.cs` — new, internal.
- `test/Compono.DependencyInjection.Tests/` — new project.
- `Compono.sln` (or equivalent solution file) — add both new projects.
- `docs/packages/compono-dependencyinjection.md` — new.
- `docs/packages/index.md` — new row.
- `README.md` — new package-table row.
- `docs/roadmap/post-mvp.md` — remove the now-shipped candidate.
- `docs/roadmap/future-packages.md` — package count, graduation note.
- `.github/workflows/package-validation.yaml` — added `Compono.DependencyInjection`
  to the baseline/pack/CS1591 loops.
- `.github/scripts/inspect-packed-nupkgs.sh` — added `Compono.DependencyInjection`
  to the package loop and its manifest-assertion case block.
- `.github/workflows/docs.yml` — added `Compono.DependencyInjection` to the
  pre-API-reference-generation build loop and path filters.
- `docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md` —
  Amendment 1 (dependency removal).

## Test Plan

Unit tests in `test/Compono.Tests` (core primitive) and
`test/Compono.DependencyInjection.Tests` (package), matching
`testing.md`'s existing conventions (see `CompositionRowTests.cs` for the
established style). No `bUnit` dependency anywhere in this repo —
`Compono.DependencyInjection`'s test suite proves `row.AsServiceProvider()`
is a correct `IServiceProvider` per ADR-0047's own contract; it does not
attempt to prove bUnit's `AddFallbackServiceProvider` ordering, which is
bUnit's own test suite's responsibility, not this package's. Local
`dotnet pack` → local-feed → restore verification closes out the plan, per
this repo's established package-release convention — no dependency on an
actual nuget.org publish to reach `Done`.

## Notes

All ADR-0047 architectural boundaries held during implementation - no
design change was needed, only two narrow, implementation-level findings
worth recording (neither reopens ADR-0047):

1. **Nullability at a bare `Type` boundary.** `Resolve<TValue>()`'s
   non-nullable-rejection behavior comes from `TValue`'s compile-time
   nullable annotation, threaded through a `CompositionRequestDescriptor`.
   A bare runtime `Type` argument (`TryResolveConfigured(Type type, ...)`)
   has no equivalent annotation to read - there is no way to know, from a
   `Type` object alone, whether the caller "meant" `string` or `string?`.
   `TryResolveConfigured` therefore always validates as
   `Nullability.Nullable` for every stage it reaches: a legitimate `null`
   from scope/a registration/a provider is always accepted, never rejected
   as "non-nullable." This matches `IServiceProvider.GetService(Type)`'s
   own null-friendly BCL contract, which is the entire reason this method
   exists - not a deviation from it. The plan's originally-scoped
   "non-nullable type still throws" test doesn't apply and was removed
   (see the Tests section above); nothing else about ADR-0047's stated
   contract changed.
2. **A new `PathSegment` kind was required, not just plumbing.** Giving
   `TryResolveConfigured` correct path/seed bookkeeping (needed so a
   registration factory or provider it invokes can still call
   `context.Resolve<T>()`/`DeriveSeed()` correctly, and so a thrown
   failure gets a real diagnosed path) meant adding an eighth
   `PathSegment.ConfiguredResolution` kind, threaded through
   `RandomSource.Fork` and `CompositionPath`'s two display switches. Care
   was needed here: the fork-key tag byte space already had all 8 values
   (0-7) accounted for across the seven existing `PathSegment` kinds plus
   `DeriveSeedTag`'s own fixed salt - the new kind got the next unused
   value (`8`), `DeriveSeedTag` itself was left untouched at `7`, per
   ADR-0012's deterministic-output compatibility guarantee (renumbering an
   existing tag would silently change every derived-seed value for
   existing consumers on a fixed seed). All 242 pre-existing
   `test/Compono.Tests` cases still pass unchanged, confirming this.

Verification performed: `dotnet build` on the full solution (zero
warnings, `CS1591` doc-comment gate included), the full existing
`test/Compono.Tests` suite (242/242) plus 10 new
`CompositionRowTryResolveConfiguredTests`, the new
`Compono.DependencyInjection.Tests` (8/8, later 9/9 - see below), and
every other existing test project in the solution (Bogus, Generators,
NSubstitute, TUnit, TestDoubles, XunitV3 - all green), then a real
`dotnet pack` → local feed → packaged-consumer `dotnet run` proving
`row.AsServiceProvider()` works from an actual restored NuGet package, not
just an in-repo `ProjectReference`.

3. **PR review finding (Codex, #105): `ComponoServiceProvider` had no
   synchronization.** Two concurrent first-time `GetService` calls for the
   same type could both miss the adapter's cache and enter the same
   mutable `CompositionRow`/`CompositionContext` simultaneously - not just
   an ordinary "`Dictionary` isn't thread-safe" risk, but a real risk of
   corrupting `CompositionContext`'s own unsynchronized `_path`/`_random`/
   trace bookkeeping, or handing two same-type callers different instances
   despite the documented stable-identity guarantee. Fixed with a plain
   `lock(object)` around the whole cache-check/resolve/cache-write section
   in `GetService` - not `System.Threading.Lock`
   (`coding-standards.md`'s usual preference), since that type doesn't
   exist on this package's `net8.0` target. Verified the fix is load-
   bearing, not cosmetic: temporarily reverted it and confirmed the new
   regression test (`GetService_ReturnsTheSameInstance_UnderConcurrentFirstCalls`,
   16 parallel first-time calls against a provider with an artificial
   delay) failed reliably (3/3 runs) without the lock, then passed
   reliably (5/5 runs) with it restored.
4. **PR review finding (Codex, #105): the XML doc overstated the
   provider-failure contract.** `TryResolveConfigured`'s doc said a
   reachable-but-failing stage always throws a diagnosed
   `CompositionException` - true for an exact registration factory
   (wrapped via `InvokeFactory`), but not for a stage 4-6
   `ICompositionValueProvider`, whose own thrown exception propagates
   uncaught per ADR-0024's existing Provider Failure Semantics (confirmed
   by this plan's own `TryResolveConfigured_Throws_WhenAReachableProviderThrows`
   test, which asserts `InvalidOperationException`, not
   `CompositionException`). Corrected both `CompositionRow.TryResolveConfigured`'s
   and the internal `CompositionContext.TryResolveConfigured`'s XML docs to
   distinguish the two cases explicitly - a documentation-precision fix,
   not a behavior change (the pipeline already worked this way; only the
   doc was wrong).

## Second PR review round (Codex, #105) findings

5. **P1 — the CI package-validation gate never covered
   `Compono.DependencyInjection`.** Real gap: `package-validation.yaml`'s
   three enumeration loops and `inspect-packed-nupkgs.sh`'s package loop
   were never updated when the package was added, so this pre-merge gate
   silently never packed, CS1591-checked, or content-inspected it. Fixed
   (see Packaging verification section above); verified by running the
   validation script against a real 7-package local pack.
6. **P2 — `Microsoft.Extensions.DependencyInjection.Abstractions`'s bare
   `8.0.2` floor should have been a tested range, per ADR-0031 Amendment
   1.** Investigating the fix (attempting a per-TargetFramework conditional
   range, one per TFM's own latest major) surfaced something bigger: the
   package doesn't reference anything from that namespace at all -
   `row.AsServiceProvider()` returns plain `System.IServiceProvider`
   (BCL). **The dependency was removed entirely** rather than range-pinned
   - see ADR-0047 Amendment 1. This also incidentally explains an anomaly
   hit while implementing the per-TFM-range attempt: `net11.0`'s packed
   `.nuspec` dependency group silently dropped the reference while
   net8/9/10 kept it - moot now that there's no dependency to drop.
   - **Implementation-process note, not a design finding**: my first
     attempt at the per-TFM conditional `PackageVersion` syntax nested a
     `<ItemGroup Condition="...">` directly inside the file's existing
     unconditioned `<ItemGroup>` - invalid MSBuild (an `ItemGroup` cannot
     contain another `ItemGroup`), which broke Central Package Management
     resolution for the *entire* `Directory.Packages.props` file (every
     package, not just this one - `NU1015` across unrelated projects).
     Caught immediately by a real `dotnet pack` failing outright, fixed by
     closing/reopening the outer `ItemGroup` around the new conditional
     ones as siblings. Recorded here since the eventual fix (removing the
     dependency) means this particular MSBuild lesson isn't visible
     anywhere else in the final diff.
7. **P2 — `docs/roadmap/post-mvp.md` still listed the package as
   outstanding while `docs/roadmap/future-packages.md` (edited in this
   same PR) already described it as shipped.** A real internal
   inconsistency this PR introduced, correctly caught. My earlier
   reasoning ("defer the roadmap edit until an actual merge, matching
   every prior entry's pattern") turned out to not actually hold once
   checked against what I'd already written elsewhere in this same diff -
   fixed by doing the `post-mvp.md` edit now instead, treating this PR's
   merge as the shipping event, consistently with `future-packages.md`.

## Third PR review round (Codex, #105) findings

8. **P1 — sibling `TryResolveConfigured` calls on the same row shared a
   fork identity, silently colliding derived-randomness values.** The
   most serious finding across all review rounds. `PathSegment.ConfiguredResolution`
   was originally designed with no ordinal ("never has siblings" -
   wrong: two sequential top-level `TryResolveConfigured` calls on the
   same row ARE siblings under the row's pre-rooted path, exactly like
   `TestParameter`/`ManualResolve`). Confirmed with a real repro before
   fixing: `Register<ProbeA>(ctx => new ProbeA(ctx.DeriveSeed()))` and
   `Register<ProbeB>(ctx => new ProbeB(ctx.DeriveSeed()))`, resolved
   sequentially via `TryResolveConfigured`, produced the *identical*
   derived value. Fixed by giving `ConfiguredResolution` an `Ordinal`
   (matching `TestParameter`/`ManualResolve`'s existing shape exactly),
   backed by a new per-`CompositionContext` counter
   (`_nextConfiguredResolutionOrdinal`), threaded through `RandomSource.Fork`
   and `CompositionPath`'s two display switches. New permanent regression
   test (`TryResolveConfigured_GivesSiblingRequests_IndependentRandomStreams`)
   reproduces the exact scenario and passes with the fix; full 243-test
   `Compono.Tests` suite still green.
9. **P2 — ADR-0047's own Core Primitive text still promised a wrapped
   `CompositionException` for a provider failure**, even after the code's
   XML doc was corrected in the second review round. Recorded as
   Amendment 2 (Accepted ADRs stay immutable - corrections get dated
   amendments, not silent edits to the original text).
10. **P2 — this plan's own completed checklist still described adding
    the (later-removed) `Microsoft.Extensions.DependencyInjection.Abstractions`
    package reference**, contradicting ADR-0047 Amendment 1. Reworded the
    checklist item to record the amended, dependency-free outcome instead
    of the originally-scoped one.

## Fourth PR review round (Codex, #105) findings

11. **P2 — `TryResolveConfigured`'s XML doc still listed an impossible
    null-failure case.** Since the method always validates as
    `Nullability.Nullable`, a legitimate `null` result is never rejected —
    only a wrong-runtime-type value can throw. Leftover from an earlier
    edit that changed the validation semantics without fully updating this
    doc. Corrected, and the API reference regenerated to match.

## Fifth PR review round (Codex, #105) findings

12. **P2 — `AsServiceProvider()` created a fresh adapter (and fresh lock)
    on every call, so wrapping the same row twice and using both
    providers concurrently could still race inside the row's shared
    `CompositionContext`.** Each adapter's lock only serialized its own
    calls, never against a different adapter's, for the same row. Fixed
    by memoizing one adapter per row via `ConditionalWeakTable<CompositionRow,
    IServiceProvider>` (itself thread-safe, guarantees the same value for
    the same key across concurrent callers) — `AsServiceProvider()` now
    returns the identical instance for the same row on every call, so
    there is exactly one lock per row regardless of how many times a
    consumer calls it. Confirmed with a repro before fixing (two
    separately-obtained providers for the same row, used concurrently,
    failed reliably 3/3 on the per-call-adapter code) and a new permanent
    regression test that passes reliably (5/5) with the fix.

## Sixth PR review round (Codex, #105) findings

13. **P2 — disposal ownership was only documented on the internal
    `ComponoServiceProvider`, invisible from the public surface.** A
    consumer only ever sees `IServiceProvider` plus `AsServiceProvider()`'s
    own doc, neither of which mentioned that the adapter never disposes a
    cached resolved value. Added to both `AsServiceProvider()`'s XML doc
    remarks and the package guide's "What it gives you" list - this was
    already required by this plan's own Documentation task ("the adapter's
    caching/null/disposal contract"), just missed when originally written.
14. **P2 — four user-facing doc pages still listed only the previous six
    packages**, contradicting `docs/packages/index.md`/`README.md`
    (already correct) and `docs/roadmap/future-packages.md` (already
    describes the package as shipped): `docs/index.md`'s package table,
    `docs/roadmap/index.md`'s "Today" shipped list,
    `docs/getting-started/installation.md`'s optional-package install
    commands, and `docs/getting-started/ai-agent-skill.md`'s two package
    enumerations. All four updated. Historical records that also list the
    prior six packages (ADR-0042, PLAN-0043, PLAN-0044,
    RESEARCH-0005) were deliberately left untouched - they're
    point-in-time snapshots of when they were written, not current-state
    docs.

## Seventh PR review round (Codex, #105) findings

15. **P2 — ADR-0047's own "Behavior, precisely" prose still described a
    nullable-vs-non-nullable distinction `TryResolveConfigured` never
    actually makes.** Same root cause as finding 11 (the code's XML doc
    fix didn't propagate back to the ADR's own text this time either).
    Recorded as Amendment 3.
16. **P2 — the new eighth `PathSegment.ConfiguredResolution` kind's tag/
    ordinal decision existed only in code comments and this plan's Notes,
    not in either governing ADR.** ADR-0012 (Composition Path Identity and
    Deterministic Random Forking) is the actual authoritative record for
    segment-tag/reproducibility decisions (its own Amendment 2 established
    an explicit tag-collision-test requirement for exactly this kind of
    change) - recorded there as Amendment 3, cross-linked from ADR-0047.
    While fixing this, found the concrete gap the amendment's own
    precedent calls out: `RandomSourceTests.Fork_ProducesDistinctOutput_ForEachSegmentKindAtSameOrdinalOrIndex`
    tested all seven pre-existing kinds pairwise-distinct but never
    included the eighth. Added it - passes, confirming tag `8`'s output is
    genuinely distinct from the other seven at ordinal `0`.

## Eighth PR review round (Codex, #105) findings

17. **P2 — `TryResolveConfigured` leaked trace entries on the exception
    path.** Every non-exceptional return already rewinds `_trace` to its
    entry checkpoint, but an exception propagating out (a stage 3a
    factory's wrapped `CompositionException`, or a stage 4-6 provider's
    own raw exception) skipped straight to `finally`, which only restored
    `_path`/`_random`/`_currentDeclaringType` - never the trace. Since
    `BuildDiagnostic` slices from index `0`, a later, unrelated failing
    call on the same row would pick up the orphaned entries too. Fixed
    with a `catch when (!isNestedInAnotherInvocation)` clause that rewinds
    and rethrows - gated on `_manualResolveFrames.Count == 0` at entry, so
    an enclosing operation's own exception handling (the case where this
    call was itself reached from inside another factory/provider's own
    invocation) still gets to see these entries, only a genuinely
    top-level call rewinds them away. Confirmed with a repro before fixing
    (temporarily reverted the catch clause; the new regression test failed
    reliably 3/3) and the fix passing reliably (5/5) with it restored.
18. **P2 — this plan's own completed checklist still said a throwing
    provider "throws `CompositionException`," contradicting the actual,
    documented (ADR-0047 Amendment 2) provider-exception contract one
    bullet below it.** Reworded to state the two shapes throw different
    exception types, matching what the rest of this same checklist item
    already said correctly.

## Ninth PR review round (Codex, #105) findings

19. **P2 — ADR-0047 never recorded the row-wide adapter identity change
    from finding 6's fix.** `AsServiceProvider()` moved from "fresh
    adapter per call" to a `ConditionalWeakTable`-memoized adapter per
    row, but that lifetime/identity contract only ever got written down
    here in this plan, not in the ADR itself. Recorded as ADR-0047
    Amendment 4.
20. **P2 — the cross-row recursion warning (XML doc `<remarks>` and the
    ADR's own "Recursion" section) was factually wrong.** Both claimed a
    cross-row cycle overflows the stack because "each hop is a fresh
    `CompositionContext` with empty guard state." That doesn't match
    `CompositionRow`: its underlying `CompositionContext` is created once
    and reused for every call made on that row, so the existing
    `IsFactoryActive`/provider reentrance guards do carry state across a
    cross-row hop back into the same row, and do trip. Verified directly
    with a two-row repro
    (`CrossRowCycle_IsDetectedAsARecursiveFactory_NotAStackOverflow`):
    the cycle throws a diagnosed `CompositionException` ("Recursive
    registration or configuration-rule factory detected"), never a
    `StackOverflowException`, reliably across repeated runs. Corrected
    the XML doc `<remarks>` and recorded the correction as ADR-0047
    Amendment 4 (superseding the original Recursion section and its
    matching Negative Consequences bullet, without rewriting them in
    place per this repo's ADR-immutability rule).
21. **P2 — `docs/public-api.md`'s inline "Package Guides" list (lines
    16-18) still named only the previous five integration packages.**
    Missed in an earlier round's doc sweep because this file is a
    tombstone that mostly just points elsewhere (per ADR-0030 Amendment
    2) — its own inline package enumeration was overlooked. Added
    `Compono.DependencyInjection` to the list.
22. **P2 — `TryResolveConfigured_GivesSiblingRequests_IndependentRandomStreams`
    built its `Composer` without `.WithSeed(...)`, so it ran on a new
    random seed every execution and only asserted inequality between two
    folded 32-bit derived values.** Theoretically flaky on a colliding
    seed, and not reproducible from the test alone if it ever did fail.
    Pinned a fixed seed via `.WithSeed(...)`.

## Tenth PR review round (Codex, #105) findings

23. **P2 — config-rule factory exceptions are wrapped too, not just exact
    registration factories.** `CompositionRow.TryResolveConfigured`'s XML
    doc (and ADR-0047 Amendment 2) said only an exact registration
    factory's failure gets wrapped in a `CompositionException` -
    incomplete, since `TypeRuleProvider.TryCompose` invokes a
    `.For<T>().Use(...)` configuration-rule factory through the exact same
    `InvokeFactory`, wrapping its failure identically. Verified with a new
    test (`TryResolveConfigured_Throws_WhenAReachableConfigurationRuleFactoryThrows`)
    and corrected both XML docs plus recorded the correction as ADR-0047
    Amendment 5.
24. **P1 — `ConfiguredResolution`'s fork identity is call-order-dependent,
    not type-dependent, which breaks reproducibility under concurrent
    first-time resolution.** Verified directly (no actual race needed -
    swapping call order alone reproduces it) with
    `TryResolveConfigured_DerivedValue_DependsOnCallOrder_NotOnWhichTypeWasRequested`.
    Deliberately not code-fixed: the only way to make identity
    order-independent is keying it off the requested `Type` instead of an
    incrementing ordinal, which would be the only `PathSegment` kind doing
    so (every other kind derives identity from a stable ordinal/index,
    never a name/type, per `Fork_IsUnaffectedByName_ButDiffersByOrdinal`)
    and would hash a formatted identifier as a fork key, which `Fnv1a`'s
    own design deliberately forbids. This is a genuine architectural
    decision, not a same-scope bug fix - per this plan's own governing
    instruction to stop and surface exactly this kind of finding rather
    than silently redesigning around it. Recorded as ADR-0047 Amendment 5;
    the "safe to use concurrently" XML doc guarantee is narrowed to
    thread-safety (no corruption/torn state), not ordinal-assignment
    determinism under concurrent first-time resolution. Sequential
    resolution - the documented, evidenced use case - is unaffected.
25. **P2 — a concurrent cross-row cycle deadlocks, not just recurses.**
    Distinct from finding 20's *sequential* cross-row cycle (caught by the
    existing reentrance guard): two cross-wired rows resolved on two
    threads each acquire their own row's adapter lock, then block waiting
    for the other's - a classic AB-BA deadlock the reentrance guard never
    gets a chance to see. Verified via repro: hung reliably (5/5) with the
    original plain `lock`. Fixed by replacing `ComponoServiceProvider`'s
    `lock` with a bounded `Monitor.TryEnter` (10s) that throws a diagnosed
    `TimeoutException` (wrapped in `CompositionException` if it fires
    inside a factory) instead of blocking forever - the same repro now
    throws reliably (5/5) instead of hanging. Ordinary uncontended calls
    are unaffected (the lock is still acquired immediately). Regression
    test: `GetService_ThrowsTimeoutException_RatherThanDeadlocking_OnAConcurrentCrossRowCycle`
    in `Compono.DependencyInjection.Tests` (the real `AsServiceProvider()`,
    not a stand-in, since the fix lives in `ComponoServiceProvider`
    itself). Recorded as ADR-0047 Amendment 5.

## Eleventh PR review round (Codex, #105) findings

26. **P2 — the deadlock fix (finding 25) bounded every `GetService` call,
    not just the genuinely deadlock-risky ones.** A single row's lock can
    never deadlock by itself (whoever holds it eventually releases it) -
    deadlock is only possible when a thread already holding ONE row's
    lock tries to acquire ANOTHER's while nested inside a factory/provider
    callback. The original fix's fixed `Monitor.TryEnter` timeout applied
    to top-level calls too, so legitimately slow user code (a slow custom
    provider, a debugger pause, loaded CI) contending only with another
    top-level call for the *same* row could throw a spurious
    `TimeoutException`. Fixed by tracking a `[ThreadStatic]`
    `t_heldAdapterLockDepth` (how many `ComponoServiceProvider` locks this
    thread currently holds, across every row it touches) and only applying
    the bounded `TryEnter` when a call is nested (depth > 0 on entry) - a
    fresh top-level call uses a plain, unbounded `Monitor.Enter`, so
    waiting out a slow same-row resolution, however long, always
    succeeds. Verified both directions: reverted to the always-bounded
    version and confirmed a new test
    (`GetService_DoesNotTimeOut_ForOrdinaryContentionLongerThanTheLockTimeout`,
    a 12-second same-row factory delay under concurrent access) failed
    with `TimeoutException` (matching the finding exactly), then restored
    the depth-tracking fix and confirmed it passes; re-ran
    `GetService_ThrowsTimeoutException_RatherThanDeadlocking_OnAConcurrentCrossRowCycle`
    to confirm the original deadlock protection is unaffected (3/3).

## Twelfth PR review round (Codex, #105) findings

27. **P2 — even the nested-only bounded wait (finding 26) couldn't tell a
    genuine cycle apart from ordinary nested contention.** Nesting alone
    doesn't imply a cycle: Row A's factory calling into Row B is nested
    whether or not Row B's own resolution ever calls back into Row A. A
    legitimate nested cross-row call blocked behind a *different*,
    independently slow caller already inside Row B would still hit the
    fixed timeout, even with no cycle anywhere. Replaced the timeout
    entirely with real wait-for-cycle detection: `ComponoServiceProvider`
    now tracks (in two static maps guarded by one lock) which thread owns
    each adapter's lock and which adapter each thread is currently
    blocked trying to acquire; before blocking on a lock another thread
    owns, it walks that chain and refuses immediately with a diagnosed
    `CompositionException` only if the walk leads back to the calling
    thread. Every other wait - including a legitimately slow nested
    cross-row call - is now unbounded (no timeout anywhere in this path).
    Caught a real bug in my own first draft while writing this: seeding
    the walk's visited-set with the current thread (to bound the walk)
    let the dedup check silently swallow the one case that mattered,
    since the self-match would never run - fixed by checking
    `probe == thisThread` before the dedup guard, not after. Also had to
    fix the original deadlock regression test itself: it used a
    `Barrier(2)`, which hangs when the losing thread's row retries its
    factory after the winner's cycle-refusal releases its lock (a second,
    unmatched participant for that phase) - replaced with idempotent
    `ManualResetEventSlim`s. Verified three ways: the original two-row
    cycle now refuses in well under a second instead of after a fixed
    wait; the same-row slow-contention test (finding 26) still passes;
    and a new test proving the exact scenario this finding described - a
    nested cross-row call blocked 12 seconds behind an unrelated,
    non-cyclic slow caller in the target row - now succeeds instead of
    timing out. Also reverted to a plain `lock` and confirmed the cycle
    test hangs (5/5) without the fix, restoring it and confirming success.
    Recorded as ADR-0047 Amendment 6.
28. **P2 — ADR-0047 never recorded the nested-only-timeout behavior from
    finding 26**, only this plan's Notes did. Superseded by Amendment 6
    above, which records both finding 26 and finding 27 together since
    finding 26's fix was itself superseded before merge.
