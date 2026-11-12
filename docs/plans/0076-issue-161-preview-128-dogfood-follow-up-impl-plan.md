# [PLAN-0076] Issue #161 preview.128 Dogfood Follow-Up (Documentation)

**Status:** Done. Beads epic: `compono-6bg`.

**Implements:** [RESEARCH-0041](../research/0041-issue-161-preview-128-dogfood-follow-up.md)
decisions (maintainer, 2026-11-12). No ADR is created or amended. Every
change is documentation that describes existing, `Accepted` behavior
(ADR-0022, ADR-0056, ADR-0070 item 9, ADR-0072).

## Goal

A consumer who hits any of the four preview.128 findings can find the answer
in the docs:

- the generic `IMeterFactory` or `ILogger` failure leads to `UseMetrics()` or
  `UseLogging()`;
- reading the row's `TConfig` has a low-boilerplate recipe;
- `[Shared]` and `Share<IMeterFactory>()` are presented as alternatives, not
  a pair;
- a failing `ComposeData` row is explained as reducing the reported result
  count.

## Scope

**In scope:** docs, the skill and its evals, and the compiled examples that
back new docs snippets.

**Out of scope** (RESEARCH-0041): any core message or hint mechanism (F1
option 2 or 3), any `TConfig` opt-in API, any change to `UseMetrics()`
sharing, and per-row failure isolation or any change to `ComposeData`
failure behavior. No production code or public API changes. ApiCompat,
pack-consume, and AOT gates are untouched, because no shipped assembly
changes.

One PR, since the changes are small and all trace back to one dogfood
comment.

## Tasks

### D1: Missing provider troubleshooting (F1) (`compono-6bg.1`)

- [x] `docs/troubleshooting/common-errors.md`, "Runtime composition
      failures": add guidance for the exact message naming
      `IMeterFactory` → call `UseMetrics()`, and `ILogger`/`ILogger<T>` →
      call `UseLogging()`. Word it as "if you meant Compono.Metrics or
      Compono.Logging to supply it". Note that supplying the type through
      `Register<T>()`/`UseServiceProvider` is also valid. Cross-link the
      existing Metrics misuse entry.
- [x] Correct the stale `CompositionException` sample in that section so
      it matches the real text built at `CompositionContext.cs:681`. The
      sample reads "No registration, semantic provider, …", but the real
      message includes "configuration rule": "No registration, configuration
      rule, semantic provider, test-double provider, built-in provider, or
      generated plan could satisfy …".
- [x] `docs/packages/compono-metrics.md` and `docs/packages/compono-logging.md`:
      add a one-line "forgot `UseX()`?" pointer to the troubleshooting entry.
- [x] `skills/compono/references/metrics.md` and `logging.md`: the same
      mapping.

### D2: Reading the row's `TConfig` (F2) (`compono-6bg.2`)

- [x] Keep `builder.Register(() => config)` in the profile, plus a composed
      `TConfig` parameter, as the **primary documented recommendation** in
      `docs/cookbook/vary-composed-configuration-per-theory-row.md` and
      `docs/packages/compono-xunitv3.md` (per-row configuration). Leave the
      existing wording as the main path.
- [x] After it, add a short **optional** note: consumers who repeat the line
      across many profiles can write their own base profile, for example
      `ConfigProfile<TConfig>` from RESEARCH-0041. Make clear that this is a
      consumer-side convenience for repeated patterns. It isn't a Compono
      type or API, and Compono won't ship it. Note that a derived profile
      that also calls `Register` for `TConfig` gets the duplicate-registration
      error.
- [x] Back the optional snippet with a compiled test in
      `test/Compono.XunitV3.Tests`, with the base type declared in the test
      project. Add no AOT test unless the snippet appears in AOT docs.
- [x] `skills/compono/references/xunit-v3.md`: keep `Register(() => config)`
      as the recommendation. Mention the base profile only as an optional
      consumer pattern, if it's mentioned at all.

### D3: `[Shared]` vs `Share<IMeterFactory>()` (F3) (`compono-6bg.3`)

- [x] `docs/packages/compono-metrics.md` lead example: remove
      `.Share<IMeterFactory>()` from the profile used with a
      `[Shared] IMeterFactory` parameter, or keep it and explain it. Add a
      short "one or the other" note: `[Shared]` for per-test intent (it's
      resolved first, in any parameter position), `Share<IMeterFactory>()`
      for profile-wide intent, undecorated parameters, and `Composer.Create`
      graphs. Explain why sharing matters: each factory captures only its
      own meters.
- [x] `skills/compono/references/metrics.md` "`Share<T>()` and `[Shared]`":
      replace "Prefer `Share<IMeterFactory>()`" with the same either/or
      guidance.
- [x] Check `docs/migrating-from-microsoft-extensions-diagnostics-testing.md`
      for the same redundant pairing.
- [x] **Don't** add a `Compono.XunitV3` reference to
      `test/Compono.Metrics.Tests`.
- [x] Adapter-level sharing coverage, if warranted: first check whether
      `test/Compono.XunitV3.Tests` already proves that a `[Shared]`
      parameter declared after its dependent, with two nested consumers,
      gets the same instance with no `Share<T>()`. Add a test there only if
      that's missing. Use a stand-in interface and provider; don't pull in
      `Compono.Metrics`.
- [x] Keep metrics lifetime coverage in `test/Compono.Metrics.Tests`, which
      is already in place: `EachComposedRequest_GetsItsOwnFactory_AbsentShared`
      and `Share_MakesMeterFactoryGraphWide_WithinOneRoot`. Add a test there
      only if a new docs claim isn't backed. One candidate: without sharing,
      two nested consumers in one `Composer.Create` graph get different
      factories.

### D4: `ComposeData` failure and result counts (F4) (`compono-6bg.4`)

Wording rule for every item below: describe **whole-theory** failure. The
theory test case fails once, with the first failing row's
`CompositionException`, and the remaining rows aren't executed. Never
suggest that each row failed independently or was reported as a separate
failure.

- [x] `docs/packages/compono-xunitv3.md` "Failures, seeds, and row labels":
      extend the existing "fails as a unit" text.
      - Composition happens when the theory runs, and all rows are built
        before any of them runs. A composition failure in one row therefore
        stops the rest: no row of that theory executes, including rows that
        would have passed.
      - The theory then reports a single failed result instead of one result
        per row, so the suite's reported test count can drop while the
        failure is present.
      - Every data attribute on the same method is affected, including
        `[InlineData]` rows.
      - Once the composition error is fixed, every row runs and is counted
        again.
- [x] `docs/packages/compono-xunitv3-aot.md`: the same explanation. ADR-0070
      item 9 applies to both engines.
- [x] `docs/troubleshooting/common-errors.md`: a symptom entry, "A
      `ComposeData` theory's test count dropped / only one result is
      reported". It covers the cause, the single-result behavior, the fix,
      and a link to the guide section.
- [x] `skills/compono/references/xunit-v3.md`, and the AOT reference if it's
      separate: the same explanation, condensed.

### D5: Skill evals (`compono-6bg.5`)

- [x] `skills/compono/evals/evals.json`: add or update evals for
      (a) "IMeterFactory could not be satisfied" → `UseMetrics()`;
      (b) "do I need both `UseMetrics().Share<IMeterFactory>()` and
      `[Shared]`?" → either one; (c) "my theory count dropped when a row
      failed" → expected, ADR-0070; fix the row. Update any existing eval
      whose expected output prescribes both together.

### D6: Validation (`compono-6bg.6`)

- [x] `dotnet build` — 0 warnings, 0 errors.
- [x] Relevant existing test suites (at least `Compono.XunitV3.Tests`,
      `Compono.Metrics.Tests`, and any project touched) green on every
      supported TFM; full `dotnet test` green.
- [x] `mkdocs build --strict` passes.
- [x] Validate `skills/compono/evals/evals.json`: it parses as JSON, new
      entries match the existing schema, and ids are unique. The repo has
      no dedicated eval validator, so use `jq` or an equivalent.
- [x] Confirm `git diff` shows no `src/` changes. ApiCompat, pack-consume,
      AOT, and package-validation gates don't apply unless that changes.
- [x] Regenerate `docs/reference/api` only if any public XML doc changed
      (none is planned).
- [x] Update this plan's checklist and Notes in the same PR.

## Critical Files

- `docs/troubleshooting/common-errors.md`
- `docs/packages/compono-metrics.md`, `docs/packages/compono-logging.md`
- `docs/packages/compono-xunitv3.md`, `docs/packages/compono-xunitv3-aot.md`
- `docs/cookbook/vary-composed-configuration-per-theory-row.md`
- `docs/migrating-from-microsoft-extensions-diagnostics-testing.md`
- `skills/compono/references/{metrics,logging,xunit-v3}.md`
- `skills/compono/evals/evals.json`
- `test/Compono.XunitV3.Tests`, `test/Compono.Metrics.Tests` (example-backing tests only)

## Test Plan

Only tests that back new docs claims:

- the optional base-profile snippet (D2), in `Compono.XunitV3.Tests`;
- adapter-level `[Shared]` sharing (D3), in `Compono.XunitV3.Tests`, only if
  it isn't already covered;
- metrics lifetime (D3), in `Compono.Metrics.Tests`, only if a new claim is
  unbacked.

No behavior changes, so no regression tests are needed. Existing `ComposeData` failure tests
already cover the as-a-unit contract.

## Notes

- 2026-11-12: plan proposed from RESEARCH-0041. Each finding was reproduced
  in an out-of-repo harness (xUnit 4.0.1, net11.0, JIT). The maintainer
  chose documentation-only for all four.
- 2026-11-12: maintainer review of the proposal made four changes:
  - D4 stays, and is worded as whole-theory failure, not per-row failure.
  - D3 adds no `Compono.XunitV3` dependency to `Compono.Metrics.Tests`;
    adapter coverage goes in `Compono.XunitV3.Tests`.
  - D2 keeps `Register(() => config)` as the primary recommendation, with
    `ConfigProfile<TConfig>` as an optional consumer-side convenience only.
  - D1 includes the stale `CompositionException` sample correction.
- 2026-11-12: maintainer approved the plan and added D6 gates (`mkdocs build
  --strict`, evals.json validation, per-TFM suites, zero warnings).
  - Beads: epic `compono-6bg`; children `.1` (D1) through `.6` (D6).
  - Dependencies: D1, D2, D3, and D4 can run in parallel.
  - D5 is blocked by D1, D3, and D4.
  - D6 is blocked by D1 through D5.
- 2026-11-12: D1-D6 implemented locally (not committed).
  - D1: corrected the runtime sample to include "configuration rule"; added
    the `IMeterFactory`/`ILogger` mapping in `common-errors.md`, the
    `Compono.Metrics`/`Compono.Logging` guides, and the skill references;
    cross-linked the Metrics misuse entry.
  - D2: kept `Register(() => config)` primary; added the optional
    consumer-side `ConfigProfile<TConfig>` note (cookbook + package guide,
    brief skill note), backed by
    `OptionalBaseProfile_RegistersRowConfig_ForAComposedParameter` in
    `DocumentedRecipeTests`.
  - D3: removed the redundant `Share<IMeterFactory>()` from the lead
    `[Shared]` example and added one-or-the-other guidance to the package
    guide and skill. The migration guide needed no change (it already uses
    `[Shared]` without `Share`). Adapter-level coverage was missing, so added
    `SharedParameter_DeclaredAfterDependent_IsSharedAcrossTwoNestedConsumers`;
    added `NestedConsumers_GetIndependentFactories_WithinOneRoot_AbsentShared`
    to back the no-sharing claim.
  - D4: documented the whole-theory failure contract in the xUnit v3 package
    guide, the AOT guide, `common-errors.md`, and the skill reference.
  - D5: added evals 75-77; no existing eval prescribed both sharing
    mechanisms together.
  - D6 validation (macOS arm64): `dotnet build` 0 warnings/0 errors; full
    `dotnet test` 7,642 total, 0 failed, 501 skipped; `Compono.XunitV3.Tests`
    (339 passed per TFM) and `Compono.Metrics.Tests` (105-106 passed per TFM)
    green on net8.0-net11.0; `uv run mkdocs build --strict` passed;
    `evals.json` parses, 77 entries, 0 duplicate ids; `git diff` shows no
    `src/` changes.
- 2026-11-12: review corrections applied (no Beads reopened; epic and
  children remain closed).
  - F1: removed the false "or a catch-all provider resolved first" clause
    from the missing-provider guidance in the skill metrics/logging
    references; a catch-all that resolves prevents that failure, and
    registration order is now called out as a separate concern.
  - F2: strengthened the optional base-profile test — distinct config per
    row, the SUT consumes the composed config (`RetentionPolicy.Window`),
    and the derived profile no longer manually registers `RetentionPolicy`.
  - F3: corrected the duplicate-registration wording in the cookbook to a
    `CompositionException` wrapping a `CompositionConfigurationException`.
  - F4: corrected the `DocumentedRecipeTests` header to separate the
    RESEARCH-0040 recipes (also AOT-proven) from the RESEARCH-0041 optional
    consumer-side base-profile recipe (JIT-only coverage).
  - F5: corrected the `Compono.Metrics` "no props asset" claim in the
    package guide and skill reference — the package ships a
    `build`/`buildTransitive` props asset that defaults
    `ComponoMetricsOwnsMeterFactory` to `true`; consumers can override it
    but don't configure it. Eval 75 no longer asserts unrelated MSBuild
    behavior, and eval 67's ownership-property wording was corrected.
  - Revalidation (macOS arm64): `dotnet build` 0 warnings/0 errors; full
    `dotnet test` 7,642 total, 0 failed, 501 skipped; `Compono.XunitV3.Tests`
    339 passed per TFM and `Compono.Metrics.Tests` 105-106 passed per TFM on
    net8.0-net11.0; `uv run mkdocs build --strict` passed; `evals.json`
    parses with 77 unique ids; `git diff -- src/` empty.
- 2026-11-12: targeted verification approved the plan for commit and PR.
  Final editorial corrections: eval 75 now describes the failure across all
  composition resolution stages, and the metrics docs/skill describe the
  packaged props asset (consumer-overridable) rather than calling it
  internal. Revalidated `evals.json` (77 unique ids) and
  `uv run mkdocs build --strict`; `git diff -- src/` remains empty.
