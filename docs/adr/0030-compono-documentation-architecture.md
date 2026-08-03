# [ADR-0030] Compono Documentation Architecture

**Status:** Accepted

**Date:** 2026-08-03

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

`docs/mvp.md`'s Milestone 8 scope originally said only "README and
getting-started guide, Architecture documentation, Samples" — it named a
handful of artifacts, not a structure, and left "how is all of this
organized so a stranger can learn Compono" entirely undesigned. Left alone,
that design work would have started from scratch at the beginning of
Milestone 8, using only whatever context happened to still be fresh in
whoever picked it up.

Milestone 7 changes what's available for that design work. Dogfooding
`cosmere-tracker`'s real AutoFixture-based test kit
([ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md))
is the first time anyone has actually *used* Compono the way a real adopter
would — hit real friction, asked real "how do I..." questions, discovered
which AutoFixture concepts translate cleanly and which don't, and found a
real gap in the framework itself (`CMP0001` blocking a directly-composed
`HttpClient`, per the migration guide). That's exactly the raw material a
good information architecture needs, and it's expensive to reconstruct
later from a finished migration guide and a closed research doc — the
specific moments of "I didn't know where to look for this" are the thing
most likely to be lost if documentation design waits for Milestone 8.
[PLAN-0007 Amendment](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-4-2026-08-03-documentation-architecture-becomes-a-required-milestone-7-deliverable)
records why this work moved into Milestone 7 itself; this ADR is the actual
design decision that amendment made room for.

The scope here is deliberately bounded: this ADR decides the
**architecture** — the hierarchy, the purpose/audience/ordering of every
section, and how forward-looking content stays separate from the primary
learning path — not the finished prose. Milestone 8 writes, refines, and
publishes content against this blueprint; this ADR and
`docs/documentation-architecture.md` (the living reference doc this ADR
produces) are what it executes against.

## Decision Drivers

- The documentation must be organized around **what a developer is trying
  to do**, not around Compono's own package/namespace boundaries — a
  newcomer doesn't arrive already knowing that `[Shared]` lives in
  `Compono.XunitV3` or that `UseBogus()` lives in `Compono.Bogus`; they
  arrive with a question ("how do I customize one member?") and the
  structure should answer it without requiring that knowledge first.
- A new user should be able to go from "never heard of Compono" to
  "productively using it" by reading forward through one path, not by
  jumping between unrelated top-level sections to assemble the story
  themselves.
- Every one of the concrete questions in ADR-0029's dogfooding evidence and
  the questions the user listed directly ("how do I reproduce a failure,"
  "why did composition fail," "what order do providers execute") needs an
  obvious, single home — not "technically covered somewhere across three
  documents."
- Forward-looking content (roadmap candidates, `Proposed` ADRs, experimental
  features, future integration packages) has to be genuinely discoverable
  — a reader auditing "is X planned?" needs a real path to the answer — but
  must not contaminate the primary learning path with content that isn't
  usable yet. `docs/mvp.md`'s own non-goals list and ADR-0029's roadmap
  mechanics already establish that "planned" and "shipped" are different
  categories; the documentation structure has to keep making that
  distinction visible.
- The migration guide (`docs/migrating-from-autofixture.md`) is a
  flagship artifact, not an appendix — it is the only place in the entire
  documentation set backed by a real, evidence-audited migration, and it
  answers a real, common on-ramp question ("I already know AutoFixture,
  how do I think in Compono terms").
- Reuse established documentation prior art rather than inventing a bespoke
  taxonomy from nothing — per `design-decisions.md` rule 5, note explicitly
  what's adopted vs. changed rather than silently copying a pattern.
- The architecture has to survive Compono actually growing — new packages,
  new integrations, new milestones — without requiring another redesign
  each time; per the user's framing, "assume this becomes the long-term
  foundation for all public Compono documentation."

## Considered Options

### Organizing principle

1. **Package/namespace-oriented.** One top-level section per package
   (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus`),
   each covering that package's own concepts, how-tos, and reference
   material end to end.
2. **Developer-journey-oriented**, loosely following the
   [Diátaxis](https://diataxis.fr) framework's four-mode split (tutorials,
   how-to guides, reference, explanation) adapted to Compono's own
   vocabulary — sections organized around what stage of learning/use a
   reader is in, with package-specific content living inside whichever
   journey stage it's relevant to (a "Package Guides" area for the
   ecosystem-level "when do I install this" question, but member-level
   concepts like `[Shared]` or `UseBogus()` documented where the *concept*
   belongs, not siloed by package).

### Where the migration guide lives

1. **Keep it under `docs/migration/`, linked from elsewhere but not part of
   the primary nav** — effectively an appendix.
2. **Promote it to a first-class, top-level entry** in the main
   documentation hierarchy, positioned for the specific audience it serves
   (an experienced AutoFixture user), not folded into How-to Guides or
   Concepts where that framing would get diluted.

### Where forward-looking/experimental content lives

1. **Mixed into the relevant section, flagged inline** — e.g., a
   `Proposed` capability documented directly under Concepts or Package
   Guides with an "experimental"/"planned" admonition banner.
2. **A dedicated `Roadmap` area** collecting all forward-looking content
   (evidence-backed roadmap candidates, `Proposed`-but-`Accepted` ADRs not
   yet implemented, future milestones, experimental features, future
   integration packages) with its own "available today vs. experimental vs.
   planned" framing, cross-linked from the relevant concept/package page
   but never inline in the primary learning path.

## Decision Outcome

### Chosen: developer-journey organization (Diátaxis-adapted), migration guide promoted to top-level, dedicated Roadmap area

**Developer-journey organization (Option 2 for organizing principle).**
Package-oriented organization (Option 1) was rejected because it's
organized around Compono's own implementation boundaries, not around what a
reader is trying to accomplish — the exact anti-pattern this ADR's Decision
Drivers call out. A reader asking "how do I customize one member?" doesn't
benefit from first figuring out that the answer lives in `Compono` core
while "how do I use NSubstitute?" lives in a different top-level section
organized the same way for a different package; both are "how do I..."
questions and belong in the same *kind* of section. The chosen structure
adapts [Diátaxis](https://diataxis.fr)'s four-mode split — the framework
distinguishes documentation by two axes (learning vs. working, and
practical vs. theoretical), producing tutorials, how-to guides, reference,
and explanation as the four resulting quadrants. What's adopted: the
four-mode distinction itself, and the principle that a single page should
commit to one mode rather than mixing "here's why" with "here's how." What's
changed: Diátaxis doesn't have an opinion on cookbooks, package-ecosystem
guides, or migration guides specifically, and Compono's explanation mode
splits further into **Concepts** (the mental model a user needs while
working) and **Architecture** (why the system is built this way
internally, aimed at a different, deeper-curiosity reader) rather than one
undifferentiated "explanation" bucket — this split maps directly onto two
real audiences this project already has evidence for (a test author who
needs the composition mental model vs. a contributor or curious adopter who
wants to know why source generation, why fail-fast recursion, why this
seeding scheme).

**Migration guide promoted to top-level (Option 2 for its placement).**
Keeping it under `docs/migration/` as a linked-but-not-navigated appendix
(Option 1) undersells the thing Milestone 7 actually produced: a
real-evidence, before/after account of every AutoFixture concept an adopter
already knows, written and audited against an actual migration rather than
theory. Its audience — someone who already knows AutoFixture and wants the
fastest possible on-ramp — is real and distinct enough to deserve its own
top-level entry rather than being folded into How-to Guides (which assumes
no prior framework knowledge) or Concepts (which teaches Compono's model on
its own terms, not by contrast). See "Documentation hierarchy" below for
exactly where it sits in the reading order.

**Dedicated Roadmap area (Option 2 for forward-looking content).** Inline
flagging (Option 1) was rejected because it risks exactly the contamination
this ADR's Decision Drivers warn against — a newcomer skimming Concepts for
"how composition works today" shouldn't have to mentally filter out
"...except this part, which is still `Proposed`." A dedicated `Roadmap`
area, explicitly framed around "available today / experimental / planned,"
gives forward-looking content a real, indexed home (satisfying "is X
planned?" for a reader who's actually looking) without ever appearing
uninvited in the primary learning path. Cross-links run one direction: a
concept page may link out to "there's a proposed enhancement for this, see
Roadmap," but the Roadmap area is never a prerequisite for understanding
anything that exists today.

### Documentation hierarchy

Ten top-level areas, in the order a developer is expected to progress
through them (later areas assume familiarity built by earlier ones, except
Reference/Troubleshooting/Roadmap, which are look-up destinations reachable
from anywhere):

1. **Getting Started** — audience: someone who has never used Compono.
   What it is, why it exists (vs. AutoFixture/hand-written setup), install,
   first composed test. Ends by branching the reader toward Concepts (to
   understand the model) or Cookbook (to solve an immediate problem).
2. **Concepts** — audience: someone actively writing tests with Compono.
   The mental model: composition, profiles, registrations/rules, shared
   values, providers (conceptually — internals are Architecture's job),
   determinism/seeding, collections. Each page answers "what is this and
   when do I reach for it," not "how is it implemented."
3. **How-to Guides** — audience: someone with a specific, moderately-scoped
   task who already has the Concepts model. Task-oriented, assumes
   prerequisite concepts are already understood and links back to them
   rather than re-explaining. Covers exactly the question list the user
   supplied (create an object, write a composed theory, customize one
   member, register a type, use profiles, reproduce a failure, etc.).
4. **Cookbook** — audience: someone who wants a fast, copy/paste answer to
   one narrow, practical problem, without reading a full guide. Every page
   solves exactly one problem, short and self-contained. Distinct from
   How-to Guides by scope and depth (a How-to Guide teaches a skill; a
   Cookbook page hands over a working snippet) — see "Cookbook" below.
5. **Migrating from AutoFixture** — audience: an experienced AutoFixture
   user. Placed here, after the reader already has Getting Started/Concepts
   available to link back into, but before Package Guides, since migration
   decisions (which packages to adopt) naturally lead into package-level
   detail next.
6. **Package Guides** — audience: someone deciding whether/how to adopt a
   specific integration package. One page per package
   (`Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`):
   when to install it, when not to, ecosystem fit, common patterns/mistakes,
   interactions with the other packages.
7. **Architecture** — audience: a contributor, a curious adopter, or anyone
   evaluating Compono at a deeper level than "how do I use it." Why Compono
   exists and how it works internally: source generation, generated plans,
   plan discovery, the provider pipeline, deterministic seeding,
   performance, design philosophy — tradeoffs and rationale, not just
   descriptions. The public-facing home for the `docs/adr/` decision log
   (linked as a "why we built it this way" index for readers who want the
   full paper trail), and where `docs/performance.md` moves.
8. **Troubleshooting** — audience: anyone stuck, at any stage. Reachable
   from everywhere, not just read start-to-end: common errors/diagnostics
   (indexed by diagnostic code, e.g. `CMP0001`), a FAQ, and "why did
   composition fail"/"what order do providers execute"-style questions that
   don't fit neatly into Concepts because they're debugging-first, not
   model-first.
9. **Reference** — audience: anyone who already knows what they're looking
   for and wants the authoritative, exhaustive answer. Generated API
   reference, a diagnostics index (every `CMP` code), glossary. Not meant
   to be read start to end.
10. **Roadmap** — audience: anyone asking "is X available/planned?"
    Available-today vs. experimental vs. planned, evidence-backed roadmap
    candidates (`docs/roadmap/post-mvp.md`, already established by
    ADR-0029), `Proposed`/`Accepted`-but-unimplemented ADRs, future
    milestones, experimental features, future integration packages. Never
    a prerequisite for anything above it.

`Contributing`, `Manifesto`, and `Design Principles` are not part of the
developer-journey progression (they're not "how do I use Compono," they're
"how do I contribute" and "what does this project believe") and stay as
standalone pages linked from Architecture/the site footer rather than
occupying a numbered slot in the journey. `docs/mvp.md`,
`docs/plans/`, and `docs/research/` remain internal engineering-process
artifacts — useful to a contributor reading the ADR trail, not part of the
public site nav; only `docs/adr/` graduates into the public Architecture
area, per the driver above.

### Cookbook

Distinct from How-to Guides on two axes: **scope** (one page, one problem —
never "and then also configure X") and **depth** (a working snippet plus
the minimum context to trust it's correct, not a taught skill). Cookbook
pages assume the reader already has Compono installed and understands
enough to paste code in; they don't re-teach Concepts. Every Cookbook page
answers exactly one of the concrete "how do I..." questions this ADR's
Decision Drivers list that's narrow enough to be a recipe rather than a
guide (e.g. "generate a realistic-looking email" is Cookbook; "how do
providers get composed together" is Concepts, not Cookbook, because it's a
model, not a recipe).

### Package Guides

Each of the four current packages
(`Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`) gets one
page answering, in this order: when to install it, when *not* to (the
absence of this is exactly what made ADR-0029's `Compono.Bogus`
"mandatory adoption" clause necessary to write explicitly — a
package's own docs should make this call obvious without needing an ADR to
say so), how it fits into the ecosystem (what it depends on, what depends
on it), common usage patterns, common mistakes, and interactions with the
other three packages. A future package (per the Roadmap area) gets its own
page here the moment it ships, not before.

### Migrating from AutoFixture

Organized around the AutoFixture concepts a migrator already knows, not a
mechanical API mapping table — matching how
`docs/migrating-from-autofixture.md` is already structured today
(`Freeze<T>()`, `AutoDataAttribute`/customizations, `AutoNSubstituteCustomization`,
recursion behaviors, specimen builders, `Compono.Bogus` as an added
capability). Milestone 7 already produced and evidence-audited this content
as a living document (ADR-0029); this ADR's contribution is the placement
decision above (Option 2), not new content — Milestone 8 refines and
publishes what Milestone 7 wrote, per each entry's existing shape:
AutoFixture approach, Compono approach, why Compono chose that design,
concepts that disappear entirely vs. become simpler, tradeoffs, and a real
before/after example.

### Architecture

Explains **why**, not just **what** — tradeoffs and rejected alternatives,
matching the bar this repo's own ADRs are already held to
(`design-decisions.md`), rather than restating implementation. Topics:
source generation (why generated-first, not reflection-first —
ADR-0001), generated plans and plan discovery (ADR-0004), the provider
pipeline (ADR-0010's stage ordering, "what order do providers execute" from
the Decision Drivers list), deterministic seeding (ADR-0012/ADR-0026), and
performance (`docs/performance.md`, moved here). Each Architecture page
cross-links to the ADR(s) that made the underlying decision, rather than
re-deriving the reasoning — this is the same "link to the ADR, don't
re-explain it" rule `design-decisions.md` already applies everywhere else,
now applied to public-facing prose too.

### Roadmap and future evolution

The single home for everything not yet available to a user today:

- **Evidence-backed roadmap candidates** — `docs/roadmap/post-mvp.md`,
  already established by ADR-0029; every entry traces to real migration
  evidence and its own `Proposed` ADR.
- **`Proposed`/`Accepted`-but-unimplemented ADRs** — indexed here by
  status, distinct from `docs/adr/README.md`'s own full index (which is an
  engineering artifact listing every ADR regardless of implementation
  state) — this index filters to just "decided or proposed, not yet real,"
  answering "is X planned?" directly instead of requiring a reader to
  cross-reference implementation status against the ADR list by hand.
- **Future milestones** — a public-friendly summary of `docs/mvp.md`'s
  remaining roadmap, without exposing the internal phase-by-phase execution
  detail that belongs in `docs/plans/`.
- **Experimental features** — if/when Compono ships an opt-in experimental
  capability (e.g., a future reflection-compatibility mode per
  ADR-0001's own Consequences section), its own page lives wherever its
  *concept* belongs (Concepts, Package Guides, etc.) but carries an
  "Experimental" admonition banner and a line pointing back to this
  Roadmap area — not hidden away from where a user would naturally look for
  it, just clearly labeled once found.
- **Future integration packages** — get a placeholder entry here (name,
  intended purpose, status) the moment they're roadmapped, and graduate
  into their own Package Guides page the moment they ship — never both at
  once.

### Positive Consequences

- A new user has one linear path from zero knowledge to productive use,
  instead of needing to assemble the story from package-scoped docs
  themselves.
- Every concrete question this ADR's Decision Drivers and the user's brief
  listed has exactly one obvious home, checkable by reading the hierarchy
  above.
- Forward-looking content stays fully discoverable without ever
  contaminating a page a new user would read while learning Compono today.
- The migration guide gets the prominence its real, evidence-audited
  content earned, without distorting the rest of the hierarchy's
  organizing principle to accommodate it.
- The architecture accommodates new packages, new milestones, and new
  experimental features by adding a page in an existing area, not by
  redesigning the hierarchy.

### Negative Consequences

- Package-specific content is deliberately *not* consolidated into one
  place per package — a reader learning `Compono.NSubstitute` may need to
  read a Concepts page, a How-to page, and the `Compono.NSubstitute`
  Package Guide to get the full picture, rather than one package-scoped
  document. Accepted: this is the direct tradeoff of organizing around the
  reader's journey instead of Compono's own package boundaries, which this
  ADR's Decision Drivers explicitly prefer.
- Splitting Concepts from Architecture (rather than one "explanation"
  bucket, as plain Diátaxis would have it) is a deviation from the
  framework being adapted, and requires editorial judgment per page about
  which of the two a given topic belongs to. Accepted: the split maps onto
  two real, distinct audiences this project already has evidence for (see
  "Decision Outcome" above), and the alternative (one undifferentiated
  explanation section) would force a curious-internals reader to wade
  through user-facing conceptual material to find the "why," or vice versa.
- This ADR designs the hierarchy and section purposes but does not write
  the content — Milestone 8 could still under-deliver on any individual
  section regardless of how well the structure is designed. Accepted: an
  architecture is a necessary precondition for good documentation, not a
  substitute for actually writing it.

## Pros and Cons of the Options

### Package/namespace-oriented

- Good, because it mirrors how the codebase itself is organized, which is
  easy for a *maintainer* to keep in sync.
- Good, because "which package do I need" is trivially answerable.
- Bad, because a reader arrives with a task, not a package, and has to
  learn Compono's own module boundaries before the docs become useful.
- Bad, because concepts that span packages (e.g. `[Shared]` behavior
  interacting with `Compono.NSubstitute`'s substitute creation) don't have
  a clean single home.

### Developer-journey-oriented, Diátaxis-adapted (chosen)

- Good, because it matches how a reader actually arrives and progresses:
  question-first, not package-first.
- Good, because it's a proven external framework, not a bespoke taxonomy
  invented for this one project — lower risk of missing an entire mode of
  documentation a reader needs.
- Good, because it scales cleanly as new packages ship — a new package adds
  a Package Guide and slots its concepts into existing Concepts/How-to/
  Cookbook pages, rather than requiring a whole new top-level section.
- Bad, because package-specific content is spread across sections instead
  of consolidated (see Negative Consequences above).

### Keep the migration guide as an appendix

- Good, because it requires no navigation/placement decision at all.
- Bad, because it undersells the guide's actual, evidence-backed content
  and makes it harder for its real audience (an AutoFixture user evaluating
  Compono) to find.

### Promote the migration guide to top-level (chosen)

- Good, because it matches the guide's actual value and gives its distinct
  audience a direct path.
- Good, because it sits naturally between Cookbook (solving today's problem)
  and Package Guides (which packages to adopt), which is exactly the
  decision-making moment a migrating AutoFixture user is in.
- Bad, because it's one more top-level nav entry to maintain — accepted,
  given the content already exists and just needs a placement decision.

### Mix forward-looking content inline, flagged

- Good, because a reader learning about a concept sees "and here's what's
  coming" in the same place, no extra navigation.
- Bad, because it risks exactly the primary-learning-path contamination
  this ADR's Decision Drivers warn against, and makes "is X planned"
  unanswerable without reading every page that might mention it.

### Dedicated Roadmap area (chosen)

- Good, because "is X planned" has one direct, indexed answer.
- Good, because nothing in the primary learning path ever needs an
  in-context caveat about what's real vs. proposed.
- Bad, because a reader has to know the Roadmap area exists to find
  forward-looking content proactively — mitigated by cross-linking from the
  relevant concept/package page outward, per "Roadmap and future evolution"
  above.

## Links

- [ADR-0029](0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md) —
  Milestone 7's dogfooding strategy; its Amendment 4 is what made room for
  this ADR's scope within Milestone 7
- [PLAN-0007](../plans/0007-milestone-7-dogfooding.md) — Phase 5 tracks
  execution of this ADR's decisions (the skeleton, the migration-guide
  promotion, the Milestone 8 handoff)
- [docs/documentation-architecture.md](../documentation-architecture.md) —
  the living reference doc this ADR produces: the concrete hierarchy, every
  section's purpose/audience/contents, and the ordering a new user should
  follow
- [Diátaxis](https://diataxis.fr) — the external framework this ADR adapts
  (four-mode split); see "Decision Outcome" above for what was kept vs.
  changed
- `docs/migrating-from-autofixture.md` — the flagship artifact
  this ADR promotes to top-level placement
- `docs/roadmap/post-mvp.md` — the evidence-backed roadmap content the
  Roadmap area's structure is built around
- `docs/adr/README.md` — the full ADR index; this ADR's Roadmap area
  surfaces a status-filtered *subset* of it for public readers, not a
  replacement
- `mkdocs.yml` — the site nav Milestone 8 updates to match this ADR's
  hierarchy when it publishes

## Amendment 1 (2026-08-03): documentation as a first-class product, two new areas, and cross-cutting standards

Review of this ADR's first draft surfaced ten points that sharpen the
decision without reversing it — the developer-journey hierarchy adapted
from Diátaxis, the migration guide's top-level promotion, and the dedicated
Roadmap area all stand exactly as originally decided. This Amendment adds:

1. **Documentation is a first-class product of Compono, not supporting
   material.** Its quality, discoverability, consistency, maintainability,
   and overall learning experience are considered part of the framework
   itself and evolve alongside the codebase — the same rigor this repo
   already holds source code to (`coding-standards.md`, `testing.md`)
   applies to documentation. This is now the governing philosophy statement
   for every documentation decision going forward, stated explicitly in
   `docs/documentation-architecture.md` rather than left implicit.
2. **A documentation quality bar**, applied per page (not a rigid template
   — a bar to strive for): what problem this solves, why/when to use it,
   when *not* to, a minimal example, a realistic example, common mistakes,
   and links to related concepts/cookbook/API reference. Recorded in full
   in `docs/documentation-architecture.md`'s new "Documentation quality
   bar" section.
3. **Every page must lead somewhere.** Documentation must not dead-end — a
   Concepts page ends by pointing at the How-to Guide that applies it, the
   Cookbook recipes that use it, and the API reference that documents it
   precisely. This generalizes what each section's existing "Relates to"
   already did informally into an explicit, restated rule every new page is
   held to.
4. **Cookbook is expected to become one of the largest sections** —
   plausibly hundreds of recipes over the project's lifetime, not a small
   fixed set. This changes how Cookbook is described (an intentionally
   large, growing, searchable surface) but not its per-page shape.
5. **Samples becomes its own top-level area, distinct from Cookbook.**
   Cookbook solves one narrow problem per page; Samples are complete,
   runnable applications (Basic Usage, ASP.NET API, CQRS, Clean
   Architecture, Minimal APIs, MediatR, EF Core) demonstrating multiple
   concepts working together in realistic architecture. Conflating the two
   would force either Cookbook's recipes to bloat past "short and
   self-contained," or Samples to shrink past "demonstrates a realistic,
   multi-concept setup" — genuinely different jobs, per the Considered
   Options this ADR already applies elsewhere in this document.
6. **Best Practices becomes its own top-level area.** Distinct from
   Concepts (what things are) and Cookbook (how to solve one narrow
   problem): accumulated guidance for "what's the recommended way" once a
   reader already knows how to use Compono — organizing profiles at scale,
   naming conventions, reusing configuration, performance recommendations,
   keeping tests readable and deterministic, avoiding brittleness.
7. **Learning Paths** — curated, ordered reading lists through the
   *existing* hierarchy for a specific starting point ("I'm new to
   Compono," "I'm migrating from AutoFixture," "I use xUnit/NSubstitute,"
   "I want realistic data," "I want to extend Compono"). Pure navigation
   aid, not new content and not a new organizing principle — it lives
   inside Getting Started (`getting-started/learning-paths.md`) rather than
   becoming its own top-level area, since it's a wayfinding tool for a
   reader who just arrived, not a distinct audience/purpose the way the
   other ten areas are.
8. **Architecture splits into three distinguishable parts**: Design
   Principles (current, evolving — what Compono believes), Current
   Architecture (how it works today — source generation, generated plans,
   provider pipeline, deterministic seeding, performance), and the
   Historical Decision Log (the public `docs/adr/` index, unchanged from
   this ADR's original "decision-log.md" plan). Most users only need
   Current Architecture; contributors additionally want the Decision Log;
   keeping the three visibly separate (not one undifferentiated
   "Architecture" page) serves both without making either audience wade
   through the other's material.
9. **API reference supplements documentation, it never replaces it.**
   Explicit philosophy: a reader should be able to learn Compono entirely
   through Getting Started, Concepts, Guides, Cookbook, Samples, and Best
   Practices; Reference answers a precise question for someone who already
   knows what they're looking for, not a teaching surface. This sharpens
   Reference's existing "not meant to be read start to end" framing into an
   explicit product-level stance.
10. **The hierarchy is package-count-agnostic by design, stated
    explicitly.** Package Guides and Roadmap's future-packages entry
    already implied this; this Amendment makes it an explicit design
    constraint: adding a fifth, sixth, or Nth integration package must
    require exactly one new Package Guide page and one new ecosystem-map
    row — never a hierarchy redesign, regardless of how large the package
    set grows.

None of these ten points changes this ADR's core Decision Outcome
(developer-journey organization adapted from Diátaxis, migration guide
promoted to top-level, dedicated Roadmap area for forward-looking content)
— points 5 and 6 add two new areas within that same organizing principle,
and the rest are cross-cutting standards or internal refinements to
existing areas. `docs/documentation-architecture.md` is updated in full to
reflect all ten points, per this repo's "docs/*.md describes current
intent, ADRs describe the decision" split.
