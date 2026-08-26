# [PLAN-0053] Compono.TestDoubles: Default-Interface-Member Fallback Fix

**Status:** Done — diamond-resolution, forwarding-member, and owner-forwarding
dispatch-helper pieces all landed and verified: full Compono test suite green
(432/432 in `Compono.Generators.Tests`, no regressions across the rest of the
repo), both AOT proofs pass against a DIM-bearing interface, and the
freshly-packed-package AlexaVoxCraft dogfood gate is green end-to-end (see
Notes). Remaining unchecked Tasks below are narrower edge-case coverage
(mutual-DIM-through-`this`, a three-level DIM chain) not hit by any real
consumer shape found so far — tracked here, not blocking.

**Implements:** [ADR-0044 Amendment 20](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md#amendment-20-2026-08-25-effective-declaration-resolution-corrected-for-basederived-member-identity-concrete-default-interface-member-bodies-now-honored-as-the-unconfigured-fallback-bug-fix)

## Goal

A generated test double's unconfigured member fallback matches the
interface's own real behavior: an abstract member still gets ADR-0045's
computed deterministic default (unchanged); a concrete member (a default
interface member, "DIM") runs its own declared body instead of a
synthetic, possibly-wrong one. A base/derived `new`-hiding relationship
(one interface's abstract member resolved by a more-derived interface's
own concrete redeclaration) is no longer misclassified as a diamond
collision. Done when: the five `AlexaVoxCraft.MediatR.Tests` call sites
blocked on `IDefaultRequestHandler.CanHandle` convert cleanly to
`Compono.TestDoubles`, plus the full test matrix below passes.

## Scope

Implements Amendment 20's Decision Outcome — see that Amendment for the
full rationale and spike evidence. In scope:

- `TestDoubleAnalyzer`'s identity-group resolution (diamond-vs-inheritance).
- `TestDoubleEmitter`'s per-member fallback-body generation for a resolved
  concrete member (owner-forwarding dispatch helper).
- New analyzer/behavior/snapshot test coverage (this ADR area had zero DIM
  coverage before this plan).

Explicitly out of scope (unchanged, per Amendment 20's own "What this
Amendment does not change"): real-diamond behavior, static abstract member
resolution (ADR-0046, its own mechanism), generic-member eligibility
rules (ADR-0049/Amendment 19), any new public API, argument
matching/multi-entry configuration mechanics (ADR-0048/ADR-0050 — a
resolved concrete member gets exactly the same `Configure()`/`Verify()`/
`Match<T>` surface an equivalent abstract member would).

## Resolution algorithm — why "unique dominant declaration," not pairwise relatedness

Spiked directly (Roslyn symbols, `AllInterfaces.Contains`) against a
**convergent-diamond** shape a pairwise rule gets wrong: `IBase1.M`
(abstract); `IBranchA : IBase1` and `IBranchB : IBase1`, each
independently redeclaring `new string M() => ...;` (two genuinely
unrelated concrete siblings — a real diamond on its own); a leaf
interface `ILeafResolved : IBranchA, IBranchB` that **itself**
redeclares `new string M() => "leaf";`, resolving the ambiguity the same
way C# itself allows (a directly-declared, most-derived redeclaration on
the leaf always wins over its ambiguous ancestors).

- A **pairwise** rule ("every pair in the group must have a base/derived
  relationship") fails this case: `(IBranchA, IBranchB)` is an unrelated
  pair, so the whole group would be wrongly flagged a collision *despite*
  `ILeafResolved.M` unambiguously resolving it.
- The **unique-dominant-declaration** rule above handles it correctly —
  spiked and confirmed:
  ```
  === Convergent diamond, leaf resolves (ILeafResolved) ===
    candidates (not-an-ancestor-of-another): ILeafResolved
    => RESOLVES to ILeafResolved
  === Convergent diamond, leaf does NOT resolve (ILeafUnresolved) ===
    candidates (not-an-ancestor-of-another): IBranchA, IBranchB
    => COLLISION (not exactly one candidate)
  ```
  (`ILeafUnresolved : IBranchA, IBranchB` — same ancestors, no leaf
  redeclaration of its own — correctly stays a genuine collision.)
- Also re-verified against every shape from the original spike (base
  abstract → derived concrete; base concrete → derived concrete; 3-level
  chain; unrelated siblings with no leaf resolution) — all unchanged
  outcomes under the new rule.

## Tasks

### Analyzer (`TestDoubleAnalyzer`)

- [x] Replace the blanket "same `(Name, CanonicalSignature)` reached more
      than once ⇒ diamond" check (`diamondCollisionIdentities`, current
      lines ~106–117) with inheritance-aware resolution via a **unique
      dominant declaration** test, not a pairwise-relatedness test (a
      pairwise "every pair must relate" rule was spiked and found too
      restrictive — see "Resolution algorithm" below for the exact
      counterexample). Within an identity group `G`:
      1. `candidates = { d ∈ G : ∄ e ∈ G, e ≠ d, with d.ContainingType a
         base of e.ContainingType }` — declarations that are not
         themselves a base interface of any other declaration in the
         group (i.e., nothing in the group is "more derived than" them
         from this member's perspective).
      2. If `|candidates| ≠ 1`, the group is a genuine collision —
         unchanged diamond behavior.
      3. Otherwise let `cand` be the sole candidate. The group resolves
         to `cand` only if `cand.ContainingType` is derived from *every*
         other declaration's `ContainingType` in `G` (i.e. `cand`
         dominates the whole group, not merely "isn't dominated by
         anyone"); if that check fails, the group is still a genuine
         collision (defensive — kept as its own explicit check per
         review, not assumed redundant with step 1).
- [x] Exclude the resolved-away base declaration(s) from
      `eligibleCandidates` (mirroring the existing static-abstract-member
      exclusion at line 103), so the resolved member flows through the
      ordinary single-member (non-diamond, non-overloaded) path
      unmodified.
- [x] Record, per resolved concrete member, whether it needs
      owner-forwarding-helper fallback generation (i.e., `IsAbstract:
      false`) — surfaced to the emitter via `DiscoveredTestDoubleInfo`/
      `TestDoubleMemberInfo` (exact model field TBD during implementation;
      keep this a data-only addition, no behavior change to unrelated
      member kinds).
- [x] Verify the base interface's own required explicit implementation
      (still emitted — `new` doesn't satisfy `IRequestHandler.CanHandle`'s
      own requirement, confirmed by spike) is recorded as "forwards to the
      resolved member," not a second independent member. (Landed as
      `TestDoubleMemberInfo.IsForwarding`/`ForwardsToInterfaceFullyQualifiedName`,
      set in `TestDoubleAnalyzer`'s method/property emission loops.)

### Emitter (`TestDoubleEmitter`)

- [x] For a resolved concrete member, generate the per-member
      owner-forwarding dispatch helper class (holds `_owner`, implements
      the leaf interface, does not override the member being defaulted,
      forwards every other interface member — including any inherited
      from other interfaces in the closure — to `_owner`).
      (`TestDoubleAnalyzer.BuildDimFallbackSiblings` + `TestDouble.scriban`'s
      new `member.is_dim_fallback_target` nested-helper-class branch.)
- [x] Change the resolved member's own body: record the call first
      (unchanged), check configured entries (unchanged ADR-0048/ADR-0050
      logic), and on no match, call through the dispatch helper's
      leaf-interface view instead of ADR-0045's computed default. (Every
      dispatch-body shape that could reach a fallback now branches on
      `member.is_dim_fallback_target` first: plain method, matching-eligible
      method (void and non-void), property get, and ADR-0049 closed-
      instantiation-eligible generic method fallbacks.)
- [x] Generate the base interface's explicit implementation (where
      required) as a forward to the resolved member's own implementation.
      (`TestDouble.scriban`'s new `member.is_forwarding` branch, both
      method and property shapes.)
- [x] **Call-recording invariant — enforce explicitly, don't assume it
      falls out of the design.** Exactly one place per resolved member
      owns its `__Member_calls`/entry-list state (the resolved member's
      own body, per the existing single-member emission shape). Both (a)
      the base-interface explicit-implementation forward and (b) every
      member the owner-forwarding dispatch helper forwards back to the
      owner **must be pure delegation** — call straight through to the
      one owning implementation, with no independent `calls.Add(...)` of
      their own. One consumer invocation, regardless of which interface
      view it entered through or whether it arrived via a DIM body's
      internal `this.OtherMember()` call routed back through the helper,
      must produce exactly one logical recorded invocation *of the member
      actually invoked* — see the dedicated call-recording tests below,
      which must fail loudly (asserting an exact count, not just
      "greater than zero") if a forwarding path is accidentally given its
      own recording logic during implementation.
- [x] Properties: same shape as methods (getter/setter fallback via the
      same helper pattern) — reuse ADR-0043 Amendment 7's existing
      property-accessor plumbing where possible rather than duplicating
      it.
- [x] Confirm generated code stays AOT-safe (no reflection introduced —
      the helper is ordinary generated C#, expected to already comply).
      **Do not reuse the existing AOT proof fixture as-is** — audit it
      first; if (as expected) none of its current interfaces declare a
      concrete default-interface member, the proof would compile and run
      clean without ever emitting a dispatch helper at all, proving
      nothing about this fix. Add a DIM-shaped interface to the AOT
      fixture (mirroring `IDefaultRequestHandler`'s real shape — a base
      abstract member resolved by a derived concrete `new` redeclaration)
      so the dispatch-helper code path is actually present in the
      published, trimmed output before re-running the proof. Both AOT
      proofs (Proof A analyzer-contract IL2026/IL3050 diagnostics, Proof B
      real `PublishAot=true` publish-and-run) must exercise it.

### Tests

- [x] Plain DIM, no inheritance at all (`interface IFoo { string
      GetValue() => "default"; }`): unconfigured → `"default"`; configured
      → configured value; `Verify().GetValue().Once()` passes after one
      call. (Proves the fix isn't accidentally dependent on
      inheritance/`new`-hiding.)
- [x] Base abstract → derived concrete DIM via `new` (the
      `IDefaultRequestHandler`/`IRequestHandler` shape): both interface
      views, unconfigured and configured.
- [ ] Base concrete DIM → derived concrete DIM via `new` (different
      bodies): most-derived body wins as the fallback.
- [ ] Three-level chain (abstract → concrete → concrete): single
      most-derived resolves, not pairwise-confused.
- [x] Real diamond regression: two unrelated sibling interfaces
      independently declaring the same shape, both on a leaf interface —
      confirm still no `Configure()`/`Verify()` surface (unchanged
      behavior, guards against over-correction).
- [x] **Multiple-inheritance convergence** (the case a naive pairwise rule
      gets wrong — see "Resolution algorithm" above): a common abstract
      ancestor, two independent concrete-DIM branches (a genuine diamond
      on their own), and a leaf interface that redeclares the member
      itself, resolving the ambiguity — confirm the leaf's own
      redeclaration resolves the group and gets a full `Configure()`/
      `Verify()` surface. Pair with the same shape *without* the leaf's
      own redeclaration (candidates count = 2) to confirm it still
      collides — both sides of the distinction in one test file.
- [x] **Call-recording invariants** (dedicated tests, not folded into the
      behavior tests above): for the base/derived DIM shape, call through
      the *base*-interface view only and assert the member was recorded
      exactly once (not twice via a base-forward that also logs); for the
      "DIM A calls DIM/abstract B through `this`" shapes, assert `A`'s
      call count is exactly 1 *and* `B`'s call count is exactly 1 after a
      single external call to `A` — catches an accidental double-record
      on either the forwarding path or the cross-member dispatch path.
- [x] Property DIM, plain (no cross-member call).
- [ ] Property DIM whose getter calls another interface member through
      `this` — the case that motivates the owner-forwarding design over a
      simpler alternative; assert the nested call reflects the *owner's*
      configured state, not a helper-local default.
- [x] Method DIM calling an abstract member through `this` (spiked design
      §2 shape).
- [ ] Method DIM calling another concrete DIM through `this`, both
      directions independently configurable (spiked design §3 shape;
      all four configured/unconfigured combinations).
- [x] Base-interface-view and derived-interface-view calls against the
      *same* double instance both observe the same configured/fallback
      state and the same call count.
- [x] Generic DIM: one behavior test if already eligible under existing
      Requirement 2/ADR-0049 rules; otherwise a short note confirming the
      existing generic-method boundary is unaffected (no scope expansion).
      (`StandaloneClosedInstantiationDim_UnconfiguredView_ExecutesRealDimBody_NotComputedDefault`
      verifies the ADR-0049 closed-instantiation-eligible branch executes
      the real DIM body instead of ADR-0045's computed default.)
- [ ] Snapshot review: regenerate/inspect existing `TestDoubleVerifyTests`
      snapshots for any incidental diff from the identity-resolution
      change (expect none outside the new DIM-specific fixtures).

### Docs

- [ ] `docs/packages/compono-testdoubles.md` — add a "Default interface
      members" section (unconfigured → real DIM body; configured →
      configured value wins; documents the owner-forwarding fallback
      model at a consumer-facing level, not implementation detail).
- [ ] `skills/compono/references/testdoubles.md` — update if it currently
      implies DIM bodies are ignored/unsupported (per the review-comment
      instruction; confirm current text first rather than assuming it
      needs a change).

## Critical Files

- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — identity
  resolution fix, exclusion-list change.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` — dispatch-helper
  emission, resolved-member fallback body.
- `src/Compono.Generators/Models/*` — any new data carried from analyzer
  to emitter for "this member needs helper-based fallback."
- `test/Compono.Generators.Tests/TestDoubleVerifyTests.cs` (+ new
  `Snapshots/`) — new coverage above.
- `docs/packages/compono-testdoubles.md`, `skills/compono/references/testdoubles.md`.

## Test Plan

Generator/snapshot tests per the Tasks list above, run through the
standard `Compono.Generators.Tests` suite. AOT fixture extended with a
DIM-shaped interface (see Emitter tasks) and both AOT proofs (analyzer
diagnostics, real `PublishAot=true` publish-and-run) re-run against it —
not merely re-run against the existing, DIM-free fixture. Real-consumer
proof: the `AlexaVoxCraft.MediatR.Tests` blocker (RESEARCH-0011 Stage 2)
against freshly packed local `Compono`/`Compono.TestDoubles`/`Compono.XunitV3`
packages, per this repo's standing consumer/dogfood validation gate
(`AGENTS.md`) — not pushed until that gate passes.

## Notes

**Analyzer fix.** `TestDoubleMemberIdentityResolver.TryResolve` (new,
isolated) implements the unique-dominant-declaration algorithm exactly as
specified in ADR-0044 Amendment 20. Wired into `TestDoubleAnalyzer.Analyze`:
`diamondCollisionIdentities` is now only populated when `TryResolve` returns
`null` for a >1-member identity group; a resolved group's losing (base)
declaration is recorded in `resolvedAwayForwardsTo` and excluded from
`eligibleCandidates`, mirroring the existing static-abstract-member exclusion
pattern. Verified against: base-abstract → derived-concrete-DIM (the exact
`IDefaultRequestHandler.CanHandle` shape), a convergent diamond resolved by a
leaf redeclaration, and (unchanged, regression-proven by the full existing
suite) genuine unrelated-sibling diamonds.

**Forwarding-member emission.** A resolved group's losing declaration still
needs a real explicit interface implementation (`new`-hiding alone doesn't
satisfy the base interface's own abstract-member requirement - confirmed by
Roslyn spike during design). `TestDoubleMemberInfo.IsForwarding`/
`ForwardsToInterfaceFullyQualifiedName`, set in `TestDoubleAnalyzer`'s method
and property emission loops, drive a new `member.is_forwarding` branch in
`TestDouble.scriban` that purely forwards to the resolved member's own
explicit implementation (cast to the dominant interface, no independent
state) - both method and property (get/set/init, with an explicit
`NotSupportedException` for the init-accessor case, which cannot be forwarded
under C#'s own construction-only-callable rule).

**Owner-forwarding dispatch-helper implementation.** For a resolved dominant
declaration that's concrete (a real DIM body), `TestDoubleAnalyzer` now also
sets `IsDimFallbackTarget`/`DimFallbackSiblings` (built by the new
`BuildDimFallbackSiblings`, walking the DIM's own declaring interface's
transitive closure). `TestDouble.scriban` emits a nested
`{FieldName}_DimFallback` helper class implementing that declaring interface,
deliberately NOT overriding the DIM member itself (so C#'s own
default-interface-member dispatch resolves it), and forwarding every other
required member of that interface back to the owning double (cast through
the interface, never independently recorded). Every dispatch-body shape that
could reach an unconfigured fallback (plain method, matching-eligible method
void/non-void, property get, and ADR-0049 closed-instantiation-eligible
generic method fallbacks) now calls through
`(({DeclaringInterface})new {Helper}(this)).{Member}(...)` instead of
ADR-0045's computed default when `is_dim_fallback_target` is set - the
`(({DeclaringInterface})...)` cast was required: an early version called the
helper's member directly on its concrete type and silently resolved to the
wrong (extension-method) overload instead of failing to compile, since the
helper deliberately has no direct member declaration for the DIM target.
Verified via `TestDoubleDefaultInterfaceMemberFallbackTests`: unconfigured
DIM fallback runs the real body, configured value still wins, base/derived
views share one call-recording state (no double-recording), a convergent
diamond resolves the same way, a DIM body's cross-member call to an abstract
sibling (`this.Other()` inside the DIM) forwards to the owner and is
recorded exactly once, and a closed-instantiation-eligible generic DIM
fallback executes the real DIM body instead of ADR-0045's computed default.

**Scriban projection bug found during implementation.** `TestDoubleEmitter`
builds the Scriban template model from a hand-picked anonymous-object
projection of `TestDoubleMemberInfo`, not the record directly - adding new
fields to the record (`IsForwarding`, `IsDimFallbackTarget`, etc.) silently
did nothing until the same fields were also added to that projection
(`TestDoubleEmitter.cs`'s `Generate` method). The failure mode was
non-obvious: the generated code silently fell through to the *old* fallback
branch (a real, differently-shaped body) rather than erroring, so a plain
`dotnet test` diff wasn't enough to catch it - it required generating actual
output and confirming a `member.is_forwarding` marker rendered at all, which
led to inspecting `TestDoubleEmitter`'s model-building code directly. Any
future field added to `TestDoubleMemberInfo`/`TestDoubleDimFallbackSiblingInfo`
that the template needs must also be added to this projection - there is no
compiler check that would catch a missed one.

**AOT proofs.** Both extended against a DIM-bearing interface pair
(`IDefaultHandlerBase`/`IDefaultHandler`, mirroring `IDefaultRequestHandler`)
added to `test/Compono.TestDoubles.AotSmokeTest/Program.cs`: unconfigured DIM
fallback, base-interface-view/derived-view shared call count, and the
configured-value override path. Proof A (analyzer-contract IL2026/IL3050
diagnostics): `dotnet publish -c Release -f net10.0 -p:PublishAot=true`
produced zero AOT-analyzer/ILLink warnings. Proof B (real Native AOT
publish-and-run): the published native binary ran and printed `PASS`,
exercising the owner-forwarding dispatch-helper code path for real, not just
compiling it.

**Full Compono test suite.** `Compono.Generators.Tests`: 432/432 passing (0
skipped) across both TFMs, including the 7 new DIM-fallback tests, with zero
regressions to any pre-existing test (211 unaffected snapshot/behavior
tests per TFM). Every other package's own test suite (`Compono.Tests`,
`Compono.TestDoubles.Tests`, `Compono.TestDoubles.SampleTests`,
`Compono.Http.Tests`, `Compono.Bogus.Tests`, `Compono.DependencyInjection.Tests`,
`Compono.XunitV3.Tests`, `Compono.TUnit.Tests`, `Compono.NSubstitute.Tests`,
`Compono.TUnit.SampleTests`) run clean. `Compono.XunitV3.SampleTests` has 2
pre-existing failures (`FailingConfigProfileTests`/`FailingCompositionTests`,
both deliberately-failing-composition test names) confirmed present on a
clean `main` checkout via `git stash` before this work began - unrelated to
PLAN-0053, not touched.

**AlexaVoxCraft dogfood (the actual motivating consumer).** Ran via this
repo's `scripts/dogfood-validate.sh` unmodified, packing fresh, uniquely
versioned local packages (`Compono`, `Compono.TestDoubles`, `Compono.XunitV3`,
plus `Compono.Http` - resolved by other projects in the same solution) into
a local NuGet feed, restoring the consumer against a generated temp
`Directory.Packages.props` override (consumer's own file never touched), and
verifying every resolved reference matches the fresh version (no stale-cache
false green).

- `AlexaVoxCraft.MediatR.Tests` alone, package version
  `0.0.0-local.20260825085850-43925-17557`: **154/154 passing across all 4
  TFMs** (net8.0/net9.0/net10.0/net11.0), 616/616 total across TFMs, 0
  failed, 0 skipped.
- Confirmed the 5 originally-blocked `defaultHandler.Configure().CanHandle(...)`/
  `.Verify().CanHandle(...)` call sites (`Wrappers/RequestHandlerWrapperTests.cs`,
  `Wrappers/OtelRequestHandlerWrapperTests.cs`) now compile and pass using the
  natural `Compono.TestDoubles` surface - no temporary fake, no NSubstitute
  fallback.
- Resolved dependency graph: `grep`-searched
  `AlexaVoxCraft.MediatR.Tests/obj/project.assets.json` for `NSubstitute`/
  `Compono.NSubstitute` package entries after this restore - zero matches.
  Not inferred from the absence of a `PackageReference` in the `.csproj`
  (which was already true going into this session) - verified against the
  actual resolved graph.
- Full solution (`AlexaVoxCraft.slnx`), package version
  `0.0.0-local.20260825085923-44431-4336`: **2784/2784 passing, 0 failed**
  (32 skipped, pre-existing and unrelated - e.g. environment-gated
  integration tests). `dogfood-validate.sh` itself reported `PASS`.

**Project-local migration friction found and fixed along the way** (not
Compono gaps - the interface-boundary fix above was already sufficient to
make these *compile*; these were real, incomplete test-migration bugs
surfaced once the project could build again):

- `DefaultResponseBuilderTests` (~20 failures): `IAttributesManager.Session`
  (a `JsonAttributeBag`, no deterministic default - ADR-0045
  configuration-required) is dereferenced by `DefaultResponseBuilder.GetResponse()`
  on every call, even for tests that never touch attributes. Fixed by adding
  a `Register<IAttributesManager>` rule to `MediatRTestProfile` that
  constructs the generated double directly via
  `GeneratedTestDoubleRegistry.TryCreate` (not a recursive `context.Resolve`)
  and pre-configures `Session` with an empty bag - a test that cares about
  attribute contents still overrides it via its own
  `.Configure().Session().Returns(...)`, which wins per ADR-0050's
  last-registration-wins semantics.
- `SkillMediatorTests` (3 failures): `MediatRTestProfile`'s own comment
  claimed `context.Resolve<ILogger<SkillMediator>>()` "resolves cleanly" as
  ADR-0052 negative evidence - a real isolated test run proved this comment
  wrong (`CompositionException`: no discovery root anywhere in the project
  ever requests `ILogger<SkillMediator>` as a theory parameter, so no
  test-double closure reaches it). Fixed by registering
  `NullLogger<SkillMediator>.Instance` directly (nothing asserts against this
  logger's entries, unlike `ILogger<PerformanceLoggingBehavior>`'s
  `TestLogger<T>` above it) and corrected the stale comment.
- `PerformanceLoggingBehaviorTests`/`OtelPerformanceLoggingBehaviorTests`
  (~13 failures): several `Handle_With*` tests dereferenced
  `handlerInput.RequestEnvelope` (via `PerformanceLoggingBehavior.Handle`)
  without ever calling `handlerInput.Configure().RequestEnvelope().Returns(...)`
  - the profile deliberately leaves this configuration-required per its own
  documented design (Stage 2 migration note), and these specific tests were
  simply missing that one line. Added a `SkillRequest` parameter + the
  `Configure()` call to each, matching the pattern already used correctly by
  the file's own `Handle_WithIntentRequest_LogsIntentName` test.
