# Capability & Package Admission

**Audience:** anyone asking *"should Compono support X?"* — a contributor
proposing a new capability, a maintainer triaging a feature request, or an
agent evaluating a candidate package before starting a design pass.

**What this page is:** the current, standalone, operational description of
how Compono decides whether a proposed capability, feature, integration, or
package gets admitted. Read this page alone to run the process — you do not
need to read any ADR first.

**What this page is not:** a history of *why* Compono's admission policy
looks the way it does. That rationale — the alternatives considered, the
research behind each threshold, the real candidates evaluated against
it — lives in [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)
and [ADR-0039](../adr/0039-future-extension-package-admission-gate-and-release-sequence.md)
(see **Provenance** at the bottom). This page is kept in sync with those
ADRs as the current process; if a future ADR changes the policy, this page
must be updated in the same PR — the same "update docs alongside the
decision that changed them" rule this repo already applies to every other
subsystem doc (see [Contributing](../contributing.md)).

## When this process applies

Run this process for:

- a genuinely new capability (Compono does something it doesn't do today)
- a material expansion of an existing package's public surface
- a new extension/integration package

Do **not** run this process for:

- a bug fix restoring behavior an `Accepted` ADR or existing documentation
  already promised
- straightforward implementation work against an already-`Accepted` ADR
- a small, mechanical, additive change with no new design decision (a
  cookbook recipe, a test, a docs fix)

This process is a gate on new *design* decisions, not ceremony for every
change — see [`contributing.md`](../contributing.md)'s "before you start"
section for the same boundary stated from a contributor's side ("anything
beyond a small fix... open a Feature Request issue first").

## The two-stage model

Every candidate passes through two independent gates, in order. They
answer different questions and neither substitutes for the other:

| Gate | Question | Answered by |
|---|---|---|
| **Gate A — Architectural admission** | Could this legitimately be part of Compono at all? | This page, applied once per candidate |
| **Gate B — Evidence admission** | Is there real reason to build it *now*? | Real demand: dogfooding friction, a repeated consumer request, or an explicit product-owner request |

A candidate that fails Gate A is rejected (or downgraded to a
documentation-only idea) regardless of how much demand exists for it — Gate
A is not a formality an eager candidate can outweigh with enthusiasm. A
candidate that clears Gate A but has no Gate B evidence yet is a real,
recorded idea, not yet worth building.

### Terminology

These four terms have precise, distinct meanings — use them, not looser
synonyms, when discussing where a candidate stands:

1. **Candidate** — proposed, not yet evaluated against Gate A.
2. **Admitted candidate** — cleared Gate A: architecturally legitimate,
   still no evidence. Recorded in
   [`docs/roadmap/future-packages.md`](../roadmap/future-packages.md), not
   as its own `Proposed` ADR.
3. **Roadmap item** — cleared Gate B too: real evidence exists. Gets its
   own problem-focused `Proposed` ADR, listed in
   [`docs/roadmap/post-mvp.md`](../roadmap/post-mvp.md).
4. **Committed implementation work** — the roadmap item's ADR reaches
   `Accepted` (its own full design pass, not just the problem statement)
   and a Plan moves `In Progress` against it.

"Not a package" and "not a good idea" are different verdicts — a proposal
can fail as a new *package* and still be admitted as core behavior, an
addition to an existing package, or a documentation recipe. Don't conflate
"this capability should exist" with "this deserves a new NuGet package";
Gate A's last criterion below exists specifically to keep those questions
separate.

## Step-by-step: running the process

### Step 1 — State the concrete problem

Require a real consumer/testing/composition problem, described concretely
— not "this would be nice" or "AutoFixture has this." Compono's explicit
non-goal is reproducing AutoFixture feature-for-feature
([design-principles.md](design-principles.md)); the existence of a feature
somewhere else is not itself a reason to add it here. If you can't state a
concrete scenario where a real test author is blocked or meaningfully
worse off without this, stop — there's no problem yet to evaluate against
the gates below.

### Step 2 — Check how the problem is solved today

Before treating this as a gap, check whether it's already solved by:

- Compono core
- an existing Compono extension package
- ordinary .NET APIs
- first-party Microsoft testing abstractions
- an established ecosystem library
- a few lines of ordinary consumer code
- documentation/sample guidance (a Cookbook recipe, not a code change)

The existence of boilerplate alone does not justify a new abstraction —
Gate A's "meaningful abstraction" criterion (Step 4) asks specifically
whether a consumer could already write this themselves in an afternoon.
`Compono.FakeItEasy` failed admission for exactly this reason: FakeItEasy's
`Sdk.Create.Fake(Type)` is a real extension point, but the resulting
package would be ~80% structurally identical to the already-shipped
`Compono.NSubstitute` — the "gap" was already closed by an existing
package wearing a different ecosystem's name.

**"The .NET API is short" answers a narrower question than "this is
already solved."** Checking "is this already solved" means checking
whether the consumer gets a natural, discoverable, composition-native
answer *without leaving Compono's composition model* — not just whether
the underlying framework call is short. A one-line `Options.Create(...)`
or `new ConfigurationBuilder()...Build()` can still leave a real gap if a
consumer has to repeatedly reconstruct integration ceremony by hand, keep
several related registrations consistent with each other, or rediscover
the same pattern from memory every time — that's friction in the
composition workflow, not in the construction call, and this step must
weigh both. Two real, `Accepted` precedents establish that Compono already
treats this as legitimate: `CompositionBuilder.Share<T>()`
([ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md))
was admitted even though `[Shared]` already made sharing fully possible,
because expressing it as graph-wide, profile-reusable composition
configuration — rather than an attribute a consumer must remember to
attach to every relevant test signature — was itself real product value;
`Compono.Bogus` ([ADR-0027](../adr/0027-compono-bogus-package-design.md))
was admitted even though a consumer could always hand-write a
plausible-looking fake value, because making that behavior discoverable
and natural inside the composition model (`UseBogus()`) was the actual
value, not raw difficulty. **Neither direction of this is a license to
loosen the bar**: "the framework already lets you do this" does not, by
itself, prove the capability is already well-solved *inside Compono's
model* — but "I sometimes forget the exact syntax" does not, by itself,
justify a Compono abstraction either. A convenience that leaves the
consumer no more correct, expressive, reusable, or composition-native than
before is still a trivial wrapper, and still fails Step 3 below — a named
method wrapping one `Register<T>()` call for an already-simple type is
exactly this trap, not an example of legitimate ergonomic value.

### Step 3 — Check Gate A: architectural admission

A candidate must clear **all five** of the following — not just one — to
become an admitted candidate. These are evaluated once per candidate, and
none of them requires evidence of demand yet (that's Gate B):

1. **Compono-specific value.** It solves meaningful composition-related
   friction, not branding or convenience around an already-easy call. A
   package that exists only because its underlying library is popular
   fails here, even if it can technically claim to "supply composed
   values." This friction is not only "the operation is hard to perform"
   — real composition ergonomics count too, when the improvement is
   genuinely about expressing intent inside Compono's composition model
   rather than merely shortening syntax. Evidence worth weighing here
   (none of it automatic — each still has to show up as a real, sourced
   finding, not an assumption) includes: expressing intent at the
   composition level instead of leaking it into individual tests or
   production types; keeping consumers inside the composition model
   instead of forcing them to reconstruct framework-specific ceremony
   repeatedly; making behavior naturally reusable through profiles;
   making related dependency shapes coherent instead of independently
   hand-wired (and thus prone to silently drifting inconsistent);
   establishing consistent semantics consumers would otherwise repeatedly
   reinvent, each slightly differently; and making the obvious,
   discoverable Compono path also the correct one. `Share<T>()` and
   `Compono.Bogus` (Step 2, above) both cleared this criterion on exactly
   this kind of evidence, not on raw difficulty.
2. **Native ecosystem fit.** The resulting API is idiomatic in the
   integrated ecosystem's own terms — not a clone of an existing Compono
   integration's shape bolted onto a different framework's extension
   model. (`Compono.NUnit`'s `IParameterDataSource` gives genuine
   per-parameter granularity `Compono.XunitV3`'s row model doesn't have —
   a real, distinct shape, not a re-skin.)
3. **Meaningful abstraction.** Consumers get materially more than a
   trivial extension method they could write themselves in an afternoon.
   Judge this against the *whole* consumer composition workflow, not just
   the one construction expression — remembering which framework API/
   package is involved, wrapping and registering it correctly, keeping
   several related registrations consistent with each other, and making
   the result reusable, can add up to real friction even when any single
   line of it looks trivial in isolation. A candidate clears this
   criterion by collapsing that workflow into a coherent composition
   concept, not merely by giving an existing one-liner a Compono-branded
   name — a helper that leaves the consumer no more correct, expressive,
   reusable, or composition-native than before still fails here, no
   matter how the friction is described.
4. **Architectural fit.** It can be built entirely on an existing public
   extension point, or on a to-be-designed extension point named
   explicitly as a prerequisite — never on a core change invented ad hoc
   during the candidate's own design pass. (`Compono`'s core package must
   never reference or know about an integration package — see
   [design-principles.md](design-principles.md)'s "Modular architecture."
   If satisfying the candidate requires reflection or hidden state, that
   conflicts with [ADR-0001](../adr/0001-source-generation-first.md)'s
   no-reflection-by-default posture and needs a much higher bar to survive
   as an intentional exception rather than a workaround.)
5. **Package-boundary justification.** *If* this is proposed as a new
   package: the dependency genuinely belongs outside core, and is
   substantial enough to justify another independently-consumed artifact
   rather than a documentation recipe or an addition to an existing
   package. This is a separate question from "should this capability
   exist" — see Step 6.

Maintenance/CI/docs/skill-maintenance cost (an additional entry in the
`compono` agent skill's detection table, another package guide, another
CI package-validation target) is a real **weighing factor** across all
five criteria above, not a standalone sixth pass/fail condition — it's
linear and small per additional package for this repo's existing routing
pattern, and shouldn't by itself veto a candidate that otherwise clears
the five bars.

A candidate that fails Gate A does not get a `Proposed` ADR of its own,
regardless of demand. It's either rejected outright, or — where a Gate A
finding surfaces a genuine, narrower recipe worth writing down —
downgraded to a **documentation-only idea** recorded in
`future-packages.md` (see Outcomes, below).

### Step 4 — Check Gate B: evidence admission

A candidate that clears Gate A is an admitted candidate — architecturally
legitimate, but still just an idea until real evidence justifies building
it now. Evidence can come from more than one source; dogfooding is strong
evidence but is **not** the only accepted trigger:

- **Dogfooding friction** — a real migration or real project surfaces
  repeated, concrete friction. This is the strongest form of evidence
  because it's falsifiable: a spike built to exercise a hypothesis is
  structurally likely to "prove" it matters even when real usage wouldn't;
  a real call site that already existed before Compono was involved is
  not.
- **A repeated, concrete consumer request** naming a specific scenario.
- **An explicit product-owner request** — this alone has satisfied Gate B
  for real, shipped packages (`Compono.TUnit`, `Compono.NUnit`, and
  Compono-owned source-generated test doubles all cleared Gate B this way,
  with no dogfooding evidence at the time). A clear ask from whoever owns
  the product direction is real evidence, not a fallback used only when
  dogfooding hasn't happened yet.

When evidence does come from a real migration, weigh it with the same four
questions every time — for the candidate gaps named below and any further
one a migration surfaces:

1. **Observed frequency.** How many real, distinct places actually needed
   this behavior — not "could plausibly use it," but did, in the code as
   it stood.
2. **Was this scenario ever intended to work?** If Compono's documented or
   `Accepted`-ADR behavior already claims to support the scenario and it
   doesn't, that's a **bug**, not a design question — fix it through the
   normal engineering workflow (`tasks/implement.md`/`tasks/pr-review.md`),
   not this process. Skip the remaining questions.
3. **Workaround cost.** Concretely, what does Compono's existing explicit
   alternative cost — extra parameters, extra lines, an implementation
   detail leaking into a test signature — shown as a real before/after, not
   a hypothetical. A low or zero cost points toward "acceptable
   alternative, no new capability needed"; a real, material cost points
   toward a genuine capability gap.
4. **Principle alignment.** Would satisfying this gap require reflection
   or hidden state conflicting with the no-reflection-by-default posture,
   or with this project's explicit-over-implicit bias
   ([design-principles.md](design-principles.md))? A gap that can only be
   closed by working against an existing constraint needs a much higher
   bar on frequency and cost before it becomes a genuine capability gap
   rather than an intentional design difference.

**Do not turn "it must be dogfooded" into an absolute prerequisite** — an
admitted candidate with no dogfooding history can still clear Gate B on an
explicit, well-reasoned product-owner request. Conversely, don't let "a
consumer might want this someday" stand in for real evidence either — a
plausible-sounding future want is not evidence under any of the three
triggers above.

### Step 5 — Weigh the cost

Evidence required should be proportional to the cost and permanence of what's
being proposed. Consider, relative to the candidate's actual scope:

- public API surface added
- generator complexity, if any
- runtime machinery and allocations
- new dependencies
- ongoing maintenance burden
- documentation burden (a new Concept page, Package Guide, or Cookbook
  entry)
- skill/eval burden (the `compono` agent skill's per-package reference
  files)
- compatibility commitments and future design constraints this creates

A one-line addition to an existing package's public surface needs far less
evidence than a new package with its own release cadence and support
surface.

### Step 6 — If admitted, decide where it belongs

Passing Gate A and Gate B means the capability should exist — it does not
by itself mean it needs a new package. Work down this list and stop at the
smallest home that's honestly justified:

1. **Core `Compono`.** Only if it doesn't depend on any test framework or
   test-double/data library — core must never reference or know about an
   integration package.
2. **An existing extension package.** If the capability's dependency
   already matches an existing package's ecosystem (e.g. something
   NSubstitute-specific belongs in `Compono.NSubstitute`, not a new
   package).
3. **A new extension package.** Only when Gate A's package-boundary
   criterion is genuinely satisfied — the dependency belongs outside core
   *and* outside every existing package, and is substantial enough to
   justify its own independently-consumed artifact.
4. **Documentation/sample guidance only.** When the capability is real and
   worth recording, but doesn't need new code at all — a Cookbook recipe
   showing how a consumer builds it themselves in a few lines.

Two real precedents show this isn't a rubber stamp toward "new package":

- **`Compono.DependencyInjection`** shipped, but as a narrower
  configured-resolution `IServiceProvider` bridge
  (`row.AsServiceProvider()`) than the "richer DI integration" idea
  originally evaluated (keyed-service resolution, DI-scope ownership) —
  that larger idea failed Gate A's architectural-fit criterion because it
  needed a core concept that didn't exist yet, and remains a
  documentation-only idea today, unrelated to the narrower thing that
  actually shipped under the same name.
- **FakeItEasy** support was downgraded from a package candidate to a
  documentation-only recipe ("how to write your own
  `ICompositionValueProvider` for FakeItEasy," following
  `Compono.NSubstitute`'s published shape) rather than becoming
  `Compono.FakeItEasy` — the capability is real and worth documenting, but
  didn't justify its own package.

## Outcomes

Every evaluated candidate ends in exactly one of these:

| Outcome | Meaning | Where it's recorded |
|---|---|---|
| **Rejected** | Fails Gate A; no legitimate Compono capability here | Not recorded as a candidate; reopen only if new evidence changes the Gate A analysis |
| **Documentation-only** | Fails Gate A as a *package*, but the capability is worth a recipe/guide | `docs/roadmap/future-packages.md`, "Documentation-only ideas" |
| **Deferred** | Clears Gate A, but blocked on an external factor (e.g. a dependency's maintenance health) | `docs/roadmap/future-packages.md`, "Deferred indefinitely," with an explicit re-evaluation trigger |
| **Admitted candidate** | Clears Gate A; no Gate B evidence yet | `docs/roadmap/future-packages.md`, "Admitted candidates" |
| **Roadmap item** | Clears Gate A and Gate B | Problem-only `Proposed` ADR, listed in `docs/roadmap/post-mvp.md` |
| **Committed implementation work** | Roadmap item's ADR reaches `Accepted`, Plan moves `In Progress` | The ADR + its Plan, per the normal design/implement workflow |

A finding from a real migration that isn't a genuine capability gap at all
still gets classified and recorded (per ADR-0029), even though it never
enters this table as a candidate:

- **Acceptable Compono-native alternative** — a different API than the
  thing being compared against, but the replacement stays pleasant (low
  workaround cost, no material readability loss). Documented as a pattern
  in the relevant guide; no ADR or Amendment needed.
- **Intentional design difference** — the alternative would conflict with
  Compono's principles, or costs more than its observed value justifies. A
  dated Amendment to the ADR that governs the existing behavior records
  the evidence and the "no change" verdict — this is a real, indexed "no,"
  not a dropped finding.
- **Migration-only friction** — pain during a one-time conversion that
  doesn't persist in the resulting test suite. Recorded as a tip for the
  next migrator; no ADR or Amendment needed.

## Decision flow

```
Proposed capability / package
        |
        v
Step 1: Is there a real, concrete problem?
        | no -> stop, not a candidate
        v yes
Step 2: Is an existing solution already good enough?
        | yes -> Documentation-only (a recipe, not a package)
        v no
Step 3: Gate A — architecturally legitimate?
  (Compono-specific value, native ecosystem fit, meaningful
   abstraction, architectural fit, package-boundary justification)
        | fails -> Rejected, or Documentation-only if a narrower
        |          recipe genuinely survives
        v clears
   Admitted candidate
        |
        v
Step 4: Gate B — real evidence now?
  (dogfooding friction / repeated consumer request /
   explicit product-owner request)
        | no evidence yet -> stays Admitted candidate
        | external blocker -> Deferred (with re-evaluation trigger)
        v evidence exists
   Roadmap item -> Proposed ADR
        |
        v
   ADR reaches Accepted, Plan In Progress
        |
        v
   Committed implementation work
        |
        v
Step 6: Where does it live?
  Core / Existing package / New package / Docs-only
```

## Worked examples

Real Compono history, not hypotheticals:

- **`Compono.TUnit` (admitted, shipped).** Cleared Gate A on a real,
  distinct integration surface (TUnit's `IDataSourceAttribute` family,
  per-row `TestBuilderContext`) — not because TUnit is source-generated
  like Compono itself; that original rationale didn't survive scrutiny and
  was explicitly retired. Cleared Gate B via an explicit product-owner
  request, not dogfooding. Reached committed implementation work and
  shipped.
- **`Compono.Http` (admitted, shipped).** Cleared Gate B through real
  dogfooding evidence: a real consumer project's hand-rolled,
  reflection-based `HttpMessageHandler` fake, used across 41 real call
  sites, with duplicated fake-handler classes solving the same problem
  three different ways. The friction was real, repeated, and worse than
  every surveyed alternative — a strong Gate B case.
- **`Compono.FakeItEasy` (rejected as a package, documentation-only).**
  Real extension point, but ~80% structurally identical to
  `Compono.NSubstitute` — failed "meaningful abstraction" relative to a
  package that already exists, not because FakeItEasy's own API is thin.
- **`CompositionBuilder.Share<T>()` (admitted, shipped — composition
  ergonomics, not raw difficulty).** `[Shared]` already made sharing a
  value across a composition graph fully possible before this shipped —
  nothing was technically blocked. Admitted anyway because expressing
  sharing as graph-wide, profile-reusable composition configuration,
  instead of an attribute a consumer has to remember to attach to every
  relevant test signature, was itself real Compono-specific value (Step 2/
  Gate A criterion 1, above).
- **`Compono.Bogus` (admitted, shipped — composition ergonomics, not raw
  difficulty).** A consumer could always hand-write a plausible-looking
  fake value; nothing about that is hard. Admitted because making
  realistic-looking values discoverable and natural inside the
  composition model (`UseBogus()`) was the actual value.
- **`Compono.Moq` (deferred).** A workable integration surface exists, but
  Moq had shipped no release in roughly 23 months and carries
  reputational damage from a past incident — deferred with an explicit
  re-evaluation trigger (Moq resumes active releases), not silently
  dropped.
- **NSubstitute's `ConfigureMembers` (intentional design difference).**
  Real dogfooding surfaced a case where AutoFixture's
  `AutoNSubstituteCustomization { ConfigureMembers = true }`
  auto-configures every generated substitute's members recursively.
  `Compono.NSubstitute` deliberately doesn't — that gap was weighed
  through the Gate B rubric and, if the evidence supports it, recorded as
  a dated Amendment to the ADR governing that decision rather than
  becoming a new roadmap item. (Illustrative of the *mechanism*; check
  that ADR's own Amendments for the actual, current verdict rather than
  treating this summary as the record.)

## When this page is not enough

This page is the operational summary. For the full reasoning behind a
threshold, a rejected alternative, or a specific candidate's disposition,
follow the links into the ADRs below — you shouldn't need to, but they're
the permanent record if you want it.

## Provenance

This page consolidates the current admission policy from:

- [ADR-0029](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md)
  — the evidence rubric (Gate B), the four evidence questions, the
  five-way finding classification (bug / roadmap candidate / acceptable
  alternative / intentional design difference / migration-only friction),
  and the bug-handling carve-out.
- [ADR-0039](../adr/0039-future-extension-package-admission-gate-and-release-sequence.md)
  and its Amendment 1 — the two-stage model, Gate A's five criteria, the
  candidate/admitted-candidate/roadmap-item/committed-implementation-work
  terminology, and the explicit rejection of a committed release sequence.
- Real Gate A/Gate B applications recorded in
  [ADR-0040](../adr/0040-compono-tunit-package-design.md) (`Compono.TUnit`),
  [ADR-0042](../adr/0042-compono-owned-source-generated-test-doubles.md)
  (Compono-owned test doubles),
  [ADR-0051](../adr/0051-compono-http-handler-based-testing-package.md)
  (`Compono.Http`), and
  [ADR-0059](../adr/0059-compono-nunit-package-design.md) (`Compono.NUnit`)
  — the worked examples above are drawn from these.
- [`docs/roadmap/future-packages.md`](../roadmap/future-packages.md) and
  [`docs/roadmap/post-mvp.md`](../roadmap/post-mvp.md) — the live,
  current status of every candidate this process has ever evaluated.
- [ADR-0056](../adr/0056-composition-builder-share-graph-wide-sharing.md)
  and [ADR-0027](../adr/0027-compono-bogus-package-design.md) — the
  `Share<T>()`/`Compono.Bogus` precedent establishing that composition
  ergonomics (Step 2, Gate A criterion 1 above) count as legitimate
  Compono-specific value independent of raw operation difficulty.
- [`docs/research/0028-compono-options-configuration-admission-research.md`](../research/0028-compono-options-configuration-admission-research.md)
  — the investigation whose reassessment surfaced that this page could be
  read too narrowly (raw API simplicity treated as sufficient reason to
  stop at Step 2) and fed the composition-ergonomics clarifications above
  back into this page.

The ADRs above remain authoritative for historical rationale and the
architectural decision history — the alternatives considered, why they
were rejected, and the research behind each threshold. **This page is the
canonical current operational description of the admission process.**
When a future ADR changes the policy (a new Gate A criterion, a revised
evidence bar, a retired outcome category), update this page in the same
PR — an admission process that only lives correctly in an ADR's prose
recreates exactly the discoverability problem this page exists to solve.
