# [ADR-0033] Public Preview Samples Strategy

**Status:** Accepted

**Date:** 2026-08-04

**Decision Makers:** Nick Cipollina, Claude (design review)

## Context

[ADR-0030](0030-compono-documentation-architecture.md)'s Amendment 1 made
Samples its own top-level documentation area, distinct from Cookbook, and
`docs/documentation-architecture.md`'s tree names seven candidate sample
applications (Basic Usage, ASP.NET API, CQRS, Clean Architecture, Minimal
APIs, MediatR, EF Core) as the long-term set — but explicitly left "where
Sample applications physically live" and their build/CI story as an Open
Item for Milestone 8, and separately warned against treating that
candidate list as a mandate ("this architecture doesn't decide the exact
build/CI story... a question for whoever executes this section"). This
ADR makes both decisions: which samples ship in the first public preview,
and how they're built/verified.

## Decision Drivers

- Samples exist to prove Compono's ecosystem works in realistic use, not
  to showcase application-architecture patterns for their own sake — a
  sample whose code is mostly CQRS/Clean-Architecture/MediatR boilerplate
  teaches a reader about that pattern, not about Compono.
- The MVP's own success criteria
  ([`docs/mvp.md`](../mvp.md)) are the actual bar a launch sample set
  needs to prove: composing object graphs without runtime reflection, an
  xUnit v3 theory with composed parameters, a shared test double injected
  into a system under test, Bogus providing deterministic semantic
  values, a reproducible seed on failure.
- A sample is real, buildable, CI-maintained code — every sample added
  multiplies ongoing maintenance burden (keeping it compiling against
  every future Compono change) for a single-maintainer project during a
  preview whose priority is documentation and package readiness, not
  sample-app breadth.
- Samples must be verified against what a real external consumer will
  actually install — a sample that only ever builds via in-repo
  `ProjectReference`s could silently diverge from what the published
  `.nupkg`s actually provide.

## Considered Options

### Launch set size

1. **Two samples: Basic Usage + ASP.NET API.**
2. **One sample: Basic Usage only.**
3. **All seven candidates from `documentation-architecture.md`'s tree.**

### Build/verification story

1. **Project references only**, matching the rest of the in-repo test
   suites.
2. **Project references during development, packed-package verification
   for acceptance** — samples build against `ProjectReference`s day to
   day (fast inner loop, matches how every other in-repo test project
   works), but are additionally verified once against the real packed
   `.nupkg`s (the same local-feed pattern
   `test/Compono.XunitV3.SampleTests` already established across
   PLAN-0004/0005/0006) as part of the public-preview acceptance pass.
3. **Packed packages only, no project references**, mirroring
   `cosmere-tracker`'s external-consumer pattern exactly.

## Decision Outcome

**Two samples for the first preview (Option 1 for launch set size).**
One sample (Option 2) doesn't demonstrate the full ecosystem — Compono
alone, without `Compono.NSubstitute`/`Compono.Bogus` working together in
a realistic multi-layer application, understates exactly the "coherent
test composition experience across Core/xUnit v3/NSubstitute/Bogus"
`docs/mvp.md`'s own Objective states. All seven (Option 3) was rejected
per the Decision Drivers above: five of the seven candidates would be
predominantly architecture-pattern scaffolding (CQRS, Clean Architecture,
Minimal APIs, MediatR, EF Core) with Compono usage as a minority of each
file, multiplying CI/maintenance surface five-fold for marginal
additional proof of Compono itself. The remaining five stay recorded as
future candidates in `docs/documentation-architecture.md`'s tree, added
only once they'd demonstrate a materially different Compono pattern the
two launch samples don't already cover — not merely a different host
framework.

**Basic Usage** — a small project demonstrating `Composer.Create()`,
`Create<T>()`, `CreateMany<T>()`, a reusable profile, registrations and
member rules, a simple `[Compose<TProfile>]` xUnit theory, and
deterministic seed reproduction. Deliberately minimal — it exists to
support Getting Started and to be the single clearest reference
implementation of ordinary Compono usage, not to demonstrate breadth.

**ASP.NET API** — a realistic but tightly scoped API application
demonstrating all four packages together: `Compono`, `Compono.XunitV3`,
`Compono.NSubstitute`, `Compono.Bogus`, reusable test profiles, a
`[Shared]` substitute injected into the system under test, realistic
deterministic request/domain data, inline plus composed theory values,
explicit substitute setup, one integration-style service/endpoint test,
and failure reproduction through a seed. Uses only enough ASP.NET
structure to host the scenario — not an architecture showcase; Compono
usage stays the dominant content of the sample.

**Project references for development, packed-package verification for
acceptance (Option 2 for build/verification).** Pure project references
(Option 1) risk exactly the divergence-from-published-artifacts problem
`docs/mvp.md`'s own "verify the packed artifacts themselves, not rely
only on project-reference tests" instruction (carried into this
milestone's brief) warns against. Packed-packages-only (Option 3) would
slow the sample's own inner development loop for no benefit during
authoring — `cosmere-tracker`'s external-consumer constraint (no sibling
`compono` checkout in its own CI) doesn't apply here, since samples live
in this same repository. The chosen hybrid gets both: fast iteration
during authoring, and the same "prove it against what actually ships"
guarantee `test/Compono.XunitV3.SampleTests` already established as
this repo's own precedent (PLAN-0004 Phase 3/PLAN-0005 Phase 2/PLAN-0006
Phase 2's "real packaged run" verification, and ADR-0031's local-feed
packed-consumer checklist item) — reused here, not reinvented.

### Physical location and CI participation

Both samples live under a top-level `samples/` directory (sibling to
`src/`/`test/`), each a real, independently buildable project included in
the main solution so it builds (and, where it contains tests, runs) on
every CI push exactly like any other project — no separate sample-only
pipeline. `docs/samples/*.md` (the documentation-facing pages ADR-0030
already scoped) are short overviews linking out to the real project —
documentation *about* the sample, not the sample's code, per ADR-0030's
own "Samples" section. The exact project names/paths and each sample's
concrete task list are execution detail, tracked in
[PLAN-0008](../plans/0008-milestone-8-public-preview.md) rather than
fixed here.

## Positive Consequences

- The launch set directly proves the MVP's own success criteria instead
  of a broader, less-targeted claim of "ecosystem coverage."
- Two real, CI-maintained projects is a sustainable ongoing burden for a
  single-maintainer preview; the remaining five candidates stay available
  to add later from real evidence of need, per ADR-0030's package/
  sample-count-agnostic design philosophy.
- Packed-package verification closes the same "tested against project
  references only" gap this milestone's brief explicitly calls out for
  package readiness generally.

## Negative Consequences

- Five documented candidate samples (CQRS, Clean Architecture, Minimal
  APIs, MediatR, EF Core) stay unbuilt at launch — a reader evaluating
  Compono against one of those specific architectural patterns has no
  sample to look at yet. Accepted: `docs/roadmap/future-packages.md`-style
  framing (a stated future candidate, not silently dropped) keeps this
  discoverable without inflating the first preview's scope.
- The packed-package verification step is one more CI job to maintain
  (packing the four publishable packages to a local feed — `Compono.Generators`
  comes along embedded inside `Compono`'s own package, not as a separate
  restore — and restoring samples against it)
  on top of the project-reference build. Accepted: this repo already
  pays this cost for `Compono.XunitV3.SampleTests`; extending it to the
  two new samples is marginal, not new infrastructure.

## Pros and Cons of the Options

### Two samples: Basic Usage + ASP.NET API (chosen)

- Good, because it directly covers the MVP's own success criteria.
- Good, because it's a sustainable CI/maintenance footprint.
- Bad, because five documented future candidates go unbuilt at launch.

### One sample: Basic Usage only

- Good, because it's the smallest possible footprint.
- Bad, because it never demonstrates NSubstitute/Bogus working together
  in a realistic multi-layer application.

### All seven candidates

- Good, because it maximizes architectural-pattern coverage.
- Bad, because most of each app's code would be pattern-specific
  scaffolding, not Compono usage, for a five-fold CI/maintenance cost.

### Project references only

- Good, because it's the simplest, fastest inner loop.
- Bad, because it never verifies the samples against what a consumer
  actually installs — exactly the gap this milestone's brief warns
  against.

### Project references for dev, packed verification for acceptance (chosen)

- Good, because it gets fast iteration and real-artifact verification.
- Good, because it reuses this repo's own established local-feed pattern.
- Bad, because it's one more CI job (packing + local-feed restore) to
  maintain.

### Packed packages only

- Good, because it matches an external consumer's actual experience most
  closely.
- Bad, because it slows the sample's own development loop for no benefit
  — samples live in this repo, unlike `cosmere-tracker`.

## Links

- [ADR-0030](0030-compono-documentation-architecture.md) — Amendment 1's
  Samples-as-its-own-area decision this ADR resolves the remaining Open
  Item for
- `docs/mvp.md` — the MVP success criteria the launch set is scoped to
  prove
- PLAN-0004/PLAN-0005/PLAN-0006 — the existing local-feed
  packed-consumer-verification precedent this ADR's build story reuses
- [ADR-0031](0031-public-preview-release-and-versioning-policy.md) —
  the package-readiness checklist's local-feed verification item, the
  same mechanism applied here
- [PLAN-0008](../plans/0008-milestone-8-public-preview.md) — Phase 4
  builds both samples
