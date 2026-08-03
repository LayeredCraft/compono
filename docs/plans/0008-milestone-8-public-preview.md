# [PLAN-0008] Milestone 8: Public Preview

**Status:** Not Started — **draft backlog, not yet phased.** A PR review
correctly flagged that this plan's single, undifferentiated Tasks section
(58+ pages, sample apps, API-reference tooling, repository-process work,
and publishing, all under one status) doesn't give Milestone 8 independently
shippable phases the way `design-decisions.md`'s "Writing a Plan" section
expects — real phase boundaries with their own checklists and statuses,
"each phase ships as its own PR." Splitting this backlog into ordered
phases is itself a real design decision (how coarse, what belongs together,
what blocks what) deserving its own deep dive
(`tasks/design.md`) before Milestone 8 starts executing against it — not a
quick restructuring inside this PR, which is Milestone 7's own scope. This
plan stays in its current flat, unphased shape as a scoped *backlog*
(satisfying [ADR-0029 Amendment 4](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-4-2026-08-03-documentation-architecture-becomes-a-required-milestone-7-deliverable)'s
requirement that Milestone 8 get a scoped list instead of a blank page) —
its own phase design happens before Milestone 8's plan moves to
`In Progress`, per `docs/plans/README.md`'s "every ADR/design a plan
implements must already be settled" rule.

**Implements:** [ADR-0030](../adr/0030-compono-documentation-architecture.md)
(documentation architecture, including Amendment 1) — this plan is the
scoped, page-by-page backlog [PLAN-0007](0007-milestone-7-dogfooding.md)
Phase 5 produced per
[ADR-0029 Amendment 4](../adr/0029-milestone-7-dogfooding-strategy-and-capability-gap-decision-framework.md#amendment-4-2026-08-03-documentation-architecture-becomes-a-required-milestone-7-deliverable),
so Milestone 8 starts executing rather than re-deriving scope.

## Goal

Every page in `docs/documentation-architecture.md`'s tree has real,
published content (not a Phase 5 stub); `docs/migrating-from-autofixture.md`
is polished to publication-ready; every Open Item that document lists is
resolved; `0.x` packages are published; and `mkdocs.yml`'s nav reflects the
final, real hierarchy with the Phase 5 "legacy" entries retired or folded
in per the Open Items' resolution.

## Scope

Per `docs/mvp.md`'s Milestone 8 section and
[ADR-0030](../adr/0030-compono-documentation-architecture.md) (including
Amendment 1):

- Write every page listed in the Tasks section below — replacing each
  Phase 5 stub (`> **Status:** Skeleton...`) with real content meeting
  `documentation.md`'s (engineering-workflow reference) quality bar: what
  problem it solves, why/when to use it, when not to, a minimal example, a
  realistic example, common mistakes, and links to related concepts/
  Cookbook/reference — per "Every page leads somewhere."
- Polish `docs/migrating-from-autofixture.md` from "substantially
  complete" to publication-ready (content, not structure — Phase 5 already
  promoted it to its top-level path).
- Resolve every Open Item in `docs/documentation-architecture.md`: API
  reference generation toolchain, Cookbook navigation/tagging at scale,
  where Sample applications physically live, versioning policy,
  contribution guidance, issue templates, `docs/public-api.md`/
  `docs/manifesto.md`'s eventual disposition.
- Publish `0.x` packages, benchmark results, explicit known limitations.
- Retire `mkdocs.yml`'s "(legacy)" nav entries once their content's
  Open-Item disposition is resolved (see Tasks).

Explicitly deferred: any change to the documentation *architecture* itself
(hierarchy, section purposes, audiences) — that's ADR-0030's decision, not
this plan's; if Milestone 8 discovers real friction with the architecture
while writing against it, that's a new design pass (`tasks/design.md`,
likely an Amendment to ADR-0030), not a silent deviation absorbed here.

## Tasks

Grouped by `docs/documentation-architecture.md` section, in its own
reading order. Each leaf item replaces that page's Phase 5 stub.

### Home

- [ ] `docs/index.md` (site Home, not a Phase 5 stub — a pre-existing real
      page) — review and correct its example, which currently calls a
      nonexistent `Compono.Create(builder => ...)`; the real API is
      `Composer.Create(builder => ...)` (`src/Compono/Composer.cs`)

### Getting Started

- [ ] `getting-started/index.md`
- [ ] `getting-started/installation.md`
- [ ] `getting-started/first-test.md`
- [ ] `getting-started/learning-paths.md`
- [ ] `getting-started/next-steps.md`

### Concepts

- [ ] `concepts/index.md`
- [ ] `concepts/composition-model.md`
- [ ] `concepts/profiles.md`
- [ ] `concepts/registrations-and-rules.md`
- [ ] `concepts/shared-values.md`
- [ ] `concepts/providers.md`
- [ ] `concepts/determinism-and-seeding.md`
- [ ] `concepts/collections.md`

### How-to Guides

- [ ] `how-to/index.md`
- [ ] `how-to/create-an-object.md`
- [ ] `how-to/write-a-composed-theory.md`
- [ ] `how-to/customize-a-member.md`
- [ ] `how-to/register-a-type.md`
- [ ] `how-to/use-profiles.md`
- [ ] `how-to/share-a-value-across-a-test.md`

### Cookbook

- [ ] `cookbook/index.md`
- [ ] Resolve the "Cookbook navigation/tagging at scale" Open Item before
      (or alongside) writing the first real batch of recipe pages —
      deciding subcategorization now avoids reorganizing 50+ pages later.
- [ ] First batch of recipe pages (not exhaustive — grows over time per
      ADR-0030 Amendment 1): generate a realistic email, freeze a shared
      `HttpMessageHandler`, override one field only for one test, seed a
      specific failing case for reproduction, compose a substitute with
      one method stubbed.

### Samples

- [ ] Resolve "Where Sample applications physically live" Open Item
      (build/CI story for a top-level `samples/` directory) before writing
      real sample apps.
- [ ] `samples/index.md`
- [ ] `samples/basic-usage.md` (+ the runnable project it describes)
- [ ] `samples/aspnet-api.md` (+ project)
- [ ] `samples/cqrs.md` (+ project)
- [ ] `samples/clean-architecture.md` (+ project)
- [ ] `samples/minimal-apis.md` (+ project)
- [ ] `samples/mediatr.md` (+ project)
- [ ] `samples/ef-core.md` (+ project)

### Migrating from AutoFixture

- [ ] Polish `docs/migrating-from-autofixture.md` to publication-ready
      (content refinement only — already promoted to this path, already
      substantially complete per Milestone 7).
- [ ] Add its real `mkdocs.yml` nav entry content review (nav entry itself
      already exists from Phase 5's skeleton).

### Package Guides

- [ ] `packages/index.md` (the ecosystem map table)
- [ ] `packages/compono.md`
- [ ] `packages/compono-xunitv3.md`
- [ ] `packages/compono-nsubstitute.md`
- [ ] `packages/compono-bogus.md`

### Best Practices

- [ ] `best-practices/index.md`
- [ ] `best-practices/organizing-profiles.md`
- [ ] `best-practices/large-test-suites.md`
- [ ] `best-practices/naming-conventions.md`
- [ ] `best-practices/reusing-configuration.md`
- [ ] `best-practices/performance-recommendations.md`
- [ ] `best-practices/deterministic-and-non-brittle-tests.md`

### Architecture

- [ ] `architecture/index.md`
- [ ] `architecture/design-principles.md` — also resolve the
      `docs/public-api.md`/`docs/manifesto.md` disposition Open Item here
      (redistribute their content into this page, or keep them as internal
      cross-references — decide and record which).
- [ ] `architecture/current/source-generation.md`
- [ ] `architecture/current/generated-plans-and-discovery.md`
- [ ] `architecture/current/provider-pipeline.md`
- [ ] `architecture/current/deterministic-seeding.md`
- [ ] `architecture/current/performance.md` (move `docs/performance.md`'s
      real content here, then retire the legacy nav entry)
- [ ] `architecture/decision-log.md` (the public-facing index into
      `docs/adr/`)
- [ ] Retire `mkdocs.yml`'s "(legacy)" `architecture.md`/
      `design-principles.md`/`public-api.md`/`performance.md` nav entries
      once their content has a real home above.

### Troubleshooting

- [ ] `troubleshooting/index.md`
- [ ] `troubleshooting/common-errors.md` (start from the real `CMP0001`
      finding Milestone 7's dogfooding surfaced)
- [ ] `troubleshooting/faq.md` (start from the real gap-3 "why fail-fast
      instead of omit" finding)

### Reference

- [ ] Resolve the "API reference generation toolchain" Open Item (its own
      light-dive ADR) before generating `reference/api/`.
- [ ] `reference/api/` (generated)
- [ ] `reference/index.md`
- [ ] `reference/diagnostics.md` (every `CMP` code)
- [ ] `reference/glossary.md`

### Roadmap

- [ ] `roadmap/index.md`
- [ ] `roadmap/post-mvp.md` — only if [PLAN-0007](0007-milestone-7-dogfooding.md)
      Phase 3 hasn't produced it by the time Milestone 8 reaches this task;
      otherwise this is already done, just needs its `mkdocs.yml` nav entry.
- [ ] `roadmap/proposed-adrs.md`
- [ ] `roadmap/future-packages.md`

### Repository-process content

- [ ] Repository-root `README.md` — review and update (distinct from
      `docs/getting-started/*`; part of the original Milestone 8 scope,
      not part of the `docs/` hierarchy this backlog otherwise tracks)
- [ ] `contributing.md`
- [ ] Versioning policy (page location decided alongside `contributing.md`)
- [ ] Issue templates
- [ ] Benchmark results
- [ ] Explicit known limitations

### Publishing

- [ ] Publish `0.x` packages
- [ ] Final `mkdocs.yml` nav pass — every "(legacy)" entry retired or
      resolved, nav matches `docs/documentation-architecture.md` exactly

## Critical Files

- Every path listed in Tasks above, under `docs/` — Phase 5 already
  created each as a stub; this plan replaces stub content with real
  content, not new files, **except**: `docs/index.md` and repository-root
  `README.md` (both pre-existing real pages this plan corrects/updates, not
  Phase 5 stubs); `samples/*`'s actual runnable projects;
  `contributing.md`/versioning/issue-template content; `reference/api/`,
  which doesn't exist at all yet and depends on the API-reference
  generation toolchain Open Item; and `roadmap/post-mvp.md`, which also
  doesn't exist yet and is conditional on
  [PLAN-0007](0007-milestone-7-dogfooding.md) Phase 3 (`Not Started`) —
  treat both as genuinely new artifacts to create, not stubs to replace.
- `mkdocs.yml` — nav's "(legacy)" entries retired as their content's
  disposition resolves.
- `docs/documentation-architecture.md` — its Open Items section shrinks as
  each item is resolved; update it in place as that happens (it's a living
  reference, not an ADR).

## Test Plan

Documentation content has no automated test suite; Sample applications
(`samples/*`) get real, buildable projects and, where practical, their own
tests demonstrating the pattern they showcase — matching `testing.md`'s
bar for any real code this plan produces. `reference/api/`'s generation
step (once its toolchain is chosen) should fail the build if a public
member is missing its required XML doc comment, per `documentation.md`'s
existing hard requirement.

## Notes

Anything discovered while actually writing against
`docs/documentation-architecture.md`'s blueprint that suggests the
architecture itself needs to change (not just this plan's task list)
gets recorded here, then routed to `tasks/design.md` for an ADR-0030
Amendment — a plan being wrong about *how*/*what pages* doesn't require
touching ADR-0030, but a real architectural gap discovered through use
does, per this project's own stated intent to evolve the architecture from
real experience rather than further upfront prediction.
