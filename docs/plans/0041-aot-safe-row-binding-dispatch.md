# [PLAN-0041] AOT-Safe Row-Binding Dispatch

**Status:** Not Started

**Implements:** [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)

## Goal

`Compono.XunitV3` and `Compono.TUnit` both dispatch into `CompositionRow`'s
generic `Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>` methods through a
generator-emitted `RowInvokerCache<T>` in core `Compono` — no
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
- `RowInvokerCache<T>` added to core `Compono`.
- `Compono.Generators` extended to emit `RowInvokerCache<T>` registrations
  for every parameter type discovered through either attribute family
  (`ComposeMethodDiscovery`'s existing `TransformMethod` walk).
- `Compono.XunitV3.Binding.RowInvokers` migrated to look up
  `RowInvokerCache<T>` instead of building delegates via reflection.
- `Compono.TUnit.Binding.RowInvokers` (PLAN-0040 Phase 0, not yet merged)
  built against `RowInvokerCache<T>` from the start — this plan's own work
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
  doesn't exist in `Compono.TUnit` yet (Phase 1 hasn't been built); left
  for that phase's own design pass.
- Any change to `BindingPlan.ValidateSignature`, `ComposeMethodDiscovery`'s
  discovery logic, or either package's binding algorithm/seed policy/
  diagnostics behavior — this plan changes *how* dispatch delegates get
  built, not what they do.

## Tasks

- [ ] **Core**: add `src/Compono/RowInvokerCache.cs` —
      `RowInvokerCache<T>` with three settable static fields (`Resolve`,
      `ResolveShared`, `ShareExplicit`), mirroring `PlanCache<T>`'s own
      shape and XML-doc reasoning (closed-generic-static-field, populated
      by a generated module initializer, public setter for the same
      cross-assembly reason `PlanCache<T>.Instance` has one). Define the
      three delegate types (`RowInvokerResolve<T>`, `RowInvokerResolveShared<T>`,
      `RowInvokerShareExplicit<T>`, or equivalent) alongside it, matching
      `Compono.XunitV3.Binding.RowInvokers`' existing non-generic delegate
      shapes' *intent* but now generic in `T` since they live in core and
      must work for any composed type.
- [ ] **Generator**: extend `Compono.Generators`' per-discovered-type
      emission (wherever `PlanCache<T>.Instance = ...` is currently
      emitted) to also emit `RowInvokerCache<T>.Resolve/.ResolveShared/
      .ShareExplicit = static (...) => ...` for every distinct parameter
      type `ComposeMethodDiscovery.TransformMethod` sees — including
      built-in-composable types that never get a `PlanCache<T>` entry,
      since `TransformMethod` already iterates `method.Parameters`
      unconditionally regardless of whether a plan is needed.
- [ ] `Compono.Generators.Tests`: a snapshot test proving a `[Compose]`-
      reachable parameter type gets a real emitted `RowInvokerCache<T>`
      registration, not just a `PlanCache<T>` one — covering the
      built-in-type case (e.g. `string`/`int`) specifically, since that's
      the case with no existing `PlanCache<T>` precedent to lean on.
- [ ] **`Compono.XunitV3`**: rewrite `Binding/RowInvokers.cs` to look up
      `RowInvokerCache<T>`'s fields instead of building them via
      `MakeGenericMethod`/`Delegate.CreateDelegate` — `System.Reflection`
      drops out of this file entirely. `BindingPlan`'s own call sites into
      `RowInvokers.Build` stay the same shape (same return type), so this
      is contained to `RowInvokers.cs` and whatever thin adapter is needed
      to bridge core's now-generic delegate types to the package's own
      existing non-generic ones (or drop the non-generic wrapper shapes
      entirely if nothing else needs them — implementation's call).
- [ ] `Compono.XunitV3.Tests`: existing `RowInvokers`/binding tests still
      pass unmodified in behavior (same public dispatch outcomes) — add
      one test proving no `MakeGenericMethod`/reflection-based path
      remains reachable (e.g. asserting the built delegates are the exact
      cached `RowInvokerCache<T>` instances, not freshly reflected ones).
- [ ] **`Compono.TUnit`** (coordinated with PLAN-0040 Phase 0's branch,
      not yet merged): `Binding/RowInvokers.cs` built directly against
      `RowInvokerCache<T>` from its first commit — never a
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
      consumer, not just a design argument. Investigate whether this needs
      its own CI job (Native AOT publish is RID-specific, slower, and
      typically Linux-only in this repo's existing CI) or can fold into
      `package-validation.yaml`'s existing local-feed smoke-test step.
- [ ] Document the outcome — whichever way the AOT verification lands —
      in this plan's own Notes section: confirmed AOT-safe end-to-end, or
      a specific remaining gap (e.g. the deferred `[Shared]`-detection
      reflection turns out to matter after all) recorded honestly rather
      than assumed away.
- [ ] `docs/packages/compono-tunit.md` (once PLAN-0040 Phase 0 actually
      merges): note Native AOT support explicitly, backed by this plan's
      real verification — not asserted without it.

## Critical Files

- `src/Compono/RowInvokerCache.cs` — new.
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` — extended
  emission, alongside the existing `PlanCache<T>` module-initializer code.
- `src/Compono.XunitV3/Binding/RowInvokers.cs` — reflection removed,
  rewritten to look up `RowInvokerCache<T>`.
- `src/Compono.TUnit/Binding/RowInvokers.cs` — PLAN-0040 Phase 0's own
  file, built against `RowInvokerCache<T>` from the start (coordinated
  with that plan's branch).
- `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs` (or a new
  sibling file) — the `RowInvokerCache<T>` emission snapshot test.
- `test/Compono.TUnit.SampleTests/` — the real Native AOT publish-and-run
  proof.

## Test Plan

- `Compono.Generators.Tests`: snapshot proof of real `RowInvokerCache<T>`
  emission, including the built-in-type case.
- `Compono.XunitV3.Tests`: existing binding/dispatch tests unchanged in
  outcome; one new test proving no reflection-based path remains
  reachable.
- `Compono.TUnit.Tests`/`Compono.TUnit.SampleTests`: PLAN-0040's own
  binding-plan and packaged-consumer tests, adjusted to the new
  `RowInvokers` shape rather than duplicated.
- A real `dotnet publish -p:PublishAot=true` + run — the actual
  deliverable this whole plan exists to produce, not optional polish.

## Notes

Nothing yet — this plan was just drafted, alongside ADR-0041, before any
implementation started.
