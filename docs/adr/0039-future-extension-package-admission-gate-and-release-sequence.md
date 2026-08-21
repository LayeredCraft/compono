# [ADR-0039] Future Extension Package Admission Gate and Release Sequence

**Status:** Accepted

**Date:** 2026-08-11

**Decision Makers:** Nick Cipollina, Claude (design deep dive)

## Context

[Future Packages](../roadmap/future-packages.md) lists natural candidate
extension packages that aren't designed or committed yet: additional
test-framework integrations, additional test-double integrations, and a
richer `Microsoft.Extensions.DependencyInjection` integration. None of
these has a concrete design or a real-demand trigger — per
[ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
evidence-over-prediction bias, a candidate only becomes real roadmap
content once real demand and a concrete design exist.

This ADR's first version (2026-08-11, revised in place below rather than
superseded by a new number — the ADR was still `Proposed`, so the
numbering/immutability rule that protects an `Accepted` ADR's original text
does not apply yet) proposed a three-item OR-gate ("supply composed
values, or expose composed values naturally to a test framework, or bridge
an established registration system") plus a committed six-package release
sequence (`Compono.TUnit`, `Compono.NUnit`, `Compono.MSTest`,
`Compono.FakeItEasy`, `Compono.Moq`, `Compono.DependencyInjection`, in that
order, TUnit first because its source-generated test model is
architecturally close to Compono's own).

A deep design dive against that first version — inspecting the three
shipped integration packages (`Compono.XunitV3`, `Compono.NSubstitute`,
`Compono.Bogus`), the ADRs that established their extension points
(ADR-0021, ADR-0024, ADR-0025), the DI boundary ADR-0019 already draws,
and the *current* public extension mechanisms of all six candidate
ecosystems (TUnit, NUnit, MSTest, FakeItEasy, Moq,
`Microsoft.Extensions.DependencyInjection`) — found two things wrong with
that first version, both corrected below:

1. **The three-item gate describes *which shape* of package a candidate
   is, not *whether it deserves to exist*.** It's an OR of near-tautologies:
   almost any wrapper around a test library can claim to "supply composed
   values." It cannot actually reject a candidate that exists only because
   its underlying library is popular — the exact failure mode this ADR
   exists to prevent (`Compono.TUnit` existing because TUnit exists,
   regardless of whether TUnit's extension surface offers anything
   Compono-specific to compose against).
2. **The committed sequence has no evidentiary basis.**
   [`docs/roadmap/post-mvp.md`](../roadmap/post-mvp.md) records that two
   real dogfooding passes under ADR-0029 (`cosmere-tracker`,
   `trivia-platform`) have already run and surfaced **zero** outstanding
   roadmap candidates in the test-framework/test-double/DI space. Every one
   of the six named candidates has zero evidence today. Recording a
   specific six-package order this far ahead of any real signal is
   prediction dressed as planning — directly against ADR-0029's own
   rationale, not a neutral non-binding note.

This revision replaces the OR-gate with a two-stage admission model and
drops the committed sequence, while keeping the parts of the original
proposal that held up under research: a standing gate is worth having, and
some candidates (see Decision Outcome) do have a real, non-wrapper
integration surface worth recording even before evidence exists for them.

## Decision Drivers

- `docs/architecture/design-principles.md`'s bias against scope creep and
  against becoming "a monolithic testing toolkit" / "a feature-complete
  wrapper over every third-party integration" — the package set should
  grow deliberately, not by accretion, and a gate that can't reject a
  branding wrapper doesn't actually protect against either anti-goal.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)'s
  evidence-over-prediction bias — sharpened by this revision's research
  finding that zero of the six candidates currently has any evidence at
  all (`docs/roadmap/post-mvp.md`). A gate can record that a candidate is
  *architecturally legitimate* without pretending that also means it's
  *due*.
- The existing extension-point pattern
  (`Compono.NSubstitute`/`Compono.Bogus` on `ICompositionValueProvider`
  per [ADR-0024](0024-public-provider-extensibility-model.md);
  `Compono.XunitV3` on `CompositionRow`/`CreateRow` per
  [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md)) —
  every shipped package builds on a public extension point that was itself
  designed by its own core-extension ADR before the package's own design
  ADR. A future package's admission has to be judged against this real
  precedent, not an abstract standard.
- [ADR-0019](0019-registrations-and-service-provider-injection.md) already
  drew an explicit boundary for `Microsoft.Extensions.DependencyInjection`:
  core ships a thin `IServiceProvider` fallback only; a "richer" DI
  integration is explicitly out of scope for core and was always expected
  to be a separate package built the same way NSubstitute/Bogus are.
- Skill-maintenance cost is real but, per direct inspection of
  `skills/compono`'s existing detection-table + per-package-reference-file
  pattern, linear and small per additional package — this driver should be
  *weighed* in the gate, not treated as a standalone blocker.

## Considered Options

1. No formal admission gate — evaluate each future package candidate
   independently, whenever real demand surfaces, with no standing
   criteria.
2. A single OR-gate (any one of three conditions) applied to every future
   candidate, plus a committed candidate release sequence — this ADR's
   original 2026-08-11 proposal.
3. A two-stage admission model (an architectural "shape" gate this ADR
   owns, feeding into ADR-0029's existing evidence gate), with no committed
   sequence — only non-binding heuristics for candidates that clear both
   stages simultaneously.

## Decision Outcome

Chosen option: **Option 3** — a two-stage admission model, no committed
release sequence.

### Why not Option 1 (no gate)

Rejected for the same reason the original proposal rejected it: without a
standing bar, a low-value candidate can reach a design pass before its
lack of value is obvious, and — per this revision's research — the
concrete case (`Compono.TUnit` existing because TUnit exists) is real
enough to name explicitly, not hypothetical.

### Why not Option 2 (this ADR's own first version)

Rejected on the two grounds in Context above: the OR-gate can't actually
reject a branding wrapper, and the committed sequence had no evidentiary
support once checked against `docs/roadmap/post-mvp.md`'s actual dogfooding
record.

### The two-stage admission model

**Gate A — architectural admission (this ADR).** A candidate must clear
**all** of the following, not just one, before it's treated as an
*admitted candidate*:

- **Compono-specific value.** It solves meaningful composition-related
  friction, not branding or convenience around an already-easy call.
- **Native ecosystem fit.** The resulting API is idiomatic in the
  integrated ecosystem's own terms — not a `Compono.XunitV3`-shaped clone
  bolted onto a different framework's extension model.
- **Meaningful abstraction.** Consumers get materially more than a trivial
  extension method they could write themselves in an afternoon.
- **Architectural fit.** It can be built entirely on an existing public
  extension point, or on a to-be-designed extension point this ADR names
  explicitly as a prerequisite — never on a core change invented ad hoc
  during the package's own design pass.
- **Package-boundary justification.** The dependency genuinely belongs
  outside core, and is substantial enough to be its own package rather
  than a documentation recipe or an addition to an existing package.

Maintenance/CI/docs/skill cost is a **weighing factor** across these five,
not a sixth pass/fail condition on its own — per the skill-cost finding
above, that cost is real but small for this repo's existing routing
pattern, and shouldn't by itself veto a candidate that clears the five
bars above.

A candidate that fails Gate A doesn't get a `Proposed` ADR of its own
regardless of demand — the same consequence the original OR-gate stated,
now backed by a gate that can actually produce that verdict for a case
like `Compono.TUnit exists because TUnit exists`.

**Gate B — evidence admission (unchanged, ADR-0029).** A candidate that
clears Gate A is an **admitted candidate**, not yet roadmap content. It
still needs a real-demand trigger — dogfooding friction, repeated
consumer request, or another concrete signal per ADR-0029's rubric —
before it becomes a **roadmap item** with its own problem-focused
`Proposed` ADR. This ADR does not relax, restate, or duplicate ADR-0029's
evidence rubric; Gate A and Gate B are answering different questions
(*could this be a legitimate Compono package* vs. *is there real reason to
build it now*) and both must clear, in that order, before a design pass
starts.

### Terminology (per this repo's own request to keep these distinct)

1. **Candidate** — named in `docs/roadmap/future-packages.md`, not yet
   evaluated against Gate A.
2. **Admitted candidate** — cleared Gate A: architecturally legitimate,
   still no evidence. Recorded in `future-packages.md`, not in a
   `Proposed` ADR of its own.
3. **Roadmap item** — cleared Gate B too: real evidence exists. Gets a
   problem-only `Proposed` ADR per ADR-0029, listed in
   `docs/roadmap/post-mvp.md`.
4. **Committed implementation work** — the roadmap item's ADR reaches
   `Accepted` and a `Plan` moves `In Progress`.

Nothing in this ADR moves any candidate past stage 2. Reaching stage 3 for
any of them requires real evidence this ADR cannot manufacture or predict.

### Candidate-by-candidate Gate A disposition

Evaluated against each ecosystem's *current* (2026) public extension
mechanisms, researched directly rather than assumed:

- **`Compono.TUnit` — admitted candidate.** TUnit's `IDataSourceAttribute`
  family (`UntypedDataSourceGeneratorAttribute` in particular — TUnit's own
  docs cite AutoFixture-shaped libraries as the motivating case for it),
  its per-row `TestBuilderContext`, its combinatorial interplay with
  `[Arguments]`, and its `DependencyInjectionDataSourceAttribute`-style
  class-constructor composition path together give a real, richer-than-xUnit-v3
  integration surface. **The original TUnit-first rationale (source-gen
  architectural kinship) does not survive scrutiny and is retired here** —
  TUnit being source-generated is not, on its own, consumer value; the
  actual justification is this concrete data-source extension surface,
  unrelated to whether TUnit's own test model happens to be source-generated.
- **`Compono.NUnit` — admitted candidate.** `IParameterDataSource` offers
  genuine *per-parameter* composition granularity `Compono.XunitV3`'s
  row model doesn't have, and `ITestBuilder`/`IFixtureBuilder` cover the
  row/fixture-constructor cases. A real, distinct integration shape, not a
  clone of `Compono.XunitV3`.
- **`Compono.MSTest` — admitted candidate, weakest of the three.**
  `ITestDataSource` is a stable, long-standing extension point (unchanged
  in shape since MSTest v1.2, still current in v4.3), but it's thin:
  synchronous only, no per-row context object, no combinatorial engine to
  interoperate with. It still clears "meaningful abstraction" — composed
  values as named test parameters plus a `GetDisplayName` hook for seed
  disclosure is real value over an in-body `Composer.Create<T>()` call —
  but it's the least distinctive of the three test-framework candidates.
- **`Compono.FakeItEasy` — does not clear Gate A today; downgraded to a
  documentation-only idea.** `FakeItEasy.Sdk.Create.Fake(Type)` is a real,
  clean extension point, but the resulting package would be ~80%
  structurally identical to `Compono.NSubstitute` (same
  `ICompositionValueProvider` shape, same `NotHandled`/`Handled` split,
  same static substitutability predicate), and no dogfooding pass in this
  repo's own history has ever surfaced friction pointing at FakeItEasy over
  NSubstitute. This fails "meaningful abstraction" relative to a package
  that already exists, not because FakeItEasy's own API is thin. Recorded
  in `future-packages.md` as a documentation recipe ("how to write your own
  `ICompositionValueProvider` for FakeItEasy," following
  `Compono.NSubstitute`'s published shape almost line for line) rather than
  a package candidate — promote back to a package candidate only if that
  recipe itself surfaces real friction or demand.
- **`Compono.Moq` — deferred indefinitely, blocked on maintenance health,
  not TFM compatibility.** An earlier draft of this ADR claimed Moq's lack
  of a `net8.0`/`net9.0`-specific target made it TFM-incompatible with
  Compono's own floor — that claim is wrong and is retracted here: Moq
  ships `netstandard2.0`/`netstandard2.1` assets, and
  [ADR-0037](0037-netstandard2.1-compatibility-floor.md) (its
  "netstandard2.1 Compatibility" section) already documents that a
  `net8.0`/`net9.0` project consumes a `netstandard2.0`/`netstandard2.1`-only
  dependency through NuGet's own asset-compatibility fallback without
  issue — a `net8.0`/`net9.0` consumer can restore and use Moq today. The
  actual, narrower basis for deferral: Moq has shipped **no release in
  roughly 23 months** and carries durable reputational damage from the
  4.20.0 SponsorLink incident — a workable, if reflection-heavier,
  integration surface (Moq's API is otherwise a structural cousin of
  NSubstitute's) doesn't overcome a dependency this stale. Recorded as an
  explicit deferral with a re-evaluation trigger (Moq resumes active,
  regular releases), not a silent drop — `future-packages.md` states the
  reason so it isn't mistaken for lost interest.
- **`Compono.DependencyInjection` — does not clear Gate A today as a
  package; downgraded to a documentation-only idea pending a prerequisite
  core ADR.** Research into current
  `Microsoft.Extensions.DependencyInjection` (net8/net9/net10-era) surfaces
  exactly two ideas that would need real Compono-specific bridging: keyed
  service resolution (`GetKeyedService`/`AddKeyedSingleton`, .NET 8+) and
  DI-scope ownership for a composition. Both require a **core** concept
  that doesn't exist yet — a keyed/named `CompositionProviderRequest`, and
  a composition-scope-owns-DI-scope lifetime model — which is itself a
  future core-extension ADR in the shape of ADR-0021/ADR-0024, not
  something a package's own design pass should invent ad hoc (this fails
  Gate A's "architectural fit" leg exactly as written: buildable on an
  *existing or explicitly-named* extension point, not one invented during
  the package's own design). Every other "richer DI integration" idea
  (auto-registration sugar, descriptor-driven validation) is achievable by
  a consumer today in a handful of lines against the existing
  `UseServiceProvider(...)` fallback ([ADR-0019](0019-registrations-and-service-provider-injection.md))
  and doesn't need a package at all. Recorded in `future-packages.md` as an
  idea explicitly gated behind that prerequisite core design, not a named
  package in any sequence. If it's ever designed, `Compono.DependencyInjection`
  remains the correct name — ADR-0019 already anticipated it under this
  name, and it's consistent with the `Compono.<EcosystemName>` pattern.

### No committed release sequence

Unlike the original proposal, this ADR records **no candidate order**.
`docs/roadmap/post-mvp.md`'s finding that two real dogfooding passes have
already produced zero outstanding roadmap candidates in this entire space
means every admitted candidate above is equally without evidence today —
there is no principled basis for ranking them against each other before
Gate B has anything to rank.

**Non-binding heuristics only**, to apply *if and when* more than one
candidate clears Gate B at roughly the same time: prefer higher
Compono-specific value relative to ongoing maintenance cost, and prefer
whichever candidate exercises a meaningfully different part of Compono's
architecture (validating a distinct extension point) over one that merely
repeats an already-proven pattern. **Category completion — finishing all
test-framework integrations before starting a test-double integration, or
vice versa — is explicitly rejected as a sequencing principle.** It has no
grounding in evidence or architecture; it's aesthetic completionism, and
ADR-0029's whole framework exists to keep decisions like this tied to real
signal instead.

### Implementation-readiness note (not a Gate A dimension)

Both `Compono.XunitV3` (row-binding/materialization logic) and
`Compono.NSubstitute` (the interface/delegate/abstract-class
substitutability predicate) currently hold logic that a second package in
their respective category would otherwise duplicate. This doesn't affect
either candidate's Gate A disposition, but whichever admitted candidate
is the first in its category to actually reach Gate B should extract that
shared logic to an internal location as part of its own design pass,
rather than cloning it. Recorded here so it isn't rediscovered from
scratch; not itself a design decision.

### Positive Consequences

- Gate A can actually produce a "no" for a candidate that exists only
  because its underlying library is popular, which the original OR-gate
  structurally could not.
- The candidate-by-candidate Gate A pass gives `future-packages.md` an
  honest, evidence-checked disposition for all six original candidates
  instead of leaving them as an undifferentiated list.
- No sequence is recorded that a later design pass would have to justify
  ignoring — the sequence question is deferred entirely to whenever real
  evidence exists to answer it.
- The two-stage terminology (candidate / admitted candidate / roadmap item
  / committed implementation work) gives every future discussion of "is X
  real yet" an unambiguous answer.

### Negative Consequences

- This revision spends more up-front analysis than the original three-item
  gate did, for candidates that may never clear Gate B at all — accepted,
  because the alternative (a gate that can't reject a branding wrapper) is
  the exact failure mode this ADR exists to prevent.
- `Compono.FakeItEasy` and `Compono.DependencyInjection` are downgraded
  from named package candidates to documentation-only ideas without either
  having been given its own design pass — accepted, because the research
  behind both downgrades is recorded explicitly above (structural identity
  to an existing package; a real core prerequisite that doesn't exist yet),
  not asserted without reasoning, and either can be reopened the moment
  new evidence or a prerequisite ADR changes the picture.
- `Compono.Moq`'s deferral is contingent on an external project's release
  cadence, outside this repo's control — accepted, with an explicit
  re-evaluation trigger recorded rather than a silent drop.

## Pros and Cons of the Options

### No formal admission gate

- Good, because it defers all criteria to each candidate's own future
  design pass, when more context exists.
- Bad, because without a standing bar, a low-value candidate could reach a
  design pass before its lack of value is obvious — and this revision's
  research shows that's not hypothetical (`Compono.FakeItEasy`,
  `Compono.Moq` both would have looked superficially admissible under no
  gate at all).

### Single OR-gate + committed sequence (this ADR's original 2026-08-11 proposal)

- Good, because it records both a qualification bar and sequencing
  rationale in one place, with a stable linkable identity.
- Bad, because the OR-gate cannot reject a candidate that merely claims to
  "supply composed values" — it describes package shape, not merit.
- Bad, because the committed sequence had no evidentiary basis once
  checked against `docs/roadmap/post-mvp.md`'s actual dogfooding record —
  zero candidates have evidence today, so ranking them was prediction, not
  planning.

### Two-stage admission model, no committed sequence (chosen)

- Good, because Gate A can produce a real "no" (demonstrated concretely
  against `Compono.FakeItEasy`/`Compono.Moq`/`Compono.DependencyInjection`
  above) while still admitting genuinely distinct integrations
  (`Compono.TUnit`/`Compono.NUnit`/`Compono.MSTest`).
- Good, because it keeps Gate A (architectural legitimacy) and Gate B
  (evidence) as separate questions rather than conflating them, matching
  how ADR-0029 already frames "roadmap candidate" as its own gate.
- Good, because it doesn't ask this ADR to predict an order no evidence
  yet supports.
- Bad, because a reader wanting "just tell me what's next" gets no ranked
  answer — accepted, since ADR-0029's own framework says that answer
  doesn't exist yet, and pretending otherwise is the thing being corrected.

## Links

- [Future Packages](../roadmap/future-packages.md) — the roadmap page this
  ADR is proposed from, and where each candidate's Gate A disposition
  above should be reflected.
- [Post-MVP Roadmap](../roadmap/post-mvp.md) — the evidence record showing
  zero outstanding roadmap candidates from two real dogfooding passes,
  the finding that eliminated this ADR's original committed sequence.
- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  Gate B, unchanged by this ADR; the evidence-over-prediction bias this
  revision is scoped to actually honor, not just avoid contradicting.
- [ADR-0024](0024-public-provider-extensibility-model.md),
  [ADR-0025](0025-compono-nsubstitute-package-design.md),
  [ADR-0021](0021-row-composition-entry-point-for-test-framework-integrations.md),
  [ADR-0022](0022-compono-xunit-package-design.md) — the existing
  extension-point pattern every Gate A disposition above is judged
  against.
- [ADR-0019](0019-registrations-and-service-provider-injection.md) — the
  DI boundary this ADR's `Compono.DependencyInjection` disposition builds
  on directly; already anticipated this package's name and shape.
- [ADR-0037](0037-netstandard2.1-compatibility-floor.md) — documents the
  NuGet asset-compatibility fallback that makes Moq's
  `netstandard2.0`/`netstandard2.1` assets consumable from `net8.0`/
  `net9.0`; the reason `Compono.Moq`'s deferral below is grounded in
  maintenance health, not TFM incompatibility.

## Amendment 1 (2026-08-21): `Compono.DependencyInjection` — the name has since been claimed by a different, narrower, separately-accepted design

The Candidate-by-candidate Gate A disposition above says
`Compono.DependencyInjection` "does not clear Gate A today as a package,"
evaluating one specific idea: a **richer** `Microsoft.Extensions.DependencyInjection`
integration (keyed-service resolution, DI-scope ownership for a
composition) requiring core concepts that didn't exist. That disposition
still stands, unchanged, for that idea — it remains ungated and
undesigned, and `future-packages.md`'s "richer `Microsoft.Extensions.DependencyInjection`
integration" entry still records it as such.

A **different, narrower** idea — a reverse bridge exposing a
`CompositionRow` as a plain `IServiceProvider` (`row.AsServiceProvider()`)
for consumers that already accept one — was separately proposed, gated
through Gate A on its own merits (buildable entirely on an existing
extension point, `CompositionRow.TryResolveConfigured`, with no new core
concept invented), and accepted as [ADR-0047](0047-compono-dependencyinjection-configured-resolution-bridge.md).
It shipped under the name `Compono.DependencyInjection` — the same name
this ADR's own disposition above used for the richer idea, and the name
this ADR's own text already anticipated being reused ("If it's ever
designed, `Compono.DependencyInjection` remains the correct name").

Caught in PR review (#105, for ADR-0047/PLAN-0047): read in isolation,
this ADR's Candidate-by-candidate disposition still reads as rejecting
"the package that ships under this name," which is no longer accurate —
a package by this name exists and is accepted, just not the one this
ADR's disposition evaluated. This amendment records the reconciliation:
this ADR's original Gate A "no" is about the richer MS.DI-integration
idea specifically, not about the name or about every possible package
that could ever be named `Compono.DependencyInjection`; ADR-0047 is a
separate, later, independently-gated acceptance for a different design
that happens to share the name. Both stand simultaneously without
conflict once read this way. No text above is edited — this amendment
exists precisely so the original disposition's reasoning (why the richer
idea specifically failed Gate A) stays intact and undiluted.
