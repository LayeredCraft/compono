# [PLAN-0041] AOT-Safe Row-Binding Dispatch

**Status:** Not Started

**Implements:** [ADR-0041](../adr/0041-aot-safe-row-binding-dispatch.md)

## Goal

Core `Compono` and `Compono.XunitV3` dispatch into `CompositionRow`'s
generic `Resolve<T>`/`ResolveShared<T>`/`ShareExplicit<T>` methods through a
generator-populated `RowInvokerRegistry` in core `Compono` — a non-generic,
`Type`-keyed lookup, not a closed-generic static field (see ADR-0041
Amendment 2 for why `RowInvokerCache<T>` as originally drafted couldn't
actually be read from `BindingPlan.Build`). No
`MethodInfo.MakeGenericMethod`/`Delegate.CreateDelegate` remains anywhere in
`Compono.XunitV3`'s binding path. Verified "done" means:
`Compono.XunitV3.Binding.RowInvokers` no longer imports `System.Reflection`
for dispatch purposes, and a real `dotnet publish -p:PublishAot=true` + run
proves the mechanism itself — `RowInvokerRegistry` populated by real
generator output, exercised through a real composed-type and a real
provider-resolved-leaf-type parameter — survives trimming/AOT with no
`MissingMetadataException`/trim warning.

**This plan is deliberately scoped to what's buildable and completable on
`main` alone** — see the circular-dependency finding in this plan's Notes.
`Compono.TUnit`'s own use of `RowInvokerRegistry`, and the full
end-to-end Native-AOT proof through the real `Compono.TUnit` package
chain, are PLAN-0040 Phase 0's own tasks (added there), executed once
Phase 0's branch (PR #73) rebases onto this plan's merged result — not
this plan's own deliverable. This plan must land, and merge to `main`,
before `Compono.TUnit` first merges, since `publish-preview.yaml`
auto-publishes every packable `src/` project on every push to `main`
(ADR-0041's Context) — but "must land first" doesn't mean "must itself
contain TUnit-specific work it can't actually complete from `main`."

## Scope

**In scope:**
- `RowInvokerRegistry` added to core `Compono` — `Register`/`TryGet` over
  a `Type` key, holding the three non-generic dispatch delegates
  (`ResolveInvoker`/`ResolveSharedInvoker`/`ShareExplicitInvoker`, moved
  from `Compono.XunitV3.Binding.RowInvokers`' own local definition into
  core).
- A dispatch-eligibility guard, applied wherever the generator decides
  whether to emit a `RowInvokerRegistry.Register(...)` call for a
  parameter type — see "Dispatch-eligibility guard" task below. Required
  because the naive "record every parameter type" approach would try to
  emit `row.Resolve<T>(...)` for shapes that can never legally be a
  generic type argument (a `ref struct` like `Span<int>` — confirmed
  `CompositionRow.Resolve<TValue>()` has no `allows ref struct`
  constraint — or a pointer type, a hard C# language restriction, not
  just a Compono one), which would break the *generated code's own
  compile*, not just fail gracefully at runtime.
- `Compono.Generators` extended so `ComposeMethodDiscovery.TransformMethod`
  records every *dispatch-eligible* method parameter's own type directly
  (not filtered through `TransitiveClosureWalker`'s plan-eligibility walk,
  which deliberately excludes provider-resolved leaf types — ADR-0041
  Amendment 2's Flaw 2), and emits a `RowInvokerRegistry.Register(...)`
  call for each distinct one.
- `Compono.XunitV3.Binding.RowInvokers` migrated to look up
  `RowInvokerRegistry` instead of building delegates via reflection.
- A real Native AOT publish-and-run smoke test proving the
  `RowInvokerRegistry` mechanism itself — populated by real generator
  output — survives trimming/AOT, covering both a custom composed type
  and a provider-resolved leaf type. Scoped to the mechanism, not to
  `Compono.TUnit` specifically (which doesn't exist on `main` yet) — a
  minimal harness or `Compono.XunitV3`-based project is sufficient; it
  doesn't need to prove anything TUnit-specific, since `RowInvokerRegistry`
  itself has no framework-specific code path.

**Assigned to PLAN-0040 Phase 0 instead** (not this plan's own tasks —
see Notes):
- `Compono.TUnit.Binding.RowInvokers` built against `RowInvokerRegistry`
  from its first commit.
- The full end-to-end Native AOT publish-and-run proof through the real
  packaged `Compono.TUnit` dependency chain (`Compono.TUnit.SampleTests`
  or a dedicated AOT-only sibling project).
- `docs/packages/compono-tunit.md`'s Native AOT claim, backed by that
  proof.

**Explicitly deferred** (per ADR-0041's "smallest maintainable design"
driver — not part of this plan):
- `BindingPlan.cs`'s `ReflectionInfo.GetCustomAttributes(typeof(SharedAttribute),
  false)` `[Shared]`-detection reflection — a different, lower-risk
  category (attribute-presence read, not dynamic code generation). This
  plan's own AOT smoke test (`Compono.XunitV3`-based) covers whether this
  assumption holds for that package; PLAN-0040 Phase 0's own AOT proof
  covers it for `Compono.TUnit`'s identical check.
- `Compono.XunitV3.Binding.ConfigProfileBinder`'s `ConstructorInfo.Invoke`
  (used for `[Compose<TProfile, TConfig>]`'s `TConfig` construction) —
  doesn't exist in `Compono.TUnit` yet; PLAN-0040 Phase 1 carries its own
  explicit AOT-analysis gate for this (see that plan's Phase 1 task list,
  added per ADR-0041 Amendment 1).
- Any change to `BindingPlan.ValidateSignature`'s existing checks (generic
  method, `ref`/`out`/`in`, `params`, duplicate `[Shared]`), or either
  package's binding algorithm/seed policy/diagnostics behavior — this
  plan changes *how* dispatch delegates get built and *what additional
  data the generator records*, not those existing semantics. The
  dispatch-eligibility guard above is new *generator-side* logic, not a
  `BindingPlan.ValidateSignature` change — see that task for why a
  runtime-side check is still needed too.

## Tasks

- [ ] **Core**: add `src/Compono/RowInvokerRegistry.cs` — the three
      non-generic delegate types `Compono.XunitV3.Binding.RowInvokers`
      already defines locally today (`ResolveInvoker`/`ResolveSharedInvoker`/
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
- [ ] **Dispatch-eligibility guard (generator-side).** Before recording a
      parameter type for `RowInvokerRegistry` emission, reject shapes that
      cannot legally be a generic type argument to `Resolve<T>()`/etc. at
      all — reuse `ComposedTypeAnalyzer`'s existing root-validity checks
      (`ContainsTypeParameter`, `IsRefLikeType`, "not an `INamedTypeSymbol`
      and not a recognized collection shape") rather than re-deriving them,
      since those are exactly the shapes `ComposedTypeAnalyzer.Analyze`
      already classifies as unusable (`CMP0006`/`CMP0009`/open-generic
      diagnostics) before ever reaching `TransitiveClosureWalker.Walk`.
      Extract them into a small shared helper `ComposedTypeAnalyzer` and
      the new discovery-recording step (task below) can both call, rather
      than duplicating the three checks. A parameter type that fails this
      guard is simply not registered — it was never going to get a working
      generated plan either, and `BindingPlan.ValidateSignature`'s
      existing checks don't currently reject `ref struct`/pointer
      *by-value* parameters (only `ref`/`out`/`in`/`params` *modifiers* -
      confirmed by rereading `BindingPlan.cs`'s `ValidateSignature`), so
      this is a **new, real gap this task also has to close on the
      runtime side**: `Compono.XunitV3.Binding.BindingPlan.ValidateSignature`
      needs its own explicit rejection for a `ref struct`/pointer-typed
      by-value parameter, with a clear `CompositionException` message -
      not a silent `RowInvokerRegistry.TryGet` miss with no useful
      diagnosis. (This was already a latent, undiagnosed gap under the
      old `MakeGenericMethod`-based design too - `MakeGenericMethod` would
      have thrown its own unclear error for the same shapes at runtime.
      This task makes it a clear, intentional diagnostic instead of an
      accidental one, which the old design never had either.)
- [ ] **Generator, discovery**: extend `ComposeMethodDiscovery.TransformMethod`
      (`src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`) to
      record every *dispatch-eligible* (per the guard above) method
      parameter's own type (namespace/name/emitted fully-qualified name —
      the same shape `DiscoveredTypeInfo` already carries) directly from
      the `foreach (var parameter in method.Parameters)` loop that's
      already there, independent of `ComposedTypeAnalyzer.Analyze`'s
      plan-eligibility result. Thread this through as a new field alongside
      the existing `TransitiveClosureResult` (new result model or an added
      field — implementation's call) rather than folding it into `.Types`,
      which stays exactly what it always was (a plan-generation worklist,
      not a complete parameter-type inventory - ADR-0041 Amendment 2's
      Flaw 2 is specifically about not repeating that conflation).
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
      snapshot coverage. A second test proving a `ref struct`/pointer-typed
      parameter produces *no* `RowInvokerRegistry.Register` emission (the
      dispatch-eligibility guard actually excludes it, not just in theory).
- [ ] **`Compono.XunitV3`**: rewrite `Binding/RowInvokers.cs` to call
      `RowInvokerRegistry.TryGet(parameterType, ...)` instead of building
      delegates via `MakeGenericMethod`/`Delegate.CreateDelegate` —
      `System.Reflection` drops out of this file entirely.
- [ ] `Compono.XunitV3.Tests`: existing `RowInvokers`/binding tests still
      pass unmodified in behavior (same public dispatch outcomes) — add a
      test proving no `MakeGenericMethod`/reflection-based path remains
      reachable; one covering a provider-resolved leaf parameter type
      specifically (the case that was previously unregistered under the
      original, now-superseded design); one covering the new
      `ValidateSignature` rejection for a `ref struct`/pointer-typed
      by-value parameter, with its own clear diagnostic message.
- [ ] **Real Native AOT verification, scoped to the mechanism, not to
      `Compono.TUnit`.** A `dotnet publish -c Release -p:PublishAot=true`
      + run, using whatever's already available on `main` — most likely a
      minimal new sample/harness project (or extending an existing
      `Compono.XunitV3.SampleTests`-shaped project if that turns out
      simpler) exercising a real `[Compose]`-composed custom type *and* a
      provider-resolved leaf type (e.g. `string`) through the real,
      generator-populated `RowInvokerRegistry` — proving the shared
      mechanism survives trimming/AOT for real, not as a design argument.
      Investigate whether this needs its own CI job (Native AOT publish is
      RID-specific, slower, and typically Linux-only in this repo's
      existing CI) or can fold into `package-validation.yaml`'s existing
      local-feed smoke-test step.
- [ ] Document the outcome in this plan's own Notes section: confirmed
      AOT-safe end-to-end for the mechanism, or a specific remaining gap
      (e.g. the deferred `[Shared]`-detection reflection turns out to
      matter after all) recorded honestly rather than assumed away.

## Critical Files

- `src/Compono/RowInvokerRegistry.cs` — new.
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `src/Compono.Generators/Discovery/ComposedTypeAnalyzer.cs` — extended
  discovery (a shared dispatch-eligibility helper, and recording every
  eligible parameter's own type, not just plan-eligible ones).
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` — extended
  emission, alongside the existing `PlanCache<T>` module-initializer code.
- `src/Compono.XunitV3/Binding/RowInvokers.cs` — reflection removed,
  rewritten to look up `RowInvokerRegistry`.
- `src/Compono.XunitV3/Binding/BindingPlan.cs` — new explicit rejection
  for `ref struct`/pointer-typed by-value parameters.
- `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs` (or a new
  sibling file) — the `RowInvokerRegistry` emission snapshot tests
  (provider-resolved leaf type included; dispatch-ineligible type
  excluded).
- A new/extended sample project for the real Native AOT publish-and-run
  proof, scoped to the shared mechanism (not `Compono.TUnit`).

## Test Plan

- `Compono.Generators.Tests`: snapshot proof of real `RowInvokerRegistry`
  emission, including the provider-resolved-leaf-type case Amendment 2
  found missing under the original design, and proof that a
  dispatch-ineligible (`ref struct`/pointer) parameter type produces no
  emission at all.
- `Compono.XunitV3.Tests`: existing binding/dispatch tests unchanged in
  outcome; new tests for a reflection-free dispatch path, a leaf
  parameter type, and the new `ValidateSignature` rejection.
- A real `dotnet publish -p:PublishAot=true` + run, covering both a
  custom composed type and a provider-resolved leaf type — the actual
  deliverable this whole plan exists to produce, not optional polish.

## Notes

**PR #74 Codex review, round 1 (2026-08-12)**: 3 findings, all confirmed
real:
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
  explicitly deferred extracting the shared binding cache and still
  labeled `src/Compono.TUnit/Binding/*` as "duplicated pattern, not
  shared" - directly contradicting ADR-0041's decision. Fixed both spots.

**PR #74 Codex review, round 2 (2026-08-12)**: 3 more findings, all
confirmed real:
- 🐛 (P1): the round-1 fix ("record every parameter's own type") didn't
  account for parameter types that can never legally be a generic type
  argument at all — a `ref struct` (e.g. `Span<int>`; confirmed
  `CompositionRow.Resolve<TValue>()` has no `allows ref struct`
  constraint) or a pointer type (`int*`, a hard C# language restriction).
  Naively recording these would make the generator emit
  `RowInvokerRegistry.Register(typeof(Span<int>), (row, d) =>
  row.Resolve<Span<int>>(d), ...)` — code that doesn't compile. Added the
  dispatch-eligibility guard task, reusing `ComposedTypeAnalyzer`'s
  existing root-validity checks, plus a new `BindingPlan.ValidateSignature`
  rejection for the same shapes (a real, previously-undiagnosed gap even
  under the old `MakeGenericMethod` design — it just failed differently).
- 🐛 (P1): this plan's own task list depended on files
  (`src/Compono.TUnit/Binding/RowInvokers.cs`,
  `test/Compono.TUnit.SampleTests/`) that only exist on PR #73's unmerged
  branch, while stating this plan must complete before that PR merges — a
  genuine circular dependency that made this plan uncompletable as
  written if implemented from `main` alone. Rescoped: this plan now
  covers core + generator + `Compono.XunitV3` only (all real on `main`
  today), with its own AOT proof scoped to the shared mechanism rather
  than to `Compono.TUnit`. The `Compono.TUnit`-specific work and the full
  end-to-end AOT proof through the real package move to PLAN-0040 Phase
  0's own task list.
- ⚠️ (P2): ADR-0040's Amendment 1 still named `RowInvokerCache<T>` even
  after ADR-0041 Amendment 2 corrected the mechanism - added ADR-0040
  Amendment 2, a short dated correction pointing at the actual mechanism.
