# [PLAN-0044] Compono.TestDoubles v2: Overloads, Generic Methods, Minimal Call Verification

**Status:** In Progress

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

### Phase 0 — Overloaded-member support (Done)

- [x] `TestDoubleAnalyzer`: replace whole-name `duplicateConfigurationMemberNames`
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
- [x] Per-overload field/extension identity: a new identifier-hash helper
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
- [x] `TestDoubleEmitter`/`TestDouble.scriban`: dispatch bodies for every
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
- [x] Overload-set-internal partial support: an overload whose own shape
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
- [x] `DiagnosticDescriptors`: narrow `CMP0022`'s message to name the
      specific unsupported overload, not the whole member name.
- [x] **Shared helper, extended for generics (Amendment 14):** the
      existing `IsApplicableToZeroArguments` helper (reused by both the
      `Configure`/`Verify` bridge collision check and the `object`-member
      collision check) returns `false` immediately for any generic member
      whose type parameters aren't all inferable from its own *required*
      value parameters — in practice, any generic member with zero
      required value parameters is never an implicit zero-argument
      candidate, since implicit type inference has nothing to infer from.
      This fixes a generic `Configure<T>()`/`Verify<T>()` interface member
      no longer wrongly colliding with the bridge (nothing to infer `T`
      from at a bare `Configure()` call, so per this repo's own compile-
      spike-verified "applicability, not name-existence" rule — PLAN-0043
      PR #83 review round 2 — extension search proceeds normally). **This
      escape hatch requires the *generated discriminator extension* to
      itself be generic — corrected per Amendment 16, which caught that
      the rule as first stated checked the wrong genericity.** Only an
      *overloaded* generic method's extension is generic (Amendment 1); a
      *solo* generic method's extension stays non-generic and zero-
      argument (Requirement 2's original design, unchanged) and therefore
      has no real escape hatch — it keeps colliding with
      `ToString`/`GetHashCode`/`GetType`/`Equals` exactly like any other
      zero-parameter member, unaffected by this fix. Test both: a solo
      `ToString<T>()` still collides (diagnosed); an *overloaded*
      `ToString<T>()` (sharing a name with another `ToString` overload)
      does not, reachable via `Configure().ToString<int>()`.
      **Implementation note:** `IsApplicableToZeroArguments` now bails out
      for *any* generic method (not the narrower "zero required value
      parameters" test) — behaviorally equivalent for every real member
      Phase 0 can construct (no generic member reaches this check yet,
      since Phase 1 hasn't shipped generic-method support), and the
      object-collision check below reads `extensionArity` directly rather
      than routing through this same helper. Revisit both simplifications
      once Phase 1 adds real generic, non-solo members to exercise them
      against.
- [x] `TestDoubleAnalyzer`'s existing `object`-member collision check
      (`ToString`/`GetHashCode`/`GetType`, **plus `Equals` — new, per
      Amendment 14**) withholds the `Configure()`/`Verify()` surface only
      when the generated discriminator extension is genuinely applicable
      to an implicit (no explicit type argument) call of the matching
      arity, using the extended helper above — **not** simply
      `Parameters.Length == 0` (Amendment 12's rule, now folded into the
      shared helper together with the generic-arity fix). For
      `ToString`/`GetHashCode`/`GetType` this means genuinely zero
      parameters *and* zero type parameters. For `Equals` specifically —
      `object.Equals(object)` accepts any type via boxing/reference
      conversion, so a **non-generic** discriminator applicable to exactly
      one implicit argument (e.g. `Equals(int format)`) collides, with no
      escape hatch, **unless the parameter's own type is ref-like**
      (`parameterType.IsRefLikeType` — corrected per Amendment 16: a
      `Span<T>`/`ref struct` parameter has no boxing or reference
      conversion to `object` at all, the same restriction that already
      excludes a ref-like *return* type elsewhere, so `object.Equals(object)`
      is never actually applicable to it and the surface is kept — test
      `Equals(Span<int> value)` explicitly). Pointer-typed parameters need
      no equivalent check here — they're already excluded before this
      point by Amendment 5 Finding 12's `unsafe`-context rule. A
      **generic** `Equals<T>(T value)` keeps its surface too, reachable
      via `Equals<int>(5)` (per the shared-helper task above — only when
      the extension itself is generic, i.e. an overloaded `Equals`; a
      solo generic `Equals<T>(T value)` still collides). A non-overloaded
      member's
      extension is still always zero-parameter, zero-type-parameter
      (unchanged behavior, still always collides for the original three
      names; `Equals` was never a collision risk for a non-overloaded,
      zero-argument extension and stays that way) — genuinely widening
      supported surface, per this repo's own compile-spike-verified
      precedent for the analogous `Configure`-collision case (PLAN-0043,
      PR #83 review round 2).
- [x] `Verify()`-tests (generator-output snapshots): `IResponseBuilder`-shaped
      interface (`Speak(string?)`/`Speak(params ISsml[])`), a mixed
      supported/unsupported overload set, a diamond-shaped inherited
      overload, **and one diamond test per identity-canonicalization case**
      (nullable annotation, `dynamic`/`object`, tuple element names,
      `nint`/`System.IntPtr` — Amendment 6/7/8/10 Findings 14, 17, 19, and
      Amendment 10; the generic-parameter-name case moves to Phase 1, once
      generic methods exist to test it with) — **partial: only the
      `nint`/`System.IntPtr` case has its own diamond test
      (`DiamondInheritedNintAndIntPtrOverload_ReportsScopedOverloadedDiagnostic`)**;
      the nullable-annotation, `dynamic`/`object`, and tuple-element-name
      cases exercise the same recursive `AppendCanonical` code path (see
      `TestDoubleOverloadIdentity`) but don't each have their own dedicated
      diamond test yet — add them before Phase 1 revisits this file,
      **and a mixed overload set with an `out` parameter of a type with no
      deterministic default** (whole-interface rejection, Amendment 8
      Finding 20), alongside one with an `out` parameter that does have a
      default (definitely-assigned fallback body, same finding), **and an
      overloaded `ToString(int format)`-shaped member** (Amendment 11 —
      proves the corrected `object`-collision check supports an overloaded
      member sharing a name with an `object` method, where the
      non-overloaded case still correctly collides), **and a
      `ToString(params object[] values)`-shaped overload** (Amendment 12 —
      proves the surface is kept, not withheld, for a params/all-optional
      overload that's applicable to zero arguments but not genuinely
      zero-parameter), **and an overloaded, non-generic `Equals(int
      format)`-shaped member** (Amendment 14 — proves the new `Equals`
      collision check withholds the surface, since `object.Equals(object)`
      accepts any boxable/convertible type with no escape hatch).
- [x] **Packaged-consumer smoke test, this phase's own shape only**
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
- [x] **Docs, this phase's own shape** (moved here from the original
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
      check with "does the return type's **symbol graph** reference any of
      the method's own type-parameter symbols" — walk `ITypeSymbol`
      structure (generic type arguments, array/tuple element types,
      recursively) and compare against `method.TypeParameters` via
      `SymbolEqualityComparer`, **not** a syntax-tree/`SyntaxNode` check
      (corrected per Codex review — a metadata-defined interface like the
      actual `ILogger<T>` from a referenced assembly has no syntax tree in
      the consumer's own compilation at all; a syntax-based check would
      silently fail to classify it, exactly the real-world motivating
      case this ADR exists for). Reject only the type-parameter-dependent
      case, under a refined diagnostic (next available code after
      `CMP0028`); cover a metadata-defined interface (not just a
      source-declared one) in this phase's own packaged-consumer smoke
      test. **Also
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
      their combination produces invalid generated code, **and a generic,
      zero-value-parameter `Configure<T>()`/`ToString<T>()`-shaped
      collision non-case** (Amendment 14 — proves the extended
      `IsApplicableToZeroArguments` helper correctly does *not* flag
      either the bridge or the `object`-collision check for a generic
      member with nothing for the compiler to infer from at an implicit
      call site).
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
  bridge + verifier extension classes, `RecordCall()` dispatch (not a raw
  `Interlocked.Increment` — Amendment 2 Finding 1 routes it through the
  public bridge for cross-assembly accessibility).
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

**Pre-implementation design-review loop closed (2026-08-14).** Fourteen
review rounds against ADR-0044 (Amendments 1-14, plus the two plan-only
process fixes above) found real defects every round, but the pattern
shifted from structural (early rounds: cross-assembly accessibility,
core generated-code compile failures, field-emission gaps, phase-
sequencing contradictions) to narrow edge-case corrections against helper
logic that doesn't exist as compiled code yet (later rounds:
canonicalization cases, collision-detection refinements) — the same
transition ADR-0043's own pre-implementation review hit at a similar
round count. Confirmed directly with the requester: further refinement
continues during actual implementation (`tasks/implement.md`'s build/
test/PR-review cycle) rather than this text-review cycle continuing
indefinitely. This plan's task list above already reflects every
correction from all fourteen rounds — Phase 0 is next once implementation
is explicitly requested.

**Phase 0 implementation (2026-08-14): the packaged-consumer smoke test
caught a real cross-assembly-shaped defect the in-process `Verify()`
snapshot suite could not.** The initial `InfoDiagnostics` entries (the
diamond-collision and `ref`/`out`/`in`-fallback informational diagnostics)
carried the analyzer's call-site `Location`, same as `Diagnostics` already
does. That's correct for `Diagnostics` (each whole-interface failure is
deliberately reported at its own request site, and the merge step in
`ComponoIncrementalGenerator` already special-cases multiple failures for
the same interface). It's wrong for `InfoDiagnostics`: those diagnostics
describe a structural property of the interface's own declaration, not the
call site, and `DiagnosticInfo.Equals` includes `Location` — so the exact
same interface, reached from two different `[Compose]` theory methods in
`Compono.TestDoubles.SampleTests`, produced two "distinct"
`DiscoveredTestDoubleInfo` values (same `Members`, different
`InfoDiagnostics` locations) and tripped the generic `CMP0028`
conflicting-metadata merge path — silently discarding the double instead of
emitting it. Fixed by reporting `InfoDiagnostics` with `Location: null`,
matching the existing `ConflictingTestDoubleMetadata` diagnostic's own
precedent for a structural (not call-site) diagnostic. This is exactly the
class of defect this plan's Notes section above predicted packaged
verification would catch — it did.
