# [ADR-0042] Compono-Owned Source-Generated Test Doubles

**Status:** Accepted

**Date:** 2026-08-13

**Decision Makers:** Nick Cipollina, Claude (Gate A admission check)

## Context

`Compono.NSubstitute` ([ADR-0025](0025-compono-nsubstitute-package-design.md))
is Compono's only way to automatically satisfy an otherwise-unresolvable
interface/abstract-class/delegate dependency in a composition graph — the
`[Shared] IRepository repository` case `composer.Create<MyService>()` relies
on with zero explicit registration. It does this by delegating to
NSubstitute's own runtime proxy generation
(`Substitute.For(Type[], object[])`), which is exactly why it works with a
plain runtime `System.Type` — but that same runtime-proxy mechanism is not
Native-AOT/trimming-safe, and any consumer who installs
`Compono.NSubstitute` accepts that cost for the whole package, not just the
specific tests that need NSubstitute's richer setup/verification surface.

A prior investigation (this repo's TUnit.Mocks admission exploration,
recorded informally, not yet an ADR) evaluated whether an *external*
source-generated mocking library could fill this role instead, preserving
`composer.Create<MyService>()`'s zero-declaration UX without a runtime
proxy. That investigation included a real two-generator Roslyn spike and
concluded, with direct experimental evidence: **no**. Every current
source-generated .NET mocking library researched —
[TUnit.Mocks](https://github.com/thomhurst/TUnit),
[Imposter](https://github.com/themidnightgospel/Imposter), and
[Rocks](https://github.com/JasonBock/Rocks) — requires a compile-time-visible
trigger written directly in the consumer's own source
(`Mock.Of<T>()`/`T.Mock()`/`[assembly: GenerateMock(typeof(T))]` for
TUnit.Mocks; `[assembly: GenerateImposter(typeof(T))]` for Imposter;
`[assembly: Rock(typeof(T), BuildType.Create)]` for Rocks). None of them can
be triggered by a *different* generator's emitted source in the same
compilation — proven directly, not assumed, against TUnit.Mocks (a
`MockRegistry.TryCreateAutoMock` runtime bridge exists, but only succeeds
for a type the generator has already produced a factory for; a type whose
only trigger came from a sibling generator's output failed identically to
one never triggered at all, reproduced across clean and incremental
builds). This is a structural property of compile-time source generation
(a generic type argument must be known to the compiler), not a
library-specific limitation.

This leaves a real, evidenced gap: **Compono has no way to preserve
automatic, zero-declaration interface-dependency composition without a
runtime-proxy dependency**, because the only generator positioned to close
that gap without a cross-generator handoff is `Compono.Generators` itself —
it already performs the composition-graph discovery (walking constructor
parameters via the Roslyn semantic model, `LeafTypeClassifier`'s existing
interface/abstract-class/delegate-leaf classification per
[ADR-0024](0024-public-provider-extensibility-model.md) Amendment 2) that
any generated-double mechanism would need to hook into, in the same
generation pass, with no sibling generator involved at all.

A Gate A admission check (per [ADR-0039](0039-future-extension-package-admission-gate-and-release-sequence.md))
against this idea found it clears, narrowly, conditioned on staying scoped
to exactly that gap — a fallback default-value generator for otherwise-
unresolvable composition-graph leaves — and explicitly not widening into a
general-purpose mocking framework competing with
NSubstitute/Moq/FakeItEasy/TUnit.Mocks on setup/verification breadth. This
ADR records that admitted problem. The requester's own explicit request is
this ADR's Gate B evidence trigger — the same shape of trigger that already
cleared `Compono.TUnit`'s Gate B (an explicit product-owner request, not
dogfooding, per `docs/roadmap/future-packages.md`) — not dogfooding
friction or a repeated consumer request.

## Decision Drivers

- **Zero-declaration composition is the entire justification.** The value
  this capability exists to provide is specifically "a dependency the
  generator already discovered gets a working default with no consumer
  action" — not a new setup/verification API surface. Anything that
  requires a consumer to declare a type up front (the way every external
  source-generated mocking library requires) doesn't close this gap; it's
  already achievable today via `Compono.NSubstitute` or any of those
  libraries directly.
- **No cross-generator dependency, ever.** The Gate A check's central
  finding is that this is only architecturally sound because
  `Compono.Generators` can own discovery and generation in the same pass.
  Any design that reintroduces a handoff to a second generator — including
  a *separately-packaged* Compono extension whose generator code is meant
  to cooperate with `Compono.Generators`'s own pass — reproduces the exact
  failure this ADR exists to avoid, proven experimentally against
  TUnit.Mocks.
- **`docs/adr/0001-source-generation-first.md`'s no-reflection-by-default
  posture and Native AOT/trimming safety** — the entire point of this
  capability is to give Compono an AOT-safe alternative to
  `Compono.NSubstitute`'s runtime-proxy dependency; a design that
  reintroduces reflection-based dynamic dispatch, `Activator.CreateInstance`,
  `MakeGenericMethod`, or an expression-tree-compiled setup surface
  undermines the reason this ADR exists.
- **Explicit-over-implicit, applied to activation.** `LeafTypeClassifier`
  today never tries to satisfy an interface leaf itself — it always defers
  to the runtime provider pipeline (`context.Resolve<T>()`). Any design
  that has the generator start emitting behavior for a leaf it previously
  left alone must be behind a signal the generator can observe **at compile
  time** (an attribute, an MSBuild property — the same category of
  mechanism `[Compose<TProfile>]`/`[Shared]`/`[Composable]` already are),
  never a runtime-only call the generator has no way to see. A consumer
  upgrading core Compono must never have a previously-failing composition
  silently start manufacturing a double.
- **`Compono.NSubstitute` is not being deprecated or replaced.** This
  capability's v1 scope — default-value doubles, at minimum, with the
  deep-design pass still to determine whether a minimal strongly typed
  return/throw configuration surface belongs in v1 too (see Decision
  Outcome) — excludes verification, strict mode, class mocking, and
  protected-member support outright, and is deliberately smaller than
  what `Compono.NSubstitute` already provides regardless of how that open
  question resolves. Anything beyond v1's eventual scope is still
  `Compono.NSubstitute`'s job, or a consumer's own explicit registration.
- **Compono integration first; standalone usability only if it falls out
  cleanly.** The requester's explicit priority order: (1) first-class,
  automatic integration with Compono's composition model is the capability's
  entire reason to exist and cannot be compromised; (2) if the underlying
  generated-double mechanism can also be exercised standalone (without
  `Composer`/`[Compose]`/`CompositionRow`) without weakening (1) or the
  AOT/source-generation posture, that is a genuine bonus worth designing
  for; (3) standalone usability must never justify added complexity, and
  must be dropped rather than distort the architecture if it doesn't fall
  out cleanly. A standalone consumer, unlike Compono itself, has no
  composition-graph discovery signal to rely on and would reasonably need
  its own explicit compile-time trigger (the same shape every external
  library already uses) — that's expected and not a contradiction of the
  "no explicit declaration" goal, which is specifically a Compono-
  integration property, not a universal one.
- **Maintenance cost is real but bounded, per the Gate A check** — this
  extends one existing decision point (`LeafTypeClassifier`) and adds one
  new `ICompositionValueProvider` implementation
  (`NSubstituteProvider`-sized precedent) for the runtime-facing control
  surface, whatever package(s) that surface ends up shipping in (Decision
  Outcome leaves that topology open) — not a new subsystem invented from
  nothing.

## Considered Options

Per [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-driven restraint (a roadmap-candidate outcome records the problem
only, not the API), the options below are the admission-level alternatives
this ADR chooses among — not API/architecture options, which are explicitly
deferred to a future deep-design pass per Decision Outcome.

1. **Reject.** Stay with `Compono.NSubstitute` as the only automatic
   test-double mechanism; accept its runtime-proxy/AOT limitation as a
   permanent tradeoff for consumers who need automatic composition.
2. **Admit as a general-purpose Compono-owned mocking framework**, competing
   with NSubstitute/Moq/FakeItEasy/TUnit.Mocks on setup/verification
   breadth (strict mode, argument matchers, callbacks, class mocking,
   protected members, etc.).
3. **Admit only as a narrowly-scoped generated-test-double capability** —
   a fallback default-value generator for otherwise-unresolvable
   composition-graph leaves, explicitly not a general mocking API, with a
   deep design pass still to come.

## Decision Outcome

**Chosen: Option 3.** Gate A clears for a narrowly-scoped capability;
Option 2 is explicitly rejected at the admission level, before any API
design happens, so scope discipline is a starting constraint the deep
design pass inherits rather than something it has to independently
rediscover. Option 1 (reject entirely) doesn't survive Gate A's own
findings — Section "Verification" above establishes real, checkable
Compono-specific value (a zero-declaration UX no external library can
provide) rather than "sounds aligned," which is exactly what Gate A exists
to distinguish.

**This ADR records the problem and the admission result only.** No API
shape, package name, package-boundary split (core vs. extension package),
or generator-emission mechanism is decided here — those are explicitly
deferred to a future `/engineering-workflow` deep-design pass, per this
repo's own restraint for roadmap-candidate ADRs
([ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)).
The Gate A admission check that fed this ADR did identify a strong
candidate shape worth carrying into that pass as a starting point, not a
commitment: `Compono.Generators` gains a compile-time-gated capability to
emit a default-value-returning double type plus an internal lookup for any
interface leaf it discovers, registered into the runtime pipeline through a
new `ICompositionValueProvider` implementation activated the same explicit
way `UseNSubstitute()` already is — zero new core engine mechanism, built
entirely on [ADR-0024](0024-public-provider-extensibility-model.md)'s
already-accepted extension point. That candidate shape is not locked in by
this ADR.

**Explicitly deferred to the deep-design pass, not decided here:**

- The consumer-facing control-surface API (how a test configures or
  verifies a generated double) — candidate shapes (a `Mock<T>`-style
  wrapper, a generated concrete type implementing the interface directly,
  a companion-lookup API, profile-level configuration) were surveyed during
  the admission check but none is chosen.
- **Whether v1 needs a minimal, strongly typed return/throw configuration
  capability** (conceptually `mock.SomeCall(...).Returns(...)`/`.Throws(...)`),
  not just default-value doubles. This ADR does not decide this either way —
  it is an open product/design question for the deep-design pass, tied
  directly to the interception investigation below and to whether
  standalone consumption falls out cleanly, not a Non-Goal. Verification,
  strict mode, class/protected-member mocking remain excluded from v1
  regardless of how this resolves (see Non-Goals).
- **Interception as a possible mechanism for that control surface** —
  raised explicitly by the requester as an exploration axis, not a
  requirement, specifically for whether it can produce a natural, strongly
  typed `mock.SomeCall(...).Returns(...)`/`.Throws(...)` syntax without a
  large runtime reflection or expression-tree subsystem. The deep-design
  pass must investigate current C#/.NET interception mechanisms' stability
  for a production NuGet package, preview/compiler-specific dependencies,
  Native AOT/trimming implications, whether it would remain purely
  syntactic sugar over generator-owned state (never becoming foundational
  to the double engine itself), and debugging/tooling implications of
  compile-time rewriting — compared honestly against non-interception
  alternatives, and rejected if it adds magic, harms AOT/trimming, or
  depends on unstable language features, regardless of how attractive the
  resulting syntax looks.
- The exact package boundary/topology — whether the generated-double
  mechanism ends up entirely inside core `Compono`, as a standalone
  `Compono.*` package with separate Compono integration, as an engine
  layer plus a thin integration layer, or another shape. The Gate A check
  found the *generator emission* logic must live inside `Compono.Generators`
  (it needs the same-pass discovery access nothing else has), but that
  finding constrains, rather than fully decides, the eventual package
  topology.
- Package name.
- The precedence rule between a generated double and an installed
  `Compono.NSubstitute` when both could satisfy the same request (the Gate
  A check's working assumption — explicit registration → explicit provider
  → generated double → NSubstitute — needs its own explicit design, not
  registration-order accident, matching a gap already flagged and left
  open in the TUnit.Mocks investigation for any second test-double
  provider).

### Non-Goals (load-bearing, not aspirational)

Per the Gate A check's explicit scope boundary — carried into this ADR
verbatim so a future design or implementation pass can't quietly widen it
without revisiting this decision:

- Not a general-purpose mocking framework. Explicitly not competing with
  NSubstitute/Moq/FakeItEasy/TUnit.Mocks/Rocks/Imposter on setup/
  verification breadth.
- No protected-member support, ever.
- No static-abstract-member support, ever (the same call TUnit.Mocks
  itself made — "deemed infeasible" given a source-generator design).
- No class/partial mocking in v1 — interfaces only, matching
  `Compono.NSubstitute`'s own original MVP-stage scope before abstract-class
  support was added.
- No `ref`/`out`/`in` parameter support, no generic-method support, no
  indexer/event support, no strict mode, no call-recording/verification —
  in v1. Any of these may be picked up in a later milestone, but only
  through this repo's normal design process, not folded in silently during
  implementation of this ADR's eventual plan.
- Not a replacement for or deprecation of `Compono.NSubstitute` — see
  Decision Drivers above.

**Deliberately not a Non-Goal here:** whether v1 includes a minimal
configured-return/throw capability. That question is still open — see
Decision Outcome's deferred-questions list — and this section does not
prejudge it. What *is* fixed regardless of that outcome is the boundary
above: no verification, no strict mode, no class/protected-member mocking,
in v1.

### Positive Consequences

- Records a real, evidenced capability gap (Compono has no AOT-safe path to
  automatic interface-dependency composition) rather than a speculative
  "mocking sounds aligned" motivation — the admission check's TUnit.Mocks/
  Imposter/Rocks research directly supports this ADR's Context rather than
  being asserted without backing.
- Keeps scope discipline as an admission-level decision, before any API
  design work happens, so the deep-design pass starts from an already-
  narrowed problem instead of having to independently rediscover "don't
  build another Moq."
- Gate A's architectural-fit finding (same generator owns discovery and
  generation, no sibling-generator handoff, builds on ADR-0024's existing
  extension point) gives the deep-design pass real confidence the core
  mechanism is buildable, rather than starting from an open question.

### Negative Consequences

- This ADR commits to a real, if narrow, new generator responsibility and
  runtime-facing capability before any concrete API or package topology
  exists — accepted, because ADR-0029's own restraint pattern for
  roadmap-candidate ADRs is exactly "record the problem now, design
  later," and the requester's explicit product-owner request is this
  ADR's Gate B trigger. This ADR does not commit to a new package — see
  Decision Outcome's deferred-questions list.
- The "Compono integration first, standalone second" priority (Decision
  Drivers) means the deep-design pass may conclude standalone usability
  isn't achievable without compromising the Compono-native UX, in which
  case it should be dropped rather than forced — an accepted, explicitly
  sanctioned outcome, not a design failure if it happens.
- Interception (raised as an exploration axis) may turn out to be
  unusable for a production AOT-safe package — also an accepted, expected
  possible outcome of investigating it honestly rather than committing to
  it here.

## Pros and Cons of the Options

### Reject

- Good, because it adds no new scope, package, or maintenance surface.
- Bad, because it leaves a real, evidenced gap unaddressed:
  `Compono.NSubstitute` remains the only automatic test-double mechanism,
  and it is not AOT/trimming-safe — a permanent limitation for consumers
  who want both automatic composition and Native AOT.

### Admit as a general-purpose mocking framework

- Good, because it would make Compono materially less dependent on any
  external mocking library for any scenario, not just the default case.
- Bad, because it directly risks the manifesto's anti-goal (feature-parity
  chasing against an entire category of existing, mature libraries);
  duplicates responsibility TUnit.Mocks/Imposter/Rocks/NSubstitute/Moq/
  FakeItEasy already own well; and the Gate A check found no differentiated
  Compono-specific value for anything beyond the zero-declaration case —
  every richer feature is already available today via
  `Compono.NSubstitute` or a directly-referenced mocking library.

### Admit only as a narrowly-scoped generated-test-double capability (chosen)

- Good, because the differentiated value (zero-declaration composition,
  proven unreachable by any external library) is real and checkable, not
  asserted.
- Good, because the scope boundary is set at admission time, before any
  API commitment, matching how `Compono.NSubstitute`'s own durable non-goal
  ("no recursive auto-configuration") was set at its own design time and
  held up under later dogfooding pressure (ADR-0025 Amendment 2).
- Bad, because "narrowly scoped" still requires real design discipline
  through implementation to hold — mitigated by recording the Non-Goals
  list above as load-bearing, explicitly meant to survive into the deep
  design pass and any resulting plan.

## Amendment 1 (2026-08-18): narrows "no static-abstract-member support, ever"

The Non-Goals list's static-abstract-member bullet reads "no
static-abstract-member support, ever (the same call TUnit.Mocks itself
made — 'deemed infeasible' given a source-generator design)." Real
dogfooding evidence
([RESEARCH-0005](../research/0005-lightsaber-skill-testdoubles-v2-third-dogfood.md))
against `ncipollina/lightsaber-skill`'s `IAmazonS3` surfaced a narrower
question this bullet's "infeasible" framing didn't distinguish: whether a
generated double must provide *configurable, mockable* behavior for a
static abstract member (genuinely infeasible for a source generator in
the way this ADR describes — process-global state, no per-instance
scoping) versus whether it merely needs to *satisfy C#'s interface-
conformance requirement* for that member (an ordinary, already-legal,
reflection-free static method/property/operator body — no different in
kind from any other member this package already emits).
[ADR-0046](0046-static-abstract-member-conformance-only-generation.md)
(`Proposed`) answers the second, narrower question: a generated double
now provides a conformance-only static implementation that throws if
invoked, with no `Configure()`/`Verify()` surface. This bullet's *core*
claim — no configurable/mockable static-member support — stands
unchanged and unreversed; only its "ever" absolutism narrows to exclude
the conformance-only case ADR-0046 scopes. This is a correction to how
broadly the original claim was stated, not a reversal of the underlying
design boundary.

## Amendment 2 (2026-08-18): full `Compono.NSubstitute` substitutability is a goal, not an aspiration

The Non-Goals bullet "Not a general-purpose mocking framework. Explicitly
not competing with NSubstitute/Moq/FakeItEasy/TUnit.Mocks/Rocks/Imposter
on setup/verification breadth" was read, in practice, as license to treat
any narrow, rare `Compono.NSubstitute`-vs-`Compono.TestDoubles` capability
gap as an acceptable, permanent difference — exactly the call
RESEARCH-0005 made on its first pass (classifying `IAmazonS3`'s
static-abstract-member gap "not a roadmap candidate" because it was narrow
and rare), before being corrected by the product owner the same day. That
correction is recorded here so it doesn't have to be relearned the next
time a similarly narrow gap surfaces.

**The scope was too narrow.** This bullet's "not competing on breadth"
language was written with the whole external mocking landscape in mind
(Moq, FakeItEasy, TUnit.Mocks, Rocks, Imposter) — Compono has no ambition
to chase feature parity with the general state of the art, and that
remains true, unchanged. It does **not** extend to `Compono.NSubstitute`
specifically. `Compono.NSubstitute` and `Compono.TestDoubles` are both
first-party Compono packages solving the same problem
(interface-dependency test-double substitution) by two different
mechanisms (runtime proxy vs. AOT-safe source generation) — the entire
reason `Compono.TestDoubles` exists per this ADR's Context is so a
consumer who can't or doesn't want the runtime-proxy cost has a real
alternative. "A real alternative" means a consumer who chooses
`Compono.TestDoubles` can actually leave `Compono.NSubstitute` behind, not
"can leave it behind for most of their test suite, with an irreducible
remainder for whatever `Compono.NSubstitute` happens to still do better."

**The corrected policy:** any real, evidenced case — surfaced through
actual dogfooding or a real migration, not a speculative feature audit —
where `Compono.NSubstitute` can satisfy an interface or member shape that
`Compono.TestDoubles` cannot **is, by definition, a roadmap candidate**.
This overrides ADR-0029's general frequency/cost-weighted classification
discretion for this specific category: rarity is not, on its own, a valid
reason to classify an evidenced `Compono.NSubstitute`-vs-`Compono.TestDoubles`
gap as "acceptable alternative, not a roadmap item" the way it validly can
for an unrelated finding (a project-local workaround, a genuinely
unreachable theoretical case, etc. — those classifications are unaffected
by this Amendment). One real occurrence, in one real project, is
sufficient evidence under this policy — a second or third dogfooding
project hitting the same gap is corroborating, not required.

**What this does not change:** `Compono.TestDoubles` still does not
proactively implement `Compono.NSubstitute`'s full feature surface ahead
of evidence — argument matchers, call-order verification, and any other
NSubstitute capability stay non-goals *until* a real case demonstrates
`Compono.TestDoubles` cannot otherwise satisfy a real interface a consumer
needs. The trigger for new work is still real evidence, exactly as
ADR-0029 requires; only the *classification* of that evidence changes —
once a real gap is found, "is this common enough to bother with" is no
longer part of the analysis for whether it's a candidate, only for how
it's prioritized against other candidates. [ADR-0046](0046-static-abstract-member-conformance-only-generation.md)
is the first case this corrected policy applies to, retroactively
justifying a classification RESEARCH-0005 initially got wrong under the
old reading.

**Consistency note on this ADR's own Decision Drivers:** the original
Decision Driver "`Compono.NSubstitute` is not being deprecated or
replaced" now reads ambiguously against the policy above, which needs
clarifying rather than editing in place (the original bullet's text
stands as written, per this ADR's own immutability rule — this note is
the correction). The durable policy, stated precisely: **`Compono.NSubstitute`
is not deprecated.** `Compono.TestDoubles` is intended to become a
complete alternative for a consumer who chooses source-generated,
AOT-safe test doubles over a runtime-proxy dependency — that consumer
should eventually be able to drop `Compono.NSubstitute` entirely, which is
exactly what this Amendment's policy and Gate-B (ADR-0046) are in service
of. That is a statement about what an individual consumer can choose to
do, not about `Compono.NSubstitute`'s own status: it remains a fully
supported, independently useful integration package in its own right —
not deprecated, not removed, not planned for removal, and not
second-class relative to `Compono.TestDoubles`. The original bullet's
"not replaced" clause is the part this note narrows: read it as "not
replaced *as a package*," not as "no consumer will ever be able to fully
substitute one for the other" — the latter reading is exactly what
Amendment 2 corrects.

## Links

- [ADR-0043](0043-compono-generated-test-doubles-design.md) — the deep-design
  pass that settles every question this ADR deferred (control-surface
  mechanism, `[Shared]`/access shape, generator architecture, package
  boundary and name); this ADR's own admission result, Decision Drivers,
  and Non-Goals remain the governing record it designs against.
- [ADR-0039](0039-future-extension-package-admission-gate-and-release-sequence.md) —
  the two-stage admission model (Gate A/Gate B) this ADR's own admission
  check applies.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  the evidence-over-prediction bias and problem-only-ADR restraint this
  ADR follows for a roadmap-candidate outcome; Gate B (evidence admission),
  satisfied here by an explicit product-owner request, the same trigger
  shape that cleared `Compono.TUnit`'s Gate B.
- [ADR-0024](0024-public-provider-extensibility-model.md) — the
  `ICompositionValueProvider` extension point any eventual design is
  expected to build on, per the Gate A check's architectural-fit finding.
- [ADR-0025](0025-compono-nsubstitute-package-design.md) — `Compono.NSubstitute`,
  the existing automatic test-double mechanism this capability is meant to
  complement, not replace; its Amendment 2 (a durable non-goal holding up
  under real dogfooding pressure) is the precedent this ADR's own Non-Goals
  list is modeled on.
- [ADR-0001](0001-source-generation-first.md) — the no-reflection-by-default
  posture this capability exists to extend into the test-double space, and
  the standard ("prove it, don't assume it") its eventual AOT verification
  must meet, matching `Compono.TUnit`'s own PLAN-0040 precedent.
- [ADR-0040](0040-compono-tunit-package-design.md) — the most recent
  package admitted via an explicit product-owner Gate B trigger rather than
  dogfooding evidence, the direct precedent for this ADR's own Gate B
  trigger.
- `docs/roadmap/future-packages.md` — updated alongside this ADR to record
  this capability as a roadmap item.
