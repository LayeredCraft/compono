# Compono Documentation Architecture

Decision record: [ADR-0030](adr/0030-compono-documentation-architecture.md)
(including Amendment 1). Tracked by:
[PLAN-0007](plans/0007-milestone-7-dogfooding.md) Phase 5 (architecture +
skeleton, Milestone 7) and Milestone 8 (writing, refining, publishing
against this blueprint).

This is the living reference for Compono's public documentation — the
hierarchy, what belongs in each section, who it's for, and the order a new
user should move through it. It describes **intended** state: most of the
pages named below exist today as placeholder skeletons (a "Status:
Skeleton" banner and a one-paragraph purpose statement, no real content —
see "Status" per section for exactly which). A handful of real exceptions
don't exist purely as skeletons: `contributing.md` doesn't exist at all yet
(Milestone 8 Phase 6); `reference/api/` is real, generated content as of
[ADR-0032](adr/0032-api-reference-documentation-toolchain.md)/PLAN-0008
Phase 1, not a placeholder. `docs/roadmap/post-mvp.md` was one of these
exceptions until [PLAN-0007](plans/0007-milestone-7-dogfooding.md) Phase 3
produced it — real content, not a skeleton, since Milestone 7's dogfooding
pass surfaced zero roadmap-candidate findings. ADR-0030 records *why*
this shape was chosen; this document is
*what* it is, kept current as Milestone 8 replaces each skeleton with real
content.

This is a blueprint, not finished documentation — nothing here should be
read as "already published."

## Documentation is a first-class product

**The documentation is a first-class product of Compono, not supporting
material.** The quality, discoverability, consistency, maintainability, and
overall learning experience are considered part of the framework itself
and evolve alongside the codebase. This governs every decision in this
document and every documentation decision made after it — the same rigor
`coding-standards.md` and `testing.md` hold source code to applies here.
Concretely, this means: a documentation gap is a product gap, not a
backlog nice-to-have; documentation changes get the same review scrutiny as
code; and the "Documentation quality bar" below is a bar to actually meet,
not aspirational text.

## Documentation quality bar

Not a rigid template — a template that fits every page equally well
doesn't exist, and forcing one produces padded, mechanical pages. This is
the bar every public documentation page should strive to clear, and the
thing a documentation review checks a page against:

- What problem does this solve?
- Why would I use it?
- When should I use it?
- When should I *not* use it?
- A minimal example.
- A realistic example.
- Common mistakes.
- Related concepts.
- Related Cookbook articles.
- Related API reference.

Different sections satisfy this differently — a Cookbook page's "minimal
example" *is* the whole page, while a Concepts page's realistic example
might be a link to a Sample rather than inline code — but every page
should have *answered* each of these somewhere, even if the answer is a
link outward rather than inline prose.

## Every page leads somewhere

Documentation must not dead-end. Every page ends by pointing at the next
thing a reader plausibly wants — this is what "Related concepts/Cookbook/
API reference" in the quality bar above means in practice, generalized into
a standing rule. For example, a Concepts page about profiles should end
with something like:

- Learn how to build a profile → How-to Guides
- Learn how to use a profile in xUnit → Package Guides (`Compono.XunitV3`)
- Related Cookbook recipes → Cookbook
- Related API reference → Reference

Every section's own "Relates to" line below is this rule applied to that
section specifically — read them as instances of this one standing rule,
not a separate concern per section.

## How to read this document

Each section states: **Audience** (who's reading), **Status** (exists
today / skeleton only / not yet created), **Purpose**, **Contents** (what
belongs here, what doesn't), and **Relates to** (what it assumes the
reader already knows, and what it hands off to next — see "Every page
leads somewhere" above). The numbered order matches the intended reading
order for a new user working straight through; Troubleshooting, Reference,
and Roadmap are look-up destinations, reachable from anywhere, not
additional steps in that linear path.

## Documentation tree

```
docs/
├── index.md                                  # Home (exists — light edits only)
├── getting-started/
│   ├── index.md                              # What is Compono?
│   ├── installation.md
│   ├── first-test.md                         # Your first composed theory
│   ├── learning-paths.md                     # curated paths through the site (see below)
│   └── next-steps.md
├── concepts/
│   ├── index.md
│   ├── composition-model.md
│   ├── profiles.md
│   ├── registrations-and-rules.md
│   ├── shared-values.md
│   ├── providers.md
│   ├── determinism-and-seeding.md
│   └── collections.md
├── how-to/
│   ├── index.md
│   ├── create-an-object.md
│   ├── write-a-composed-theory.md
│   ├── customize-a-member.md
│   ├── register-a-type.md
│   ├── use-profiles.md
│   └── share-a-value-across-a-test.md
├── cookbook/
│   ├── index.md
│   └── <one page per narrow, practical problem — expected to grow into the
│        largest section on the site; see "Cookbook" below>
├── samples/                                   # public-preview launch: index.md +
│   ├── index.md                               # basic-usage.md + aspnet-api.md only —
│   ├── basic-usage.md                         # ADR-0033 defers cqrs/clean-architecture/
│   └── aspnet-api.md                          # minimal-apis/mediatr/ef-core as future
│                                               # candidates, not stub nav pages (see "5. Samples")
├── migrating-from-autofixture.md             # promoted to top-level (Phase 5)
├── packages/
│   ├── index.md
│   ├── compono.md
│   ├── compono-xunitv3.md
│   ├── compono-nsubstitute.md
│   └── compono-bogus.md                      # one more page per future package —
│                                              # never a hierarchy change; see "Package Guides"
├── best-practices/
│   ├── index.md
│   ├── organizing-profiles.md
│   ├── large-test-suites.md
│   ├── naming-conventions.md
│   ├── reusing-configuration.md
│   ├── performance-recommendations.md
│   └── deterministic-and-non-brittle-tests.md
├── architecture/
│   ├── index.md                              # how these three parts relate
│   ├── design-principles.md                  # current, evolving: what Compono believes
│   ├── current/
│   │   ├── source-generation.md
│   │   ├── generated-plans-and-discovery.md
│   │   ├── provider-pipeline.md
│   │   ├── deterministic-seeding.md
│   │   └── performance.md                    # moved from docs/performance.md
│   └── decision-log.md                       # historical: public-facing index into docs/adr/
├── troubleshooting/
│   ├── index.md
│   ├── common-errors.md                      # indexed by diagnostic code (CMP0001, ...)
│   └── faq.md
├── reference/
│   ├── index.md                              # states: supplements the docs, doesn't replace them
│   ├── api/                                  # generated API reference
│   ├── diagnostics.md                        # every CMP code, one entry each
│   └── glossary.md
├── roadmap/
│   ├── index.md                              # today / experimental / planned
│   ├── post-mvp.md                           # real content (PLAN-0007 Phase 3, done)
│   ├── proposed-adrs.md                      # status-filtered ADR index
│   └── future-packages.md
└── contributing.md                           # not yet created (Milestone 8 scope)
```

`docs/manifesto.md` and `docs/public-api.md` exist today but are **not**
part of this canonical tree — per
[ADR-0030 Amendment 2](adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items),
both are retired once their content is fully redistributed into
`architecture/design-principles.md`/`architecture/` + `reference/`
(Milestone 8 Phase 5), not kept as a permanent third location for the
same material. Shown struck through in the Open Items list below rather
than left in the tree, so this diagram matches what Phase 7's
`mkdocs.yml` nav check actually verifies against.

Internal, non-public artifacts that stay exactly where they are and do
**not** join the site nav: `docs/adr/` (source of truth
`architecture/decision-log.md` indexes from), `docs/plans/`,
`docs/research/`, `docs/mvp.md`. These describe engineering process, not
usage — see ADR-0030's Decision Outcome for why they stay separate from the
learning path.

## 1. Getting Started

**Audience:** someone who has never used Compono.
**Status:** real content (all 5 pages, written in
[PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 2).
`docs/index.md` covers a subset of this today.
**Purpose:** answer "what is this, and can I get something working in the
next five minutes," and point every kind of newcomer toward the right next
step.
**Contents:**
- `index.md` — what Compono is, the composition-over-object-generation
  pitch, how it differs from AutoFixture/hand-written setup (a short
  contrast, not the full migration story — that's its own section).
- `installation.md` — which package(s) to add for a first project
  (`Compono` + `Compono.XunitV3` covers the common case), minimal
  `csproj`/`nuget.config` needs.
- `first-test.md` — one worked example: a composed xUnit theory, end to
  end, explained line by line.
- `learning-paths.md` — curated, ordered reading lists for a specific
  starting point, borrowed from Microsoft Learn's learning-path pattern.
  Pure navigation — every linked page already exists somewhere else in
  this hierarchy; a path never introduces new content of its own. Starting
  candidate paths: "I'm new to Compono," "I'm migrating from AutoFixture,"
  "I use xUnit," "I use NSubstitute," "I want realistic data," "I want to
  extend Compono." Lives here (not as its own top-level area) because it's
  a wayfinding aid for someone who just arrived, not a distinct
  audience/purpose the way each numbered section below is.
- `next-steps.md` — branches the reader outward: "want the mental model
  next? → Concepts. Have an immediate problem to solve? → Cookbook. Want a
  curated path instead of picking yourself? → Learning Paths."
**Relates to:** assumes nothing. Hands off to Concepts (for the model),
Cookbook (for an immediate task), or Learning Paths (for a curated route
through everything else).

## 2. Concepts

**Audience:** someone actively writing tests with Compono who needs the
mental model, not yet a specific task.
**Status:** real content (all 8 pages, written in
[PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 2).
**Purpose:** teach *what things are and when to reach for them* — the
vocabulary every later section assumes.
**Contents:** one page per concept, each answering "what is this / when do
I use it," not "how is it implemented" (that's Architecture):
- `composition-model.md` — what "composing" a graph means in Compono's
  terms; `Composer`/`CompositionContext` at a conceptual level.
- `profiles.md` — `ICompositionProfile`, why/when to group configuration
  into one. Example of "every page leads somewhere" applied: this page ends
  with links to How-to Guides' "use a profile," Package Guides'
  `Compono.XunitV3` page (using a profile in xUnit specifically), relevant
  Cookbook recipes, and the `ICompositionProfile` API reference entry.
- `registrations-and-rules.md` — `Register<T>`, `For<T>().Use(...)`,
  member rules — the configuration surface, conceptually.
- `shared-values.md` — `[Shared]`, why/when a value needs to be the same
  instance across a composition.
- `providers.md` — what a provider is and does, conceptually (the
  pipeline's actual stage order is Architecture's job, not this page's).
- `determinism-and-seeding.md` — what "deterministic by design" means for
  a test author (reproducible failures), not the derivation algorithm
  itself.
- `collections.md` — `CreateMany<T>()`, collection-size policy.
**Relates to:** assumes Getting Started. Hands off to How-to Guides (apply
a concept to a task), Package Guides (which package a concept lives in),
Cookbook (a narrow recipe using this concept), and Reference (the precise
API entry) — per "Every page leads somewhere" above.

## 3. How-to Guides

**Audience:** someone with a specific, moderately-scoped task, who already
has the Concepts model.
**Status:** real content (all 7 pages, written in
[PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 2).
**Purpose:** task-oriented instructions — assumes prerequisite concepts,
links back to them instead of re-teaching.
**Contents:** directly covers the concrete question list ADR-0030's
Decision Drivers named:
- `create-an-object.md` — how do I create an object?
- `write-a-composed-theory.md` — how do I write my first composed theory?
  (deeper than Getting Started's worked example — variations, edge cases.)
- `customize-a-member.md` — how do I customize one member?
- `register-a-type.md` — how do I register a type?
- `use-profiles.md` — how do I use profiles?
- `share-a-value-across-a-test.md` — how do I share a value across a test?
**Relates to:** assumes Concepts. Hands off to Cookbook for narrower,
single-snippet variants of the same tasks, to Best Practices for "what's
the recommended way to do this at scale," and to Package Guides for
integration-specific how-tos (`use-nsubstitute`/`use-bogus`-shaped
questions live in the relevant Package Guide, not here, since they require
package-specific context this section doesn't assume).

## 4. Cookbook

**Audience:** someone who wants a fast, copy/paste answer to one narrow
problem, without reading a full guide.
**Status:** skeleton exists (`index.md` only — placeholder, no real
recipes yet).
**Purpose:** recipes, not lessons. See ADR-0030's "Cookbook" section for
the exact scope/depth distinction from How-to Guides, and "Samples" below
for the distinction from complete applications.
**Contents:** one page per problem, each short and self-contained (working
code first, minimal surrounding context). Candidate pages, drawn from real
questions this milestone's dogfooding surfaced or already listed elsewhere
in this document: "generate a realistic email," "freeze a shared
`HttpMessageHandler`," "override one field only for one test," "seed a
specific failing case for reproduction," "compose a substitute with one
method stubbed." **This section is expected to become one of the largest
on the site** — plausibly 50-100+ recipes in the near term and
several hundred over the project's lifetime, not a small fixed set. That's
a strength, not a maintenance risk: recipes are narrow enough to write and
review quickly, easy to contribute (a natural first PR for a new
contributor), and searchable in a way a handful of long guides isn't. At
that scale, flat alphabetical listing stops being the primary navigation
method — see "Open Items" below for the tagging/subcategorization decision
this implies.
**Relates to:** assumes Getting Started only (a reader may jump straight
here without reading Concepts first — that's the point). Cross-links back
to the relevant Concept/How-to page for readers who want the *why* behind
a recipe, and out to Samples for a reader whose "recipe" has grown into
"I actually need to see this working end to end."

## 5. Samples

**Audience:** someone who wants to see multiple concepts working together
in a realistic application, not a single isolated problem.
**Status:** per [ADR-0033](adr/0033-public-preview-samples-strategy.md),
the public-preview launch set is **two** runnable samples (Basic Usage,
ASP.NET API) — not the eight originally sketched here. `docs/samples/`
carries only those two real pages plus `index.md` at launch; the other
five (CQRS, Clean Architecture, Minimal APIs, MediatR, EF Core) are
recorded as future candidates (see Contents below), not published
skeleton pages, and their `mkdocs.yml` nav entries are removed rather
than shipped as placeholders — see
[PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 4.
**Purpose:** complete, runnable applications — deliberately distinct from
Cookbook, not a larger cookbook entry. Cookbook is one problem, one
solution, copy/paste, short; Samples are full applications with realistic
architecture, demonstrating how Compono's pieces compose together in
practice. Conflating the two would force Cookbook's recipes to bloat past
"short and self-contained" or Samples to shrink past "a realistic,
multi-concept setup" — they solve genuinely different reader needs.
**Contents:** at launch, exactly two runnable applications — Basic Usage
(the core workflow, small and focused) and ASP.NET API (the full
ecosystem — `Compono`/`Compono.XunitV3`/`Compono.NSubstitute`/
`Compono.Bogus` — in one realistic application). CQRS, Clean
Architecture, Minimal APIs, MediatR, and EF Core remain documented as
future candidates in `docs/roadmap/future-packages.md`-style framing (not
as their own nav entries or stub pages) — per ADR-0033, each graduates to
a real page only once it would demonstrate a materially different
Compono pattern the launch two don't already cover, not merely a
different host framework. Each `docs/samples/*.md` page for a real
sample is a short overview (what it demonstrates, which concepts/packages
it exercises) linking out to the actual runnable project — the sample
*applications themselves* live as real, buildable code (e.g. a top-level
`samples/` directory alongside `src/`/`test/`), not as documentation
prose; `docs/samples/` is the documentation *about* them, not the code.
**Relates to:** assumes Concepts and, typically, Package Guides (a sample
usually exercises more than one package together). Hands off to Best
Practices for "why is this sample structured this way" and Architecture for
readers who want the deeper "why" behind a pattern the sample uses.

## 6. Migrating from AutoFixture

**Audience:** an experienced AutoFixture user evaluating or actively
migrating to Compono.
**Status:** exists today at `docs/migrating-from-autofixture.md` —
substantially complete (Milestone 7's own required deliverable), promoted
to this top-level path in Phase 5, `mkdocs.yml` nav entry already added.
Only its content's final publication review remains, which is Milestone
8's job.
**Purpose:** the fastest on-ramp for someone who already thinks in
AutoFixture's terms — organized around AutoFixture concepts, not a
mechanical API mapping table.
**Contents:** unchanged from what Milestone 7 already produced — one
section per AutoFixture concept
(`Freeze<T>()`/hidden shared values, `AutoDataAttribute`/customizations,
`AutoNSubstituteCustomization`, recursion behaviors, specimen builders,
`Compono.Bogus` as an added capability), each covering: the AutoFixture
approach, the Compono approach, why Compono chose that design, concepts
that disappear entirely vs. become simpler, tradeoffs, and a real
before/after example from the actual `cosmere-tracker` migration. See
ADR-0030's "Migrating from AutoFixture" section for why this stays
content-stable and only its placement changes.
**Relates to:** assumes the reader already knows AutoFixture (doesn't
re-teach Compono from zero — links into Concepts for that). Hands off to
Package Guides once the reader has decided what to adopt.

## 7. Package Guides

**Audience:** someone deciding whether/how to adopt a specific package.
**Status:** real content, all 5 pages
([PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 3).
**Purpose:** the ecosystem-level "what is this package for" question, one
page per package.
**Contents:** per package (`compono.md`, `compono-xunitv3.md`,
`compono-nsubstitute.md`, `compono-bogus.md`), in this fixed order per
page: when to install it, when *not* to, how it fits into the ecosystem
(dependencies, what depends on it), common usage patterns, common
mistakes, interactions with the other packages. `packages/index.md` is the
ecosystem map — a single table a reader scans to pick which package(s)
they need before diving into any one guide. **This structure is
package-count-agnostic by design** — today's four packages are not assumed
to be the permanent set; a fifth, sixth, or Nth integration package adds
exactly one page here and one row to the ecosystem-map table, never a
hierarchy redesign.
**Relates to:** assumes Concepts (package guides don't re-teach `[Shared]`
or profiles from scratch, they show how this package participates in
them). A future package gets its page here the moment it ships (see
Roadmap's "future integration packages").

## 8. Best Practices

**Audience:** someone who already knows how to use Compono and is now
asking "what's the recommended way to do this at scale?"
**Status:** skeleton exists (all 7 pages — placeholder only, no real
content yet).
**Purpose:** accumulated experience and guidance — distinct from Concepts
(what things are) and Cookbook (how to solve one narrow problem right
now). This is where "the recommended way" lives once there is one.
**Contents:**
- `organizing-profiles.md` — how to structure profiles as a project grows
  (one per feature area vs. one per test project, composition via
  `AddProfile<T>`, when to split vs. combine).
- `large-test-suites.md` — patterns that hold up as a suite scales.
- `naming-conventions.md` — profile/test naming that stays readable at
  scale.
- `reusing-configuration.md` — sharing registrations/rules across profiles
  without duplication.
- `performance-recommendations.md` — practical guidance, distinct from
  Architecture's `performance.md` (which explains *why* the numbers are
  what they are); this page is "what should I actually do."
- `deterministic-and-non-brittle-tests.md` — keeping tests reproducible
  and resistant to unrelated changes, as a suite grows.
**Relates to:** assumes Concepts, How-to Guides, and typically Package
Guides (best practices are often package-specific, e.g. NSubstitute usage
patterns at scale). Hands off to Samples for a worked example of a
practice actually applied, and Architecture for the deeper "why" behind a
recommendation.

## 9. Architecture

**Audience:** three related but distinct readers, kept visibly separate
rather than merged into one undifferentiated "Architecture" page (Amendment
1): most users only need **Current Architecture**; contributors
additionally want the **Historical Decision Log**; anyone evaluating
Compono's philosophy wants **Design Principles**.
**Status:** skeleton exists for all 8 pages in the structure below
(placeholder only); separately, `docs/architecture.md`, `docs/performance.md`,
`docs/design-principles.md`, `docs/manifesto.md`, `docs/public-api.md`
have real content today but pre-date this hierarchy — consolidating that
real content into the skeleton below is Milestone 8 work.
**Purpose:** why Compono exists and how it works internally — tradeoffs and
rejected alternatives, not just a description of the current shape.
**Contents:** three explicitly separate parts:
- **Design Principles** (`design-principles.md`) — current, evolving: what
  Compono believes (composition over object generation, predictability
  over magic, source-generated by default, deterministic by design).
  Absorbs `docs/design-principles.md`/`docs/manifesto.md`'s content over
  time (see Open Items) — this is a *living* statement, revised as the
  project's philosophy actually evolves, not a historical snapshot.
- **Current Architecture** (`current/`) — how it works today:
  `source-generation.md` (why generated-first, not reflection-first —
  ADR-0001), `generated-plans-and-discovery.md` (ADR-0004),
  `provider-pipeline.md` (the actual stage order — ADR-0010, "what order do
  providers execute" answered at the depth Concepts' `providers.md`
  deliberately didn't go to), `deterministic-seeding.md` (the derivation
  algorithm itself — ADR-0012/ADR-0026), `performance.md` (moved from
  `docs/performance.md`, methodology and results unchanged).
- **Historical Decision Log** (`decision-log.md`) — the public-facing index
  into `docs/adr/`: every `Accepted`/`Superseded` ADR, one line each, for a
  reader who wants the full paper trail. Not a duplicate of
  `docs/adr/README.md` (the engineering-process index, including
  `Proposed` ones — see Roadmap below for where those surface publicly).
  As this log grows across years of decisions, it stays a pure historical
  record — readers wanting "how it works today" belong in Current
  Architecture, not here.
**Relates to:** assumes Concepts (explains the internals behind the model
Concepts already taught). Every Current Architecture/Design Principles page
cross-links to the ADR(s) that made the underlying decision rather than
re-deriving the reasoning.

## 10. Troubleshooting

**Audience:** anyone stuck, at any point — reachable from anywhere, not a
step in the linear reading order.
**Status:** real content, all 3 pages
([PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 3).
**Purpose:** fast path from "something's wrong" to a fix.
**Contents:**
- `common-errors.md` — indexed by diagnostic code (e.g. `CMP0001`, the
  real gap Milestone 7's dogfooding surfaced) and by symptom ("why did
  composition fail," "why is my substitute returning null").
- `faq.md` — questions that don't map to a single diagnostic but come up
  repeatedly ("why does Compono fail fast on recursion instead of omitting
  a value like AutoFixture did" is exactly this shape — a real gap-3
  finding from Milestone 7).
**Relates to:** self-contained; links back to the relevant Concepts/
Architecture page for readers who want to understand *why* an error exists,
not just how to fix it.

## 11. Reference

**Audience:** anyone who already knows what they're looking for.
**Status:** `api/` generated for all four publishable packages, per
[ADR-0032](adr/0032-api-reference-documentation-toolchain.md)
([PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 1);
`index.md`/`diagnostics.md`/`glossary.md` all real content
([PLAN-0008](plans/0008-milestone-8-public-preview.md) Phase 3).
**Purpose:** authoritative, exhaustive, not meant to be read start to end.
**API reference supplements the documentation — it never replaces it.** A
reader should be able to learn Compono entirely through Getting Started,
Concepts, Guides, Cookbook, Samples, and Best Practices; Reference exists
to answer a precise question for someone who already knows the framework,
not to teach it. If a reader can only find the answer to "how do I use
this" in Reference and nowhere else, that's a gap in one of the other
sections, not a job Reference should grow into filling.
**Contents:**
- `api/` — generated API reference (from XML doc comments —
  `documentation.md`'s existing requirement that every public member has
  one is exactly what makes this possible).
- `diagnostics.md` — every `CMP` code, one entry each: message, cause, fix.
  `Troubleshooting/common-errors.md` links here for the full detail on a
  specific code.
- `glossary.md` — every term Concepts/Architecture introduce, one-line
  definitions, cross-linked back to the page that actually teaches it.
**Relates to:** reachable from anywhere; assumes nothing on its own.

## 12. Roadmap

**Audience:** anyone asking "is X available, experimental, or planned?"
**Status:** skeleton exists for `index.md`/`proposed-adrs.md`/
`future-packages.md` (placeholder only). `docs/roadmap/post-mvp.md` is
real content, produced by
[PLAN-0007](plans/0007-milestone-7-dogfooding.md) Phase 3 — currently
stating a zero-roadmap-candidate outcome and pointing to
`docs/research/0001-autofixture-comparison.md` for the full per-finding
reasoning, since Milestone 7's dogfooding pass didn't surface any
candidates itself.
**Purpose:** the single, indexed home for everything not fully available
today — never a prerequisite for anything in sections 1-11.
**Contents:**
- `index.md` — the today/experimental/planned framing itself, and a map of
  the other three pages.
- `post-mvp.md` — evidence-backed roadmap candidates *only*, each tracing
  to a migration-guide entry, a research finding, and a `Proposed` ADR
  (ADR-0029's own required shape); non-candidate findings stay in the
  research record and their governing ADR's Amendments, never listed
  here — currently empty of actual candidates, itself documented as a
  real finding rather than left ambiguous.
- `proposed-adrs.md` — a status-filtered view of `docs/adr/README.md`:
  every ADR that's `Proposed`, or `Accepted` but not yet implemented,
  answering "is X planned" directly.
- `future-packages.md` — placeholder entries for roadmapped-but-unshipped
  integration packages (name, intended purpose, status); an entry graduates
  to its own Package Guides page the moment it ships, never staying in both
  places at once — the same package-count-agnostic principle Package
  Guides states explicitly.
**Relates to:** cross-linked *from* Concepts/Package Guides/Architecture
pages that have a relevant proposed enhancement, never the reverse — see
ADR-0030's Decision Outcome for why the link direction matters.

## Suggested reading order (new user, start to finish)

1. Getting Started (all pages, in order — or jump straight to a Learning
   Path if one matches)
2. Concepts (all pages — the vocabulary the rest of the site assumes)
3. How-to Guides (whichever pages match the reader's actual task)
4. Cookbook (as narrower needs come up — not necessarily read start to end)
5. Samples (once the reader wants to see concepts working together)
6. Migrating from AutoFixture (only if the reader is coming from AutoFixture)
7. Package Guides (once the reader knows which packages they need)
8. Best Practices (once the reader is comfortable and wants "the
   recommended way")
9. Architecture (once the reader wants the "why," not just the "how")

Troubleshooting, Reference, and Roadmap are consulted as needed, not read
in sequence — surfaced via search and cross-links from the sections above,
not by requiring a reader to have gone through 1-9 first.

## Open Items

All six items originally tracked here are now resolved by Milestone 8's
deep-design pass — kept below (struck through, not deleted) as the record
of what was open and where each resolution lives, per this repo's
"docs/*.md describes current intent, link to the ADR that shaped it"
convention:

- ~~**API reference generation toolchain**~~ — resolved by
  [ADR-0032](adr/0032-api-reference-documentation-toolchain.md): a
  Markdown generator (tool picked via a time-boxed bake-off,
  `DefaultDocumentation` the leading candidate) producing pages inside
  the existing MkDocs Material site, not a separate DocFX site.
- ~~**Cookbook navigation/tagging at scale**~~ — resolved by
  [ADR-0030 Amendment 2](adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items):
  deferred, not decided now — launch flat + search-only, revisit at
  ~20-25 recipes or real search-insufficiency evidence.
- ~~**Where Sample applications physically live**~~ — resolved by
  [ADR-0033](adr/0033-public-preview-samples-strategy.md): top-level
  `samples/` directory, in-solution CI build, project references for
  development plus packed-package verification for acceptance.
- ~~**Versioning policy**~~ — resolved by
  [ADR-0031](adr/0031-public-preview-release-and-versioning-policy.md):
  lockstep versioning across all five packages, `0.x` compatibility
  policy, package-readiness checklist.
- ~~**Issue templates**~~ — resolved by
  [ADR-0030 Amendment 2](adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items):
  minimal practical contributor set (`contributing.md`, `SECURITY.md`,
  `CODE_OF_CONDUCT.md`, bug-report/feature-request issue templates, one
  PR template) — no governance ceremony beyond that.
- ~~**`docs/public-api.md`/`docs/manifesto.md`'s eventual disposition**~~ —
  resolved by
  [ADR-0030 Amendment 2](adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items):
  retired, content redistributed, generalized into a standing "every
  concept has exactly one canonical home" documentation principle.
