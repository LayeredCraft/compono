# [PLAN-0044] Compono.TestDoubles v2: Overloads, Generic Methods, Minimal Call Verification

**Status:** Not Started

**Implements:** ADR-0044 (design), ADR-0043 (v1 base design), ADR-0042
(admitted problem)

## Goal

An interface declaring overloaded members (per-overload, not
per-interface, rejection granularity), or a generic method whose return
type doesn't depend on its own type parameter(s) (e.g. `ILogger<T>`'s
`Log<TState>`/`BeginScope<TState>`), generates a working `Compono.TestDoubles`
double instead of falling back to the runtime-provider path — and
`mediator.Verify().Send().Once()`/`.Never()`/`.Exactly(n)` lets a test
assert a member was actually called, closing the three gaps
`docs/roadmap/post-mvp.md`'s `lightsaber-skill` dogfooding finding
identified. A real `dotnet publish -p:PublishAot=true` run proves all
three new shapes together stay Native-AOT-safe, and a second dogfooding
pass against `lightsaber-skill` quantifies how much the finding actually
improved.

## Scope

Builds exactly what [ADR-0044](../adr/0044-compono-testdoubles-v2-overloads-generics-verification.md)
decided: per-overload configuration via typed, value-ignored discriminator
parameters; generic-method support scoped to return types independent of
the method's own type parameters; a `Verify()` bridge with
`Never`/`Once`/`Exactly(n)` backed by an `Interlocked`-incremented counter
folded into the existing `ReturnConfig<T>` slot. Explicitly deferred, per
ADR-0044's own Non-Goals: argument matchers, call-order verification,
argument-aware call recording, per-closed-generic-instantiation
configuration, interface-level partial support for indexers/events/other
still-unsupported shapes, class/protected/static-abstract-member support.

## Tasks

### Phase 0 — Overloaded-member support

- [ ] `TestDoubleAnalyzer`: replace whole-name `duplicateConfigurationMemberNames`
      rejection with per-overload analysis — group by name, but only flag
      a genuine ambiguity (identical parameter-type signature, which C#
      itself can't produce, so effectively unreachable) rather than every
      repeated name.
- [ ] Per-overload field/extension identity: a new identifier-hash helper
      keyed on the overload's full parameter-type list, reusing
      `TestDoubleIdentifierNaming`'s existing sanitizer + FNV-1a-hash
      convention (sibling helper, not a modification).
- [ ] `TestDoubleEmitter`/`TestDouble.scriban`: emit one `ReturnConfig<T>`
      field and one typed-parameter configuration extension per overload;
      dispatch bodies for every overload regardless of whether that
      specific overload's own shape is independently supported.
- [ ] Overload-set-internal partial support: an overload whose own shape
      is unsupported (`ref`/`out`/`in`, pointer, non-nullable-no-default
      return) gets a deterministic-default dispatch body and an
      informational diagnostic, but does **not** reject its sibling
      overloads.
- [ ] `DiagnosticDescriptors`: narrow `CMP0022`'s message to name the
      specific unsupported overload, not the whole member name.
- [ ] `Verify()`-tests (generator-output snapshots): `IResponseBuilder`-shaped
      interface (`Speak(string?)`/`Speak(params ISsml[])`), a mixed
      supported/unsupported overload set, a diamond-shaped inherited
      overload.

### Phase 1 — Generic-method support

- [ ] `TestDoubleAnalyzer`: replace the blanket `IsGenericMethod → reject`
      check with "does the return type's syntax tree reference any of the
      method's own type parameters" — reject only that case, under a
      refined diagnostic (next available code after `CMP0028`).
- [ ] Constraint-clause propagation: emit each type parameter's
      `where T : ...` clause verbatim (reference-type/value-type/`notnull`/
      base-type/interface constraints), extending the existing
      `SymbolDisplay`-based type-reference emission rather than inventing
      new text-building logic.
- [ ] Nullable-annotation preservation on type-parameter-referencing text,
      reusing `NullableAwareFullyQualifiedFormat`.
- [ ] `TestDoubleEmitter`/`TestDouble.scriban`: explicit interface
      implementation stays generic (type parameters + constraints);
      configuration extension stays non-generic, member-level, exactly
      like an ordinary member.
- [ ] `Verify()`-tests: an `ILogger<T>`-shaped interface (`Log<TState>`,
      `BeginScope<TState>` — the actual motivating shape), a
      multi-type-parameter generic method, a still-unsupported
      generic-return-type method (diagnosed, not emitted).

### Phase 2 — Minimal call recording and verification

- [ ] Core `Compono`: add `internal int CallCount` + `public readonly int
      ConfiguredCallCount` to `ReturnConfig<T>` (same internal-write/
      public-read split as `HasValue`/`Value`/`Exception`, per ADR-0043
      Amendment 3 — no new per-member field).
- [ ] Core `Compono`: new `CallVerifier` (public readonly struct,
      `Never()`/`Once()`/`Exactly(int)`) and `TestDoubleVerificationException`
      (plain `Exception` subtype, matching `CompositionException`'s
      convention — no xUnit/TUnit/AwesomeAssertions reference from core).
- [ ] `TestDoubleEmitter`/`TestDouble.scriban`: `Interlocked.Increment(ref
      __member.CallCount)` at the top of every dispatch body (methods and
      property accessors alike), unconditionally — a call counts whether
      it hits configured, default, or throw behavior.
- [ ] New per-interface generated shape: `<Hash>_VerifyExtension`
      (`Verify(this TInterface)` bridge, same cast-failure-message
      convention as `Configure()`), `<Hash>_DoubleVerifier` (the distinct
      wrapper type `Verify()` returns, avoiding `Configure()`/`Verify()`
      extension-resolution ambiguity), `<Hash>_DoubleVerification`
      (per-member/per-overload `CallVerifier`-returning extensions, reusing
      Phase 0's overload-discriminator mechanism where applicable).
- [ ] Public-API-surface approval test update (`Compono.Tests` and/or
      `Compono.TestDoubles.Tests`, matching the existing pattern) for
      `CallVerifier`/`TestDoubleVerificationException`/`ReturnConfig<T>`'s
      new members.
- [ ] Concurrency test: parallel calls to the same double member, asserted
      via `Verify().Member().Exactly(n)` for a known `n`, proving
      `Interlocked.Increment` correctness under real contention (not just
      single-threaded).
- [ ] `Verify()`-tests: `Once`/`Never`/`Exactly(n)` pass and fail paths
      (fail path asserts the thrown `TestDoubleVerificationException`'s
      message), a verified call that also has configured `Returns`/`Throws`
      behavior (proving counting and configured-behavior dispatch don't
      interfere), overload-scoped verification.

### Phase 3 — AOT, performance, and package verification

- [ ] `test/Compono.TestDoubles.AotSmokeTest`: extend to exercise all
      three new shapes together (an overloaded member, a covered generic
      method, a verified call) in the same `dotnet publish
      -p:PublishAot=true` run — zero `IL2xxx`/`IL3xxx` warnings, real
      execution, matching PLAN-0043 Phase 2's own standard.
- [ ] `test/Compono.TestDoubles.SampleTests`: extend the packaged-consumer
      sample with the same three shapes, proving the real NuGet-packaged
      path (not just the in-process generator harness), matching PLAN-0043
      Phase 2's local-feed pattern.
- [ ] Targeted benchmarks only (per ADR-0044's "AOT and performance"
      section — not a general competitive suite): `Interlocked.Increment`
      overhead per verified call, and overload-dispatch overhead for a
      member with several sibling overloads — both following this repo's
      existing benchmark-suite policy (ADR-0034), non-misleading
      comparisons only.

### Phase 4 — Docs and skill alignment

- [ ] `docs/packages/compono-testdoubles.md` — document overload
      discriminators, generic-method scope, `Verify()`/`Never`/`Once`/
      `Exactly(n)`, and the still-excluded shapes (updated "AutoFixture/
      NSubstitute-habit trap" section — verification exists now, but
      remains deliberately minimal, not general).
- [ ] `skills/compono/references/testdoubles.md` — same content, skill
      shape; explicit guidance on when a shape still needs
      `Compono.NSubstitute` (argument matchers, call-order, class mocking).
- [ ] `docs/reference/diagnostics.md` — new/narrowed diagnostic entries
      (overload-scoped `CMP0022`, the new generic-return-type-dependent
      code).
- [ ] `docs/roadmap/post-mvp.md` — move the `lightsaber-skill` finding from
      "outstanding" to "shipped," matching the existing `ComposeAttribute`
      graduation entry's shape, once Phase 5 confirms real improvement.
- [ ] `docs/plans/README.md` status flip to `Done`.

### Phase 5 — Re-dogfood against `lightsaber-skill`

- [ ] Re-run the exact `lightsaber-skill` migration analysis against the
      shipped v2 package. Quantify: how many of the ~40 original
      NSubstitute call sites now migrate; whether `ILogger<T>` and
      `IResponseBuilder` now generate; how much (if any) of `IAmazonS3`
      now generates given its overloads are fixed; whether the two
      `Received(1)` sites now migrate to `Verify().Send().Once()`; whether
      any test still needs both `Compono.NSubstitute` and
      `Compono.TestDoubles` side by side.
- [ ] Record the result in `docs/roadmap/post-mvp.md`'s finding entry
      (graduated per Phase 4's task) — success criterion is **material
      improvement** ("small minority, no practical dependency reduction" →
      "viable for a meaningful portion of the suite"), not zero remaining
      NSubstitute usage. Any residual gap goes back through this repo's
      normal evidence-based roadmap process (a new `docs/research/*.md`
      finding or a fresh roadmap-candidate ADR), not folded into this plan
      after the fact.

## Critical Files

- `src/Compono.Generators/Discovery/TestDoubleAnalyzer.cs` — per-overload
  analysis, generic-method return-type-dependency check.
- `src/Compono.Generators/Discovery/TestDoubleDefaults.cs` — unchanged in
  spirit; may need a shared helper for "does this type reference type
  parameter X" (generic-method scoping).
- `src/Compono.Generators/Emitters/TestDoubleIdentifierNaming.cs` — new
  sibling helper for per-overload discriminator hashing.
- `src/Compono.Generators/Emitters/TestDoubleEmitter.cs`,
  `src/Compono.Generators/Templates/TestDouble.scriban` — per-overload
  fields/extensions, generic method/constraint emission, `Verify()`
  bridge + verifier extension classes, `Interlocked.Increment` dispatch.
- `src/Compono.Generators/Models/TestDoubleMemberInfo.cs` and siblings —
  new fields for overload-discriminator identity and generic type
  parameter/constraint data.
- `src/Compono.Generators/Diagnostics/DiagnosticDescriptors.cs` — `CMP0022`
  message narrowing, new generic-return-type-dependent code.
- `src/Compono/ReturnConfig.cs` — `CallCount`/`ConfiguredCallCount`.
- `src/Compono/CallVerifier.cs`, `src/Compono/TestDoubleVerificationException.cs` —
  new core primitives.
- `test/Compono.Generators.Tests/` — new `Verify()` snapshot tests for
  overloads, generic methods, and the `Verify()` bridge's generated shape.
- `test/Compono.Tests/` or `test/Compono.TestDoubles.Tests/` —
  `CallVerifier` unit tests (pass/fail/message), concurrency test.
- `test/Compono.TestDoubles.SampleTests/`, `test/Compono.TestDoubles.AotSmokeTest/` —
  extended with all three new shapes.
- `docs/packages/compono-testdoubles.md`, `skills/compono/references/testdoubles.md`,
  `docs/reference/diagnostics.md`, `docs/roadmap/post-mvp.md`.

## Test Plan

Matches `references/testing.md` and PLAN-0043's own established pattern:
`Verify()`-based generator-output snapshot tests per new shape (including
mixed supported/unsupported overload sets and the still-unsupported
generic-return-type case), unit tests for `CallVerifier`/`ReturnConfig<T>`'s
new members in isolation, a concurrency test for the `Interlocked`
call counter, extended packaged-sample and AOT-smoke-test coverage
exercising all three shapes together through the real packaged path, and
targeted (not general) benchmarks per ADR-0044's "AOT and performance"
section.

## Notes

Phase boundaries follow the decomposition confirmed directly with the
requester when this plan was drafted: overloads and generics are
independent generator-analysis changes and ship as separate phases/PRs
even though both touch `TestDoubleAnalyzer`/`TestDoubleEmitter` closely,
per `design-decisions.md`'s "each phase ships as its own PR" rule.
Verification (Phase 2) depends on neither but is ordered after both
because its own tests are more useful once overload-scoped verification
(`Verify().Speak(string.Empty)`) has real overloaded members to verify
against. Phase 3 (AOT/perf) intentionally comes after all three shapes
exist, matching PLAN-0043's own "prove the whole thing together" AOT
phase rather than three separate partial AOT proofs. Phase 5 (re-dogfood)
is last by construction — it measures the other phases' real-world effect
and can't run before they ship.
