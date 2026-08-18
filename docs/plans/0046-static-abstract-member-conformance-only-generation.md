# [PLAN-0046] Effective Interface Contract for Inherited Static Abstract Members

**Status:** Done

**Implements:** [ADR-0046](../adr/0046-static-abstract-member-conformance-only-generation.md)

## Goal

A test-double-eligible interface whose closure declares a static abstract
member (method, property, or operator) that's already resolved by a
more-derived interface in the same closure (C#'s own "most specific
implementation" rule) generates and resolves through
`UseGeneratedTestDoubles()` alone, with every member — including the
previously-blocking one — behaving exactly as the real interface actually
behaves. Closes Gate-B (the "can `lightsaber-skill` remove
`Compono.NSubstitute` entirely" acceptance criterion) end to end, verified
by a real second migration pass against `IAmazonS3` specifically, not just
generator-level coverage.

## Scope

Implements ADR-0046's chosen option (effective-interface-contract
analysis) in full, per its Decision Outcome. A static abstract member with
no override anywhere in its interface's closure stays whole-interface-
rejected under the existing `CMP0021`, unchanged — per ADR-0046, C# itself
(`CS8920`) makes such an interface uncomposable through Compono's generic
`Resolve<TValue>()` regardless of what this generator does, so no stub or
fallback behavior is implemented for that case. Out of scope entirely,
unchanged by this plan: events, indexers, and variable-argument methods
stay whole-interface-rejected under `CMP0021` — this plan narrows one
specific trigger condition of `CMP0021`, it doesn't touch its handling of
those other three shapes. Per
[ADR-0042 Amendment 2](../adr/0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration),
a future evidenced need for configurable static-member behavior, or for
composing a genuinely unresolved static abstract member some other way,
would be its own new roadmap candidate, not a reason to widen this plan's
scope now.

**No new public API surface**: no new diagnostic code, no new exception
type, no new emission branch. This plan's earlier draft (still visible in
ADR-0046's "Decision Outcome" section, which records it in full rather
than silently rewriting) proposed exactly those three things
(`CMP0033`, `TestDoubleUnsupportedMemberException`, a conformance-only
stub emission branch) before implementation-time compile spikes proved
the design wrong (would have silently broken `IAmazonS3`'s own real
implementation) and unreachable (blocked upstream by `CS8920` for the
case it would have legitimately applied to) at the same time. All of that
machinery is removed, not shipped.

One implementation phase, one PR for the generator fix, its tests, and its
docs — the closing `lightsaber-skill` dogfood is a **separate, follow-up
PR** (see "Notes"): it requires publishing a new Compono preview package
with this fix included before `lightsaber-skill`'s own dependency bump can
consume it, which can't happen inside this PR.

## Tasks

### Analyzer: effective-interface-contract resolution

- [x] `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs`: the
      `IMethodSymbol { IsStatic: true, IsAbstract: true, ... }` branch and
      the `IPropertySymbol { IsStatic: true, IsAbstract: true }` branch
      (previously unconditional `return Failure(...)`) each gain one
      guard, checked first: if
      `interfaceType.FindImplementationForInterfaceMember(member)` returns
      non-null, the member is already resolved by a more-derived interface
      in the closure (verified against Roslyn directly, not assumed) —
      `continue` past it, the same disposition an ordinary non-abstract
      static member already gets. If it returns `null`, fall through to
      the original, unchanged `Failure(...)` (whole-interface `CMP0021`
      rejection) — a genuinely unresolved static abstract member's
      disposition doesn't change.
- [x] Confirm `CMP0021`'s own message/condition is otherwise unchanged for
      the shapes it still legitimately rejects: events, indexers,
      variable-argument methods, and a genuinely unresolved static
      abstract member.
- [x] `src/Compono.Generators/AnalyzerReleases.Unshipped.md`: update
      `CMP0021`'s row description to reflect the narrowed trigger
      condition (a resolved-via-derived-interface static abstract member
      no longer fires it at all).

### Test coverage

- [x] `test/Compono.Generators.Tests/`: generator-level compile-and-verify
      tests (not diagnostics-only — `GeneratorTestHelpers.Verify`, which
      actually reparses and compiles the generated output, catching the
      class of bug a diagnostics-only assertion would have missed) for
      the general Roslyn/interface-inheritance rule, covering a static
      abstract method, property, and operator each resolved by a
      more-derived interface — confirm no diagnostic fires, the double
      generates completely normally, and a sibling instance member's
      `Configure()`/`Verify()` surface is unaffected. Separately, confirm
      a genuinely unresolved static abstract method/property/operator
      still reports `CMP0021` and rejects the whole interface, unchanged
      from before this ADR.
- [x] `test/Compono.TestDoubles.SampleTests/`: a packaged-consumer test
      (`StaticAbstractMemberTests.cs`) proving the fix through the real
      `Compono` → `Compono.Generators` → `Compono.TestDoubles` dependency
      chain — an IAmazonS3-shaped interface (a base interface declaring a
      static abstract member, a derived interface re-implementing it)
      resolves and its ordinary instance member works through
      `Configure()`/`TestDoubleNotConfiguredException` exactly as any
      other configuration-required member would.
- [x] `test/Compono.TestDoubles.AotSmokeTest/Program.cs`: extended with
      the same IAmazonS3-shaped probe interface pair, proving the
      resolved-via-derived-interface member doesn't reject the leaf
      interface under Native AOT/trimming either — verified via a real
      `dotnet publish -p:PublishAot=true` + run, not just a JIT test.

### Docs/skill alignment

- [x] `docs/packages/compono-testdoubles.md`: new "Static abstract members
      inherited from a base interface" section (near the
      "Configuration-required members" section) documenting the supported
      shape with the real `IAmazonS3`/`IAmazonService` example; "What it
      deliberately doesn't do" corrected to say a *genuinely unimplemented*
      static abstract member still rejects the interface, not "a static
      abstract member" unconditionally.
- [x] `docs/reference/diagnostics.md`, `skills/compono/references/diagnostics.md`:
      `CMP0021`'s entry corrected to describe the narrowed trigger
      condition and link the new supported-shape documentation.
- [x] `skills/compono/references/testdoubles.md`: "still unsupported"
      prose corrected the same way, plus a short note on the
      resolved-via-derived-interface exception.
- [x] `skills/compono/SKILL.md`, `docs/troubleshooting/common-errors.md`:
      checked — neither names static abstract members specifically, no
      change needed.
- [x] No `docs/reference/api/Compono/` regeneration needed — no new public
      type was added (the originally-planned
      `TestDoubleUnsupportedMemberException` was withdrawn, not shipped).

### Gate-B closing dogfood: `lightsaber-skill`

- [x] Published `Compono`/`Compono.TestDoubles`/`Compono.XunitV3`
      `0.5.0-preview.74`, including the merged fix
      ([compono#99](https://github.com/LayeredCraft/compono/pull/99)).
- [x] Re-ran `lightsaber-skill`'s `build/deps-bump-compono-preview-73`
      branch against the new package. Migrated
      `LightsaberHandlerTests.cs`'s remaining `IAmazonS3` usage (~9 call
      sites: `Substitute.For<IAmazonS3>()`, `Arg.Any`/`.Returns()` on
      `ListObjectsV2Async`) to `Compono.TestDoubles`' `Configure()`/
      `Composer.Create(...)` pattern, matching the other four files.
      `ListObjectsV2Async` verified (reflection against the real
      `AWSSDK.S3` package) to be non-overloaded, so no discriminator was
      needed.
- [x] Removed `Compono.NSubstitute` from `Directory.Packages.props` and
      `Lightsaber.Skill.Tests.csproj`'s `PackageReference`s entirely, and
      `UseNSubstitute()` from `GeneratedTestDoublesProfile`. Confirmed
      `NSubstitute` itself is no longer pulled in even transitively
      (`dotnet list package --include-transitive`) — Gate-B's exact
      wording is "no longer required transitively," not just "no direct
      reference," and this confirms it.
- [x] Full 77-test suite passes, verified by running the built
      Microsoft.Testing.Platform executable directly (not just a clean
      compile) — 77/77, 0 skipped.
- [x] AOT scope question resolved with the user directly: `lightsaber-skill`
      doesn't need its own new AOT infrastructure — `Compono.TestDoubles.AotSmokeTest`
      (already covered by this plan's earlier task section) is sufficient.
- [x] Recorded the result as
      [RESEARCH-0006](../research/0006-lightsaber-skill-testdoubles-gate-b-closing-dogfood.md) —
      full success, every bullet above passed, no partial result to
      report this time.
- [x] Updated `docs/roadmap/post-mvp.md`'s entry for this candidate:
      removed from "outstanding" — Gate-B met in full, real
      `Compono.NSubstitute` removal, not just fewer call sites.

## Critical Files

- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — the two
  static-abstract branches gain the effective-interface-contract guard.
- `src/Compono.Generators/AnalyzerReleases.Unshipped.md` — `CMP0021` row
  description updated.
- `test/Compono.Generators.Tests/TestDoubleVerifyTests.cs`,
  `test/Compono.TestDoubles.SampleTests/StaticAbstractMemberTests.cs`,
  `test/Compono.TestDoubles.AotSmokeTest/Program.cs` — new test coverage.
- `docs/packages/compono-testdoubles.md`, `docs/reference/diagnostics.md`,
  `skills/compono/references/diagnostics.md`,
  `skills/compono/references/testdoubles.md` — doc/skill alignment.
- `docs/adr/0046-static-abstract-member-conformance-only-generation.md` —
  rewritten in place (still `Proposed` when the rewrite happened, so no
  Amendment was needed) to record both the corrected design and the two
  compile-spike findings that invalidated the original one.
- `docs/roadmap/post-mvp.md`, `docs/research/0006-lightsaber-skill-testdoubles-gate-b-closing-dogfood.md`
  — the Gate-B closing result.
- (External repo) `ncipollina/lightsaber-skill`'s `Directory.Packages.props`,
  `Lightsaber.Skill.Tests.csproj`, `Composition/GeneratedTestDoublesProfile.cs`,
  `Handlers/LightsaberHandlerTests.cs` — the actual migration
  ([lightsaber-skill#108](https://github.com/ncipollina/lightsaber-skill/pull/108)).

## Test Plan

Matches `references/testing.md`'s existing pattern for this feature area:
generator-level compile-and-verify tests for the analyzer fix (not
diagnostics-only — full reparse-and-recompile of the generated output),
packaged-consumer behavior tests through the real dependency chain, and
Native AOT proof. Before this PR merges, it must demonstrate all of the
following, not a subset:

- A static abstract method, property, and operator, each already resolved
  by a more-derived interface in the closure, no longer reject their
  declaring interface (`CMP0021` doesn't fire; no other diagnostic fires
  either — the fix doesn't add one).
- The resolved member's real, existing implementation is preserved
  exactly — the double doesn't emit anything for it, so there's nothing
  that could shadow or change its behavior.
- A genuinely unresolved static abstract method/property/operator (no
  override anywhere in the closure) still reports `CMP0021` and rejects
  the whole interface, unchanged from before this ADR.
- Every other, already-supported instance member on the same interface is
  completely unaffected — same generated behavior as before this plan.
- Native AOT/trimming still passes (`Compono.TestDoubles.AotSmokeTest`),
  including the new resolved-via-derived-interface probe.
- No new public API surface exists — no new diagnostic code, no new
  exception type, verified by grep/review, not just by omission.
- `docs/reference/diagnostics.md`, `skills/compono/references/diagnostics.md`,
  `docs/packages/compono-testdoubles.md`, and any other skill/doc surface
  naming static abstract members are current, not stale.

The remaining Gate-B bullets (real `IAmazonS3` removal of
`Compono.NSubstitute` in `lightsaber-skill`, the full 77-test suite, the
closing research doc, the roadmap update) landed in the follow-up PR
described in "Notes" — all passed, recorded in
[RESEARCH-0006](../research/0006-lightsaber-skill-testdoubles-gate-b-closing-dogfood.md).

## Notes

**2026-08-18 — design corrected during implementation, not just
implemented.** This plan's original draft (ADR-0046's first accepted
design) specified conformance-only stub generation: a new `CMP0033`
diagnostic, a new `TestDoubleUnsupportedMemberException` type, and a new
emitter/template branch generating a throwing explicit static interface
implementation. Implementation proceeded far enough to generate real code
and run it against real compilation before two compile spikes (both
recorded in full in ADR-0046) proved the design wrong: it would have
silently shadowed and broken `IAmazonS3`'s own real, working
implementation (a more-derived type's own explicit static interface
implementation wins over an already-resolved base-interface override —
verified by executing the shape, not just reading the spec), and
separately, the case it would have legitimately applied to (a genuinely
unresolved static abstract member) turned out to be permanently
unreachable through Compono's own composition mechanism regardless
(`CS8920`). All three pieces of that original design were removed before
this PR, replaced by the effective-interface-contract fix this plan now
describes. This is why every "Tasks" bullet above already shows `[x]` —
this plan is being finalized alongside implementation, not written ahead
of it, per this repo's convention that a plan is a living document,
correctable via Notes rather than treated as a wrong-but-frozen spec.

**Dogfood deferred to a follow-up PR, then completed.** The generator-side
fix (`compono#99`) merged and shipped as `Compono` `0.5.0-preview.74`
independently of the `lightsaber-skill` migration, which needed that
published package before it could even start. Once the package was
available, the closing dogfood ran in full and succeeded — see
[RESEARCH-0006](../research/0006-lightsaber-skill-testdoubles-gate-b-closing-dogfood.md)
and [lightsaber-skill#108](https://github.com/ncipollina/lightsaber-skill/pull/108).
`Status` above moved from `In Progress` to `Done` once every "Gate-B
closing dogfood" task confirmed passing, not before.

**2026-08-18 — Codex review, PR #99: collision-preprocessing fix.**
`TestDoubleAnalyzer`'s `eligibleCandidates` pass (diamond-collision and
zero-argument-extension-collision detection) runs *before* the per-member
emission loop's own `FindImplementationForInterfaceMember` guard, and was
still including an already-resolved static abstract member's raw,
`IsAbstract: true` symbol - reached via the closure walk visiting its
*declaring* interface directly, not the resolved override. Since
`TestDoubleOverloadIdentity.CanonicalSignatureFor` encodes only
arity/parameter types (never return type or static-ness), a resolved
zero-parameter static member sharing a name with a same-named zero-
parameter *instance* member would be misclassified as a diamond-colliding
identity, silently withholding the real instance member's
`Configure()`/`Verify()` surface. `eligibleCandidates` now excludes any
member that's `IsStatic && IsAbstract && FindImplementationForInterfaceMember(...)
is not null` - the same condition the emission loop already uses to skip
it. Regression test:
`StaticAbstractMemberResolvedByDerivedInterface_DoesNotCollideWithSameNamedInstanceMember`.
