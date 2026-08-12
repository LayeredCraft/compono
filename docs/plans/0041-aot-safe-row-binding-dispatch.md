# [PLAN-0041] AOT-Safe Row-Binding Dispatch

**Status:** Done

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

- [x] **Core**: add `src/Compono/RowInvokerRegistry.cs`, with an XML-doc
      cross-reference to `docs/architecture/current/generated-plans-and-discovery.md`'s
      broadened collectible-`AssemblyLoadContext`-rooting note (deferred,
      same disposition as `CollectionPlanCache<T>`'s own existing,
      narrower version of this limitation - not a design change this
      plan makes, just documented consistently where a reader would look
      for it). The three non-generic delegate types
      `Compono.XunitV3.Binding.RowInvokers` already defines locally today
      (`ResolveInvoker`/`ResolveSharedInvoker`/`ShareExplicitInvoker`:
      `(CompositionRow, in CompositionRequestDescriptor) -> object?`
      shapes), moved to core, plus a `Register`/`TryGet` pair over a
      **`ConcurrentDictionary<Type, ...>`, using an atomic `GetOrAdd`
      (or equivalent `TryAdd`-shaped idempotent registration), never a
      throwing or blind-overwrite `Register`.** This is a firm design
      requirement, not an implementation detail to decide later - two
      different consuming assemblies loaded into the same process (e.g.
      both composing `string` as a `[Compose]` parameter) will each run
      their own generated module initializer, and unlike `PlanCache<T>`'s
      own cross-assembly collision (an atomic field write, silently
      nondeterministic about *which* assembly's value wins but never
      unsafe - documented, deliberately deferred, in
      `docs/architecture/current/generated-plans-and-discovery.md`'s
      "Cross-assembly plan-cache collision" item), a plain, non-concurrent
      `Dictionary<TKey, TValue>`'s *internal structure* can corrupt under
      genuinely concurrent writes from two module initializers running on
      different threads during assembly load - a strictly worse failure
      mode than "last write wins," not just a variant of it. `GetOrAdd`
      is safe here specifically because every registration for the same
      `Type` is functionally interchangeable regardless of which assembly
      generated it (the emitted lambda is always the same shape,
      `(row, descriptor) => row.Resolve<T>(descriptor)`, for the same
      `T`) - unlike `PlanCache<T>`'s own open "which assembly's plan wins"
      question, there is no real "which one is correct" ambiguity to defer
      here, so this doesn't need its own class-of-problem design
      discussion the way that item does.
- [x] **`EditorBrowsable` decision, made deliberately, not by default.**
      Chose (a): `RowInvokerRegistry` is left undecorated, matching
      `PlanCache<T>`/`CollectionPlanCache<T>` exactly.
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
- [x] **Dispatch-eligibility guard (generator-side).** Before recording a
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
- [x] **Dispatch-eligibility guard, part 2: accessibility.** The three
      shape checks above don't catch every unnameable-in-generated-code
      case — a provider-resolved leaf type (e.g. a `private`/`internal`
      nested interface satisfied by a substitute provider) passes them
      cleanly (`LeafTypeClassifier` says "provider-resolved, no diagnostic
      needed" - true for plan-generation purposes, since a provider-
      resolved type is never constructed by generated code at all today),
      but `RowInvokerRegistry.Register(typeof(T), (row, d) =>
      row.Resolve<T>(d), ...)` still needs to *name* `T` from a top-level,
      file-scoped generated type - exactly the same accessibility-domain
      problem `TransitiveClosureWalker.cs:249-251` already solves for
      collection element/key types via `Compilation.IsSymbolAccessibleWithin`
      (feeding the existing `CMP0012` "Collection element or key type is
      not accessible" diagnostic). Reuse that same
      `IsSymbolAccessibleWithin(type, compilation.Assembly)` check for
      every row-invoker-eligible parameter type - not just collection
      shapes, which is all `CMP0012` currently covers - and add a new
      diagnostic (`CMP0013` or the next free number; a
      `RowInvokerRegistry`-scoped sibling to `CMP0012`, not a reuse of it,
      since the message needs to explain a method-parameter-reachable type
      being unregisterable, not a collection element) for the case this
      guard actually rejects. Confirmed this is a real, previously-
      unchecked gap: no existing accessibility check applies to a
      provider-resolved parameter type today, since it never goes through
      `ConstructorSelector`'s own accessible-constructor filtering (which
      implicitly requires the type itself be reachable) the way an
      ordinary composed type does.
- [x] **Generator, discovery**: extend `ComposeMethodDiscovery.TransformMethod`
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
- [x] **Generator, emission**: extend `ComponoIncrementalGenerator.cs`'s
      existing per-discovered-type emission to also emit
      `RowInvokerRegistry.Register(typeof(T), static (row, descriptor) =>
      row.Resolve<T>(descriptor), ..., ...)` for every distinct parameter
      type the new discovery field above collects, deduplicated across the
      whole compilation - including types that never get a `PlanCache<T>`/
      `CollectionPlanCache<T>` entry (built-in-composable types, since
      dispatch registration and plan generation are now two independently-
      populated concerns).
- [x] `Compono.Generators.Tests`: a snapshot test proving a `[Compose]`-
      reachable **provider-resolved leaf type** (e.g. `string`) gets a real
      emitted `RowInvokerRegistry.Register` call, not just a
      `PlanCache<T>`-needing custom type - this is the exact case
      ADR-0041 Amendment 2's Flaw 2 found unregistered, so the test must
      cover precisely that gap, not just repeat the existing generated-plan
      snapshot coverage. A second test proving a `ref struct`/pointer-typed
      parameter produces *no* `RowInvokerRegistry.Register` emission (the
      dispatch-eligibility guard actually excludes it, not just in theory).
      A third test, beyond this task's own two, additionally proves the
      accessibility half of the guard: an inaccessible (private nested)
      provider-resolved parameter type reports `CMP0013` instead of
      emitting uncompilable generated code.
- [x] `test/Compono.Tests`: a `RowInvokerRegistry`-specific test proving
      `GetOrAdd`-style idempotent registration - two simulated "consuming
      assemblies" (two separate `Register`/`GetOrAdd` calls for the exact
      same `Type` with two distinct-but-functionally-equivalent delegate
      sets) both succeed, neither throws, and `TryGet` afterward returns a
      working (if unspecified-which-one) entry - covering the two-
      consumer-assembly-sharing-one-`Compono`-instance scenario directly,
      not just asserting the API shape compiles.
- [x] **`Compono.XunitV3`**: rewrite `Binding/RowInvokers.cs` to call
      `RowInvokerRegistry.TryGet(parameterType, ...)` instead of building
      delegates via `MakeGenericMethod`/`Delegate.CreateDelegate` —
      `System.Reflection` drops out of this file entirely.
- [x] `Compono.XunitV3.Tests`: existing `RowInvokers`/binding tests still
      pass unmodified in behavior (same public dispatch outcomes) — add a
      test proving no `MakeGenericMethod`/reflection-based path remains
      reachable; one covering a provider-resolved leaf parameter type
      specifically (the case that was previously unregistered under the
      original, now-superseded design); one covering the new
      `ValidateSignature` rejection for a `ref struct`/pointer-typed
      by-value parameter, with its own clear diagnostic message. This
      project doesn't reference `Compono.Generators` as an analyzer
      (`testing.md`'s hand-fake convention, confirmed directly - no
      `RowInvokerRegistration.g.cs`/`CompositionPlan.g.cs` output at all
      even after a clean rebuild), so a new `Fixtures/RowInvokerRegistryFakes.cs`
      module initializer hand-fakes the registrations a real generator
      would emit for every distinct dispatch-eligible parameter type
      `SampleTestMethods` declares - the same disposition as
      `SampleTestMethods.CollectionExhaustionPlan`/`DisposableProfile`
      already hand-faking a generated collection plan/registration.
- [x] **Real Native AOT verification, scoped to the mechanism, not to
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
      local-feed smoke-test step. New `test/Compono.AotSmokeTest/` (a
      throwaway console harness, not a real xUnit v3/TUnit host) - not
      added to `package-validation.yaml`/any CI job in this pass; see
      Notes.
- [x] Document the outcome in this plan's own Notes section: confirmed
      AOT-safe end-to-end for the mechanism, or a specific remaining gap
      (e.g. the deferred `[Shared]`-detection reflection turns out to
      matter after all) recorded honestly rather than assumed away.

## Critical Files

- `src/Compono/RowInvokerRegistry.cs` — new. Also carries the
  `ResolveInvoker`/`ResolveSharedInvoker`/`ShareExplicitInvoker` delegate
  types, moved here from `Compono.XunitV3.Binding.RowInvokers`.
- `src/Compono.Generators/Discovery/ComposeMethodDiscovery.cs`,
  `src/Compono.Generators/Discovery/ComposedTypeAnalyzer.cs` — extended
  discovery (a shared dispatch-eligibility helper covering both shape and
  accessibility, and recording every eligible parameter's own type, not
  just plan-eligible ones).
- `src/Compono.Generators/Models/RowInvokerTypeInfo.cs`,
  `src/Compono.Generators/Models/ComposeMethodDiscoveryResult.cs` — new
  models threading the new discovery field alongside the existing
  `TransitiveClosureResult`.
- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` — new
  `CMP0013` diagnostic for an inaccessible row-invoker-eligible parameter
  type (a `RowInvokerRegistry`-scoped sibling to `CMP0012`'s existing
  collection-element-type check).
- `src/Compono.Generators/Emitters/RowInvokerRegistrationEmitter.cs`,
  `src/Compono.Generators/Templates/RowInvokerRegistration.scriban` — new
  emitter/template for the `RowInvokerRegistry.Register(...)`
  module-initializer registration.
- `src/Compono.Generators/ComponoIncrementalGenerator.cs` — extended
  pipeline, alongside the existing `PlanCache<T>` module-initializer code.
- `src/Compono.XunitV3/Binding/RowInvokers.cs` — reflection removed,
  rewritten to look up `RowInvokerRegistry`.
- `src/Compono.XunitV3/Binding/BindingPlan.cs` — new explicit rejection
  for `ref struct`/pointer-typed by-value parameters.
- `test/Compono.Generators.Tests/CompositionPlanVerifyTests.cs` — the
  `RowInvokerRegistry` emission snapshot tests (provider-resolved leaf
  type, dispatch-ineligible type excluded, inaccessible type/`CMP0013`).
- `test/Compono.Tests/RowInvokerRegistryTests.cs` — new; idempotent
  registration coverage.
- `test/Compono.XunitV3.Tests/RowInvokersTests.cs`,
  `test/Compono.XunitV3.Tests/BindingPlanTests.cs`,
  `test/Compono.XunitV3.Tests/Fixtures/RowInvokerRegistryFakes.cs` (new) —
  reflection-free dispatch coverage, the new `ValidateSignature` rejection,
  and the hand-fake registrations this project needs since it doesn't
  reference `Compono.Generators` as an analyzer.
- `test/Compono.AotSmokeTest/` — new; the real Native AOT publish-and-run
  proof, scoped to the shared mechanism (not `Compono.TUnit`).

## Test Plan

- `Compono.Generators.Tests`: snapshot proof of real `RowInvokerRegistry`
  emission, including the provider-resolved-leaf-type case Amendment 2
  found missing under the original design; proof that a dispatch-ineligible
  (`ref struct`/pointer) parameter type produces no emission at all; proof
  that an inaccessible provider-resolved parameter type produces the new
  accessibility diagnostic instead of uncompilable generated code.
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

**PR #74 Codex review, round 3 (2026-08-12)**: 1 finding, confirmed real:
- 🐛 (P1): the round-2 dispatch-eligibility guard (shape-only: open
  generic, `ref struct`, non-`INamedTypeSymbol`-non-collection) didn't
  cover accessibility — a `private`/`internal` nested interface satisfied
  by a provider passes `LeafTypeClassifier`'s "provider-resolved, no
  diagnostic" check cleanly (true for plan-generation purposes, since a
  provider-resolved type is never constructed by generated code today),
  but `RowInvokerRegistry.Register(typeof(T), ...)` still needs to *name*
  `T` from a top-level, file-scoped generated type - the exact
  accessibility-domain problem `TransitiveClosureWalker.cs:249-251`
  already solves for collection element/key types via
  `Compilation.IsSymbolAccessibleWithin`, feeding the existing `CMP0012`
  diagnostic. Confirmed no existing check applies to a provider-resolved
  *parameter* type today (it never goes through `ConstructorSelector`'s
  own accessible-constructor filtering the way an ordinary composed type
  does). Extended the dispatch-eligibility guard task to reuse the same
  `IsSymbolAccessibleWithin` check, feeding a new, `RowInvokerRegistry`-
  scoped diagnostic (a sibling to `CMP0012`, not a reuse of it).

**PR #74 Codex review, round 4 (2026-08-12)**: 1 finding, confirmed real,
different category from rounds 1-3 (not "this design doesn't work" - "this
design has the same already-accepted tradeoff `CollectionPlanCache<T>`
already documents, now broader in scope"):
- ⚠️ (P2): `RowInvokerRegistry`'s plain `Dictionary<Type, ...>` has no
  closed-generic-instantiation home-context tie the way `PlanCache<T>`/
  `CollectionPlanCache<T>` do, so it roots every registered parameter
  type's generated dispatch delegate (and the generating assembly)
  permanently - broader than `CollectionPlanCache<T>`'s own documented
  limitation, which is scoped only to collections whose type arguments
  are entirely BCL types. Same disposition as that existing, deliberately
  deferred limitation (documented in
  `docs/architecture/current/generated-plans-and-discovery.md`, extended
  to name `RowInvokerRegistry`): neither `docs/mvp.md`'s scope nor
  Compono's primary test-runner consumers currently exercise collectible-
  ALC hosting, so this is recorded, not redesigned around, consistent
  with ADR-0041's own "smallest maintainable design" driver.

**PR #74 Codex review, round 6 (2026-08-12)**: 2 findings, both confirmed
real:
- 🐛 (P1): the round-1/round-4 design left cross-assembly registration
  semantics unspecified ("confirm during implementation whether a
  `ConcurrentDictionary` is still warranted"). Verified this is worse than
  `PlanCache<T>`'s own already-documented, already-deferred cross-assembly
  collision: a plain static field write is atomic/safe under concurrent
  module-initializer execution even though nondeterministic about which
  assembly's value wins, but a non-concurrent `Dictionary<TKey, TValue>`'s
  *internal structure* can corrupt under genuinely concurrent writes -
  strictly worse than "last write wins." Firmed this into a real design
  requirement: `ConcurrentDictionary` + atomic `GetOrAdd` (never a
  throwing or blind-overwrite `Register`) - safe here specifically because
  every registration for the same `Type` is functionally interchangeable
  regardless of source assembly, unlike `PlanCache<T>`'s own genuine
  "which plan is correct" ambiguity. Added a dedicated `Compono.Tests`
  task covering two consumer assemblies sharing one `Compono` instance.
- ⚠️ (P2): `docs/architecture/current/generated-plans-and-discovery.md` is
  the shipped-state reference (per `design-decisions.md`'s own rule -
  `docs/*.md` describes current state, an ADR/plan describes a decision),
  but round 4's edit described `RowInvokerRegistry` in plain present tense
  even though PLAN-0041 is `Not Started` - readers would have no way to
  tell this paragraph describes a `Not Started` plan's design, not
  `Compono`'s actual current dispatch mechanism. Marked it explicitly as
  planned/not-yet-implemented, with a note to rewrite it in plain present
  tense once PLAN-0041 actually ships (matching `tasks/implement.md`'s own
  step 6 convention for exactly this kind of doc update).

**PR #74 Codex review, round 7 (2026-08-12)**: 2 findings, both confirmed
real — both about this plan's own decisions not yet being reflected back
into the ADR that's supposed to be authoritative:
- 🐛 (P1): round 6's idempotent-registration requirement
  (`ConcurrentDictionary` + atomic `GetOrAdd`) only existed in this plan,
  not in ADR-0041 itself — an implementer reading only the accepted ADR
  (the actual decision record) would have found an unconstrained
  `Dictionary`/`Register` sketch and could legitimately have built
  something incompatible with what this plan requires. Added ADR-0041
  Amendment 3, and updated the architecture doc's still-`Dictionary<Type,
  ...>`-named planned paragraph to match.
- ⚠️ (P2): ADR-0041's own Amendment 1 still directed Phase 1 to "extend
  PLAN-0041's smoke test" for `[Compose<TProfile, TConfig>]` — the same
  stale instruction round 5 already corrected in PLAN-0040's own copy,
  but never fixed in the ADR that instruction originally came from.
  Amendment 3 corrects it, redirecting to PLAN-0040 Phase 0's own
  dedicated `Compono.TUnit` AOT harness.

## Implementation (2026-08-12)

**Confirmed AOT-safe end-to-end for the mechanism.** All tasks above are
complete: `RowInvokerRegistry` in core `Compono`, the generator's
dispatch-eligibility guard (shape + accessibility, `CMP0013`) and
discovery/emission, `Compono.XunitV3.Binding.RowInvokers`/`BindingPlan`
migrated off `MakeGenericMethod`/`Delegate.CreateDelegate` entirely, and a
real `dotnet publish -c Release -f net10.0 -r osx-arm64 -p:PublishAot=true
--self-contained true` + run against `test/Compono.AotSmokeTest/` — a new,
throwaway console harness (not a real xUnit v3/TUnit host) that declares
its own `Compono.XunitV3.ComposeAttribute`-metadata-name stand-in (the same
trick `Compono.Generators.Tests` already uses) on a method taking both a
custom composed type (`Widget`, needs a real `PlanCache<Widget>` entry) and
a provider-resolved leaf type (`string`, needs none), then dispatches both
through the real, generator-populated `RowInvokerRegistry` directly (no
dependency on `Compono.XunitV3` itself). The published native binary ran
standalone and printed a real composed `Widget.Name`/`leaf` pair with exit
code 0; a second run with `-p:TrimmerSingleWarn=false` (to surface every
individual trim warning rather than the default one-line summary) produced
zero `IL2`/`IL3`-prefixed trim/AOT warnings. `System.Reflection` no longer
appears anywhere in `Compono.XunitV3.Binding.RowInvokers`, confirmed by
inspection of the rewritten file, not just by the passing test suite.

**One real, unplanned finding surfaced during test verification, not part
of this plan's original task list.** `Compono.XunitV3.Tests` — a
`ProjectReference`-based consumer of `Compono`/`Compono.XunitV3`, not the
packed-`PackageReference`-based `Compono.XunitV3.SampleTests` — does not
actually get `Compono.Generators` flowing as an analyzer into its own
compilation at all (confirmed directly: zero generator-emitted `.cs` files
even with `EmitCompilerGeneratedFiles=true` after a clean rebuild). This
was already true before this plan's changes and invisible under the old
`MakeGenericMethod`-based design (which needed no generator output to
dispatch), and is consistent with `testing.md`'s own stated hand-fake
convention for this project (`DisposableValue`/`CollectionExhaustionPlan`
already stand in for what a real generator/registration would provide) —
not a regression this plan introduced, but a previously-latent gap this
plan's new generator-output dependency made load-bearing for the first
time. Fixed by adding `Fixtures/RowInvokerRegistryFakes.cs`, a module
initializer hand-registering `RowInvokerRegistry` entries for every
distinct dispatch-eligible parameter type `SampleTestMethods` declares -
the same disposition as that project's existing hand-fakes, not a design
change.

**AOT harness packaging note, also unplanned.** `test/Compono.AotSmokeTest/`
initially referenced `Compono` via `ProjectReference` (simpler, no local
NuGet feed needed) - `dotnet publish -p:PublishAot=true`'s global
properties (`PublishAot`/`RuntimeIdentifier`/`SelfContained`) turned out to
flow down through that `ProjectReference` into `Compono.csproj`'s own
analyzer-only `ProjectReference` to `Compono.Generators.csproj`
(`netstandard2.0`), failing the whole publish with `NETSDK1207` ("AOT is
not supported for the target framework") for a project that was never
actually part of the published app. This never affects any real consumer
(every real consumer - `Compono.XunitV3.SampleTests` included - uses
`PackageReference`, which has no such project-graph propagation), so
rather than changing `Compono.csproj`'s own already-shipped, already-
working analyzer-reference shape for one harness project's own
`ProjectReference` convenience, `Compono.AotSmokeTest` was switched to
consume `Compono` via a local-feed `PackageReference` instead (mirroring
`Compono.XunitV3.SampleTests`' own pattern, packed by
`pack-compono.sh` - no concurrency lock, since this harness runs as a
manual one-shot, not concurrently across TFMs in CI).

**Not done in this pass, left for a follow-up decision, not silently
dropped:** whether `test/Compono.AotSmokeTest/`'s publish-and-run proof
gets wired into a CI job (`package-validation.yaml` or its own) - Native
AOT publish is RID-specific and noticeably slower than an ordinary
build/test step, and this repo's existing CI matrix doesn't run one today.
Verified manually, once, on `osx-arm64` (this session's own host) - not
verified on Linux/Windows RIDs, and not re-verified automatically on every
future change to `RowInvokerRegistry`/the generator's emission.
