# [PLAN-0037] netstandard2.1 Compatibility Floor

**Status:** Superseded by [PLAN-0038](0038-net8-net9-explicit-multi-target.md) — see Notes

**Implements:** [ADR-0037](../adr/0037-netstandard2.1-compatibility-floor.md)

## Goal

All four packages (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
`Compono.Bogus`) build and restore cleanly targeting `netstandard2.1`, in
addition to their existing `net10.0;net11.0`, with the packed-consumer
smoke test proving a real `net8.0` **and** `net9.0` consumer can restore
the packed NuGet artifacts, select the `netstandard2.1` asset, compile,
and exercise Compono successfully.

## Scope

In scope, per [ADR-0037](../adr/0037-netstandard2.1-compatibility-floor.md#decision-outcome):

- Add `netstandard2.1` to `TargetFrameworks` on all four packages'
  `.csproj` files.
- Shim the three identified BCL/language gaps in `Compono`'s own source:
  `Random.Shared` (manual `#if`-guarded, lock-based fallback — see Tasks
  for why not `ThreadLocal<Random>`), `ArgumentNullException.ThrowIfNull`
  (`Polyfill` package), `required` members (`PolySharp` package).
- Compile-check `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`'s
  own source against `netstandard2.1` as an explicit audit, not an
  assumption that `Compono`'s three known gaps are exhaustive — see Tasks.
- Update CI (`package-validation.yaml`, `docs.yml` if it assumes a single
  TFM) and packaging validation to cover the new TFM leg, via the packed
  local-feed artifacts (not project references) against real `net8.0` and
  `net9.0` consumer projects.
- Update `docs/packages/index.md`'s TFM claim, plus any other doc making
  the same "net10.0/net11.0 only" statement, to current state.
- Add a `references/security.md` note for the two new source-only
  compile-time dependencies (`PolySharp`, `Polyfill`), per ADR-0037's
  Negative Consequences.

Explicitly deferred (not in this plan):

- Any TFM below `netstandard2.1` (e.g. `netstandard2.0`, `.NET Framework`) —
  not requested, not evidenced by a real blocked consumer.
- Widening `Compono.Generators`'s target — already `netstandard2.0`,
  unaffected by this decision (ADR-0037's Decision Outcome).
- Verifying the `structured-logging` repo specifically — this plan makes
  the package installable there; actually dogfooding in that repo is a
  separate follow-up, not this plan's deliverable.

**If the integration-package compatibility audit (Tasks, below) surfaces
BCL/language gaps beyond the three already identified in `Compono`, stop
and evaluate rather than reflexively adding more shims** — per ADR-0037's
"the floor is cheap, not a design target" principle, an unexpectedly large
shim surface is a reason to revisit whether `netstandard2.1` is still the
right floor, not a problem to route around silently.

## Tasks

**Core package changes**

- [ ] `src/Compono/Compono.csproj` — add `netstandard2.1` to `TargetFrameworks`.
- [ ] `src/Compono/Compono.csproj` — add `PolySharp` (`PrivateAssets="all"`,
      compile-time only) and `Polyfill` (`PrivateAssets="all"`, compile-time
      only) package references, each scoped to `netstandard2.1` only via
      an `Condition="'$(TargetFramework)' == 'netstandard2.1'"` `ItemGroup`
      so `net10.0`/`net11.0` builds carry neither dependency.
- [ ] `src/Compono/CompositionSeed.cs` — guard the two `Random.Shared` call
      sites with `#if NET6_0_OR_GREATER` / `#else`. Fallback is a single
      shared `Random` instance guarded by a `lock`, not `ThreadLocal<Random>`:
      `Generate()`/`GenerateRowSeed()` are each called once per root
      `Composer.Create`/`CreateRow` call, not per element (`CreateMany`'s
      per-item forking goes through `CompositionSeed.Fork`'s deterministic
      FNV-1a combine, no `Random` involved) — so lock contention isn't a
      realistic concern, and a locked single instance is simpler to reason
      about and test than per-thread state with its own lifecycle. Revisit
      only if profiling later shows real contention.
- [ ] Confirm the ~20 `ArgumentNullException.ThrowIfNull` call sites and
      the `required` member declarations compile unmodified against
      `netstandard2.1` once the two polyfill packages are referenced (no
      source change expected beyond the `Random.Shared` guard above — this
      is a verification task, not an authoring one).

**Integration packages**

- [ ] `src/Compono.XunitV3/Compono.XunitV3.csproj` — add `netstandard2.1`.
- [ ] `src/Compono.NSubstitute/Compono.NSubstitute.csproj` — add `netstandard2.1`.
- [ ] `src/Compono.Bogus/Compono.Bogus.csproj` — add `netstandard2.1`.
- [ ] Verify each package's third-party dependency
      (`xunit.v3.extensibility.core`, `NSubstitute`, `Bogus`) actually
      restores and resolves under `netstandard2.1` locally, not just per
      the NuGet-listing check already done during design (ADR-0037's
      Decision Outcome records the listing check; this is the real local
      build/restore verification).
- [ ] **Explicit compatibility audit**: compile `Compono.XunitV3`'s,
      `Compono.NSubstitute`'s, and `Compono.Bogus`'s own source against
      `netstandard2.1` and inspect the result — don't just rely on the
      build happening to succeed as a side effect of the TFM add. Record
      any additional net6.0+-only BCL/language usage found beyond the
      three gaps already identified in `Compono`. If anything is found,
      stop and evaluate per the Scope section's escape-hatch note before
      adding another shim.

**CI / packaging**

- [ ] `.github/workflows/package-validation.yaml` — extend the
      packed-consumer smoke test with `netstandard2.1` coverage: a
      consumer project targeting `net8.0` and a second targeting `net9.0`,
      each referencing the **packed local-feed NuGet artifacts** (matching
      the existing smoke test's pattern — not `ProjectReference`), so the
      test proves what a real consumer actually receives, not just that
      the source compiles. Both TFMs if CI cost is reasonable, per the
      review discussion; drop to one only if runtime cost turns out to be
      prohibitive, and note why here if so.
- [ ] `.github/workflows/docs.yml` — confirm its "build for net10.0" API
      doc-generation step doesn't need a `netstandard2.1` counterpart (it
      generates one canonical API reference; check whether the new TFM's
      public surface differs at all before assuming no doc regen is
      needed).
- [ ] Confirm `dotnet pack` output includes all three TFMs' assemblies in
      the resulting `.nupkg` for all four packages.

**Docs**

- [ ] `docs/packages/index.md` — replace the forward-reference added
      during design with the actual current-state TFM list.
- [ ] `docs/getting-started/installation.md` — check for a TFM claim that
      needs the same update.
- [ ] `docs/packages/compono.md`, `compono-xunitv3.md`,
      `compono-nsubstitute.md`, `compono-bogus.md` — check each Package
      Guide's own TFM statement.
- [ ] `docs/roadmap/post-mvp.md` — no change expected (this finding didn't
      originate from the ADR-0029 dogfooding framework), but confirm no
      stale cross-reference is introduced.
- [ ] `references/security.md` (engineering-workflow skill) — add the
      `PolySharp`/`Polyfill` compile-time-only dependency note per
      ADR-0037's Negative Consequences.
- [ ] `skills/compono/SKILL.md` and any package-TFM claim in
      `skills/compono/references/*.md` — check for staleness.

**Tests**

- [ ] `test/Compono.Tests` — add coverage for the `Random.Shared` lock-based
      fallback path (the `netstandard2.1`-only branch can't be exercised by
      a `net10.0`/`net11.0` test run; needs either a `netstandard2.1`-targeted
      test leg or a way to unit-test the fallback logic directly regardless
      of the host TFM — decide the concrete mechanism during implementation
      and record it here).
- [ ] Full solution test run passes on `net10.0`/`net11.0` unchanged (no
      behavior change expected on the primary TFMs).

## Critical Files

- `src/Compono/Compono.csproj` — add `netstandard2.1`, `PolySharp`/`Polyfill` refs (netstandard2.1-only).
- `src/Compono/CompositionSeed.cs` — `#if NET6_0_OR_GREATER`-guarded `Random.Shared` fallback (lock-based, not `ThreadLocal<Random>`).
- `src/Compono.XunitV3/Compono.XunitV3.csproj`, `src/Compono.NSubstitute/Compono.NSubstitute.csproj`, `src/Compono.Bogus/Compono.Bogus.csproj` — add `netstandard2.1`.
- `.github/workflows/package-validation.yaml` — new `net8.0`/`net9.0` packed-consumer legs against the `netstandard2.1` asset.
- `docs/packages/index.md` and per-package guides — TFM claim updates.
- `.claude/skills/engineering-workflow/references/security.md` — new compile-time dependency note.

## Test Plan

- Existing `net10.0`/`net11.0` test suites run unchanged — this plan must
  not regress primary-TFM behavior.
- New: a test path exercising the `netstandard2.1`-only `Random.Shared`
  lock-based fallback (mechanism TBD during implementation — see Tasks).
- New: packed-consumer smoke test extended to `net8.0` and `net9.0`
  consumer projects referencing the packed local-feed artifacts (not
  project references) and selecting the `netstandard2.1` asset — this is
  the test that actually proves the original blocker (`structured-logging`,
  .NET 8/9) is fixed, end to end, not just that `Compono` compiles for the
  TFM in isolation.

## Notes

Design context: this plan followed a deep-dive design conversation
(2026-08-10) that also considered explicit `net8.0;net9.0` multi-targeting
instead of a `netstandard2.1` floor — rejected because it only covers two
named TFMs rather than current and future .NET implementations that
support .NET Standard 2.1, and reads as contradicting ADR-0031's "never
one release behind" framing more directly than an independent floor TFM
does. See ADR-0037's Pros and Cons for the full comparison.

Review refinements (2026-08-10, before implementation started): switched
the `Random.Shared` fallback from `ThreadLocal<Random>` to a single
lock-guarded `Random` instance (simpler, no per-thread lifecycle, seed
generation isn't a hot path); added the explicit integration-package
compatibility audit task rather than assuming `Compono`'s three known
gaps are exhaustive; corrected ADR-0037's build-output count; tightened
"any future in-support release" wording to avoid implying a Microsoft
support-policy guarantee; confirmed the packed-consumer smoke test covers
both `net8.0` and `net9.0` against the real packed artifacts.

**Superseded (2026-08-10):** the Tasks list above ("Core package changes",
"Integration packages") was executed as an implementation-time audit,
exactly as scoped — and it found real gaps beyond the three known ones.
`NullabilityInfoContext` turned out fine (`Meziantou.Polyfill` covers it,
confirmed by a real build) and `FrozenDictionary`/`FrozenSet` would have
been a cheap fallback, but `DateOnly`/`TimeOnly` (whole public composable
types, not shimmable) and `Enum.GetValuesAsUnderlyingType` (would have
reintroduced the exact reflection path ADR-0001/PR #11 rejected, scoped to
`netstandard2.1` only) were not narrow-and-shimmable the way ADR-0037
assumed before this audit ran. Per this plan's own Scope-section escape
hatch ("stop and evaluate rather than reflexively adding more shims"),
implementation stopped there and the direction was re-decided:
[ADR-0038](../adr/0038-net8-net9-explicit-multi-target.md) replaces the
`netstandard2.1` floor with an explicit `net8.0`/`net9.0` multi-target,
which needs zero shims for any gap found (verified: the full solution
builds clean across all four TFMs with zero `#if` guards and zero polyfill
dependencies). All source-level scratch changes made during this audit
(the `#if` guards, the temporary `netstandard2.1` TFM additions, the
temporary `Meziantou.Polyfill` references) were reverted before
[PLAN-0038](0038-net8-net9-explicit-multi-target.md) started fresh. This
plan is left as-is otherwise — an accurate record of what was scoped, and
why it changed, at the time.
