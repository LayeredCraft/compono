# [PLAN-0045] Compono.TestDoubles: Configuration-Required Members

**Status:** Not Started

**Implements:** [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)

## Goal

A `Compono.TestDoubles` member (property or method, including through
`Task<T>`/`ValueTask<T>`) that returns a non-nullable reference type with
no deterministic default no longer rejects its whole interface at
generation time, **provided the member would otherwise have a real
`Configure()`/`Verify()` surface** — the interface generates, and that
specific member throws a clear `TestDoubleNotConfiguredException` if
invoked before `Configure().Member(...).Returns(...)`/`.Throws(...)` is
called, via the new `CMP0032` diagnostic (one per interface, not one per
member). A member with no deterministic default that *also* has no
configuration surface for an unrelated reason (a diamond collision, a
zero-argument-extension collision, an overloaded `ref`/`out`/`in`
parameter) is unaffected — it keeps its unchanged `CMP0025` whole-
interface rejection, same as today, so no member ever ends up throwing
unconditionally with no way to configure it. A real
`dotnet publish -p:PublishAot=true` run proves the new dispatch shape
stays AOT-safe, and a third `lightsaber-skill` dogfooding pass measures
whether real tests can now actually drop `Compono.NSubstitute` — not just
how many interfaces generate.

## Scope

Builds exactly what [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)
decided: a new member-scoped dispatch fallback (throw instead of a
computed default) for the one specific `CMP0025` sub-case ADR-0045
identifies, reusing the existing `ReturnConfig<T>`/`ReturnConfigBuilder<T>`
state machinery unchanged, plus one new exception type
(`TestDoubleNotConfiguredException`) and one new diagnostic (`CMP0032`).
Explicitly deferred/out of scope, per ADR-0045's own boundaries: relaxing
whole-interface rejection for any other unsupported shape (pointer
parameters, `ref`/`out`/`in` parameters without a sibling overload,
unconstrained `T?` type parameters, a generic method whose return type
depends on its own type parameter); special-casing fluent self-return
(rejected in ADR-0045); manufacturing or composing return values
(rejected in ADR-0045); reopening ADR-0044/PLAN-0044 (both stay
`Accepted`/`Done`, untouched).

## Tasks

### Phase 0 — Configuration-required return semantics (Not Started)

- [ ] `src/Compono/TestDoubleNotConfiguredException.cs`: new `sealed`
      exception type, message-only constructor, matching
      `TestDoubleVerificationException`'s exact shape.
- [ ] `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs`: add
      `CMP0032` ("Test-double member(s) require explicit configuration"),
      `DiagnosticSeverity.Info`, **interface-scoped, count-only message
      text** (per ADR-0045 Amendment 1: one diagnostic per interface,
      fired once with a count of how many members require configuration,
      not one per member — avoids diagnostic-noise blowup on a large
      real-world interface like `IAmazonS3`; the exact member identity is
      supplied precisely by `TestDoubleNotConfiguredException` at the
      point a configuration-required member is actually invoked
      unconfigured, so the diagnostic doesn't need to enumerate members by
      name to stay useful). **`CMP0025`'s message text/descriptor is
      unchanged** (ADR-0045 Amendment 4 — it still describes all four of
      its original shape sub-cases, including "a non-nullable reference
      type with no deterministic default"): only the *condition* for
      reaching that fourth branch changes, per the analyzer task below —
      it now fires only when the member wouldn't have had a configuration
      surface anyway (Amendment 3's combined-shape case); every other
      no-default member takes the new `CMP0032` path instead.
- [ ] `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs`: at the
      method-return-type check (`TryGetDefaultExpression` failure for a
      method's return type) and the property-type check (same failure for
      a property's type), stop returning whole-interface `Failure(...)`
      for the "non-nullable reference, no deterministic default" case —
      **but only when the member would otherwise have a real
      `HasConfigurationSurface` (Amendment 3)**: genuinely-unimplementable
      shapes (by-ref, pointer, ref-like, checked separately just above
      these two call sites) keep failing exactly as today, and so does a
      member that combines "no deterministic default" with "no
      configuration surface for an unrelated reason" (a diamond-colliding
      identity, a zero-argument-extension collision, or an overloaded
      `ref`/`out`/`in` parameter) — reuse whichever of
      `isDiamondCollision`/`isZeroArgCollision`/`hasRefOutInParameter` is
      already computed at that point rather than adding new detection
      logic. Only when a real surface would exist: mark the member as
      configuration-required (member-scoped for *generation* purposes,
      following the same shape `CMP0030`'s out-parameter exclusion
      already uses to keep an interface generating while excluding just
      one member's full surface) and collect it into a per-interface
      count, the same "collect across the member-walk" shape `CMP0028`
      already uses. After the full member walk, if that count is nonzero,
      emit exactly one `CMP0032` for the interface, naming the interface
      and the count — not one per member (Amendment 1).
- [ ] `src/Compono.Generators/Emitters/TestDoubleEmitter.cs` /
      `src/Compono.Generators/Templates/TestDouble.scriban`: new dispatch-
      body branch for a configuration-required member — identical
      `RecordCall()`/`HasConfiguredException`/`HasConfiguredValue` shape
      every member already has, with the final fallback emitting
      `throw new global::Compono.TestDoubleNotConfiguredException(...)`
      (a fully literal, generation-time-computed message: interface name,
      member name and signature, the fix hint) instead of a computed
      default expression. `Configure()`/`Verify()` extension generation
      for this member is **unchanged** — it already works for any `T`
      regardless of whether `T` has a default, since
      `ReturnConfigBuilder<T>.Returns`/`.Throws` never depended on one.
- [ ] `test/Compono.Generators.Tests/`: generator snapshot/behavior tests
      — a configuration-required method member, a configuration-required
      property member, confirming (a) the interface still generates, (b)
      exactly **one** `CMP0032` fires for the interface with the correct
      count in its text (not one per member), (c) an interface with
      *multiple* configuration-required members (an `IAmazonS3`-shaped
      regression case) still emits exactly one `CMP0032`, with the count
      matching, (d) every other member on the same interface is
      unaffected, (e) `CMP0025` still fires unchanged for a genuinely
      unimplementable shape on a *different* interface (regression
      coverage — this ADR narrows `CMP0025`'s scope, doesn't remove its
      remaining trigger), (f) **`CMP0025`/`CMP0024` also still fire
      unchanged for the combined shapes Amendments 3 and 5 identify** —
      an overloaded `ref`/`out`/`in` member with a no-default return type,
      a diamond-colliding member with a no-default return type, and an
      object-member-collision-shaped method (`ToString`/`GetHashCode`/
      `GetType`/`Equals`) with a no-default return type (Amendment 5 —
      this one relies on `Failure(...)` unconditionally discarding any
      provisional configuration-required marking, so it's worth proving
      empirically, not just trusting the Amendment's own reasoning) —
      each on an interface that also has an unrelated, genuinely
      configuration-required member, confirming the combined-shape gate
      doesn't accidentally suppress `CMP0025`/`CMP0024` or leak a
      surfaceless member into `CMP0032`'s count.
- [ ] `test/Compono.TestDoubles.Tests/`: packaged-consumer behavior tests
      — a configuration-required member throws
      `TestDoubleNotConfiguredException` when unconfigured, returns the
      configured value after `Returns(...)`, throws the configured
      exception after `Throws(...)` — same three-state coverage every
      other member type already has. Note this project uses a
      `ProjectReference` to `Compono.TestDoubles`, not a packaged
      `.nupkg` consumer — see the packaged smoke test below for the
      cross-assembly proof this alone doesn't provide.
- [ ] **Packaged-consumer smoke test, this phase's own shape only**
      (added per Codex review — matching PLAN-0044's own established
      pattern, added there for the identical reason: `dotnet pack` core
      `Compono`/`Compono.Generators` into a local feed, a throwaway
      consumer project referencing the packed `.nupkg` (never a
      `ProjectReference`) with `ComponoGeneratedTestDoubles=true`,
      exercising a configuration-required method, property, and the
      combined-shape regression case end to end with a real `dotnet
      build`/`dotnet run`. PLAN-0044's own Notes record that every defect
      its review round found (`CS0122`, `CS0460`, `CS0111`, `CS0214`,
      `CS0177`) was exactly the class of cross-assembly compile failure an
      in-process snapshot test cannot catch — this phase does not ship
      (its own PR does not merge) until this smoke test is green, rather
      than deferring all packaged proof to Phase 2.
- [ ] **Docs, this phase's own shape** (moved here from a later docs-only
      phase per Codex review — matching PLAN-0044's own precedent for the
      identical reason: `references/documentation.md`'s "update the
      relevant doc in the same PR" rule means Phase 0 shipping the public
      exception, the runtime behavior, and `CMP0032` as its own PR can't
      leave `docs/packages/compono-testdoubles.md`/`docs/reference/diagnostics.md`
      still describing `CMP0025` as unconditional whole-interface
      rejection until some later PR):
  - `docs/packages/compono-testdoubles.md`: new "Configuration-required
    members" section (parallel to the existing "Overloaded members"/
    "Generic methods"/"Call verification" sections) documenting the
    dispatch rule and a real example using one of RESEARCH-0004's
    acceptance interfaces. Update "Deterministic defaults for
    unconfigured members" to cross-reference the new section rather than
    imply every non-nullable-reference return is still a hard rejection.
  - `docs/reference/diagnostics.md`: `CMP0025`'s entry is **not**
    narrowed (ADR-0045 Amendment 4) — update its Cause text to note the
    fourth sub-case now only fires when the member also has no
    configuration surface for an unrelated reason, cross-referencing the
    new "Configuration-required members" doc section for the ordinary
    case. Add a `CMP0032` entry (Cause/Fix, matching the existing
    entries' shape) explaining it's one diagnostic per interface (a
    count), not whole-interface rejection.
  - `skills/compono/references/diagnostics.md`: same two updates,
    keeping the skill-local summary table consistent with the canonical
    file (per the pattern PLAN-0044 Phase 4 already established for
    keeping these two files in sync).
  - `skills/compono/references/testdoubles.md`: document the new
    configuration-required-member behavior for agent-facing migration
    guidance — in particular, that an agent migrating a test off
    `Compono.NSubstitute` should now expect some generated members to
    require explicit `Returns(...)`/`Throws(...)` before use, rather than
    assuming "it generated, therefore every call is safe unconfigured."

### Phase 1 — Async and fluent-return regression coverage (Not Started)

- [ ] `test/Compono.TestDoubles.Tests/` (or `SampleTests`): a
      `Task<TReference>`-returning configuration-required member and a
      `ValueTask<TReference>`-returning one, both states (unconfigured
      throws, configured returns/throws) — proving ADR-0045's "no
      separate implementation needed" claim empirically, not just by
      design reasoning. If this surfaces a real gap (contrary to the
      ADR's expectation), record it as an ADR-0045 Amendment before
      proceeding, per this repo's Amendment convention — don't silently
      patch around it.
- [ ] A fluent self-returning member (`IResponseBuilder`-shaped: a method
      returning the interface itself) — confirm it's configuration-
      required like any other non-nullable reference return (no special
      case), and that configuring it (`Returns(self)`) works for a
      chained-call test, matching ADR-0045's "Fluent self-returning
      members" decision.
- [ ] Confirm zero behavior change for every already-shipped
      deterministic-default member shape (`bool`, `int`, nullable
      reference, `Task`, known collection shapes) — existing v1/v2 tests
      continue passing unmodified; add one small regression test mixing a
      configuration-required member and a deterministic-default member on
      the same interface if no existing test already covers this
      combination.

### Phase 2 — Packaged/AOT verification (Not Started)

Phase 0's own lightweight packaged smoke test already proves basic
cross-assembly compilation; this phase is the AOT-specific proof and the
full supported-TFM matrix, not the first point this feature gets packaged
at all.

- [ ] Extend `test/Compono.TestDoubles.AotSmokeTest/Program.cs` to
      exercise a configuration-required synchronous method, a
      configuration-required property, and a configuration-required
      `Task<T>`-returning method — both the configured-success path and
      the throws-when-unconfigured path — under a real
      `dotnet publish -p:PublishAot=true` run. Manually verify zero
      IL2xxx/IL3xxx warnings and a correct exit code, per this repo's
      "prove it, don't assume it" standard (PLAN-0044 Phase 3's same
      discipline).
- [ ] `test/Compono.TestDoubles.SampleTests/`: a real packaged-`.nupkg`
      test proving the same shapes across all supported TFMs, matching
      PLAN-0044 Phase 3's existing pattern (no workflow change expected —
      runs automatically in CI via `package-validation.yaml`).
- [ ] Performance: no new benchmark class added preemptively (ADR-0045's
      "Performance" section, per ADR-0034's benchmark-only-if-real-risk
      policy). If implementation surfaces an actual measured concern
      during this phase, record it as an ADR-0045 Amendment and add a
      targeted benchmark then — not before.

### Phase 3 — Documentation consistency pass (Not Started)

Every doc touch introducing this feature's own behavior already happened
in Phase 0 (moved there per Codex review, matching PLAN-0044's own
precedent — see Phase 0's "Docs, this phase's own shape" task). Unlike
PLAN-0044 (which phased overloads/generics/verification across three
separate PRs and needed a real cross-cutting consistency pass), this
plan's only behavior-introducing phase is Phase 0 — so this phase is
narrower: a final repo-wide sweep for anything Phase 0's own doc task
wouldn't have touched directly.

- [ ] Re-check `docs/troubleshooting/common-errors.md` and
      `docs/getting-started/ai-agent-skill.md` for the same stale-range-
      cap pattern PLAN-0044 Phase 4 found and fixed there (`CMP0020`-
      `CMP0031` ranges now need to include `CMP0032`).
- [ ] Grep the repo for any other stale `CMP0020`-`CMP0031`-style range
      caps or "returning a non-nullable reference always rejects" claims
      outside historical/ADR context, matching the proactive sweep
      PLAN-0044 Phase 4 ran before its own final push.

### Phase 4 — Third `lightsaber-skill` dogfood (Not Started)

- [ ] Re-run the exact `lightsaber-skill` migration analysis (same method
      as RESEARCH-0004) against the shipped implementation of this ADR.
      Quantify against the acceptance cases: `IResponseBuilder`,
      `IAmazonS3`, `ISkillMediator`, `IOptions<LightsaberOptions>`,
      `ILambdaContext`, `IHandlerInput` — which now generate; which of
      their members are configuration-required vs. deterministic-default;
      whether `ILogger<T>` (already working under v2) still works
      unchanged (regression check, not a redesign target).
- [ ] **The acceptance criterion is "can real tests remove
      `Compono.NSubstitute`," not "do more interfaces generate."**
      Quantify against the same ~40 original NSubstitute call sites: how
      many can now migrate; how many tests, if any, can drop
      `Compono.NSubstitute` entirely; whether any test still needs both
      providers side by side and why.
- [ ] Record the result as a new `docs/research/*.md` finding (next
      sequential number after RESEARCH-0004), following the same
      evidence-record convention. Update `docs/roadmap/post-mvp.md`'s
      entry for this candidate accordingly — move it from "outstanding"
      to "shipped" only if the real-test-removal bar is actually met; if
      it's a partial improvement short of that bar, record the honest
      result the same way RESEARCH-0004 did, and open a further roadmap
      candidate for any residual gap rather than overstating this one.

## Critical Files

- `src/Compono/TestDoubleNotConfiguredException.cs` — new exception type.
- `src/Compono/ReturnConfig.cs`, `ReturnConfigBuilder.cs` — unchanged,
  reused as-is; listed for reviewer visibility that nothing here changes.
- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` — new
  `CMP0032` descriptor; `CMP0025`'s own message text is unchanged
  (Amendment 4) — only the analyzer condition for reaching it narrows.
- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — the method-
  return-type and property-type default-lookup failure branches change
  from whole-interface `Failure(...)` to member-scoped configuration-
  required marking, for the one sub-case ADR-0045 scopes.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs`,
  `src/Compono.Generators/Templates/TestDouble.scriban` — new
  configuration-required dispatch-body branch.
- `test/Compono.Generators.Tests/`, `test/Compono.TestDoubles.Tests/`,
  `test/Compono.TestDoubles.SampleTests/`, `test/Compono.TestDoubles.AotSmokeTest/Program.cs` —
  new coverage per phase above.
- `docs/packages/compono-testdoubles.md`, `docs/reference/diagnostics.md`,
  `skills/compono/references/diagnostics.md`,
  `skills/compono/references/testdoubles.md` — doc/skill alignment for
  this feature's own behavior (Phase 0, per Codex review).
- `docs/troubleshooting/common-errors.md`,
  `docs/getting-started/ai-agent-skill.md` — final stale-range-cap sweep
  (Phase 3).
- `docs/roadmap/post-mvp.md`, a new `docs/research/000N-*.md` — Phase 4's
  dogfood result.

## Test Plan

Matches `references/testing.md`'s existing pattern for this feature area
(established by PLAN-0043/PLAN-0044): generator-level snapshot/behavior
tests for the analysis and diagnostic changes (Phase 0), packaged-consumer
behavior tests for the three dispatch states (unconfigured throws,
configured-return, configured-throws) across the sync/property/async
shapes (Phases 0-1), a real `PublishAot=true` execution proof rather than
static AOT-safety analysis (Phase 2, "prove it, don't assume it"), and a
real external-project dogfooding pass as the final acceptance test
(Phase 4) rather than relying on in-repo tests alone to validate the
real-world claim this ADR is motivated by.

## Notes

Phase 1's "no separate implementation needed for async" expectation
(ADR-0045's own reasoning, based on `ReturnConfig<T>` already being
generic over the member's real declared return type) is a hypothesis
carried into the plan, not a certainty — Phase 1's task list explicitly
calls for recording an ADR-0045 Amendment if implementation proves it
wrong, rather than silently reshaping the plan around a surprise.
