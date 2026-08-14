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
      rejection with per-overload analysis — group by **full signature
      identity** (see the identity-hash task below) **across the whole
      transitive closure** (`closure.SelectMany(i => i.GetMembers())`, the
      same iteration the existing pre-pass already uses — not scoped to
      one declaring interface at a time), flagging any group with more
      than one member. Two real overloads within one interface never
      share a full-signature identity (the compiler enforces that), but
      two same-named, same-shaped members inherited from *different* base
      interfaces (a diamond) genuinely do — corrected per ADR-0044
      Amendment 3 Finding 8, which also caught that an earlier plan draft
      mischaracterized this as "effectively unreachable." The existing
      `TestDoubleVerifyTests.DiamondInheritedSameNameProperty_ReportsOverloadedDiagnostic`
      must keep passing, adapted to this phase's new *scoped* outcome
      (Configure()/Verify() surface withheld for the colliding identity
      only, not the whole interface — a real improvement over v1's
      blanket rejection for this case, not a regression).
- [ ] Per-overload field/extension identity: a new identifier-hash helper
      keyed on the overload's full parameter-type list, **each
      parameter's `RefKind`, and generic arity (type-parameter count)**.
      `M()`/`M<T>()` (Amendment 2 Finding 3) and `M(int)`/`M(ref int)`
      (Amendment 3 Finding 7) are both legal overload pairs an identity
      keyed on parameter types alone would collapse to the same identity.
      **Each parameter type is canonicalized via one recursive transform
      before hashing, not a list of special cases** (Amendment 8 Finding
      19 — three consecutive review rounds each found one more
      non-signature-affecting decoration, so the fix generalizes instead
      of adding a fifth): walk the type through every generic type
      argument, array element type, and tuple element type, at every
      nesting level, and (a) strip nullable-reference annotation, (b)
      replace `dynamic` with `object`, (c) replace a named tuple with its
      underlying `ValueTuple<...>` form, (d) replace a reference to the
      member's own type parameter with its ordinal-position token
      (Amendment 5 Finding 11), (e) normalize `nint`/`nuint` to
      `System.IntPtr`/`System.UIntPtr` (Amendment 10). This covers
      `IA.M<T>(T)`/`IB.M<U>(U)`, `IA.M(string)`/`IB.M(string?)`,
      `IA.M(dynamic)`/`IB.M(object)`, `IA.M((int X, int Y))`/
      `IB.M((int A, int B))`, `IA.M(nint)`/`IB.M(System.IntPtr)`, and
      nested cases (`IEnumerable<(int X, int Y)>` vs
      `IEnumerable<(int A, int B)>`) uniformly. **Treat this as an open
      principle** ("exclude anything the C# compiler doesn't treat as
      signature-affecting"), not a closed enumeration — add a
      diamond-collision test for each case above, and don't assume a
      sixth can't surface during implementation. Include
      ref-kind and arity in the hash from this phase on, even though every
      Phase-0-supported overload has arity zero and no `ref`/`out`/`in`
      support until later, so later phases never have to change an
      already-shipped naming/hint-name scheme. Reuses
      `TestDoubleIdentifierNaming`'s existing sanitizer + FNV-1a-hash
      convention (sibling helper, not a modification).
- [ ] `TestDoubleEmitter`/`TestDouble.scriban`: dispatch bodies for every
      overload, regardless of whether that specific overload's own shape
      is independently supported. **A `ReturnConfig<T>` field and typed-
      parameter configuration extension are emitted only for an overload
      that actually gets a `Configure()` surface** (corrected per
      ADR-0044 Amendment 4 Finding 9 — an unconditional "one field per
      overload" would duplicate-declare a field for two diamond-colliding
      identities, which withhold `Configure()`/`Verify()` for both without
      rejecting the interface). An overload with no configuration surface
      — unsupported shape or diamond collision alike — gets an inline
      deterministic-default dispatch body with no backing field at all.
- [ ] Overload-set-internal partial support: an overload whose own shape
      is unsupported *and has a constructible fallback body* — **`ref`/
      `out`/`in` parameters only** — gets a deterministic-default dispatch
      body and an informational diagnostic, but does **not** reject its
      sibling overloads. Pointer/function-pointer parameters do **not**
      get this treatment, removed per ADR-0044 Amendment 5 Finding 12: a
      pointer-typed parameter requires the method to be declared `unsafe`
      regardless of whether the body touches it, and this feature never
      emits `unsafe` generated code or requires a consumer to set
      `AllowUnsafeBlocks` — restoring
      [ADR-0043 Amendment 10 Finding Y](../adr/0043-compono-generated-test-doubles-design.md#amendment-10-2026-08-13-set-only-properties-diagnosed-parameter-names-escaped-unsafe-parameter-shapes-diagnosed)'s
      existing, unchanged v1 disposition for this shape (whole-interface
      rejection). A return type with no deterministic default has no
      constructible body at any granularity and still triggers today's
      existing whole-interface rejection, unchanged from v1 — corrected
      per ADR-0044 Amendment 1, not the "gets a fallback body" treatment
      an earlier plan draft implied. **Every `out` parameter in a fallback
      body must be definitely assigned before every return path** (`CS0177`
      otherwise) — assign it `TestDoubleDefaults`'s own deterministic-
      default expression for its type, the same lookup already used for
      return types, not new logic (Amendment 8 Finding 20). **If that
      lookup fails for even one `out` parameter, the whole overload joins
      the no-constructible-body bucket above** (whole-interface rejection)
      rather than silently assigning `default` and risking a non-nullable-
      contract violation. `ref`/`in` parameters need no such handling —
      they're never required to be written.
- [ ] `DiagnosticDescriptors`: narrow `CMP0022`'s message to name the
      specific unsupported overload, not the whole member name.
- [ ] `Verify()`-tests (generator-output snapshots): `IResponseBuilder`-shaped
      interface (`Speak(string?)`/`Speak(params ISsml[])`), a mixed
      supported/unsupported overload set, a diamond-shaped inherited
      overload, **and one diamond test per identity-canonicalization case**
      (nullable annotation, `dynamic`/`object`, tuple element names,
      `nint`/`System.IntPtr` — Amendment 6/7/8/10 Findings 14, 17, 19, and
      Amendment 10; the generic-parameter-name case moves to Phase 1, once
      generic methods exist to test it with),
      **and a mixed overload set with an `out` parameter of a type with no
      deterministic default** (whole-interface rejection, Amendment 8
      Finding 20), alongside one with an `out` parameter that does have a
      default (definitely-assigned fallback body, same finding).
- [ ] **Packaged-consumer smoke test, this phase's own shape only**
      (added per ADR-0044 Amendment 6's process finding — see this plan's
      Notes section): `dotnet pack` core `Compono`/`Compono.Generators`
      into a local feed, a throwaway consumer project referencing the
      packed `.nupkg` (never a `ProjectReference`, matching every existing
      `*.SampleTests` project's own pattern) with
      `ComponoGeneratedTestDoubles=true`, exercising an overloaded
      interface end to end (including a supported `out`-parameter overload
      alongside it, per Amendment 8 Finding 20) and a real `dotnet build`/
      `dotnet run`. Every defect this review round found (`CS0122`,
      `CS0460`, `CS0111`, `CS0214`, `CS0177`) is exactly the class of
      cross-assembly compile failure an in-process `Verify()` snapshot
      test cannot catch, since it only diffs generated source text against
      a golden file rather than actually compiling it into a genuinely
      separate consumer assembly. This phase does not ship (its own PR
      does not merge) until this
      smoke test is green.
- [ ] **Docs, this phase's own shape** (moved here from the original
      single Phase 4, per Codex review — `references/documentation.md`'s
      "update the relevant doc in the same PR" rule means Phases 0-2
      shipping independently can't leave shipped behavior undocumented
      until Phase 4 catches up):
      `docs/packages/compono-testdoubles.md` and
      `skills/compono/references/testdoubles.md` gain the overload-
      discriminator section; `docs/reference/diagnostics.md` gains the
      overload-scoped `CMP0022` message update.

### Phase 1 — Generic-method support

- [ ] `TestDoubleAnalyzer`: replace the blanket `IsGenericMethod → reject`
      check with "does the return type's syntax tree reference any of the
      method's own type parameters" — reject only that case, under a
      refined diagnostic (next available code after `CMP0028`). **Also
      diagnose and exclude a method using `T?` on one of its own type
      parameters, in a parameter or its own declaration — constrained or
      unconstrained, regardless of which constraint** (Amendment 6 Finding
      15, unified with Amendment 9's withdrawal of a narrower
      constrained-only exception Amendment 8 briefly introduced and
      Amendment 9 retracted) — modeling C#'s exact permitted constraint-
      restatement rules for this case correctly isn't something this ADR
      has a verified answer for (two review rounds gave conflicting
      answers for the exact permitted keyword set), and the real
      motivating shape (`ILogger<T>.Log<TState>`) never uses `TState?` at
      all, so there's no evidence forcing a guess either way.
- [ ] Constraint-clause propagation: emit each type parameter's
      `where T : ...` clause verbatim (reference-type/value-type/`notnull`/
      base-type/interface constraints), extending the existing
      `SymbolDisplay`-based type-reference emission rather than inventing
      new text-building logic. **Generated generic *extension* methods
      only** (Amendment 1's overloaded-generic case) — never on the
      explicit interface implementation, which inherits its constraints
      automatically and cannot redeclare them (`CS0460`, corrected per
      ADR-0044 Amendment 2 Finding 2), **with no exception** — Amendment 8
      briefly introduced a narrow nullable-disambiguation exception here,
      which Amendment 9 withdrew after a second review round disputed the
      exact permitted keyword set; the corresponding type-parameter shape
      is diagnosed and excluded instead (see the task above), so this
      emitter task never reaches a case needing one.
- [ ] Nullable-annotation preservation on type-parameter-referencing text,
      reusing `NullableAwareFullyQualifiedFormat`.
- [ ] `TestDoubleEmitter`/`TestDouble.scriban`: explicit interface
      implementation stays generic (type parameters **only** — never
      constraints, which are inherited automatically and redeclaring them
      is `CS0460`; corrected per ADR-0044 Amendment 4 Finding 10, a plan-
      text sync fix for the rule Amendment 2 Finding 2 already decided);
      configuration extension stays non-generic, member-level, exactly
      like an ordinary member — **unless the member is also overloaded**
      (see the combined-interaction task below), in which case the
      extension becomes generic too, per ADR-0044 Amendment 1, and *does*
      carry the full constraint clauses (it's an ordinary standalone
      method, not an interface implementation).
- [ ] **Overloaded generic methods (ADR-0044 Amendment 1):** when a
      generic method's name is shared by another overload, its
      configuration/verification extension becomes generic itself —
      reusing the overload's own type parameters and constraint clauses
      verbatim, purely for compile-time overload selection — while the
      backing slot stays fixed per Requirement 2's existing rule (no
      per-closed-generic storage). Depends on both Phase 0's per-overload
      discriminator mechanism and this phase's constraint-propagation
      work, so it can only land once both exist.
- [ ] `Verify()`-tests: an `ILogger<T>`-shaped interface (`Log<TState>`,
      `BeginScope<TState>` — the actual motivating shape), a
      multi-type-parameter generic method, a still-unsupported
      generic-return-type method (diagnosed, not emitted), **the
      inherited-generic-parameter-name diamond case** (`IA.M<T>(T)` /
      `IB.M<U>(U)`, moved from Phase 0 now that generic methods exist to
      test it with — Amendment 5 Finding 11), **and a dedicated
      overloaded-generic-method test** (`void Process<T>(T value)`
      / `void Process<T>(IEnumerable<T> values)` on the same interface) —
      required per ADR-0044 Amendment 1 so Phase 0's overload support and
      this phase's generic support don't each pass independently while
      their combination produces invalid generated code.
- [ ] **Packaged-consumer smoke test, this phase's own shape** (same
      rationale as Phase 0's own task above): an `ILogger<T>`-shaped
      interface through a real packed `.nupkg` + throwaway consumer
      project, real `dotnet build`/`dotnet run`. This phase does not ship
      until it's green.
- [ ] **Docs, this phase's own shape** (same rationale as Phase 0's own
      doc task above): `docs/packages/compono-testdoubles.md` and
      `skills/compono/references/testdoubles.md` gain the generic-method
      scope section (the `ILogger<T>` motivating case, and what stays
      excluded); `docs/reference/diagnostics.md` gains the new generic-
      return-type-dependent diagnostic code.

### Phase 2 — Minimal call recording and verification

- [ ] Core `Compono`: add `internal int CallCount` + `public readonly int
      ConfiguredCallCount` + a **public `RecordCall()` instance method**
      (`Interlocked.Increment(ref CallCount)`, declared on `ReturnConfig<T>`
      itself) to `ReturnConfig<T>` (same internal-write/public-mutation-
      surface split as `HasValue`/`Value`/`Exception`, per ADR-0043
      Amendment 3 — no new per-member field). Generated dispatch code
      calls `__member.RecordCall()`, never `Interlocked.Increment(ref
      __member.CallCount)` directly — the raw field is `internal` to core
      `Compono` and unwritable from the consumer assembly generated code
      actually lives in, the same cross-assembly defect class ADR-0043
      Amendment 3/8 already fixed twice, caught again here per ADR-0044
      Amendment 2 Finding 1.
- [ ] Core `Compono`: new `CallVerifier` (public readonly struct,
      `Never()`/`Once()`/`Exactly(int)`) and `TestDoubleVerificationException`
      (plain `Exception` subtype, matching `CompositionException`'s
      convention — no xUnit/TUnit/AwesomeAssertions reference from core).
- [ ] `TestDoubleEmitter`/`TestDouble.scriban`: `__member.RecordCall()` at
      the top of every dispatch body **that has a backing field** (methods
      and property accessors alike) — a call counts whether it hits
      configured, default, or throw behavior. **Not emitted for a member
      with no `Configure()` surface** (corrected per ADR-0044 Amendment 5
      Finding 13, matching Phase 0/Amendment 4's "no field ⇒ nothing to
      increment" rule): an unsupported-shape or diamond-colliding overload
      has no `Verify()` surface either (Amendment 1), so nothing could
      ever read a count for it.
- [ ] `TestDoubleAnalyzer`: generalize the existing `Configure`-collision
      check (ADR-0043 Amendment 3 Finding E) to a reserved-name set
      (`Configure`, `Verify`) using the same zero-argument-applicability
      logic already established — an interface declaring its own `Verify`
      member would otherwise silently shadow the new bridge exactly like
      an undiagnosed `Configure` collision would have, per ADR-0044
      Amendment 2 Finding 4. Not a new diagnostic code, the existing one's
      scope widens.
- [ ] New per-interface generated shape: `<Hash>_VerifyExtension`
      (`Verify(this TInterface)` bridge, same cast-failure-message
      convention as `Configure()`), `<Hash>_DoubleVerifier` (the distinct
      wrapper type `Verify()` returns, avoiding `Configure()`/`Verify()`
      extension-resolution ambiguity), `<Hash>_DoubleVerification`
      (per-member/per-overload `CallVerifier`-returning extensions, reusing
      Phase 0's overload-discriminator mechanism where applicable). Each
      generated verification extension reads `ConfiguredCallCount` (never
      the internal `CallCount` field directly — same cross-assembly
      accessibility rule as everywhere else) and constructs `CallVerifier`
      with **both** required arguments: the observed count and a
      compile-time member-description string (the declaring interface's
      display name + member name) — corrected per ADR-0044 Amendment 3
      Findings 5 and 6, which caught this exact generated line reading an
      inaccessible field *and* under-supplying `CallVerifier`'s
      constructor.
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
- [ ] **Packaged-consumer smoke test, this phase's own shape** (same
      rationale as Phases 0/1's own tasks): `Verify().Member().Once()`
      through a real packed `.nupkg` + throwaway consumer project, real
      `dotnet build`/`dotnet run`. This phase does not ship until it's
      green.
- [ ] **Docs, this phase's own shape** (same rationale as Phases 0/1's own
      doc tasks): `docs/packages/compono-testdoubles.md` and
      `skills/compono/references/testdoubles.md` gain the `Verify()`/
      `Never`/`Once`/`Exactly(n)` section, including the updated
      "AutoFixture/NSubstitute-habit trap" framing — verification exists
      now, but stays deliberately minimal, not general — and explicit
      guidance on when a shape still needs `Compono.NSubstitute` (argument
      matchers, call-order, class mocking). No `diagnostics.md` task here
      — verification introduces a runtime exception
      (`TestDoubleVerificationException`), not a new compile diagnostic.

### Phase 3 — AOT, performance, and package verification

- [ ] `test/Compono.TestDoubles.AotSmokeTest`: extend to exercise all
      three new shapes together (an overloaded member, a covered generic
      method, a verified call) in the same `dotnet publish
      -p:PublishAot=true` run — zero `IL2xxx`/`IL3xxx` warnings, real
      execution, matching PLAN-0043 Phase 2's own standard.
- [ ] `test/Compono.TestDoubles.SampleTests`: extend the packaged-consumer
      sample with the same three shapes, proving the real NuGet-packaged
      path (not just the in-process generator harness), matching PLAN-0043
      Phase 2's local-feed pattern — this is the combined-shapes proof;
      Phases 0-2 already each proved their own shape individually via
      their own lighter packaged smoke test (added per Amendment 6's
      process finding, see Notes), so this phase isn't the first point
      any of the three shapes gets compiled as a real external consumer.
- [ ] Targeted benchmarks only (per ADR-0044's "AOT and performance"
      section — not a general competitive suite): `Interlocked.Increment`
      overhead per verified call, and overload-dispatch overhead for a
      member with several sibling overloads — both following this repo's
      existing benchmark-suite policy (ADR-0034), non-misleading
      comparisons only.

### Phase 4 — Documentation consistency pass

Retitled and reduced from an original "write all the docs here" phase,
per Codex review: `references/documentation.md`'s "update the relevant
doc in the same PR that changes the behavior it describes" rule means
Phases 0-2, each shipping as its own PR, can't leave their own shipped
behavior undocumented until this phase catches up later — a doc lagging
shipped code is exactly the rot that rule exists to prevent, and with
three independent phase releases in between, "later" could mean several
real, published package versions with an inaccurate Package Guide. Each
doc task now lives in the phase that actually introduces the behavior it
describes (see Phases 0-2's own new doc tasks above). What's left here is
genuinely cross-cutting, only possible once all three shapes' content
already exists from those phases:

- [ ] Read `docs/packages/compono-testdoubles.md` and
      `skills/compono/references/testdoubles.md` end-to-end, written
      incrementally across three separate phases/PRs — fix any structural,
      ordering, or cross-referencing issues that only become visible once
      the full v2 picture is assembled (e.g. the overload/generic/
      verification sections should read as one coherent package, not three
      independently-written additions).
- [ ] `docs/reference/diagnostics.md` — same consistency check across the
      Phase 0/1 diagnostic entries added incrementally.

This phase completes and ships on its own — the roadmap-graduation and
plan-status tasks originally listed here move to Phase 5 below, where
they're actually completable (corrected per Codex review, Finding 18: as
written, this phase couldn't finish in its own PR since one of its own
tasks explicitly waited on Phase 5, and flipping the whole plan to `Done`
here would have happened before the last phase even ran).

### Phase 5 — Re-dogfood against `lightsaber-skill`

- [ ] Re-run the exact `lightsaber-skill` migration analysis against the
      shipped v2 package. Quantify: how many of the ~40 original
      NSubstitute call sites now migrate; whether `ILogger<T>` and
      `IResponseBuilder` now generate; how much (if any) of `IAmazonS3`
      now generates given its overloads are fixed; whether the two
      `Received(1)` sites now migrate to `Verify().Send().Once()`; whether
      any test still needs both `Compono.NSubstitute` and
      `Compono.TestDoubles` side by side.
- [ ] Record the result in `docs/roadmap/post-mvp.md`'s finding entry.
      **`docs/roadmap/post-mvp.md` — move the `lightsaber-skill` finding
      from "outstanding" to "shipped,"** matching the existing
      `ComposeAttribute` graduation entry's shape (moved here from Phase 4
      per Codex review Finding 18 — it can't graduate until this phase's
      own analysis exists to graduate it with). Success criterion is
      **material improvement** ("small minority, no practical dependency
      reduction" → "viable for a meaningful portion of the suite"), not
      zero remaining NSubstitute usage. Any residual gap goes back through
      this repo's normal evidence-based roadmap process (a new
      `docs/research/*.md` finding or a fresh roadmap-candidate ADR), not
      folded into this plan after the fact.
- [ ] **`docs/plans/README.md` status flip to `Done`** (moved here from
      Phase 4 per Codex review Finding 18 — the plan as a whole isn't done
      until its last phase is).

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

**Packaged verification moved into Phases 0-2 themselves, not deferred
entirely to Phase 3** (added during PR #87's own design review, a Codex
finding against this plan's original draft). The original draft deferred
*all* real packaged-consumer compilation to Phase 3, relying on in-process
`Verify()` generator-output snapshot tests for Phases 0-2 — but every
defect that same design-review process found along the way (`CallCount`
cross-assembly write/read access, `CS0460` constraint redeclaration,
`CS0111` duplicate discriminator declarations, `CS0214` unsafe-context
requirements) was exactly the class of cross-assembly compile failure a
snapshot test *cannot* catch, since it only diffs generated source text
against a golden file rather than compiling it as a genuinely separate
consumer assembly. Given each of Phases 0/1/2 ships as its own PR/release
(the phase-per-PR rule above), shipping any of them without packaged
verification would mean the exact defect class this review spent multiple
rounds catching by hand could just as easily reach a real consumer
instead. Each of Phases 0-2 now ends with its own lightweight packaged
smoke test (a real `dotnet pack` + local-feed consumer, not the full
`PublishAot` proof) and does not ship until it's green; Phase 3's own
sample/AOT extension stays the *combined*, all-three-shapes-together
proof, not the first point any individual shape gets compiled externally.

**Documentation moved into Phases 0-2 as well, for the same underlying
reason** (a later Codex finding against this same draft, same review
cycle): the original single "Phase 4 — docs and skill alignment" would
have left each of Phases 0/1/2's shipped behavior undocumented in
`docs/packages/compono-testdoubles.md`/`skills/compono/references/testdoubles.md`
until Phase 4 caught up — directly against `references/documentation.md`'s
"update the relevant doc in the same PR" rule, and with three independent
phase releases possibly landing before Phase 4 runs, "later" could mean
several published package versions with a stale Package Guide. Each doc
task now lives in the phase introducing its own behavior; Phase 4 shrank
to a genuinely cross-cutting consistency pass over content that was
necessarily written incrementally across three separate PRs.
