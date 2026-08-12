# [PLAN-0041] AOT-Safe Row-Binding Dispatch

**Status:** Not Started

**Implements:** [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)

## Goal

`Compono.XunitV3` and `Compono.TUnit` both dispatch into `CompositionRow`'s
generic `Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>` methods through a
generator-populated `RowInvokerRegistry` in core `Compono` — a non-generic,
`Type`-keyed lookup, not a closed-generic static field (see ADR-0041
Amendment 2 for why `RowInvokerCache<T>` as originally drafted couldn't
actually be read from `BindingPlan.Build`). No
`MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` remains anywhere in
either package's binding path. Verified "done" means: both packages' own
`RowInvokers.cs` files no longer import `System.Reflection` for dispatch
purposes, and a real `dotnet publish -p:PublishAot=true` + run of a
`Compono.TUnit`-consuming project succeeds with no
`MissingMetadataException`/trim warning for the composition path.

This plan must land — and PLAN-0040's own Phase 0 PR must build against its
result — before `Compono.TUnit` first merges to `main`, since
`publish-preview.yaml` auto-publishes every packable `src/` project on every
push to `main` (ADR-0041's Context). PLAN-0040's Phase 0 branch
(`feat/plan-0040-phase-0-compono-tunit-skeleton`, PR #73) is held, not
merged, pending this plan.

## Scope

**In scope:**
- `RowInvokerRegistry` added to core `Compono` — `Register`/`TryGet` over
  a `Type` key, holding the three non-generic dispatch delegates
  (`ResolveInvoker`/`ResolveSharedInvoker`/`ShareExplicitInvoker`, moved
  from each package's own local definition into core).
- `Compono.Generators` extended so `ComposeMethodDiscovery.TransformMethod`
  records every method parameter's own type directly (not filtered through
  `TransitiveClosureWalker`'s plan-eligibility walk, which deliberately
  excludes provider-resolved leaf types — ADR-0041 Amendment 2's Flaw 2),
  and emits a `RowInvokerRegistry.Register(...)` call for each distinct one.
- `Compono.XunitV3.Binding.RowInvokers` migrated to look up
  `RowInvokerRegistry` instead of building delegates via reflection.
- `Compono.TUnit.Binding.RowInvokers` (PLAN-0040 Phase 0, not yet merged)
  built against `RowInvokerRegistry` from the start — this plan's own work
  item, coordinated with PLAN-0040's Phase 0 branch rather than duplicated.
- A real Native AOT publish-and-run smoke test proving the composition path
  survives trimming/AOT for a `Compono.TUnit` consumer.

**Explicitly deferred** (per ADR-0041's "smallest maintainable design"
driver — not part of this plan):
- `BindingPlan.cs`'s `ReflectionInfo.GetCustomAttributes(typeof(SharedAttribute),
  false)` `[Shared]`-detection reflection — a different, lower-risk
  category (attribute-presence read, not dynamic code generation). The AOT
  smoke test below covers whether this assumption holds; no redesign is
  planned unless it doesn't.
- `Compono.XunitV3.Binding.ConfigProfileBinder`'s `ConstructorInfo.Invoke`
  (used for `[Compose<TProfile, TConfig>]`'s `TConfig` construction) —
  doesn't exist in `Compono.TUnit` yet; PLAN-0040 Phase 1 carries its own
  explicit AOT-analysis gate for this (see that plan's Phase 1 task list,
  added per ADR-0041 Amendment 1).
- Any change to `BindingPlan.ValidateSignature`, `ComposeMethodDiscovery`'s
  plan-eligibility walk (`ComposedTypeAnalyzer`/`TransitiveClosureWalker`),
  or either package's binding algorithm/seed policy/diagnostics behavior —
  this plan changes *how* dispatch delegates get built and *what additional
  data the generator records alongside its existing walk*, not the walk's
  own plan-eligibility semantics.

## Tasks

- [ ] **Core**: add `src/Compono/RowInvokerRegistry.cs` — the three
      non-generic delegate types this package's `RowInvokers.cs` already
      defines locally today (`ResolveInvoker`/`ResolveSharedInvoker`/
      `ShareExplicitInvoker`: `(CompositionRow, in CompositionRequestDescriptor) -> object?`
      shapes), moved to core, plus a `Register(Type, ResolveInvoker,
      ResolveSharedInvoker, ShareExplicitInvoker)` method and a
      `TryGet(Type, out ResolveInvoker, out ResolveSharedInvoker, out
      ShareExplicitInvoker)` accessor over an internal `Dictionary<Type,
      ...>` (thread-safety: populated once via generated module
      initializers before any test runs, read-only thereafter from
      `BindingPlan.Build`'s perspective - confirm during implementation
      whether a `ConcurrentDictionary` is still warranted for safety against
      concurrent test-assembly-load ordering, or whether module-initializer
      ordering guarantees make a plain `Dictionary` sufficient).
- [ ] **`EditorBrowsable` decision, made deliberately, not by default.**
      Neither `PlanCache<T>` nor `CollectionPlanCache<T>` — the two
      existing "generator infrastructure, not consumer-facing" caches —
      carry `[EditorBrowsable(EditorBrowsableState.Never)]` today; both
      are plain `public static class`, documented via XML comments
      explaining they're populated by generated module initializers.
      Hiding only the new `RowInvokerRegistry` would make it inconsistent
      with its own two closest precedents, not aligned with an existing
      convention. Two legitimate options, pick one explicitly during
      implementation rather than defaulting silently: (a) leave
      `RowInvokerRegistry` undecorated, matching `PlanCache<T>`/
      `CollectionPlanCache<T>` exactly, or (b) apply
      `[EditorBrowsable(Never)]` to all three together, as its own small,
      explicit consistency pass (arguably out of this plan's own
      "row-binding dispatch" scope, and `PlanCache<T>`/
      `CollectionPlanCache<T>`'s public API shape is already shipped, so
      changing their attribution is a lower-risk but real docs/API-surface
      change worth its own line in this plan's Critical Files if chosen).
- [ ] **Generator, discovery**: extend `ComposeMethodDiscovery.TransformMethod`
      (`src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`) to
      record every method parameter's own type (namespace/name/emitted
      fully-qualified name — the same shape `DiscoveredTypeInfo` already
      carries) directly from the `foreach (var parameter in
      method.Parameters)` loop that's already there, independent of
      `ComposedTypeAnalyzer.Analyze`'s plan-eligibility result. Thread this
      through as a new field alongside the existing `TransitiveClosureResult`
      (new result model or an added field — implementation's call) rather
      than folding it into `.Types`, which stays exactly what it always
      was (a plan-generation worklist, not a complete parameter-type
      inventory - ADR-0041 Amendment 2's Flaw 2 is specifically about not
      repeating that conflation).
- [ ] **Generator, emission**: extend `ComponoIncrementalGenerator.cs`'s
      existing per-discovered-type emission to also emit
      `RowInvokerRegistry.Register(typeof(T), static (row, descriptor) =>
      row.Resolve<T>(descriptor), ..., ...)` for every distinct parameter
      type the new discovery field above collects, deduplicated across the
      whole compilation - including types that never get a `PlanCache<T>`/
      `CollectionPlanCache<T>` entry (built-in-composable types, since
      dispatch registration and plan generation are now two independently-
      populated concerns).
- [ ] `Compono.Generators.Tests`: a snapshot test proving a `[Compose]`-
      reachable **provider-resolved leaf type** (e.g. `string`) gets a real
      emitted `RowInvokerRegistry.Register` call, not just a
      `PlanCache<T>`-needing custom type - this is the exact case
      ADR-0041 Amendment 2's Flaw 2 found unregistered, so the test must
      cover precisely that gap, not just repeat the existing generated-plan
      snapshot coverage.
- [ ] **`Compono.XunitV3`**: rewrite `Binding/RowInvokers.cs` to call
      `RowInvokerRegistry.TryGet(parameterType, ...)` instead of building
      delegates via `MakeGenericMethod`/`Delegate.CreateDelegate` —
      `System.Reflection` drops out of this file entirely. Decide (and
      document) the failure mode for a `TryGet` miss - should be
      unreachable in practice (the generator registers every parameter
      type it discovers), but `BindingPlan`/`RowInvokers` still needs a
      clear `CompositionException` message if it somehow happens, rather
      than a silent null-reference failure.
- [ ] `Compono.XunitV3.Tests`: existing `RowInvokers`/binding tests still
      pass unmodified in behavior (same public dispatch outcomes) — add a
      test proving no `MakeGenericMethod`/reflection-based path remains
      reachable, and one covering a provider-resolved leaf parameter type
      specifically (the case that was previously unregistered under the
      original, now-superseded design).
- [ ] **`Compono.TUnit`** (coordinated with PLAN-0040 Phase 0's branch,
      not yet merged): `Binding/RowInvokers.cs` built directly against
      `RowInvokerRegistry` from its first commit — never a
      `MakeGenericMethod` version to later replace. PLAN-0040 Phase 0's
      own binding-plan unit tests (already written against the
      reflection-based version on PR #73's branch) get adjusted to assert
      against the new lookup-based `RowInvokers` instead.
- [ ] **Real Native AOT verification** — extend or add to
      `test/Compono.TUnit.SampleTests` (or a dedicated AOT-only project,
      if `PublishAot=true` conditionally on one TFM proves awkward to
      fold into the existing packaged-consumer project): `dotnet publish
      -c Release -p:PublishAot=true`, then run the published native
      executable, for a real `[Compose]`-composed test — proving the
      composition path survives trimming/AOT for a real `Compono.TUnit`
      consumer, not just a design argument. Cover both a custom composed
      type (a `PlanCache<T>`-backed one) and a provider-resolved leaf
      parameter type (e.g. `string`), since those are now two genuinely
      different code paths through `RowInvokerRegistry`. Investigate
      whether this needs its own CI job (Native AOT publish is
      RID-specific, slower, and typically Linux-only in this repo's
      existing CI) or can fold into `package-validation.yaml`'s existing
      local-feed smoke-test step.
- [ ] Document the outcome — whichever way the AOT verification lands —
      in this plan's own Notes section: confirmed AOT-safe end-to-end, or
      a specific remaining gap (e.g. the deferred `[Shared]`-detection
      reflection turns out to matter after all) recorded honestly rather
      than assumed away.
- [ ] `docs/packages/compono-tunit.md` (once PLAN-0040 Phase 0 actually
      merges): note Native AOT support explicitly, backed by this plan's
      real verification — not asserted without it.

## Critical Files

- `src/Compono/RowInvokerRegistry.cs` — new.
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs` — extended
  to record every parameter's own type, not just plan-eligible ones.
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` — extended
  emission, alongside the existing `PlanCache<T>` module-initializer code.
- `src/Compono.XunitV3/Binding/RowInvokers.cs` — reflection removed,
  rewritten to look up `RowInvokerRegistry`.
- `src/Compono.TUnit/Binding/RowInvokers.cs` — PLAN-0040 Phase 0's own
  file, built against `RowInvokerRegistry` from the start (coordinated
  with that plan's branch).
- `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs` (or a new
  sibling file) — the `RowInvokerRegistry` emission snapshot test,
  covering a provider-resolved leaf type specifically.
- `test/Compono.TUnit.SampleTests/` — the real Native AOT publish-and-run
  proof.

## Test Plan

- `Compono.Generators.Tests`: snapshot proof of real `RowInvokerRegistry`
  emission, including the provider-resolved-leaf-type case Amendment 2
  found missing under the original design.
- `Compono.XunitV3.Tests`: existing binding/dispatch tests unchanged in
  outcome; one new test proving no reflection-based path remains
  reachable, one covering a leaf parameter type.
- `Compono.TUnit.Tests`/`Compono.TUnit.SampleTests`: PLAN-0040's own
  binding-plan and packaged-consumer tests, adjusted to the new
  `RowInvokers` shape rather than duplicated.
- A real `dotnet publish -p:PublishAot=true` + run, covering both a
  custom composed type and a provider-resolved leaf type — the actual
  deliverable this whole plan exists to produce, not optional polish.

## Notes

**PR #74 Codex review (2026-08-12)**: 3 findings, all confirmed real:
- 🐛 (P1): `RowInvokerCache<T>` as originally drafted (a closed-generic
  static field) cannot actually be read from `BindingPlan.Build`, which
  only ever has a runtime `System.Type`, not a compile-time `T` -
  `PlanCache<T>`'s own pattern only works because its caller is generic
  with `T` bound at a real call site, which `BindingPlan` is not.
  Corrected to a non-generic, `Type`-keyed `RowInvokerRegistry` (ADR-0041
  Amendment 2).
- 🐛 (P1): the original design assumed extending the existing
  per-discovered-type emission would cover built-in/provider-resolved
  parameter types "for free," but verified against real source
  (`TransitiveClosureWalker.cs:135-136`, `:224-225`) that
  `TransitiveClosureResult.Types` deliberately excludes exactly those
  types - they never reach the emission point being extended at all.
  Fixed by having `ComposeMethodDiscovery.TransformMethod` separately
  record every parameter's own type, independent of the plan-eligibility
  walk (ADR-0041 Amendment 2).
- ⚠️ (P1): `docs/plans/0040-compono-tunit-package-design.md` still
  explicitly deferred extracting the shared binding cache (lines ~114-117)
  and still labeled `src/Compono.TUnit/Binding/*` as "duplicated pattern,
  not shared" (Critical Files) - directly contradicting ADR-0041's
  decision. Fixed both spots to reflect that `RowInvokers.cs` specifically
  is shared via `RowInvokerRegistry` from the start, while `BindingPlan.cs`
  et al. remain genuinely duplicated per ADR-0040's own binding-logic
  decision (unaffected by ADR-0041).
