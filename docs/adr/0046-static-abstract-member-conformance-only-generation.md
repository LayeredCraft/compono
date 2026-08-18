# [ADR-0046] Static Abstract Member Conformance-Only Generation

**Status:** Proposed

**Date:** 2026-08-18

**Decision Makers:** solo (product-owner-directed: explicit Gate-B
acceptance criterion supplied by the user)

## Context

`Compono.TestDoubles` currently rejects an entire interface at generation
time (`CMP0021`, informational, whole-interface `Failure`) if that
interface declares **any** static abstract member (a method, property, or
operator with no default body — C# 11+'s static-abstract-in-interfaces
feature). This bucket also currently catches events, indexers, and
C-style variable-argument methods, but those are unrelated shapes with
their own reasons for staying out of scope (see `docs/packages/compono-testdoubles.md`'s
"What it deliberately doesn't do" and ADR-0042's Non-Goals) — this ADR is
scoped to static abstract members only.

[RESEARCH-0005](../research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md)'s
third `lightsaber-skill` dogfood found this is now the **sole** remaining
blocker preventing that real project's test suite from fully removing
`Compono.NSubstitute`: `IAmazonS3` declares one static abstract member,
`CreateDefaultClientConfig()`, and because `CMP0021` rejects the whole
interface for it, `IAmazonS3` can't resolve through
`UseGeneratedTestDoubles()` alone — the other 20+ instance members on that
interface, which have no problem of their own, get pulled down with it.

RESEARCH-0005 originally classified this "not a bug, not a roadmap
candidate" under ADR-0029's general "material improvement" bar — a
reasonable call at that bar, since static abstract members are a narrow,
rare shape (the only one observed across three `lightsaber-skill`
dogfooding passes and two prior AutoFixture-migration projects) already
handled by `Compono.NSubstitute`'s documented fallback chain. It was
reclassified the same day against a stronger, explicit product-owner
requirement:

> I need `Compono.TestDoubles` to be capable of completely replacing
> `Compono.NSubstitute` in `lightsaber-skill`.

Measured against that specific requirement (this ADR's "Gate-B"), a
single, precisely-identified static-abstract-member blocker standing
between the current state and full removal is exactly ADR-0029's
roadmap-candidate rubric — real, evidenced, and now product-critical, even
though it's still narrow and still a documented non-goal in the general
sense.

The question this ADR answers is narrow by design, per the product
owner's own framing: **can a generated double satisfy C#'s interface-
conformance requirement for a static abstract member, without providing
any configurable, mockable, or otherwise meaningful behavior for that
member?** This is explicitly not a request to add static mocking, argument
matching, call verification, or any other NSubstitute-parity feature for
static members — only enough for the type to compile and for the other,
supported instance members on the same interface to stop being collateral
damage.

## Decision Drivers

- **Gate-B**: `lightsaber-skill`'s test project must be able to remove
  `Compono.NSubstitute` entirely — the explicit, stated product
  requirement this ADR responds to.
- **Source generation only, no reflection** — `docs/adr/0001-source-generation-first.md`'s
  standing constraint; whatever this ADR decides has to be emittable as
  ordinary C# by the existing Roslyn generator, not resolved at runtime.
- **Native AOT / trimming safety** — the generated code must survive
  `test/Compono.TestDoubles.AotSmokeTest` unchanged in kind (no new
  reflection, no runtime code generation).
- **No general static-mocking subsystem** — explicitly out of scope per
  the product owner's framing; the fix must not grow into `Returns()`/
  `Throws()`/`Verify()` surface for static members.
- **Minimal new API surface, weighed against semantic accuracy** — this
  package reuses existing types where the semantics genuinely match
  (`ReturnConfig`/`ReturnConfigBuilder` reused as-is across
  ADR-0044/ADR-0045), but a new, narrowly-scoped exception type is the
  established response when a failure is a genuinely different *mode*,
  not just a new trigger for an existing one — `TestDoubleVerificationException`
  exists distinctly from `CompositionException` for exactly this reason.
  Minimality is a tiebreaker, not an override, when a reused type would
  mislead a consumer about what actually happened.
- **Diagnostic honesty** — per the `CMP0025`→`CMP0032` precedent
  (ADR-0045), narrowing a whole-interface-rejection bucket needs its own
  new informational diagnostic, not a silent behavior change under the
  existing `CMP0021` message (which still applies, unchanged, to events,
  indexers, and variable-argument methods).

## Considered Options

1. **Status quo** — continue whole-interface rejection (`CMP0021`) for any
   interface declaring a static abstract member.
2. **Conformance-only generation** — the generated double provides a real
   `public static` implementation of the member (matching whatever return
   type/parameters/operator shape the interface declares), whose body
   unconditionally throws a Compono-owned exception if actually invoked.
   No `Configure()`/`Verify()`/`ReturnConfig` slot is generated for it —
   there is nothing to configure, ever. The interface's other, supported
   members are entirely unaffected and keep their normal generated
   behavior.
3. **Deterministic default for static members when a "safe" one exists**
   — e.g., a static abstract property returning a value type could return
   `default`, mirroring how instance deterministic defaults already work
   for `bool`/`int`/nullable-reference returns.
4. **Full configurable static-member support** — generate a real
   `Configure()`/`Verify()` surface for static members too, so a test
   could `SomeInterface.Configure().StaticMember().Returns(...)`.

## Decision Outcome

Chosen option: **"Conformance-only generation" (Option 2)**, because it's
the smallest change that satisfies Gate-B without violating any of the
other decision drivers, and it directly matches the product owner's own
stated preferred shape.

Implementing a `public static` interface member with a throwing body is
completely ordinary, already-legal C# — no new language feature, no
runtime code generation, and no reflection is involved. It's exactly as
AOT-safe as every other generated member Compono already emits (a plain
static method/property/operator body, resolved entirely at compile time);
`test/Compono.TestDoubles.AotSmokeTest` gets a new interface exercising
this shape (a static-abstract-member interface, unconfigured-invoke-throws
proven under Native AOT, mirroring how `IProfileRepository` already proves
the instance-level configuration-required shape there) as this ADR's own
acceptance test, alongside the existing packaged-consumer coverage in
`Compono.TestDoubles.SampleTests`.

The conformance-only stub throws a **new, dedicated exception type**,
**`TestDoubleUnsupportedMemberException`**, not the existing
`TestDoubleNotConfiguredException`. The two failure modes are genuinely
different, not just differently worded: `TestDoubleNotConfiguredException`
means "this member supports configuration and you haven't configured it
yet" — an actionable, fixable state (`Configure().Member(...).Returns(...)`
resolves it). A static abstract member has no `Configure()` surface at
all, ever — reusing the "not configured" name would assert a fixable
state that doesn't exist, and would prime a consumer reading a stack trace
to reach for a `Configure()` call that won't compile for that member,
before they've read far enough into the message to learn why. Evaluated
against consumer troubleshooting (the exception *type* is scanned before
the message), differential catching (a consumer's test-infrastructure
helper reasonably wants to treat "forgot to configure" — a test-authoring
bug — differently from "Compono structurally can't support this member" —
a package-capability boundary, worth surfacing differently, e.g. falling
back to `Compono.NSubstitute` for that one call), and future reuse (this
is the first instance of a broader "conformance-only, permanently
unsupported by design" category, not a one-off), a dedicated type is
materially clearer than a shared one. It costs one more small, sealed,
message-only exception type — the same minimal shape every existing
Compono exception already uses — which is a low price for not misleading
every future consumer who hits this path. The name reuses "unsupported,"
the exact vocabulary `CMP0021`'s own diagnostic message already uses
("declares a test-double interface member [that] is an unsupported
kind"), so the runtime exception and the compile-time diagnostic describe
the same boundary in the same words:

```
'IAmazonS3.CreateDefaultClientConfig' is a static abstract interface
member - Compono.TestDoubles generates a conformance-only implementation
for it (the type must implement it to compile) but provides no
configurable behavior for static members. This call always throws.
```

A new informational diagnostic, **`CMP0033`**, fires once per interface
(a count, same convention `CMP0032` established) when this applies — the
existing `CMP0021` stays exactly as-is for the shapes it still legitimately
rejects (events, indexers, variable-argument methods); it simply no longer
fires for the static-abstract-member case, the same way `CMP0025`
narrowed without being deleted when `CMP0032` was introduced (ADR-0045).

This is scoped identically for static abstract **methods**, **properties**,
and **operators** — all three report through the same code path today
(`TestDoubleAnalyzer.cs`'s `IMethodSymbol { IsStatic: true, IsAbstract: true, ... }`
and `IPropertySymbol { IsStatic: true, IsAbstract: true }` branches) — but
the emitted shape must be an **explicit static interface implementation**
(`static ReturnType IInterface.Member(...)`/`static ReturnType IInterface.operator +(...)`),
the same convention every instance member this generator already emits
uses (e.g. `global::...IResponseBuilder.Speak(string? speechOutput)`),
not a plain `public static` declaration. This isn't a stylistic choice for
operators specifically: a plain `public static` operator overload is
illegal C# unless at least one operand is the enclosing type itself, which
is not the case for an interface-typed operand
(`static abstract IRepository operator +(IRepository, IRepository)`, where
neither operand is the generated double's own concrete type) — only the
explicit-interface-implementation form is legal there, per C#'s
static-abstract-interface-member rules. Applying the same explicit form
uniformly to methods and properties too (not just where required for
operators) keeps one emission convention across every member kind this
generator produces, rather than a plain-`public static` special case for
two of the three static shapes and an explicit-implementation special case
for the third.

### Positive Consequences

- Directly closes Gate-B for `lightsaber-skill`: `IAmazonS3` (and any
  future interface with the same shape) generates and resolves through
  `UseGeneratedTestDoubles()` alone, with every instance member behaving
  exactly as it already does for any other interface.
- No new dispatch machinery, no new mocking surface, no reflection — the
  smallest possible shape that satisfies the stated requirement.
- Consistent with the existing `CMP0025`→`CMP0032` narrowing precedent;
  reviewers and future maintainers see the same pattern applied twice, not
  two different mechanisms for similar problems.
- A generated double that includes a static abstract member is now
  strictly *more* honest about the boundary than "the whole interface is
  unavailable" — the failure is scoped to exactly the one call a test
  should never be making anyway (an app calling a static abstract member
  on the interface it's substituting is testing the SDK/BCL default
  factory logic, not the code under test).

### Negative Consequences

- A test that genuinely needs to invoke `IAmazonS3.CreateDefaultClientConfig()`
  through the double (unusual, but not impossible — e.g. a test asserting
  on the SDK's own default config shape) gets an unconditional throw with
  no way to configure around it, same as before this ADR from that one
  call's perspective. Two distinct things are true about this, and they
  shouldn't be conflated: **today**, `Compono.NSubstitute` remains fully
  available as a working escape hatch for that specific test — this ADR
  doesn't remove that option, and this is an even narrower audience than
  "any test using `IAmazonS3`." But per
  [ADR-0042 Amendment 2](0042-compono-owned-source-generated-test-doubles.md#amendment-2-2026-08-18-full-compononsubstitute-substitutability-is-a-goal-not-an-aspiration),
  that escape hatch existing today is **not** license to treat this as an
  intentionally permanent `Compono.TestDoubles` limitation. If a real
  consumer later demonstrates an actual need for configurable/mockable
  static-member behavior — not a hypothetical, an evidenced case the same
  way `IAmazonS3` itself was — that evidence is automatically a new
  roadmap candidate under Amendment 2's policy, regardless of how rare it
  is; rarity affects prioritization against other candidates, not whether
  it counts as one. This ADR's Option 4 (full configurable static-member
  support) stays out of scope *for now*, for lack of that evidence — not
  because it's been decided against forever.
- One more diagnostic code to document across `docs/reference/diagnostics.md`,
  `skills/compono/references/diagnostics.md`, and
  `docs/packages/compono-testdoubles.md` — small, matches the existing
  documentation burden every prior `CMP00xx` addition has carried.
- One more public exception type (`TestDoubleUnsupportedMemberException`)
  for consumers to be aware of — small; it follows the exact shape every
  existing Compono exception already uses (sealed, message-only
  constructor), and a fifth narrowly-scoped type is consistent with, not
  a departure from, this package's existing one-type-per-failure-mode
  convention.

## Pros and Cons of the Options

### 1. Status quo (continue rejecting)

- Good, because it requires zero new code.
- Bad, because it directly fails Gate-B — `lightsaber-skill` cannot fully
  remove `Compono.NSubstitute` while this stands, which is the entire
  reason this ADR exists.
- Bad, because it's disproportionate: one member with no possible
  meaningful test-double behavior currently disables 20+ members that
  have real, working generated behavior today.

### 2. Conformance-only generation (chosen)

- Good, because it's the smallest change that satisfies Gate-B.
- Good, because it's ordinary, already-legal, reflection-free,
  AOT-trivial C# — no new generator capability class, just a new emission
  branch for a shape the analyzer already detects.
- Good, because it keeps a hard, honest boundary (an unconditional throw)
  rather than inventing a value that might silently paper over a real
  static-member dependency in the code under test.
- Bad, because it adds one more `CMP00xx` diagnostic and one more section
  to every diagnostics-facing doc — accepted as proportionate to the
  capability gained.

### 3. Deterministic default for "safe" static members

- Good, because it would let a value-typed static abstract member
  (e.g., a numeric default) work without ever throwing.
- Bad, because "safe" isn't a real, general test — the same reasoning
  ADR-0045 already rejected for *instance* members applies at least as
  strongly here (manufacturing a value the generator can't actually
  justify is semantically arbitrary), and static members are far more
  likely to be genuine factory/identity logic (`CreateDefaultClientConfig`
  is exactly this) where a fabricated default is actively misleading
  rather than merely unhelpful.
- Bad, because "genuinely safe" would need per-shape special-casing
  (which return types count as safe?) that reintroduces exactly the
  complexity/inconsistency ADR-0045's Decision Outcome already argued
  against for instance members. Rejected for the same reasons, applied to
  a case with weaker justification (static factory members are less
  likely than instance properties to have an innocuous default).

### 4. Full configurable static-member support

- Good, because it would be the most complete answer, closing any future
  gap in this area permanently.
- Bad, because it's explicitly out of scope per the product owner's own
  framing ("no general static mocking subsystem," "no NSubstitute
  feature-parity chase") and per this ADR's Decision Drivers.
- Bad, because static-member "mocking" has real semantic hazards a simple
  `Configure()`/`Returns()` surface doesn't have for instance members —
  static state is process-global, so a `Configure()` call on a static
  member would need to reason about test isolation/parallelization
  (xUnit v3's Microsoft.Testing.Platform runner, which this suite already
  uses, runs tests in parallel by default) in a way instance-member
  doubles never do, since each instance double is already inherently
  test-scoped. Not evidenced as needed by any real project yet — no
  dogfooding pass, including this one, has surfaced a test that actually
  needs to configure a static member's return value, only tests that
  never call one at all.

## Links

- [RESEARCH-0005](../research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md) —
  the evidence record and Gate-B reclassification this ADR responds to.
- [ADR-0045](0045-testdoubles-configuration-required-members.md) — the
  directly-analogous precedent (narrowing a whole-interface-rejection
  diagnostic into a per-member exception for one specific, common shape,
  while leaving genuinely unsupported shapes rejected).
- [ADR-0042](0042-compono-owned-source-generated-test-doubles.md) — the
  Non-Goals section this ADR narrows (static abstract members move from
  "unsupported" to "conformance-only supported"; events, indexers, and
  variable-argument methods remain unsupported, unchanged).
- [ADR-0043](0043-compono-generated-test-doubles-design.md) — original
  `CMP0021` whole-interface-rejection design this ADR narrows.
