# [PLAN-0008] Milestone 8: Public Preview

**Status:** In Progress

**Implements:** [ADR-0030](../adr/0030-compono-documentation-architecture.md)
(including Amendments 1-2), [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)
(release/versioning policy, package-readiness bar),
[ADR-0032](../adr/0032-api-reference-documentation-toolchain.md) (API
reference toolchain), [ADR-0033](../adr/0033-public-preview-samples-strategy.md)
(samples strategy) — a `docs/mvp.md` roadmap milestone drawing on four
`Accepted` ADRs, per `docs/plans/README.md`'s multi-ADR plan convention.

## Goal

An external developer can discover, understand, install, use
successfully, troubleshoot, evaluate, and safely contribute to Compono
using only public artifacts (nuget.org packages, the deployed docs site,
the GitHub repository) — verified by a clean-room acceptance pass
(Phase 8) that follows only public instructions and consumes only
published/local-feed packages. See "Exit criteria" below for the full,
checkable bar.

## Scope

Per `docs/mvp.md`'s Milestone 8 section and the four ADRs above. This
plan supersedes the earlier flat, unphased PLAN-0008 draft (recorded in
Notes below) — same total scope, now split into independently shippable
phases, each its own PR, per `design-decisions.md`'s "each phase ships as
its own PR" rule.

Explicitly deferred (unchanged from the original draft): any change to
the documentation *architecture* itself (ADR-0030's hierarchy, section
purposes, audiences) — friction discovered while executing this plan
routes to a new `tasks/design.md` pass (likely an ADR-0030 Amendment), not
a silent deviation absorbed here. Any post-MVP product capability
discovered while writing docs or running the acceptance test is recorded
in `docs/roadmap/post-mvp.md`, not built in this milestone — per the
milestone brief's own "don't introduce unrelated post-MVP features"
constraint. A blocking bug found along the way may be fixed in its own
scoped PR, same precedent as ADR-0029's "Bug handling."

## Phase ordering rationale

The milestone brief's suggested shape (decisions → toolchain → package
hardening → core docs → package-specific docs → samples/cookbook →
contributor readiness → release pipeline → acceptance → closeout) is
adopted with two changes, both driven by real dependencies this design
pass surfaced:

- **Release pipeline moves out of its own phase.** The user's own
  decision (this plan's design conversation) settled that the pipeline
  itself needs no redesign — only the `alpha`→`preview` identifier rename
  ([ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)).
  That change is mechanically trivial and has no dependency on anything
  else in this plan, so it's folded into Phase 0 rather than getting a
  whole phase to itself. It belongs in Phase 0, not a later checkpoint —
  every version `publish-preview.yaml` produces is a SemVer prerelease
  regardless of the identifier string (`-alpha.N` and `-preview.N` are
  both still prereleases, both still excluded from a plain `dotnet add
  package` install), so renaming it carries no risk of a still-in-progress
  milestone looking publicly "done." The actual "does this look done"
  gate is Phase 8's manual GitHub Release publish
  (`publish-release.yaml`), which this rename has no effect on either
  way — see ADR-0031's "Preview publishing identifier" Decision Outcome
  for the full mechanics.
- **Package-readiness hardening moves before documentation writing, not
  after.** Package Guides, Samples, and the acceptance test all need real,
  installable packages to write accurate content against and verify
  examples with — writing Package Guides against packages that haven't
  yet been hardened (symbols, validation, packed-consumer verification)
  risks documenting a shape that changes underneath the docs before
  launch.

## Phase 0: Package readiness hardening

**Status:** Done

**Checkpoint: Package Quality Complete** — every package meets ADR-0031's
readiness bar and can be safely built against by every later phase.

Executes [ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)'s
package-readiness bar against all five packages — four independently
published (`Compono`, `Compono.XunitV3`, `Compono.NSubstitute`,
`Compono.Bogus`), plus `Compono.Generators`, which never gets its own
`.nupkg` (`IsPackable=false`, embedded inside `Compono.nupkg`'s
`analyzers/dotnet/cs`, per [ADR-0003](../adr/0003-generator-package-distribution.md))
and is verified by inspecting that embedded content, not by packing it
independently. No documentation content depends on this phase's *outcome*
being novel — it hardens what already ships — but Phases 3/4/8 depend on
it being *done* before writing package-specific content or running the
acceptance test.

- [x] Rename `publish-preview.yaml`'s `prereleaseIdentifier` input from
      `alpha` to `preview` (ADR-0031) — `main` starts publishing
      `0.x.y-preview.N` instead of `0.x.y-alpha.N`. Still a real SemVer
      prerelease either way, so this carries no sequencing risk and
      belongs here, not at a later checkpoint.
- [x] Per [ADR-0031 Amendment 1](../adr/0031-public-preview-release-and-versioning-policy.md#amendment-1-2026-08-04-third-party-dependencies-use-tested-ranges-not-exact-pins),
      give `Directory.Packages.props`'s `PackageVersion` entries for the
      three dependencies that flow into a publishable package's own
      `.nuspec` a deliberate tested range instead of a bare (unbounded
      floor) or exact-pin version: `NSubstitute` → `[6.0.0, 7.0.0)`,
      `Bogus` → `[35.6.5, 36.0.0)`, `xunit.v3.extensibility.core` →
      `[3.2.2, 4.0.0)`. Everything else in the file (Roslyn/Scriban/
      polyfill build-time-only deps that never flow to a consumer's
      `.nuspec`, and test-only tooling versions) stays as originally
      declared — no blanket pin or range policy applies to those.
- [x] Fix the same gap for the *internal* dependency: `Compono.XunitV3`/
      `Compono.NSubstitute`/`Compono.Bogus` each reference `Compono` via a
      plain `<ProjectReference>` (`PrivateAssets="none"`), which
      `dotnet pack` converts into a bare-version (minimum-inclusive)
      dependency on `Compono`'s current version, not an exact match —
      a consumer could install e.g. `Compono.XunitV3 0.3.0` alongside a
      newer `Compono 0.5.0` and have it restore successfully, exactly the
      cross-package version mismatch ADR-0031 declares unsupported.
      **Keep `<ProjectReference>` — it stays the mechanism for local
      development** (fast inner loop, no local-feed round-trip needed to
      iterate on an integration package against core changes in the same
      repo); this task only overrides the NuGet dependency *version
      range* that `dotnet pack` writes into the `.nuspec` at pack time
      (`[$(Version)]` instead of the default bare version), it does not
      switch to `PackageReference`. Verify by inspecting the packed
      `.nuspec`'s `<dependencies>` entry in the package-contents-inspection
      CI job below, not just its file listing.
- [x] Add `PackageTags` and `PackageReleaseNotes` to `Directory.Build.props`,
      not per-project — one shared, uniform value for all five packages
      (`PackageTags`: `testing;test-data;source-generator;dotnet`;
      `PackageReleaseNotes`: `$(PackageProjectUrl)/releases`, the repo's
      stable releases index rather than a specific version tag — a
      per-version `.../releases/tag/v$(Version)` link would 404 for every
      preview build `publish-preview.yaml` pushes on a plain `main` push,
      since no GitHub Release or tag exists for those, only for versions
      published through `publish-release.yaml`). Matches how
      `PackageLicenseExpression`/`RepositoryUrl`/`PackageIcon`/
      `PackageReadmeFile` are already centralized — the five packages
      distribute as one coherent set, so their discovery metadata comes
      from one place, not five copies that can drift. No per-package tag
      differentiation (e.g. `xunit`/`nsubstitute`/`bogus`) — the package
      name itself already carries that distinction.
- [x] Add a `<Title>` to each of the four publishable packages'
      `.csproj` files (none currently set one — `Directory.Build.props`
      only centralizes what's genuinely uniform; a human-friendly title
      is per-package, same as `Description` already is). A short,
      human-friendly name distinct from the raw package ID (e.g. "Compono
      — Core Composition Engine," not just "Compono") — ADR-0031's
      discovery-metadata bar names title alongside tags/description, and
      nothing before this task actually added one. Verify via the
      package-contents-inspection CI job's manifest check, not just file
      listing.
- [x] Add `Microsoft.DotNet.PackageValidation`
      (`EnablePackageValidation=true`) to `Directory.Build.props`'s
      packable `PropertyGroup`, with no static
      `PackageValidationBaselineVersion` value. **Do not try to inject
      this into `publish-preview.yaml`/`publish-release.yaml`** — both
      are a single job each calling `uses:
      LayeredCraft/devops-templates/.github/workflows/publish-*.yml`,
      with no step-level hook inside a reusable-workflow job to insert a
      "compute baseline, then pack" sequence into, and extending the
      shared `devops-templates` workflow with a new input is a
      cross-repo change out of scope here. Instead, fold this into the
      **locally-controlled pack/contents-inspection CI job** below (this
      repo fully controls it): query nuget.org for each package's
      currently-latest published version, then run
      `dotnet pack -p:PackageValidationBaselineVersion=<prior-version>`
      for each of the four publishable packages, as a **pre-merge PR
      gate** — catching an accidental break before it reaches `main` is
      strictly better than catching it after either publish workflow has
      already run. **Skip this gate on any PR carrying the
      `breaking-change` label** — that label already means the break is
      deliberate and permitted by ADR-0031's own `0.X+1.0` policy, so
      failing the same PR on the incompatibility the label declares would
      be a self-contradiction. The very first-ever publish has nothing to
      query yet, so validation is inert for exactly that one case.
      **This new job's own trigger must include `labeled`/`unlabeled`
      PR activity types, not just `pr-build.yaml`'s default
      `opened`/`synchronize`/`reopened`** — `release-drafter.yaml` applies
      the `breaking-change` autolabel in its own, separately-triggered
      workflow run, so a gate that only reads labels at the PR's initial
      push can run before the label exists (blocking a real, legitimate
      break) or stay green after the label is later removed (silently
      missing an unjustified break on the same commit). Read the current
      label state at the gate's own run time, never a cached value from
      an earlier trigger.
- [x] Reconfigure `.github/release-drafter.yml`'s `version-resolver` so
      the `breaking-change` label maps to `minor`, not the file's current
      `major` — as configured today, a labeled breaking-change PR
      resolves the next version as `1.0.0`, silently exiting the `0.x`
      preview line the first time anyone uses the label, rather than the
      deliberate `0.X+1.0` minor bump ADR-0031's compatibility policy
      requires. Leave `breaking-change` in `categories` unchanged — only
      its `version-resolver` bucket moves.
- [x] Add a new, locally-controlled CI job (a real job in this repo's own
      workflow, distinct from `publish-preview.yaml`/`publish-release.yaml`'s
      opaque `uses:` calls) that packs the four publishable packages and:
      asserts each `.nupkg`'s file listing matches the expected shape per
      TFM (lib, README, icon, no stray build artifacts;
      `analyzers/dotnet/cs` for `Compono` specifically, containing
      `Compono.Generators.dll` — this is also where `Compono.Generators`
      itself gets verified, by content inspection rather than an
      independent pack); runs the API-compatibility baseline check from
      the task above (`-p:PackageValidationBaselineVersion=<prior-version>`,
      skipped on `breaking-change`-labeled PRs); and runs
      `dotnet build -p:WarningsAsErrors=CS1591` for the four publishable
      packages so a missing public-member doc comment actually fails CI
      — `Directory.Build.props`' existing `GenerateDocumentationFile`
      setting deliberately leaves `CS1591` a warning for ordinary builds,
      which alone never fails `dotnet build`. All three checks run as a
      **pre-merge PR gate**, on the same trigger (see the trigger note
      above — must include `labeled`/`unlabeled`, not just
      `pr-build.yaml`'s default activity types).
- [x] Extend the local-feed packed-consumer pattern (already used by
      `test/Compono.XunitV3.SampleTests`) to restore and smoke-test the
      four publishable packages together from one local feed, as a
      standing CI gate — not ad hoc per milestone. Reuses
      `Compono.XunitV3.SampleTests`' own `PackToLocalFeed` restore
      directly (`--filter-not-class` on the one deliberately-failing
      class, MTP's actual filter syntax — `dotnet test`'s VSTest-style
      `--filter` produced zero matched tests against an MTP host and was
      corrected during verification) rather than inventing a second
      project; the new `package-validation.yaml` CI job runs it as a
      pre-merge gate. Verified locally: 16/16 tests pass against packages
      restored from `.local-nuget-feed`.
- [x] Verify (not redesign) `PrivateAssets`/analyzer-transitivity holds
      for every package, not just `Compono`/`Compono.Generators`. Proven
      by the local-feed smoke test above, not just static inspection: all
      16 passing tests compose real generated plans reached only through
      `Compono.XunitV3`/`Compono.NSubstitute`/`Compono.Bogus`'s
      `PackageReference`s (never a `ProjectReference` to `Compono` or
      `Compono.Generators` in `Compono.XunitV3.SampleTests`), so
      `Compono.Generators`' analyzer packaging demonstrably flows
      transitively through every integration package's own
      `PrivateAssets="none"` reference to `Compono`.
- [x] Spot-check of `Directory.Packages.props`'s current dependency
      licenses — self-contained to this phase, not dependent on
      `contributing.md` (which doesn't exist until Phase 6; see Phase 6's
      own Tasks for the standing review-habit note this spot-check feeds
      into). Findings: every dependency in the file is MIT, BSD-2/3-Clause,
      or Apache-2.0 (xUnit v3 family, NSubstitute, AwesomeAssertions,
      Bogus, Scriban, Meziantou.Polyfill, Microsoft.CodeAnalysis.*,
      Microsoft.SourceLink.GitHub, BenchmarkDotNet, AutoFixture,
      Basic.Reference.Assemblies, Verify.*) — no copyleft (GPL/AGPL/LGPL)
      dependency, nothing incompatible with Compono's own MIT license.

## Phase 1: API reference toolchain evaluation and wiring

**Status:** Done

Executes [ADR-0032](../adr/0032-api-reference-documentation-toolchain.md).
Depends on Phase 0 only loosely (needs real packages to generate against,
but can run against `main`'s current build) — sequenced early because
`reference/api/` is a dependency for later cross-links (Concepts,
Package Guides, Cookbook) that are expected to point into it.

- [x] Time-boxed bake-off: `DefaultDocumentation` vs. `xmldocmd` (plus any
      other maintained candidate surfaced) against a representative slice
      of Compono's real public API, scored against ADR-0032's evaluation
      criteria (generics, overloads, inheritance, extension methods,
      attributes, nullable signatures, `<see>`/cross-package refs, doc-tag
      coverage, stable filenames/anchors, MkDocs Material readability,
      deterministic output, maintenance/TFM compatibility). Record the
      result in this plan's Notes.
- [x] Wire the winning tool into CI: generates `docs/reference/api/`
      Markdown from **the four publishable packages'** (`Compono`,
      `Compono.XunitV3`, `Compono.NSubstitute`, `Compono.Bogus`) compiled
      DLL + XML doc file — not `Compono.Generators`, which
      `IsPackable=false` and is an internal analyzer implementation
      embedded in `Compono.nupkg`, not a consumer-referenceable library;
      generating a `reference/api/Compono.Generators` section for it
      would be empty or misleading. `Compono.Generators` is verified as
      package content (Phase 0's `.nuspec` inspection), not documented as
      public API here.
- [x] Add the drift-detection CI gate (regeneration produces no
      uncommitted diff) and the missing-XML-doc-comment gate where the
      tool supports it.
- [x] `reference/index.md` states the "supplements, never replaces"
      philosophy (already drafted per ADR-0030 Amendment 1's framing).

## Phase 2: Core documentation and README

**Status:** Not Started

**Checkpoint: Documentation Foundation Complete** — a newcomer has a
complete, real (non-stub) linear path from zero knowledge to productive
use; every later section can safely link back into this one.

The primary learning path a newcomer needs before anything
package-specific or task-specific makes sense — sequenced before Phases
3-5 since How-to Guides/Package Guides/Cookbook/Best Practices all assume
Concepts, and Samples/Migration Guide pages link back into Getting
Started.

- [ ] `docs/index.md` — fix the `Compono.Create(builder => ...)` example
      to the real `Composer.Create(builder => ...)` API
      (`src/Compono/Composer.cs`).
- [ ] Repository-root `README.md` — review and update (distinct artifact
      from `docs/getting-started/*`, per `docs/mvp.md`'s Milestone 8
      scope). Apply ADR-0030 Amendment 2's benchmark-claims policy: no
      comparative AutoFixture performance claims.
- [ ] `getting-started/index.md`, `installation.md`, `first-test.md`,
      `learning-paths.md`, `next-steps.md`.
- [ ] `concepts/index.md`, `composition-model.md`, `profiles.md`,
      `registrations-and-rules.md`, `shared-values.md`, `providers.md`,
      `determinism-and-seeding.md`, `collections.md`.
- [ ] `how-to/index.md`, `create-an-object.md`,
      `write-a-composed-theory.md`, `customize-a-member.md`,
      `register-a-type.md`, `use-profiles.md`,
      `share-a-value-across-a-test.md`.

## Phase 3: Package guides, migration guide, troubleshooting, reference

**Status:** Not Started

Content with the most existing raw material to draw from (Milestone 7's
research/migration-guide evidence, real diagnostics already shipped) —
sequenced after Phase 2 (assumes Concepts) and Phase 0 (packages must be
final-shaped to describe accurately) and Phase 1 (`reference/diagnostics.md`/
`glossary.md` complete the Reference section Phase 1 started).

- [ ] `packages/index.md` (ecosystem map table), `packages/compono.md`,
      `packages/compono-xunitv3.md`, `packages/compono-nsubstitute.md`,
      `packages/compono-bogus.md`.
- [ ] Polish `docs/migrating-from-autofixture.md` to publication-ready
      (content refinement only, per ADR-0030's "content-stable" framing —
      no structural change).
- [ ] `troubleshooting/index.md`, `troubleshooting/common-errors.md`
      (start from the real `CMP0001` finding), `troubleshooting/faq.md`
      (start from the real gap-3 "why fail-fast" finding).
- [ ] `reference/diagnostics.md` (every `CMP` code), `reference/glossary.md`.
- [ ] Explicit known-limitations content, surfaced from Getting Started,
      Package Guides, Troubleshooting, and release notes (not one
      obscure page) — sourced from `docs/research/0001-autofixture-comparison.md`'s
      recorded findings (`CMP0001`, fail-fast recursion vs. omission, the
      Compose-family stacking constraint, `Compono.Bogus`'s exact
      member-name-matching limits) plus `docs/mvp.md`'s Non-goals list.

## Phase 4: Samples, cookbook, best practices

**Status:** Not Started

**Checkpoint: Public Documentation Feature Complete** — every
`docs/documentation-architecture.md` section that depends on real,
runnable code (Samples, Cookbook) now has it; only architecture
consolidation, contributor readiness, and final hardening remain.

Executes [ADR-0033](../adr/0033-public-preview-samples-strategy.md) for
samples. Sequenced after Phase 2 (Concepts/Package Guides content to link
back to) and Phase 0 (packed-package verification needs hardened
packages).

- [ ] `samples/Compono.Samples.BasicUsage/` — real project, added to
      `Compono.slnx`, `ProjectReference`s during authoring.
      `docs/samples/basic-usage.md` overview page.
- [ ] `samples/Compono.Samples.AspNetApi/` — real project, added to
      `Compono.slnx`. `docs/samples/aspnet-api.md` overview page.
- [ ] `docs/samples/index.md` overview; record the five deferred
      candidates (CQRS, Clean Architecture, Minimal APIs, MediatR, EF
      Core) as future candidates, not silently dropped.
- [ ] **Remove, not just leave unbuilt:** delete the five deferred
      samples' Phase 5 (PLAN-0007) stub pages
      (`docs/samples/{cqrs,clean-architecture,minimal-apis,mediatr,ef-core}.md`)
      and their five `mkdocs.yml` nav entries. Per ADR-0033/
      `docs/documentation-architecture.md`'s "5. Samples" section, a
      deferred sample is recorded as a future candidate in prose, not
      published as a placeholder nav entry — leaving the existing
      skeleton stubs in place would put five dead-end pages into the
      live public-preview site.
- [ ] Local-feed packed-package verification job for both samples
      (reuses Phase 0's local-feed infrastructure).
- [ ] `cookbook/index.md` (flat, alphabetical, per ADR-0030 Amendment 2's
      deferred-navigation decision) plus the first recipe batch (5-10
      pages): generate a realistic email, freeze a shared
      `HttpMessageHandler`, override one field only for one test, seed a
      specific failing case for reproduction, compose a substitute with
      one method stubbed. Each recipe captures the stable front matter
      (`title`, description, `packages`, `concepts`) ADR-0030 Amendment 2
      requires even though nothing consumes it for navigation yet.
- [ ] `best-practices/index.md`, `organizing-profiles.md`,
      `large-test-suites.md`, `naming-conventions.md`,
      `reusing-configuration.md`, `performance-recommendations.md`,
      `deterministic-and-non-brittle-tests.md`.

## Phase 5: Architecture consolidation and legacy retirement

**Status:** Not Started

Executes ADR-0030 Amendment 2's "one canonical home" principle. Sequenced
after Phase 2 (Concepts must exist for Architecture pages to cross-link
back to) — the last phase touching `docs/architecture.md`/
`docs/performance.md`/`docs/design-principles.md`/`docs/manifesto.md`/
`docs/public-api.md`'s real pre-existing content, so it can safely
consume and then retire them.

- [ ] `architecture/index.md`, `architecture/design-principles.md`
      (absorbs `docs/design-principles.md`/`docs/manifesto.md`'s
      content).
- [ ] `architecture/current/source-generation.md`,
      `generated-plans-and-discovery.md`, `provider-pipeline.md`,
      `deterministic-seeding.md`, `performance.md` (moves
      `docs/performance.md`'s real methodology/results, publishing them
      per ADR-0030 Amendment 2's benchmark-claims policy).
- [ ] `architecture/decision-log.md` (public-facing index into
      `docs/adr/`).
- [ ] Retire **all five** pre-existing legacy pages this phase
      consolidates — `docs/public-api.md`, `docs/manifesto.md`,
      `docs/architecture.md`, `docs/design-principles.md`, and
      `docs/performance.md` — from navigation and canonical-content
      ownership once every cross-reference has a new home in
      `architecture/`/`reference/`, and delete all five `mkdocs.yml`
      "(legacy)" nav entries. **Do not delete any of the five files
      themselves.** 24+ `Accepted` ADRs link to one or more of them by
      path, and this repo's own ADR-immutability rule means none of that
      historical text can be rewritten to point elsewhere. Replace each
      file's content with a short redirect/tombstone stub ("this content
      moved to `architecture/...`, see there") instead — satisfies
      "retired, not part of the public nav, one canonical home" while
      keeping every existing ADR link resolvable, which Phase 7's
      site-wide broken-link check would otherwise fail against files this
      plan can't touch. (An earlier version of this task only tombstoned
      the first two — the same problem applies identically to the other
      three, since they're excluded from the canonical tree the same way.)
- [ ] `roadmap/index.md`, `roadmap/proposed-adrs.md`,
      `roadmap/future-packages.md` (`roadmap/post-mvp.md` already real
      content from PLAN-0007 Phase 3 — just needs its nav confirmed).

## Phase 6: Contributor and repository readiness

**Status:** Not Started

Executes ADR-0030 Amendment 2's governance-scope decision. Independent of
the documentation content phases above — could run in parallel with
Phases 2-5 in principle, sequenced here mainly for reviewability (one
focused PR, not interleaved with doc-content PRs).

- [ ] **Two files, not one.** `docs/contributing.md` — the full docs-site
      page: build/test/PR expectations, cross-linking this skill's
      public-facing equivalents, plus the license-review note Phase 0's
      dependency spot-check feeds into (any PR adding or bumping a
      dependency version — Dependabot-authored or not — gets its target
      package's license checked as part of normal review, per ADR-0031:
      Dependabot's own flow catches vulnerabilities, not licenses, so
      this is an ongoing review habit, not a one-time task that was
      already "done" after Phase 0). **Separately, a root-level
      `CONTRIBUTING.md`** — this repo's own `contributing.md` reference
      (`.claude/skills/engineering-workflow/references/contributing.md`)
      already states the requirement: "if this project opens to outside
      contributors, split this section out into a real `CONTRIBUTING.md`
      at repo root and link it from `README.md`" — exactly what this
      milestone does. `CONTRIBUTING.md` at repo root is what GitHub
      actually surfaces (the "Contributing" prompt on a new issue/PR,
      the community-standards checklist) — a docs-site page alone is
      invisible to that flow. Keep it short: a few sentences plus a link
      to `docs/contributing.md` for the full detail, not a duplicate.
      Also add the link from repository-root `README.md` (a small
      addition to Phase 2's README review/update, done here since
      `CONTRIBUTING.md` doesn't exist until this phase).
- [ ] `SECURITY.md` — vulnerability reporting process.
- [ ] `CODE_OF_CONDUCT.md` — standard, uncustomized Contributor Covenant.
- [ ] GitHub issue templates: bug report, feature/roadmap proposal.
- [ ] One lightweight PR template.
- [ ] "Good first issue" candidates identified from the Cookbook recipe
      backlog (a natural first-PR shape, per ADR-0030 Amendment 1's own
      framing of Cookbook recipes).

## Phase 7: Final navigation, link, and snippet validation pass

**Status:** Not Started

A dedicated hardening phase before publication — every prior phase wrote
content against its own section; this phase verifies the whole site holds
together, not just each page in isolation.

- [ ] `mkdocs.yml` final nav pass: every "(legacy)" entry retired or
      resolved (Phase 5), nav matches `docs/documentation-architecture.md`'s
      tree exactly.
- [ ] Site-wide broken-link check (internal cross-links, per "every page
      leads somewhere").
- [ ] Code-snippet compilation check where practical (snippets drawn from
      real sample/test code, per `documentation.md`'s "prefer real
      examples" quality bar — verify they still compile against current
      `main`, not just that they did when written).
- [ ] Spelling/style pass across the full site.

## Phase 8: Clean-room public-preview acceptance test and first publication

**Status:** Not Started

**Checkpoint: Release Candidate** — every prior phase is done; this phase
either confirms the milestone is genuinely ready to publish or sends
findings back to an earlier phase before anything ships.

The milestone's actual proof point — depends on every content and
package-readiness phase above being done.

- [ ] Clean-room acceptance test: a fresh project, following only the
      public docs site and consuming only published (or local-feed, for
      pre-publish verification) packages — the five-minute Getting
      Started path, one How-to Guide task, one Cookbook recipe, one
      Package Guide's "when to install" decision, one Troubleshooting
      lookup, all followed literally as written, no ADR/internal-repo
      knowledge assumed. See "Public-preview acceptance checklist" below
      for the full list.
- [ ] Cut the first real `0.x` release: publish a GitHub Release and mark
      it published (not draft) — this is the actual "does this look
      done" gate (`publish-release.yaml`, triggered by
      `release: types: [published]`), completely independent of
      `publish-preview.yaml`'s `preview` identifier (renamed in Phase 0,
      unrelated to this step).
- [ ] Verify all four publishable packages installable from nuget.org
      post-publish (not just the local-feed pre-check); verify
      `Compono.Generators` is present inside the installed `Compono`
      package's `analyzers/dotnet/cs` and actually runs.
- [ ] Verify the documentation site is live at its public URL with the
      final nav.

## Phase 9: Final MVP documentation and closeout

**Status:** Not Started

**Checkpoint: Milestone 8 / MVP Complete** — `docs/mvp.md` reflects the
real, final outcome and every MVP success criterion has an honest
verdict.

- [ ] `docs/mvp.md`'s Milestone 8 section: outcome, links to all four
      ADRs and this plan, exit-criteria results.
- [ ] Final MVP success-criteria review (`docs/mvp.md`'s "Success
      Criteria" list) — each marked met/partially met/unmet, honestly,
      against real evidence from this milestone and Milestone 7's.
- [ ] `docs/adr/README.md`/`docs/plans/README.md` — confirm all rows
      accurate (already updated for ADR-0031/0032/0033 during this
      design pass).

## Package-readiness checklist

The concrete, executable form of
[ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md)'s
package-readiness bar — that ADR states the long-lived policy (what must
be true of a package before release), this plan owns *how* it gets
verified. Applied to all five packages — four independently published,
plus `Compono.Generators`, verified by content inspection inside
`Compono.nupkg` rather than an independent pack (it's `IsPackable=false`,
per ADR-0003). Phase 0's Tasks above are this checklist:

- [x] `publish-preview.yaml`'s `prereleaseIdentifier` renamed from
      `alpha` to `preview`.
- [x] `Directory.Packages.props`'s `PackageVersion` entries for
      `NSubstitute`/`Bogus`/`xunit.v3.extensibility.core` (the three
      third-party dependencies that flow into a publishable package's own
      `.nuspec`) declare a deliberate tested range (tested minimum,
      exclusive next-untested-major upper bound), per
      [ADR-0031 Amendment 1](../adr/0031-public-preview-release-and-versioning-policy.md#amendment-1-2026-08-04-third-party-dependencies-use-tested-ranges-not-exact-pins) —
      not a bare unbounded floor and not a blanket exact pin.
- [x] Each integration package's generated `Compono` dependency (from its
      own `<ProjectReference>`) is exact-pinned in the packed `.nuspec`,
      not left at the same bare-version minimum floor.
- [x] `PackageTags`/`PackageReleaseNotes` set once in
      `Directory.Build.props` (one uniform value for all five packages,
      not per-project) — `PackageReleaseNotes` points at the repo's
      stable releases index (`$(PackageProjectUrl)/releases`), not a
      per-version tag URL, since most published preview versions have no
      matching GitHub Release/tag to link to (see Phase 0's Tasks above).
- [x] Each of the four publishable packages has a per-package `<Title>`
      (human-friendly, distinct from the raw package ID) — not
      centralized, since it's genuinely per-package like `Description`.
- [x] `Microsoft.DotNet.PackageValidation` enabled
      (`EnablePackageValidation=true`), with no static baseline value —
      instead, a **locally-controlled CI job in this repo** (not inside
      `publish-preview.yaml`/`publish-release.yaml`, which are opaque
      `uses:` calls to the shared `devops-templates` workflow with no
      hook point for this) queries nuget.org for each package's latest
      published version and runs
      `dotnet pack -p:PackageValidationBaselineVersion=<prior-version>`
      as a pre-merge PR gate, skipped on `breaking-change`-labeled PRs.
- [x] `.github/release-drafter.yml`'s `version-resolver` remaps
      `breaking-change` from `major` to `minor` (its `categories` entry
      is unchanged) — otherwise a labeled breaking-change PR would
      silently resolve to `1.0.0`, exiting `0.x` by accident.
- [x] The same locally-controlled CI job asserts each publishable
      `.nupkg`'s file listing matches the expected per-TFM shape (lib,
      README, icon, no stray build artifacts; `analyzers/dotnet/cs` for
      `Compono` specifically, containing `Compono.Generators.dll` — this
      is `Compono.Generators`' own verification, not a separate pack of
      it).
- [x] Local-feed packed-consumer smoke test covers the four publishable
      packages together, as a standing CI gate.
- [x] `PrivateAssets`/analyzer transitivity verified for every package.
- [x] Dependency license spot-check against `Directory.Packages.props`'s
      current set (Phase 0) — the standing review-habit note for *future*
      dependency changes lives in Phase 6's `contributing.md`, not here.

## Release-readiness checklist

- [ ] `publish-preview.yaml`'s identifier renamed from `alpha` to
      `preview` (Phase 0) — the actual "does this look done" gate is the
      manually-published GitHub Release below, not this rename.
- [ ] All four publishable packages passed the locally-controlled
      `Microsoft.DotNet.PackageValidation` PR gate (Phase 0) before this
      version merged to `main` — a real baseline was resolved (not a
      first-ever/inert run silently treated as normal), or the merged PR
      legitimately carried the `breaking-change` label.
- [ ] `.github/release-drafter.yml`'s `breaking-change` label resolves to
      a minor bump, confirmed against a real labeled PR before the first
      one ships for real (Phase 0).
- [ ] Local-feed packed-consumer smoke test passes for the four
      publishable packages together (Phase 0).
- [ ] Package-contents inspection CI step passes for the four publishable
      packages, and separately confirms `Compono.Generators.dll` is
      present inside `Compono.nupkg`'s `analyzers/dotnet/cs` (Phase 0).
- [ ] Every public member has an XML doc comment — actually enforced as a
      build failure (Phase 0's new CI job runs
      `dotnet build -p:WarningsAsErrors=CS1591` for the four publishable
      packages specifically), not merely assumed from
      `GenerateDocumentationFile=true`. That existing `Directory.Build.props`
      setting deliberately leaves `CS1591` a warning, not an error, for
      normal local/CI builds (see its own comment there) — a warning
      alone doesn't fail `dotnet build`, so nothing before Phase 0 was an
      actual enforcement gate on its own. Phase 1's reference-generation
      gate is a second, tool-dependent check, not a substitute for this
      one.
- [ ] `docs/roadmap/index.md`'s compatibility framing and every affected
      Package Guide are current with the version about to publish.
- [ ] If this release includes a breaking-change-labeled PR, the
      generated release notes carry the "⚠️ Breaking Changes" section
      (release-drafter's `categories` grouping renders it automatically —
      nothing to check if no such PR is included this time; the absence
      of the section is itself the "nothing broke" signal, per ADR-0031).
- [ ] Documentation site deploys successfully from the same `main` commit
      being released (verified via `docs.yml`, not a separate manual
      check).

## Public-preview acceptance checklist

Run during Phase 8, using only public artifacts (no internal-repo
knowledge, no ADR references, no local source checkout beyond what's
needed to author the fresh test project):

- [ ] A stranger can find Compono via GitHub search or nuget.org search
      (package tags/description, Phase 0) and land on a README that
      states what it is, why it exists, and what to do next within
      seconds of scrolling.
- [ ] `dotnet add package Compono`/`Compono.XunitV3` (the common-case
      pair, per Getting Started) succeeds from a clean project against
      published nuget.org packages.
- [ ] The five-minute Getting Started path succeeds verbatim, start to
      finish, with no undocumented step.
- [ ] At least one How-to Guide task, followed literally, succeeds.
- [ ] At least one Cookbook recipe, copy-pasted, works without
      modification.
- [ ] The relevant Package Guide's "when to install" section is
      sufficient to decide whether to add `Compono.NSubstitute`/
      `Compono.Bogus` without reading any other page.
- [ ] A deliberately-triggered **compile-time** failure (e.g. an
      ambiguous-constructor type composed directly, `CMP0001`) produces a
      `CMP`-coded build error, and Troubleshooting's `common-errors.md`
      resolves it by that code.
- [ ] A deliberately-triggered **runtime** composition failure (e.g. a
      genuine construction cycle) produces a readable, path-annotated
      error and a reproducible seed via `CompositionDiagnostic`
      (`src/Compono/CompositionDiagnostic.cs` — no diagnostic-code field,
      unlike the compile-time case above), and Troubleshooting's
      `common-errors.md`/`faq.md` resolves it **by symptom**, not by
      code — these are two different failure modes with two different
      resolution paths, not one combined check.
- [ ] The API reference (`reference/api/`) answers at least one "what
      does this method do / what does it throw" question the guides
      don't already answer inline.
- [ ] Every link followed during the acceptance pass resolves (no 404s,
      no dead cross-references).
- [ ] Nothing in the acceptance pass required reading `docs/adr/`,
      `docs/plans/`, or `docs/research/` — confirming those stay
      internal engineering artifacts, not a hidden prerequisite.

## Exit criteria

Milestone 8 — and the entire MVP — is complete only when every item below
is true, checked honestly (met / partially met / unmet is an acceptable
outcome for the final MVP review in Phase 9, but every item here must be
individually resolved, not left ambiguous):

- [ ] All four publishable `0.x` packages are available on nuget.org and
      installable in a clean project; `Compono.Generators` is present and
      running inside the installed `Compono` package.
- [ ] Packed-package consumer verification passes (Phase 0's local-feed
      gate, plus Phase 8's post-publish nuget.org verification).
- [ ] The documentation site is publicly deployed and live at its stated
      URL.
- [ ] The root `README.md` accurately directs each audience (newcomer,
      AutoFixture migrator, contributor) to its right next step.
- [ ] The five-minute Getting Started flow succeeds from a clean project
      (Phase 8's acceptance test).
- [ ] All public APIs have useful XML documentation (Phase 0/1's gates).
- [ ] API reference (`reference/api/`) and diagnostics reference
      (`reference/diagnostics.md`) are published (Phase 1/3).
- [ ] Both required samples (Basic Usage, ASP.NET API) build in CI
      (Phase 4).
- [ ] Benchmark methodology and results are published, without
      unsupported comparative claims (Phase 5, per ADR-0030 Amendment 2).
- [ ] Known limitations and the `0.x` compatibility policy are explicit
      and discoverable from multiple entry points, not one obscure page
      (Phase 3, per ADR-0031).
- [ ] Contribution, security, and release guidance exist — both
      root `CONTRIBUTING.md` (linked from `README.md`) and
      `docs/contributing.md`, plus `SECURITY.md`, `CODE_OF_CONDUCT.md`,
      issue/PR templates (Phase 6).
- [ ] The AutoFixture migration guide is publication-ready (Phase 3).
- [ ] Every package and documentation link is valid (Phase 7).
- [ ] The public-preview acceptance test (above) passes end to end using
      only public-facing instructions (Phase 8).
- [ ] `docs/mvp.md`'s MVP-wide Success Criteria are reviewed one final
      time and each honestly marked met, partially met, or unmet
      (Phase 9).

## Critical Files

- Every path listed in each phase's Tasks above, under `docs/` — most
  already exist as Phase 5 (PLAN-0007) stubs; this plan replaces stub
  content with real content, not new files, **except**: `docs/index.md`
  and repository-root `README.md` (pre-existing real pages this plan
  corrects); `samples/*`'s real runnable projects (new, Phase 4);
  root `CONTRIBUTING.md`/`docs/contributing.md`/`SECURITY.md`/
  `CODE_OF_CONDUCT.md`/issue-template/PR-template content (new, Phase 6);
  `reference/api/` (new, Phase 1, generated);
  `docs/reference/diagnostics.md`/`glossary.md` content (Phase 3).
- `mkdocs.yml` — nav updated per phase as content lands; final pass in
  Phase 7. Phase 1 adds the "API Reference" sub-section under "Reference"
  (one entry per package's `index.md` landing page; the ~150 generated
  member/type pages per package are reachable through cross-links, not
  individually listed in nav).
- `.config/dotnet-tools.json` — new local tool manifest (Phase 1), pinning
  `defaultdocumentation.console` 1.2.5.
- `.github/scripts/generate-api-reference.sh` — new (Phase 1): regenerates
  `docs/reference/api/<package>/` for the four publishable packages from
  their compiled net10.0 assembly + XML doc file, core-first so the three
  integration packages' cross-package `<see cref>`s resolve locally, plus
  the `#ctor`-filename post-processing fix described in this phase's Notes.
- `.github/workflows/docs.yml` — the drift-detection gate was initially a
  separate `api-reference.yaml` workflow, deleted during PR #47 review at
  the user's direction: with no `needs`/`workflow_run` link between two
  independently-triggered workflows, a failing drift check could never
  actually stop `docs.yml` from deploying stale/incorrect content. The
  regenerate-and-diff-check steps now run as `docs.yml`'s own first real
  steps, sequentially before `mkdocs build` — the site only ever builds
  from `docs/reference/api` content already confirmed fresh in the same
  job. `docs.yml`'s trigger paths expanded to include the four publishable
  packages' `src/` paths (previously `api-reference.yaml`-only) so a
  source-only PR still runs the check.
- `.github/workflows/publish-preview.yaml` — `prereleaseIdentifier`
  renamed from `alpha` to `preview` (Phase 0). No other change to either
  publish workflow — the API-compatibility baseline check lives in a new,
  separate, locally-controlled CI job instead (below), not inside either
  `uses:`-based publish workflow.
- A new CI workflow/job (this repo's own, not `devops-templates`),
  triggered on `pull_request` including `labeled`/`unlabeled` (not just
  the default activity types) — packs the four publishable packages, runs
  the nuget.org baseline lookup and `Microsoft.DotNet.PackageValidation`
  check (skipped on `breaking-change`-labeled PRs, evaluated against the
  label state current at its own run), asserts `.nupkg` contents, and
  runs `dotnet build -p:WarningsAsErrors=CS1591` for the four publishable
  packages, as a pre-merge PR gate (Phase 0).
- `.github/release-drafter.yml` — `breaking-change` remapped from
  `major` to `minor` in `version-resolver` (Phase 0).
- `Directory.Packages.props` — `PackageVersion` entries converted to
  exact-pin bracket syntax (Phase 0).
- `src/Compono.XunitV3/Compono.XunitV3.csproj`,
  `src/Compono.NSubstitute/Compono.NSubstitute.csproj`,
  `src/Compono.Bogus/Compono.Bogus.csproj` — each package's generated
  `Compono` dependency overridden to exact-pin syntax at pack time
  (Phase 0).
- `Directory.Build.props` — package-validation and tags/release-notes
  properties added, shared across all five packages (Phase 0).
- `Compono.slnx` — both new sample projects added (Phase 4).
- `docs/documentation-architecture.md` — Open Items section already
  updated to reflect all six resolutions as part of this design pass;
  further updated in place as content lands and stub statuses flip to
  real.
- `docs/mvp.md` — Milestone 8 section (Phase 9).

## Test Plan

Documentation content itself has no automated test suite beyond the
link/snippet-validation gates in Phase 1 (API reference drift) and Phase
7 (site-wide link/snippet check). Both samples (Phase 4) get real,
buildable projects with, where practical, their own tests demonstrating
the pattern they showcase, matching `testing.md`'s bar for any real code
this plan produces. Package-readiness changes (Phase 0) are verified by
the new CI gates themselves (package validation, contents inspection,
local-feed restore) rather than a separate hand-run test plan. Phase 8's
acceptance checklist is this plan's actual end-to-end verification.

## Notes

The original PLAN-0008 draft (produced by PLAN-0007 Phase 5) was a flat,
unphased backlog, deliberately left unphased pending this design pass —
see that phase's own Notes in
[PLAN-0007](0007-milestone-7-dogfooding.md#phase-5-2026-08-03) for why.
This rewrite is that design pass: same total scope (every page, every
package-readiness item, every repository-process artifact the draft
listed), now split into ten independently-shippable phases (0-9), with
four new/amended ADRs
([ADR-0031](../adr/0031-public-preview-release-and-versioning-policy.md),
[ADR-0032](../adr/0032-api-reference-documentation-toolchain.md),
[ADR-0033](../adr/0033-public-preview-samples-strategy.md), and
[ADR-0030 Amendment 2](../adr/0030-compono-documentation-architecture.md#amendment-2-2026-08-04-resolving-milestone-8s-remaining-open-items))
settling every decision the draft's Tasks section left implicit. Anything
discovered while actually executing a phase that suggests the
*architecture* (not just this plan's task list) needs to change is
recorded here, then routed to `tasks/design.md` for an ADR-0030 Amendment
— per this plan's own Scope section above.

### Phase 0 (2026-08-04)

Mid-implementation correction: ADR-0031's original "Exact, tested
dependency pins during `0.x`" bullet applied one blanket exact-pin rule to
both the internal Compono-family lockstep dependency *and* Compono's
external third-party dependencies (Bogus, NSubstitute, xUnit). Caught
before either shipped to a real consumer — the internal case (lockstep
`Compono` pin inside each integration package's `.nuspec`) is correct and
unchanged; the external case was wrong and is corrected in
[ADR-0031 Amendment 1](../adr/0031-public-preview-release-and-versioning-policy.md#amendment-1-2026-08-04-third-party-dependencies-use-tested-ranges-not-exact-pins):
third-party dependencies that flow into a publishable package's own
`.nuspec` now get a deliberate tested range (tested minimum, exclusive
next-untested-major upper bound) instead of a blanket exact pin or an
unbounded bare-version floor. `Directory.Packages.props` and this phase's
Tasks/Package-readiness checklist above reflect the corrected policy, not
the original text.

Also caught during verification: `dotnet test`'s VSTest-style `--filter`
flag produces zero matched tests against an MTP v2 host (exit code 5,
"Zero tests ran") — MTP's actual simple-filter syntax is
`--filter-not-class`/`--filter-class`/etc., passed after a `--` separator.
The local-feed packed-consumer smoke test task above (and
`package-validation.yaml`) uses `-- --filter-not-class
"Compono.XunitV3.SampleTests.FailingCompositionTests"`, verified locally
(16/16 tests pass).

### Phase 1 (2026-08-04)

**Bake-off result: `DefaultDocumentation` (`DefaultDocumentation.Console`
1.2.5) wins**, run against a representative slice — all four publishable
packages' real net10.0 assemblies + XML doc files, not a synthetic sample —
scored against every ADR-0032 criterion:

- **`xmldocmd` (2.9.0) eliminated outright on maintenance/TFM compatibility**,
  the first criterion it failed: its own package ships host builds only for
  `net6.0`/`net7.0`, and running it (via `dotnet tool run`, any host) against
  `net10.0`-targeted assemblies throws
  `FileNotFoundException: Could not load file or assembly 'System.Runtime,
  Version=10.0.0.0...'` — a hard failure, not a degraded-output case. No
  amount of further evaluation on the other criteria was relevant once this
  failed.
- **`DefaultDocumentation` passed every other criterion** against the real
  API surface: generics (`CollectionPlanCache<T>`, `ICompositionPlan<T>`),
  overloads (`Register<T>`'s two overloads got distinct, correctly
  cross-linked pages), inheritance (`ComposableAttribute : Attribute`
  rendered with the full chain), attributes, nullable signatures
  (`Nullability` enum's doc came through verbatim), `<exception>`/`<returns>`/
  `<remarks>`/`<typeparam>` all rendered correctly (verified against
  `Composer.CreateMany<T>(int)` and `CompositionBuilder`'s real doc
  comments). Ships a `net10.0` host build already (current, not lagging the
  repo's own TFMs) and is under active release (39 published versions,
  1.2.5 current). Deterministic: two consecutive runs against the same
  input produced byte-identical output, verified both for a single package
  and for the full four-package generation run.
- **Eight real defects found and fixed during wiring, not left as accepted
  gaps** — the first two caught before the PR opened, the other six by PR
  #47's automated review (`chatgpt-codex-connector`, across four review
  passes), addressed in the same PR rather than deferred:
  1. **Cross-package `<see cref>` resolution.** Generating each package
     standalone, a `<see cref="Compono.Composer"/>` in `Compono.XunitV3`'s
     XML docs fell back to `DotnetApiFactory`, which treats any type it
     doesn't recognize as a BCL type and links to a fabricated
     `learn.microsoft.com/en-us/dotnet/api/compono.composer` URL (404).
     Fixed by generating `Compono` first with
     `--LinksOutputFilePath`/`--LinksBaseUrl`, then feeding that links file
     to each integration package's `--ExternLinksFilePaths` — verified: the
     rendered link becomes `[Composer](../Compono/Compono.Composer.md
     ...)`, a real local page.
  2. **`#` in generated filenames.** `DefaultDocumentation` names a
     parameterless constructor's page after the raw CLR metadata name
     (`Compono.ComposableAttribute.#ctor.md`) — `#` is the URL fragment
     delimiter, so a real `mkdocs build` (not just eyeballing the Markdown)
     parsed links into that filename as truncated-path-plus-fragment and
     reported it as a broken link. `.github/scripts/generate-api-reference.sh`
     renames every such file to `.ctor` post-generation and rewrites the
     handful of other generated pages that link to it, confined to
     `Compono` core in practice (the only package with a documented
     parameterless constructor).
  3. **Bogus links for internal Compono types (PR #47 review).** The same
     `DotnetApiFactory` fallback as (1) fires for any `<see cref>` on a
     *public* member's XML docs that names an *internal* Compono type (e.g.
     `CompositionBuilder`'s docs mention `CompositionConfiguration`) — no
     local page exists for it (Public-only generation) and it's never in
     `--ExternLinksFilePaths` either, so it falls to the same fabricated,
     dead `learn.microsoft.com/en-us/dotnet/api/compono.*` URL as (1). 38
     such dead links across 29 files, not the single isolated instance
     (`SeedAsNullable`) originally noticed and wrongly framed as a
     hover-only cosmetic quirk during initial verification — the scale
     only became clear from PR review's exhaustive scan. Fixed by
     post-processing every generated page: a link whose target matches
     that URL pattern is rewritten to plain inline code of its (unescaped)
     type name instead of a dead link, since there is no real page to
     point it at without publishing internal implementation types, which
     would contradict generating only the public surface in the first
     place.
  4. **Stale in-page anchor names on renamed `#ctor` pages (PR #47
     review).** Fix (2)'s filename rename corrected every *href* pointing
     at a constructor-overload page, but `DefaultDocumentation`'s own
     same-page anchor `name` values on that page are built as "the part of
     the filename after its own last `#`, `#`, member id"
     (`name='ctor.md#Compono.ComposableAttribute.ComposableAttribute()'`)
     — internally consistent only on the assumption the filename still
     contains a literal `#` acting as the real fragment delimiter. Once
     that `#` is renamed away, the anchor's stale `ctor.md#` prefix no
     longer matches the (already-correct) href fragment, so a deep link
     into a specific overload landed at the top of the page instead of
     that overload's section. Fixed by stripping the stale prefix from the
     renamed page's own anchors in the same post-processing pass.
  5. **`<paramref>`/`<typeparamref>` self-references target the wrong page
     (PR #47 second review pass).** A member's own parameter/type-parameter
     doc (e.g. a constructor's "`diagnostic` is null" exception doc, or
     `Register<T>`'s own `T` typeparam doc) always links back to its
     *containing type's* page, but the anchor lives wherever
     `OverloadsGenerator` actually placed that specific overload once a
     member has more than one (its own dedicated page, not the type's).
     Flagged on the `Compono.CompositionException` constructor case
     specifically; verified to be the general `OverloadsGenerator`
     interaction, not constructor-specific — **92 mismatched fragment
     links across all four packages** (69 in `Compono` alone, e.g.
     `Compono.CompositionBuilder.Register`'s own `T`/`factory` parameter
     docs). Fixed generally, not with another special case: the generation
     script now builds an anchor-id → actual-file map per package
     directory (from every `<a name='...'>` in that directory) and
     rewrites any same-package link whose target file doesn't actually
     contain the anchor it points at. 0 mismatches remain after the fix,
     confirmed by the same scan that found the original 92.
  6. **Bogus links for third-party dependency types (PR #47 third review
     pass).** The same `DotnetApiFactory` fallback as (3), but for types
     from `Bogus`/`NSubstitute`/`xUnit.v3` (e.g. `Bogus.Faker`,
     `NSubstitute.Substitute.For`, `Xunit.v3.IDataAttribute`) — fix (3)'s
     pattern only matched `compono.*`, so it left every non-Compono
     fallback link untouched. Generalized fix (3)'s pattern from a
     `compono.*` blocklist to a `system.*`/`microsoft.*` **allowlist** —
     the only namespaces `learn.microsoft.com/en-us/dotnet/api/` ever
     actually resolves — so it now catches every non-BCL fallback
     regardless of which package the referenced type belongs to, current
     or future.
  7. **The generalized fix (6) itself had a link-text parsing bug**,
     caught before pushing (own verification, not another review round):
     `[^\]]+` for the link-text capture group stops at the first literal
     `]`, but a signature like `NSubstitute.Substitute.For`'s array
     parameters renders its display text with escaped brackets
     (`...\[\],System\.Object\[\]\)`) — the regex silently failed to match
     at all rather than matching wrong, so the fix from (6) missed exactly
     the two links it was written to catch until this was found. Fixed by
     changing the text-group pattern to `(?:[^\]\\]|\\.)+` (an unescaped
     non-`]` character, or a backslash-escaped pair), which treats `\]` as
     a literal character rather than a false terminator.
- **CI architecture changed on user direction, mid-review**: the
  drift-detection gate was originally a separate, independently-triggered
  `api-reference.yaml` workflow with no `needs`/`workflow_run` link to
  `docs.yml` — meaning a failing drift check could never actually stop
  `docs.yml` from building and deploying stale/incorrect content, since
  the two workflows had no ordering relationship at all. Per the user's
  explicit preference (sequential dependency over two disconnected
  workflows — the local-generation-only alternative was rejected since it
  would reverse ADR-0032's explicit "CI must catch drift" Decision
  Outcome without an ADR amendment), `api-reference.yaml` is deleted and
  its regenerate-and-diff-check steps moved into `docs.yml`'s own `build`
  job, as its first real steps, sequentially before `mkdocs build`.
  `docs.yml`'s trigger paths expanded to include the four packages' `src/`
  paths so a source-only PR (no `docs/` change) still runs the check.
  8. **`mkdocs build` never actually enforced ADR-0032's broken-link
     requirement (PR #47 fourth review pass).** `docs.yml` ran `mkdocs
     build --clean` with no `--strict`, and `mkdocs.yml` doesn't enable
     strict validation either, so ADR-0032's "CI fails the build when...
     broken internal links" bullet was unenforced by anything — a warning
     never fails a plain `mkdocs build`. Enabling `--strict` surfaced
     exactly 4 pre-existing `WARNING`-level broken links (the
     `.claude/skills/`/`.agents/skills/` cross-references from ADR-0014/
     0015/0016/0022, already visible as noise in every earlier verification
     pass in this Notes section) — not a path-depth bug: `.claude/skills/`
     is outside `docs_dir` entirely, so no relative-path correction could
     ever make these resolve inside the built site. Per the user's explicit
     direction (weighed against deferring to Phase 7, which already owns a
     "site-wide broken-link check" as its own task — enabling `--strict`
     now doesn't preclude that later, more comprehensive pass), fixed both
     at once: `docs.yml` now runs `mkdocs build --clean --strict`, and the
     4 ADR cross-references were converted from a dead hyperlink to plain,
     unlinked text (`` the engineering-workflow skill's `design-decisions.md`
     reference ``) — a mechanical fix to link *syntax* only, the
     Decision/Rationale/Consequences prose itself is untouched, consistent
     with `design-decisions.md`'s own ADR-immutability rule. Verified:
     `mkdocs build --clean --strict` now exits 0 (previously aborted with
     exactly those 4 warnings).
- **One cosmetic, accepted gap**: link `title` attributes (hover tooltips)
  carry `DefaultDocumentation`'s markdown-escaped `\<`/`\>` verbatim, since
  MkDocs/python-markdown doesn't re-process escapes inside a link's title
  string — visible only on hover, never in link text or navigation, and
  consistent with ADR-0032's already-accepted "less polished... in some
  edge cases" Negative Consequence. Not fixed; recorded here rather than
  silently absorbed.
- **Verified with a real `mkdocs build`**, not just inspecting generated
  Markdown (`documentation.md`'s "do real manual verification" bar, applied
  here even though this isn't source-generator-facing — same principle,
  generated content the tests don't otherwise exercise): `uv run mkdocs
  build --clean` against the full site including all four packages'
  generated `reference/api/` content builds clean (the only `WARNING`s are
  four pre-existing, unrelated broken links to `.claude/skills/`/
  `.agents/skills/` paths from ADR pages, not touched by this phase).
- **Missing-XML-doc-comment gate**: `DefaultDocumentation` has no
  independent detection of its own (`--IncludeUndocumentedItems=False`
  just silently omits an undocumented public member from output, it
  doesn't fail). The actual enforcement is Phase 0's pre-existing
  `dotnet build -p:WarningsAsErrors=CS1591` gate in
  `package-validation.yaml`, which already blocks a missing doc comment on
  any public member before this workflow's regeneration step ever runs —
  satisfies ADR-0032's "where the tool supports it" qualifier rather than
  leaving a real gap.
- Net10.0 build output only (not net11.0) — the two TFMs share the same
  public API surface, and generating from both would either double the
  work for no additional coverage or require picking one to diff against
  anyway.
