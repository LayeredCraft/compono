# [PLAN-0045] Compono.TestDoubles: Configuration-Required Members

**Status:** Not Started

**Implements:** [ADR-0045](../adr/0045-testdoubles-configuration-required-members.md)

## Goal

A `Compono.TestDoubles` member (property or method, including through
`Task<T>`/`ValueTask<T>`) that returns a non-nullable reference type with
no deterministic default no longer rejects its whole interface at
generation time — the interface generates, and that specific member
throws a clear `TestDoubleNotConfiguredException` if invoked before
`Configure().Member(...).Returns(...)`/`.Throws(...)` is called. `CMP0025`
narrows to cover only the three genuinely unimplementable return shapes
(by-ref, pointer, ref-like); a new `CMP0032` covers the configuration-
required case, member-scoped rather than whole-interface. A real
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
      `CMP0032` ("Test-double member requires explicit configuration"),
      `DiagnosticSeverity.Info`, member-scoped message text (does not
      claim whole-interface fallback). Narrow `CMP0025`'s message text so
      it only describes the three remaining genuinely-unimplementable
      shapes (by-ref, pointer, ref-like) — the fourth sub-case moves to
      `CMP0032` instead of sharing `CMP0025`'s text.
- [ ] `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs`: at the
      method-return-type check (`TryGetDefaultExpression` failure for a
      method's return type) and the property-type check (same failure for
      a property's type), stop returning whole-interface `Failure(...)`
      for the "non-nullable reference, no deterministic default" case
      specifically — genuinely-unimplementable shapes (by-ref, pointer,
      ref-like, checked separately just above these two call sites) keep
      failing exactly as today. Instead, mark the member as
      configuration-required (member-scoped, following the same shape
      `CMP0030`'s out-parameter exclusion already uses to keep an
      interface generating while excluding just one member's full
      surface) and emit `CMP0032`, info-severity, naming the interface
      and member.
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
      `CMP0032` fires with correct interface/member text, (c) every other
      member on the same interface is unaffected, (d) `CMP0025` still
      fires unchanged for a genuinely unimplementable shape on a
      *different* interface (regression coverage — this ADR narrows
      `CMP0025`'s scope, doesn't remove its remaining trigger).
- [ ] `test/Compono.TestDoubles.Tests/`: packaged-consumer behavior tests
      — a configuration-required member throws
      `TestDoubleNotConfiguredException` when unconfigured, returns the
      configured value after `Returns(...)`, throws the configured
      exception after `Throws(...)` — same three-state coverage every
      other member type already has.

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

### Phase 3 — Docs and skill alignment (Not Started)

- [ ] `docs/packages/compono-testdoubles.md`: new "Configuration-required
      members" section (parallel to the existing "Overloaded members"/
      "Generic methods"/"Call verification" sections) documenting the
      dispatch rule, the property/async/fluent-self-return decisions, and
      a real example using one of RESEARCH-0004's acceptance interfaces.
      Update "Deterministic defaults for unconfigured members" to
      cross-reference the new section rather than imply every non-
      nullable-reference return is still a hard rejection.
- [ ] `docs/reference/diagnostics.md`: narrow `CMP0025`'s entry to its
      remaining scope (by-ref/pointer/ref-like only); add a `CMP0032`
      entry (Cause/Fix, matching the existing entries' shape) explaining
      it's member-scoped, not whole-interface, and pointing at the new
      "Configuration-required members" doc section.
- [ ] `skills/compono/references/diagnostics.md`: same two updates,
      keeping the skill-local summary table consistent with the canonical
      file (per the pattern PLAN-0044 Phase 4 already established for
      keeping these two files in sync).
- [ ] `skills/compono/references/testdoubles.md`: document the new
      configuration-required-member behavior for agent-facing migration
      guidance — in particular, that an agent migrating a test off
      `Compono.NSubstitute` should now expect some generated members to
      require explicit `Returns(...)`/`Throws(...)` before use, rather
      than assuming "it generated, therefore every call is safe
      unconfigured."
- [ ] Re-check `docs/troubleshooting/common-errors.md` and
      `docs/getting-started/ai-agent-skill.md` for the same stale-range-
      cap pattern PLAN-0044 Phase 4 found and fixed there (`CMP0020`-
      `CMP0031` ranges now need to include `CMP0032`).

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
  `CMP0032` descriptor; `CMP0025`'s message text narrowed.
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
  `skills/compono/references/testdoubles.md`,
  `docs/troubleshooting/common-errors.md`,
  `docs/getting-started/ai-agent-skill.md` — doc/skill alignment (Phase 3).
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
