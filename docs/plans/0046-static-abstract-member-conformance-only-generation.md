# [PLAN-0046] Static Abstract Member Conformance-Only Generation

**Status:** Not Started

**Implements:** [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)

## Goal

A test-double-eligible interface that declares a static abstract member
(method, property, or operator) generates and resolves through
`UseGeneratedTestDoubles()` alone — the member itself compiles as a
conformance-only stub that throws the new `TestDoubleUnsupportedMemberException`
if invoked, with zero effect on the interface's other, supported members.
Closes Gate-B (the "can `lightsaber-skill` remove `Compono.NSubstitute`
entirely" acceptance criterion) end to end, verified by a real second
migration pass against `IAmazonS3` specifically, not just generator-level
coverage.

## Scope

Implements ADR-0046's chosen option (conformance-only generation) in full,
per its Decision Outcome. Deferred, per the ADR's own Consequences and
rejected options: any configurable/mockable behavior for static members
(Option 4), and any "safe default" heuristic for static members (Option
3) — a static abstract member is always a throwing stub, no exceptions.
Out of scope entirely, unchanged by this plan: events, indexers, and
variable-argument methods stay whole-interface-rejected under the
existing `CMP0021` — this plan narrows `CMP0021`'s trigger condition, it
doesn't touch its handling of those other three shapes. Per
[ADR-0042 Amendment 2](../adr/0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration),
a future evidenced need for configurable static-member behavior would be
its own new roadmap candidate, not a reason to widen this plan's scope
now.

One implementation phase, one PR — the work below is grouped into task
subsections for readability, not sequenced phases; nothing here ships
independently of the rest (per this repo's "one phase = one PR"
convention, splitting a scope this size into four separate phases/PRs
would be more review overhead than the actual change warrants, since the
generator change, its tests, its docs, and its closing acceptance test
are all one decision, not four).

## Tasks

### Generator: analyzer, emitter, diagnostics

- [ ] `src/Compono/TestDoubleUnsupportedMemberException.cs`: new sealed
      exception type, same minimal shape as `TestDoubleNotConfiguredException`/
      `TestDoubleVerificationException` (a single string-message
      constructor, no extra properties) — see ADR-0046's Decision Outcome
      for why this is a dedicated type rather than reusing
      `TestDoubleNotConfiguredException`.
- [ ] `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs`: change the
      `IMethodSymbol { IsStatic: true, IsAbstract: true, ... }` branch
      (currently `return Failure(...)`) and the `IPropertySymbol { IsStatic: true, IsAbstract: true }`
      branch (same) to instead record a conformance-only member (a new
      `DiscoveredMember`-shaped entry, or equivalent, carrying enough
      signature info — name, return type, parameter list, `MethodKind`
      for operators — for the emitter to produce a matching override) and
      `continue` the scan, incrementing a `conformanceOnlyCount` the same
      way `configurationRequiredCount` already works for `CMP0032`.
      Static abstract **operators** (`MethodKind.UserDefinedOperator`)
      follow the same branch — the existing comment explaining why
      operators are checked before the general `MethodKind` filter stays
      correct and applies unchanged.
      **Ordering hazard (Codex review, PR #98):** the existing
      `method.IsVararg` check (line ~346) lives in the *ordinary* instance-
      method case, reached only after the static-abstract pattern above has
      already had first chance to match — a method that is somehow both
      static-abstract *and* a C-style vararg (`__arglist`) would match the
      static-abstract branch first and never reach the vararg check at all,
      silently getting recorded as conformance-only instead of correctly
      staying `CMP0021`-rejected. `IMethodSymbol.Parameters` excludes the
      `__arglist` sentinel entirely (per the existing vararg regression
      test's own documented finding), so a conformance-only stub built from
      `.Parameters` for such a method would emit the wrong signature and
      fail to compile. **Fix:** add an explicit `method.IsVararg` guard
      *inside* the static-abstract branch, checked before recording the
      member as conformance-only — if true, keep the original
      `return Failure(...)` (whole-interface `CMP0021` rejection),
      unchanged. Add a regression test for this exact combination (or
      document why it's unconstructible in practice, if a real compile
      spike shows static-abstract + vararg can't co-occur at all) rather
      than assuming the ordering is safe.
- [ ] `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs`: add
      `CMP0033` (Info, `Compono.TestDoubles` category) — "An interface has
      one or more static abstract members Compono generates a
      conformance-only implementation for (the type must implement them to
      compile); invoking one always throws `TestDoubleUnsupportedMemberException`
      — one diagnostic per interface (a count), not one per member,
      matching `CMP0032`'s convention."
- [ ] `src/Compono.Generators/AnalyzerReleases.Unshipped.md`: new
      `CMP0033` row (required by `EnforceExtendedAnalyzerRules`, same as
      every prior `CMP00xx` addition).
- [ ] `src/Compono.Generators/Emitters/TestDoubleEmitter.cs`,
      `src/Compono.Generators/Templates/TestDouble.scriban`: new emission
      branch for a conformance-only static member — emits it as an
      **explicit static interface implementation**
      (`static <ReturnType> <FullyQualifiedInterface>.Member(<params>)` /
      `static <ReturnType> <FullyQualifiedInterface>.operator +(<params>)`
      for operators), matching the same explicit-implementation convention
      every instance member this generator already emits (no `public`
      modifier — that's not legal on an explicit interface implementation
      either). **Not** a plain `public static` declaration (Codex review,
      PR #98): for an operator whose declared operand types are the
      interface itself rather than the implementing type
      (`static abstract IRepository operator +(IRepository, IRepository)`),
      a plain `public static` operator overload is illegal C# — neither
      operand is the enclosing (generated) type, which ordinary operator
      overload rules require. Only the explicit-interface-implementation
      form is legal for that case, and applying it uniformly to methods and
      properties too (not just where operators require it) keeps one
      emission shape across every static member kind rather than a special
      case for operators alone. Body is
      `throw new global::Compono.TestDoubleUnsupportedMemberException("...")`
      with the message format ADR-0046's Decision Outcome specifies. No
      `ReturnConfig` field, no `Configure()`/`Verify()` extension method is
      generated for this member — confirm this by inspecting emitted
      source (`-p:EmitCompilerGeneratedFiles=true`) against a probe
      interface during implementation, the same verification technique
      RESEARCH-0005 used, and confirm the emitted operator actually
      compiles against a real interface-typed-operand probe interface
      (mirroring the `IRepository operator +(IRepository, IRepository)`
      shape Codex's review flagged), not just a probe where the operand
      happens to already be the concrete generated type.
- [ ] Confirm `CMP0021`'s own message/condition is otherwise unchanged —
      it still fires, whole-interface, for events, indexers, and
      variable-argument methods; only the static-abstract-member condition
      moves out of it.

### Test coverage

- [ ] `test/Compono.Generators.Tests/`: generator-level snapshot/behavior
      tests — a static abstract method, a static abstract property, and a
      static abstract operator, each on their own probe interface;
      confirm `CMP0033` fires (count matches), `CMP0021` does not fire for
      these three interfaces, and the interface's other instance members
      still generate their normal `Configure()`/`Verify()` surface
      unaffected.
- [ ] `test/Compono.TestDoubles.SampleTests/`: a packaged-consumer test
      proving invoke-always-throws-`TestDoubleUnsupportedMemberException`
      for the static member, through the real `Compono` →
      `Compono.Generators` → `Compono.TestDoubles` dependency chain
      (matching `ConfigurationRequiredMemberTests.cs`'s existing pattern
      for the instance-level case) — and a same-interface instance member
      proven completely unaffected, mirroring
      `Deterministic_default_members_are_unaffected_by_sibling_configuration_required_members`.
      Also confirm no `Configure()`/`Verify()` extension method exists for
      the static member at all (a compile-time absence, not a runtime
      check) — the point of the dedicated exception type is that a
      consumer shouldn't be tempted to look for one.
- [ ] `test/Compono.TestDoubles.AotSmokeTest/Program.cs`: extend with a
      new probe interface declaring a static abstract member alongside a
      normal instance member — prove the unconfigured-static-member-throws
      behavior survives Native AOT/trimming, the same way
      `IProfileRepository`'s configuration-required shapes are proven
      there today.

### Docs/skill alignment

- [ ] `docs/packages/compono-testdoubles.md`: move static abstract members
      out of "What it deliberately doesn't do" into the supported-shapes
      narrative (near the `CMP0032`/configuration-required section), and
      resolve this plan's own forward-reference note added during the
      design pass.
- [ ] `docs/reference/diagnostics.md`, `skills/compono/references/diagnostics.md`:
      new `CMP0033` entries, matching `CMP0032`'s existing entry shape.
- [ ] `skills/compono/references/testdoubles.md`, `skills/compono/SKILL.md`:
      align any "still unsupported" prose that names static abstract
      members specifically.
- [ ] `docs/troubleshooting/common-errors.md`: add
      `TestDoubleUnsupportedMemberException` if that doc catalogs
      exception types/message shapes (check current pattern before adding
      — don't duplicate if it already documents exception types
      generically rather than per-type).
- [ ] `docs/reference/api/Compono/` — regenerated pages for the new
      `TestDoubleUnsupportedMemberException` type (ADR-0032's toolchain,
      drift-checked in CI, same as every prior new public type in this
      area).

### Gate-B closing dogfood: `lightsaber-skill`

- [ ] Re-run RESEARCH-0005's `lightsaber-skill` branch
      (`build/deps-bump-compono-preview-73`, or a fresh branch from
      `main` if that one's gone stale) against the shipped implementation
      of this ADR. Migrate `LightsaberHandlerTests.cs`'s remaining
      `IAmazonS3` usage (~9 call sites: `Substitute.For<IAmazonS3>()`,
      `Arg.Any`/`.Returns()` on `ListObjectsV2Async`) to
      `Compono.TestDoubles`, the same `Configure()`/`Composer.Create(...)`
      pattern the other four files already use.
- [ ] Remove `Compono.NSubstitute` from `Directory.Packages.props` and
      `Lightsaber.Skill.Tests.csproj`'s `PackageReference`s entirely.
      Confirm `NSubstitute` itself is no longer pulled in even
      transitively (`dotnet list package` or equivalent) — Gate-B's exact
      wording is "no longer required transitively," not just "no direct
      reference."
- [ ] Full 77-test suite passes, verified by running the built
      Microsoft.Testing.Platform executable directly (not just a clean
      compile) — same verification bar RESEARCH-0005 held itself to.
- [ ] `lightsaber-skill` doesn't currently have its own AOT smoke test —
      confirm whether "the AOT verification still passes" (per the user's
      acceptance criteria) refers to `Compono.TestDoubles.AotSmokeTest`
      in *this* repo (covered above) or implies `lightsaber-skill` needs
      one added; if the latter, that's a scope question to resolve with
      the user before assuming it, not something to add unprompted.
- [ ] Record the result as a new `docs/research/000N-*.md` finding
      (next sequential number after RESEARCH-0005), following the same
      evidence-record convention — this is the actual Gate-B acceptance
      test, so state plainly whether it passed in full (every bullet
      above) or only partially, same honesty bar RESEARCH-0004/RESEARCH-0005
      held.
- [ ] Update `docs/roadmap/post-mvp.md`'s entry for this candidate:
      remove from "outstanding" only if Gate-B is met in full (real
      `Compono.NSubstitute` removal, not just "fewer call sites"); if
      partial, record the honest result and keep the candidate listed.

## Critical Files

- `src/Compono/TestDoubleUnsupportedMemberException.cs` — new exception
  type (see ADR-0046's Decision Outcome for why it's distinct from
  `TestDoubleNotConfiguredException`).
- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — the two
  static-abstract branches change from whole-interface `Failure(...)` to
  member-scoped conformance-only recording.
- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` — new
  `CMP0033` descriptor.
- `src/Compono.Generators/AnalyzerReleases.Unshipped.md` — new `CMP0033`
  row.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs`,
  `src/Compono.Generators/Templates/TestDouble.scriban` — new
  conformance-only static-member emission branch.
- `test/Compono.Generators.Tests/`, `test/Compono.TestDoubles.SampleTests/`,
  `test/Compono.TestDoubles.AotSmokeTest/Program.cs` — new test coverage.
- `docs/packages/compono-testdoubles.md`, `docs/reference/diagnostics.md`,
  `skills/compono/references/diagnostics.md`,
  `skills/compono/references/testdoubles.md`, `skills/compono/SKILL.md`,
  `docs/troubleshooting/common-errors.md` — doc/skill alignment.
- `docs/roadmap/post-mvp.md`, a new `docs/research/000N-*.md` — the
  Gate-B closing result.
- (External repo) `ncipollina/lightsaber-skill`: `Directory.Packages.props`,
  `test/Lightsaber.Skill.Tests/Lightsaber.Skill.Tests.csproj`,
  `test/Lightsaber.Skill.Tests/Handlers/LightsaberHandlerTests.cs` — the
  actual migration.

## Test Plan

Matches `references/testing.md`'s existing pattern for this feature area
(established by PLAN-0043/PLAN-0044/PLAN-0045): generator-level
snapshot/behavior tests for the analyzer/diagnostic change, packaged-
consumer behavior tests through the real dependency chain, and Native AOT
proof — plus, unlike prior plans in this area, a **closing real-world
acceptance test** that isn't just another dogfood pass but the literal
Gate-B criterion this ADR exists to satisfy. Before this PR merges, it
must demonstrate all of the following, not a subset:

- Static abstract methods, properties, and operators no longer reject
  their declaring interface (`CMP0021` no longer fires for this case;
  `CMP0033` does).
- The generated static member is conformance-only — it always throws
  `TestDoubleUnsupportedMemberException`, unconditionally, with no
  configured/unconfigured state to distinguish.
- No `Configure()`/`Verify()` surface is generated for that member — a
  compile-time absence, verified directly, not just an untested claim.
- Every other, already-supported instance member on the same interface is
  completely unaffected — same generated behavior as before this plan.
- Native AOT/trimming still passes (`Compono.TestDoubles.AotSmokeTest`).
- `IAmazonS3` specifically now generates and resolves through
  `UseGeneratedTestDoubles()` alone in `lightsaber-skill`.
- All remaining NSubstitute call sites in `LightsaberHandlerTests.cs`
  (~9) migrate to `Compono.TestDoubles`.
- `Compono.NSubstitute` is removed from `lightsaber-skill`'s test project
  package references entirely.
- `NSubstitute` itself is no longer present, even transitively, in that
  project's dependency graph.
- The full 77-test `lightsaber-skill` suite passes, verified by running
  the built test executable, not just a clean compile.
- `docs/reference/diagnostics.md`, `skills/compono/references/diagnostics.md`,
  `docs/packages/compono-testdoubles.md`, and any other skill/doc surface
  naming static abstract members are current, not stale.
- The Gate-B result is recorded honestly in a new research doc and
  reflected accurately in `docs/roadmap/post-mvp.md` — in full if every
  bullet above passed, or as an honest partial result if not, per the
  same standard RESEARCH-0004/RESEARCH-0005 held themselves to.

## Notes

(Empty — plan not yet started.)
