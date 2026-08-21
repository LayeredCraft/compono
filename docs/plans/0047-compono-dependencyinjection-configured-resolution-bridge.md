# [PLAN-0047] Compono.DependencyInjection: Configured-Resolution IServiceProvider Bridge

**Status:** In Progress

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
  - [x] Add `Microsoft.Extensions.DependencyInjection.Abstractions` package
        reference (the one new external dependency this package needs,
        per ADR-0047). Pinned `8.0.2` in `Directory.Packages.props` (this
        repo's lowest supported TFM's release line; forward-compatible with
        net9.0/net10.0/net11.0 too).
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
      provider) still throws `CompositionException`, not `false`. Covered
      both shapes: a throwing registration factory (wrapped, diagnosed
      `CompositionException`) and a throwing stage-4-6 provider (per
      ADR-0024, propagates uncaught as the provider's own exception type).

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
- [ ] Update `docs/roadmap/post-mvp.md`: remove the `Compono.DependencyInjection`
      bullet from "Current state" once shipped (following the exact
      pattern passes 2-6 already established — "this finding is no longer
      listed here"). **Deliberately left unchecked**: every prior "no
      longer listed here" entry in this doc was written after the shipping
      PR actually merged (some cite a real merged PR number). This plan's
      code isn't committed/merged yet (per explicit instruction: hold for
      review before commit/push) - claiming "shipped" in the roadmap doc
      now would be inaccurate. Do this edit as part of (or immediately
      after) the merging PR, not before.

### Packaging verification

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
`Compono.DependencyInjection.Tests` (8/8), and every other existing test
project in the solution (Bogus, Generators, NSubstitute, TUnit,
TestDoubles, XunitV3 - all green), then a real `dotnet pack` → local feed
→ packaged-consumer `dotnet run` proving `row.AsServiceProvider()` works
from an actual restored NuGet package, not just an in-repo
`ProjectReference`.

Not yet done, deliberately: committing/pushing this work (holding for
review, per explicit instruction) and the `docs/roadmap/post-mvp.md`
"no longer listed here" edit (deferred until the merging PR, so the doc
never claims "shipped" before it's true).
